using System;
using Godot;
using STS2Mobile.Diagnostics;

namespace STS2Mobile.Launcher.Components;

// Debug performance overlay: four rows of "value · sparkline · detail", anchored
// below the status bar in the top-right corner where the game keeps no HUD.
//
// This assembly references GodotSharp directly instead of building through
// Godot.NET.Sdk, so there are no source generators and the engine cannot dispatch
// _Process/_Draw overrides to our types. Everything here therefore runs off the
// SceneTree.ProcessFrame signal and draws with built-in Line2D nodes.
//
// Overhead is deliberately bounded: per-frame work is one timestamp diff, while
// sampling, text formatting and graph updates happen at SampleHz.
public class FpsOverlay : CanvasLayer
{
    private const int SampleHz = 4;
    private const float SampleInterval = 1f / SampleHz;
    private const int HistoryLength = 120; // 30 s at SampleHz
    private const int TopOffset = 100;
    private const int RightMargin = 24;
    private const int GraphWidth = 150;
    private const int RowHeight = 46;
    private const int LabelWidth = 38;
    private const int ValueWidth = 76;
    private const int DetailWidth = 148;
    private const int CaptionSize = 12;
    private const int ValueSize = 17;
    private const int DetailSize = 13;
    private const int PanelPadding = 10;
    private const int ColumnGap = 8;

    // Keeps a 100% reading off the very top edge, so a full bar still reads as a
    // line rather than merging with the row above.
    private const int GraphInset = 7;

    // Fixed axes: an auto-scaled graph makes 5% GPU look identical to 95%.
    private const float LoadMin = 0f;
    private const float LoadMax = 100f;
    private const float TempMin = 30f;
    private const float TempMax = 60f;
    private const float FpsMin = 0f;
    private const float FpsBaseMax = 60f;

    private const float LoadAlertThreshold = 90f;
    private const float TempAlertThreshold = 50f;


    private static readonly Color PanelColor = new(0.02f, 0.02f, 0.04f, 0.72f);
    private static readonly Color PanelBorder = new(0.55f, 0.55f, 0.62f, 0.28f);
    private static readonly Color TextColor = new(1f, 1f, 1f, 0.92f);
    private static readonly Color AlertLine = new(1f, 0.36f, 0.32f, 0.98f);

    // One hue per metric. A single colour for all four made the rows blur
    // together; these are distinct at a glance but stay within one palette.
    private static readonly Color FpsAccent = new(0.42f, 0.88f, 0.95f, 0.95f);
    private static readonly Color CpuAccent = new(0.98f, 0.78f, 0.36f, 0.95f);
    private static readonly Color GpuAccent = new(0.68f, 0.62f, 0.98f, 0.95f);
    private static readonly Color TempAccent = new(0.98f, 0.58f, 0.35f, 0.95f);

    private static readonly Color CaptionColor = new(0.68f, 0.70f, 0.76f, 0.85f);
    private static readonly Color DetailColor = new(0.78f, 0.80f, 0.85f, 0.80f);
    private static readonly Color MidlineColor = new(1f, 1f, 1f, 0.10f);
    private static readonly Color SeparatorColor = new(1f, 1f, 1f, 0.07f);

    private readonly SystemStatsReader _stats = new();

    private StatRow _fpsRow;
    private StatRow _cpuRow;
    private StatRow _gpuRow;
    private StatRow _tempRow;

    private SceneTree _tree;
    private ulong _lastFrameUsec;
    private double _sampleTimer;
    private int _framesSinceSample;

    // Roughly four seconds at 60 fps; long enough to catch a hitch, short
    // enough that one recovers quickly.
    private readonly float[] _frameTimes = new float[240];
    private int _frameTimeCount;
    private int _frameTimeHead;
    private float _fpsSum;
    private int _fpsSamples;

