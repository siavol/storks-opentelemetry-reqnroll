namespace Storks.OpenTelemetry.Reqnroll.Core;

/// <summary>The full paths of the files the Telemetry Formatter writes at the end of the run.</summary>
internal sealed record ReportFiles(string Viewer, string Traces, string Logs)
{
    /// <summary>
    /// From the formatter's configuration: <c>outputFilePath</c> (default <c>reports/telemetry.html</c>), <c>tracesFilePath</c> and
    /// <c>logsFilePath</c> (default next to the viewer). Relative paths are taken from the working directory, as the built-in
    /// formatters do; <paramref name="resolvePlaceholders"/> is applied to each configured path.
    /// </summary>
    public static ReportFiles From(IDictionary<string, object> configuration, Func<string, string> resolvePlaceholders)
    {
        string? Configured(string key) =>
            configuration.TryGetValue(key, out var value) && value?.ToString()?.Trim() is { Length: > 0 } path
                ? Path.GetFullPath(resolvePlaceholders(path))
                : null;

        var viewer = Configured("outputFilePath") ?? Path.GetFullPath(Path.Combine("reports", "telemetry.html"));
        var directory = Path.GetDirectoryName(viewer)!;
        return new ReportFiles(viewer,
            Configured("tracesFilePath") ?? Path.Combine(directory, OtlpJsonFiles.TracesFileName),
            Configured("logsFilePath") ?? Path.Combine(directory, OtlpJsonFiles.LogsFileName));
    }
}
