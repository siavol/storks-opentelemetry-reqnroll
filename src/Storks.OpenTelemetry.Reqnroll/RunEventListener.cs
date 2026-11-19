using Reqnroll;
using Reqnroll.Bindings;
using Reqnroll.Events;
using Storks.OpenTelemetry.Reqnroll.Core;

namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>
/// Writes the link as an <c>&lt;a href&gt;</c> log line via <c>WriteLine</c> (<c>text/x.cucumber.log+plain</c>) to the step or
/// hook currently running. Reqnroll's HTML report renders log messages as HTML, so the link is clickable there; an attached
/// file is not (<c>text/html</c> is shown as escaped source, <c>text/uri-list</c> is unreachable via AddAttachment).
/// </summary>
internal sealed class ReqnrollLinkWriter(IReqnrollOutputHelper output) : ILinkWriter
{
    public void WriteLink(string link) =>
        output.WriteLine($"<a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">Open telemetry</a>");
}

/// <summary>
/// Async listener: output added on <see cref="StepFinishedEvent"/> lands on the Gherkin step itself, not on a hook (output
/// written from an AfterStep hook would land on the hook's test step). Records the run (steps, hooks, statuses, windows)
/// and writes the step link. Never throws: errors go to <see cref="PluginDiagnostics"/>.
/// </summary>
internal sealed class RunEventListener(RunTracker tracker, RunRecord record, PluginDiagnostics diagnostics) : IAsyncExecutionEventListener
{
    /// <summary>
    /// Start of the hook running now. The listener belongs to one test thread, and its hooks (run, feature and scenario
    /// ones) run one after another, so one pending start is enough.
    /// </summary>
    private DateTimeOffset? _hookStart;

    public Task OnEventAsync(IExecutionEvent executionEvent)
    {
        try
        {
            switch (executionEvent)
            {
                case ScenarioStartedEvent started:
                    ReqnrollScenario.Run(started.ScenarioContext, record);
                    break;
                case StepStartedEvent started:
                    ReqnrollScenario.State(started.ScenarioContext).StepStart = DateTimeOffset.UtcNow;
                    break;
                case StepFinishedEvent finished:
                    OnStepFinished(finished);
                    break;
                case ScenarioFinishedEvent finished:
                    OnScenarioFinished(finished);
                    break;
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

    private void OnStepFinished(StepFinishedEvent finished)
    {
        var context = finished.ScenarioContext;
        var run = ReqnrollScenario.Run(context, record);
        var stepStart = ReqnrollScenario.State(context).StepStart ?? DateTimeOffset.UtcNow;
        var status = ReqnrollScenario.Status(finished.StepContext.Status);
        var error = status == RunStatus.Failed ? ReqnrollScenario.ErrorText(context.TestError) : null;
        var output = context.ScenarioContainer.Resolve<IReqnrollOutputHelper>();

        tracker.StepFinished(run, new ReqnrollLinkWriter(output),
            finished.StepContext.StepInfo.StepDefinitionType.ToString(), finished.StepContext.StepInfo.Text,
            stepStart, DateTimeOffset.UtcNow, status, error);
    }

    /// <summary>Hooks of a scenario; run and feature hooks lie outside scenario windows and go to the run record.</summary>
    private static bool IsScenarioHook(HookType type) => type is HookType.BeforeScenario or HookType.AfterScenario
        or HookType.BeforeScenarioBlock or HookType.AfterScenarioBlock or HookType.BeforeStep or HookType.AfterStep;

    private void OnHookFinished(HookBindingFinishedEvent finished)
    {
        var end = DateTimeOffset.UtcNow;
        var start = _hookStart ?? end - finished.Duration;
        _hookStart = null;
        var binding = finished.HookBinding;
        var hook = new HookRun(binding.HookType.ToString(), binding.Method.Type.Name + "." + binding.Method.Name, start, end,
            ReqnrollScenario.Status(finished.HookStatus));
        var run = IsScenarioHook(binding.HookType) ? ReqnrollScenario.Run(finished.ContextManager.ScenarioContext, record) : null;
        tracker.HookFinished(run, hook, AssemblyName(binding.Method.Type.AssemblyName));
    }

    /// <summary>Simple name of an assembly given by its full or simple name.</summary>
    private static string AssemblyName(string? name) => (name ?? "").Split(',')[0].Trim();

    private void OnScenarioFinished(ScenarioFinishedEvent finished)
    {
        var context = finished.ScenarioContext;
        var run = ReqnrollScenario.Run(context, record);
        var status = ReqnrollScenario.Status(context.ScenarioExecutionStatus);
        tracker.ScenarioFinished(run, DateTimeOffset.UtcNow, status, status == RunStatus.Failed ? ReqnrollScenario.ErrorText(context.TestError) : null);
    }
}
