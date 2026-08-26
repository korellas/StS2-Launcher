using Godot;

namespace STS2Mobile.Launcher.Components;

// The game's back button: its red arrow ribbon, used to leave a submenu. Falls
// back to a text entry when the game pack isn't mounted.
public class GameBackButton : Button
{
    // From the game's settings screen: the arrow spans 59% of the flag's width,
    // centred just left of middle because the flag tapers to a point on the right.
    private const float ArrowWidthRatio = 0.59f;
    private const float ArrowCenterX = 0.48f;
    private const float FlagWidth = 140f;

    public GameBackButton(float scale)
    {
        Flat = true;

        var ribbon = GameAssets.Load<Texture2D>(GameAssets.BackButton);
        if (ribbon != null)
        {
            // Height follows the flag's own aspect. Fixing both figures meant any
            // change to one squashed the artwork, and the arrow — inset by four
            // hardcoded pixel values — stopped matching it.
            float width = FlagWidth * scale;
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
                // Sized from its width, not fitted into an inset box. Insetting and
                // letting KeepAspectCentered fit meant the box's height bound first
                // and shrank the arrow to well under the width the game gives it.
                float arrowWidth = width * ArrowWidthRatio;
                float arrowHeight = arrowWidth * arrow.GetHeight() / arrow.GetWidth();
                glyph.SetAnchorsPreset(LayoutPreset.TopLeft);
                glyph.OffsetLeft = width * ArrowCenterX - arrowWidth * 0.5f;
                glyph.OffsetTop = (height - arrowHeight) * 0.5f;
                glyph.OffsetRight = glyph.OffsetLeft + arrowWidth;
                glyph.OffsetBottom = glyph.OffsetTop + arrowHeight;
                AddChild(glyph);
            }
        }
        else
        {
            Text = Localization.Tr("ACTION_CLOSE");
            LauncherTheme.ApplyGameFont(this, 20, scale);
            AddThemeColorOverride("font_color", LauncherTheme.Cream);
            CustomMinimumSize = new Vector2((int)(FlagWidth * scale), (int)(76 * scale));
        }

        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
    }
}
