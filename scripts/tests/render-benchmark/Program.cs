using STS2Mobile.Launcher;

void Check(bool value, string message)
{
    if (!value)
        throw new Exception(message);
}
var samples = new BenchmarkSamples();
Check(samples.Summarize().GpuMeanMs == null, "Unavailable GPU must not become 0");
foreach (double frame in new[] { 10d, 20d, 30d, 40d })
    samples.Add(frame, 2, 4, 5);
var summary = samples.Summarize();
Check(
    summary.Frames == 4
        && summary.FrameMeanMs == 25
        && summary.FrameP95Ms == 40
        && summary.FrameP99Ms == 40,
    "Cadence percentiles"
);
Check(
    summary.Fps == 40
        && summary.GpuMeanMs == 4
        && summary.CpuMeanMs == 2
        && summary.DrawCallsMean == 5,
    "Mean and time weighted FPS"
);
var missing = new BenchmarkSamples();
missing.Add(10, 1, 0, 0);
Check(missing.Summarize().GpuMeanMs == null, "Unsupported timestamp 0 must be unavailable");
var data = new RenderBenchmarkData { Phase = 0, Running = true };
Check(
    data.CanResume(-2) && !data.CanResume(-99) && !data.CanResume(0),
    "Resume only after matching one-shot cold boot"
);
data.Phase = 4;
Check(!data.CanResume(-2), "Out-of-range phase rejected");
data.Phase = 1;
Check(data.CanResume(0), "Next phase uses correct native mode");
var path = Path.Combine(Path.GetTempPath(), "sts2-bench-test-" + Guid.NewGuid() + ".json");
data.Results.Add(
    new BenchmarkResult
    {
        Case = "Cards",
        Variant = "baseline",
        Metrics = summary,
    }
);
data.Save(path);
var loaded = RenderBenchmarkData.Load(path);
Check(
    loaded.Phase == 1
        && loaded.Results[0].Metrics.FrameP99Ms == 40
        && loaded.Results[0].Metrics.GpuMeanMs == 4,
    "Resume and results survive offline restart"
);
File.Delete(path);
Console.WriteLine("PASS benchmark statistics, availability, guarded restart, offline persistence");
var cases = RenderBenchmarkCase.QualityCases().ToArray();
foreach (var group in cases.GroupBy(x => x.Scene))
{
    var first = group.First();
    var last = group.Last();
    Check(
        (first with { Name = "same" }) == (last with { Name = "same" }),
        "Repeated baseline must match exactly"
    );
    foreach (var variant in group.Skip(1).SkipLast(1))
    {
        int changed = typeof(RenderBenchmarkCase)
            .GetProperties()
            .Where(p => p.Name != "Name" && p.Name != "Scene")
            .Count(p => !Equals(p.GetValue(first), p.GetValue(variant)));
        Check(
            changed == 1 && variant.Fps == 0,
            "Each quality comparison changes exactly one option without FPS masking"
        );
    }
}
Check(
    cases.Any(x => x.Msaa == 8)
        && cases.Any(x => x.Hdr)
        && cases.Any(x => x.Filter == 6)
        && cases.Any(x => x.Direct)
        && cases.Any(x => x.Blur == 0)
        && cases.Any(x => !x.Distortion)
        && cases.Any(x => x.Particles == 0),
    "Coverage of render controls"
);
var report = data.Report();
Check(
    report.Contains("GPU=4.000/4.000") && report.Contains("Game assembly"),
    "Copyable report includes provenance and measurements"
);
Console.WriteLine("PASS controlled one-option comparisons and repeated baselines");
