using System.Text.Json;

namespace AutoPass;

public enum PotionBlockMode
{
    /// Any usable potion blocks auto-pass (most conservative).
    Always,

    /// Potions only block auto-pass in elite and boss fights — hallway fights
    /// auto-pass even while you're hoarding potions.
    ElitesAndBosses,

    /// Potions never block auto-pass. The turn ends the moment your hand is dead,
    /// so drink before playing your last card.
    Never,
}

/// <summary>
/// Live settings, persisted to user://AutoPass.settings.json.
/// </summary>
public static class AutoPassSettings
{
    public static bool Enabled = true;

    public static PotionBlockMode PotionMode = PotionBlockMode.Always;

    private record Persisted(bool Enabled, string PotionMode);

    private static string FilePath =>
        Godot.ProjectSettings.GlobalizePath("user://AutoPass.settings.json");

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return;
            }
            var p = JsonSerializer.Deserialize<Persisted>(File.ReadAllText(FilePath));
            if (p == null)
            {
                return;
            }
            Enabled = p.Enabled;
            if (Enum.TryParse(p.PotionMode, out PotionBlockMode mode))
            {
                PotionMode = mode;
            }
        }
        catch (Exception e)
        {
            AutoPassMod.Logger.Warn($"Failed to load settings, using defaults: {e.Message}");
        }
    }

    public static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(
                new Persisted(Enabled, PotionMode.ToString()),
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e)
        {
            AutoPassMod.Logger.Warn($"Failed to save settings: {e.Message}");
        }
    }
}
