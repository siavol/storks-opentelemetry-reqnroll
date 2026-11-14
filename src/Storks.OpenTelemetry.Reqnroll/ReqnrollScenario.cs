using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Reqnroll;

namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>What the plugin reads from Reqnroll about the scenario running now: its run in the record, statuses, errors.</summary>
internal static class ReqnrollScenario
{
    public static ScenarioState State(IScenarioContext context) => context.ScenarioContainer.Resolve<ScenarioState>();

    /// <summary>
    /// The run of this scenario in the record, begun on first use (on ScenarioStartedEvent); the plugin's BeforeScenario
    /// hook then opens its window (<see cref="TelemetryHooks.OpenWindow"/>).
    /// </summary>
    public static ScenarioRun Run(IScenarioContext context, RunRecord record)
    {
        var state = State(context);
        if (state.Run is { } existing) return existing;
        var feature = context.ScenarioContainer.Resolve<FeatureContext>().FeatureInfo;
        var run = record.Begin(feature.Title, context.ScenarioInfo.Title, context.ScenarioInfo.CombinedTags ?? [],
            DateTimeOffset.UtcNow, FeatureUri(feature));
        state.Run = run;
        return run;
    }

    /// <summary>
    /// Feature file path (<c>Features/Checkout.feature</c>) from the feature's Cucumber messages. Reqnroll 3.3.4 keeps them
    /// non-public (<c>FeatureInfo.FeatureCucumberMessages.Source.Uri</c>), so this goes by reflection and falls back to
    /// the feature folder; nothing here may fail the scenario.
    /// </summary>
    [SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "Reqnroll exposes the feature file path only through non-public members; read-only, and any failure falls back to the folder.")]
    private static string? FeatureUri(FeatureInfo feature)
    {
        try
        {
            const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            static object? Get(object? o, string name)
            {
                var value = o?.GetType().GetProperty(name, any)?.GetValue(o);
                return value is Delegate d && d.Method.GetParameters().Length == 0 ? d.DynamicInvoke() : value;
            }
            if (Get(Get(Get(feature, "FeatureCucumberMessages"), "Source"), "Uri") is string { Length: > 0 } uri) return uri;
        }
        catch
        {
            // fall through
        }
        return string.IsNullOrEmpty(feature.FolderPath) ? null : feature.FolderPath;
    }

    /// <summary>Message and stack trace of a step error, as the report shows it.</summary>
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
