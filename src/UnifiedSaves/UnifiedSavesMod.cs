using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Saves;

namespace UnifiedSaves;

/// <summary>
/// STS2 sandboxes modded play into a separate "modded/profileN" save directory.
/// The "modded/" prefix is produced in exactly one place in the game:
/// UserDataPathProvider.GetProfileDir. This mod patches it out so modded play
/// reads and writes your normal profiles — after snapshotting every save file
/// first, so a misbehaving mod can never cost you progress you can't get back.
/// </summary>
[ModInitializer(nameof(Initialize))]
public static class UnifiedSavesMod
{
    public const string ModId = "UnifiedSaves";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        // Back up before the patch below can cause a single write to the real
        // profiles. If the backup fails we still unify (the game hasn't lost
        // anything by unifying per se), but we say so loudly.
        try
        {
            SaveBackup.Run();
        }
        catch (Exception e)
        {
            Logger.Warn($"Save backup FAILED: {e}");
        }

        var harmony = new Harmony(ModId);
        harmony.PatchAll();
        Logger.Info("Save paths unified: modded play now uses your normal profiles.");
    }
}

[HarmonyPatch(typeof(UserDataPathProvider), nameof(UserDataPathProvider.GetProfileDir))]
public static class GetProfileDirPatch
{
    public static bool Prefix(int profileId, ref string __result)
    {
        __result = $"profile{profileId}";
        return false;
    }
}
