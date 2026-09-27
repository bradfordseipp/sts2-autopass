using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Saves;

namespace UnifiedSaves;

/// <summary>
/// STS2 sandboxes modded play into a separate "modded/profileN" save directory.
/// The "modded" prefix is produced in exactly one place in the game — this mod
/// patches it out so modded play reads and writes your normal profiles, after
/// snapshotting every save file first.
///
/// The choke point moved between game versions, so we patch whichever exists:
/// - v0.111+ (beta): GetAccountDir(bool? forceModState) — we leave explicit
///   forceModState:true callers alone in case anything deliberately addresses
///   the modded directory.
/// - v0.107 (stable): GetProfileDir(int).
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

        var getAccountDir = AccessTools.DeclaredMethod(
            typeof(UserDataPathProvider), "GetAccountDir", new[] { typeof(bool?) });
        if (getAccountDir != null)
        {
            harmony.Patch(getAccountDir,
                prefix: new HarmonyMethod(typeof(UnifiedSavesMod), nameof(GetAccountDirPrefix)));
        }
        else
        {
            var getProfileDir = AccessTools.DeclaredMethod(
                typeof(UserDataPathProvider), "GetProfileDir", new[] { typeof(int) });
            if (getProfileDir == null)
            {
                Logger.Warn("Could not find the save-path method to patch — game version " +
                    "not supported. Saves are NOT unified this session.");
                return;
            }
            harmony.Patch(getProfileDir,
                prefix: new HarmonyMethod(typeof(UnifiedSavesMod), nameof(GetProfileDirPrefix)));
        }

        Logger.Info("Save paths unified: modded play now uses your normal profiles.");
    }

    public static bool GetAccountDirPrefix(bool? forceModState, ref string __result)
    {
        if (forceModState == true)
        {
            return true;
        }
        __result = "";
        return false;
    }

    public static bool GetProfileDirPrefix(int profileId, ref string __result)
    {
        __result = $"profile{profileId}";
        return false;
    }
}
