using Godot;

namespace STS2Mobile.Launcher.Components;

// The game's back button: its red arrow ribbon, used to leave a submenu. Falls
// back to a text entry when the game pack isn't mounted.
public class GameBackButton : Button
{
    // In the game's settings screen the arrow stands a little over half the flag's
    // height. Heights are the one measurement that screenshot could give honestly:
    // the flag runs off the left of the screen, so every width in it is clipped,
    // and the two ratios taken from those widths both blew the arrow up.
    private const float ArrowHeightRatio = 0.53f;
    private const float ArrowCenterX = 0.44f;

    // The game's flag stands 150px tall on a 1510px-tall screen; this is the width
    // that puts ours at the same height once the sprite's aspect is applied.
    private const float FlagWidth = 175f;

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
                // Fitted into a box rather than sized outright. KeepAspectCentered
                // scales the sprite down to whatever box it is given and never up,
                // so the arrow cannot escape the flag however wrong the box is —
                // the property both of the last two attempts gave away by
                // computing the arrow's size themselves.
                float glyphHeight = height * ArrowHeightRatio;
                float glyphWidth = width * 0.72f;

                glyph.SetAnchorsPreset(LayoutPreset.TopLeft);
                glyph.OffsetLeft = width * ArrowCenterX - glyphWidth * 0.5f;
                glyph.OffsetTop = (height - glyphHeight) * 0.5f;
                glyph.OffsetRight = glyph.OffsetLeft + glyphWidth;
                glyph.OffsetBottom = glyph.OffsetTop + glyphHeight;

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
