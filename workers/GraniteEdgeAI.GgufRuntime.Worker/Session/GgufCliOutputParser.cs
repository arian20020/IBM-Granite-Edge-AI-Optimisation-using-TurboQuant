using GraniteEdgeAI.GgufRuntime.Contracts.Events;

namespace GraniteEdgeAI.GgufRuntime.Worker.Session;

internal enum GgufCliOutputSource
{
    StandardOutput,
    StandardError,
}

internal enum GgufCliOutputKind
{
    Ready,
    ResponseStarted,
    TextDelta,
    ResponseCompleted,
    Failure,
    Diagnostic,
}

internal sealed record GgufCliOutput(
    GgufCliOutputKind Kind,
    string? Text = null,
    string? DiagnosticCode = null,
    GgufCompletionReason? CompletionReason = null,
    string? FailureCode = null);

internal static class GgufCliOutputParser
{
    private const int MaximumFrameCharacters = 400_000;

    internal static GgufCliOutput Parse(string line, GgufCliOutputSource source)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        if (line.Length == 0 || line.Length > MaximumFrameCharacters)
        {
            throw new InvalidOperationException(
                "The adapter emitted an invalid output frame.");
        }

        if (source == GgufCliOutputSource.StandardError)
        {
            return new GgufCliOutput(
                GgufCliOutputKind.Diagnostic,
                DiagnosticCode: "cli-stderr");
        }

        return line switch
        {
            "G1READY" => new GgufCliOutput(GgufCliOutputKind.Ready),
            "G1RESPONSE" => new GgufCliOutput(
                GgufCliOutputKind.ResponseStarted),
            "G1DONE stop" => new GgufCliOutput(
                GgufCliOutputKind.ResponseCompleted,
                CompletionReason: GgufCompletionReason.Stop),
            "G1DONE length" => new GgufCliOutput(
                GgufCliOutputKind.ResponseCompleted,
                CompletionReason: GgufCompletionReason.Length),
            "G1FAIL chat-template-unsupported" => Failure(
                "chat-template-unsupported"),
            "G1FAIL model-load-failed" => Failure("model-load-failed"),
            "G1FAIL turboquant-runtime-required" => Failure(
                "turboquant-runtime-required"),
            "G1FAIL protocol-violation" => Failure("protocol-violation"),
            _ when line.StartsWith("G1DELTA ", StringComparison.Ordinal) =>
                new GgufCliOutput(
                    GgufCliOutputKind.TextDelta,
                    GgufAdapterProtocol.DecodeContent(line[8..])),
            _ => throw new InvalidOperationException(
                "The adapter emitted an unrecognized output frame."),
        };
    }

    private static GgufCliOutput Failure(string code) =>
        new(GgufCliOutputKind.Failure, FailureCode: code);
}
