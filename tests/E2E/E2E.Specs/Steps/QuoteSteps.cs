using System.Net.Http.Json;
using E2E.Specs.Hooks;
using NUnit.Framework;
using Reqnroll;

namespace E2E.Specs.Steps;

[Binding]
public sealed class QuoteSteps : IDisposable
{
    private readonly HttpClient _front = SutHooks.CreateFrontClient();
    private decimal _price;

    [When("the customer asks for a quote for {string}")]
    public async Task WhenTheCustomerAsksForAQuote(string sku) =>
        _price = await _front.GetFromJsonAsync<decimal>(new Uri($"/quotes/{sku}", UriKind.Relative));

    [Then("the quoted price is {decimal}")]
    public void ThenTheQuotedPriceIs(decimal expected) => Assert.That(_price, Is.EqualTo(expected));

    public void Dispose() => _front.Dispose();
}
