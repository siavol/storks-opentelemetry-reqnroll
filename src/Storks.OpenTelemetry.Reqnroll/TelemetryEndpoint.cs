namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>
/// The plugin's in-process OTLP receiver, as seen by a test project. The plugin registers it in Reqnroll's global
/// container: resolve it in a [BeforeTestRun] hook and hand <see cref="GetExporterEnvironment"/> to the SUT.
/// </summary>
public interface ITelemetryEndpoint
{
    /// <summary>Base address of the in-process OTLP/HTTP receiver, e.g. http://127.0.0.1:41234/.</summary>
    Uri Endpoint { get; }

    /// <summary>
    /// Environment variables that point a SUT's OpenTelemetry OTLP exporter at the receiver: the endpoint (without a
    /// trailing slash), the <c>http/protobuf</c> protocol, and span and log batch delays shortened to 100 ms so that
    /// telemetry exported just before the end of the test run still arrives.
    /// </summary>
    /// <returns>
    /// A name-to-value map for anything that takes environment variables: an Aspire resource, <c>Process.Start</c>,
    /// a container.
    /// </returns>
    IReadOnlyDictionary<string, string> GetExporterEnvironment();
}

internal sealed class TelemetryEndpoint(Uri endpoint) : ITelemetryEndpoint
{
    // Exporter defaults are 5 s (spans) and 1 s (logs): the last scenarios' telemetry would miss the end of the run.
    private const string BatchDelayMilliseconds = "100";

    public Uri Endpoint { get; } = endpoint;

    public IReadOnlyDictionary<string, string> GetExporterEnvironment() => new Dictionary<string, string>
    {
        ["OTEL_EXPORTER_OTLP_ENDPOINT"] = Endpoint.ToString().TrimEnd('/'),
        ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
        ["OTEL_BSP_SCHEDULE_DELAY"] = BatchDelayMilliseconds,
        ["OTEL_BLRP_SCHEDULE_DELAY"] = BatchDelayMilliseconds,
    };
}
