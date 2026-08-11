using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace AutoPass;

[ModInitializer(nameof(Initialize))]
public static class AutoPassMod
{
    public const string ModId = "AutoPass";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        AutoPassSettings.Load();
        var harmony = new Harmony(ModId);
        harmony.PatchAll();
        Logger.Info("AutoPass loaded.");
    }
}
