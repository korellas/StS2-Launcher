using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Saves;
using STS2Mobile.Launcher;

namespace STS2Mobile.Patches;

public static class GraphicsPatches
{
    private static int _startupFramePacing;
    private static GraphicsRuntime _runtime;
    public static GraphicsSettings Settings { get; private set; }
    public static bool RestartRequired =>
        Settings != null && Settings.FramePacing != _startupFramePacing;

    public static void Initialize()
    {
        if (Settings != null)
            return;
        Settings = GraphicsSettings.Load();
        _startupFramePacing = Settings.FramePacing;
    }

    public static void Apply(Harmony harmony)
    {
        var assembly = typeof(NGame).Assembly;
        PatchHelper.Patch(
            harmony,
            typeof(NGame),
            "InitializeGraphicsPreferences",
            postfix: PatchHelper.Method(typeof(GraphicsPatches), nameof(GraphicsPreferencesPostfix))
        );
        var msaaType = assembly.GetType("MegaCrit.Sts2.Core.Nodes.Screens.Settings.NMsaaPaginator");
        if (msaaType != null)
            PatchHelper.Patch(
                harmony,
                msaaType,
                "OnIndexChanged",
                postfix: PatchHelper.Method(
                    typeof(GraphicsPatches),
                    nameof(GraphicsPreferencesPostfix)
                )
            );
        var cardType = assembly.GetType("MegaCrit.Sts2.Core.Nodes.Cards.NCard");
        if (cardType != null)
            PatchHelper.Patch(
                harmony,
                cardType,
                "Reload",
                postfix: PatchHelper.Method(typeof(GraphicsPatches), nameof(CardReloadPostfix))
            );
        var blurType = assembly.GetType("MegaCrit.Sts2.Core.Nodes.Vfx.NRadialBlurVfx");
        if (blurType != null)
            PatchHelper.Patch(
                harmony,
                blurType,
                "Activate",
                prefix: PatchHelper.Method(typeof(GraphicsPatches), nameof(RadialBlurPrefix))
            );
    }

    public static void StartRuntime(SceneTree tree)
    {
        Initialize();
        if (_runtime != null && GodotObject.IsInstanceValid(_runtime))
            return;
        _runtime = new GraphicsRuntime();
        tree.Root.AddChild(_runtime);
        _runtime.Initialize();
    }

    public static void GraphicsPreferencesPostfix()
    {
        _runtime?.ApplyViewportSettings();
    }

    public static void CardReloadPostfix(object __instance)
    {
        if (Settings == null || __instance is not Node card)
            return;
        var group = card.GetNodeOrNull<CanvasGroup>("%PortraitCanvasGroup");
        if (group == null)
            return;
        // Locked and Ancient portraits require their blur/mask material. Reload
        // also restores the group mode when a pooled card changes visibility.
        var mode =
            Settings.DirectCardPortraits && group.Material == null
                ? RenderingServer.CanvasGroupMode.Disabled
                : RenderingServer.CanvasGroupMode.Transparent;
        RenderingServer.CanvasItemSetCanvasGroupMode(
            group.GetCanvasItem(),
            mode,
            group.ClearMargin,
            true,
            group.FitMargin,
            group.UseMipmaps
        );
    }

    public static bool RadialBlurPrefix(object __instance)
    {
        if (Settings == null)
            return true;
        if (Settings.RadialBlurSamples == 0)
            return false;
        if (
            __instance is Node node
            && node.GetNodeOrNull<CanvasItem>("Rect")?.Material is ShaderMaterial material
        )
            material.SetShaderParameter("sampling_count", Settings.RadialBlurSamples);
        return true;
    }

    public static Viewport.Msaa GetMsaa(int value) =>
        value switch
        {
            2 => Viewport.Msaa.Msaa2X,
            4 => Viewport.Msaa.Msaa4X,
            8 => Viewport.Msaa.Msaa8X,
            _ => Viewport.Msaa.Disabled,
        };

    public static void ConfigureWarmupViewport(SubViewport viewport)
    {
        int msaa = Settings.Msaa < 0 ? SaveManager.Instance.SettingsSave.Msaa : Settings.Msaa;
        viewport.Msaa2D = GetMsaa(msaa);
        viewport.UseHdr2D = _runtime?.GetTree().Root.UseHdr2D ?? false;
    }
}

public class GraphicsRuntime : Node
{
    private Window _window;
    private bool _resizeQueued;
    private bool _defaultHdr;
    private Viewport.DefaultCanvasItemTextureFilter _defaultFilter;
    private const string DistortionShader =
        "res://shaders/vfx/distortion/vfx_screen_distortion_outward_shader.gdshader";
    private const string ScreamDistortionShader =
        "res://shaders/vfx/scream/vfx_scream_distortion_polar_shader.gdshader";

    public void Initialize()
    {
        _window = GetTree().Root;
        _defaultHdr = _window.UseHdr2D;
        _defaultFilter = _window.CanvasItemDefaultTextureFilter;
        _window.SizeChanged += QueueRenderResize;
        GetTree().NodeAdded += OnNodeAdded;
        TreeExiting += OnExitTree;
        ApplyViewportSettings();
        QueueRenderResize();
        PatchHelper.Log(
            $"[Graphics] Scale={GraphicsPatches.Settings.RenderScale}%, MSAA={GraphicsPatches.Settings.Msaa}, HDR={GraphicsPatches.Settings.Hdr}, pacing={GraphicsPatches.Settings.FramePacing}"
        );
        PatchHelper.Log(
            $"[Graphics] Native pacing enabled={ProjectSettings.GetSettingWithOverride("display/window/frame_pacing/android/enable_frame_pacing")}, mode={ProjectSettings.GetSettingWithOverride("display/window/frame_pacing/android/swappy_mode")}"
        );
    }

