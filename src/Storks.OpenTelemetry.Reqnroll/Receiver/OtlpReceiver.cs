using System.Net;
using Google.Protobuf;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using Storks.OpenTelemetry.Reqnroll.Core;

namespace Storks.OpenTelemetry.Reqnroll.Receiver;

/// <summary>
/// In-process OTLP/HTTP protobuf receiver (Telemetry Source). Listens on a dynamic loopback port.
/// Accepts logs at POST /v1/logs and traces at POST /v1/traces; other signals are acknowledged and dropped.
/// </summary>
internal sealed class OtlpReceiver : IAsyncDisposable
{
    private readonly TelemetryStore _store;
    private readonly Action<string>? _onError;
    private WebApplication? _app;

    /// <param name="store">Receives every accepted span and log.</param>
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

    /// <summary>Keeps the accepted request as received and its neutral mapping; a log's id is its position among the received requests.</summary>
    public static void StoreLogs(TelemetryStore store, ExportLogsServiceRequest export) =>
        store.AddLogs(export, requestIndex => OtlpMapper.MapLogs(export, requestIndex));

    /// <summary>Keeps the accepted request as received and its neutral mapping.</summary>
    public static void StoreTraces(TelemetryStore store, ExportTraceServiceRequest export) =>
        store.AddTraces(export, OtlpMapper.MapSpans(export));

    public Uri Endpoint { get; private set; } = null!;

    public async Task StartAsync()
    {
        // The empty builder reads no configuration (environment variables, appsettings.json, command line), so ambient
        // Kestrel endpoints, ASPNETCORE_URLS or ASPNETCORE_HTTP_PORTS of the test process cannot add listeners:
        // the receiver only ever listens on its own loopback port.
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseKestrelCore();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
        builder.Services.AddRoutingCore();
        var app = builder.Build();

        app.MapPost("/v1/logs", ctx => Handle(ctx.Request, "/v1/logs", ExportLogsServiceRequest.Parser,
            export => StoreLogs(_store, export), new ExportLogsServiceResponse()));
        app.MapPost("/v1/traces", ctx => Handle(ctx.Request, "/v1/traces", ExportTraceServiceRequest.Parser,
            export => StoreTraces(_store, export), new ExportTraceServiceResponse()));
        app.MapPost("/v1/metrics", () => Results.Bytes([], "application/x-protobuf"));

        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        Endpoint = new Uri(address); // the root path: "http://127.0.0.1:<port>/"
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
