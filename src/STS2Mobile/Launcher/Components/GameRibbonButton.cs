using Godot;

namespace STS2Mobile.Launcher.Components;

// The game's ribbon buttons — the red "no" and green "yes" that sit at the foot
// of its confirmation popups. Drawn from the game's own sprites so a launcher
// prompt is indistinguishable from one the game raises itself.
public class GameRibbonButton : Button
{
    // Natural sprite dimensions, so callers size the ribbon from the artwork's
    // own aspect instead of stretching it into whatever box a ratio produces.
    public Vector2 SpriteSize { get; } = Vector2.Zero;

    public GameRibbonButton(string text, float scale, bool confirm)
    {
        Text = text;
        // No minimum: the dialog anchors these to measured fractions of the panel.
        ClipText = true;

        var sprite = GameAssets.Load<Texture2D>(
            confirm ? GameAssets.PopupConfirmButton : GameAssets.PopupCancelButton
        );

        if (sprite != null)
        {
            SpriteSize = new Vector2(sprite.GetWidth(), sprite.GetHeight());

            // Only the horizontal margins are set. SetTextureMarginAll would also
            // fix the vertical ones, and a stylebox's texture margins act as a
            // minimum size — which is what inflated these into squares instead of
            // letting them stay the flat ribbons the game draws.
            var style = new StyleBoxTexture { Texture = sprite };
            style.TextureMarginLeft = sprite.GetWidth() / 3f;
            style.TextureMarginRight = sprite.GetWidth() / 3f;

            // The confirm sprite is blue in the atlas; the game tints it green for
            // an affirmative answer, which is the colour pairing players read.
            if (confirm)
                style.ModulateColor = new Color(0.55f, 1.15f, 0.5f);

            foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
                AddThemeStyleboxOverride(state, style);
        }
        else
        {
            foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
                AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }

        LauncherTheme.ApplyGameFont(this, 19, scale, bold: true);
        AddThemeColorOverride("font_color", Colors.White);
        AddThemeColorOverride("font_hover_color", LauncherTheme.Cream);
        AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.7f));
        AddThemeConstantOverride("shadow_offset_y", (int)(2 * scale));
    }
}
