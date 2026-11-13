using GherkinOtel.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(GherkinOtelPlugin))]

namespace GherkinOtel.ReqnrollPlugin;

public sealed class GherkinOtelPlugin : IRuntimePlugin
{
    public void Initialize(
        RuntimePluginEvents runtimePluginEvents,
        RuntimePluginParameters runtimePluginParameters,
        UnitTestProviderConfiguration unitTestProviderConfiguration)
    {
        // Lets Reqnroll discover the [Binding] hooks in this assembly without any manual registration.
        // Hooks (rather than plugin lifecycle events) are used because only they run in a hook context
        // in which attachments reach the Cucumber Messages report.
        runtimePluginEvents.ConfigurationDefaults += (_, args) =>
            args.ReqnrollConfiguration.AdditionalStepAssemblies.Add(typeof(GherkinOtelPlugin).Assembly.FullName!);

        runtimePluginEvents.CustomizeGlobalDependencies += (_, args) =>
        {
            var store = new TelemetryStore();
            var receiver = new OtlpReceiver(store);
            // Started here, i.e. before any hook can run.
            receiver.StartAsync().GetAwaiter().GetResult();

            var container = args.ObjectContainer;
            container.RegisterInstanceAs(store);
            container.RegisterInstanceAs(receiver);
            container.RegisterInstanceAs<ITelemetryEndpoint>(new TelemetryEndpoint(receiver.Endpoint));
            container.RegisterInstanceAs(PluginOptions.FromEnvironment());
        };
    }
}
