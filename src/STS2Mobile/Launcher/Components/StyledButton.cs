using Godot;

namespace STS2Mobile.Launcher.Components;

public class StyledButton : Button
{
    public StyledButton(string text, float scale, int fontSize = 14, int height = 42)
    {
        Text = text;
        float heightPx = height * scale;
        CustomMinimumSize = new Vector2(0, (int)heightPx);
        LauncherTheme.ApplyFont(this, fontSize, scale);

        AddThemeColorOverride("font_color", LauncherTheme.Cream);
        AddThemeColorOverride("font_hover_color", LauncherTheme.Gold);
        AddThemeColorOverride("font_pressed_color", LauncherTheme.Gold);
        AddThemeColorOverride("font_disabled_color", LauncherTheme.Dim);

        int radius = (int)(6 * scale);
        AddThemeStyleboxOverride("normal", MakeFilled(LauncherTheme.ButtonFill, radius));
        AddThemeStyleboxOverride("hover", MakeFilled(LauncherTheme.ButtonHover, radius));
        AddThemeStyleboxOverride("pressed", MakeFilled(LauncherTheme.ButtonPressed, radius));
        AddThemeStyleboxOverride("disabled", MakeFilled(LauncherTheme.ButtonDisabled, radius));
        AddThemeStyleboxOverride("focus", MakeFilled(new Color(0, 0, 0, 0), radius));
    }

    public static StyleBoxFlat MakeFilled(Color bg, int cornerRadius)
    {
        var style = new StyleBoxFlat();
        style.BgColor = bg;
        style.SetCornerRadiusAll(cornerRadius);
        return style;
    }

    public static StyleBoxFlat MakeOutline(Color borderColor, int cornerRadius, int borderWidth)
    {
        var style = new StyleBoxFlat();
        style.BgColor = Colors.Transparent;
        style.BorderColor = borderColor;
        style.SetBorderWidthAll(borderWidth);
        style.SetCornerRadiusAll(cornerRadius);
        return style;
    }
}
