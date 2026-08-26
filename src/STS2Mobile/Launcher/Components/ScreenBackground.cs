using Godot;

namespace STS2Mobile.Launcher.Components;

// Full-bleed key art behind the launcher and the loading screen, in place of the
// flat grey fill this used to be. The solid colour stays underneath so a missing
// or unreadable image degrades to the old look instead of a blank screen.
public class ScreenBackground : ColorRect
{
    public ScreenBackground()
    {
        Color = new Color(0.04f, 0.05f, 0.09f);
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var art = LauncherTheme.LoadKeyArt();
        if (art == null)
            return;

        var texture = new TextureRect
        {
            Texture = art,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            // Cover rather than fit: phone viewports range from near-square when
            // the fold is open to very wide when closed, and letterboxing the art
            // would reintroduce the boxed-in look.
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Modulate = LauncherTheme.ArtTint,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        texture.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(texture);

        // Darkens top and bottom so status text and controls stay readable over
        // the brightest part of the art.
        var vignette = new TextureRect
        {
            Texture = LauncherTheme.Vignette(
                new Color(0f, 0f, 0f, 0.08f),
                new Color(0f, 0f, 0f, 0.45f)
            ),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        vignette.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(vignette);
    }
}
