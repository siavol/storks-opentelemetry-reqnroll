namespace GherkinOtel.ReqnrollPlugin;

public static class StepSlice
{
    /// <summary>
    /// Slice: the subset of Scenario Telemetry that started inside <paramref name="window"/>.
    /// A filter over the already collected telemetry, not a separate collection.
    /// </summary>
    public static ScenarioTelemetry Slice(this ScenarioTelemetry telemetry, ObservationWindow window) =>
        telemetry with
        {
            Window = window,
            Logs = telemetry.Logs.Where(l => window.Contains(l.Timestamp)).ToList(),
            Spans = telemetry.Spans.Where(sp => window.Contains(sp.Start)).ToList(),
        };
}
