using BaseLib.Config;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace AutoPass;

[ModInitializer(nameof(Initialize))]
public static class AutoPassMod
{
    public const string ModId = "AutoPass";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    /// The registered config instance; constructing it loads persisted values,
    /// and Save() writes them back. Used by the F8 toggle to persist on change.
    public static AutoPassConfig? Config { get; private set; }

    public static void Initialize()
    {
        Config = new AutoPassConfig();
        ModConfigRegistry.Register(ModId, Config);

        // Re-evaluate when any setting changes in the menu, so enabling AutoPass
        // while already out of actions ends the turn immediately.
        Config.ConfigChanged += (_, _) => AutoEndTurnPatch.TryScheduleAutoEnd();

        var harmony = new Harmony(ModId);
        harmony.PatchAll();
        HotkeyToggle.Install();
        Logger.Info("AutoPass loaded. F8 toggles auto-pass at any time.");
    }
}
