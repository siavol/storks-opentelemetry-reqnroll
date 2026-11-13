namespace GherkinOtel.ReqnrollPlugin;

/// <summary>Thread-safe accumulator of everything received during the run.</summary>
public sealed class TelemetryStore
{
    private readonly object _gate = new();
    private readonly List<TelemetryLog> _logs = [];
    private readonly List<TelemetrySpan> _spans = [];

    public void AddLogs(IEnumerable<TelemetryLog> logs)
    {
        lock (_gate) _logs.AddRange(logs);
    }

    public void AddSpans(IEnumerable<TelemetrySpan> spans)
    {
        lock (_gate) _spans.AddRange(spans);
    }

    /// <summary>Copy of everything received so far.</summary>
    public IReadOnlyList<TelemetryLog> SnapshotLogs()
    {
        lock (_gate) return _logs.ToArray();
    }

    public IReadOnlyList<TelemetrySpan> SnapshotSpans()
    {
        lock (_gate) return _spans.ToArray();
    }
}
