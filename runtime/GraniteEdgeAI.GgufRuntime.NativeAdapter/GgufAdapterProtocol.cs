using System.Text;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal enum GgufAdapterCommandType
{
    Turn,
    Start,
    Prompt,
    Stop,
}

internal enum GgufAdapterRole
{
    User,
    Assistant,
}

internal sealed record GgufAdapterMessage(GgufAdapterRole Role, string Content);

internal sealed record GgufAdapterCommand(
    GgufAdapterCommandType Type,
    GgufAdapterMessage? Message = null,
    string? Content = null);

internal static class GgufAdapterProtocol
{
    private const int MaximumFrameCharacters = 400_000;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static GgufAdapterCommand Parse(string frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Length == 0 || frame.Length > MaximumFrameCharacters)
        {
            throw new InvalidOperationException("The adapter frame is invalid.");
        }

        if (frame.Equals("G1START", StringComparison.Ordinal))
        {
            return new GgufAdapterCommand(GgufAdapterCommandType.Start);
        }

        if (frame.Equals("G1STOP", StringComparison.Ordinal))
        {
            return new GgufAdapterCommand(GgufAdapterCommandType.Stop);
        }

        if (frame.StartsWith("G1PROMPT ", StringComparison.Ordinal))
        {
            return new GgufAdapterCommand(
                GgufAdapterCommandType.Prompt,
                Content: Decode(frame[9..]));
        }

        if (frame.StartsWith("G1TURN ", StringComparison.Ordinal))
        {
            string[] parts = frame.Split(' ', 3, StringSplitOptions.None);
            if (parts.Length != 3)
            {
                throw new InvalidOperationException("The adapter turn is invalid.");
            }

            GgufAdapterRole role = parts[1] switch
            {
                "U" => GgufAdapterRole.User,
                "A" => GgufAdapterRole.Assistant,
                _ => throw new InvalidOperationException("The adapter role is invalid."),
            };
            return new GgufAdapterCommand(
                GgufAdapterCommandType.Turn,
                new GgufAdapterMessage(role, Decode(parts[2])));
        }

        throw new InvalidOperationException("The adapter command is invalid.");
    }

    internal static string EncodeDelta(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return $"G1DELTA {Convert.ToBase64String(Encoding.UTF8.GetBytes(content))}";
    }

    private static string Decode(string value)
    {
        try
        {
            return StrictUtf8.GetString(Convert.FromBase64String(value));
        }
        catch (Exception exception) when (
            exception is FormatException or DecoderFallbackException)
        {
            throw new InvalidOperationException(
                "The adapter content is invalid.",
                exception);
        }
    }
}
