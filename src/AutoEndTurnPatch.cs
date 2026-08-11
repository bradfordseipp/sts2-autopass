using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace AutoPass;

/// <summary>
/// The game recomputes the End Turn button's "glow" (nothing left to play) on every
/// combat state change in StartOrStopPulseVfx. We piggyback on that: when the local
/// player has no playable card AND no usable potion AND the game is fully idle, we
/// press the End Turn button through its own public CallReleaseLogic(), which
/// re-validates CanTurnBeEnded and enqueues the same EndPlayerTurnAction the UI would.
///
/// "Fully idle" includes the game-action queue being empty and no action executing.
/// This matters for effects like Unceasing Top: the empty-hand hook that draws its
/// card runs AFTER the card-effect flag clears but INSIDE the still-executing
/// PlayCardAction — without the queue check we could end the turn over its draw.
/// Because a busy queue blocks us, we also listen to ActionQueueChanged so the
/// check re-runs the moment the queue drains.
/// </summary>
[HarmonyPatch(typeof(NEndTurnButton), "StartOrStopPulseVfx")]
public static class AutoEndTurnPatch
{
    private static bool _pending;
    private static NEndTurnButton? _button;
    private static ActionQueueSet? _subscribedQueueSet;

    public static void Postfix(NEndTurnButton __instance)
    {
        _button = __instance;
        EnsureQueueSubscription();
        TryScheduleAutoEnd();
    }

    /// Re-evaluate when the action queue drains — the last combat state change of a
    /// sequence can happen while its action is still executing, so without this we
    /// would sometimes check too early, be (correctly) blocked, and never re-check.
    private static void EnsureQueueSubscription()
    {
        var queueSet = RunManager.Instance?.ActionQueueSet;
        if (queueSet == null || ReferenceEquals(queueSet, _subscribedQueueSet))
        {
            return;
        }
        if (_subscribedQueueSet != null)
        {
            _subscribedQueueSet.ActionQueueChanged -= TryScheduleAutoEnd;
        }
        queueSet.ActionQueueChanged += TryScheduleAutoEnd;
        _subscribedQueueSet = queueSet;
    }

    private static void TryScheduleAutoEnd()
    {
        var button = _button;
        if (_pending || button == null || !AutoPassSettings.Enabled ||
            !Godot.GodotObject.IsInstanceValid(button) || !ShouldAutoEndTurn(button))
        {
            return;
        }

        // Defer one frame: we may be inside the game's event dispatch here, and
        // pressing the button mid-dispatch would re-enter it. Conditions are
        // re-checked when the deferred call runs.
        _pending = true;
        Godot.Callable.From(() =>
        {
            _pending = false;
            var b = _button;
            if (b != null && Godot.GodotObject.IsInstanceValid(b) && ShouldAutoEndTurn(b))
            {
                AutoPassMod.Logger.Info("No actions left; auto-ending turn.");
                b.CallReleaseLogic();
            }
        }).CallDeferred();
    }

    private static bool ShouldAutoEndTurn(NEndTurnButton button)
    {
        if (!AutoPassSettings.Enabled)
        {
            return false;
        }

        var combatManager = CombatManager.Instance;
        if (combatManager == null || !combatManager.IsInProgress || combatManager.IsOverOrEnding)
        {
            return false;
        }

        // The game must be fully idle: no game action executing (a PlayCardAction is
        // still executing while empty-hand hooks like Unceasing Top resolve) and
        // nothing queued (queued card plays / potions are actions-to-be).
        var runManager = RunManager.Instance;
        if (runManager == null ||
            runManager.ActionExecutor.CurrentlyRunningAction != null ||
            !runManager.ActionQueueSet.IsEmpty)
        {
            return false;
        }

        if (Traverse.Create(button).Field("_combatState").GetValue() is not CombatState combatState)
        {
            return false;
        }

        if (combatState.CurrentSide != CombatSide.Player)
        {
            return false;
        }

        Player? me;
        try
        {
            me = LocalContext.GetMe(combatState);
        }
        catch
        {
            return false;
        }

        if (me?.Creature == null || !me.Creature.IsAlive)
        {
            return false;
        }

        // During someone's extra turn, only that player may act.
        if (combatManager.PlayersTakingExtraTurn.Count != 0 &&
            !combatManager.PlayersTakingExtraTurn.Contains(me))
        {
            return false;
        }

        if (combatManager.IsPlayerReadyToEndTurn(me) ||
            combatManager.IsExecutingCardOrPotionEffect(me) ||
            combatManager.PlayerActionsDisabled)
        {
            return false;
        }

        // Only act in the Play phase: during Start/AutoPrePlay the hand is still being
        // drawn (draws are queued actions), so "no playable cards" is transiently true
        // at the start of every turn and we'd end the turn before it began.
        var playerCombatState = me.PlayerCombatState;
        if (playerCombatState == null || playerCombatState.Phase != PlayerTurnPhase.Play)
        {
            return false;
        }

        if (playerCombatState.HasCardsToPlay())
        {
            return false;
        }

        if (PotionsBlockHere(combatState) && me.Potions.Any(IsManuallyUsableInCombat))
        {
            return false;
        }

        return true;
    }

    private static bool PotionsBlockHere(CombatState combatState)
    {
        switch (AutoPassSettings.PotionMode)
        {
            case PotionBlockMode.Never:
                return false;
            case PotionBlockMode.ElitesAndBosses:
                var roomType = combatState.Encounter?.RoomType;
                return roomType == MegaCrit.Sts2.Core.Rooms.RoomType.Elite
                    || roomType == MegaCrit.Sts2.Core.Rooms.RoomType.Boss;
            default:
                return true;
        }
    }

    private static bool IsManuallyUsableInCombat(MegaCrit.Sts2.Core.Models.PotionModel potion)
    {
        if (potion.IsQueued || potion.HasBeenRemovedFromState)
        {
            return false;
        }
        if (potion.Usage != PotionUsage.CombatOnly && potion.Usage != PotionUsage.AnyTime)
        {
            return false;
        }
        return potion.PassesCustomUsabilityCheck;
    }
}
