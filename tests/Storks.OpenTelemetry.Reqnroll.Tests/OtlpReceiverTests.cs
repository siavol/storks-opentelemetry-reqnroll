using System.Net;
using System.Net.Http.Headers;
using Google.Protobuf;
using NUnit.Framework;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Storks.OpenTelemetry.Reqnroll;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

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
        var log = store.Snapshot().Logs.Single();
        Assert.That(log, Is.EqualTo(new TelemetryLog(when, "orders-api", LogSeverity.Warning, "Orders.Cat", "pricing failed") { Id = "0.0.0.0" }),
            "id: position request.resource.scope.record among the received requests");
    }

    [Test]
    public async Task Posted_otlp_traces_land_in_the_store_as_neutral_spans()
    {
        var store = new TelemetryStore();
        await using var receiver = new OtlpReceiver(store);
        await receiver.StartAsync();

        var when = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        ulong Nanos(DateTimeOffset t) => (ulong)(t.ToUnixTimeMilliseconds() * 1_000_000);
        var resource = new ResourceSpans { Resource = new Resource() };
        resource.Resource.Attributes.Add(new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "pricing-api" } });
        var scope = new ScopeSpans();
        var span = new Span
        {
            TraceId = ByteString.CopyFrom(Enumerable.Range(1, 16).Select(i => (byte)i).ToArray()),
            SpanId = ByteString.CopyFrom(Enumerable.Range(1, 8).Select(i => (byte)i).ToArray()),
            ParentSpanId = ByteString.CopyFrom(Enumerable.Range(11, 8).Select(i => (byte)i).ToArray()),
            Name = "GET /prices/{sku}",
            Kind = Span.Types.SpanKind.Server,
            StartTimeUnixNano = Nanos(when),
            EndTimeUnixNano = Nanos(when.AddMilliseconds(40)),
            Status = new Status { Code = Status.Types.StatusCode.Error },
        };
        span.Attributes.Add(new KeyValue { Key = "http.response.status_code", Value = new AnyValue { IntValue = 500 } });
        scope.Spans.Add(span);
        resource.ScopeSpans.Add(scope);
        var request = new ExportTraceServiceRequest();
        request.ResourceSpans.Add(resource);

        using var http = new HttpClient();
        var content = new ByteArrayContent(request.ToByteArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
        var response = await http.PostAsync(new Uri(receiver.Endpoint, "v1/traces"), content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var mapped = store.Snapshot().Spans.Single();
        Assert.That(mapped.TraceId, Is.EqualTo("0102030405060708090a0b0c0d0e0f10"));
        Assert.That(mapped.SpanId, Is.EqualTo("0102030405060708"));
        Assert.That(mapped.ParentSpanId, Is.EqualTo("0b0c0d0e0f101112"));
        Assert.That(mapped.Service, Is.EqualTo("pricing-api"));
        Assert.That(mapped.Name, Is.EqualTo("GET /prices/{sku}"));
        Assert.That(mapped.Kind, Is.EqualTo(SpanKind.Server));
        Assert.That(mapped.Start, Is.EqualTo(when));
        Assert.That(mapped.End, Is.EqualTo(when.AddMilliseconds(40)));
        Assert.That(mapped.Status, Is.EqualTo(SpanStatus.Error));
        Assert.That(mapped.Attributes["http.response.status_code"], Is.EqualTo("500"));
    }
}
