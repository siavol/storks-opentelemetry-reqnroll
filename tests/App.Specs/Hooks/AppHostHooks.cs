using Aspire.Hosting;
using Aspire.Hosting.Testing;
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
    public static async Task StartAppHost()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>();
        _app = await builder.BuildAsync();
        await _app.StartAsync();
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("orders-api").WaitAsync(TimeSpan.FromMinutes(3));
    }

    [AfterTestRun]
    public static async Task StopAppHost()
    {
        if (_app is not null) await _app.DisposeAsync();
        _app = null;
    }
}
