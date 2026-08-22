namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

/// <summary>
/// Resolves the published feasibility executable supplied by CI or a local
/// integration-test command.
/// </summary>
public static class PublishedProbeLocation
{
    public const string EnvironmentVariable =
        "LLAMASHARP_SPIKE_PUBLISH_DIR";

    public const string ExecutableFileName =
        "GraniteEdgeAI.ModelInspection.LlamaSharpSpike.exe";

    public static string RequireFromEnvironment()
    {
        string? path = Environment.GetEnvironmentVariable(
            EnvironmentVariable);

        if (string.IsNullOrWhiteSpace(path) ||
            !Directory.Exists(path))
        {
            throw new InvalidOperationException(
                $"{EnvironmentVariable} must point to a published spike directory.");
        }

        string fullPath = Path.GetFullPath(path);
        string executablePath = Path.Combine(
            fullPath,
            ExecutableFileName);

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException(
                "Published feasibility executable was not found.",
                executablePath);
        }

        return fullPath;
    }
}
