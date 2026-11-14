namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>
/// Scenario-scoped state (created by Reqnroll's container per scenario), shared by the plugin's hooks and the listener:
/// the scenario run being recorded and the start of the step running now.
/// </summary>
internal sealed class ScenarioState
{
    public ScenarioRun? Run { get; set; }
    internal DateTimeOffset? StepStart { get; set; }
}
