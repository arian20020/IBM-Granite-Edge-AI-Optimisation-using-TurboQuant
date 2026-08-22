using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
public sealed class CleanupInventoryContractTests
{
    private static readonly string Root = FindRepositoryRoot();

    private static readonly string[] RequiredFixtureCatalogueRoots =
    [
        "shared/GraniteEdgeAI.ModelInspection.Fixtures",
        "tests/TestFixtures/ModelInspectionScenarios"
    ];

    private const string RequiredFixtureReportPath =
        "docs/evidence/testing/Model-Inspection-Fixture-Catalog.md";

    private static readonly string[] CompleteRoots =
    [
        "IBM Granite with TurboQuant (Intel)/Features/ModelInspection",
        "shared/GraniteEdgeAI.ModelInspection.Contracts",
        "shared/GraniteEdgeAI.ModelInspection.Fixtures",
        "shared/GraniteEdgeAI.ModelInspection.Transport",
        "infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient",
        "runtime/GraniteEdgeAI.ModelInspection.LlamaSharp",
        "workers/GraniteEdgeAI.ModelInspection.Worker",
        "tools/ModelInspection.LlamaSharpSpike",
        "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests",
        "tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests",
        "tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests",
        "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests",
        "tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests",
        "tests/ProcessFixtures/GraniteEdgeAI.ModelInspection.ProtocolTestWorker",
        "tests/TestFixtures/ModelInspectionScenarios",
        "scripts/model-inspection"
    ];

