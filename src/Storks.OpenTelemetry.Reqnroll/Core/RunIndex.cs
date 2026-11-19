using System.Globalization;
using System.Text.Json;

namespace Storks.OpenTelemetry.Reqnroll.Core;

/// <summary>
/// An Observation Window in the OTLP/JSON time format (<see cref="StartUnixNano"/>, <see cref="EndUnixNano"/>) and the
/// telemetry attributed to it: <see cref="SpanIds"/> are the spans (OTLP span id, hex) that started in it, <see cref="LogIds"/>
/// the logs (<see cref="TelemetryLog.Id"/>) written in it, both chronological.
/// </summary>
internal abstract record AttributedWindow(string StartUnixNano, string EndUnixNano, IReadOnlyList<string> SpanIds, IReadOnlyList<string> LogIds);

/// <summary>Ids of spans (OTLP span id, hex) and logs (<see cref="TelemetryLog.Id"/>), chronological.</summary>
internal sealed record TelemetryIds(IReadOnlyList<string> SpanIds, IReadOnlyList<string> LogIds);

/// <summary>A step of a scenario run; its window does not include its BeforeStep and AfterStep hooks.</summary>
internal sealed record StepIndexEntry(
    int Number, string Keyword, string Text, string Status, long OffsetMs, long DurationMs, string? Error,
    string StartUnixNano, string EndUnixNano, IReadOnlyList<string> SpanIds, IReadOnlyList<string> LogIds)
    : AttributedWindow(StartUnixNano, EndUnixNano, SpanIds, LogIds);

/// <summary>
/// A hook call of the test project (the plugin's own hooks are not recorded): a scenario hook, or a run or feature hook of
/// Unattributed Telemetry. <see cref="Name"/> is <c>Class.Method</c>; <see cref="OffsetMs"/> is from the start of the
/// scenario or of the run.
/// </summary>
internal sealed record HookIndexEntry(
    string HookType, string Name, string Status, long OffsetMs, long DurationMs,
    string StartUnixNano, string EndUnixNano, IReadOnlyList<string> SpanIds, IReadOnlyList<string> LogIds)
    : AttributedWindow(StartUnixNano, EndUnixNano, SpanIds, LogIds);

/// <summary>
/// A scenario run; <see cref="DurationMs"/> is the time its steps took, from the first step start to the last step end
/// (the whole window when it has no steps); <see cref="AttributedWindow.SpanIds"/> and <see cref="AttributedWindow.LogIds"/>
/// are all spans and logs of its window (steps and hooks); <see cref="Hooks"/> are its scenario hook calls in order of call.
/// <see cref="HookTelemetry"/> is what the viewer hides while "Show hooks" is off: the spans that started in hook windows
/// with their subtree, the logs of those spans, and the logs written in hook windows that have no span of their own among
/// the other spans of the scenario (a log of a step's span stays with its span even when written in a hook window).
/// </summary>
internal sealed record ScenarioIndexEntry(
    string Id, string Name, string Status, IReadOnlyList<string> Tags, long DurationMs, string? Error, IReadOnlyList<StepIndexEntry> Steps,
    IReadOnlyList<HookIndexEntry> Hooks, TelemetryIds HookTelemetry,
    string StartUnixNano, string EndUnixNano, IReadOnlyList<string> SpanIds, IReadOnlyList<string> LogIds)
    : AttributedWindow(StartUnixNano, EndUnixNano, SpanIds, LogIds);

/// <summary>
/// Unattributed Telemetry: the spans and logs that started outside every scenario window, on the run's scale (the window
/// runs from the run start to the end of its last window). <see cref="Hooks"/> are the run and feature hook calls of the
/// test project by start time with the part of that telemetry that started in their windows; the rest lies outside any window.
/// </summary>
internal sealed record UnattributedIndexEntry(
    IReadOnlyList<HookIndexEntry> Hooks, string StartUnixNano, string EndUnixNano, IReadOnlyList<string> SpanIds, IReadOnlyList<string> LogIds)
    : AttributedWindow(StartUnixNano, EndUnixNano, SpanIds, LogIds);

/// <summary>A feature and its scenario runs.</summary>
internal sealed record FeatureIndexEntry(string Name, IReadOnlyList<ScenarioIndexEntry> Scenarios);

