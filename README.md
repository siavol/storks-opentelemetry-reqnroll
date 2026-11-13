# dotnet-gherkin-otel

Reqnroll-плагин, который прикладывает к отчёту телеметрию SUT (трейсы и логи по OTLP) под каждым сценарием и шагом.

Нужны .NET 11 SDK и Docker (Postgres поднимается через Aspire).

## Запуск приложения

```sh
dotnet run --project app/AppHost
```

Откроется дашборд Aspire с трейсами и логами Orders, Pricing и Postgres.

## Запуск тестов

```sh
dotnet test tests/GherkinOtel.ReqnrollPlugin.Tests   # unit-тесты плагина, без Docker
dotnet test tests/App.Sut.Tests                      # SUT через AppHost
dotnet test tests/App.Specs                          # сценарии Reqnroll; красный @demo-failure падает по замыслу
dotnet test tests/App.Specs.ReportChecks             # проверка артефактов; запускать после App.Specs
```

Без красного сценария: `dotnet test tests/App.Specs --filter "TestCategory!=demo-failure"`.

Отчёты: `tests/App.Specs/bin/Debug/net11.0/reports/` (`reqnroll_report.html`, `reqnroll_report.ndjson`, `unattributed-telemetry.txt`).

Пауза перед формированием вложений: `GHERKINOTEL_SETTLE_PAUSE_MS` (по умолчанию 250).
