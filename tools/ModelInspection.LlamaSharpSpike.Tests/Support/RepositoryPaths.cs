namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.Support;

/// <summary>
/// Locates repository-owned source and project files from test output folders.
/// </summary>
internal static class RepositoryPaths
{
    internal static string FindRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);

        while (current is not null)
        {
            bool hasGlobalJson = File.Exists(
                Path.Combine(current.FullName, "global.json"));
            bool hasTools = Directory.Exists(
                Path.Combine(current.FullName, "tools"));
            bool hasApplication = Directory.Exists(
                Path.Combine(
                    current.FullName,
                    "IBM Granite with TurboQuant (Intel)"));

            if (hasGlobalJson && hasTools && hasApplication)
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Repository root was not found from the test output directory.");
    }
}
