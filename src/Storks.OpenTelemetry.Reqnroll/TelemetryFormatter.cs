using Reqnroll.BoDi;
using Reqnroll.Formatters;
using Reqnroll.Formatters.Configuration;
using Reqnroll.Formatters.RuntimeSupport;
using Storks.OpenTelemetry.Reqnroll.Core;
using Storks.OpenTelemetry.Reqnroll.Receiver;

namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>
/// The Telemetry Formatter, switched on by an <c>opentelemetry</c> entry in Reqnroll's <c>formatters</c> configuration.
/// It reads no messages yet: when the run finishes it writes the files (<see cref="RunEnd"/>) from what the plugin collected.
/// </summary>
internal sealed class TelemetryFormatter(IFormattersConfigurationProvider configurationProvider, IFormatterLog logger, IObjectContainer container)
    : FormatterBase(configurationProvider, logger, FormatterName)
{
    /// <summary>The formatter's name, which is also its configuration key and container key: Reqnroll matches them by name.</summary>
    public const string FormatterName = "opentelemetry";

    /// <summary>Whether the plugin is on: formatters are enabled and the configuration has an <c>opentelemetry</c> entry.</summary>
    public static bool IsSwitchedOn(IFormattersConfigurationProvider configuration) =>
        configuration.Enabled && configuration.GetFormatterConfigurationByName(FormatterName) is not null;

    // The paths are read from the same configuration by StorksPlugin, which registers them as ReportFiles.
    public override void LaunchInner(IDictionary<string, object> formatterConfigString, Action<bool> onAfterInitialization) =>
        onAfterInitialization(true);

    // Runs only while the plugin is on. The channel completes after TestRunFinished, which Reqnroll sends after all
    // AfterTestRun hooks and then waits for this task.
    protected override async Task ConsumeAndFormatMessagesBackgroundTask(CancellationToken cancellationToken)
    {
        await foreach (var _ in PostedMessages.Reader.ReadAllAsync(cancellationToken)) { }
        await RunEnd.Finish(container.Resolve<ReportFiles>(), container.Resolve<OtlpReceiver>(), container.Resolve<TelemetryStore>(),
            container.Resolve<RunRecord>(), container.Resolve<PluginDiagnostics>(), container.Resolve<RunTracker>(), container.Resolve<RunOutput>());
    }
}
