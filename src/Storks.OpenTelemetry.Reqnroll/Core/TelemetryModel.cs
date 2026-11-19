namespace Storks.OpenTelemetry.Reqnroll.Core;

internal enum LogSeverity { Trace, Debug, Information, Warning, Error, Critical }

/// <summary>
/// Neutral log record. <see cref="Id"/> is opaque, given by the Telemetry Source; a log without one cannot be referred to
/// and is left out of the index.
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
