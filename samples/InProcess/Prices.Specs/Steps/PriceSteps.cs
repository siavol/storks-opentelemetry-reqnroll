using System.Net.Http.Json;
using NUnit.Framework;
using Prices.Specs.Hooks;
using Reqnroll;

namespace Prices.Specs.Steps;

[Binding]
public sealed class PriceSteps : IDisposable
{
    private readonly HttpClient _prices = SutHooks.CreateClient();
    private PriceDto? _price;

    [When("the customer asks for the price of {string}")]
    public async Task WhenTheCustomerAsksForThePriceOf(string sku) =>
        _price = await _prices.GetFromJsonAsync<PriceDto>($"/prices/{sku}");

    [Then("the price is {decimal}")]
    public void ThenThePriceIs(decimal expected) =>
        Assert.That(_price!.Price, Is.EqualTo(expected));

    public void Dispose() => _prices.Dispose();

    private sealed record PriceDto(string Sku, decimal Price);
}