    public static FpsOverlay Show(SceneTree tree)
    {
        var overlay = new FpsOverlay { Layer = 128 };
        overlay.ProcessMode = ProcessModeEnum.Always;
        overlay.Build();
        tree.Root.AddChild(overlay);
        overlay.Attach(tree);
        return overlay;
    }

    private void Build()
    {
        int panelWidth = LabelWidth + ValueWidth + GraphWidth + DetailWidth + PanelPadding * 2;

        var panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 1f,
            AnchorRight = 1f,
            OffsetLeft = -(panelWidth + RightMargin),
            OffsetRight = -RightMargin,
            OffsetTop = TopOffset,
            OffsetBottom = TopOffset + RowHeight * 4 + 3 + PanelPadding * 2,
        };
        var panelStyle = new StyleBoxFlat { BgColor = PanelColor, BorderColor = PanelBorder };
        panelStyle.SetCornerRadiusAll(10);
        panelStyle.SetBorderWidthAll(1);
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(panel);

        var rows = new VBoxContainer
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = PanelPadding,
            OffsetTop = PanelPadding,
            OffsetRight = -PanelPadding,
            OffsetBottom = -PanelPadding,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        rows.AddThemeConstantOverride("separation", 0);
        panel.AddChild(rows);

        _fpsRow = StatRow.Add(rows, "fps", "FPS", FpsMin, FpsBaseMax, FpsAccent, alertAbove: null);
        AddSeparator(rows);
        _cpuRow = StatRow.Add(rows, "cpu", "CPU", LoadMin, LoadMax, CpuAccent, LoadAlertThreshold);
        AddSeparator(rows);
        _gpuRow = StatRow.Add(rows, "gpu", "GPU", LoadMin, LoadMax, GpuAccent, LoadAlertThreshold);
        AddSeparator(rows);
        _tempRow = StatRow.Add(rows, "temp", "TEMP", TempMin, TempMax, TempAccent, TempAlertThreshold);
    }

    private static void AddSeparator(Control parent)
    {
        var line = new ColorRect
        {
            Color = SeparatorColor,
            CustomMinimumSize = new Vector2(0, 1),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        parent.AddChild(line);
    }

    private void Attach(SceneTree tree)
    {
        _tree = tree;
        _lastFrameUsec = Time.GetTicksUsec();
        tree.ProcessFrame += OnProcessFrame;
        TreeExiting += Detach;
    }

    private void Detach()
    {
        if (_tree == null)
            return;
        _tree.ProcessFrame -= OnProcessFrame;
        _tree = null;
    }

    private void OnProcessFrame()
    {
        ulong now = Time.GetTicksUsec();
        double delta = (now - _lastFrameUsec) / 1_000_000.0;
        _lastFrameUsec = now;

        _framesSinceSample++;

        _frameTimes[_frameTimeHead] = (float)delta;
        _frameTimeHead = (_frameTimeHead + 1) % _frameTimes.Length;
        if (_frameTimeCount < _frameTimes.Length)
            _frameTimeCount++;

        _sampleTimer += delta;
        if (_sampleTimer < SampleInterval)
            return;

        Sample(_sampleTimer);
        _sampleTimer = 0;
        _framesSinceSample = 0;
    }

    private void Sample(double elapsed)
    {
        float fps = (float)(_framesSinceSample / elapsed);
        _fpsSum += fps;
        _fpsSamples++;

        float average = _fpsSum / _fpsSamples;
        _fpsRow.Push(fps, $"{fps:F0}", $"avg {average:F0}   low {WorstFps():F0}");

        float? cpu = _stats.ReadCpuPercent(elapsed);
        float? ram = _stats.ReadRamMegabytes();
        _cpuRow.Push(cpu, cpu is float c ? $"{c:F0} %" : null, ram is float r ? $"RAM {r:F0} MB" : "");

        float? gpu = _stats.ReadGpuPercent();
        float vram = SystemStatsReader.VideoMemoryMegabytes();
        _gpuRow.Push(gpu, gpu is float g ? $"{g:F0} %" : null, $"VRAM {vram:F0} MB");

        float? temp = _stats.ReadTemperatureCelsius();
        _tempRow.Push(temp, temp is float t ? $"{t:F1} °C" : null, ThermalStatus());
    }

    // Longest frame in the recent window, as fps. This is where stutter shows up
    // even when the average looks healthy.
    //
    // The window is a ring buffer rather than a grow-then-reset list: the old
    // version kept every sample until it filled, so a single multi-second frame
    // pinned the reading for as long as it took to refill. Long frames are still
    // counted — a stall is a real reading — they just age out of the window.
    private float WorstFps()
    {
        float worst = 0f;
        for (int i = 0; i < _frameTimeCount; i++)
        {
            if (_frameTimes[i] > worst)
                worst = _frameTimes[i];
        }

        return worst > 0f ? 1f / worst : 0f;
    }

    private string _thermalStatus = "";
    private int _thermalPollCounter;

    // Cheap to read, but it crosses into Java, so poll it once a second.
    private string ThermalStatus()
    {
        if (_thermalPollCounter++ % SampleHz != 0)
            return _thermalStatus;

        try
        {
            var jcw = Engine.GetSingleton("JavaClassWrapper");
            var wrapper = (GodotObject)jcw.Call("wrap", "com.game.sts2launcher.GodotApp");
            var godotApp = (GodotObject)wrapper.Call("getInstance");
            _thermalStatus = (string)godotApp.Call("getThermalStatus");
        }
        catch
        {
            _thermalStatus = "";
        }

        return _thermalStatus;
    }

    // One "value · sparkline · detail" line, with its own fixed axis.
    private sealed class StatRow
    {
        private readonly HBoxContainer _container;
        private readonly Label _value;
        private readonly Label _detail;
        private readonly Sparkline _graph;
        private readonly float? _alertAbove;
        private bool _everReadable;

        private StatRow(HBoxContainer container, Label value, Sparkline graph, Label detail, float? alertAbove)
        {
            _alertAbove = alertAbove;
            _container = container;
            _value = value;
            _graph = graph;
            _detail = detail;
        }

        public static StatRow Add(
            VBoxContainer parent,
            string name,
            string label,
            float min,
            float max,
            Color accent,
            float? alertAbove
        )
        {
            var row = new HBoxContainer
            {
                Name = name,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            row.AddThemeConstantOverride("separation", ColumnGap);
            parent.AddChild(row);

            var caption = MakeLabel(LabelWidth, HorizontalAlignment.Left, CaptionSize);
            caption.Text = label;
            caption.AddThemeColorOverride("font_color", CaptionColor);
            row.AddChild(caption);

            var value = MakeLabel(ValueWidth, HorizontalAlignment.Right, ValueSize);
            row.AddChild(value);

            var graph = new Sparkline(min, max, accent, alertAbove)
            {
                CustomMinimumSize = new Vector2(GraphWidth, RowHeight),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                // Line2D happily paints past its parent's bounds, which let the
                // fps trace spill over the row above and out of the panel.
                ClipContents = true,
            };
            row.AddChild(graph);

            var detail = MakeLabel(DetailWidth, HorizontalAlignment.Right, DetailSize);
            detail.AddThemeColorOverride("font_color", DetailColor);
            row.AddChild(detail);

            return new StatRow(row, value, graph, detail, alertAbove);
        }

        private static Label MakeLabel(int width, HorizontalAlignment align, int size)
        {
            var label = new Label
            {
                CustomMinimumSize = new Vector2(width, RowHeight),
                HorizontalAlignment = align,
                VerticalAlignment = VerticalAlignment.Center,
                // Values change width constantly; clipping keeps a long reading
                // from reflowing the row and making the panel jitter.
                ClipText = true,
                AutowrapMode = TextServer.AutowrapMode.Off,
            };
            label.AddThemeColorOverride("font_color", TextColor);
            label.AddThemeFontSizeOverride("font_size", size);
            return label;
        }

        // A null sample means the counter was unreadable. Hide the row only while
        // it has never produced a value: once it works, a transient failure keeps
        // the last reading instead of making the row blink in and out.
        public void Push(float? sample, string valueText, string detailText)
        {
            if (sample is not float v)
            {
                if (!_everReadable)
                    _container.Visible = false;
                return;
            }

            _everReadable = true;
            _container.Visible = true;
            _value.Text = valueText;
            if (_alertAbove is float threshold)
                _value.AddThemeColorOverride("font_color", v >= threshold ? AlertLine : TextColor);
            _detail.Text = detailText ?? "";
            _graph.Push(v);
        }
    }

    // Fixed-capacity ring buffer rendered with a built-in Line2D on a fixed axis,
    // so the same shape always means the same load.
    private sealed class Sparkline : Control
    {
        private readonly float[] _samples = new float[HistoryLength];
        private readonly Line2D _line = new();
        private readonly Line2D _midline = new();
        private readonly Polygon2D _fill = new();
        private readonly float _min;
        private readonly float _max;
        private readonly Color _accent;
        private readonly float? _alertAbove;
        private int _count;
        private int _head;

        public Sparkline(float min, float max, Color accent, float? alertAbove)
        {
            _min = min;
            _max = max;
            _accent = accent;
            _alertAbove = alertAbove;

            // Drawn back to front: the reference line, the filled area, then the
            // trace. A bare 2px line read as a scribble; the fill gives the shape
            // a body and makes low values legible at a glance.
            _midline.Width = 1f;
            _midline.DefaultColor = MidlineColor;
            AddChild(_midline);

            _fill.Color = new Color(_accent, 0.18f);
            AddChild(_fill);

            _line.Width = 2f;
            _line.DefaultColor = _accent;
            _line.Antialiased = true;
            AddChild(_line);
        }

        public void Push(float value)
        {
            _samples[_head] = value;
            _head = (_head + 1) % HistoryLength;
            if (_count < HistoryLength)
                _count++;

            if (_alertAbove is float threshold)
            {
                var colour = value >= threshold ? AlertLine : _accent;
                _line.DefaultColor = colour;
                _fill.Color = new Color(colour, 0.18f);
            }

            Rebuild();
        }

        private void Rebuild()
        {
            if (_count < 2)
                return;

            var size = Size;
            if (size.X <= 0f || size.Y <= 0f)
                size = CustomMinimumSize;

            // The fps axis is the one series that can legitimately exceed its
            // nominal ceiling, so let it grow rather than clip a 90 fps reading.
            float max = _max;
            for (int i = 0; i < _count; i++)
            {
                if (_samples[i] > max)
                    max = _samples[i];
            }

            float span = Math.Max(0.001f, max - _min);
            float stepX = size.X / (HistoryLength - 1);
            float top = GraphInset;
            float plotHeight = Math.Max(1f, size.Y - GraphInset * 2);
            int oldest = (_head - _count + HistoryLength) % HistoryLength;

            var points = new Vector2[_count];
            for (int i = 0; i < _count; i++)
            {
                float value = _samples[(oldest + i) % HistoryLength];
                float normalised = Math.Clamp((value - _min) / span, 0f, 1f);
                points[i] = new Vector2(i * stepX, top + plotHeight - normalised * plotHeight);
            }

            _line.Points = points;

            // Close the trace down to the baseline to get a filled area.
            var polygon = new Vector2[points.Length + 2];
            points.CopyTo(polygon, 0);
            polygon[^2] = new Vector2(points[^1].X, top + plotHeight);
            polygon[^1] = new Vector2(points[0].X, top + plotHeight);
            _fill.Polygon = polygon;

            float midY = top + plotHeight * 0.5f;
            _midline.Points = new[] { new Vector2(0, midY), new Vector2(size.X, midY) };
        }
    }
}
