using NUnit.Framework;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

public class RunTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    /// <summary>The README section on the repository, reachable from a package user's test output.</summary>
    private const string WindowOverlapSection = "https://github.com/OWNER/storks-opentelemetry-reqnroll/blob/main/README.md#window-overlap";

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

    [Test]
    public void Window_opened_while_another_is_open_warns_with_both_scenarios_and_the_readme_section()
    {
        var diagnostics = new PluginDiagnostics();
        var record = new RunRecord();
        var tracker = new RunTracker(record, diagnostics);
        var checkout = record.Begin("Checkout", "Pay by card", [], T0);
        var pricing = record.Begin("Pricing", "Apply discount", [], T0);

        tracker.WindowOpened(checkout, T0);
        tracker.WindowOpened(pricing, T0.AddMilliseconds(10));

        var warning = diagnostics.Drain().Single();
        Assert.That(warning, Does.Contain("Window Overlap")
            .And.Contain("'Pricing: Apply discount'")
            .And.Contain("'Checkout: Pay by card'")
            .And.Contain("shows up in each of the overlapping scenarios")
            .And.Contain(WindowOverlapSection));
        Assert.That(pricing.Start, Is.EqualTo(T0.AddMilliseconds(10)), "the window is opened as before");
    }

    [Test]
    public void Windows_one_after_another_give_no_warning_and_no_summary()
    {
        var diagnostics = new PluginDiagnostics();
        var record = new RunRecord();
        var tracker = new RunTracker(record, diagnostics);
        var first = record.Begin("Checkout", "Pay by card", [], T0);
        var second = record.Begin("Checkout", "Pay by card", [], T0);

        tracker.WindowOpened(first, T0);
        tracker.WindowClosed(first, T0.AddMilliseconds(5));
        tracker.WindowOpened(second, T0.AddMilliseconds(6));
        tracker.WindowClosed(second, T0.AddMilliseconds(9));

        Assert.That(diagnostics.Drain(), Is.Empty);
        Assert.That(tracker.WindowOverlapSummary(), Is.Null);
        Assert.That(first.End, Is.EqualTo(T0.AddMilliseconds(5)), "the window is closed as before");
    }

    [Test]
    public void Summary_counts_the_windows_that_opened_into_an_overlap()
    {
        var diagnostics = new PluginDiagnostics();
        var record = new RunRecord();
        var tracker = new RunTracker(record, diagnostics);
        var a = record.Begin("F", "A", [], T0);
        var b = record.Begin("F", "B", [], T0);
        var c = record.Begin("F", "C", [], T0);
        var d = record.Begin("F", "D", [], T0);

        tracker.WindowOpened(a, T0);
        tracker.WindowOpened(b, T0.AddMilliseconds(1));   // overlaps A
        tracker.WindowClosed(a, T0.AddMilliseconds(2));
        tracker.WindowOpened(c, T0.AddMilliseconds(3));   // overlaps B
        tracker.WindowClosed(b, T0.AddMilliseconds(4));
        tracker.ScenarioFinished(c, T0.AddMilliseconds(5), "failed", "boom"); // closing hook did not run
        tracker.WindowOpened(d, T0.AddMilliseconds(6));   // alone

        Assert.That(diagnostics.Drain(), Has.Count.EqualTo(2));
        Assert.That(tracker.WindowOverlapSummary(), Does.Contain("Window Overlap: 2 scenario windows")
            .And.Contain("shows up in each of the overlapping scenarios")
            .And.Contain(WindowOverlapSection));
    }

    [Test]
    public void Failing_overlap_summary_gives_no_summary_and_is_reported_as_a_diagnostic()
    {
        var diagnostics = new PluginDiagnostics();
        var tracker = new RunTracker(new RunRecord(), diagnostics, new FailingWindowOverlap());

        Assert.That(tracker.WindowOverlapSummary(), Is.Null);
        Assert.That(diagnostics.Drain(), Has.One.Contain("failed to summarize Window Overlap").And.Contain("overlap check is broken"));
    }
}
