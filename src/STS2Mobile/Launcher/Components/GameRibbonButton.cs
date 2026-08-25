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
            var ribbon = new NinePatchRect
            {
                Texture = sprite,
                MouseFilter = MouseFilterEnum.Ignore,
                ShowBehindParent = true,
            };
            // The ribbon's flared ends must not stretch, so only the flat middle
            // is allowed to grow.
            int inset = (int)(sprite.GetWidth() / 3f);
            ribbon.PatchMarginLeft = inset;
            ribbon.PatchMarginRight = inset;
            ribbon.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(ribbon);
        }

        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());

        LauncherTheme.ApplyGameFont(this, 22, scale, bold: true);
        AddThemeColorOverride("font_color", Colors.White);
        AddThemeColorOverride("font_hover_color", LauncherTheme.Cream);
        AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.7f));
        AddThemeConstantOverride("shadow_offset_y", (int)(2 * scale));
    }
}
