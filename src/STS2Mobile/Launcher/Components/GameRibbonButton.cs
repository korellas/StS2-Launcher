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

    private readonly Font _labelFont;
    private readonly int _labelSize;

    // What the label actually needs. The ribbon used to be sized purely from a
    // fraction of the panel, so a three-syllable word like "아니요" did not fit
    // and ClipText silently rendered it as "아".
    public float MeasuredTextWidth =>
        _labelFont?.GetStringSize(Text, HorizontalAlignment.Left, -1, _labelSize).X ?? 0f;

    public GameRibbonButton(string text, float scale, bool confirm)
    {
        Text = text;
        // Not clipped: the dialog sizes these to fit the label, and an overflow
        // that is visible can be reported, where a truncation cannot.
        ClipText = false;

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

        _labelSize = (int)(19 * scale);
        _labelFont = LauncherTheme.GameFont(bold: true);
        LauncherTheme.ApplyGameFont(this, 19, scale, bold: true);
        AddThemeColorOverride("font_color", Colors.White);
        AddThemeColorOverride("font_hover_color", LauncherTheme.Cream);
        AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.7f));
        AddThemeConstantOverride("shadow_offset_y", (int)(2 * scale));
    }
}
