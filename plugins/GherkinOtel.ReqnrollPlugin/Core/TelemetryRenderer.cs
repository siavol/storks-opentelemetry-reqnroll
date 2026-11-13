using System.Globalization;
using System.Text;

namespace GherkinOtel.ReqnrollPlugin;

public static class TelemetryRenderer
{
    public const string NoTelemetry = "нет телеметрии";

    /// <summary>Chronological text list; time is relative to the window start.</summary>
    public static string Render(ScenarioTelemetry telemetry)
    {
        if (telemetry.Logs.Count == 0) return NoTelemetry;

        var sb = new StringBuilder();
        foreach (var log in telemetry.Logs)
        {
            var rel = (log.Timestamp - telemetry.Window.Start).TotalSeconds;
            sb.Append(CultureInfo.InvariantCulture, $"+{rel:0.000}s  {log.Service}  {log.Level}  {log.Category}  {log.Message}\n");
        }
        return sb.ToString();
    }
}
