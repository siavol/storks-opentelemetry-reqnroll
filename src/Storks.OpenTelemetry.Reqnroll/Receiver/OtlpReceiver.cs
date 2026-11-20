using System.Diagnostics;
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
/// In-process OTLP/HTTP receiver (Telemetry Source) on a dynamic loopback port, so that parallel test runs never clash
/// and nothing outside the machine can send to it.
/// </summary>
internal sealed class OtlpReceiver : IAsyncDisposable
{
    private const string SourceName = "Storks.OpenTelemetry.Reqnroll.Receiver";
    private readonly TelemetryStore _store;
    private readonly Action<string, string>? _onError;
    private WebApplication? _app;

    public OtlpReceiver(TelemetryStore store, Action<string, string>? onError = null)
    {
        _store = store;
        _onError = onError;
    }

    private void Report(string kind, string detail)
    {
        try { _onError?.Invoke(kind, detail); }
        catch { /* the error sink must never break the receiver */ }
    }

    /// <summary>Errors are reported and never stop the receiver: one bad request must not lose the rest of the run.</summary>
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
            Report("rejected invalid OTLP request", $"{path}: {ex.Message}");
            http.StatusCode = StatusCodes.Status400BadRequest;
        }
        catch (Exception ex)
        {
            Report("failed to process OTLP request", $"{path}: {ex.GetType().Name}: {ex.Message}");
            if (!http.HasStarted) http.StatusCode = StatusCodes.Status500InternalServerError;
        }
    }

    public static void StoreLogs(TelemetryStore store, ExportLogsServiceRequest export) =>
        store.AddLogs(export, requestIndex => OtlpMapper.MapLogs(export, requestIndex));

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
        // Hosting picks these up instead of its "Microsoft.AspNetCore" ones, so an In-process SUT's ASP.NET Core
        // instrumentation never sees receiver requests, which would otherwise be exported back here in a loop.
        builder.Services.AddSingleton(new DiagnosticListener(SourceName));
        builder.Services.AddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>());
        builder.Services.AddSingleton(new ActivitySource(SourceName));
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
