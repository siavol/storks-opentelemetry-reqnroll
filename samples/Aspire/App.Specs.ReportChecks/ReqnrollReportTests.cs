using NUnit.Framework;

namespace App.Specs.ReportChecks;

/// <summary>The Reqnroll report is left as its formatters make it: the plugin writes nothing into it.</summary>
public class ReqnrollReportTests
{
    [Test]
    public void Report_has_no_attachments_and_no_log_messages_of_the_plugin()
    {
        var messages = CucumberMessages.Load();

        Assert.That(messages.Attachments().Select(a => $"{a.FileName} ({a.MediaType}): {a.Body}"),
            Has.None.Contain("telemetry.html").And.None.Contain("[Storks.OpenTelemetry]"), "the plugin writes no links or lines into the report");
        Assert.That(messages.Attachments().Where(a => a.FileName is not null), Is.Empty, "no attached files");
        var unattributed = Path.Combine(CucumberMessages.ReportDirectory, "unattributed-telemetry.txt");
        Assert.That(File.Exists(unattributed), Is.False, $"Unattributed Telemetry is in the viewer only: {unattributed}");
    }

    [Test]
    public void Viewer_has_every_scenario_of_the_report_with_its_own_id_and_the_failed_one()
    {
        var scenarios = ViewerIndexReportTests.DataBlock(ViewerIndexReportTests.Page(), "run-index")["features"]!.AsArray().SelectMany(f => f!["scenarios"]!.AsArray()).ToList();

        Assert.That(scenarios.Select(s => (string?)s!["name"]), Is.EquivalentTo(CucumberMessages.Load().ScenarioNames()));
        Assert.That(scenarios.Select(s => (string?)s!["id"]), Is.Unique.And.All.Match("^[0-9a-f]{32}$"));
        Assert.That(scenarios.Select(s => (string?)s!["status"]), Is.EquivalentTo(new[] { "passed", "failed" }));
    }
}
