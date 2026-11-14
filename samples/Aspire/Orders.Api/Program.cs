using Microsoft.EntityFrameworkCore;

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
        logger.LogWarning(ex, "Pricing request failed for SKU {Sku}, saving order with price 0", request.Sku);
        unitPrice = 0m;
    }

    var order = new Order { Sku = request.Sku, Quantity = request.Quantity, UnitPrice = unitPrice };
    db.Orders.Add(order);
    await db.SaveChangesAsync();
    return Results.Created($"/orders/{order.Id}", OrderResponse.From(order));
});

app.MapGet("/orders/{id:guid}", async (Guid id, OrdersDbContext db) =>
    await db.Orders.FindAsync(id) is { } order ? Results.Ok(OrderResponse.From(order)) : Results.NotFound());

app.Run();

public record CreateOrderRequest(string Sku, int Quantity);

public record OrderResponse(Guid Id, string Sku, int Quantity, decimal UnitPrice, decimal Total)
{
    public static OrderResponse From(Order o) => new(o.Id, o.Sku, o.Quantity, o.UnitPrice, o.UnitPrice * o.Quantity);
}

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
}

public class PricingClient(HttpClient http)
{
    public async Task<decimal> GetPriceAsync(string sku)
    {
        var response = await http.GetFromJsonAsync<PriceDto>($"/prices/{Uri.EscapeDataString(sku)}");
        return response!.Price;
    }

    private record PriceDto(string Sku, decimal Price);
}
