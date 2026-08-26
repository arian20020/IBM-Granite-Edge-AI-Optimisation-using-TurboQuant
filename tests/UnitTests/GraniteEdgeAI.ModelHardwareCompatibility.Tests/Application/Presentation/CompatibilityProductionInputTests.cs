using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using System.Reflection;
using System.Collections;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Presentation;

[TestClass]
public sealed class CompatibilityProductionInputTests
{
    private const ulong GiB = 1024UL * 1024 * 1024;

    [TestMethod]
    public void ValidProductionInput_ReachesARealDecision()
    {
        CompatibilityScreenModel result = CompatibilityEngine.Run(ValidInput());

        Assert.AreEqual(CompatibilityScreenState.EstimatedCompatible, result.State);
        Assert.IsNotNull(result.Setup);
        Assert.AreEqual(4, result.Modes.Count);
    }

    [TestMethod]
    public void CurrentFitEvaluation_RetainsOptionalPlanningAuthority()
    {
        DateTimeOffset now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

        CompatibilityEvaluation evaluation = CompatibilityEngine.EvaluateProduction(
            ValidOpenVinoInput(now), new FixedTimeProvider(now));

        Assert.AreEqual(
            CompatibilityScreenState.EstimatedCompatible,
            evaluation.Screen.State);
        Assert.IsNotNull(evaluation.PlanningSession);
        Assert.AreEqual(
            OptimizationRoute.OpenVino,
            evaluation.PlanningSession.Route);
        Assert.IsNull(evaluation.CurrentConfiguration);
    }

    [TestMethod]
    public void ProductionEngine_UsesTheProportionalV2AvailableMemoryReserve()
    {
        CompatibilityScreenModel result = CompatibilityEngine.Run(ValidInput());

        Assert.IsNotNull(result.Setup);
        ulong availableBytes = 48 * GiB;
        ulong expectedReserve = (ulong)Math.Ceiling(availableBytes * 0.10m);
        Assert.AreEqual(
            availableBytes - expectedReserve,
            result.Setup.SafeBudgetBytes);
    }

