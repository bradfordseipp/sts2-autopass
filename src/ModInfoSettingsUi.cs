using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;

namespace AutoPass;

/// <summary>
/// Injects AutoPass's settings into the game's own Mods screen: when AutoPass is
/// the selected mod, its info panel grows an interactive settings section.
/// Controls are plain Godot UI inheriting the game's theme.
/// </summary>
[HarmonyPatch(typeof(NModInfoContainer), nameof(NModInfoContainer.Fill))]
public static class ModInfoSettingsUi
{
    private const string NodeName = "AutoPassSettingsSection";

    public static void Postfix(NModInfoContainer __instance, Mod mod)
    {
        __instance.GetNodeOrNull(NodeName)?.QueueFree();

        if (mod.manifest?.id != AutoPassMod.ModId)
        {
            return;
        }

        var box = new VBoxContainer { Name = NodeName };
        box.AddThemeConstantOverride("separation", 10);

        // Pin to the bottom of the info panel, full width, above the panel edge.
        box.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        box.OffsetLeft = 16;
        box.OffsetRight = -16;
        box.OffsetTop = -170;
        box.OffsetBottom = -24;

        var header = new Label { Text = "Settings" };
        header.AddThemeColorOverride("font_color", new Color(1f, 0.84f, 0.4f));
        box.AddChild(header);

        var enabledToggle = new CheckButton
        {
            Text = "Auto-end turn when no actions remain",
            ButtonPressed = AutoPassSettings.Enabled,
        };
        enabledToggle.Toggled += pressed =>
        {
            AutoPassSettings.Enabled = pressed;
            AutoPassSettings.Save();
        };
        box.AddChild(enabledToggle);

        var potionRow = new HBoxContainer();
        potionRow.AddThemeConstantOverride("separation", 12);
        potionRow.AddChild(new Label { Text = "Potions block auto-pass:" });

        var potionDropdown = new OptionButton();
        potionDropdown.AddItem("Always", (int)PotionBlockMode.Always);
        potionDropdown.AddItem("Elites & bosses only", (int)PotionBlockMode.ElitesAndBosses);
        potionDropdown.AddItem("Never", (int)PotionBlockMode.Never);
        potionDropdown.Selected = (int)AutoPassSettings.PotionMode;
        potionDropdown.ItemSelected += index =>
        {
            AutoPassSettings.PotionMode = (PotionBlockMode)(int)index;
            AutoPassSettings.Save();
        };
        potionRow.AddChild(potionDropdown);
        box.AddChild(potionRow);

        __instance.AddChild(box);
    }
}
