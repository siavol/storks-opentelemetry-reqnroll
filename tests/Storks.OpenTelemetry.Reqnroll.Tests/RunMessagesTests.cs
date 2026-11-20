using System.Text.Json;
using Io.Cucumber.Messages.Types;
using NUnit.Framework;
using Reqnroll.Formatters.PayloadProcessing;
using Storks.OpenTelemetry.Reqnroll.Core;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

/// <summary>
/// The Cucumber Messages stream as the formatter receives it, plus received telemetry, gives the attribution index.
/// Envelopes are written as NDJSON, the way Reqnroll serializes them, since the message constructors differ between Reqnroll versions.
/// </summary>
public class RunMessagesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    private static object Ts(double ms)
    {
        var t = T0.AddTicks((long)Math.Round(ms * TimeSpan.TicksPerMillisecond)) - DateTimeOffset.UnixEpoch;
        return new { seconds = (long)t.TotalSeconds, nanos = (t.Ticks % TimeSpan.TicksPerSecond) * 100 };
    }

    private static Envelope E(object message) => NdjsonSerializer.Deserialize(JsonSerializer.Serialize(message));

    private static readonly object At = new { line = 1, column = 1 };

    private static object GherkinStep(string id, string keyword, string keywordType, string text) =>
        new { location = At, keyword, keywordType, text, id };

    private static Envelope Document() => E(new
    {
        gherkinDocument = new
        {
            uri = "Features/Checkout.feature",
            comments = Array.Empty<object>(),
            feature = new
            {
                location = At,
                tags = Array.Empty<object>(),
                language = "en",
                keyword = "Feature",
                name = "Checkout",
                description = "",
                children = new object[]
                {
                    new
                    {
                        background = new
                        {
                            location = At, keyword = "Background", name = "", description = "", id = "bg",
                            steps = new[] { GherkinStep("ast-given", "Given ", "Context", "a customer") },
                        },
                    },
                    new
                    {
                        scenario = new
                        {
                            location = At, tags = Array.Empty<object>(), keyword = "Scenario", name = "Pay by card", description = "",
                            examples = Array.Empty<object>(), id = "ast-scenario",
                            steps = new[]
                            {
                                GherkinStep("ast-when", "When ", "Action", "the customer pays"),
                                GherkinStep("ast-and", "And ", "Conjunction", "the customer confirms"),
                            },
                        },
                    },
                },
            },
        },
    });

    private static Envelope Pickle(string id, string name) => E(new
    {
        pickle = new
        {
            id,
            uri = "Features/Checkout.feature",
            name,
            language = "en",
            astNodeIds = new[] { "ast-scenario" },
            tags = new[] { new { name = "@demo-failure", astNodeId = "tag" } },
            steps = new[]
            {
                new { id = id + "-given", astNodeIds = new[] { "ast-given" }, type = "Context", text = "a customer" },
                new { id = id + "-when", astNodeIds = new[] { "ast-when" }, type = "Action", text = "the customer pays" },
                new { id = id + "-and", astNodeIds = new[] { "ast-and" }, type = "Action", text = "the customer confirms" },
            },
        },
    });

    private static Envelope Hook(string id, string type, string className, string method) => E(new
    {
        hook = new { id, type, sourceReference = new { javaMethod = new { className, methodName = method, methodParameterTypes = Array.Empty<string>() } } },
    });

    /// <summary>A test case of the pickle: BeforeScenario hook, the three steps, AfterScenario hook.</summary>
    private static Envelope TestCase(string id, string pickleId) => E(new
    {
        testCase = new
        {
            id,
            pickleId,
            testSteps = new object[]
            {
                new { id = id + "-before", hookId = "h-before" },
                new { id = id + "-given", pickleStepId = pickleId + "-given" },
                new { id = id + "-when", pickleStepId = pickleId + "-when" },
                new { id = id + "-and", pickleStepId = pickleId + "-and" },
                new { id = id + "-after", hookId = "h-after" },
            },
        },
    });

    private static Envelope CaseStarted(string id, string testCaseId, double ms, int attempt = 0) =>
        E(new { testCaseStarted = new { id, testCaseId, attempt, timestamp = Ts(ms) } });

    private static Envelope CaseFinished(string startedId, double ms, bool willBeRetried = false) =>
        E(new { testCaseFinished = new { testCaseStartedId = startedId, timestamp = Ts(ms), willBeRetried } });

    private static Envelope StepStarted(string startedId, string testStepId, double ms) =>
        E(new { testStepStarted = new { testCaseStartedId = startedId, testStepId, timestamp = Ts(ms) } });

    private static Envelope StepFinished(string startedId, string testStepId, double ms, string status = "PASSED", object? exception = null) =>
        E(new
        {
            testStepFinished = new
            {
                testCaseStartedId = startedId,
                testStepId,
                timestamp = Ts(ms),
                testStepResult = new { status, duration = new { seconds = 0, nanos = 0 }, exception },
            },
        });

    private static Envelope[] Definitions() =>
    [
        Hook("h-before", "BEFORE_TEST_CASE", "App.Specs.App.Specs.Hooks.OrdersReadinessHooks", "EnsureOrdersAnswers"),
        Hook("h-after", "AFTER_TEST_CASE", "App.Specs.App.Specs.Hooks.DataHooks", "Cleanup"),
        Document(),
        Pickle("p1", "Pay by card"),
        TestCase("tc1", "p1"),
    ];

    /// <summary>One run of test case tc1 from <paramref name="at"/>: hook [0,10], steps [20,30], [40,50], [60,70], hook [80,90], finished at 100.</summary>
    private static Envelope[] Attempt(string startedId, double at, string lastStatus = "PASSED", object? exception = null, bool willBeRetried = false) =>
    [
        CaseStarted(startedId, "tc1", at),
        StepStarted(startedId, "tc1-before", at), StepFinished(startedId, "tc1-before", at + 10),
        StepStarted(startedId, "tc1-given", at + 20), StepFinished(startedId, "tc1-given", at + 30),
        StepStarted(startedId, "tc1-when", at + 40), StepFinished(startedId, "tc1-when", at + 50),
        StepStarted(startedId, "tc1-and", at + 60), StepFinished(startedId, "tc1-and", at + 70, lastStatus, exception),
        StepStarted(startedId, "tc1-after", at + 80), StepFinished(startedId, "tc1-after", at + 90),
        CaseFinished(startedId, at + 100, willBeRetried),
    ];

    private static (RunRecord Record, RunTracker Tracker) Read(IEnumerable<Envelope> envelopes)
    {
        var (record, tracker, problems) = ReadWithProblems(envelopes);
        Assert.That(problems, Has.None.Contain("failed"), "every test case run is read");
        return (record, tracker);
    }

    private static (RunRecord Record, RunTracker Tracker, IReadOnlyList<string> Problems) ReadWithProblems(IEnumerable<Envelope> envelopes)
    {
        var record = new RunRecord(T0);
        var diagnostics = new PluginDiagnostics();
        var tracker = new RunTracker(diagnostics);
        var messages = new RunMessages();
        foreach (var envelope in envelopes) messages.Add(envelope);
        messages.Record(record, tracker, diagnostics);
        return (record, tracker, diagnostics.Drain());
    }

    private static string UnixNano(double ms) =>
        ((T0.AddTicks((long)Math.Round(ms * TimeSpan.TicksPerMillisecond)) - DateTimeOffset.UnixEpoch).Ticks * 100).ToString(System.Globalization.CultureInfo.InvariantCulture);

    [Test]
    public void Scenario_steps_and_scenario_hooks_get_their_windows_names_and_tags_from_the_messages()
    {
        var (record, _) = Read([.. Definitions(), .. Attempt("s1", 1000)]);

        var feature = RunIndex.Build(record).Features.Single();
        Assert.That(feature.Name, Is.EqualTo("Checkout"));
        var scenario = feature.Scenarios.Single();
        Assert.That((scenario.Name, scenario.Status), Is.EqualTo(("Pay by card", "passed")));
        Assert.That(scenario.Tags, Is.EqualTo(new[] { "demo-failure" }), "as Reqnroll's scenario tags, without '@'");
        Assert.That((scenario.StartUnixNano, scenario.EndUnixNano), Is.EqualTo((UnixNano(1000), UnixNano(1100))));
        Assert.That(scenario.Steps.Select(s => (s.Number, s.Keyword, s.Text, s.StartUnixNano, s.EndUnixNano)), Is.EqualTo(new[]
        {
            (1, "Given", "a customer", UnixNano(1020), UnixNano(1030)),
            (2, "When", "the customer pays", UnixNano(1040), UnixNano(1050)),
            (3, "And", "the customer confirms", UnixNano(1060), UnixNano(1070)),
        }));
        Assert.That(scenario.Hooks.Select(h => (h.HookType, h.Name, h.Status, h.StartUnixNano, h.EndUnixNano)), Is.EqualTo(new[]
        {
            ("BeforeScenario", "OrdersReadinessHooks.EnsureOrdersAnswers", "passed", UnixNano(1000), UnixNano(1010)),
            ("AfterScenario", "DataHooks.Cleanup", "passed", UnixNano(1080), UnixNano(1090)),
        }));
    }

    [Test]
    public void Telemetry_of_a_step_window_is_attributed_to_that_step()
    {
        var (record, _) = Read([.. Definitions(), .. Attempt("s1", 1000)]);
        var span = new TelemetrySpan("t1", "s-when", null, "POST /orders", "orders-api", SpanKind.Server,
            T0.AddMilliseconds(1045), T0.AddMilliseconds(1046), SpanStatus.Unset, new Dictionary<string, string>());

        var scenario = RunIndex.Build(record, [span]).Features.Single().Scenarios.Single();

        Assert.That(scenario.Steps.Single(s => s.Keyword == "When").SpanIds, Is.EqualTo(new[] { "s-when" }));
    }

    [Test]
    public void Every_attempt_of_a_retried_test_case_is_a_scenario_run_of_its_own()
    {
        var (record, _) = Read([.. Definitions(), .. Attempt("s1", 1000, "FAILED", willBeRetried: true), .. Attempt("s2", 2000)]);

        var scenarios = RunIndex.Build(record).Features.Single().Scenarios;
        Assert.That(scenarios.Select(s => (s.Status, s.StartUnixNano)), Is.EqualTo(new[] { ("failed", UnixNano(1000)), ("passed", UnixNano(2000)) }));
        Assert.That(scenarios[0].Id, Is.Not.EqualTo(scenarios[1].Id));
    }

    [Test]
    public void Failed_step_carries_the_exception_message()
    {
        var exception = new { type = "AssertionException", message = "Expected: 50m", stackTrace = "   at CheckoutSteps.Then()" };
        var (record, _) = Read([.. Definitions(), .. Attempt("s1", 1000, "FAILED", exception)]);

        var scenario = RunIndex.Build(record).Features.Single().Scenarios.Single();
        Assert.That(scenario.Status, Is.EqualTo("failed"));
        Assert.That(scenario.Steps[2].Status, Is.EqualTo("failed"));
        Assert.That(scenario.Steps[2].Error, Is.EqualTo("Expected: 50m"));
        Assert.That(scenario.Error, Is.EqualTo(scenario.Steps[2].Error));
    }

    [Test]
    public void Without_an_exception_message_the_error_is_the_exception_type_and_stack_trace()
    {
        var exception = new { type = "AssertionException", message = "", stackTrace = "   at CheckoutSteps.Then()" };
        var (record, _) = Read([.. Definitions(), .. Attempt("s1", 1000, "FAILED", exception)]);

        var step = RunIndex.Build(record).Features.Single().Scenarios.Single().Steps[2];
        Assert.That(step.Error, Is.EqualTo("AssertionException\n\n   at CheckoutSteps.Then()"));
    }

    [Test]
    public void Interleaved_messages_of_two_parallel_scenarios_are_told_apart_by_test_case_run_and_counted_as_Window_Overlap()
    {
        var first = Attempt("s1", 1000);
        var second = Attempt("s2", 1050, "FAILED");
        var interleaved = first.Zip(second, (a, b) => new[] { a, b }).SelectMany(x => x);

        var (record, tracker) = Read([.. Definitions(), .. interleaved]);

        var scenarios = RunIndex.Build(record).Features.Single().Scenarios;
        Assert.That(scenarios.Select(s => (s.Status, s.StartUnixNano, s.Steps.Count, s.Hooks.Count)),
            Is.EqualTo(new[] { ("passed", UnixNano(1000), 3, 2), ("failed", UnixNano(1050), 3, 2) }));
        Assert.That(scenarios[1].Steps[0].StartUnixNano, Is.EqualTo(UnixNano(1070)));
        Assert.That(tracker.WindowOverlapSummary(), Does.StartWith("Window Overlap: 1 scenario window opened"));
    }

    private static Envelope RunHookStarted(string id, string hookId, double ms) =>
        E(new { testRunHookStarted = new { id, testRunStartedId = "run", hookId, timestamp = Ts(ms) } });

    private static Envelope RunHookFinished(string startedId, double ms, string status = "PASSED") =>
        E(new
        {
            testRunHookFinished = new
            {
                testRunHookStartedId = startedId,
                result = new { status, duration = new { seconds = 0, nanos = 0 } },
                timestamp = Ts(ms),
            },
        });

    [Test]
    public void BeforeTestRun_and_AfterTestRun_hooks_get_their_windows_on_the_unattributed_scale()
    {
        var (record, _) = Read(
        [
            Hook("h-start", "BEFORE_TEST_RUN", "App.Specs.App.Specs.Hooks.AppHostHooks", "StartAppHost"),
            Hook("h-stop", "AFTER_TEST_RUN", "App.Specs.App.Specs.Hooks.AppHostHooks", "StopAppHost"),
            RunHookStarted("r1", "h-start", 0), RunHookFinished("r1", 500),
            RunHookStarted("r2", "h-stop", 900), RunHookFinished("r2", 1000, "FAILED"),
        ]);

        var hooks = RunIndex.Build(record).Unattributed.Hooks;
        Assert.That(hooks.Select(h => (h.HookType, h.Name, h.Status, h.StartUnixNano, h.EndUnixNano)), Is.EqualTo(new[]
        {
            ("BeforeTestRun", "AppHostHooks.StartAppHost", "passed", UnixNano(0), UnixNano(500)),
            ("AfterTestRun", "AppHostHooks.StopAppHost", "failed", UnixNano(900), UnixNano(1000)),
        }));
    }

    [Test]
    public void Repeated_feature_hook_whose_finish_points_at_the_first_start_gets_a_window_per_run()
    {
        // Reqnroll's TestRunHookFinished of a repeated [BeforeFeature] refers to the id of its first start.
        var (record, _) = Read(
        [
            Hook("h-seed", "BEFORE_TEST_RUN", "App.Specs.App.Specs.Hooks.FeatureHooks", "Seed"),
            RunHookStarted("f1", "h-seed", 100), RunHookFinished("f1", 200),
            RunHookStarted("f2", "h-seed", 300), RunHookFinished("f1", 450),
        ]);

        var hooks = RunIndex.Build(record).Unattributed.Hooks;
        Assert.That(hooks.Select(h => (h.Name, h.StartUnixNano, h.EndUnixNano)), Is.EqualTo(new[]
        {
            ("FeatureHooks.Seed", UnixNano(100), UnixNano(200)),
            ("FeatureHooks.Seed", UnixNano(300), UnixNano(450)),
        }));
    }

    [Test]
    public void Run_hook_call_that_cannot_be_read_is_reported_and_the_other_run_hooks_are_kept()
    {
        var (record, _, problems) = ReadWithProblems(
        [
            Hook("h-start", "BEFORE_TEST_RUN", "App.Specs.App.Specs.Hooks.AppHostHooks", "StartAppHost"),
            Hook("h-stop", "AFTER_TEST_RUN", "App.Specs.App.Specs.Hooks.AppHostHooks", "StopAppHost"),
            RunHookStarted("r1", "h-start", 0), RunHookStarted("r1", "h-start", 10),
            E(new
            {
                testRunHookFinished = new
                {
                    testRunHookStartedId = "r1",
                    result = new { status = "PASSED", duration = new { seconds = 0, nanos = 0 } },
                    timestamp = new { seconds = long.MaxValue / 2, nanos = 0 },
                },
            }),
            RunHookStarted("r2", "h-stop", 900), RunHookFinished("r2", 1000),
        ]);

        Assert.That(RunIndex.Build(record).Unattributed.Hooks.Select(h => h.Name), Is.EqualTo(new[] { "AppHostHooks.StopAppHost" }));
        Assert.That(problems, Has.One.StartsWith("failed to read a run or feature hook call from Cucumber Messages"));
    }

    [Test]
    public void Run_hook_without_a_finish_is_not_in_the_index()
    {
        var (record, _) = Read(
        [
            Hook("h-start", "BEFORE_TEST_RUN", "App.Specs.App.Specs.Hooks.AppHostHooks", "StartAppHost"),
            RunHookStarted("r1", "h-start", 0),
        ]);

        Assert.That(RunIndex.Build(record).Unattributed.Hooks, Is.Empty);
    }

    [Test]
    public void Scenarios_one_after_another_have_no_Window_Overlap()
    {
        var (_, tracker) = Read([.. Definitions(), .. Attempt("s1", 1000), .. Attempt("s2", 1100)]);

        Assert.That(tracker.WindowOverlapSummary(), Is.Null);
    }
}
