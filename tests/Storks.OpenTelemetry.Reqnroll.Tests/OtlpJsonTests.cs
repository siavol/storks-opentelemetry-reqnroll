using System.Text.Json.Nodes;
using Google.Protobuf;
using NUnit.Framework;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Storks.OpenTelemetry.Reqnroll;
using Storks.OpenTelemetry.Reqnroll.Core;
using Storks.OpenTelemetry.Reqnroll.Receiver;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

/// <summary>Accepted OTLP requests are kept as-is by the store and written as OTLP/JSON Lines.</summary>
public class OtlpJsonTests
{
    private static KeyValue Str(string key, string value) => new() { Key = key, Value = new AnyValue { StringValue = value } };

    private static ExportTraceServiceRequest TraceRequest(string service = "pricing-api")
    {
        var span = new Span
        {
            TraceId = ByteString.CopyFrom(Enumerable.Range(1, 16).Select(i => (byte)i).ToArray()),
            SpanId = ByteString.CopyFrom(Enumerable.Range(1, 8).Select(i => (byte)(i + 0xA0)).ToArray()),
            ParentSpanId = ByteString.CopyFrom(Enumerable.Range(1, 8).Select(i => (byte)(i + 0xB0)).ToArray()),
            Name = "GET /prices/{sku}",
            Kind = Span.Types.SpanKind.Server,
            StartTimeUnixNano = 1_767_268_800_000_000_123UL,
            EndTimeUnixNano = 1_767_268_800_040_000_000UL,
            Status = new Status { Code = Status.Types.StatusCode.Error },
        };
        span.Attributes.Add(new KeyValue { Key = "http.response.status_code", Value = new AnyValue { IntValue = 500 } });
        var resource = new ResourceSpans { Resource = new Resource() };
        resource.Resource.Attributes.Add(Str("service.name", service));
        var scope = new ScopeSpans { Scope = new InstrumentationScope { Name = "Pricing.Scope", Version = "1.2.3" } };
        scope.Spans.Add(span);
        resource.ScopeSpans.Add(scope);
        var request = new ExportTraceServiceRequest();
        request.ResourceSpans.Add(resource);
        return request;
    }

    private static ExportLogsServiceRequest LogsRequest()
    {
        var record = new LogRecord
        {
            TimeUnixNano = 1_767_268_800_000_000_000UL,
            SeverityNumber = SeverityNumber.Warn,
            SeverityText = "Warning",
            Body = new AnyValue { StringValue = "pricing failed" },
            TraceId = ByteString.CopyFrom(Enumerable.Range(1, 16).Select(i => (byte)i).ToArray()),
        };
        record.Attributes.Add(Str("custom", "value"));
        var resource = new ResourceLogs { Resource = new Resource() };
        resource.Resource.Attributes.Add(Str("service.name", "orders-api"));
        var scope = new ScopeLogs { Scope = new InstrumentationScope { Name = "Orders.Cat" } };
        scope.LogRecords.Add(record);
        resource.ScopeLogs.Add(scope);
        var request = new ExportLogsServiceRequest();
        request.ResourceLogs.Add(resource);
        return request;
    }

    private static string TempDir() => Path.Combine(Path.GetTempPath(), "storks-otel-tests", Guid.NewGuid().ToString("N"));

    private static string? ServiceOf(string traceLine) =>
        (string?)JsonNode.Parse(traceLine)!["resourceSpans"]![0]!["resource"]!["attributes"]![0]!["value"]!["stringValue"];

    [Test]
    public void Store_keeps_accepted_requests_as_they_were_received()
    {
        var store = new TelemetryStore();
        var traces = TraceRequest();
        var logs = LogsRequest();

        OtlpReceiver.StoreTraces(store, traces);
        OtlpReceiver.StoreLogs(store, logs);

        Assert.That(store.Snapshot().TraceRequests.Single(), Is.EqualTo(traces));
        Assert.That(store.Snapshot().LogRequests.Single(), Is.EqualTo(logs));
    }

    [Test]
    public void Receiver_stores_the_raw_request_next_to_the_neutral_model()
    {
        var store = new TelemetryStore();
        var traces = TraceRequest();

        OtlpReceiver.StoreTraces(store, traces);

        Assert.That(store.Snapshot().TraceRequests.Single(), Is.EqualTo(traces));
        Assert.That(store.Snapshot().Spans, Has.Count.EqualTo(1));
    }

    [Test]
    public void Trace_request_becomes_one_otlp_json_line_with_resource_scope_and_attributes()
    {
        var line = OtlpJson.Serialize(TraceRequest());

        Assert.That(line, Does.Not.Contain('\n'));
        var rs = JsonNode.Parse(line)!["resourceSpans"]!.AsArray().Single()!;
        Assert.That((string?)rs["resource"]!["attributes"]![0]!["key"], Is.EqualTo("service.name"));
        Assert.That((string?)rs["resource"]!["attributes"]![0]!["value"]!["stringValue"], Is.EqualTo("pricing-api"));
        var scope = rs["scopeSpans"]!.AsArray().Single()!;
        Assert.That((string?)scope["scope"]!["name"], Is.EqualTo("Pricing.Scope"));
        Assert.That((string?)scope["scope"]!["version"], Is.EqualTo("1.2.3"));
        var span = scope["spans"]!.AsArray().Single()!;
        Assert.That((string?)span["name"], Is.EqualTo("GET /prices/{sku}"));
        Assert.That((string?)span["attributes"]![0]!["key"], Is.EqualTo("http.response.status_code"));
        Assert.That((string?)span["attributes"]![0]!["value"]!["intValue"], Is.EqualTo("500"));
    }

