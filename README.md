# Storks.OpenTelemetry.Reqnroll

A Reqnroll plugin that collects SUT telemetry (traces and logs over OTLP) during a test run and shows it by scenario and step in the Telemetry Viewer next to the Reqnroll report; in the report, every step and scenario links to the viewer.

The package `src/Storks.OpenTelemetry.Reqnroll` (assembly `Storks.OpenTelemetry.ReqnrollPlugin`) targets `net10.0`; its unit tests live in `tests/`, and the Aspire sample in `samples/Aspire/`. Building the whole repository requires the .NET 11 SDK and Docker (Postgres is started through Aspire).

## Running the application

```sh
dotnet run --project samples/Aspire/AppHost
```

The Aspire dashboard opens with traces and logs from Orders, Pricing and Postgres.

## Running the tests

```sh
dotnet test tests/Storks.OpenTelemetry.Reqnroll.Tests     # plugin unit tests, no Docker
dotnet test samples/Aspire/App.Sut.Tests                   # SUT through the AppHost
dotnet test samples/Aspire/App.Specs                       # Reqnroll scenarios; the red @demo-failure fails by design
dotnet test samples/Aspire/App.Specs.ReportChecks          # artifact checks; run after App.Specs
```

Without the red scenario: `dotnet test samples/Aspire/App.Specs --filter "TestCategory!=demo-failure"`.

Reports: `samples/Aspire/App.Specs/bin/Debug/net11.0/reports/` (`reqnroll_report.html`, `reqnroll_report.ndjson`, `telemetry.html` — the Telemetry Viewer, `traces.jsonl` and `logs.jsonl` — all of the run's telemetry in OTLP/JSON Lines). `STORKS_OTEL_REPORT_DIR` overrides the folder.
