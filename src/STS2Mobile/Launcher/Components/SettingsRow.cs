using Godot;

namespace STS2Mobile.Launcher.Components;

// One line of the settings panel: name on the left, controls on the right, the
// arrangement the game's own settings screen uses.
public class SettingsRow : HBoxContainer
{
    public SettingsRow(string label, float scale, int fontSize = 21)
    {
        CustomMinimumSize = new Vector2(0, (int)(52 * scale));
        AddThemeConstantOverride("separation", (int)(16 * scale));

        var caption = new StyledLabel(label, scale, fontSize, HorizontalAlignment.Left);
        caption.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        caption.VerticalAlignment = VerticalAlignment.Center;
        AddChild(caption);
    }

    public void AddControl(Control control)
    {
        control.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        AddChild(control);
    }

    public static ColorRect Separator(float scale) =>
        new()
        {
            Color = new Color(1f, 1f, 1f, 0.10f),
            CustomMinimumSize = new Vector2(0, 1),
            MouseFilter = MouseFilterEnum.Ignore,
        };
}
