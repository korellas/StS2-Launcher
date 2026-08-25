using Godot;

namespace STS2Mobile.Launcher.Components;

// A settings row drawn as the game draws them: the game's own checkbox sprite on
// the left, label text beside it, the whole row tappable. Falls back to a plain
// "Label: ON/OFF" entry when the game pack isn't mounted.
public class GameCheckRow : Button
{
    private readonly TextureRect _box;
    private readonly Label _label;
    private readonly Texture2D _ticked;
    private readonly Texture2D _unticked;
    private readonly string _caption;

    public GameCheckRow(string caption, float scale, int fontSize = 22)
    {
        _caption = caption;
        ToggleMode = true;
        Flat = true;
        CustomMinimumSize = new Vector2(0, (int)(46 * scale));

        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());

        _ticked = GameAssets.Load<Texture2D>(GameAssets.CheckboxTicked);
        _unticked = GameAssets.Load<Texture2D>(GameAssets.CheckboxUnticked);

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.AddThemeConstantOverride("separation", (int)(14 * scale));
        AddChild(row);

        if (_ticked != null || _unticked != null)
        {
            _box = new TextureRect
            {
                CustomMinimumSize = new Vector2((int)(30 * scale), (int)(30 * scale)),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            row.AddChild(_box);
        }

        _label = new Label
        {
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        LauncherTheme.ApplyGameFont(_label, fontSize, scale);
        _label.AddThemeColorOverride("font_color", LauncherTheme.Ink);
        row.AddChild(_label);

        Toggled += _ => Refresh();
        Refresh();
    }

    private void Refresh()
    {
        if (_box != null)
            _box.Texture = ButtonPressed ? _ticked : _unticked;

        // Without the sprites there is no visual state, so the text has to carry it.
        _label.Text = _box != null ? _caption : $"{_caption}: {(ButtonPressed ? "ON" : "OFF")}";
    }
}
