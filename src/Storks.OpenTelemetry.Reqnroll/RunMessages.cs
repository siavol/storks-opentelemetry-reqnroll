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
    /// Adds a scenario run per test case attempt and the run and feature hook calls, and passes the scenario windows to
    /// the Window Overlap check. What cannot be read is reported and skipped.
    /// </summary>
    public void Record(RunRecord record, RunTracker tracker, PluginDiagnostics diagnostics)
    {
        RecordRunHooks(record, diagnostics);

        var runs = new List<ScenarioRun>();
        foreach (var started in _started.OrderBy(s => Order(s.Timestamp)))
        {
            try { runs.Add(Run(record, started)); }
            catch (System.Exception ex) { diagnostics.Report("failed to read a test case run from Cucumber Messages", $"{started.Id}: {ex.Message}"); }
        }

        // At the same instant a window closes before another opens: they only touch.
        var edges = runs.SelectMany(r => new[] { (At: r.Start, Opens: true, Run: r), (At: r.End, Opens: false, Run: r) });
        foreach (var (_, opens, run) in edges.OrderBy(e => e.At).ThenBy(e => e.Opens))
        {
            if (opens) tracker.WindowOpened(run);
            else tracker.WindowClosed(run);
        }
    }

    /// <summary>
    /// Pairs a hook finish with the earliest open start of the same hook: Reqnroll's finish of a repeated
    /// [BeforeFeature]/[AfterFeature] names the first call's start. Calls of the same hook may be mixed up in a parallel run.
    /// </summary>
    private void RecordRunHooks(RunRecord record, PluginDiagnostics diagnostics)
    {
        var starts = _runHookStarted.DistinctBy(s => s.Id).ToDictionary(s => s.Id);
        var open = _runHookStarted.OrderBy(s => Order(s.Timestamp)).ToList();
        foreach (var finished in _runHookFinished.OrderBy(f => Order(f.Timestamp)))
        {
            try
            {
                if (!starts.TryGetValue(finished.TestRunHookStartedId, out var named)) continue;
                var started = open.FirstOrDefault(s => s.HookId == named.HookId) ?? named;
                open.Remove(started);
                if (!_hooks.TryGetValue(started.HookId, out var hook)) continue;
                record.AddRunHook(new HookRun(HookTypeName(hook.Type), HookName(hook), Time(started.Timestamp), Time(finished.Timestamp),
                    Status(finished.Result.Status)));
            }
            catch (System.Exception ex)
            {
                diagnostics.Report("failed to read a run or feature hook call from Cucumber Messages", $"{finished.TestRunHookStartedId}: {ex.Message}");
            }
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
                run.AddHook(new HookRun(HookTypeName(hook.Type), HookName(hook), start, end, status));
            }
        }

        // A failed scenario's TestCaseFinished may be missing if the run was cut short.
        var finish = _finished.TryGetValue(started.Id, out var finished) ? Time(finished) : run.Steps.Select(s => s.End).Append(run.Start).Max();
        var first = results.FirstOrDefault(r => r.Status != RunStatus.Passed);
        run.Finish(finish, first.Status ?? RunStatus.Passed, first.Error);
        return run;
    }

    // Sorting must not throw on a timestamp out of DateTimeOffset's range: that message alone is reported.
    private static (long, long) Order(Timestamp t) => (t.Seconds, t.Nanos);

    private static DateTimeOffset Time(Timestamp t) =>
        DateTimeOffset.UnixEpoch.AddSeconds(t.Seconds).AddTicks(t.Nanos / 100);

    /// <summary>The exception message and stack trace; the exception type stands in for a message Reqnroll 3.0.x leaves empty.</summary>
    private static string? ErrorText(TestStepResult result)
    {
        var exception = result.Exception;
        var headline = string.IsNullOrEmpty(exception?.Message) ? exception?.Type : exception.Message;
        var text = string.Join("\n\n", new[] { headline, exception?.StackTrace }.Where(s => !string.IsNullOrEmpty(s)));
        return text.Length > 0 ? text : NullIfEmpty(result.Message);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    private static string Status(TestStepResultStatus status) => status switch
    {
        TestStepResultStatus.PASSED => RunStatus.Passed,
        TestStepResultStatus.FAILED or TestStepResultStatus.AMBIGUOUS => RunStatus.Failed,
        TestStepResultStatus.UNDEFINED => RunStatus.Undefined,
        TestStepResultStatus.PENDING => RunStatus.Pending,
        _ => RunStatus.Skipped,
    };

    /// <summary>Reqnroll's hook names; run and feature hooks are both BEFORE_TEST_RUN/AFTER_TEST_RUN in the messages.</summary>
    private static string HookTypeName(HookType? type) => type switch
    {
        HookType.BEFORE_TEST_CASE => "BeforeScenario",
        HookType.AFTER_TEST_CASE => "AfterScenario",
        HookType.BEFORE_TEST_STEP => "BeforeStep",
        HookType.AFTER_TEST_STEP => "AfterStep",
        HookType.BEFORE_TEST_RUN => "BeforeTestRun",
        HookType.AFTER_TEST_RUN => "AfterTestRun",
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
