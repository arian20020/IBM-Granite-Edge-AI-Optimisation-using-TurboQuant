namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

/// <summary>
/// Locates repository fixtures for trusted integration tests without embedding
/// a user-specific checkout path.
/// </summary>
public static class RepositoryRootLocator
{
    public static string Find()
    {
        string? workspace = Environment.GetEnvironmentVariable(
            "GITHUB_WORKSPACE");

        if (!string.IsNullOrWhiteSpace(workspace) &&
            IsRepositoryRoot(workspace))
        {
            return Path.GetFullPath(workspace);
        }

        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (IsRepositoryRoot(current.FullName))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Repository root could not be located for integration fixtures.");
    }

    private static bool IsRepositoryRoot(string path)
    {
        return File.Exists(Path.Combine(path, "global.json")) &&
               Directory.Exists(Path.Combine(path, "tools")) &&
               Directory.Exists(Path.Combine(path, "tests", "TestFixtures"));
    }
}
