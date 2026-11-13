using Reqnroll;

namespace GherkinOtel.ReqnrollPlugin;

/// <summary>Observation Window bookkeeping and attachment of Scenario Telemetry. Never changes the scenario result.</summary>
[Binding]
public sealed class TelemetryHooks(
    IReqnrollOutputHelper output,
    TelemetryStore store,
    PluginOptions options)
{
    private DateTimeOffset _scenarioStart;

    [BeforeScenario(Order = int.MinValue)]
    public void OpenWindow() => _scenarioStart = DateTimeOffset.UtcNow;

    [AfterScenario(Order = int.MaxValue)]
    public async Task AttachScenarioTelemetry()
    {
        try
        {
            var window = new ObservationWindow(_scenarioStart, DateTimeOffset.UtcNow);
            await Task.Delay(options.SettlePause);

            var scenario = Attribution.Attribute(store.Snapshot(), store.SnapshotSpans(), window).Scenario;
            var text = TelemetryRenderer.Render(scenario);

            var dir = Path.Combine(Path.GetTempPath(), "gherkin-otel", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "scenario-telemetry.txt");
            await File.WriteAllTextAsync(file, text);
            output.AddAttachment(file);
        }
        catch (Exception ex)
        {
            output.WriteLine($"[GherkinOtel] failed to attach scenario telemetry: {ex}");
        }
    }

    [AfterTestRun(Order = int.MaxValue)]
    public static async Task StopReceiver(OtlpReceiver receiver) => await receiver.DisposeAsync();
}
