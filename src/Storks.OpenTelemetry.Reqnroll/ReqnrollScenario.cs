using Reqnroll;
using Storks.OpenTelemetry.Reqnroll.Core;

namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>What the plugin reads from Reqnroll about the scenario running now: its run in the record, statuses, errors.</summary>
internal static class ReqnrollScenario
{
    public static ScenarioState State(IScenarioContext context) => context.ScenarioContainer.Resolve<ScenarioState>();

    /// <summary>Begun on first use, since the listener and the hooks each may be first to need it.</summary>
    public static ScenarioRun Run(IScenarioContext context, RunRecord record)
    {
        var state = State(context);
        if (state.Run is { } existing) return existing;
        var feature = context.ScenarioContainer.Resolve<FeatureContext>().FeatureInfo;
        var run = record.Begin(feature.Title, context.ScenarioInfo.Title, context.ScenarioInfo.CombinedTags ?? [],
            DateTimeOffset.UtcNow);
        state.Run = run;
        return run;
    }

    /// <summary>As the Reqnroll report shows it, so the two read the same.</summary>
    public static string? ErrorText(Exception? error)
    {
        if (error is null) return null;
        return string.IsNullOrEmpty(error.StackTrace) ? error.Message : error.Message + "\n\n" + error.StackTrace;
    }

    public static string Status(ScenarioExecutionStatus status) => status switch
    {
        ScenarioExecutionStatus.OK => RunStatus.Passed,
        ScenarioExecutionStatus.TestError => RunStatus.Failed,
        ScenarioExecutionStatus.BindingError => RunStatus.Failed,
        ScenarioExecutionStatus.UndefinedStep => RunStatus.Undefined,
        ScenarioExecutionStatus.StepDefinitionPending => RunStatus.Pending,
        _ => RunStatus.Skipped,
    };
}
