# Standard OTLP/JSON and our own Telemetry Viewer next to the Reqnroll report instead of text in attachments

The plugin saves the run's telemetry to the report folder in the standard OTLP/JSON Lines format (`traces.jsonl`, `logs.jsonl`, as the OTLP File Exporter does), and it is meant to be viewed in our own viewer (the Telemetry Viewer, `telemetry.html`) next to the report. Text attachments on scenarios and steps are replaced by links to the viewer.

The reason is the limitations of the Reqnroll HTML formatter: text attachments are shown without UTF-8 (Cyrillic and special characters get garbled), and HTML in attachments is not rendered, so the span tree with logs stays flat text.

## Considered options

- **Text in attachments** (the previous approach). Needs no new artifacts, but hits the encoding problem and the lack of rendering; a waterfall and navigation cannot be done in text.
- **An external tool** (Jaeger, Aspire Dashboard, etc.). Requires running a service, does not work from a downloaded CI artifact without network access, and knows nothing about scenarios and steps. The standard files still remain available to any such tool.
- **Our own report entirely** instead of the Reqnroll report. Too expensive: we would have to repeat everything the formatter already does.

## Consequences

- Besides the neutral model (which stays for Attribution, see ADR 0001), the store keeps the original received OTLP requests. They are written to the files losslessly and without filtering by scenario, including Unattributed Telemetry: one line is one `ExportTraceServiceRequest` or `ExportLogsServiceRequest`.
- The files are written once, at the end of the run, to the report folder next to `telemetry.html` (`STORKS_OTEL_REPORT_DIR` overrides it). If the run does not reach the end, there are no files.
- A failure to write the files does not change the run's result: it goes to the diagnostic output.
- The viewer link in the report is not an attachment but a log message `<a href>` via `IReqnrollOutputHelper.WriteLine` (`text/x.cucumber.log+plain`): the formatter renders it as HTML, and the link is clickable. A `text/html` attachment is shown as escaped source, and `text/uri-list` cannot be produced through `AddAttachment`.
- Serialization follows OTLP/JSON, not "default protobuf JSON": trace/span ids in hex, enums as numbers, time in nanoseconds as a string.
