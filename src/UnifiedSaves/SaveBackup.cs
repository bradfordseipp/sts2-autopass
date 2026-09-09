namespace UnifiedSaves;

/// <summary>
/// Snapshots the game's save data into "backups/auto-yyyyMMdd-HHmmss/" inside the
/// game's user-data directory, keeping the newest 10. Runs at mod init, which is
/// before the game loads or writes any profile data.
/// </summary>
public static class SaveBackup
{
    private const int SnapshotsToKeep = 10;

    /// Top-level entries that are not save data.
    private static readonly string[] SkipDirs = { "backups", "logs", "shader_cache", "vulkan" };

    public static void Run()
    {
        string root = Godot.OS.GetUserDataDir();
        if (!Directory.Exists(root))
        {
            UnifiedSavesMod.Logger.Warn($"User data dir not found: {root}");
            return;
        }

        string backupsDir = Path.Combine(root, "backups");
        Directory.CreateDirectory(backupsDir);

        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string finalDir = Path.Combine(backupsDir, $"auto-{stamp}");
        if (Directory.Exists(finalDir))
        {
            return;
        }

        // Copy into a ".partial" dir first so an interrupted backup is never
        // mistaken for a good one.
        string stagingDir = finalDir + ".partial";
        if (Directory.Exists(stagingDir))
        {
            Directory.Delete(stagingDir, recursive: true);
        }

        int files = 0;
        foreach (string entry in Directory.EnumerateFileSystemEntries(root))
        {
            string name = Path.GetFileName(entry);
            if (SkipDirs.Contains(name, StringComparer.OrdinalIgnoreCase) || name.EndsWith(".partial"))
            {
                continue;
            }
            files += CopyRecursive(entry, Path.Combine(stagingDir, name));
        }

        Directory.Move(stagingDir, finalDir);
        UnifiedSavesMod.Logger.Info($"Backed up {files} save files to {finalDir}");

        Rotate(backupsDir);
    }

    private static int CopyRecursive(string source, string dest)
    {
        if (File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(source, dest);
            return 1;
        }

        int copied = 0;
        foreach (string entry in Directory.EnumerateFileSystemEntries(source))
        {
            copied += CopyRecursive(entry, Path.Combine(dest, Path.GetFileName(entry)));
        }
        return copied;
    }

    private static void Rotate(string backupsDir)
    {
        var old = Directory.GetDirectories(backupsDir, "auto-*")
            .Where(d => !d.EndsWith(".partial"))
            .OrderByDescending(Path.GetFileName)
            .Skip(SnapshotsToKeep)
            .ToList();
        foreach (string dir in old)
        {
            Directory.Delete(dir, recursive: true);
        }
        if (old.Count > 0)
        {
            UnifiedSavesMod.Logger.Info($"Pruned {old.Count} old save backup(s).");
        }
    }
}
