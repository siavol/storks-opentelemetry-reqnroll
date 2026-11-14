using System.Net;
using System.Net.Http.Json;
using App.Specs.Hooks;
using NUnit.Framework;
using Reqnroll;

namespace App.Specs.Steps;

[Binding]
public sealed class CheckoutSteps : IDisposable
{
    private readonly HttpClient _orders = AppHostHooks.CreateOrdersClient();
    private OrderDto? _order;

    [When("the customer orders {int} of {string}")]
    public async Task WhenTheCustomerOrders(int quantity, string sku)
    {
        var response = await _orders.PostAsJsonAsync("/orders", new { sku, quantity });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        _order = await response.Content.ReadFromJsonAsync<OrderDto>();
    }

    [Then("the order total is {decimal}")]
    public void ThenTheOrderTotalIs(decimal expected) =>
        Assert.That(_order!.Total, Is.EqualTo(expected));

    public void Dispose() => _orders.Dispose();

    private sealed record OrderDto(Guid Id, string Sku, int Quantity, decimal UnitPrice, decimal Total);
}
