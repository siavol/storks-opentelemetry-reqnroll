using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Storks.OpenTelemetry.Reqnroll;

namespace Prices.Specs.Hooks;

/// <summary>Hosts Prices.Api in the test process once per test run.</summary>
[Binding]
public static class SutHooks
{
    private static WebApplicationFactory<Program>? _factory;

    public static HttpClient CreateClient() =>
        (_factory ?? throw new InvalidOperationException("Prices.Api is not running")).CreateClient();

    [BeforeTestRun]
    public static void StartSut(ITelemetryEndpoint telemetry)
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
            web.ConfigureServices(services => services.AddOpenTelemetry().UseReqnrollExporter(telemetry)));
    }

    // Disposing the factory flushes the SUT's last telemetry before the plugin writes the report.
    [AfterTestRun]
    public static async Task StopSut()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        _factory = null;
    }
}
