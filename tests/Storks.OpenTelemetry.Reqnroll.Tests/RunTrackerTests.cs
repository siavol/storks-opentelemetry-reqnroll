using NUnit.Framework;
using Storks.OpenTelemetry.Reqnroll.Core;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

public class RunTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    /// <summary>The README section on the repository, reachable from a package user's test output.</summary>
    private const string WindowOverlapSection = "https://github.com/siavol/storks-opentelemetry-reqnroll/blob/main/README.md#window-overlap";

    [Test]
    public void Window_opened_while_another_is_open_is_only_counted_in_the_summary_with_the_readme_section()
    {
        var diagnostics = new PluginDiagnostics();
        var record = new RunRecord();
        var tracker = new RunTracker(diagnostics);

        tracker.WindowOpened(record.Begin("Checkout", "Pay by card", [], T0));
        tracker.WindowOpened(record.Begin("Pricing", "Apply discount", [], T0.AddMilliseconds(10)));

        Assert.That(diagnostics.Drain(), Is.Empty, "no warning per scenario");
        Assert.That(tracker.WindowOverlapSummary(), Does.Contain("Window Overlap: 1 scenario window").And.Contain(WindowOverlapSection));
    }

    [Test]
    public void Failing_overlap_check_is_reported_as_a_diagnostic_and_gives_no_summary()
    {
        var diagnostics = new PluginDiagnostics();
        var record = new RunRecord();
        var tracker = new RunTracker(diagnostics, new FailingWindowOverlap());
        var run = record.Begin("F", "S", [], T0);

        Assert.DoesNotThrow(() => tracker.WindowOpened(run));
        Assert.DoesNotThrow(() => tracker.WindowClosed(run));
        Assert.That(tracker.WindowOverlapSummary(), Is.Null);
        Assert.That(diagnostics.Drain(), Has.Count.EqualTo(2).And.All.Contain("overlap check is broken"), "opening and closing are one kind");
    }
}
