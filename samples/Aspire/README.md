# Aspire sample

1. **What it shows:** Reqnroll scenarios against an Aspire app with the plugin on; the `@demo-failure` scenario fails on purpose.
2. **Run:** `dotnet test samples/Aspire/App.Specs` from the repo root. Needs Docker (Postgres container).
3. **Report:** `samples/Aspire/App.Specs/bin/Debug/net11.0/reports/telemetry.html`.
4. **Projects:**
   - `AppHost`: Aspire app model: Orders.Api, Pricing.Api, Postgres.
   - `Orders.Api`: places orders, calls Pricing.Api, stores orders in Postgres.
   - `Pricing.Api`: returns SKU prices; fails for `BROKEN-1`.
   - `ServiceDefaults`: shared OpenTelemetry setup of the services.
   - `App.Specs`: the Reqnroll tests with the plugin.
