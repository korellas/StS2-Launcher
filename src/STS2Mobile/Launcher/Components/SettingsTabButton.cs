using Godot;

namespace STS2Mobile.Launcher.Components;

public class SettingsTabButton : Button
{
    private readonly TextureRect _outline;
    private readonly StyledLabel _label;

    public SettingsTabButton(string text, float scale)
    {
        CustomMinimumSize = new Vector2(128 * scale, 45 * scale);
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        var background = new TextureRect
        {
            Texture = GameAssets.Load<Texture2D>(
                "res://images/atlases/ui_atlas.sprites/settings_tab_selected.tres"
            ),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(0.9f, 0.9f, 0.9f),
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);
        _outline = new TextureRect
        {
            Texture = GameAssets.Load<Texture2D>(
                "res://images/atlases/ui_atlas.sprites/settings_tab_stroke.tres"
            ),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(0.3648f, 0.9104f, 0.96f, 0.752941f),
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
        _outline.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_outline);
        MoveChild(_outline, 0);
        _label = new StyledLabel(text, scale, 21);
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.MouseFilter = MouseFilterEnum.Ignore;
        _label.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_label);
    }

    public void SetSelected(bool selected)
    {
        _outline.Visible = selected;
        _label.Modulate = selected ? Colors.White : new Color(0.65f, 0.65f, 0.65f);
    }
}
