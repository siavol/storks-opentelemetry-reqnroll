using System.Diagnostics;

namespace Prices.Api;

public sealed record BasketLine(string Sku, int Quantity);

public sealed record QuoteRequest(IReadOnlyList<BasketLine> Lines);

public sealed record QuotedLine(string Sku, int Quantity, decimal UnitPrice, int DiscountPercent, decimal Total);

public sealed record Quote(IReadOnlyList<QuotedLine> Lines, decimal Total);

/// <summary>Prices a basket: catalog price per line, 10% off a line of 10 or more.</summary>
internal sealed class QuoteCalculator(ILogger<QuoteCalculator> logger)
{
    public const string SourceName = "Prices.Api";
    private const int BulkQuantity = 10;
    private const int BulkDiscountPercent = 10;
    private static readonly ActivitySource Source = new(SourceName);
    private static readonly Dictionary<string, decimal> Catalog = new() { ["SKU-1"] = 25.00m, ["SKU-2"] = 40.00m };

    /// <returns>The quote, or the first SKU that is not in the catalog.</returns>
    public (Quote? Quote, string? UnknownSku) Calculate(QuoteRequest request)
    {
        using var activity = Source.StartActivity("quote.calculate");
        activity?.SetTag("quote.lines", request.Lines.Count);
        Log.QuoteRequested(logger, request.Lines.Count);

        var lines = new List<QuotedLine>();
        foreach (var line in request.Lines)
        {
            var quoted = QuoteLine(line);
            if (quoted is null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, $"unknown SKU {line.Sku}");
                Log.QuoteRejected(logger, line.Sku);
                return (null, line.Sku);
            }
            lines.Add(quoted);
        }

        var total = lines.Sum(l => l.Total);
        Log.QuoteTotal(logger, total);
        return (new Quote(lines, total), null);
    }

    private QuotedLine? QuoteLine(BasketLine line)
    {
        using var activity = Source.StartActivity("quote.line");
        activity?.SetTag("sku", line.Sku);
        activity?.SetTag("quantity", line.Quantity);

        var unitPrice = LookUp(line.Sku);
        if (unitPrice is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            return null;
        }
        var discountPercent = ApplyDiscount(line);
        var total = unitPrice.Value * line.Quantity * (100 - discountPercent) / 100;
        return new QuotedLine(line.Sku, line.Quantity, unitPrice.Value, discountPercent, total);
    }

    private decimal? LookUp(string sku)
    {
        using var activity = Source.StartActivity("catalog.lookup");
        activity?.SetTag("sku", sku);
        if (!Catalog.TryGetValue(sku, out var price))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "not in catalog");
            Log.SkuNotInCatalog(logger, sku);
            return null;
        }
        activity?.SetTag("unit_price", (double)price);
        Log.CatalogPrice(logger, sku, price);
        return price;
    }

    private int ApplyDiscount(BasketLine line)
    {
        using var activity = Source.StartActivity("discount.apply");
        var percent = line.Quantity >= BulkQuantity ? BulkDiscountPercent : 0;
        activity?.SetTag("discount.percent", percent);
        if (percent > 0) Log.BulkDiscountApplied(logger, percent, line.Quantity, line.Sku);
        return percent;
    }
}
