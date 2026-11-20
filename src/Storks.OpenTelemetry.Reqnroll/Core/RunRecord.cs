namespace Storks.OpenTelemetry.Reqnroll.Core;

/// <summary>One Gherkin step of one scenario run, as it actually ran.</summary>
internal sealed record StepRun(int Number, string Keyword, string Text, DateTimeOffset Start, DateTimeOffset End, string Status, string? Error);

/// <summary>One call of a hook of the test project (a scenario hook such as BeforeScenario or AfterStep, or a run or feature hook): type, method (<c>Class.Method</c>) and window.</summary>
internal sealed record HookRun(string HookType, string Name, DateTimeOffset Start, DateTimeOffset End, string Status);

/// <summary>
/// One run of a scenario. Outline examples and retries are separate runs, each with its own <see cref="Id"/>.
/// A run is built by one thread from the Cucumber Messages, so the run itself needs no locking.
/// </summary>
internal sealed class ScenarioRun
{
    private readonly List<StepRun> _steps = [];
    private readonly List<HookRun> _hooks = [];

    internal ScenarioRun(string feature, string name, IReadOnlyList<string> tags, DateTimeOffset start)
    {
        Feature = feature;
        Name = name;
        Tags = tags;
        Start = start;
        End = start;
    }

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Feature { get; }

    public string Name { get; }
    public IReadOnlyList<string> Tags { get; }
    /// <summary>Start of the Observation Window: <c>TestCaseStarted</c>.</summary>
    public DateTimeOffset Start { get; }

    /// <summary>End of the Observation Window: <c>TestCaseFinished</c>.</summary>
    public DateTimeOffset End { get; private set; }
    public string Status { get; private set; } = RunStatus.Passed;
    public string? Error { get; private set; }
    public IReadOnlyList<StepRun> Steps => _steps;

    /// <summary>Scenario hook calls in order of call.</summary>
    public IReadOnlyList<HookRun> Hooks => _hooks;

    /// <summary>
    /// Appends a step; steps are numbered from 1 in the order of the test case. Its window does not include its BeforeStep and
    /// AfterStep hooks: they are test steps of their own.
    /// </summary>
    public int AddStep(string keyword, string text, DateTimeOffset start, DateTimeOffset end, string status, string? error)
    {
        var number = _steps.Count + 1;
        _steps.Add(new StepRun(number, keyword, text, start, end, status, error));
        return number;
    }

    public void AddHook(HookRun hook) => _hooks.Add(hook);

    /// <summary>Ends the scenario window.</summary>
    public void Finish(DateTimeOffset end, string status, string? error)
    {
        End = end;
        Status = status;
        Error = error;
    }
}

/// <summary>
/// Thread-safe record of the test run (the source of the run index): every scenario run and the run and feature hook
/// calls (BeforeTestRun, BeforeFeature, AfterFeature, AfterTestRun) of the test project. <see cref="Start"/> is the start
/// of the run's scale in the viewer: the record is created when the plugin starts, before any hook runs.
/// </summary>
internal sealed class RunRecord(DateTimeOffset start)
{
    private readonly Lock _gate = new();
    private readonly List<ScenarioRun> _runs = [];
    private readonly List<HookRun> _runHooks = [];

    public RunRecord() : this(DateTimeOffset.UtcNow) { }

    public DateTimeOffset Start { get; } = start;

    public void AddRunHook(HookRun hook)
    {
        lock (_gate) _runHooks.Add(hook);
    }

    /// <summary>Run and feature hook calls, by start time.</summary>
    public IReadOnlyList<HookRun> RunHooks()
    {
        lock (_gate) return _runHooks.OrderBy(h => h.Start).ToArray();
    }

    public ScenarioRun Begin(string feature, string name, IReadOnlyList<string> tags, DateTimeOffset start)
    {
        var run = new ScenarioRun(feature, name, tags, start);
        lock (_gate) _runs.Add(run);
        return run;
    }

    public IReadOnlyList<ScenarioRun> Snapshot()
    {
        lock (_gate) return _runs.ToArray();
    }
}
