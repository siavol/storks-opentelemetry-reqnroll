using Reqnroll.BoDi;
using Reqnroll.Configuration;
using Reqnroll.Events;
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
            RegisterGlobalDependencies(args.ObjectContainer, args.ReqnrollConfiguration);

        // An async listener, because output (the step link) written on StepFinishedEvent lands on the Gherkin step
        // itself; written from an AfterStep hook it would land on the hook's test step. Subscribed before
        // CucumberMessagePublisher, so it runs first; samples/Aspire/App.Specs.ReportChecks guard this order.
        runtimePluginEvents.CustomizeTestThreadDependencies += (_, args) =>
        {
            var container = args.ObjectContainer;
            if (!container.Resolve<ITelemetryEndpoint>().IsEnabled) return;
            container.Resolve<ITestThreadExecutionEventPublisher>()
                .AddListener(new RunEventListener(container.Resolve<RunTracker>(), container.Resolve<RunRecord>(), container.Resolve<PluginDiagnostics>()));
        };
    }

    /// <summary>
    /// Decides once, from Reqnroll's formatters configuration, whether the plugin is on. Reqnroll launches the formatters
    /// right after this point, before any hook. Off, only a switched-off <see cref="ITelemetryEndpoint"/> is registered.
    /// </summary>
    internal static void RegisterGlobalDependencies(IObjectContainer container, ReqnrollConfiguration configuration)
    {
        container.RegisterTypeAs<TelemetryFormatter, ICucumberMessageFormatter>(TelemetryFormatter.FormatterName);
        if (!TelemetryFormatter.IsSwitchedOn(container.Resolve<IFormattersConfigurationProvider>()))
        {
            container.RegisterInstanceAs<ITelemetryEndpoint>(new TelemetryEndpoint(null));
            return;
        }

        // Binds the [Binding] hooks in this assembly; binding discovery reads this list after this point.
        configuration.AdditionalStepAssemblies.Add(typeof(StorksPlugin).Assembly.FullName!);

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
        container.RegisterInstanceAs(new RunTracker(record, diagnostics));
        container.RegisterInstanceAs(RunOutput.ProcessStreams());
        container.RegisterInstanceAs<ITelemetryEndpoint>(new TelemetryEndpoint(receiver.Endpoint));
    }
}
