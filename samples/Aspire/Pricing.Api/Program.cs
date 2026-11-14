using Pricing.Api;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapGet("/prices/{sku}", (string sku, ILogger<Program> logger) =>
{
    if (sku == "BROKEN-1")
    {
        Log.PricingFailed(logger, sku);
        return Results.Problem(detail: $"Pricing is broken for {sku}", statusCode: StatusCodes.Status500InternalServerError);
    }

    return Results.Ok(new PriceResponse(sku, 25.00m));
});

app.Run();

public partial class Program;
