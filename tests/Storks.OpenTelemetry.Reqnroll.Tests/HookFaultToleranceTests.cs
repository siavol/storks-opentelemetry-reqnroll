using Storks.OpenTelemetry.Reqnroll;
using NUnit.Framework;
using Reqnroll;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

public class HookFaultToleranceTests
{
    private sealed class FakeOutput(bool failOnWrite) : IReqnrollOutputHelper
    {
        public List<string> Lines { get; } = [];

        public void WriteLine(string message)
        {
            if (failOnWrite) throw new IOException("output is broken");
            Lines.Add(message);
        }

        public void WriteLine(string format, params object[] args) => WriteLine(string.Format(format, args));

        public void AddAttachment(string filePath) => throw new NotSupportedException("the plugin attaches no files");
    }

    [Test]
    public void Receiver_errors_are_flushed_to_test_output_after_the_scenario()
    {
        var output = new FakeOutput(failOnWrite: false);
        var diagnostics = new PluginDiagnostics();
        var hooks = new TelemetryHooks(output, diagnostics, new ScenarioState());
        diagnostics.Report("rejected invalid OTLP request");
        hooks.OpenWindow();

        hooks.FlushDiagnostics();

        Assert.That(output.Lines, Has.Some.Contain("rejected invalid OTLP request"));
    }

    [Test]
    public void Broken_output_does_not_fail_the_scenario()
    {
        var diagnostics = new PluginDiagnostics();
        diagnostics.Report("rejected invalid OTLP request");
        var hooks = new TelemetryHooks(new FakeOutput(failOnWrite: true), diagnostics, new ScenarioState());

        Assert.DoesNotThrow(hooks.FlushDiagnostics);
    }
}
