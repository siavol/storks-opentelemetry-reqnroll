using GherkinOtel.ReqnrollPlugin;
using NUnit.Framework;

namespace GherkinOtel.ReqnrollPlugin.Tests;

public class AttributionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly ObservationWindow Window = new(T0, T0.AddSeconds(10));

    private static TelemetryLog Log(double offsetSeconds, string message) =>
        new(T0.AddSeconds(offsetSeconds), "orders-api", LogSeverity.Information, "Orders.Cat", message);

    [Test]
    public void Log_inside_window_is_scenario_telemetry()
    {
        var result = Attribution.Attribute([Log(5, "inside")], Window);

        Assert.That(result.Scenario.Logs.Select(l => l.Message), Is.EqualTo(new[] { "inside" }));
        Assert.That(result.Remainder, Is.Empty);
    }

    [Test]
    public void Logs_before_and_after_window_go_to_remainder()
    {
        var result = Attribution.Attribute([Log(-1, "before"), Log(5, "inside"), Log(11, "after")], Window);

        Assert.That(result.Scenario.Logs.Select(l => l.Message), Is.EqualTo(new[] { "inside" }));
        Assert.That(result.Remainder.Select(l => l.Message), Is.EquivalentTo(new[] { "before", "after" }));
    }

    [Test]
    public void Scenario_logs_are_chronological_regardless_of_input_order()
    {
        var result = Attribution.Attribute([Log(7, "c"), Log(1, "a"), Log(3, "b")], Window);

        Assert.That(result.Scenario.Logs.Select(l => l.Message), Is.EqualTo(new[] { "a", "b", "c" }));
    }
}
