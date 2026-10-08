using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace STS2Mobile.Launcher;

public sealed record RenderBenchmarkCase(string Scene, string Name)
{
    public int Scale { get; init; } = 100;
    public int Msaa { get; init; }
    public bool Hdr { get; init; }
    public int Filter { get; init; } = 2;
    public bool Direct { get; init; }
    public int Blur { get; init; } = 12;
    public bool Distortion { get; init; } = true;
    public int Particles { get; init; } = 100;
    public int Fps { get; init; }

    public static IEnumerable<RenderBenchmarkCase> QualityCases()
    {
        foreach (string scene in new[] { "Cards", "Geometry", "Effects" })
        {
            var baseline = new RenderBenchmarkCase(scene, "baseline-start");
            yield return baseline;
            yield return baseline with
            {
                Name = "HDR on",
                Hdr = true,
            };
            yield return baseline with
            {
                Name = "resolution 85%",
                Scale = 85,
            };
            if (scene == "Cards")
            {
                yield return baseline with
                {
                    Name = "nearest",
                    Filter = 1,
                };
                yield return baseline with
                {
                    Name = "mipmap",
                    Filter = 4,
                };
                yield return baseline with
                {
                    Name = "anisotropic",
                    Filter = 6,
                };
                yield return baseline with
                {
                    Name = "direct portraits",
                    Direct = true,
                };
            }
            if (scene == "Geometry")
                foreach (int msaa in new[] { 2, 4, 8 })
                    yield return baseline with
                    {
                        Name = $"MSAA {msaa}x",
                        Msaa = msaa,
                    };
            if (scene == "Effects")
            {
                yield return baseline with
                {
                    Name = "blur 6 samples",
                    Blur = 6,
                };
                yield return baseline with
                {
                    Name = "blur off",
                    Blur = 0,
                };
                yield return baseline with
                {
                    Name = "distortion off",
                    Distortion = false,
                };
                yield return baseline with
                {
                    Name = "particles 50%",
                    Particles = 50,
                };
                yield return baseline with
                {
                    Name = "particles off",
                    Particles = 0,
                };
            }
            yield return baseline with
            {
                Name = "baseline-end",
            };
        }
    }
}

public sealed class BenchmarkMetrics
{
    public int Frames { get; set; }
    public double Fps { get; set; }
    public double FrameMeanMs { get; set; }
    public double FrameP95Ms { get; set; }
    public double FrameP99Ms { get; set; }
    public double CpuMeanMs { get; set; }
    public double? GpuMeanMs { get; set; }
    public double? GpuP95Ms { get; set; }
    public double DrawCallsMean { get; set; }
}

public sealed class BenchmarkSamples
{
    private readonly List<double> _frames = new();
    private readonly List<double> _gpu = new();
    private double _cpu;
    private double _draws;

    public void Add(double frameMs, double cpuMs, double gpuMs, int drawCalls)
    {
        if (!double.IsFinite(frameMs) || frameMs <= 0)
            return;
        _frames.Add(frameMs);
        _cpu += cpuMs;
        _draws += drawCalls;
        if (double.IsFinite(gpuMs) && gpuMs > 0)
            _gpu.Add(gpuMs);
    }

    public BenchmarkMetrics Summarize()
    {
        if (_frames.Count == 0)
            return new BenchmarkMetrics();
        var ordered = _frames.OrderBy(x => x).ToArray();
        var gpu = _gpu.OrderBy(x => x).ToArray();
        double mean = _frames.Average();
        return new BenchmarkMetrics
        {
            Frames = _frames.Count,
            Fps = 1000 / mean,
            FrameMeanMs = mean,
            FrameP95Ms = Percentile(ordered, .95),
            FrameP99Ms = Percentile(ordered, .99),
            CpuMeanMs = _cpu / _frames.Count,
            GpuMeanMs = gpu.Length > 0 ? gpu.Average() : null,
            GpuP95Ms = gpu.Length > 0 ? Percentile(gpu, .95) : null,
            DrawCallsMean = _draws / _frames.Count,
        };
    }

