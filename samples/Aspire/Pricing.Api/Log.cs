namespace Pricing.Api;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Pricing failed for SKU {Sku}")]
    public static partial void PricingFailed(ILogger logger, string sku);
}
