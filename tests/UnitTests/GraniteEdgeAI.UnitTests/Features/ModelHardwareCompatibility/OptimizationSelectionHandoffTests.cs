using System.Reflection;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.ViewModels;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
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
    }

    [TestMethod]
    public async Task AvailableDestination_RaisesExactlyOneTypedEventForIssuedSelection()
    {
        OptimizationExecutionPlan plan = Plan(OptimizationPreferenceSelection.Manual(50));
        OptimizationSelectionHandoff.TryCreate(
            plan, plan.Binding, plan.CapabilitySnapshot,
            OptimizationPreferenceSelection.Manual(50), out OptimizationSelectionHandoff? issued);
        var destination = OptimizationDestination.Available(
            (OptimizationPreferenceSelection preference,
                out OptimizationSelectionHandoff? handoff) =>
            {
                handoff = preference == plan.Preference ? issued : null;
                return handoff is not null;
            });
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(ActionableOptimizationScreen()),
            continueDestinationAvailable: true,
            memoryRecovery: null,
            destination);
        int events = 0;
        OptimizationSelectionHandoff? observed = null;
        viewModel.OptimizationRequested += (_, handoff) =>
        {
            events++;
            observed = handoff;
        };

        await viewModel.StartAsync();
        viewModel.SelectManualPreference(50);
        viewModel.ContinueCommand.Execute(null);

        Assert.AreEqual(1, events);
        Assert.AreSame(issued, observed);
    }

    [TestMethod]
    public async Task MissingDestination_IsVisibleDisabledAndCannotRaiseEvent()
    {
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(ActionableOptimizationScreen()),
            continueDestinationAvailable: true);
        int events = 0;
        viewModel.OptimizationRequested += (_, _) => events++;

        await viewModel.StartAsync();

        Assert.AreEqual("Coming later", viewModel.Presentation.PrimaryActionText);
        Assert.IsFalse(viewModel.Presentation.PrimaryActionEnabled);
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null));
        viewModel.ContinueCommand.Execute(null);
        Assert.AreEqual(0, events);
    }

    [TestMethod]
    public async Task IssuerReturningDifferentSelection_FailsClosedWithoutEvent()
    {
        OptimizationExecutionPlan plan = Plan(OptimizationPreferenceSelection.Manual(50));
        OptimizationSelectionHandoff.TryCreate(
            plan, plan.Binding, plan.CapabilitySnapshot,
            OptimizationPreferenceSelection.Manual(50), out OptimizationSelectionHandoff? issued);
        var destination = OptimizationDestination.Available(
            (OptimizationPreferenceSelection _, out OptimizationSelectionHandoff? handoff) =>
            {
                handoff = issued;
                return true;
            });
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(ActionableOptimizationScreen()),
            true, null, destination);
        int events = 0;
        viewModel.OptimizationRequested += (_, _) => events++;

        await viewModel.StartAsync();
        viewModel.SelectManualPreference(70);
        viewModel.ContinueCommand.Execute(null);

        Assert.AreEqual(0, events);
    }

    [TestMethod]
    public async Task ThrowingIssuer_FailsClosedWithoutEscapingTheUiCommand()
    {
        var destination = OptimizationDestination.Available(
            (OptimizationPreferenceSelection _, out OptimizationSelectionHandoff? handoff) =>
            {
                handoff = null;
                throw new InvalidOperationException("private adapter failure");
            });
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(ActionableOptimizationScreen()),
            true, null, destination);
        int events = 0;
        viewModel.OptimizationRequested += (_, _) => events++;
        await viewModel.StartAsync();

        viewModel.ContinueCommand.Execute(null);

        Assert.AreEqual(0, events);
    }

    [TestMethod]
    public async Task SelectionChangedDuringIssuance_DropsTheNowStaleHandoff()
    {
        OptimizationExecutionPlan plan = Plan(OptimizationPreferenceSelection.Manual(50));
        OptimizationSelectionHandoff.TryCreate(
            plan, plan.Binding, plan.CapabilitySnapshot,
            OptimizationPreferenceSelection.Manual(50), out OptimizationSelectionHandoff? issued);
        CompatibilityViewModel? viewModel = null;
        var destination = OptimizationDestination.Available(
            (OptimizationPreferenceSelection _, out OptimizationSelectionHandoff? handoff) =>
            {
                viewModel!.SelectManualPreference(70);
                handoff = issued;
                return true;
            });
        viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(ActionableOptimizationScreen()),
            true, null, destination);
        int events = 0;
        viewModel.OptimizationRequested += (_, _) => events++;
        await viewModel.StartAsync();
        viewModel.SelectManualPreference(50);

        viewModel.ContinueCommand.Execute(null);

        Assert.AreEqual(0, events);
        Assert.AreEqual(70, viewModel.SelectedPreference!.PreferenceValue);
    }

    [TestMethod]
    public void PublicHandoffShapeContainsNoPathOrMutableSelectionProperty()
    {
        string[] properties = typeof(OptimizationSelectionHandoff)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
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
            properties);
        Assert.IsTrue(typeof(OptimizationSelectionHandoff)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .All(property => !property.CanWrite));
    }

    private static OptimizationExecutionPlan Plan(
        OptimizationPreferenceSelection preference)
    {
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "22222222222242228222222222222222",
            "33333333333343338333333333333333",
            Digest,
            2UL * 1024 * 1024 * 1024,
            "44444444444444448444444444444444",
            Digest);
        OptimizationCapabilitySnapshot snapshot = Snapshot(Digest);
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "chat", 512, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        OptimizationCandidate candidate = OptimizationCandidate.Create(
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
        OptimizationExecutionPayload payload = OptimizationExecutionPayload.ForGguf(
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
            Guid.Parse("55555555-5555-4555-8555-555555555555"),
            binding,
            snapshot,
            workload,
            candidate,
            payload,
            preference,
            false,
            Digest,
            DateTimeOffset.UnixEpoch
        ]);
    }

    private static OptimizationCapabilitySnapshot Snapshot(string digest)
    {
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            "gguf-q4", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 32768,
            SupportLevel.DeclaredSupported, false);
        return OptimizationCapabilitySnapshot.ForGguf(
            "gguf-capability", digest,
            GgufCapabilityPayload.Create("runtime", [admitted]));
    }

    private static CompatibilityScreenModel ActionableOptimizationScreen()
    {
        CompatibilityOptimizationModeView[] modes =
        [
            Mode(CompatibilityOptimizationLabelCode.Automatic, null),
            Mode(CompatibilityOptimizationLabelCode.MaximumEfficiency, 10),
            Mode(CompatibilityOptimizationLabelCode.Efficient, 30),
            Mode(CompatibilityOptimizationLabelCode.Balanced, 50),
            Mode(CompatibilityOptimizationLabelCode.HighCapability, 70),
            Mode(CompatibilityOptimizationLabelCode.MaximumCapability, 90)
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
        int? slider) => CompatibilityOptimizationModeView.ForPresentation(
            label,
            slider,
            OptimizationRoute.Gguf,
            GgufWeightFormat.Q4KM,
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
            false);
}
