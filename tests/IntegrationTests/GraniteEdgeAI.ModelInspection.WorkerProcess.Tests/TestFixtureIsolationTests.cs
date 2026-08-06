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
        string[] productionRoots =
        [
            Path.Combine(root, "shared"),
            Path.Combine(root, "workers"),
            Path.Combine(root, "infrastructure"),
            Path.Combine(root, "IBM Granite with TurboQuant (Intel)")
        ];

        List<string> violations = [];
        foreach (string productionRoot in productionRoots)
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
                         path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
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

        Assert.AreEqual(
            0,
            violations.Count,
            string.Join(Environment.NewLine, violations));
    }

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
