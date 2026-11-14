namespace Storks.OpenTelemetry.Reqnroll;

internal enum LogSeverity { Trace, Debug, Information, Warning, Error, Critical }

/// <summary>
/// Neutral log record; independent of OTLP. <see cref="Id"/> is an opaque id given by the Telemetry Source, by which the
/// index and the viewer refer to the record (the OTLP receiver: its position among the received requests); a log without
/// an id cannot be referred to and is left out of the index.
/// </summary>
internal sealed record TelemetryLog(
    DateTimeOffset Timestamp,
    string Service,
    LogSeverity Level,
    string Category,
    string Message,
    string? TraceId = null,
    string? SpanId = null)
{
    public string? Id { get; init; }
}

/// <summary>Observation Window: [Start, End], both inclusive.</summary>
internal readonly record struct ObservationWindow(DateTimeOffset Start, DateTimeOffset End)
{
    public bool Contains(DateTimeOffset t) => t >= Start && t <= End;
}
