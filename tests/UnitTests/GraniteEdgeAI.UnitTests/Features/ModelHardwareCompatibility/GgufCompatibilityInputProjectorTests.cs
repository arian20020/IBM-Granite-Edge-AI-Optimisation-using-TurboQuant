using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
public sealed class GgufCompatibilityInputProjectorTests
{
    private static readonly Guid ModelRunId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid HardwareRunId =
        Guid.Parse("33333333-3333-4333-8333-333333333333");

    [TestMethod]
    public void EligiblePairedEvidence_ProjectsExactCalculationFacts()
    {
        ModelInspectionExecutionResult terminal = Terminal();
        ModelInspectionHandoff model = ModelHandoff(terminal);
        HardwareInspectionHandoff hardware = HardwareHandoff();
        AvailableMemorySnapshot fresh = new(
            21UL * 1024 * 1024 * 1024,
            DateTimeOffset.UtcNow);

        bool projected = GgufCompatibilityInputProjector.TryProject(
            model,
            terminal,
            HardwareRunId,
            hardware,
            fresh,
            out CompatibilityProductionInput? input);

        Assert.IsTrue(projected);
        Assert.IsNotNull(input);
        Assert.AreEqual(ModelRunId, input.ModelInspectionRunId);
        Assert.AreEqual(HardwareRunId, input.ProductHardwareRunId);
        Assert.AreEqual(4_096UL, input.Model.FileLengthBytes);
        Assert.AreEqual(24, input.Model.LayerCount);
        Assert.AreEqual(2_048, input.Model.EmbeddingSize);
        Assert.AreEqual(16, input.Model.AttentionHeadCount);
        Assert.AreEqual(8, input.Model.KeyValueHeadCount);
        Assert.AreEqual(4_096, input.Model.DeclaredContextLimit);
        Assert.AreEqual(15, input.Model.FileType);
        Assert.AreEqual(2, input.Model.QuantisationVersion);
        Assert.AreEqual(3_000_000_000UL, input.Model.ParameterCount);
        Assert.AreEqual(32UL * 1024 * 1024 * 1024, input.Hardware.InstalledSystemMemoryBytes);
        Assert.AreEqual(8UL * 1024 * 1024 * 1024, input.Hardware.InstalledDedicatedDeviceMemoryBytes);
        Assert.AreEqual(500_000_000_000UL, input.Hardware.FreeStorageBytes);
        CollectionAssert.Contains(input.Hardware.PresentDevices.ToArray(), DeviceRouteId.Cpu);
        CollectionAssert.Contains(input.Hardware.PresentDevices.ToArray(), DeviceRouteId.IntelIntegratedGpu);
        CollectionAssert.Contains(input.Hardware.PresentDevices.ToArray(), DeviceRouteId.IntelDiscreteGpu);
        CollectionAssert.Contains(input.Hardware.PresentDevices.ToArray(), DeviceRouteId.IntelNpu);
        CollectionAssert.AreEquivalent(
            new[] { CompatibilityBackend.Cpu, CompatibilityBackend.IntelSycl },
            input.Hardware.VerifiedBackends.ToArray());
        Assert.AreEqual(fresh.AvailablePhysicalBytes, input.FreshResources.AvailableSystemMemoryBytes);
        Assert.AreEqual(0UL, input.FreshResources.AvailableDedicatedDeviceMemoryBytes);
    }

    [TestMethod]
    public void FreshAvailabilityAboveEarlierHardwareFactsIsConservativelyBound()
    {
        ModelInspectionExecutionResult terminal = Terminal();
        Assert.IsTrue(GgufCompatibilityInputProjector.TryPrepare(
            ModelHandoff(terminal),
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            out PreparedGgufCompatibilityInput? prepared));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityFreshResourcesInput fresh = CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(
                prepared!.Hardware.InstalledSystemMemoryBytes + 1),
            prepared.Hardware.InstalledDedicatedDeviceMemoryBytes + 1,
            prepared.Hardware.FreeStorageBytes + 1,
            now);

