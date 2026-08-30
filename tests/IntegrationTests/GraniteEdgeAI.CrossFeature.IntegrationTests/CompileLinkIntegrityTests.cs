using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class CompileLinkIntegrityTests
{
    [TestMethod]
    public void LinkedProductionSourcesResolveCanonicallyWithoutLocalDuplicates()
    {
        string projectDirectory = ProjectDirectory();
        string repository = RepositoryRoot(projectDirectory);
        string projectPath = Path.Combine(projectDirectory,
            "GraniteEdgeAI.CrossFeature.IntegrationTests.csproj");
        XDocument project = XDocument.Load(projectPath);
        string[] allowedRoots =
        [
            Canonical(Path.Combine(repository, "IBM Granite with TurboQuant (Intel)")),
            Canonical(Path.Combine(repository, "shared")),
            Canonical(Path.Combine(repository, "infrastructure"))
        ];
        var linkedDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var canonicalSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (XElement item in project.Descendants()
                     .Where(element => element.Name.LocalName == "Compile"
                         && element.Attribute("Include") is not null))
        {
            string include = item.Attribute("Include")!.Value;
            string? link = item.Attribute("Link")?.Value;
            Assert.IsNotNull(link, $"Explicit linked source has no Link: {include}");
            IReadOnlyList<string> sources = Expand(projectDirectory, include);
            Assert.IsTrue(sources.Count > 0, $"Compile Include resolved no files: {include}");
            string[] excludes = (item.Attribute("Exclude")?.Value ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (string source in sources.Where(source =>
                         !excludes.Any(exclude => Matches(projectDirectory, source, exclude))))
            {
                string canonical = Canonical(source);
                Assert.IsTrue(File.Exists(canonical), canonical);
                Assert.IsTrue(allowedRoots.Any(root => IsUnder(canonical, root)),
                    $"Linked source escaped allowed production roots: {canonical}");
                Assert.IsFalse(HasSegment(canonical, "bin") || HasSegment(canonical, "obj"),
                    $"Generated output was linked as production: {canonical}");
                Assert.IsTrue(canonicalSources.Add(canonical),
                    $"Production source linked more than once: {canonical}");

                string destination = RenderLink(link!, include, source, projectDirectory);
                Assert.IsTrue(linkedDestinations.Add(destination),
                    $"Duplicate Link destination: {destination}");
            }
        }

        Assert.IsFalse(Directory.Exists(Path.Combine(projectDirectory, "Production")),
            "Test-local Production sources could shadow canonical linked sources.");
    }

    [TestMethod]
    public void CoverageMapListsEveryExecutableMethod()
    {
        string projectDirectory = ProjectDirectory();
        string map = File.ReadAllText(Path.Combine(projectDirectory, "COVERAGE-MAP.md"));
        Regex methodPattern = new(
            @"\[TestMethod\](?:\s*\[[^\r\n]+\])*\s*public\s+(?:async\s+)?(?:Task|void)\s+(?<name>[A-Za-z0-9_]+)\s*\(",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
        string[] methods = Directory.EnumerateFiles(projectDirectory, "*.cs")
            .SelectMany(path => methodPattern.Matches(File.ReadAllText(path))
                .Select(match => match.Groups["name"].Value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        foreach (string method in methods)
            StringAssert.Contains(map, $"`{method}`", method);
    }

    private static IReadOnlyList<string> Expand(string projectDirectory, string include)
    {
        string normalized = include.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        int wildcard = normalized.IndexOf('*');
        if (wildcard < 0)
            return [Path.GetFullPath(Path.Combine(projectDirectory, normalized))];

        int separator = normalized.LastIndexOf(
            Path.DirectorySeparatorChar, wildcard);
        string root = Path.GetFullPath(Path.Combine(
            projectDirectory, normalized[..separator]));
        string pattern = Path.GetFileName(normalized);
        SearchOption search = normalized.Contains(
            $"**{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(root, pattern, search).ToArray();
    }

    private static bool Matches(
        string projectDirectory,
        string source,
        string exclude)
    {
        string canonicalSource = Canonical(source);
        string normalized = exclude.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        int wildcard = normalized.IndexOf('*');
        if (wildcard < 0)
            return string.Equals(canonicalSource,
                Canonical(Path.Combine(projectDirectory, normalized)),
                StringComparison.OrdinalIgnoreCase);
        string prefix = normalized[..wildcard].TrimEnd(Path.DirectorySeparatorChar);
        return IsUnder(canonicalSource,
            Canonical(Path.Combine(projectDirectory, prefix)));
    }

    private static string RenderLink(
        string link,
        string include,
        string source,
        string projectDirectory)
    {
        string fileName = Path.GetFileNameWithoutExtension(source);
        string extension = Path.GetExtension(source);
        string recursive = string.Empty;
        int recursiveMarker = include.IndexOf("**", StringComparison.Ordinal);
        if (recursiveMarker >= 0)
        {
            string prefix = include[..recursiveMarker]
                .TrimEnd('\\', '/');
            string root = Path.GetFullPath(Path.Combine(projectDirectory, prefix));
            recursive = Path.GetRelativePath(root, Path.GetDirectoryName(source)!)
                .Replace('/', '\\');
            if (recursive == ".") recursive = string.Empty;
            else recursive += "\\";
        }
        return link.Replace("%(RecursiveDir)", recursive, StringComparison.Ordinal)
            .Replace("%(Filename)", fileName, StringComparison.Ordinal)
            .Replace("%(Extension)", extension, StringComparison.Ordinal);
    }

    private static bool HasSegment(string path, string segment) =>
        path.Split(Path.DirectorySeparatorChar)
            .Contains(segment, StringComparer.OrdinalIgnoreCase);

    private static bool IsUnder(string path, string root) =>
        path.StartsWith(root + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static string Canonical(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static string ProjectDirectory() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", ".."));

    private static string RepositoryRoot(string projectDirectory) =>
        Path.GetFullPath(Path.Combine(projectDirectory, "..", "..", ".."));
}
