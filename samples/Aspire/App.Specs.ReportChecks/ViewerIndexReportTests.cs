using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace App.Specs.ReportChecks;

/// <summary>The attribution index and the OTLP/JSON traces embedded into telemetry.html.</summary>
public class ViewerIndexReportTests
{
    private const string Red = "Customer places an order for a SKU whose price cannot be fetched";
    private const string Green = "Customer places an order for a regular SKU";

    internal static string Page()
    {
        var path = Path.Combine(CucumberMessages.ReportDirectory, "telemetry.html");
        Assert.That(File.Exists(path), $"Run samples/Aspire/App.Specs first. Not found: {path}");
        return File.ReadAllText(path);
    }

    internal static JsonNode DataBlock(string html, string id)
    {
        var m = Regex.Match(html, $"<script type=\"application/json\" id=\"{id}\">(.*?)</script>", RegexOptions.Singleline);
        Assert.That(m.Success, $"telemetry.html has no data block {id}");
        return JsonNode.Parse(m.Groups[1].Value)!;
    }

    /// <summary>Embedded spans by span id, with their service.</summary>
    private static Dictionary<string, (JsonNode Span, string Service)> Spans(string html) =>
        DataBlock(html, "otlp-traces").AsArray()
            .SelectMany(r => r!["resourceSpans"]!.AsArray())
            .SelectMany(rs => rs!["scopeSpans"]!.AsArray().SelectMany(ss => ss!["spans"]!.AsArray())
                .Select(s => (Span: s!, Service: (string)rs["resource"]!["attributes"]!.AsArray()
                    .First(a => (string?)a!["key"] == "service.name")!["value"]!["stringValue"]!)))
            .GroupBy(x => (string)x.Span["spanId"]!)
            .ToDictionary(g => g.Key, g => g.First());

    private static string? Attribute(JsonNode span, string key)
    {
        var value = span["attributes"]?.AsArray().FirstOrDefault(a => (string?)a!["key"] == key)?["value"];
        return value is null ? null : (value["intValue"] ?? value["stringValue"])?.ToString();
    }

    /// <summary>The index entry of the When step of the scenario.</summary>
    private static JsonNode WhenStep(string html, string scenarioName)
    {
        var scenario = DataBlock(html, "run-index")["features"]!.AsArray()
            .SelectMany(f => f!["scenarios"]!.AsArray()).Single(s => (string?)s!["name"] == scenarioName)!;
        var step = scenario["steps"]!.AsArray().Single(s => ((string?)s!["text"])?.StartsWith("the customer orders", StringComparison.Ordinal) == true)!;
        Assert.That((string?)step["keyword"], Is.EqualTo("When"));
        return step;
    }

    /// <summary>Embedded log record by its index id "request.resource.scope.record", with its service.</summary>
    private static (JsonNode Log, string Service) Log(string html, string id)
    {
        var p = id.Split('.').Select(int.Parse).ToArray();
        var resourceLogs = DataBlock(html, "otlp-logs")[p[0]]!["resourceLogs"]![p[1]]!;
        var service = (string)resourceLogs["resource"]!["attributes"]!.AsArray()
            .First(a => (string?)a!["key"] == "service.name")!["value"]!["stringValue"]!;
        return (resourceLogs["scopeLogs"]![p[2]]!["logRecords"]![p[3]]!, service);
    }

    [Test]
    public void When_step_of_red_scenario_has_the_Orders_warning_about_the_failed_Pricing_call_under_an_Orders_span()
    {
        var html = Page();
        var step = WhenStep(html, Red);

        var warning = step["logIds"]!.AsArray().Select(id => Log(html, (string)id!))
            .Where(l => ((string?)l.Log["body"]?["stringValue"])?.Contains("Pricing request failed") == true)
            .ToList();
        Assert.That(warning, Has.Count.EqualTo(1), "the Orders warning is not attributed to the When step");
        var (log, service) = warning[0];
        Assert.That(service, Is.EqualTo("orders-api"));
        Assert.That((int?)log["severityNumber"], Is.InRange(13, 16), "WRN");
        var spans = Spans(html);
        var spanId = (string?)log["spanId"];
        Assert.That(spanId, Is.Not.Null.And.Not.Empty, "the warning has no span context");
        Assert.That(spans.TryGetValue(spanId!, out var span) && span.Service == "orders-api", $"span {spanId} of the warning is not an embedded Orders span");
        Assert.That(step["spanIds"]!.AsArray().Select(x => (string?)x), Does.Contain(spanId), "the Orders span is in the same step");
    }

