using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Storks.OpenTelemetry.Reqnroll;
using Reqnroll;

namespace App.Specs.Hooks;

/// <summary>Starts the AppHost once per test run and stops it at the end of the run.</summary>
[Binding]
public static class AppHostHooks
{
    private static DistributedApplication? _app;

    public static HttpClient CreateOrdersClient() =>
        (_app ?? throw new InvalidOperationException("AppHost is not running")).CreateHttpClient("orders-api");

    [BeforeTestRun]
    public static async Task StartAppHost(ITelemetryEndpoint telemetry)
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>();
        foreach (var name in new[] { "orders-api", "pricing-api" })
        {
            builder.CreateResourceBuilder<ProjectResource>(name)
                .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", telemetry.Endpoint.ToString().TrimEnd('/'))
                .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf")
                .WithEnvironment("OTEL_BLRP_SCHEDULE_DELAY", "100")
                .WithEnvironment("OTEL_BSP_SCHEDULE_DELAY", "100");
        }
        _app = await builder.BuildAsync();
        await _app.StartAsync();
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("orders-api").WaitAsync(TimeSpan.FromMinutes(3));
        await WarmUpOrders();
    }

    /// <summary>
    /// Aspire reports orders-api healthy before it has finished starting (it still waits for Postgres and creates
    /// the schema). Poll a cheap DB-touching request until Orders really answers, then give the exporters time to
    /// flush, so that startup telemetry is not captured by the first scenario's window.
    /// </summary>
    private static async Task WarmUpOrders()
    {
        using var client = CreateOrdersClient();
        var deadline = DateTimeOffset.UtcNow.AddMinutes(3);
        while (true)
        {
            try
            {
                using var response = await client.GetAsync($"/orders/{Guid.NewGuid()}");
                if ((int)response.StatusCode < 500) break;
            }
            catch (HttpRequestException) { }
            if (DateTimeOffset.UtcNow >= deadline) throw new TimeoutException("orders-api did not answer the warm-up request");
            await Task.Delay(500);
        }
        await Task.Delay(TimeSpan.FromSeconds(1)); // exporters flush every 100 ms
    }

    [AfterTestRun]
    public static async Task StopAppHost()
    {
        if (_app is not null) await _app.DisposeAsync();
        _app = null;
    }
}
