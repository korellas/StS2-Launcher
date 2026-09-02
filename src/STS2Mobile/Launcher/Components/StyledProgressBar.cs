using Godot;

namespace STS2Mobile.Launcher.Components;

public class StyledProgressBar : ProgressBar
{
    public StyledProgressBar(float scale)
    {
        // Slim and fully rounded, like the game's sliders, rather than a tall
        // square block.
        float heightPx = 12 * scale;
        CustomMinimumSize = new Vector2(0, (int)heightPx);

        AddThemeStyleboxOverride(
            "background",
            LauncherTheme.Pill(LauncherTheme.TrackFill, heightPx)
        );
        AddThemeStyleboxOverride("fill", LauncherTheme.Pill(LauncherTheme.Gold, heightPx));

        LauncherTheme.ApplyGameFont(this, 12, scale);
        AddThemeColorOverride("font_color", LauncherTheme.Cream);
    }
}
