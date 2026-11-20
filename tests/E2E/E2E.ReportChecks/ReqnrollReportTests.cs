using NUnit.Framework;

namespace E2E.ReportChecks;

/// <summary>The Reqnroll report is left as its formatters make it: the plugin writes nothing into it.</summary>
public class ReqnrollReportTests
{
    [Test]
    public void Report_has_no_attachments_and_no_lines_of_the_plugin()
    {
        var attachments = Reports.JsonLines("reqnroll_report.ndjson").Where(m => m.ContainsKey("attachment"))
            .Select(m => m["attachment"]!).ToList();

        Assert.That(attachments.Select(a => a.ToJsonString()),
            Has.None.Contain("telemetry.html").And.None.Contain("[Storks.OpenTelemetry]"), "the plugin writes no links or lines into the report");
        Assert.That(attachments.Where(a => a["fileName"] is not null), Is.Empty, "no attached files");
    }
}
