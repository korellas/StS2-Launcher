using Godot;
using STS2Mobile.Launcher.Components;

namespace STS2Mobile.Launcher.Sections;

public sealed class LegalInfoView : VBoxContainer
{
    public LegalInfoView(float scale)
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        var body = new RichTextLabel
        {
            Text = LegalNotices.Load(),
            FitContent = true,
            ScrollActive = false,
            SelectionEnabled = true,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, (int)(420 * scale)),
            MouseFilter = MouseFilterEnum.Pass,
        };
        body.AddThemeFontSizeOverride("normal_font_size", (int)(17 * scale));
        body.AddThemeColorOverride("default_color", LauncherTheme.Cream);
        body.AddThemeConstantOverride("line_separation", (int)(7 * scale));
        AddChild(body);
    }
}
