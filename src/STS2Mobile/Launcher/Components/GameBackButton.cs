using Godot;

namespace STS2Mobile.Launcher.Components;

// The game's back button: its red arrow ribbon, used to leave a submenu. Falls
// back to a text entry when the game pack isn't mounted.
public class GameBackButton : Button
{
    // The device reports flag=300x202 and arrow=180x180. The arrow's canvas is
    // square while the arrow drawn on it is wide and short, so roughly a quarter
    // of that canvas is transparent above and below — which is why every attempt
    // that treated the canvas as the arrow came out wrong.
    //
    // In the game the visible arrow stands 0.53 of the flag's height, and fills
    // about 0.76 of its own canvas, so the canvas belongs on screen at 0.53/0.76
    // of the flag's height. Being square, that is 0.70 * 202/300 = 0.47 of the
    // flag's width — which is what the reference measures, arrived at without
    // using any width from it.
    private const float ArrowCanvasRatio = 0.70f;
    private const float ArrowCenterX = 0.55f;

    // Puts the flag at the height the game draws it once the sprite's aspect is
    // applied.
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
                // A square box, because the sprite is square: the arrow then lands
                // wherever the artwork puts it inside that square, rather than
                // wherever a guess at its padding puts it. Fitted, not sized, so
                // it cannot escape the flag if the box is still wrong.
                float box = height * ArrowCanvasRatio;

                glyph.SetAnchorsPreset(LayoutPreset.TopLeft);
                glyph.OffsetLeft = width * ArrowCenterX - box * 0.5f;
                glyph.OffsetTop = (height - box) * 0.5f;
                glyph.OffsetRight = glyph.OffsetLeft + box;
                glyph.OffsetBottom = glyph.OffsetTop + box;

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
