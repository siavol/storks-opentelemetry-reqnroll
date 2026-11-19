namespace Storks.OpenTelemetry.Reqnroll.Core;

/// <summary>The folder the plugin writes its end-of-run files to (telemetry.html, traces.jsonl, logs.jsonl).</summary>
internal static class ReportDirectory
{
    /// <summary>Env var overriding the folder; the default is the "reports" folder next to the test assembly.</summary>
    public const string Variable = "STORKS_OTEL_REPORT_DIR";

    /// <summary>
    /// "reports" under the output directory, where a relative <c>outputFilePath</c> such as <c>reports/reqnroll_report.html</c>
    /// puts the Reqnroll report, so that the report's relative links reach the viewer.
    /// </summary>
    public static string Resolve() =>
        Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } dir
            ? dir
            : Path.Combine(AppContext.BaseDirectory, "reports");
}
