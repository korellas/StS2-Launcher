using System;
using System.Collections.Generic;
using Godot;
using STS2Mobile.Launcher.Components;
using STS2Mobile.Patches;

namespace STS2Mobile.Launcher;

// Native nodes keep this entry independent of GameStartup and game service singletons.
public sealed class RenderBenchmarkFixture
{
    public const string StrikePath =
        "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";
    public const string DefendPath =
        "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";
    public const string FramePath =
        "res://images/atlases/ui_atlas.sprites/card/card_frame_attack_s.tres";
    public const string BlurPath = "res://shaders/radial_blur.gdshader";
    public const string DistortionPath =
        "res://shaders/vfx/distortion/vfx_screen_distortion_outward_shader.gdshader";
    private readonly Texture2D _strike;
    private readonly Texture2D _defend;
    private readonly Texture2D _frame;
    private readonly Shader _blur;
    private readonly Shader _distortion;
    private readonly List<Node2D> _moving = new();
    private Node2D _scene;

    public RenderBenchmarkFixture()
    {
        _strike = Required<Texture2D>(StrikePath);
        _defend = Required<Texture2D>(DefendPath);
        _frame = Required<Texture2D>(FramePath);
        _blur = Required<Shader>(BlurPath);
        _distortion = Required<Shader>(DistortionPath);
    }

    private static T Required<T>(string path)
        where T : Resource =>
        GameAssets.Load<T>(path)
        ?? throw new InvalidOperationException($"Missing benchmark asset: {path}");

    public void Build(SubViewport viewport, Vector2I physicalSize, RenderBenchmarkCase test)
    {
        _scene?.Free();
        _moving.Clear();
        viewport.Size = new Vector2I(
            Math.Max(1, physicalSize.X * test.Scale / 100),
            Math.Max(1, physicalSize.Y * test.Scale / 100)
        );
        viewport.CanvasTransform = new Transform2D(
            0,
            new Vector2(viewport.Size.X / 1920f, viewport.Size.Y / 1080f),
            0,
            Vector2.Zero
        );
        viewport.UseHdr2D = test.Hdr;
        viewport.Msaa2D = GraphicsPatches.GetMsaa(test.Msaa);
        viewport.CanvasItemDefaultTextureFilter = test.Filter switch
        {
            1 => Viewport.DefaultCanvasItemTextureFilter.Nearest,
            4 or 6 => Viewport.DefaultCanvasItemTextureFilter.LinearWithMipmaps,
            _ => Viewport.DefaultCanvasItemTextureFilter.Linear,
        };
        if (test.Filter == 6)
            RenderingServer.ViewportSetDefaultCanvasItemTextureFilter(
                viewport.GetViewportRid(),
                RenderingServer.CanvasItemTextureFilter.LinearWithMipmapsAnisotropic
            );
        _scene = new Node2D();
        viewport.AddChild(_scene);
        _scene.AddChild(
            new ColorRect
            {
                Size = new Vector2(1920, 1080),
                Color = new Color(.055f, .065f, .1f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            }
        );
        if (test.Scene != "Geometry")
            AddCards(test.Scene == "Cards" ? 24 : 12, test.Direct);
        if (test.Scene != "Cards")
            AddGeometry(test.Scene == "Geometry" ? 64 : 16);
        if (test.Scene == "Effects")
            AddEffects(test);
    }

    private void AddCards(int count, bool direct)
    {
        for (int i = 0; i < count; i++)
        {
            var card = new Node2D
            {
                Position = new Vector2(190 + i % 8 * 220, 195 + i / 8 * 310),
                Scale = Vector2.One * (.6f + i % 3 * .14f),
            };
            _scene.AddChild(card);
            var portrait = new CanvasGroup { FitMargin = 1, ClearMargin = 1 };
            card.AddChild(portrait);
            RenderingServer.CanvasItemSetCanvasGroupMode(
                portrait.GetCanvasItem(),
                direct
                    ? RenderingServer.CanvasGroupMode.Disabled
                    : RenderingServer.CanvasGroupMode.Transparent,
                1,
                true,
                1,
                false
            );
            portrait.AddChild(
                new TextureRect
                {
                    Texture = i % 2 == 0 ? _strike : _defend,
                    Position = new Vector2(-125, -145),
                    Size = new Vector2(250, 190),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                }
            );
            card.AddChild(
                new TextureRect
                {
                    Texture = _frame,
                    Position = new Vector2(-145, -190),
                    Size = new Vector2(290, 380),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                }
            );
            _moving.Add(card);
        }
    }

    private void AddGeometry(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var polygon = new Polygon2D
            {
                Position = new Vector2(115 + i % 8 * 240, 65 + i / 8 * 135),
                Polygon = new[]
                {
                    new Vector2(-80, -3),
                    new Vector2(0, -58),
                    new Vector2(80, 3),
                    new Vector2(0, 58),
                },
                Color = new Color(i % 3 == 0 ? 1.8f : .4f, .5f, .85f, .75f),
                Antialiased = false,
            };
            _scene.AddChild(polygon);
            _moving.Add(polygon);
        }
    }

    private void AddEffects(RenderBenchmarkCase test)
    {
        if (test.Particles > 0)
        {
            var material = new ParticleProcessMaterial
            {
                ParticleFlagDisableZ = true,
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
                EmissionBoxExtents = new Vector3(960, 540, 0),
                Gravity = new Vector3(0, 20, 0),
                Direction = new Vector3(1, 0, 0),
                InitialVelocityMin = 15,
                InitialVelocityMax = 35,
                ScaleMin = .035f,
                ScaleMax = .06f,
                Color = new Color(.4f, .8f, 1.6f, .4f),
            };
            _scene.AddChild(
                new GpuParticles2D
                {
                    Amount = 512 * test.Particles / 100,
                    Lifetime = 4,
                    Preprocess = 4,
                    UseFixedSeed = true,
                    Seed = 65537,
                    FixedFps = 60,
                    Position = new Vector2(960, 540),
                    VisibilityRect = new Rect2(-1100, -650, 2200, 1300),
                    Texture = _defend,
                    ProcessMaterial = material,
                    Emitting = true,
                }
            );
        }
        if (test.Distortion)
        {
            // Reset screen-reading boundaries explicitly so each effect sees the preceding pass.
            _scene.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
            var material = new ShaderMaterial { Shader = _distortion };
            material.SetShaderParameter("distortion_base_intensity", .035f);
            _scene.AddChild(
                new TextureRect
                {
                    Texture = _strike,
                    Material = material,
                    Size = new Vector2(1920, 1080),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    Modulate = new Color(1, 1, 1, .6f),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                }
            );
        }
        if (test.Blur > 0)
        {
            _scene.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
            var material = new ShaderMaterial { Shader = _blur };
            material.SetShaderParameter("sampling_count", test.Blur);
            material.SetShaderParameter("blur_power", .008f);
            _scene.AddChild(
                new ColorRect
                {
                    Size = new Vector2(1920, 1080),
                    Material = material,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                }
            );
        }
    }

    public void Animate(double seconds)
    {
        float phase = (float)(seconds * Math.Tau / 5);
        for (int i = 0; i < _moving.Count; i++)
            _moving[i].Rotation = .18f * MathF.Sin(phase + i * .37f);
    }
}
