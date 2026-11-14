using NUnit.Framework;
using Reqnroll;
using Storks.OpenTelemetry.Reqnroll;

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

        public void WriteLine(string format, params object[] args) => WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args));

        public void AddAttachment(string filePath) => throw new NotSupportedException("the plugin attaches no files");
    }

    private static TelemetryHooks Hooks(IReqnrollOutputHelper output, PluginDiagnostics diagnostics, ScenarioState state, IWindowOverlap? overlap = null) =>
        new(output, diagnostics, state, new RunTracker(new RunRecord(), diagnostics, overlap));

    [Test]
    public void Overlap_warning_of_a_parallel_scenario_is_written_to_its_test_output()
    {
        var diagnostics = new PluginDiagnostics();
        var record = new RunRecord();
        var tracker = new RunTracker(record, diagnostics);
        var first = new TelemetryHooks(new FakeOutput(failOnWrite: false), diagnostics,
            new ScenarioState { Run = record.Begin("Checkout", "Pay by card", [], DateTimeOffset.UtcNow) }, tracker);
        var output = new FakeOutput(failOnWrite: false);
        var second = new TelemetryHooks(output, diagnostics,
            new ScenarioState { Run = record.Begin("Pricing", "Apply discount", [], DateTimeOffset.UtcNow) }, tracker);

        first.OpenWindow();
        second.OpenWindow();
        second.FlushDiagnostics();

        Assert.That(output.Lines, Has.One.StartWith("[Storks.OpenTelemetry] Window Overlap")
            .And.Contain("'Pricing: Apply discount'").And.Contain("'Checkout: Pay by card'"));
    }

    [Test]
    public void Failing_overlap_check_does_not_change_the_scenario_and_is_written_to_test_output()
    {
        var output = new FakeOutput(failOnWrite: false);
        var diagnostics = new PluginDiagnostics();
        var state = new ScenarioState { Run = new RunRecord().Begin("F", "S", [], DateTimeOffset.UnixEpoch) };
        var hooks = Hooks(output, diagnostics, state, new FailingWindowOverlap());

        var beforeOpen = DateTimeOffset.UtcNow;
        Assert.DoesNotThrow(hooks.OpenWindow);
        Assert.DoesNotThrow(hooks.CloseWindow);
        state.Run.Finish(DateTimeOffset.UtcNow.AddSeconds(1), "passed", null);
        hooks.FlushDiagnostics();

        Assert.That(state.Run.Start, Is.GreaterThanOrEqualTo(beforeOpen), "the window is still opened");
        Assert.That(state.Run.End, Is.LessThan(DateTimeOffset.UtcNow.AddSeconds(1)), "the window is still closed by the hook");
        Assert.That(state.Run.Status, Is.EqualTo("passed"));
        Assert.That(output.Lines, Has.Some.Contain("overlap check is broken"));
    }

    [Test]
    public void Receiver_errors_are_flushed_to_test_output_after_the_scenario()
    {
        var output = new FakeOutput(failOnWrite: false);
        var diagnostics = new PluginDiagnostics();
        var hooks = Hooks(output, diagnostics, new ScenarioState());
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
        var hooks = Hooks(new FakeOutput(failOnWrite: true), diagnostics, new ScenarioState());

        Assert.DoesNotThrow(hooks.FlushDiagnostics);
    }
}
