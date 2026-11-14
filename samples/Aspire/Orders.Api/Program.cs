using Microsoft.EntityFrameworkCore;
using Orders.Api;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<OrdersDbContext>("ordersdb");
builder.Services.AddHttpClient<PricingClient>(client => client.BaseAddress = new Uri("https+http://pricing-api"));

var app = builder.Build();
app.MapDefaultEndpoints();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.EnsureCreatedAsync();
}

app.MapPost("/orders", async (CreateOrderRequest request, PricingClient pricing, OrdersDbContext db, ILogger<Program> logger) =>
{
    decimal unitPrice;
    try
    {
        unitPrice = await pricing.GetPriceAsync(request.Sku);
    }
    catch (HttpRequestException ex)
    {
        Log.PricingFailed(logger, ex, request.Sku);
        unitPrice = 0m;
    }

    var order = new Order { Sku = request.Sku, Quantity = request.Quantity, UnitPrice = unitPrice };
    db.Orders.Add(order);
    await db.SaveChangesAsync();
    return Results.Created($"/orders/{order.Id}", OrderResponse.From(order));
});

app.MapGet("/orders/{id:guid}", async (Guid id, OrdersDbContext db) =>
    await db.Orders.FindAsync(id) is { } order ? Results.Ok(OrderResponse.From(order)) : Results.NotFound());

await app.RunAsync();
