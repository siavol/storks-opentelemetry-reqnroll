using Reqnroll.Formatters;
using Reqnroll.Formatters.Configuration;
using Reqnroll.Formatters.RuntimeSupport;

namespace Storks.OpenTelemetry.Reqnroll;

/// <summary>
/// The Telemetry Formatter, switched on by an <c>opentelemetry</c> entry in Reqnroll's <c>formatters</c> configuration.
/// It needs no messages yet: while it is on, the plugin's hooks do the work.
/// </summary>
internal sealed class TelemetryFormatter(IFormattersConfigurationProvider configurationProvider, IFormatterLog logger)
    : FormatterBase(configurationProvider, logger, FormatterName)
{
    /// <summary>The formatter's name, which is also its configuration key and container key: Reqnroll matches them by name.</summary>
    public const string FormatterName = "opentelemetry";

    /// <summary>Whether the plugin is on: formatters are enabled and the configuration has an <c>opentelemetry</c> entry.</summary>
    public static bool IsSwitchedOn(IFormattersConfigurationProvider configuration) =>
        configuration.Enabled && configuration.GetFormatterConfigurationByName(FormatterName) is not null;

    public override void LaunchInner(IDictionary<string, object> formatterConfigString, Action<bool> onAfterInitialization) =>
        onAfterInitialization(true);

    // Drained to the end, so that FormatterBase can close the formatter when the run finishes.
    protected override async Task ConsumeAndFormatMessagesBackgroundTask(CancellationToken cancellationToken)
    {
        await foreach (var _ in PostedMessages.Reader.ReadAllAsync(cancellationToken)) { }
    }
}
