using Godot;

namespace STS2Mobile.Launcher.Components;

// The game's ribbon buttons — the red "no" and green "yes" that sit at the foot
// of its confirmation popups. Drawn from the game's own sprites so a launcher
// prompt is indistinguishable from one the game raises itself.
public class GameRibbonButton : Button
{
    public GameRibbonButton(string text, float scale, bool confirm)
    {
        Text = text;
        Flat = true;
        CustomMinimumSize = new Vector2((int)(190 * scale), (int)(58 * scale));

        var sprite = GameAssets.Load<Texture2D>(
            confirm ? GameAssets.PopupConfirmButton : GameAssets.PopupCancelButton
        );

        if (sprite != null)
        {
            // The flared ends must not stretch, so only the flat middle grows.
            var style = new StyleBoxTexture { Texture = sprite };
            style.SetTextureMarginAll(sprite.GetWidth() / 3f);

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

        LauncherTheme.ApplyGameFont(this, 22, scale, bold: true);
        AddThemeColorOverride("font_color", Colors.White);
        AddThemeColorOverride("font_hover_color", LauncherTheme.Cream);
        AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.7f));
        AddThemeConstantOverride("shadow_offset_y", (int)(2 * scale));
    }
}
