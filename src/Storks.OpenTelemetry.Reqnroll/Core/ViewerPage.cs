using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace Storks.OpenTelemetry.Reqnroll.Core;

/// <summary>
/// The self-contained Telemetry Viewer page: it embeds the received requests as the same OTLP/JSON as the .jsonl files,
/// so the viewer shows them without loss.
/// </summary>
internal static class ViewerPage
{
    private const string IndexPlaceholder = "__RUN_INDEX__";
    private const string TracesPlaceholder = "__OTLP_TRACES__";
    private const string LogsPlaceholder = "__OTLP_LOGS__";
    private const string ResourceName = "Storks.OpenTelemetry.Reqnroll.Viewer.telemetry.viewer.html";

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

    public static void Write(string path, RunRecord record, TelemetryStore store)
    {
        // Log ids are positions in the embedded requests, so the index is built from the very snapshot that is embedded.
        var telemetry = store.Snapshot();
        var index = RunIndex.Build(record, telemetry.Spans, telemetry.Logs);
        var html = Render(index, telemetry.TraceRequests, telemetry.LogRequests);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, html);
    }

    private static string JsonArray(IEnumerable<Google.Protobuf.IMessage> requests) =>
        ScriptSafe("[" + string.Join(",", requests.Select(OtlpJson.Serialize)) + "]");

    /// <summary>In JSON '&lt;' occurs only inside strings, where &lt; means the same; so no "&lt;/script" or "&lt;!--" can appear.</summary>
    private static string ScriptSafe(string json) => json.Replace("<", "\\u003c");
}
