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

    private const int HostedPackagedFloor = 686;

    private const string HostedPackagedFilter =
        "/TestCaseFilter:\"TestCategory!=ModelInspectionVisualRegression&TestCategory!=ModelInspectionControlledOs\"";

    private const string HostedPackagedStepSha256 =
        "785FD6A8A33CD7C2A7F5C2687595FD4F58FA8FFC5B48602E6ECF64BFD647F584";

    private const string ControlledWorkflowSha256 =
        "5A58BE19B9B7F0A6E56ECF6DA136AE138AC1F74A7FDEFAA72BEFC58B353CFEC7";

    private static readonly (string ClassName, int Count)[]
        HostedProtectedClassCounts =
        [
            ("GraniteEdgeAI.UnitTests.ModelInspectionRequestFactoryTests", 10),
            ("GraniteEdgeAI.UnitTests.ModelInspectionContractTests", 33),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Runtime.ModelInspectionProbeResultTests", 4),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Runtime.WorkerRequestMapperTests", 3),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Runtime.WorkerResultMapperTests", 9),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Runtime.WorkerProcessLlamaModelProbeTests", 7),
            ("GraniteEdgeAI.UnitTests.ModelInspectionClassifierTests", 5),
            ("GraniteEdgeAI.UnitTests.ModelInspectionServiceTests", 4),
            ("GraniteEdgeAI.UnitTests.DelegateCommandTests", 2),
            ("GraniteEdgeAI.UnitTests.ModelInspectionViewModelTests", 24),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.InspectionProgressPresentationFactoryTests", 13),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionPresentationFactoryTests", 18),
            ("GraniteEdgeAI.UnitTests.InspectionVisualStateGuardTests", 13),
            ("GraniteEdgeAI.UnitTests.ModelInspectionPageNavigationTests", 39),
            ("GraniteEdgeAI.UnitTests.OnboardingModelInspectionNavigationTests", 14),
            ("GraniteEdgeAI.UnitTests.ModelInspectionWorkerCompositionTests", 13),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionAssetContractTests", 3),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionDisplayTextPolicyTests", 46),
            ("GraniteEdgeAI.UnitTests.ModelInspectionViewSnapshotTests", 6),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionFigmaStatePresentationTests", 77),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.InspectionProgressRowsTests", 16),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionActionCardTests", 4),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionContentCardTests", 25),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionModelCardTests", 9),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionOutcomeCardTests", 7),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionRenderCoordinatorTests", 27),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionMotionTests", 29),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.ModelInspectionDisclosureTests", 5),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionRenderHarnessTests", 2),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionRenderedStateTests", 21),
            ("GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionAccessibilityTests", 9)
        ];

    private static readonly string[] ControlledReferenceNames =
    [
        "01-inspection-progress.png",
        "02-ready.png",
        "03-ready-expanded.png",
        "04-ready-with-warnings.png",
        "05-ready-with-warnings-expanded.png",
        "06-conversion-required.png",
        "07-conversion-required-expanded.png",
        "08-incomplete-package.png",
        "09-unsupported.png",
        "10-invalid.png",
        "11-invalid-expanded.png",
        "12-cancelled.png",
        "13-operational-failure.png"
    ];

    [TestMethod]
    public void BuildWorkflowContainsCurrentContractGate()
    {
        string workflow = ReadWorkflow();

        StringAssert.Contains(workflow, "shared");
        StringAssert.Contains(workflow, "tests/ContractTests");
        StringAssert.Contains(workflow, "CONTRACT_TEST_PROJECT");
        StringAssert.Contains(workflow, "Run Model Inspection contract tests");
        StringAssert.Contains(workflow, "--minimum-expected-tests 357");
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
            "--minimum-expected-tests 357",
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
        string checkout = ExtractWorkflowStep(
            workflow,
            "Check out required build inputs");

        StringAssert.Contains(
            checkout,
            "uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0");
        foreach (string partialCheckoutToken in new[]
                 {
                     "sparse-checkout:",
                     "sparse-checkout-cone-mode:",
                     "filter:"
                 })
        {
            Assert.IsFalse(
                checkout.Contains(
                    partialCheckoutToken,
                    StringComparison.OrdinalIgnoreCase),
                "Cleanup and Release-isolation inputs require the full " +
                $"physical repository checkout: {partialCheckoutToken}");
        }
    }

    [TestMethod]
    public void BuildWorkflowExecutesEveryGate2LayerWithoutRetainingRawResults()
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
            "GraniteEdgeAI.ModelInspection.ProtocolTestWorker"
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
        Assert.IsTrue(orphanCheckIndex >= 0);
        string orphanStep = ExtractWorkflowStep(
            workflow,
            "Check for orphaned Gate 2 processes");
        StringAssert.Contains(
            orphanStep,
            "if: ${{ always() }}",
            "Gate 2 orphan detection must still run after a failed test step.");
        Assert.IsFalse(
            workflow.Contains(
                "actions/upload-artifact@",
                StringComparison.OrdinalIgnoreCase),
            "Raw Gate 2 TRX must remain runner-ephemeral until a complete sanitizer exists.");
        Assert.IsFalse(
            workflow.Contains("gate2-verification-", StringComparison.Ordinal),
            "The permanent workflow must not advertise a raw Gate 2 artifact.");

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

        string focusedWorkerClientWorkflow = File.ReadAllText(Path.Combine(
            Root,
            ".github",
            "workflows",
            "model-inspection-worker-client-tests.yml"));
        string[] workerInstallationFragments =
        [
            "--minimum-expected-tests 91",
            "WorkerInstallationLayoutTests",
            "Expected exactly five fixed-layout tests"
        ];
        foreach (string fragment in workerInstallationFragments)
        {
            StringAssert.Contains(
                workflow,
                fragment,
                $"The permanent workflow is missing the fixed worker-layout guard: {fragment}");
            StringAssert.Contains(
                focusedWorkerClientWorkflow,
                fragment,
                $"The focused workflow is missing the fixed worker-layout guard: {fragment}");
        }

        string focusedProcessWorkflow = File.ReadAllText(Path.Combine(
            Root,
            ".github",
            "workflows",
            "model-inspection-worker-process-tests.yml"));
        string[] packagingFragments =
        [
            "--minimum-expected-tests 32",
            "ProductionWorkerPackagingTests"
        ];
        foreach (string fragment in packagingFragments)
        {
            StringAssert.Contains(
                workflow,
                fragment,
                $"The permanent workflow is missing the packaging guard: {fragment}");
            StringAssert.Contains(
                focusedProcessWorkflow,
                fragment,
                $"The focused process workflow is missing the packaging guard: {fragment}");
        }
        StringAssert.Contains(
            workflow,
            "$minimumExpectedTests = 686",
            "The permanent workflow must protect the current packaged application floor.");
        string[] protectedApplicationClassFragments =
        [
            "ModelInspectionRequestFactoryTests' = 10",
            "ModelInspectionContractTests' = 33",
            "ModelInspectionProbeResultTests' = 4",
            "WorkerRequestMapperTests' = 3",
            "WorkerResultMapperTests' = 9",
            "WorkerProcessLlamaModelProbeTests' = 7",
            "ModelInspectionClassifierTests' = 5",
            "ModelInspectionServiceTests' = 4",
            "DelegateCommandTests' = 2",
            "ModelInspectionViewModelTests' = 24",
            "InspectionProgressPresentationFactoryTests' = 13",
            "ModelInspectionPresentationFactoryTests' = 18",
            "InspectionVisualStateGuardTests' = 13",
            "ModelInspectionPageNavigationTests' = 39",
            "OnboardingModelInspectionNavigationTests' = 14",
            "ModelInspectionWorkerCompositionTests' = 13",
            "ModelInspectionAssetContractTests' = 3",
            "ModelInspectionDisplayTextPolicyTests' = 46",
            "ModelInspectionViewSnapshotTests' = 6",
            "ModelInspectionFigmaStatePresentationTests' = 77",
            "InspectionProgressRowsTests' = 16",
            "InspectionActionCardTests' = 4",
            "InspectionContentCardTests' = 25",
            "InspectionModelCardTests' = 9",
            "InspectionOutcomeCardTests' = 7",
            "ModelInspectionRenderCoordinatorTests' = 27",
            "ModelInspectionMotionTests' = 29",
            "ModelInspectionDisclosureTests' = 5",
            "ModelInspectionRenderHarnessTests' = 2",
            "ModelInspectionRenderedStateTests' = 21",
            "ModelInspectionAccessibilityTests' = 9"
        ];
        foreach (string fragment in protectedApplicationClassFragments)
        {
            StringAssert.Contains(
                workflow,
                fragment,
                $"The permanent workflow is missing the exact application guard: {fragment}");
        }
        StringAssert.Contains(
            focusedProcessWorkflow,
            "scripts/model-inspection",
            "The focused process workflow must stage the manifest scripts used by the packaging test.");
    }

    [TestMethod]
    public void BuildWorkflowProtectsExactHostedEquivalentPackagedEvidence()
    {
        string workflow = ReadWorkflow();
        string[] errors = ValidateHostedPackagedWorkflow(workflow);

        Assert.AreEqual(
            0,
            errors.Length,
            string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void HostedPackagedGuardRejectsFilterFloorClassAndRawTrxMutations()
    {
        string workflow = ReadWorkflow();
        Assert.AreEqual(0, ValidateHostedPackagedWorkflow(workflow).Length);

        string[] mutations =
        [
            workflow.Replace(
                HostedPackagedFilter,
                "/TestCaseFilter:\"TestCategory!=ModelInspectionVisualRegression\"",
                StringComparison.Ordinal),
            workflow.Replace(
                "$minimumExpectedTests = 686",
                "$minimumExpectedTests = 685",
                StringComparison.Ordinal),
            workflow.Replace(
                "'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionAssetContractTests' = 3",
                string.Empty,
                StringComparison.Ordinal),
            workflow.Replace(
                "'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionDisplayTextPolicyTests' = 46",
                "'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionDisplayTextPolicyTests' = 45",
                StringComparison.Ordinal),
            workflow.Replace(
                "            " + HostedPackagedFilter,
                "            /TestCaseFilter:\"TestCategory!=ModelInspectionVisualRegression\"\n" +
                "          # " + HostedPackagedFilter,
                StringComparison.Ordinal),
            workflow.Replace(
                "$minimumExpectedTests = 686",
                "$minimumExpectedTests = 686\n          $minimumExpectedTests = 1",
                StringComparison.Ordinal),
            workflow.Replace(
                "          foreach ($entry in $protectedClassCounts.GetEnumerator()) {",
                "          $protectedClassCounts.Clear()\n" +
                "          foreach ($entry in $protectedClassCounts.GetEnumerator()) {",
                StringComparison.Ordinal),
            workflow + Environment.NewLine +
                "path: D:/s/TestResults/UnitTests/**/*.trx",
            workflow + Environment.NewLine +
                "      - name: Upload renamed private unit result" + Environment.NewLine +
                "        uses: actions/upload-artifact@unapproved" + Environment.NewLine +
                "        with:" + Environment.NewLine +
                "          path: D:/s/private/GraniteEdgeAI.UnitTests.trx",
            workflow + Environment.NewLine +
                "      - name: Always upload broad private results" + Environment.NewLine +
                "        if: ${{ always() }}" + Environment.NewLine +
                "        uses: actions/upload-artifact@unapproved" + Environment.NewLine +
                "        with:" + Environment.NewLine +
                "          path: D:/s/private/**",
            workflow + Environment.NewLine +
                "      - name: Case-varied raw result upload" + Environment.NewLine +
                "        uses: Actions/Upload-Artifact@unapproved" + Environment.NewLine +
                "        with:" + Environment.NewLine +
                "          path: D:/s/private/**"
        ];

        foreach (string mutation in mutations)
        {
            Assert.IsNotEmpty(
                ValidateHostedPackagedWorkflow(mutation),
                "The hosted packaged guard accepted a floor/filter/class/privacy mutation.");
        }
    }

    [TestMethod]
    public void ControlledEvidenceWorkflowIsManualPreflightOnlyAndFailClosed()
    {
        string workflow = ReadControlledEvidenceWorkflow();
        string[] errors = ValidateControlledEvidenceWorkflow(workflow);

        Assert.AreEqual(
            0,
            errors.Length,
            string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void ControlledEvidencePreflightRejectsCampaignReferenceClassPinAndExecutionMutations()
    {
        string workflow = ReadControlledEvidenceWorkflow();
        Assert.AreEqual(0, ValidateControlledEvidenceWorkflow(workflow).Length);

        string[] mutations =
        [
            workflow.Replace(
                "campaign: light-96dpi-100text",
                "campaign: light-missing",
                StringComparison.Ordinal),
            workflow.Replace(
                "13-operational-failure.png",
                string.Empty,
                StringComparison.Ordinal),
            workflow.Replace(
                "ModelInspectionVisualReferenceIntegrityTests.cs",
                string.Empty,
                StringComparison.Ordinal),
            workflow.Replace(
                "visual-reference-manifest.json",
                string.Empty,
                StringComparison.Ordinal),
            workflow.Replace(
                "MODEL_INSPECTION_VISUAL_APPROVED_OS_BUILD",
                "MISSING_APPROVED_OS_BUILD",
                StringComparison.Ordinal),
            workflow.Replace(
                "CONTROLLED_EVIDENCE_PREFLIGHT_BLOCKED",
                "CONTROLLED_EVIDENCE_PREFLIGHT_READY",
                StringComparison.Ordinal),
            workflow.Replace(
                "          throw (",
                "          Write-Host (",
                StringComparison.Ordinal),
            workflow.Replace(
                    "          throw (\n            'CONTROLLED_EVIDENCE_PREFLIGHT_BLOCKED",
                    "          Write-Host (\n            'CONTROLLED_EVIDENCE_PREFLIGHT_BLOCKED",
                    StringComparison.Ordinal)
                .Replace(
                    "          using System;\n",
                    "          throw (\n          using System;\n",
                    StringComparison.Ordinal),
            workflow.Replace(
                "Actual Windows text scale could not be read.",
                "Assume 100 percent when Windows text scale is unreadable.",
                StringComparison.Ordinal),
            InsertBeforeControlledTerminalThrow(workflow, "exit 0"),
            InsertBeforeControlledTerminalThrow(workflow, "return"),
            InsertBeforeControlledTerminalThrow(workflow, "trap { continue }"),
            InsertBeforeControlledTerminalThrow(workflow, "break"),
            InsertBeforeControlledTerminalThrow(workflow, "continue"),
            InsertBeforeControlledTerminalThrow(
                workflow,
                "continue # exit preflight"),
            InsertBeforeControlledTerminalThrow(
                workflow,
                "continue outer"),
            InsertBeforeControlledTerminalThrow(
                workflow,
                "if ($true) { continue }"),
            InsertBeforeControlledTerminalThrow(
                workflow,
                "if ($true) { $null = 1; CoNtInUe }"),
            InsertBeforeControlledTerminalThrow(
                workflow,
                "Invoke-Expression ('con'+'tinue')"),
            InsertBeforeControlledTerminalThrow(
                workflow,
                "[Environment]::Exit(0)"),
            InsertBeforeControlledTerminalThrow(
                workflow,
                "$host.SetShouldExit(0)"),
            workflow.Replace(
                "on:\n  workflow_dispatch:",
                "on:\n  workflow_dispatch:\n  schedule:\n    - cron: '0 0 * * *'",
                StringComparison.Ordinal),
            InsertBeforeControlledTerminalThrow(
                workflow,
                "dotnet test tests.csproj --logger trx"),
            InsertBeforeControlledTerminalThrow(
                workflow,
                "dotnet.exe test tests.csproj"),
            InsertBeforeControlledTerminalThrow(
                workflow,
                "MSBUILD.EXE tests.sln"),
            InsertBeforeControlledTerminalThrow(
                workflow,
                "VSTEST.CONSOLE.EXE tests.build.appxrecipe"),
            workflow + Environment.NewLine + "dotnet build tests.csproj",
            workflow + Environment.NewLine + "vstest.console.exe tests.build.appxrecipe",
            workflow + Environment.NewLine + "uses: actions/upload-artifact@unapproved"
        ];

        foreach (string mutation in mutations)
        {
            Assert.IsNotEmpty(
                ValidateControlledEvidenceWorkflow(mutation),
                "The controlled preflight guard accepted a blocker/campaign/execution mutation.");
        }
    }

    [TestMethod]
    public void VisualStudioDebugGuideHasExactlySeventeenOrderedScopeSafeSteps()
    {
        string path = Path.Combine(
            Root,
            "docs",
            "development",
            "Model-Inspection-Visual-Studio-Debug-Guide.md");
        Assert.IsTrue(File.Exists(path), "The beginner Visual Studio guide is missing.");
        string guide = File.ReadAllText(path);
        System.Text.RegularExpressions.MatchCollection headings =
            System.Text.RegularExpressions.Regex.Matches(
                guide,
                @"^## Step (?<number>\d+):",
                System.Text.RegularExpressions.RegexOptions.Multiline |
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Assert.AreEqual(17, headings.Count, "The guide must contain exactly 17 ordered steps.");
        for (int index = 0; index < headings.Count; index++)
        {
            Assert.AreEqual(
                index + 1,
                int.Parse(
                    headings[index].Groups["number"].Value,
                    System.Globalization.CultureInfo.InvariantCulture));
        }

        string[] requiredFragments =
        [
            "IBM Granite with TurboQuant (Intel).slnx",
            "Folder View",
            "Debug",
            "x64",
            "Set as Startup Project",
            "IBM Granite with TurboQuant (Intel) (Package)",
            "MsixPackage",
            "MSBuild.exe",
            "Restore",
            "Rebuild",
            "F5",
            ".gguf",
            "Check model package",
            "Read model configuration",
            "Validate tokenizer and chat setup",
            "Validate model structure",
            "Confirm core runtime compatibility",
            "Cancel",
            "Retry",
            "Choose another",
            "animation effects",
            "keyboard-only",
            "200%",
            "High Contrast",
            "Narrator",
            "Coming later",
            "conversion/report-only states were not exercised",
            "OpenVINO",
            "TurboQuant",
            "GPU",
            "full inference",
            "context creation",
            "benchmark"
        ];
        foreach (string fragment in requiredFragments)
        {
            StringAssert.Contains(guide, fragment, $"The exact 17-step guide is missing: {fragment}");
        }

        const string PackagedProfile = "IBM Granite with TurboQuant (Intel) (Package)";
        string launchSettings = File.ReadAllText(Path.Combine(
            Root,
            "IBM Granite with TurboQuant (Intel)",
            "Properties",
            "launchSettings.json"));
        using System.Text.Json.JsonDocument launchSettingsDocument =
            System.Text.Json.JsonDocument.Parse(launchSettings);
        Assert.IsTrue(
            launchSettingsDocument.RootElement
                .GetProperty("profiles")
                .TryGetProperty(PackagedProfile, out System.Text.Json.JsonElement profile),
            "The guide's packaged launch profile must exist in launchSettings.json.");
        Assert.AreEqual(
            "MsixPackage",
            profile.GetProperty("commandName").GetString(),
            "The documented packaged profile must use Visual Studio's MSIX launch command.");
    }

    [TestMethod]
    public void FigmaVisualEvidenceKeepsExactlyThirteenStrictRowsOpen()
    {
        string path = Path.Combine(
            Root,
            "docs",
            "evidence",
            "testing",
            "Model-Inspection-Figma-Visual-Verification.md");
        Assert.IsTrue(File.Exists(path), "The truthful Figma visual evidence record is missing.");
        string evidence = File.ReadAllText(path);
        System.Text.RegularExpressions.MatchCollection rows =
            System.Text.RegularExpressions.Regex.Matches(
                evidence,
                @"^\| (?<state>\d{2}) \|",
                System.Text.RegularExpressions.RegexOptions.Multiline |
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Assert.AreEqual(13, rows.Count, "The evidence record must list exactly 13 ordered state rows.");
        for (int index = 0; index < rows.Count; index++)
        {
            Assert.AreEqual(
                index + 1,
                int.Parse(
                    rows[index].Groups["state"].Value,
                    System.Globalization.CultureInfo.InvariantCulture));
        }

        Assert.AreEqual(
            13,
            CountOccurrences(
                evidence,
                "Not available - exact Figma node export absent"));
        Assert.AreEqual(
            13,
            CountOccurrences(
                evidence,
                "Not run - strict regression class absent"));
        string[] requiredFragments =
        [
            "total `686`, executed `686`, passed `686`",
            "failed/error/timeout/aborted/inconclusive/not-executed `0`",
            "classes 286/286",
            "journey 1/1",
            "`task-12-final-hosted-equivalent-packaged.trx`",
            "Base HEAD `5f90a5d9299363214f11454f548ff8571d98b1a5`",
            "dirty working-tree candidate",
            "local hosted-equivalent candidate",
            "hosted exact-head remains open",
            "8,190,259 bytes",
            "8A171A3A1DF66D158990789A368C439752EBE5309B364A870E0C0107B519B7EB",
            "NOT RUN - controlled operator evidence absent",
            "raw TRX was not retained or staged",
            "DoD 2, 8, 11, 12, and 13 remain open"
        ];
        foreach (string fragment in requiredFragments)
        {
            StringAssert.Contains(evidence, fragment, $"The evidence record is missing: {fragment}");
        }
        Assert.AreEqual(
            6,
            CountOccurrences(
                evidence,
                "`NOT CAPTURED - ordinary local run, not controlled evidence`"),
            "The six uncontrolled environment settings must each remain explicitly uncaptured.");
    }

    [TestMethod]
    public void Task12WorkflowGuideAndEvidenceUseAsciiPunctuation()
    {
        string[] relativePaths =
        [
            ".github/workflows/model-inspection-visual-regression.yml",
            "docs/development/Model-Inspection-Visual-Studio-Debug-Guide.md",
            "docs/evidence/testing/Model-Inspection-Figma-Visual-Verification.md"
        ];
        foreach (string relativePath in relativePaths)
        {
            string text = File.ReadAllText(Path.Combine(
                Root,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            Assert.IsFalse(
                text.Contains('\u2014'),
                $"Task 12 files use ASCII punctuation so encoding cannot corrupt them: {relativePath}");
            Assert.IsFalse(
                text.Contains("â", StringComparison.Ordinal),
                $"Task 12 file contains a mojibake lead character: {relativePath}");
        }
    }

    [TestMethod]
    public void Task12DocumentGuardsRejectStepRowAndAbsolutePathMutations()
    {
        string guide = File.ReadAllText(Path.Combine(
            Root,
            "docs",
            "development",
            "Model-Inspection-Visual-Studio-Debug-Guide.md"));
        string evidence = File.ReadAllText(Path.Combine(
            Root,
            "docs",
            "evidence",
            "testing",
            "Model-Inspection-Figma-Visual-Verification.md"));
        Assert.AreEqual(
            0,
            ValidateVisualStudioGuide(guide).Length,
            string.Join(Environment.NewLine, ValidateVisualStudioGuide(guide)));
        Assert.AreEqual(
            0,
            ValidateFigmaVisualEvidence(evidence).Length,
            string.Join(Environment.NewLine, ValidateFigmaVisualEvidence(evidence)));

        string[] guideMutations =
        [
            "Coming later" + Environment.NewLine + guide.Replace(
                    "expose `Coming later` help",
                    "expose future-action help",
                    StringComparison.Ordinal),
            guide.Replace(
                "IBM Granite with TurboQuant (Intel) (Package)",
                "IBM Granite with TurboQuant (Intel) (Unpackaged)",
                StringComparison.Ordinal),
            guide.Replace(
                "does not add or validate OpenVINO",
                "adds and validates OpenVINO",
                StringComparison.Ordinal),
            guide.Replace(
                    "does not add or validate OpenVINO",
                    "adds and validates OpenVINO",
                    StringComparison.Ordinal) +
                Environment.NewLine +
                "<!-- It does not add or validate OpenVINO, TurboQuant, GPU execution, full inference, context creation, conversion, report export, or a benchmark. -->",
            guide.Replace(
                    "does not add or validate OpenVINO",
                    "adds and validates OpenVINO",
                    StringComparison.Ordinal) +
                Environment.NewLine +
                "~~~text" + Environment.NewLine +
                "It does not add or validate OpenVINO, TurboQuant, GPU execution, full inference, context creation, conversion, report export, or a benchmark." +
                Environment.NewLine + "~~~",
            guide.Replace(
                    "does not add or validate OpenVINO",
                    "adds and validates OpenVINO",
                    StringComparison.Ordinal) +
                Environment.NewLine +
                "~~It does not add or validate OpenVINO, TurboQuant, GPU execution, full inference, context creation, conversion, report export, or a benchmark.~~",
            guide.Replace(
                    "does not add or validate OpenVINO",
                    "adds and validates OpenVINO",
                    StringComparison.Ordinal) +
                Environment.NewLine +
                "<del>It does not add or validate OpenVINO, TurboQuant, GPU execution, full inference, context creation, conversion, report export, or a benchmark.</del>",
            guide.Replace(
                    "does not add or validate OpenVINO",
                    "adds and validates OpenVINO",
                    StringComparison.Ordinal) +
                Environment.NewLine +
                "<s>It does not add or validate OpenVINO, TurboQuant, GPU execution, full inference, context creation, conversion, report export, or a benchmark.</s>",
            guide.Replace(
                    "does not add or validate OpenVINO",
                    "adds and validates OpenVINO",
                    StringComparison.Ordinal) +
                Environment.NewLine +
                "[scope]: # \"It does not add or validate OpenVINO, TurboQuant, GPU execution, full inference, context creation, conversion, report export, or a benchmark.\"",
            guide + Environment.NewLine + "/workspace/arian/private-model.gguf",
            guide + Environment.NewLine + @"\Users\Arian\private-model.gguf",
            guide + Environment.NewLine + @"C:\Users\private\model.gguf"
        ];
        foreach (string mutation in guideMutations)
        {
            Assert.IsNotEmpty(
                ValidateVisualStudioGuide(mutation),
                "The guide guard accepted moved step content or an absolute path.");
        }
        foreach (string htmlMutation in guideMutations.Where(
                     mutation => mutation.Contains("<del>", StringComparison.Ordinal) ||
                         mutation.Contains("<s>", StringComparison.Ordinal)))
        {
            Assert.IsTrue(
                ValidateVisualStudioGuide(htmlMutation).Any(
                    error => error.Contains("raw HTML tag", StringComparison.Ordinal)),
                "The guide's raw-HTML mutation must fail specifically at the visible-Markdown boundary.");
        }
        Assert.IsTrue(
            ValidateVisualStudioGuide(guideMutations.Single(
                    mutation => mutation.Contains("[scope]:", StringComparison.Ordinal)))
                .Any(error => error.Contains(
                    "link-reference definition",
                    StringComparison.Ordinal)),
            "The guide link-reference mutation must fail specifically at the plain-paragraph boundary.");

        string[] evidenceMutations =
        [
            evidence.Replace(
                "| 01 | `142:2151` | Inspection progress |",
                "| 01 | `142:2213` | Inspection progress |",
                StringComparison.Ordinal),
            evidence.Replace(
                "| 02 | `142:2213` | Ready, collapsed |",
                "| 02 | `142:2213` | Ready, expanded |",
                StringComparison.Ordinal),
            evidence.Replace(
                "task-12-final-hosted-equivalent-packaged.trx",
                "renamed-result.trx",
                StringComparison.Ordinal),
            evidence.Replace(
                "| OS build | `NOT CAPTURED - ordinary local run, not controlled evidence` |",
                "| OS build | `22631` |",
                StringComparison.Ordinal),
            evidence + Environment.NewLine + "/workspace/arian/private-result.trx",
            evidence + Environment.NewLine + @"\Users\Arian\private-result.trx",
            evidence + Environment.NewLine +
                "file:/workspace/arian/private-result.trx",
            evidence + Environment.NewLine +
                "file:///workspace/arian/private-result.trx",
            evidence + Environment.NewLine +
                @"file:\workspace\arian\private-result.trx",
            evidence + Environment.NewLine +
                "file:C:/Users/Arian/private-result.trx",
            evidence.Replace(
                "No OpenVINO, TurboQuant,",
                "OpenVINO, TurboQuant,",
                StringComparison.Ordinal),
            evidence.Replace(
                    "No OpenVINO, TurboQuant,",
                    "OpenVINO, TurboQuant,",
                    StringComparison.Ordinal) +
                Environment.NewLine +
                "<!-- Consequently DoD 2, 8, 11, 12, and 13 remain open. No claim is made for strict Figma pixel fidelity, actual controlled High Contrast/200% behavior, manual Narrator acceptance, or hosted exact-head closure. No OpenVINO, TurboQuant, Vulkan/GPU, full inference/context, Hardware Fit, conversion execution, report export, benchmark, extracted-MSIX, or real external-model evidence was produced. -->",
            evidence.Replace(
                    "No OpenVINO, TurboQuant,",
                    "OpenVINO, TurboQuant,",
                    StringComparison.Ordinal) +
                Environment.NewLine +
                "~~~text" + Environment.NewLine +
                "Consequently DoD 2, 8, 11, 12, and 13 remain open. No claim is made for strict Figma pixel fidelity, actual controlled High Contrast/200% behavior, manual Narrator acceptance, or hosted exact-head closure. No OpenVINO, TurboQuant, Vulkan/GPU, full inference/context, Hardware Fit, conversion execution, report export, benchmark, extracted-MSIX, or real external-model evidence was produced." +
                Environment.NewLine + "~~~",
            evidence.Replace(
                    "No OpenVINO, TurboQuant,",
                    "OpenVINO, TurboQuant,",
                    StringComparison.Ordinal) +
                Environment.NewLine +
                "~~Consequently DoD 2, 8, 11, 12, and 13 remain open. No claim is made for strict Figma pixel fidelity, actual controlled High Contrast/200% behavior, manual Narrator acceptance, or hosted exact-head closure. No OpenVINO, TurboQuant, Vulkan/GPU, full inference/context, Hardware Fit, conversion execution, report export, benchmark, extracted-MSIX, or real external-model evidence was produced.~~",
            evidence.Replace(
                    "No OpenVINO, TurboQuant,",
                    "OpenVINO, TurboQuant,",
                    StringComparison.Ordinal) +
                Environment.NewLine +
                "<del>Consequently DoD 2, 8, 11, 12, and 13 remain open. No claim is made for strict Figma pixel fidelity, actual controlled High Contrast/200% behavior, manual Narrator acceptance, or hosted exact-head closure. No OpenVINO, TurboQuant, Vulkan/GPU, full inference/context, Hardware Fit, conversion execution, report export, benchmark, extracted-MSIX, or real external-model evidence was produced.</del>",
            evidence.Replace(
                    "No OpenVINO, TurboQuant,",
                    "OpenVINO, TurboQuant,",
                    StringComparison.Ordinal) +
                Environment.NewLine +
                "<s>Consequently DoD 2, 8, 11, 12, and 13 remain open. No claim is made for strict Figma pixel fidelity, actual controlled High Contrast/200% behavior, manual Narrator acceptance, or hosted exact-head closure. No OpenVINO, TurboQuant, Vulkan/GPU, full inference/context, Hardware Fit, conversion execution, report export, benchmark, extracted-MSIX, or real external-model evidence was produced.</s>",
            evidence.Replace(
                    "No OpenVINO, TurboQuant,",
                    "OpenVINO, TurboQuant,",
                    StringComparison.Ordinal) +
                Environment.NewLine +
                "[scope]: # \"Consequently DoD 2, 8, 11, 12, and 13 remain open. No claim is made for strict Figma pixel fidelity, actual controlled High Contrast/200% behavior, manual Narrator acceptance, or hosted exact-head closure. No OpenVINO, TurboQuant, Vulkan/GPU, full inference/context, Hardware Fit, conversion execution, report export, benchmark, extracted-MSIX, or real external-model evidence was produced.\"",
            evidence + Environment.NewLine + "D:/s/private/result.trx"
        ];
        foreach (string mutation in evidenceMutations)
        {
            Assert.IsNotEmpty(
                ValidateFigmaVisualEvidence(mutation),
                "The evidence guard accepted a row/node/cell or absolute-path mutation.");
        }
        foreach (string htmlMutation in evidenceMutations.Where(
                     mutation => mutation.Contains("<del>", StringComparison.Ordinal) ||
                         mutation.Contains("<s>", StringComparison.Ordinal)))
        {
            Assert.IsTrue(
                ValidateFigmaVisualEvidence(htmlMutation).Any(
                    error => error.Contains("raw HTML tag", StringComparison.Ordinal)),
                "The evidence raw-HTML mutation must fail specifically at the visible-Markdown boundary.");
        }
        Assert.IsTrue(
            ValidateFigmaVisualEvidence(evidenceMutations.Single(
                    mutation => mutation.Contains("[scope]:", StringComparison.Ordinal)))
                .Any(error => error.Contains(
                    "link-reference definition",
                    StringComparison.Ordinal)),
            "The evidence link-reference mutation must fail specifically at the plain-paragraph boundary.");
    }

    [TestMethod]
    public void SharedWinUiStateTestClassesAreNotParallelized()
    {
        string[] serializedTestPaths =
        [
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageLayoutTests.cs",
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs",
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionActionCardTests.cs",
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs",
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionDisclosureTests.cs",
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionModelCardTests.cs",
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionOutcomeCardTests.cs",
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/ModelInspectionDisclosureTests.cs",
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionMotionTests.cs",
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionAccessibilityTests.cs",
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs",
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderHarnessTests.cs"
        ];

        foreach (string relativePath in serializedTestPaths)
        {
            string source = File.ReadAllText(Path.Combine(
                Root,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            Assert.IsTrue(
                HasClassLevelDoNotParallelize(source),
                $"The shared Window/composition/disclosure test class must carry a class-level [DoNotParallelize]: {relativePath}");
        }
    }

    [TestMethod]
    public void NewlyDiscoveredWindowCompositionDisclosureAndControlledOsClassesAreNotParallelized()
    {
        string[] discoveryRoots =
        [
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection",
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding"
        ];
        string[] applicablePaths = discoveryRoots
            .Select(root => Path.Combine(
                Root,
                root.Replace('/', Path.DirectorySeparatorChar)))
            .SelectMany(root => Directory.EnumerateFiles(
                root,
                "*Tests.cs",
                SearchOption.AllDirectories))
            .Where(path => IsSharedWinUiStateTestSource(
                path,
                File.ReadAllText(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.IsNotEmpty(
            applicablePaths,
            "The Window/composition/disclosure discovery unexpectedly found no test classes.");
        foreach (string path in applicablePaths)
        {
            string source = File.ReadAllText(path);
            Assert.IsTrue(
                HasClassLevelDoNotParallelize(source),
                $"A discovered shared-state test class is missing class-level [DoNotParallelize]: {Path.GetRelativePath(Root, path)}");
        }

        const string SerializedSource =
            "[TestClass]\n" +
            "[DoNotParallelize]\n" +
            "public sealed class SyntheticTests { }";
        Assert.IsTrue(HasClassLevelDoNotParallelize(SerializedSource));
        Assert.IsFalse(
            HasClassLevelDoNotParallelize(SerializedSource.Replace(
                "[DoNotParallelize]",
                "// [DoNotParallelize]",
                StringComparison.Ordinal)),
            "A commented-out MSTest attribute must not satisfy the serialization guard.");

        const string TargetTypedWindowSource =
            "[TestClass]\n" +
            "public sealed class FutureWindowTests {\n" +
            "  public void Render() { Window window = new(); }\n" +
            "}";
        Assert.IsTrue(
            IsSharedWinUiStateTestSource(
                "FutureWindowTests.cs",
                TargetTypedWindowSource),
            "Target-typed Window construction must enter shared-state discovery.");

        const string RenderHostSource =
            "[TestClass]\n" +
            "public sealed class FutureRenderTests {\n" +
            "  public async Task Render() { await WinUiRenderHost.ShowAsync(page); }\n" +
            "}";
        Assert.IsTrue(
            IsSharedWinUiStateTestSource(
                "FutureRenderTests.cs",
                RenderHostSource),
            "WinUiRenderHost users must enter shared-state discovery.");

        const string CompositionRenderingSource =
            "[TestClass]\n" +
            "public sealed class FutureCompositionTests {\n" +
            "  public void Render() { CompositionTarget.Rendering += OnRendering; }\n" +
            "}";
        Assert.IsTrue(
            IsSharedWinUiStateTestSource(
                "FutureCompositionTests.cs",
                CompositionRenderingSource),
            "CompositionTarget.Rendering users must enter shared-state discovery.");

        const string FullyQualifiedWindowSource =
            "[TestClass]\n" +
            "public sealed class FutureQualifiedWindowTests {\n" +
            "  public void Render() { var window = new Microsoft.UI.Xaml.Window(); }\n" +
            "}";
        Assert.IsTrue(
            IsSharedWinUiStateTestSource(
                "FutureQualifiedWindowTests.cs",
                FullyQualifiedWindowSource),
            "Fully-qualified Window construction must enter shared-state discovery.");

        const string GlobalQualifiedWindowSource =
            "[TestClass] public sealed class FutureGlobalWindowTests { " +
            "public void Render() { var window = new global::Microsoft.UI.Xaml.Window(); } }";
        Assert.IsTrue(
            IsSharedWinUiStateTestSource(
                "FutureGlobalWindowTests.cs",
                GlobalQualifiedWindowSource),
            "The exact global-qualified Window construction must enter shared-state discovery.");
        Assert.IsFalse(
            HasClassLevelDoNotParallelize(GlobalQualifiedWindowSource),
            "A global-qualified Window test class without [DoNotParallelize] must fail serialization.");

        const string MultipleTestClassesSource =
            "[TestClass]\n" +
            "[DoNotParallelize]\n" +
            "public sealed class FirstTests { }\n" +
            "[TestClass]\n" +
            "public sealed class SecondTests { }\n";
        Assert.IsFalse(
            HasClassLevelDoNotParallelize(MultipleTestClassesSource),
            "Every test class in a shared-state source file must carry [DoNotParallelize].");

        const string SameLineParallelClassSource =
            "[TestClass] [DoNotParallelize] public sealed class FirstInlineTests { } " +
            "[TestClass] public sealed class ParallelWindowTests { " +
            "[UITestMethod] public void Render() { var window = new Microsoft.UI.Xaml.Window(); } }";
        Assert.IsFalse(
            HasClassLevelDoNotParallelize(SameLineParallelClassSource),
            "A same-line second shared-state test class must carry its own [DoNotParallelize].");

        const string QualifiedSerializedSource =
            "[Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute] " +
            "[Microsoft.VisualStudio.TestTools.UnitTesting.DoNotParallelizeAttribute] " +
            "public sealed class QualifiedSerializedTests { " +
            "[UITestMethod] public void Render() { CompositionTarget.Rendering += OnRender; } }";
        Assert.IsTrue(
            HasClassLevelDoNotParallelize(QualifiedSerializedSource),
            "Qualified MSTest attribute forms must satisfy the per-class serialization guard.");

        const string GlobalQualifiedSerializedSource =
            "[global::Microsoft.VisualStudio.TestTools.UnitTesting.TestClass] " +
            "[global::Microsoft.VisualStudio.TestTools.UnitTesting.DoNotParallelize] " +
            "public sealed class GlobalQualifiedTests { " +
            "[global::Microsoft.VisualStudio.TestTools.UnitTesting.UITestMethodAttribute] " +
            "public void Render() { CompositionTarget.Rendering += OnRender; } }";
        Assert.IsTrue(
            HasClassLevelDoNotParallelize(GlobalQualifiedSerializedSource),
            "The exact global-qualified MSTest attribute forms must remain supported.");

        foreach (string fakeAttribute in new[]
                 {
                     "Fake.DoNotParallelize",
                     "Fake.DoNotParallelizeAttribute"
                 })
        {
            string fakeSerializedSource =
                $"[TestClass] [{fakeAttribute}] public sealed class FakeOwnedTests {{ " +
                "[UITestMethod] public void Render() { Window window = new(); } }";
            Assert.IsFalse(
                HasClassLevelDoNotParallelize(fakeSerializedSource),
                $"A non-MSTest attribute must not satisfy serialization ownership: {fakeAttribute}");
        }

        const string UiMethodOnlySecondClassSource =
            "[TestClass] [DoNotParallelize] public sealed class FirstGuardedTests { } " +
            "public sealed class UiMethodOnlyWindowTests { " +
            "[UITestMethod] public void Render() { Window window = new(); } }";
        Assert.IsFalse(
            HasClassLevelDoNotParallelize(UiMethodOnlySecondClassSource),
            "A class segment with UITestMethod/shared-state markers must carry [DoNotParallelize].");

        string controlledSourcePath = Path.Combine(
            Root,
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionControlledAccessibilityTests.cs"
                .Replace('/', Path.DirectorySeparatorChar));
        Assert.IsFalse(
            File.Exists(controlledSourcePath),
            "The future controlled OS class entered the tree without updating the fail-closed visual workflow and exact execution evidence.");
    }

    private static string[] ValidateHostedPackagedWorkflow(string workflow)
    {
        var errors = new List<string>();
        string? step = TryExtractWorkflowStep(
            workflow,
            "Run unit and WinUI UI-thread tests");
        if (step is null)
        {
            errors.Add("The packaged test step is missing.");
            return errors.ToArray();
        }

        string normalizedStep = step.Replace(
            "\r\n",
            "\n",
            StringComparison.Ordinal);
        string executableStep = RemovePowerShellNonExecutableText(
            normalizedStep);
        string stepHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(normalizedStep.Trim())));
        if (!string.Equals(
                stepHash,
                HostedPackagedStepSha256,
                StringComparison.Ordinal))
        {
            errors.Add(
                $"The executable packaged gate step drifted from its reviewed structure: {stepHash}.");
        }

        if (CountOccurrences(executableStep, HostedPackagedFilter) != 1)
        {
            errors.Add("The packaged step must use the exact hosted-equivalent two-category exclusion once.");
        }

        string floor = $"$minimumExpectedTests = {HostedPackagedFloor}";
        System.Text.RegularExpressions.MatchCollection floorAssignments =
            System.Text.RegularExpressions.Regex.Matches(
                executableStep,
                @"(?m)^[ \t]*(?:\+\+|--)?\$minimumExpectedTests(?:\+\+|--|[ \t]*(?:=|\+=|-=|\*=|/=))",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (CountOccurrences(executableStep, floor) != 1 ||
            floorAssignments.Count != 1)
        {
            errors.Add(
                $"The packaged step must assign the observed {HostedPackagedFloor}-test floor exactly once without later mutation.");
        }

        const string MapMarker = "$protectedClassCounts = [ordered]@{";
        int mapStart = executableStep.IndexOf(MapMarker, StringComparison.Ordinal);
        int mapEnd = mapStart < 0
            ? -1
            : executableStep.IndexOf(
                "foreach ($entry in $protectedClassCounts.GetEnumerator())",
                mapStart,
                StringComparison.Ordinal);
        if (mapStart < 0 || mapEnd <= mapStart)
        {
            errors.Add("The exact protected-class map is missing.");
        }
        else
        {
            string map = executableStep[mapStart..mapEnd];
            foreach ((string className, int count) in HostedProtectedClassCounts)
            {
                string entry = $"'{className}' = {count}";
                if (CountOccurrences(map, entry) != 1)
                {
                    errors.Add($"The exact protected class/count entry drifted: {entry}");
                }
            }

            int entryCount = map
                .Split('\n')
                .Count(line => line.TrimStart().StartsWith(
                    "'GraniteEdgeAI.",
                    StringComparison.Ordinal));
            if (entryCount != HostedProtectedClassCounts.Length)
            {
                errors.Add(
                    $"The protected-class map must contain exactly {HostedProtectedClassCounts.Length} entries, found {entryCount}.");
            }

            string[] blockedStrictClasses =
            [
                "ModelInspectionVisualReferenceIntegrityTests",
                "ModelInspectionVisualRegressionTests",
                "ModelInspectionControlledAccessibilityTests"
            ];
            foreach (string blockedClass in blockedStrictClasses)
            {
                if (map.Contains(blockedClass, StringComparison.Ordinal))
                {
                    errors.Add($"The ordinary workflow falsely claims the absent strict class: {blockedClass}");
                }
            }

            System.Text.RegularExpressions.MatchCollection mapAssignments =
                System.Text.RegularExpressions.Regex.Matches(
                    executableStep,
                    @"(?m)^[ \t]*\$protectedClassCounts[ \t]*=",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (mapAssignments.Count != 1 ||
                System.Text.RegularExpressions.Regex.IsMatch(
                    executableStep,
                    @"(?i)\$protectedClassCounts\s*\.(?:Clear|Add|Remove|RemoveAt|TryAdd)\s*\(",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant) ||
                System.Text.RegularExpressions.Regex.IsMatch(
                    executableStep,
                    @"(?im)^\s*\$protectedClassCounts\s*\[[^\]]+\]\s*=",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            {
                errors.Add(
                    "The protected class/count map must remain immutable from its single assignment through its verification loop.");
            }
        }

        string[] prohibitedRawRetention =
        [
            "Upload unit-test results",
            "Upload Gate 2 verification results",
            "gate2-verification-",
            "TestResults/UnitTests/**/*.trx",
            "TestResults\\UnitTests\\**\\*.trx"
        ];
        foreach (string prohibited in prohibitedRawRetention)
        {
            if (workflow.Contains(prohibited, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"The permanent workflow must not retain raw identity-bearing unit TRX: {prohibited}");
            }
        }

        if (CountOccurrences(workflow, "GraniteEdgeAI.UnitTests.trx") != 2)
        {
            errors.Add(
                "The packaged TRX filename must occur only in its logger and local parser contexts.");
        }

        if (workflow.Contains(
                "actions/upload-artifact@",
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                "The permanent workflow must not upload any raw test artifact until a complete sanitizer and gated publish directory exist.");
        }

        return errors.ToArray();
    }

    private static string[] ValidateVisualStudioGuide(string guide)
    {
        var errors = new List<string>();
        string visibleGuide = GetVisibleMarkdown(
            guide,
            errors,
            "The Visual Studio guide");
        System.Text.RegularExpressions.MatchCollection headings =
            System.Text.RegularExpressions.Regex.Matches(
                visibleGuide,
                @"^## Step (?<number>\d+):",
                System.Text.RegularExpressions.RegexOptions.Multiline |
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (headings.Count != 17)
        {
            errors.Add("The guide must contain exactly 17 ordered steps.");
            return errors.ToArray();
        }

        string[][] fragmentsByStep =
        [
            ["IBM Granite with TurboQuant (Intel).slnx", "Folder View"],
            ["Debug", "x64"],
            ["WinUI", "Set as Startup Project"],
            [
                "IBM Granite with TurboQuant (Intel) (Package)",
                "packaged Local Machine",
                "MsixPackage"
            ],
            ["Debug > Stop Debugging", "MSBuild.exe", "ModelInspection.Worker", "ProtocolTestWorker"],
            ["Restore NuGet Packages", "Rebuild Solution"],
            ["F5", "Start Debugging"],
            ["valid local `.gguf`", "full path", "external Granite model"],
            [
                "Check model package",
                "Read model configuration",
                "Validate tokenizer and chat setup",
                "Validate model structure",
                "Confirm core runtime compatibility"
            ],
            ["Expand", "Collapse", "without replacing", "focus"],
            ["Cancel", "Retry", "Choose another"],
            ["Animation effects", "off", "same state and focus endpoints"],
            ["keyboard-only", "Tab", "Enter or Space", "disabled future actions"],
            ["200%", "critical clipping", "Restore"],
            ["High Contrast", "visible focus", "Restore"],
            ["Narrator", "NOT RUN", "polite", "terminal region"],
            [
                "Ready or Ready-with-warnings",
                "Coming later",
                "Hardware Fit",
                "conversion",
                "report",
                "conversion/report-only states were not exercised"
            ]
        ];

        for (int index = 0; index < headings.Count; index++)
        {
            int number = int.Parse(
                headings[index].Groups["number"].Value,
                System.Globalization.CultureInfo.InvariantCulture);
            if (number != index + 1)
            {
                errors.Add($"Guide step {index + 1} is numbered {number}.");
            }

            int start = headings[index].Index;
            int end = index + 1 < headings.Count
                ? headings[index + 1].Index
                : visibleGuide.Length;
            string step = System.Text.RegularExpressions.Regex.Replace(
                visibleGuide[start..end],
                @"\s+",
                " ",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            foreach (string fragment in fragmentsByStep[index])
            {
                if (!step.Contains(fragment, StringComparison.Ordinal))
                {
                    errors.Add($"Guide step {index + 1} is missing: {fragment}");
                }
            }
        }

        foreach (string fragment in new[]
                 {
                     "OpenVINO",
                     "TurboQuant",
                     "GPU",
                     "full inference",
                     "context creation",
                     "benchmark"
                 })
        {
            if (!visibleGuide.Contains(fragment, StringComparison.Ordinal))
            {
                errors.Add($"The guide scope boundary is missing: {fragment}");
            }
        }

        const string ExpectedGuideIntroductionParagraph =
            "This beginner guide exercises the existing packaged WinUI Model Inspection " +
            "journey on the x64 GGUF, CPU-only, LLamaSharp/llama.cpp `VocabOnly` path. " +
            "It does not add or validate OpenVINO, TurboQuant, GPU execution, full inference, " +
            "context creation, conversion, report export, or a benchmark.";
        if (!HasExactPlainParagraphInSection(
                visibleGuide,
                "# Model Inspection Visual Studio Debug guide",
                "## Step 1:",
                ExpectedGuideIntroductionParagraph))
        {
            errors.Add(
                "The guide introduction must retain its exact negative scope claim as one standalone plain paragraph.");
        }

        const string ExpectedGuideClosingParagraph =
            "One developer-machine pass cannot prove exact Figma pixels, actual controlled " +
            "High Contrast or 200% evidence on an approved pinned runner, Narrator acceptance " +
            "that was not recorded, or hosted exact-head CI. It also cannot be used to claim " +
            "OpenVINO, TurboQuant, GPU, full inference, context creation, Hardware Fit, " +
            "conversion execution, report export, or benchmark support.";
        if (!HasExactPlainParagraphInSection(
                visibleGuide,
                "## What this journey cannot close",
                null,
                ExpectedGuideClosingParagraph))
        {
            errors.Add(
                "The guide closing section must retain its exact negative scope claim as one standalone plain paragraph.");
        }

        if (ContainsPrivateAbsolutePath(guide))
        {
            errors.Add("The guide contains a machine/user absolute path.");
        }

        return errors.ToArray();
    }

    private static string[] ValidateFigmaVisualEvidence(string evidence)
    {
        var errors = new List<string>();
        string visibleEvidence = GetVisibleMarkdown(
            evidence,
            errors,
            "The Figma evidence record");
        (string State, string Node, string Presentation)[] expectedRows =
        [
            ("01", "142:2151", "Inspection progress"),
            ("02", "142:2213", "Ready, collapsed"),
            ("03", "142:2280", "Ready, expanded"),
            ("04", "142:2403", "Ready with warnings, collapsed"),
            ("05", "142:2476", "Ready with warnings, expanded"),
            ("06", "142:2599", "Conversion required, collapsed"),
            ("07", "142:2664", "Conversion required, expanded"),
            ("08", "142:2787", "Incomplete package"),
            ("09", "142:2851", "Unsupported model"),
            ("10", "142:2910", "Invalid model, collapsed"),
            ("11", "142:2973", "Invalid model, expanded"),
            ("12", "142:3096", "Inspection cancelled"),
            ("13", "142:3154", "Inspection operational failure")
        ];
        System.Text.RegularExpressions.MatchCollection rows =
            System.Text.RegularExpressions.Regex.Matches(
                visibleEvidence,
                @"^\| (?<state>\d{2}) \| `(?<node>\d+:\d+)` \| (?<presentation>[^|]+?) \| (?<reference>[^|]+?) \| (?<actual>[^|]+?) \| (?<result>[^|]+?) \|[ \t]*$",
                System.Text.RegularExpressions.RegexOptions.Multiline |
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (rows.Count != expectedRows.Length)
        {
            errors.Add($"The evidence record must contain exactly {expectedRows.Length} strict rows.");
        }
        else
        {
            for (int index = 0; index < rows.Count; index++)
            {
                (string state, string node, string presentation) = expectedRows[index];
                System.Text.RegularExpressions.Match row = rows[index];
                if (!string.Equals(row.Groups["state"].Value, state, StringComparison.Ordinal) ||
                    !string.Equals(row.Groups["node"].Value, node, StringComparison.Ordinal) ||
                    !string.Equals(row.Groups["presentation"].Value.Trim(), presentation, StringComparison.Ordinal) ||
                    !string.Equals(
                        row.Groups["reference"].Value.Trim(),
                        "Not available - exact Figma node export absent",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        row.Groups["actual"].Value.Trim(),
                        "Not run - strict regression class absent",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        row.Groups["result"].Value.Trim(),
                        "OPEN / BLOCKED",
                        StringComparison.Ordinal))
                {
                    errors.Add($"Strict evidence row {state} does not match its exact node/cells.");
                }
            }
        }

        foreach (string fragment in new[]
                 {
                     "total `686`, executed `686`, passed `686`",
                     "failed/error/timeout/aborted/inconclusive/not-executed `0`",
                     "classes 286/286",
                     "journey 1/1",
                     "`task-12-final-hosted-equivalent-packaged.trx`",
                     "Base HEAD `5f90a5d9299363214f11454f548ff8571d98b1a5`",
                     "dirty working-tree candidate",
                     "local hosted-equivalent candidate",
                     "hosted exact-head remains open",
                     "8,190,259 bytes",
                     "8A171A3A1DF66D158990789A368C439752EBE5309B364A870E0C0107B519B7EB",
                     "- Figma file: `gAmBX1DYh71hqxHVqiivus`",
                     "NOT RUN - controlled operator evidence absent",
                     "raw TRX was not retained or staged",
                     "DoD 2, 8, 11, 12, and 13 remain open"
                 })
        {
            if (!visibleEvidence.Contains(fragment, StringComparison.Ordinal))
            {
                errors.Add($"The evidence record is missing: {fragment}");
            }
        }

        if (CountOccurrences(
                visibleEvidence,
                "- Figma file: `gAmBX1DYh71hqxHVqiivus`") != 1)
        {
            errors.Add("The durable Figma metadata label/value must occur exactly once.");
        }

        const string ExactNegativeEvidenceParagraph =
            "Consequently DoD 2, 8, 11, 12, and 13 remain open. " +
            "No claim is made for strict Figma pixel fidelity, actual controlled High Contrast/200% behavior, manual Narrator acceptance, or hosted exact-head closure. " +
            "No OpenVINO, TurboQuant, Vulkan/GPU, full inference/context, Hardware Fit, conversion execution, report export, benchmark, extracted-MSIX, or real external-model evidence was produced.";
        if (!HasExactPlainParagraphInSection(
                visibleEvidence,
                "## Controlled and manual evidence still open",
                null,
                ExactNegativeEvidenceParagraph))
        {
            errors.Add(
                "The controlled/manual section must retain its exact strict/backend negative claim as one standalone plain paragraph.");
        }

        string[] uncontrolledSettings =
        [
            "OS build",
            "Resolution",
            "DPI",
            "Text scale",
            "Theme",
            "Animations"
        ];
        foreach (string setting in uncontrolledSettings)
        {
            string row =
                $"| {setting} | `NOT CAPTURED - ordinary local run, not controlled evidence` |";
            if (CountOccurrences(visibleEvidence, row) != 1)
            {
                errors.Add($"The ordinary evidence setting must remain explicitly uncaptured: {setting}");
            }
        }
        if (CountOccurrences(
                visibleEvidence,
                "`NOT CAPTURED - ordinary local run, not controlled evidence`") !=
            uncontrolledSettings.Length)
        {
            errors.Add("The evidence record must contain exactly six uncaptured setting values.");
        }

        if (ContainsPrivateAbsolutePath(evidence))
        {
            errors.Add("The evidence record contains a machine/user absolute path.");
        }

        return errors.ToArray();
    }

    private static bool HasExactPlainParagraphInSection(
        string markdown,
        string sectionHeading,
        string? sectionEndHeadingPrefix,
        string expectedParagraph)
    {
        string normalized = markdown.Replace(
            "\r\n",
            "\n",
            StringComparison.Ordinal);
        System.Text.RegularExpressions.MatchCollection sectionHeadings =
            System.Text.RegularExpressions.Regex.Matches(
                normalized,
                "(?m)^" +
                    System.Text.RegularExpressions.Regex.Escape(sectionHeading) +
                    "[ \\t]*$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (sectionHeadings.Count != 1)
        {
            return false;
        }

        int contentStart = sectionHeadings[0].Index +
            sectionHeadings[0].Length;
        if (contentStart < normalized.Length &&
            normalized[contentStart] == '\n')
        {
            contentStart++;
        }

        int contentEnd = normalized.Length;
        if (sectionEndHeadingPrefix is not null)
        {
            System.Text.RegularExpressions.Match endHeading =
                System.Text.RegularExpressions.Regex.Match(
                    normalized[contentStart..],
                    "(?m)^" +
                        System.Text.RegularExpressions.Regex.Escape(
                            sectionEndHeadingPrefix) +
                        ".*$",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (!endHeading.Success)
            {
                return false;
            }

            contentEnd = contentStart + endHeading.Index;
        }
        else
        {
            int headingLevel = sectionHeading.TakeWhile(
                character => character == '#').Count();
            System.Text.RegularExpressions.Match nextHeading =
                System.Text.RegularExpressions.Regex.Match(
                    normalized[contentStart..],
                    $@"(?m)^#{{1,{headingLevel}}}[ \t]+",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (nextHeading.Success)
            {
                contentEnd = contentStart + nextHeading.Index;
            }
        }

        string section = normalized[contentStart..contentEnd];
        int exactParagraphCount = 0;
        foreach (string paragraph in
                 System.Text.RegularExpressions.Regex.Split(
                     section,
                     @"\n[ \t]*\n",
                     System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            string rawParagraph = paragraph.Trim('\r', '\n');
            string trimmedParagraph = rawParagraph.Trim();
            if (rawParagraph.StartsWith("    ", StringComparison.Ordinal) ||
                rawParagraph.StartsWith('\t') ||
                System.Text.RegularExpressions.Regex.IsMatch(
                    trimmedParagraph,
                    @"^(?:#{1,6}[ \t]|>[ \t]?|[-+*][ \t]|\d+[.)][ \t]|\[[^\]\r\n]+\]:|\|)",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            {
                continue;
            }

            string normalizedParagraph =
                System.Text.RegularExpressions.Regex.Replace(
                    trimmedParagraph,
                    @"\s+",
                    " ",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (string.Equals(
                    normalizedParagraph,
                    expectedParagraph,
                    StringComparison.Ordinal))
            {
                exactParagraphCount++;
            }
        }

        return exactParagraphCount == 1;
    }

    private static string GetVisibleMarkdown(
        string markdown,
        List<string> errors,
        string documentName)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(
                markdown,
                @"(?s)<!--.*?-->|<!--|-->",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            errors.Add(
                $"{documentName} must not hide evidence or scope claims in HTML comments.");
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(
                markdown,
                @"(?m)^[ \t]*(?:\x60{3,}|~{3,})",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            errors.Add(
                $"{documentName} must not hide evidence or scope claims in fenced blocks.");
        }

        if (markdown.Contains("~~", StringComparison.Ordinal))
        {
            errors.Add(
                $"{documentName} must not negate evidence or scope claims with Markdown strikethrough.");
        }

        const string RawHtmlTagPattern =
            @"(?is)<\s*/?\s*[A-Za-z][A-Za-z0-9-]*(?:\s[^<>]*?)?\s*/?\s*>";
        if (System.Text.RegularExpressions.Regex.IsMatch(
                markdown,
                RawHtmlTagPattern,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            errors.Add(
                $"{documentName} must not contain a raw HTML tag; this evidence document is plain Markdown.");
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(
                markdown,
                @"(?m)^[ \t]{0,3}\[[^\]\r\n]+\]:[ \t]*\S.*$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            errors.Add(
                $"{documentName} must not contain a link-reference definition; scope claims must be standalone plain paragraphs.");
        }

        string visible = System.Text.RegularExpressions.Regex.Replace(
            markdown,
            @"(?s)<!--.*?-->",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        visible = System.Text.RegularExpressions.Regex.Replace(
            visible,
            @"(?ms)^[ \t]*(?:\x60{3,}|~{3,})[^\r\n]*\r?\n.*?^[ \t]*(?:\x60{3,}|~{3,})[ \t]*(?:\r?\n|$)",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return System.Text.RegularExpressions.Regex.Replace(
            visible,
            @"(?s)~~.*?~~",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static bool ContainsPrivateAbsolutePath(string text)
    {
        const string AllowedFigmaMetadata =
            "- Figma file: `gAmBX1DYh71hqxHVqiivus`";
        string pathScanText = text.Replace(
            AllowedFigmaMetadata,
            string.Empty,
            StringComparison.Ordinal);
        if (System.Text.RegularExpressions.Regex.IsMatch(
                pathScanText,
                @"(?i)\bfile\s*:",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            return true;
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(
                pathScanText,
                @"(?im)\b[A-Za-z]:[\\/]",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            return true;
        }

        System.Text.RegularExpressions.MatchCollection rootedCandidates =
            System.Text.RegularExpressions.Regex.Matches(
                pathScanText,
                @"(?m)(?<![A-Za-z0-9_.:/\\-])(?<path>[\\/][^\s`|<>()\[\]{}\""']+)",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        foreach (System.Text.RegularExpressions.Match match in rootedCandidates)
        {
            string candidate = match.Groups["path"].Value.TrimEnd(
                '.',
                ',',
                ';',
                ':');
            if (Path.IsPathRooted(candidate) ||
                candidate.StartsWith('/') ||
                candidate.StartsWith('\\'))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] ValidateControlledEvidenceWorkflow(string workflow)
    {
        var errors = new List<string>();
        string[] requiredFragments =
        [
            "name: Model Inspection controlled evidence preflight (blocked)",
            "workflow_dispatch:",
            "campaign: light-96dpi-100text",
            "campaign: high-contrast",
            "campaign: text-scale-200",
            "runner: model-inspection-visual-light",
            "runner: model-inspection-visual-high-contrast",
            "runner: model-inspection-visual-text-scale-200",
            "MODEL_INSPECTION_VISUAL_APPROVED_OS_BUILD",
            "MODEL_INSPECTION_VISUAL_APPROVED_RASTERIZER_BUILD",
            "visual-reference-manifest.json",
            "base64-bitset-v1",
            "The reference directory contains an unapproved file",
            "ModelInspectionVisualReferenceIntegrityTests.cs",
            "ModelInspectionVisualRegressionTests.cs",
            "ModelInspectionControlledAccessibilityTests.cs",
            "ModelInspectionVisualRegression",
            "ModelInspectionControlledOs",
            "ModelInspectionControlledHighContrast",
            "ModelInspectionControlledTextScale200",
            "[DoNotParallelize]",
            "CONTROLLED_EVIDENCE_PREFLIGHT_BLOCKED",
            "13-row controlled execution is not implemented",
            "sanitized artifact scan/upload closure is not implemented"
        ];
        foreach (string fragment in requiredFragments)
        {
            if (!workflow.Contains(fragment, StringComparison.Ordinal))
            {
                errors.Add($"The controlled preflight is missing: {fragment}");
            }
        }

        foreach (string referenceName in ControlledReferenceNames)
        {
            if (CountOccurrences(workflow, referenceName) != 1)
            {
                errors.Add($"The controlled preflight must name the exact reference once: {referenceName}");
            }
        }

        string normalizedWorkflow = workflow.Replace(
            "\r\n",
            "\n",
            StringComparison.Ordinal);
        string workflowHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(normalizedWorkflow.Trim())));
        if (!string.Equals(
                workflowHash,
                ControlledWorkflowSha256,
                StringComparison.Ordinal))
        {
            errors.Add(
                $"The normalized controlled preflight changed without deliberate review and SHA-256 repinning: {workflowHash}.");
        }

        const string DispatchOnlyTrigger =
            "\non:\n" +
            "  workflow_dispatch:\n\n" +
            "permissions:\n";
        if (CountOccurrences(normalizedWorkflow, DispatchOnlyTrigger) != 1)
        {
            errors.Add(
                "The blocked controlled preflight must expose only workflow_dispatch and no automatic trigger.");
        }

        string failClosedTextScaleBlock = string.Join(
            "\n",
            [
                "          $textScale = $null",
                "          try {",
                "            $textScale = [int](Get-ItemPropertyValue `",
                "              -LiteralPath 'HKCU:\\Software\\Microsoft\\Accessibility' `",
                "              -Name TextScaleFactor `",
                "              -ErrorAction Stop)",
                "            if ($textScale -le 0) {",
                "              throw 'Windows returned an invalid text scale.'",
                "            }",
                "          }",
                "          catch {",
                "            $textScale = $null",
                "            $blockers.Add('Actual Windows text scale could not be read.')",
                "          }"
            ]);
        if (CountOccurrences(normalizedWorkflow, failClosedTextScaleBlock) != 1 ||
            !normalizedWorkflow.Contains(
                "          if ($null -eq $textScale) {\n" +
                "            $blockers.Add('Actual Windows text scale is unknown.')",
                StringComparison.Ordinal))
        {
            errors.Add(
                "The controlled preflight must block an unreadable or invalid Windows text scale.");
        }

        string terminalThrowBlock = string.Join(
            "\n",
            [
                "          throw (",
                "            'CONTROLLED_EVIDENCE_PREFLIGHT_BLOCKED: exact references, strict ' +",
                "            'classes, approved environment pins, and privacy-safe evidence ' +",
                "            'retention must all close before artifact production.')"
            ]);
        if (CountOccurrences(normalizedWorkflow, "\n          throw (") != 1 ||
            !normalizedWorkflow.TrimEnd().EndsWith(
                terminalThrowBlock,
                StringComparison.Ordinal))
        {
            errors.Add(
                "The blocked controlled preflight must end with its sole executable terminating throw outside comments or here-strings.");
        }

        string executableWorkflow = RemovePowerShellNonExecutableText(
            normalizedWorkflow);
        (string Name, string Pattern)[] earlyTerminationPatterns =
        [
            ("exit", @"(?im)(?<![\w-])exit(?![\w-])"),
            ("return", @"(?im)(?<![\w-])return(?![\w-])"),
            ("trap", @"(?im)(?<![\w-])trap(?![\w-])"),
            ("break", @"(?im)(?<![\w-])break(?![\w-])"),
            ("Environment.Exit", @"(?im)\[(?:System\.)?Environment\]::Exit\s*\("),
            ("SetShouldExit", @"(?im)\.SetShouldExit\s*\(")
        ];
        foreach ((string name, string pattern) in earlyTerminationPatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    executableWorkflow,
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            {
                errors.Add(
                    $"The controlled preflight contains an early termination/interception primitive: {name}.");
            }
        }

        const string ExpectedErrorActionContinue =
            "          $blockers | ForEach-Object { Write-Error $_ -ErrorAction Continue }";
        if (CountOccurrences(
                executableWorkflow,
                ExpectedErrorActionContinue) != 1)
        {
            errors.Add(
                "The controlled preflight must retain its one pinned non-terminating blocker write.");
        }

        string continueTokenScan = executableWorkflow.Replace(
            ExpectedErrorActionContinue,
            string.Empty,
            StringComparison.Ordinal);
        System.Text.RegularExpressions.MatchCollection executableContinues =
            System.Text.RegularExpressions.Regex.Matches(
                continueTokenScan,
                @"(?i)\bcontinue\b",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        const string ExpectedLoopContinue =
            "              continue\n" +
            "            }\n\n" +
            "            $source = Get-Content";
        if (executableContinues.Count != 1 ||
            CountOccurrences(normalizedWorkflow, ExpectedLoopContinue) != 1)
        {
            errors.Add(
                "After its pinned ErrorAction value is removed, the controlled preflight may contain only its one expected strict-class loop continue token.");
        }

        if (workflow.Contains('\u2014') ||
            workflow.Contains("â", StringComparison.Ordinal))
        {
            errors.Add("The controlled preflight must use ASCII punctuation.");
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(
                executableWorkflow,
                @"(?im)(?<![\w.-])(?:dotnet(?:\.exe)?|msbuild(?:\.exe)?|vstest(?:\.console)?(?:\.exe)?)(?![\w.-])",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            errors.Add(
                "The preflight-only workflow must not invoke a build or test executable.");
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(
                executableWorkflow,
                @"(?im)(?<![\w.-])(?:invoke-expression|iex)(?![\w.-])",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            errors.Add(
                "The preflight-only workflow must not use Invoke-Expression/IEX dynamic execution.");
        }

        string[] prohibitedExecutionFragments =
        [
            "invoke-pester",
            "start-process",
            "--logger",
            "/logger:",
            "--report-trx",
            ".trx",
            "/TestCaseFilter:",
            "actions/upload-artifact",
            "ResultsDirectory:",
            "TestResults",
            "Publish-TestResults",
            "New-Item -ItemType Directory"
        ];
        foreach (string fragment in prohibitedExecutionFragments)
        {
            if (executableWorkflow.Contains(
                    fragment,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"The preflight-only workflow must not produce test/artifact evidence: {fragment}");
            }
        }

        return errors.ToArray();
    }

    private static string RemovePowerShellNonExecutableText(string script)
    {
        string result = System.Text.RegularExpressions.Regex.Replace(
            script,
            @"(?ms)@'.*?^[ \t]*'@[ \t]*(?:\n|$)",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            "(?ms)@\".*?^[ \\t]*\"@[ \\t]*(?:\\n|$)",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"(?s)<#.*?#>",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return System.Text.RegularExpressions.Regex.Replace(
            result,
            @"(?m)#.*$",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static string ReadControlledEvidenceWorkflow()
    {
        string path = Path.Combine(
            Root,
            ".github",
            "workflows",
            "model-inspection-visual-regression.yml");
        Assert.IsTrue(
            File.Exists(path),
            "The manual fail-closed controlled-evidence preflight workflow is missing.");
        return File.ReadAllText(path);
    }

    private static string InsertBeforeControlledTerminalThrow(
        string workflow,
        string statement)
    {
        const string Marker =
            "          throw (\n" +
            "            'CONTROLLED_EVIDENCE_PREFLIGHT_BLOCKED";
        Assert.IsTrue(
            workflow.Contains(Marker, StringComparison.Ordinal),
            "The controlled workflow terminal throw marker is missing.");
        return workflow.Replace(
            Marker,
            $"          {statement}\n{Marker}",
            StringComparison.Ordinal);
    }

    private static string? TryExtractWorkflowStep(
        string workflow,
        string stepName)
    {
        string marker = $"- name: {stepName}";
        int stepStart = workflow.IndexOf(marker, StringComparison.Ordinal);
        if (stepStart < 0)
        {
            return null;
        }

        int nextStep = workflow.IndexOf(
            "\n      - name:",
            stepStart + marker.Length,
            StringComparison.Ordinal);
        return nextStep >= 0
            ? workflow[stepStart..nextStep]
            : workflow[stepStart..];
    }

    private static bool IsSharedWinUiStateTestSource(
        string path,
        string source)
    {
        string code = RemoveCSharpCommentsAndStrings(source);
        return path.EndsWith(
                "DisclosureTests.cs",
                StringComparison.OrdinalIgnoreCase) ||
            ContainsSharedWinUiStateCode(code);
    }

    private static bool ContainsSharedWinUiStateCode(string code) =>
        code.Contains("ModelInspectionControlledOs", StringComparison.Ordinal) ||
        code.Contains("RenderTargetBitmap", StringComparison.Ordinal) ||
        code.Contains(
            "ElementCompositionPreview.GetElementVisual",
            StringComparison.Ordinal) ||
        code.Contains("CompositionTarget.Rendering", StringComparison.Ordinal) ||
        code.Contains("WinUiRenderHost", StringComparison.Ordinal) ||
        System.Text.RegularExpressions.Regex.IsMatch(
            code,
            @"\bnew\s+(?:(?:global::)?Microsoft\.UI\.Xaml\.)?Window\s*(?:\{|\()|\bWindow\s+[A-Za-z_]\w*\s*=\s*new\s*(?:\(|\{)",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static bool HasClassLevelDoNotParallelize(string source)
    {
        string code = RemoveCSharpCommentsAndStrings(source);
        System.Text.RegularExpressions.MatchCollection headers =
            System.Text.RegularExpressions.Regex.Matches(
                code,
                @"\bclass\s+[A-Za-z_]\w*",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        int totalTestClassTokens = CountMSTestAttributeTokens(
            code,
            "TestClass");
        int associatedTestClassTokens = 0;
        int relevantClassCount = 0;
        for (int index = 0; index < headers.Count; index++)
        {
            System.Text.RegularExpressions.Match header = headers[index];
            string attributes = GetClassAttributePrefix(
                code,
                header.Index);
            int headerTestClassTokens = CountMSTestAttributeTokens(
                attributes,
                "TestClass");
            associatedTestClassTokens += headerTestClassTokens;

            int segmentEnd = index + 1 < headers.Count
                ? headers[index + 1].Index
                : code.Length;
            string classSegment = code[header.Index..segmentEnd];
            bool hasUiTestMethod = CountMSTestAttributeTokens(
                classSegment,
                "UITestMethod") > 0;
            bool hasSharedStateMarker = ContainsSharedWinUiStateCode(
                classSegment);
            if (headerTestClassTokens == 0 &&
                !hasUiTestMethod &&
                !hasSharedStateMarker)
            {
                continue;
            }

            relevantClassCount++;
            if (CountMSTestAttributeTokens(
                    attributes,
                    "DoNotParallelize") == 0)
            {
                return false;
            }
        }

        return relevantClassCount > 0 &&
            associatedTestClassTokens == totalTestClassTokens;
    }

    private static string GetClassAttributePrefix(
        string code,
        int classKeywordIndex)
    {
        int prefixStart = classKeywordIndex;
        while (true)
        {
            int cursor = prefixStart;
            SkipWhitespaceBackward(code, ref cursor);
            int wordEnd = cursor;
            while (cursor > 0 &&
                   (char.IsLetterOrDigit(code[cursor - 1]) ||
                    code[cursor - 1] == '_'))
            {
                cursor--;
            }

            if (cursor == wordEnd ||
                !IsClassModifier(code[cursor..wordEnd]))
            {
                break;
            }

            prefixStart = cursor;
        }

        while (true)
        {
            int cursor = prefixStart;
            SkipWhitespaceBackward(code, ref cursor);
            if (cursor == 0 || code[cursor - 1] != ']')
            {
                break;
            }

            int attributeStart = FindMatchingOpenBracketBackward(
                code,
                cursor - 1);
            if (attributeStart < 0)
            {
                break;
            }

            prefixStart = attributeStart;
        }

        return code[prefixStart..classKeywordIndex];
    }

    private static void SkipWhitespaceBackward(
        string code,
        ref int cursor)
    {
        while (cursor > 0 && char.IsWhiteSpace(code[cursor - 1]))
        {
            cursor--;
        }
    }

    private static bool IsClassModifier(string token) => token is
        "public" or
        "internal" or
        "private" or
        "protected" or
        "abstract" or
        "sealed" or
        "static" or
        "partial" or
        "new" or
        "file" or
        "unsafe" or
        "record";

    private static int FindMatchingOpenBracketBackward(
        string code,
        int closeBracketIndex)
    {
        int depth = 0;
        for (int index = closeBracketIndex; index >= 0; index--)
        {
            if (code[index] == ']')
            {
                depth++;
            }
            else if (code[index] == '[')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static int CountMSTestAttributeTokens(
        string code,
        string attributeName)
    {
        string tokenPattern =
            @"(?<![A-Za-z0-9_.:])(?:(?:global::)?Microsoft\.VisualStudio\.TestTools\.UnitTesting\.)?" +
            System.Text.RegularExpressions.Regex.Escape(attributeName) +
            @"(?:Attribute)?(?![A-Za-z0-9_.:])";
        int count = 0;
        foreach (System.Text.RegularExpressions.Match attributeList in
                 System.Text.RegularExpressions.Regex.Matches(
                     code,
                     @"\[[^\[\]]*\]",
                     System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            count += System.Text.RegularExpressions.Regex.Matches(
                attributeList.Value,
                tokenPattern,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant).Count;
        }

        return count;
    }

    private static string RemoveCSharpCommentsAndStrings(string source)
    {
        string result = System.Text.RegularExpressions.Regex.Replace(
            source,
            "(?s)\\$?\\\"\\\"\\\".*?\\\"\\\"\\\"",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            "(?s)@\\\"(?:\\\"\\\"|[^\\\"])*\\\"",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            "(?s)\\\"(?:\\\\.|[^\\\"\\\\])*\\\"|'(?:\\\\.|[^'\\\\])*'",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"(?s)/\*.*?\*/",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return System.Text.RegularExpressions.Regex.Replace(
            result,
            @"(?m)//.*$",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
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
