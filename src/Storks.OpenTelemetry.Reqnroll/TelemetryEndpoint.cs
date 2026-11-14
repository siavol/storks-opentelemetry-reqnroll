namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>
/// Published by the plugin in Reqnroll's global container. A test project resolves it in its
/// [BeforeTestRun] hook and points the SUT's OTLP exporter at <see cref="Endpoint"/>.
/// </summary>
public interface ITelemetryEndpoint
{
    /// <summary>Base address of the in-process OTLP/HTTP receiver, e.g. http://127.0.0.1:41234/.</summary>
    Uri Endpoint { get; }
}

internal sealed class TelemetryEndpoint(Uri endpoint) : ITelemetryEndpoint
{
    public Uri Endpoint { get; } = endpoint;
}