    [Test]
    public void When_step_of_red_scenario_has_the_Pricing_span_with_status_code_500_in_the_index()
    {
        var html = Page();
        var step = WhenStep(html, Red);

        var spans = Spans(html);
        var stepSpans = step["spanIds"]!.AsArray().Select(id => spans[(string)id!]).ToList();
        Assert.That(stepSpans, Has.Some.Matches<(JsonNode Span, string Service)>(s =>
            s.Service == "pricing-api" && Attribute(s.Span, "http.response.status_code") == "500"),
            "no Pricing span with status code 500 attributed to the When step");
    }

    [Test]
    public void When_step_of_green_scenario_has_the_Orders_trace_with_the_Pricing_call_and_Postgres_queries_and_logs_of_both_services()
    {
        var html = Page();
        var step = WhenStep(html, Green);
        var spans = Spans(html);
        var stepSpans = step["spanIds"]!.AsArray().Select(id => spans[(string)id!]).ToList();

        // OTLP span kind: 2 = Server, 3 = Client.
        var post = stepSpans.Single(s => s.Service == "orders-api" && (int?)s.Span["kind"] == 2 && ((string?)s.Span["name"])?.StartsWith("POST", StringComparison.Ordinal) == true);
        var trace = stepSpans.Where(s => (string?)s.Span["traceId"] == (string?)post.Span["traceId"]).ToList();
        Assert.That(trace, Has.Some.Matches<(JsonNode Span, string Service)>(s => s.Service == "orders-api" && (int?)s.Span["kind"] == 3), "no Orders client call");
        Assert.That(trace, Has.Some.Matches<(JsonNode Span, string Service)>(s =>
            s.Service == "pricing-api" && (int?)s.Span["kind"] == 2 && Attribute(s.Span, "http.response.status_code") == "200"), "no Pricing server span with 200");
        Assert.That(trace, Has.Some.Matches<(JsonNode Span, string Service)>(s => Attribute(s.Span, "db.query.text") is not null), "no Postgres query");

        Assert.That(step["logIds"]!.AsArray().Select(id => Log(html, (string)id!).Service).Distinct(),
            Is.SupersetOf(new[] { "orders-api", "pricing-api" }), "logs of both services in the When step");
    }

    [Test]
    public void Failed_step_of_red_scenario_carries_the_full_error_with_stack_trace()
    {
        var html = Page();
        var feature = DataBlock(html, "run-index")["features"]!.AsArray()
            .Single(f => f!["scenarios"]!.AsArray().Any(s => (string?)s!["name"] == Red))!;
        var failed = feature["scenarios"]!.AsArray().Single(s => (string?)s!["name"] == Red)!["steps"]!.AsArray()
            .Single(s => (string?)s!["status"] == "failed")!;
        Assert.That((string?)failed["error"], Does.Contain("Expected").And.Contain(" at App.Specs.Steps.CheckoutSteps"));
    }

    [Test]
    public void Every_log_attributed_to_a_step_is_also_attributed_to_its_scenario_and_embedded_in_the_page()
    {
        var html = Page();
        foreach (var scenario in DataBlock(html, "run-index")["features"]!.AsArray().SelectMany(f => f!["scenarios"]!.AsArray()))
        {
            var ofScenario = scenario!["logIds"]!.AsArray().Select(x => (string)x!).ToHashSet();
            foreach (var id in scenario["steps"]!.AsArray().SelectMany(s => s!["logIds"]!.AsArray()).Select(x => (string)x!))
            {
                Assert.That(ofScenario, Does.Contain(id), (string?)scenario["name"]);
                Assert.That(Log(html, id).Log["timeUnixNano"] ?? Log(html, id).Log["observedTimeUnixNano"], Is.Not.Null, $"log {id}");
            }
        }
    }

    [Test]
    public void Every_span_attributed_to_a_step_is_also_attributed_to_its_scenario_and_embedded_in_the_page()
    {
        var html = Page();
        var spans = Spans(html);
        foreach (var scenario in DataBlock(html, "run-index")["features"]!.AsArray().SelectMany(f => f!["scenarios"]!.AsArray()))
        {
            var ofScenario = scenario!["spanIds"]!.AsArray().Select(x => (string)x!).ToHashSet();
            foreach (var id in scenario["steps"]!.AsArray().SelectMany(s => s!["spanIds"]!.AsArray()).Select(x => (string)x!))
            {
                Assert.That(ofScenario, Does.Contain(id), (string?)scenario["name"]);
                Assert.That(spans.ContainsKey(id), $"span {id} of the index is not embedded");
            }
        }
    }

