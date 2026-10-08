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
        Case = "Merchant",
        Variant = "baseline",
        Metrics = summary,
        LoadTimings = new()
        {
            new BenchmarkLoadTiming
            {
                From = "CombatIdle",
                To = "Merchant",
                DurationMs = 123.5,
                FirstVisit = true,
            },
        },
    }
);
data.Boots.Add(
    new BenchmarkBootTiming
    {
        Phase = 1,
        LaunchToSceneMs = 2000,
        GameInitializationMs = 1500,
    }
);
data.Save(path);
var loaded = RenderBenchmarkData.Load(path);
Check(
    loaded.Phase == 1
        && loaded.Results[0].Metrics.FrameP99Ms == 40
        && loaded.Results[0].Metrics.GpuMeanMs == 4
        && loaded.Results[0].LoadTimings.Single().DurationMs == 123.5
        && loaded.Results[0].LoadTimings.Single().FirstVisit
        && loaded.Boots.Single().GameInitializationMs == 1500
        && loaded.Boots.Single().LaunchToSceneMs == 2000,
    "Resume and results survive offline restart"
);
File.Delete(path);
Console.WriteLine("PASS benchmark statistics, availability, guarded restart, offline persistence");
var cases = RenderBenchmarkCase.QualityCases().ToArray();
Check(
    cases
        .Select(x => x.Scene)
        .Distinct()
        .OrderBy(x => x)
        .SequenceEqual(
            new[]
            {
                "CombatCards",
                "CombatEffects",
                "CombatIdle",
                "Deck",
                "Map",
                "Merchant",
                "Transitions",
            }
        ),
    "Real combat, merchant, map, deck and transition scenarios must all be compared"
);
Check(
    !new RenderBenchmarkData { Version = 1, Running = true }.CanResume(-2),
    "Synthetic benchmark jobs must not resume as actual combat benchmarks"
);
Check(
    new RenderBenchmarkData { Version = 1 }
        .Report()
        .Contains("Synthetic") && new RenderBenchmarkData().Report().Contains("Actual combat"),
    "Copied reports must distinguish synthetic results from actual combat results"
);
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
    report.Contains("GPU=4.000/4.000")
        && report.Contains("Game assembly")
        && report.Contains("CombatIdle -> Merchant: 123.5 ms")
        && report.Contains("app launch to first test screen=2000.0 ms"),
    "Copyable report includes provenance and measurements"
);
var pacing = RenderBenchmarkCase.PacingCases(1, new[] { 30, 0 }).ToArray();
Check(
    pacing
        .Select(x => x.Scene)
        .Distinct()
        .OrderBy(x => x)
        .SequenceEqual(
            cases.Select(x => x.Scene).Distinct().Where(x => x != "Transitions").OrderBy(x => x)
        ),
    "Native pacing comparisons cover all steady game scenarios"
);
foreach (var group in pacing.GroupBy(x => x.Scene))
{
    Check(group.Select(x => x.Fps).SequenceEqual(new[] { 30, 0 }), "Every frame cap is compared");
    foreach (var test in group)
        Check(
            (test with { Name = "baseline", Fps = 0 })
                == new RenderBenchmarkCase(test.Scene, "baseline"),
            "Pacing comparisons keep visual settings identical"
        );
}
Console.WriteLine("PASS controlled one-option comparisons and repeated baselines");
var captureJob = new RenderBenchmarkData { CaptureOnly = true, Running = true };
captureJob.Save(path);
Check(
    RenderBenchmarkData.Load(path).CaptureOnly,
    "Capture job identity survives the automatic restart"
);
File.Delete(path);
Check(captureJob.CanResume(-2), "Visual comparison resumes its isolated capture boot");
captureJob.Phase = 1;
Check(!captureJob.CanResume(0), "Visual comparison must not enter pacing phases");
var comparisons = RenderBenchmarkCase.ComparisonCases().ToArray();
Check(
    comparisons.All(test =>
        !test.Hdr && test.Distortion && test.Particles == 100 && test.Blur != 6
    ),
    "Removed settings must not be included in the visual comparison job"
);
Check(
    comparisons
        .Where(test => test.Scale != 100)
        .Select(test => test.Scale)
        .Distinct()
        .OrderBy(x => x)
        .SequenceEqual(new[] { 50, 75, 85 }),
    "Capture every retained resolution choice"
);
foreach (var group in comparisons.GroupBy(test => test.Scene))
{
    Check(
        group.First().Name == "baseline-start" && group.Last().Name == "baseline-end",
        "Visual comparison retains paired same-scene baselines"
    );
    foreach (var test in group.Skip(1).SkipLast(1))
        Check(
            typeof(RenderBenchmarkCase)
                .GetProperties()
                .Where(property => property.Name != "Name" && property.Name != "Scene")
                .Count(property =>
                    !Equals(property.GetValue(group.First()), property.GetValue(test))
                ) == 1,
            "Visual comparison changes exactly one option"
        );
}
Console.WriteLine("PASS isolated visual capture job and retained option coverage");
byte[] originalPixels = [10, 20, 30, 255, 50, 60, 70, 255];
byte[] changedPixels = [14, 18, 32, 255, 50, 60, 70, 254];
var difference = PixelDifference.Calculate(originalPixels, changedPixels);
Check(
    difference.Pixels.SequenceEqual(new byte[] { 4, 4, 4, 255, 1, 1, 1, 255 }),
    "Difference image stores the maximum RGBA channel delta at each pixel"
);
Check(
    difference.Histogram.Sum() == 2 && difference.Histogram[4] == 1 && difference.Histogram[1] == 1,
    "Histogram counts pixels, including alpha-only changes"
);
Check(
    difference.Maximum == 4 && difference.MeanAbsolute == 1.125,
    "Difference statistics use unamplified channel values"
);
var identical = PixelDifference.Calculate(originalPixels, originalPixels);
Check(
    identical.Histogram[0] == 2 && identical.Maximum == 0 && identical.MeanAbsolute == 0,
    "Identical captures have zero difference"
);
var extreme = PixelDifference.Calculate(new byte[4], new byte[] { 255, 255, 255, 255 });
Check(
    extreme.Histogram[255] == 1 && extreme.MeanAbsolute == 255,
    "Maximum differences do not overflow"
);
foreach (var invalid in new[] { Array.Empty<byte>(), new byte[3], new byte[4] })
{
    bool rejected = false;
    try
    {
        PixelDifference.Calculate(originalPixels, invalid);
    }
    catch (ArgumentException)
    {
        rejected = true;
    }
    Check(rejected, "Unequal or invalid RGBA buffers cannot be compared");
}
Console.WriteLine("PASS exact pixel differences, alpha, histogram and input validation");

