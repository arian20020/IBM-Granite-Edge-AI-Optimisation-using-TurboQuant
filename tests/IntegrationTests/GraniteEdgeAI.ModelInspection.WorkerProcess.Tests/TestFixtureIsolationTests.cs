using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerProcess.Tests;

/// <summary>
/// Prevents abnormal test modes from becoming production switches.
/// </summary>
[TestClass]
public sealed class TestFixtureIsolationTests
{
    private static readonly string[] ForbiddenTokens =
    [
        "TestWorkerScenario",
        "CrashAfterHello",
        "HangAfterStart",
        "IgnoreCancellation",
        "ExitRootWithLiveChild",
        "ProtocolTestWorker"
    ];

    [TestMethod]
    public void ProductionSourcesContainNoFixtureScenarioSwitches()
    {
        string root = FindRepositoryRoot();
        List<string> violations = FindFixtureViolations(root);

        Assert.AreEqual(
            0,
            violations.Count,
            string.Join(Environment.NewLine, violations));

        AssertControlledRuntimeRootScan();
    }

    private static void AssertControlledRuntimeRootScan()
    {
        DirectoryInfo controlledRoot = Directory.CreateTempSubdirectory(
            "GraniteEdgeAI-FixtureIsolation-");
        try
        {
            foreach (string productionRoot in GetProductionRoots(
                         controlledRoot.FullName))
            {
                Directory.CreateDirectory(productionRoot);
            }

            string siblingSource = Path.Combine(
                controlledRoot.FullName,
                "runtime",
                "Future.Runtime.Sibling",
                "Probe.cs");
            Directory.CreateDirectory(
                Path.GetDirectoryName(siblingSource)!);
            File.WriteAllText(
                siblingSource,
                "internal enum TestWorkerScenario { }");

            string binaryOutputSource = Path.Combine(
                controlledRoot.FullName,
                "runtime",
                "Future.Runtime.Sibling",
                "bin",
                "Generated.cs");
            Directory.CreateDirectory(
                Path.GetDirectoryName(binaryOutputSource)!);
            File.WriteAllText(
                binaryOutputSource,
                "internal sealed class CrashAfterHello { }");

            string intermediateOutputSource = Path.Combine(
                controlledRoot.FullName,
                "runtime",
                "Future.Runtime.Sibling",
                "obj",
                "Generated.cs");
            Directory.CreateDirectory(
                Path.GetDirectoryName(intermediateOutputSource)!);
            File.WriteAllText(
                intermediateOutputSource,
                "internal sealed class HangAfterStart { }");

            List<string> violations = FindFixtureViolations(
                controlledRoot.FullName);

            Assert.HasCount(1, violations);
            Assert.AreEqual(
                Path.Combine(
                    "runtime",
                    "Future.Runtime.Sibling",
                    "Probe.cs") + " contains TestWorkerScenario",
                violations[0]);
        }
        finally
        {
            controlledRoot.Delete(recursive: true);
        }
    }

    private static List<string> FindFixtureViolations(string root)
    {
        List<string> violations = [];
        foreach (string productionRoot in GetProductionRoots(root))
        {
            Assert.IsTrue(
                Directory.Exists(productionRoot),
                $"Production root was not staged: {productionRoot}");
            foreach (string file in Directory.EnumerateFiles(
                         productionRoot,
                         "*.*",
                         SearchOption.AllDirectories)
                     .Where(static path =>
                         path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                         path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                     .Where(path => !IsUnderBuildOutputDirectory(
                         productionRoot,
                         path)))
            {
                string content = File.ReadAllText(file);
                foreach (string token in ForbiddenTokens)
                {
                    if (content.Contains(token, StringComparison.Ordinal))
                    {
                        violations.Add(
                            $"{Path.GetRelativePath(root, file)} contains {token}");
                    }
                }
            }
        }

        violations.Sort(StringComparer.Ordinal);
        return violations;
    }

    private static bool IsUnderBuildOutputDirectory(
        string productionRoot,
        string path) =>
        Path.GetRelativePath(productionRoot, path)
            .Split(Path.DirectorySeparatorChar)
            .Any(static segment =>
                segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("obj", StringComparison.OrdinalIgnoreCase));

    private static string[] GetProductionRoots(string root) =>
    [
        Path.Combine(root, "shared"),
        Path.Combine(root, "workers"),
        Path.Combine(root, "runtime"),
        Path.Combine(root, "infrastructure"),
        Path.Combine(root, "IBM Granite with TurboQuant (Intel)")
    ];

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "global.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "The repository root containing global.json could not be found.");
    }
}
