# Security Policy

## Reporting a vulnerability

Please do not report security vulnerabilities through public GitHub issues, discussions or pull requests.

Report them privately through GitHub private vulnerability reporting: open the repository's **Security** tab and click **Report a vulnerability**. See GitHub's guide on [privately reporting a security vulnerability](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability).

Include what you can:

- the affected package version;
- a description of the problem and its impact;
- steps to reproduce or a proof of concept.

You will get a reply in the advisory thread. Once a fix is released, the advisory is published with credit to the reporter unless you ask otherwise.

## Supported versions

Only the latest released version of `Storks.OpenTelemetry.Reqnroll` receives security fixes.

## Scope

The plugin is a test-time tool: during a test run it hosts an OTLP receiver on localhost and writes reports to disk. Problems in the system under test, in Reqnroll or in the OpenTelemetry SDK belong to those projects.
