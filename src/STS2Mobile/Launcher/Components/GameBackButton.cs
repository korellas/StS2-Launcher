using Godot;

namespace STS2Mobile.Launcher.Components;

// The game's back button: its red arrow ribbon, used to leave a submenu. Falls
// back to a text entry when the game pack isn't mounted.
public class GameBackButton : Button
{
    public GameBackButton(float scale)
    {
        Flat = true;

        var ribbon = GameAssets.Load<Texture2D>(GameAssets.BackButton);
        if (ribbon != null)
        {
            CustomMinimumSize = new Vector2((int)(96 * scale), (int)(58 * scale));

            var flag = new TextureRect
            {
                Texture = ribbon,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            flag.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(flag);

            // The ribbon sprite is the flag alone; the arrow is a separate sprite
            // that the game lays over it.
            var arrow = GameAssets.Load<Texture2D>(GameAssets.BackButtonArrow);
            if (arrow != null)
            {
                var glyph = new TextureRect
                {
                    Texture = arrow,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                glyph.SetAnchorsPreset(LayoutPreset.FullRect);
                glyph.OffsetLeft = (int)(10 * scale);
                glyph.OffsetRight = -(int)(22 * scale);
                glyph.OffsetTop = (int)(12 * scale);
                glyph.OffsetBottom = -(int)(12 * scale);
                AddChild(glyph);
            }
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
