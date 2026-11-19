using System.Reflection;
using NUnit.Framework;
using Reqnroll;
using Storks.OpenTelemetry.Reqnroll.Core;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

/// <summary>The scenario's Observation Window is opened and closed by the plugin's own hooks, which stay outside it.</summary>
public class ScenarioWindowTests
{
    private sealed class NullOutput : IReqnrollOutputHelper
    {
        public void WriteLine(string message) { }
        public void WriteLine(string format, params object[] args) { }
        public void AddAttachment(string filePath) { }
    }

    private static int AfterScenarioOrder(Type type, string method) =>
        type.GetMethod(method)!.GetCustomAttribute<AfterScenarioAttribute>()!.Order;

    [Test]
    public void Plugin_closes_the_window_after_the_test_project_hooks_then_writes_the_scenario_link_then_flushes_diagnostics()
    {
        var close = AfterScenarioOrder(typeof(TelemetryHooks), nameof(TelemetryHooks.CloseWindow));
        var link = AfterScenarioOrder(typeof(ScenarioLinkHooks), nameof(ScenarioLinkHooks.WriteScenarioLink));
        var flush = AfterScenarioOrder(typeof(TelemetryHooks), nameof(TelemetryHooks.FlushDiagnostics));

        Assert.That(close, Is.GreaterThan(10000), "after test project hooks with the default order");
        Assert.That(new[] { close, link, flush }, Is.Ordered.Ascending.And.Unique);
    }

    [Test]
    public void Window_runs_from_the_plugin_opening_hook_to_its_closing_hook_not_to_the_end_of_the_scenario()
    {
        var record = new RunRecord();
        var state = new ScenarioState { Run = record.Begin("F", "S", [], DateTimeOffset.UtcNow.AddSeconds(-5)) };
        var diagnostics = new PluginDiagnostics();
        var hooks = new TelemetryHooks(new NullOutput(), diagnostics, state, new RunTracker(record, diagnostics));

        var beforeOpen = DateTimeOffset.UtcNow;
        hooks.OpenWindow();
        var afterOpen = DateTimeOffset.UtcNow;
        hooks.CloseWindow();
        var afterClose = DateTimeOffset.UtcNow;
        state.Run.Finish(afterClose.AddSeconds(1), "passed", null);

        Assert.That(state.Run.Start, Is.InRange(beforeOpen, afterOpen));
        Assert.That(state.Run.End, Is.InRange(afterOpen, afterClose), "plugin hooks after the closing one are outside the window");
        Assert.That(state.Run.Status, Is.EqualTo("passed"));
    }

    [Test]
    public void Without_the_closing_hook_the_window_ends_when_the_scenario_finishes()
    {
        var t0 = DateTimeOffset.UnixEpoch;
        var run = new RunRecord().Begin("F", "S", [], t0);

        run.Finish(t0.AddSeconds(1), "failed", "boom");

        Assert.That(run.End, Is.EqualTo(t0.AddSeconds(1)));
    }
}
