namespace Orders.Api;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Pricing request failed for SKU {Sku}, saving order with price 0")]
    public static partial void PricingFailed(ILogger logger, Exception exception, string sku);
}
