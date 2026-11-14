using System.Text;

namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>Writes everything received over OTLP as OTLP/JSON Lines: one line per accepted request, no filtering.</summary>
public static class OtlpJsonFiles
{
    public const string TracesFileName = "traces.jsonl";
    public const string LogsFileName = "logs.jsonl";

    /// <summary>Writes both files (empty if nothing was received) and returns the directory.</summary>
    public static string Write(string directory, TelemetryStore store)
    {
        Directory.CreateDirectory(directory);
        var utf8 = new UTF8Encoding(false);
        var telemetry = store.Snapshot();
        File.WriteAllLines(Path.Combine(directory, TracesFileName), telemetry.TraceRequests.Select(OtlpJson.Serialize), utf8);
        File.WriteAllLines(Path.Combine(directory, LogsFileName), telemetry.LogRequests.Select(OtlpJson.Serialize), utf8);
        return directory;
    }
}
