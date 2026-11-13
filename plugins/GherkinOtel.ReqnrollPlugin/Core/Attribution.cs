namespace GherkinOtel.ReqnrollPlugin;

public static class Attribution
{
    /// <summary>Splits telemetry into what started in the window (chronological) and the remainder.</summary>
    public static AttributionResult Attribute(IEnumerable<TelemetryLog> logs, ObservationWindow window)
    {
        var inside = new List<TelemetryLog>();
        var rest = new List<TelemetryLog>();
        foreach (var log in logs) (window.Contains(log.Timestamp) ? inside : rest).Add(log);
        inside.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        return new AttributionResult(new ScenarioTelemetry(window, inside), rest);
    }
}
