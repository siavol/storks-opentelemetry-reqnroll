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

    /// <summary>Decoded attachment bodies of every step of the scenario, keyed by step text (steps without attachments map to an empty list).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> AttachmentsByStep(string scenarioName)
    {
        var pickle = Of("pickle").Single(p => (string?)p["name"] == scenarioName);
        var stepText = pickle["steps"]!.AsArray().ToDictionary(s => (string)s!["id"]!, s => (string)s!["text"]!);
        var testCase = Of("testCase").Single(t => (string?)t["pickleId"] == (string)pickle["id"]!);
        var stepOfTestStep = testCase["testSteps"]!.AsArray()
            .Where(t => t!["pickleStepId"] is not null)
            .ToDictionary(t => (string)t!["id"]!, t => stepText[(string)t!["pickleStepId"]!]);
        var startedIds = Of("testCaseStarted").Where(s => (string?)s["testCaseId"] == (string)testCase["id"]!).Select(s => (string)s["id"]!).ToHashSet();

        // Hook steps are shared test steps, so a step slice is attributed to the pickle step that ran last before it.
        var result = stepText.Values.Distinct().ToDictionary(t => t, _ => new List<string>());
        string? current = null;
        foreach (var m in _messages)
        {
            if (m["testStepStarted"] is JsonObject ts && startedIds.Contains((string)ts["testCaseStartedId"]!)
                && stepOfTestStep.TryGetValue((string)ts["testStepId"]!, out var text))
                current = text;
            else if (m["attachment"] is JsonObject a && a["testCaseStartedId"] is { } started && startedIds.Contains((string)started!)
                && (string?)a["fileName"] == "step-telemetry.txt" && current is not null)
                result[current].Add((string?)a["contentEncoding"] == "BASE64"
                    ? Encoding.UTF8.GetString(Convert.FromBase64String((string)a["body"]!))
                    : (string)a["body"]!);
        }
        return result.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value);
    }

    /// <summary>Decoded bodies of the scenario-level telemetry file (scenario-telemetry.txt) only, without step slices.</summary>
    public IReadOnlyList<string> ScenarioTelemetryOf(string scenarioName) =>
        AttachmentsOfScenario(scenarioName, "scenario-telemetry.txt");

    /// <summary>Decoded attachment bodies of the scenario (pickle) with the given name.</summary>
    public IReadOnlyList<string> AttachmentsOfScenario(string scenarioName, string? fileName = null)
    {
        var pickleIds = Of("pickle").Where(p => (string?)p["name"] == scenarioName).Select(p => (string)p["id"]!).ToHashSet();
        var testCaseIds = Of("testCase").Where(t => pickleIds.Contains((string)t["pickleId"]!)).Select(t => (string)t["id"]!).ToHashSet();
        var startedIds = Of("testCaseStarted").Where(s => testCaseIds.Contains((string)s["testCaseId"]!)).Select(s => (string)s["id"]!).ToHashSet();

        return Of("attachment")
            .Where(a => a["testCaseStartedId"] is { } id && startedIds.Contains((string)id!))
            .Where(a => fileName is null || (string?)a["fileName"] == fileName)
            .Select(a => (string?)a["contentEncoding"] == "BASE64"
                ? Encoding.UTF8.GetString(Convert.FromBase64String((string)a["body"]!))
                : (string)a["body"]!)
            .ToList();
    }
}
