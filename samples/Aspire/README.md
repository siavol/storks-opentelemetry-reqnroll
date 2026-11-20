# Aspire sample

Reqnroll scenarios against an Aspire app with the plugin enabled; the `@demo-failure` scenario fails on purpose.

Projects:
- `AppHost`: the Aspire app model (Orders.Api, Pricing.Api, Postgres).
- `Orders.Api`: places orders, calls Pricing.Api, stores orders in Postgres.
- `Pricing.Api`: returns SKU prices; fails for `BROKEN-1`.
- `ServiceDefaults`: shared OpenTelemetry setup of the services.
- `App.Specs`: the Reqnroll tests with the plugin.

## Run

Prerequisite: Docker, for the Postgres container.

From this folder:
```shell
dotnet test App.Specs
```

The report is written to `App.Specs/bin/Debug/net11.0/reports/telemetry.html`.
