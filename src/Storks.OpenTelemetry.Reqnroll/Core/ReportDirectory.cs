namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>The folder the plugin writes its end-of-run files to (telemetry.html, traces.jsonl, logs.jsonl).</summary>
internal static class ReportDirectory
{
    /// <summary>Env var overriding the folder; the default is the "reports" folder next to the test assembly.</summary>
    public const string Variable = "STORKS_OTEL_REPORT_DIR";

    /// <summary>
    /// The folder where App.Specs' reqnroll.json sends the formatter output (relative paths there resolve against the
    /// output directory), i.e. bin/&lt;cfg&gt;/&lt;tfm&gt;/reports, unless <see cref="Variable"/> is set.
    /// </summary>
    public static string Resolve() =>
        Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } dir
            ? dir
            : Path.Combine(AppContext.BaseDirectory, "reports");
}
