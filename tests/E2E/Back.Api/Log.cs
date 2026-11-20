namespace Back.Api;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Price requested for SKU {Sku}")]
    public static partial void PriceRequested(ILogger logger, string sku);
}
