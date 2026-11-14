using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;

namespace GherkinOtel.ReqnrollPlugin;

/// <summary>OTLP protobuf -> neutral model. The only place that knows OTLP types.</summary>
internal static class OtlpMapper
{
    /// <summary>
    /// Id of a log record among the received log requests: its position "request.resourceLogs.scopeLogs.logRecords"
    /// (all zero-based), so the viewer finds the record in the embedded OTLP/JSON array of requests.
    /// </summary>
    public static string LogId(int request, int resource, int scope, int record) => $"{request}.{resource}.{scope}.{record}";

    /// <summary>Logs of a request, each with its <see cref="LogId"/>; <paramref name="requestIndex"/> is the position of the request among the received ones.</summary>
    public static IEnumerable<TelemetryLog> MapLogs(ExportLogsServiceRequest request, int requestIndex)
    {
        for (var ri = 0; ri < request.ResourceLogs.Count; ri++)
        {
            var resourceLogs = request.ResourceLogs[ri];
            var service = ServiceName(resourceLogs.Resource);
            for (var si = 0; si < resourceLogs.ScopeLogs.Count; si++)
            for (var li = 0; li < resourceLogs.ScopeLogs[si].LogRecords.Count; li++)
            {
                var scope = resourceLogs.ScopeLogs[si];
                var record = scope.LogRecords[li];
                var nanos = record.TimeUnixNano != 0 ? record.TimeUnixNano : record.ObservedTimeUnixNano;
                yield return new TelemetryLog(
                    FromNanos(nanos),
                    service,
                    MapSeverity(record.SeverityNumber),
                    scope.Scope?.Name ?? "",
                    Format(record.Body),
                    Hex(record.TraceId),
                    Hex(record.SpanId))
                {
                    Id = LogId(requestIndex, ri, si, li),
                };
            }
        }
    }

    public static IEnumerable<TelemetrySpan> MapSpans(ExportTraceServiceRequest request)
    {
        foreach (var resourceSpans in request.ResourceSpans)
        {
            var service = ServiceName(resourceSpans.Resource);
            foreach (var scope in resourceSpans.ScopeSpans)
            foreach (var span in scope.Spans)
            {
                yield return new TelemetrySpan(
                    Hex(span.TraceId)!,
                    Hex(span.SpanId)!,
                    Hex(span.ParentSpanId),
                    span.Name,
                    service,
                    (SpanKind)(int)span.Kind,
                    FromNanos(span.StartTimeUnixNano),
                    span.EndTimeUnixNano == 0 ? null : FromNanos(span.EndTimeUnixNano),
                    span.Status?.Code switch
                    {
                        OpenTelemetry.Proto.Trace.V1.Status.Types.StatusCode.Ok => SpanStatus.Ok,
                        OpenTelemetry.Proto.Trace.V1.Status.Types.StatusCode.Error => SpanStatus.Error,
                        _ => SpanStatus.Unset,
                    },
                    span.Attributes
                        .GroupBy(a => a.Key)
                        .ToDictionary(g => g.Key, g => Format(g.Last().Value)));
            }
        }
    }

    private static string ServiceName(OpenTelemetry.Proto.Resource.V1.Resource? resource) =>
        resource?.Attributes.FirstOrDefault(a => a.Key == "service.name")?.Value?.StringValue ?? "unknown";

    private static DateTimeOffset FromNanos(ulong nanos) => DateTimeOffset.UnixEpoch.AddTicks((long)(nanos / 100));

    private static string? Hex(Google.Protobuf.ByteString bytes) =>
        bytes.IsEmpty ? null : Convert.ToHexStringLower(bytes.ToByteArray());

    private static LogSeverity MapSeverity(SeverityNumber n) => (int)n switch
    {
        >= 21 => LogSeverity.Critical,
        >= 17 => LogSeverity.Error,
        >= 13 => LogSeverity.Warning,
        >= 9 => LogSeverity.Information,
        >= 5 => LogSeverity.Debug,
        _ => LogSeverity.Trace,
    };

    private static string Format(AnyValue? v) => v?.ValueCase switch
    {
        AnyValue.ValueOneofCase.StringValue => v.StringValue,
        AnyValue.ValueOneofCase.IntValue => v.IntValue.ToString(),
        AnyValue.ValueOneofCase.DoubleValue => v.DoubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
        AnyValue.ValueOneofCase.BoolValue => v.BoolValue.ToString(),
        null or AnyValue.ValueOneofCase.None => "",
        _ => v.ToString(),
    };
}
