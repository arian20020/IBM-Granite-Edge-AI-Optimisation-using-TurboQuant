using System.Reflection;
using GraniteEdgeAI.Features.ModelHardwareCompatibility;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.ViewModels;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
public sealed class OptimizationSelectionHandoffTests
{
    private const string Digest =
        "1111111111111111111111111111111111111111111111111111111111111111";
    private const string OtherDigest =
        "2222222222222222222222222222222222222222222222222222222222222222";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    internal static OptimizationJourneyEntryContext RequiredJourneyEntry()
    {
        OptimizationExecutionPlan plan = Plan(
            OptimizationPreferenceSelection.Automatic());
        Assert.IsTrue(OptimizationSelectionHandoff.TryCreate(
            plan,
            plan.Binding,
            plan.CapabilitySnapshot,
            plan.Preference,
            out OptimizationSelectionHandoff? handoff));
        return new OptimizationJourneyEntryContext(
            handoff!,
            OptimizationJourneyOrigin.Required,
            currentModelFallback: null);
    }

    [TestMethod]
    public void ExactCurrentAuthority_ProducesIdentityBoundV3Handoff()
    {
        OptimizationExecutionPlan plan = Plan(OptimizationPreferenceSelection.Manual(50));

        Assert.IsTrue(OptimizationSelectionHandoff.TryCreate(
            plan,
            plan.Binding,
            plan.CapabilitySnapshot,
            OptimizationPreferenceSelection.Manual(50),
            out OptimizationSelectionHandoff? handoff));

        Assert.IsNotNull(handoff);
        Assert.AreEqual(plan.ModelInspectionRunId, handoff.ModelInspectionRunId);
        Assert.AreEqual(plan.Binding.ModelInspectionHandoffId, handoff.ModelInspectionHandoffId);
        Assert.AreEqual(plan.ProductHardwareRunId, handoff.ProductHardwareRunId);
        Assert.AreEqual(plan.OptimizationPlanId, handoff.OptimizationPlanId);
        Assert.AreEqual(plan.ConfigurationSha256, handoff.ConfigurationSha256);
        Assert.AreSame(plan, handoff.Plan);
        Assert.AreEqual(OptimizationExecutionPlan.CurrentContractVersion,
            handoff.Plan.ContractVersion);
    }

    [TestMethod]
    public void StaleCapabilitySnapshot_RequiresNewPlan()
    {
        OptimizationExecutionPlan plan = Plan(OptimizationPreferenceSelection.Automatic());
        OptimizationCapabilitySnapshot current = Snapshot(OtherDigest);

        Assert.IsFalse(OptimizationSelectionHandoff.TryCreate(
            plan, plan.Binding, current, OptimizationPreferenceSelection.Automatic(), out _));
    }

    [TestMethod]
    public void SameCapabilityDigestWithDifferentSnapshotId_RequiresNewPlan()
    {
        OptimizationExecutionPlan plan =
            Plan(OptimizationPreferenceSelection.Automatic());
        OptimizationCapabilitySnapshot substituted =
            Snapshot(Digest, "gguf-capability-substituted");

        Assert.IsFalse(OptimizationSelectionHandoff.TryCreate(
            plan,
            plan.Binding,
            substituted,
            OptimizationPreferenceSelection.Automatic(),
            out _));
    }

    [TestMethod]
    public void ChangedSliderSelection_RequiresNewPlan()
    {
        OptimizationExecutionPlan plan = Plan(OptimizationPreferenceSelection.Manual(50));

        Assert.IsFalse(OptimizationSelectionHandoff.TryCreate(
            plan,
            plan.Binding,
            plan.CapabilitySnapshot,
            OptimizationPreferenceSelection.Manual(70),
            out _));
    }

