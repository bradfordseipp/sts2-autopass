using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace AutoPass;

/// <summary>
/// When a card-selection prompt requires picking an exact number of cards and every
/// eligible card is functionally identical (e.g. Survivor discarding one of three
/// unenchanted Strikes), the choice is meaningless — resolve it automatically and
/// skip the selection UI, using the same early-return pattern the game itself uses
/// for forced selections (eligible count <= MinSelect).
///
/// Optional selections (MinSelect != MaxSelect, e.g. Well-Laid Plans' "retain up
/// to N") are never auto-resolved: even with identical cards, HOW MANY you pick
/// still matters.
///
/// Single-player only: selection results are synchronized between clients in
/// multiplayer and skipping the choice flow isn't worth any desync risk.
/// </summary>
public static class AutoPickIdentical
{
    public static bool TryAutoPick(Player player, CardSelectorPrefs prefs,
        System.Collections.Generic.List<CardModel> candidates,
        out System.Collections.Generic.List<CardModel> picked)
    {
        picked = null!;

        if (!AutoPassSettings.Enabled || !AutoPassSettings.AutoPickIdentical)
        {
            return false;
        }

        // Leave the test-support selector and explicit-confirmation prompts alone.
        if (CardSelectCmd.Selector != null || prefs.RequireManualConfirmation)
        {
            return false;
        }

        int count = prefs.MinSelect;
        if (count <= 0 || prefs.MaxSelect != count)
        {
            return false;
        }

        // candidates.Count <= count is the game's own forced-pick auto-path.
        if (candidates.Count <= count)
        {
            return false;
        }

        if (player.RunState == null || player.RunState.Players.Count != 1 ||
            !LocalContext.IsMe(player))
        {
            return false;
        }

        var first = candidates[0];
        for (int i = 1; i < candidates.Count; i++)
        {
            if (!AreFunctionallyIdentical(first, candidates[i]))
            {
                return false;
            }
        }

        picked = candidates.Take(count).ToList();
        AutoPassMod.Logger.Info(
            $"Auto-picked {count}x {first.Id} — all {candidates.Count} eligible cards are identical.");
        return true;
    }

    private static bool AreFunctionallyIdentical(CardModel a, CardModel b)
    {
        try
        {
            if (!a.Id.Equals(b.Id))
            {
                return false;
            }

            // Persistent state: upgrade, enchantment, per-card counters (Props).
            // FloorAddedToDeck is bookkeeping with no gameplay meaning — ignore it.
            var sa = a.ToSerializable();
            var sb = b.ToSerializable();
            sa.FloorAddedToDeck = null;
            sb.FloorAddedToDeck = null;
            if (System.Text.Json.JsonSerializer.Serialize(sa) !=
                System.Text.Json.JsonSerializer.Serialize(sb))
            {
                return false;
            }

            // Live combat state that serialization doesn't capture.
            return a.EnergyCost.GetWithModifiers(CostModifiers.All) ==
                       b.EnergyCost.GetWithModifiers(CostModifiers.All)
                && a.GetStarCostWithModifiers() == b.GetStarCostWithModifiers()
                && a.IsSlyThisTurn == b.IsSlyThisTurn
                && a.ShouldRetainThisTurn == b.ShouldRetainThisTurn
                && a.CanPlay() == b.CanPlay();
        }
        catch
        {
            // Any doubt (e.g. a card that can't serialize) → show the screen.
            return false;
        }
    }
}

[HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromHand))]
public static class FromHandAutoPickPatch
{
    public static bool Prefix(Player player, CardSelectorPrefs prefs,
        System.Func<CardModel, bool>? filter,
        ref System.Threading.Tasks.Task<System.Collections.Generic.IEnumerable<CardModel>> __result)
    {
        if (CombatManager.Instance == null || CombatManager.Instance.IsOverOrEnding)
        {
            return true;
        }

        var candidates = PileType.Hand.GetPile(player).Cards
            .Where(filter ?? (_ => true)).ToList();
        if (!AutoPickIdentical.TryAutoPick(player, prefs, candidates, out var picked))
        {
            return true;
        }

        // Mirror the game's own pre-selection step for local players.
        NPlayerHand.Instance?.CancelAllCardPlay();
        __result = System.Threading.Tasks.Task.FromResult<
            System.Collections.Generic.IEnumerable<CardModel>>(picked);
        return false;
    }
}

[HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromCombatPile),
    typeof(PlayerChoiceContext), typeof(CardPile), typeof(Player),
    typeof(CardSelectorPrefs), typeof(System.Func<CardModel, bool>))]
public static class FromCombatPileAutoPickPatch
{
    public static bool Prefix(CardPile pile, Player player, CardSelectorPrefs prefs,
        System.Func<CardModel, bool> filter,
        ref System.Threading.Tasks.Task<System.Collections.Generic.IEnumerable<CardModel>> __result)
    {
        if (CombatManager.Instance == null || CombatManager.Instance.IsEnding ||
            !pile.IsCombatPile)
        {
            return true;
        }

        var candidates = pile.Cards.Where(filter).ToList();
        if (!AutoPickIdentical.TryAutoPick(player, prefs, candidates, out var picked))
        {
            return true;
        }

        __result = System.Threading.Tasks.Task.FromResult<
            System.Collections.Generic.IEnumerable<CardModel>>(picked);
        return false;
    }
}
