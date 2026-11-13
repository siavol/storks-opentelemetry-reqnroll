using System.Net;
using System.Net.Http.Headers;
using Google.Protobuf;
using GherkinOtel.ReqnrollPlugin;
using NUnit.Framework;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;

namespace GherkinOtel.ReqnrollPlugin.Tests;

/// <summary>Loopback-only smoke test of the receiver through its HTTP interface.</summary>
public class OtlpReceiverTests
{
    [Test]
    public async Task Posted_otlp_logs_land_in_the_store_as_neutral_records()
    {
        var store = new TelemetryStore();
        await using var receiver = new OtlpReceiver(store);
        await receiver.StartAsync();

        var when = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var request = new ExportLogsServiceRequest();
        var resource = new ResourceLogs { Resource = new Resource() };
        resource.Resource.Attributes.Add(new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "orders-api" } });
        var scope = new ScopeLogs { Scope = new InstrumentationScope { Name = "Orders.Cat" } };
        scope.LogRecords.Add(new LogRecord
        {
            TimeUnixNano = (ulong)(when.ToUnixTimeMilliseconds() * 1_000_000),
            SeverityNumber = SeverityNumber.Warn,
            Body = new AnyValue { StringValue = "pricing failed" },
        });
        resource.ScopeLogs.Add(scope);
        request.ResourceLogs.Add(resource);

        using var http = new HttpClient();
        var content = new ByteArrayContent(request.ToByteArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
        var response = await http.PostAsync(new Uri(receiver.Endpoint, "v1/logs"), content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(receiver.Endpoint.IsLoopback, Is.True);
        var log = store.Snapshot().Single();
        Assert.That(log, Is.EqualTo(new TelemetryLog(when, "orders-api", LogSeverity.Warning, "Orders.Cat", "pricing failed")));
    }
}
