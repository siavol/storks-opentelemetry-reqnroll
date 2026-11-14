using System.Text.Json.Nodes;
using NUnit.Framework;

namespace GherkinOtel.ReqnrollPlugin.Tests;

/// <summary>Run record + received telemetry -> which scenario hook window each span and log started in (the viewer hides them with "Show hooks" off).</summary>
public class HookAttributionIndexTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Ms(double ms) => T0.AddTicks((long)Math.Round(ms * TimeSpan.TicksPerMillisecond));

    private static TelemetrySpan Span(string id, double startMs, string? parent = null) =>
        new("t1", id, parent, "op " + id, "orders-api", SpanKind.Server, Ms(startMs), Ms(startMs + 1), SpanStatus.Unset,
            new Dictionary<string, string>());

    private static TelemetryLog Log(string id, double atMs, string? spanId = null) =>
        new(Ms(atMs), "orders-api", LogSeverity.Information, "Orders", "log " + id, spanId is null ? null : "t1", spanId) { Id = id };

    /// <summary>
    /// Scenario [0, 300]: BeforeScenario [5, 20], BeforeStep [30, 40], step "When" [40, 100], AfterStep [100, 110],
    /// step "Then" [120, 200], AfterScenario [210, 290].
    /// </summary>
    private static void WithHooks(RunRecord record)
    {
        var run = record.Begin("F", "S", [], Ms(0));
        run.AddHook(new HookRun("BeforeScenario", "AppHooks.Seed", Ms(5), Ms(20), "passed"));
        run.AddHook(new HookRun("BeforeStep", "AppHooks.Before", Ms(30), Ms(40), "passed"));
        run.AddStep("When", "first", Ms(40), Ms(100), "passed", null);
        run.AddHook(new HookRun("AfterStep", "AppHooks.After", Ms(100), Ms(110), "failed"));
        run.AddStep("Then", "second", Ms(120), Ms(200), "passed", null);
        run.AddHook(new HookRun("AfterScenario", "AppHooks.Cleanup", Ms(210), Ms(290), "passed"));
        run.Finish(Ms(300), "passed", null);
    }

    [Test]
    public void Span_and_log_that_started_in_a_hook_window_are_attributed_to_that_hook_and_not_to_a_step()
    {
        var record = new RunRecord();
        WithHooks(record);
        TelemetrySpan[] spans = [Span("seed", 10), Span("before-step", 35), Span("step", 50), Span("cleanup-end", 290), Span("gap", 205)];
        TelemetryLog[] logs = [Log("seed", 20), Log("step", 60), Log("after-step", 105), Log("cleanup", 250)];

        var scenario = RunIndex.Build(record.Snapshot(), spans, logs).Features.Single().Scenarios.Single();

        Assert.That(scenario.Hooks.Select(h => (h.HookType, h.Name, h.Status)), Is.EqualTo(new[]
        {
            ("BeforeScenario", "AppHooks.Seed", "passed"), ("BeforeStep", "AppHooks.Before", "passed"),
            ("AfterStep", "AppHooks.After", "failed"), ("AfterScenario", "AppHooks.Cleanup", "passed"),
        }), "in order of call");
        Assert.That(scenario.Hooks.Select(h => h.SpanIds), Is.EqualTo(new[] { new[] { "seed" }, new[] { "before-step" }, [], new[] { "cleanup-end" } }));
        Assert.That(scenario.Hooks.Select(h => h.LogIds), Is.EqualTo(new[] { new[] { "seed" }, [], new[] { "after-step" }, new[] { "cleanup" } }));
        Assert.That(scenario.Steps[0].SpanIds, Is.EqualTo(new[] { "step" }));
        Assert.That(scenario.Steps[0].LogIds, Is.EqualTo(new[] { "step" }));
        Assert.That(scenario.SpanIds, Has.Member("gap"), "outside steps and hooks the span still belongs to the scenario");
    }

    [Test]
    public void Hook_telemetry_hidden_with_hooks_off_is_the_hook_spans_with_their_subtree_and_their_logs_and_spanless_logs_of_hook_windows()
    {
        var record = new RunRecord();
        WithHooks(record);
        TelemetrySpan[] spans =
        [
            Span("seed", 10), // BeforeScenario
            Span("seed-child", 50, parent: "seed"), // started in the step, under a hook span
            Span("grandchild", 130, parent: "seed-child"),
            Span("step", 50),
        ];
        TelemetryLog[] logs =
        [
            Log("seed", 15, spanId: "seed"),
            Log("child", 60, spanId: "seed-child"), // step window, span of the hook subtree
            Log("step", 60, spanId: "step"),
            Log("spanless", 104), // AfterStep window, no span
            Log("step-late", 105, spanId: "step"), // AfterStep window, span of the step: stays with its span
            Log("foreign", 106, spanId: "elsewhere"), // AfterStep window, span not in the scenario
        ];

        var scenario = RunIndex.Build(record.Snapshot(), spans, logs).Features.Single().Scenarios.Single();

        Assert.That(scenario.HookTelemetry.SpanIds, Is.EqualTo(new[] { "seed", "seed-child", "grandchild" }));
        Assert.That(scenario.HookTelemetry.LogIds, Is.EqualTo(new[] { "seed", "child", "spanless", "foreign" }));
        Assert.That(scenario.Hooks[2].LogIds, Has.Member("step-late"), "attribution itself does not change");
    }

    [Test]
    public void Index_json_carries_hook_windows_as_OTLP_unix_nanos_with_offset_from_the_scenario_start()
    {
        var record = new RunRecord();
        WithHooks(record);

        var json = JsonNode.Parse(RunIndex.Build(record.Snapshot(), [Span("00f067aa0ba902b7", 10)]).ToJson())!;

        var hook = json["features"]![0]!["scenarios"]![0]!["hooks"]![0]!;
        Assert.That((string?)hook["hookType"], Is.EqualTo("BeforeScenario"));
        Assert.That((string?)hook["name"], Is.EqualTo("AppHooks.Seed"));
        // T0 = 2026-11-14T10:00:00Z = 1794650400 s since the Unix epoch.
        Assert.That((string?)hook["startUnixNano"], Is.EqualTo("1794650400005000000"));
        Assert.That((string?)hook["endUnixNano"], Is.EqualTo("1794650400020000000"));
        Assert.That((long?)hook["offsetMs"], Is.EqualTo(5));
        Assert.That((long?)hook["durationMs"], Is.EqualTo(15));
        Assert.That(hook["spanIds"]!.AsArray().Select(x => (string?)x), Is.EqualTo(new[] { "00f067aa0ba902b7" }));
    }
}
