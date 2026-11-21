using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;
using Prices.Specs.Hooks;
using Reqnroll;

namespace Prices.Specs.Steps;

[Binding]
public sealed class QuoteSteps : IDisposable
{
    private readonly HttpClient _prices = SutHooks.CreateClient();
    private List<BasketLineDto> _basket = [];
    private HttpResponseMessage? _response;

    [Given("the basket has")]
    public void GivenTheBasketHas(DataTable table) =>
        _basket = [.. table.Rows.Select(row => new BasketLineDto(row["sku"], int.Parse(row["quantity"], CultureInfo.InvariantCulture)))];

    [When("the customer asks for a quote")]
    public async Task WhenTheCustomerAsksForAQuote() =>
        _response = await _prices.PostAsJsonAsync("/quotes", new { lines = _basket });

    [Then("the quote total is {decimal}")]
    public async Task ThenTheQuoteTotalIs(decimal expected)
    {
        Assert.That(_response!.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var quote = await _response.Content.ReadFromJsonAsync<QuoteDto>();
        Assert.That(quote!.Total, Is.EqualTo(expected));
    }

    [Then("the quote is rejected for {string}")]
    public async Task ThenTheQuoteIsRejectedFor(string sku)
    {
        Assert.That(_response!.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        var rejection = await _response.Content.ReadFromJsonAsync<RejectionDto>();
        Assert.That(rejection!.UnknownSku, Is.EqualTo(sku));
    }

    public void Dispose()
    {
        _response?.Dispose();
        _prices.Dispose();
    }

    private sealed record BasketLineDto(string Sku, int Quantity);

    private sealed record QuoteDto(decimal Total);

    private sealed record RejectionDto(string UnknownSku);
}
