# Contributing

Thanks for helping with `Storks.OpenTelemetry.Reqnroll`. This guide covers how to build and test the repository locally, so that your PR passes CI the first time.

Terms such as Observation Window, Attribution and Telemetry Viewer are defined in [GLOSSARY.md](GLOSSARY.md); design decisions live in [docs/adr/](docs/adr/).

## Requirements

- **.NET 11 SDK.** The package targets `net10.0`; the tests and samples target `net11.0`, so the whole repository needs the .NET 11 SDK. The exact SDK version is pinned in `global.json`.
- **Docker.** Only for the Aspire sample: Aspire starts Postgres in a container. The plugin's unit tests do not need Docker.

## Repository layout

| Path | Contents |
| --- | --- |
| `src/Storks.OpenTelemetry.Reqnroll/` | The package (assembly `Storks.OpenTelemetry.ReqnrollPlugin`, `net10.0`). |
| `tests/Storks.OpenTelemetry.Reqnroll.Tests/` | Unit tests of the package. No Docker. |
| `samples/Aspire/` | The Aspire sample: AppHost, Orders.Api, Pricing.Api, ServiceDefaults, SUT tests (`App.Sut.Tests`), Reqnroll scenarios (`App.Specs`) and report checks (`App.Specs.ReportChecks`). |

## Build

```sh
dotnet build Storks.OpenTelemetry.Reqnroll.slnx
```

## Unit tests

```sh
dotnet test tests/Storks.OpenTelemetry.Reqnroll.Tests
```

These are fast, need no Docker, and are what most changes to `src/` should be covered by.

## The Aspire sample

The sample is the end-to-end check: a real SUT exports traces and logs over OTLP to the plugin's receiver during a Reqnroll run. It needs Docker.

Run the application on its own:

```sh
dotnet run --project samples/Aspire/AppHost
```

The Aspire dashboard opens with traces and logs from Orders, Pricing and Postgres.

Run the sample's tests:

```sh
dotnet test samples/Aspire/App.Sut.Tests                   # SUT through the AppHost
dotnet test samples/Aspire/App.Specs                       # Reqnroll scenarios; the red @demo-failure fails by design
dotnet test samples/Aspire/App.Specs.ReportChecks          # report checks; run after App.Specs
```

`App.Specs` contains a scenario tagged `@demo-failure` that fails on purpose, so that the report shows a red scenario next to its telemetry. A run of `App.Specs` therefore ends with one failed test. To run without it:

```sh
dotnet test samples/Aspire/App.Specs --filter "TestCategory!=demo-failure"
```

### Package reference modes

The sample's projects reference the package with a regular `PackageReference`. A shared targets file decides what it resolves to:

- **Default (project mode).** The reference is replaced with a `ProjectReference` to `src/Storks.OpenTelemetry.Reqnroll`. This is what the commands above use: edit the plugin, rerun the sample, no packing.
- **Local package mode (`UseLocalPackage=true`).** The package is restored from a local feed of built nupkg files, exactly as a user would consume it. The sample uses a separate restore folder in this mode, so a stale build in the global NuGet cache is never picked up. CI's E2E job runs the sample this way on the nupkg produced by the build job.

```sh
dotnet pack src/Storks.OpenTelemetry.Reqnroll -o artifacts/packages
dotnet test samples/Aspire/App.Specs -p:UseLocalPackage=true
dotnet test samples/Aspire/App.Specs.ReportChecks
```

## Checking the report

After `App.Specs` the reports are in `samples/Aspire/App.Specs/bin/Debug/net11.0/reports/`:

| File | What it is |
| --- | --- |
| `reqnroll_report.html` | The Reqnroll HTML report; every scenario and step links to the Telemetry Viewer. |
| `reqnroll_report.ndjson` | The Cucumber Messages stream behind the report. |
| `telemetry.html` | The Telemetry Viewer: the run's traces and logs by scenario and step. |
| `traces.jsonl`, `logs.jsonl` | All of the run's telemetry in OTLP/JSON Lines. |

The environment variable `STORKS_OTEL_REPORT_DIR` overrides the folder.

`App.Specs.ReportChecks` verifies these artifacts automatically (the report, the OTLP/JSON files and the viewer index). Still, when you change the viewer or Attribution, open `reqnroll_report.html` in a browser, follow a step's link and check that `telemetry.html` shows the expected spans and logs.

## Style and analyzers

