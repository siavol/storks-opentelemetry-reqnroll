using System.Reflection;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace GherkinOtel.ReqnrollPlugin;

/// <summary>
/// Renders and writes the self-contained Telemetry Viewer page (<c>telemetry.html</c>). Data goes into
/// <c>&lt;script type="application/json"&gt;</c> blocks: the attribution index (<c>run-index</c>) and the received
/// trace and log requests as arrays of OTLP/JSON (<c>otlp-traces</c>, <c>otlp-logs</c>), the same objects as the lines of
/// <c>traces.jsonl</c> and <c>logs.jsonl</c>. Logs in the index are referred to by <see cref="TelemetryLog.Id"/>, which the
/// OTLP receiver sets to the position of the record in <c>otlp-logs</c>.
/// </summary>
public static class ViewerPage
{
    private const string IndexPlaceholder = "__RUN_INDEX__";
    private const string TracesPlaceholder = "__OTLP_TRACES__";
    private const string LogsPlaceholder = "__OTLP_LOGS__";
    private const string ResourceName = "GherkinOtel.ReqnrollPlugin.Viewer.telemetry.viewer.html";

    public static string Render(RunIndex index, IReadOnlyList<ExportTraceServiceRequest> traces, IReadOnlyList<ExportLogsServiceRequest> logs)
    {
        using var stream = typeof(ViewerPage).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"embedded resource {ResourceName} not found");
        using var reader = new StreamReader(stream);
        var template = reader.ReadToEnd();
        // ToJson() is ASCII-only, so it cannot contain a literal "</script>".
        return template
            .Replace(IndexPlaceholder, index.ToJson())
            .Replace(TracesPlaceholder, JsonArray(traces))
            .Replace(LogsPlaceholder, JsonArray(logs));
    }

    public static string Write(string directory, RunRecord record, TelemetryStore store)
    {
        // Log ids are positions in the embedded requests, so the index is built from the very snapshot that is embedded.
        var telemetry = store.Snapshot();
        var index = RunIndex.Build(record, telemetry.Spans, telemetry.Logs);
        var html = Render(index, telemetry.TraceRequests, telemetry.LogRequests);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, TelemetryLinks.PageName);
        File.WriteAllText(path, html);
        return path;
    }

    private static string JsonArray(IEnumerable<Google.Protobuf.IMessage> requests) =>
        ScriptSafe("[" + string.Join(",", requests.Select(OtlpJson.Serialize)) + "]");

    /// <summary>In JSON '&lt;' occurs only inside strings, where < means the same; so no "&lt;/script" or "&lt;!--" can appear.</summary>
    private static string ScriptSafe(string json) => json.Replace("<", "\\u003c");
}
