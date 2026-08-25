using Godot;

namespace STS2Mobile.Launcher.Components;

public class StyledLabel : Label
{
    public StyledLabel(
        string text,
        float scale,
        int fontSize = 15,
        HorizontalAlignment align = HorizontalAlignment.Center,
        bool onParchment = false
    )
    {
        Text = text;
        HorizontalAlignment = align;
        LauncherTheme.ApplyGameFont(this, fontSize, scale);

        if (onParchment)
        {
            // Dark text on light paper needs no shadow, and adding one smears it.
            AddThemeColorOverride("font_color", LauncherTheme.Ink);
            return;
        }

        AddThemeColorOverride("font_color", LauncherTheme.Cream);

        // Over the artwork a shadow is what keeps text legible without a panel.
        AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.75f));
        AddThemeConstantOverride("shadow_offset_x", 0);
        AddThemeConstantOverride("shadow_offset_y", (int)(2 * scale));
        AddThemeConstantOverride("shadow_outline_size", (int)(3 * scale));
    }
}