    private void OnExitTree()
    {
        _window.SizeChanged -= QueueRenderResize;
        GetTree().NodeAdded -= OnNodeAdded;
    }

    public void ApplyViewportSettings()
    {
        var settings = GraphicsPatches.Settings;
        int msaa = settings.Msaa < 0 ? SaveManager.Instance.SettingsSave.Msaa : settings.Msaa;
        _window.Msaa2D = GraphicsPatches.GetMsaa(msaa);
        _window.UseHdr2D = settings.Hdr < 0 ? _defaultHdr : settings.Hdr == 1;
        var filter = settings.TextureFilter switch
        {
            1 => Viewport.DefaultCanvasItemTextureFilter.Nearest,
            2 => Viewport.DefaultCanvasItemTextureFilter.Linear,
            4 or 6 => Viewport.DefaultCanvasItemTextureFilter.LinearWithMipmaps,
            _ => _defaultFilter,
        };
        _window.CanvasItemDefaultTextureFilter = filter;
        // RenderingServer also supports anisotropy, which Viewport's smaller
        // filter enum cannot represent. The persisted values belong to that API.
        if (settings.TextureFilter == 6)
            RenderingServer.ViewportSetDefaultCanvasItemTextureFilter(
                _window.GetViewportRid(),
                RenderingServer.CanvasItemTextureFilter.LinearWithMipmapsAnisotropic
            );
        PatchHelper.Log(
            $"[Graphics] Applied MSAA={_window.Msaa2D}, HDR={_window.UseHdr2D}, filter={filter}, anisotropic={settings.TextureFilter == 6}, MaxFPS={Engine.MaxFps}"
        );
        QueueRenderResize();
    }

    private void QueueRenderResize()
    {
        if (_resizeQueued)
            return;
        _resizeQueued = true;
        Callable.From(ApplyRenderSize).CallDeferred();
    }

    private void ApplyRenderSize()
    {
        _resizeQueued = false;
        if (!IsInsideTree())
            return;
        var size = _window.Size;
        if (size.X <= 0 || size.Y <= 0)
            return;
        float scale = GraphicsPatches.Settings.RenderScale / 100f;
        int width = Math.Max(2, (int)Math.Round(size.X * scale));
        int height = Math.Max(2, (int)Math.Round(size.Y * scale));
        var ratio = new Vector2((float)width / size.X, (float)height / size.Y);
        var viewport = _window.GetViewportRid();
        // Keep Window's logical size and input transform intact. Only the render
        // server target and its drawing transform shrink; presentation upscales.
        RenderingServer.ViewportSetSize(viewport, width, height);
        RenderingServer.ViewportSetGlobalCanvasTransform(
            viewport,
            _window.GetFinalTransform().Scaled(ratio)
        );
        PatchHelper.Log(
            $"[Graphics] Render target={width}x{height}, layout={_window.GetVisibleRect().Size}"
        );
    }

    private void OnNodeAdded(Node node)
    {
        bool backgroundParticle =
            node is GpuParticles2D or CpuParticles2D && IsCombatBackground(node);
        bool distortion = IsScreenDistortion(node);
        if (!backgroundParticle && !distortion)
            return;
        if (node.IsNodeReady())
            ApplyEffectSettings(node);
        else
            node.Connect(
                Node.SignalName.Ready,
                Callable.From(() => ApplyEffectSettings(node)),
                (uint)GodotObject.ConnectFlags.OneShot
            );
    }

    private static bool IsCombatBackground(Node node)
    {
        for (var parent = node.GetParent(); parent != null; parent = parent.GetParent())
            if (parent.GetType().FullName == "MegaCrit.Sts2.Core.Nodes.Rooms.NCombatBackground")
                return true;
        return false;
    }

    private static bool IsScreenDistortion(Node node) =>
        node is CanvasItem { Material: ShaderMaterial material }
        && material.Shader?.ResourcePath is DistortionShader or ScreamDistortionShader;

    private static void ApplyEffectSettings(Node node)
    {
        var settings = GraphicsPatches.Settings;
        if (node is GpuParticles2D or CpuParticles2D && IsCombatBackground(node))
        {
            const string originalAmountKey = "_mobile_graphics_original_amount";
            int amount = node is GpuParticles2D gpu ? gpu.Amount : ((CpuParticles2D)node).Amount;
            if (!node.HasMeta(originalAmountKey))
                node.SetMeta(originalAmountKey, amount);
            int originalAmount = node.GetMeta(originalAmountKey).AsInt32();
            int reducedAmount = Math.Max(1, originalAmount * settings.BackgroundParticles / 100);
            if (node is GpuParticles2D gpuParticles)
            {
                if (settings.BackgroundParticles == 0)
                {
                    gpuParticles.Emitting = false;
                    gpuParticles.Hide();
                }
                else
                    gpuParticles.Amount = reducedAmount;
            }
            else if (node is CpuParticles2D cpuParticles)
            {
                if (settings.BackgroundParticles == 0)
                {
                    cpuParticles.Emitting = false;
                    cpuParticles.Hide();
                }
                else
                    cpuParticles.Amount = reducedAmount;
            }
        }
        if (settings.ScreenDistortion || node is not CanvasItem item || !IsScreenDistortion(node))
            return;
        item.VisibilityChanged += HideDistortion;
        item.TreeExiting += Detach;
        HideDistortion();
        void HideDistortion()
        {
            if (item.Visible)
                item.Hide();
        }
        void Detach()
        {
            item.VisibilityChanged -= HideDistortion;
            item.TreeExiting -= Detach;
        }
    }
}
