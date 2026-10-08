using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace STS2Mobile.Launcher;

public static class RenderComparisonPage
{
    public static async Task<string> Write(RenderBenchmarkData data, SceneTree tree)
    {
        var captures = data
            .Results.Where(result => result.Screenshot != null && File.Exists(result.Screenshot))
            .ToArray();
        if (captures.Length == 0)
            throw new InvalidOperationException(Localization.Tr("BENCH_NO_CAPTURE"));
        string directory = Path.Combine(OS.GetDataDir(), "render-comparison");
        Directory.CreateDirectory(directory);
        var results = new List<object>();
        for (int index = 0; index < captures.Length; index++)
        {
            var capture = captures[index];
            DifferenceCapture start = null,
                end = null;
            if (capture.Variant is not ("baseline-start" or "baseline-end"))
            {
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                foreach (string baseline in new[] { "start", "end" })
                {
                    var original = captures.FirstOrDefault(result =>
                        result.Case == capture.Case && result.Variant == "baseline-" + baseline
                    );
                    if (original == null)
                        continue;
                    var difference = WriteDifference(
                        original.Screenshot,
                        capture.Screenshot,
                        Path.Combine(directory, $"diff-{index:D3}-{baseline}.png")
                    );
                    if (baseline == "start")
                        start = difference;
                    else
                        end = difference;
                }
            }
            results.Add(
                new
                {
                    capture.Case,
                    capture.Variant,
                    Screenshot = new Uri(capture.Screenshot).AbsoluteUri,
                    DiffStart = start,
                    DiffEnd = end,
                }
            );
        }
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
        string path = Path.Combine(directory, "index.html");
        File.WriteAllText(path, html);
        return path;
    }

    private sealed record DifferenceCapture(
        string Screenshot,
        int[] Histogram,
        double MeanAbsolute,
        int Maximum
    );

    private static DifferenceCapture WriteDifference(
        string originalPath,
        string changedPath,
        string path
    )
    {
        using var original = Image.LoadFromFile(originalPath);
        using var changed = Image.LoadFromFile(changedPath);
        if (
            original == null
            || changed == null
            || original.IsEmpty()
            || changed.IsEmpty()
            || original.GetSize() != changed.GetSize()
        )
            return null;
        original.Convert(Image.Format.Rgba8);
        changed.Convert(Image.Format.Rgba8);
        var difference = PixelDifference.Calculate(original.GetData(), changed.GetData());
        using var image = Image.CreateFromData(
            original.GetWidth(),
            original.GetHeight(),
            false,
            Image.Format.Rgba8,
            difference.Pixels
        );
        if (image.SavePng(path) != Error.Ok)
            throw new IOException("Could not save pixel difference image");
        return new DifferenceCapture(
            new Uri(path).AbsoluteUri,
            difference.Histogram,
            difference.MeanAbsolute,
            difference.Maximum
        );
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
