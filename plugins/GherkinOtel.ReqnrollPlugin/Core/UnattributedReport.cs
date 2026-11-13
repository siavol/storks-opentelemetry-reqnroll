namespace GherkinOtel.ReqnrollPlugin;

/// <summary>Writes the Unattributed Telemetry file (same text representation as Scenario Telemetry).</summary>
public static class UnattributedReport
{
    public const string FileName = "unattributed-telemetry.txt";

    /// <summary>Env var overriding the directory of the file; the default is the "reports" folder next to the test assembly.</summary>
    public const string DirectoryVariable = "GHERKINOTEL_REPORT_DIR";

    /// <summary>
    /// Where the file goes by default: the folder where App.Specs' reqnroll.json sends the formatter
    /// output (relative paths there resolve against the output directory), i.e. bin/&lt;cfg&gt;/&lt;tfm&gt;/reports.
    /// </summary>
    public static string DefaultDirectory() =>
        Environment.GetEnvironmentVariable(DirectoryVariable) is { Length: > 0 } dir
            ? dir
            : Path.Combine(AppContext.BaseDirectory, "reports");

    /// <summary>Writes the file with everything in <paramref name="store"/> not contained in any of <paramref name="windows"/>.</summary>
    public static string Write(string directory, TelemetryStore store, RunWindows windows)
    {
        var rest = RunAttribution.Unattributed(store.Snapshot(), windows.Snapshot());
        // Times are relative to the first unattributed record, since there is no scenario start here.
        var origin = rest.Count > 0 ? rest[0].Timestamp : DateTimeOffset.UtcNow;
        var text = TelemetryRenderer.Render(new ScenarioTelemetry(new ObservationWindow(origin, origin), rest));

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, FileName);
        File.WriteAllText(path, text);
        return path;
    }
}
