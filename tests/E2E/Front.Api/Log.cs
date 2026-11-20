namespace Front.Api;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Price request to back-api failed for SKU {Sku} with {StatusCode}, quoting 0")]
    public static partial void BackFailed(ILogger logger, string sku, int statusCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Quoted SKU {Sku} at {Price}")]
    public static partial void Quoted(ILogger logger, string sku, decimal price);
}
