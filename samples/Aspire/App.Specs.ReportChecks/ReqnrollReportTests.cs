using NUnit.Framework;

namespace App.Specs.ReportChecks;

/// <summary>The Reqnroll report itself: only Telemetry Viewer links, no telemetry text.</summary>
public class ReqnrollReportTests
{
    private const string Red = "Customer places an order for a SKU whose price cannot be fetched";
    private const string Green = "Customer places an order for a regular SKU";

    [Test]
    public void Report_has_no_telemetry_text_attachments_and_no_unattributed_text_file_next_to_it()
    {
        var messages = CucumberMessages.Load();

        Assert.That(messages.Attachments().Where(a => a.MediaType != "text/x.cucumber.log+plain" || a.FileName is not null)
                .Select(a => $"{a.FileName} ({a.MediaType})"),
            Is.Empty, "the report carries only log messages (telemetry links), no attached files");
        var unattributed = Path.Combine(CucumberMessages.ReportDirectory, "unattributed-telemetry.txt");
        Assert.That(File.Exists(unattributed), Is.False, $"Unattributed Telemetry is in the viewer only: {unattributed}");
    }

    [Test]
    public void Every_gherkin_step_has_exactly_one_telemetry_link_and_no_hook_has_a_step_link()
    {
        var messages = CucumberMessages.Load();
        foreach (var scenario in new[] { Red, Green })
        {
            // The link is a log message (WriteLine, text/x.cucumber.log+plain) with a clickable <a href> (ticket 03).
            var stepLinks = messages.TelemetryLinks(scenario).Where(l => l.Link.Contains("&step=")).ToList();

            // Guards the listener order: a step link must land on the Gherkin step, never on a (shared) hook step.
            Assert.That(stepLinks.Where(l => l.HookType is not null || l.StepText is null), Is.Empty, $"{scenario}: step link written to a hook");
            Assert.That(stepLinks.Select(l => l.StepText), Is.EquivalentTo(messages.StepTexts(scenario)), $"{scenario}: every step needs exactly one link");
            Assert.That(stepLinks.Select(l => l.Link).All(l => l.All(ch => ch < 128)), Is.True, "links are ASCII");
            Assert.That(stepLinks.Select(l => l.Link), Has.All.Match(@"^telemetry\.html#s=[0-9a-f]{32}&step=\d+$"));
        }
    }

    [Test]
    public void Scenario_has_a_link_without_step_in_the_AfterScenario_hook_and_all_links_of_a_run_share_the_scenario_id()
    {
        var messages = CucumberMessages.Load();
        foreach (var scenario in new[] { Red, Green })
        {
            var all = messages.TelemetryLinks(scenario);
            var links = all.Select(l => l.Link).ToList();
            var scenarioLinks = all.Where(l => !l.Link.Contains("&step=")).ToList();
            Assert.That(scenarioLinks, Has.Count.EqualTo(1), scenario);
            Assert.That(scenarioLinks.Single().HookType, Is.EqualTo("AFTER_TEST_CASE"), $"{scenario}: scenario link must be written in an AfterScenario hook");
            Assert.That(links.Select(l => l.Split('#')[1].Split('&')[0]).Distinct().Count(), Is.EqualTo(1), scenario);
        }
    }

    [Test]
    public void Viewer_page_is_written_next_to_the_report_with_the_index_of_all_linked_scenarios()
    {
        var path = Path.Combine(CucumberMessages.ReportDirectory, "telemetry.html");
        Assert.That(File.Exists(path), $"Run samples/Aspire/App.Specs first. Not found: {path}");
        var html = File.ReadAllText(path);
        var messages = CucumberMessages.Load();

        foreach (var scenario in new[] { Red, Green })
        {
            var id = messages.TelemetryLinks(scenario)[0].Link.Split('#')[1].Split('&')[0].Substring(2);
            Assert.That(html, Does.Contain(id), scenario);
        }
        Assert.That(html, Does.Contain("\"status\":\"failed\""));
    }
}
