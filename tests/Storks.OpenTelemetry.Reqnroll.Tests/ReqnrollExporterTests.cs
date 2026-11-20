using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using Storks.OpenTelemetry.Reqnroll.Core;
using Storks.OpenTelemetry.Reqnroll.Receiver;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

/// <summary>An In-process SUT's OpenTelemetry, set up with UseReqnrollExporter, against a real receiver.</summary>
public class ReqnrollExporterTests
{
    private const string Source = "InProcess.Sut";

    [Test]
    public async Task Spans_and_logs_of_an_in_process_SUT_reach_the_receiver()
    {
        var store = new TelemetryStore();
        await using var receiver = new OtlpReceiver(store);
        await receiver.StartAsync();

        RunSut(new TelemetryEndpoint(receiver.Endpoint), _ => { });

        AssertArrived(store);
    }

    [Test]
    public async Task Exporting_to_the_receiver_works_next_to_the_SUTs_own_UseOtlpExporter()
    {
        var store = new TelemetryStore();
        await using var receiver = new OtlpReceiver(store);
        await receiver.StartAsync();

        // The SUT's own exporter goes nowhere reachable; only the receiver's copy is checked.
        RunSut(new TelemetryEndpoint(receiver.Endpoint), otel => otel.UseOtlpExporter(
            OtlpExportProtocol.HttpProtobuf, new Uri("http://127.0.0.1:9/")));

        AssertArrived(store);
    }

    [Test]
    public void With_the_formatter_off_the_SUT_still_creates_spans_and_logs()
    {
        Assert.DoesNotThrow(() => RunSut(new TelemetryEndpoint(null), _ => { }));
    }

    private static void RunSut(ITelemetryEndpoint telemetry, Action<IOpenTelemetryBuilder> sutExporters)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var otel = services.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource(Source)).WithLogging();
        sutExporters(otel);
        otel.UseReqnrollExporter(telemetry);

        using (var provider = services.BuildServiceProvider())
        {
            provider.GetRequiredService<TracerProvider>();
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("InProcess.Sut.Orders");
            using var source = new ActivitySource(Source);
            using (source.StartActivity("GET /orders")) logger.LogInformation("order placed");
        }
    }

    private static void AssertArrived(TelemetryStore store)
    {
        var snapshot = store.Snapshot();
        Assert.That(snapshot.Spans.Select(s => s.Name), Is.EqualTo(new[] { "GET /orders" }));
        Assert.That(snapshot.Logs.Select(l => l.Message), Is.EqualTo(new[] { "order placed" }));
    }
}
