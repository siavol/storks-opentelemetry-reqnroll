using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Reqnroll;
using Storks.OpenTelemetry.Reqnroll;

namespace E2E.Specs.Hooks;

/// <summary>Starts Back.Api and Front.Api as separate processes for the test run, with their telemetry sent to the plugin.</summary>
[Binding]
public static class SutHooks
{
    private static readonly List<Process> Processes = [];
    private static Uri? _frontUrl;

    public static HttpClient CreateFrontClient() =>
        new() { BaseAddress = _frontUrl ?? throw new InvalidOperationException("Front.Api is not running") };

    [BeforeTestRun]
    public static async Task StartSut(ITelemetryEndpoint telemetry)
    {
        var backUrl = FreeLocalUrl();
        _frontUrl = FreeLocalUrl();
        await Start("Back.Api", backUrl, telemetry, []);
        await Start("Front.Api", _frontUrl, telemetry, new() { ["BACK_API_URL"] = backUrl.ToString() });
    }

    [AfterTestRun]
    public static void StopSut()
    {
        foreach (var process in Enumerable.Reverse(Processes))
        {
            // SIGTERM lets the SUT shut down and flush its telemetry; Windows has no such signal, so the SUT is killed there.
            if (OperatingSystem.IsWindows()) process.Kill();
            else Process.Start("kill", ["-TERM", process.Id.ToString(CultureInfo.InvariantCulture)])?.WaitForExit();
            if (!process.WaitForExit(TimeSpan.FromSeconds(10))) process.Kill();
            process.Dispose();
        }
        Processes.Clear();
    }

    private static async Task Start(string name, Uri url, ITelemetryEndpoint telemetry, Dictionary<string, string> environment)
    {
        var start = new ProcessStartInfo("dotnet", [Path.Combine(AppContext.BaseDirectory, $"{name}.dll")])
        {
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
        };
        start.Environment["ASPNETCORE_URLS"] = url.ToString();
        foreach (var (key, value) in telemetry.GetExporterEnvironment().Concat(environment)) start.Environment[key] = value;
        var process = Process.Start(start) ?? throw new InvalidOperationException($"{name} did not start");
        Processes.Add(process);
        await WaitUntilListening(name, url, process);
    }

    private static async Task WaitUntilListening(string name, Uri url, Process process)
    {
        using var client = new HttpClient { BaseAddress = url };
        var deadline = DateTimeOffset.UtcNow.AddMinutes(1);
        while (true)
        {
            try
            {
                // Any answer means it listens: there is no route at the root.
                using var response = await client.GetAsync(new Uri("/", UriKind.Relative));
                return;
            }
            catch (HttpRequestException) when (!process.HasExited && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(200);
            }
        }
    }

    private static Uri FreeLocalUrl()
    {
        // shortcut: the port is free when probed, not reserved; another process may take it before the SUT starts.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}");
    }
}