        Assert.IsTrue(prepared.TryBindFresh(
            fresh,
            out CompatibilityProductionInput? input));
        Assert.IsNotNull(input);
        Assert.AreEqual(
            prepared.Hardware.InstalledSystemMemoryBytes,
            input.FreshResources.AvailableSystemMemoryBytes);
        Assert.AreEqual(
            prepared.Hardware.InstalledDedicatedDeviceMemoryBytes,
            input.FreshResources.AvailableDedicatedDeviceMemoryBytes);
        Assert.AreEqual(
            prepared.Hardware.FreeStorageBytes,
            input.FreshResources.AvailableStorageBytes);
    }

    [TestMethod]
    public void MissingOptionalModelFacts_RemainAbsent()
    {
        ModelInspectionEvidence evidence = PresentationTestData.CreateEvidence(layerCount: null);
        ModelInspectionExecutionResult terminal = ModelInspectionExecutionResult.Completed(
            PresentationTestData.CreateResult(ModelInspectionOutcome.Ready, evidence: evidence));
        ModelInspectionHandoff model = ModelHandoff(terminal);

        Assert.IsTrue(GgufCompatibilityInputProjector.TryProject(
            model,
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            new AvailableMemorySnapshot(1, DateTimeOffset.UtcNow),
            out CompatibilityProductionInput? input));

        Assert.IsNull(input!.Model.LayerCount);
    }

    [TestMethod]
    public void ExactPinnedDownloads_MissingKvHeads_ProjectVerifiedValue()
    {
        foreach (ModelDownloadCatalogEntry entry in PinnedGraniteModelCatalog.Entries)
        {
            ModelInspectionExecutionResult terminal = PinnedDownloadTerminal(
                entry,
                kvHeadCount: null);

            Assert.IsTrue(GgufCompatibilityInputProjector.TryPrepare(
                ModelHandoff(terminal),
                terminal,
                HardwareRunId,
                HardwareHandoff(),
                out PreparedGgufCompatibilityInput? prepared),
                entry.Id);

            Assert.AreEqual(8, prepared!.Model.KeyValueHeadCount, entry.Id);
        }
    }

    [TestMethod]
    public void ExactPinnedDownload_PresentKvHeadsRemainAuthoritative()
    {
        ModelDownloadCatalogEntry entry = PinnedGraniteModelCatalog.ForPreferenceBand(
            OptimizationPreferenceBand.Balanced);
        ModelInspectionExecutionResult terminal = PinnedDownloadTerminal(
            entry,
            kvHeadCount: 4);

        Assert.IsTrue(GgufCompatibilityInputProjector.TryPrepare(
            ModelHandoff(terminal),
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            out PreparedGgufCompatibilityInput? prepared));

        Assert.AreEqual(4, prepared!.Model.KeyValueHeadCount);
    }

    [TestMethod]
    public void PartialOrUnknownPinnedIdentity_MissingKvHeadsRemainAbsent()
    {
        ModelDownloadCatalogEntry entry = PinnedGraniteModelCatalog.ForPreferenceBand(
            OptimizationPreferenceBand.Balanced);
        ModelInspectionExecutionResult wrongLength = PinnedDownloadTerminal(
            entry,
            kvHeadCount: null,
            lengthBytes: entry.ExpectedByteLength + 1);
        ModelInspectionExecutionResult wrongDigest = PinnedDownloadTerminal(
            entry,
            kvHeadCount: null,
            modelSha256: new string('a', 64));
        ModelInspectionExecutionResult arbitrary = PinnedDownloadTerminal(
            entry,
            kvHeadCount: null,
            lengthBytes: 4_096,
            modelSha256: PresentationTestData.Sha256);

        foreach (ModelInspectionExecutionResult terminal in
            new[] { wrongLength, wrongDigest, arbitrary })
        {
            Assert.IsTrue(GgufCompatibilityInputProjector.TryPrepare(
                ModelHandoff(terminal),
                terminal,
                HardwareRunId,
                HardwareHandoff(),
                out PreparedGgufCompatibilityInput? prepared));
            Assert.IsNull(prepared!.Model.KeyValueHeadCount);
        }
    }

    [TestMethod]
    public void ExactPinnedQ4_MissingKvHeadsReachesEstimableCompatibility()
    {
        ModelDownloadCatalogEntry entry = PinnedGraniteModelCatalog.ForPreferenceBand(
            OptimizationPreferenceBand.Balanced);
        ModelInspectionExecutionResult terminal = PinnedDownloadTerminal(
            entry,
            kvHeadCount: null);
        Assert.IsTrue(GgufCompatibilityInputProjector.TryPrepare(
            ModelHandoff(terminal),
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            out PreparedGgufCompatibilityInput? prepared));
        Assert.IsTrue(GgufOptimizationProductionAuthority.TryCreate(
            prepared!,
            out GgufOptimizationProductionAuthority? authority));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityEvaluation evaluation = authority!.Evaluate(
            CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(21UL * 1024 * 1024 * 1024),
                0,
                500_000_000_000UL,
                now),
            new HashSet<string>(),
            now,
            CancellationToken.None);

        Assert.IsFalse(evaluation.Screen.Findings.Any(static finding =>
            finding.Code == CompatibilityFindingCode.NoCandidateCouldBeEstimated));
    }

    [TestMethod]
    public void ExactPinnedQ3_ProductionWorkerShapeReachesEstimableCompatibility()
    {
        ModelDownloadCatalogEntry entry = PinnedGraniteModelCatalog.ForPreferenceBand(
            OptimizationPreferenceBand.Efficient);
        ModelInspectionExecutionResult terminal = PinnedDownloadTerminal(
            entry,
            kvHeadCount: null,
            fileType: 12,
            declaredContextLength: 1_048_576,
            parameterCount: null);
        Assert.IsTrue(GgufCompatibilityInputProjector.TryPrepare(
            ModelHandoff(terminal),
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            out PreparedGgufCompatibilityInput? prepared));
        Assert.AreEqual(8, prepared!.Model.KeyValueHeadCount);
        Assert.IsTrue(GgufOptimizationProductionAuthority.TryCreate(
            prepared,
            out GgufOptimizationProductionAuthority? authority));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityEvaluation evaluation = authority!.Evaluate(
            CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(21UL * 1024 * 1024 * 1024),
                0,
                500_000_000_000UL,
                now),
            new HashSet<string>(),
            now,
            CancellationToken.None);

        Assert.AreEqual(CompatibilityScreenState.EstimatedCompatible,
            evaluation.Screen.State);
    }

    [TestMethod]
    public void ExactRetainedQ4Header_ResolvesTensorDerivedParameterCountWhenMetadataIsAbsent()
    {
        ModelInspectionExecutionResult terminal = ActualHeaderTerminal(parameterCount: null);

        Assert.IsTrue(GgufCompatibilityInputProjector.TryPrepare(
            ModelHandoff(terminal),
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            out PreparedGgufCompatibilityInput? prepared));

        Assert.AreEqual(3_402_836_480UL, prepared!.Model.ParameterCount);
    }

    [TestMethod]
    public void PresentConflictingParameterMetadata_RemainsAuthoritativeAndIsNotReplaced()
    {
        ModelInspectionExecutionResult terminal =
            ActualHeaderTerminal(parameterCount: 3_000_000_000);

        Assert.IsTrue(GgufCompatibilityInputProjector.TryPrepare(
            ModelHandoff(terminal),
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            out PreparedGgufCompatibilityInput? prepared));

        Assert.AreEqual(3_000_000_000UL, prepared!.Model.ParameterCount);
    }

    [TestMethod]
    public void PackagedCurrentReleaseProjectsQ8AndBothVerifiedTurboFormats()
    {
        ModelInspectionExecutionResult terminal = ActualHeaderTerminal(parameterCount: null);
        Assert.IsTrue(GgufCompatibilityInputProjector.TryPrepare(
            ModelHandoff(terminal),
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            out PreparedGgufCompatibilityInput? prepared));
        Assert.IsTrue(GgufOptimizationProductionAuthority.TryCreate(
            prepared!,
            out GgufOptimizationProductionAuthority? authority));

        OptimizationCapabilitySnapshot snapshot = Snapshot(authority!);
        Assert.IsFalse(snapshot.Gguf!.Admitted.Any(static admission =>
            admission.EvidenceId.StartsWith("GGUF-V4-", StringComparison.Ordinal)));
        Assert.IsFalse(snapshot.Gguf.RuntimeAuthority!.Profiles.Keys.Any(static evidenceId =>
            evidenceId.StartsWith("GGUF-V4-", StringComparison.Ordinal)));
        Assert.AreEqual(4, snapshot.Gguf.Admitted.Count(static admission =>
            admission.EvidenceId.StartsWith("GGUF-CURRENT-08EF-", StringComparison.Ordinal)));
        Assert.AreEqual(4, snapshot.Gguf.RuntimeAuthority.Profiles.Keys.Count(static evidenceId =>
            evidenceId.StartsWith("GGUF-CURRENT-08EF-", StringComparison.Ordinal)));
        Assert.IsFalse(snapshot.Gguf.Admitted.Any(static admission =>
            admission.EvidenceId.StartsWith("GGUF-CURRENT-08EF-", StringComparison.Ordinal)
            && (admission.Level != SupportLevel.DeclaredSupported
                || admission.RequiresEvidence)));
        GgufAdmittedConfiguration turbo4Admission = snapshot.Gguf.Admitted.Single(
            static admission =>
                admission.EvidenceId == "GGUF-CURRENT-08EF-CPU-TURBO4-01");
        Assert.AreEqual(GgufKvCacheFormat.TurboQuant4Bit,
            turbo4Admission.KvCache);
        Assert.AreEqual(4, snapshot.Gguf.RuntimeAuthority.Profiles[
            turbo4Admission.EvidenceId].ThreadCount);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityEvaluation evaluation = authority!.Evaluate(
            CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(20UL * 1024 * 1024 * 1024),
                0,
                500_000_000_000UL,
                now),
            new HashSet<string>(),
            now,
            CancellationToken.None);
        OptimizationCandidate[] currentCandidates =
        [
            .. PlanningCandidates(evaluation).Where(static candidate =>
                candidate.EvidenceId.StartsWith("GGUF-CURRENT-08EF-", StringComparison.Ordinal))
        ];
        OptimizationCandidate q8 = currentCandidates.Single(static candidate =>
            candidate.EvidenceId == "GGUF-CURRENT-08EF-CPU-Q8-01");
        OptimizationCandidate turbo4 = currentCandidates.Single(static candidate =>
            candidate.EvidenceId == "GGUF-CURRENT-08EF-CPU-TURBO4-01");
        Assert.IsTrue(currentCandidates.Any(static candidate =>
            candidate.EvidenceId == "GGUF-CURRENT-08EF-CPU-TURBO3-COMPAT-01"));
        Assert.IsFalse(currentCandidates.Any(static candidate => candidate.IsExperimental));
        Assert.IsTrue(authority.TryGetOptimizationAuthority(
            OptimizationRoute.Gguf,
            out IOptimizationExecutionPayloadComposer? composer,
            out OptimizationIssuanceAuthority? issuance));
        CompatibilityOptimizationView optimization =
            evaluation.OptionalOptimization
            ?? throw new AssertFailedException(
                "Current released alternatives were not projected.");
        string q8Identity = optimization.ExactSafeModes
            .Single(static mode =>
                mode.Mode.GgufKvCache == GgufKvCacheFormat.Q8_0)
            .CandidateIdentity;
        OptimizationPreferenceSelection preference =
            OptimizationPreferenceSelection.Exact(q8Identity);
        OptimizationExecutionPlan plan = evaluation.PlanningSession!.Issue(
            preference,
            composer!,
            issuance!,
            TimeProvider.System);
        Assert.AreEqual(q8, plan.Candidate);
        Assert.AreEqual(GgufCacheType.Q8Zero,
            plan.ExecutionPayload.Gguf!.KeyCacheType);
        Assert.AreEqual(GgufCacheType.Q8Zero,
            plan.ExecutionPayload.Gguf.ValueCacheType);
        Assert.IsFalse(plan.ExecutionPayload.Gguf.RequiresPersistentConversion);

        string turbo4Identity = optimization.ExactSafeModes
            .Single(static mode =>
                mode.Mode.GgufKvCache == GgufKvCacheFormat.TurboQuant4Bit)
            .CandidateIdentity;
        OptimizationPreferenceSelection turbo4Preference =
            OptimizationPreferenceSelection.Exact(turbo4Identity);
        OptimizationExecutionPlan turbo4Plan = evaluation.PlanningSession.Issue(
            turbo4Preference,
            composer!,
            issuance!,
            TimeProvider.System);
        Assert.AreEqual(turbo4, turbo4Plan.Candidate);
        Assert.AreEqual(GgufCacheType.Turbo4,
            turbo4Plan.ExecutionPayload.Gguf!.KeyCacheType);
        Assert.AreEqual(GgufCacheType.Turbo4,
            turbo4Plan.ExecutionPayload.Gguf.ValueCacheType);
        Assert.AreEqual(4, turbo4Plan.ExecutionPayload.Gguf.ThreadCount);
        Assert.IsTrue(turbo4Plan.ExecutionPayload.Gguf.FlashAttention);
        Assert.IsFalse(turbo4Plan.ExecutionPayload.Gguf.RequiresPersistentConversion);
    }

    [TestMethod]
    public void MismatchedHardwareRunOrModelLength_FailsClosed()
    {
        ModelInspectionExecutionResult terminal = Terminal();
        ModelInspectionHandoff model = ModelHandoff(terminal);
        ModelInspectionHandoff altered = new(
            model.SchemaVersion,
            model.ModelInspectionHandoffId,
            model.ModelInspectionRunId,
            model.Outcome,
            model.ModelSha256,
            model.ModelLengthBytes + 1);

        Assert.IsFalse(GgufCompatibilityInputProjector.TryProject(
            model,
            terminal,
            Guid.NewGuid(),
            HardwareHandoff(),
            new AvailableMemorySnapshot(1, DateTimeOffset.UtcNow),
            out _));
        Assert.IsFalse(GgufCompatibilityInputProjector.TryProject(
            altered,
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            new AvailableMemorySnapshot(1, DateTimeOffset.UtcNow),
            out _));
    }

    [TestMethod]
    public void StaleFreshMemory_FailsClosed()
    {
        ModelInspectionExecutionResult terminal = Terminal();

        Assert.IsFalse(GgufCompatibilityInputProjector.TryProject(
            ModelHandoff(terminal),
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            new AvailableMemorySnapshot(1, DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1)),
            out _));
    }

    [TestMethod]
    public void ReleaseV5DoesNotProjectHistoricalTurbo3Quality()
    {
        const ulong gib = 1024UL * 1024 * 1024;
        const string granite3bSha256 =
            "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29";
        var prepared = new PreparedGgufCompatibilityInput(
            ModelRunId,
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            granite3bSha256,
            HardwareRunId,
            PresentationTestData.Sha256,
            GgufCompatibilityModelInput.Create(
                3 * gib,
                32,
                4_096,
                32,
                8,
                8_192,
                15,
                2),
            CompatibilityHardwareInput.Create(
                TotalPhysicalMemory.FromBytes(16 * gib),
                0,
                500_000_000_000UL,
                [DeviceRouteId.Cpu],
                [CompatibilityBackend.Cpu]));
        Assert.IsTrue(GgufOptimizationProductionAuthority.TryCreate(
            prepared,
            out GgufOptimizationProductionAuthority? authority));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityEvaluation evaluation = authority!.Evaluate(
            CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(10 * gib),
                0,
                500_000_000_000UL,
                now),
            new HashSet<string>(),
            now,
            CancellationToken.None);

        Assert.IsNull(evaluation.OptionalOptimization);
        Assert.IsFalse(PlanningCandidates(evaluation).Any(static candidate =>
            candidate.EvidenceId == "GGUF-V4-CPU-TURBO3-01"));
    }

    [TestMethod]
    public void AtomicBotProductionAuthority_PublishedCpuBaselineHasOneAdmissionAt4096()
    {
        const ulong gib = 1024UL * 1024 * 1024;
        const string granite3bSha256 =
            "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29";
        var prepared = new PreparedGgufCompatibilityInput(
            ModelRunId,
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            granite3bSha256,
            HardwareRunId,
            PresentationTestData.Sha256,
            GgufCompatibilityModelInput.Create(
                3 * gib, 32, 4_096, 32, 8, 8_192, 15, 2),
            CompatibilityHardwareInput.Create(
                TotalPhysicalMemory.FromBytes(16 * gib),
                0,
                500_000_000_000UL,
                [DeviceRouteId.Cpu],
                [CompatibilityBackend.Cpu]));
        Assert.IsTrue(GgufOptimizationProductionAuthority.TryCreate(
            prepared,
            out GgufOptimizationProductionAuthority? authority));

        OptimizationCapabilitySnapshot snapshot = Snapshot(authority!);
        GgufAdmittedConfiguration[] currentAt4096 =
            CurrentCpuAdmissionsAt(snapshot, 4_096);

        Assert.HasCount(
            1,
            currentAt4096,
            "Published measured coverage must not overlap the generic current CPU admission.");
        Assert.AreEqual("AB-KV3-F16-4K", currentAt4096[0].EvidenceId);
        Assert.IsTrue(snapshot.Gguf!.RuntimeAuthority!.Profiles.ContainsKey(
            currentAt4096[0].EvidenceId));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityEvaluation evaluation = authority!.Evaluate(
            CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(10 * gib),
                0,
                500_000_000_000UL,
                now),
            new HashSet<string>(),
            now,
            CancellationToken.None);
        Assert.AreNotEqual(
            CompatibilityScreenState.NotEstablished,
            evaluation.Screen.State,
            "A unique published current baseline must reach a real engine decision.");
    }

    [DataTestMethod]
    [DataRow(512)]
    [DataRow(2_048)]
    [DataRow(4_096)]
    [DataRow(8_192)]
    [DataRow(32_768)]
    public void AtomicBotProductionAuthority_CurrentLaunchUsesTheEvaluatedDefaultContext(
        int maximumContext)
    {
        GgufOptimizationProductionAuthority authority = CreatePublished3bAuthority(
            maximumContext,
            hasVulkanBackend: false,
            hasIntegratedGpu: false);
        OptimizationCapabilitySnapshot snapshot = Snapshot(authority);
        int[] boundaries = [512, 2_048, 4_095, 4_096, 4_097, maximumContext];

        foreach (int context in boundaries.Where(value =>
            value >= 512 && value <= maximumContext).Distinct())
        {
            Assert.HasCount(
                1,
                CurrentCpuAdmissionsAt(snapshot, context),
                $"Current CPU coverage at {context} tokens must have no gap or overlap.");
        }

        int evaluatedContext = Math.Min(4_096, maximumContext);
        GgufAdmittedConfiguration atEvaluatedContext =
            CurrentCpuAdmissionsAt(snapshot, evaluatedContext).Single();
        Assert.AreEqual(
            evaluatedContext == 4_096
                ? "AB-KV3-F16-4K"
                : "gguf-current-cpu",
            atEvaluatedContext.EvidenceId);
        GgufExecutionPayload payload = CurrentPayload(authority).Gguf!;
        Assert.AreEqual(evaluatedContext, payload.ContextSize);
        Assert.AreEqual(GgufRuntimeBackend.Cpu, payload.Backend);
        Assert.AreEqual(GgufOptimizationProductionAuthority.PackagedCpuDeviceId,
            payload.DeviceId);
        Assert.AreEqual(GgufCacheType.F16, payload.KeyCacheType);
        Assert.AreEqual(GgufCacheType.F16, payload.ValueCacheType);
        Assert.AreEqual(0, payload.GpuLayerCount);
        Assert.IsFalse(payload.FlashAttention);
        Assert.IsTrue(payload.ThreadCount is >= 1 and <= 16);
        Assert.AreEqual(128, payload.BatchSize);
        Assert.AreEqual(512, payload.MaximumGeneratedTokens);
        Assert.AreEqual("cpu-imported", payload.ProfileId);
        Assert.AreEqual(evaluatedContext == 4_096 ? "Measured" : "Estimated",
            payload.EvidenceGrade);
        Assert.AreEqual(snapshot.Gguf.RuntimeAuthority!.RuntimeBuildId,
            payload.RuntimeBuildId);
        Assert.AreEqual(snapshot.Gguf.RuntimeAuthority!.RuntimeSourceCommit,
            payload.RuntimeSourceCommit);
    }

    [TestMethod]
    public void AtomicBotProductionAuthority_RejectsDeclaredContextBelowEstimatorMinimum()
    {
        const ulong gib = 1024UL * 1024 * 1024;
        var prepared = new PreparedGgufCompatibilityInput(
            ModelRunId,
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29",
            HardwareRunId,
            PresentationTestData.Sha256,
            GgufCompatibilityModelInput.Create(
                3 * gib, 32, 4_096, 32, 8, 256, 15, 2),
            CompatibilityHardwareInput.Create(
                TotalPhysicalMemory.FromBytes(16 * gib),
                0,
                500_000_000_000UL,
                [DeviceRouteId.Cpu],
                [CompatibilityBackend.Cpu]));

        Assert.IsFalse(GgufOptimizationProductionAuthority.TryCreate(
            prepared,
            out GgufOptimizationProductionAuthority? authority));
        Assert.IsNull(authority);
    }

    [TestMethod]
    public void AtomicBotProductionAuthority_CurrentHandoffUsesOnlyItsExactEvaluation()
    {
        const ulong gib = 1024UL * 1024 * 1024;
        GgufOptimizationProductionAuthority authority = CreatePublished3bAuthority(
            maximumContext: 32_768,
            hasVulkanBackend: false,
            hasIntegratedGpu: false);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityEvaluation evaluation = authority.Evaluate(
            CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(10 * gib),
                0,
                500_000_000_000UL,
                now),
            new HashSet<string>(),
            now,
            CancellationToken.None);
        CompatibilityEvaluation copiedEvaluation = evaluation with { };
        using var custody = new ModelSourceCustodyRegistry();
        using var registry = new CurrentModelChatLaunchRegistry(custody);

        Assert.AreEqual(
            CompatibilityScreenState.EstimatedCompatible,
            evaluation.Screen.State);
        Assert.AreEqual(4_096, evaluation.Screen.CurrentSetup!.ContextTokens);
        Assert.IsNull(authority.ResolveCurrentModel(copiedEvaluation, registry));
        CompatibilityEvaluation newer = authority.Evaluate(
            CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(10 * gib),
                0,
                500_000_000_000UL,
                now.AddSeconds(1)),
            new HashSet<string>(),
            now.AddSeconds(1),
            CancellationToken.None);
        Assert.IsNull(authority.ResolveCurrentModel(evaluation, registry));
        Assert.IsNotNull(authority.ResolveCurrentModel(newer, registry));
        Assert.AreEqual(4_096, CurrentPayload(authority).Gguf!.ContextSize);
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void AtomicBotProductionAuthority_VulkanGatesKeepCpuBaselineUnique(
        bool hasVulkanBackend,
        bool hasIntegratedGpu)
    {
        GgufOptimizationProductionAuthority authority = CreatePublished3bAuthority(
            maximumContext: 8_192,
            hasVulkanBackend,
            hasIntegratedGpu);
        OptimizationCapabilitySnapshot snapshot = Snapshot(authority);

        Assert.HasCount(1, CurrentCpuAdmissionsAt(snapshot, 4_096));
        bool hasPublishedVulkan = snapshot.Gguf!.Admitted.Any(admission =>
            admission.Backend == CompatibilityBackend.IntelVulkan
            && admission.Device == DeviceRouteId.IntelIntegratedGpu);
        Assert.AreEqual(
            hasVulkanBackend && hasIntegratedGpu,
            hasPublishedVulkan);
    }

    [TestMethod]
    public void ReleaseV5DoesNotIssueTheHistoricalVulkanPlan()
    {
        const ulong gib = 1024UL * 1024 * 1024;
        var prepared = new PreparedGgufCompatibilityInput(
            ModelRunId,
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29",
            HardwareRunId,
            PresentationTestData.Sha256,
            GgufCompatibilityModelInput.Create(
                3 * gib, 32, 4_096, 32, 8, 8_192, 15, 2),
            CompatibilityHardwareInput.Create(
                TotalPhysicalMemory.FromBytes(32 * gib),
                0,
                500_000_000_000UL,
                [DeviceRouteId.Cpu, DeviceRouteId.IntelIntegratedGpu],
                [CompatibilityBackend.Cpu, CompatibilityBackend.IntelVulkan]));
        Assert.IsTrue(GgufOptimizationProductionAuthority.TryCreate(
            prepared,
            out GgufOptimizationProductionAuthority? authority));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityEvaluation evaluation = authority!.Evaluate(
            CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(24 * gib),
                null,
                500_000_000_000UL,
                now),
            new HashSet<string>(),
            now,
            CancellationToken.None);
        Assert.IsNull(evaluation.PlanningSession);
        Assert.IsNull(evaluation.OptionalOptimization);
        Assert.IsFalse(PlanningCandidates(evaluation).Any(static candidate =>
            candidate.Configuration is GgufRouteConfiguration configuration
            && configuration.Backend == CompatibilityBackend.IntelVulkan));
    }

    [TestMethod]
    [TestCategory("RequiresVerifiedQuantizer")]
    public async Task VerifiedDevelopmentQuantizer_EnablesOptionalOptimizationAction()
    {
        string packagedManifest = Path.Combine(
            AppContext.BaseDirectory,
            "Tools",
            "GgufQuantizer",
            "llama-quantize.package.manifest.json");
        if (!File.Exists(packagedManifest))
        {
            Assert.Inconclusive(
                "This local integration check requires the verified GGUF quantizer package.");
        }

        ModelInspectionExecutionResult terminal = ActualHeaderTerminal(parameterCount: null);
        Assert.IsTrue(GgufCompatibilityInputProjector.TryPrepare(
            ModelHandoff(terminal),
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            out PreparedGgufCompatibilityInput? prepared), "prepare");
        Assert.IsTrue(GgufOptimizationProductionAuthority.TryCreate(
            prepared!,
            out GgufOptimizationProductionAuthority? authority), "authority create");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityEvaluation evaluation = authority!.Evaluate(
            CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(21UL * 1024 * 1024 * 1024),
                0,
                500_000_000_000UL,
                now),
            new HashSet<string>(),
            now,
            CancellationToken.None);
        Assert.IsTrue(authority.TryGetOptimizationAuthority(
            evaluation.PlanningSession!.Route,
            out var composer,
            out var issuanceAuthority), "issuance authority");
        OptimizationExecutionPlan plan = evaluation.PlanningSession.Issue(
            GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization
                .OptimizationPreferenceSelection.Automatic(),
            composer!,
            issuanceAuthority!,
            TimeProvider.System);
        GgufQuantiserIdentity quantiser = Snapshot(authority!).Gguf?.AdmittedQuantiser
            ?? throw new AssertFailedException("The GGUF authority did not bind its verified quantizer.");
        Assert.AreEqual("granite-edge-ai-atomicbot-llama-quantize-x64", quantiser.PackageId);
        Assert.AreEqual(
            "atomicbot-llama-quantize-519f0c594a8e31467d2e2f2cf17054c9e7e11536",
            quantiser.ToolVersion);
        Assert.AreEqual(
            "0a17247d4807b520532df54f96ed73f3cf6fa921f879d8d465b44541b41d36e3",
            quantiser.ExecutableSha256);
        Assert.IsNull(plan.ExecutionPayload.Gguf!.Quantiser);
        Assert.IsNull(plan.ExecutionPayload.Gguf.RequantisationPolicy);
        Assert.IsFalse(plan.Candidate.Metrics.RequiresPersistentChange);
        Assert.AreEqual(
            "2d039fdba954b7c9b17f4e552e7a69d5082f988861cc08db9bee3263f293b1b8",
            authority.QuantizerManifestSha256);
        Assert.AreEqual(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Tools", "GgufQuantizer")),
            authority.QuantizerPackageRoot);
        var viewModel = new GraniteEdgeAI.Features.ModelHardwareCompatibility
            .ViewModels.CompatibilityViewModel(
            (_, _) => Task.FromResult(evaluation),
            actionAuthority: authority);

        await viewModel.StartAsync();

        Assert.IsNotNull(evaluation.OptionalOptimization);
        Assert.IsNotNull(viewModel.Presentation.Optimization);
        Assert.IsNotNull(viewModel.CurrentOptimizationHandoff);
        Assert.IsTrue(viewModel.Presentation.PrimaryActionEnabled);
    }

    [TestMethod]
    [TestCategory("RequiresVerifiedQuantizer")]
    public void ExactBf16SourceAndAtomicBotPackageAdmitOnlyMeasuredQ3Conversion()
    {
        string packagedManifest = Path.Combine(
            AppContext.BaseDirectory,
            "Tools",
            "GgufQuantizer",
            "llama-quantize.package.manifest.json");
        if (!File.Exists(packagedManifest))
        {
            Assert.Inconclusive(
                "This integration check requires the verified app-local AtomicBot package.");
        }

        ModelInspectionExecutionResult terminal = Bf16HeaderTerminal(parameterCount: null);
        Assert.IsTrue(GgufCompatibilityInputProjector.TryPrepare(
            ModelHandoff(terminal),
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            out PreparedGgufCompatibilityInput? prepared));
        Assert.AreEqual(VerifiedGgufOptimizationEvidence.ParameterCount,
            prepared!.Model.ParameterCount);
        Assert.IsTrue(GgufOptimizationProductionAuthority.TryCreate(
            prepared,
            out GgufOptimizationProductionAuthority? authority));

        OptimizationCapabilitySnapshot snapshot = Snapshot(authority!);
        GgufAdmittedConfiguration q3 = snapshot.Gguf!.Admitted.Single(item =>
            item.EvidenceId == "GGUF-CURRENT-08EF-BF16-Q3-COMPAT-01");
        Assert.AreEqual(GgufWeightFormat.Q3KM, q3.Weights);
        Assert.AreEqual(GgufKvCacheFormat.F16, q3.KvCache);
        Assert.AreEqual(
            "granite-edge-ai-atomicbot-llama-quantize-x64",
            snapshot.Gguf.AdmittedQuantiser!.PackageId);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityEvaluation evaluation = authority!.Evaluate(
            CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(32UL * 1024 * 1024 * 1024),
                0,
                500_000_000_000UL,
                now),
            new HashSet<string>(),
            now,
            CancellationToken.None);
        OptimizationCandidate candidate = PlanningCandidates(evaluation).Single(item =>
            item.EvidenceId == "GGUF-CURRENT-08EF-BF16-Q3-COMPAT-01");
        Assert.AreEqual(OptimizationConversionProvenance.HigherPrecisionSource,
            candidate.ConversionProvenance);
        Assert.AreEqual(OptimizationQualityLevel.Good, candidate.QualityLevel);
        Assert.IsTrue(candidate.Metrics.RequiresPersistentChange);
        Assert.IsFalse(candidate.IsExperimental);
    }

    [TestMethod]
    [TestCategory("CurrentBf16GgufComposition")]
    [TestCategory("RequiresVerifiedQuantizer")]
    public void CurrentBf16HeaderReachesQ3AlternativeQualifiedMinimumAndNormalIssuer()
    {
        const string evidenceId = "GGUF-CURRENT-08EF-BF16-Q3-COMPAT-01";
        ModelInspectionExecutionResult terminal = Bf16HeaderTerminal(parameterCount: null);
        Assert.IsTrue(GgufCompatibilityInputProjector.TryPrepare(ModelHandoff(terminal), terminal,
            HardwareRunId, HardwareHandoff(), out PreparedGgufCompatibilityInput? prepared));
        Assert.AreEqual(VerifiedGgufOptimizationEvidence.ParameterCount, prepared!.Model.ParameterCount);
        Assert.IsTrue(GgufOptimizationProductionAuthority.TryCreate(prepared, out var authority));
        OptimizationCapabilitySnapshot snapshot = Snapshot(authority!);
        Assert.IsTrue(VerifiedGgufOptimizationEvidence.MatchesBf16Q3Quantizer(snapshot.Gguf!.AdmittedQuantiser));
        Assert.AreEqual(GgufWeightFormat.Q3KM, snapshot.Gguf.Admitted.Single(item => item.EvidenceId == evidenceId).Weights);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityEvaluation evaluation = authority!.Evaluate(CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(8UL * 1024 * 1024 * 1024), 0,
            64UL * 1024 * 1024 * 1024, now), new HashSet<string>(), now, CancellationToken.None);
        OptimizationCandidate candidate = PlanningCandidates(evaluation).Single(item => item.EvidenceId == evidenceId);
        Assert.AreEqual(OptimizationConversionProvenance.HigherPrecisionSource, candidate.ConversionProvenance);
        Assert.IsTrue(candidate.Metrics.RequiresPersistentChange);
        CompatibilityOptimizationView optimization = evaluation.OptionalOptimization ?? evaluation.Screen.Optimization
            ?? throw new AssertFailedException("The real BF16 composition must expose its verified Q3 alternative.");
        var choice = optimization.ExactSafeModes.Single(item => item.Mode.GgufWeights == GgufWeightFormat.Q3KM);
        var presentation = CompatibilityPresentationFactory.From(evaluation);
        Assert.IsNotNull(presentation.MemoryOverview);
        Assert.AreEqual(optimization.ExactSafeModes.Min(item => item.Mode.SystemSharedPredictedPeakBytes),
            presentation.MemoryOverview.MinimumRequiredBytes);
        Assert.IsTrue(authority.TryGetOptimizationAuthority(OptimizationRoute.Gguf, out var composer, out var issuance));
        var plan = evaluation.PlanningSession!.Issue(OptimizationPreferenceSelection.Exact(choice.CandidateIdentity),
            composer!, issuance!, TimeProvider.System);
        Assert.AreEqual(evidenceId, plan.Candidate.EvidenceId);
        Assert.IsTrue(plan.ExecutionPayload.Gguf!.RequiresPersistentConversion);
        Assert.IsTrue(plan.ProducesPersistentArtifact);
    }

    [TestMethod]
    public void ProductionAuthorityDoesNotInvokeTheExternalAtomicBotConstructionStage()
    {
        string source = File.ReadAllText(Path.Combine(
            GraniteEdgeAI.UnitTests.Features.ModelOptimization
                .RepositoryTestPaths.FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "ModelHardwareCompatibility",
            "Infrastructure",
            "GgufOptimizationProductionAuthority.cs"));

        StringAssert.Contains(source, "AppContext.BaseDirectory");
        StringAssert.Contains(source, "Tools");
        StringAssert.Contains(source, "GgufQuantizer");
        Assert.IsFalse(
            source.Contains(@"C:\AI\granite-gguf-validation-20260907", StringComparison.Ordinal),
            "Runtime authority must never invoke the external construction stage.");
        Assert.IsFalse(
            source.Contains("developmentStage", StringComparison.Ordinal),
            "Runtime authority must not bypass the app-local verified package in DEBUG builds.");
    }

    [TestMethod]
    [DoNotParallelize]
    public void AbsentOrWrongAppLocalQuantizerPreservesRuntimeOnlyCompatibility()
    {
        string packaged = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "Tools",
            "GgufQuantizer"));
        string held = packaged + ".held-" + Guid.NewGuid().ToString("N");
        string wrongManifest = Path.Combine(packaged, "llama-quantize.package.manifest.json");
        bool hadPackage = Directory.Exists(packaged);
        Assert.IsFalse(Directory.Exists(held));
        if (hadPackage)
        {
            Directory.Move(packaged, held);
        }
        try
        {
            AssertRuntimeOnlyCompatibilityWithoutQuantizer(
                "absent package",
                expectsPlanningSession: true);
            AssertRuntimeOnlyCompatibilityWithoutQuantizer(
                "absent package BF16",
                expectsPlanningSession: false,
                terminal: Bf16HeaderTerminal(parameterCount: null));

            Directory.CreateDirectory(packaged);
            File.WriteAllText(wrongManifest, "{}", System.Text.Encoding.UTF8);
            AssertRuntimeOnlyCompatibilityWithoutQuantizer(
                "wrong package",
                expectsPlanningSession: true);
            AssertRuntimeOnlyCompatibilityWithoutQuantizer(
                "wrong package BF16",
                expectsPlanningSession: false,
                terminal: Bf16HeaderTerminal(parameterCount: null));
            File.Delete(wrongManifest);
            Directory.Delete(packaged, recursive: false);
        }
        finally
        {
            if (File.Exists(wrongManifest))
            {
                File.Delete(wrongManifest);
            }
            if (Directory.Exists(packaged))
            {
                string[] unexpected = Directory.GetFileSystemEntries(packaged);
                Assert.AreEqual(0, unexpected.Length, "The parity test will not delete unexpected package state.");
                Directory.Delete(packaged, recursive: false);
            }
            if (hadPackage)
            {
                Directory.Move(held, packaged);
            }
        }
    }

    [TestMethod]
    [TestCategory("RequiresVerifiedQuantizer")]
    public async Task VerifiedDevelopmentQuantizer_EnablesRequiredOptimizationAction()
    {
        string packagedManifest = Path.Combine(
            AppContext.BaseDirectory,
            "Tools",
            "GgufQuantizer",
            "llama-quantize.package.manifest.json");
        if (!File.Exists(packagedManifest))
        {
            Assert.Inconclusive(
                "This local integration check requires the verified GGUF quantizer package.");
        }

        const ulong gib = 1024UL * 1024 * 1024;
        ModelInspectionExecutionResult terminal =
            Bf16HeaderTerminal(parameterCount: null);
        Assert.IsTrue(GgufCompatibilityInputProjector.TryPrepare(
            ModelHandoff(terminal),
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            out PreparedGgufCompatibilityInput? prepared));
        Assert.IsTrue(GgufOptimizationProductionAuthority.TryCreate(
            prepared!,
            out GgufOptimizationProductionAuthority? authority));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityEvaluation evaluation = authority!.Evaluate(
            CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(4 * gib),
                0,
                500_000_000_000UL,
                now),
            new HashSet<string>(),
            now,
            CancellationToken.None);
        var viewModel = new GraniteEdgeAI.Features.ModelHardwareCompatibility
            .ViewModels.CompatibilityViewModel(
            (_, _) => Task.FromResult(evaluation),
            actionAuthority: authority);

        await viewModel.StartAsync();

        Assert.AreEqual(
            CompatibilityScreenState.OptimisationRequired,
            evaluation.Screen.State);
        Assert.IsNotNull(viewModel.Presentation.Optimization);
        OptimizationSelectionHandoff handoff =
            viewModel.CurrentOptimizationHandoff
            ?? throw new AssertFailedException(
                "The exact Q3 plan was not handed off.");
        Assert.IsTrue(viewModel.Presentation.PrimaryActionEnabled);
        Assert.IsTrue(viewModel.ContinueCommand.CanExecute(null));
        Assert.AreEqual(
            "GGUF-CURRENT-08EF-BF16-Q3-COMPAT-01",
            handoff.Plan.Candidate.EvidenceId);
        Assert.AreEqual(
            GgufWeightFormat.Q3KM,
            handoff.Plan.ExecutionPayload.Gguf!
                .PersistentTargetWeightFormat);
        Assert.IsTrue(handoff.Plan.ExecutionPayload
            .Gguf.RequiresPersistentConversion);
        Assert.IsNotNull(handoff.Plan.ExecutionPayload
            .Gguf.Quantiser);
    }

    private static GgufOptimizationProductionAuthority CreatePublished3bAuthority(
        int maximumContext,
        bool hasVulkanBackend,
        bool hasIntegratedGpu)
    {
        const ulong gib = 1024UL * 1024 * 1024;
        List<DeviceRouteId> devices = [DeviceRouteId.Cpu];
        List<CompatibilityBackend> backends = [CompatibilityBackend.Cpu];
        if (hasIntegratedGpu)
        {
            devices.Add(DeviceRouteId.IntelIntegratedGpu);
        }
        if (hasVulkanBackend)
        {
            backends.Add(CompatibilityBackend.IntelVulkan);
        }
        var prepared = new PreparedGgufCompatibilityInput(
            ModelRunId,
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29",
            HardwareRunId,
            PresentationTestData.Sha256,
            GgufCompatibilityModelInput.Create(
                3 * gib, 32, 4_096, 32, 8, maximumContext, 15, 2),
            CompatibilityHardwareInput.Create(
                TotalPhysicalMemory.FromBytes(32 * gib),
                0,
                500_000_000_000UL,
                devices,
                backends));
        Assert.IsTrue(GgufOptimizationProductionAuthority.TryCreate(
            prepared,
            out GgufOptimizationProductionAuthority? authority));
        return authority!;
    }

    private static void AssertRuntimeOnlyCompatibilityWithoutQuantizer(
        string scenario,
        bool expectsPlanningSession,
        ModelInspectionExecutionResult? terminal = null)
    {
        terminal ??= ActualHeaderTerminal(parameterCount: null);
        Assert.IsTrue(GgufCompatibilityInputProjector.TryPrepare(
            ModelHandoff(terminal),
            terminal,
            HardwareRunId,
            HardwareHandoff(),
            out PreparedGgufCompatibilityInput? prepared), scenario + " prepare");
        List<string> firstChanceFailures = [];
        EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> observeFailure =
            (_, args) => firstChanceFailures.Add(
                args.Exception.GetType().FullName + ": " + args.Exception.Message);
        AppDomain.CurrentDomain.FirstChanceException += observeFailure;
        bool created;
        GgufOptimizationProductionAuthority? authority;
        try
        {
            created = GgufOptimizationProductionAuthority.TryCreate(
                prepared!,
                out authority);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= observeFailure;
        }
        Assert.IsTrue(
            created,
            scenario + " authority; first-chance failures: "
                + string.Join(" | ", firstChanceFailures));
        Assert.IsNull(authority!.QuantizerPackageRoot, scenario + " quantizer root");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompatibilityEvaluation evaluation = authority.Evaluate(
            CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(21UL * 1024 * 1024 * 1024),
                0,
                500_000_000_000UL,
                now),
            new HashSet<string>(),
            now,
            CancellationToken.None);

        if (expectsPlanningSession)
        {
            Assert.IsNotNull(evaluation.PlanningSession, scenario + " planning session");
            Assert.IsTrue(
                PlanningCandidates(evaluation).All(static candidate =>
                    !candidate.Metrics.RequiresPersistentChange),
                scenario + " exposed a persistent conversion candidate");
        }
        else
        {
            Assert.IsNull(evaluation.PlanningSession, scenario + " planning session");
            Assert.IsNull(
                evaluation.OptionalOptimization,
                scenario + " optional optimization");
        }
        OptimizationExecutionPayload current = CurrentPayload(authority);
        Assert.IsNotNull(current.Gguf, scenario + " current GGUF payload");
        Assert.IsNull(current.Gguf.Quantiser, scenario + " current quantizer");
        Assert.IsFalse(
            current.Gguf.RequiresPersistentConversion,
            scenario + " current payload became persistent");
    }

    private static OptimizationCapabilitySnapshot Snapshot(
        GgufOptimizationProductionAuthority authority)
    {
        FieldInfo? field = typeof(GgufOptimizationProductionAuthority)
            .GetField("_snapshot", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return Assert.IsInstanceOfType<OptimizationCapabilitySnapshot>(
            field.GetValue(authority));
    }

    private static OptimizationExecutionPayload CurrentPayload(
        GgufOptimizationProductionAuthority authority)
    {
        FieldInfo? field = typeof(GgufOptimizationProductionAuthority)
            .GetField("_currentPayload", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return Assert.IsInstanceOfType<OptimizationExecutionPayload>(
            field.GetValue(authority));
    }

    private static IReadOnlyList<OptimizationCandidate> PlanningCandidates(
        CompatibilityEvaluation evaluation)
    {
        if (evaluation.PlanningSession is null)
        {
            return [];
        }

        FieldInfo? field = evaluation.PlanningSession.GetType()
            .GetField("frontier", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return Assert.IsInstanceOfType<IReadOnlyList<OptimizationCandidate>>(
            field.GetValue(evaluation.PlanningSession));
    }

    private static GgufAdmittedConfiguration[] CurrentCpuAdmissionsAt(
        OptimizationCapabilitySnapshot snapshot,
        int context) =>
    [
        .. snapshot.Gguf!.Admitted.Where(admission =>
            admission.Weights == GgufWeightFormat.Imported
            && admission.KvCache == GgufKvCacheFormat.F16
            && admission.Backend == CompatibilityBackend.Cpu
            && admission.Device == DeviceRouteId.Cpu
            && admission.Offload == GpuOffloadLevel.None
            && admission.MinimumContextTokens <= context
            && admission.MaximumContextTokens >= context)
    ];

    private static ModelInspectionExecutionResult Terminal() =>
        ModelInspectionExecutionResult.Completed(
            PresentationTestData.CreateResult(ModelInspectionOutcome.Ready));

    private static ModelInspectionExecutionResult ActualHeaderTerminal(
        ulong? parameterCount)
    {
        const string sourceSha =
            "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29";
        ModelInspectionEvidence seed = PresentationTestData.CreateEvidence();
        var evidence = new ModelInspectionEvidence(
            new ModelInspectionFileEvidence(
                "granite-4.1-3b-Q4_K_M.gguf",
                PresentationTestData.Sha256,
                2_099_501_664,
                DateTimeOffset.UtcNow,
                sourceSha,
                integrityPreserved: true),
            new ModelInspectionConfigurationEvidence(
                "GGUF",
                ggufVersion: 3,
                modelName: "Granite 4.1 3B",
                architecture: "granite",
                fileType: 15,
                quantisationVersion: 2,
                declaredContextLength: 131_072,
                embeddingSize: 2_560,
                layerCount: 40,
                attentionHeadCount: 40,
                kvHeadCount: 8,
                parameterCount),
            seed.Tokenizer,
            seed.ChatTemplate,
            seed.Runtime,
            seed.Observations);
        return ModelInspectionExecutionResult.Completed(
            PresentationTestData.CreateResult(
                ModelInspectionOutcome.Ready,
                evidence: evidence));
    }

    private static ModelInspectionExecutionResult Bf16HeaderTerminal(
        ulong? parameterCount)
    {
        ModelInspectionEvidence seed = PresentationTestData.CreateEvidence();
        var evidence = new ModelInspectionEvidence(
            new ModelInspectionFileEvidence(
                "granite-4.1-3b-bf16.gguf",
                PresentationTestData.Sha256,
                checked((long)VerifiedGgufOptimizationEvidence.Bf16SourceModelLengthBytes),
                DateTimeOffset.UtcNow,
                "e5fc3d677f42a9cba091ea6084cf619bd434ff5ac56b893d3bf5d4f604581091",
                integrityPreserved: true),
            new ModelInspectionConfigurationEvidence(
                "GGUF",
                ggufVersion: 3,
                modelName: "Granite 4.1 3B",
                architecture: "granite",
                fileType: 32,
                quantisationVersion: 2,
                declaredContextLength: 131_072,
                embeddingSize: 2_560,
                layerCount: 40,
                attentionHeadCount: 40,
                kvHeadCount: 8,
                parameterCount),
            seed.Tokenizer,
            seed.ChatTemplate,
            seed.Runtime,
            seed.Observations);
        return ModelInspectionExecutionResult.Completed(
            PresentationTestData.CreateResult(
                ModelInspectionOutcome.Ready,
                evidence: evidence));
    }

    private static ModelInspectionExecutionResult PinnedDownloadTerminal(
        ModelDownloadCatalogEntry entry,
        int? kvHeadCount,
        long? lengthBytes = null,
        string? modelSha256 = null,
        int fileType = 15,
        ulong declaredContextLength = 131_072,
        ulong? parameterCount = 3_000_000_000)
    {
        ModelInspectionEvidence seed = PresentationTestData.CreateEvidence();
        var evidence = new ModelInspectionEvidence(
            new ModelInspectionFileEvidence(
                entry.FileName,
                PresentationTestData.Sha256,
                lengthBytes ?? entry.ExpectedByteLength,
                DateTimeOffset.UtcNow,
                modelSha256 ?? entry.ExpectedSha256,
                integrityPreserved: true),
            new ModelInspectionConfigurationEvidence(
                "GGUF",
                ggufVersion: 3,
                modelName: "Granite 4.0 H Micro",
                architecture: "granitehybrid",
                fileType,
                quantisationVersion: 2,
                declaredContextLength,
                embeddingSize: 2_048,
                layerCount: 40,
                attentionHeadCount: 32,
                kvHeadCount,
                parameterCount),
            seed.Tokenizer,
            seed.ChatTemplate,
            seed.Runtime,
            seed.Observations);
        return ModelInspectionExecutionResult.Completed(
            PresentationTestData.CreateResult(
                ModelInspectionOutcome.Ready,
                evidence: evidence));
    }

    private static ModelInspectionHandoff ModelHandoff(ModelInspectionExecutionResult terminal)
    {
        Assert.IsTrue(ModelInspectionHandoffProjector.TryProject(
            ModelRunId,
            ModelRunId,
            terminal,
            out ModelInspectionHandoff? handoff));
        return handoff!;
    }

    [TestMethod]
    public void Prepare_RetainsTheExactHardwareSnapshotDigest()
    {
        ModelInspectionExecutionResult terminal = Terminal();
        HardwareInspectionHandoff hardware = HardwareHandoff();

        Assert.IsTrue(GgufCompatibilityInputProjector.TryPrepare(
            ModelHandoff(terminal), terminal, HardwareRunId, hardware,
            out PreparedGgufCompatibilityInput? prepared));

        Assert.AreEqual(
            hardware.Snapshot.Identity.Sha256,
            prepared!.HardwareSnapshotSha256);
    }

    private static HardwareInspectionHandoff HardwareHandoff() =>
        HardwareInspectionHandoff.Create(
            HardwareRunId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(HardwareRunId));
}
