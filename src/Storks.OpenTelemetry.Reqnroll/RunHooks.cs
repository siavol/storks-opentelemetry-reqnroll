using Reqnroll;

namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>End-of-run work: writes the Telemetry Viewer page and the OTLP/JSON files, and stops the receiver. Never fails the run.</summary>
[Binding]
internal static class RunHooks
{
    // Runs after the test project's own AfterTestRun hooks (default order 10000), so that telemetry
    // flushed by the SUT while shutting down is still received. No pause is needed here: stopping the AppHost
    // waits for the SUT processes, which export what they have before exiting (ticket 09, demo run).
    [AfterTestRun(Order = int.MaxValue)]
    public static async Task FinishRun(OtlpReceiver receiver, TelemetryStore store, RunRecord record, PluginDiagnostics diagnostics, RunTracker tracker)
    {
        var directory = ReportDirectory.Resolve();
        try
        {
            var page = ViewerPage.Write(directory, record, store);
            Console.WriteLine($"[Storks.OpenTelemetry] telemetry viewer written to {page}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Storks.OpenTelemetry] failed to write the telemetry viewer: {ex}");
        }

        try
        {
            OtlpJsonFiles.Write(directory, store);
            Console.WriteLine($"[Storks.OpenTelemetry] OTLP/JSON telemetry written to {directory}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Storks.OpenTelemetry] failed to write OTLP/JSON telemetry: {ex}");
        }

        try
        {
            await receiver.DisposeAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Storks.OpenTelemetry] failed to stop the receiver: {ex}");
        }

        // Before the drain, so that a failure to summarize is drained too.
        var summary = tracker.WindowOverlapSummary();
        foreach (var message in diagnostics.Drain()) Console.Error.WriteLine($"[Storks.OpenTelemetry] {message}");
        if (summary is not null) Console.Error.WriteLine($"[Storks.OpenTelemetry] {summary}");
    }
}
