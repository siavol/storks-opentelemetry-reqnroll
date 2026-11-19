using Reqnroll;
using Storks.OpenTelemetry.Reqnroll.Core;
using Storks.OpenTelemetry.Reqnroll.Receiver;

namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>End-of-run work. Never fails the run: the tests have passed or failed on their own by then.</summary>
[Binding]
internal static class RunHooks
{
    // Runs after the test project's own AfterTestRun hooks (default order 10000), so that telemetry
    // flushed by the SUT while shutting down is still received. No pause is needed here: stopping the SUT
    // waits for its processes, which export what they have before exiting.
    [AfterTestRun(Order = int.MaxValue)]
    public static async Task FinishRun(OtlpReceiver receiver, TelemetryStore store, RunRecord record, PluginDiagnostics diagnostics,
        RunTracker tracker, RunOutput output)
    {
        var directory = ReportDirectory.Resolve();
        try
        {
            var page = ViewerPage.Write(directory, record, store);
            output.Info($"telemetry viewer written to {page}");
        }
        catch (Exception ex)
        {
            output.Error($"failed to write the telemetry viewer: {ex}");
        }

        try
        {
            OtlpJsonFiles.Write(directory, store);
            output.Info($"OTLP/JSON telemetry written to {directory}");
        }
        catch (Exception ex)
        {
            output.Error($"failed to write OTLP/JSON telemetry: {ex}");
        }

        try
        {
            await receiver.DisposeAsync();
        }
        catch (Exception ex)
        {
            output.Error($"failed to stop the receiver: {ex}");
        }

        // Before the drain, so that a failure to summarize is drained too.
        var summary = tracker.WindowOverlapSummary();
        foreach (var message in diagnostics.Drain()) output.Error(message);
        if (summary is not null) output.Error(summary);
    }
}
