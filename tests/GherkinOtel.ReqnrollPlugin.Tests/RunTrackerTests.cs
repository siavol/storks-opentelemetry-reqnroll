using NUnit.Framework;

namespace GherkinOtel.ReqnrollPlugin.Tests;

public class RunTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    private sealed class FakeLinkWriter(bool fail = false) : ILinkWriter
    {
        public List<string> Links { get; } = [];
        public void WriteLink(string link)
        {
            if (fail) throw new IOException("output broken");
            Links.Add(link);
        }
    }

    [Test]
    public void Finished_step_gets_numbered_link_and_is_recorded()
    {
        var record = new RunRecord();
        var tracker = new RunTracker(record, new PluginDiagnostics());
        var run = record.Begin("F", "S", [], T0);
        var writer = new FakeLinkWriter();

        tracker.StepFinished(run, writer, "Given", "a", T0, T0.AddMilliseconds(1), "passed", null);
        tracker.StepFinished(run, writer, "When", "b", T0, T0.AddMilliseconds(1), "failed", "boom");

        Assert.That(writer.Links, Is.EqualTo(new[] { $"telemetry.html#s={run.Id}&step=1", $"telemetry.html#s={run.Id}&step=2" }));
        Assert.That(run.Steps.Select(s => s.Status), Is.EqualTo(new[] { "passed", "failed" }));
    }

    [Test]
    public void Link_write_failure_is_reported_as_diagnostics_and_does_not_throw()
    {
        var diagnostics = new PluginDiagnostics();
        var record = new RunRecord();
        var tracker = new RunTracker(record, diagnostics);
        var run = record.Begin("F", "S", [], T0);

        tracker.StepFinished(run, new FakeLinkWriter(fail: true), "Given", "a", T0, T0, "passed", null);
        tracker.WriteScenarioLink(run, new FakeLinkWriter(fail: true));

        Assert.That(diagnostics.Drain(), Has.Count.EqualTo(2).And.All.Contain("output broken"));
        Assert.That(run.Steps, Has.Count.EqualTo(1), "step is still recorded");
    }

    [Test]
    public void Scenario_hook_call_is_recorded_with_type_method_and_window_but_hooks_of_the_plugin_are_not()
    {
        var record = new RunRecord();
        var tracker = new RunTracker(record, new PluginDiagnostics());
        var run = record.Begin("F", "S", [], T0);

        tracker.HookFinished(run, new HookRun("BeforeScenario", "AppHooks.ResetData", T0, T0.AddMilliseconds(5), "passed"), "App.Specs");
        tracker.HookFinished(run, new HookRun("AfterScenario", "TelemetryHooks.CloseWindow", T0.AddMilliseconds(6), T0.AddMilliseconds(7), "passed"),
            typeof(RunTracker).Assembly.GetName().Name!);

        Assert.That(run.Hooks, Is.EqualTo(new[] { new HookRun("BeforeScenario", "AppHooks.ResetData", T0, T0.AddMilliseconds(5), "passed") }));
    }

    [Test]
    public void Run_and_feature_hook_calls_are_recorded_on_the_run_but_hooks_of_the_plugin_are_not()
    {
        var record = new RunRecord(T0);
        var tracker = new RunTracker(record, new PluginDiagnostics());

        tracker.HookFinished(null, new HookRun("BeforeTestRun", "AppHostHooks.StartAppHost", T0, T0.AddSeconds(5), "passed"), "App.Specs");
        tracker.HookFinished(null, new HookRun("BeforeFeature", "FeatureHooks.Seed", T0.AddSeconds(6), T0.AddSeconds(7), "passed"), "App.Specs");
        tracker.HookFinished(null, new HookRun("AfterTestRun", "RunHooks.FinishRun", T0.AddSeconds(9), T0.AddSeconds(10), "passed"),
            typeof(RunTracker).Assembly.GetName().Name!);

        Assert.That(record.Start, Is.EqualTo(T0));
        Assert.That(record.RunHooks(), Is.EqualTo(new[]
        {
            new HookRun("BeforeTestRun", "AppHostHooks.StartAppHost", T0, T0.AddSeconds(5), "passed"),
            new HookRun("BeforeFeature", "FeatureHooks.Seed", T0.AddSeconds(6), T0.AddSeconds(7), "passed"),
        }));
    }
}
