using NUnit.Framework;

namespace App.Specs.ReportChecks;

public class ScenarioTelemetryReportTests
{
    private const string Red = "Customer places an order for a SKU whose price cannot be fetched";
    private const string Green = "Customer places an order for a regular SKU";

    [Test]
    public void Red_scenario_has_attachment_with_orders_warning_about_failed_pricing_call()
    {
        var attachments = CucumberMessages.Load().AttachmentsOfScenario(Red);

        Assert.That(attachments, Has.Some.Matches<string>(a =>
            a.Split('\n').Any(l => l.Contains("orders-api") && l.Contains("Warning") && l.Contains("Pricing request failed"))),
            "no attachment with an Orders warning about the failed Pricing call");
    }

    [Test]
    public void Step_of_red_scenario_that_called_Pricing_has_slice_with_orders_warning_and_failed_step_has_a_slice()
    {
        var steps = CucumberMessages.Load().AttachmentsByStep(Red);

        // NB: the red scenario fails in the "Then" assertion; the Orders warning happens in the "When" call (see ticket report).
        Assert.That(steps.Single(s => s.Key.StartsWith("the order total")).Value, Is.Not.Empty, "failed step has no slice");
        var calling = steps.Single(s => s.Key.StartsWith("the customer orders")).Value;
        Assert.That(calling, Has.Some.Matches<string>(a =>
            a.Split('\n').Any(l => l.Contains("orders-api") && l.Contains("Warning") && l.Contains("Pricing request failed"))));
    }

    [Test]
    public void Every_step_has_a_slice_attachment_and_scenario_attachment_contains_all_slice_lines()
    {
        var messages = CucumberMessages.Load();
        foreach (var scenario in new[] { Red, Green })
        {
            var byStep = messages.AttachmentsByStep(scenario);
            var sliceBodies = byStep.Values.SelectMany(v => v).ToList();
            var scenarioLevel = messages.AttachmentsOfScenario(scenario).Where(a => !sliceBodies.Contains(a)).ToList();
            Assert.That(scenarioLevel, Is.Not.Empty, $"{scenario}: scenario-level attachment missing");
            var scenarioText = string.Join('\n', scenarioLevel);
            foreach (var (step, slices) in byStep)
            {
                Assert.That(slices, Is.Not.Empty, $"{scenario} / {step}: no slice attachment");
                foreach (var line in slices.SelectMany(s => s.Split('\n', StringSplitOptions.RemoveEmptyEntries)))
                    if (!line.Contains("нет телеметрии"))
                        Assert.That(scenarioText, Does.Contain(line.Substring(line.IndexOf("s  ", StringComparison.Ordinal) + 3)));
            }
        }
    }

    [Test]
    public void Green_scenario_has_attachment_with_logs_from_both_services()
    {
        var attachments = CucumberMessages.Load().AttachmentsOfScenario(Green);

        Assert.That(attachments, Has.Some.Matches<string>(a => a.Contains("orders-api") && a.Contains("pricing-api")));
    }

    /// <summary>Lines of the subtree of the first top-level line matching <paramref name="rootMarker"/> (root included).</summary>
    private static string[] Subtree(string attachment, string rootMarker)
    {
        var lines = attachment.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var start = Array.FindIndex(lines, l => !char.IsWhiteSpace(l[0]) && l.Contains(rootMarker));
        if (start < 0) return [];
        return lines.Skip(start).Take(1)
            .Concat(lines.Skip(start + 1).TakeWhile(l => char.IsWhiteSpace(l[0]))).ToArray();
    }

    [Test]
    public void Red_scenario_tree_has_pricing_500_inside_orders_trace_and_orders_warning_under_orders_span()
    {
        var attachments = CucumberMessages.Load().AttachmentsOfScenario(Red);

        Assert.That(attachments, Has.Some.Matches<string>(a =>
        {
            var tree = Subtree(a, "orders-api  POST");
            return tree.Any(l => l.StartsWith(' ') && l.Contains("pricing-api") && l.Contains("http.response.status_code=500"))
                && tree.Any(l => l.StartsWith(' ') && l.Contains("orders-api") && l.Contains("Warning") && l.Contains("Pricing request failed"));
        }), "no tree with a Pricing 500 span and an Orders warning nested under the Orders POST span");
    }

    [Test]
    public void Green_scenario_tree_shows_orders_to_pricing_and_postgres_queries()
    {
        var attachments = CucumberMessages.Load().AttachmentsOfScenario(Green);

        Assert.That(attachments, Has.Some.Matches<string>(a =>
        {
            var tree = Subtree(a, "orders-api  POST");
            return tree.Any(l => l.StartsWith(' ') && l.Contains("orders-api") && l.Contains("[Client]"))
                && tree.Any(l => l.StartsWith(' ') && l.Contains("pricing-api") && l.Contains("[Server]") && l.Contains("http.response.status_code=200"))
                && tree.Any(l => l.StartsWith(' ') && l.Contains("db.query.text="));
        }), "no tree with Orders -> Pricing and Postgres queries under the Orders POST span");
    }
}
