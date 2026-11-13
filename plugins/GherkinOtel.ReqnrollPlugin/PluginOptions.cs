namespace GherkinOtel.ReqnrollPlugin;

/// <summary>Plugin settings, read from environment variables (Reqnroll has no extension point in reqnroll.json).</summary>
public sealed record PluginOptions(TimeSpan SettlePause)
{
    public const string SettlePauseVariable = "GHERKINOTEL_SETTLE_PAUSE_MS";
    public static readonly TimeSpan DefaultSettlePause = TimeSpan.FromMilliseconds(250);

    public static PluginOptions FromEnvironment() =>
        new(int.TryParse(Environment.GetEnvironmentVariable(SettlePauseVariable), out var ms) && ms >= 0
            ? TimeSpan.FromMilliseconds(ms)
            : DefaultSettlePause);
}
