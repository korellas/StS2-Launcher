using System;
using System.IO;
using Godot;

namespace STS2Mobile.Launcher;

public enum GraphicsPreset
{
    GameDefault,
    Quality,
    Balanced,
    Battery,
    Custom,
}

public sealed class GraphicsSettings
{
    public static string Path => System.IO.Path.Combine(OS.GetDataDir(), "game", "override.cfg");
    private const string Section = "mobile_graphics";
    private const string PacingEnabled = "window/frame_pacing/android/enable_frame_pacing";
    private const string PacingMode = "window/frame_pacing/android/swappy_mode";

    public int RenderScale = 100;
    public int Hdr;
    public int TextureFilter = -1;
    public bool DirectCardPortraits;
    public int RadialBlurSamples = 12;
    public bool ScreenDistortion = true;
    public int BackgroundParticles = 100;
    public bool ShaderWarmup = true;
    public int FramePacing = -1;

    public GraphicsPreset GetPreset()
    {
        foreach (
            var preset in new[]
            {
                GraphicsPreset.GameDefault,
                GraphicsPreset.Quality,
                GraphicsPreset.Balanced,
                GraphicsPreset.Battery,
            }
        )
        {
            var values = PresetValues(preset);
            if (
                RenderScale == values.RenderScale
                && Hdr == values.Hdr
                && TextureFilter == values.TextureFilter
                && DirectCardPortraits == values.DirectCardPortraits
                && RadialBlurSamples == values.RadialBlurSamples
                && ScreenDistortion == values.ScreenDistortion
                && BackgroundParticles == values.BackgroundParticles
            )
                return preset;
        }
        return GraphicsPreset.Custom;
    }

    public void ApplyPreset(GraphicsPreset preset) => CopyVisualsFrom(PresetValues(preset));

    public GraphicsSettings Copy() => (GraphicsSettings)MemberwiseClone();

    public void CopyVisualsFrom(GraphicsSettings values)
    {
        RenderScale = values.RenderScale;
        Hdr = values.Hdr;
        TextureFilter = values.TextureFilter;
        DirectCardPortraits = values.DirectCardPortraits;
        RadialBlurSamples = values.RadialBlurSamples;
        ScreenDistortion = values.ScreenDistortion;
        BackgroundParticles = values.BackgroundParticles;
    }

    private static GraphicsSettings PresetValues(GraphicsPreset preset) =>
        preset switch
        {
            GraphicsPreset.GameDefault => new GraphicsSettings(),
            GraphicsPreset.Quality => new GraphicsSettings { TextureFilter = 6 },
            GraphicsPreset.Balanced => new GraphicsSettings { RenderScale = 85, TextureFilter = 4 },
            GraphicsPreset.Battery => new GraphicsSettings
            {
                RenderScale = 75,
                TextureFilter = 4,
                RadialBlurSamples = 0,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(preset)),
        };

    public static GraphicsSettings Load()
    {
        var settings = new GraphicsSettings();
        try
        {
            using var config = new ConfigFile();
            if (config.Load(Path) != Error.Ok)
                return settings;
            settings.RenderScale = ReadInt(config, "render_scale", 100, 50, 75, 85, 100);
            settings.TextureFilter = ReadInt(config, "texture_filter", -1, -1, 1, 2, 4, 6);
            settings.DirectCardPortraits = ReadBool(config, "direct_card_portraits", false);
            settings.RadialBlurSamples = ReadInt(config, "radial_blur_samples", 12, 0, 12);
            settings.ShaderWarmup = ReadBool(config, "shader_warmup", true);
            settings.FramePacing = ReadInt(config, "frame_pacing", -1, -2, -1, 0, 1, 2);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Graphics] Could not load preferences: {ex.Message}");
        }
        return settings;
    }

    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            using var config = new ConfigFile();
            if (File.Exists(Path) && config.Load(Path) != Error.Ok)
                throw new IOException("Could not read existing graphics configuration");
            config.SetValue(Section, "render_scale", RenderScale);
            if (config.HasSectionKey(Section, "msaa"))
                config.EraseSectionKey(Section, "msaa");
            config.SetValue(Section, "hdr", Hdr);
            config.SetValue(Section, "texture_filter", TextureFilter);
            config.SetValue(Section, "direct_card_portraits", DirectCardPortraits);
            config.SetValue(Section, "radial_blur_samples", RadialBlurSamples);
            config.SetValue(Section, "screen_distortion", ScreenDistortion);
            config.SetValue(Section, "background_particles", BackgroundParticles);
            config.SetValue(Section, "shader_warmup", ShaderWarmup);
            config.SetValue(Section, "frame_pacing", FramePacing);
            if (FramePacing == -1)
            {
                if (config.HasSectionKey("display", PacingEnabled))
                    config.EraseSectionKey("display", PacingEnabled);
                if (config.HasSectionKey("display", PacingMode))
                    config.EraseSectionKey("display", PacingMode);
            }
            else
            {
                // Godot reads override.cfg beside --main-pack before creating Vulkan.
                config.SetValue("display", PacingEnabled, FramePacing != -2);
                config.SetValue("display", PacingMode, Math.Max(0, FramePacing));
            }
            var error = config.Save(Path);
            if (error != Error.Ok)
                throw new IOException($"Could not save graphics configuration: {error}");
            return true;
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Graphics] Could not save preferences: {ex.Message}");
            return false;
        }
    }

    private static int ReadInt(ConfigFile config, string key, int fallback, params int[] allowed)
    {
        var value = config.GetValue(Section, key, fallback);
        if (value.VariantType != Variant.Type.Int)
            return fallback;
        int result = value.AsInt32();
        return Array.IndexOf(allowed, result) >= 0 ? result : fallback;
    }

    private static bool ReadBool(ConfigFile config, string key, bool fallback)
    {
        var value = config.GetValue(Section, key, fallback);
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }
}
