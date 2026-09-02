using System;
using Godot;

namespace STS2Mobile.Launcher.Components;

public class StyledPanel : CenterContainer
{
    public VBoxContainer Content { get; }

    public StyledPanel(float scale, float widthRatio = 0.7f, bool transparent = false)
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var vpSize = new Vector2(1920, 1080); // fallback, overridden after AddChild
        var panelContainer = new PanelContainer();
        panelContainer.CustomMinimumSize = new Vector2(vpSize.X * widthRatio, 0);

        // The game never shows a screen-filling box: its menus put content
        // straight onto the artwork. A full-bleed panel here hid the background
        // entirely and read as a dialog with no edges.
        var style = transparent
            ? new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) }
            : LauncherTheme.Panel(scale);
        style.ContentMarginLeft = S(scale, 28);
        style.ContentMarginRight = S(scale, 28);
        style.ContentMarginTop = S(scale, 24);
        style.ContentMarginBottom = S(scale, 24);
        panelContainer.AddThemeStyleboxOverride("panel", style);
        AddChild(panelContainer);

        Content = new VBoxContainer();
        Content.SizeFlagsVertical = SizeFlags.ExpandFill;
        Content.AddThemeConstantOverride("separation", S(scale, 10));
        panelContainer.AddChild(Content);

        // Defer viewport-based sizing until in tree
        _panelContainer = panelContainer;
        _widthRatio = widthRatio;
    }

    public PanelContainer Panel => _panelContainer;
    private readonly PanelContainer _panelContainer;
    private readonly float _widthRatio;

    // No absolute caps — phones routinely have viewports far above 1400×800
    // in landscape (Galaxy S24 ≈ 2316×1080, tablets and foldables larger
    // still), and capping makes the panel sit in a 4:3 island with dead
    // space around it. Use 95%×95% of whatever the viewport actually is.
    private const float HeightRatio = 0.95f;

    public void UpdateSizeFromViewport(Vector2 vpSize)
    {
        var w = vpSize.X * _widthRatio;
        var h = vpSize.Y * HeightRatio;
        _panelContainer.CustomMinimumSize = new Vector2(w, h);
    }

    private static int S(float scale, int v) => (int)(v * scale);
}
