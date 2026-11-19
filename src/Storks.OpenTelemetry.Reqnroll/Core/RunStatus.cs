namespace Storks.OpenTelemetry.Reqnroll.Core;

/// <summary>Statuses of scenarios, steps and hooks in the run record and the index, as Cucumber names them.</summary>
internal static class RunStatus
{
    public const string Passed = "passed";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
    public const string Undefined = "undefined";
    public const string Pending = "pending";
}
