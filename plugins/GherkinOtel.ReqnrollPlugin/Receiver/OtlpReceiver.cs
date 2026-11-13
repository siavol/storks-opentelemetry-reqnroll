using Google.Protobuf;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Proto.Collector.Logs.V1;

namespace GherkinOtel.ReqnrollPlugin;

/// <summary>
/// In-process OTLP/HTTP protobuf receiver (Telemetry Source). Listens on a dynamic loopback port.
/// Currently accepts logs at POST /v1/logs; other signals are acknowledged and dropped.
/// </summary>
public sealed class OtlpReceiver : IAsyncDisposable
{
    private readonly TelemetryStore _store;
    private readonly Action<string>? _onError;
    private WebApplication? _app;

    /// <param name="onError">Receives a description of every rejected or failed request (e.g. to write it to test output).</param>
    public OtlpReceiver(TelemetryStore store, Action<string>? onError = null)
    {
        _store = store;
        _onError = onError;
    }

    private void Report(string message)
    {
        try { _onError?.Invoke(message); }
        catch { /* the error sink must never break the receiver */ }
    }

    public Uri Endpoint { get; private set; } = null!;

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();

        app.MapPost("/v1/logs", async (HttpRequest request) =>
        {
            try
            {
                using var ms = new MemoryStream();
                await request.Body.CopyToAsync(ms);
                var export = ExportLogsServiceRequest.Parser.ParseFrom(ms.ToArray());
                _store.AddLogs(OtlpMapper.MapLogs(export));
                return Results.Bytes(new ExportLogsServiceResponse().ToByteArray(), "application/x-protobuf");
            }
            catch (InvalidProtocolBufferException ex)
            {
                Report($"rejected invalid OTLP request to /v1/logs: {ex.Message}");
                return Results.BadRequest();
            }
            catch (Exception ex)
            {
                Report($"failed to process OTLP request to /v1/logs: {ex}");
                return Results.StatusCode(500);
            }
        });
        app.MapPost("/v1/traces", () => Results.Bytes([], "application/x-protobuf"));
        app.MapPost("/v1/metrics", () => Results.Bytes([], "application/x-protobuf"));

        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        Endpoint = new Uri(address.EndsWith('/') ? address : address + "/");
        _app = app;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is null) return;
        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;
    }
}
