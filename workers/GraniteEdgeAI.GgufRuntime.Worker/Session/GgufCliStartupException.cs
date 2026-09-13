namespace GraniteEdgeAI.GgufRuntime.Worker.Session;

internal sealed class GgufCliStartupException(string code) : Exception
{
    internal string Code { get; } = string.IsNullOrWhiteSpace(code)
        ? throw new ArgumentException("A stable failure code is required.", nameof(code))
        : code;
}
