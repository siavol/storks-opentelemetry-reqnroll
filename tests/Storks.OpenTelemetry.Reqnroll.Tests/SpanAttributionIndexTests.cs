using NUnit.Framework;
using Storks.OpenTelemetry.Reqnroll.Core;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

/// <summary>Run record + received spans -> which scenario run and step each span is attributed to (rule: span started in the Observation Window).</summary>
public class SpanAttributionIndexTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Ms(double ms) => T0.AddTicks((long)Math.Round(ms * TimeSpan.TicksPerMillisecond));

    private static TelemetrySpan Span(string id, double startMs, string? parent = null) =>
        new("t1", id, parent, "op " + id, "orders-api", SpanKind.Server, Ms(startMs), Ms(startMs + 1), SpanStatus.Unset,
            new Dictionary<string, string>());

    /// <summary>Scenario [0, 300] ms with steps [10, 100] and [100.0001, 200]; 200..300 is the AfterScenario hook window.</summary>
    private static ScenarioRun TwoSteps(RunRecord record, string name = "S", double shiftMs = 0)
    {
        var run = record.Begin("F", name, [], Ms(shiftMs));
        run.AddStep("When", "first", Ms(shiftMs + 10), Ms(shiftMs + 100), "passed", null);
        run.AddStep("Then", "second", Ms(shiftMs + 100.0001), Ms(shiftMs + 200), "passed", null);
        run.Finish(Ms(shiftMs + 300), "passed", null);
        return run;
    }

    [Test]
    public void Span_is_attributed_to_the_step_in_whose_window_it_started_with_both_window_ends_inclusive()
    {
        var record = new RunRecord();
        TwoSteps(record);
        TelemetrySpan[] spans =
        [
            Span("after-scenario", 300.0001), // outside the scenario: not attributed to it
            Span("before-step", 9.999), // scenario window, before the first step: scenario only
            Span("first-end", 100),
            Span("first-start", 10),
            Span("second-start", 100.0001),
            Span("second-end", 200),
            Span("after-steps", 250), // AfterScenario hook: scenario only
        ];

        var scenario = RunIndex.Build(record.Snapshot(), spans).Features.Single().Scenarios.Single();

        Assert.That(scenario.Steps[0].SpanIds, Is.EqualTo(new[] { "first-start", "first-end" }), "chronological");
        Assert.That(scenario.Steps[1].SpanIds, Is.EqualTo(new[] { "second-start", "second-end" }));
        Assert.That(scenario.SpanIds, Is.EqualTo(new[] { "before-step", "first-start", "first-end", "second-start", "second-end", "after-steps" }));
    }

    [Test]
    public void Span_belongs_to_the_window_it_started_in_however_it_ends_unfinished_or_past_the_scenario_or_before_its_window()
    {
        var record = new RunRecord();
        TwoSteps(record);
        TelemetrySpan[] spans =
        [
            Span("past-scenario", 150) with { End = Ms(1000) },
            Span("unfinished", 20) with { End = null },
            Span("early", -5) with { End = Ms(50) }, // started before the scenario window, ended inside it
        ];

        var index = RunIndex.Build(record.Snapshot(), spans);

        var scenario = index.Features.Single().Scenarios.Single();
        Assert.That(scenario.Steps[0].SpanIds, Is.EqualTo(new[] { "unfinished" }));
        Assert.That(scenario.Steps[1].SpanIds, Is.EqualTo(new[] { "past-scenario" }));
        Assert.That(scenario.SpanIds, Has.No.Member("early"));
        Assert.That(index.Unattributed.SpanIds, Is.EqualTo(new[] { "early" }));
    }

    [Test]
    public void Outline_example_runs_get_their_own_ids_and_only_the_spans_of_their_own_windows()
    {
        var record = new RunRecord();
        var example1 = TwoSteps(record, "Outline");
        var example2 = TwoSteps(record, "Outline", shiftMs: 1000);

        var scenarios = RunIndex.Build(record.Snapshot(), [Span("a", 50), Span("b", 1050), Span("c", 1150)]).Features.Single().Scenarios;

        Assert.That(scenarios.Select(s => s.Id), Is.EqualTo(new[] { example1.Id, example2.Id }));
        Assert.That(scenarios[0].Steps.Select(s => s.SpanIds), Is.EqualTo(new[] { new[] { "a" }, Array.Empty<string>() }));
        Assert.That(scenarios[1].Steps.Select(s => s.SpanIds), Is.EqualTo(new[] { new[] { "b" }, new[] { "c" } }));
    }

    [Test]
    public void Index_json_carries_windows_as_OTLP_unix_nanos_and_span_ids_of_steps()
    {
        var record = new RunRecord();
        TwoSteps(record);

        var json = System.Text.Json.Nodes.JsonNode.Parse(RunIndex.Build(record.Snapshot(), [Span("00f067aa0ba902b7", 10)]).ToJson())!;

        var scenario = json["features"]![0]!["scenarios"]![0]!;
        var step = scenario["steps"]![0]!;
        // T0 = 2026-11-14T10:00:00Z = 1794650400 s since the Unix epoch.
        Assert.That((string?)scenario["startUnixNano"], Is.EqualTo("1794650400000000000"));
        Assert.That((string?)step["startUnixNano"], Is.EqualTo("1794650400010000000"));
        Assert.That((string?)step["endUnixNano"], Is.EqualTo("1794650400100000000"));
        Assert.That(step["spanIds"]!.AsArray().Select(x => (string?)x), Is.EqualTo(new[] { "00f067aa0ba902b7" }));
    }
}
