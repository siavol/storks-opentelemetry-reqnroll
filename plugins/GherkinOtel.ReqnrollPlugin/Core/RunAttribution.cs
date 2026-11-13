namespace GherkinOtel.ReqnrollPlugin;

/// <summary>Run-level attribution: the remainder across the windows of ALL scenarios of the run.</summary>
public static class RunAttribution
{
    /// <summary>
    /// Telemetry not contained in any scenario window (startup, schema creation, late data), chronological.
    /// Belonging is decided by timestamp (for spans: by start time), same rule as per-scenario attribution.
    /// </summary>
    public static IReadOnlyList<TelemetryLog> Unattributed(IEnumerable<TelemetryLog> logs, IReadOnlyCollection<ObservationWindow> scenarioWindows)
    {
        var rest = logs.Where(l => !scenarioWindows.Any(w => w.Contains(l.Timestamp))).ToList();
        rest.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        return rest;
    }
}
