using Godot;

namespace STS2Mobile.Launcher.Components;

// The game's back button: its red arrow ribbon, used to leave a submenu. Falls
// back to a text entry when the game pack isn't mounted.
public class GameBackButton : Button
{
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
                // Both sprites drawn at one scale factor and concentric — no ratio
                // or padding constants at all. They are two layers of the same
                // piece of UI art, so the artwork already encodes where the arrow
                // sits; every figure hand-fitted here so far has been a guess, and
                // the last one came from comparing the game's flag, which runs off
                // the left of its screen, against ours, which does not.
                float unit = width / ribbon.GetWidth();
                float arrowWidth = arrow.GetWidth() * unit;
                float arrowHeight = arrow.GetHeight() * unit;

                glyph.SetAnchorsPreset(LayoutPreset.TopLeft);
                glyph.OffsetLeft = (width - arrowWidth) * 0.5f;
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
