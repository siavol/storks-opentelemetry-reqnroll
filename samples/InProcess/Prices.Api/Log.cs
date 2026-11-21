namespace Prices.Api;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Quote requested for {Lines} lines")]
    public static partial void QuoteRequested(ILogger logger, int lines);

    [LoggerMessage(Level = LogLevel.Information, Message = "Catalog price of {Sku} is {Price}")]
    public static partial void CatalogPrice(ILogger logger, string sku, decimal price);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SKU {Sku} is not in the catalog")]
    public static partial void SkuNotInCatalog(ILogger logger, string sku);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bulk discount {Percent}% applied to {Quantity} x {Sku}")]
    public static partial void BulkDiscountApplied(ILogger logger, int percent, int quantity, string sku);

    [LoggerMessage(Level = LogLevel.Information, Message = "Quote total is {Total}")]
    public static partial void QuoteTotal(ILogger logger, decimal total);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Quote rejected: unknown SKU {Sku}")]
    public static partial void QuoteRejected(ILogger logger, string sku);
}
