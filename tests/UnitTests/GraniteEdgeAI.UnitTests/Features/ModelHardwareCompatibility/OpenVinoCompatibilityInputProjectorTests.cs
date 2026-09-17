using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.ModelHardwareCompatibility;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.ViewModels;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

using InspectionOutcome = GraniteEdgeAI.Features.ModelInspection.Contracts.ModelInspectionOutcome;

[TestClass]
public sealed class OpenVinoCompatibilityInputProjectorTests
{
    private static readonly Guid ModelRunId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ModelHandoffId =
        Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid HardwareRunId =
        Guid.Parse("44444444-4444-4444-8444-444444444444");
    private const string Sha256 =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [TestMethod]
    public void InspectedGraniteShapeFlowsIntoOpenVinoCompatibilityInput()
    {
        OpenVinoStaticPackageEvidence evidence = new(
            1,
            Sha256,
            Sha256,
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
        ModelInspectionHandoff model = new(
            ModelInspectionHandoff.CurrentSchemaVersion,
            ModelHandoffId,
            ModelRunId,
            InspectionOutcome.Ready,
            Sha256,
            evidence.ModelLengthBytes);
        HardwareInspectionHandoff hardware = HardwareInspectionHandoff.Create(
            HardwareRunId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(HardwareRunId));

        bool preparedSuccessfully = OpenVinoCompatibilityInputProjector.TryPrepare(
            model,
            evidence,
            HardwareRunId,
            hardware,
            out PreparedOpenVinoCompatibilityInput? prepared);

        Assert.IsTrue(preparedSuccessfully);
        Assert.IsNotNull(prepared);
        Assert.AreEqual(40, prepared.Model.LayerCount);
        Assert.AreEqual(4_096, prepared.Model.EmbeddingSize);
        Assert.AreEqual(32, prepared.Model.AttentionHeadCount);
        Assert.AreEqual(8, prepared.Model.KeyValueHeadCount);
        Assert.AreEqual(131_072, prepared.Model.DeclaredContextLimit);
    }

    [UITestMethod]
    public void ExactExperimentedPackageReceivesItsEvidenceParameterBucket()
    {
        const string modelSha =
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256;
        OpenVinoStaticPackageEvidence evidence = new(
            1,
            Sha256,
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
        ModelInspectionHandoff model = new(
            ModelInspectionHandoff.CurrentSchemaVersion,
            ModelHandoffId,
            ModelRunId,
            InspectionOutcome.Ready,
            modelSha,
            evidence.ModelLengthBytes);
        HardwareInspectionHandoff hardware = HardwareInspectionHandoff.Create(
            HardwareRunId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(
                HardwareRunId));

        Assert.IsTrue(OpenVinoCompatibilityInputProjector.TryPrepare(
            model,
            evidence,
            HardwareRunId,
            hardware,
            out PreparedOpenVinoCompatibilityInput? prepared));
        Assert.AreEqual(
            VerifiedOpenVinoOptimizationEvidence.ParameterCount,
            prepared!.Model.ParameterCount);
    }

    [UITestMethod]
    public void ConstrainedExperimentedPackageOffersOnlyItsSafeVerifiedInt4CacheFormat()
    {
        PreparedOpenVinoCompatibilityInput prepared = PrepareExactExperimentedInput();
        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(
            prepared,
            VerifiedReleasedBuildEvidence(),
            optimizationAvailable: true,
            out OpenVinoOptimizationProductionAuthority? authority));
        Assert.IsNotNull(authority);

        CompatibilityEvaluation evaluation = EvaluateWithAvailableMemory(
            authority,
            4_300_000_000UL);

        Assert.AreEqual(
            CompatibilityScreenState.OptimisationRequired,
            evaluation.Screen.State);
        Assert.IsNotNull(evaluation.PlanningSession);
        Assert.IsTrue(authority.TryGetOptimizationAuthority(
            OptimizationRoute.OpenVino,
            out IOptimizationExecutionPayloadComposer? composer,
            out OptimizationIssuanceAuthority? issuance));
        Assert.IsNotNull(composer);
        Assert.IsNotNull(issuance);

        var selectedCaches = new HashSet<OpenVinoKvCachePrecision>();
        foreach (int value in new[] { 0, 25, 50, 75, 100 })
        {
            OptimizationExecutionPlan plan = evaluation.PlanningSession!.Issue(
                OptimizationPreferenceSelection.Manual(value),
                composer!,
                issuance!,
                TimeProvider.System);
            Assert.AreEqual(OpenVinoWeightPrecision.FourBit,
                plan.ExecutionPayload.OpenVino!.TargetWeightPrecision);
            Assert.IsTrue(plan.Candidate.Evidence is not null,
                "Every selectable OpenVINO setup must retain exact measured evidence.");
            selectedCaches.Add(plan.ExecutionPayload.OpenVino.KvCachePrecision);
        }

        CollectionAssert.AreEquivalent(
            new[]
            {
                OpenVinoKvCachePrecision.U4
            },
            selectedCaches.ToArray());
    }

    [TestMethod]
    public void MissingOptionalConverterKeepsCurrentModelCompatibilityAvailable()
    {
        PreparedOpenVinoCompatibilityInput prepared = PrepareExactExperimentedInput();

        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(
            prepared,
            VerifiedReleasedBuildEvidence(),
            optimizationAvailable: false,
            out OpenVinoOptimizationProductionAuthority? authority));
        Assert.IsNotNull(authority);

        CompatibilityFreshResourcesInput fresh = CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(16UL * 1024 * 1024 * 1024),
            availableDedicatedDeviceMemoryBytes: null,
            availableStorageBytes: 64UL * 1024 * 1024 * 1024,
            DateTimeOffset.UtcNow);
        CompatibilityEvaluation evaluation = authority.Evaluate(
            fresh,
            new HashSet<string>(StringComparer.Ordinal),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.AreNotEqual(
            CompatibilityScreenState.NotEstablished,
            evaluation.Screen.State,
            "The imported FP16 package must match one current-model capability.");
        Assert.IsFalse(authority.TryGetOptimizationAuthority(
            OptimizationRoute.OpenVino,
            out IOptimizationExecutionPayloadComposer? composer,
            out var issuance));
        Assert.IsNull(composer);
        Assert.IsNull(issuance);
        Assert.IsTrue(authority.IsCurrentModelChatAvailable(OptimizationRoute.OpenVino));
    }

