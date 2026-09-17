using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Presentation;

[TestClass]
public sealed class GgufRestoredCacheCompatibilityTests
{
    private const ulong GiB = 1024UL * 1024 * 1024;
    [TestMethod]
    [DataRow(GgufKvCacheFormat.F16)]
    [DataRow(GgufKvCacheFormat.Q8_0)]
    [DataRow(GgufKvCacheFormat.TurboQuant3Bit)]
    [DataRow(GgufKvCacheFormat.TurboQuant4Bit)]
    public void ExactReleasedCurrentCacheIsEvaluatedAndRetained(GgufKvCacheFormat cache)
    {
        var screen = CompatibilityEngine.EvaluateProduction(Input(cache, 12 * GiB)).Screen;
        Assert.AreEqual(CompatibilityScreenState.EstimatedCompatible, screen.State);
        Assert.IsTrue(screen.UseCurrentModelAvailable);
        Assert.AreEqual(cache, screen.CurrentSetup!.GgufKvCache);
    }

    [TestMethod]
    [DataRow(GgufKvCacheFormat.TurboQuant3Bit)]
    [DataRow(GgufKvCacheFormat.TurboQuant4Bit)]
    public void RestoredCacheCannotBypassFreshMemoryGate(GgufKvCacheFormat cache)
    {
        Assert.IsFalse(CompatibilityEngine.EvaluateProduction(Input(cache, 512 * 1024 * 1024)).Screen.UseCurrentModelAvailable);
    }

    [TestMethod]
    public void UnadmittedTurbo2IsNotSubstitutedWithF16()
    {
        var screen = CompatibilityEngine.EvaluateProduction(Input(GgufKvCacheFormat.TurboQuant2Bit, 12 * GiB)).Screen;
        Assert.IsFalse(screen.UseCurrentModelAvailable);
    }

    private static CompatibilityProductionInput Input(GgufKvCacheFormat cache, ulong memory)
    {
        const string modelHash = "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29";
        string digest = new('a', 64);
        Guid modelRun = Guid.NewGuid(), hardwareRun = Guid.NewGuid(), handoff = Guid.NewGuid();
        var model = GgufCompatibilityModelInput.Create(2_099_501_664, 40, 2560, 40, 8, 131072, 15, 2, 3_402_836_480);
        var current = CompatibilityCurrentModelInput.ForGguf(model, GgufRouteConfiguration.Create(
            GgufWeightFormat.Imported, cache, CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None));
        var records = VerifiedGgufOptimizationEvidence.Records(modelHash, model.FileLengthBytes, model.ParameterCount,
            "D53299AB05D5B31DB28BA4C233FC30798EECE1BA83EAA11CDED1B5727FFEADC1",
            PublishedGgufOptimizationEvidence.RuntimeBuildId, PublishedGgufOptimizationEvidence.RuntimeSourceCommit);
        var ids = records.Select(record => record.EvidenceId).ToHashSet(StringComparer.Ordinal);
        Assert.IsTrue(ids.Contains("GGUF-CURRENT-08EF-CPU-TURBO3-COMPAT-01"));
        var admissions = VerifiedGgufOptimizationEvidence.Admissions(ids);
        var runtime = GgufRuntimeAuthority.Create(PublishedGgufOptimizationEvidence.RuntimeBuildId,
            PublishedGgufOptimizationEvidence.RuntimeSourceCommit, VerifiedGgufOptimizationEvidence.ExecutionProfiles(ids));
        var snapshot = OptimizationCapabilitySnapshot.ForGguf("saved-cache", digest,
            GgufCapabilityPayload.Create(runtime.RuntimeBuildId, admissions,
                turboQuantImplementation: GgufTurboQuantImplementationIdentity.Create(runtime.RuntimeBuildId,
                    runtime.RuntimeSourceCommit, CompatibilityBackend.Cpu, DeviceRouteId.Cpu), runtimeAuthority: runtime));
        var hardware = CompatibilityHardwareInput.Create(TotalPhysicalMemory.FromBytes(16 * GiB), 0, 100 * GiB,
            [DeviceRouteId.Cpu], [CompatibilityBackend.Cpu]);
        var binding = OptimizationJourneyBinding.Create(modelRun.ToString("N"), handoff.ToString("N"), modelHash,
            model.FileLengthBytes, hardwareRun.ToString("N"), digest);
        var optimization = CompatibilityOptimizationProductionInput.Create(snapshot,
            OptimizationWorkload.Create("local-chat", 512, OptimizationAssessment.Acceptable, [ContextTokenCount.FromTokens(4096)]),
            binding, new HashSet<string>(), new OptimizationEvidenceCatalog(records));
        return CompatibilityProductionInput.Create(modelRun, hardwareRun, current,
            CompatibilityJourneyAuthorityInput.Create(handoff, modelHash, CompatibilityFactDigest.ComputeModel(current),
                digest, CompatibilityFactDigest.ComputeHardware(hardware)), hardware,
            CompatibilityFreshResourcesInput.Create(CurrentlyAvailableMemory.FromBytes(memory), 0, 100 * GiB, DateTimeOffset.UtcNow), optimization);
    }
}
