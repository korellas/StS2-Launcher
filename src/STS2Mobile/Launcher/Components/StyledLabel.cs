using Godot;

namespace STS2Mobile.Launcher.Components;

public class StyledLabel : Label
{
    public StyledLabel(
        string text,
        float scale,
        int fontSize = 15,
        HorizontalAlignment align = HorizontalAlignment.Center
    )
    {
        Text = text;
        HorizontalAlignment = align;
        LauncherTheme.ApplyFont(this, fontSize, scale);
        AddThemeColorOverride("font_color", LauncherTheme.Cream);

        // The art behind this is busy; a shadow keeps the text legible without
        // needing a panel behind every label.
        AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.75f));
        AddThemeConstantOverride("shadow_offset_x", 0);
        AddThemeConstantOverride("shadow_offset_y", (int)(2 * scale));
        AddThemeConstantOverride("shadow_outline_size", (int)(3 * scale));
    }
}
