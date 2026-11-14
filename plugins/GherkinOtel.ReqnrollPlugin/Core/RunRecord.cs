namespace GherkinOtel.ReqnrollPlugin;

/// <summary>One Gherkin step of one scenario run, as it actually ran.</summary>
public sealed record StepRun(int Number, string Keyword, string Text, DateTimeOffset Start, DateTimeOffset End, string Status, string? Error);

/// <summary>One call of a hook of the test project (a scenario hook such as BeforeScenario or AfterStep, or a run or feature hook): type, method (<c>Class.Method</c>) and window.</summary>
public sealed record HookRun(string HookType, string Name, DateTimeOffset Start, DateTimeOffset End, string Status);

/// <summary>
/// One run of a scenario. Outline examples and retries are separate runs, each with its own <see cref="Id"/>.
/// A scenario runs on a single thread, so the run itself needs no locking.
/// </summary>
public sealed class ScenarioRun
{
    private readonly List<StepRun> _steps = [];
    private readonly List<HookRun> _hooks = [];

    internal ScenarioRun(string feature, string name, IReadOnlyList<string> tags, DateTimeOffset start, string? featureUri = null)
    {
        Feature = feature;
        FeatureUri = featureUri;
        Name = name;
        Tags = tags;
        Start = start;
        End = start;
    }

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Feature { get; }

    /// <summary>Path of the feature file as in Cucumber messages (e.g. <c>Features/Checkout.feature</c>), if known.</summary>
    public string? FeatureUri { get; }
    public string Name { get; }
    public IReadOnlyList<string> Tags { get; }
    /// <summary>Start of the Observation Window: when the plugin's BeforeScenario hook opened it (<see cref="OpenWindow"/>).</summary>
    public DateTimeOffset Start { get; private set; }

    /// <summary>End of the Observation Window: when the plugin's AfterScenario hook closed it, else when the scenario finished.</summary>
    public DateTimeOffset End { get; private set; }
    public string Status { get; private set; } = RunStatus.Passed;
    public string? Error { get; private set; }
    public IReadOnlyList<StepRun> Steps => _steps;

    /// <summary>Scenario hook calls in order of call.</summary>
    public IReadOnlyList<HookRun> Hooks => _hooks;

    /// <summary>
    /// Appends a step; steps are numbered from 1 in order of completion. Its window does not include its BeforeStep and
    /// AfterStep hooks: Reqnroll 3.3.4 runs them before StepStarted and after StepFinished.
    /// </summary>
    public int AddStep(string keyword, string text, DateTimeOffset start, DateTimeOffset end, string status, string? error)
    {
        var number = _steps.Count + 1;
        _steps.Add(new StepRun(number, keyword, text, start, end, status, error));
        return number;
    }

    public void AddHook(HookRun hook) => _hooks.Add(hook);

    private bool _closed;

    public void OpenWindow(DateTimeOffset start) => Start = start;

    /// <summary>Closes the window before the plugin's own AfterScenario hooks; the scenario finishes after them.</summary>
    public void CloseWindow(DateTimeOffset end)
    {
        End = end;
        _closed = true;
    }

    /// <param name="end">End of the window unless <see cref="CloseWindow"/> has closed it already.</param>
    public void Finish(DateTimeOffset end, string status, string? error)
    {
        if (!_closed) End = end;
        Status = status;
        Error = error;
    }
}

/// <summary>
/// Thread-safe record of the test run (the source of the run index): every scenario run and the run and feature hook
/// calls (BeforeTestRun, BeforeFeature, AfterFeature, AfterTestRun) of the test project. <see cref="Start"/> is the start
/// of the run's scale in the viewer: the record is created when the plugin starts, before any hook runs.
/// </summary>
public sealed class RunRecord(DateTimeOffset start)
{
    private readonly object _gate = new();
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

    public ScenarioRun Begin(string feature, string name, IReadOnlyList<string> tags, DateTimeOffset start, string? featureUri = null)
    {
        var run = new ScenarioRun(feature, name, tags, start, featureUri);
        lock (_gate) _runs.Add(run);
        return run;
    }

    public IReadOnlyList<ScenarioRun> Snapshot()
    {
        lock (_gate) return _runs.ToArray();
    }
}
