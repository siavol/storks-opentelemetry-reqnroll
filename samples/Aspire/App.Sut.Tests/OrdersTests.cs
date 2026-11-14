using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using NUnit.Framework;

namespace App.Sut.Tests;

[TestFixture]
public class OrdersTests
{
    private DistributedApplication _app = null!;
    private HttpClient _orders = null!;

    [OneTimeSetUp]
    public async Task StartAppHost()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>();
        _app = await builder.BuildAsync();
        await _app.StartAsync();
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("orders-api").WaitAsync(TimeSpan.FromMinutes(3));
        _orders = _app.CreateHttpClient("orders-api");
    }

    [OneTimeTearDown]
    public async Task StopAppHost()
    {
        _orders?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
    }

    [Test]
    public async Task Order_for_regular_sku_gets_price_from_pricing_and_can_be_read_back()
    {
        var created = await _orders.PostAsJsonAsync("/orders", new { sku = "SKU-1", quantity = 2 });
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var order = await created.Content.ReadFromJsonAsync<OrderDto>();
        Assert.That(order!.UnitPrice, Is.EqualTo(25.00m));
        Assert.That(order.Total, Is.EqualTo(50.00m));

        var read = await _orders.GetFromJsonAsync<OrderDto>($"/orders/{order.Id}");
        Assert.That(read, Is.EqualTo(order));
    }

    [Test]
    public async Task Order_for_broken_sku_is_saved_with_zero_price_when_pricing_fails()
    {
        var created = await _orders.PostAsJsonAsync("/orders", new { sku = "BROKEN-1", quantity = 2 });
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var order = await created.Content.ReadFromJsonAsync<OrderDto>();
        Assert.That(order!.UnitPrice, Is.EqualTo(0m));
        Assert.That(order.Total, Is.EqualTo(0m));

        var read = await _orders.GetFromJsonAsync<OrderDto>($"/orders/{order.Id}");
        Assert.That(read, Is.EqualTo(order));
    }

    private record OrderDto(Guid Id, string Sku, int Quantity, decimal UnitPrice, decimal Total);
}
