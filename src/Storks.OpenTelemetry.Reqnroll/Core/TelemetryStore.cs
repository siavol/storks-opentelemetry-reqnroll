using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>Everything received up to one moment: the neutral model and the accepted OTLP requests, consistent with each other.</summary>
internal sealed record TelemetrySnapshot(
    IReadOnlyList<TelemetrySpan> Spans, IReadOnlyList<TelemetryLog> Logs,
    IReadOnlyList<ExportTraceServiceRequest> TraceRequests, IReadOnlyList<ExportLogsServiceRequest> LogRequests);

/// <summary>
/// Thread-safe accumulator of everything received during the run: the neutral model (used for Attribution, ADR 0001)
/// and the accepted OTLP requests as received (written to the OTLP/JSON files and the viewer without loss, ADR 0002).
/// </summary>
internal sealed class TelemetryStore
{
    private readonly Lock _gate = new();
    private readonly List<TelemetryLog> _logs = [];
    private readonly List<TelemetrySpan> _spans = [];
    private readonly List<ExportLogsServiceRequest> _logRequests = [];
    private readonly List<ExportTraceServiceRequest> _traceRequests = [];

    /// <summary>Keeps an accepted trace request and its spans in the neutral model.</summary>
    public void AddTraces(ExportTraceServiceRequest request, IEnumerable<TelemetrySpan> spans)
    {
        var list = spans.ToList();
        lock (_gate)
        {
            _traceRequests.Add(request);
            _spans.AddRange(list);
        }
    }

    /// <summary>
    /// Keeps an accepted log request and its logs in the neutral model. <paramref name="logs"/> gets the position of the
    /// request among the received log requests: <see cref="TelemetryLog.Id"/> may refer to it, and it stays valid in
    /// every later <see cref="Snapshot"/>.
    /// </summary>
    public void AddLogs(ExportLogsServiceRequest request, Func<int, IEnumerable<TelemetryLog>> logs)
    {
        lock (_gate)
        {
            _logs.AddRange(logs(_logRequests.Count).ToList());
            _logRequests.Add(request);
        }
    }

    public TelemetrySnapshot Snapshot()
    {
        lock (_gate) return new(_spans.ToArray(), _logs.ToArray(), _traceRequests.ToArray(), _logRequests.ToArray());
    }
}
