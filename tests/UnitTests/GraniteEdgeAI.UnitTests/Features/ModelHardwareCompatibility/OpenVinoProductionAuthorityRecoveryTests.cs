using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.OpenVino.WorkerClient;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.Features.ModelHardwareCompatibility;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Line = Microsoft.UI.Xaml.Shapes.Line;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
[DoNotParallelize]
public sealed class OpenVinoProductionAuthorityRecoveryTests
{
    public TestContext TestContext { get; set; } = null!;

    [UITestMethod]
    [TestCategory("CurrentRawOpenVinoComposition")]
    [TestCategory("RequiresLocalModelPackage")]
    public async Task ActualRawPackageCurrentWorkersReachAlternativesAndQualifiedMinimum()
    {
        const string path = "C:/AI/Models/Granite-4.1-3B-OpenVINO-Raw";
        Assert.IsTrue(Directory.Exists(path), "Exact raw package is required.");
        var inspection = await Task.Run(() => new OpenVinoStaticPackageInspector().Inspect(path));
        Assert.IsNotNull(inspection.Evidence);
        var evidence = inspection.Evidence!;
        Assert.AreEqual(VerifiedOpenVinoOptimizationEvidence.SourceModelSha256, evidence.ModelSha256);
        Assert.AreEqual(6_805_673_303L, evidence.ModelLengthBytes);
        var hardwareRun = Guid.NewGuid();
        var handoff = new ModelInspectionHandoff(ModelInspectionHandoff.CurrentSchemaVersion,
            Guid.NewGuid(), Guid.NewGuid(), ModelInspectionOutcome.Ready, evidence.ModelSha256, evidence.ModelLengthBytes);
        var hardware = HardwareInspectionHandoff.Create(hardwareRun, HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(hardwareRun));
        Assert.IsTrue(OpenVinoCompatibilityInputProjector.TryPrepare(handoff, evidence, hardwareRun, hardware, out var prepared));
        var official = OpenVinoOfficialWorkerAuthority.CreateInstallation(
            "C:/AI/shared-chat-official-stage-20260909-01",
            VerifiedOpenVinoOptimizationEvidence.PackagedOfficialWorkerManifestSha256);
        var turbo = OpenVinoTurboQuantWorkerAuthority.CreateInstallation(
            "C:/AI/shared-chat-tq-stage-20260909-01",
            "39a4eccc05d4677b59f7f882cc50f875591ee3d3f90f1037e61e17fe3a34c35e",
            VerifiedOpenVinoOptimizationEvidence.TurboPatchSeriesSha256,
            VerifiedOpenVinoOptimizationEvidence.TurboRuntimeManifestSha256);
        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(prepared!, official.ExpectedBuildEvidence,
            turbo.ExpectedBuildEvidence, true, out var authority));
        var now = DateTimeOffset.UtcNow;
        var evaluation = authority!.Evaluate(CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(8UL * 1024 * 1024 * 1024), null,
            64UL * 1024 * 1024 * 1024, now), new HashSet<string>(), now, CancellationToken.None);
        Assert.IsNotNull(evaluation.PlanningSession);
        Assert.IsNotNull(evaluation.Screen.Optimization);
        CompatibilityOptimizationView optimization = evaluation.Screen.Optimization!;
        var modes = optimization.ExactSafeModes;
        Assert.IsTrue(modes.Any(item => item.Mode.OpenVinoKvCache == OpenVinoKvCacheFormat.U4));
        Assert.IsTrue(modes.Any(item => item.Mode.OpenVinoKvCache == OpenVinoKvCacheFormat.TurboQuantTbq3));
        // The page maps preference bands; the authority supplies exact safe candidates.
        foreach (var (weights, cache) in new[]
        {
            (OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.TurboQuantTbq3),
            (OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.TurboQuantTbq4),
            (OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.U4),
            (OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.U8),
            (OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.RouteDefault)
        })
        {
            Assert.IsTrue(modes.Any(item => item.Mode.OpenVinoWeights == weights
                && item.Mode.OpenVinoKvCache == cache), $"Missing released choice: {weights}/{cache}");
        }
        var presentation = CompatibilityPresentationFactory.From(evaluation);
        Assert.IsNotNull(presentation.MemoryOverview);
        Assert.IsNotNull(presentation.MemoryOverview.MinimumRequiredBytes);
        Assert.IsTrue(authority.TryGetOptimizationAuthority(OptimizationRoute.OpenVino, out var composer, out var issuance));
        var plan = evaluation.PlanningSession!.Issue(OptimizationPreferenceSelection.Automatic(), composer!, issuance!, TimeProvider.System);
        Assert.AreEqual(OpenVinoWeightPrecision.Fp16, plan.ExecutionPayload.OpenVino!.SourceWeightPrecision);
        Assert.IsTrue(plan.ExecutionPayload.RequiresPersistentConversion);
        Assert.IsTrue(plan.ProducesPersistentArtifact);
    }

    [UITestMethod]
    [TestCategory("CurrentRetainedOpenVinoU4")]
    [TestCategory("RequiresLocalModelPackage")]
    public async Task ActualRetainedPackageReachesVerifiedU4IssuerWhileFailedDefaultRemainsBlocked()
    {
        const string path = "C:/Users/Student/AppData/Local/GraniteEdgeAI/Optimization/OpenVinoOutputs/output-05c2cd0580c54e18a110091fff7473bc-1";
        if (!Directory.Exists(path)) Assert.Inconclusive("Exact retained package is required for this evidence test.");
        var inspection = await Task.Run(() => new OpenVinoStaticPackageInspector().Inspect(path));
        Assert.IsNotNull(inspection.Evidence);
        var evidence = inspection.Evidence!;
        Assert.AreEqual("d5f33732fddb37c150f8ebd4be163c40474a245cd3a8f7683b63996d5f60ed68", evidence.PackageManifestDigest);
        Assert.AreEqual("fcfb6ec62a2b823d7d1aebedee193083eaa2f86d9e89722e46102c7a4b90bd27", evidence.ModelSha256);
        var hardwareRun = Guid.NewGuid();
        var handoff = new ModelInspectionHandoff(ModelInspectionHandoff.CurrentSchemaVersion,
            Guid.NewGuid(), Guid.NewGuid(), ModelInspectionOutcome.Ready, evidence.ModelSha256, evidence.ModelLengthBytes);
        var hardware = HardwareInspectionHandoff.Create(hardwareRun, HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(hardwareRun));
        var official = OpenVinoOfficialWorkerAuthority.CreateInstallation(
            "C:/AI/shared-chat-official-stage-20260909-01",
            "f0089dae967a0b4249238f9bf49db02ba44e111c78b83ebff36778f6d0ddcbe2");
        foreach (bool exactPackage in new[] { true, false })
        {
            var projectedEvidence = exactPackage ? evidence : evidence with { PackageManifestDigest = new string('a', 64) };
            Assert.IsTrue(OpenVinoCompatibilityInputProjector.TryPrepare(handoff, projectedEvidence, hardwareRun,
                hardware, out var prepared));
            Assert.AreEqual(3_402_836_480UL, prepared!.Model.ParameterCount);
            Assert.AreEqual(projectedEvidence.PackageManifestDigest, prepared.PackageManifestSha256);
            Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(prepared, official.ExpectedBuildEvidence,
                true, out var authority));
            var now = DateTimeOffset.UtcNow;
            var evaluation = authority!.Evaluate(CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(8UL * 1024 * 1024 * 1024), null,
                64UL * 1024 * 1024 * 1024, now), new HashSet<string>(), now, CancellationToken.None);
            Assert.IsFalse(evaluation.Screen.UseCurrentModelAvailable);
            Assert.IsTrue(authority.IsCurrentModelChatAvailable(OptimizationRoute.OpenVino),
                "Route capability remains available; failed-baseline eligibility is refused by the screen and current-chat command.");
            if (!exactPackage)
            {
                Assert.AreEqual(CompatibilityScreenState.NotEstablished, evaluation.Screen.State);
                Assert.IsFalse(evaluation.Screen.ContinueEnabled);
                continue;
            }
            Assert.AreEqual(CompatibilityScreenState.OptimisationRequired, evaluation.Screen.State);
            Assert.AreEqual(CompatibilityFitState.Safe, evaluation.Screen.CurrentSetup!.Fit);
            Assert.IsTrue(evaluation.Screen.ContinueEnabled);
            Assert.IsNotNull(evaluation.PlanningSession);
            Assert.IsTrue(evaluation.Screen.Optimization!.SafeSliderModes.All(item => item.Mode.OpenVinoKvCache == OpenVinoKvCacheFormat.U4));
            Assert.IsTrue(authority.TryGetOptimizationAuthority(OptimizationRoute.OpenVino, out var composer, out var issuance));
            var plan = evaluation.PlanningSession!.Issue(OptimizationPreferenceSelection.Automatic(), composer!, issuance!, TimeProvider.System);
            Assert.AreEqual(OpenVinoKvCachePrecision.U4, plan.ExecutionPayload.OpenVino!.KvCachePrecision);
            Assert.AreEqual(OpenVinoWeightPrecision.FourBit, plan.ExecutionPayload.OpenVino.SourceWeightPrecision);
            Assert.AreEqual(OpenVinoWeightPrecision.FourBit, plan.ExecutionPayload.OpenVino.TargetWeightPrecision);
            Assert.IsFalse(plan.ProducesPersistentArtifact);
            Assert.IsFalse(plan.ExecutionPayload.RequiresPersistentConversion);
            Assert.ThrowsExactly<ArgumentException>(() => evaluation.PlanningSession.Issue(
                OptimizationPreferenceSelection.Automatic(), new FalsePersistentComposer(composer!), issuance!, TimeProvider.System));
            Assert.ThrowsExactly<ArgumentException>(() => evaluation.PlanningSession.Issue(
                OptimizationPreferenceSelection.Automatic(), composer!, issuance!, new EvidenceTimeProvider(now.AddSeconds(31))));
            var presentation = CompatibilityPresentationFactory.From(evaluation);
            Assert.IsTrue(presentation.PrimaryActionEnabled);
            StringAssert.Contains(presentation.OutcomeDetail, "failed output checks");
            Assert.IsNotNull(presentation.MemoryOverview);
            Assert.AreEqual(evaluation.Screen.Optimization.SafeSliderModes.Min(item => item.Mode.SystemSharedPredictedPeakBytes),
                presentation.MemoryOverview.MinimumRequiredBytes);
            foreach (ElementTheme theme in new[] { ElementTheme.Light, ElementTheme.Dark })
            {
                var page = new CompatibilityPage { StartAutomatically = false, RequestedTheme = theme };
                await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(page, 1200, 700);
                page.Apply(presentation);
                var primary = (Button)page.FindName("BtnCompatibilityPrimary");
                Assert.AreEqual(Visibility.Visible, primary.Visibility);
                Assert.IsTrue(primary.IsEnabled);
                Assert.AreEqual("Choose optimisation", primary.Content);
                Assert.AreEqual("CompatibilityAction.ConfigureModel", AutomationProperties.GetAutomationId(primary));
                Assert.IsFalse(page.ViewModel.ChatCurrentModelCommand.CanExecute(null));
                Assert.AreEqual(Visibility.Visible, ((Line)page.FindName("CompatibilityMinimumRamMarker")).Visibility);
                StringAssert.Contains(((TextBlock)page.FindName("CompatibilityMinimumRamLabel")).Text, "Smallest");
                RenderedFrame frame = await host.CaptureAsync();
                TestContext.AddResultFile(await frame.SavePngAsync($"verified-u4-failed-default-{theme}.png"));
            }
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            var cancelledEvaluation = authority.Evaluate(CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(8UL * 1024 * 1024 * 1024), null,
                64UL * 1024 * 1024 * 1024, now), new HashSet<string>(), now, cancelled.Token);
            Assert.AreEqual(CompatibilityScreenState.Cancelled, cancelledEvaluation.Screen.State);
            Assert.IsFalse(cancelledEvaluation.Screen.ContinueEnabled);
            Assert.IsFalse(cancelledEvaluation.Screen.UseCurrentModelAvailable);
            Assert.IsNull(cancelledEvaluation.PlanningSession);
        }
        var after = await Task.Run(() => new OpenVinoStaticPackageInspector().Inspect(path));
        Assert.AreEqual(evidence.PackageManifestDigest, after.Evidence!.PackageManifestDigest);
    }

    private sealed class EvidenceTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FalsePersistentComposer(IOptimizationExecutionPayloadComposer inner) : IOptimizationExecutionPayloadComposer
    {
        public OptimizationRoute Route => OptimizationRoute.OpenVino;

        public OptimizationExecutionPayload Compose(OptimizationCandidate candidate)
        {
            var valid = inner.Compose(candidate).OpenVino!;
            return OptimizationExecutionPayload.ForOpenVino(OpenVinoExecutionPayload.Create(
                valid.ConfigurationId, valid.Device, valid.Maturity, valid.EvidenceId,
                valid.SourceWeightPrecision, valid.TargetWeightPrecision, valid.KvCachePrecision,
                valid.CompiledCacheEnabled, valid.CompiledCacheIsDisposable, valid.CompiledCacheIsModelArtifact,
                createsCompletePackage: true, valid.BuildIdentity, valid.OptimizerVersions,
                valid.TurboQuantBuild, valid.KvCacheAlgorithm));
        }
    }

    [TestMethod]
    [DataRow("2c8b77eba5f26d693fe3cbde164c8fe88f5d6d2496a6f79ea666eae0dd4271d5", true)]
    [DataRow("7455fc0fcd20a2dcb38ef9636b26323e988083819ee810fba110b6171c7e6e88", true)]
    [DataRow("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    public void ExactCurrentWorkerCreatesAuthorityWhileMismatchReturnsFalseWithoutThrowing(string digest, bool accepted)
    {
        var hardwareRun = Guid.NewGuid();
        var evidence = new OpenVinoStaticPackageEvidence(1, new string('a', 64),
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256, 6_805_673_303,
            "granite", "GraniteForCausalLM", "text-generation-with-past", 131_072,
            "float16", "PreTrainedTokenizerFast", 10, true, 40, 4096, 32, 8);
        var model = new ModelInspectionHandoff(ModelInspectionHandoff.CurrentSchemaVersion,
            Guid.NewGuid(), Guid.NewGuid(), ModelInspectionOutcome.Ready,
            evidence.ModelSha256, evidence.ModelLengthBytes);
        var hardware = HardwareInspectionHandoff.Create(hardwareRun, HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(hardwareRun));
        Assert.IsTrue(OpenVinoCompatibilityInputProjector.TryPrepare(model, evidence, hardwareRun,
            hardware, out var prepared));
        var official = OpenVinoOfficialWorkerAuthority.CreateInstallation(Path.GetFullPath("official-test-worker"),
            "e6171b0e77b3b71356109e7529794b9a8a034d6b4f8576d3ad15c7ed4cd579bf");
        var turbo = OpenVinoTurboQuantWorkerAuthority.CreateInstallation(Path.GetFullPath("turbo-test-worker"), digest,
            VerifiedOpenVinoOptimizationEvidence.TurboPatchSeriesSha256,
            VerifiedOpenVinoOptimizationEvidence.TurboRuntimeManifestSha256);
        bool created = OpenVinoOptimizationProductionAuthority.TryCreate(prepared!, official.ExpectedBuildEvidence,
            turbo.ExpectedBuildEvidence, true, out var authority);
        Assert.AreEqual(accepted, created);
        if (accepted) Assert.IsNotNull(authority);
        else Assert.IsNull(authority);
    }
}
