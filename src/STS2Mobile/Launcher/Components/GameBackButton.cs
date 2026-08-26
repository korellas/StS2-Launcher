using Godot;

namespace STS2Mobile.Launcher.Components;

// The game's back button: its red arrow ribbon, used to leave a submenu. Falls
// back to a text entry when the game pack isn't mounted.
public class GameBackButton : Button
{
    private const float ArrowPadLeft = 0.12f;
    private const float ArrowPadRight = 0.26f;
    private const float ArrowPadVertical = 0.22f;

    public GameBackButton(float scale)
    {
        Flat = true;

        var ribbon = GameAssets.Load<Texture2D>(GameAssets.BackButton);
        if (ribbon != null)
        {
            // Height follows the flag's own aspect. Fixing both figures meant any
            // change to one squashed the artwork, and the arrow — inset by four
            // hardcoded pixel values — stopped matching it.
            float width = 96 * scale;
            float height = width * ribbon.GetHeight() / ribbon.GetWidth();
            CustomMinimumSize = new Vector2((int)width, (int)height);

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
                // The flag tapers to a point on the right, so the arrow sits in the
                // solid part: padded more on that side. As fractions of the button
                // these hold whatever size it ends up.
                glyph.OffsetLeft = width * ArrowPadLeft;
                glyph.OffsetRight = -width * ArrowPadRight;
                glyph.OffsetTop = height * ArrowPadVertical;
                glyph.OffsetBottom = -height * ArrowPadVertical;
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