    private static string? Body(JsonNode log) => (string?)log["body"]?["stringValue"];

    [Test]
    public void Startup_telemetry_is_Unattributed_in_the_BeforeTestRun_window_and_not_attributed_to_any_scenario()
    {
        var html = Page();
        var index = DataBlock(html, "run-index");
        var un = index["unattributed"]!;
        var ofScenarios = index["features"]!.AsArray().SelectMany(f => f!["scenarios"]!.AsArray())
            .SelectMany(s => s!["logIds"]!.AsArray()).Select(x => (string)x!).ToHashSet();
        var unLogs = un["logIds"]!.AsArray().Select(x => (string)x!).ToList();
        string[] startup = ["Application started", "CREATE TABLE \"Orders\""];

        foreach (var text in startup)
        {
            Assert.That(unLogs.Where(id => Body(Log(html, id).Log)?.Contains(text) == true), Is.Not.Empty, $"'{text}' is not Unattributed");
            Assert.That(ofScenarios.Where(id => Body(Log(html, id).Log)?.Contains(text) == true), Is.Empty, $"'{text}' is attributed to a scenario");
        }
        Assert.That(unLogs.Intersect(ofScenarios), Is.Empty, "a log is both Unattributed and in a scenario");

        var hooks = un["hooks"]!.AsArray();
        Assert.That(hooks.Select(h => (string?)h!["name"]), Has.None.Contains("RunHooks").And.None.Contains("TelemetryHooks"), "the plugin's own hooks are not recorded");
        var beforeTestRun = hooks.Single(h => (string?)h!["hookType"] == "BeforeTestRun")!;
        Assert.That((string?)beforeTestRun["name"], Is.EqualTo("AppHostHooks.StartAppHost"));
        Assert.That(beforeTestRun["logIds"]!.AsArray().Select(id => Body(Log(html, (string)id!).Log)),
            Has.Some.Contains("Application started"), "the AppHost start is in the BeforeTestRun window");
        Assert.That(hooks.Select(h => ((string?)h!["hookType"], (string?)h["name"])), Does.Contain(("AfterTestRun", "AppHostHooks.StopAppHost")));
        Assert.That(ulong.Parse((string)un["startUnixNano"]!, CultureInfo.InvariantCulture), Is.LessThanOrEqualTo(ulong.Parse((string)beforeTestRun["startUnixNano"]!, CultureInfo.InvariantCulture)), "the run scale starts before its first hook");
    }

    [Test]
    public void Telemetry_the_SUT_exports_while_the_AfterTestRun_hook_stops_it_is_in_the_viewer()
    {
        var html = Page();
        var afterTestRun = DataBlock(html, "run-index")["unattributed"]!["hooks"]!.AsArray()
            .Single(h => (string?)h!["hookType"] == "AfterTestRun" && (string?)h["name"] == "AppHostHooks.StopAppHost")!;

        Assert.That(afterTestRun["logIds"]!.AsArray().Select(id => Body(Log(html, (string)id!).Log)),
            Has.Some.Contains("Application is shutting down"), "the files are written after the test project's AfterTestRun hooks");
    }

    [Test]
    public void Every_scenario_records_the_BeforeScenario_hook_of_the_test_project_with_its_Orders_request_and_no_hook_of_the_plugin()
    {
        var html = Page();
        var spans = Spans(html);
        foreach (var scenario in DataBlock(html, "run-index")["features"]!.AsArray().SelectMany(f => f!["scenarios"]!.AsArray()))
        {
            var name = (string?)scenario!["name"];
            var hooks = scenario["hooks"]!.AsArray();
            Assert.That(hooks.Select(h => ((string?)h!["hookType"], (string?)h["name"])),
                Is.EqualTo(new[] { ("BeforeScenario", "OrdersReadinessHooks.EnsureOrdersAnswers") }), $"{name}: the plugin's own hooks are not recorded");
            var hook = hooks.Single()!;
            Assert.That(hook["spanIds"]!.AsArray().Select(id => spans[(string)id!]), Has.Some.Matches<(JsonNode Span, string Service)>(s =>
                s.Service == "orders-api" && Attribute(s.Span, "http.response.status_code") == "404"), $"{name}: the readiness request is not in the hook window");
            var first = scenario["steps"]![0]!;
            Assert.That(ulong.Parse((string)hook["endUnixNano"]!, CultureInfo.InvariantCulture), Is.LessThan(ulong.Parse((string)first["startUnixNano"]!, CultureInfo.InvariantCulture)),
                $"{name}: the hook window overlaps the first step");
        }
    }
}
