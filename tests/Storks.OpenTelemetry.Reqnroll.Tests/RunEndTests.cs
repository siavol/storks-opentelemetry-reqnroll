using NUnit.Framework;
using Storks.OpenTelemetry.Reqnroll.Core;
using Storks.OpenTelemetry.Reqnroll.Receiver;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

/// <summary>The end-of-run lines go to the plugin's run output, each with the plugin's prefix.</summary>
public class RunEndTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task End_of_run_writes_the_files_where_configured_and_the_overlap_summary_to_the_run_output()
    {
        var dir = Path.Combine(Path.GetTempPath(), "storks-otel-tests", Guid.NewGuid().ToString("N"));
        var files = new ReportFiles(Path.Combine(dir, "v", "run.html"), Path.Combine(dir, "t", "traces.jsonl"), Path.Combine(dir, "logs.jsonl"));
        var store = new TelemetryStore();
        var record = new RunRecord();
        var diagnostics = new PluginDiagnostics();
        var tracker = new RunTracker(record, diagnostics);
        tracker.WindowOpened(record.Begin("Checkout", "Pay by card", [], T0), T0);
        tracker.WindowOpened(record.Begin("Pricing", "Apply discount", [], T0), T0.AddMilliseconds(10));
        using var info = new StringWriter();
        using var errors = new StringWriter();
        await using var receiver = new OtlpReceiver(store);

        await RunEnd.Finish(files, receiver, store, record, diagnostics, tracker, new RunOutput(info, errors));

        Assert.That(new[] { files.Viewer, files.Traces, files.Logs }.Select(File.Exists), Is.All.True);
        Assert.That(Lines(info), Has.Some.EqualTo($"[Storks.OpenTelemetry] telemetry viewer written to {files.Viewer}"));
        Assert.That(Lines(errors), Has.One.StartWith("[Storks.OpenTelemetry] Window Overlap: 1 scenario window"),
            "the summary; the scenario's own warning was drained here too, since no scenario hook flushed it");
    }

    [Test]
    public void Broken_run_output_does_not_fail_the_run_hook()
    {
        var output = new RunOutput(new BrokenWriter(), new BrokenWriter());

        Assert.DoesNotThrow(() => output.Info("telemetry viewer written"));
        Assert.DoesNotThrow(() => output.Error("failed to stop the receiver"));
    }

    private static string[] Lines(StringWriter writer) => writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    private sealed class BrokenWriter : TextWriter
    {
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
        public override void Write(char value) => throw new IOException("console is gone");
    }
}
