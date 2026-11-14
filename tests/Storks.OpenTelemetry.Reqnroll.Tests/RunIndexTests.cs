using System.Text.Json.Nodes;
using NUnit.Framework;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

public class RunIndexTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    private static ScenarioRun Scenario(RunRecord record, string feature, string name, params (string Status, string? Error)[] steps)
    {
        var run = record.Begin(feature, name, ["@demo"], T0);
        var n = 0;
        foreach (var (status, error) in steps)
        {
            n++;
            run.AddStep("When", $"step {n}", T0.AddMilliseconds(100 * (n - 1)), T0.AddMilliseconds(100 * n - 10), status, error);
        }
        run.Finish(T0.AddMilliseconds(100 * n), steps.Any(s => s.Status == "failed") ? "failed" : "passed", steps.FirstOrDefault(s => s.Error is not null).Error);
        return run;
    }

    [Test]
    public void Every_scenario_run_gets_its_own_id_even_for_the_same_scenario()
    {
        var record = new RunRecord();
        var a = record.Begin("F", "S", [], T0);
        var b = record.Begin("F", "S", [], T0);

        Assert.That(a.Id, Is.Not.EqualTo(b.Id));
        Assert.That(a.Id, Does.Match("^[0-9a-f]{32}$"), "id goes into a URL hash, keep it plain");
    }

    [Test]
    public void Index_groups_scenario_runs_by_feature_with_status_and_failed_step_error()
    {
        var record = new RunRecord();
        Scenario(record, "Checkout", "green", ("passed", null), ("passed", null));
        var red = Scenario(record, "Checkout", "red", ("passed", null), ("failed", "Expected 50 but was 0"));
        Scenario(record, "Other", "x", ("skipped", null));

        var index = RunIndex.Build(record.Snapshot());

        Assert.That(index.Features.Select(f => f.Name), Is.EqualTo(new[] { "Checkout", "Other" }));
        var checkout = index.Features[0];
        Assert.That(checkout.Scenarios.Select(s => s.Status), Is.EqualTo(new[] { "passed", "failed" }));
        var redScenario = checkout.Scenarios[1];
        Assert.That(redScenario.Id, Is.EqualTo(red.Id));
        Assert.That(redScenario.Steps[1].Status, Is.EqualTo("failed"));
        Assert.That(redScenario.Steps[1].Error, Is.EqualTo("Expected 50 but was 0"));
        Assert.That(redScenario.Error, Is.EqualTo("Expected 50 but was 0"));
    }

    [Test]
    public void Feature_carries_the_path_of_its_feature_file()
    {
        var record = new RunRecord();
        record.Begin("Checkout", "S", [], T0, "Features/Checkout.feature").Finish(T0, "passed", null);

        var json = JsonNode.Parse(RunIndex.Build(record.Snapshot()).ToJson())!;

        Assert.That((string?)json["features"]![0]!["uri"], Is.EqualTo("Features/Checkout.feature"));
    }

    [Test]
    public void Steps_are_numbered_from_one_and_have_window_relative_to_scenario_start()
    {
        var record = new RunRecord();
        Scenario(record, "F", "S", ("passed", null), ("passed", null));

        var scenario = RunIndex.Build(record.Snapshot()).Features.Single().Scenarios.Single();

        Assert.That(scenario.Steps.Select(s => s.Number), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(scenario.Steps[1].OffsetMs, Is.EqualTo(100));
        Assert.That(scenario.Steps[1].DurationMs, Is.EqualTo(90));
        Assert.That(scenario.Tags, Is.EqualTo(new[] { "@demo" }));
    }

    [Test]
    public void Scenario_duration_spans_its_steps_from_the_first_step_start_to_the_last_step_end_without_the_hooks_around_them()
    {
        var record = new RunRecord();
        var run = record.Begin("F", "S", [], T0);
        run.AddHook(new HookRun("BeforeScenario", "AppHooks.Seed", T0.AddMilliseconds(1), T0.AddMilliseconds(40), "passed"));
        run.AddStep("Given", "a", T0.AddMilliseconds(50), T0.AddMilliseconds(60), "passed", null);
        run.AddStep("When", "b", T0.AddMilliseconds(100), T0.AddMilliseconds(170), "passed", null);
        run.AddHook(new HookRun("AfterScenario", "AppHooks.Clean", T0.AddMilliseconds(180), T0.AddMilliseconds(900), "passed"));
        run.Finish(T0.AddMilliseconds(1000), "passed", null);
        var stepless = record.Begin("F", "no steps", [], T0);
        stepless.Finish(T0.AddMilliseconds(30), "passed", null);

        var scenarios = RunIndex.Build(record.Snapshot()).Features.Single().Scenarios;

        Assert.That(scenarios[0].DurationMs, Is.EqualTo(120), "as the header of prototype D");
        Assert.That(scenarios[1].DurationMs, Is.EqualTo(30), "without steps: the whole window");
    }

    [Test]
    public void Json_contains_every_scenario_id_and_uses_camelCase()
    {
        var record = new RunRecord();
        var a = Scenario(record, "F", "a", ("passed", null));
        var b = Scenario(record, "F", "b", ("failed", "boom"));

        var json = JsonNode.Parse(RunIndex.Build(record.Snapshot()).ToJson())!;

        var ids = json["features"]![0]!["scenarios"]!.AsArray().Select(s => (string)s!["id"]!).ToArray();
        Assert.That(ids, Is.EqualTo(new[] { a.Id, b.Id }));
        Assert.That((string)json["features"]![0]!["scenarios"]![1]!["steps"]![0]!["error"]!, Is.EqualTo("boom"));
    }

    [Test]
    public void Links_point_to_the_viewer_with_scenario_and_optional_step_in_hash()
    {
        Assert.That(TelemetryLinks.Scenario("abc"), Is.EqualTo("telemetry.html#s=abc"));
        Assert.That(TelemetryLinks.Step("abc", 3), Is.EqualTo("telemetry.html#s=abc&step=3"));
    }
}
