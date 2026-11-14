using Microsoft.EntityFrameworkCore;

namespace Orders.Api;

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

    private sealed record PriceDto(string Sku, decimal Price);
}
