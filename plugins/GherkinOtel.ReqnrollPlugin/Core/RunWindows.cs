namespace GherkinOtel.ReqnrollPlugin;

/// <summary>Thread-safe record of the Observation Windows of all scenarios of the run.</summary>
public sealed class RunWindows
{
    private readonly object _gate = new();
    private readonly List<ObservationWindow> _windows = [];

    public void Add(ObservationWindow window)
    {
        lock (_gate) _windows.Add(window);
    }

    public IReadOnlyList<ObservationWindow> Snapshot()
    {
        lock (_gate) return _windows.ToArray();
    }
}
