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
    Diagnostic,
}

internal sealed record GgufCliOutput(
    GgufCliOutputKind Kind,
    string? Text = null,
    string? DiagnosticCode = null);

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
            "G1DONE" => new GgufCliOutput(
                GgufCliOutputKind.ResponseCompleted),
            _ when line.StartsWith("G1DELTA ", StringComparison.Ordinal) =>
                new GgufCliOutput(
                    GgufCliOutputKind.TextDelta,
                    GgufAdapterProtocol.DecodeContent(line[8..])),
            _ => throw new InvalidOperationException(
                "The adapter emitted an unrecognized output frame."),
        };
    }
}
