using NUnit.Framework;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

public class TelemetryEndpointTests
{
    [Test]
    public void Exporter_environment_points_the_SUT_at_the_receiver_over_http_protobuf_with_short_batch_delays()
    {
        var endpoint = new TelemetryEndpoint(new Uri("http://127.0.0.1:41234/"));

        var environment = endpoint.GetExporterEnvironment();

        Assert.That(environment, Is.EquivalentTo(new Dictionary<string, string>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:41234",
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
            ["OTEL_BSP_SCHEDULE_DELAY"] = "100",
            ["OTEL_BLRP_SCHEDULE_DELAY"] = "100",
        }));
    }
}
