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
        StringAssert.Contains(workflow, "--minimum-expected-tests 129");
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
            "The dedicated contract project must run as a complete suite without a redundant category filter.");
        StringAssert.Contains(
            contractStep,
            "--minimum-expected-tests 129",
            "The contract floor must remain aligned with the mandatory contract suite.");
    }

    [TestMethod]
    public void BuildWorkflowUsesNativeMtpTrxReportingForGate2Tests()
    {
        string workflow = ReadWorkflow();
        string[] stepNames =
        [
            "Run Model Inspection contract tests",
            "Run Gate 2 transport tests",
            "Run Gate 2 worker host tests",
            "Run Gate 2 WorkerClient tests",
            "Run Gate 2 real process tests"
        ];

        foreach (string stepName in stepNames)
        {
            string step = ExtractWorkflowStep(workflow, stepName);

            Assert.IsFalse(
                step.Contains("--logger", StringComparison.Ordinal),
                $"The MTP test step still uses the legacy VSTest logger option: {stepName}");
            StringAssert.Contains(
                step,
                "--report-trx",
                $"The MTP test step does not enable native TRX reporting: {stepName}");
            StringAssert.Contains(
                step,
                "--report-trx-filename",
                $"The MTP test step does not give its TRX a deterministic file name: {stepName}");
        }
    }

    [TestMethod]
    public void Gate2MtpTestProjectsReferenceTrxReporter()
    {
        string[] projectPaths =
        [
            "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj",
            "tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj",
            "tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj",
            "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj",
            "tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj"
        ];

        const string ReporterReference =
            "<PackageReference Include=\"Microsoft.Testing.Extensions.TrxReport\" Version=\"2.3.2\" />";

        foreach (string projectPath in projectPaths)
        {
            string absolutePath = Path.Combine(
                Root,
                projectPath.Replace('/', Path.DirectorySeparatorChar));
            string project = File.ReadAllText(absolutePath);

            StringAssert.Contains(
                project,
                ReporterReference,
                $"The Gate 2 MTP test project does not pin the TRX reporter: {projectPath}");
        }
    }

    [TestMethod]
    public void BuildWorkflowChecksOutCleanupInventoryInputs()
    {
        string workflow = ReadWorkflow();

        StringAssert.Contains(workflow, "            docs");
        StringAssert.Contains(workflow, "            scripts");
        StringAssert.Contains(workflow, "            tools");
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

        string focusedWorkerWorkflow = File.ReadAllText(Path.Combine(
            Root,
            ".github",
            "workflows",
            "model-inspection-worker-tests.yml"));
        string[] workerEngineIdentityFragments =
        [
            "Verify exact worker engine test identities",
            "Expected exactly 67 worker engine cases",
            "InspectAsyncMapsAllowlistedFailureToFixedMessageWithoutDetailLeak' = 10",
            "InspectAsyncMissingIntegrityEvidenceTakesVerificationFailurePrecedence' = 6",
            "InspectAsyncRejectsIncompleteOrUnsafeSuccessfulResult' = 41"
        ];

        foreach (string fragment in workerEngineIdentityFragments)
        {
            StringAssert.Contains(
                workflow,
                fragment,
                $"The permanent workflow is missing the exact Worker engine guard: {fragment}");
            StringAssert.Contains(
                focusedWorkerWorkflow,
                fragment,
                $"The focused workflow is missing the exact Worker engine guard: {fragment}");
        }
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
