using System.Text.Json.Nodes;
using NUnit.Framework;

namespace GherkinOtel.ReqnrollPlugin.Tests;

/// <summary>Run record + received telemetry -> Unattributed Telemetry of the index: what no scenario window took, split by run and feature hook windows.</summary>
public class UnattributedIndexTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Ms(double ms) => T0.AddTicks((long)Math.Round(ms * TimeSpan.TicksPerMillisecond));

    private static TelemetrySpan Span(string id, double startMs) =>
        new("t1", id, null, "op " + id, "orders-api", SpanKind.Server, Ms(startMs), Ms(startMs + 1), SpanStatus.Unset,
            new Dictionary<string, string>());

    private static TelemetryLog Log(string id, double atMs) =>
        new(Ms(atMs), "orders-api", LogSeverity.Information, "Orders", "log " + id, null, null) { Id = id };

    /// <summary>
    /// Run from 0: BeforeTestRun [10, 1000], BeforeFeature [1100, 1150], scenario [1200, 1500], AfterFeature [1600, 1650],
    /// AfterTestRun [1700, 2000].
    /// </summary>
    private static RunRecord Run()
    {
        var record = new RunRecord(Ms(0));
        record.AddRunHook(new HookRun("BeforeTestRun", "AppHostHooks.StartAppHost", Ms(10), Ms(1000), "passed"));
        record.AddRunHook(new HookRun("AfterTestRun", "AppHostHooks.StopAppHost", Ms(1700), Ms(2000), "passed"));
        record.AddRunHook(new HookRun("BeforeFeature", "FeatureHooks.Seed", Ms(1100), Ms(1150), "passed"));
        record.AddRunHook(new HookRun("AfterFeature", "FeatureHooks.Clean", Ms(1600), Ms(1650), "passed"));
        var run = record.Begin("F", "S", [], Ms(1200));
        run.AddStep("When", "it runs", Ms(1250), Ms(1400), "passed", null);
        run.Finish(Ms(1500), "passed", null);
        return record;
    }

    [Test]
    public void Telemetry_outside_every_scenario_window_is_unattributed_and_split_by_the_run_and_feature_hook_windows()
    {
        TelemetrySpan[] spans = [Span("startup", 20), Span("seed", 1120), Span("step", 1300), Span("gap", 1050), Span("stop", 1800), Span("late", 2500)];
        TelemetryLog[] logs = [Log("started", 900), Log("in-scenario", 1210), Log("between", 1580), Log("clean", 1650)];

        var index = RunIndex.Build(Run(), spans, logs);

        var un = index.Unattributed;
        Assert.That(un.SpanIds, Is.EqualTo(new[] { "startup", "gap", "seed", "stop", "late" }), "chronological, without scenario spans");
        Assert.That(un.LogIds, Is.EqualTo(new[] { "started", "between", "clean" }));
        Assert.That(un.Hooks.Select(h => h.HookType), Is.EqualTo(new[] { "BeforeTestRun", "BeforeFeature", "AfterFeature", "AfterTestRun" }), "by start time");
        Assert.That(un.Hooks.Select(h => h.SpanIds), Is.EqualTo(new[] { new[] { "startup" }, new[] { "seed" }, [], new[] { "stop" } }));
        Assert.That(un.Hooks.Select(h => h.LogIds), Is.EqualTo(new[] { new[] { "started" }, [], new[] { "clean" }, [] }), "window bounds are inclusive");
        var scenario = index.Features.Single().Scenarios.Single();
        Assert.That(scenario.SpanIds, Is.EqualTo(new[] { "step" }));
        Assert.That(scenario.LogIds, Is.EqualTo(new[] { "in-scenario" }));
    }

    [Test]
    public void Index_json_carries_the_run_scale_and_the_run_hook_windows_with_offsets_from_the_run_start()
    {
        var json = JsonNode.Parse(RunIndex.Build(Run(), [Span("startup", 20)]).ToJson())!;

        var un = json["unattributed"]!;
        // T0 = 2026-11-14T10:00:00Z = 1794650400 s since the Unix epoch; the run ends with its last window.
        Assert.That((string?)un["startUnixNano"], Is.EqualTo("1794650400000000000"));
        Assert.That((string?)un["endUnixNano"], Is.EqualTo("1794650402000000000"));
        var hook = un["hooks"]![0]!;
        Assert.That(((string?)hook["hookType"], (string?)hook["name"]), Is.EqualTo(("BeforeTestRun", "AppHostHooks.StartAppHost")));
        Assert.That((long?)hook["offsetMs"], Is.EqualTo(10));
        Assert.That((long?)hook["durationMs"], Is.EqualTo(990));
        Assert.That((string?)hook["startUnixNano"], Is.EqualTo("1794650400010000000"));
        Assert.That(hook["spanIds"]!.AsArray().Select(x => (string?)x), Is.EqualTo(new[] { "startup" }));
        Assert.That(un["spanIds"]!.AsArray().Select(x => (string?)x), Is.EqualTo(new[] { "startup" }));
    }
}