    [TestMethod]
    public void CleanupSourceListIsSortedUniqueAndContainsOnlyExistingFiles()
    {
        string[] sourceFiles = ReadSourceFiles();
        string[] sortedUnique = sourceFiles
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(sortedUnique, sourceFiles);

        string[] missing = sourceFiles
            .Where(path => !File.Exists(Path.Combine(Root, path)))
            .ToArray();

        Assert.AreEqual(
            0,
            missing.Length,
            $"The cleanup source list contains missing files:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }

    [TestMethod]
    public void CleanupInventoryContainsEverySourceFileExactlyOnce()
    {
        string[] sourceFiles = ReadSourceFiles();
        string[] inventoryFiles = ReadInventoryFiles();
        CollectionAssert.AreEqual(sourceFiles, inventoryFiles);

        string inventoryPath = Path.Combine(
            Root,
            "docs",
            "reviews",
            "model-inspection-cleanup-inventory.md");
        string[] inventoryRows = File.ReadAllLines(inventoryPath);
        string header = inventoryRows.Single(line =>
            line.Contains(
                "supersedes the earlier historical cardinality",
                StringComparison.Ordinal));
        StringAssert.Contains(
            header,
            $"The current source list and ledger contain {sourceFiles.Length} " +
            "exact, sorted, unique paths.");
        StringAssert.Contains(
            header,
            $"cleanup reconciliation is recorded at " +
            $"{sourceFiles.Length}/{inventoryFiles.Length}");

        foreach (string selfLedgerPath in new[]
                 {
                     "docs/reviews/model-inspection-cleanup-inventory.md",
                     "docs/reviews/model-inspection-cleanup-source-files.txt"
                 })
        {
            string rowPrefix = $"| `{selfLedgerPath}` |";
            string row = inventoryRows.Single(line =>
                line.StartsWith(rowPrefix, StringComparison.Ordinal));
            StringAssert.Contains(
                row,
                $"current cleanup scope contains {sourceFiles.Length} " +
                "exact, sorted, unique paths");
            StringAssert.Contains(
                row,
                $"source/inventory reconcile at " +
                $"{sourceFiles.Length}/{inventoryFiles.Length}");
        }
    }

    [TestMethod]
    public void CurrentCleanupScopeIsFullyInventoried()
    {
        string[] sourceFiles = ReadSourceFiles();
        string[] currentFiles = DiscoverCurrentScopeFiles();
        string[] missingRoots = RequiredFixtureCatalogueRoots
            .Except(CompleteRoots, StringComparer.Ordinal)
            .ToArray();
        string[] missing = currentFiles
            .Except(sourceFiles, StringComparer.Ordinal)
            .ToArray();
        string inventoryHeader = File.ReadLines(Path.Combine(
                Root,
                "docs",
                "reviews",
                "model-inspection-cleanup-inventory.md"))
            .Single(line => line.Contains(
                "supersedes the earlier historical cardinality",
                StringComparison.Ordinal));

        List<string> failures = [];
        if (missingRoots.Length != 0)
        {
            failures.Add(
                $"Required complete roots are missing:{Environment.NewLine}" +
                string.Join(Environment.NewLine, missingRoots));
        }

        if (!currentFiles.Contains(
                RequiredFixtureReportPath,
                StringComparer.Ordinal) ||
            !sourceFiles.Contains(
                RequiredFixtureReportPath,
                StringComparer.Ordinal))
        {
            failures.Add(
                $"Required fixture report is absent from the cleanup source list: " +
                RequiredFixtureReportPath);
        }

        if (missing.Length != 0)
        {
            failures.Add(
                $"Current Model Inspection files need {missing.Length} inventory rows:" +
                $"{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
        }

        int expectedSourceCount = sourceFiles
            .Union(currentFiles, StringComparer.Ordinal)
            .Count();
        string expectedHeaderCount =
            $"The current source list and ledger contain {expectedSourceCount} " +
            "exact, sorted, unique paths.";
        if (!inventoryHeader.Contains(expectedHeaderCount, StringComparison.Ordinal))
        {
            failures.Add(
                $"The inventory header must contain the computed current-scope claim: " +
                expectedHeaderCount);
        }

        Assert.AreEqual(
            0,
            failures.Count,
            string.Join(
                $"{Environment.NewLine}{Environment.NewLine}",
                failures));
    }

    private static string[] ReadSourceFiles() =>
        File.ReadAllLines(Path.Combine(
                Root,
                "docs",
                "reviews",
                "model-inspection-cleanup-source-files.txt"))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(Normalize)
            .ToArray();

    private static string[] ReadInventoryFiles() =>
        File.ReadLines(Path.Combine(
                Root,
                "docs",
                "reviews",
                "model-inspection-cleanup-inventory.md"))
            .Where(line => line.StartsWith("| `", StringComparison.Ordinal))
            .Select(line => line.Split('`')[1])
            .Select(Normalize)
            .ToArray();

    private static string[] DiscoverCurrentScopeFiles()
    {
        HashSet<string> files = new(StringComparer.Ordinal);

        foreach (string root in CompleteRoots
                     .Concat(RequiredFixtureCatalogueRoots)
                     .Distinct(StringComparer.Ordinal))
        {
            string fullRoot = Path.Combine(Root, root);
            if (!Directory.Exists(fullRoot))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(
                         fullRoot,
                         "*",
                         SearchOption.AllDirectories))
            {
                if (!IsGeneratedBuildOutput(file))
                {
                    files.Add(ToRelative(file));
                }
            }
        }

        AddMatchingFiles(
            files,
            "IBM Granite with TurboQuant (Intel)/Features/ModelImport",
            contentMustContainModelInspection: true);
        AddMatchingFiles(
            files,
            "IBM Granite with TurboQuant (Intel)/Features/Onboarding",
            contentMustContainModelInspection: true);
        AddMatchingFiles(
            files,
            "tests/UnitTests/GraniteEdgeAI.UnitTests",
            contentMustContainModelInspection: true);

        foreach (string workflow in Directory.EnumerateFiles(
                     Path.Combine(Root, ".github", "workflows"),
                     "*.yml"))
        {
            string name = Path.GetFileName(workflow);
            if (name.Equals("build-and-test.yml", StringComparison.Ordinal) ||
                name.StartsWith("model-inspection-", StringComparison.Ordinal) ||
                name.StartsWith("llamasharp-", StringComparison.Ordinal))
            {
                files.Add(ToRelative(workflow));
            }
        }

        foreach (string document in Directory.EnumerateFiles(
                     Path.Combine(Root, "docs"),
                     "*",
                     SearchOption.AllDirectories))
        {
            string relative = ToRelative(document);
            if (relative.Contains("model-inspection", StringComparison.OrdinalIgnoreCase) ||
                relative.Contains("llamasharp", StringComparison.OrdinalIgnoreCase))
            {
                files.Add(relative);
            }
        }

        foreach (string path in new[]
        {
            "IBM Granite with TurboQuant (Intel).slnx",
            "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj",
            "IBM Granite with TurboQuant (Intel)/MainWindow.xaml",
            "IBM Granite with TurboQuant (Intel)/MainWindow.xaml.cs",
            "shared/README.md",
            "infrastructure/README.md",
            "workers/README.md",
            "tools/README.md"
        })
        {
            if (File.Exists(Path.Combine(Root, path)))
            {
                files.Add(path);
            }
        }

        return files.OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    private static void AddMatchingFiles(
        ISet<string> files,
        string relativeRoot,
        bool contentMustContainModelInspection)
    {
        string fullRoot = Path.Combine(Root, relativeRoot);
        if (!Directory.Exists(fullRoot))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(
                     fullRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            if (IsGeneratedBuildOutput(file))
            {
                continue;
            }

            if (!contentMustContainModelInspection ||
                File.ReadAllText(file).Contains(
                    "ModelInspection",
                    StringComparison.OrdinalIgnoreCase))
            {
                files.Add(ToRelative(file));
            }
        }
    }

    private static bool IsGeneratedBuildOutput(string path)
    {
        string[] segments = ToRelative(path).Split('/');
        return segments.Any(segment =>
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    private static string ToRelative(string path) =>
        Normalize(Path.GetRelativePath(Root, path));

    private static string Normalize(string path) =>
        path.Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        string[] startingPaths =
        [
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            Path.GetDirectoryName(
                typeof(CleanupInventoryContractTests).Assembly.Location)
                ?? AppContext.BaseDirectory
        ];

        foreach (string startingPath in startingPaths)
        {
            DirectoryInfo? directory = new(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                    File.Exists(Path.Combine(
                        directory.FullName,
                        "IBM Granite with TurboQuant (Intel).slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root for cleanup inventory tests.");
    }
}
