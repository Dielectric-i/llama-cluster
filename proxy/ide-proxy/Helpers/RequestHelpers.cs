using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Slowrig.IdeProxy.Helpers;

public static class RequestHelpers
{
    public static string GenerateRequestId()
        => Guid.NewGuid().ToString("N")[..12];

    public static async Task<byte[]> ReadBodyAsync(HttpContext context)
    {
        using var ms = new MemoryStream();
        await context.Request.Body.CopyToAsync(ms);
        return ms.ToArray();
    }

    public static (string model, bool stream, int messagesCount, int toolsCount, int? maxTokens)
        ExtractChatMetadata(string bodyString, ILogger logger, string requestId)
    {
        try
        {
            using var doc = JsonDocument.Parse(bodyString);
            var root = doc.RootElement;

            var model = root.TryGetProperty("model", out var modelProp)
                ? modelProp.GetString() ?? "unknown"
                : "unknown";

            var stream = root.TryGetProperty("stream", out var streamProp)
                && streamProp.GetBoolean();

            var messagesCount = root.TryGetProperty("messages", out var msgProp)
                && msgProp.ValueKind == JsonValueKind.Array
                ? msgProp.GetArrayLength()
                : 0;

            var toolsCount = root.TryGetProperty("tools", out var toolsProp)
                && toolsProp.ValueKind == JsonValueKind.Array
                ? toolsProp.GetArrayLength()
                : 0;

            int? maxTokens = null;
            if (root.TryGetProperty("max_tokens", out var mtProp))
                maxTokens = mtProp.GetInt32();

            return (model, stream, messagesCount, toolsCount, maxTokens);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "{RequestId} failed_to_parse_request_body", requestId);
            return ("unknown", false, 0, 0, null);
        }
    }
}
