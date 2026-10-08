using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using STS2Mobile.Launcher.Components;
using STS2Mobile.Patches;

namespace STS2Mobile.Launcher.Sections;

public class GraphicsSection : VBoxContainer
{
    public event Action<string, string> HelpRequested;
    public event Action BenchmarkRequested;
    public event Action EngineBenchmarkRequested;
    public event Action ComparisonRequested;
    private readonly List<Action> _refreshChoices = new();
    private readonly GraphicsSettings _settings;
    private readonly StyledLabel _status;
    private readonly float _scale;

    public GraphicsSection(float scale, VBoxContainer performanceControls)
    {
        _scale = scale;
        _settings = GraphicsPatches.Settings;
        AddThemeConstantOverride("separation", (int)(10 * scale));
        var description = new StyledLabel(
            Localization.Tr("GRAPHICS_HELP"),
            scale,
            14,
            HorizontalAlignment.Left
        );
        description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(description);

        AddChoices(
            "GRAPHICS_PRESET",
            () => (int)_settings.GetPreset(),
            new[]
            {
                ((int)GraphicsPreset.GameDefault, Tr("GRAPHICS_GAME_DEFAULT")),
                ((int)GraphicsPreset.Quality, Tr("GRAPHICS_PRESET_QUALITY")),
                ((int)GraphicsPreset.Balanced, Tr("GRAPHICS_PRESET_BALANCED")),
                ((int)GraphicsPreset.Battery, Tr("GRAPHICS_PRESET_BATTERY")),
            },
            value => _settings.ApplyPreset((GraphicsPreset)value)
        );
        AddChoices(
            "GRAPHICS_RESOLUTION",
            () => _settings.RenderScale,
            new[] { (100, "100%"), (85, "85%"), (75, "75%"), (50, "50%") },
            value => _settings.RenderScale = value
        );
        AddChoices(
            "GRAPHICS_MSAA",
            () => _settings.Msaa,
            new[]
            {
                (-1, Tr("GRAPHICS_GAME_DEFAULT")),
                (0, Tr("STATE_OFF")),
                (2, "2×"),
                (4, "4×"),
                (8, "8×"),
            },
            value => _settings.Msaa = value
        );
        AddChoices(
            "GRAPHICS_FILTER",
            () => _settings.TextureFilter,
            new[]
            {
                (-1, Tr("GRAPHICS_GAME_DEFAULT")),
                (1, Tr("GRAPHICS_NEAREST")),
                (2, Tr("GRAPHICS_LINEAR")),
                (4, Tr("GRAPHICS_MIPMAP")),
                (6, Tr("GRAPHICS_ANISOTROPIC")),
            },
            value => _settings.TextureFilter = value
        );
        AddChoices(
            "GRAPHICS_CARD_COMPOSITION",
            () => _settings.DirectCardPortraits ? 1 : 0,
            new[] { (0, Tr("GRAPHICS_ORIGINAL")), (1, Tr("GRAPHICS_DIRECT")) },
            value => _settings.DirectCardPortraits = value == 1
        );
        AddChoices(
            "GRAPHICS_RADIAL_BLUR",
            () => _settings.RadialBlurSamples,
            new[] { (12, Tr("GRAPHICS_ORIGINAL")), (0, Tr("STATE_OFF")) },
            value => _settings.RadialBlurSamples = value
        );
        AddChoices(
            "GRAPHICS_WARMUP",
            () => _settings.ShaderWarmup ? 1 : 0,
            new[] { (1, Tr("STATE_ON")), (0, Tr("STATE_OFF")) },
            value => _settings.ShaderWarmup = value == 1
        );
        AddChoices(
            "GRAPHICS_PACING",
            () => _settings.FramePacing,
            new[]
            {
                (-1, Tr("GRAPHICS_GAME_DEFAULT")),
                // Godot 4.5.1's Vulkan driver swaps the documented modes 0 and 2.
                (0, Tr("GRAPHICS_PACING_AUTO")),
                (1, Tr("GRAPHICS_PACING_AUTO_FPS")),
                (2, Tr("GRAPHICS_PACING_FIXED")),
                (-2, Tr("STATE_OFF")),
            },
            value => _settings.FramePacing = value
        );

        var warmup = new GameMenuButton(Tr("GRAPHICS_REBUILD_WARMUP"), scale, fontSize: 16);
        warmup.Pressed += () =>
        {
            try
            {
                File.Delete(System.IO.Path.Combine(OS.GetUserDataDir(), "shader_warmup_version"));
                _status.Text = Tr(
                    _settings.ShaderWarmup ? "GRAPHICS_WARMUP_PENDING" : "GRAPHICS_WARMUP_DISABLED"
                );
            }
            catch (Exception ex)
            {
                _status.Text = Tr("GRAPHICS_SAVE_FAILED");
                PatchHelper.Log($"[Graphics] Could not reset warmup: {ex.Message}");
            }
        };
        AddChild(warmup);
        _status = new StyledLabel("", scale, 14, HorizontalAlignment.Left);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(_status);
        AddChild(performanceControls);
        var comparison = new GameMenuButton(Tr("COMPARE_TITLE"), scale, fontSize: 16);
        comparison.Pressed += () => ComparisonRequested?.Invoke();
        AddChild(comparison);
        var engineBenchmark = new GameMenuButton(Tr("ENGINE_BENCH_TITLE"), scale, fontSize: 16);
        engineBenchmark.Pressed += () => EngineBenchmarkRequested?.Invoke();
        AddChild(engineBenchmark);
        var benchmark = new GameMenuButton(Tr("BENCH_TITLE"), scale, fontSize: 16);
        benchmark.Pressed += () => BenchmarkRequested?.Invoke();
        AddChild(benchmark);
    }

