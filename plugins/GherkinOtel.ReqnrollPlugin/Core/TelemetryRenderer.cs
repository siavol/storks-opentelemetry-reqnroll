using System.Globalization;
using System.Text;

namespace GherkinOtel.ReqnrollPlugin;

public static class TelemetryRenderer
{
    public const string NoTelemetry = "нет телеметрии";
    public const string Unfinished = "не завершён";

    // Shown on span lines, in this order; first match of the alternatives wins.
    private static readonly string[][] KeyAttributes =
    [
        ["http.request.method", "http.method"],
        ["http.route", "url.path", "http.target"],
        ["http.response.status_code", "http.status_code"],
        ["db.query.text", "db.statement"],
    ];

    /// <summary>Text tree: spans nested by parent, logs under their span; time is relative to the render origin (window start unless set).</summary>
    public static string Render(ScenarioTelemetry telemetry)
    {
        if (telemetry.Logs.Count == 0 && telemetry.Spans.Count == 0) return NoTelemetry;

        var start = telemetry.RenderOrigin ?? telemetry.Window.Start;
        var spanKeys = telemetry.Spans.Select(s => (s.TraceId, s.SpanId)).ToHashSet();
        var childrenOf = telemetry.Spans
            .Where(s => s.ParentSpanId is not null && spanKeys.Contains((s.TraceId, s.ParentSpanId)))
            .ToLookup(s => (s.TraceId, s.ParentSpanId!));
        var logsOf = telemetry.Logs
            .Where(l => l.SpanId is not null && l.TraceId is not null && spanKeys.Contains((l.TraceId, l.SpanId)))
            .ToLookup(l => (l.TraceId!, l.SpanId!));

        // Top level: roots (parent absent from the set) and logs not tied to a span of the set, by time.
        var top = new List<(DateTimeOffset Time, object Item)>();
        foreach (var s in telemetry.Spans)
            if (s.ParentSpanId is null || !spanKeys.Contains((s.TraceId, s.ParentSpanId))) top.Add((s.Start, s));
        foreach (var l in telemetry.Logs)
            if (l.SpanId is null || l.TraceId is null || !spanKeys.Contains((l.TraceId, l.SpanId))) top.Add((l.Timestamp, l));

        var sb = new StringBuilder();
        foreach (var (_, item) in top.OrderBy(t => t.Time)) Append(item, 0);
        return sb.ToString();

        void Append(object item, int depth)
        {
            sb.Append(' ', depth * 2);
            if (item is TelemetryLog log)
            {
                sb.Append(CultureInfo.InvariantCulture, $"+{Rel(log.Timestamp):0.000}s  {log.Service}  {log.Level}  {log.Category}  {Continued(log.Message, depth)}\n");
                return;
            }
            var span = (TelemetrySpan)item;
            sb.Append(CultureInfo.InvariantCulture, $"+{Rel(span.Start):0.000}s  {span.Service}  {span.Name}  [{span.Kind}]  ");
            sb.Append(span.End is { } end ? FormatDuration(end - span.Start) : Unfinished);
            sb.Append(CultureInfo.InvariantCulture, $"  {span.Status}");
            foreach (var alternatives in KeyAttributes)
                foreach (var key in alternatives)
                    if (span.Attributes.TryGetValue(key, out var value))
                    {
                        sb.Append(CultureInfo.InvariantCulture, $"  {key}={value.ReplaceLineEndings(" ")}");
                        break;
                    }
            sb.Append('\n');

            var inner = childrenOf[(span.TraceId, span.SpanId)].Select(c => (c.Start, (object)c))
                .Concat(logsOf[(span.TraceId, span.SpanId)].Select(l => (l.Timestamp, (object)l)))
                .OrderBy(t => t.Item1);
            foreach (var (_, child) in inner) Append(child, depth + 1);
        }

        double Rel(DateTimeOffset t) => (t - start).TotalSeconds;
    }

    /// <summary>Continuation lines of a multi-line message stay indented, so they never look like a top-level item.</summary>
    private static string Continued(string message, int depth) =>
        message.ReplaceLineEndings("\n" + new string(' ', depth * 2 + 2));

    private static string FormatDuration(TimeSpan d) =>
        d.TotalMilliseconds >= 1000
            ? string.Create(CultureInfo.InvariantCulture, $"{d.TotalSeconds:0.###}s")
            : string.Create(CultureInfo.InvariantCulture, $"{Math.Round(d.TotalMilliseconds):0}ms");
}
