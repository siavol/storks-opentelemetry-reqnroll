using GherkinOtel.ReqnrollPlugin;
using NUnit.Framework;

namespace GherkinOtel.ReqnrollPlugin.Tests;

public class UnattributedReportTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static TelemetryLog Log(double s, string message) =>
        new(T0.AddSeconds(s), "orders-api", LogSeverity.Information, "Orders.Cat", message);

    [Test]
    public void Report_file_has_startup_telemetry_and_omits_what_belongs_to_a_scenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gherkin-otel-tests", Guid.NewGuid().ToString("N"));
        var store = new TelemetryStore();
        store.AddLogs([Log(-3, "applying migrations"), Log(2, "inside scenario"), Log(9, "late")]);
        var windows = new RunWindows();
        windows.Add(new ObservationWindow(T0, T0.AddSeconds(5)));

        var path = UnattributedReport.Write(dir, store, windows);

        Assert.That(path, Is.EqualTo(Path.Combine(dir, UnattributedReport.FileName)));
        var text = File.ReadAllText(path);
        Assert.That(text, Does.Contain("applying migrations").And.Contain("late"));
        Assert.That(text, Does.Not.Contain("inside scenario"));
    }

    [Test]
    public void Report_file_is_written_even_when_nothing_is_unattributed()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gherkin-otel-tests", Guid.NewGuid().ToString("N"));

        var path = UnattributedReport.Write(dir, new TelemetryStore(), new RunWindows());

        Assert.That(File.ReadAllText(path), Is.EqualTo(TelemetryRenderer.NoTelemetry));
    }
}
