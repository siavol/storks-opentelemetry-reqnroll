using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;

namespace GherkinOtel.ReqnrollPlugin;

/// <summary>OTLP protobuf -> neutral model. The only place that knows OTLP types.</summary>
internal static class OtlpMapper
{
    public static IEnumerable<TelemetryLog> MapLogs(ExportLogsServiceRequest request)
    {
        foreach (var resourceLogs in request.ResourceLogs)
        {
            var service = resourceLogs.Resource?.Attributes
                .FirstOrDefault(a => a.Key == "service.name")?.Value?.StringValue ?? "unknown";
            foreach (var scope in resourceLogs.ScopeLogs)
            foreach (var record in scope.LogRecords)
            {
                var nanos = record.TimeUnixNano != 0 ? record.TimeUnixNano : record.ObservedTimeUnixNano;
                yield return new TelemetryLog(
                    DateTimeOffset.UnixEpoch.AddTicks((long)(nanos / 100)),
                    service,
                    MapSeverity(record.SeverityNumber),
                    scope.Scope?.Name ?? "",
                    Format(record.Body),
                    record.TraceId.IsEmpty ? null : Convert.ToHexStringLower(record.TraceId.ToByteArray()),
                    record.SpanId.IsEmpty ? null : Convert.ToHexStringLower(record.SpanId.ToByteArray()));
            }
        }
    }

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