Formatting and analyzer rules are enforced by the build, so review is not spent on style:

- `.editorconfig` holds the style rules; `EnforceCodeStyleInBuild` and `AnalysisLevel=latest-recommended` (set for every project in `Directory.Build.props`) turn them and the .NET analyzers into build diagnostics.
- CI builds with warnings as errors. Fix warnings instead of suppressing them; a targeted suppression needs a comment explaining why. The existing ones are in `.editorconfig` (test projects: `CA1707` for sentence-style test names, `CA1861` for inline expected values) and `samples/Aspire/AppHost/AppHost.csproj` (`ASPIRE010`).
- The public API is only `ITelemetryEndpoint`; every other type in the package is `internal` (the unit tests see them through `InternalsVisibleTo`, and Reqnroll discovers internal `[Binding]` classes, so hook classes stay internal too). The contract is tracked by `Microsoft.CodeAnalysis.PublicApiAnalyzers` in `src/Storks.OpenTelemetry.Reqnroll/PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`: adding, changing or removing a public member without updating these files fails the build (`RS0016`/`RS0017`). New members go to `PublicAPI.Unshipped.txt`; on release they move to `PublicAPI.Shipped.txt`. The package project generates its XML documentation file, so an undocumented public member fails the build too (`CS1591`).

Before opening a PR, run what CI runs:

```sh
dotnet restore Storks.OpenTelemetry.Reqnroll.slnx --locked-mode
dotnet format Storks.OpenTelemetry.Reqnroll.slnx --verify-no-changes --no-restore
dotnet build Storks.OpenTelemetry.Reqnroll.slnx -warnaserror --no-restore
dotnet test tests/Storks.OpenTelemetry.Reqnroll.Tests --no-build
```

`dotnet format Storks.OpenTelemetry.Reqnroll.slnx` without `--verify-no-changes` fixes formatting in place.

### Dependencies

- All package versions live in one file, `Directory.Packages.props` (Central Package Management). Do not put `Version` on a `PackageReference` in a csproj. Versions shared by a package family are properties there; the Reqnroll version is `ReqnrollVersion`, and `-p:ReqnrollVersion=3.x.y` builds against another Reqnroll (the lock files record the default version, so add `-p:RestoreLockedMode=false` and do not commit the lock files that restore rewrites).
- `global.json` pins the .NET SDK and the `Aspire.AppHost.Sdk` MSBuild SDK. MSBuild resolves SDKs before reading properties, so when you update Aspire, change `AspireVersion` in `Directory.Packages.props` and `Aspire.AppHost.Sdk` in `global.json` together.
- Restore uses NuGet lock files (`packages.lock.json`, one per project). When the environment variable `CI` is `true` (as on GitHub Actions), restore runs in locked mode and fails if a lock file is out of date. After adding or updating a dependency, run `dotnet restore Storks.OpenTelemetry.Reqnroll.slnx` and commit the updated lock files. To check them the way CI does:

```sh
dotnet restore Storks.OpenTelemetry.Reqnroll.slnx --locked-mode
```

## Pull requests

- Keep a PR to one change, and link the issue it addresses.
- Cover behavior changes with tests: unit tests for the package, the report checks for anything visible in the report.
- Use the terms from [GLOSSARY.md](GLOSSARY.md) in code, tests and docs; add a term there if you introduce one.
- CI runs on every PR, including PRs from forks: build on ubuntu and windows, unit tests, compatibility with the minimum and latest Reqnroll 3.x, the Aspire sample E2E and workflow linters. SonarCloud is skipped for forks.

Bugs and feature requests go through the issue templates. Security vulnerabilities go through private reporting, see [SECURITY.md](SECURITY.md).

## Versioning and releases

The project uses SemVer. The version comes from git tags `vX.Y.Z` through MinVer; nothing in the repository holds a version number. A build without a tag gets a unique prerelease version derived from the commit height, so a locally cached package never masks a fresh build.

To release (maintainers):

```sh
git tag v0.1.0
git push origin v0.1.0
```

The tag starts `release.yml`: build, E2E on the built nupkg, then the publish job waits for approval in the `nuget` GitHub environment. After approval it pushes exactly the nupkg and snupkg that passed E2E to nuget.org through Trusted Publishing (no stored API key) and creates a GitHub Release with generated release notes. There is no `CHANGELOG.md`; the release notes are the changelog.

## License

By contributing you agree that your contributions are licensed under the [MIT License](LICENSE).
