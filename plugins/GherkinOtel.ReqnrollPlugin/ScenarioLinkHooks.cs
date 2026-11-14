using Reqnroll;

namespace GherkinOtel.ReqnrollPlugin;

/// <summary>Writes the scenario-level Telemetry Viewer link (log message of this AfterScenario hook). Never changes the scenario result.</summary>
[Binding]
public sealed class ScenarioLinkHooks(IScenarioContext scenarioContext, IReqnrollOutputHelper output, RunRecord record, RunTracker tracker, PluginDiagnostics diagnostics)
{
    [AfterScenario(Order = TelemetryHooks.ScenarioLinkOrder)]
    public void WriteScenarioLink()
    {
        try
        {
            var run = ReqnrollScenario.Run(scenarioContext, record);
            tracker.WriteScenarioLink(run, new ReqnrollLinkWriter(output));
        }
        catch (Exception ex)
        {
            diagnostics.Report($"failed to write scenario link: {ex.Message}");
        }
    }
}
