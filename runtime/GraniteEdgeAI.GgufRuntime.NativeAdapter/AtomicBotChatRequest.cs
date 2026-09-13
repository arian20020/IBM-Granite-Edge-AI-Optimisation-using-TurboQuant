using System.Text.Json;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal static class AtomicBotChatRequest
{
    private const string SystemInstruction =
        "You are Granite Edge AI, a concise general-purpose assistant. " +
        "Answer the user's question directly and accurately. " +
        "Distinguish facts from uncertainty and state when you are unsure.";

    internal static byte[] Build(
        IReadOnlyList<GgufAdapterMessage> history,
        string prompt,
        int maximumGeneratedTokens)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumGeneratedTokens);

        var messages = new List<object>(history.Count + 2)
        {
            new { role = "system", content = SystemInstruction },
        };
        messages.AddRange(history.Select(message => new
        {
            role = message.Role == GgufAdapterRole.User ? "user" : "assistant",
            content = message.Content,
        }));
        messages.Add(new { role = "user", content = prompt });
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            model = "local-granite",
            messages,
            stream = true,
            max_tokens = maximumGeneratedTokens,
            temperature = 0.2,
            seed = 42,
        });
    }
}
