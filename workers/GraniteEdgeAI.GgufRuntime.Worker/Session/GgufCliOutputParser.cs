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
    internal static GgufCliOutput Parse(string line, GgufCliOutputSource source)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        if (source == GgufCliOutputSource.StandardError)
        {
            return new GgufCliOutput(
                GgufCliOutputKind.Diagnostic,
                DiagnosticCode: "cli-stderr");
        }

        return line switch
        {
            "__G1_READY__" => new GgufCliOutput(GgufCliOutputKind.Ready),
            "__G1_RESPONSE_START__" => new GgufCliOutput(
                GgufCliOutputKind.ResponseStarted),
            "__G1_RESPONSE_DONE__" => new GgufCliOutput(
                GgufCliOutputKind.ResponseCompleted),
            _ => new GgufCliOutput(GgufCliOutputKind.TextDelta, line),
        };
    }
}
