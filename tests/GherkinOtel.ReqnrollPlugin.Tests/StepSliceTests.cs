using GherkinOtel.ReqnrollPlugin;
using NUnit.Framework;

namespace GherkinOtel.ReqnrollPlugin.Tests;

public class StepSliceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static TelemetryLog Log(double sec, string msg) =>
        new(T0.AddSeconds(sec), "orders-api", LogSeverity.Information, "Cat", msg);

    [Test]
    public void Step_slice_contains_only_telemetry_started_in_the_step_window_and_is_a_subset_of_scenario_telemetry()
    {
        var scenario = new ScenarioTelemetry(new ObservationWindow(T0, T0.AddSeconds(10)),
            [Log(0.5, "before"), Log(2, "in-step"), Log(3, "edge"), Log(5, "after"), Log(8, "late")]);
        var step = new ObservationWindow(T0.AddSeconds(1), T0.AddSeconds(3));

        var slice = scenario.Slice(step);

        Assert.That(slice.Logs.Select(l => l.Message), Is.EqualTo(new[] { "in-step", "edge" }));
        Assert.That(slice.Window, Is.EqualTo(step));
        Assert.That(slice.Logs, Is.SubsetOf(scenario.Logs));
    }

    [Test]
    public void Step_slice_without_telemetry_renders_no_telemetry()
    {
        var scenario = new ScenarioTelemetry(new ObservationWindow(T0, T0.AddSeconds(10)), [Log(5, "x")]);

        var text = TelemetryRenderer.Render(scenario.Slice(new ObservationWindow(T0, T0.AddSeconds(1))));

        Assert.That(text, Is.EqualTo(TelemetryRenderer.NoTelemetry));
    }

    private static TelemetrySpan Span(string id, string? parent, string name, double startSec) =>
        new("t1", id, parent, name, "orders-api", SpanKind.Server, T0.AddSeconds(startSec), T0.AddSeconds(startSec + 0.5),
            SpanStatus.Unset, new Dictionary<string, string>());

    [Test]
    public void Step_slice_contains_spans_that_started_in_the_step_window_with_their_logs_nested()
    {
        var inside = Span("a", null, "POST /orders", 2);
        var scenario = new ScenarioTelemetry(new ObservationWindow(T0, T0.AddSeconds(10)),
            [new TelemetryLog(T0.AddSeconds(2.1), "orders-api", LogSeverity.Warning, "Cat", "inner", "t1", "a")])
        {
            Spans = [Span("b", null, "early", 0.5), inside, Span("c", null, "late", 8)],
        };

        var slice = scenario.Slice(new ObservationWindow(T0.AddSeconds(1), T0.AddSeconds(3)));
        var lines = TelemetryRenderer.Render(slice).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.That(slice.Spans, Is.EqualTo(new[] { inside }));
        Assert.That(lines[0], Does.Contain("POST /orders"));
        Assert.That(lines[1], Does.StartWith("  ").And.Contain("inner"));
    }

    [Test]
    public void Step_slice_times_are_relative_to_the_scenario_start()
    {
        var scenario = new ScenarioTelemetry(new ObservationWindow(T0, T0.AddSeconds(10)), [Log(2, "in-step")]);

        var text = TelemetryRenderer.Render(scenario.Slice(new ObservationWindow(T0.AddSeconds(1), T0.AddSeconds(3))));

        Assert.That(text, Does.StartWith("+2.000s"));
    }
}
