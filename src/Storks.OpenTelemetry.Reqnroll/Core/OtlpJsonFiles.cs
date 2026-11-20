using System.Text;

namespace Storks.OpenTelemetry.Reqnroll.Core;

/// <summary>Writes everything received over OTLP as OTLP/JSON Lines: one line per accepted request, no filtering.</summary>
internal static class OtlpJsonFiles
{
    public const string TracesFileName = "traces.jsonl";
    public const string LogsFileName = "logs.jsonl";

    public static void Write(string tracesPath, string logsPath, TelemetryStore store)
    {
        var utf8 = new UTF8Encoding(false);
        var telemetry = store.Snapshot();
        WriteLines(tracesPath, telemetry.TraceRequests.Select(OtlpJson.Serialize), utf8);
        WriteLines(logsPath, telemetry.LogRequests.Select(OtlpJson.Serialize), utf8);
    }

    private static void WriteLines(string path, IEnumerable<string> lines, Encoding encoding)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllLines(path, lines, encoding);
    }
}