    private static double Percentile(double[] ordered, double fraction) =>
        ordered[
            Math.Clamp((int)Math.Ceiling(ordered.Length * fraction) - 1, 0, ordered.Length - 1)
        ];
}

public sealed class BenchmarkResult
{
    public string Case { get; set; }
    public string Variant { get; set; }
    public string Applied { get; set; }
    public string ThermalStart { get; set; }
    public string ThermalEnd { get; set; }
    public string Screenshot { get; set; }
    public BenchmarkMetrics Metrics { get; set; }
}

public sealed class RenderBenchmarkData
{
    public const int FormatVersion = 1;
    public static readonly int[] PacingModes = { -2, 0, 1, 2 };
    public int Version { get; set; } = FormatVersion;
    public string StartedUtc { get; set; } = DateTime.UtcNow.ToString("O");
    public string Device { get; set; }
    public string Engine { get; set; }
    public string App { get; set; }
    public string GameAssembly { get; set; }
    public string GameLibraryVersion { get; set; }
    public string RuntimeVersion { get; set; }
    public string Renderer { get; set; }
    public string Resolution { get; set; }
    public int Phase { get; set; }
    public bool Running { get; set; }
    public bool ShowResults { get; set; }
    public string Status { get; set; } = "Ready";
    public List<BenchmarkResult> Results { get; set; } = new();

    public bool CanResume(int nativePacing) =>
        Version == FormatVersion
        && Running
        && Phase >= 0
        && Phase < PacingModes.Length
        && PacingModes[Phase] == nativePacing;

    public void Save(string path)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this));
        File.Move(temporary, path, overwrite: true);
    }

    public static RenderBenchmarkData Load(string path) =>
        File.Exists(path)
            ? JsonSerializer.Deserialize<RenderBenchmarkData>(File.ReadAllText(path))
            : null;

    public string Report()
    {
        var text = new StringBuilder();
        text.AppendLine($"STS2 controlled rendering benchmark v{Version} | {Status}");
        text.AppendLine(
            $"UTC {StartedUtc}\nApp {App} | Engine {Engine}\nDevice {Device}\nRenderer {Renderer}\nResolution {Resolution}\nGame assembly {GameAssembly}"
        );
        if (GameLibraryVersion != null || RuntimeVersion != null)
            text.AppendLine(
                $"Game DLL version {GameLibraryVersion ?? "N/A"} | .NET runtime {RuntimeVersion ?? "N/A"}"
            );
        text.AppendLine(
            "Fixtures use game textures/shaders and native Canvas2D nodes; this is not full combat performance or battery consumption."
        );
        text.AppendLine(
            "Quality: uncapped / VSync off / native pacing off. Pacing: same composite scene, automatic cold starts, capped and uncapped."
        );
        text.AppendLine(
            "CPU/GPU are benchmark viewport rendering ms; frame times are whole-frame cadence. GPU N/A means timestamps unavailable. Warmup and screenshots excluded from samples."
        );
        text.AppendLine(
            "Baseline repeated before/after each scene's quality comparisons. Compare within the same scene; thermal changes and baseline drift can obscure small differences. No automatic no-effect verdict."
        );
        text.AppendLine(
            "Shader prewarming is a startup option and is not evaluated by steady rendering tests."
        );
        foreach (var result in Results)
        {
            var m = result.Metrics;
            text.AppendLine($"\n{result.Case} | {result.Variant} | {result.Applied}");
            text.AppendLine(
                FormattableString.Invariant(
                    $"n={m.Frames}, FPS={m.Fps:F2}, frame avg/p95/p99={m.FrameMeanMs:F3}/{m.FrameP95Ms:F3}/{m.FrameP99Ms:F3} ms, CPU={m.CpuMeanMs:F3} ms, GPU={Format(m.GpuMeanMs)}/{Format(m.GpuP95Ms)} ms avg/p95, canvas draws={m.DrawCallsMean:F1}"
                )
            );
            text.AppendLine($"Thermal {result.ThermalStart} → {result.ThermalEnd}");
        }
        return text.ToString();
    }

    private static string Format(double? value) =>
        value?.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "N/A";
}
