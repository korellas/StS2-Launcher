using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Godot;

namespace STS2Mobile.Launcher;

public static class RenderComparisonPage
{
    public static string Write(RenderBenchmarkData data)
    {
        var results = data
            .Results.Where(result => result.Screenshot != null && File.Exists(result.Screenshot))
            .Select(result => new
            {
                result.Case,
                result.Variant,
                Screenshot = new Uri(result.Screenshot).AbsoluteUri,
            })
            .ToArray();
        if (results.Length == 0)
            throw new InvalidOperationException(Localization.Tr("BENCH_NO_CAPTURE"));
        string json = JsonSerializer.Serialize(
            new
            {
                data.App,
                data.Device,
                data.StartedUtc,
                data.Resolution,
                Results = results,
            }
        );
        string html = ReadResource("RenderComparison.html")
            .Replace("__COMPARISON_SCRIPT__", ReadResource("RenderComparison.js"))
            .Replace("__COMPARISON_DATA__", json);
        string directory = Path.Combine(OS.GetDataDir(), "render-comparison");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "index.html");
        File.WriteAllText(path, html);
        return path;
    }

    private static string ReadResource(string name)
    {
        using var stream =
            Assembly.GetExecutingAssembly().GetManifestResourceStream("STS2Mobile.Launcher." + name)
            ?? throw new InvalidOperationException("Missing comparison page resource: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