    [TestMethod]
    public void ProductionEngine_UsesTheAuthoritativeGeneratedFrontier()
    {
        Guid modelRun = Guid.NewGuid();
        Guid hardwareRun = Guid.NewGuid();
        Guid handoffId = Guid.NewGuid();
        const string digest =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        OpenVinoAdmittedConfiguration current =
            OpenVinoAdmittedConfiguration.Create(
                "ov-original", DeviceRouteId.Cpu, OpenVinoWeightFormat.Original,
                OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled, 1, 512, 8192,
                SupportLevel.DeclaredSupported, requiresEvidence: false);
        OpenVinoAdmittedConfiguration alternative =
            OpenVinoAdmittedConfiguration.Create(
                "ov-int4", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int4,
                OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled, 1, 512, 8192,
                SupportLevel.DeclaredSupported, requiresEvidence: false);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-cap", digest, OpenVinoCapabilityPayload.Create(
                    "2026.1.0", [current, alternative],
                    [
                        OpenVinoAuthority(current.EvidenceId, digest),
                        OpenVinoAuthority(alternative.EvidenceId, digest)
                    ]));
        OpenVinoCompatibilityModelInput model =
            OpenVinoCompatibilityModelInput.Create(
                3 * GiB, 32, 4096, 32, 8, 8192);
        CompatibilityCurrentModelInput currentModel =
            CompatibilityCurrentModelInput.ForOpenVino(
                model,
                OpenVinoRouteConfiguration.Create(
                    OpenVinoWeightFormat.Original, OpenVinoKvCacheFormat.U8,
                    DeviceRouteId.Cpu, OpenVinoPerformanceHint.Latency,
                    OpenVinoCompiledCachePolicy.Disabled, 1),
                OpenVinoWeightPrecision.Fp16);
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            modelRun.ToString("N"), handoffId.ToString("N"), digest,
            model.PackageLengthBytes, hardwareRun.ToString("N"), digest);
        CompatibilityOptimizationProductionInput optimization =
            OptimizationInput(snapshot, binding);
        CompatibilityProductionInput legacy = ValidInput(
            modelRun, hardwareRun, availableSystemMemoryBytes: 4 * GiB);
        CompatibilityHardwareInput hardware =
            OpenVinoHardware(DeviceRouteId.Cpu, CompatibilityBackend.OpenVinoCpu);
        CompatibilityProductionInput input = CompatibilityProductionInput.Create(
            modelRun, hardwareRun, currentModel,
            JourneyAuthority(handoffId, digest, currentModel, hardware),
            hardware,
            legacy.FreshResources, optimization);

        CompatibilityScreenModel result = CompatibilityEngine.Run(input);

        Assert.AreEqual(CompatibilityScreenState.OptimisationRequired, result.State);
        Assert.IsNotNull(result.Optimization);
        Assert.AreEqual(OptimizationRoute.OpenVino, result.RecommendedSetup!.Route);
        Assert.IsNull(result.Setup);
        Assert.AreEqual(CompatibilityFitState.DoesNotFit, result.CurrentSetup!.Fit);
        Assert.AreEqual(RuntimeRouteId.OpenVinoGenAi, result.CurrentSetup.Route);
    }

    private static OpenVinoExecutionAuthority OpenVinoAuthority(
        string evidenceId,
        string digest) =>
        OpenVinoExecutionAuthority.Create(
            evidenceId, evidenceId, OpenVinoWeightPrecision.Fp16,
            OpenVinoBuildIdentity.Create(
                "2026.1.0", "2026.1.0", "2026.1.0", digest),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["openvino"] = "2026.1.0"
            },
            compiledCacheIsDisposable: true,
            turboQuantBuild: null);

    private static CompatibilityProductionInput ValidOpenVinoInput(
        DateTimeOffset observedAtUtc)
    {
        Guid modelRun = Guid.NewGuid();
        Guid hardwareRun = Guid.NewGuid();
        Guid handoff = Guid.NewGuid();
        const string digest =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        OpenVinoAdmittedConfiguration admitted =
            OpenVinoAdmittedConfiguration.Create(
                "ov-current", DeviceRouteId.Cpu, OpenVinoWeightFormat.Original,
                OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled, 1, 512, 8192,
                SupportLevel.DeclaredSupported, false);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-cap", digest, OpenVinoCapabilityPayload.Create(
                    "2026.1.0", [admitted],
                    [OpenVinoAuthority(admitted.EvidenceId, digest)]));
        OpenVinoCompatibilityModelInput model =
            OpenVinoCompatibilityModelInput.Create(
                GiB, 16, 2048, 16, 4, 4096);
        CompatibilityCurrentModelInput current =
            CompatibilityCurrentModelInput.ForOpenVino(
                model,
                OpenVinoRouteConfiguration.Create(
                    OpenVinoWeightFormat.Original, OpenVinoKvCacheFormat.U8,
                    DeviceRouteId.Cpu, OpenVinoPerformanceHint.Latency,
                    OpenVinoCompiledCachePolicy.Disabled, 1),
                OpenVinoWeightPrecision.Fp16);
        CompatibilityHardwareInput hardware =
            OpenVinoHardware(DeviceRouteId.Cpu, CompatibilityBackend.OpenVinoCpu);
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            modelRun.ToString("N"), handoff.ToString("N"), digest,
            model.PackageLengthBytes, hardwareRun.ToString("N"), digest);
        return CompatibilityProductionInput.Create(
            modelRun, hardwareRun, current,
            JourneyAuthority(handoff, digest, current, hardware), hardware,
            CompatibilityFreshResourcesInput.Create(
                16 * GiB, 4 * GiB, 500 * GiB, observedAtUtc),
            OptimizationInput(snapshot, binding));
    }

    [TestMethod]
    public void ProductionEngine_GgufFrontierIsReachableWithoutAUiSelectionRerun()
    {
        Guid modelRun = Guid.NewGuid();
        Guid hardwareRun = Guid.NewGuid();
        Guid handoffId = Guid.NewGuid();
        const string digest =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        const string commit = "0123456789abcdef0123456789abcdef01234567";
        GgufAdmittedConfiguration current = GgufAdmittedConfiguration.Create(
            "gguf-f16", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 8192,
            SupportLevel.DeclaredSupported, requiresEvidence: false);
        GgufAdmittedConfiguration alternative = GgufAdmittedConfiguration.Create(
            "gguf-q8", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Imported, GgufKvCacheFormat.Q8_0,
            GpuOffloadLevel.None, 512, 8192,
            SupportLevel.DeclaredSupported, requiresEvidence: false);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForGguf(
                "gguf-cap", digest, GgufCapabilityPayload.Create(
                    "b4321", [current, alternative],
                    runtimeAuthority: GgufRuntimeAuthority.Create(
                        "b4321", commit,
                        [GgufProfile(current.EvidenceId),
                         GgufProfile(alternative.EvidenceId)])));
        CompatibilityScreenModel? found = null;
        for (ulong available = 3 * GiB;
             available <= 6 * GiB;
             available += 16UL * 1024 * 1024)
        {
            CompatibilityProductionInput legacy = ValidInput(
                modelRun, hardwareRun, available);
            OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
                modelRun.ToString("N"), handoffId.ToString("N"), digest,
                legacy.Model.FileLengthBytes, hardwareRun.ToString("N"), digest);
            CompatibilityOptimizationProductionInput optimization =
                OptimizationInput(snapshot, binding);
            CompatibilityCurrentModelInput currentModel =
                CompatibilityCurrentModelInput.ForGguf(
                    legacy.Model,
                    GgufRouteConfiguration.Create(
                        GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
                        CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
                        GpuOffloadLevel.None));
            CompatibilityScreenModel result = CompatibilityEngine.Run(
                CompatibilityProductionInput.Create(
                    modelRun, hardwareRun, currentModel,
                    JourneyAuthority(
                        handoffId, digest, currentModel, legacy.Hardware),
                    legacy.Hardware, legacy.FreshResources, optimization));
            if (result.State == CompatibilityScreenState.OptimisationRequired)
            {
                found = result;
                break;
            }
        }

        Assert.IsNotNull(found, "The GGUF Q8 cache frontier never became actionable.");
        Assert.AreEqual(OptimizationRoute.Gguf, found.RecommendedSetup!.Route);
        Assert.AreEqual(RuntimeRouteId.LlamaCpp, found.CurrentSetup!.Route);
        Assert.IsNotNull(found.Optimization);
    }

    private static GgufExecutionProfileAuthority GgufProfile(string evidenceId) =>
        GgufExecutionProfileAuthority.Create(
            evidenceId, EvidenceGrade.Estimated, $"profile-{evidenceId}",
            flashAttention: false, threadCount: 4, batchSize: 128,
            maximumGeneratedTokens: 256);

    private static CompatibilityOptimizationProductionInput OptimizationInput(
        OptimizationCapabilitySnapshot snapshot,
        OptimizationJourneyBinding binding) =>
        CompatibilityOptimizationProductionInput.Create(
            snapshot,
            OptimizationWorkload.Create(
                "chat", 512, OptimizationAssessment.Poor,
                [ContextTokenCount.FromTokens(4096)]),
            binding,
            new HashSet<string>());

    private static OptimizationCapabilitySnapshot OptimizationSnapshotForCollectionTest()
    {
        const string digest =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        GgufAdmittedConfiguration admission = GgufAdmittedConfiguration.Create(
            "gguf-q8-cache", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Imported, GgufKvCacheFormat.Q8_0,
            GpuOffloadLevel.None, 512, 8192,
            SupportLevel.DeclaredSupported, false);
        return OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap", digest,
            GgufCapabilityPayload.Create(
                "b4321", [admission],
                runtimeAuthority: GgufRuntimeAuthority.Create(
                    "b4321", "0123456789abcdef0123456789abcdef01234567",
                    [GgufProfile(admission.EvidenceId)])));
    }

    [TestMethod]
    public void ProductionEngine_WithoutFrontierCannotClaimOptimizationIsAvailable()
    {
        CompatibilityScreenModel result = CompatibilityEngine.Run(ValidInput(
            Guid.NewGuid(), Guid.NewGuid(), availableSystemMemoryBytes: 4 * GiB));

        Assert.AreNotEqual(
            CompatibilityScreenState.OptimisationRequired,
            result.State);
        Assert.IsNull(result.Optimization);
    }

    [TestMethod]
    [DataRow(-31)]
    [DataRow(6)]
    public void ProductionEngine_RejectsStaleOrFutureFreshResourceEvidence(
        int observedOffsetSeconds)
    {
        DateTimeOffset now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        CompatibilityProductionInput input = ValidInput(
            Guid.NewGuid(), Guid.NewGuid(), 16 * GiB,
            now.AddSeconds(observedOffsetSeconds));

        CompatibilityScreenModel result = CompatibilityEngine.Run(
            input, new FixedTimeProvider(now));

        Assert.AreEqual(CompatibilityScreenState.NotEstablished, result.State);
        Assert.IsFalse(result.ContinueEnabled);
    }

    [TestMethod]
    public void ProductionEngine_RechecksFreshnessAtExecutionRatherThanConstruction()
    {
        DateTimeOffset captured = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        CompatibilityProductionInput input = ValidInput(
            Guid.NewGuid(), Guid.NewGuid(), 16 * GiB, captured);

        CompatibilityScreenModel result = CompatibilityEngine.Run(
            input, new FixedTimeProvider(captured.AddSeconds(31)));

        Assert.AreEqual(CompatibilityScreenState.NotEstablished, result.State);
    }

    [TestMethod]
    [DataRow(-31)]
    [DataRow(6)]
    public void OpenVinoProductionEngine_RejectsStaleOrFutureEvidence(
        int observedOffsetSeconds)
    {
        DateTimeOffset now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        CompatibilityScreenModel result = CompatibilityEngine.Run(
            ValidOpenVinoInput(now.AddSeconds(observedOffsetSeconds)),
            new FixedTimeProvider(now));

        Assert.AreEqual(CompatibilityScreenState.NotEstablished, result.State);
        Assert.IsFalse(result.ContinueEnabled);
    }

    [TestMethod]
    public void OpenVinoProductionEngine_RechecksFreshnessAtExecution()
    {
        DateTimeOffset captured = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        CompatibilityProductionInput input = ValidOpenVinoInput(captured);

        CompatibilityScreenModel result = CompatibilityEngine.Run(
            input, new FixedTimeProvider(captured.AddSeconds(31)));

        Assert.AreEqual(CompatibilityScreenState.NotEstablished, result.State);
    }

    [TestMethod]
    [DataRow(-30)]
    [DataRow(5)]
    public void FreshnessPolicy_AcceptsExactReviewedBoundaries(int offsetSeconds)
    {
        DateTimeOffset now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        CompatibilityScreenModel gguf = CompatibilityEngine.Run(
            ValidInput(Guid.NewGuid(), Guid.NewGuid(), 16 * GiB,
                now.AddSeconds(offsetSeconds)),
            new FixedTimeProvider(now));
        CompatibilityScreenModel openVino = CompatibilityEngine.Run(
            ValidOpenVinoInput(now.AddSeconds(offsetSeconds)),
            new FixedTimeProvider(now));

        Assert.AreNotEqual(CompatibilityScreenState.NotEstablished, gguf.State);
        Assert.AreNotEqual(CompatibilityScreenState.NotEstablished, openVino.State);
    }

    [TestMethod]
    public void LegacySixArgumentFactory_PreservesNullOptimizationBehavior()
    {
        CompatibilityProductionInput legacy = ValidInput();

        CompatibilityProductionInput result = CompatibilityProductionInput.Create(
            legacy.ModelInspectionRunId,
            legacy.ProductHardwareRunId,
            legacy.Model,
            legacy.Hardware,
            legacy.FreshResources,
            optimization: null);

        Assert.AreEqual(OptimizationRoute.Gguf, result.CurrentModel.Route);
        Assert.IsNull(result.Optimization);
    }

    [TestMethod]
    [DataRow("model-run")]
    [DataRow("handoff")]
    [DataRow("model-sha")]
    [DataRow("length")]
    [DataRow("hardware-run")]
    [DataRow("hardware-sha")]
    public void AuthoritativeProductionInput_RejectsEveryJourneyMismatch(
        string mismatch)
    {
        const string digest =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        const string otherDigest =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        Guid modelRun = Guid.NewGuid();
        Guid hardwareRun = Guid.NewGuid();
        Guid handoff = Guid.NewGuid();
        Guid other = Guid.NewGuid();
        CompatibilityProductionInput legacy = ValidInput(modelRun, hardwareRun, 4 * GiB);
        GgufAdmittedConfiguration current = GgufAdmittedConfiguration.Create(
            "gguf-f16", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 8192,
            SupportLevel.DeclaredSupported, requiresEvidence: false);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForGguf(
                "gguf-cap", digest, GgufCapabilityPayload.Create(
                    "b4321", [current], runtimeAuthority: GgufRuntimeAuthority.Create(
                        "b4321", "0123456789abcdef0123456789abcdef01234567",
                        [GgufProfile(current.EvidenceId)])));
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            mismatch == "model-run" ? other.ToString("N") : modelRun.ToString("N"),
            mismatch == "handoff" ? other.ToString("N") : handoff.ToString("N"),
            mismatch == "model-sha" ? otherDigest : digest,
            legacy.Model.FileLengthBytes + (mismatch == "length" ? 1UL : 0UL),
            mismatch == "hardware-run" ? other.ToString("N") : hardwareRun.ToString("N"),
            mismatch == "hardware-sha" ? otherDigest : digest);

        CompatibilityCurrentModelInput currentModel =
            CompatibilityCurrentModelInput.ForGguf(
                legacy.Model,
                GgufRouteConfiguration.Create(
                    GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
                    CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
                    GpuOffloadLevel.None));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityProductionInput.Create(
                modelRun,
                hardwareRun,
                currentModel,
                JourneyAuthority(handoff, digest, currentModel, legacy.Hardware),
                legacy.Hardware,
                legacy.FreshResources,
                OptimizationInput(snapshot, binding)),
            mismatch);
    }

    [TestMethod]
    [DataRow("model-facts")]
    [DataRow("model-configuration")]
    [DataRow("hardware-facts")]
    [DataRow("hardware-dedicated")]
    [DataRow("hardware-storage")]
    [DataRow("hardware-device")]
    [DataRow("hardware-backend")]
    public void AuthoritativeProductionInput_RejectsOwnerHashReplayOverChangedFacts(
        string mutation)
    {
        Guid modelRun = Guid.NewGuid();
        Guid hardwareRun = Guid.NewGuid();
        Guid handoff = Guid.NewGuid();
        const string artifactDigest =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        CompatibilityProductionInput legacy = ValidInput(
            modelRun, hardwareRun, 16 * GiB);
        CompatibilityCurrentModelInput current =
            CompatibilityCurrentModelInput.ForGguf(
                legacy.Model,
                GgufRouteConfiguration.Create(
                    GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
                    CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
                    GpuOffloadLevel.None));
        CompatibilityHardwareInput hardware = legacy.Hardware;
        GgufAdmittedConfiguration admission = GgufAdmittedConfiguration.Create(
            "gguf-f16", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 8192,
            SupportLevel.DeclaredSupported, false);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForGguf(
                "gguf-cap", artifactDigest,
                GgufCapabilityPayload.Create(
                    "b4321", [admission],
                    runtimeAuthority: GgufRuntimeAuthority.Create(
                        "b4321", "0123456789abcdef0123456789abcdef01234567",
                        [GgufProfile(admission.EvidenceId)])));
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            modelRun.ToString("N"), handoff.ToString("N"), artifactDigest,
            current.ModelLengthBytes, hardwareRun.ToString("N"), artifactDigest);
        CompatibilityJourneyAuthorityInput authority =
            CompatibilityJourneyAuthorityInput.Create(
                handoff,
                artifactDigest,
                CompatibilityFactDigest.ComputeModel(current),
                artifactDigest,
                CompatibilityFactDigest.ComputeHardware(hardware));

        CompatibilityCurrentModelInput submittedModel = mutation switch
        {
            "model-facts" => CompatibilityCurrentModelInput.ForGguf(
                GgufCompatibilityModelInput.Create(
                    legacy.Model.FileLengthBytes, legacy.Model.LayerCount,
                    legacy.Model.EmbeddingSize + 1,
                    legacy.Model.AttentionHeadCount,
                    legacy.Model.KeyValueHeadCount,
                    legacy.Model.DeclaredContextLimit,
                    legacy.Model.FileType,
                    legacy.Model.QuantisationVersion),
                current.GgufConfiguration!),
            "model-configuration" => CompatibilityCurrentModelInput.ForGguf(
                legacy.Model,
                GgufRouteConfiguration.Create(
                    GgufWeightFormat.Imported, GgufKvCacheFormat.Q8_0,
                    CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
                    GpuOffloadLevel.None)),
            _ => current
        };
        CompatibilityHardwareInput submittedHardware = mutation switch
        {
            "hardware-facts" => CompatibilityHardwareInput.Create(
                hardware.InstalledSystemMemoryBytes + 1,
                hardware.InstalledDedicatedDeviceMemoryBytes,
                hardware.FreeStorageBytes,
                hardware.PresentDevices, hardware.VerifiedBackends),
            "hardware-dedicated" => CompatibilityHardwareInput.Create(
                hardware.InstalledSystemMemoryBytes,
                hardware.InstalledDedicatedDeviceMemoryBytes + 1,
                hardware.FreeStorageBytes,
                hardware.PresentDevices, hardware.VerifiedBackends),
            "hardware-storage" => CompatibilityHardwareInput.Create(
                hardware.InstalledSystemMemoryBytes,
                hardware.InstalledDedicatedDeviceMemoryBytes,
                hardware.FreeStorageBytes + 1,
                hardware.PresentDevices, hardware.VerifiedBackends),
            "hardware-device" => CompatibilityHardwareInput.Create(
                hardware.InstalledSystemMemoryBytes,
                hardware.InstalledDedicatedDeviceMemoryBytes,
                hardware.FreeStorageBytes,
                [DeviceRouteId.IntelNpu], hardware.VerifiedBackends),
            "hardware-backend" => CompatibilityHardwareInput.Create(
                hardware.InstalledSystemMemoryBytes,
                hardware.InstalledDedicatedDeviceMemoryBytes,
                hardware.FreeStorageBytes,
                hardware.PresentDevices, [CompatibilityBackend.IntelSycl]),
            _ => hardware
        };

        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityProductionInput.Create(
                modelRun, hardwareRun, submittedModel, authority,
                submittedHardware, legacy.FreshResources,
                OptimizationInput(snapshot, binding)));
    }

    [TestMethod]
    public void AuthoritativeProductionInput_RejectsCrossRouteCapabilityEvidence()
    {
        Guid modelRun = Guid.NewGuid();
        Guid hardwareRun = Guid.NewGuid();
        Guid handoff = Guid.NewGuid();
        const string digest =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        CompatibilityProductionInput legacy = ValidInput(modelRun, hardwareRun, 4 * GiB);
        OpenVinoAdmittedConfiguration admission =
            OpenVinoAdmittedConfiguration.Create(
                "ov-original", DeviceRouteId.Cpu, OpenVinoWeightFormat.Original,
                OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled, 1, 512, 8192,
                SupportLevel.DeclaredSupported, false);
        OptimizationCapabilitySnapshot openVino =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-cap", digest, OpenVinoCapabilityPayload.Create(
                    "2026.1.0", [admission],
                    [OpenVinoAuthority(admission.EvidenceId, digest)]));
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            modelRun.ToString("N"), handoff.ToString("N"), digest,
            legacy.Model.FileLengthBytes, hardwareRun.ToString("N"), digest);

        CompatibilityCurrentModelInput currentModel =
            CompatibilityCurrentModelInput.ForGguf(
                legacy.Model,
                GgufRouteConfiguration.Create(
                    GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
                    CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
                    GpuOffloadLevel.None));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityProductionInput.Create(
                modelRun, hardwareRun,
                currentModel,
                JourneyAuthority(handoff, digest, currentModel, legacy.Hardware),
                legacy.Hardware, legacy.FreshResources,
                OptimizationInput(openVino, binding)));
    }

    [TestMethod]
    public void ProductionEngine_RejectsOpenVinoSourcePrecisionMismatch()
    {
        Guid modelRun = Guid.NewGuid();
        Guid hardwareRun = Guid.NewGuid();
        Guid handoff = Guid.NewGuid();
        const string digest =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        OpenVinoAdmittedConfiguration admission =
            OpenVinoAdmittedConfiguration.Create(
                "ov-original", DeviceRouteId.Cpu, OpenVinoWeightFormat.Original,
                OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled, 1, 512, 8192,
                SupportLevel.DeclaredSupported, false);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-cap", digest, OpenVinoCapabilityPayload.Create(
                    "2026.1.0", [admission],
                    [OpenVinoAuthority(admission.EvidenceId, digest)]));
        OpenVinoCompatibilityModelInput model =
            OpenVinoCompatibilityModelInput.Create(
                3 * GiB, 32, 4096, 32, 8, 8192);
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            modelRun.ToString("N"), handoff.ToString("N"), digest,
            model.PackageLengthBytes, hardwareRun.ToString("N"), digest);
        CompatibilityProductionInput legacy = ValidInput(
            modelRun, hardwareRun, availableSystemMemoryBytes: 4 * GiB);
        CompatibilityCurrentModelInput currentModel =
            CompatibilityCurrentModelInput.ForOpenVino(
                model,
                OpenVinoRouteConfiguration.Create(
                    OpenVinoWeightFormat.Original, OpenVinoKvCacheFormat.U8,
                    DeviceRouteId.Cpu, OpenVinoPerformanceHint.Latency,
                    OpenVinoCompiledCachePolicy.Disabled, 1),
                OpenVinoWeightPrecision.EightBit);
        CompatibilityHardwareInput hardware =
            OpenVinoHardware(DeviceRouteId.Cpu, CompatibilityBackend.OpenVinoCpu);
        CompatibilityProductionInput input = CompatibilityProductionInput.Create(
            modelRun, hardwareRun,
            currentModel,
            JourneyAuthority(handoff, digest, currentModel, hardware),
            hardware, legacy.FreshResources,
            OptimizationInput(snapshot, binding));

        CompatibilityScreenModel result = CompatibilityEngine.Run(input);

        Assert.AreEqual(CompatibilityScreenState.NotEstablished, result.State);
        Assert.IsNull(result.Optimization);
    }

    [TestMethod]
    [DataRow(DeviceRouteId.Cpu, CompatibilityBackend.OpenVinoCpu, DeviceRouteId.Cpu, true)]
    [DataRow(DeviceRouteId.Cpu, CompatibilityBackend.Cpu, DeviceRouteId.Cpu, false)]
    [DataRow(DeviceRouteId.IntelIntegratedGpu, CompatibilityBackend.OpenVinoGpu,
        DeviceRouteId.IntelIntegratedGpu, true)]
    [DataRow(DeviceRouteId.IntelIntegratedGpu, CompatibilityBackend.OpenVinoGpu,
        DeviceRouteId.IntelDiscreteGpu, false)]
    [DataRow(DeviceRouteId.IntelDiscreteGpu, CompatibilityBackend.OpenVinoGpu,
        DeviceRouteId.IntelDiscreteGpu, true)]
    [DataRow(DeviceRouteId.IntelNpu, CompatibilityBackend.OpenVinoNpu,
        DeviceRouteId.IntelNpu, true)]
    [DataRow(DeviceRouteId.IntelNpu, CompatibilityBackend.OpenVinoNpu,
        DeviceRouteId.Cpu, false)]
    public void ProductionEngine_RequiresExactOpenVinoDeviceAndBackendCoherence(
        DeviceRouteId configuredDevice,
        CompatibilityBackend verifiedBackend,
        DeviceRouteId presentDevice,
        bool expectedEstablished)
    {
        Guid modelRun = Guid.NewGuid();
        Guid hardwareRun = Guid.NewGuid();
        Guid handoff = Guid.NewGuid();
        const string digest =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        OpenVinoAdmittedConfiguration admission =
            OpenVinoAdmittedConfiguration.Create(
                "ov-current", configuredDevice, OpenVinoWeightFormat.Original,
                OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled, 1, 512, 8192,
                SupportLevel.DeclaredSupported, false);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-cap", digest, OpenVinoCapabilityPayload.Create(
                    "2026.1.0", [admission],
                    [OpenVinoAuthority(admission.EvidenceId, digest)]));
        OpenVinoCompatibilityModelInput model =
            OpenVinoCompatibilityModelInput.Create(
                1 * GiB, 16, 2048, 16, 4, 4096);
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            modelRun.ToString("N"), handoff.ToString("N"), digest,
            model.PackageLengthBytes, hardwareRun.ToString("N"), digest);
        CompatibilityProductionInput legacy = ValidInput(
            modelRun, hardwareRun, availableSystemMemoryBytes: 16 * GiB);
        CompatibilityCurrentModelInput currentModel =
            CompatibilityCurrentModelInput.ForOpenVino(
                model,
                OpenVinoRouteConfiguration.Create(
                    OpenVinoWeightFormat.Original, OpenVinoKvCacheFormat.U8,
                    configuredDevice, OpenVinoPerformanceHint.Latency,
                    OpenVinoCompiledCachePolicy.Disabled, 1),
                OpenVinoWeightPrecision.Fp16);
        CompatibilityHardwareInput hardware =
            OpenVinoHardware(presentDevice, verifiedBackend);
        CompatibilityFreshResourcesInput fresh =
            CompatibilityFreshResourcesInput.Create(
                legacy.FreshResources.AvailableSystemMemoryBytes,
                configuredDevice == DeviceRouteId.IntelDiscreteGpu
                    ? 4 * GiB
                    : 0,
                legacy.FreshResources.AvailableStorageBytes,
                legacy.FreshResources.ObservedAtUtc);
        CompatibilityProductionInput input = CompatibilityProductionInput.Create(
            modelRun, hardwareRun,
            currentModel,
            JourneyAuthority(handoff, digest, currentModel, hardware),
            hardware,
            fresh,
            OptimizationInput(snapshot, binding));

        CompatibilityScreenModel result = CompatibilityEngine.Run(input);

        if (expectedEstablished)
        {
            Assert.AreEqual(
                CompatibilityScreenState.EstimatedCompatible, result.State);
        }
        else
        {
            Assert.AreEqual(CompatibilityScreenState.NotEstablished, result.State);
            Assert.IsNull(result.Optimization);
        }
    }

    private static CompatibilityHardwareInput OpenVinoHardware(
        DeviceRouteId device,
        CompatibilityBackend backend) =>
        CompatibilityHardwareInput.Create(
            16 * GiB, 4 * GiB, 500 * GiB, [device], [backend]);

    [TestMethod]
    public void ProductionInput_RejectsEmptyOrDuplicateRunIdentities()
    {
        CompatibilityProductionInput valid = ValidInput();

        Assert.ThrowsExactly<ArgumentException>(() => CompatibilityProductionInput.Create(
            Guid.Empty,
            valid.ProductHardwareRunId,
            valid.Model,
            valid.Hardware,
            valid.FreshResources));

        Assert.ThrowsExactly<ArgumentException>(() => CompatibilityProductionInput.Create(
            valid.ModelInspectionRunId,
            valid.ModelInspectionRunId,
            valid.Model,
            valid.Hardware,
            valid.FreshResources));
    }

    [TestMethod]
    public void ModelInput_RejectsZeroLengthAndNonPositiveOptionalFacts()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            GgufCompatibilityModelInput.Create(
                0, 32, 4096, 32, 8, 8192, 15, 2));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            GgufCompatibilityModelInput.Create(
                3 * GiB, 0, 4096, 32, 8, 8192, 15, 2));
    }

    [TestMethod]
    public void HardwareInput_RejectsMissingCapacityAndUnknownEnums()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            CompatibilityHardwareInput.Create(
                0,
                0,
                500 * GiB,
                [DeviceRouteId.Cpu],
                [CompatibilityBackend.Cpu]));

        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityHardwareInput.Create(
                64 * GiB,
                0,
                500 * GiB,
                [DeviceRouteId.Unspecified],
                [CompatibilityBackend.Cpu]));
    }

    [TestMethod]
    public void InputCollections_AreDefensivelyCopied()
    {
        HashSet<DeviceRouteId> devices = [DeviceRouteId.Cpu];
        HashSet<CompatibilityBackend> backends = [CompatibilityBackend.Cpu];
        CompatibilityHardwareInput input = CompatibilityHardwareInput.Create(
            64 * GiB, 0, 500 * GiB, devices, backends);

        devices.Clear();
        backends.Clear();

        CollectionAssert.Contains(input.PresentDevices.ToArray(), DeviceRouteId.Cpu);
        CollectionAssert.Contains(input.VerifiedBackends.ToArray(), CompatibilityBackend.Cpu);
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((ISet<DeviceRouteId>)input.PresentDevices).Add(
                DeviceRouteId.IntelNpu));
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((ISet<CompatibilityBackend>)input.VerifiedBackends).Add(
                CompatibilityBackend.OpenVinoNpu));
    }

    [TestMethod]
    public void HardwareInput_EnumeratesEachCollectionExactlyOnceWithoutReadingCount()
    {
        HostileCountCollection<DeviceRouteId> devices = new(DeviceRouteId.Cpu);
        HostileCountCollection<CompatibilityBackend> backends =
            new(CompatibilityBackend.Cpu);

        CompatibilityHardwareInput input = CompatibilityHardwareInput.Create(
            16 * GiB, 0, 100 * GiB, devices, backends);

        Assert.AreEqual(1, devices.EnumerationCount);
        Assert.AreEqual(1, backends.EnumerationCount);
        CollectionAssert.AreEqual(
            new[] { DeviceRouteId.Cpu }, input.PresentDevices.ToArray());
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(999)]
    public void HardwareInput_RejectsEveryUndefinedEnumValue(int raw)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityHardwareInput.Create(
                16 * GiB, 0, 100 * GiB,
                [(DeviceRouteId)raw],
                [CompatibilityBackend.Cpu]));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityHardwareInput.Create(
                16 * GiB, 0, 100 * GiB,
                [DeviceRouteId.Cpu],
                [(CompatibilityBackend)raw]));
    }

    [TestMethod]
    public void OptimizationInput_SnapshotsOptInsExactlyOnceWithoutReadingCount()
    {
        HostileReadOnlySet<string> optedIn = new("gguf-q8-cache");

        CompatibilityOptimizationProductionInput input =
            CompatibilityOptimizationProductionInput.Create(
                OptimizationSnapshotForCollectionTest(),
                OptimizationWorkload.Create(
                    "chat", 512, OptimizationAssessment.Poor,
                    [ContextTokenCount.FromTokens(4096)]),
                OptimizationJourneyBinding.Create(
                    "mi-run", "mi-handoff", new string('a', 64), 1,
                    "hw-run", new string('b', 64)),
                optedIn);

        Assert.AreEqual(1, optedIn.EnumerationCount);
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((ISet<string>)input.OptedInExperimentalEvidenceIds).Add("another"));
    }

    [TestMethod]
    public void ProductionInput_ConsentReissuePreservesAuthorityAndCopiesExactIds()
    {
        CompatibilityProductionInput original = ValidOpenVinoInput(
            new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        HashSet<string> consent = new(StringComparer.Ordinal)
        {
            "ov-experimental"
        };

        CompatibilityProductionInput revised =
            original.WithOptedInExperimentalEvidenceIds(consent);
        consent.Clear();

        Assert.AreSame(original.CurrentModel, revised.CurrentModel);
        Assert.AreSame(original.Hardware, revised.Hardware);
        Assert.AreSame(original.FreshResources, revised.FreshResources);
        Assert.AreSame(original.JourneyAuthority, revised.JourneyAuthority);
        Assert.AreSame(
            original.Optimization!.Snapshot,
            revised.Optimization!.Snapshot);
        CollectionAssert.AreEqual(
            new[] { "ov-experimental" },
            revised.Optimization.OptedInExperimentalEvidenceIds.ToArray());
    }

    [TestMethod]
    public void FreshResources_RequireUtcObservation()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityFreshResourcesInput.Create(
                48 * GiB,
                0,
                500 * GiB,
                new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.FromHours(1))));
    }

    [TestMethod]
    [DataRow("system")]
    [DataRow("dedicated")]
    [DataRow("storage")]
    public void FreshResources_CannotExceedBoundInstalledHardware(string axis)
    {
        CompatibilityProductionInput valid = ValidInput();
        CompatibilityFreshResourcesInput contradictory =
            CompatibilityFreshResourcesInput.Create(
                axis == "system"
                    ? valid.Hardware.InstalledSystemMemoryBytes + 1
                    : valid.FreshResources.AvailableSystemMemoryBytes,
                axis == "dedicated"
                    ? valid.Hardware.InstalledDedicatedDeviceMemoryBytes + 1
                    : valid.FreshResources.AvailableDedicatedDeviceMemoryBytes,
                axis == "storage"
                    ? valid.Hardware.FreeStorageBytes + 1
                    : valid.FreshResources.AvailableStorageBytes,
                valid.FreshResources.ObservedAtUtc);

        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityProductionInput.Create(
                valid.ModelInspectionRunId, valid.ProductHardwareRunId,
                valid.Model, valid.Hardware, contradictory));
    }

    [TestMethod]
    public void ProductionBoundary_CarriesNoFreeFormOrPathLikeStrings()
    {
        Type[] boundaryTypes =
        [
            typeof(GgufCompatibilityModelInput),
            typeof(OpenVinoCompatibilityModelInput),
            typeof(CompatibilityCurrentModelInput),
            typeof(CompatibilityHardwareInput),
            typeof(CompatibilityFreshResourcesInput),
            typeof(CompatibilityProductionInput),
        ];

        foreach (Type type in boundaryTypes)
        {
            PropertyInfo[] strings = type.GetProperties()
                .Where(property => property.PropertyType == typeof(string))
                .ToArray();

            Assert.AreEqual(0, strings.Length, $"{type.Name} carries free-form text.");
        }

        string[] journeyStrings = typeof(CompatibilityJourneyAuthorityInput)
            .GetProperties()
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(
            new[]
            {
                "HardwareFactsSha256", "HardwareSnapshotSha256",
                "ModelFactsSha256", "ModelSha256"
            },
            journeyStrings);
        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityJourneyAuthorityInput.Create(
                Guid.NewGuid(), @"C:\private\model.gguf", new string('a', 64)));
    }

    private static CompatibilityProductionInput ValidInput() =>
        ValidInput(Guid.NewGuid(), Guid.NewGuid(), 48 * GiB);

    private static CompatibilityProductionInput ValidInput(
        Guid modelRun,
        Guid hardwareRun,
        ulong availableSystemMemoryBytes,
        DateTimeOffset? observedAtUtc = null)
    {
        DateTimeOffset observedAt = observedAtUtc ?? DateTimeOffset.UtcNow;
        return CompatibilityProductionInput.Create(
            modelRun,
            hardwareRun,
            GgufCompatibilityModelInput.Create(
                3 * GiB,
                32,
                4096,
                32,
                8,
                8192,
                15,
                2),
            CompatibilityHardwareInput.Create(
                64 * GiB,
                0,
                500 * GiB,
                [DeviceRouteId.Cpu],
                [CompatibilityBackend.Cpu]),
            CompatibilityFreshResourcesInput.Create(
                availableSystemMemoryBytes,
                0,
                500 * GiB,
                observedAt));
    }

    private static CompatibilityJourneyAuthorityInput JourneyAuthority(
        Guid handoffId,
        string artifactDigest,
        CompatibilityCurrentModelInput currentModel,
        CompatibilityHardwareInput hardware) =>
        CompatibilityJourneyAuthorityInput.Create(
            handoffId,
            artifactDigest,
            CompatibilityFactDigest.ComputeModel(currentModel),
            artifactDigest,
            CompatibilityFactDigest.ComputeHardware(hardware));

    private sealed class SinglePassEnumerable<T>(T value) : IEnumerable<T>
    {
        public int EnumerationCount { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            EnumerationCount++;
            if (EnumerationCount != 1)
            {
                throw new InvalidOperationException("The source was enumerated twice.");
            }

            yield return value;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class HostileCountCollection<T>(T value) : ICollection<T>
    {
        private readonly SinglePassEnumerable<T> source = new(value);

        public int EnumerationCount => source.EnumerationCount;
        public int Count => throw new InvalidOperationException("Count is hostile.");
        public bool IsReadOnly => true;
        public IEnumerator<T> GetEnumerator() => source.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool Contains(T item) => throw new NotSupportedException();
        public void CopyTo(T[] array, int arrayIndex) => throw new NotSupportedException();
        public void Add(T item) => throw new NotSupportedException();
        public bool Remove(T item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
    }

    private sealed class HostileReadOnlySet<T>(T value) : IReadOnlySet<T>
    {
        private readonly SinglePassEnumerable<T> source = new(value);

        public int EnumerationCount => source.EnumerationCount;
        public int Count => throw new InvalidOperationException("Count is hostile.");
        public IEnumerator<T> GetEnumerator() => source.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool Contains(T item) => EqualityComparer<T>.Default.Equals(value, item);
        public bool IsProperSubsetOf(IEnumerable<T> other) => throw new NotSupportedException();
        public bool IsProperSupersetOf(IEnumerable<T> other) => throw new NotSupportedException();
        public bool IsSubsetOf(IEnumerable<T> other) => throw new NotSupportedException();
        public bool IsSupersetOf(IEnumerable<T> other) => throw new NotSupportedException();
        public bool Overlaps(IEnumerable<T> other) => throw new NotSupportedException();
        public bool SetEquals(IEnumerable<T> other) => throw new NotSupportedException();
    }
}
