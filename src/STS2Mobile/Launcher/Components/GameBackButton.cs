using Godot;

namespace STS2Mobile.Launcher.Components;

// The game's back button: its red arrow ribbon, used to leave a submenu. Falls
// back to a text entry when the game pack isn't mounted.
public class GameBackButton : Button
{
    public GameBackButton(float scale)
    {
        Flat = true;

        var sprite = GameAssets.Load<Texture2D>(GameAssets.BackButton);
        if (sprite != null)
        {
            Icon = sprite;
            ExpandIcon = false;
            AddThemeConstantOverride("icon_max_width", (int)(64 * scale));
            CustomMinimumSize = new Vector2((int)(72 * scale), (int)(52 * scale));
        }
        else
        {
            Text = Localization.Tr("ACTION_CLOSE");
            LauncherTheme.ApplyGameFont(this, 20, scale);
            AddThemeColorOverride("font_color", LauncherTheme.Cream);
            CustomMinimumSize = new Vector2((int)(96 * scale), (int)(52 * scale));
        }

        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
    }
}
