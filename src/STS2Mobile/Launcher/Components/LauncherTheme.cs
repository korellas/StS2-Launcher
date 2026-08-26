using System;
using System.IO;
using Godot;
using STS2Mobile.Launcher;

namespace STS2Mobile.Launcher.Components;

// Single source of visual truth for the launcher and loading screens, styled to
// sit next to the game's own menus: full-bleed key art, translucent navy panels,
// warm cream and gold text, and rounded/pill shapes rather than square blocks.
//
// The launcher boots from a bootstrap PCK that contains only project.godot, so
// res:// has no assets. Everything here is loaded from the files GodotApp
// extracts out of the APK on startup, the same way the FMOD logo already is.
public static class LauncherTheme
{
    public static readonly Color Cream = new(0.94f, 0.89f, 0.77f);
    public static readonly Color Gold = new(0.96f, 0.76f, 0.31f);
    public static readonly Color Dim = new(0.66f, 0.64f, 0.60f);

    // Text drawn on the game's parchment submenu panel, where cream disappears.
    public static readonly Color Ink = new(0.22f, 0.15f, 0.09f);

    // The game's popup sprite is warm stone; its own prompts render it in
    // this blue-grey, and the launcher matches so the surfaces read as one set.
    public static readonly Color PanelSlate = new(0.50f, 0.68f, 0.92f);

    public static readonly Color PanelFill = new(0.05f, 0.08f, 0.15f, 0.80f);
    public static readonly Color PanelBorder = new(0.48f, 0.58f, 0.76f, 0.30f);

    public static readonly Color ButtonFill = new(0.11f, 0.17f, 0.28f, 0.92f);
    public static readonly Color ButtonHover = new(0.17f, 0.25f, 0.39f, 0.95f);
    public static readonly Color ButtonPressed = new(0.08f, 0.12f, 0.20f, 0.95f);
    public static readonly Color ButtonDisabled = new(0.12f, 0.13f, 0.16f, 0.75f);

    public static readonly Color TrackFill = new(0f, 0f, 0f, 0.55f);

    // Shown at full strength: the menu sits over the darker lower half of the
    // artwork and carries its own shadow, so dimming only made the screen muddy.
    public static readonly Color ArtTint = new(1f, 1f, 1f);

    private static FontFile _font;
    private static bool _fontTried;

    // Cinzel (SIL Open Font License), shipped in the APK. Null when unavailable,
    // in which case callers simply keep Godot's default font.
    public static FontFile Font
    {
        get
        {
            if (_fontTried)
                return _font;
            _fontTried = true;

            try
            {
                var path = Path.Combine(OS.GetDataDir(), "launcher_font.ttf");
                if (!File.Exists(path))
                {
                    PatchHelper.Log($"[Theme] font not found at {path}");
                    return null;
                }

                var font = new FontFile();
                font.LoadDynamicFont(path);
                _font = font;
            }
            catch (Exception ex)
            {
                PatchHelper.Log($"[Theme] font load failed: {ex.Message}");
            }

            return _font;
        }
    }

    public static Texture2D LoadLogo() => LoadPng("launcher_logo.png");

    public static Texture2D LoadKeyArt() => LoadPng("launcher_bg.png");

    private static Texture2D LoadPng(string fileName)
    {
        try
        {
            var path = Path.Combine(OS.GetDataDir(), fileName);
            if (!File.Exists(path))
            {
                PatchHelper.Log($"[Theme] image not found at {path}");
                return null;
            }

            var image = new Image();
            image.LoadPngFromBuffer(File.ReadAllBytes(path));
            return ImageTexture.CreateFromImage(image);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Theme] {fileName} load failed: {ex.Message}");
            return null;
        }
    }

    // Prefers the game's own Kreon face once the game pack is mounted, so the
    // launcher and the game read as one product. Korean text falls back to the
    // game's Korean face, which Kreon has no glyphs for.
    // Exposed for RichTextLabel, which reads normal_font rather than font and so
    // cannot go through ApplyGameFont.
    public static Font GameFont(bool bold = false)
    {
        var path = Localization.IsKorean
            ? GameAssets.FontKorean
            : (bold ? GameAssets.FontBold : GameAssets.FontRegular);
        return GameAssets.Load<Font>(path);
    }

    public static void ApplyGameFont(Control control, int fontSize, float scale, bool bold = false)
    {
        // Kreon carries no Hangul, so Korean has to use the face the game itself
        // switches to; otherwise every character renders as a box.
        var path = Localization.IsKorean
            ? GameAssets.FontKorean
            : (bold ? GameAssets.FontBold : GameAssets.FontRegular);
        var gameFont = GameAssets.Load<Font>(path);
        if (gameFont != null)
        {
            control.AddThemeFontOverride("font", gameFont);
            control.AddThemeFontSizeOverride("font_size", (int)(fontSize * scale));
            return;
        }

        ApplyFont(control, fontSize, scale);
    }

    public static void ApplyFont(Control control, int fontSize, float scale)
    {
        if (Font != null)
            control.AddThemeFontOverride("font", Font);
        control.AddThemeFontSizeOverride("font_size", (int)(fontSize * scale));
    }

    // Styles a ScrollContainer's vertical bar to match the panels it sits in.
    //
    // The game's own scrollbar sprites were tried first and rendered as a white
    // slab: they are atlas entries, and StyleBoxTexture ignores an AtlasTexture's
    // region. Flat boxes in the same palette are predictable, which a borrowed
    // texture was not.
    public static void ApplyGameScrollbar(ScrollContainer container, float scale)
    {
        var bar = container.GetVScrollBar();
        if (bar == null)
            return;

        float width = 10 * scale;
        bar.CustomMinimumSize = new Vector2((int)width, 0);

        var track = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.28f) };
        track.SetCornerRadiusAll((int)(width / 2f));
        bar.AddThemeStyleboxOverride("scroll", track);

        var grabber = new StyleBoxFlat { BgColor = new Color(0.78f, 0.62f, 0.34f, 0.85f) };
        grabber.SetCornerRadiusAll((int)(width / 2f));
        bar.AddThemeStyleboxOverride("grabber", grabber);

        var active = new StyleBoxFlat { BgColor = new Color(0.92f, 0.75f, 0.42f, 0.95f) };
        active.SetCornerRadiusAll((int)(width / 2f));
        bar.AddThemeStyleboxOverride("grabber_highlight", active);
        bar.AddThemeStyleboxOverride("grabber_pressed", active);
    }

    public static StyleBoxFlat Panel(float scale)
    {
        var style = new StyleBoxFlat { BgColor = PanelFill, BorderColor = PanelBorder };
        style.SetCornerRadiusAll((int)(18 * scale));
        style.SetBorderWidthAll(Math.Max(1, (int)(1 * scale)));
        style.ShadowColor = new Color(0f, 0f, 0f, 0.45f);
        style.ShadowSize = (int)(10 * scale);
        return style;
    }

    // Fully rounded ends, matching the game's pill buttons and slider tracks.
    public static StyleBoxFlat Pill(Color fill, float heightPx)
    {
        var style = new StyleBoxFlat { BgColor = fill };
        style.SetCornerRadiusAll((int)(heightPx / 2f));
        return style;
    }

    // Vertical fade used to keep text legible over the art without flattening it.
    public static Texture2D Vignette(Color top, Color bottom)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, top);
        gradient.SetColor(1, bottom);

        return new GradientTexture2D
        {
            Gradient = gradient,
            Width = 4,
            Height = 256,
            FillFrom = new Vector2(0, 0),
            FillTo = new Vector2(0, 1),
        };
    }
}
