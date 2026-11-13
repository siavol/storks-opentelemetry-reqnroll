using GherkinOtel.ReqnrollPlugin;
using NUnit.Framework;

namespace GherkinOtel.ReqnrollPlugin.Tests;

public class RendererTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly ObservationWindow Window = new(T0, T0.AddSeconds(10));

    [Test]
    public void Empty_scenario_telemetry_renders_no_telemetry()
    {
        var text = TelemetryRenderer.Render(new ScenarioTelemetry(Window, []));

        Assert.That(text, Does.Contain("нет телеметрии"));
    }

    [Test]
    public void Logs_render_in_order_with_relative_time_service_level_category_and_message()
    {
        var logs = new[]
        {
            new TelemetryLog(T0.AddMilliseconds(120), "orders-api", LogSeverity.Information, "Orders.Cat", "first"),
            new TelemetryLog(T0.AddMilliseconds(1500), "pricing-api", LogSeverity.Warning, "Pricing.Cat", "second"),
        };

        var lines = TelemetryRenderer.Render(new ScenarioTelemetry(Window, logs))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.That(lines, Does.Not.Contain("нет телеметрии"));
        Assert.That(lines.Single(l => l.Contains("first")), Does.Match(@"^\+0\.120s\s+orders-api\s+Information\s+Orders\.Cat\s+first$"));
        Assert.That(lines.Single(l => l.Contains("second")), Does.Match(@"^\+1\.500s\s+pricing-api\s+Warning\s+Pricing\.Cat\s+second$"));
        Assert.That(Array.FindIndex(lines, l => l.Contains("first")), Is.LessThan(Array.FindIndex(lines, l => l.Contains("second"))));
    }
}
