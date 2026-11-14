# Storks.OpenTelemetry.Reqnroll

Reqnroll-плагин, который собирает телеметрию SUT (трейсы и логи по OTLP) за прогон и показывает её по сценариям и шагам в Telemetry Viewer рядом с отчётом Reqnroll; в отчёте у каждого шага и сценария ссылка на просмотрщик.

Пакет `src/Storks.OpenTelemetry.Reqnroll` (сборка `Storks.OpenTelemetry.ReqnrollPlugin`) таргетирует `net10.0`, его unit-тесты лежат в `tests/`, а пример на Aspire — в `samples/Aspire/`. Для сборки всего репозитория нужны .NET 11 SDK и Docker (Postgres поднимается через Aspire).

## Запуск приложения

```sh
dotnet run --project samples/Aspire/AppHost
```

Откроется дашборд Aspire с трейсами и логами Orders, Pricing и Postgres.

## Запуск тестов

```sh
dotnet test tests/Storks.OpenTelemetry.Reqnroll.Tests     # unit-тесты плагина, без Docker
dotnet test samples/Aspire/App.Sut.Tests                   # SUT через AppHost
dotnet test samples/Aspire/App.Specs                       # сценарии Reqnroll; красный @demo-failure падает по замыслу
dotnet test samples/Aspire/App.Specs.ReportChecks          # проверка артефактов; запускать после App.Specs
```

Без красного сценария: `dotnet test samples/Aspire/App.Specs --filter "TestCategory!=demo-failure"`.

Отчёты: `samples/Aspire/App.Specs/bin/Debug/net11.0/reports/` (`reqnroll_report.html`, `reqnroll_report.ndjson`, `telemetry.html` — Telemetry Viewer, `traces.jsonl` и `logs.jsonl` — вся телеметрия прогона в OTLP/JSON Lines). Папку переопределяет `STORKS_OTEL_REPORT_DIR`.