var stall = new BenchmarkSamples();
stall.Add(8, 1, 5, 10);
stall.Add(200, 1, 5, 10);
stall.Add(double.NaN, 1, 5, 10);
Check(
    stall.Summarize().MinimumFps == 5 && stall.Summarize().FrameMaxMs == 200,
    "A real stall remains in minimum FPS and maximum frame time"
);
var engineJob = new RenderBenchmarkData { EngineOnly = true, Running = true };
Check(engineJob.CanResume(-2), "Engine benchmark resumes with native pacing disabled");
engineJob.Phase = 1;
Check(!engineJob.CanResume(0), "Engine benchmark never enters an option sweep");
engineJob.Phase = 0;
engineJob.Results.Add(
    new BenchmarkResult
    {
        Case = "CombatIdle",
        Variant = "current visuals",
        Metrics = stall.Summarize(),
    }
);
engineJob.Save(path);
var engineLoaded = RenderBenchmarkData.Load(path);
Check(
    engineLoaded.EngineOnly && engineLoaded.Results.Single().Metrics.MinimumFps == 5,
    "Engine identity and worst frame survive restart"
);
Check(
    engineLoaded.Report().Contains("engine scene benchmark")
        && engineLoaded.Report().Contains("minimum FPS=5.00")
        && !engineLoaded.Report().Contains("Quality:")
        && !engineLoaded.Report().Contains("Baseline repeated"),
    "Engine reports distinguish uncapped no-capture measurements from option comparisons"
);
File.Delete(path);
Console.WriteLine("PASS engine job isolation and unfiltered worst frame");

var engineCases = RenderBenchmarkCase.EngineCases().ToArray();
Check(
    engineCases
        .Select(test => test.Scene)
        .SequenceEqual(new[] { "CombatTurns", "KaiserCrabTurns", "WaterfallGiantTurns" }),
    "Engine comparison covers full turns in ordinary combat and both boss candidates"
);
foreach (var test in engineCases)
    Check(
        test.Scale == 100
            && !test.Hdr
            && test.Msaa == 2
            && test.Filter == 4
            && test.Direct
            && test.Blur == 12
            && test.Distortion
            && test.Particles == 100
            && test.Fps == 0,
        "Both engines use explicit identical graphics settings without inherited values"
    );
Check(
    !new RenderBenchmarkData
    {
        Version = 2,
        EngineOnly = true,
        Running = true,
    }.CanResume(-2),
    "Old inherited-setting engine jobs cannot resume with the controlled boss protocol"
);
Check(
    !new RenderBenchmarkData
    {
        Version = 5,
        EngineOnly = true,
        Running = true,
    }.CanResume(-2),
    "The longer combat protocol cannot resume as a four-turn comparison"
);
Check(RenderBenchmarkCase.CombatTurnLimit == 4, "Both apps stop after four complete combat turns");
Check(
    !new RenderBenchmarkData
    {
        Version = 6,
        EngineOnly = true,
        Running = true,
    }.CanResume(-2),
    "Boosted-health jobs cannot resume with game-default health"
);
Check(
    engineJob.Report().Contains("game-default player and enemy HP")
        && !engineJob.Report().Contains("Fixture player HP="),
    "Reports identify unmodified game health"
);
Check(
    !new RenderBenchmarkData { Version = 5, EngineOnly = true }
        .Report()
        .Contains("Up to 4 player turns"),
    "Older long-sequence reports are not relabeled as four-turn results"
);
Check(
    engineJob.Report().Contains("enemy turns")
        && engineJob.Report().Contains("normal draw, discard and energy")
        && !engineJob.Report().Contains("no damage"),
    "The report identifies full combat turns and the fixture controls"
);
engineJob.Results[0].Sequence = "turns=4, cards=12, HP=80->62";
engineJob.Save(path);
Check(
    RenderBenchmarkData.Load(path).Report().Contains("turns=4, cards=12, HP=80->62"),
    "Observed combat progression survives report persistence"
);
File.Delete(path);
engineJob.Results[0].Applied = "vsyncActual=Enabled, refreshHz=120.00";
Check(
    engineJob.Report().Contains("vsyncActual=Enabled")
        && engineJob.Report().Contains("requested Mailbox presentation")
        && !engineJob.Report().Contains("inherited game settings")
        && !engineJob.Report().Contains("Uncapped / VSync off"),
    "Report records actual presentation state and does not promise unsupported VSync disabling"
);
Console.WriteLine("PASS identical engine visuals, boss coverage and presentation provenance");
