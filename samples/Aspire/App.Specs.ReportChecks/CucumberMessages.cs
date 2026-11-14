using System.Net;
using System.Text.RegularExpressions;
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
            throw new FileNotFoundException($"Run samples/Aspire/App.Specs first (needs docker). Not found: {path}");
        return new CucumberMessages(File.ReadLines(path).Where(l => l.Length > 0).Select(l => JsonNode.Parse(l)!.AsObject()).ToList());
    }

    private IEnumerable<JsonObject> Of(string kind) =>
        _messages.Where(m => m.ContainsKey(kind)).Select(m => m[kind]!.AsObject());

    /// <summary>Texts of the Gherkin steps of the scenario (pickle) with the given name.</summary>
    public IReadOnlyList<string> StepTexts(string scenarioName) =>
        Of("pickle").Single(p => (string?)p["name"] == scenarioName)["steps"]!.AsArray().Select(s => (string)s!["text"]!).ToList();

    /// <summary>Media type and file name (null for log messages) of every attachment of the run.</summary>
    public IReadOnlyList<(string? MediaType, string? FileName)> Attachments() =>
        Of("attachment").Select(a => ((string?)a["mediaType"], (string?)a["fileName"])).ToList();

    /// <summary>
    /// Telemetry links of the scenario's test case runs: log messages (<c>text/x.cucumber.log+plain</c>, written via
    /// <c>IReqnrollOutputHelper.WriteLine</c>) whose body is an <c>&lt;a href="telemetry.html#..."&gt;</c>. For each: the
    /// decoded href, the text of the Gherkin step it belongs to (null if not on a Gherkin step) and the type of the hook it
    /// belongs to (e.g. <c>AFTER_TEST_CASE</c>; null if not on a hook).
    /// </summary>
    public IReadOnlyList<(string Link, string? StepText, string? HookType)> TelemetryLinks(string scenarioName)
    {
        var pickle = Of("pickle").Single(p => (string?)p["name"] == scenarioName);
        var stepText = pickle["steps"]!.AsArray().ToDictionary(s => (string)s!["id"]!, s => (string)s!["text"]!);
        var testCase = Of("testCase").Single(t => (string?)t["pickleId"] == (string)pickle["id"]!);
        var testSteps = testCase["testSteps"]!.AsArray().ToDictionary(t => (string)t!["id"]!, t => t!.AsObject());
        var hookType = Of("hook").ToDictionary(h => (string)h["id"]!, h => (string?)h["type"]);
        var startedIds = Of("testCaseStarted").Where(s => (string?)s["testCaseId"] == (string)testCase["id"]!).Select(s => (string)s["id"]!).ToHashSet();

        return Of("attachment")
            .Where(a => (string?)a["mediaType"] == "text/x.cucumber.log+plain" && a["testCaseStartedId"] is { } id && startedIds.Contains((string)id!))
            .Select(a => (Message: a, Href: Regex.Match((string)a["body"]!, "<a\\s+href=\"(telemetry\\.html#[^\"]*)\"")))
            .Where(x => x.Href.Success)
            .Select(x =>
            {
                var step = (string?)x.Message["testStepId"] is { } testStepId && testSteps.TryGetValue(testStepId, out var s) ? s : null;
                var pickleStepId = (string?)step?["pickleStepId"];
                var hookId = (string?)step?["hookId"];
                return (WebUtility.HtmlDecode(x.Href.Groups[1].Value),
                    pickleStepId is null ? null : stepText[pickleStepId],
                    hookId is null ? null : hookType.GetValueOrDefault(hookId));
            })
            .ToList();
    }
}
