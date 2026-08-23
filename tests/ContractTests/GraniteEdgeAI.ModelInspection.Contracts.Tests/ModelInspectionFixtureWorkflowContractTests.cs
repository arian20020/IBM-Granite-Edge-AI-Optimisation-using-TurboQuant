using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

/// <summary>
/// Pins the permanent Debug/x64 packaged fixture-gallery campaign separately
/// from the unchanged Release hosted-equivalent campaign.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public sealed class ModelInspectionFixtureWorkflowContractTests
{
    private const string CampaignStepName =
        "Run serialized Debug x64 Model Inspection fixture gallery";

    private const string IsolationStepName =
        "Verify Model Inspection fixture Release isolation";

    private const string ExactCategoryFilter =
        "/TestCaseFilter:\"TestCategory=ModelInspectionFixtureGallery\"";

    private const string ExactDebugCondition =
        "'$(Configuration)|$(Platform)' == 'Debug|x64'";

    private const string HostedReleaseStepName =
        "Run unit and WinUI UI-thread tests";

    private const string CheckoutStepName =
        "Check out required build inputs";

    private const string HostedReleaseStepSha256 =
        "D307B116AF331C950726E9B66E01DCD13142312021492396FB9A6486521088E1";

    private const int HostedReleaseExpectedTotal = 858;

    // This sentinel stays invalid evidence for mutation-baseline validation.
    private const int UnmeasuredTrxCount = -1;

    private const int ExpectedTrxTotal = 220;

    private static readonly string Root = FindRepositoryRoot();

    private static readonly (string SourcePath, string ClassName, int Count)[]
        ExpectedFixtureClasses =
        [
            ("Features/ModelInspection/DebugFixtures/DebugModelInspectionServiceTests.cs",
                "GraniteEdgeAI.UnitTests.DebugModelInspectionServiceTests", 41),
            ("Features/ModelInspection/DebugFixtures/ModelInspectionFixtureAdapterTests.cs",
                "GraniteEdgeAI.UnitTests.ModelInspectionFixtureAdapterTests", 12),
            ("Features/ModelInspection/DebugFixtures/ModelInspectionFixtureGalleryTests.cs",
                "GraniteEdgeAI.UnitTests.ModelInspectionFixtureGalleryTests", 73),
            ("Features/ModelInspection/DebugFixtures/ModelInspectionFixtureInteractionTests.cs",
                "GraniteEdgeAI.UnitTests.ModelInspectionFixtureInteractionTests", 6),
            ("Features/ModelInspection/DebugFixtures/ModelInspectionFixtureLifetimeTests.cs",
                "GraniteEdgeAI.UnitTests.ModelInspectionFixtureLifetimeTests", 13),
            ("Features/ModelInspection/DebugFixtures/ModelInspectionFixturePageLifecycleTests.cs",
                "GraniteEdgeAI.UnitTests.ModelInspectionFixturePageLifecycleTests", 7),
            ("Features/ModelInspection/DebugFixtures/ModelInspectionFixturePresetTests.cs",
                "GraniteEdgeAI.UnitTests.ModelInspectionFixturePresetTests", 25),
            ("Features/ModelInspection/DebugFixtures/ModelInspectionFixtureScreenContractTests.cs",
                "GraniteEdgeAI.UnitTests.ModelInspectionFixtureScreenContractTests", 37),
            ("Features/ModelInspection/DebugFixtures/ModelInspectionFixtureViewModelIntegrationTests.cs",
                "GraniteEdgeAI.UnitTests.ModelInspectionFixtureViewModelIntegrationTests", 6)
        ];

    [TestMethod]
    public void WorkflowHasMeasuredSerializedDebugCampaignAndReleaseIsolation()
    {
        var errors = new List<string>(Validate(
            ReadWorkflow(),
            ReadUnitTestProject(),
            ReadFixtureClassSources(),
            allowUnmeasuredCounts: false));
        errors.AddRange(ValidateProgressPolishGateFixtureBoundary(
            ReadProgressPolishGate()));

        Assert.AreEqual(0, errors.Count, string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void GuardRejectsCampaignBoundaryEvidenceAndPrivacyMutations()
    {
        string workflow = CreateMutationBaselineWorkflow();
        string project = ReadUnitTestProject();
        Dictionary<string, string> sources = ReadFixtureClassSources();
        var lifetimeEntry = ExpectedFixtureClasses.Single(entry =>
            entry.ClassName.EndsWith(".ModelInspectionFixtureLifetimeTests", StringComparison.Ordinal));

        Assert.AreEqual(
            0,
            Validate(workflow, project, sources, allowUnmeasuredCounts: true).Length,
            "The mutation fixture must model every structural campaign contract before measured counts exist.");

        string progressPolishGate = ReadProgressPolishGate();
        Assert.AreEqual(
            0,
            ValidateProgressPolishGateFixtureBoundary(progressPolishGate).Length,
            string.Join(
                Environment.NewLine,
                ValidateProgressPolishGateFixtureBoundary(progressPolishGate)));
        (string Name, string Original, string Replacement)[] gateMutations =
        [
            ("Debug build ordering",
                "Invoke-DebugBuild -EvidenceDirectory $phaseRoot",
                "Write-Host 'Debug build omitted' # Invoke-DebugBuild -EvidenceDirectory $phaseRoot"),
            ("Interaction/Lifetime boundary",
                "Invoke-PackagedTests -Name 'InteractionLifetime' -Filter $interactionLifetimeFilter",
                "Write-Host 'focused lifecycle omitted' # Invoke-PackagedTests -Name 'InteractionLifetime' -Filter $interactionLifetimeFilter"),
            ("fixture-category boundary",
                "Invoke-PackagedTests -Name 'FixtureCategory' -Filter $fixtureCategoryFilter",
                "Write-Host 'fixture category omitted' # Invoke-PackagedTests -Name 'FixtureCategory' -Filter $fixtureCategoryFilter"),
            ("hosted Release boundary",
                "Invoke-PackagedTests -Name 'HostedRelease' -Filter $hostedReleaseFilter",
                "Write-Host 'hosted Release omitted' # Invoke-PackagedTests -Name 'HostedRelease' -Filter $hostedReleaseFilter"),
            ("N-001 boundary",
                "Invoke-PackagedTests -Name 'N001' -Filter $n001FullyQualifiedName",
                "Write-Host 'N-001 omitted' # Invoke-PackagedTests -Name 'N001' -Filter $n001FullyQualifiedName"),
            ("Release isolation boundary",
                "Invoke-ReleaseIsolation -EvidenceDirectory $phaseRoot",
                "Write-Host 'isolation omitted' # Invoke-ReleaseIsolation -EvidenceDirectory $phaseRoot"),
            ("runtime measured total",
                "Runtime = 189",
                "Runtime = 188"),
            ("Interaction/Lifetime measured total",
                "InteractionLifetime = 19",
                "InteractionLifetime = 18"),
            ("fixture-category measured total",
                "FixtureCategory = 220",
                "FixtureCategory = 219"),
            ("hosted Release measured total",
                "HostedRelease = 858",
                "HostedRelease = 857")
        ];
        foreach ((string name, string original, string replacement) in gateMutations)
        {
            string mutation = progressPolishGate.Replace(
                original,
                replacement,
                StringComparison.Ordinal);
            Assert.AreNotEqual(
                progressPolishGate,
                mutation,
                $"The fixture-gate mutation anchor drifted: {name}.");
            Assert.IsNotEmpty(
                ValidateProgressPolishGateFixtureBoundary(mutation),
                $"The fixture workflow guard accepted the {name} mutation.");
        }


        string normalizedGate = progressPolishGate.Replace("\r\n", "\n", StringComparison.Ordinal);
        const string RuntimeCommand =
            "Invoke-CheckedCommand -Name 'runtime-project' -Command {";
        string relocatedRuntimeCommand = normalizedGate
            .Replace($"            {RuntimeCommand}\n", string.Empty, StringComparison.Ordinal)
            .Replace(
                "        }\n    }\n}\ncatch {",
                $"        }}\n    }}\n    {RuntimeCommand}\n}}\ncatch {{",
                StringComparison.Ordinal);
        Assert.AreNotEqual(
            normalizedGate,
            relocatedRuntimeCommand,
            "The RuntimeWorker relocation mutation anchor drifted.");
        Assert.IsNotEmpty(
            ValidateProgressPolishGateFixtureBoundary(relocatedRuntimeCommand),
            "The fixture workflow guard accepted a RuntimeWorker command outside its phase.");

        const string CleanupCommand =
            "Invoke-CleanupGate -EvidenceDirectory $phaseRoot";
        string relocatedFinalSourceCommand = normalizedGate
            .Replace($"            {CleanupCommand}\n", string.Empty, StringComparison.Ordinal)
            .Replace(
                "        }\n    }\n}\ncatch {",
                $"        }}\n    }}\n    {CleanupCommand}\n}}\ncatch {{",
                StringComparison.Ordinal);
        Assert.AreNotEqual(
            normalizedGate,
            relocatedFinalSourceCommand,
            "The FinalSource relocation mutation anchor drifted.");
        Assert.IsNotEmpty(
            ValidateProgressPolishGateFixtureBoundary(relocatedFinalSourceCommand),
            "The fixture workflow guard accepted a FinalSource command outside its phase.");

        const string HostedReleaseCommand =
            "Invoke-PackagedTests -Name 'HostedRelease' -Filter $hostedReleaseFilter";
        string quotedHostedReleaseCommand = normalizedGate.Replace(
            $"            {HostedReleaseCommand}\n",
            $"            Write-Host \"{HostedReleaseCommand}\"\n",
            StringComparison.Ordinal);
        Assert.AreNotEqual(
            normalizedGate,
            quotedHostedReleaseCommand,
            "The quoted phase-command mutation anchor drifted.");
        Assert.IsNotEmpty(
            ValidateProgressPolishGateFixtureBoundary(quotedHostedReleaseCommand),
            "The fixture workflow guard accepted an inert quoted phase command.");

        string multilineQuotedHostedReleaseCommand = normalizedGate.Replace(
            $"            {HostedReleaseCommand}\n",
            $"            Write-Host \"\n{HostedReleaseCommand}\n            \"\n",
            StringComparison.Ordinal);
        Assert.AreNotEqual(
            normalizedGate,
            multilineQuotedHostedReleaseCommand,
            "The multiline quoted phase-command mutation anchor drifted.");
        Assert.IsNotEmpty(
            ValidateProgressPolishGateFixtureBoundary(multilineQuotedHostedReleaseCommand),
            "The fixture workflow guard accepted an inert multiline quoted phase command.");

        string commentConfusedQuotedHostedReleaseCommand = normalizedGate.Replace(
            $"            {HostedReleaseCommand}\n",
            $"            Write-Host \"#\"\n            \"\n{HostedReleaseCommand}\n            \"\n            Write-Host \"#\"\n",
            StringComparison.Ordinal);
        Assert.AreNotEqual(
            normalizedGate,
            commentConfusedQuotedHostedReleaseCommand,
            "The comment-confused quoted phase-command mutation anchor drifted.");
        Assert.IsNotEmpty(
            ValidateProgressPolishGateFixtureBoundary(commentConfusedQuotedHostedReleaseCommand),
            "The fixture workflow guard accepted an inert quoted phase command after stripping hash literals.");

        string duplicateMeasuredTotal = normalizedGate.Replace(
            "$ExpectedTotals = [ordered]@{",
            "$interactionLifetimeExpectedTotal = 19\n$ExpectedTotals = [ordered]@{",
            StringComparison.Ordinal);
        Assert.AreNotEqual(
            normalizedGate,
            duplicateMeasuredTotal,
            "The duplicate measured-total mutation anchor drifted.");
        Assert.IsNotEmpty(
            ValidateProgressPolishGateFixtureBoundary(duplicateMeasuredTotal),
            "The fixture workflow guard accepted a second measured-total source.");

        string duplicateRuntimeMeasuredTotal = normalizedGate.Replace(
            "$ExpectedTotals = [ordered]@{",
            "$runtimeExpectedTotal = 189\n$ExpectedTotals = [ordered]@{",
            StringComparison.Ordinal);
        Assert.AreNotEqual(
            normalizedGate,
            duplicateRuntimeMeasuredTotal,
            "The duplicate runtime measured-total mutation anchor drifted.");
        Assert.IsNotEmpty(
            ValidateProgressPolishGateFixtureBoundary(duplicateRuntimeMeasuredTotal),
            "The fixture workflow guard accepted a second runtime measured-total source.");

        (string Name, string Workflow)[] workflowMutations =
        [
            ("category", workflow.Replace(
                ExactCategoryFilter,
                "/TestCaseFilter:\"TestCategory=Contract\"",
                StringComparison.Ordinal)),
            ("configuration", workflow.Replace(
                "$fixtureGalleryConfiguration = 'Debug'",
                "$fixtureGalleryConfiguration = 'Release'",
                StringComparison.Ordinal)),
            ("platform", workflow.Replace(
                "$fixtureGalleryPlatform = 'x64'",
                "$fixtureGalleryPlatform = 'ARM64'",
                StringComparison.Ordinal)),
            ("total", workflow.Replace(
                $"$fixtureGalleryExpectedTotal = {ExpectedTrxTotal}",
                $"$fixtureGalleryExpectedTotal = {ExpectedTrxTotal - 1}",
                StringComparison.Ordinal)),
            ("class count", workflow.Replace(
                $"'{lifetimeEntry.ClassName}' = {lifetimeEntry.Count}",
                $"'{lifetimeEntry.ClassName}' = {lifetimeEntry.Count - 1}",
                StringComparison.Ordinal)),
            ("class set", workflow.Replace(
                $"'{lifetimeEntry.ClassName}' = {lifetimeEntry.Count}",
                string.Empty,
                StringComparison.Ordinal)),
            ("isolation", workflow.Replace(
                ExtractWorkflowStep(workflow, IsolationStepName),
                string.Empty,
                StringComparison.Ordinal)),
            ("raw TRX cleanup", workflow.Replace(
                "Remove-Item -LiteralPath $fixtureGalleryTrxPath -Force",
                "Write-Host 'TRX cleanup omitted'",
                StringComparison.Ordinal)),
            ("raw TRX upload", workflow + "\n" +
                "      - name: Upload private fixture result\n" +
                "        uses: actions/upload-artifact@v4\n" +
                "        with:\n" +
                "          path: D:/s/TestResults/ModelInspectionFixtures/*.trx\n"),
            ("Release campaign", workflow.Replace(
                "$minimumExpectedTests = 686",
                "$minimumExpectedTests = 685",
                StringComparison.Ordinal)),
            ("measured Release total", workflow.Replace(
                "$measuredExpectedTests = 858",
                "$measuredExpectedTests = 857",
                StringComparison.Ordinal)),
            ("duplicate campaign step", workflow + "\n      " +
                ExtractWorkflowStep(workflow, CampaignStepName)),
            ("duplicate Release step", workflow + "\n      " +
                ExtractWorkflowStep(workflow, HostedReleaseStepName)),
            ("duplicate isolation step", workflow + "\n      " +
                ExtractWorkflowStep(workflow, IsolationStepName)),
            ("campaign ordering", SwapWorkflowSteps(
                workflow, CampaignStepName, HostedReleaseStepName)),
            ("Debug app build", workflow.Replace(
                "msbuild \"$env:APP_PROJECT\" /target:Restore,Build /property:Configuration=$fixtureGalleryConfiguration /property:Platform=$fixtureGalleryPlatform",
                "Write-Host 'app build omitted'",
                StringComparison.Ordinal)),
            ("Debug test build", workflow.Replace(
                "dotnet build \"$env:TEST_PROJECT\" --configuration $fixtureGalleryConfiguration --runtime win-x64 -p:Platform=$fixtureGalleryPlatform",
                "Write-Host 'test build omitted'",
                StringComparison.Ordinal)),
            ("VSTest x64", workflow.Replace(
                "/Platform:x64",
                "/Platform:ARM64",
                StringComparison.Ordinal)),
            ("LASTEXITCODE", workflow.Replace(
                "if ($LASTEXITCODE -ne 0) { throw 'Debug fixture-gallery VSTest failed.' }",
                string.Empty,
                StringComparison.Ordinal)),
            ("TRX existence", workflow.Replace(
                "if (-not (Test-Path -LiteralPath $fixtureGalleryTrxPath -PathType Leaf)) { throw 'Fixture-gallery TRX was not created.' }",
                string.Empty,
                StringComparison.Ordinal)),
            ("runtime class-count sum", workflow.Replace(
                "$fixtureGalleryExpectedClassTotal = [int](($fixtureGalleryExpectedClassCounts.Values | Measure-Object -Sum).Sum)",
                "$fixtureGalleryExpectedClassTotal = $fixtureGalleryExpectedTotal",
                StringComparison.Ordinal)),
            ("runtime exact class set", workflow.Replace(
                "$classSetDifference = @(Compare-Object -ReferenceObject $expectedClassNames -DifferenceObject $actualClassNames)",
                "$classSetDifference = @()",
                StringComparison.Ordinal)),
            ("sparse checkout", workflow.Replace(
                "        uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0",
                "        uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0\n" +
                "        with:\n" +
                "          sparse-checkout: |\n" +
                "            .github\n" +
                "            scripts",
                StringComparison.Ordinal))
        ];

        foreach ((string name, string mutation) in workflowMutations)
        {
            Assert.IsNotEmpty(
                Validate(mutation, project, sources, allowUnmeasuredCounts: true),
                $"The workflow guard accepted the {name} mutation.");
        }

        string lifetimePath = lifetimeEntry.SourcePath;
        Dictionary<string, string> serializationMutation = new(sources, StringComparer.Ordinal)
        {
            [lifetimePath] =
                sources[lifetimePath].Replace(
                    "[DoNotParallelize]",
                    "// [DoNotParallelize]",
                    StringComparison.Ordinal)
        };
        Assert.IsNotEmpty(
            Validate(workflow, project, serializationMutation, allowUnmeasuredCounts: true),
            "The workflow guard accepted a fixture class that was no longer serialized.");

        Dictionary<string, string> categoryCommentMutation = new(sources, StringComparer.Ordinal)
        {
            [lifetimePath] = sources[lifetimePath].Replace(
                "[TestCategory(\"ModelInspectionFixtureGallery\")]",
                "// [TestCategory(\"ModelInspectionFixtureGallery\")]",
                StringComparison.Ordinal)
        };
        Assert.IsNotEmpty(
            Validate(workflow, project, categoryCommentMutation, allowUnmeasuredCounts: true),
            "The workflow guard treated a commented category as a class-level attribute.");

        Dictionary<string, string> testClassMutation = new(sources, StringComparer.Ordinal)
        {
            [lifetimePath] = sources[lifetimePath].Replace(
                "[TestClass]",
                "// [TestClass]",
                StringComparison.Ordinal)
        };
        Assert.IsNotEmpty(
            Validate(workflow, project, testClassMutation, allowUnmeasuredCounts: true),
            "The workflow guard accepted a categorized type without its class-level TestClass attribute.");

        Dictionary<string, string> rogueClassMutation = new(sources, StringComparer.Ordinal)
        {
            ["Features/Unrelated/RogueFixtureCampaignTests.cs"] =
                "namespace GraniteEdgeAI.UnitTests;\n" +
                "[TestClass]\n[DoNotParallelize]\n" +
                "[TestCategory(\"ModelInspectionFixtureGallery\")]\n" +
                "public sealed class RogueFixtureCampaignTests { }\n"
        };
        Assert.IsNotEmpty(
            Validate(workflow, project, rogueClassMutation, allowUnmeasuredCounts: true),
            "The workflow guard accepted a categorized class outside the closed nine-class set.");

        string packageSetMutation = project.Replace(
            "MI-050-progress-starting-secure-inspection.fixture.json",
            "MI-999-mutated-package-set.fixture.json",
            StringComparison.Ordinal);
        Assert.IsNotEmpty(
            Validate(workflow, packageSetMutation, sources, allowUnmeasuredCounts: true),
            "The workflow guard accepted a changed packaged fixture-resource set.");
    }

    private static string[] Validate(
        string workflow,
        string unitTestProject,
        IReadOnlyDictionary<string, string> fixtureSources,
        bool allowUnmeasuredCounts)
    {
        var errors = new List<string>();
        ValidateFullRepositoryCheckout(workflow, errors);
        ValidateUniqueStepNames(workflow, errors);
        ValidateUnchangedReleaseCampaign(workflow, errors);
        ValidateDebugCampaign(workflow, allowUnmeasuredCounts, errors);
        ValidateSerializedClassSet(fixtureSources, errors);
        ValidateDebugOnlyPackageSet(unitTestProject, errors);
        ValidateReleaseIsolation(workflow, errors);
        ValidateCampaignOrder(workflow, errors);

        if (workflow.Contains("actions/upload-artifact@", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("The permanent workflow must not upload raw TRX or test artifacts.");
        }

        return errors.ToArray();
    }

    private static void ValidateFullRepositoryCheckout(
        string workflow,
        ICollection<string> errors)
    {
        string? checkout = TryExtractWorkflowStep(workflow, CheckoutStepName);
        if (checkout is null)
        {
            errors.Add("The workflow checkout step is missing.");
            return;
        }

        RequireExactlyOnce(
            checkout,
            "uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0",
            errors);
        foreach (string partialCheckoutToken in new[]
                 {
                     "sparse-checkout:",
                     "sparse-checkout-cone-mode:",
                     "filter:"
                 })
        {
            if (checkout.Contains(
                    partialCheckoutToken,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    "Release isolation requires a full physical repository checkout; " +
                    $"partial checkout token is forbidden: {partialCheckoutToken}");
            }
        }
    }

    private static void ValidateUniqueStepNames(
        string workflow,
        ICollection<string> errors)
    {
        foreach (string stepName in new[]
                 {
                     CampaignStepName,
                     HostedReleaseStepName,
                     IsolationStepName
                 })
        {
            int count = CountWorkflowSteps(workflow, stepName);
            if (count != 1)
            {
                errors.Add($"The workflow must contain exactly one '{stepName}' step; found {count}.");
            }
        }
    }

    private static void ValidateUnchangedReleaseCampaign(
        string workflow,
        ICollection<string> errors)
    {
        string? step = TryExtractWorkflowStep(workflow, HostedReleaseStepName);
        if (step is null)
        {
            errors.Add("The existing Release hosted-equivalent packaged campaign is missing.");
            return;
        }

        string normalized = step.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        string executable = RemovePowerShellComments(normalized);
        string hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(normalized)));
        if (!string.Equals(hash, HostedReleaseStepSha256, StringComparison.Ordinal))
        {
            errors.Add($"The existing Release packaged campaign changed: {hash}.");
        }

        string expectedTotal =
            $"$measuredExpectedTests = {HostedReleaseExpectedTotal}";
        const string ExactMeasuredTotalGuard =
            "if ([int]$counters.total -ne $measuredExpectedTests -or [int]$counters.executed -ne $measuredExpectedTests)";
        if (CountOccurrences(executable, expectedTotal) != 1 ||
            CountOccurrences(executable, ExactMeasuredTotalGuard) != 1)
        {
            errors.Add(
                $"The Release packaged campaign must enforce the measured {HostedReleaseExpectedTotal}-test total exactly once.");
        }
    }

    private static void ValidateDebugCampaign(
        string workflow,
        bool allowUnmeasuredCounts,
        ICollection<string> errors)
    {
        string? step = TryExtractWorkflowStep(workflow, CampaignStepName);
        if (step is null)
        {
            errors.Add("The permanent serialized Debug/x64 fixture-gallery campaign is missing.");
            if (!allowUnmeasuredCounts)
            {
                errors.Add("Debug fixture-gallery TRX counts remain unmeasured.");
            }
            return;
        }

        string executable = RemovePowerShellComments(step.Replace(
            "\r\n", "\n", StringComparison.Ordinal));
        RequireExactlyOnce(executable, "$fixtureGalleryConfiguration = 'Debug'", errors);
        RequireExactlyOnce(executable, "$fixtureGalleryPlatform = 'x64'", errors);
        RequireExactlyOnce(executable,
            "msbuild \"$env:APP_PROJECT\" /target:Restore,Build /property:Configuration=$fixtureGalleryConfiguration /property:Platform=$fixtureGalleryPlatform",
            errors);
        RequireExactlyOnce(executable,
            "if ($LASTEXITCODE -ne 0) { throw 'Debug fixture-gallery app build failed.' }",
            errors);
        RequireExactlyOnce(executable,
            "dotnet build \"$env:TEST_PROJECT\" --configuration $fixtureGalleryConfiguration --runtime win-x64 -p:Platform=$fixtureGalleryPlatform",
            errors);
        RequireExactlyOnce(executable,
            "if ($LASTEXITCODE -ne 0) { throw 'Debug fixture-gallery test build failed.' }",
            errors);
        RequireExactlyOnce(executable, ExactCategoryFilter, errors);
        RequireExactlyOnce(executable,
            "GraniteEdgeAI.UnitTests/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.UnitTests.build.appxrecipe",
            errors);
        RequireExactlyOnce(executable,
            "$vswhere = \"${env:ProgramFiles(x86)}\\Microsoft Visual Studio\\Installer\\vswhere.exe\"",
            errors);
        RequireExactlyOnce(executable,
            "$vstest = & $vswhere -latest -products * -find '**\\Common7\\IDE\\CommonExtensions\\Microsoft\\TestWindow\\vstest.console.exe' | Select-Object -First 1",
            errors);
        RequireExactlyOnce(executable, "if (-not $vstest)", errors);
        RequireExactlyOnce(executable,
            "& $vstest $fixtureGalleryRecipe /Platform:x64 /Logger:\"trx;LogFileName=ModelInspectionFixtureGallery.trx\" /ResultsDirectory:\"D:/s/TestResults/ModelInspectionFixtures\" " + ExactCategoryFilter,
            errors);
        RequireExactlyOnce(executable,
            "if ($LASTEXITCODE -ne 0) { throw 'Debug fixture-gallery VSTest failed.' }",
            errors);
        RequireExactlyOnce(executable,
            "if (-not (Test-Path -LiteralPath $fixtureGalleryTrxPath -PathType Leaf)) { throw 'Fixture-gallery TRX was not created.' }",
            errors);
        RequireExactlyOnce(executable,
            "[xml]$trx = Get-Content -LiteralPath $fixtureGalleryTrxPath -Raw",
            errors);
        RequireExactlyOnce(executable, $"$fixtureGalleryExpectedTotal = {ExpectedTrxTotal}", errors);
        RequireExactlyOnce(executable, "$fixtureGalleryExpectedClassCounts = [ordered]@{", errors);
        RequireExactlyOnce(executable,
            "$fixtureGalleryExpectedClassTotal = [int](($fixtureGalleryExpectedClassCounts.Values | Measure-Object -Sum).Sum)",
            errors);
        RequireExactlyOnce(executable,
            "if ($fixtureGalleryExpectedClassTotal -ne $fixtureGalleryExpectedTotal)",
            errors);
        RequireExactlyOnce(executable, "Remove-Item -LiteralPath $fixtureGalleryTrxPath -Force", errors);
        RequireExactlyOnce(executable,
            "if (Test-Path -LiteralPath $fixtureGalleryTrxPath -PathType Leaf) { throw 'Raw fixture-gallery TRX was retained.' }",
            errors);
        RequireExactlyOnce(executable, "finally {", errors);

        int appBuildIndex = executable.IndexOf("msbuild \"$env:APP_PROJECT\"", StringComparison.Ordinal);
        int appBuildExitIndex = executable.IndexOf(
            "if ($LASTEXITCODE -ne 0) { throw 'Debug fixture-gallery app build failed.' }",
            StringComparison.Ordinal);
        int testBuildIndex = executable.IndexOf("dotnet build \"$env:TEST_PROJECT\"", StringComparison.Ordinal);
        int testBuildExitIndex = executable.IndexOf(
            "if ($LASTEXITCODE -ne 0) { throw 'Debug fixture-gallery test build failed.' }",
            StringComparison.Ordinal);
        int vstestResolutionIndex = executable.IndexOf("$vstest = & $vswhere", StringComparison.Ordinal);
        int tryIndex = executable.IndexOf("try {", StringComparison.Ordinal);
        int vstestIndex = executable.IndexOf("& $vstest ", StringComparison.Ordinal);
        int vstestExitIndex = executable.IndexOf(
            "if ($LASTEXITCODE -ne 0) { throw 'Debug fixture-gallery VSTest failed.' }",
            StringComparison.Ordinal);
        int trxExistenceIndex = executable.IndexOf(
            "if (-not (Test-Path -LiteralPath $fixtureGalleryTrxPath -PathType Leaf))",
            StringComparison.Ordinal);
        int trxReadIndex = executable.IndexOf(
            "[xml]$trx = Get-Content -LiteralPath $fixtureGalleryTrxPath -Raw",
            StringComparison.Ordinal);
        int finallyIndex = executable.IndexOf("finally {", StringComparison.Ordinal);
        int cleanupIndex = executable.IndexOf(
            "Remove-Item -LiteralPath $fixtureGalleryTrxPath -Force",
            StringComparison.Ordinal);
        if (!(appBuildIndex >= 0 && appBuildIndex < appBuildExitIndex &&
              appBuildExitIndex < testBuildIndex && testBuildIndex < testBuildExitIndex &&
              testBuildExitIndex < vstestResolutionIndex && vstestResolutionIndex < tryIndex &&
              tryIndex < vstestIndex && vstestIndex < vstestExitIndex &&
              vstestExitIndex < trxExistenceIndex && trxExistenceIndex < trxReadIndex &&
              trxReadIndex < finallyIndex && finallyIndex < cleanupIndex))
        {
            errors.Add(
                "Debug builds, VSTest, TRX validation, and raw-TRX cleanup must execute in the required fail-closed order.");
        }

        if (executable.Contains("/Parallel", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("The serialized fixture-gallery campaign must not enable VSTest parallel execution.");
        }

        int mapStart = executable.IndexOf(
            "$fixtureGalleryExpectedClassCounts = [ordered]@{",
            StringComparison.Ordinal);
        int mapEnd = mapStart < 0
            ? -1
            : executable.IndexOf(
                "foreach ($entry in $fixtureGalleryExpectedClassCounts.GetEnumerator())",
                mapStart,
                StringComparison.Ordinal);
        if (mapStart < 0 || mapEnd <= mapStart)
        {
            errors.Add("The exact fixture-gallery class-count map is missing.");
        }
        else
        {
            string map = executable[mapStart..mapEnd];
            foreach (var entry in ExpectedFixtureClasses)
            {
                RequireExactlyOnce(map, $"'{entry.ClassName}' = {entry.Count}", errors);
            }

            int entryCount = map.Split('\n').Count(line =>
                line.TrimStart().StartsWith("'GraniteEdgeAI.UnitTests.", StringComparison.Ordinal));
            if (entryCount != ExpectedFixtureClasses.Length)
            {
                errors.Add($"The fixture-gallery map must contain exactly {ExpectedFixtureClasses.Length} classes.");
            }
        }

        foreach (string token in new[]
                 {
                     "$counters.total -ne $fixtureGalleryExpectedTotal",
                     "$counters.executed -ne $fixtureGalleryExpectedTotal",
                     "$counters.passed -ne $fixtureGalleryExpectedTotal",
                     "$actualClassNames = @($definitions | ForEach-Object { [string]$_.TestMethod.className } | Sort-Object -Unique)",
                     "$expectedClassNames = @($fixtureGalleryExpectedClassCounts.Keys | Sort-Object)",
                     "$classSetDifference = @(Compare-Object -ReferenceObject $expectedClassNames -DifferenceObject $actualClassNames)",
                     "if ($classSetDifference.Count -ne 0)",
                     "$classIds.Count -ne [int]$entry.Value",
                     "$passedClassResults.Count -ne [int]$entry.Value"
                 })
        {
            RequireExactlyOnce(executable, token, errors);
        }

        bool allCountsUnmeasured =
            ExpectedTrxTotal == UnmeasuredTrxCount &&
            ExpectedFixtureClasses.All(entry => entry.Count == UnmeasuredTrxCount);
        if (!allCountsUnmeasured &&
            ExpectedTrxTotal != ExpectedFixtureClasses.Sum(entry => entry.Count))
        {
            errors.Add("The expected fixture-gallery total must equal the sum of all nine class counts.");
        }

        if (!allowUnmeasuredCounts &&
            (allCountsUnmeasured || ExpectedTrxTotal <= 0 ||
             ExpectedFixtureClasses.Any(entry => entry.Count <= 0)))
        {
            errors.Add("Debug fixture-gallery TRX counts remain unmeasured.");
        }
    }

    private static void ValidateSerializedClassSet(
        IReadOnlyDictionary<string, string> fixtureSources,
        ICollection<string> errors)
    {
        CategorizedFixtureClass[] discovered = fixtureSources
            .SelectMany(pair => DiscoverCategorizedFixtureClasses(pair.Key, pair.Value))
            .OrderBy(item => item.ClassName, StringComparer.Ordinal)
            .ThenBy(item => item.SourcePath, StringComparer.Ordinal)
            .ToArray();
        string[] expectedClassNames = ExpectedFixtureClasses
            .Select(entry => entry.ClassName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] actualClassNames = discovered
            .Select(item => item.ClassName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!actualClassNames.SequenceEqual(expectedClassNames, StringComparer.Ordinal))
        {
            errors.Add(
                "The repository-wide ModelInspectionFixtureGallery class set must be exactly: " +
                string.Join(", ", expectedClassNames) + ".");
        }

        foreach (var entry in ExpectedFixtureClasses)
        {
            CategorizedFixtureClass[] matches = discovered
                .Where(item => string.Equals(
                    item.ClassName, entry.ClassName, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                errors.Add($"The categorized fixture class must exist exactly once: {entry.ClassName}.");
                continue;
            }

            CategorizedFixtureClass match = matches[0];
            if (!string.Equals(match.SourcePath, entry.SourcePath, StringComparison.Ordinal))
            {
                errors.Add(
                    $"The categorized fixture class moved from {entry.SourcePath}: {entry.ClassName}.");
            }

            if (!match.IsSealed)
            {
                errors.Add($"The categorized fixture class must remain sealed: {entry.ClassName}.");
            }

            if (!match.IsTestClass)
            {
                errors.Add($"The categorized fixture class lost its class-level TestClass attribute: {entry.ClassName}.");
            }

            if (match.CategoryCount != 1)
            {
                errors.Add($"The fixture category must appear exactly once at class level: {entry.ClassName}.");
            }

            if (!match.IsSerialized)
            {
                errors.Add($"The packaged fixture class is no longer serialized: {entry.ClassName}.");
            }
        }
    }

    private static void ValidateDebugOnlyPackageSet(
        string project,
        ICollection<string> errors)
    {
        System.Xml.Linq.XDocument document = System.Xml.Linq.XDocument.Parse(project);
        System.Xml.Linq.XElement[] debugGroups = document.Root!
            .Elements("ItemGroup")
            .Where(group => string.Equals(
                (string?)group.Attribute("Condition"),
                ExactDebugCondition,
                StringComparison.Ordinal))
            .ToArray();
        if (debugGroups.Length != 1)
        {
            errors.Add("The packaged fixture set must have one exact Debug|x64 ItemGroup.");
            return;
        }

        string[] compileIncludes = debugGroups[0].Elements("Compile")
            .Select(item => (string?)item.Attribute("Include"))
            .Where(value => value is not null)
            .Select(value => value!)
            .ToArray();
        foreach (var entry in ExpectedFixtureClasses)
        {
            string sourceName = Path.GetFileName(entry.SourcePath)!;
            string expected = $"Features\\ModelInspection\\DebugFixtures\\{sourceName}";
            if (compileIncludes.Count(value => string.Equals(value, expected, StringComparison.Ordinal)) != 1)
            {
                errors.Add($"The Debug-only packaged class include drifted: {sourceName}.");
            }
        }

        string fixtureRoot = Path.Combine(
            Root, "tests", "TestFixtures", "ModelInspectionScenarios");
        string[] physicalFixtures = Directory.GetFiles(
                fixtureRoot, "*.fixture.json", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path)!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] packagedNames = debugGroups[0].Elements("Content")
            .Select(item => (string?)item.Attribute("Include"))
            .Where(value => value is not null)
            .Select(value => Path.GetFileName(value!)!)
            .Where(value => value.EndsWith(".json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expectedPackageNames = physicalFixtures
            .Append("model-inspection-fixture-coverage-policy.json")
            .Append("model-inspection-fixture.schema.json")
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (physicalFixtures.Length != 50 ||
            !packagedNames.SequenceEqual(expectedPackageNames, StringComparer.Ordinal))
        {
            errors.Add("The Debug package must contain exactly schema, policy, and the 50 physical fixture descriptors.");
        }
    }

    private static void ValidateReleaseIsolation(
        string workflow,
        ICollection<string> errors)
    {
        string? isolation = TryExtractWorkflowStep(workflow, IsolationStepName);
        if (isolation is null)
        {
            errors.Add("The mandatory Release fixture-isolation step is missing.");
            return;
        }

        foreach (string token in new[]
                 {
                     "working-directory: ${{ github.workspace }}",
                     "& '.\\scripts\\model-inspection\\Test-ModelInspectionFixtureReleaseIsolation.ps1'",
                     "TestResults\\ModelInspectionFixtures\\ReleaseIsolation\\release-isolation-evidence.json",
                     "$isolationEvidence.status -ne 'passed'"
                 })
        {
            RequireExactlyOnce(isolation, token, errors);
        }

        if (isolation.Contains("continue-on-error", StringComparison.OrdinalIgnoreCase) ||
            isolation.Contains("if: ${{ always() }}", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Release isolation must be a required fail-closed workflow step.");
        }
    }

    private static string[] ValidateProgressPolishGateFixtureBoundary(
        string script)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(script))
        {
            errors.Add("The progress-polish gate script is missing.");
            return errors.ToArray();
        }

        string executable = RemovePowerShellNonExecutableText(script)
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        string? runtimeWorker = ExtractPowerShellPhaseBody(executable, "RuntimeWorker");
        string? debug = ExtractPowerShellPhaseBody(executable, "Debug");
        string? release = ExtractPowerShellPhaseBody(executable, "Release");
        string? finalSource = ExtractPowerShellPhaseBody(executable, "FinalSource");
        if (runtimeWorker is null || debug is null || release is null || finalSource is null)
        {
            errors.Add("The RuntimeWorker, Debug, Release, and FinalSource gate phases are mandatory and balanced.");
            return errors.ToArray();
        }

        (string Phase, string Body, string[] Commands)[] commandsByPhase =
        [
            ("RuntimeWorker", runtimeWorker,
            [
                "Invoke-CheckedCommand -Name 'runtime-project' -Command {",
                "Invoke-CheckedCommand -Name 'worker-project' -Command {"
            ]),
            ("Debug", debug,
            [
                "Invoke-DebugBuild -EvidenceDirectory $phaseRoot",
                "Invoke-PackagedTests -Name 'InteractionLifetime' -Filter $interactionLifetimeFilter",
                "Invoke-PackagedTests -Name 'FixtureCategory' -Filter $fixtureCategoryFilter",
                "Invoke-PackagedTests -Name 'FocusedPolish' -Filter $polishFilter"
            ]),
            ("Release", release,
            [
                "Invoke-ReleaseBuild -EvidenceDirectory $phaseRoot",
                "Invoke-PackagedTests -Name 'HostedRelease' -Filter $hostedReleaseFilter",
                "Invoke-PackagedTests -Name 'N001' -Filter $n001FullyQualifiedName",
                "Invoke-ReleaseIsolation -EvidenceDirectory $phaseRoot"
            ]),
            ("FinalSource", finalSource,
            [
                "Invoke-ContractsGate -EvidenceDirectory $phaseRoot",
                "Invoke-CleanupGate -EvidenceDirectory $phaseRoot",
                "Invoke-DiffGate -EvidenceDirectory $phaseRoot",
                "Invoke-ReleaseIsolation -EvidenceDirectory $phaseRoot"
            ])
        ];
        var expectedGlobalCommandCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach ((string phase, string body, string[] commands) in commandsByPhase)
        {
            int previous = -1;
            foreach (string command in commands)
            {
                int current = IndexOfExactPowerShellStatement(body, command);
                if (current <= previous || CountExactPowerShellStatements(body, command) != 1)
                {
                    errors.Add(
                        $"The {phase} gate command is missing, duplicated, or out of order: {command}");
                }

                previous = current;
                expectedGlobalCommandCounts[command] =
                    expectedGlobalCommandCounts.GetValueOrDefault(command) + 1;
            }
        }

        foreach ((string command, int expectedCount) in expectedGlobalCommandCounts)
        {
            int actualCount = CountExactPowerShellStatements(executable, command);
            if (actualCount != expectedCount)
            {
                errors.Add(
                    $"The phase command must occur exactly {expectedCount} time(s), found {actualCount}: {command}");
            }
        }

        const string ExpectedTotalsStart = "$ExpectedTotals = [ordered]@{";
        const string ExpectedTotalsEnd = "$ExpectedTestMaps = [ordered]@{";
        int expectedTotalsStart = executable.IndexOf(ExpectedTotalsStart, StringComparison.Ordinal);
        int expectedTotalsEnd = expectedTotalsStart < 0
            ? -1
            : executable.IndexOf(
                ExpectedTotalsEnd,
                expectedTotalsStart + ExpectedTotalsStart.Length,
                StringComparison.Ordinal);
        string expectedTotals = expectedTotalsStart >= 0 && expectedTotalsEnd > expectedTotalsStart
            ? executable[expectedTotalsStart..expectedTotalsEnd]
            : string.Empty;
        string[] exactExpectedTotals =
        [
            "Runtime = 189",
            "Worker = 77",
            "InteractionLifetime = 19",
            "FixtureCategory = 220",
            "FocusedPolish = 326",
            "HostedRelease = 858",
            "N001 = 1",
            "Contracts = 357"
        ];
        string[] actualExpectedTotals = Regex.Matches(
                expectedTotals,
                @"(?m)^\s*(?<name>[A-Za-z][A-Za-z0-9]*)\s*=\s*(?<value>\d+)\s*$",
                RegexOptions.CultureInvariant)
            .Select(match =>
                $"{match.Groups["name"].Value} = {match.Groups["value"].Value}")
            .ToArray();
        if (!actualExpectedTotals.SequenceEqual(exactExpectedTotals, StringComparer.Ordinal))
        {
            errors.Add("The ordered ExpectedTotals map must contain exactly the eight measured totals.");
        }

        foreach (string entry in exactExpectedTotals)
        {
            if (CountOccurrences(expectedTotals, entry) != 1 ||
                CountOccurrences(executable, entry) != 1)
            {
                errors.Add($"The gate measured-total map drifted: {entry}");
            }
        }

        if (Regex.IsMatch(
                executable,
                @"\$(?!ExpectedTotal\b)[A-Za-z_][A-Za-z0-9_]*ExpectedTotal\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            errors.Add("The gate must keep each measured total only in the ordered ExpectedTotals map.");
        }

        const string FixtureMapStart = "FixtureCategory = [ordered]@{";
        const string FixtureMapEnd = "FocusedPolish = [ordered]@{";
        int fixtureMapStart = script.IndexOf(FixtureMapStart, StringComparison.Ordinal);
        int fixtureMapEnd = fixtureMapStart < 0
            ? -1
            : script.IndexOf(FixtureMapEnd, fixtureMapStart, StringComparison.Ordinal);
        string fixtureMap = fixtureMapStart >= 0 && fixtureMapEnd > fixtureMapStart
            ? script[fixtureMapStart..fixtureMapEnd]
            : string.Empty;
        foreach ((string _, string className, int count) in ExpectedFixtureClasses)
        {
            string entry = $"'{className}' = {count}";
            if (CountOccurrences(fixtureMap, entry) != 1)
            {
                errors.Add($"The gate fixture class map drifted: {entry}");
            }
        }

        return errors.ToArray();
    }

    private static string? ExtractPowerShellPhaseBody(string script, string phase)
    {
        string marker = $"'{phase}' {{";
        int[] markerIndices = FindExactPowerShellStatementIndices(script, marker);
        if (markerIndices.Length != 1)
        {
            return null;
        }

        int openBrace = markerIndices[0] + marker.LastIndexOf('{');
        int depth = 0;
        bool inSingleQuotedString = false;
        bool inDoubleQuotedString = false;
        for (int index = openBrace; index < script.Length; index++)
        {
            char current = script[index];
            if (inSingleQuotedString)
            {
                if (current == '\'' && index + 1 < script.Length && script[index + 1] == '\'')
                {
                    index++;
                }
                else if (current == '\'')
                {
                    inSingleQuotedString = false;
                }

                continue;
            }

            if (inDoubleQuotedString)
            {
                if (current == '`' && index + 1 < script.Length)
                {
                    index++;
                }
                else if (current == '"')
                {
                    inDoubleQuotedString = false;
                }

                continue;
            }

            if (current == '\'')
            {
                inSingleQuotedString = true;
            }
            else if (current == '"')
            {
                inDoubleQuotedString = true;
            }
            else if (current == '{')
            {
                depth++;
            }
            else if (current == '}' && --depth == 0)
            {
                return script[(openBrace + 1)..index];
            }
        }

        return null;
    }

    private static int IndexOfExactPowerShellStatement(
        string script,
        string statement) => FindExactPowerShellStatementIndices(script, statement)
            .FirstOrDefault(-1);

    private static int CountExactPowerShellStatements(
        string script,
        string statement) => FindExactPowerShellStatementIndices(script, statement).Length;

    private static int[] FindExactPowerShellStatementIndices(
        string script,
        string statement)
    {
        var indices = new List<int>();
        bool inSingleQuotedString = false;
        bool inDoubleQuotedString = false;
        int lineStart = 0;
        while (lineStart <= script.Length)
        {
            int newline = script.IndexOf('\n', lineStart);
            int lineEnd = newline >= 0 ? newline : script.Length;
            bool lineStartsInQuotedString = inSingleQuotedString || inDoubleQuotedString;
            int contentStart = lineStart;
            while (contentStart < lineEnd &&
                   (script[contentStart] == ' ' || script[contentStart] == '\t'))
            {
                contentStart++;
            }

            int contentEnd = lineEnd;
            while (contentEnd > contentStart &&
                   (script[contentEnd - 1] == ' ' ||
                    script[contentEnd - 1] == '\t' ||
                    script[contentEnd - 1] == '\r'))
            {
                contentEnd--;
            }

            if (!lineStartsInQuotedString &&
                script.AsSpan(contentStart, contentEnd - contentStart)
                    .SequenceEqual(statement.AsSpan()))
            {
                indices.Add(contentStart);
            }

            for (int index = lineStart; index < lineEnd; index++)
            {
                char current = script[index];
                if (inSingleQuotedString)
                {
                    if (current == '\'' && index + 1 < lineEnd && script[index + 1] == '\'')
                    {
                        index++;
                    }
                    else if (current == '\'')
                    {
                        inSingleQuotedString = false;
                    }

                    continue;
                }

                if (inDoubleQuotedString)
                {
                    if (current == '`' && index + 1 < lineEnd)
                    {
                        index++;
                    }
                    else if (current == '"')
                    {
                        inDoubleQuotedString = false;
                    }

                    continue;
                }

                if (current == '\'')
                {
                    inSingleQuotedString = true;
                }
                else if (current == '"')
                {
                    inDoubleQuotedString = true;
                }
            }

            if (newline < 0)
            {
                break;
            }

            lineStart = newline + 1;
        }

        return indices.ToArray();
    }

    private static void ValidateCampaignOrder(
        string workflow,
        ICollection<string> errors)
    {
        int debugIndex = FindWorkflowStepIndex(workflow, CampaignStepName);
        int releaseIndex = FindWorkflowStepIndex(workflow, HostedReleaseStepName);
        int isolationIndex = FindWorkflowStepIndex(workflow, IsolationStepName);
        if (debugIndex >= 0 && releaseIndex >= 0 && isolationIndex >= 0 &&
            !(debugIndex < releaseIndex && releaseIndex < isolationIndex))
        {
            errors.Add(
                "Workflow order must be Debug fixture gallery, unchanged Release hosted campaign, then Release isolation.");
        }
    }

    private static string CreateMutationBaselineWorkflow()
    {
        string workflow = ReadWorkflow();
        int campaignCount = CountWorkflowSteps(workflow, CampaignStepName);
        int isolationCount = CountWorkflowSteps(workflow, IsolationStepName);
        if (campaignCount != 0 || isolationCount != 0)
        {
            return workflow;
        }

        string release = ExtractWorkflowStep(workflow, HostedReleaseStepName);
        string replacement =
            CreateUnmeasuredCampaignStep() + "\n\n      " +
            release + "\n\n      " +
            CreateReleaseIsolationStep();
        return workflow.Replace(release, replacement, StringComparison.Ordinal);
    }

    private static string CreateUnmeasuredCampaignStep()
    {
        string map = string.Join("\n", ExpectedFixtureClasses.Select(entry =>
            $"            '{entry.ClassName}' = {entry.Count}"));
        return
            $"- name: {CampaignStepName}\n" +
            "        run: |\n" +
            "          $fixtureGalleryConfiguration = 'Debug'\n" +
            "          $fixtureGalleryPlatform = 'x64'\n" +
            "          msbuild \"$env:APP_PROJECT\" /target:Restore,Build /property:Configuration=$fixtureGalleryConfiguration /property:Platform=$fixtureGalleryPlatform\n" +
            "          if ($LASTEXITCODE -ne 0) { throw 'Debug fixture-gallery app build failed.' }\n" +
            "          dotnet build \"$env:TEST_PROJECT\" --configuration $fixtureGalleryConfiguration --runtime win-x64 -p:Platform=$fixtureGalleryPlatform\n" +
            "          if ($LASTEXITCODE -ne 0) { throw 'Debug fixture-gallery test build failed.' }\n" +
            "          $fixtureGalleryRecipe = 'D:/s/tests/UnitTests/GraniteEdgeAI.UnitTests/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.UnitTests.build.appxrecipe'\n" +
            "          $fixtureGalleryTrxPath = 'D:/s/TestResults/ModelInspectionFixtures/ModelInspectionFixtureGallery.trx'\n" +
            "          $vswhere = \"${env:ProgramFiles(x86)}\\Microsoft Visual Studio\\Installer\\vswhere.exe\"\n" +
            "          $vstest = & $vswhere -latest -products * -find '**\\Common7\\IDE\\CommonExtensions\\Microsoft\\TestWindow\\vstest.console.exe' | Select-Object -First 1\n" +
            "          if (-not $vstest) { throw 'Visual Studio app-container test runner was not found.' }\n" +
            "          try {\n" +
            "            & $vstest $fixtureGalleryRecipe /Platform:x64 /Logger:\"trx;LogFileName=ModelInspectionFixtureGallery.trx\" /ResultsDirectory:\"D:/s/TestResults/ModelInspectionFixtures\" " + ExactCategoryFilter + "\n" +
            "            if ($LASTEXITCODE -ne 0) { throw 'Debug fixture-gallery VSTest failed.' }\n" +
            "            if (-not (Test-Path -LiteralPath $fixtureGalleryTrxPath -PathType Leaf)) { throw 'Fixture-gallery TRX was not created.' }\n" +
            "            [xml]$trx = Get-Content -LiteralPath $fixtureGalleryTrxPath -Raw\n" +
            $"            $fixtureGalleryExpectedTotal = {ExpectedTrxTotal}\n" +
            "            $fixtureGalleryExpectedClassCounts = [ordered]@{\n" + map + "\n            }\n" +
            "            $fixtureGalleryExpectedClassTotal = [int](($fixtureGalleryExpectedClassCounts.Values | Measure-Object -Sum).Sum)\n" +
            "            if ($fixtureGalleryExpectedClassTotal -ne $fixtureGalleryExpectedTotal) { throw 'Fixture-gallery expected class counts do not sum to the expected total.' }\n" +
            "            $counters = $trx.TestRun.ResultSummary.Counters\n" +
            "            if ([int]$counters.total -ne $fixtureGalleryExpectedTotal -or [int]$counters.executed -ne $fixtureGalleryExpectedTotal -or [int]$counters.passed -ne $fixtureGalleryExpectedTotal) { throw 'Fixture-gallery total drifted.' }\n" +
            "            $definitions = @($trx.TestRun.TestDefinitions.UnitTest)\n" +
            "            $results = @($trx.TestRun.Results.UnitTestResult)\n" +
            "            $actualClassNames = @($definitions | ForEach-Object { [string]$_.TestMethod.className } | Sort-Object -Unique)\n" +
            "            $expectedClassNames = @($fixtureGalleryExpectedClassCounts.Keys | Sort-Object)\n" +
            "            $classSetDifference = @(Compare-Object -ReferenceObject $expectedClassNames -DifferenceObject $actualClassNames)\n" +
            "            if ($classSetDifference.Count -ne 0) { throw 'Fixture-gallery class set drifted.' }\n" +
            "            foreach ($entry in $fixtureGalleryExpectedClassCounts.GetEnumerator()) {\n" +
            "              $classIds = @($definitions | Where-Object { $_.TestMethod.className -eq $entry.Key } | ForEach-Object { $_.id })\n" +
            "              $passedClassResults = @($results | Where-Object { $_.testId -in $classIds -and $_.outcome -eq 'Passed' })\n" +
            "              if ($classIds.Count -ne [int]$entry.Value -or $passedClassResults.Count -ne [int]$entry.Value) { throw 'Fixture-gallery class count drifted.' }\n" +
            "            }\n" +
            "          } finally {\n" +
            "            if (Test-Path -LiteralPath $fixtureGalleryTrxPath -PathType Leaf) {\n" +
            "              Remove-Item -LiteralPath $fixtureGalleryTrxPath -Force\n" +
            "            }\n" +
            "          }\n" +
            "          if (Test-Path -LiteralPath $fixtureGalleryTrxPath -PathType Leaf) { throw 'Raw fixture-gallery TRX was retained.' }";
    }

    private static string CreateReleaseIsolationStep() =>
            $"- name: {IsolationStepName}\n" +
            "        working-directory: ${{ github.workspace }}\n" +
            "        run: |\n" +
            "          $evidencePath = Join-Path $env:GITHUB_WORKSPACE 'TestResults\\ModelInspectionFixtures\\ReleaseIsolation\\release-isolation-evidence.json'\n" +
            "          & '.\\scripts\\model-inspection\\Test-ModelInspectionFixtureReleaseIsolation.ps1' -EvidencePath $evidencePath\n" +
            "          $isolationEvidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json\n" +
            "          if ($isolationEvidence.status -ne 'passed') { throw 'Release isolation did not pass.' }\n";

    private static void RequireExactlyOnce(
        string value,
        string token,
        ICollection<string> errors)
    {
        if (CountOccurrences(value, token) != 1)
        {
            errors.Add($"Expected exactly one executable campaign token: {token}");
        }
    }

    private static string RemovePowerShellComments(string value) =>
        System.Text.RegularExpressions.Regex.Replace(
            value,
            @"(?m)#.*$",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string RemovePowerShellNonExecutableText(string script)
    {
        char[] sanitized = script.ToCharArray();
        bool inSingleQuotedString = false;
        bool inDoubleQuotedString = false;
        int blockCommentDepth = 0;
        char hereStringQuote = '\0';
        bool onlyWhitespaceSinceLineStart = true;

        for (int index = 0; index < script.Length; index++)
        {
            char current = script[index];
            if (current is '\r' or '\n')
            {
                onlyWhitespaceSinceLineStart = true;
                continue;
            }

            if (hereStringQuote != '\0')
            {
                if (onlyWhitespaceSinceLineStart &&
                    current == hereStringQuote &&
                    index + 1 < script.Length &&
                    script[index + 1] == '@' &&
                    PowerShellLineRemainderIsWhitespace(script, index + 2))
                {
                    sanitized[index] = ' ';
                    sanitized[index + 1] = ' ';
                    index++;
                    hereStringQuote = '\0';
                    onlyWhitespaceSinceLineStart = false;
                    continue;
                }

                sanitized[index] = ' ';
                if (current is not (' ' or '\t'))
                {
                    onlyWhitespaceSinceLineStart = false;
                }

                continue;
            }

            if (blockCommentDepth > 0)
            {
                sanitized[index] = ' ';
                if (current == '<' &&
                    index + 1 < script.Length &&
                    script[index + 1] == '#')
                {
                    sanitized[index + 1] = ' ';
                    blockCommentDepth++;
                    index++;
                }
                else if (current == '#' &&
                         index + 1 < script.Length &&
                         script[index + 1] == '>')
                {
                    sanitized[index + 1] = ' ';
                    blockCommentDepth--;
                    index++;
                }

                continue;
            }

            if (inSingleQuotedString)
            {
                if (current == '\'' &&
                    index + 1 < script.Length &&
                    script[index + 1] == '\'')
                {
                    index++;
                }
                else if (current == '\'')
                {
                    inSingleQuotedString = false;
                }

                onlyWhitespaceSinceLineStart = false;
                continue;
            }

            if (inDoubleQuotedString)
            {
                if (current == '`' && index + 1 < script.Length)
                {
                    index++;
                }
                else if (current == '"')
                {
                    inDoubleQuotedString = false;
                }

                onlyWhitespaceSinceLineStart = false;
                continue;
            }

            if (current == '#')
            {
                while (index < script.Length &&
                       script[index] is not ('\r' or '\n'))
                {
                    sanitized[index++] = ' ';
                }

                index--;
                continue;
            }

            if (current == '<' &&
                index + 1 < script.Length &&
                script[index + 1] == '#')
            {
                sanitized[index] = ' ';
                sanitized[index + 1] = ' ';
                blockCommentDepth = 1;
                index++;
                continue;
            }

            if (current == '@' &&
                index + 1 < script.Length &&
                (script[index + 1] is '\'' or '"') &&
                PowerShellLineRemainderIsWhitespace(script, index + 2))
            {
                hereStringQuote = script[index + 1];
                sanitized[index] = ' ';
                sanitized[index + 1] = ' ';
                index++;
                onlyWhitespaceSinceLineStart = false;
                continue;
            }

            if (current == '\'')
            {
                inSingleQuotedString = true;
            }
            else if (current == '"')
            {
                inDoubleQuotedString = true;
            }

            if (current is not (' ' or '\t'))
            {
                onlyWhitespaceSinceLineStart = false;
            }
        }

        return new string(sanitized);
    }

    private static bool PowerShellLineRemainderIsWhitespace(
        string script,
        int offset)
    {
        for (int index = offset;
             index < script.Length && script[index] != '\n';
             index++)
        {
            if (script[index] is not (' ' or '\t' or '\r'))
            {
                return false;
            }
        }

        return true;
    }

    private static int CountOccurrences(string value, string token)
    {
        int count = 0;
        int offset = 0;
        while ((offset = value.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += token.Length;
        }

        return count;
    }

    private static string ExtractWorkflowStep(string workflow, string stepName) =>
        TryExtractWorkflowStep(workflow, stepName) ??
        throw new AssertFailedException($"Workflow step was not found: {stepName}");

    private static string? TryExtractWorkflowStep(string workflow, string stepName)
    {
        System.Text.RegularExpressions.Match marker = FindWorkflowStep(workflow, stepName);
        if (!marker.Success)
        {
            return null;
        }

        int start = marker.Index + marker.Groups["indent"].Length;
        string nextPattern =
            $@"(?m)^{System.Text.RegularExpressions.Regex.Escape(marker.Groups["indent"].Value)}-\s+name:";
        System.Text.RegularExpressions.Match next =
            System.Text.RegularExpressions.Regex.Match(
                workflow,
                nextPattern,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        while (next.Success && next.Index <= marker.Index)
        {
            next = next.NextMatch();
        }

        return next.Success ? workflow[start..next.Index] : workflow[start..];
    }

    private static int CountWorkflowSteps(string workflow, string stepName) =>
        System.Text.RegularExpressions.Regex.Matches(
            workflow,
            WorkflowStepPattern(stepName),
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1)).Count;

    private static int FindWorkflowStepIndex(string workflow, string stepName)
    {
        System.Text.RegularExpressions.Match match = FindWorkflowStep(workflow, stepName);
        return match.Success ? match.Index : -1;
    }

    private static System.Text.RegularExpressions.Match FindWorkflowStep(
        string workflow,
        string stepName) =>
        System.Text.RegularExpressions.Regex.Match(
            workflow,
            WorkflowStepPattern(stepName),
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

    private static string WorkflowStepPattern(string stepName) =>
        $@"(?m)^(?<indent>[ \t]*)-\s+name:\s*{System.Text.RegularExpressions.Regex.Escape(stepName)}[ \t]*\r?$";

    private static string SwapWorkflowSteps(
        string workflow,
        string firstStepName,
        string secondStepName)
    {
        const string Placeholder = "__MODEL_INSPECTION_WORKFLOW_STEP_SWAP__";
        Assert.IsFalse(workflow.Contains(Placeholder, StringComparison.Ordinal));
        string first = ExtractWorkflowStep(workflow, firstStepName);
        string second = ExtractWorkflowStep(workflow, secondStepName);
        return workflow
            .Replace(first, Placeholder, StringComparison.Ordinal)
            .Replace(second, first, StringComparison.Ordinal)
            .Replace(Placeholder, second, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> ReadFixtureClassSources()
    {
        string directory = Path.Combine(
            Root, "tests", "UnitTests", "GraniteEdgeAI.UnitTests");
        return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Select(path => new
            {
                Path = path,
                RelativePath = Path.GetRelativePath(directory, path).Replace('\\', '/')
            })
            .Where(item => !IsGeneratedSourcePath(item.RelativePath))
            .ToDictionary(
                item => item.RelativePath,
                item => File.ReadAllText(item.Path),
                StringComparer.Ordinal);
    }

    private static bool IsGeneratedSourcePath(string relativePath) =>
        relativePath.Split('/').Any(segment =>
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
            segment.StartsWith(".task", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<CategorizedFixtureClass> DiscoverCategorizedFixtureClasses(
        string sourcePath,
        string source)
    {
        string codeMask = MaskCSharpTrivia(source, maskLiterals: true);
        string commentFreeSource = MaskCSharpTrivia(source, maskLiterals: false);
        const string ClassPattern =
            @"\b(?<modifiers>(?:(?:public|internal|private|protected|static|abstract|sealed|partial|new|unsafe|file)\s+)*)class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)";
        const string CategoryPattern =
            "\\[\\s*(?:Microsoft\\.VisualStudio\\.TestTools\\.UnitTesting\\.)?TestCategory(?:Attribute)?\\s*\\(\\s*\"ModelInspectionFixtureGallery\"\\s*\\)\\s*\\]";
        const string SerializationPattern =
            @"\[\s*(?:Microsoft\.VisualStudio\.TestTools\.UnitTesting\.)?DoNotParallelize(?:Attribute)?\s*(?:\(\s*\))?\s*\]";
        const string TestClassPattern =
            @"\[\s*(?:Microsoft\.VisualStudio\.TestTools\.UnitTesting\.)?TestClass(?:Attribute)?\s*(?:\(\s*\))?\s*\]";

        foreach (System.Text.RegularExpressions.Match declaration in
                 System.Text.RegularExpressions.Regex.Matches(
                     codeMask,
                     ClassPattern,
                     System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                     TimeSpan.FromSeconds(1)).Cast<System.Text.RegularExpressions.Match>())
        {
            int attributeStart = FindImmediateAttributeBlockStart(codeMask, declaration.Index);
            if (attributeStart == declaration.Index)
            {
                continue;
            }

            string attributes = commentFreeSource[attributeStart..declaration.Index];
            int categoryCount = System.Text.RegularExpressions.Regex.Matches(
                attributes,
                CategoryPattern,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1)).Count;
            if (categoryCount == 0)
            {
                continue;
            }

            string namespaceName = FindNamespace(codeMask, declaration.Index);
            string shortName = declaration.Groups["name"].Value;
            yield return new CategorizedFixtureClass(
                sourcePath,
                string.IsNullOrEmpty(namespaceName) ? shortName : $"{namespaceName}.{shortName}",
                categoryCount,
                System.Text.RegularExpressions.Regex.IsMatch(
                    attributes,
                    SerializationPattern,
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(1)),
                System.Text.RegularExpressions.Regex.IsMatch(
                    attributes,
                    TestClassPattern,
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(1)),
                declaration.Groups["modifiers"].Value
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .Contains("sealed", StringComparer.Ordinal));
        }
    }

    private static int FindImmediateAttributeBlockStart(string codeMask, int declarationStart)
    {
        int cursor = declarationStart - 1;
        while (cursor >= 0 && char.IsWhiteSpace(codeMask[cursor]))
        {
            cursor--;
        }

        int firstAttribute = declarationStart;
        while (cursor >= 0 && codeMask[cursor] == ']')
        {
            int depth = 1;
            cursor--;
            while (cursor >= 0 && depth > 0)
            {
                if (codeMask[cursor] == ']')
                {
                    depth++;
                }
                else if (codeMask[cursor] == '[')
                {
                    depth--;
                }

                cursor--;
            }

            if (depth != 0)
            {
                return declarationStart;
            }

            firstAttribute = cursor + 1;
            while (cursor >= 0 && char.IsWhiteSpace(codeMask[cursor]))
            {
                cursor--;
            }
        }

        return firstAttribute;
    }

    private static string FindNamespace(string codeMask, int declarationStart)
    {
        const string NamespacePattern =
            @"\bnamespace\s+(?<name>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\s*[;{]";
        System.Text.RegularExpressions.MatchCollection matches =
            System.Text.RegularExpressions.Regex.Matches(
                codeMask[..declarationStart],
                NamespacePattern,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        return matches.Count == 0
            ? string.Empty
            : matches[matches.Count - 1].Groups["name"].Value;
    }

    private static string MaskCSharpTrivia(string source, bool maskLiterals)
    {
        char[] masked = source.ToCharArray();

        void Mask(int start, int end)
        {
            for (int index = start; index < end; index++)
            {
                if (masked[index] is not ('\r' or '\n'))
                {
                    masked[index] = ' ';
                }
            }
        }

        int offset = 0;
        while (offset < source.Length)
        {
            if (offset + 1 < source.Length &&
                source[offset] == '/' && source[offset + 1] == '/')
            {
                int end = source.IndexOf('\n', offset + 2);
                end = end < 0 ? source.Length : end;
                Mask(offset, end);
                offset = end;
                continue;
            }

            if (offset + 1 < source.Length &&
                source[offset] == '/' && source[offset + 1] == '*')
            {
                int closing = source.IndexOf("*/", offset + 2, StringComparison.Ordinal);
                int end = closing < 0 ? source.Length : closing + 2;
                Mask(offset, end);
                offset = end;
                continue;
            }

            if (source[offset] == '\'')
            {
                int end = offset + 1;
                while (end < source.Length)
                {
                    if (source[end] == '\\' && end + 1 < source.Length)
                    {
                        end += 2;
                        continue;
                    }

                    if (source[end++] == '\'')
                    {
                        break;
                    }
                }

                if (maskLiterals)
                {
                    Mask(offset, end);
                }

                offset = end;
                continue;
            }

            if (source[offset] == '"')
            {
                int quoteCount = 1;
                while (offset + quoteCount < source.Length &&
                       source[offset + quoteCount] == '"')
                {
                    quoteCount++;
                }

                int end;
                if (quoteCount >= 3)
                {
                    end = offset + quoteCount;
                    while (end < source.Length)
                    {
                        if (source[end] != '"')
                        {
                            end++;
                            continue;
                        }

                        int closingCount = 1;
                        while (end + closingCount < source.Length &&
                               source[end + closingCount] == '"')
                        {
                            closingCount++;
                        }

                        if (closingCount >= quoteCount)
                        {
                            end += quoteCount;
                            break;
                        }

                        end += closingCount;
                    }
                }
                else
                {
                    bool verbatim = offset > 0 && source[offset - 1] == '@';
                    end = offset + 1;
                    while (end < source.Length)
                    {
                        if (verbatim && source[end] == '"' &&
                            end + 1 < source.Length && source[end + 1] == '"')
                        {
                            end += 2;
                            continue;
                        }

                        if (!verbatim && source[end] == '\\' && end + 1 < source.Length)
                        {
                            end += 2;
                            continue;
                        }

                        if (source[end++] == '"')
                        {
                            break;
                        }
                    }
                }

                if (maskLiterals)
                {
                    Mask(offset, end);
                }

                offset = end;
                continue;
            }

            offset++;
        }

        return new string(masked);
    }

    private sealed record CategorizedFixtureClass(
        string SourcePath,
        string ClassName,
        int CategoryCount,
        bool IsSerialized,
        bool IsTestClass,
        bool IsSealed);

    private static string ReadWorkflow() => File.ReadAllText(Path.Combine(
        Root, ".github", "workflows", "build-and-test.yml"));

    private static string ReadProgressPolishGate()
    {
        string path = Path.Combine(
            Root,
            "scripts",
            "model-inspection",
            "Invoke-ModelInspectionProgressPolishGate.ps1");
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static string ReadUnitTestProject() => File.ReadAllText(Path.Combine(
        Root, "tests", "UnitTests", "GraniteEdgeAI.UnitTests",
        "GraniteEdgeAI.UnitTests.csproj"));

    private static string FindRepositoryRoot()
    {
        foreach (string startingPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            DirectoryInfo? directory = new(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                    File.Exists(Path.Combine(directory.FullName, ".github", "workflows", "build-and-test.yml")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
