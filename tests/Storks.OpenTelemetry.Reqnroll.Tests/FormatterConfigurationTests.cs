using NUnit.Framework;
using Reqnroll.BoDi;
using Reqnroll.Configuration;
using Reqnroll.Formatters.Configuration;
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
        public string ResolveTemplatePlaceholders(string template) => template;
#pragma warning restore CA1822
    }

    private static ObjectContainer Container(bool enabled, params string[] formatterNames)
    {
        var container = new ObjectContainer();
        container.RegisterInstanceAs<IFormattersConfigurationProvider>(new FakeFormattersConfiguration(enabled,
            formatterNames.ToDictionary(name => name, IDictionary<string, object> (_) => new Dictionary<string, object>())));
        return container;
    }

    private static ITelemetryEndpoint Configure(ObjectContainer container, ReqnrollConfiguration? configuration = null)
    {
        StorksPlugin.RegisterGlobalDependencies(container, configuration ?? ConfigurationLoader.GetDefault());
        return container.Resolve<ITelemetryEndpoint>();
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
}
