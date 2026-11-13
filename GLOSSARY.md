# Gherkin OTel

Эксперимент: сбор телеметрии тестируемого приложения во время прогона Reqnroll-сценариев и прикрепление её к отчёту о тестах, чтобы по отчёту было видно, что происходило внутри приложения.

## Language

**System Under Test (SUT)**:
Приложение, поведение которого проверяют сценарии и чью телеметрию собирают.
_Avoid_: сервис, app, target

**Scenario Telemetry**:
Трейсы и логи SUT, отнесённые к конкретному сценарию (и, по возможности, шагу) тестового прогона.
_Avoid_: captured telemetry, test traces, дамп

**Telemetry Source**:
Откуда поступает телеметрия SUT: из того же процесса, что и тесты (in-process), или от отдельно запущенного приложения (out-of-process).
_Avoid_: exporter, receiver, provider

**Attribution**:
Отнесение единицы телеметрии к сценарию или шагу.
_Avoid_: correlation, matching, binding

**Observation Window**:
Интервал времени сценария или шага; телеметрия, начавшаяся в этом интервале, относится к нему.
_Avoid_: time range, timeframe, slot

**Unattributed Telemetry**:
Телеметрия SUT, которую не удалось отнести ни к одному сценарию.
_Avoid_: orphan spans, шум
