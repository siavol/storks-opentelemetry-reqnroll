using NUnit.Framework;

namespace GherkinOtel.ReqnrollPlugin.Tests;

/// <summary>Seam 2: attribution and rendering of spans (neutral model, no network).</summary>
public class SpanTreeTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly ObservationWindow Window = new(T0, T0.AddSeconds(10));

    private static TelemetrySpan Span(string id, string? parent, string name, double startSec, double? endSec,
        string service = "orders-api", SpanStatus status = SpanStatus.Unset, params (string, string)[] attrs) =>
        new("t1", id, parent, name, service, SpanKind.Server, T0.AddSeconds(startSec),
            endSec is null ? null : T0.AddSeconds(endSec.Value), status,
            attrs.ToDictionary(a => a.Item1, a => a.Item2));

    private static string[] Lines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static int Indent(string[] lines, string contains)
    {
        var l = lines.Single(x => x.Contains(contains));
        return l.Length - l.TrimStart().Length;
    }

    [Test]
    public void Span_started_in_window_and_ended_after_it_is_attributed()
    {
        var span = Span("a", null, "late", 9, 12);

        var result = Attribution.Attribute([], [span], Window);

        Assert.That(result.Scenario.Spans.Select(s => s.Name), Is.EqualTo(new[] { "late" }));
    }

    [Test]
    public void Span_started_before_window_is_not_attributed()
    {
        var span = Span("a", null, "early", -1, 3);

        var result = Attribution.Attribute([], [span], Window);

        Assert.That(result.Scenario.Spans, Is.Empty);
        Assert.That(result.RemainderSpans.Select(s => s.Name), Is.EqualTo(new[] { "early" }));
    }

    [Test]
    public void Unfinished_span_is_marked()
    {
        var text = TelemetryRenderer.Render(new ScenarioTelemetry(Window, [])
        {
            Spans = [Span("a", null, "hanging", 1, null)],
        });

        Assert.That(Lines(text).Single(l => l.Contains("hanging")), Does.Contain("не завершён"));
    }

    [Test]
    public void Span_with_parent_outside_the_set_becomes_a_root()
    {
        var text = TelemetryRenderer.Render(new ScenarioTelemetry(Window, [])
        {
            Spans = [Span("a", "missing-parent", "orphan", 1, 2)],
        });

        var line = Lines(text).Single(l => l.Contains("orphan"));
        Assert.That(line, Does.Match(@"^\+1\.000s"), "root spans are not indented");
    }

    [Test]
    public void Children_are_nested_under_their_parent_with_deeper_indent()
    {
        var text = TelemetryRenderer.Render(new ScenarioTelemetry(Window, [])
        {
            Spans =
            [
                Span("p", null, "POST /orders", 1, 3),
                Span("c", "p", "GET /prices", 1.5, 2, "orders-api"),
                Span("g", "c", "GET /prices/{sku}", 1.6, 1.9, "pricing-api"),
            ],
        });

        var lines = Lines(text);
        Assert.That(Indent(lines, "POST /orders"), Is.EqualTo(0));
        Assert.That(Indent(lines, "GET /prices "), Is.GreaterThan(Indent(lines, "POST /orders")));
        Assert.That(Indent(lines, "GET /prices/{sku}"), Is.GreaterThan(Indent(lines, "GET /prices ")));
        Assert.That(Array.FindIndex(lines, l => l.Contains("POST /orders")), Is.LessThan(Array.FindIndex(lines, l => l.Contains("GET /prices/{sku}"))));
    }

    [Test]
    public void Span_line_shows_service_relative_start_duration_status_and_key_attributes()
    {
        var text = TelemetryRenderer.Render(new ScenarioTelemetry(Window, [])
        {
            Spans =
            [
                Span("a", null, "GET /prices/{sku}", 1.25, 1.35, "pricing-api", SpanStatus.Error,
                    ("http.request.method", "GET"), ("http.route", "/prices/{sku}"), ("http.response.status_code", "500"),
                    ("db.query.text", "SELECT 1"), ("noise", "x")),
            ],
        });

        var line = Lines(text).Single();
        Assert.That(line, Does.StartWith("+1.250s"));
        Assert.That(line, Does.Contain("pricing-api"));
        Assert.That(line, Does.Contain("100ms"));
        Assert.That(line, Does.Contain("Error"));
        Assert.That(line, Does.Contain("GET").And.Contain("/prices/{sku}").And.Contain("500").And.Contain("SELECT 1"));
        Assert.That(line, Does.Not.Contain("noise"));
    }

    [Test]
    public void Log_with_span_id_is_rendered_under_its_span_and_log_without_is_top_level()
    {
        var span = Span("s", null, "POST /orders", 1, 3);
        var inside = new TelemetryLog(T0.AddSeconds(2), "orders-api", LogSeverity.Warning, "Cat", "inside-msg", "t1", "s");
        var loose = new TelemetryLog(T0.AddSeconds(5), "orders-api", LogSeverity.Information, "Cat", "loose-msg");

        var lines = Lines(TelemetryRenderer.Render(new ScenarioTelemetry(Window, [inside, loose]) { Spans = [span] }));

        Assert.That(Indent(lines, "inside-msg"), Is.GreaterThan(Indent(lines, "POST /orders")));
        Assert.That(Indent(lines, "loose-msg"), Is.EqualTo(0));
        var spanIdx = Array.FindIndex(lines, l => l.Contains("POST /orders"));
        Assert.That(Array.FindIndex(lines, l => l.Contains("inside-msg")), Is.EqualTo(spanIdx + 1));
        Assert.That(Array.FindIndex(lines, l => l.Contains("loose-msg")), Is.GreaterThan(spanIdx + 1));
    }

    [Test]
    public void Log_whose_span_is_not_in_the_set_stays_top_level()
    {
        var log = new TelemetryLog(T0.AddSeconds(2), "orders-api", LogSeverity.Warning, "Cat", "stray", "t1", "gone");

        var line = Lines(TelemetryRenderer.Render(new ScenarioTelemetry(Window, [log]))).Single();

        Assert.That(line, Does.Match(@"^\+2\.000s"));
    }
}
