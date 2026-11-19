# Storks.OpenTelemetry.Reqnroll

An experiment: collect the telemetry of the application under test while Reqnroll scenarios run and show it next to the test report, so the report shows what was happening inside the application.

## Language

**System Under Test (SUT)**:
The application whose behavior the scenarios verify and whose telemetry is collected.
_Avoid_: service, app, target

**Scenario Telemetry**:
SUT traces and logs attributed to a specific scenario (and, where possible, step) of a test run.
_Avoid_: captured telemetry, test traces, dump

**Telemetry Source**:
Where SUT telemetry comes from: the same process as the tests (in-process) or a separately started application (out-of-process).
_Avoid_: exporter, receiver, provider

**Attribution**:
Assigning a unit of telemetry to a scenario or step.
_Avoid_: correlation, matching, binding

**Observation Window**:
The time interval of a scenario or step; telemetry that started within this interval belongs to it.
_Avoid_: time range, timeframe, slot

**Window Overlap**:
The Observation Window of one scenario opening while another scenario's window is still open, as happens when scenarios run in parallel. Attribution goes by time only, so telemetry within the overlap shows up in each of the overlapping scenarios; the Telemetry Formatter reports how many there were at the end of the run.
_Avoid_: collision, parallel conflict

**Step Slice**:
The part of Scenario Telemetry that started within the Observation Window of one scenario step.
_Avoid_: step telemetry

**Telemetry Formatter**:
The plugin as a Reqnroll formatter: it is switched on by its entry in the `formatters` configuration, alone or next to other formatters, and writes the Telemetry Viewer and the run's telemetry files. Switched off, it collects nothing and writes nothing.
_Avoid_: reporter, add-on, extension

**Telemetry Viewer**:
The self-contained HTML page (`telemetry.html` by default) that the Telemetry Formatter writes: a report of the run on its own, with Scenario Telemetry by feature, scenario and step (a span waterfall, logs and properties on one timeline) and Unattributed Telemetry. Other formatters' reports are left as they are.
_Avoid_: dashboard, telemetry report, attachment

**Unattributed Telemetry**:
SUT telemetry that could not be attributed to any scenario.
_Avoid_: orphan spans, noise