    [TestMethod]
    public void ChangedSourceOrHardwareBinding_RequiresNewPlan()
    {
        OptimizationExecutionPlan plan = Plan(OptimizationPreferenceSelection.Automatic());
        OptimizationJourneyBinding changedSource = OptimizationJourneyBinding.Create(
            plan.Binding.ModelInspectionRunId,
            plan.Binding.ModelInspectionHandoffId,
            OtherDigest,
            plan.Binding.ModelLengthBytes,
            plan.Binding.ProductHardwareRunId,
            plan.Binding.HardwareSnapshotSha256);
        OptimizationJourneyBinding changedHardware = OptimizationJourneyBinding.Create(
            plan.Binding.ModelInspectionRunId,
            plan.Binding.ModelInspectionHandoffId,
            plan.Binding.ModelSha256,
            plan.Binding.ModelLengthBytes,
            plan.Binding.ProductHardwareRunId,
            OtherDigest);
        OptimizationJourneyBinding changedLength = OptimizationJourneyBinding.Create(
            plan.Binding.ModelInspectionRunId,
            plan.Binding.ModelInspectionHandoffId,
            plan.Binding.ModelSha256,
            plan.Binding.ModelLengthBytes + 1,
            plan.Binding.ProductHardwareRunId,
            plan.Binding.HardwareSnapshotSha256);
        OptimizationJourneyBinding changedRun = OptimizationJourneyBinding.Create(
            "66666666666646668666666666666666",
            plan.Binding.ModelInspectionHandoffId,
            plan.Binding.ModelSha256,
            plan.Binding.ModelLengthBytes,
            plan.Binding.ProductHardwareRunId,
            plan.Binding.HardwareSnapshotSha256);
        OptimizationJourneyBinding changedHandoff =
            OptimizationJourneyBinding.Create(
                plan.Binding.ModelInspectionRunId,
                "77777777777747778777777777777777",
                plan.Binding.ModelSha256,
                plan.Binding.ModelLengthBytes,
                plan.Binding.ProductHardwareRunId,
                plan.Binding.HardwareSnapshotSha256);
        OptimizationJourneyBinding changedProductHardwareRun =
            OptimizationJourneyBinding.Create(
                plan.Binding.ModelInspectionRunId,
                plan.Binding.ModelInspectionHandoffId,
                plan.Binding.ModelSha256,
                plan.Binding.ModelLengthBytes,
                "88888888888848888888888888888888",
                plan.Binding.HardwareSnapshotSha256);

        Assert.IsFalse(OptimizationSelectionHandoff.TryCreate(
            plan, changedSource, plan.CapabilitySnapshot,
            OptimizationPreferenceSelection.Automatic(), out _));
        Assert.IsFalse(OptimizationSelectionHandoff.TryCreate(
            plan, changedHardware, plan.CapabilitySnapshot,
            OptimizationPreferenceSelection.Automatic(), out _));
        Assert.IsFalse(OptimizationSelectionHandoff.TryCreate(
            plan, changedLength, plan.CapabilitySnapshot,
            OptimizationPreferenceSelection.Automatic(), out _));
        Assert.IsFalse(OptimizationSelectionHandoff.TryCreate(
            plan, changedRun, plan.CapabilitySnapshot,
            OptimizationPreferenceSelection.Automatic(), out _));
        Assert.IsFalse(OptimizationSelectionHandoff.TryCreate(
            plan, changedHandoff, plan.CapabilitySnapshot,
            OptimizationPreferenceSelection.Automatic(), out _));
        Assert.IsFalse(OptimizationSelectionHandoff.TryCreate(
            plan, changedProductHardwareRun, plan.CapabilitySnapshot,
            OptimizationPreferenceSelection.Automatic(), out _));
    }

    [TestMethod]
    public void ChangedCapabilityRoute_RequiresNewPlan()
    {
        OptimizationExecutionPlan plan =
            Plan(OptimizationPreferenceSelection.Automatic());
        OptimizationCapabilitySnapshot openVino = OpenVinoPlan(
            OptimizationPreferenceSelection.Automatic()).CapabilitySnapshot;

        Assert.IsFalse(OptimizationSelectionHandoff.TryCreate(
            plan,
            plan.Binding,
            openVino,
            OptimizationPreferenceSelection.Automatic(),
            out _));
    }

    [TestMethod]
    public void InvalidPlanIdentityOrDigest_FailsClosed()
    {
        OptimizationPreferenceSelection preference =
            OptimizationPreferenceSelection.Automatic();
        OptimizationExecutionPlan emptyId = Plan(
            preference,
            planId: Guid.Empty);
        OptimizationExecutionPlan malformedDigest = Plan(
            preference,
            configurationSha256: "not-a-sha256");
        OptimizationExecutionPlan routeMismatch = Plan(
            preference,
            selectedCandidate: OpenVinoPlan(preference).Candidate);

        Assert.IsFalse(OptimizationSelectionHandoff.TryCreate(
            emptyId, emptyId.Binding, emptyId.CapabilitySnapshot,
            preference, out _));
        Assert.IsFalse(OptimizationSelectionHandoff.TryCreate(
            malformedDigest, malformedDigest.Binding,
            malformedDigest.CapabilitySnapshot, preference, out _));
        Assert.IsFalse(OptimizationSelectionHandoff.TryCreate(
            routeMismatch, routeMismatch.Binding,
            routeMismatch.CapabilitySnapshot, preference, out _));
    }

