using System.Globalization;
using System.Text.Json;

namespace Storks.OpenTelemetry.Reqnroll.Core;

/// <summary>An Observation Window and its telemetry, in OTLP/JSON terms, so the viewer matches them to the embedded requests as they are.</summary>
internal abstract record AttributedWindow(string StartUnixNano, string EndUnixNano, IReadOnlyList<string> SpanIds, IReadOnlyList<string> LogIds);

internal sealed record TelemetryIds(IReadOnlyList<string> SpanIds, IReadOnlyList<string> LogIds);

/// <summary>A step of a scenario run; its window does not include its BeforeStep and AfterStep hooks.</summary>
internal sealed record StepIndexEntry(
    int Number, string Keyword, string Text, string Status, long OffsetMs, long DurationMs, string? Error,
    string StartUnixNano, string EndUnixNano, IReadOnlyList<string> SpanIds, IReadOnlyList<string> LogIds)
    : AttributedWindow(StartUnixNano, EndUnixNano, SpanIds, LogIds);

internal sealed record HookIndexEntry(
    string HookType, string Name, string Status, long OffsetMs, long DurationMs,
    string StartUnixNano, string EndUnixNano, IReadOnlyList<string> SpanIds, IReadOnlyList<string> LogIds)
    : AttributedWindow(StartUnixNano, EndUnixNano, SpanIds, LogIds);

internal sealed record ScenarioIndexEntry(
    string Id, string Name, string Status, IReadOnlyList<string> Tags, long DurationMs, string? Error, IReadOnlyList<StepIndexEntry> Steps,
    IReadOnlyList<HookIndexEntry> Hooks, TelemetryIds HookTelemetry,
    string StartUnixNano, string EndUnixNano, IReadOnlyList<string> SpanIds, IReadOnlyList<string> LogIds)
    : AttributedWindow(StartUnixNano, EndUnixNano, SpanIds, LogIds);

internal sealed record UnattributedIndexEntry(
    IReadOnlyList<HookIndexEntry> Hooks, string StartUnixNano, string EndUnixNano, IReadOnlyList<string> SpanIds, IReadOnlyList<string> LogIds)
    : AttributedWindow(StartUnixNano, EndUnixNano, SpanIds, LogIds);

internal sealed record FeatureIndexEntry(string Name, IReadOnlyList<ScenarioIndexEntry> Scenarios);

/// <summary>
/// The attribution index embedded into the Telemetry Viewer page. A span belongs to the window it started in, a log to
/// the window it was written in, by its own time and regardless of its span.
/// </summary>
internal sealed record RunIndex(IReadOnlyList<FeatureIndexEntry> Features, UnattributedIndexEntry Unattributed)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static RunIndex Build(RunRecord record, IReadOnlyList<TelemetrySpan>? spans = null, IReadOnlyList<TelemetryLog>? logs = null) =>
        Build(record.Snapshot(), record.RunHooks(), record.Start, spans, logs);

    /// <summary>Without a run record, for tests of scenario Attribution alone.</summary>
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

    /// <summary>A span or a log, reduced to what Attribution needs. <see cref="Span"/> is a span's parent, or a log's span.</summary>
    private readonly record struct Timed(string Id, DateTimeOffset Time, string? Span);

    /// <summary>Hands each item to one window only: windows that share an instant would otherwise both get it.</summary>
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
        var hooks = run.Hooks.Select(h => HookEntry(h, run.Start, attributor)).ToList();
        return new(run.Id, run.Name, run.Status, run.Tags, Ms(StepsDuration(run)),
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

    /// <summary>The scenario's own time, without the hooks around its steps, which "Show hooks" may hide.</summary>
    private static TimeSpan StepsDuration(ScenarioRun run) =>
        run.Steps.Count == 0 ? run.End - run.Start : run.Steps.Max(st => st.End) - run.Steps.Min(st => st.Start);

    private static HookIndexEntry HookEntry(HookRun h, DateTimeOffset origin, Attributor attributor)
    {
        var (hookSpans, hookLogs) = attributor.Take(h.Start, h.End);
        return new(h.HookType, h.Name, h.Status, Ms(h.Start - origin), Ms(h.End - h.Start),
            UnixNano(h.Start), UnixNano(h.End), hookSpans, hookLogs);
    }

    /// <summary>
    /// What the viewer hides while "Show hooks" is off: hook spans with their subtree and their logs, and hook-window logs,
    /// except a log of a shown span, which stays with that span.
    /// </summary>
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

    /// <summary>Never negative, should the test process's clock be adjusted during the run.</summary>
    private static long Ms(TimeSpan t) => Math.Max(0, (long)Math.Round(t.TotalMilliseconds));

    private static string UnixNano(DateTimeOffset t) =>
        ((t - DateTimeOffset.UnixEpoch).Ticks * 100).ToString(CultureInfo.InvariantCulture);

    /// <summary>ASCII-only (the Web defaults escape '&lt;' and '&amp;'), so it is safe to inline into a script element.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Json);
}
