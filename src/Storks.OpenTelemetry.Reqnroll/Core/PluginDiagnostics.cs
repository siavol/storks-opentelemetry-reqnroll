using System.Collections.Concurrent;

namespace Storks.OpenTelemetry.Reqnroll.Core;

/// <summary>
/// Plugin-internal problems raised during the run (e.g. by the receiver thread). They are queued here and written once
/// at the end of the run, one line per kind; they never fail a scenario.
/// </summary>
internal sealed class PluginDiagnostics
{
    /// <summary>Starts every line the plugin writes to the test output.</summary>
    public const string Prefix = "[Storks.OpenTelemetry] ";

    private readonly ConcurrentQueue<(string Kind, string? Detail)> _problems = new();

    /// <summary>Problems of the same <paramref name="kind"/> collapse into one line with a count and the first detail.</summary>
    public void Report(string kind, string? detail = null) => _problems.Enqueue((kind, detail));

    /// <summary>One line per kind, in the order the kinds first occurred.</summary>
    public IReadOnlyList<string> Drain()
    {
        var drained = new List<(string Kind, string? Detail)>();
        while (_problems.TryDequeue(out var p)) drained.Add(p);
        return drained.GroupBy(p => p.Kind).Select(Line).ToList();
    }

    private static string Line(IGrouping<string, (string Kind, string? Detail)> kind)
    {
        var count = kind.Count();
        var detail = kind.First().Detail;
        if (count == 1) return detail is null ? kind.Key : $"{kind.Key}: {detail}";
        return detail is null ? $"{kind.Key} ({count} times)" : $"{kind.Key} ({count} times, first: {detail})";
    }
}
