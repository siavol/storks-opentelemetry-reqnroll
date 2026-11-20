using Storks.OpenTelemetry.Reqnroll.Core;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

/// <summary>A Window Overlap check that fails on every call, to show that its failures never reach a scenario.</summary>
internal sealed class FailingWindowOverlap : IWindowOverlap
{
    public void Opened(ScenarioRun run) => throw new InvalidOperationException("overlap check is broken");
    public void Closed(ScenarioRun run) => throw new InvalidOperationException("overlap check is broken");
    public string? Summary() => throw new InvalidOperationException("overlap check is broken");
}
