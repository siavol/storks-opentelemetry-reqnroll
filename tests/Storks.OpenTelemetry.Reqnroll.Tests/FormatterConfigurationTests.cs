using NUnit.Framework;
using Reqnroll.BoDi;
using Reqnroll.Configuration;
using Reqnroll.Formatters.Configuration;
using Storks.OpenTelemetry.Reqnroll.Core;
using Storks.OpenTelemetry.Reqnroll.Receiver;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

/// <summary>Seam "formatters configuration → plugin state": what the plugin registers for a given Reqnroll formatters configuration.</summary>
public class FormatterConfigurationTests
{
    private sealed class FakeFormattersConfiguration(bool enabled, Dictionary<string, IDictionary<string, object>> formatters)
        : IFormattersConfigurationProvider
    {
        public bool Enabled => enabled;

        public IDictionary<string, object> GetFormatterConfigurationByName(string formatterName) =>
            formatters.GetValueOrDefault(formatterName)!;

        // Part of the interface from Reqnroll 3.3 on; on 3.0 an ordinary extra method, hence CA1822 there.
#pragma warning disable CA1822
        public string ResolveTemplatePlaceholders(string template) => template.Replace("{buildRunId}", "run-7");
#pragma warning restore CA1822
    }

    private static ObjectContainer Container(bool enabled, params string[] formatterNames) =>
        Container(enabled, formatterNames.ToDictionary(name => name, IDictionary<string, object> (_) => new Dictionary<string, object>()));

    private static ObjectContainer Container(bool enabled, Dictionary<string, IDictionary<string, object>> formatters)
    {
        var container = new ObjectContainer();
        container.RegisterInstanceAs<IFormattersConfigurationProvider>(new FakeFormattersConfiguration(enabled, formatters));
        return container;
    }

    private static ITelemetryEndpoint Configure(ObjectContainer container, ReqnrollConfiguration? configuration = null)
    {
        StorksPlugin.RegisterGlobalDependencies(container, configuration ?? ConfigurationLoader.GetDefault());
        return container.Resolve<ITelemetryEndpoint>();
    }

    private static async Task<ReportFiles> FilesFor(Dictionary<string, object> entry)
    {
        using var container = Container(enabled: true, new Dictionary<string, IDictionary<string, object>> { ["opentelemetry"] = entry });
        Configure(container);
        await using var receiver = container.Resolve<OtlpReceiver>();
        return container.Resolve<ReportFiles>();
    }

    [Test]
    public void Without_an_opentelemetry_entry_the_plugin_is_off_and_starts_no_receiver()
    {
        using var container = Container(enabled: true, "html", "message");
        var configuration = ConfigurationLoader.GetDefault();

        var endpoint = Configure(container, configuration);

        Assert.That(endpoint.IsEnabled, Is.False);
        Assert.That(endpoint.GetExporterEnvironment(), Is.Empty);
        Assert.That(container.IsRegistered<OtlpReceiver>(), Is.False);
        Assert.That(configuration.AdditionalStepAssemblies, Is.Empty, "the plugin's hooks are not bound");
    }

    [Test]
    public void With_formatters_disabled_the_entry_does_not_switch_the_plugin_on()
    {
        using var container = Container(enabled: false, "opentelemetry");

        var endpoint = Configure(container);

        Assert.That(endpoint.IsEnabled, Is.False);
        Assert.That(endpoint.GetExporterEnvironment(), Is.Empty);
        Assert.That(container.IsRegistered<OtlpReceiver>(), Is.False);
    }

    [Test]
    public async Task With_an_opentelemetry_entry_the_plugin_is_on_and_points_the_exporter_at_its_receiver()
    {
        using var container = Container(enabled: true, "opentelemetry");
        var configuration = ConfigurationLoader.GetDefault();

        var endpoint = Configure(container, configuration);
        await using var receiver = container.Resolve<OtlpReceiver>();

        Assert.That(endpoint.IsEnabled, Is.True);
        Assert.That(endpoint.Endpoint, Is.EqualTo(receiver.Endpoint));
        Assert.That(endpoint.GetExporterEnvironment()["OTEL_EXPORTER_OTLP_ENDPOINT"], Is.EqualTo(receiver.Endpoint.ToString().TrimEnd('/')));
        Assert.That(configuration.AdditionalStepAssemblies, Has.Member(typeof(StorksPlugin).Assembly.FullName));
    }

    [Test]
    public async Task Without_paths_the_viewer_is_reports_telemetry_html_and_the_jsonl_files_lie_next_to_it()
    {
        var files = await FilesFor([]);

        var reports = Path.GetFullPath("reports");
        Assert.That(files, Is.EqualTo(new ReportFiles(
            Path.Combine(reports, "telemetry.html"), Path.Combine(reports, "traces.jsonl"), Path.Combine(reports, "logs.jsonl"))));
    }

    [Test]
    public async Task OutputFilePath_moves_the_viewer_and_the_jsonl_files_with_it()
    {
        var files = await FilesFor(new() { ["outputFilePath"] = "out/run.html" });

        var output = Path.GetFullPath("out");
        Assert.That(files, Is.EqualTo(new ReportFiles(
            Path.Combine(output, "run.html"), Path.Combine(output, "traces.jsonl"), Path.Combine(output, "logs.jsonl"))));
    }

    [Test]
    public async Task TracesFilePath_and_logsFilePath_override_the_jsonl_files()
    {
        // Strings, as REQNROLL_FORMATTERS_OPENTELEMETRY=tracesFilePath=...;logsFilePath=... delivers them.
        var logs = Path.Combine(Path.GetTempPath(), "otel", "l.jsonl");
        var files = await FilesFor(new() { ["tracesFilePath"] = "raw/t.jsonl", ["logsFilePath"] = logs });

        Assert.That(files.Viewer, Is.EqualTo(Path.GetFullPath(Path.Combine("reports", "telemetry.html"))));
        Assert.That(files.Traces, Is.EqualTo(Path.GetFullPath(Path.Combine("raw", "t.jsonl"))), "relative to the working directory, as for the built-in formatters");
        Assert.That(files.Logs, Is.EqualTo(logs));
    }

    [Test]
    public async Task Paths_get_the_placeholders_of_the_built_in_formatters_where_Reqnroll_has_them()
    {
        Assume.That(typeof(IFormattersConfigurationProvider).GetMethod("ResolveTemplatePlaceholders"), Is.Not.Null,
            "placeholders came with Reqnroll 3.3");

        var files = await FilesFor(new()
        {
            ["outputFilePath"] = "{buildRunId}/t.html",
            ["tracesFilePath"] = "{buildRunId}/t.jsonl",
            ["logsFilePath"] = "{buildRunId}/l.jsonl",
        });

        var run = Path.GetFullPath("run-7");
        Assert.That(files, Is.EqualTo(new ReportFiles(Path.Combine(run, "t.html"), Path.Combine(run, "t.jsonl"), Path.Combine(run, "l.jsonl"))));
    }
}
