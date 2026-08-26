using System.Text;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;

namespace GraniteEdgeAI.GgufRuntime.Worker.Session;

internal static class GgufAdapterProtocol
{
    private const string StartFrame = "G1START";
    private const string StopFrame = "G1STOP";

    internal static IReadOnlyList<string> EncodeInitialTurns(
        IReadOnlyList<GgufConversationTurn> turns)
    {
        ArgumentNullException.ThrowIfNull(turns);
        var frames = new List<string>(turns.Count + 1);
        foreach (GgufConversationTurn turn in turns)
        {
            string role = turn.Role switch
            {
                GgufConversationRole.User => "U",
                GgufConversationRole.Assistant => "A",
                _ => throw new ArgumentOutOfRangeException(nameof(turns)),
            };
            frames.Add($"G1TURN {role} {Encode(turn.Content)}");
        }

        frames.Add(StartFrame);
        return frames;
    }

    internal static string EncodePrompt(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        return $"G1PROMPT {Encode(content)}";
    }

    internal static string EncodeStop() => StopFrame;

    internal static string DecodeContent(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        try
        {
            byte[] bytes = Convert.FromBase64String(content);
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (Exception exception) when (
            exception is FormatException or DecoderFallbackException)
        {
            throw new InvalidOperationException(
                "The adapter emitted an invalid content frame.",
                exception);
        }
    }

    private static string Encode(string content) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
}
