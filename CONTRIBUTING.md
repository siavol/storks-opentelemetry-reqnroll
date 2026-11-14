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

The sample's projects reference the package with a regular `PackageReference`, exactly as a user's project would. `samples/Directory.Build.targets` decides what it resolves to:

- **Default (project mode).** The reference is replaced with a `ProjectReference` to `src/Storks.OpenTelemetry.Reqnroll`. This is what the commands above use: edit the plugin, rerun the sample (or debug it with F5), no packing. Restore is locked by the committed `packages.lock.json` files as usual.
- **Local package mode (`UseLocalPackage=true`).** The package is restored from the local feed of packed nupkg files, `artifacts/packages`, exactly as a user would consume it; this checks the packaging itself (the `.ReqnrollPlugin` assembly is found, the Telemetry Viewer is embedded). CI's E2E job runs the sample this way on the nupkg produced by the build job.

Run order in local package mode:

```sh
dotnet pack src/Storks.OpenTelemetry.Reqnroll -o artifacts/packages
dotnet test samples/Aspire/App.Specs -p:UseLocalPackage=true     # the red @demo-failure fails by design
dotnet test samples/Aspire/App.Specs.ReportChecks                # no flag: it only reads App.Specs' reports
```

What the mode changes:

- Packages are restored into a separate folder, `artifacts/samples-packages`, so the global NuGet cache never supplies a stale build. The plugin's copy there is deleted before every restore, so packing again at the same version is picked up too.
- The plugin version is the highest one in the feed (a floating `*-*`). To use one exact package, add `-p:StorksPackageVersion=<version>`; the feed folder can be changed with `-p:StorksLocalFeed=<dir>`.
- Restore is not locked in this mode, even with `CI=true`: the package version changes with every commit, so a committed lock file cannot describe it. The lock file is written to `obj/packages.local.lock.json` and re-evaluated on every restore, so the committed lock files are left untouched. All other dependencies are the same as in project mode, where locked restore checks them, so CI runs `dotnet restore Storks.OpenTelemetry.Reqnroll.slnx --locked-mode` (project mode) before the E2E run.
- Switching between modes triggers a fresh restore of the sample; nothing needs cleaning.

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

## README images

The README is also the package's page on nuget.org, which renders neither Mermaid nor relative image paths. So its images are PNG files in `docs/images/`, referenced by absolute `raw.githubusercontent.com` URLs. The "How it works" diagram is generated from `docs/images/how-it-works.mmd`; after changing it, regenerate the PNG with the [Mermaid CLI](https://github.com/mermaid-js/mermaid-cli) and commit both:

```sh
npx -p @mermaid-js/mermaid-cli@11.4.2 mmdc -i docs/images/how-it-works.mmd -o docs/images/how-it-works.png -s 2 -b white
```

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

- All package versions live in one file, `Directory.Packages.props` (Central Package Management). Do not put `Version` on a `PackageReference` in a csproj. Versions shared by a package family are properties there.
  - The one exception is the plugin itself: the samples' `PackageVersion` of `Storks.OpenTelemetry.Reqnroll` is declared in `samples/Directory.Build.targets`, because it exists only in local package mode, where its version is the newest package in the local feed (see [Package reference modes](#package-reference-modes)).
- Reqnroll has two properties in `Directory.Packages.props`:
  - `ReqnrollMinVersion` (3.0.0) is the oldest Reqnroll the plugin is verified on. The package depends on `[ReqnrollMinVersion, 4.0)` and the plugin compiles against that lower bound. Reqnroll 4.x is excluded until it is verified.
  - `ReqnrollVersion` is the concrete Reqnroll that the unit tests and the samples run on (they set `VersionOverride="$(ReqnrollVersion)"`). Pass `-p:ReqnrollVersion=3.x.y` to test against another Reqnroll. The lock files record the default version, so also pass `-p:RestoreLockedMode=false`, and do not commit the lock files that restore rewrites.
- The compatibility matrix runs the unit tests on the minimum and on the latest Reqnroll 3.x:

```sh
# minimum supported Reqnroll
dotnet test tests/Storks.OpenTelemetry.Reqnroll.Tests -p:ReqnrollVersion=3.0.0 -p:RestoreLockedMode=false
# latest Reqnroll 3.x (floating version)
dotnet test tests/Storks.OpenTelemetry.Reqnroll.Tests "-p:ReqnrollVersion=3.*" -p:CentralPackageFloatingVersionsEnabled=true -p:RestoreLockedMode=false
```

