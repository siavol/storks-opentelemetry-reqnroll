using Reqnroll;
using Reqnroll.Bindings;
using Reqnroll.Events;
using Storks.OpenTelemetry.Reqnroll.Core;

namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>
/// Records the run and feature hooks from Reqnroll's events; scenarios, steps and their hooks come from the Cucumber
/// Messages (<see cref="RunMessages"/>). Never throws: a plugin error must not change a test result.
/// </summary>
internal sealed class RunEventListener(RunTracker tracker, PluginDiagnostics diagnostics) : IAsyncExecutionEventListener
{
    /// <summary>
    /// Start of the hook running now. The listener belongs to one test thread, and its hooks run one after another,
    /// so one pending start is enough.
    /// </summary>
    private DateTimeOffset? _hookStart;

    public Task OnEventAsync(IExecutionEvent executionEvent)
    {
        try
        {
            switch (executionEvent)
            {
                case HookBindingStartedEvent:
                    _hookStart = DateTimeOffset.UtcNow;
                    break;
                case HookBindingFinishedEvent finished:
                    OnHookFinished(finished);
                    break;
            }
        }
        catch (Exception ex)
        {
            diagnostics.Report($"run listener failed on {executionEvent.GetType().Name}: {ex}");
        }
        return Task.CompletedTask;
    }

    private static bool IsRunOrFeatureHook(HookType type) =>
        type is HookType.BeforeTestRun or HookType.AfterTestRun or HookType.BeforeFeature or HookType.AfterFeature;

    private void OnHookFinished(HookBindingFinishedEvent finished)
    {
        var end = DateTimeOffset.UtcNow;
        var start = _hookStart ?? end - finished.Duration;
        _hookStart = null;
        var binding = finished.HookBinding;
        if (!IsRunOrFeatureHook(binding.HookType)) return;
        var hook = new HookRun(binding.HookType.ToString(), binding.Method.Type.Name + "." + binding.Method.Name, start, end,
            Status(finished.HookStatus));
        tracker.HookFinished(hook, AssemblyName(binding.Method.Type.AssemblyName));
    }

    private static string AssemblyName(string? name) => (name ?? "").Split(',')[0].Trim();

    private static string Status(ScenarioExecutionStatus status) => status switch
    {
        ScenarioExecutionStatus.OK => RunStatus.Passed,
        ScenarioExecutionStatus.TestError => RunStatus.Failed,
        ScenarioExecutionStatus.BindingError => RunStatus.Failed,
        ScenarioExecutionStatus.UndefinedStep => RunStatus.Undefined,
        ScenarioExecutionStatus.StepDefinitionPending => RunStatus.Pending,
        _ => RunStatus.Skipped,
    };
}
