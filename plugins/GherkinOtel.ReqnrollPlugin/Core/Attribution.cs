namespace GherkinOtel.ReqnrollPlugin;

public static class Attribution
{
    /// <summary>Splits telemetry into what started in the window (chronological) and the remainder.</summary>
    public static AttributionResult Attribute(IEnumerable<TelemetryLog> logs, IEnumerable<TelemetrySpan> spans, ObservationWindow window)
    {
        var (logsInside, logsRest) = Partition(logs, l => l.Timestamp, window);
        var (spansInside, spansRest) = Partition(spans, sp => sp.Start, window);
        return new AttributionResult(new ScenarioTelemetry(window, logsInside) { Spans = spansInside }, logsRest) { RemainderSpans = spansRest };
    }

    /// <summary>Items whose time is in the window, sorted by time, and all the others (in input order).</summary>
    private static (List<T> Inside, List<T> Outside) Partition<T>(IEnumerable<T> items, Func<T, DateTimeOffset> time, ObservationWindow window)
    {
        var inside = new List<T>();
        var rest = new List<T>();
        foreach (var item in items) (window.Contains(time(item)) ? inside : rest).Add(item);
        inside.Sort((a, b) => time(a).CompareTo(time(b)));
        return (inside, rest);
    }
}
