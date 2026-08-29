using System.Xml.Linq;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests.Support;

internal sealed record ProductionDebugFixtureRoot(
    string RelativePath,
    string FullPath);

internal static class ProductionDebugFixtureInventory
{
    private static readonly string[] ItemTypes =
    [
        "Compile",
        "Page",
        "None",
        "Content",
        "EmbeddedResource",
        "PRIResource",
    ];

    internal static IReadOnlyList<ProductionDebugFixtureRoot> Discover(
        string applicationProjectPath)
    {
        string projectDirectory = Path.GetDirectoryName(
            Path.GetFullPath(applicationProjectPath))!;
        string features = Path.Combine(projectDirectory, "Features");
        if (!Directory.Exists(features))
        {
            return [];
        }

        return Directory.EnumerateDirectories(features)
            .Select(feature => Path.Combine(feature, "DebugFixtures"))
            .Where(Directory.Exists)
            .Where(root => Directory.EnumerateFiles(
                root,
                "*",
                SearchOption.AllDirectories).Any())
            .Select(root => new ProductionDebugFixtureRoot(
                Path.GetRelativePath(projectDirectory, root).Replace('/', '\\'),
                Path.GetFullPath(root)))
            .OrderBy(root => root.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlyList<string> Validate(
        string applicationProjectPath)
    {
        IReadOnlyList<ProductionDebugFixtureRoot> roots = Discover(
            applicationProjectPath);
        if (roots.Count == 0)
        {
            return ["No production DebugFixtures roots were discovered."];
        }

        var failures = new List<string>();
        foreach ((string configuration, string platform) in new[]
                 {
                     ("Release", "x64"),
                     ("Release", "x86"),
                     ("Release", "ARM64"),
                     ("Debug", "x86"),
                     ("Debug", "ARM64"),
                 })
        {
            EvaluatedMsBuildItem[] leaked = EvaluateFixtureItems(
                applicationProjectPath,
                roots,
                configuration,
                platform,
                compatibilityGallery: false);
            foreach (EvaluatedMsBuildItem item in leaked)
            {
                failures.Add(
                    $"{configuration}|{platform} contains {Relative(applicationProjectPath, item.FullPath)} ({item.ItemType}).");
            }
        }

        ValidateDebugClosure(
            applicationProjectPath,
            roots,
            compatibilityGallery: false,
            failures);
        ValidateDebugClosure(
            applicationProjectPath,
            roots,
            compatibilityGallery: true,
            failures);
        return failures;
    }

    private static void ValidateDebugClosure(
        string projectPath,
        IReadOnlyList<ProductionDebugFixtureRoot> roots,
        bool compatibilityGallery,
        ICollection<string> failures)
    {
        HashSet<string> authorized = AuthorizedIncludes(
            projectPath,
            roots,
            compatibilityGallery);
        EvaluatedMsBuildItem[] actual = EvaluateFixtureItems(
            projectPath,
            roots,
            "Debug",
            "x64",
            compatibilityGallery);
        HashSet<string> actualPaths = actual
            .Select(item => Relative(projectPath, item.FullPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (EvaluatedMsBuildItem item in actual)
        {
            string relative = Relative(projectPath, item.FullPath);
            if (!authorized.Contains(relative))
            {
                failures.Add(
                    $"Debug|x64 compatibility={compatibilityGallery} contains unauthorized fixture {relative} ({item.ItemType}).");
            }
        }
        foreach (string expected in authorized)
        {
            if (!actualPaths.Contains(expected))
            {
                failures.Add(
                    $"Debug|x64 compatibility={compatibilityGallery} omits authorized fixture {expected}.");
            }
        }
    }

    private static HashSet<string> AuthorizedIncludes(
        string projectPath,
        IReadOnlyList<ProductionDebugFixtureRoot> roots,
        bool compatibilityGallery)
    {
        XDocument project = XDocument.Load(projectPath);
        var authorized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (XElement item in project.Descendants().Where(element =>
                     ItemTypes.Contains(element.Name.LocalName, StringComparer.Ordinal)))
        {
            string? include = item.Attribute("Include")?.Value;
            if (string.IsNullOrWhiteSpace(include) ||
                include.IndexOfAny(['*', '?']) >= 0 ||
                include.Contains("$(", StringComparison.Ordinal) ||
                include.Contains("@(", StringComparison.Ordinal) ||
                include.Contains("%(", StringComparison.Ordinal))
            {
                continue;
            }
            string condition = item.Parent?.Attribute("Condition")?.Value ?? string.Empty;
            bool ordinary = string.Equals(
                condition,
                "'$(Configuration)|$(Platform)' == 'Debug|x64'",
                StringComparison.Ordinal);
            bool optIn = compatibilityGallery && string.Equals(
                condition,
                "'$(CompatibilityFixtureGallery)' == 'true'",
                StringComparison.Ordinal);
            if (!ordinary && !optIn)
            {
                continue;
            }
            string fullPath = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(projectPath)!,
                include));
            if (roots.Any(root => IsWithin(fullPath, root.FullPath)))
            {
                authorized.Add(Relative(projectPath, fullPath));
            }
        }
        return authorized;
    }

    private static EvaluatedMsBuildItem[] EvaluateFixtureItems(
        string projectPath,
        IReadOnlyList<ProductionDebugFixtureRoot> roots,
        string configuration,
        string platform,
        bool compatibilityGallery) => EvaluatedMsBuildItems.Evaluate(
            projectPath,
            ItemTypes,
            properties: new Dictionary<string, string>
            {
                ["Configuration"] = configuration,
                ["Platform"] = platform,
                ["DesignTimeBuild"] = "true",
                ["CompatibilityFixtureGallery"] = compatibilityGallery ? "true" : "false",
            })
        .Where(item => roots.Any(root => IsWithin(item.FullPath, root.FullPath)))
        .ToArray();

    private static bool IsWithin(string candidate, string root)
    {
        string prefix = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(candidate).StartsWith(
            prefix,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string Relative(string projectPath, string fullPath) =>
        Path.GetRelativePath(Path.GetDirectoryName(projectPath)!, fullPath)
            .Replace('/', '\\');
}
