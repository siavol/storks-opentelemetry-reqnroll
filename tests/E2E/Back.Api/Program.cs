using Back.Api;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddOpenTelemetry(logging => logging.IncludeFormattedMessage = true);
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("back-api"))
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
    .UseOtlpExporter();

var app = builder.Build();

app.MapGet("/prices/{sku}", (string sku, ILogger<Program> logger) =>
{
    Log.PriceRequested(logger, sku);
    // The SKU of the expected red scenario.
    return sku == "BROKEN-1" ? Results.StatusCode(StatusCodes.Status500InternalServerError) : Results.Ok(25.00m);
});

await app.RunAsync();
