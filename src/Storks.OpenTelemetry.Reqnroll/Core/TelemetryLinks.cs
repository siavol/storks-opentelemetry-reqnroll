namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>Links to the Telemetry Viewer page; relative, since the page lies next to the report.</summary>
internal static class TelemetryLinks
{
    public const string PageName = "telemetry.html";

    public static string Scenario(string runId) => $"{PageName}#s={runId}";

    public static string Step(string runId, int stepNumber) => $"{PageName}#s={runId}&step={stepNumber}";
}
