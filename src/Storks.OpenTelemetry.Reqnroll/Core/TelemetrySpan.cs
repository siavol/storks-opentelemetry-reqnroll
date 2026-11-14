namespace Storks.OpenTelemetry.Reqnroll;

public enum SpanKind { Unspecified, Internal, Server, Client, Producer, Consumer }

public enum SpanStatus { Unset, Ok, Error }

/// <summary>Neutral span; independent of OTLP and Activity. <c>End</c> is null when the span is not finished.</summary>
public sealed record TelemetrySpan(
    string TraceId,
    string SpanId,
    string? ParentSpanId,
    string Name,
    string Service,
    SpanKind Kind,
    DateTimeOffset Start,
    DateTimeOffset? End,
    SpanStatus Status,
    IReadOnlyDictionary<string, string> Attributes);
