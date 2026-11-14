using System.Net;
using System.Net.Http.Headers;
using Google.Protobuf;
using NUnit.Framework;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using Storks.OpenTelemetry.Reqnroll;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

public class ReceiverFaultToleranceTests
{
    private static ByteArrayContent Proto(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
        return content;
    }

    [Test]
    public async Task Invalid_request_is_reported_to_the_error_sink()
    {
        var errors = new List<string>();
        await using var receiver = new OtlpReceiver(new TelemetryStore(), errors.Add);
        await receiver.StartAsync();
        using var http = new HttpClient();

        await http.PostAsync(new Uri(receiver.Endpoint, "v1/logs"), Proto([0xFF, 0xFF, 0xFF, 0xFF, 0x01]));

        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("v1/logs"));
    }

    [Test]
    public async Task Invalid_request_gets_an_error_and_the_receiver_keeps_accepting_valid_ones()
    {
        var store = new TelemetryStore();
        await using var receiver = new OtlpReceiver(store);
        await receiver.StartAsync();
        using var http = new HttpClient();
        var logsUrl = new Uri(receiver.Endpoint, "v1/logs");

        var bad = await http.PostAsync(logsUrl, Proto([0xFF, 0xFF, 0xFF, 0xFF, 0x01]));
        var wrongPath = await http.PostAsync(new Uri(receiver.Endpoint, "v1/nonsense"), Proto([]));
        var wrongMethod = await http.GetAsync(logsUrl);

        Assert.That((int)bad.StatusCode, Is.InRange(400, 499));
        Assert.That((int)wrongPath.StatusCode, Is.InRange(400, 499));
        Assert.That((int)wrongMethod.StatusCode, Is.InRange(400, 499));

        var request = new ExportLogsServiceRequest();
        var resource = new ResourceLogs { Resource = new Resource() };
        var scope = new ScopeLogs();
        scope.LogRecords.Add(new LogRecord { TimeUnixNano = 1_000_000_000, Body = new global::OpenTelemetry.Proto.Common.V1.AnyValue { StringValue = "ok" } });
        resource.ScopeLogs.Add(scope);
        request.ResourceLogs.Add(resource);
        var good = await http.PostAsync(logsUrl, Proto(request.ToByteArray()));

        Assert.That(good.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(store.Snapshot().Logs.Single().Message, Is.EqualTo("ok"));
    }

    [TestCase("v1/logs")]
    [TestCase("v1/traces")]
    public async Task Malformed_protobuf_gets_400_is_reported_and_the_receiver_keeps_running(string path)
    {
        var errors = new List<string>();
        await using var receiver = new OtlpReceiver(new TelemetryStore(), errors.Add);
        await receiver.StartAsync();
        using var http = new HttpClient();

        var bad = await http.PostAsync(new Uri(receiver.Endpoint, path), Proto([0xFF, 0xFF, 0xFF, 0xFF, 0x01]));
        var good = await http.PostAsync(new Uri(receiver.Endpoint, path), Proto([]));

        Assert.That(bad.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain(path));
        Assert.That(good.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
