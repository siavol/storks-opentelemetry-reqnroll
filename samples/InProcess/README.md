# In-process sample

Reqnroll scenarios against an API hosted in the test process with `WebApplicationFactory`, with the plugin enabled. No Docker needed.

Projects:
- `Prices.Api`: returns SKU prices; its `Program` sets up OpenTelemetry the way a real app does.
- `Prices.Specs`: the Reqnroll tests with the plugin. `Hooks/SutHooks.cs` starts the API with `UseReqnrollExporter` in `[BeforeTestRun]` and disposes it in `[AfterTestRun]`.

## Run

From this folder:
```shell
dotnet test Prices.Specs
```

The report is written to `Prices.Specs/bin/Debug/net11.0/reports/telemetry.html`.
