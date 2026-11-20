using System.Text.Json.Nodes;
using NUnit.Framework;

namespace E2E.ReportChecks;

/// <summary>The whole run's telemetry as OTLP/JSON Lines, next to the viewer.</summary>
public class OtlpJsonFilesTests
{
    [TestCase("traces.jsonl", "resourceSpans", "scopeSpans", "spans")]
    [TestCase("logs.jsonl", "resourceLogs", "scopeLogs", "logRecords")]
    public void File_is_otlp_json_with_both_services(string file, string resourceKey, string scopeKey, string recordsKey)
    {
        var resources = Reports.JsonLines(file).SelectMany(l => l[resourceKey]!.AsArray()).ToList();

        Assert.That(resources.Select(r => Reports.ServiceName(r!)), Does.Contain("front-api").And.Contain("back-api"));
        Assert.That(resources.SelectMany(r => r![scopeKey]!.AsArray()).SelectMany(s => s![recordsKey]!.AsArray()),
            Has.All.Matches<JsonNode?>(r => r!["timeUnixNano"] is not null || r["startTimeUnixNano"] is not null), "OTLP/JSON records");
    }
}
