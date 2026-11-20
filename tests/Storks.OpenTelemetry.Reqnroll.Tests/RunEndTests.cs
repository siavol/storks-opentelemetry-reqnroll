using Io.Cucumber.Messages.Types;
using NUnit.Framework;
using Storks.OpenTelemetry.Reqnroll.Core;
using Storks.OpenTelemetry.Reqnroll.Receiver;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

/// <summary>The end-of-run lines go to the plugin's run output, each with the plugin's prefix, and stay few.</summary>
public class RunEndTests
{
    [Test]
    public async Task Run_without_problems_writes_the_files_where_configured_and_exactly_one_line()
    {
        var run = new Run();

        await run.Finish();

        Assert.That(new[] { run.Files.Viewer, run.Files.Traces, run.Files.Logs }.Select(File.Exists), Is.All.True);
        Assert.That(Lines(run.Info), Is.EqualTo(new[] { $"[Storks.OpenTelemetry] telemetry written to {run.Files.Viewer}" }));
        Assert.That(Lines(run.Errors), Is.Empty);
    }

    [Test]
    public async Task Window_Overlap_gives_one_summary_line_with_the_count_and_no_line_per_scenario()
    {
        var run = new Run();
        Envelope[] threeOverlapping = [.. RunMessagesTests.Definitions(), .. RunMessagesTests.Attempt("s1", 0),
            .. RunMessagesTests.Attempt("s2", 10), .. RunMessagesTests.Attempt("s3", 20)];
        foreach (var envelope in threeOverlapping) run.Messages.Add(envelope);

        await run.Finish();

        var errors = Lines(run.Errors);
        Assert.That(errors, Has.Length.EqualTo(1));
        Assert.That(errors[0], Does.StartWith("[Storks.OpenTelemetry] Window Overlap: 2 scenario windows opened while another was open")
            .And.Contain(WindowOverlap.ReadmeSection));
    }

    [Test]
    public async Task Problems_of_one_kind_give_one_line_with_the_count_and_the_first_example()
    {
        var run = new Run();
        for (var i = 0; i < 100; i++) run.Diagnostics.Report("rejected invalid OTLP request", $"/v1/traces: bad request {i}");

        await run.Finish();

        Assert.That(Lines(run.Errors), Is.EqualTo(new[]
        {
            "[Storks.OpenTelemetry] rejected invalid OTLP request (100 times, first: /v1/traces: bad request 0)",
        }));
    }

    [Test]
    public async Task Problems_of_two_kinds_give_two_lines_and_a_single_problem_has_no_count()
    {
        var run = new Run();
        run.Diagnostics.Report("rejected invalid OTLP request", "/v1/traces: bad request");
        run.Diagnostics.Report("failed to process OTLP request", "/v1/logs: store is broken");
        run.Diagnostics.Report("rejected invalid OTLP request", "/v1/logs: bad request");

        await run.Finish();

        Assert.That(Lines(run.Errors), Is.EqualTo(new[]
        {
            "[Storks.OpenTelemetry] rejected invalid OTLP request (2 times, first: /v1/traces: bad request)",
            "[Storks.OpenTelemetry] failed to process OTLP request: /v1/logs: store is broken",
        }));
    }

    [Test]
    public void Broken_run_output_does_not_fail_the_run_hook()
    {
        var output = new RunOutput(new BrokenWriter(), new BrokenWriter());

        Assert.DoesNotThrow(() => output.Info("telemetry viewer written"));
        Assert.DoesNotThrow(() => output.Error("failed to stop the receiver"));
    }

    private static string[] Lines(StringWriter writer) => writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    private sealed class Run
    {
        public ReportFiles Files { get; }
        public RunMessages Messages { get; } = new();
        public PluginDiagnostics Diagnostics { get; } = new();
        public StringWriter Info { get; } = new();
        public StringWriter Errors { get; } = new();

        public Run()
        {
            var dir = Path.Combine(Path.GetTempPath(), "storks-otel-tests", Guid.NewGuid().ToString("N"));
            Files = new ReportFiles(Path.Combine(dir, "v", "run.html"), Path.Combine(dir, "t", "traces.jsonl"), Path.Combine(dir, "logs.jsonl"));
        }

        public async Task Finish()
        {
            var store = new TelemetryStore();
            await using var receiver = new OtlpReceiver(store);
            await RunEnd.Finish(Files, receiver, store, Diagnostics, Messages, new RunOutput(Info, Errors));
        }
    }

    private sealed class BrokenWriter : TextWriter
    {
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
        public override void Write(char value) => throw new IOException("console is gone");
    }
}