/// <summary>
/// The attribution index embedded into the Telemetry Viewer page: features, scenario runs, steps, and which spans are
/// attributed to each run and step, and the Unattributed Telemetry. Attribution rule: a span belongs
/// to the window it started in, a log to the window it was written in (by its own time, regardless of its span).
/// </summary>
internal sealed record RunIndex(IReadOnlyList<FeatureIndexEntry> Features, UnattributedIndexEntry Unattributed)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Index of the whole run: scenario runs, and the run and feature hooks on the scale of Unattributed Telemetry.</summary>
    /// <param name="record">The run's scenarios and hooks.</param>
    /// <param name="spans">Received spans.</param>
    /// <param name="logs">Received logs; those without an <see cref="TelemetryLog.Id"/> cannot be referenced and are left out.</param>
    public static RunIndex Build(RunRecord record, IReadOnlyList<TelemetrySpan>? spans = null, IReadOnlyList<TelemetryLog>? logs = null) =>
        Build(record.Snapshot(), record.RunHooks(), record.Start, spans, logs);

    /// <summary>Index of scenario runs alone (no run hooks); the run's scale starts with its first scenario.</summary>
    public static RunIndex Build(IReadOnlyList<ScenarioRun> runs, IReadOnlyList<TelemetrySpan>? spans = null, IReadOnlyList<TelemetryLog>? logs = null) =>
        Build(runs, [], runs.Count == 0 ? DateTimeOffset.UnixEpoch : runs.Min(r => r.Start), spans, logs);

    private static RunIndex Build(IReadOnlyList<ScenarioRun> runs, IReadOnlyList<HookRun> runHooks, DateTimeOffset start,
        IReadOnlyList<TelemetrySpan>? spans, IReadOnlyList<TelemetryLog>? logs)
    {
        var spanItems = (spans ?? []).Select(s => new Timed(s.SpanId, s.Start, s.ParentSpanId)).OrderBy(t => t.Time).ToList();
        var logItems = (logs ?? []).Where(l => l.Id is not null).Select(l => new Timed(l.Id!, l.Timestamp, l.SpanId)).OrderBy(t => t.Time).ToList();
        var features = runs.GroupBy(r => r.Feature) // GroupBy keeps first-appearance order
            .Select(g => new FeatureIndexEntry(g.Key, g.Select(r => ScenarioEntry(r, spanItems, logItems)).ToList()))
            .ToList();
        return new(features, UnattributedEntry(features, runs, runHooks, start, spanItems, logItems));
    }

    /// <summary>A span (by start time, <see cref="Span"/> is its parent) or a log (by its time, <see cref="Span"/> is its span), reduced to what attribution needs.</summary>
    private readonly record struct Timed(string Id, DateTimeOffset Time, string? Span);

    /// <summary>
    /// Hands the spans and logs of an enclosing window out to the windows inside it, in order of the calls: windows inside
    /// one window do not overlap, and should two share an instant, the earlier call keeps the item.
    /// </summary>
    private sealed class Attributor(IReadOnlyList<Timed> spans, IReadOnlyList<Timed> logs)
    {
        private readonly HashSet<string> _takenSpans = [];
        private readonly HashSet<string> _takenLogs = [];

        public (List<string> Spans, List<string> Logs) Take(DateTimeOffset start, DateTimeOffset end) =>
            (Ids(In(spans, start, end)).Where(_takenSpans.Add).ToList(), Ids(In(logs, start, end)).Where(_takenLogs.Add).ToList());
    }

    private static ScenarioIndexEntry ScenarioEntry(ScenarioRun run, IReadOnlyList<Timed> spans, IReadOnlyList<Timed> logs)
    {
        var spansIn = In(spans, run.Start, run.End);
        var logsIn = In(logs, run.Start, run.End);
        var attributor = new Attributor(spansIn, logsIn);
        var steps = run.Steps.Select(s =>
        {
            var (stepSpans, stepLogs) = attributor.Take(s.Start, s.End);
            return new StepIndexEntry(s.Number, s.Keyword, s.Text, s.Status, Ms(s.Start - run.Start), Ms(s.End - s.Start), s.Error,
                UnixNano(s.Start), UnixNano(s.End), stepSpans, stepLogs);
        }).ToList();
        // Hook windows do not overlap step windows either; should they share an instant, the step keeps the item.
        var hooks = run.Hooks.Select(h => HookEntry(h, run.Start, attributor)).ToList();
        // The duration in the viewer's scenario header: from the first step start to the last step end, without the hooks around them.
        var duration = run.Steps.Count == 0 ? run.End - run.Start : run.Steps.Max(st => st.End) - run.Steps.Min(st => st.Start);
        return new(run.Id, run.Name, run.Status, run.Tags, Ms(duration),
            run.Error ?? run.Steps.FirstOrDefault(s => s.Error is not null)?.Error, steps, hooks, HookTelemetry(hooks, spansIn, logsIn),
            UnixNano(run.Start), UnixNano(run.End), Ids(spansIn), Ids(logsIn));
    }

    private static UnattributedIndexEntry UnattributedEntry(IReadOnlyList<FeatureIndexEntry> features, IReadOnlyList<ScenarioRun> runs,
        IReadOnlyList<HookRun> runHooks, DateTimeOffset start, IReadOnlyList<Timed> spans, IReadOnlyList<Timed> logs)
    {
        var scenarios = features.SelectMany(f => f.Scenarios).ToList();
        var inScenarioSpans = scenarios.SelectMany(s => s.SpanIds).ToHashSet();
        var inScenarioLogs = scenarios.SelectMany(s => s.LogIds).ToHashSet();
        var restSpans = spans.Where(t => !inScenarioSpans.Contains(t.Id)).ToList();
        var restLogs = logs.Where(t => !inScenarioLogs.Contains(t.Id)).ToList();
        var attributor = new Attributor(restSpans, restLogs);
        var hooks = runHooks.OrderBy(h => h.Start).Select(h => HookEntry(h, start, attributor)).ToList();
        var end = runHooks.Select(h => h.End).Concat(runs.Select(r => r.End)).Append(start).Max();
        return new(hooks, UnixNano(start), UnixNano(end), Ids(restSpans), Ids(restLogs));
    }

    /// <param name="h">The hook run to index.</param>
    /// <param name="origin">Start of the scenario or of the run, for <see cref="HookIndexEntry.OffsetMs"/>.</param>
    /// <param name="attributor">Assigns the hook's window its telemetry.</param>
    private static HookIndexEntry HookEntry(HookRun h, DateTimeOffset origin, Attributor attributor)
    {
        var (hookSpans, hookLogs) = attributor.Take(h.Start, h.End);
        return new(h.HookType, h.Name, h.Status, Ms(h.Start - origin), Ms(h.End - h.Start),
            UnixNano(h.Start), UnixNano(h.End), hookSpans, hookLogs);
    }

    private static TelemetryIds HookTelemetry(IReadOnlyList<HookIndexEntry> hooks, IReadOnlyList<Timed> spans, IReadOnlyList<Timed> logs)
    {
        var hookSpans = hooks.SelectMany(h => h.SpanIds).ToHashSet();
        var hookLogs = hooks.SelectMany(h => h.LogIds).ToHashSet();
        var parentOf = new Dictionary<string, string?>();
        foreach (var t in spans) parentOf.TryAdd(t.Id, t.Span);
        bool UnderHook(string id, HashSet<string> seen) =>
            hookSpans.Contains(id) || (seen.Add(id) && parentOf.TryGetValue(id, out var parent) && parent is not null && UnderHook(parent, seen));
        var hidden = spans.Where(t => UnderHook(t.Id, [])).ToList();
        var hiddenSet = Ids(hidden).ToHashSet();
        bool OfShownSpan(Timed log) => log.Span is not null && parentOf.ContainsKey(log.Span) && !hiddenSet.Contains(log.Span);
        var hiddenLogs = logs.Where(l => (l.Span is not null && hiddenSet.Contains(l.Span)) || (hookLogs.Contains(l.Id) && !OfShownSpan(l)));
        return new(Ids(hidden), Ids(hiddenLogs));
    }

    private static List<Timed> In(IEnumerable<Timed> items, DateTimeOffset start, DateTimeOffset end)
    {
        var window = new ObservationWindow(start, end);
        return items.Where(t => window.Contains(t.Time)).ToList();
    }

    private static List<string> Ids(IEnumerable<Timed> items) => items.Select(t => t.Id).ToList();

    /// <summary>Whole milliseconds, never negative (a window is never before its origin, nor ends before it starts).</summary>
    private static long Ms(TimeSpan t) => Math.Max(0, (long)Math.Round(t.TotalMilliseconds));

    private static string UnixNano(DateTimeOffset t) =>
        ((t - DateTimeOffset.UnixEpoch).Ticks * 100).ToString(CultureInfo.InvariantCulture);

    /// <summary>JSON with ASCII-only output ('&lt;', '&amp;' and non-ASCII are \u-escaped), safe to inline into a script element.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Json);
}
