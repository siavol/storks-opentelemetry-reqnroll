namespace Storks.OpenTelemetry.Reqnroll.Core;

/// <summary>
/// Checks scenario windows for Window Overlap. Reqnroll-independent so it can be tested directly.
/// Failures are reported through <see cref="PluginDiagnostics"/> and never propagate: they must not change a result.
/// </summary>
internal sealed class RunTracker(PluginDiagnostics diagnostics, IWindowOverlap? overlap = null)
{
    private readonly IWindowOverlap _overlap = overlap ?? new WindowOverlap();

    /// <summary>The scenario's Observation Window opened; warns about Window Overlap (Attribution is unchanged).</summary>
    public void WindowOpened(ScenarioRun run)
    {
        try
        {
            if (_overlap.Opened(run) is { } warning) diagnostics.Report(warning);
        }
        catch (Exception ex) { diagnostics.Report($"failed to check Window Overlap of scenario '{run.Name}': {ex.Message}"); }
    }

    public void WindowClosed(ScenarioRun run)
    {
        try { _overlap.Closed(run); }
        catch (Exception ex) { diagnostics.Report($"failed to check Window Overlap of scenario '{run.Name}': {ex.Message}"); }
    }

    public string? WindowOverlapSummary()
    {
        try { return _overlap.Summary(); }
        catch (Exception ex)
        {
            diagnostics.Report($"failed to summarize Window Overlap: {ex.Message}");
            return null;
        }
    }
}
