# The plugin is a Reqnroll formatter of its own, not an add-on to the Reqnroll report

The plugin is the Telemetry Formatter: a Reqnroll formatter (`ICucumberMessageFormatter`) named `opentelemetry`, switched on by its entry in the `formatters` configuration like `html` and `message`, alone or next to other formatters. It reads the Cucumber Messages stream, takes the Observation Windows from it, and writes the Telemetry Viewer and the OTLP/JSON files. It writes nothing into other formatters' reports: the "Open telemetry" links in the Reqnroll report are gone.

The reason: the links were log messages in the Cucumber Messages stream, so they reached every formatter (the NDJSON, Allure and so on), not only the HTML report, and the plugin ran as soon as the package was referenced, with no way to switch it off but removing the reference. The Telemetry Viewer already shows features, scenarios, steps, statuses and errors, so it stands as a report of the run on its own.

## Considered options

- **An add-on to the Reqnroll report** (the previous approach, ADR 0002): links written via `IReqnrollOutputHelper.WriteLine`, windows from execution events and binding hooks. Changes other reports, cannot be switched off by configuration.
- **A formatter for switching on, execution events for the windows.** Keeps the current window code, but works through two Reqnroll mechanisms at once.
- **A setting or environment variable of our own** instead of a formatter entry. One more configuration style next to Reqnroll's.

## Consequences

- Switched off (no `opentelemetry` entry, or formatters disabled), the plugin starts no receiver and writes no files; `ITelemetryEndpoint.IsEnabled` is false and `GetExporterEnvironment()` returns an empty map, so the test project's hooks need no change to switch it on and off. Whether it is on is decided in `CustomizeGlobalDependencies` from `IFormattersConfigurationProvider`, before any hook can ask for the endpoint.
- The files are written and the receiver stops on `TestRunFinished`, which Reqnroll sends after all `[AfterTestRun]` hooks and waits for. The plugin has no binding hooks of its own.
- Observation Windows come from the messages' timestamps (`TestCaseStarted`, `TestStepStarted`/`Finished`, `TestRunHookStarted`/`Finished`). Each retry attempt is a scenario run of its own. Run and feature hooks cannot be told apart by their message type, and a repeated feature hook's finish refers to its first run (a Reqnroll bug). The formatter pairs such a finish with the earliest unfinished start of the same hook, which is exact in sequential runs and can swap runs of the same hook in parallel ones; this affects only hooks in Unattributed Telemetry. A failed step's error text is empty in Reqnroll 3.0.x messages, so the formatter falls back to the exception type and stack trace there.
- Settings live in the formatter's configuration: `outputFilePath` (the viewer, `reports/telemetry.html` by default), `tracesFilePath` and `logsFilePath` (next to the viewer by default). `STORKS_OTEL_REPORT_DIR` is gone.
- The formatter cannot write into a scenario's test output, and the messages arrive after the scenario anyway, so all of the plugin's own output is a few lines at the end of the run: where the viewer is, the Window Overlap count, problems grouped by kind with a count.
- This supersedes the consequence of ADR 0002 about the viewer link in the report; the rest of ADR 0002 (OTLP/JSON files, our own viewer) stands.
