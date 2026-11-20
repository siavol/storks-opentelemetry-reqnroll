using Storks.OpenTelemetry.Reqnroll.Core;
using Storks.OpenTelemetry.Reqnroll.Receiver;

namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>End-of-run work, done by the formatter on TestRunFinished. Never fails the run: the tests have passed or failed on their own by then.</summary>
internal static class RunEnd
{
    // TestRunFinished comes after the test project's AfterTestRun hooks, so telemetry flushed by the SUT while shutting
    // down has been received. No pause is needed here: stopping the SUT waits for its processes, which export what they
    // have before exiting.
    public static async Task Finish(ReportFiles files, OtlpReceiver receiver, TelemetryStore store, RunMessages messages, RunRecord record,
        PluginDiagnostics diagnostics, RunTracker tracker, RunOutput output)
    {
        try
        {
            await receiver.DisposeAsync();
        }
        catch (Exception ex)
        {
            output.Error($"failed to stop the receiver: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            messages.Record(record, tracker, diagnostics);
        }
        catch (Exception ex)
        {
            output.Error($"failed to read the scenarios from Cucumber Messages: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            ViewerPage.Write(files.Viewer, record, store);
            // The only line of a normal run: the jsonl paths are in the configuration.
            output.Info($"telemetry written to {files.Viewer}");
        }
        catch (Exception ex)
        {
            output.Error($"failed to write the telemetry viewer: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            OtlpJsonFiles.Write(files.Traces, files.Logs, store);
        }
        catch (Exception ex)
        {
            output.Error($"failed to write OTLP/JSON telemetry: {ex.GetType().Name}: {ex.Message}");
        }

        // Before the drain, so that a failure to summarize is drained too.
        var summary = tracker.WindowOverlapSummary();
        foreach (var message in diagnostics.Drain()) output.Error(message);
        if (summary is not null) output.Error(summary);
    }
}
