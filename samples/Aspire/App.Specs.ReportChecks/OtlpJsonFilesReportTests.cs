using System.Text.Json.Nodes;
using NUnit.Framework;

namespace App.Specs.ReportChecks;

/// <summary>traces.jsonl and logs.jsonl in the report folder: the whole run's telemetry as OTLP/JSON Lines.</summary>
public class OtlpJsonFilesReportTests
{
    private static List<JsonObject> Lines(string file)
    {
        var path = Path.Combine(CucumberMessages.ReportDirectory, file);
        Assert.That(File.Exists(path), $"Run samples/Aspire/App.Specs first. Not found: {path}");
        return File.ReadLines(path).Where(l => l.Length > 0).Select(l => JsonNode.Parse(l)!.AsObject()).ToList();
    }

    private static IEnumerable<string> ServiceNames(IEnumerable<JsonObject> lines, string resourceKey) =>
        lines.SelectMany(l => l[resourceKey]!.AsArray())
            .SelectMany(r => r!["resource"]!["attributes"]!.AsArray())
            .Where(a => (string?)a!["key"] == "service.name")
            .Select(a => (string)a!["value"]!["stringValue"]!);

    [Test]
    public void Traces_file_is_otlp_json_with_both_services_and_demo_spans()
    {
        var lines = Lines("traces.jsonl");

        Assert.That(ServiceNames(lines, "resourceSpans"), Does.Contain("orders-api").And.Contain("pricing-api"));
        var spans = lines.SelectMany(l => l["resourceSpans"]!.AsArray())
            .SelectMany(r => r!["scopeSpans"]!.AsArray()).SelectMany(s => s!["spans"]!.AsArray()).ToList();
        Assert.That(spans, Is.Not.Empty);
        Assert.That(spans.All(s => ((string)s!["traceId"]!).Length == 32 && ((string)s["spanId"]!).Length == 16), "ids must be hex");
        Assert.That(spans, Has.Some.Matches<JsonNode?>(s => ((string?)s!["name"] ?? "").Contains("/prices/")), "no Pricing span of the demo scenarios");
    }

    [Test]
    public void Logs_file_is_otlp_json_with_both_services_and_demo_logs()
    {
        var lines = Lines("logs.jsonl");

        Assert.That(ServiceNames(lines, "resourceLogs"), Does.Contain("orders-api").And.Contain("pricing-api"));
        var bodies = lines.SelectMany(l => l["resourceLogs"]!.AsArray())
            .SelectMany(r => r!["scopeLogs"]!.AsArray()).SelectMany(s => s!["logRecords"]!.AsArray())
            .Select(r => (string?)r!["body"]!["stringValue"]).ToList();
        Assert.That(bodies, Has.Some.Contain("Pricing request failed"), "no Orders warning of the red demo scenario");
    }
}
