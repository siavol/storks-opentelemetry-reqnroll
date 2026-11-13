using GherkinOtel.ReqnrollPlugin;
using NUnit.Framework;
using Reqnroll;

namespace GherkinOtel.ReqnrollPlugin.Tests;

public class HookFaultToleranceTests
{
    private sealed class FakeOutput(bool failOnAttachment) : IReqnrollOutputHelper
    {
        public List<string> Lines { get; } = [];
        public List<string> Attachments { get; } = [];

        public void WriteLine(string message) => Lines.Add(message);
        public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(format, args));

        public void AddAttachment(string filePath)
        {
            if (failOnAttachment) throw new IOException("attachment storage is broken");
            Attachments.Add(filePath);
        }
    }

    [Test]
    public async Task Attachment_failure_is_written_to_test_output_and_does_not_throw()
    {
        var output = new FakeOutput(failOnAttachment: true);
        var hooks = new TelemetryHooks(output, new TelemetryStore(), new PluginOptions(TimeSpan.Zero), new RunWindows(), new PluginDiagnostics());
        hooks.OpenWindow();

        await hooks.AttachScenarioTelemetry();

        Assert.That(output.Lines, Has.Some.Contain("attachment storage is broken"));
    }

    [Test]
    public async Task Receiver_errors_are_flushed_to_test_output_after_the_scenario()
    {
        var output = new FakeOutput(failOnAttachment: false);
        var diagnostics = new PluginDiagnostics();
        var hooks = new TelemetryHooks(output, new TelemetryStore(), new PluginOptions(TimeSpan.Zero), new RunWindows(), diagnostics);
        diagnostics.Report("rejected invalid OTLP request");
        hooks.OpenWindow();

        await hooks.AttachScenarioTelemetry();

        Assert.That(output.Lines, Has.Some.Contain("rejected invalid OTLP request"));
        Assert.That(output.Attachments, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Scenario_window_is_recorded_so_that_it_is_excluded_from_the_run_remainder()
    {
        var output = new FakeOutput(failOnAttachment: false);
        var windows = new RunWindows();
        var hooks = new TelemetryHooks(output, new TelemetryStore(), new PluginOptions(TimeSpan.Zero), windows, new PluginDiagnostics());

        hooks.OpenWindow();
        await hooks.AttachScenarioTelemetry();

        Assert.That(windows.Snapshot(), Has.Count.EqualTo(1));
    }
}
