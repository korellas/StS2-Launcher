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
        {
            // Apply the game's theme and then leave it alone. Theme *overrides*
            // outrank the theme itself, so adding our own font and colours on top
            // silently discarded everything we mounted the game pack for.
            Theme = theme;

            // The theme is keyed on Label, so Button's own styleboxes are absent
            // from it and Godot falls back to the engine default — a grey rounded
            // rectangle that showed up as a frame around every entry. Clearing
            // them leaves the text the theme actually styles.
            foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
                AddThemeStyleboxOverride(state, new StyleBoxEmpty());

            // The theme carries colours and styleboxes but no size, so without
            // this every entry renders at Godot's default and looks like fine
            // print next to the artwork.
            AddThemeFontSizeOverride("font_size", (int)(fontSize * scale));

            // The theme's font has no Hangul; swap it out rather than render boxes.
            if (Localization.IsKorean)
            {
                var korean = GameAssets.Load<Font>(GameAssets.FontKorean);
                if (korean != null)
                    AddThemeFontOverride("font", korean);
            }
            if (onParchment)
                AddThemeColorOverride("font_color", LauncherTheme.Ink);
            return;
        }

        // Without the game pack, approximate the same shape: text only, no
        // background, so the fallback differs in typeface rather than in form.
        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());

        LauncherTheme.ApplyFont(this, fontSize, scale);
        AddThemeColorOverride("font_color", onParchment ? LauncherTheme.Ink : LauncherTheme.Cream);
        AddThemeColorOverride("font_hover_color", LauncherTheme.Gold);
        AddThemeColorOverride("font_pressed_color", LauncherTheme.Gold);
        AddThemeColorOverride("font_disabled_color", LauncherTheme.Dim);
        AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.8f));
        AddThemeConstantOverride("shadow_offset_y", (int)(2 * scale));
        AddThemeConstantOverride("shadow_outline_size", (int)(3 * scale));
    }
}
