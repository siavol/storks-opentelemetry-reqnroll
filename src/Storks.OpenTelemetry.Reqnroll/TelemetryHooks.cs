using Reqnroll;

namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>
/// Opens and closes the scenario's Observation Window, so that the test project's scenario hooks are inside it and the
/// plugin's own hooks are not, and, last of all, writes the plugin's diagnostics to the scenario output. Telemetry itself
/// goes to the Telemetry Viewer at the end of the run (<see cref="RunHooks"/>). Never changes the scenario result.
/// </summary>
[Binding]
public sealed class TelemetryHooks(IReqnrollOutputHelper output, PluginDiagnostics diagnostics, ScenarioState state)
{
    /// <summary>Order of the plugin's AfterScenario hooks: close the window, write the scenario link, flush diagnostics.</summary>
    internal const int CloseWindowOrder = int.MaxValue - 2, ScenarioLinkOrder = int.MaxValue - 1, FlushDiagnosticsOrder = int.MaxValue;

    [BeforeScenario(Order = int.MinValue)]
    public void OpenWindow() => state.Run?.OpenWindow(DateTimeOffset.UtcNow);

    [AfterScenario(Order = CloseWindowOrder)]
    public void CloseWindow() => state.Run?.CloseWindow(DateTimeOffset.UtcNow);

    [AfterScenario(Order = FlushDiagnosticsOrder)]
    public void FlushDiagnostics()
    {
        try
        {
            foreach (var message in diagnostics.Drain()) output.WriteLine($"[Storks.OpenTelemetry] {message}");
        }
        catch { /* reporting problems must never change the scenario result */ }
    }
}
