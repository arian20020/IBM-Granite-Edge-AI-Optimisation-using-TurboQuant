using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

/// <summary>
/// Protects the hosted CI boundary that executes the framework-neutral Model
/// Inspection contract suite before the WinUI application is restored or built.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public sealed class BuildWorkflowContractTests
{
    /// <summary>
    /// Proves that the build workflow checks out the shared contract sources,
    /// identifies the contract-test project, runs the gate, and refuses to
    /// accept a materially reduced test count.
    /// </summary>
    [TestMethod]
    public void BuildWorkflow_ContainsRequiredModelInspectionContractGate()
    {
        string repositoryRoot = FindRepositoryRoot();
        string workflowPath = Path.Combine(
            repositoryRoot,
            ".github",
            "workflows",
            "build-and-test.yml");
        string workflow = File.ReadAllText(workflowPath);

        StringAssert.Contains(workflow, "shared");
        StringAssert.Contains(workflow, "tests/ContractTests");
        StringAssert.Contains(workflow, "CONTRACT_TEST_PROJECT");
        StringAssert.Contains(
            workflow,
            "Run Model Inspection contract tests");
        StringAssert.Contains(
            workflow,
            "--minimum-expected-tests 41");
    }

    /// <summary>
    /// Walks upward from deterministic runtime locations until the repository
    /// markers used by the workflow contract are found.
    /// </summary>
    private static string FindRepositoryRoot()
    {
        string[] startingPaths =
        [
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            Path.GetDirectoryName(
                typeof(BuildWorkflowContractTests).Assembly.Location)
                ?? AppContext.BaseDirectory
        ];

        foreach (string startingPath in startingPaths)
        {
            DirectoryInfo? directory = new(startingPath);

            while (directory is not null)
            {
                string globalJsonPath = Path.Combine(
                    directory.FullName,
                    "global.json");
                string workflowPath = Path.Combine(
                    directory.FullName,
                    ".github",
                    "workflows",
                    "build-and-test.yml");

                if (File.Exists(globalJsonPath) &&
                    File.Exists(workflowPath))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root containing global.json and the build workflow.");
    }
}
