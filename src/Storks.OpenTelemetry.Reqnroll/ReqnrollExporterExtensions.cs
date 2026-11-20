using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>Points an In-process SUT's OpenTelemetry at the plugin's receiver.</summary>
public static class ReqnrollExporterExtensions
{
    // Exporter defaults are 5 s (spans) and 1 s (logs): the last scenarios' telemetry would miss the end of the run.
    // GetExporterEnvironment passes the same delay to an Out-of-process SUT.
    internal const int BatchDelayMilliseconds = 100;

    /// <summary>
    /// Exports the SUT's traces and logs to the plugin's receiver over <c>http/protobuf</c>, with span and log batch
    /// delays shortened to 100 ms. Call it on the SUT's services, e.g. in a <c>WebApplicationFactory</c>'s
    /// <c>ConfigureServices</c>, with the <see cref="ITelemetryEndpoint"/> from a [BeforeTestRun] hook.
    /// It adds export only, next to any exporters the SUT already has (<c>UseOtlpExporter()</c> included):
    /// no instrumentation, no sources, no metrics. Does nothing when the plugin is off (<see cref="ITelemetryEndpoint.IsEnabled"/> is false).
    /// </summary>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static IOpenTelemetryBuilder UseReqnrollExporter(this IOpenTelemetryBuilder builder, ITelemetryEndpoint telemetry)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(telemetry);
        if (!telemetry.IsEnabled) return builder;

        var endpoint = telemetry.Endpoint;
        // Processors, not AddOtlpExporter: the OTel SDK rejects Add*OtlpExporter next to the SUT's UseOtlpExporter.
        return builder
            .WithTracing(tracing => tracing.AddProcessor(_ => new BatchActivityExportProcessor(
                new OtlpTraceExporter(ExporterOptions(endpoint, "v1/traces")), scheduledDelayMilliseconds: BatchDelayMilliseconds)))
            .WithLogging(logging => logging.AddProcessor(_ => new BatchLogRecordExportProcessor(
                new OtlpLogExporter(ExporterOptions(endpoint, "v1/logs")), scheduledDelayMilliseconds: BatchDelayMilliseconds)));
    }

    private static OtlpExporterOptions ExporterOptions(Uri endpoint, string signalPath) => new()
    {
        Endpoint = new Uri(endpoint, signalPath),
        Protocol = OtlpExportProtocol.HttpProtobuf,
    };
}
