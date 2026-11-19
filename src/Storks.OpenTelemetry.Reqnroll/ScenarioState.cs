using Storks.OpenTelemetry.Reqnroll.Core;

namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>Scenario-scoped, so that parallel scenarios on other threads never see each other's state.</summary>
internal sealed class ScenarioState
{
    public ScenarioRun? Run { get; set; }
    internal DateTimeOffset? StepStart { get; set; }
}
