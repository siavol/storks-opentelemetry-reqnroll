using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Protobuf;

namespace Storks.OpenTelemetry.Reqnroll.Core;

/// <summary>
/// Serializes an OTLP export request as a single line of OTLP/JSON (as the OTLP File Exporter writes it):
/// camelCase names, enums as numbers, 64-bit integers as strings, trace/span ids as hex instead of base64.
/// </summary>
internal static class OtlpJson
{
    private static readonly JsonFormatter Formatter = new(JsonFormatter.Settings.Default.WithFormatEnumsAsIntegers(true));

    // The only bytes fields that OTLP/JSON requires in hex (bytesValue of AnyValue stays base64).
    private static readonly HashSet<string> IdFields = ["traceId", "spanId", "parentSpanId"];

    public static string Serialize(IMessage request)
    {
        var node = JsonNode.Parse(Formatter.Format(request))!;
        HexIds(node);
        return node.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    private static void HexIds(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj.ToList())
                {
                    if (IdFields.Contains(name) && value is JsonValue v && v.TryGetValue<string>(out var base64))
                        obj[name] = Convert.ToHexStringLower(Convert.FromBase64String(base64));
                    else
                        HexIds(value);
                }
                break;
            case JsonArray array:
                foreach (var item in array) HexIds(item);
                break;
        }
    }
}
