namespace GherkinOtel.ReqnrollPlugin;

/// <summary>Thread-safe accumulator of everything received during the run.</summary>
public sealed class TelemetryStore
{
    private readonly object _gate = new();
    private readonly List<TelemetryLog> _logs = [];

    public void AddLogs(IEnumerable<TelemetryLog> logs)
    {
        lock (_gate) _logs.AddRange(logs);
    }

    /// <summary>Copy of everything received so far.</summary>
    public IReadOnlyList<TelemetryLog> Snapshot()
    {
        lock (_gate) return _logs.ToArray();
    }
}
