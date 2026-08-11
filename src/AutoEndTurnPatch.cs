using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace AutoPass;

/// <summary>
/// The game recomputes the End Turn button's "glow" (nothing left to play) on every
/// combat state change in StartOrStopPulseVfx. We piggyback on that: when the local
/// player has no playable card AND no usable potion AND the game is idle, we press
/// the End Turn button through its own public CallReleaseLogic(), which re-validates
/// CanTurnBeEnded and enqueues the same EndPlayerTurnAction the UI would.
/// </summary>
[HarmonyPatch(typeof(NEndTurnButton), "StartOrStopPulseVfx")]
public static class AutoEndTurnPatch
{
    private static bool _pending;

    public static void Postfix(NEndTurnButton __instance)
    {
        if (_pending || !AutoPassSettings.Enabled || !ShouldAutoEndTurn(__instance))
        {
            return;
        }

        // Defer one frame: we're inside the game's CombatStateChanged dispatch here,
        // and pressing the button mid-dispatch would re-enter it. Conditions are
        // re-checked when the deferred call runs.
        _pending = true;
        Godot.Callable.From(() =>
        {
            _pending = false;
            if (Godot.GodotObject.IsInstanceValid(__instance) && ShouldAutoEndTurn(__instance))
            {
                AutoPassMod.Logger.Info("No actions left; auto-ending turn.");
                __instance.CallReleaseLogic();
            }
        }).CallDeferred();
    }

    private static bool ShouldAutoEndTurn(NEndTurnButton button)
    {
        var combatManager = CombatManager.Instance;
        if (combatManager == null || !combatManager.IsInProgress || combatManager.IsOverOrEnding)
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
