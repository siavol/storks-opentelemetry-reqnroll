namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>
/// The plugin's end-of-run lines (where the files are, its own errors, the Window Overlap summary), each with
/// <see cref="PluginDiagnostics.Prefix"/>. Never throws.
/// </summary>
internal sealed class RunOutput(TextWriter info, TextWriter error)
{
    /// <summary>
    /// The test process's own standard output and error streams. Not <see cref="Console.Out"/>: test frameworks redirect it
    /// per test, and what an AfterTestRun hook writes there belongs to no test, so NUnit drops it. The process streams
    /// reach the console of <c>dotnet test</c> at detailed verbosity (<c>--logger "console;verbosity=detailed"</c>).
    /// </summary>
    public static RunOutput ProcessStreams() => new(Open(Console.OpenStandardOutput()), Open(Console.OpenStandardError()));

    public void Info(string message) => Write(info, message);

    public void Error(string message) => Write(error, message);

    private static void Write(TextWriter writer, string message)
    {
        try { writer.WriteLine(PluginDiagnostics.Prefix + message); }
        catch { /* the output is only a report: it must never fail the run */ }
    }

    private static TextWriter Open(Stream stream) => TextWriter.Synchronized(new StreamWriter(stream) { AutoFlush = true });
}
