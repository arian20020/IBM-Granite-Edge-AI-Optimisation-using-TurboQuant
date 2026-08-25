using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using System.Reflection;

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
        CompatibilityProductionInput legacy = ValidInput(
            modelRun, hardwareRun, availableSystemMemoryBytes: 4 * GiB);
        const string digest =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        OpenVinoAdmittedConfiguration admitted =
            OpenVinoAdmittedConfiguration.Create(
                "ov-int4", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int4,
                OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled, 1, 512, 8192,
                SupportLevel.DeclaredSupported, requiresEvidence: false);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-cap", digest, OpenVinoCapabilityPayload.Create(
                    "2026.1.0", [admitted],
                    [OpenVinoExecutionAuthority.Create(
                        "ov-int4", "ov-int4", OpenVinoWeightPrecision.Fp16,
                        OpenVinoBuildIdentity.Create(
                            "2026.1.0", "2026.1.0", "2026.1.0", digest),
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["openvino"] = "2026.1.0"
                        },
                        compiledCacheIsDisposable: true,
                        turboQuantBuild: null)]));
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            modelRun.ToString("N"), "handoff", digest,
            legacy.Model.FileLengthBytes, hardwareRun.ToString("N"), digest);
        CompatibilityOptimizationProductionInput optimization =
            CompatibilityOptimizationProductionInput.Create(
                snapshot,
                OptimizationWorkload.Create(
                    "chat", 512, OptimizationAssessment.Poor,
                    [ContextTokenCount.FromTokens(4096)]),
                binding,
                new HashSet<string>());
        CompatibilityProductionInput input = CompatibilityProductionInput.Create(
            modelRun, hardwareRun, legacy.Model, legacy.Hardware,
            legacy.FreshResources, optimization);

        CompatibilityScreenModel result = CompatibilityEngine.Run(input);

        Assert.AreEqual(CompatibilityScreenState.OptimisationRequired, result.State);
        Assert.IsNotNull(result.Optimization);
        Assert.AreEqual(OptimizationRoute.OpenVino, result.RecommendedSetup!.Route);
        Assert.IsNull(result.Setup);
        Assert.AreEqual(CompatibilityFitState.DoesNotFit, result.CurrentSetup!.Fit);
    }

    [TestMethod]
    public void ProductionEngine_GgufFrontierIsReachableWithoutAUiSelectionRerun()
    {
        Guid modelRun = Guid.NewGuid();
        Guid hardwareRun = Guid.NewGuid();
        const string digest =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        const string commit = "0123456789abcdef0123456789abcdef01234567";
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            "gguf-q8", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Imported, GgufKvCacheFormat.Q8_0,
            GpuOffloadLevel.None, 512, 8192,
            SupportLevel.DeclaredSupported, requiresEvidence: false);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForGguf(
                "gguf-cap", digest, GgufCapabilityPayload.Create(
                    "b4321", [admitted], runtimeAuthority: GgufRuntimeAuthority.Create(
                        "b4321", commit,
                        [GgufExecutionProfileAuthority.Create(
                            admitted.EvidenceId, EvidenceGrade.Estimated, "profile",
                            flashAttention: false, threadCount: 4, batchSize: 128,
                            maximumGeneratedTokens: 256)])));
        CompatibilityScreenModel? found = null;
        for (ulong available = 3 * GiB;
             available <= 6 * GiB;
             available += 16UL * 1024 * 1024)
        {
            CompatibilityProductionInput legacy = ValidInput(
                modelRun, hardwareRun, available);
            OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
                modelRun.ToString("N"), "handoff", digest,
                legacy.Model.FileLengthBytes, hardwareRun.ToString("N"), digest);
            CompatibilityOptimizationProductionInput optimization =
                CompatibilityOptimizationProductionInput.Create(
                    snapshot,
                    OptimizationWorkload.Create(
                        "chat", 512, OptimizationAssessment.Poor,
                        [ContextTokenCount.FromTokens(4096)]),
                    binding,
                    new HashSet<string>());
            CompatibilityScreenModel result = CompatibilityEngine.Run(
                CompatibilityProductionInput.Create(
                    modelRun, hardwareRun, legacy.Model, legacy.Hardware,
                    legacy.FreshResources, optimization));
            if (result.State == CompatibilityScreenState.OptimisationRequired)
            {
                found = result;
                break;
            }
        }

        Assert.IsNotNull(found, "The GGUF Q8 cache frontier never became actionable.");
        Assert.AreEqual(OptimizationRoute.Gguf, found.RecommendedSetup!.Route);
        Assert.IsNotNull(found.Optimization);
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
    public void ProductionBoundary_CarriesNoFreeFormOrPathLikeStrings()
    {
        Type[] boundaryTypes =
        [
            typeof(GgufCompatibilityModelInput),
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
    }

    private static CompatibilityProductionInput ValidInput() =>
        ValidInput(Guid.NewGuid(), Guid.NewGuid(), 48 * GiB);

    private static CompatibilityProductionInput ValidInput(
        Guid modelRun,
        Guid hardwareRun,
        ulong availableSystemMemoryBytes)
    {
        DateTimeOffset observedAt = DateTimeOffset.UtcNow;
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
}
