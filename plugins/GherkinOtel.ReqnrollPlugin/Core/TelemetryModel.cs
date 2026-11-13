namespace GherkinOtel.ReqnrollPlugin;

public enum LogSeverity { Trace, Debug, Information, Warning, Error, Critical }

/// <summary>Neutral log record; independent of OTLP.</summary>
public sealed record TelemetryLog(
    DateTimeOffset Timestamp,
    string Service,
    LogSeverity Level,
    string Category,
    string Message,
    string? TraceId = null,
    string? SpanId = null);

/// <summary>Observation Window: [Start, End], both inclusive.</summary>
public readonly record struct ObservationWindow(DateTimeOffset Start, DateTimeOffset End)
{
    public bool Contains(DateTimeOffset t) => t >= Start && t <= End;
}

/// <summary>Scenario Telemetry: everything that started inside the window, chronological.</summary>
public sealed record ScenarioTelemetry(ObservationWindow Window, IReadOnlyList<TelemetryLog> Logs)
{
    /// <summary>Spans that started in the window, chronological.</summary>
    public IReadOnlyList<TelemetrySpan> Spans { get; init; } = [];
}

public sealed record AttributionResult(ScenarioTelemetry Scenario, IReadOnlyList<TelemetryLog> Remainder)
{
    public IReadOnlyList<TelemetrySpan> RemainderSpans { get; init; } = [];
}
