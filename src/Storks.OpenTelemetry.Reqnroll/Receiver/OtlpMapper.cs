using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using Storks.OpenTelemetry.Reqnroll.Core;

namespace Storks.OpenTelemetry.Reqnroll.Receiver;

/// <summary>OTLP protobuf -> neutral model, which Attribution works on without knowing OTLP.</summary>
internal static class OtlpMapper
{
    /// <summary>
    /// A log's position among the received requests, so that the viewer finds the record in the embedded OTLP/JSON
    /// without the index copying it. The viewer builds the same id; change both together.
    /// </summary>
    public static string LogId(int request, int resource, int scope, int record) => $"{request}.{resource}.{scope}.{record}";

    public static IEnumerable<TelemetryLog> MapLogs(ExportLogsServiceRequest request, int requestIndex)
    {
        for (var ri = 0; ri < request.ResourceLogs.Count; ri++)
        {
            var resourceLogs = request.ResourceLogs[ri];
            var service = ServiceName(resourceLogs.Resource);
            for (var si = 0; si < resourceLogs.ScopeLogs.Count; si++)
            {
                var scope = resourceLogs.ScopeLogs[si];
                for (var li = 0; li < scope.LogRecords.Count; li++)
                {
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
    }

    public static IEnumerable<TelemetrySpan> MapSpans(ExportTraceServiceRequest request)
    {
        foreach (var resourceSpans in request.ResourceSpans)
        {
            var service = ServiceName(resourceSpans.Resource);
            foreach (var scope in resourceSpans.ScopeSpans)
            {
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
                            global::OpenTelemetry.Proto.Trace.V1.Status.Types.StatusCode.Ok => SpanStatus.Ok,
                            global::OpenTelemetry.Proto.Trace.V1.Status.Types.StatusCode.Error => SpanStatus.Error,
                            _ => SpanStatus.Unset,
                        },
                        span.Attributes
                            .GroupBy(a => a.Key)
                            .ToDictionary(g => g.Key, g => Format(g.Last().Value)));
                }
            }
        }
    }

    private static string ServiceName(global::OpenTelemetry.Proto.Resource.V1.Resource? resource) =>
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
        AnyValue.ValueOneofCase.IntValue => v.IntValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
        AnyValue.ValueOneofCase.DoubleValue => v.DoubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
        AnyValue.ValueOneofCase.BoolValue => v.BoolValue.ToString(),
        null or AnyValue.ValueOneofCase.None => "",
        _ => v.ToString(),
    };
}