    private void AddChoices(
        string label,
        Func<int> current,
        (int Value, string Text)[] choices,
        Action<int> update
    )
    {
        var row = new SettingsRow(Tr(label), _scale, fontSize: 18);
        row.AddHelpButton(_scale, () => HelpRequested?.Invoke(label, label + "_INFO"));
        var option = new HBoxContainer();
        var previousButton = ArrowButton(true);
        var nextButton = ArrowButton(false);
        int selected = -1;
        var valueLabel = new StyledLabel("", _scale, 16)
        {
            CustomMinimumSize = new Vector2(210 * _scale, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        option.AddChild(previousButton);
        option.AddChild(valueLabel);
        option.AddChild(nextButton);
        void Refresh()
        {
            selected = Array.FindIndex(choices, choice => choice.Value == current());
            valueLabel.Text = selected >= 0 ? choices[selected].Text : Tr("GRAPHICS_PRESET_CUSTOM");
        }
        _refreshChoices.Add(Refresh);
        Refresh();
        previousButton.Pressed += () => Select(-1);
        nextButton.Pressed += () => Select(1);
        void Select(int direction)
        {
            int index =
                selected < 0
                    ? (direction > 0 ? 0 : choices.Length - 1)
                    : (selected + direction + choices.Length) % choices.Length;
            var before = _settings.Copy();
            update(choices[index].Value);
            if (!_settings.Save())
            {
                _settings.CopyVisualsFrom(before);
                _settings.ShaderWarmup = before.ShaderWarmup;
                _settings.FramePacing = before.FramePacing;
                _status.Text = Tr("GRAPHICS_SAVE_FAILED");
                return;
            }
            foreach (var refresh in _refreshChoices)
                refresh();
            if (before.Msaa != _settings.Msaa)
            {
                try
                {
                    File.Delete(
                        System.IO.Path.Combine(OS.GetUserDataDir(), "shader_warmup_version")
                    );
                }
                catch (Exception ex)
                {
                    PatchHelper.Log($"[Graphics] Could not reset warmup: {ex.Message}");
                }
            }
            _status.Text = Tr(
                GraphicsPatches.RestartRequired ? "GRAPHICS_RESTART_PENDING" : "GRAPHICS_SAVED"
            );
        }
        row.AddControl(option);
        AddChild(row);
        AddChild(SettingsRow.Separator(_scale));
    }

    private GameMenuButton ArrowButton(bool left)
    {
        var texture = GameAssets.Load<Texture2D>(
            left
                ? "res://images/atlases/ui_atlas.sprites/settings_tiny_left_arrow.tres"
                : "res://images/atlases/ui_atlas.sprites/settings_tiny_right_arrow.tres"
        );
        var button = new GameMenuButton(texture == null ? (left ? "◀" : "▶") : "", _scale)
        {
            Icon = texture,
            ExpandIcon = true,
            CustomMinimumSize = new Vector2(32 * _scale, 40 * _scale),
        };
        button.AddThemeConstantOverride("icon_max_width", (int)(22 * _scale));
        return button;
    }

    private static string Tr(string key) => Localization.Tr(key);
}
