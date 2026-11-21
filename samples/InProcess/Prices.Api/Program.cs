using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prices.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddOpenTelemetry(logging => logging.IncludeFormattedMessage = true);
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("prices-api"))
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddSource(QuoteCalculator.SourceName))
    .UseOtlpExporter();
builder.Services.AddSingleton<QuoteCalculator>();

var app = builder.Build();

app.MapPost("/quotes", (QuoteRequest request, QuoteCalculator calculator) =>
{
    var (quote, unknownSku) = calculator.Calculate(request);
    return quote is not null ? Results.Ok(quote) : Results.UnprocessableEntity(new { unknownSku });
});

await app.RunAsync();
