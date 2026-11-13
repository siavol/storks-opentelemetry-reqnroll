using GherkinOtel.ReqnrollPlugin;
using NUnit.Framework;

namespace GherkinOtel.ReqnrollPlugin.Tests;

public class UnattributedTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static TelemetryLog Log(double offsetSeconds, string message) =>
        new(T0.AddSeconds(offsetSeconds), "orders-api", LogSeverity.Information, "Orders.Cat", message);

    private static ObservationWindow Window(double from, double to) => new(T0.AddSeconds(from), T0.AddSeconds(to));

    [Test]
    public void Telemetry_outside_all_scenario_windows_is_the_run_remainder()
    {
        var logs = new[] { Log(-5, "startup"), Log(2, "in first"), Log(7, "between"), Log(12, "in second"), Log(20, "late") };

        var remainder = RunAttribution.Unattributed(logs, [Window(0, 5), Window(10, 15)]);

        Assert.That(remainder.Select(l => l.Message), Is.EqualTo(new[] { "startup", "between", "late" }));
    }

    [Test]
    public void Without_any_windows_everything_is_unattributed()
    {
        Assert.That(RunAttribution.Unattributed([Log(1, "a"), Log(0, "b")], []).Select(l => l.Message), Is.EqualTo(new[] { "b", "a" }));
    }
}
