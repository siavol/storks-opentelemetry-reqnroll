# The SUT runs out-of-process in tests via Aspire.Hosting.Testing

Scenarios run against the SUT started from the same AppHost as a manual run (Orders, Pricing, Postgres), not against an in-process `WebApplicationFactory`. So the plugin cannot hook into the application's OTel SDK and receives telemetry over OTLP: an OTLP receiver runs in the test process, and the AppHost resources export to it. We chose this path because it is more realistic and needs no separate test infrastructure (Testcontainers).

## Consequences

- The plugin core works with its own neutral span/log model. OTLP is just one source; an in-process source can be added later without changing the core. The OTLP → neutral model mapping lives only in the receiver (`Receiver/`); the attribution index is built from the neutral model in the store. A log refers to its original record by an opaque id supplied by the source: for the OTLP receiver it is the record's position among the received requests, which the Telemetry Viewer uses to find the record in the embedded OTLP/JSON (ADR 0002).
- The export batch of another process cannot be flushed: the resources get a shorter export interval, and everything the user sees is built by the plugin at the end of the run, after the AppHost stops, when the SUT processes have already sent what was left. There are no pauses in hooks or at the end of the run: Attribution goes by the telemetry's own timestamps, so late export does not move it, and stopping the AppHost waits for the SUT processes to export what they have. If the test project does not stop the SUT in AfterTestRun, telemetry from the last milliseconds may not make it.
- The AppHost starts once per run, not per scenario.