    [Test]
    public void Ids_are_hex_times_are_nanosecond_strings_and_enums_are_numbers()
    {
        var span = JsonNode.Parse(OtlpJson.Serialize(TraceRequest()))!["resourceSpans"]![0]!["scopeSpans"]![0]!["spans"]![0]!;

        Assert.That((string?)span["traceId"], Is.EqualTo("0102030405060708090a0b0c0d0e0f10"));
        Assert.That((string?)span["spanId"], Is.EqualTo("a1a2a3a4a5a6a7a8"));
        Assert.That((string?)span["parentSpanId"], Is.EqualTo("b1b2b3b4b5b6b7b8"));
        Assert.That((string?)span["startTimeUnixNano"], Is.EqualTo("1767268800000000123"));
        Assert.That((string?)span["endTimeUnixNano"], Is.EqualTo("1767268800040000000"));
        Assert.That((int?)span["kind"], Is.EqualTo(2));
        Assert.That((int?)span["status"]!["code"], Is.EqualTo(2));
    }

    [Test]
    public void Logs_request_keeps_resource_scope_attributes_and_hex_trace_id()
    {
        var rl = JsonNode.Parse(OtlpJson.Serialize(LogsRequest()))!["resourceLogs"]![0]!;
        var record = rl["scopeLogs"]![0]!["logRecords"]![0]!;

        Assert.That((string?)rl["resource"]!["attributes"]![0]!["value"]!["stringValue"], Is.EqualTo("orders-api"));
        Assert.That((string?)rl["scopeLogs"]![0]!["scope"]!["name"], Is.EqualTo("Orders.Cat"));
        Assert.That((string?)record["body"]!["stringValue"], Is.EqualTo("pricing failed"));
        Assert.That((int?)record["severityNumber"], Is.EqualTo(13));
        Assert.That((string?)record["timeUnixNano"], Is.EqualTo("1767268800000000000"));
        Assert.That((string?)record["traceId"], Is.EqualTo("0102030405060708090a0b0c0d0e0f10"));
        Assert.That((string?)record["attributes"]![0]!["value"]!["stringValue"], Is.EqualTo("value"));
    }

    [Test]
    public void Files_have_one_line_per_accepted_request_in_arrival_order()
    {
        var dir = TempDir();
        var store = new TelemetryStore();
        OtlpReceiver.StoreTraces(store, TraceRequest("orders-api"));
        OtlpReceiver.StoreTraces(store, TraceRequest("pricing-api"));
        OtlpReceiver.StoreLogs(store, LogsRequest());

        OtlpJsonFiles.Write(Path.Combine(dir, "traces.jsonl"), Path.Combine(dir, "logs.jsonl"), store);

        var traces = File.ReadAllLines(Path.Combine(dir, "traces.jsonl"));
        var logs = File.ReadAllLines(Path.Combine(dir, "logs.jsonl"));
        Assert.That(traces, Has.Length.EqualTo(2));
        Assert.That(ServiceOf(traces[0]), Is.EqualTo("orders-api"));
        Assert.That(ServiceOf(traces[1]), Is.EqualTo("pricing-api"));
        Assert.That(logs, Has.Length.EqualTo(1));
    }

    [Test]
    public void Files_are_written_even_when_nothing_was_received()
    {
        var dir = TempDir();

        OtlpJsonFiles.Write(Path.Combine(dir, "traces.jsonl"), Path.Combine(dir, "logs.jsonl"), new TelemetryStore());

        Assert.That(File.ReadAllText(Path.Combine(dir, "traces.jsonl")), Is.Empty);
        Assert.That(File.ReadAllText(Path.Combine(dir, "logs.jsonl")), Is.Empty);
    }

    [Test]
    public async Task Failure_to_write_the_files_does_not_fail_the_run_and_is_reported_by_a_plugin_line()
    {
        // A regular file where the report directory should be makes every write fail.
        var blocker = TempDir();
        Directory.CreateDirectory(Path.GetDirectoryName(blocker)!);
        await File.WriteAllTextAsync(blocker, "x");
        var files = new ReportFiles(Path.Combine(blocker, "telemetry.html"), Path.Combine(blocker, "traces.jsonl"), Path.Combine(blocker, "logs.jsonl"));
        var store = new TelemetryStore();
        OtlpReceiver.StoreTraces(store, TraceRequest());
        await using var receiver = new OtlpReceiver(store);
        using var errors = new StringWriter();

        var record = new RunRecord();
        var diagnostics = new PluginDiagnostics();
        Assert.DoesNotThrowAsync(() => RunEnd.Finish(files, receiver, store, record, diagnostics, new RunTracker(record, diagnostics),
            new RunOutput(TextWriter.Null, errors)));
        Assert.That(errors.ToString(), Does.Contain("[Storks.OpenTelemetry] failed to write the telemetry viewer")
            .And.Contain("[Storks.OpenTelemetry] failed to write OTLP/JSON telemetry"));
    }
}
