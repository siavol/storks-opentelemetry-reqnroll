var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapGet("/prices/{sku}", (string sku, ILogger<Program> logger) =>
{
    if (sku == "BROKEN-1")
    {
        logger.LogError("Pricing failed for SKU {Sku}", sku);
        return Results.Problem(detail: $"Pricing is broken for {sku}", statusCode: StatusCodes.Status500InternalServerError);
    }

    return Results.Ok(new PriceResponse(sku, 25.00m));
});

app.Run();

public record PriceResponse(string Sku, decimal Price);

public partial class Program;
