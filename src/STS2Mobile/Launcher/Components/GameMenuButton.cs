using System;
using Godot;
using STS2Mobile.Launcher;

namespace STS2Mobile.Launcher.Components;

// A main-menu entry: plain text, no box. Uses the game's own
// main_menu_text_button theme when the game pack is mounted, so these read as
// the same control the game's own menu uses rather than an imitation of it.
public class GameMenuButton : Button
{
    public GameMenuButton(
        string text,
        float scale,
        int fontSize = 22,
        bool primary = false,
        bool onParchment = false
    )
    {
        Text = text;
        Flat = true;
        CustomMinimumSize = new Vector2(0, (int)((primary ? 52 : 40) * scale));

        var theme = GameAssets.Load<Theme>(GameAssets.MenuButtonTheme);
        if (theme != null)
            Theme = theme;

        // The game's menu theme is keyed on Label, while this control is a Button.
        // Set Button's text properties explicitly so English does not fall back to
        // Godot's default face and every entry stays legible over the artwork.
        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());

        LauncherTheme.ApplyGameFont(this, fontSize, scale);
        AddThemeColorOverride("font_color", onParchment ? LauncherTheme.Ink : LauncherTheme.Cream);
        AddThemeColorOverride("font_hover_color", LauncherTheme.Gold);
        AddThemeColorOverride("font_pressed_color", LauncherTheme.Gold);
        AddThemeColorOverride("font_hover_pressed_color", LauncherTheme.Gold);
        AddThemeColorOverride("font_focus_color", LauncherTheme.Gold);
        AddThemeColorOverride("font_disabled_color", LauncherTheme.Dim);
        AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.88f));
        AddThemeConstantOverride("outline_size", Math.Max(1, (int)(3 * scale)));
    }
}
