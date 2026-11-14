# Storks.OpenTelemetry.Reqnroll

A Reqnroll plugin that receives your application's OpenTelemetry traces and logs during `dotnet test` and shows them by scenario and step in a Telemetry Viewer linked from the Reqnroll report.

![Telemetry Viewer: a failed scenario with the span waterfall of its first step and the step's logs, including the error logged by the pricing service](https://raw.githubusercontent.com/OWNER/storks-opentelemetry-reqnroll/main/docs/images/telemetry-viewer.png)

## Why

A scenario goes red: `Expected: 50m But was: 0m`. The test only sees the answer; the reason is inside the system under test (SUT). Orders called Pricing, Pricing failed and logged an error, Orders fell back to a zero price. To find that, you usually open Jaeger, the Aspire dashboard or log files, guess when the scenario ran, narrow the results down to Orders and Pricing and work out which requests belong to which scenario.

With the plugin, every step and scenario in the Reqnroll report has an **Open telemetry** link. It opens the spans and logs of exactly that step or scenario: the span waterfall across services, the logs on the same timeline, and the properties of each span and log. In the screenshot above, the failed `GET /prices/{sku}` span and the `Pricing failed for SKU BROKEN-1` log are right next to the red step.

The viewer is one self-contained HTML file next to the report. It needs no running service and works the same from a CI artifact you downloaded. All of the run's telemetry is also saved as standard OTLP/JSON Lines for any other tool.

## Quickstart

You need:

- a test project on `net10.0` or later with Reqnroll 3.x;
- a SUT that the tests start on the same machine, instrumented with OpenTelemetry and exporting over OTLP with the exporter settings taken from the standard `OTEL_*` environment variables. In .NET, `UseOtlpExporter()` or `AddOtlpExporter()` do that as long as the code does not set the endpoint itself.

**1. Install the package** into the test project. The plugin activates by being referenced; there is nothing to register in `reqnroll.json`.

```sh
dotnet add package Storks.OpenTelemetry.Reqnroll
```

**2. Point the SUT's exporter at the plugin.** In the `[BeforeTestRun]` hook that starts your SUT, take `ITelemetryEndpoint` as a parameter and copy `GetExporterEnvironment()` into the SUT's environment. That loop is the one line to add:

```csharp
using System.Diagnostics;
using Reqnroll;
using Storks.OpenTelemetry.Reqnroll;

[Binding]
public static class SutHooks
{
    private static Process? _sut;

    [BeforeTestRun]
    public static void StartSut(ITelemetryEndpoint telemetry)
    {
        var start = new ProcessStartInfo("dotnet", "path/to/MyApi.dll --urls http://127.0.0.1:5080");
        foreach (var (name, value) in telemetry.GetExporterEnvironment()) start.Environment[name] = value;
        _sut = Process.Start(start);
        // ... wait until the SUT answers
    }

    [AfterTestRun]
    public static void StopSut() => _sut?.Kill(entireProcessTree: true);
}
```

`GetExporterEnvironment()` returns a plain name-to-value map, so it goes wherever environment variables go. With Aspire (`Aspire.Hosting.Testing`), for each project resource:

```csharp
builder.CreateResourceBuilder<ProjectResource>("orders-api")
    .WithEnvironment(context =>
    {
        foreach (var (name, value) in telemetry.GetExporterEnvironment()) context.EnvironmentVariables[name] = value;
    });
```

**3. Put the Reqnroll report next to the viewer**, so that its links work. Reqnroll writes no HTML report unless a formatter is configured; add a `reqnroll.json` to the test project:

```json
{
  "$schema": "https://schemas.reqnroll.net/reqnroll-config-latest.json",
  "formatters": {
    "html": { "outputFilePath": "reports/reqnroll_report.html" }
  }
}
```

**4. Run the tests:**

```sh
dotnet test
```

**5. Open the report.** The run writes everything to the `reports` folder next to the test assembly, for example `bin/Debug/net10.0/reports/`:

| File | What it is |
| --- | --- |
| `reqnroll_report.html` | The Reqnroll report (from step 3); every step and scenario links to the viewer. |
| `telemetry.html` | The Telemetry Viewer: the run's traces and logs by scenario and step. Opens on its own too. |
| `traces.jsonl`, `logs.jsonl` | All of the run's telemetry in OTLP/JSON Lines. |

Open `reqnroll_report.html` and follow **Open telemetry** on a step, or open `telemetry.html` directly.

## How it works

<!-- Generated from docs/images/how-it-works.mmd, see CONTRIBUTING.md. A PNG, because nuget.org renders no Mermaid. -->
![The SUT sends traces and logs over OTLP/HTTP to the plugin's receiver inside the test process; the plugin takes scenario and step start and end from the Reqnroll scenarios and, at the end of the run, writes telemetry.html, traces.jsonl and logs.jsonl to the reports folder, where the Open telemetry links of reqnroll_report.html lead](https://raw.githubusercontent.com/OWNER/storks-opentelemetry-reqnroll/main/docs/images/how-it-works.png)

1. **Receiver.** When the test run starts, the plugin starts an OTLP/HTTP (protobuf) receiver inside the test process, on a random port of `127.0.0.1`. It accepts traces and logs; metrics are accepted and dropped.
2. **Exporter settings.** `ITelemetryEndpoint.GetExporterEnvironment()` returns the variables that send the SUT's OTLP exporter there:

   | Variable | Value |
   | --- | --- |
   | `OTEL_EXPORTER_OTLP_ENDPOINT` | The receiver address, e.g. `http://127.0.0.1:41234` |
   | `OTEL_EXPORTER_OTLP_PROTOCOL` | `http/protobuf` |
   | `OTEL_BSP_SCHEDULE_DELAY` | `100` (ms; the SDK default is 5000) |
   | `OTEL_BLRP_SCHEDULE_DELAY` | `100` (ms; the SDK default is 1000) |

3. **Observation Windows.** Every scenario and every step has an Observation Window, the time from its start to its end. The plugin's hooks open a scenario's window before your `[BeforeScenario]` hooks and close it after your `[AfterScenario]` hooks, so the telemetry of your scenario hooks belongs to the scenario too (the viewer shows it with **Show hooks**).
4. **Attribution.** At the end of the run, each span is attributed to the window it started in, and each log to the window it was written in. Telemetry outside every scenario window, such as SUT startup or your run hooks, is Unattributed Telemetry and has its own section in the viewer.
5. **Output.** After your `[AfterTestRun]` hooks, the plugin writes `telemetry.html`, `traces.jsonl` and `logs.jsonl` and stops the receiver. The Reqnroll report gets an **Open telemetry** link on every step and scenario.

The plugin never changes a scenario's result. Its own lines start with `[Storks.OpenTelemetry]`. Problems during a scenario (a rejected OTLP request) go to that scenario's test output. The end-of-run lines (where the files were written, a file it could not write, the Window Overlap summary) belong to no test, so they go to the console of the test process; `dotnet test` shows that only at detailed verbosity:

```sh
dotnet test --logger "console;verbosity=detailed"
```

## Configuration

The plugin has one setting, an environment variable of the test process:

| Variable | Default | What it does |
| --- | --- | --- |
| `STORKS_OTEL_REPORT_DIR` | `reports` next to the test assembly (`bin/<configuration>/<tfm>/reports`) | Folder for `telemetry.html`, `traces.jsonl` and `logs.jsonl`. If you change it, point the Reqnroll HTML formatter at the same folder, or the report's links break. |

The exporter variables in the table above are not plugin settings: they are what the helper hands to your SUT.

## Limitations

### Loopback only

The receiver listens on `127.0.0.1` only, on a random port. The SUT must run on the same machine as the tests and outside containers: inside a container, `127.0.0.1` is the container itself, so a SUT in Docker (Testcontainers, Docker Compose) cannot reach the receiver yet.

### Window Overlap

Attribution goes by time only. When scenarios run in parallel, the Observation Window of one scenario opens while another one is still open: that is a Window Overlap. Telemetry that started in the overlap shows up in each of the overlapping scenarios, because the plugin cannot tell which scenario caused it.

The plugin warns about it. The test output of a scenario gets a line like:

```text
[Storks.OpenTelemetry] Window Overlap: scenario 'Pricing: Apply discount' started while 'Checkout: Pay by card' was still running, so telemetry in the overlap shows up in each of the overlapping scenarios. See https://github.com/OWNER/storks-opentelemetry-reqnroll/blob/main/README.md#window-overlap
```

and the end of the run writes one summary line with the number of overlaps to the console of the test process (with `dotnet test`, at detailed verbosity, see [How it works](#how-it-works)). In a parallel run the warning can land in the output of a neighbouring scenario; both scenario names are in the text. Sequential runs print nothing.

To get exact Attribution, run the scenarios whose telemetry you care about sequentially (do not enable parallel execution for those features in your test framework). Attribution by W3C trace context (`traceparent`), which would make parallel runs exact, is planned for 0.2.

### Export delay and the end of the run

Telemetry is attributed by its own timestamps at the end of the run, so a span that is exported late still lands in the right window, and a span that started in a step but finished after it still belongs to that step. There is no pause before a window closes, and none is needed for that. What matters is that the telemetry reaches the receiver before the run ends:

- The receiver stops after your `[AfterTestRun]` hooks. Whatever the SUT exports after that is lost. The helper shortens the exporters' batch delay to 100 ms; with the SDK defaults (5 s for spans) the last scenarios' telemetry would usually still be waiting in the SUT when it stops. Stop the SUT in your `[AfterTestRun]` hook, and stop it gracefully if you can, so that it flushes on exit.
- Work the SUT does in the background after a step ends (queues, timers) starts in a later window, or outside every window, and is attributed there.
- The files are written at the end of the run. A run that is aborted before its end writes no files.

## Samples

- [`samples/Aspire`](https://github.com/OWNER/storks-opentelemetry-reqnroll/tree/main/samples/Aspire): two ASP.NET Core services and Postgres started with `Aspire.Hosting.Testing`, Reqnroll scenarios on NUnit, and one scenario (`@demo-failure`) that fails on purpose. The screenshot above is from it. How to run it is in [CONTRIBUTING.md](https://github.com/OWNER/storks-opentelemetry-reqnroll/blob/main/CONTRIBUTING.md#the-aspire-sample).

Planned: a SUT in Docker, an in-process SUT, Allure reports, and Attribution by trace context. The roadmap will be in GitHub Issues.

## Contributing and license

Build, test and PR instructions are in [CONTRIBUTING.md](https://github.com/OWNER/storks-opentelemetry-reqnroll/blob/main/CONTRIBUTING.md). Terms such as Observation Window and Attribution are defined in [GLOSSARY.md](https://github.com/OWNER/storks-opentelemetry-reqnroll/blob/main/GLOSSARY.md). To report a vulnerability, see [SECURITY.md](https://github.com/OWNER/storks-opentelemetry-reqnroll/blob/main/SECURITY.md).

Licensed under the [MIT License](https://github.com/OWNER/storks-opentelemetry-reqnroll/blob/main/LICENSE).
