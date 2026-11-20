using Io.Cucumber.Messages.Types;
using Storks.OpenTelemetry.Reqnroll.Core;

namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>
/// Builds the scenario runs of the run record from the Cucumber Messages the formatter receives. Reqnroll sends a
/// scenario's messages only after it ends, and holds back a failed one's <c>TestCaseFinished</c> to the end of the run,
/// so the messages are only collected here and read all at once by <see cref="Record"/> on <c>TestRunFinished</c>.
/// Only reads the messages: their constructors differ between Reqnroll versions.
/// </summary>
internal sealed class RunMessages
{
    private readonly Dictionary<string, string> _featureNames = [];
    private readonly Dictionary<string, string> _keywords = [];
    private readonly Dictionary<string, Pickle> _pickles = [];
    private readonly Dictionary<string, Hook> _hooks = [];
    private readonly Dictionary<string, TestCase> _testCases = [];
    private readonly List<TestCaseStarted> _started = [];
    private readonly Dictionary<string, Timestamp> _finished = [];
    private readonly Dictionary<(string TestCaseStartedId, string TestStepId), Timestamp> _stepStarted = [];
    private readonly Dictionary<(string TestCaseStartedId, string TestStepId), TestStepFinished> _stepFinished = [];
    private readonly List<TestRunHookStarted> _runHookStarted = [];
    private readonly List<TestRunHookFinished> _runHookFinished = [];

    public void Add(Envelope envelope)
    {
        if (envelope.GherkinDocument is { } document) AddDocument(document);
        else if (envelope.Pickle is { } pickle) _pickles[pickle.Id] = pickle;
        else if (envelope.Hook is { } hook) _hooks[hook.Id] = hook;
        else if (envelope.TestCase is { } testCase) _testCases[testCase.Id] = testCase;
        else if (envelope.TestCaseStarted is { } started) _started.Add(started);
        else if (envelope.TestCaseFinished is { } finished) _finished[finished.TestCaseStartedId] = finished.Timestamp;
        else if (envelope.TestStepStarted is { } stepStarted) _stepStarted[(stepStarted.TestCaseStartedId, stepStarted.TestStepId)] = stepStarted.Timestamp;
        else if (envelope.TestStepFinished is { } stepFinished) _stepFinished[(stepFinished.TestCaseStartedId, stepFinished.TestStepId)] = stepFinished;
        else if (envelope.TestRunHookStarted is { } hookStarted) _runHookStarted.Add(hookStarted);
        else if (envelope.TestRunHookFinished is { } hookFinished) _runHookFinished.Add(hookFinished);
    }

    private void AddDocument(GherkinDocument document)
    {
        if (document.Feature is not { } feature) return;
        _featureNames[document.Uri] = feature.Name;
        foreach (var child in feature.Children)
        {
            AddSteps(child.Background?.Steps);
            AddSteps(child.Scenario?.Steps);
            foreach (var ruleChild in child.Rule?.Children ?? [])
            {
                AddSteps(ruleChild.Background?.Steps);
                AddSteps(ruleChild.Scenario?.Steps);
            }
        }
    }

    private void AddSteps(IEnumerable<Step>? steps)
    {
        foreach (var step in steps ?? []) _keywords[step.Id] = step.Keyword.Trim();
    }

    /// <summary>
    /// Adds a scenario run per <c>TestCaseStarted</c> (so every retry attempt is a run of its own) and passes the
    /// scenario windows to the Window Overlap check in time order; adds the run and feature hook calls. A test case run
    /// or hook call that cannot be read is reported and skipped.
    /// </summary>
    public void Record(RunRecord record, RunTracker tracker, PluginDiagnostics diagnostics)
    {
        try { RecordRunHooks(record); }
        catch (System.Exception ex) { diagnostics.Report("failed to read run and feature hooks from Cucumber Messages", ex.Message); }

        var runs = new List<ScenarioRun>();
        foreach (var started in _started.OrderBy(s => Time(s.Timestamp)))
        {
            try { runs.Add(Run(record, started)); }
            catch (System.Exception ex) { diagnostics.Report("failed to read a test case run from Cucumber Messages", $"{started.Id}: {ex.Message}"); }
        }

        // At the same instant a window closes before another opens: they only touch.
        foreach (var (_, opens, run) in runs.SelectMany(r => new[] { (r.Start, true, r), (r.End, false, r) }).OrderBy(e => e.Item1).ThenBy(e => e.Item2))
        {
            if (opens) tracker.WindowOpened(run);
            else tracker.WindowClosed(run);
        }
    }

    /// <summary>
    /// Pairs each hook finish with the earliest still open start of the same hook, else with the start it names: Reqnroll's
    /// finish of a repeated [BeforeFeature]/[AfterFeature] names the first call's start. Exact when hooks run one after another;
    /// in a parallel run calls of the same hook may be mixed up. A start without a finish is left out.
    /// </summary>
    private void RecordRunHooks(RunRecord record)
    {
        var starts = _runHookStarted.ToDictionary(s => s.Id);
        var open = _runHookStarted.OrderBy(s => Time(s.Timestamp)).ToList();
        foreach (var finished in _runHookFinished.OrderBy(f => Time(f.Timestamp)))
        {
            if (!starts.TryGetValue(finished.TestRunHookStartedId, out var named)) continue;
            var started = open.FirstOrDefault(s => s.HookId == named.HookId) ?? named;
            open.Remove(started);
            if (!_hooks.TryGetValue(started.HookId, out var hook)) continue;
            record.AddRunHook(new HookRun(HookType(hook.Type), HookName(hook), Time(started.Timestamp), Time(finished.Timestamp),
                Status(finished.Result.Status)));
        }
    }

