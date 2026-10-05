using BaseLib.Config;

namespace AutoPass;

public enum PotionBlockMode
{
    /// Any usable potion blocks auto-pass (most conservative).
    Always,

    /// Potions only block auto-pass in elite and boss fights (and honorary
    /// elites like the Lantern Key guardian) — hallway fights auto-pass even
    /// while you're hoarding potions.
    ElitesAndBosses,

    /// Potions never block auto-pass. The turn ends the moment your hand is dead,
    /// so drink before playing your last card.
    Never,
}

/// <summary>
/// AutoPass settings, rendered as native controls in the game's mod-config menu
/// (via BaseLib) and persisted to user://mod_configs/AutoPass.cfg automatically.
/// Properties are static so the hot path can read them directly; BaseLib binds
/// its UI to these same statics. Labels/tooltips come from the localization file
/// packed into AutoPass.pck (keys derived from the property names).
/// </summary>
[ConfigHoverTipsByDefault]
public class AutoPassConfig : SimpleModConfig
{
    public static bool Enabled { get; set; } = true;

    public static bool AutoPickIdentical { get; set; } = true;

    public static PotionBlockMode PotionMode { get; set; } = PotionBlockMode.Always;
}
