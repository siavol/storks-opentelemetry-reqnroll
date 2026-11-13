using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace App.Specs.ReportChecks;

/// <summary>Minimal reader over the Cucumber Messages NDJSON written by Reqnroll.</summary>
public sealed class CucumberMessages
{
    private readonly List<JsonObject> _messages;

    private CucumberMessages(List<JsonObject> messages) => _messages = messages;

    public static string ReportDirectory =>
        Environment.GetEnvironmentVariable("APP_SPECS_REPORT_DIR")
        ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "App.Specs", "bin", "Debug", "net11.0", "reports"));

    public static CucumberMessages Load()
    {
        var path = Path.Combine(ReportDirectory, "reqnroll_report.ndjson");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Run tests/App.Specs first (needs docker). Not found: {path}");
        return new CucumberMessages(File.ReadLines(path).Where(l => l.Length > 0).Select(l => JsonNode.Parse(l)!.AsObject()).ToList());
    }

    private IEnumerable<JsonObject> Of(string kind) =>
        _messages.Where(m => m.ContainsKey(kind)).Select(m => m[kind]!.AsObject());

    /// <summary>Decoded attachment bodies of the scenario (pickle) with the given name.</summary>
    public IReadOnlyList<string> AttachmentsOfScenario(string scenarioName)
    {
        var pickleIds = Of("pickle").Where(p => (string?)p["name"] == scenarioName).Select(p => (string)p["id"]!).ToHashSet();
        var testCaseIds = Of("testCase").Where(t => pickleIds.Contains((string)t["pickleId"]!)).Select(t => (string)t["id"]!).ToHashSet();
        var startedIds = Of("testCaseStarted").Where(s => testCaseIds.Contains((string)s["testCaseId"]!)).Select(s => (string)s["id"]!).ToHashSet();

        return Of("attachment")
            .Where(a => a["testCaseStartedId"] is { } id && startedIds.Contains((string)id!))
            .Select(a => (string?)a["contentEncoding"] == "BASE64"
                ? Encoding.UTF8.GetString(Convert.FromBase64String((string)a["body"]!))
                : (string)a["body"]!)
            .ToList();
    }
}
