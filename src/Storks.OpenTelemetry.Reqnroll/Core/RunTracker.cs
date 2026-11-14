namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>Writes a link into the test report as a log message of the step or hook currently running.</summary>
internal interface ILinkWriter
{
    void WriteLink(string link);
}

/// <summary>
/// Records the run and hands out links. Reqnroll-independent so it can be tested directly.
/// Failures are reported through <see cref="PluginDiagnostics"/> and never propagate: they must not change a result.
/// </summary>
internal sealed class RunTracker(RunRecord record, PluginDiagnostics diagnostics, IWindowOverlap? overlap = null)
{
    private readonly IWindowOverlap _overlap = overlap ?? new WindowOverlap();

    private static readonly string PluginAssembly = typeof(RunTracker).Assembly.GetName().Name!;

    public void StepFinished(ScenarioRun run, ILinkWriter writer, string keyword, string text,
        DateTimeOffset start, DateTimeOffset end, string status, string? error)
    {
        int number;
        try { number = run.AddStep(keyword, text, start, end, status, error); }
        catch (Exception ex) { diagnostics.Report($"failed to record step '{text}': {ex.Message}"); return; }
        Write(writer, TelemetryLinks.Step(run.Id, number), $"step '{text}'");
    }

    /// <summary>
    /// Records a finished hook call of the test project: a scenario hook on its <paramref name="run"/>, or, when
    /// <paramref name="run"/> is null, a run or feature hook on the run record. The plugin's own hooks are not recorded
    /// (<paramref name="assembly"/> is the hook's assembly name).
    /// </summary>
    public void HookFinished(ScenarioRun? run, HookRun hook, string assembly)
    {
        if (assembly == PluginAssembly) return;
        try
        {
            if (run is null) record.AddRunHook(hook);
            else run.AddHook(hook);
        }
        catch (Exception ex) { diagnostics.Report($"failed to record hook '{hook.Name}': {ex.Message}"); }
    }

    /// <summary>Opens the scenario's Observation Window and warns about Window Overlap (Attribution is unchanged).</summary>
    public void WindowOpened(ScenarioRun run, DateTimeOffset start)
    {
        run.OpenWindow(start);
        try
        {
            if (_overlap.Opened(run) is { } warning) diagnostics.Report(warning);
        }
        catch (Exception ex) { diagnostics.Report($"failed to check Window Overlap of scenario '{run.Name}': {ex.Message}"); }
    }

    /// <summary>Closes the scenario's Observation Window, before the plugin's own AfterScenario hooks.</summary>
    public void WindowClosed(ScenarioRun run, DateTimeOffset end)
    {
        run.CloseWindow(end);
        LeaveOverlapCheck(run);
    }

    /// <summary>The one end-of-run line about Window Overlap, or null when no windows overlapped.</summary>
    public string? WindowOverlapSummary()
    {
        try { return _overlap.Summary(); }
        catch (Exception ex) { return $"failed to summarize Window Overlap: {ex.Message}"; }
    }

    private void LeaveOverlapCheck(ScenarioRun run)
    {
        try { _overlap.Closed(run); }
        catch (Exception ex) { diagnostics.Report($"failed to check Window Overlap of scenario '{run.Name}': {ex.Message}"); }
    }

    public void WriteScenarioLink(ScenarioRun run, ILinkWriter writer) =>
        Write(writer, TelemetryLinks.Scenario(run.Id), $"scenario '{run.Name}'");

    public void ScenarioFinished(ScenarioRun run, DateTimeOffset end, string status, string? error)
    {
        try { run.Finish(end, status, error); }
        catch (Exception ex) { diagnostics.Report($"failed to record scenario '{run.Name}': {ex.Message}"); }
        // A scenario whose closing hook did not run must not count as running for the rest of the run.
        LeaveOverlapCheck(run);
    }

    private void Write(ILinkWriter writer, string link, string what)
    {
        try { writer.WriteLink(link); }
        catch (Exception ex) { diagnostics.Report($"failed to write telemetry link of {what}: {ex.Message}"); }
    }
}