    [TestMethod]
    public void PublicHandoffShapeContainsOnlySixImmutablePathFreeProperties()
    {
        PropertyInfo[] properties = typeof(OptimizationSelectionHandoff)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public);
        string[] names = properties
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "ConfigurationSha256",
                "ModelInspectionHandoffId",
                "ModelInspectionRunId",
                "OptimizationPlanId",
                "Plan",
                "ProductHardwareRunId"
            },
            names);
        Assert.IsTrue(properties.All(property => !property.CanWrite));
        Assert.IsTrue(names.All(name =>
            !name.Contains("Path", StringComparison.OrdinalIgnoreCase)
            && !name.Contains("FileName", StringComparison.OrdinalIgnoreCase)
            && !name.Contains("Selection", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task MissingRealDestination_RemainsVisibleDisabledAndCannotNavigate()
    {
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(ActionableOptimizationScreen()),
            continueDestinationAvailable: false);
        int navigationRequests = 0;
        viewModel.ContinueRequested += (_, _) => navigationRequests++;

        await viewModel.StartAsync();
        viewModel.ContinueCommand.Execute(null);

        Assert.AreEqual("Coming later", viewModel.Presentation.PrimaryActionText);
        Assert.IsFalse(viewModel.Presentation.PrimaryActionEnabled);
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null));
        Assert.AreEqual(0, navigationRequests);
    }

    private static OptimizationExecutionPlan OpenVinoPlan(
        OptimizationPreferenceSelection preference)
    {
        OpenVinoAdmittedConfiguration admitted =
            OpenVinoAdmittedConfiguration.Create(
                "ov-int8",
                DeviceRouteId.Cpu,
                OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.U8,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled,
                1,
                512,
                32768,
                SupportLevel.DeclaredSupported,
                false);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-capability",
                Digest,
                OpenVinoCapabilityPayload.Create("2026.3.0", [admitted]));
        OptimizationCandidate candidate = OptimizationCandidate.Create(
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.U8,
                DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled,
                1),
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                4096,
                2UL * 1024 * 1024 * 1024,
                4UL * 1024 * 1024 * 1024,
                2UL * 1024 * 1024 * 1024,
                0,
                0,
                requiresPersistentChange: true),
            "ov-int8",
            isExperimental: false);
        OpenVinoBuildIdentity build = OpenVinoBuildIdentity.Create(
            "2026.3.0", "2026.3.0.0", "2026.3.0", Digest);
        OptimizationExecutionPayload payload =
            OptimizationExecutionPayload.ForOpenVino(
                OpenVinoExecutionPayload.Create(
                    "openvino.cpu.int8",
                    "CPU",
                    "Released",
                    "ov-int8",
                    OpenVinoWeightPrecision.Fp16,
                    OpenVinoWeightPrecision.EightBit,
                    OpenVinoKvCachePrecision.U8,
                    false,
                    true,
                    false,
                    true,
                    build,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["openvino"] = "2026.3.0"
                    }));
        return Plan(
            preference,
            snapshot: snapshot,
            selectedCandidate: candidate,
            executionPayload: payload,
            configurationSha256: OtherDigest);
    }

    private static OptimizationExecutionPlan Plan(
        OptimizationPreferenceSelection preference,
        OptimizationJourneyBinding? binding = null,
        OptimizationCapabilitySnapshot? snapshot = null,
        OptimizationCandidate? selectedCandidate = null,
        OptimizationExecutionPayload? executionPayload = null,
        Guid? planId = null,
        string configurationSha256 = Digest,
        bool sharedWithAdjacentBand = false)
    {
        binding ??= OptimizationJourneyBinding.Create(
            "22222222222242228222222222222222",
            "33333333333343338333333333333333",
            Digest,
            2UL * 1024 * 1024 * 1024,
            "44444444444444448444444444444444",
            Digest);
        snapshot ??= Snapshot(Digest);
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "chat", 512, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        OptimizationCandidate candidate = selectedCandidate ?? OptimizationCandidate.Create(
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None),
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                4096,
                3UL * 1024 * 1024 * 1024,
                4UL * 1024 * 1024 * 1024,
                1UL * 1024 * 1024 * 1024,
                0,
                0,
                requiresPersistentChange: false),
            "gguf-q4",
            isExperimental: false);
        OptimizationExecutionPayload payload = executionPayload ?? OptimizationExecutionPayload.ForGguf(
            GgufExecutionPayload.Create(
                "runtime", Commit, GgufRuntimeBackend.Cpu, "CPU", 4096,
                GgufCacheType.F16, GgufCacheType.F16, 0, false, 4, 128,
                "Estimated", "profile", 256, GgufWeightFormat.Imported));
        ConstructorInfo constructor = typeof(OptimizationExecutionPlan)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 11);
        return (OptimizationExecutionPlan)constructor.Invoke(
        [
            OptimizationExecutionPlan.CurrentContractVersion,
            planId ?? Guid.Parse("55555555-5555-4555-8555-555555555555"),
            binding,
            snapshot,
            workload,
            candidate,
            payload,
            preference,
            sharedWithAdjacentBand,
            configurationSha256,
            DateTimeOffset.UnixEpoch
        ]);
    }

    private static OptimizationCapabilitySnapshot Snapshot(
        string digest,
        string snapshotId = "gguf-capability")
    {
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            "gguf-q4", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 32768,
            SupportLevel.DeclaredSupported, false);
        return OptimizationCapabilitySnapshot.ForGguf(
            snapshotId, digest,
            GgufCapabilityPayload.Create("runtime", [admitted]));
    }

    private static CompatibilityScreenModel ActionableOptimizationScreen(
        bool sharedWithAdjacentBand = false)
    {
        CompatibilityOptimizationModeView[] modes =
        [
            Mode(CompatibilityOptimizationLabelCode.Automatic, null,
                sharedWithAdjacentBand),
            Mode(CompatibilityOptimizationLabelCode.MaximumEfficiency, 10,
                sharedWithAdjacentBand),
            Mode(CompatibilityOptimizationLabelCode.Efficient, 30,
                sharedWithAdjacentBand),
            Mode(CompatibilityOptimizationLabelCode.Balanced, 50,
                sharedWithAdjacentBand),
            Mode(CompatibilityOptimizationLabelCode.HighCapability, 70,
                sharedWithAdjacentBand),
            Mode(CompatibilityOptimizationLabelCode.MaximumCapability, 90,
                sharedWithAdjacentBand)
        ];
        CompatibilityOptimizationView optimization =
            CompatibilityOptimizationView.ForPresentation(
                CompatibilityOptimizationLabelCode.Automatic,
                null,
                modes,
                requiresPersistentArtifact: false,
                requiresRequantisationAcknowledgement: false,
                OptimizationQualityNotice.None);
        CompatibilitySetupView setup = CompatibilitySetupView.ForPresentation(
            RuntimeRouteId.LlamaCpp,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            WeightQuantisation.Q4_K_M,
            4096,
            CompatibilityFitState.DoesNotFit,
            5UL * 1024 * 1024 * 1024,
            4UL * 1024 * 1024 * 1024,
            0,
            256UL * 1024 * 1024,
            false,
            false,
            [],
            ggufKvCache: GgufKvCacheFormat.F16);
        ConstructorInfo constructor = typeof(CompatibilityScreenModel)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 9);
        return (CompatibilityScreenModel)constructor.Invoke(
        [
            CompatibilityScreenState.OptimisationRequired,
            Array.Empty<CompatibilityFindingView>(),
            Array.Empty<CompatibilityModeView>(),
            BaselineExclusionReason.None,
            false,
            true,
            setup,
            optimization,
            true
        ]);
    }

    private static CompatibilityOptimizationModeView Mode(
        CompatibilityOptimizationLabelCode label,
        int? slider,
        bool sharedWithAdjacentBand = false) =>
        CompatibilityOptimizationModeView.ForPresentation(
            label,
            slider,
            OptimizationRoute.Gguf,
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16,
            null,
            null,
            DeviceRouteId.Cpu,
            OptimizationAssessment.Good,
            4096,
            3UL * 1024 * 1024 * 1024,
            4UL * 1024 * 1024 * 1024,
            1UL * 1024 * 1024 * 1024,
            false,
            false,
            OptimizationQualityNotice.None,
            false,
            sharedWithAdjacentBand);
}
