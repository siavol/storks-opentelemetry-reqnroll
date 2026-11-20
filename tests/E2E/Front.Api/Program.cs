using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddOpenTelemetry(logging => logging.IncludeFormattedMessage = true);
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("front-api"))
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation())
    .UseOtlpExporter();
builder.Services.AddHttpClient("back-api", client => client.BaseAddress = new Uri(builder.Configuration["BACK_API_URL"]
    ?? throw new InvalidOperationException("BACK_API_URL is not set")));

var app = builder.Build();

app.MapGet("/quotes/{sku}", async (string sku, IHttpClientFactory clients, ILogger<Program> logger) =>
{
    using var response = await clients.CreateClient("back-api").GetAsync(new Uri($"/prices/{sku}", UriKind.Relative));
    if (!response.IsSuccessStatusCode)
    {
        Log.BackFailed(logger, sku, (int)response.StatusCode);
        return Results.Ok(0m);
    }
    var price = await response.Content.ReadFromJsonAsync<decimal>();
    Log.Quoted(logger, sku, price);
    return Results.Ok(price);
});

await app.RunAsync();

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Price request to back-api failed for SKU {Sku} with {StatusCode}, quoting 0")]
    public static partial void BackFailed(ILogger logger, string sku, int statusCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Quoted SKU {Sku} at {Price}")]
    public static partial void Quoted(ILogger logger, string sku, decimal price);
}
