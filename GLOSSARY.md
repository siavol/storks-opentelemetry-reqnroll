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

**Step Slice**:
The part of Scenario Telemetry that started within the Observation Window of one scenario step.
_Avoid_: step telemetry

**Telemetry Viewer**:
The self-contained HTML page `telemetry.html` next to the Reqnroll report where the run's telemetry is viewed: Scenario Telemetry by scenario and step (a span waterfall, logs and properties on one timeline) and Unattributed Telemetry. The Reqnroll report itself only contains links to it.
_Avoid_: dashboard, telemetry report, attachment

**Unattributed Telemetry**:
SUT telemetry that could not be attributed to any scenario.
_Avoid_: orphan spans, noise
