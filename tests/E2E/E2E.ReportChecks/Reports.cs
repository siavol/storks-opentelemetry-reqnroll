using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace E2E.ReportChecks;

/// <summary>The files an E2E.Specs run leaves in its reports directory.</summary>
internal static class Reports
{
    public static string Directory =>
        Environment.GetEnvironmentVariable("E2E_REPORT_DIR")
        ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "E2E.Specs", "bin", "Debug", "net11.0", "reports"));

    public static string Existing(string file)
    {
        var path = Path.Combine(Directory, file);
        Assert.That(File.Exists(path), $"Run tests/E2E/E2E.Specs first. Not found: {path}");
        return path;
    }

    /// <summary>One JSON object per non-empty line: Cucumber Messages NDJSON or OTLP/JSON Lines.</summary>
    public static List<JsonObject> JsonLines(string file) =>
        File.ReadLines(Existing(file)).Where(l => l.Length > 0).Select(l => JsonNode.Parse(l)!.AsObject()).ToList();

    public static string Viewer() => File.ReadAllText(Existing("telemetry.html"));

    /// <summary>A JSON data block embedded into telemetry.html.</summary>
    public static JsonNode DataBlock(string html, string id)
    {
        var block = Regex.Match(html, $"<script type=\"application/json\" id=\"{id}\">(.*?)</script>", RegexOptions.Singleline);
        Assert.That(block.Success, $"telemetry.html has no data block {id}");
        return JsonNode.Parse(block.Groups[1].Value)!;
    }

    public static IEnumerable<JsonNode> Scenarios(string html) =>
        DataBlock(html, "run-index")["features"]!.AsArray().SelectMany(f => f!["scenarios"]!.AsArray()).Select(s => s!);

    public static string ServiceName(JsonNode resourceTelemetry) =>
        (string)resourceTelemetry["resource"]!["attributes"]!.AsArray()
            .First(a => (string?)a!["key"] == "service.name")!["value"]!["stringValue"]!;
}
