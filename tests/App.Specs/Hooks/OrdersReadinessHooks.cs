using System.Net;
using NUnit.Framework;
using Reqnroll;

namespace App.Specs.Hooks;

/// <summary>
/// Checks before each scenario that Orders answers. Also the demo of a scenario hook with telemetry: in the Telemetry
/// Viewer its request shows up only with "Show hooks" on.
/// </summary>
[Binding]
public static class OrdersReadinessHooks
{
    [BeforeScenario]
    public static async Task EnsureOrdersAnswers()
    {
        using var client = AppHostHooks.CreateOrdersClient();
        using var response = await client.GetAsync($"/orders/{Guid.Empty}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), "orders-api is not ready");
    }
}
