using System.IO.Enumeration;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class CompileLinkIntegrityTests
{
    private static readonly string[] CompilerPropertyNames =
    [
        "TargetFramework", "PlatformTarget", "RuntimeIdentifier",
        "ImplicitUsings", "Nullable", "TreatWarningsAsErrors", "EnableNETAnalyzers",
        "AnalysisLevel", "Deterministic"
    ];

    private static readonly string[] ExpectedProjectReferences =
    [
        @"..\..\..\shared\GraniteEdgeAI.GgufRuntime.Contracts\GraniteEdgeAI.GgufRuntime.Contracts.csproj",
        @"..\..\..\shared\GraniteEdgeAI.ModelHardwareCompatibility.Core\GraniteEdgeAI.ModelHardwareCompatibility.Core.csproj",
        @"..\..\..\shared\GraniteEdgeAI.OpenVino.Contracts\GraniteEdgeAI.OpenVino.Contracts.csproj"
    ];

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
                AssertNoReparseAncestry(repository, canonical);
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
        string[] allMethods = Directory.EnumerateFiles(projectDirectory, "*.cs")
            .SelectMany(path => methodPattern.Matches(File.ReadAllText(path))
                .Select(match => match.Groups["name"].Value))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] duplicateMethods = allMethods
            .GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() != 1)
            .Select(group => group.Key)
            .ToArray();
        Assert.AreEqual(0, duplicateMethods.Length,
            "Executable test method names must be globally unique: "
            + string.Join(", ", duplicateMethods));

        var rows = map.Split('\n')
            .Where(line => line.Count(character => character == '|') == 10)
            .Where(line => Regex.IsMatch(
                line,
                @"^\| [^|]+ \| `[A-Za-z0-9_]+` \|",
                RegexOptions.CultureInvariant | RegexOptions.NonBacktracking))
            .Select(ParseCoverageRow)
            .ToArray();
        string[] duplicateRows = rows
            .GroupBy(row => row.Method, StringComparer.Ordinal)
            .Where(group => group.Count() != 1)
            .Select(group => group.Key)
            .ToArray();
        Assert.AreEqual(0, duplicateRows.Length,
            "Coverage rows must be unique: " + string.Join(", ", duplicateRows));
        CollectionAssert.AreEquivalent(allMethods,
            rows.Select(row => row.Method).ToArray(),
            "Coverage map must contain exactly one structured row per executable method.");

        foreach (CoverageRow row in rows)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(row.Requirement), row.Method + " requirement");
            Assert.IsFalse(string.IsNullOrWhiteSpace(row.CompiledSubject), row.Method + " compiled subject");
            Assert.IsFalse(string.IsNullOrWhiteSpace(row.Layer), row.Method + " layer");
            Assert.IsFalse(string.IsNullOrWhiteSpace(row.ExpectedDisposition), row.Method + " expected disposition");
            Assert.IsFalse(string.IsNullOrWhiteSpace(row.CurrentDisposition), row.Method + " current disposition");
            Assert.IsFalse(string.IsNullOrWhiteSpace(row.ProductionOwner), row.Method + " production owner");
            Assert.IsFalse(string.IsNullOrWhiteSpace(row.FollowUp), row.Method + " follow-up");
            Assert.IsFalse(string.IsNullOrWhiteSpace(row.Limitation), row.Method + " limitation");
        }
    }

    [TestMethod]
    public void LinkedCompilationOptionsAndReferenceBoundaryRemainExplicit()
    {
        string projectDirectory = ProjectDirectory();
        XDocument project = XDocument.Load(Path.Combine(projectDirectory,
            "GraniteEdgeAI.CrossFeature.IntegrationTests.csproj"));
        Dictionary<string, string> properties = project.Descendants()
            .Where(element => CompilerPropertyNames.Contains(
                element.Name.LocalName, StringComparer.Ordinal))
            .ToDictionary(element => element.Name.LocalName, element => element.Value,
                StringComparer.Ordinal);
        var expectedProperties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TargetFramework"] = "net8.0-windows10.0.19041.0",
            ["PlatformTarget"] = "x64",
            ["RuntimeIdentifier"] = "win-x64",
            ["ImplicitUsings"] = "enable",
            ["Nullable"] = "enable",
            ["TreatWarningsAsErrors"] = "true",
            ["EnableNETAnalyzers"] = "true",
            ["AnalysisLevel"] = "latest-recommended",
            ["Deterministic"] = "true"
        };
        CollectionAssert.AreEquivalent(expectedProperties, properties);
        Assert.IsFalse(project.Descendants().Any(element =>
            element.Name.LocalName == "DefineConstants"),
            "Linked sources must not silently acquire test-only compile constants.");

        string[] projectReferences = project.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")!.Value)
            .ToArray();
        CollectionAssert.AreEqual(ExpectedProjectReferences, projectReferences);

        string[] includeGroups = project.Descendants()
            .Where(element => element.Name.LocalName == "Compile"
                && element.Attribute("Include") is not null)
            .Select(element => element.Attribute("Include")!.Value)
            .ToArray();
        Assert.AreEqual(60, includeGroups.Length,
            "Every unavoidable linked production file must remain exact and documented.");
        Assert.IsTrue(includeGroups.All(include =>
            include.StartsWith(@"..\..\..\", StringComparison.Ordinal)));
        Assert.IsTrue(includeGroups.All(include => !include.Contains('*')),
            "Temporary compile links must enumerate exact files; wildcard expansion is prohibited.");

        string coverage = File.ReadAllText(Path.Combine(
            projectDirectory, "COVERAGE-MAP.md"));
        foreach (string include in includeGroups)
        {
            string repositoryRelative = include[9..].Replace('\\', '/');
            StringAssert.Contains(coverage, $"`{repositoryRelative}`",
                "Every retained exact compile link requires a coverage-map custody row.");
        }
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
        bool recursive = normalized.Contains(
            $"**{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
        return EnumerateFilesWithoutFollowingReparsePoints(root, pattern, recursive);
    }

    private static IReadOnlyList<string> EnumerateFilesWithoutFollowingReparsePoints(
        string root,
        string pattern,
        bool recursive)
    {
        const int maximumDirectories = 4096;
        const int maximumEntries = 32768;
        string canonicalRoot = Canonical(root);
        var pending = new Stack<string>();
        var files = new List<string>();
        pending.Push(canonicalRoot);
        int visitedDirectories = 0;
        int visitedEntries = 0;

        while (pending.Count > 0)
        {
            string directory = Canonical(pending.Pop());
            Assert.IsTrue(directory.Equals(canonicalRoot, StringComparison.OrdinalIgnoreCase)
                || IsUnder(directory, canonicalRoot),
                $"Enumeration escaped its canonical root: {directory}");
            FileAttributes directoryAttributes = File.GetAttributes(directory);
            Assert.IsFalse(directoryAttributes.HasFlag(FileAttributes.ReparsePoint),
                $"Refusing to enter reparse directory: {directory}");
            Assert.IsTrue(++visitedDirectories <= maximumDirectories,
                $"Bounded enumeration exceeded {maximumDirectories} directories.");

            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                Assert.IsTrue(++visitedEntries <= maximumEntries,
                    $"Bounded enumeration exceeded {maximumEntries} entries.");
                string canonicalEntry = Canonical(entry);
                Assert.IsTrue(IsUnder(canonicalEntry, canonicalRoot),
                    $"Entry escaped its canonical root: {canonicalEntry}");
                FileAttributes attributes = File.GetAttributes(canonicalEntry);
                Assert.IsFalse(attributes.HasFlag(FileAttributes.ReparsePoint),
                    $"Linked-source enumeration encountered reparse entry: {canonicalEntry}");
                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    if (recursive)
                        pending.Push(canonicalEntry);
                }
                else if (FileSystemName.MatchesSimpleExpression(
                             pattern, Path.GetFileName(canonicalEntry), ignoreCase: true))
                {
                    files.Add(canonicalEntry);
                }
            }
        }

        return files;
    }

    private static void AssertNoReparseAncestry(string root, string path)
    {
        string canonicalRoot = Canonical(root);
        string canonicalPath = Canonical(path);
        Assert.IsTrue(canonicalPath.Equals(canonicalRoot, StringComparison.OrdinalIgnoreCase)
            || IsUnder(canonicalPath, canonicalRoot),
            $"Path escaped repository custody: {canonicalPath}");
        string relative = Path.GetRelativePath(canonicalRoot, canonicalPath);
        string current = canonicalRoot;
        Assert.IsFalse(File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint),
            $"Repository root is a reparse point: {current}");
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            Assert.IsFalse(File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint),
                $"Linked source ancestry contains a reparse point: {current}");
        }
    }

    private static CoverageRow ParseCoverageRow(string line)
    {
        string[] cells = line.Split('|');
        Assert.AreEqual(11, cells.Length,
            "Coverage rows require exactly nine populated columns: " + line);
        string methodCell = cells[2].Trim();
        Match method = Regex.Match(methodCell, @"^`(?<name>[A-Za-z0-9_]+)`$");
        Assert.IsTrue(method.Success, "Invalid coverage method cell: " + methodCell);
        return new CoverageRow(
            method.Groups["name"].Value,
            cells[1].Trim(), cells[3].Trim(), cells[4].Trim(),
            cells[5].Trim(), cells[6].Trim(), cells[7].Trim(),
            cells[8].Trim(), cells[9].Trim());
    }

    private sealed record CoverageRow(
        string Method,
        string Requirement,
        string CompiledSubject,
        string Layer,
        string ExpectedDisposition,
        string CurrentDisposition,
        string ProductionOwner,
        string FollowUp,
        string Limitation);

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

    private static string ProjectDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName,
                    "GraniteEdgeAI.CrossFeature.IntegrationTests.csproj"))
                && File.Exists(Path.Combine(directory.FullName, "COVERAGE-MAP.md")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException(
            "Cross-feature test project markers were not found in executable ancestry.");
    }

    private static string RepositoryRoot(string projectDirectory)
    {
        DirectoryInfo? directory = new(projectDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository marker was not found.");
    }
}
