using Reqnroll;

namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>
/// Opens and closes the scenario's Observation Window, so that the test project's scenario hooks are inside it and the
/// plugin's own hooks are not, and, last of all, writes the plugin's diagnostics to the scenario output. Telemetry itself
/// goes to the Telemetry Viewer at the end of the run (<see cref="RunHooks"/>). Never changes the scenario result.
/// </summary>
[Binding]
internal sealed class TelemetryHooks(IReqnrollOutputHelper output, PluginDiagnostics diagnostics, ScenarioState state, RunTracker tracker)
{
    /// <summary>Order of the plugin's AfterScenario hooks: close the window, write the scenario link, flush diagnostics.</summary>
    internal const int CloseWindowOrder = int.MaxValue - 2, ScenarioLinkOrder = int.MaxValue - 1, FlushDiagnosticsOrder = int.MaxValue;

    /// <summary>Opens the window; a Window Overlap warning, if any, goes to the diagnostics flushed below.</summary>
    [BeforeScenario(Order = int.MinValue)]
    public void OpenWindow()
    {
        if (state.Run is { } run) tracker.WindowOpened(run, DateTimeOffset.UtcNow);
    }

    [AfterScenario(Order = CloseWindowOrder)]
    public void CloseWindow()
    {
        if (state.Run is { } run) tracker.WindowClosed(run, DateTimeOffset.UtcNow);
    }

    [AfterScenario(Order = FlushDiagnosticsOrder)]
    public void FlushDiagnostics()
    {
        try
        {
            foreach (var message in diagnostics.Drain()) output.WriteLine(PluginDiagnostics.Prefix + message);
        }
        catch { /* reporting problems must never change the scenario result */ }
    }
}
