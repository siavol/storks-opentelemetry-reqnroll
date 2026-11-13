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
    public void Green_scenario_has_attachment_with_logs_from_both_services()
    {
        var attachments = CucumberMessages.Load().AttachmentsOfScenario(Green);

        Assert.That(attachments, Has.Some.Matches<string>(a => a.Contains("orders-api") && a.Contains("pricing-api")));
    }
}