    private ScenarioRun Run(RunRecord record, TestCaseStarted started)
    {
        var testCase = _testCases[started.TestCaseId];
        var pickle = _pickles[testCase.PickleId];
        var run = record.Begin(_featureNames.GetValueOrDefault(pickle.Uri, ""), pickle.Name,
            pickle.Tags.Select(t => t.Name.TrimStart('@')).ToList(), Time(started.Timestamp));

        var results = new List<(string Status, string? Error)>();
        foreach (var testStep in testCase.TestSteps)
        {
            if (!_stepStarted.TryGetValue((started.Id, testStep.Id), out var stepStart)
                || !_stepFinished.TryGetValue((started.Id, testStep.Id), out var stepFinished))
            {
                continue;
            }
            var (start, end) = (Time(stepStart), Time(stepFinished.Timestamp));
            var result = stepFinished.TestStepResult;
            var status = Status(result.Status);
            var error = status == RunStatus.Failed ? ErrorText(result) : null;
            results.Add((status, error));
            if (testStep.PickleStepId is { } pickleStepId)
            {
                var pickleStep = pickle.Steps.First(s => s.Id == pickleStepId);
                run.AddStep(_keywords.GetValueOrDefault(pickleStep.AstNodeIds.FirstOrDefault() ?? "", ""), pickleStep.Text, start, end, status, error);
            }
            else if (testStep.HookId is { } hookId && _hooks.TryGetValue(hookId, out var hook))
            {
                run.AddHook(new HookRun(HookType(hook.Type), HookName(hook), start, end, status));
            }
        }

        // A failed scenario's TestCaseFinished may be missing if the run was cut short.
        var finish = _finished.TryGetValue(started.Id, out var finished) ? Time(finished) : run.Steps.Select(s => s.End).Append(run.Start).Max();
        var first = results.FirstOrDefault(r => r.Status != RunStatus.Passed);
        run.Finish(finish, first.Status ?? RunStatus.Passed, first.Error);
        return run;
    }

    private static DateTimeOffset Time(Timestamp t) =>
        DateTimeOffset.UnixEpoch.AddSeconds(t.Seconds).AddTicks(t.Nanos / 100);

    /// <summary>As the Reqnroll report shows it: the message, then the stack trace; the exception type if there is no message (Reqnroll 3.0.x).</summary>
    private static string? ErrorText(TestStepResult result)
    {
        var exception = result.Exception;
        var message = new[] { exception?.Message, result.Message, exception?.Type }.FirstOrDefault(m => !string.IsNullOrEmpty(m));
        var stackTrace = exception?.StackTrace;
        if (message is null) return string.IsNullOrEmpty(stackTrace) ? null : stackTrace;
        return string.IsNullOrEmpty(stackTrace) ? message : message + "\n\n" + stackTrace;
    }

    private static string Status(TestStepResultStatus status) => status switch
    {
        TestStepResultStatus.PASSED => RunStatus.Passed,
        TestStepResultStatus.FAILED or TestStepResultStatus.AMBIGUOUS => RunStatus.Failed,
        TestStepResultStatus.UNDEFINED => RunStatus.Undefined,
        TestStepResultStatus.PENDING => RunStatus.Pending,
        _ => RunStatus.Skipped,
    };

    /// <summary>Reqnroll's hook names; run and feature hooks are both BEFORE_TEST_RUN/AFTER_TEST_RUN in the messages.</summary>
    private static string HookType(Io.Cucumber.Messages.Types.HookType? type) => type switch
    {
        Io.Cucumber.Messages.Types.HookType.BEFORE_TEST_CASE => "BeforeScenario",
        Io.Cucumber.Messages.Types.HookType.AFTER_TEST_CASE => "AfterScenario",
        Io.Cucumber.Messages.Types.HookType.BEFORE_TEST_STEP => "BeforeStep",
        Io.Cucumber.Messages.Types.HookType.AFTER_TEST_STEP => "AfterStep",
        Io.Cucumber.Messages.Types.HookType.BEFORE_TEST_RUN => "BeforeTestRun",
        Io.Cucumber.Messages.Types.HookType.AFTER_TEST_RUN => "AfterTestRun",
        _ => type?.ToString() ?? "",
    };

    /// <summary><c>Class.Method</c>; Reqnroll writes the class name as the assembly name, a dot, the full type name.</summary>
    private static string HookName(Hook hook)
    {
        if (hook.SourceReference?.JavaMethod is not { } method) return hook.Name ?? hook.Id;
        var className = method.ClassName;
        return className[(className.LastIndexOfAny(['.', '+']) + 1)..] + "." + method.MethodName;
    }
}
