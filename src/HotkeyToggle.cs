using Godot;

namespace AutoPass;

/// <summary>
/// F8 toggles AutoPass anywhere — including mid-fight, for the "I need to line up
/// my potion before my last card" moments. Polls the key each frame off the scene
/// tree (no custom Node subclass, so no Godot script registration needed) and
/// shows a brief overlay label as feedback.
/// </summary>
public static class HotkeyToggle
{
    private const Key ToggleKey = Key.F8;

    private static bool _wasPressed;

    public static void Install()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.ProcessFrame += Poll;
    }

    private static void Poll()
    {
        bool pressed = Input.IsKeyPressed(ToggleKey);
        if (pressed && !_wasPressed)
        {
            AutoPassSettings.Enabled = !AutoPassSettings.Enabled;
            AutoPassSettings.Save();
            AutoPassMod.Logger.Info($"Hotkey toggle: AutoPass {(AutoPassSettings.Enabled ? "ON" : "OFF")}");
            ShowOverlay(AutoPassSettings.Enabled);
        }
        _wasPressed = pressed;
    }

    private static void ShowOverlay(bool enabled)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        if (tree?.Root == null)
        {
            return;
        }

        var panel = new PanelContainer
        {
            Name = "AutoPassToggleOverlay",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        panel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        panel.OffsetTop = 60;
        panel.OffsetBottom = 60;
        panel.GrowHorizontal = Control.GrowDirection.Both;

        var label = new Label
        {
            Text = enabled ? "AutoPass: ON" : "AutoPass: OFF",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 28);
        label.AddThemeColorOverride("font_color",
            enabled ? new Color(0.56f, 0.88f, 0.56f) : new Color(0.95f, 0.6f, 0.5f));
        panel.AddChild(label);

        tree.Root.AddChild(panel);
        var timer = tree.CreateTimer(1.2);
        timer.Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(panel))
            {
                panel.QueueFree();
            }
        };
    }
}