    [TestMethod]
    public void LowMemoryRawPackageOffersAQuantisedOpenVinoConfiguration()
    {
        PreparedOpenVinoCompatibilityInput prepared = PrepareExactExperimentedInput();
        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(
            prepared,
            VerifiedReleasedBuildEvidence(),
            optimizationAvailable: true,
            out OpenVinoOptimizationProductionAuthority? authority));
        Assert.IsNotNull(authority);

        CompatibilityFreshResourcesInput fresh = CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(4UL * 1024 * 1024 * 1024),
            availableDedicatedDeviceMemoryBytes: null,
            availableStorageBytes: 64UL * 1024 * 1024 * 1024,
            DateTimeOffset.UtcNow);
        CompatibilityEvaluation evaluation = authority.Evaluate(
            fresh,
            new HashSet<string>(StringComparer.Ordinal),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.AreEqual(
            CompatibilityScreenState.OptimisationRequired,
            evaluation.Screen.State,
            "A raw FP16 package that does not fit must retain a smaller admitted " +
            "INT8 or INT4 route instead of becoming a dead end.");
        Assert.IsNotNull(evaluation.Screen.Optimization);
        Assert.IsNotNull(evaluation.PlanningSession);
    }

    [TestMethod]
    public void ExactBundledTurboQuantBuildPublishesBothExecutableCacheFormats()
    {
        PreparedOpenVinoCompatibilityInput prepared = PrepareInput();

        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(
            prepared,
            ReleasedBuildEvidence(),
            TurboQuantBuildEvidence(),
            true,
            out OpenVinoOptimizationProductionAuthority? authority));
        Assert.IsNotNull(authority);

        Assert.AreEqual(2, authority.CurrentState.CapabilityEvidenceSet.Count);
        Assert.IsTrue(authority.CurrentState.CapabilityEvidenceSet.Any(
            static evidence => evidence.Builds.TurboQuantBuild is not null));

        OpenVinoKvCacheFormat[] admitted = authority.CapabilitySnapshot.OpenVino!
            .Admitted
            .Where(static candidate =>
                candidate.Level == GraniteEdgeAI.ModelHardwareCompatibility.Core
                    .Domain.SupportLevel.Experimental)
            .Select(static candidate => candidate.KvCache)
            .Distinct()
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[]
            {
                OpenVinoKvCacheFormat.TurboQuantTbq4,
                OpenVinoKvCacheFormat.TurboQuantTbq3
            },
            admitted);
    }

    [TestMethod]
    public void ExactTurboQuantRouteProjectsDistinctSafeWeightAndCacheChoices()
    {
        PreparedOpenVinoCompatibilityInput prepared =
            PrepareExactExperimentedInput();
        string? admissionFailure = null;
        EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> capture =
            (_, args) =>
            {
                if (args.Exception is ArgumentException or OverflowException)
                    admissionFailure = args.Exception.ToString();
            };
        OpenVinoOptimizationProductionAuthority? authority;
        bool created;
        AppDomain.CurrentDomain.FirstChanceException += capture;
        try
        {
            created = OpenVinoOptimizationProductionAuthority.TryCreate(
            prepared,
            VerifiedReleasedBuildEvidence(),
            TurboQuantBuildEvidence(),
            optimizationAvailable: true,
            out authority);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= capture;
        }
        Assert.IsTrue(created, admissionFailure ?? "No optimisation authority was created.");
        Assert.IsNotNull(authority);

        CompatibilityEvaluation evaluation = EvaluateWithAvailableMemory(
            authority,
            4_300_000_000UL);

        Assert.IsNotNull(evaluation.PlanningSession);
        Assert.IsTrue(authority.TryGetOptimizationAuthority(
            OptimizationRoute.OpenVino,
            out IOptimizationExecutionPayloadComposer? composer,
            out OptimizationIssuanceAuthority? issuance));

        var weights = new HashSet<OpenVinoWeightPrecision>();
        var caches = new HashSet<OpenVinoKvCachePrecision>();
        var qualities = new HashSet<decimal>();
        foreach (int value in new[] { 0, 25, 50, 75, 100 })
        {
            OptimizationExecutionPlan plan = evaluation.PlanningSession!.Issue(
                OptimizationPreferenceSelection.Manual(value),
                composer!,
                issuance!,
                TimeProvider.System);
            Assert.IsTrue(plan.Candidate.Metrics.FitsSafely);
            var adapted = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoOptimizationPlanAdapter.Adapt(
                plan, authority.CurrentState.Capabilities,
                authority.CurrentState.CapabilityEvidenceSet,
                prepared.ModelSha256, prepared.Model.PackageLengthBytes);
            Assert.AreEqual(GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoOptimizationAdaptationStatus.Ready, adapted.Status,
                "An issued exact-runtime plan must survive execution preflight.");
            weights.Add(plan.ExecutionPayload.OpenVino!.TargetWeightPrecision);
            caches.Add(plan.ExecutionPayload.OpenVino.KvCachePrecision);
            qualities.Add(plan.Candidate.Evidence!.Quality.Value);
        }

        CollectionAssert.AreEquivalent(
            new[]
            {
                OpenVinoWeightPrecision.FourBit
            },
            weights.ToArray(),
            "The released low-memory choices use INT4 weights.");
        CollectionAssert.IsSubsetOf(
            new[]
            {
                OpenVinoKvCachePrecision.Tbq4,
                OpenVinoKvCachePrecision.Tbq3
            },
            caches.ToArray(),
            "Both real-model TurboQuant cache codecs must be reachable.");
        Assert.IsTrue(qualities.Count > 1,
            "Independently measured configurations retain distinct quality scores even when their labels share a band.");
    }

    [TestMethod]
    public void RepeatedEvaluationCanMoveFromNoFitIntoOpenVinoOptimisation()
    {
        PreparedOpenVinoCompatibilityInput prepared = PrepareExactExperimentedInput();
        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(
            prepared,
            VerifiedReleasedBuildEvidence(),
            optimizationAvailable: true,
            out OpenVinoOptimizationProductionAuthority? authority));
        Assert.IsNotNull(authority);

        CompatibilityEvaluation constrained = EvaluateWithAvailableMemory(
            authority,
            3_600_000_000UL);
        Assert.AreEqual(
            CompatibilityScreenState.NoEstimatedSafeConfiguration,
            constrained.Screen.State);

        CompatibilityEvaluation recovered = EvaluateWithAvailableMemory(
            authority,
            4_300_000_000UL);
        Assert.AreEqual(
            CompatibilityScreenState.OptimisationRequired,
            recovered.Screen.State,
            "Refreshing after memory becomes available must construct a new optimisation plan.");
        Assert.IsNotNull(recovered.Screen.Optimization);
        Assert.IsNotNull(recovered.PlanningSession);
    }

    [TestMethod]
    public async Task ActionableOpenVinoEvaluationPublishesAnIssuedOptimisation()
    {
        PreparedOpenVinoCompatibilityInput prepared = PrepareExactExperimentedInput();
        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(
            prepared,
            VerifiedReleasedBuildEvidence(),
            optimizationAvailable: true,
            out OpenVinoOptimizationProductionAuthority? authority));
        Assert.IsNotNull(authority);
        DateTimeOffset now = new(2026, 9, 1, 7, 30, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(now);
        var viewModel = new CompatibilityViewModel(
            (optedIn, token) => Task.FromResult(authority.Evaluate(
                CompatibilityFreshResourcesInput.Create(
                    CurrentlyAvailableMemory.FromBytes(4_300_000_000UL),
                    availableDedicatedDeviceMemoryBytes: null,
                    availableStorageBytes: 64UL * 1024 * 1024 * 1024,
                    now),
                optedIn,
                now,
                token)),
            actionAuthority: authority,
            timeProvider: clock);

        await viewModel.StartAsync();

        Assert.AreEqual(
            "This model needs to be quantised to run on your computer",
            viewModel.Presentation.OutcomeTitle);
        Assert.IsTrue(viewModel.Presentation.PrimaryActionEnabled);
        Assert.IsNotNull(viewModel.SelectedPreference);
        Assert.IsNotNull(viewModel.CurrentOptimizationHandoff);
    }

    [TestMethod]
    public void Fp32BoundaryWithVerifiedFp16WeightsRetainsOptimizationAuthority()
    {
        OpenVinoStaticPackageEvidence evidence = new(
            1, Sha256, Sha256, 6_805_673_303, "granite",
            "GraniteForCausalLM", "text-generation-with-past", 131_072,
            "float32", "PreTrainedTokenizerFast", 10, true,
            40, 4_096, 32, 8,
            WeightPrecision: "float16");
        ModelInspectionHandoff model = new(
            ModelInspectionHandoff.CurrentSchemaVersion,
            ModelHandoffId,
            ModelRunId,
            InspectionOutcome.Ready,
            Sha256,
            evidence.ModelLengthBytes);
        HardwareInspectionHandoff hardware = HardwareInspectionHandoff.Create(
            HardwareRunId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(HardwareRunId));
        Assert.IsTrue(OpenVinoCompatibilityInputProjector.TryPrepare(
            model, evidence, HardwareRunId, hardware,
            out PreparedOpenVinoCompatibilityInput? prepared));

        Assert.AreEqual("float16", prepared!.SourcePrecision);
        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(
            prepared,
            BuildEvidence(),
            optimizationAvailable: true,
            out OpenVinoOptimizationProductionAuthority? authority));
        Assert.IsNotNull(authority);
    }

    [UITestMethod]
    public void UnverifiedImportedInt4PackageRetainsPrecisionWithoutInventingChatAuthority()
    {
        OpenVinoStaticPackageEvidence evidence = new(
            1, Sha256, Sha256, 1_704_489_303, "granite",
            "GraniteForCausalLM", "text-generation-with-past", 131_072,
            "float16", "PreTrainedTokenizerFast", 10, true,
            40, 4_096, 32, 8,
            WeightPrecision: "int4");
        ModelInspectionHandoff model = new(
            ModelInspectionHandoff.CurrentSchemaVersion,
            ModelHandoffId,
            ModelRunId,
            InspectionOutcome.Ready,
            Sha256,
            evidence.ModelLengthBytes);
        HardwareInspectionHandoff hardware = HardwareInspectionHandoff.Create(
            HardwareRunId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(HardwareRunId));

        Assert.IsTrue(OpenVinoCompatibilityInputProjector.TryPrepare(
            model, evidence, HardwareRunId, hardware,
            out PreparedOpenVinoCompatibilityInput? prepared));

        Assert.IsNotNull(prepared);
        Assert.AreEqual(OpenVinoWeightFormat.Original, prepared.Configuration.Weights);
        Assert.AreEqual(
            OpenVinoWeightPrecision.FourBit,
            prepared.CurrentModel.OpenVinoSourcePrecision);
        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(
            prepared,
            BuildEvidence(),
            optimizationAvailable: true,
            out OpenVinoOptimizationProductionAuthority? authority),
            "An already-optimized package still needs compatibility and chat authority.");
        Assert.IsNotNull(authority);
        CompatibilityEvaluation evaluation = EvaluateWithAvailableMemory(
            authority,
            7UL * 1024 * 1024 * 1024);
        Assert.AreEqual(
            CompatibilityScreenState.NotEstablished,
            evaluation.Screen.State,
            "The synthetic worker and source identities do not establish a verified execution route.");
        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(evaluation);
        Assert.IsFalse(presentation.PrimaryActionEnabled);
        using var custody = new ModelSourceCustodyRegistry();
        using var registry = new CurrentModelChatLaunchRegistry(custody);
        Assert.IsNull(
            authority.ResolveCurrentModel(evaluation, registry),
            "Unverified identities must not produce a current-model launch handoff.");
    }

    [TestMethod]
    public void FreshAvailabilityAboveEarlierHardwareFactsIsConservativelyBound()
    {
        PreparedOpenVinoCompatibilityInput prepared = PrepareExactExperimentedInput();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityFreshResourcesInput fresh = CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(
                prepared.Hardware.InstalledSystemMemoryBytes + 1),
            availableDedicatedDeviceMemoryBytes: null,
            availableStorageBytes: prepared.Hardware.FreeStorageBytes + 1,
            now);

        Assert.IsTrue(prepared.TryBindFresh(
            fresh,
            out CompatibilityProductionInput? input));
        Assert.IsNotNull(input);
        Assert.AreEqual(
            prepared.Hardware.InstalledSystemMemoryBytes,
            input.FreshResources.AvailableSystemMemoryBytes);
        Assert.AreEqual(
            prepared.Hardware.FreeStorageBytes,
            input.FreshResources.AvailableStorageBytes);

        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(
            prepared,
            VerifiedReleasedBuildEvidence(),
            optimizationAvailable: true,
            out OpenVinoOptimizationProductionAuthority? authority));
        CompatibilityEvaluation evaluation = authority!.Evaluate(
            fresh,
            new HashSet<string>(StringComparer.Ordinal),
            now,
            CancellationToken.None);
        Assert.AreNotEqual(
            CompatibilityScreenState.NotEstablished,
            evaluation.Screen.State);
    }

    private static PreparedOpenVinoCompatibilityInput PrepareInput()
    {
        OpenVinoStaticPackageEvidence evidence = new(
            1, Sha256, Sha256, 6_805_673_303, "granite",
            "GraniteForCausalLM", "text-generation-with-past", 131_072,
            "float16", "PreTrainedTokenizerFast", 10, true,
            40, 4_096, 32, 8);
        ModelInspectionHandoff model = new(
            ModelInspectionHandoff.CurrentSchemaVersion,
            ModelHandoffId,
            ModelRunId,
            InspectionOutcome.Ready,
            Sha256,
            evidence.ModelLengthBytes);
        HardwareInspectionHandoff hardware = HardwareInspectionHandoff.Create(
            HardwareRunId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(HardwareRunId));
        Assert.IsTrue(OpenVinoCompatibilityInputProjector.TryPrepare(
            model, evidence, HardwareRunId, hardware,
            out PreparedOpenVinoCompatibilityInput? prepared));
        return prepared!;
    }

    [TestMethod]
    public void ImportedMxFp4PackageRetainsAStandardCurrentModelAuthority()
    {
        OpenVinoStaticPackageEvidence evidence = new(
            1, Sha256, Sha256, 1_808_481_280, "granite",
            "GraniteForCausalLM", "text-generation-with-past", 131_072,
            "float16", "PreTrainedTokenizerFast", 10, true,
            40, 4_096, 32, 8,
            WeightPrecision: "mxfp4");
        ModelInspectionHandoff model = new(
            ModelInspectionHandoff.CurrentSchemaVersion,
            ModelHandoffId,
            ModelRunId,
            InspectionOutcome.Ready,
            Sha256,
            evidence.ModelLengthBytes);
        HardwareInspectionHandoff hardware = HardwareInspectionHandoff.Create(
            HardwareRunId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(HardwareRunId));

        Assert.IsTrue(OpenVinoCompatibilityInputProjector.TryPrepare(
            model, evidence, HardwareRunId, hardware,
            out PreparedOpenVinoCompatibilityInput? prepared));
        Assert.AreEqual(OpenVinoWeightFormat.Original, prepared!.Configuration.Weights);
        Assert.AreEqual(OpenVinoWeightPrecision.MxFp4,
            prepared.CurrentModel.OpenVinoSourcePrecision);
        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(
            prepared,
            BuildEvidence(),
            optimizationAvailable: true,
            out OpenVinoOptimizationProductionAuthority? authority));
        Assert.IsNotNull(authority);
    }

    private static PreparedOpenVinoCompatibilityInput PrepareExactExperimentedInput()
    {
        const string modelSha =
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256;
        OpenVinoStaticPackageEvidence evidence = new(
            1, Sha256, modelSha, 6_805_673_303, "granite",
            "GraniteForCausalLM", "text-generation-with-past", 131_072,
            "float16", "PreTrainedTokenizerFast", 10, true,
            40, 4_096, 32, 8);
        ModelInspectionHandoff model = new(
            ModelInspectionHandoff.CurrentSchemaVersion,
            ModelHandoffId,
            ModelRunId,
            InspectionOutcome.Ready,
            modelSha,
            evidence.ModelLengthBytes);
        HardwareInspectionHandoff hardware = HardwareInspectionHandoff.Create(
            HardwareRunId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(
                HardwareRunId));
        Assert.IsTrue(OpenVinoCompatibilityInputProjector.TryPrepare(
            model, evidence, HardwareRunId, hardware,
            out PreparedOpenVinoCompatibilityInput? prepared));
        return prepared!;
    }

    private static OpenVinoBuildEvidence BuildEvidence() => new(
        "2026.5.0-22950-f5f594dc0c9",
        "6fbc103538d30d42da4b0b5130a4792a20f728ba",
        "2026.5.0",
        new string('1', 64));

    private static OpenVinoBuildEvidence ReleasedBuildEvidence() => new(
        "2026.3.0-22451-8a17657b995-releases/2026/3",
        "2026.3.0.0-3277-bd8d6542e3c",
        "2026.3.0.0-703-183c6f25cda",
        new string('5', 64));

    private static OpenVinoBuildEvidence VerifiedReleasedBuildEvidence() => new(
        VerifiedOpenVinoOptimizationEvidence.RuntimeBuild,
        VerifiedOpenVinoOptimizationEvidence.GenAiBuild,
        VerifiedOpenVinoOptimizationEvidence.TokenizersBuild,
        VerifiedOpenVinoOptimizationEvidence.WorkerManifestSha256);

    private static OpenVinoBuildEvidence TurboQuantBuildEvidence() => new(
        VerifiedOpenVinoOptimizationEvidence.TurboRuntimeBuild,
        VerifiedOpenVinoOptimizationEvidence.TurboGenAiBuild,
        VerifiedOpenVinoOptimizationEvidence.TurboTokenizersBuild,
        VerifiedOpenVinoOptimizationEvidence.TurboWorkerManifestSha256,
        new GraniteEdgeAI.OpenVino.Contracts.TurboQuantBuildEvidence(
            "f5f594dc0c9e5961785f0d17743486d52eac87e7",
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            VerifiedOpenVinoOptimizationEvidence.TurboPatchSeriesSha256,
            VerifiedOpenVinoOptimizationEvidence.TurboRuntimeManifestSha256));

    private static CompatibilityEvaluation EvaluateWithAvailableMemory(
        OpenVinoOptimizationProductionAuthority authority,
        ulong availableMemoryBytes)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityFreshResourcesInput fresh = CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(availableMemoryBytes),
            availableDedicatedDeviceMemoryBytes: null,
            availableStorageBytes: 64UL * 1024 * 1024 * 1024,
            now);
        return authority.Evaluate(
            fresh,
            new HashSet<string>(StringComparer.Ordinal),
            now,
            CancellationToken.None);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
