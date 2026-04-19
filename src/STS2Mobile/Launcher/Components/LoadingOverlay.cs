using System;
using Godot;

namespace STS2Mobile.Launcher.Components;

// Minimal non-blocking loading indicator. Renders a small "Loading..." label in
// the bottom-right corner with animated dots and no background, so the game's
// own intro logo and scenes render unobstructed beneath. Parented to
// SceneTree.Root as a CanvasLayer so it survives scene transitions during game
// startup.
public class LoadingOverlay : CanvasLayer
{
    private Label _textLabel;
    private float _dotsTimer;
    private int _dotsFrame;
    private string _baseText = "Loading";
    private bool _fading;

    public static LoadingOverlay Show(SceneTree tree, string text = "Loading")
    {
        var overlay = new LoadingOverlay();
        overlay._baseText = text;
        overlay.Build(tree);
        tree.Root.AddChild(overlay);
        return overlay;
    }

    public void SetText(string text)
    {
        _baseText = text;
        if (_textLabel != null)
            _textLabel.Text = _baseText + new string('.', _dotsFrame);
    }

    // Kept for API compatibility with callers that still set a "title" phase.
    public void SetTitle(string title) => SetText(title);

    public void SetSubtitle(string subtitle)
    {
        // Intentionally no-op in the minimal corner indicator.
    }

    public void FadeOutAndFree(float duration)
    {
        if (_fading)
            return;
        _fading = true;

        var tween = CreateTween();
        tween.SetParallel(true);
        foreach (var child in GetChildren())
        {
            if (child is CanvasItem ci)
                tween
                    .TweenProperty(ci, "modulate:a", 0.0, duration)
                    .SetTrans(Tween.TransitionType.Sine);
        }
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }

    public override void _Process(double delta)
    {
        _dotsTimer += (float)delta;
        if (_dotsTimer >= 0.3f)
        {
            _dotsTimer = 0f;
            _dotsFrame = (_dotsFrame + 1) % 4;
            if (_textLabel != null)
                _textLabel.Text = _baseText + new string('.', _dotsFrame);
        }
    }

    private void Build(SceneTree tree)
    {
        Layer = 1000;

        var vpSize = tree.Root.GetVisibleRect().Size;
        if (vpSize.X <= 0 || vpSize.Y <= 0)
            vpSize = new Vector2(1920, 1080);
        var scale = Math.Max(vpSize.X, vpSize.Y) / 960f;

        var labelWidth = 220f * scale;
        var labelHeight = 28f * scale;
        var margin = 24f * scale;

        _textLabel = new Label
        {
            Text = _baseText,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Size = new Vector2(labelWidth, labelHeight),
            Position = new Vector2(
                vpSize.X - labelWidth - margin,
                vpSize.Y - labelHeight - margin
            ),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _textLabel.AddThemeFontSizeOverride("font_size", (int)(16f * scale));
        _textLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.92f, 0.85f));
        _textLabel.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.75f));
        _textLabel.AddThemeConstantOverride("shadow_offset_x", (int)(1 * scale));
        _textLabel.AddThemeConstantOverride("shadow_offset_y", (int)(1 * scale));
        _textLabel.AddThemeConstantOverride("shadow_outline_size", (int)(2 * scale));
        AddChild(_textLabel);
    }
}
