using System.Text.Json;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal sealed record AtomicBotSseEvent(
    string? Text,
    GgufAdapterCompletionReason? CompletionReason,
    bool Done);

internal static class AtomicBotSseParser
{
    internal static AtomicBotSseEvent Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.StartsWith(':'))
        {
            return new AtomicBotSseEvent(null, null, false);
        }

        const string prefix = "data: ";
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new FormatException("An SSE data frame was required.");
        }

        string payload = line[prefix.Length..];
        if (payload.Equals("[DONE]", StringComparison.Ordinal))
        {
            return new AtomicBotSseEvent(null, null, true);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            JsonElement choices = document.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
            {
                return new AtomicBotSseEvent(null, null, false);
            }

            JsonElement choice = choices[0];
            string? text = null;
            if (choice.TryGetProperty("delta", out JsonElement delta)
                && delta.TryGetProperty("content", out JsonElement content)
                && content.ValueKind == JsonValueKind.String)
            {
                text = content.GetString();
            }

            GgufAdapterCompletionReason? reason = null;
            if (choice.TryGetProperty("finish_reason", out JsonElement finish)
                && finish.ValueKind == JsonValueKind.String)
            {
                reason = finish.GetString() switch
                {
                    "length" => GgufAdapterCompletionReason.Length,
                    "stop" => GgufAdapterCompletionReason.Stop,
                    _ => throw new FormatException("The completion reason is unsupported."),
                };
            }

            return new AtomicBotSseEvent(text, reason, false);
        }
        catch (JsonException exception)
        {
            throw new FormatException("The SSE JSON payload is invalid.", exception);
        }
    }
}
