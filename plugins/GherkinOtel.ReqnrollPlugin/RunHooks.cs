using Reqnroll;

namespace GherkinOtel.ReqnrollPlugin;

/// <summary>End-of-run work: writes the Unattributed Telemetry file and stops the receiver. Never fails the run.</summary>
[Binding]
public static class RunHooks
{
    // Runs after the test project's own AfterTestRun hooks (default order 10000), so that telemetry
    // flushed by the SUT while shutting down is still received.
    [AfterTestRun(Order = int.MaxValue)]
    public static async Task FinishRun(OtlpReceiver receiver, TelemetryStore store, RunWindows windows, PluginDiagnostics diagnostics)
    {
        try
        {
            var path = UnattributedReport.Write(UnattributedReport.DefaultDirectory(), store, windows);
            Console.WriteLine($"[GherkinOtel] unattributed telemetry written to {path}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GherkinOtel] failed to write unattributed telemetry: {ex}");
        }

        try
        {
            await receiver.DisposeAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GherkinOtel] failed to stop the receiver: {ex}");
        }

        foreach (var message in diagnostics.Drain()) Console.Error.WriteLine($"[GherkinOtel] {message}");
    }
}
