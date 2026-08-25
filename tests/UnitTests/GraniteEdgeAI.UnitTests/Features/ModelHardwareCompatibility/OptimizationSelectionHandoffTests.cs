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
        OptimizationExecutionPlan plan = Plan(OptimizationPreferenceSelection.Automatic());
        OptimizationSelectionHandoff.TryCreate(
            plan, plan.Binding, plan.CapabilitySnapshot,
            OptimizationPreferenceSelection.Automatic(), out OptimizationSelectionHandoff? issued);
        OptimizationHandoffAuthority authority = Authority(plan);
        var destination = OptimizationDestination.Available(
            authority,
            () => authority,
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
    public async Task ChangedSelection_InvalidatesAuthorityAndDisablesIssuance()
    {
        OptimizationExecutionPlan plan = Plan(OptimizationPreferenceSelection.Automatic());
        OptimizationSelectionHandoff.TryCreate(
            plan, plan.Binding, plan.CapabilitySnapshot,
            OptimizationPreferenceSelection.Automatic(), out OptimizationSelectionHandoff? issued);
        OptimizationHandoffAuthority authority = Authority(plan);
        var destination = OptimizationDestination.Available(
            authority,
            () => authority,
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
        Assert.IsFalse(viewModel.ContinueCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task ThrowingIssuer_FailsClosedWithoutEscapingTheUiCommand()
    {
        OptimizationExecutionPlan plan = Plan(OptimizationPreferenceSelection.Automatic());
        OptimizationSelectionHandoff.TryCreate(
            plan, plan.Binding, plan.CapabilitySnapshot,
            OptimizationPreferenceSelection.Automatic(), out OptimizationSelectionHandoff? issued);
        OptimizationHandoffAuthority authority = Authority(plan);
        var destination = OptimizationDestination.Available(
            authority,
            () => authority,
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
        OptimizationExecutionPlan plan = Plan(OptimizationPreferenceSelection.Automatic());
        OptimizationSelectionHandoff.TryCreate(
            plan, plan.Binding, plan.CapabilitySnapshot,
            OptimizationPreferenceSelection.Automatic(), out OptimizationSelectionHandoff? issued);
        OptimizationHandoffAuthority authority = Authority(plan);
        CompatibilityViewModel? viewModel = null;
        var destination = OptimizationDestination.Available(
            authority,
            () => authority,
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
        viewModel.ContinueCommand.Execute(null);

        Assert.AreEqual(0, events);
        Assert.AreEqual(70, viewModel.SelectedPreference!.PreferenceValue);
    }

    [TestMethod]
    public async Task SamePreferenceHandoffFromAnotherAuthority_IsRejectedForEveryBindingAxis()
    {
        OptimizationPreferenceSelection preference =
            OptimizationPreferenceSelection.Automatic();
        OptimizationExecutionPlan expectedPlan = Plan(preference);
        OptimizationHandoffAuthority expectedAuthority = Authority(expectedPlan);
        OptimizationExecutionPlan[] substitutions =
        [
            Plan(preference, binding: Binding(modelRunId: "66666666666646668666666666666666")),
            Plan(preference, binding: Binding(modelHandoffId: "77777777777747778777777777777777")),
            Plan(preference, binding: Binding(productHardwareRunId: "88888888888848888888888888888888")),
            Plan(preference, binding: Binding(hardwareSha256: OtherDigest)),
            Plan(preference, binding: Binding(modelSha256: OtherDigest)),
            Plan(preference, binding: Binding(modelLengthBytes: (2UL * 1024 * 1024 * 1024) + 1)),
            Plan(preference, snapshot: Snapshot(OtherDigest)),
            Plan(preference, selectedCandidate: AlternativeCandidate()),
            Plan(preference, planId: Guid.Parse("99999999-9999-4999-8999-999999999999")),
            Plan(preference, configurationSha256: OtherDigest),
            OpenVinoPlan(preference)
        ];

        foreach (OptimizationExecutionPlan substitution in substitutions)
        {
            OptimizationSelectionHandoff returned = Handoff(substitution);
            var destination = OptimizationDestination.Available(
                expectedAuthority,
                () => expectedAuthority,
                (OptimizationPreferenceSelection _, out OptimizationSelectionHandoff? handoff) =>
                {
                    handoff = returned;
                    return true;
                });
            var viewModel = new CompatibilityViewModel(
                _ => Task.FromResult(ActionableOptimizationScreen()),
                true, null, destination);
            int events = 0;
            viewModel.OptimizationRequested += (_, _) => events++;
            await viewModel.StartAsync();
            viewModel.ContinueCommand.Execute(null);

            Assert.AreEqual(0, events, substitution.ConfigurationSha256);
        }
    }

    [TestMethod]
    public async Task FreshAuthorityDriftAfterCachedHandoff_FailsClosed()
    {
        OptimizationExecutionPlan expectedPlan =
            Plan(OptimizationPreferenceSelection.Automatic());
        OptimizationSelectionHandoff cached = Handoff(expectedPlan);
        OptimizationHandoffAuthority expectedAuthority = Authority(expectedPlan);
        OptimizationHandoffAuthority currentAuthority = expectedAuthority;
        OptimizationExecutionPlan driftedPlan = Plan(
            OptimizationPreferenceSelection.Automatic(),
            binding: Binding(hardwareSha256: OtherDigest));
        var destination = OptimizationDestination.Available(
            expectedAuthority,
            () => currentAuthority,
            (OptimizationPreferenceSelection _, out OptimizationSelectionHandoff? handoff) =>
            {
                currentAuthority = Authority(driftedPlan);
                handoff = cached;
                return true;
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
    public async Task ReentrantAbaSelectionChange_CannotRestoreStaleAcceptance()
    {
        OptimizationExecutionPlan plan =
            Plan(OptimizationPreferenceSelection.Automatic());
        OptimizationSelectionHandoff issued = Handoff(plan);
        OptimizationHandoffAuthority authority = Authority(plan);
        CompatibilityViewModel? viewModel = null;
        var destination = OptimizationDestination.Available(
            authority,
            () => authority,
            (OptimizationPreferenceSelection _, out OptimizationSelectionHandoff? handoff) =>
            {
                viewModel!.SelectManualPreference(70);
                viewModel.SelectAutomaticPreference();
                handoff = issued;
                return true;
            });
        viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(ActionableOptimizationScreen()),
            true, null, destination);
        int events = 0;
        viewModel.OptimizationRequested += (_, _) => events++;
        await viewModel.StartAsync();
        viewModel.ContinueCommand.Execute(null);

        Assert.AreEqual(0, events);
    }

    [TestMethod]
    public async Task ReentrantAndSequentialExecute_ConsumeOneExactIssuanceOnly()
    {
        OptimizationExecutionPlan plan =
            Plan(OptimizationPreferenceSelection.Automatic());
        OptimizationSelectionHandoff issued = Handoff(plan);
        OptimizationHandoffAuthority authority = Authority(plan);
        CompatibilityViewModel? viewModel = null;
        bool reentered = false;
        var destination = OptimizationDestination.Available(
            authority,
            () => authority,
            (OptimizationPreferenceSelection _, out OptimizationSelectionHandoff? handoff) =>
            {
                if (!reentered)
                {
                    reentered = true;
                    viewModel!.ContinueCommand.Execute(null);
                }

                handoff = issued;
                return true;
            });
        viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(ActionableOptimizationScreen()),
            true, null, destination);
        int events = 0;
        viewModel.OptimizationRequested += (_, _) => events++;
        await viewModel.StartAsync();
        viewModel.ContinueCommand.Execute(null);
        viewModel.ContinueCommand.Execute(null);

        Assert.AreEqual(1, events);
    }

    [TestMethod]
    public async Task ThrowingSubscriber_IsContainedWithoutReEmittingAmbiguousHandoff()
    {
        OptimizationExecutionPlan plan =
            Plan(OptimizationPreferenceSelection.Automatic());
        OptimizationSelectionHandoff issued = Handoff(plan);
        OptimizationHandoffAuthority authority = Authority(plan);
        var destination = OptimizationDestination.Available(
            authority,
            () => authority,
            (OptimizationPreferenceSelection _, out OptimizationSelectionHandoff? handoff) =>
            {
                handoff = issued;
                return true;
            });
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(ActionableOptimizationScreen()),
            true, null, destination);
        int acceptedActions = 0;
        int throwingDispatches = 0;
        viewModel.OptimizationRequested += (_, _) => acceptedActions++;
        viewModel.OptimizationRequested += (_, _) =>
        {
            throwingDispatches++;
            throw new InvalidOperationException("navigation failed");
        };
        await viewModel.StartAsync();
        viewModel.ContinueCommand.Execute(null);
        viewModel.ContinueCommand.Execute(null);

        Assert.AreEqual(1, acceptedActions);
        Assert.AreEqual(1, throwingDispatches);
        Assert.AreEqual(
            CompatibilityAuxiliaryStatusKind.Error,
            viewModel.AuxiliaryStatus.Kind);
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

    private static OptimizationSelectionHandoff Handoff(
        OptimizationExecutionPlan plan)
    {
        Assert.IsTrue(OptimizationSelectionHandoff.TryCreate(
            plan,
            plan.Binding,
            plan.CapabilitySnapshot,
            plan.Preference,
            out OptimizationSelectionHandoff? handoff));
        return handoff!;
    }

    private static OptimizationHandoffAuthority Authority(
        OptimizationExecutionPlan plan)
    {
        Assert.IsTrue(OptimizationHandoffAuthority.TryCreate(
            plan,
            plan.Binding,
            plan.CapabilitySnapshot,
            plan.Preference,
            out OptimizationHandoffAuthority? authority));
        return authority!;
    }

    private static OptimizationJourneyBinding Binding(
        string modelRunId = "22222222222242228222222222222222",
        string modelHandoffId = "33333333333343338333333333333333",
        string modelSha256 = Digest,
        ulong modelLengthBytes = 2UL * 1024 * 1024 * 1024,
        string productHardwareRunId = "44444444444444448444444444444444",
        string hardwareSha256 = Digest) =>
        OptimizationJourneyBinding.Create(
            modelRunId,
            modelHandoffId,
            modelSha256,
            modelLengthBytes,
            productHardwareRunId,
            hardwareSha256);

    private static OptimizationCandidate AlternativeCandidate() =>
        OptimizationCandidate.Create(
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Q3KM,
                GgufKvCacheFormat.Q8_0,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None),
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated,
                OptimizationAssessment.Acceptable,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                4096,
                2UL * 1024 * 1024 * 1024,
                4UL * 1024 * 1024 * 1024,
                2UL * 1024 * 1024 * 1024,
                0,
                0,
                requiresPersistentChange: true),
            "gguf-q3",
            isExperimental: false);

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
        string configurationSha256 = Digest)
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
            false,
            configurationSha256,
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
