namespace Storks.OpenTelemetry.Reqnroll.Core;

/// <summary>
/// Counts Window Overlap for the end-of-run summary, since telemetry in the overlap shows up in each overlapping scenario. Thread-safe:
/// parallel scenarios open and close their windows on their own threads.
/// </summary>
internal interface IWindowOverlap
{
    void Opened(ScenarioRun run);

    void Closed(ScenarioRun run);

    /// <summary>The one end-of-run line with the number of overlaps, or null.</summary>
    string? Summary();
}

/// <inheritdoc cref="IWindowOverlap"/>
internal sealed class WindowOverlap : IWindowOverlap
{
    /// <summary>
    /// README section that explains the limitation. An absolute URL, because the summary is read in the test output of a
    /// package user, where a relative README path leads nowhere.
    /// </summary>
    internal const string ReadmeSection = "https://github.com/siavol/storks-opentelemetry-reqnroll/blob/main/README.md#window-overlap";

    private readonly Lock _gate = new();
    private readonly List<ScenarioRun> _open = [];
    private int _count;

    public void Opened(ScenarioRun run)
    {
        lock (_gate)
        {
            if (_open.Any(r => r != run)) _count++;
            if (!_open.Contains(run)) _open.Add(run);
        }
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
}