- If the plugin starts to need a newer Reqnroll, raise `ReqnrollMinVersion` and the version in the minimum command together, so the declared range stays true.
- `global.json` pins the .NET SDK and the `Aspire.AppHost.Sdk` MSBuild SDK. MSBuild resolves SDKs before reading properties, so when you update Aspire, change `AspireVersion` in `Directory.Packages.props` and `Aspire.AppHost.Sdk` in `global.json` together.
- Restore uses NuGet lock files (`packages.lock.json`, one per project). When the environment variable `CI` is `true` (as on GitHub Actions), restore runs in locked mode and fails if a lock file is out of date. After adding or updating a dependency, run `dotnet restore Storks.OpenTelemetry.Reqnroll.slnx` and commit the updated lock files. The Aspire AppHost SDK references the dashboard and DCP packages for the build machine's runtime (`Aspire.Dashboard.Sdk.linux-x64` and so on), so the samples' lock files are generated on linux-x64 and a locked restore of the samples fails on other platforms; CI checks the lock files on ubuntu, and its windows build restores the samples unlocked. To check them the way CI does:

```sh
dotnet restore Storks.OpenTelemetry.Reqnroll.slnx --locked-mode
```

## Pull requests

- Keep a PR to one change, and link the issue it addresses.
- Cover behavior changes with tests: unit tests for the package, the report checks for anything visible in the report.
- Use the terms from [GLOSSARY.md](GLOSSARY.md) in code, tests and docs; add a term there if you introduce one.
- CI (`.github/workflows/ci.yml`) runs on every PR, including PRs from forks, and on every push to `main`. Its jobs:
  - **Lint workflows:** `actionlint`, then `zizmor`, over `.github/`.
  - **Build** (`build.yml`, on ubuntu and windows): locked restore (on windows the samples restore unlocked, see [Dependencies](#dependencies)), `dotnet format --verify-no-changes`, build with warnings as errors, unit tests with coverage; on ubuntu also `dotnet pack`, uploaded as the `packages` artifact.
  - **E2E** (`e2e.yml`, ubuntu): the Aspire sample in local package mode on that nupkg, then the report checks. The run must end with exactly the `@demo-failure` scenario red; any other failure fails the job.
  - **Reqnroll 3.0.0 / 3.\***: the unit tests on the minimum and on the latest Reqnroll 3.x (see [Dependencies](#dependencies)).
  - A newer push to a PR cancels its outdated run.

Bugs and feature requests go through the issue templates. Security vulnerabilities go through private reporting, see [SECURITY.md](SECURITY.md).

## Versioning and releases

The project uses SemVer. The package version comes from git tags `vX.Y.Z` through [MinVer](https://github.com/adamralph/minver); nothing in the repository holds a version number. A build without a tag gets a unique prerelease version derived from the commit height since the last tag (for example `0.1.1-alpha.0.3`, or `0.0.0-alpha.0.17` before the first tag), so different commits never produce the same package version. MinVer needs the git history and tags: in CI, check out with full history (`fetch-depth: 0`).

The package (`dotnet pack src/Storks.OpenTelemetry.Reqnroll`) contains the plugin assembly `Storks.OpenTelemetry.ReqnrollPlugin.dll` for `net10.0`, the README and icon, and a symbols package (`.snupkg`) next to it; SourceLink points the PDB at the commit. With `CI=true` the build is a deterministic CI build (`ContinuousIntegrationBuild`).

To release (maintainers), tag a commit on `main` whose CI is green and push the tag:

```sh
git tag v0.1.0
git push origin v0.1.0
```

The tag starts `.github/workflows/release.yml`:

1. **Build:** the same `build.yml` as CI (ubuntu and windows), with the NuGet cache off, so the published package is restored from nuget.org only.
2. **Version matches the tag:** the packed nupkg and snupkg must carry exactly the tag's version (`v0.1.0` gives `0.1.0`); otherwise the run stops before anything is published.
3. **E2E:** the same `e2e.yml` as CI, on that nupkg.
4. **Publish to nuget.org:** runs in the `nuget` GitHub environment, which accepts only `v*` tags and waits for a maintainer's approval (Review deployments in the run). It is the only job with `id-token: write`: `NuGet/login` exchanges the OIDC token for a short-lived API key through Trusted Publishing (no stored API key; the nuget.org user is the `NUGET_USER` repository variable), and `dotnet nuget push` pushes exactly the nupkg and snupkg that passed E2E, without a rebuild.
5. **GitHub Release:** created for the tag with generated release notes and the nupkg attached. A version with a prerelease suffix (`v0.2.0-rc.1`) becomes a prerelease on nuget.org and on GitHub.

There is no `CHANGELOG.md`; the release notes are the changelog. A version on nuget.org cannot be deleted or replaced, only unlisted, so a broken release is fixed by a new version. After a release, move the entries of `src/Storks.OpenTelemetry.Reqnroll/PublicAPI.Unshipped.txt` to `PublicAPI.Shipped.txt`.

## License

By contributing you agree that your contributions are licensed under the [MIT License](LICENSE).
