namespace GherkinOtel.ReqnrollPlugin;

public static class Attribution
{
    /// <summary>Splits telemetry into what started in the window (chronological) and the remainder.</summary>
    public static AttributionResult Attribute(IEnumerable<TelemetryLog> logs, ObservationWindow window) =>
        Attribute(logs, [], window);

    public static AttributionResult Attribute(IEnumerable<TelemetryLog> logs, IEnumerable<TelemetrySpan> spans, ObservationWindow window)
    {
        var inside = new List<TelemetryLog>();
        var rest = new List<TelemetryLog>();
        foreach (var log in logs) (window.Contains(log.Timestamp) ? inside : rest).Add(log);
        inside.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        var spansInside = new List<TelemetrySpan>();
        var spansRest = new List<TelemetrySpan>();
        foreach (var span in spans) (window.Contains(span.Start) ? spansInside : spansRest).Add(span);
        spansInside.Sort((a, b) => a.Start.CompareTo(b.Start));

        return new AttributionResult(new ScenarioTelemetry(window, inside) { Spans = spansInside }, rest) { RemainderSpans = spansRest };
    }
}
