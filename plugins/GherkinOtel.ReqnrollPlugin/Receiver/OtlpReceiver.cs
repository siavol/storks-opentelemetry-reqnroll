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
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace GherkinOtel.ReqnrollPlugin;

/// <summary>
/// In-process OTLP/HTTP protobuf receiver (Telemetry Source). Listens on a dynamic loopback port.
/// Accepts logs at POST /v1/logs and traces at POST /v1/traces; other signals are acknowledged and dropped.
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

    /// <summary>Shared path of every signal: read body, parse, store, respond. Errors are reported and never stop the receiver.</summary>
    private async Task Handle<TRequest, TResponse>(HttpRequest request, string path, MessageParser<TRequest> parser,
        Action<TRequest> store, TResponse response)
        where TRequest : IMessage<TRequest>
        where TResponse : IMessage
    {
        var http = request.HttpContext.Response;
        try
        {
            using var ms = new MemoryStream();
            await request.Body.CopyToAsync(ms);
            store(parser.ParseFrom(ms.ToArray()));
            http.ContentType = "application/x-protobuf";
            await http.Body.WriteAsync(response.ToByteArray());
        }
        catch (InvalidProtocolBufferException ex)
        {
            Report($"rejected invalid OTLP request to {path}: {ex.Message}");
            http.StatusCode = StatusCodes.Status400BadRequest;
        }
        catch (Exception ex)
        {
            Report($"failed to process OTLP request to {path}: {ex}");
            if (!http.HasStarted) http.StatusCode = StatusCodes.Status500InternalServerError;
        }
    }

    public Uri Endpoint { get; private set; } = null!;

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();

        app.MapPost("/v1/logs", ctx => Handle(ctx.Request, "/v1/logs", ExportLogsServiceRequest.Parser,
            export => _store.AddLogs(OtlpMapper.MapLogs(export)), new ExportLogsServiceResponse()));
        app.MapPost("/v1/traces", ctx => Handle(ctx.Request, "/v1/traces", ExportTraceServiceRequest.Parser,
            export => _store.AddSpans(OtlpMapper.MapSpans(export)), new ExportTraceServiceResponse()));
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
