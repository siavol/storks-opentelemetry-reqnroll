using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Google.Protobuf;
using NUnit.Framework;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Storks.OpenTelemetry.Reqnroll.Core;
using Storks.OpenTelemetry.Reqnroll.Receiver;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

public partial class ViewerPageTests
{
    [GeneratedRegex("<script>")]
    private static partial Regex ExecutableScript();

    private static readonly DateTimeOffset T0 = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    private static (RunRecord Record, ScenarioRun Run) RunWithError(string error)
    {
        var record = new RunRecord();
        var run = record.Begin("Checkout", "red", [], T0);
        run.AddStep("Then", "the total", T0, T0.AddMilliseconds(5), "failed", error);
        run.Finish(T0.AddMilliseconds(10), "failed", error);
        return (record, run);
    }

    private static ExportTraceServiceRequest TraceRequest(string spanName)
    {
        var resource = new ResourceSpans { Resource = new Resource() };
        resource.Resource.Attributes.Add(new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "pricing-api" } });
        var scope = new ScopeSpans { Scope = new InstrumentationScope { Name = "Pricing.Scope" } };
        scope.Spans.Add(new Span
        {
            TraceId = ByteString.CopyFrom(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16),
            SpanId = ByteString.CopyFrom(0xa1, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7, 0xa8),
            Name = spanName,
            StartTimeUnixNano = 1_794_650_400_001_000_000UL,
            EndTimeUnixNano = 1_794_650_400_002_000_000UL,
        });
        resource.ScopeSpans.Add(scope);
        var request = new ExportTraceServiceRequest();
        request.ResourceSpans.Add(resource);
        return request;
    }

    private static ExportLogsServiceRequest LogsRequest(params (string Body, ulong UnixNano)[] records)
    {
        var resource = new ResourceLogs { Resource = new Resource() };
        resource.Resource.Attributes.Add(new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "orders-api" } });
        var scope = new ScopeLogs { Scope = new InstrumentationScope { Name = "Orders.Program" } };
        foreach (var (body, nanos) in records)
        {
            scope.LogRecords.Add(new LogRecord
            {
                TimeUnixNano = nanos,
                SeverityNumber = SeverityNumber.Warn,
                SeverityText = "Warning",
                Body = new AnyValue { StringValue = body },
                SpanId = ByteString.CopyFrom(0xa1, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7, 0xa8),
            });
        }
        resource.ScopeLogs.Add(scope);
        var request = new ExportLogsServiceRequest();
        request.ResourceLogs.Add(resource);
        return request;
    }

    /// <summary>Embedded log record by its index id "request.resource.scope.record" (positions in the otlp-logs block).</summary>
    private static JsonNode EmbeddedLog(string html, string id)
    {
        var p = id.Split('.').Select(int.Parse).ToArray();
        return DataBlock(html, "otlp-logs")[p[0]]!["resourceLogs"]![p[1]]!["scopeLogs"]![p[2]]!["logRecords"]![p[3]]!;
    }

    /// <summary>Content of the <c>&lt;script type="application/json" id="..."&gt;</c> data block, parsed.</summary>
    private static JsonNode DataBlock(string html, string id)
    {
        var m = Regex.Match(html, $"<script type=\"application/json\" id=\"{id}\">(.*?)</script>", RegexOptions.Singleline);
        Assert.That(m.Success, $"no data block {id}");
        return JsonNode.Parse(m.Groups[1].Value)!;
    }

    [Test]
    public void Page_embeds_the_index_with_every_scenario_id_and_has_no_placeholder_left()
    {
        var (record, run) = RunWithError("boom");

        var html = ViewerPage.Render(RunIndex.Build(record.Snapshot()), [], []);

        var index = DataBlock(html, "run-index");
        Assert.That((string?)index["features"]![0]!["scenarios"]![0]!["id"], Is.EqualTo(run.Id));
        Assert.That((string?)index["features"]![0]!["scenarios"]![0]!["error"], Is.EqualTo("boom"));
        Assert.That(html, Does.Not.Contain("__RUN_INDEX__").And.Not.Contain("__OTLP_TRACES__").And.Not.Contain("__OTLP_LOGS__"));
    }

    [Test]
    public void Page_embeds_received_trace_requests_as_OTLP_JSON_with_resource_scope_and_hex_ids()
    {
        var (record, _) = RunWithError("boom");

        var html = ViewerPage.Render(RunIndex.Build(record.Snapshot()), [TraceRequest("GET /prices/{sku}"), TraceRequest("second")], []);

        var traces = DataBlock(html, "otlp-traces").AsArray();
        Assert.That(traces, Has.Count.EqualTo(2), "one element per received request");
        var resourceSpans = traces[0]!["resourceSpans"]![0]!;
        Assert.That((string?)resourceSpans["resource"]!["attributes"]![0]!["value"]!["stringValue"], Is.EqualTo("pricing-api"));
        var scopeSpans = resourceSpans["scopeSpans"]![0]!;
        Assert.That((string?)scopeSpans["scope"]!["name"], Is.EqualTo("Pricing.Scope"));
        var span = scopeSpans["spans"]![0]!;
        Assert.That((string?)span["spanId"], Is.EqualTo("a1a2a3a4a5a6a7a8"));
        Assert.That((string?)span["name"], Is.EqualTo("GET /prices/{sku}"));
        Assert.That((string?)span["startTimeUnixNano"], Is.EqualTo("1794650400001000000"));
    }

    [Test]
    public void Page_is_self_contained_and_data_cannot_break_out_of_its_script_block()
    {
        var (record, _) = RunWithError("</script><script>alert(1)</script>");

        var html = ViewerPage.Render(RunIndex.Build(record.Snapshot()), [TraceRequest("</script><script>alert(2)</script>")],
            [LogsRequest(("</script><script>alert(3)</script>", 1))]);

        Assert.That(html, Does.Not.Contain("alert(1)</script>").And.Not.Contain("alert(2)</script>").And.Not.Contain("alert(3)</script>"));
        Assert.That((string?)DataBlock(html, "otlp-traces")[0]!["resourceSpans"]![0]!["scopeSpans"]![0]!["spans"]![0]!["name"],
            Is.EqualTo("</script><script>alert(2)</script>"), "escaped, but the value survives");
        Assert.That(html, Does.Not.Contain("fetch(").And.Not.Contain("XMLHttpRequest"));
        Assert.That(html, Does.Not.Match(@"(src|href)\s*=\s*""https?:"), "must work offline");
        Assert.That(ExecutableScript().Count(html), Is.EqualTo(1), "exactly one executable script element");
    }

    [Test]
    public void Write_puts_telemetry_html_with_the_received_spans_into_the_report_directory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "storks-otel-tests", Guid.NewGuid().ToString("N"));
        var (record, run) = RunWithError("boom");
        var store = new TelemetryStore();
        OtlpReceiver.StoreTraces(store, TraceRequest("GET /prices/{sku}")); // the span starts at T0 + 1 ms

        var path = ViewerPage.Write(dir, record, store);

        Assert.That(Path.GetFileName(path), Is.EqualTo("telemetry.html"));
        var html = File.ReadAllText(path);
        var step = DataBlock(html, "run-index")["features"]![0]!["scenarios"]![0]!["steps"]![0]!;
        Assert.That(step["spanIds"]!.AsArray().Select(x => (string?)x), Is.EqualTo(new[] { "a1a2a3a4a5a6a7a8" }));
        Assert.That(DataBlock(html, "otlp-traces").AsArray(), Has.Count.EqualTo(1));
        Assert.That(html, Does.Contain(run.Id));
    }

    [Test]
    public void Write_embeds_received_log_requests_and_the_index_refers_to_the_step_logs_by_their_position_in_them()
    {
        var dir = Path.Combine(Path.GetTempPath(), "storks-otel-tests", Guid.NewGuid().ToString("N"));
        var (record, _) = RunWithError("boom"); // step [T0, T0 + 5 ms], scenario until T0 + 10 ms
        var store = new TelemetryStore();
        OtlpReceiver.StoreLogs(store, LogsRequest(("before the run", 1_000)));
        OtlpReceiver.StoreLogs(store, LogsRequest(("in the hook", 1_794_650_400_007_000_000UL), ("Pricing request failed", 1_794_650_400_003_000_000UL)));

        var html = File.ReadAllText(ViewerPage.Write(dir, record, store));

        var scenario = DataBlock(html, "run-index")["features"]![0]!["scenarios"]![0]!;
        var stepLogs = scenario["steps"]![0]!["logIds"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.That(stepLogs, Has.Count.EqualTo(1));
        var log = EmbeddedLog(html, stepLogs[0]);
        Assert.That((string?)log["body"]!["stringValue"], Is.EqualTo("Pricing request failed"));
        Assert.That((string?)log["spanId"], Is.EqualTo("a1a2a3a4a5a6a7a8"), "hex, as in OTLP/JSON");
        Assert.That(scenario["logIds"]!.AsArray().Select(x => (string?)EmbeddedLog(html, (string)x!)["body"]!["stringValue"]),
            Is.EqualTo(new[] { "Pricing request failed", "in the hook" }));
        Assert.That(DataBlock(html, "otlp-logs").AsArray(), Has.Count.EqualTo(2), "one element per received request");
    }
}
