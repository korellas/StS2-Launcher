using System;
using System.Globalization;
using System.IO;
using Godot;

namespace STS2Mobile.Diagnostics;

// Samples per-process and device counters for the debug overlay.
//
// Every source is optional: sysfs nodes that the shell can read may still be
// blocked for an app by SELinux, and the layout differs between vendors. A
// reader that fails once is disabled for the rest of the session and reports
// null, so the overlay simply drops that row instead of throwing every frame.
public sealed class SystemStatsReader
{
    private const string ProcSelfStat = "/proc/self/stat";
    private const string ProcSelfStatm = "/proc/self/statm";
    private const string GpuBusyPath = "/sys/class/kgsl/kgsl-3d0/gpubusy";
    private const string GpuPercentPath = "/sys/class/kgsl/kgsl-3d0/gpu_busy_percentage";
    private const string ThermalPath = "/sys/class/thermal/thermal_zone0/temp";

    private static readonly long ClockTicksPerSecond = 100; // Android's USER_HZ
    private static readonly long PageSizeBytes = 4096;

    private readonly int _cpuCount = Math.Max(1, OS.GetProcessorCount());

    private bool _cpuAvailable = true;
    private bool _gpuAvailable = true;
    private bool _thermalAvailable = true;
    private bool _ramAvailable = true;

    private long _lastCpuTicks = -1;

    // Percentage of one wall-clock second spent on CPU across all cores, 0-100.
    public float? ReadCpuPercent(double elapsedSeconds)
    {
        if (!_cpuAvailable || elapsedSeconds <= 0)
            return null;

        try
        {
            var stat = File.ReadAllText(ProcSelfStat);

            // Field 14 (utime) and 15 (stime) are 1-indexed and follow the comm
            // field, which may itself contain spaces inside parentheses.
            int commEnd = stat.LastIndexOf(')');
            var fields = stat[(commEnd + 2)..].Split(' ');
            long utime = long.Parse(fields[11], CultureInfo.InvariantCulture);
            long stime = long.Parse(fields[12], CultureInfo.InvariantCulture);
            long ticks = utime + stime;

            if (_lastCpuTicks < 0)
            {
                _lastCpuTicks = ticks;
                return null;
            }

            long delta = ticks - _lastCpuTicks;
            _lastCpuTicks = ticks;

            double seconds = delta / (double)ClockTicksPerSecond;
            return (float)Math.Clamp(seconds / elapsedSeconds / _cpuCount * 100.0, 0.0, 100.0);
        }
        catch (Exception ex)
        {
            _cpuAvailable = false;
            PatchHelper.Log($"[Overlay] CPU stats unavailable: {ex.Message}");
            return null;
        }
    }

    // Adreno's gpubusy returns "busy total" in microseconds covering the window
    // since the previous read, not counters accumulated since boot — a single
    // read on this device gave "846912 1004007", a total of ~1.004 s. So the
    // ratio of the two values as read IS the utilisation; subtracting successive
    // reads underflows and produces garbage. gpu_busy_percentage is a pre-computed
    // alternative, but it reads 0 on some kernels, so it is only the fallback.
    public float? ReadGpuPercent()
    {
        if (!_gpuAvailable)
            return null;

        try
        {
            var parts = File.ReadAllText(GpuBusyPath)
                .Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (
                parts.Length >= 2
                && double.TryParse(
                    parts[0],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double busy
                )
                && double.TryParse(
                    parts[1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double total
                )
                && total > 0
            )
            {
                return (float)Math.Clamp(busy / total * 100.0, 0.0, 100.0);
            }

            // Reading gpubusy clears it, so a quick second read legitimately
            // reports an empty window. Fall back rather than report a failure.
            return ReadGpuPercentFallback();
        }
        catch (Exception ex)
        {
            _gpuAvailable = false;
            PatchHelper.Log($"[Overlay] GPU stats unavailable: {ex.Message}");
            return null;
        }
    }

    private static float? ReadGpuPercentFallback()
    {
        try
        {
            if (!File.Exists(GpuPercentPath))
                return null;

            var raw = File.ReadAllText(GpuPercentPath)
                .Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (
                raw.Length > 0
                && float.TryParse(
                    raw[0],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float percent
                )
            )
            {
                return Math.Clamp(percent, 0f, 100f);
            }
        }
        catch { }

        return null;
    }

    public float? ReadTemperatureCelsius()
    {
        if (!_thermalAvailable)
            return null;

        try
        {
            var raw = File.ReadAllText(ThermalPath).Trim();
            float milliCelsius = float.Parse(raw, CultureInfo.InvariantCulture);
            return milliCelsius / 1000f;
        }
        catch (Exception ex)
        {
            _thermalAvailable = false;
            PatchHelper.Log($"[Overlay] Thermal stats unavailable: {ex.Message}");
            return null;
        }
    }

    // Resident set size of this process, in megabytes.
    public float? ReadRamMegabytes()
    {
        if (!_ramAvailable)
            return null;

        try
        {
            var parts = File.ReadAllText(ProcSelfStatm).Trim().Split(' ');
            long residentPages = long.Parse(parts[1], CultureInfo.InvariantCulture);
            return residentPages * PageSizeBytes / 1024f / 1024f;
        }
        catch (Exception ex)
        {
            _ramAvailable = false;
            PatchHelper.Log($"[Overlay] RAM stats unavailable: {ex.Message}");
            return null;
        }
    }

    public static float VideoMemoryMegabytes()
    {
        try
        {
            ulong bytes = RenderingServer.GetRenderingInfo(
                RenderingServer.RenderingInfo.VideoMemUsed
            );
            return bytes / 1024f / 1024f;
        }
        catch
        {
            return 0f;
        }
    }
}
