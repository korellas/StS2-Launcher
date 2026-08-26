using Godot;

namespace STS2Mobile.Launcher.Components;

// The game's back button: its red arrow ribbon, used to leave a submenu. Falls
// back to a text entry when the game pack isn't mounted.
public class GameBackButton : Button
{
    // Sized against what the build actually renders rather than derived: the
    // arrow came out at 0.81 of the flag's height where the game draws it at
    // 0.53, so the box is scaled by that ratio and pulled back to the middle of
    // the flag's solid body. The 0.76 canvas-fill figure the previous derivation
    // assumed was wrong — the arrow fills far more of its square than that.
    private const float ArrowCanvasRatio = 0.45f;
    private const float ArrowCenterX = 0.47f;

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
                // Anchors, not offsets. The flag is drawn with KeepAspectCentered
                // and so follows the control's real rect, while an offset box is
                // fixed in pixels against a height computed here — so whenever the
                // layout gave the button a different height than assumed, the flag
                // shrank and the arrow did not. That is why lowering the ratio did
                // not visibly shrink it. Anchors track the real rect, so the two
                // stay locked together whatever size the button ends up.
                float fractionY = ArrowCanvasRatio;
                float fractionX = fractionY * ribbon.GetHeight() / ribbon.GetWidth();

                glyph.AnchorLeft = ArrowCenterX - fractionX * 0.5f;
                glyph.AnchorRight = ArrowCenterX + fractionX * 0.5f;
                glyph.AnchorTop = 0.5f - fractionY * 0.5f;
                glyph.AnchorBottom = 0.5f + fractionY * 0.5f;
                glyph.OffsetLeft = 0;
                glyph.OffsetRight = 0;
                glyph.OffsetTop = 0;
                glyph.OffsetBottom = 0;

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
