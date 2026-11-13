namespace GherkinOtel.ReqnrollPlugin;

/// <summary>Run-level attribution: the remainder across the windows of ALL scenarios of the run.</summary>
public static class RunAttribution
{
    /// <summary>
    /// Telemetry not contained in any scenario window (startup, schema creation, late data), chronological.
    /// Belonging is decided by timestamp (for spans: by start time), same rule as per-scenario attribution.
    /// </summary>
    public static IReadOnlyList<TelemetryLog> Unattributed(IEnumerable<TelemetryLog> logs, IReadOnlyCollection<ObservationWindow> scenarioWindows) =>
        Outside(logs, l => l.Timestamp, scenarioWindows);

    /// <summary>Spans whose start time is not contained in any scenario window, chronological.</summary>
    public static IReadOnlyList<TelemetrySpan> UnattributedSpans(IEnumerable<TelemetrySpan> spans, IReadOnlyCollection<ObservationWindow> scenarioWindows) =>
        Outside(spans, sp => sp.Start, scenarioWindows);

    private static List<T> Outside<T>(IEnumerable<T> items, Func<T, DateTimeOffset> time, IReadOnlyCollection<ObservationWindow> windows)
    {
        var rest = items.Where(i => !windows.Any(w => w.Contains(time(i)))).ToList();
        rest.Sort((a, b) => time(a).CompareTo(time(b)));
        return rest;
    }
}
