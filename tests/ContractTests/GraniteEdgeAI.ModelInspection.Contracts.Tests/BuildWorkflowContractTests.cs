using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

/// <summary>
/// Protects the hosted CI boundary that executes every Gate 2 layer before the
/// unchanged WinUI regression and preserves evidence even when a test fails.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public sealed class BuildWorkflowContractTests
{
    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    public void BuildWorkflowContainsCurrentContractGate()
    {
        string workflow = ReadWorkflow();

        StringAssert.Contains(workflow, "shared");
        StringAssert.Contains(workflow, "tests/ContractTests");
        StringAssert.Contains(workflow, "CONTRACT_TEST_PROJECT");
        StringAssert.Contains(workflow, "Run Model Inspection contract tests");
        StringAssert.Contains(workflow, "--minimum-expected-tests 75");
    }

    [TestMethod]
    public void BuildWorkflowRunsCompleteContractProjectWithoutCategoryFilter()
    {
        string workflow = ReadWorkflow();
        string contractStep = ExtractWorkflowStep(
            workflow,
            "Run Model Inspection contract tests");

        Assert.IsFalse(
            contractStep.Contains("--filter", StringComparison.Ordinal),
            "The complete contract project must run because the category filter discovers zero tests under the selected Microsoft Testing Platform configuration.");
        StringAssert.Contains(
            contractStep,
            "--minimum-expected-tests 75",
            "The contract floor must remain after the incompatible filter is removed.");
    }

    [TestMethod]
    public void BuildWorkflowExecutesAndPreservesEveryGate2Layer()
    {
        string workflow = ReadWorkflow();
        string[] requiredFragments =
        [
            "GATE2_RESULTS_DIRECTORY",
            "Run Gate 2 transport tests",
            "Run Gate 2 worker host tests",
            "Run Gate 2 WorkerClient tests",
            "Run Gate 2 real process tests",
            "GraniteEdgeAI.ModelInspection.Transport.Tests.trx",
            "GraniteEdgeAI.ModelInspection.Worker.Tests.trx",
            "GraniteEdgeAI.ModelInspection.WorkerClient.Tests.trx",
            "GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.trx",
            "Check for orphaned Gate 2 processes",
            "GraniteEdgeAI.ModelInspection.ProtocolTestWorker",
            "Upload Gate 2 verification results",
            "gate2-verification-${{ github.run_id }}-${{ github.run_attempt }}"
        ];

        foreach (string fragment in requiredFragments)
        {
            StringAssert.Contains(
                workflow,
                fragment,
                $"The full workflow is missing Gate 2 evidence fragment: {fragment}");
        }

        int orphanCheckIndex = workflow.IndexOf(
            "Check for orphaned Gate 2 processes",
            StringComparison.Ordinal);
        int artifactIndex = workflow.IndexOf(
            "Upload Gate 2 verification results",
            StringComparison.Ordinal);
        Assert.IsTrue(orphanCheckIndex >= 0);
        Assert.IsTrue(artifactIndex > orphanCheckIndex);

        string orphanAndArtifactSection = workflow[orphanCheckIndex..];
        Assert.IsTrue(
            CountOccurrences(orphanAndArtifactSection, "if: ${{ always() }}") >= 2,
            "Orphan detection and Gate 2 evidence upload must both run under always().");
    }

    private static string ReadWorkflow() =>
        File.ReadAllText(Path.Combine(
            Root,
            ".github",
            "workflows",
            "build-and-test.yml"));

    private static string ExtractWorkflowStep(
        string workflow,
        string stepName)
    {
        string marker = $"- name: {stepName}";
        int stepStart = workflow.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(
            stepStart >= 0,
            $"Workflow step was not found: {stepName}");

        int nextStep = workflow.IndexOf(
            "\n      - name:",
            stepStart + marker.Length,
            StringComparison.Ordinal);

        return nextStep >= 0
            ? workflow[stepStart..nextStep]
            : workflow[stepStart..];
    }

    private static int CountOccurrences(
        string value,
        string token)
    {
        int count = 0;
        int offset = 0;
        while (true)
        {
            int index = value.IndexOf(
                token,
                offset,
                StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            offset = index + token.Length;
        }
    }

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
