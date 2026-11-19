using Reqnroll.Events;
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
        // Lets Reqnroll discover the [Binding] hooks in this assembly without any manual registration.
        runtimePluginEvents.ConfigurationDefaults += (_, args) =>
            args.ReqnrollConfiguration.AdditionalStepAssemblies.Add(typeof(StorksPlugin).Assembly.FullName!);

        runtimePluginEvents.CustomizeGlobalDependencies += (_, args) =>
        {
            var store = new TelemetryStore();
            var diagnostics = new PluginDiagnostics();
            var receiver = new OtlpReceiver(store, diagnostics.Report);
            // Started here, i.e. before any hook can run.
            receiver.StartAsync().GetAwaiter().GetResult();

            var record = new RunRecord();
            var container = args.ObjectContainer;
            container.RegisterInstanceAs(store);
            container.RegisterInstanceAs(receiver);
            container.RegisterInstanceAs(diagnostics);
            container.RegisterInstanceAs(record);
            container.RegisterInstanceAs(new RunTracker(record, diagnostics));
            container.RegisterInstanceAs(RunOutput.ProcessStreams());
            container.RegisterInstanceAs<ITelemetryEndpoint>(new TelemetryEndpoint(receiver.Endpoint));
        };

        // An async listener, because output (the step link) written on StepFinishedEvent lands on the Gherkin step
        // itself; written from an AfterStep hook it would land on the hook's test step. Subscribed before
        // CucumberMessagePublisher, so it runs first; samples/Aspire/App.Specs.ReportChecks guard this order.
        runtimePluginEvents.CustomizeTestThreadDependencies += (_, args) =>
        {
            var container = args.ObjectContainer;
            container.Resolve<ITestThreadExecutionEventPublisher>()
                .AddListener(new RunEventListener(container.Resolve<RunTracker>(), container.Resolve<RunRecord>(), container.Resolve<PluginDiagnostics>()));
        };
    }
}
