using System.Globalization;

namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>
/// The plugin's in-process OTLP receiver, as seen by a test project. The plugin registers it in Reqnroll's global
/// container: resolve it in a [BeforeTestRun] hook and hand <see cref="GetExporterEnvironment"/> to the SUT.
/// It is registered whether the plugin is on or off, so the hook works either way.
/// </summary>
public interface ITelemetryEndpoint
{
    /// <summary>
    /// Whether the plugin is on: Reqnroll's <c>formatters</c> configuration has an <c>opentelemetry</c> entry and
    /// formatters are not disabled. Off, no receiver runs and <see cref="GetExporterEnvironment"/> is empty.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>Base address of the in-process OTLP/HTTP receiver, e.g. http://127.0.0.1:41234/.</summary>
    /// <exception cref="InvalidOperationException">The plugin is off (<see cref="IsEnabled"/> is false): there is no receiver.</exception>
    Uri Endpoint { get; }

    /// <summary>
    /// Environment variables that point a SUT's OpenTelemetry OTLP exporter at the receiver: the endpoint (without a
    /// trailing slash), the <c>http/protobuf</c> protocol, and span and log batch delays shortened to 100 ms so that
    /// telemetry exported just before the end of the test run still arrives.
    /// </summary>
    /// <returns>
    /// Empty when the plugin is off (<see cref="IsEnabled"/> is false). Otherwise a name-to-value map for anything that takes environment variables: an Aspire resource, <c>Process.Start</c>,
    /// a container.
    /// </returns>
    IReadOnlyDictionary<string, string> GetExporterEnvironment();
}

/// <param name="endpoint">The receiver's address; <c>null</c> when the plugin is off.</param>
internal sealed class TelemetryEndpoint(Uri? endpoint) : ITelemetryEndpoint
{
    public bool IsEnabled => endpoint is not null;

    public Uri Endpoint => endpoint ?? throw new InvalidOperationException(
        "The telemetry receiver is not running: add an 'opentelemetry' entry to the Reqnroll formatters configuration.");

    public IReadOnlyDictionary<string, string> GetExporterEnvironment() => !IsEnabled ? [] : new Dictionary<string, string>
    {
        ["OTEL_EXPORTER_OTLP_ENDPOINT"] = Endpoint.ToString().TrimEnd('/'),
        ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
        ["OTEL_BSP_SCHEDULE_DELAY"] = ReqnrollExporterExtensions.BatchDelayMilliseconds.ToString(CultureInfo.InvariantCulture),
        ["OTEL_BLRP_SCHEDULE_DELAY"] = ReqnrollExporterExtensions.BatchDelayMilliseconds.ToString(CultureInfo.InvariantCulture),
    };
}
