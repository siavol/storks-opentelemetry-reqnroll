using NUnit.Framework;
using Storks.OpenTelemetry.Reqnroll.Core;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

/// <summary>Run record + received logs -> which scenario run and step each log is attributed to (rule: log time in the Observation Window).</summary>
public class LogAttributionIndexTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Ms(double ms) => T0.AddTicks((long)Math.Round(ms * TimeSpan.TicksPerMillisecond));

    private static TelemetryLog Log(string id, double atMs, string? spanId = null) =>
        new(Ms(atMs), "orders-api", LogSeverity.Information, "Orders", "log " + id, spanId is null ? null : "t1", spanId) { Id = id };

    /// <summary>Scenario [0, 300] ms with steps [10, 100] and [100.0001, 200].</summary>
    private static void TwoSteps(RunRecord record)
    {
        var run = record.Begin("F", "S", [], Ms(0));
        run.AddStep("When", "first", Ms(10), Ms(100), "passed", null);
        run.AddStep("Then", "second", Ms(100.0001), Ms(200), "passed", null);
        run.Finish(Ms(300), "passed", null);
    }

    [Test]
    public void Log_is_attributed_to_the_step_in_whose_window_it_was_written_with_or_without_a_span()
    {
        var record = new RunRecord();
        TwoSteps(record);
        TelemetryLog[] logs =
        [
            Log("late", 300.0001), // after the scenario: not attributed to it
            Log("first-end", 100, spanId: "s1"),
            Log("first-start", 10), // no span context
            Log("before-step", 5), // scenario window, before the first step: scenario only
            Log("second", 150, spanId: "s2"),
            Log("after-steps", 250), // AfterScenario hook: scenario only
        ];

        var scenario = RunIndex.Build(record.Snapshot(), [], logs).Features.Single().Scenarios.Single();

        Assert.That(scenario.Steps[0].LogIds, Is.EqualTo(new[] { "first-start", "first-end" }), "chronological");
        Assert.That(scenario.Steps[1].LogIds, Is.EqualTo(new[] { "second" }));
        Assert.That(scenario.LogIds, Is.EqualTo(new[] { "before-step", "first-start", "first-end", "second", "after-steps" }));
    }

    [Test]
    public void Log_is_attributed_by_its_own_time_even_when_its_span_started_in_an_earlier_step()
    {
        var record = new RunRecord();
        TwoSteps(record);
        var span = new TelemetrySpan("t1", "s1", null, "op", "orders-api", SpanKind.Server, Ms(50), Ms(160), SpanStatus.Unset,
            new Dictionary<string, string>());

        var scenario = RunIndex.Build(record.Snapshot(), [span], [Log("in-second", 150, spanId: "s1")]).Features.Single().Scenarios.Single();

        Assert.That(scenario.Steps[0].SpanIds, Is.EqualTo(new[] { "s1" }));
        Assert.That(scenario.Steps[0].LogIds, Is.Empty);
        Assert.That(scenario.Steps[1].LogIds, Is.EqualTo(new[] { "in-second" }));
    }
}
