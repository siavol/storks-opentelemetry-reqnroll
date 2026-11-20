using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prices.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddOpenTelemetry(logging => logging.IncludeFormattedMessage = true);
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("prices-api"))
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
    .UseOtlpExporter();

var app = builder.Build();

app.MapGet("/prices/{sku}", (string sku, ILogger<Program> logger) =>
{
    Log.PriceRequested(logger, sku);
    return Results.Ok(new { sku, price = 25.00m });
});

await app.RunAsync();
