using System.Collections.Concurrent;

namespace GherkinOtel.ReqnrollPlugin;

/// <summary>
/// Plugin-internal errors raised outside a test context (e.g. by the receiver thread). They are
/// queued here and written to the test output by the next hook; they never fail a scenario.
/// </summary>
public sealed class PluginDiagnostics
{
    private readonly ConcurrentQueue<string> _messages = new();

    public void Report(string message) => _messages.Enqueue(message);

    public IReadOnlyList<string> Drain()
    {
        var drained = new List<string>();
        while (_messages.TryDequeue(out var m)) drained.Add(m);
        return drained;
    }
}
