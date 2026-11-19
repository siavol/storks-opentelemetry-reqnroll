namespace Storks.OpenTelemetry.Reqnroll.Core;

/// <summary>
/// Notices Window Overlap: the Observation Window of a scenario opening while the window of another scenario is still
/// open (scenarios run in parallel). Attribution goes by time only and checks each window on its own, so telemetry in the
/// overlap shows up in each of the overlapping scenarios; this only warns and never changes Attribution. Thread-safe: parallel scenarios open and close their
/// windows on their own threads.
/// </summary>
internal interface IWindowOverlap
{
    /// <summary>Marks the window of <paramref name="run"/> open; returns the warning if other windows are open, else null.</summary>
    string? Opened(ScenarioRun run);

    /// <summary>Marks the window of <paramref name="run"/> closed.</summary>
    void Closed(ScenarioRun run);

    /// <summary>The one end-of-run line about the overlaps noticed, or null when there were none.</summary>
    string? Summary();
}

/// <inheritdoc cref="IWindowOverlap"/>
internal sealed class WindowOverlap : IWindowOverlap
{
    /// <summary>
    /// README section that explains the limitation. An absolute URL, because the warning is read in the test output of a
    /// package user, where a relative README path leads nowhere.
    /// </summary>
    internal const string ReadmeSection = "https://github.com/siavol/storks-opentelemetry-reqnroll/blob/main/README.md#window-overlap";

    private readonly Lock _gate = new();
    private readonly List<ScenarioRun> _open = [];
    private int _count;

    public string? Opened(ScenarioRun run)
    {
        ScenarioRun[] others;
        lock (_gate)
        {
            others = _open.Where(r => r != run).ToArray();
            if (!_open.Contains(run)) _open.Add(run);
            if (others.Length > 0) _count++;
        }
        if (others.Length == 0) return null;
        return $"Window Overlap: scenario {Title(run)} started while {string.Join(", ", others.Select(Title))} "
            + $"{(others.Length == 1 ? "was" : "were")} still running, so telemetry in the overlap shows up in each of the overlapping scenarios. "
            + $"See {ReadmeSection}";
    }

    public void Closed(ScenarioRun run)
    {
        lock (_gate) _open.Remove(run);
    }

    public string? Summary()
    {
        int count;
        lock (_gate) count = _count;
        if (count == 0) return null;
        return $"Window Overlap: {count} scenario {(count == 1 ? "window" : "windows")} opened while another was open; "
            + $"telemetry in an overlap shows up in each of the overlapping scenarios. See {ReadmeSection}";
    }

    private static string Title(ScenarioRun run) => $"'{run.Feature}: {run.Name}'";
}
