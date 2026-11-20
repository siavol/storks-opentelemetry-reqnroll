using System.Text.Json.Nodes;
using NUnit.Framework;

namespace E2E.ReportChecks;

/// <summary>The run index and the OTLP/JSON telemetry embedded into telemetry.html.</summary>
public class ViewerTests
{
    private const string Red = "Customer gets a quote for a SKU whose price cannot be fetched";
    private const string Green = "Customer gets a quote for a regular SKU";

    // OTLP span kind.
    private const int Server = 2;
    private const int Client = 3;

    private string _html = "";

    [SetUp]
    public void LoadViewer() => _html = Reports.Viewer();

    /// <summary>Embedded spans by span id, with their service.</summary>
    private Dictionary<string, (JsonNode Span, string Service)> Spans() =>
        Reports.DataBlock(_html, "otlp-traces").AsArray()
            .SelectMany(r => r!["resourceSpans"]!.AsArray())
            .SelectMany(rs => rs!["scopeSpans"]!.AsArray().SelectMany(ss => ss!["spans"]!.AsArray())
                .Select(s => (Span: s!, Service: Reports.ServiceName(rs))))
            .GroupBy(x => (string)x.Span["spanId"]!)
            .ToDictionary(g => g.Key, g => g.First());

    /// <summary>Embedded log record by its index id "request.resource.scope.record", with its service.</summary>
    private (JsonNode Log, string Service) Log(string id)
    {
        var p = id.Split('.').Select(int.Parse).ToArray();
        var resourceLogs = Reports.DataBlock(_html, "otlp-logs")[p[0]]!["resourceLogs"]![p[1]]!;
        return (resourceLogs["scopeLogs"]![p[2]]!["logRecords"]![p[3]]!, Reports.ServiceName(resourceLogs));
    }

    private static string? Attribute(JsonNode span, string key)
    {
        var value = span["attributes"]?.AsArray().FirstOrDefault(a => (string?)a!["key"] == key)?["value"];
        return value is null ? null : (value["intValue"] ?? value["stringValue"])?.ToString();
    }

    /// <summary>The index entry of the When step of the scenario.</summary>
    private JsonNode WhenStep(string scenarioName)
    {
        var step = Reports.Scenarios(_html).Single(s => (string?)s["name"] == scenarioName)["steps"]!.AsArray()
            .Single(s => (string?)s!["keyword"] == "When")!;
        Assert.That((string?)step["text"], Does.StartWith("the customer asks for a quote"));
        return step;
    }

    private List<(JsonNode Span, string Service)> StepSpans(JsonNode step)
    {
        var spans = Spans();
        return step["spanIds"]!.AsArray().Select(id => spans[(string)id!]).ToList();
    }

    [Test]
    public void Viewer_has_every_scenario_of_the_report_with_its_own_id_and_the_failed_one()
    {
        var scenarios = Reports.Scenarios(_html).ToList();
        var reported = Reports.JsonLines("reqnroll_report.ndjson").Where(m => m.ContainsKey("pickle")).Select(m => (string?)m["pickle"]!["name"]);

        Assert.That(scenarios.Select(s => (string?)s["name"]), Is.EquivalentTo(reported));
        Assert.That(scenarios.Select(s => (string?)s["id"]), Is.Unique.And.All.Match("^[0-9a-f]{32}$"));
        Assert.That(scenarios.Single(s => (string?)s["name"] == Red)["status"]?.ToString(), Is.EqualTo("failed"));
        Assert.That(scenarios.Where(s => (string?)s["name"] != Red).Select(s => s["status"]?.ToString()), Is.Not.Empty.And.All.EqualTo("passed"));
    }

    [Test]
    public void When_step_of_red_scenario_has_the_Front_warning_about_the_failed_Back_call_under_a_Front_span()
    {
        var step = WhenStep(Red);

        var warnings = step["logIds"]!.AsArray().Select(id => Log((string)id!))
            .Where(l => ((string?)l.Log["body"]?["stringValue"])?.Contains("Price request to back-api failed") == true).ToList();
        Assert.That(warnings, Has.Count.EqualTo(1), "the Front warning is not attributed to the When step");
        var (log, service) = warnings[0];
        Assert.That(service, Is.EqualTo("front-api"));
        Assert.That((int?)log["severityNumber"], Is.InRange(13, 16), "WRN");
        var spanId = (string?)log["spanId"];
        Assert.That(spanId, Is.Not.Null.And.Not.Empty, "the warning has no span context");
        Assert.That(Spans().TryGetValue(spanId!, out var span) && span.Service == "front-api", $"span {spanId} of the warning is not an embedded Front span");
        Assert.That(step["spanIds"]!.AsArray().Select(x => (string?)x), Does.Contain(spanId), "the Front span is in the same step");
    }

    [Test]
    public void When_step_of_red_scenario_has_the_Back_span_with_status_code_500_under_a_Front_span()
    {
        var stepSpans = StepSpans(WhenStep(Red));

        var back = stepSpans.Where(s => s.Service == "back-api" && Attribute(s.Span, "http.response.status_code") == "500").ToList();
        Assert.That(back, Has.Count.EqualTo(1), "no Back span with status code 500 attributed to the When step");
        var parent = (string?)back[0].Span["parentSpanId"];
        Assert.That(stepSpans, Has.Some.Matches<(JsonNode Span, string Service)>(s => s.Service == "front-api" && (string?)s.Span["spanId"] == parent),
            "the Back span is not under a Front span of the step");
    }

    [Test]
    public void When_step_of_green_scenario_has_the_Front_trace_with_the_Back_call_and_logs_of_both_services()
    {
        var step = WhenStep(Green);
        var stepSpans = StepSpans(step);

        var get = stepSpans.Single(s => s.Service == "front-api" && (int?)s.Span["kind"] == Server);
        var trace = stepSpans.Where(s => (string?)s.Span["traceId"] == (string?)get.Span["traceId"]).ToList();
        Assert.That(trace, Has.Some.Matches<(JsonNode Span, string Service)>(s => s.Service == "front-api" && (int?)s.Span["kind"] == Client), "no Front client call");
        Assert.That(trace, Has.Some.Matches<(JsonNode Span, string Service)>(s =>
            s.Service == "back-api" && (int?)s.Span["kind"] == Server && Attribute(s.Span, "http.response.status_code") == "200"), "no Back server span with 200");
        Assert.That(step["logIds"]!.AsArray().Select(id => Log((string)id!).Service).Distinct(),
            Is.SupersetOf(new[] { "front-api", "back-api" }), "logs of both services in the When step");
    }
}
