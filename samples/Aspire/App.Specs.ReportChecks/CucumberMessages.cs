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

    /// <summary>Names of the scenarios (pickles) of the run.</summary>
    public IReadOnlyList<string> ScenarioNames() => Of("pickle").Select(p => (string)p["name"]!).ToList();

    /// <summary>Media type, file name (null for log messages) and body of every attachment of the run.</summary>
    public IReadOnlyList<(string? MediaType, string? FileName, string? Body)> Attachments() =>
        Of("attachment").Select(a => ((string?)a["mediaType"], (string?)a["fileName"], (string?)a["body"])).ToList();
}
