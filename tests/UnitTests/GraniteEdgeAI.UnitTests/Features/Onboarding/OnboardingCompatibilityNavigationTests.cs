using GraniteEdgeAI.Features.HardwareInspection;
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.ViewModels;
using GraniteEdgeAI.Features.ApplicationComposition;
using GraniteEdgeAI.Features.ModelHardwareCompatibility;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.Features.ModelOptimization;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection;
using GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using GraniteEdgeAI.UnitTests.Features.ModelOptimization;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.IO;

namespace GraniteEdgeAI.UnitTests.Features.Onboarding;

using InspectionOutcome = GraniteEdgeAI.Features.ModelInspection.Contracts.ModelInspectionOutcome;

[TestClass]
[DoNotParallelize]
public sealed class OnboardingCompatibilityNavigationTests
{
    [UITestMethod]
    public async Task OpenVinoReplanImportRetiresJourneyAndGgufCannotUseThatRecovery()
    {
        foreach (OptimizationRoute route in new[] { OptimizationRoute.OpenVino, OptimizationRoute.Gguf })
        {
            var entry = OptimizationSelectionHandoffTests.RequiredJourneyEntry(route);
            await using var coordinator = new OptimizationJourneyCoordinator(entry,
                new OptimizationExecutorRouter([]), new RecoveryRevalidator(), new UnusedRecoveryContextFactory());
            await coordinator.ConfirmAsync();
            Assert.AreEqual(OptimizationJourneyKind.ReplanRequired, coordinator.State.Kind);
            var shell = new OnboardingShellPage();
            try
            {
            var page = new OptimizationPage(entry);
            var plan = entry.OptimizationHandoff.Plan;
            // Even a spoofed OpenVINO presentation must not bypass the shell's GGUF route guard.
            page.ApplyPresentation(OptimizationPresentationFactory.ReplanRequired(plan.Preference,
                OptimizationConfigurationProjection.From(plan), OptimizationRoute.OpenVino));
            var frame = (Frame)shell.FindName("StageFrame");
            frame.Content = page;
            SetPrivateField(shell, "_attachedOptimizationPage", page);
            SetPrivateField(shell, "_optimizationCoordinator", coordinator);
            SetPrivateField(shell, "_currentStage", OnboardingStage.ConfigureModel);
            frame.BackStack.Add(new PageStackEntry(typeof(ModelImportPage), null, null));
            Assert.AreEqual(true, typeof(OptimizationPage).GetMethod("TryBeginImportNavigation",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(page, null));
            await (Task)typeof(OnboardingShellPage).GetMethod("HandleOptimizationIntentAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(shell, [coordinator, OptimizationCommand.ImportAnotherModel])!;
            if (route == OptimizationRoute.OpenVino)
            {
                var imported = Assert.IsInstanceOfType<ModelImportPage>(frame.Content);
                Assert.AreEqual(ModelImportPresentationMode.AllSources, GetPrivateField(imported, "_presentationMode"));
                Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
                Assert.IsTrue(coordinator.IsRetired);
                Assert.IsNull(GetPrivateField(shell, "_optimizationCoordinator"));
                Assert.IsNull(GetPrivateField(shell, "_attachedOptimizationPage"));
                Assert.AreEqual(0, frame.BackStack.Count);
                Assert.AreEqual(0, frame.ForwardStack.Count);
            }
            else
            {
                Assert.AreSame(page, frame.Content);
                Assert.AreEqual(OnboardingStage.ConfigureModel, shell.CurrentStage);
                Assert.IsFalse(coordinator.IsRetired);
                Assert.IsFalse(page.IsImportNavigationPending);
            }
            }
            finally
            {
                await shell.ShutdownAsync();
            }
        }
    }

    private sealed class RecoveryRevalidator : IOptimizationRevalidator
    {
        public Task<OptimizationRevalidationResult> RevalidateAsync(OptimizationExecutionPlan plan,
            CancellationToken cancellationToken) => Task.FromResult(
                new OptimizationRevalidationResult(false, OptimizationSupportCode.CapabilityDrift));
    }

    private sealed class UnusedRecoveryContextFactory : IOptimizationAttemptContextFactory
    {
        public Task<OptimizationAttemptContext> CreateAsync(long generation, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A replan recovery must not start staging.");
    }

    private static readonly Guid ModelRunId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid OpenVinoHandoffId =
        Guid.Parse("33333333-3333-4333-8333-333333333333");
    private ResourceDictionary? _journeyResources;
    private readonly string _modelFixtureRoot = Path.Combine(
        Path.GetTempPath(), "granite-navigation-" + Guid.NewGuid().ToString("N"));

    [TestMethod]
    public void SuccessfulImportIntentInstallsAllSourcesBeforeRetiringTheOldJourney()
    {
        string source = File.ReadAllText(Path.Combine(
            RepositoryTestPaths.FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)", "Features", "Onboarding",
            "OnboardingShellPage.xaml.cs"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        const string optimizationImportSignature =
            "private async Task ImportAnotherModelAsync(\n" +
            "            OptimizationJourneyCoordinator coordinator)";
        int method = source.IndexOf(
            optimizationImportSignature,
            StringComparison.Ordinal);
        int methodEnd = source.IndexOf(
            "private async Task LaunchOptimizedChatAsync(",
            method,
            StringComparison.Ordinal);
        Assert.IsTrue(method >= 0 && method < methodEnd,
            "The exact optimization-result import method must be present.");
        string methodBody = source[method..methodEnd];
        int install = methodBody.IndexOf(
            "StageFrame.Navigate(typeof(ModelImportPage))",
            StringComparison.Ordinal);
        int mode = methodBody.IndexOf(
            "importPage.SetPresentationMode(ModelImportPresentationMode.AllSources)",
            install, StringComparison.Ordinal);
        int retire = methodBody.IndexOf(
            "await RetireOptimizationAsync()", mode, StringComparison.Ordinal);
        int detach = methodBody.IndexOf(
            "DetachCompatibilityPage()", retire, StringComparison.Ordinal);
        int stage = methodBody.IndexOf(
            "CurrentStage = OnboardingStage.ImportModel", detach,
            StringComparison.Ordinal);

        Assert.IsTrue(install < mode && mode < retire);
        Assert.IsTrue(retire < detach && detach < stage);
        StringAssert.Contains(source,
            "ReferenceEquals(StageFrame.Content, sourcePage)");
        StringAssert.Contains(source,
            "coordinator.State.Result is not { IsSuccessful: true }");
        StringAssert.Contains(source,
            "sourcePage.CancelImportNavigation()");
    }

    [TestMethod]
    public void OptimizationSuccessBindingKeepsPersistentAndRuntimeExportsTypedAndSeparate()
    {
        string source = File.ReadAllText(Path.Combine(
            RepositoryTestPaths.FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)", "Features", "Onboarding",
            "OnboardingShellPage.xaml.cs"));

        StringAssert.Contains(source,
            "VerifiedPersistentExportTarget.FromExecutionResult(result)");
        StringAssert.Contains(source,
            "page.BindVerifiedExport(target, exportService)");
        StringAssert.Contains(source,
            "VerifiedGgufRuntimeBundleExportTarget.FromExecution(plan, runtimeResult)");
        StringAssert.Contains(source,
            "CreateGgufRuntimeProfileBundleExportService(");
        StringAssert.Contains(source,
            "page.BindVerifiedRuntimeBundleExport(runtimeTarget, runtimeExportService)");
        StringAssert.Contains(source,
            "GgufRuntimeProfileBundleExportService.TryCreateAbsentDestination(");
        StringAssert.Contains(source,
            "PickGgufRuntimeBundleExportDestinationAsync(");
        Assert.IsFalse(source.Contains(
            "CreateAbsentOptimizationDestination(selected.Path, OptimizationRoute.Gguf)",
            StringComparison.Ordinal));
    }

    [TestInitialize]
    public void InstallJourneyResources()
    {
        if (Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread() is null)
        {
            return;
        }

        _journeyResources = new ResourceDictionary
        {
            Source = new Uri(
                "ms-appx:///Features/Onboarding/Presentation/GraniteJourneyActionPalette.xaml")
        };
        Application.Current.Resources.MergedDictionaries.Add(_journeyResources);
    }

    [TestCleanup]
    public void RemoveJourneyResources()
    {
        if (Directory.Exists(_modelFixtureRoot))
        {
            Directory.Delete(_modelFixtureRoot, recursive: true);
        }
        if (_journeyResources is not null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(_journeyResources);
            _journeyResources = null;
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task MatchingCompletedJourney_NavigatesOnceAndBackDoesNotRerunHardware()
    {
        ModelInspectionExecutionResult terminal = Terminal();
        ModelInspectionHandoff modelHandoff = ModelHandoff(terminal);
        ModelInspectionPage source = CreateSourcePage();
        var service = new CountingHardwareService();
        var fresh = new SequencedFreshResourcesSource(
            FreshResources(24UL * 1024 * 1024 * 1024));
        var shell = new OnboardingShellPage(
            static (frame, request) => frame.Navigate(typeof(ModelInspectionPage), request),
            service,
            hardwareInspectionNavigator: null,
            hardwareHandoffReissuer: null,
            fresh,
            (_, handoff) => handoff.ModelInspectionRunId == ModelRunId ? terminal : null,
            compatibilityNavigator: null);
        shell.AttachModelInspectionPage(source);
        Assert.IsTrue(shell.NavigateToHardwareInspection(source, modelHandoff));
        var frame = (Frame)shell.FindName("StageFrame");
        var hardwarePage = (HardwareInspectionPage)frame.Content;
        HardwareInspectionHandoff hardwareHandoff = HardwareInspectionHandoff.Create(
            shell.CurrentProductHardwareRunId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(
                shell.CurrentProductHardwareRunId));

        Assert.IsTrue(await shell.NavigateToCompatibilityAsync(
            hardwarePage,
            new HardwareInspectionCompletedEventArgs(hardwareHandoff)));
        var compatibilityPage = frame.Content as CompatibilityPage;
        Assert.IsNotNull(compatibilityPage);
        var hardwareFacts = Assert.IsInstanceOfType<Expander>(
            compatibilityPage.FindName("CompatibilityHardwareFactsExpander"));
        Assert.AreEqual(
            Microsoft.UI.Xaml.Visibility.Collapsed,
            hardwareFacts.Visibility,
            "A handoff manually injected without a published page report must not fabricate one.");
        await compatibilityPage.ViewModel.StartAsync();
        Assert.IsTrue(
            compatibilityPage.ViewModel.Presentation.PrimaryActionEnabled,
            "A verified current-fit GGUF result must expose its direct Chat destination.");
        Assert.AreEqual("Chat with current model",
            compatibilityPage.ViewModel.Presentation.PrimaryActionText);
        Assert.AreEqual(1, fresh.CallCount);
        Assert.AreEqual(OnboardingStage.CheckHardwareFit, shell.CurrentStage,
            "Unavailable optimisation must not advance step 3.");
        Assert.AreSame(compatibilityPage, frame.Content);
        Assert.IsNull(compatibilityPage.FindName("StageIndicator"),
            "Compatibility remains inside the one onboarding shell.");

        compatibilityPage.ViewModel.BackCommand.Execute(null);

        Assert.AreSame(source, frame.Content);
        Assert.AreEqual(OnboardingStage.InspectModel, shell.CurrentStage);
        Assert.IsNull(GetPrivateField(shell, "_attachedHardwareInspectionPage"));
        Assert.AreEqual(0, service.CallCount);
        Assert.AreEqual(1, fresh.CallCount);

        compatibilityPage.ViewModel.BackCommand.Execute(null);

        Assert.AreSame(source, frame.Content,
            "A detached stale result must not navigate again.");
        Assert.AreEqual(0, service.CallCount);
        Assert.AreEqual(1, fresh.CallCount);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RealCompletedHardwarePage_TransfersFullReportToCompatibility()
    {
        ModelInspectionExecutionResult terminal = Terminal();
        ModelInspectionHandoff modelHandoff = ModelHandoff(terminal);
        ModelInspectionPage source = CreateSourcePage();
        var hardwareService = new CompletedHardwareService();
        var shell = new OnboardingShellPage(
            static (frame, request) =>
                frame.Navigate(typeof(ModelInspectionPage), request),
            hardwareService,
            (frame, configured, handoff) =>
            {
                frame.Content = new HardwareInspectionPage(
                    new HardwareInspectionViewModel(
                        hardwareService,
                        configured.InitialInspectionId,
                        new ImmediateHardwareInspectionStagePacer()),
                    handoff);
                return true;
            },
            hardwareHandoffReissuer: null,
            new FreshResourcesSource(),
            (_, handoff) => handoff.ModelInspectionRunId == ModelRunId
                ? terminal
                : null,
            (frame, page) =>
            {
                page.StartAutomatically = false;
                frame.Content = page;
                return true;
            });
        shell.AttachModelInspectionPage(source);
        Assert.IsTrue(shell.NavigateToHardwareInspection(source, modelHandoff));
        var frame = (Frame)shell.FindName("StageFrame");
        var hardwarePage = Assert.IsInstanceOfType<HardwareInspectionPage>(
            frame.Content);

        await using var host = await GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.WinUiRenderHost.ShowAsync(shell, 1200, 800);
        await WaitUntilAsync(() => frame.Content is CompatibilityPage);

        var compatibilityPage = Assert.IsInstanceOfType<CompatibilityPage>(
            frame.Content);
        Expander report = Assert.IsInstanceOfType<Expander>(
            compatibilityPage.FindName("CompatibilityHardwareFactsExpander"));
        StackPanel reportHost = Assert.IsInstanceOfType<StackPanel>(
            compatibilityPage.FindName("CompatibilityHardwareFactsHost"));
        Assert.AreEqual(Visibility.Visible, report.Visibility);
        Assert.IsFalse(report.IsExpanded);
        Assert.IsGreaterThanOrEqualTo(5, reportHost.Children.Count);
        Assert.AreEqual(1, hardwareService.CallCount);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task MismatchedHardwareRun_FailsClosedWithoutNavigation()
    {
        ModelInspectionExecutionResult terminal = Terminal();
        ModelInspectionHandoff modelHandoff = ModelHandoff(terminal);
        ModelInspectionPage source = CreateSourcePage();
        var shell = new OnboardingShellPage(
            static (frame, request) => frame.Navigate(typeof(ModelInspectionPage), request),
            new CountingHardwareService(),
            hardwareInspectionNavigator: null,
            hardwareHandoffReissuer: null,
            new FreshResourcesSource(),
            (_, _) => terminal,
            compatibilityNavigator: null);
        shell.AttachModelInspectionPage(source);
        Assert.IsTrue(shell.NavigateToHardwareInspection(source, modelHandoff));
        var frame = (Frame)shell.FindName("StageFrame");
        var hardwarePage = (HardwareInspectionPage)frame.Content;
        Guid mismatchId = Guid.NewGuid();
        HardwareInspectionHandoff mismatch = HardwareInspectionHandoff.Create(
            mismatchId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(mismatchId));

        Assert.IsFalse(await shell.NavigateToCompatibilityAsync(
            hardwarePage,
            new HardwareInspectionCompletedEventArgs(mismatch)));
        Assert.AreSame(hardwarePage, frame.Content);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task EveryCompatibilityAttempt_CapturesFreshResourcesAndCanChangeOutcome()
    {
        ModelInspectionExecutionResult terminal = Terminal();
        ModelInspectionHandoff modelHandoff = ModelHandoff(terminal);
        ModelInspectionPage source = CreateSourcePage();
        var fresh = new SequencedFreshResourcesSource(
            FreshResources(1),
            FreshResources(24UL * 1024 * 1024 * 1024));
        var shell = new OnboardingShellPage(
            static (frame, request) => frame.Navigate(typeof(ModelInspectionPage), request),
            new CountingHardwareService(), null, null, fresh,
            (_, _) => terminal,
            (frame, page) =>
            {
                page.StartAutomatically = false;
                frame.Content = page;
                return true;
            });
        shell.AttachModelInspectionPage(source);
        Assert.IsTrue(shell.NavigateToHardwareInspection(source, modelHandoff));
        var frame = (Frame)shell.FindName("StageFrame");
        var hardwarePage = (HardwareInspectionPage)frame.Content;
        HardwareInspectionHandoff hardwareHandoff = HardwareInspectionHandoff.Create(
            shell.CurrentProductHardwareRunId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(
                shell.CurrentProductHardwareRunId));

        Assert.IsTrue(await shell.NavigateToCompatibilityAsync(
            hardwarePage, new HardwareInspectionCompletedEventArgs(hardwareHandoff)));
        var compatibilityPage = (CompatibilityPage)frame.Content;
        Assert.AreEqual(0, fresh.CallCount,
            "Navigation must not freeze a resource snapshot for later attempts.");

        await compatibilityPage.ViewModel.StartAsync();
        GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation
            .CompatibilityOutcomeTone first = compatibilityPage.ViewModel.Presentation.Tone;
        await compatibilityPage.ViewModel.StartAsync();

        Assert.AreEqual(2, fresh.CallCount);
        Assert.AreNotEqual(
            GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation
                .CompatibilityOutcomeTone.Positive,
            first);
        Assert.AreEqual(
            GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation
                .CompatibilityOutcomeTone.Positive,
            compatibilityPage.ViewModel.Presentation.Tone);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CompatibilityEvaluation_DoesNotConvertDependencyCancellationToFallback()
    {
        OpenVinoOptimizationProductionAuthority authority =
            CreateVerifiedOpenVinoCurrentAuthority();
        CompatibilityEvaluationOrchestrator orchestrator =
            A1BackendProductionAuthorities.Shared.CreateCompatibility(
            new CancellingFreshResourcesSource(),
                TimeProvider.System);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await orchestrator.EvaluateAuthorityAsync(
                new HashSet<string>(StringComparer.Ordinal),
                authority.Evaluate,
                CancellationToken.None));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OpenVinoCompatibility_WithoutOptionalOptimizer_RetainsCurrentModelAuthority()
    {
        const string modelSha =
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256;
        OpenVinoStaticPackageEvidence evidence = new(
            1,
            modelSha,
            modelSha,
            6_805_673_303,
            "granite",
            "GraniteForCausalLM",
            "text-generation-with-past",
            131_072,
            "float16",
            "PreTrainedTokenizerFast",
            10,
            true,
            LayerCount: 40,
            EmbeddingSize: 4_096,
            AttentionHeadCount: 32,
            KeyValueHeadCount: 8);
        var modelHandoff = new ModelInspectionHandoff(
            ModelInspectionHandoff.CurrentSchemaVersion,
            OpenVinoHandoffId,
            ModelRunId,
            InspectionOutcome.Ready,
            modelSha,
            evidence.ModelLengthBytes);
        Guid hardwareRunId = Guid.NewGuid();
        HardwareInspectionHandoff hardwareHandoff = HardwareInspectionHandoff.Create(
            hardwareRunId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(
                hardwareRunId));
        Assert.IsTrue(
            OpenVinoCompatibilityInputProjector.TryPrepare(
                modelHandoff,
                evidence,
                hardwareRunId,
                hardwareHandoff,
                out PreparedOpenVinoCompatibilityInput? prepared),
            "The exact FP16 package and hardware evidence must bind before authority creation.");
        Assert.IsNotNull(prepared);
        OpenVinoBuildEvidence officialBuild = new(
            VerifiedOpenVinoOptimizationEvidence.RuntimeBuild,
            VerifiedOpenVinoOptimizationEvidence.GenAiBuild,
            VerifiedOpenVinoOptimizationEvidence.TokenizersBuild,
            VerifiedOpenVinoOptimizationEvidence.WorkerManifestSha256);
        Assert.IsTrue(
            OpenVinoOptimizationProductionAuthority.TryCreate(
                prepared,
                officialBuild,
                optimizationAvailable: false,
                out OpenVinoOptimizationProductionAuthority? authority),
            "The verified released OpenVINO build must retain current-model authority without an optimiser.");
        Assert.IsNotNull(authority);
        Assert.IsFalse(authority.TryGetOptimizationAuthority(
            GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.OptimizationRoute.OpenVino,
            out _,
            out _));
        Assert.IsTrue(authority.IsCurrentModelChatAvailable(
            GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.OptimizationRoute.OpenVino));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityFreshResourcesInput fresh = CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(16UL * 1024 * 1024 * 1024),
            availableDedicatedDeviceMemoryBytes: null,
            availableStorageBytes: 64UL * 1024 * 1024 * 1024,
            now);
        CompatibilityEvaluation evaluation = authority.Evaluate(
            fresh,
            new HashSet<string>(StringComparer.Ordinal),
            now,
            CancellationToken.None);
        Assert.AreEqual(
            CompatibilityScreenState.EstimatedCompatible,
            evaluation.Screen.State,
            "The verified FP16 current setup must be independently compatible.");
        using var custody = new ModelSourceCustodyRegistry();
        using var registry = new CurrentModelChatLaunchRegistry(custody);
        Assert.IsNotNull(
            authority.ResolveCurrentModel(evaluation, registry),
            "The exact evaluated context must register without a route-launcher fabrication.");
        var compatibilityPage = new CompatibilityPage(
            (_, _) => Task.FromResult(evaluation),
            authority,
            value => authority.ResolveCurrentModel(value, registry),
            continueDestinationAvailable: true)
        {
            StartAutomatically = false
        };
        await compatibilityPage.ViewModel.StartAsync();

        Assert.IsNotNull(
            compatibilityPage.ViewModel.Presentation.MachineMemory,
            "The verified hardware snapshot must reach the compatibility presentation.");
        Assert.AreEqual(
            "Chat with current model",
            compatibilityPage.ViewModel.Presentation.PrimaryActionText);
        Assert.IsTrue(compatibilityPage.ViewModel.Presentation.PrimaryActionEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OpenVinoFp32TerminalEvidence_IsCachedButCompatibilityFailsClosed()
    {
        (ModelInspectionPage source, ModelInspectionHandoff modelHandoff) =
            CreateOpenVinoSourcePage();
        InvokePrivate(
            source,
            "ApplyOpenVinoTerminalMetadata",
            new OpenVinoRouteInspectionResult(
                OpenVinoRouteInspectionOutcome.Ready,
                HandoffLease: null,
                Failure: null,
                OpenVinoRouteCapability.Candidates[0]));
        var shell = new OnboardingShellPage(
            static (frame, request) =>
                frame.Navigate(typeof(ModelInspectionPage), request),
            new CountingHardwareService(),
            hardwareInspectionNavigator: null,
            hardwareHandoffReissuer: null,
            new FreshResourcesSource(),
            (_, _) => null,
            (frame, page) =>
            {
                page.StartAutomatically = false;
                frame.Content = page;
                return true;
            });
        shell.AttachModelInspectionPage(source);
        Assert.IsTrue(shell.NavigateToHardwareInspection(source, modelHandoff));
        Assert.IsTrue(source.TryGetOpenVinoCompatibilityEvidence(
            modelHandoff,
            out OpenVinoStaticPackageEvidence? acceptedEvidence));
        Assert.IsNotNull(acceptedEvidence);
        Assert.AreEqual("float32", acceptedEvidence.WeightPrecision);
        Assert.IsTrue(source.TryGetOpenVinoSourceDirectory(
            modelHandoff,
            out string? originalDirectory));
        Assert.IsNotNull(originalDirectory);
        SetPrivateField(
            source,
            "_openVinoDirectoryPath",
            Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N")));
        Assert.IsTrue(source.TryGetOpenVinoCompatibilityEvidence(
            modelHandoff,
            out OpenVinoStaticPackageEvidence? cachedEvidence),
            "Compatibility evidence must come from the accepted terminal cache, not a second package scan.");
        Assert.AreSame(acceptedEvidence, cachedEvidence);
        SetPrivateField(source, "_openVinoDirectoryPath", originalDirectory);
        Assert.IsTrue(source.TryGetOpenVinoCompatibilityEvidence(
            modelHandoff,
            out OpenVinoStaticPackageEvidence? restoredEvidence));
        Assert.AreSame(acceptedEvidence, restoredEvidence);
        var frame = (Frame)shell.FindName("StageFrame");
        var hardwarePage = (HardwareInspectionPage)frame.Content;
        HardwareInspectionHandoff hardwareHandoff = HardwareInspectionHandoff.Create(
            shell.CurrentProductHardwareRunId,
            HardwareInspectionOutcome.CompletedWithWarnings,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(
                shell.CurrentProductHardwareRunId));

        Assert.IsFalse(await shell.NavigateToCompatibilityAsync(
            hardwarePage,
            new HardwareInspectionCompletedEventArgs(hardwareHandoff)),
            "The cached FP32 inspection evidence is immutable, but FP32 is not an admitted " +
            "OpenVINO compatibility precision and must fail closed.");
        Assert.AreSame(hardwarePage, frame.Content);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OpenVinoTerminalEvidence_AcceptsOnlyTheExactCurrentHandoffIdentity()
    {
        (ModelInspectionPage source, ModelInspectionHandoff current) =
            CreateOpenVinoSourcePage();
        InvokePrivate(
            source,
            "ApplyOpenVinoTerminalMetadata",
            new OpenVinoRouteInspectionResult(
                OpenVinoRouteInspectionOutcome.Ready,
                HandoffLease: null,
                Failure: null,
                OpenVinoRouteCapability.Candidates[0]));

        Assert.IsTrue(source.TryGetOpenVinoCompatibilityEvidence(
            current,
            out OpenVinoStaticPackageEvidence? evidence));
        Assert.IsNotNull(evidence);

        var clonedReference = new ModelInspectionHandoff(
            current.SchemaVersion,
            current.ModelInspectionHandoffId,
            current.ModelInspectionRunId,
            current.Outcome,
            current.ModelSha256,
            current.ModelLengthBytes);
        Assert.IsFalse(source.TryGetOpenVinoCompatibilityEvidence(
            clonedReference,
            out _),
            "A value-equal but non-current handoff reference must fail closed.");

        var wrongHash = new ModelInspectionHandoff(
            current.SchemaVersion,
            Guid.NewGuid(),
            current.ModelInspectionRunId,
            current.Outcome,
            new string('f', 64),
            current.ModelLengthBytes);
        Assert.IsFalse(source.TryGetOpenVinoCompatibilityEvidence(wrongHash, out _));

        var wrongLength = new ModelInspectionHandoff(
            current.SchemaVersion,
            Guid.NewGuid(),
            current.ModelInspectionRunId,
            current.Outcome,
            current.ModelSha256,
            current.ModelLengthBytes + 1);
        Assert.IsFalse(source.TryGetOpenVinoCompatibilityEvidence(wrongLength, out _));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void RejectedOpenVinoTerminalInspection_ClearsCompatibilityEvidence()
    {
        (ModelInspectionPage source, ModelInspectionHandoff current) =
            CreateOpenVinoSourcePage();
        OpenVinoRouteInspectionResult terminal = new(
            OpenVinoRouteInspectionOutcome.Ready,
            HandoffLease: null,
            Failure: null,
            OpenVinoRouteCapability.Candidates[0]);
        InvokePrivate(source, "ApplyOpenVinoTerminalMetadata", terminal);
        Assert.IsTrue(source.TryGetOpenVinoCompatibilityEvidence(current, out _));

        SetPrivateField(
            source,
            "_openVinoDirectoryPath",
            Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N")));
        InvokePrivate(source, "ApplyOpenVinoTerminalMetadata", terminal);

        Assert.IsFalse(source.TryGetOpenVinoCompatibilityEvidence(current, out _));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RetiredOpenVinoLifetime_ClearsCompatibilityEvidence()
    {
        (ModelInspectionPage source, ModelInspectionHandoff current) =
            CreateOpenVinoSourcePage();
        InvokePrivate(
            source,
            "ApplyOpenVinoTerminalMetadata",
            new OpenVinoRouteInspectionResult(
                OpenVinoRouteInspectionOutcome.Ready,
                HandoffLease: null,
                Failure: null,
                OpenVinoRouteCapability.Candidates[0]));
        Assert.IsTrue(source.TryGetOpenVinoCompatibilityEvidence(current, out _));
        SetPrivateField(source, "_openVinoCancellation", new CancellationTokenSource());

        await source.RetireOpenVinoInspectionAsync();

        Assert.IsFalse(source.TryGetOpenVinoCompatibilityEvidence(current, out _));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OpenVinoChatActivation_RestoresInspectionPageBeforeTouchingItsPromptControls()
    {
        var shell = new OnboardingShellPage();
        var inspectionPage = new ModelInspectionPage();
        var compatibilityPage = new CompatibilityPage
        {
            StartAutomatically = false
        };
        var frame = (Frame)shell.FindName("StageFrame");
        frame.Content = compatibilityPage;
        SetPrivateField(
            shell,
            "_modelInspectionPageForHardwareReturn",
            inspectionPage);
        SetPrivateField(shell, "_attachedCompatibilityPage", compatibilityPage);
        bool activatedWithConnectedPage = false;

        bool activated = await shell.ActivateOpenVinoCurrentModelChatAsync(
            inspectionPage,
            _ =>
            {
                activatedWithConnectedPage = ReferenceEquals(
                    frame.Content,
                    inspectionPage);
                return Task.FromResult(true);
            },
            CancellationToken.None);

        Assert.IsTrue(activated);
        Assert.IsTrue(activatedWithConnectedPage,
            "OpenVINO activation must not mutate a page after it has been removed from the Frame.");
        Assert.AreSame(inspectionPage, frame.Content);
        Assert.AreEqual(OnboardingStage.ReadyToChat, shell.CurrentStage);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OpenVinoChatActivationFailure_RestoresCompatibilityWithoutAdvancing()
    {
        var shell = new OnboardingShellPage();
        var inspectionPage = new ModelInspectionPage();
        var compatibilityPage = new CompatibilityPage
        {
            StartAutomatically = false
        };
        var frame = (Frame)shell.FindName("StageFrame");
        frame.Content = compatibilityPage;
        SetPrivateField(
            shell,
            "_modelInspectionPageForHardwareReturn",
            inspectionPage);
        SetPrivateField(shell, "_attachedCompatibilityPage", compatibilityPage);

        bool activated = await shell.ActivateOpenVinoCurrentModelChatAsync(
            inspectionPage,
            _ => Task.FromResult(false),
            CancellationToken.None);

        Assert.IsFalse(activated);
        Assert.AreSame(compatibilityPage, frame.Content);
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OpenVinoChatActivationException_RestoresCompatibilityWithoutCrashing()
    {
        var shell = new OnboardingShellPage();
        var inspectionPage = new ModelInspectionPage();
        var compatibilityPage = new CompatibilityPage
        {
            StartAutomatically = false
        };
        var frame = (Frame)shell.FindName("StageFrame");
        frame.Content = compatibilityPage;
        SetPrivateField(
            shell,
            "_modelInspectionPageForHardwareReturn",
            inspectionPage);
        SetPrivateField(shell, "_attachedCompatibilityPage", compatibilityPage);

        bool activated = await shell.ActivateOpenVinoCurrentModelChatAsync(
            inspectionPage,
            _ => Task.FromException<bool>(new InvalidOperationException("fixture")),
            CancellationToken.None);

        Assert.IsFalse(activated);
        Assert.AreSame(compatibilityPage, frame.Content);
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OpenVinoBuildEvidenceDoesNotDependOnOptionalConverter()
    {
        OpenVinoBuildEvidence expected = new(
            "2026.3.0-22451-8a17657b995-releases/2026/3",
            "2026.3.0.0-3277-bd8d6542e3c",
            "2026.3.0.0-703-183c6f25cda",
            new string('1', 64));
        var page = new ModelInspectionPage();
        SetPrivateField(
            page,
            "_openVinoRouteService",
            new OpenVinoRouteService(new NeverCalledOpenVinoWorkerClient(), expected));

        Assert.IsTrue(page.TryGetOpenVinoBuildEvidence(
            out OpenVinoBuildEvidence? actual));
        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public async Task WindowsFreshResourceSource_UsesCurrentProvidersAndInjectedClock()
    {
        DateTimeOffset memoryAt = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset storageAt = memoryAt.AddSeconds(1);
        DateTimeOffset observedAt = memoryAt.AddSeconds(2);
        var source = new WindowsCompatibilityFreshResourcesSource(
            new FixedMemoryProvider(7_000_000_000, memoryAt),
            _ => ValueTask.FromResult(WindowsStorageEvidence.Available(
                100_000_000_000, 40_000_000_000, storageAt)),
            new FixedTimeProvider(observedAt));

        CompatibilityFreshResourcesInput captured =
            await source.CaptureAsync(CancellationToken.None);

        Assert.AreEqual(7_000_000_000UL, captured.AvailableSystemMemoryBytes);
        Assert.AreEqual(40_000_000_000UL, captured.AvailableStorageBytes);
        Assert.AreEqual(memoryAt, captured.ObservedAtUtc,
            "The aggregate must retain the oldest accepted provider observation.");
        Assert.IsFalse(captured.DedicatedDeviceMemoryEstablished,
            "No fresh dedicated-memory provider exists, so the source must stay unknown.");
    }

    private ModelInspectionPage CreateSourcePage()
    {
        // Inspection is supplied by the test boundary, but saved-profile lookup
        // needs a real source directory. Do not depend on a nonexistent C: path.
        Directory.CreateDirectory(_modelFixtureRoot);
        string modelPath = Path.Combine(_modelFixtureRoot, "granite.gguf");
        ModelInspectionRequest template = PresentationTestData.CreateRequest();
        File.WriteAllBytes(modelPath, new byte[4096]);
        var request = new ModelInspectionRequest(modelPath, template.FileName,
            template.ExpectedFileIdentity, template.QuickScan);
        var page = new ModelInspectionPage();
        System.Reflection.MethodInfo? activate = typeof(ModelInspectionPage)
            .GetMethod(
                "ActivateRequest",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(activate);
        activate.Invoke(page, [request]);
        return page;
    }

    private static (ModelInspectionPage Page, ModelInspectionHandoff Handoff)
        CreateOpenVinoSourcePage()
    {
        string package = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "OpenVINO",
            "GenAI",
            "TinySyntheticV1",
            "package");
        OpenVinoStaticPackageInspectionResult inspection =
            new OpenVinoStaticPackageInspector().Inspect(package);
        Assert.IsNotNull(inspection.Evidence);
        OpenVinoStaticPackageEvidence evidence = inspection.Evidence;
        var handoff = new ModelInspectionHandoff(
            ModelInspectionHandoff.CurrentSchemaVersion,
            OpenVinoHandoffId,
            ModelRunId,
            InspectionOutcome.Ready,
            evidence.ModelSha256,
            evidence.ModelLengthBytes);
        var page = new ModelInspectionPage();
        SetPrivateField(page, "_openVinoDirectoryPath", package);
        SetPrivateField(page, "_openVinoHardwareHandoff", handoff);
        SetPrivateField(
            page,
            "_openVinoConfiguration",
            OpenVinoRouteCapability.Candidates[0]);
        return (page, handoff);
    }

    private static void SetPrivateField(
        ModelInspectionPage page,
        string fieldName,
        object value)
    {
        System.Reflection.FieldInfo? field = typeof(ModelInspectionPage).GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        field.SetValue(page, value);
    }

    private static void SetPrivateField(
        OnboardingShellPage page,
        string fieldName,
        object value)
    {
        System.Reflection.FieldInfo? field = typeof(OnboardingShellPage).GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        field.SetValue(page, value);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void CompatibilityBack_MissingModelOrStaleSenderDoesNotPartlyDetachResult()
    {
        var shell = new OnboardingShellPage();
        var compatibilityPage = new CompatibilityPage
        {
            StartAutomatically = false
        };
        var stalePage = new CompatibilityPage
        {
            StartAutomatically = false
        };
        var frame = (Frame)shell.FindName("StageFrame");
        frame.Content = compatibilityPage;
        SetPrivateField(shell, "_attachedCompatibilityPage", compatibilityPage);

        InvokeShellPrivate(
            shell,
            "CompatibilityPage_BackRequested",
            compatibilityPage,
            EventArgs.Empty);

        Assert.AreSame(compatibilityPage, frame.Content);
        Assert.AreSame(
            compatibilityPage,
            GetPrivateField(shell, "_attachedCompatibilityPage"));

        SetPrivateField(
            shell,
            "_modelInspectionPageForHardwareReturn",
            new ModelInspectionPage());
        InvokeShellPrivate(
            shell,
            "CompatibilityPage_BackRequested",
            stalePage,
            EventArgs.Empty);

        Assert.AreSame(compatibilityPage, frame.Content);
        Assert.AreSame(
            compatibilityPage,
            GetPrivateField(shell, "_attachedCompatibilityPage"));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CompatibilityImport_InstallsFreshAllSourcesPageBeforeDetachingResult()
    {
        var shell = new OnboardingShellPage();
        var compatibilityPage = NoAnswerCompatibilityPage();
        var frame = (Frame)shell.FindName("StageFrame");
        frame.Content = compatibilityPage;
        SetPrivateField(shell, "_attachedCompatibilityPage", compatibilityPage);
        SetPrivateField(shell, "_currentStage", OnboardingStage.CheckHardwareFit);
        Assert.IsTrue((bool)InvokeCompatibilityPrivate(
            compatibilityPage,
            "TryBeginImportNavigation")!);

        InvokeShellPrivate(
            shell,
            "CompatibilityPage_ImportAnotherModelRequested",
            compatibilityPage,
            EventArgs.Empty);
        await shell.CurrentNavigationTask;

        ModelImportPage importPage = Assert.IsInstanceOfType<ModelImportPage>(
            frame.Content);
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
        Assert.AreSame(importPage, GetPrivateField(shell, "_attachedModelImportPage"));
        Assert.IsNull(GetPrivateField(shell, "_attachedCompatibilityPage"));
        Assert.AreEqual(
            ModelImportPresentationMode.AllSources,
            GetPrivateField(importPage, "_presentationMode"));

        InvokeShellPrivate(
            shell,
            "CompatibilityPage_ImportAnotherModelRequested",
            compatibilityPage,
            EventArgs.Empty);
        Assert.AreSame(importPage, frame.Content,
            "A detached stale result must not navigate a second time.");
        Assert.IsTrue(compatibilityPage.CanCompleteImportNavigation,
            "A detached stale request must remain one-shot rather than being rearmed.");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CompatibilityImport_CancelledNavigationRetainsResultAndRestoresAction()
    {
        var shell = new OnboardingShellPage();
        var compatibilityPage = NoAnswerCompatibilityPage();
        var frame = (Frame)shell.FindName("StageFrame");
        frame.Content = compatibilityPage;
        SetPrivateField(shell, "_attachedCompatibilityPage", compatibilityPage);
        SetPrivateField(shell, "_currentStage", OnboardingStage.CheckHardwareFit);
        var backEntry = new PageStackEntry(typeof(ModelImportPage), "back", null);
        var forwardEntry = new PageStackEntry(typeof(ModelImportPage), "forward", null);
        frame.BackStack.Add(backEntry);
        frame.ForwardStack.Add(forwardEntry);
        int navigationAttempts = 0;
        NavigatingCancelEventHandler cancel = (_, args) =>
        {
            navigationAttempts++;
            args.Cancel = true;
        };
        frame.Navigating += cancel;
        Assert.IsTrue((bool)InvokeCompatibilityPrivate(
            compatibilityPage,
            "TryBeginImportNavigation")!);

        InvokeShellPrivate(
            shell,
            "CompatibilityPage_ImportAnotherModelRequested",
            compatibilityPage,
            EventArgs.Empty);
        InvokeShellPrivate(
            shell,
            "CompatibilityPage_ImportAnotherModelRequested",
            compatibilityPage,
            EventArgs.Empty);
        await shell.CurrentNavigationTask;

        Assert.AreSame(compatibilityPage, frame.Content);
        Assert.AreEqual(OnboardingStage.CheckHardwareFit, shell.CurrentStage);
        Assert.AreSame(
            compatibilityPage,
            GetPrivateField(shell, "_attachedCompatibilityPage"));
        Assert.IsFalse(compatibilityPage.CanCompleteImportNavigation);
        Assert.AreEqual(1, navigationAttempts,
            "A repeated request while settlement is pending must not navigate again.");
        CollectionAssert.AreEqual(
            new[] { backEntry }, frame.BackStack.ToArray());
        CollectionAssert.AreEqual(
            new[] { forwardEntry }, frame.ForwardStack.ToArray());
        Button import = Assert.IsInstanceOfType<Button>(
            compatibilityPage.FindName("BtnCompatibilitySecondaryForward"));
        Assert.IsTrue(import.IsEnabled);

        frame.Navigating -= cancel;
        Assert.IsTrue((bool)InvokeCompatibilityPrivate(
            compatibilityPage,
            "TryBeginImportNavigation")!);
        InvokeShellPrivate(
            shell,
            "CompatibilityPage_ImportAnotherModelRequested",
            compatibilityPage,
            EventArgs.Empty);
        await shell.CurrentNavigationTask;
        Assert.IsInstanceOfType<ModelImportPage>(frame.Content);
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CompatibilityImport_ThrownNavigationSettlesBackToExactSource()
    {
        var shell = new OnboardingShellPage();
        var compatibilityPage = NoAnswerCompatibilityPage();
        var frame = (Frame)shell.FindName("StageFrame");
        frame.Content = compatibilityPage;
        SetPrivateField(shell, "_attachedCompatibilityPage", compatibilityPage);
        SetPrivateField(shell, "_currentStage", OnboardingStage.CheckHardwareFit);
        frame.Navigating += (_, _) =>
            throw new InvalidOperationException("navigation-test-failure");
        Assert.IsTrue((bool)InvokeCompatibilityPrivate(
            compatibilityPage,
            "TryBeginImportNavigation")!);

        InvokeShellPrivate(
            shell,
            "CompatibilityPage_ImportAnotherModelRequested",
            compatibilityPage,
            EventArgs.Empty);
        await shell.CurrentNavigationTask;

        Assert.AreSame(compatibilityPage, frame.Content);
        Assert.AreSame(compatibilityPage,
            GetPrivateField(shell, "_attachedCompatibilityPage"));
        Assert.AreEqual(OnboardingStage.CheckHardwareFit, shell.CurrentStage);
        Assert.IsFalse(compatibilityPage.CanCompleteImportNavigation);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CompatibilityImport_FailureAfterTransientAttachRollsBackWithoutDuplicateOwnership()
    {
        var shell = new OnboardingShellPage();
        var compatibilityPage = NoAnswerCompatibilityPage();
        var frame = (Frame)shell.FindName("StageFrame");
        var indicator = Assert.IsInstanceOfType<
            GraniteEdgeAI.Features.Onboarding.Controls.OnboardingStageIndicator>(
                shell.FindName("StageIndicator"));
        frame.Content = compatibilityPage;
        SetPrivateField(shell, "_attachedCompatibilityPage", compatibilityPage);
        SetPrivateField(shell, "_currentStage", OnboardingStage.CheckHardwareFit);
        indicator.CurrentStage = OnboardingStage.CheckHardwareFit;
        var backEntry = new PageStackEntry(typeof(ModelImportPage), "back", null);
        var forwardEntry = new PageStackEntry(typeof(ModelImportPage), "forward", null);
        frame.BackStack.Add(backEntry);
        frame.ForwardStack.Add(forwardEntry);
        long generation = 17;
        SetPrivateField(shell, "_compatibilityImportNavigationGeneration", generation);
        Assert.IsTrue((bool)InvokeCompatibilityPrivate(
            compatibilityPage,
            "TryBeginImportNavigation")!);
        var transientPage = new ModelImportPage();
        transientPage.SetPresentationMode(ModelImportPresentationMode.AllSources);
        shell.AttachModelImportPage(transientPage);
        frame.Content = transientPage;
        frame.BackStack.Clear();
        frame.ForwardStack.Clear();

        InvokeShellPrivate(
            shell,
            "RollBackCompatibilityImport",
            compatibilityPage,
            transientPage,
            generation,
            new[] { backEntry },
            new[] { forwardEntry });

        Assert.AreSame(compatibilityPage, frame.Content);
        Assert.AreSame(compatibilityPage,
            GetPrivateField(shell, "_attachedCompatibilityPage"));
        Assert.IsNull(GetPrivateField(shell, "_attachedModelImportPage"));
        Assert.AreEqual(OnboardingStage.CheckHardwareFit, shell.CurrentStage);
        Assert.AreEqual(OnboardingStage.CheckHardwareFit, indicator.CurrentStage);
        CollectionAssert.AreEqual(new[] { backEntry }, frame.BackStack.ToArray());
        CollectionAssert.AreEqual(new[] { forwardEntry }, frame.ForwardStack.ToArray());
        Assert.IsFalse(compatibilityPage.CanCompleteImportNavigation);

        int navigationAttempts = 0;
        frame.Navigating += (_, _) => navigationAttempts++;
        Assert.IsTrue((bool)InvokeCompatibilityPrivate(
            compatibilityPage,
            "TryBeginImportNavigation")!);
        InvokeShellPrivate(
            shell,
            "CompatibilityPage_ImportAnotherModelRequested",
            compatibilityPage,
            EventArgs.Empty);
        await shell.CurrentNavigationTask;

        Assert.AreEqual(1, navigationAttempts,
            "Rollback must rearm exactly one retry without duplicate subscriptions.");
        Assert.IsInstanceOfType<ModelImportPage>(frame.Content);
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CompatibilityImport_NewerJourneyMakesDeferredAttemptStale()
    {
        var shell = new OnboardingShellPage();
        var sourcePage = NoAnswerCompatibilityPage();
        var newerPage = NoAnswerCompatibilityPage();
        var frame = (Frame)shell.FindName("StageFrame");
        frame.Content = sourcePage;
        SetPrivateField(shell, "_attachedCompatibilityPage", sourcePage);
        SetPrivateField(shell, "_currentStage", OnboardingStage.CheckHardwareFit);
        Assert.IsTrue((bool)InvokeCompatibilityPrivate(
            sourcePage,
            "TryBeginImportNavigation")!);

        InvokeShellPrivate(
            shell,
            "CompatibilityPage_ImportAnotherModelRequested",
            sourcePage,
            EventArgs.Empty);
        frame.Content = newerPage;
        SetPrivateField(shell, "_attachedCompatibilityPage", newerPage);
        var retainedEntry = new PageStackEntry(
            typeof(CompatibilityPage), "newer", null);
        frame.BackStack.Clear();
        frame.BackStack.Add(retainedEntry);
        await shell.CurrentNavigationTask;

        Assert.AreSame(newerPage, frame.Content);
        Assert.AreSame(newerPage,
            GetPrivateField(shell, "_attachedCompatibilityPage"));
        CollectionAssert.AreEqual(
            new[] { retainedEntry }, frame.BackStack.ToArray());
        Assert.IsTrue(sourcePage.CanCompleteImportNavigation,
            "A stale continuation must not rearm or otherwise mutate its detached source.");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CompatibilityImport_RetirementPreventsLateSettlementMutation()
    {
        var shell = new OnboardingShellPage();
        var sourcePage = NoAnswerCompatibilityPage();
        var frame = (Frame)shell.FindName("StageFrame");
        frame.Content = sourcePage;
        SetPrivateField(shell, "_attachedCompatibilityPage", sourcePage);
        SetPrivateField(shell, "_currentStage", OnboardingStage.CheckHardwareFit);
        Assert.IsTrue((bool)InvokeCompatibilityPrivate(
            sourcePage,
            "TryBeginImportNavigation")!);

        InvokeShellPrivate(
            shell,
            "CompatibilityPage_ImportAnotherModelRequested",
            sourcePage,
            EventArgs.Empty);
        object? contentAtRetirement = frame.Content;
        shell.Dispose();
        await shell.CurrentNavigationTask;

        Assert.AreSame(contentAtRetirement, frame.Content);
        Assert.IsTrue(sourcePage.CanCompleteImportNavigation,
            "Retirement must suppress late rollback and rearming.");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void CompatibilityConfigurePresentation_UpdatesOnlyTheStageForTheAttachedPage()
    {
        var shell = new OnboardingShellPage();
        var compatibilityPage = new CompatibilityPage
        {
            StartAutomatically = false
        };
        var stalePage = new CompatibilityPage
        {
            StartAutomatically = false
        };
        var frame = (Frame)shell.FindName("StageFrame");
        var indicator = Assert.IsInstanceOfType<
            GraniteEdgeAI.Features.Onboarding.Controls.OnboardingStageIndicator>(
                shell.FindName("StageIndicator"));
        frame.Content = compatibilityPage;
        SetPrivateField(shell, "_attachedCompatibilityPage", compatibilityPage);
        SetPrivateField(shell, "_currentStage", OnboardingStage.CheckHardwareFit);
        indicator.CurrentStage = OnboardingStage.CheckHardwareFit;

        InvokeShellPrivate(
            shell,
            "CompatibilityPage_ConfigureStageEntered",
            stalePage,
            EventArgs.Empty);
        Assert.AreEqual(OnboardingStage.CheckHardwareFit, shell.CurrentStage);

        InvokeShellPrivate(
            shell,
            "CompatibilityPage_ConfigureStageEntered",
            compatibilityPage,
            EventArgs.Empty);
        Assert.AreSame(compatibilityPage, frame.Content);
        Assert.AreEqual(OnboardingStage.ConfigureModel, shell.CurrentStage);
        Assert.AreEqual(OnboardingStage.ConfigureModel, indicator.CurrentStage);

        InvokeShellPrivate(
            shell,
            "CompatibilityPage_ConfigureStageExited",
            compatibilityPage,
            EventArgs.Empty);
        Assert.AreSame(compatibilityPage, frame.Content);
        Assert.AreEqual(OnboardingStage.CheckHardwareFit, shell.CurrentStage);
        Assert.AreEqual(OnboardingStage.CheckHardwareFit, indicator.CurrentStage);
    }

    private static object? GetPrivateField(
        OnboardingShellPage page,
        string fieldName)
    {
        System.Reflection.FieldInfo? field = typeof(OnboardingShellPage).GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return field.GetValue(page);
    }

    private static object? GetPrivateField(
        object instance,
        string fieldName)
    {
        System.Reflection.FieldInfo? field = instance.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return field.GetValue(instance);
    }

    private static object? InvokeCompatibilityPrivate(
        CompatibilityPage page,
        string methodName)
    {
        System.Reflection.MethodInfo? method = typeof(CompatibilityPage).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        return method.Invoke(page, null);
    }

    private static CompatibilityPage NoAnswerCompatibilityPage()
    {
        var page = new CompatibilityPage { StartAutomatically = false };
        page.Apply(CompatibilityPresentationFactory.From(
            CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.NotEstablished,
                [], [], BaselineExclusionReason.None,
                useCurrentModelAvailable: false,
                continueEnabled: false)));
        return page;
    }

    private static void InvokePrivate(
        ModelInspectionPage page,
        string methodName,
        object argument)
    {
        System.Reflection.MethodInfo? method = typeof(ModelInspectionPage).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(page, [argument]);
    }

    private static void InvokeShellPrivate(
        OnboardingShellPage page,
        string methodName,
        params object?[] arguments)
    {
        System.Reflection.MethodInfo? method = typeof(OnboardingShellPage).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(page, arguments);
    }

    private static void LoadHardwarePage(HardwareInspectionPage page)
    {
        System.Reflection.MethodInfo? method = typeof(HardwareInspectionPage).GetMethod(
            "OnLoaded",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(page, [page, new RoutedEventArgs()]);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (!condition())
        {
            Assert.IsTrue(
                DateTimeOffset.UtcNow < deadline,
                "The expected navigation did not complete within thirty seconds.");
            await Task.Delay(10);
        }
    }

    [TestMethod]
    public async Task WindowsFreshResourceSource_RejectsStaleMemoryEvidence()
    {
        DateTimeOffset now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        WindowsCompatibilityFreshResourcesSource source = FreshSource(
            now.AddSeconds(-31), now, now);

        CompatibilityFreshResourcesUnavailableException error =
            await Assert.ThrowsExactlyAsync<CompatibilityFreshResourcesUnavailableException>(
                async () => await source.CaptureAsync(CancellationToken.None));
        Assert.AreEqual(
            CompatibilityFreshResourcesUnavailableReason.ResourceEvidenceStale,
            error.Reason);
    }

    [TestMethod]
    public async Task WindowsFreshResourceSource_RejectsStaleStorageEvidence()
    {
        DateTimeOffset now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        WindowsCompatibilityFreshResourcesSource source = FreshSource(
            now, now.AddSeconds(-31), now);

        CompatibilityFreshResourcesUnavailableException error =
            await Assert.ThrowsExactlyAsync<CompatibilityFreshResourcesUnavailableException>(
                async () => await source.CaptureAsync(CancellationToken.None));
        Assert.AreEqual(
            CompatibilityFreshResourcesUnavailableReason.ResourceEvidenceStale,
            error.Reason);
    }

    [TestMethod]
    public async Task WindowsFreshResourceSource_RejectsFutureEvidence()
    {
        DateTimeOffset now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        WindowsCompatibilityFreshResourcesSource source = FreshSource(
            now.AddSeconds(6), now, now);

        CompatibilityFreshResourcesUnavailableException error =
            await Assert.ThrowsExactlyAsync<CompatibilityFreshResourcesUnavailableException>(
                async () => await source.CaptureAsync(CancellationToken.None));
        Assert.AreEqual(
            CompatibilityFreshResourcesUnavailableReason.ResourceEvidenceStale,
            error.Reason);
    }

    [TestMethod]
    public async Task WindowsFreshResourceSource_RejectsExcessiveProviderSkew()
    {
        DateTimeOffset now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        WindowsCompatibilityFreshResourcesSource source = FreshSource(
            now.AddSeconds(-10), now, now);

        CompatibilityFreshResourcesUnavailableException error =
            await Assert.ThrowsExactlyAsync<CompatibilityFreshResourcesUnavailableException>(
                async () => await source.CaptureAsync(CancellationToken.None));
        Assert.AreEqual(
            CompatibilityFreshResourcesUnavailableReason.ResourceEvidenceInconsistent,
            error.Reason);
    }

    [TestMethod]
    public async Task WindowsFreshResourceSource_AcceptsCloseEvidenceWithoutRestampingIt()
    {
        DateTimeOffset now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset memoryAt = now.AddSeconds(-2);
        WindowsCompatibilityFreshResourcesSource source = FreshSource(
            memoryAt, now.AddSeconds(-1), now);

        CompatibilityFreshResourcesInput captured =
            await source.CaptureAsync(CancellationToken.None);

        Assert.AreEqual(memoryAt, captured.ObservedAtUtc);
    }

    private static ModelInspectionExecutionResult Terminal() =>
        ModelInspectionExecutionResult.Completed(
            PresentationTestData.CreateResult(InspectionOutcome.Ready));

    private static ModelInspectionHandoff ModelHandoff(ModelInspectionExecutionResult terminal)
    {
        Assert.IsTrue(ModelInspectionHandoffProjector.TryProject(
            ModelRunId,
            ModelRunId,
            terminal,
            out ModelInspectionHandoff? handoff));
        return handoff!;
    }

    private static CompatibilityFreshResourcesInput FreshResources(ulong memory) =>
        CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(memory),
            availableDedicatedDeviceMemoryBytes: null,
            availableStorageBytes: 64UL * 1024 * 1024 * 1024,
            DateTimeOffset.UtcNow);

    private static WindowsCompatibilityFreshResourcesSource FreshSource(
        DateTimeOffset memoryAt,
        DateTimeOffset storageAt,
        DateTimeOffset now) =>
        new(
            new FixedMemoryProvider(7_000_000_000, memoryAt),
            _ => ValueTask.FromResult(WindowsStorageEvidence.Available(
                100_000_000_000, 40_000_000_000, storageAt)),
            new FixedTimeProvider(now));

    private static OpenVinoOptimizationProductionAuthority
        CreateVerifiedOpenVinoCurrentAuthority() =>
        CompatibilityViewModelTests.RealExactFixture().Authority;

    private sealed class FreshResourcesSource : ICompatibilityFreshResourcesSource
    {
        public ValueTask<CompatibilityFreshResourcesInput> CaptureAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(FreshResources(24UL * 1024 * 1024 * 1024));
    }

    private sealed class CancellingFreshResourcesSource
        : ICompatibilityFreshResourcesSource
    {
        public ValueTask<CompatibilityFreshResourcesInput> CaptureAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromException<CompatibilityFreshResourcesInput>(
                new OperationCanceledException("dependency cancelled"));
    }

    private sealed class SequencedFreshResourcesSource : ICompatibilityFreshResourcesSource
    {
        private readonly Queue<CompatibilityFreshResourcesInput> _snapshots;

        internal SequencedFreshResourcesSource(
            params CompatibilityFreshResourcesInput[] snapshots) =>
            _snapshots = new Queue<CompatibilityFreshResourcesInput>(snapshots);

        internal int CallCount { get; private set; }

        public ValueTask<CompatibilityFreshResourcesInput> CaptureAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(_snapshots.Dequeue());
        }
    }

    private sealed class CountingHardwareService : IHardwareInspectionService
    {
        internal int CallCount { get; private set; }

        public Task<HardwareInspectionRunResult> RunAsync(
            Guid inspectionId,
            IProgress<HardwareInspectionRunProgress> progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(HardwareInspectionRunResult.CreateCancelled(inspectionId));
        }
    }

    private sealed class CompletedHardwareService : IHardwareInspectionService
    {
        internal int CallCount { get; private set; }

        public Task<HardwareInspectionRunResult> RunAsync(
            Guid inspectionId,
            IProgress<HardwareInspectionRunProgress> progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            long sequence = 0;
            foreach (HardwareInspectionRunStage stage in
                     Enum.GetValues<HardwareInspectionRunStage>())
            {
                progress.Report(new HardwareInspectionRunProgress(
                    inspectionId,
                    ++sequence,
                    stage));
            }

            return Task.FromResult(HardwareInspectionRunResult.CreateCompleted(
                inspectionId,
                HardwareInspectionOutcome.Completed,
                HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(
                    inspectionId)));
        }
    }

    private sealed class FixedMemoryProvider(
        ulong availableBytes,
        DateTimeOffset capturedAtUtc) : IAvailableMemoryProvider
    {
        public ValueTask<AvailableMemorySnapshot> CaptureAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(
                new AvailableMemorySnapshot(availableBytes, capturedAtUtc));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class NeverCalledOpenVinoWorkerClient : IOpenVinoWorkerClient
    {
        public Task<IOpenVinoEvent> InspectAsync(
            StartInspectionCommand command,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException("The worker must not be invoked.");

        public Task<OpenVinoConversation> StartSessionAsync(
            StartSessionCommand command,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException("The worker must not be invoked.");
    }
}
