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
}
