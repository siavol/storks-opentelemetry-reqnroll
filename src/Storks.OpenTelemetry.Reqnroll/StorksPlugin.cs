using System.Reflection;
using Reqnroll.BoDi;
using Reqnroll.Formatters;
using Reqnroll.Formatters.Configuration;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;
using Storks.OpenTelemetry.Reqnroll;
using Storks.OpenTelemetry.Reqnroll.Core;
using Storks.OpenTelemetry.Reqnroll.Receiver;

[assembly: RuntimePlugin(typeof(StorksPlugin))]

namespace Storks.OpenTelemetry.Reqnroll;

internal sealed class StorksPlugin : IRuntimePlugin
{
    public void Initialize(
        RuntimePluginEvents runtimePluginEvents,
        RuntimePluginParameters runtimePluginParameters,
        UnitTestProviderConfiguration unitTestProviderConfiguration)
    {
        runtimePluginEvents.CustomizeGlobalDependencies += (_, args) =>
            RegisterGlobalDependencies(args.ObjectContainer);
    }

    /// <summary>
    /// Decides once, from Reqnroll's formatters configuration, whether the plugin is on. Reqnroll launches the formatters
    /// right after this point, before any hook. Off, only a switched-off <see cref="ITelemetryEndpoint"/> is registered.
    /// </summary>
    internal static void RegisterGlobalDependencies(IObjectContainer container)
    {
        container.RegisterTypeAs<TelemetryFormatter, ICucumberMessageFormatter>(TelemetryFormatter.FormatterName);
        var formatters = container.Resolve<IFormattersConfigurationProvider>();
        if (!TelemetryFormatter.IsSwitchedOn(formatters))
        {
            container.RegisterInstanceAs<ITelemetryEndpoint>(new TelemetryEndpoint(null));
            return;
        }

        var store = new TelemetryStore();
        var diagnostics = new PluginDiagnostics();
        var receiver = new OtlpReceiver(store, diagnostics.Report);
        // Started here, i.e. before any hook can run.
        receiver.StartAsync().GetAwaiter().GetResult();

        var record = new RunRecord();
        container.RegisterInstanceAs(store);
        container.RegisterInstanceAs(receiver);
        container.RegisterInstanceAs(diagnostics);
        container.RegisterInstanceAs(record);
        container.RegisterInstanceAs(new RunTracker(diagnostics));
        container.RegisterInstanceAs(RunOutput.ProcessStreams());
        container.RegisterInstanceAs<ITelemetryEndpoint>(new TelemetryEndpoint(receiver.Endpoint));
        container.RegisterInstanceAs(ReportFiles.From(formatters.GetFormatterConfigurationByName(TelemetryFormatter.FormatterName),
            path => ResolveTemplatePlaceholders?.Invoke(formatters, [path]) as string ?? path));
    }

    // Reqnroll 3.3 added placeholders such as {timestamp} to formatter paths; the plugin builds against 3.0, which has none.
    private static readonly MethodInfo? ResolveTemplatePlaceholders =
        typeof(IFormattersConfigurationProvider).GetMethod("ResolveTemplatePlaceholders", [typeof(string)]);
}
