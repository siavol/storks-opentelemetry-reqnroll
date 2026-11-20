using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Google.Protobuf;
using NUnit.Framework;
using OpenTelemetry;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Trace;
using Storks.OpenTelemetry.Reqnroll.Core;
using Storks.OpenTelemetry.Reqnroll.Receiver;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

public class ReceiverInvisibilityTests
{
    [Test]
    public async Task Receiver_requests_are_invisible_to_asp_net_core_instrumentation_of_an_in_process_sut()
    {
        var exported = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAspNetCoreInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();
        await using var receiver = new OtlpReceiver(new TelemetryStore());
        await receiver.StartAsync();

        using var http = new HttpClient();
        var content = new ByteArrayContent(new ExportTraceServiceRequest().ToByteArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
        var response = await http.PostAsync(new Uri(receiver.Endpoint, "v1/traces"), content);
        tracerProvider.ForceFlush();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(exported, Is.Empty);
    }
}
