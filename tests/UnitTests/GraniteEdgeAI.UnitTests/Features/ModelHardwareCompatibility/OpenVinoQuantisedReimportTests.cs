using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
public sealed class OpenVinoQuantisedReimportTests
{
    [TestMethod]
    [DataRow(OpenVinoKvCacheFormat.U4, "u4")]
    [DataRow(OpenVinoKvCacheFormat.U8, "u8")]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq3, "tbq3")]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq4, "tbq4")]
    [DataRow(OpenVinoKvCacheFormat.RouteDefault, "released-default")]
    public async Task RestoredCurrentCacheFlowsToExactChatPayload(OpenVinoKvCacheFormat cache, string expected)
    {
        const string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        Guid hardwareId = Guid.NewGuid();
        var hardware = HardwareInspectionHandoff.Create(hardwareId, HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(hardwareId));
        var evidence = new OpenVinoStaticPackageEvidence(1, hash, hash, 1_704_489_303,
            "granite", "GraniteForCausalLM", "text-generation-with-past", 131072,
            "float16", "PreTrainedTokenizerFast", 10, true, 40, 2560, 40, 8, WeightPrecision: "int4",
            SavedRuntimeConfiguration: new("CPU",
                GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoCapabilityPerformanceHint.Latency,
                1, 4096, false, cache switch
                {
                    OpenVinoKvCacheFormat.U4 => GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoKvCachePrecision.U4,
                    OpenVinoKvCacheFormat.U8 => GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoKvCachePrecision.U8,
                    OpenVinoKvCacheFormat.TurboQuantTbq3 => GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoKvCachePrecision.Tbq3,
                    OpenVinoKvCacheFormat.TurboQuantTbq4 => GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoKvCachePrecision.Tbq4,
                    _ => GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoKvCachePrecision.ReleasedDefault
                }));
        var model = new ModelInspectionHandoff(ModelInspectionHandoff.CurrentSchemaVersion,
            Guid.NewGuid(), Guid.NewGuid(), GraniteEdgeAI.Features.ModelInspection.Contracts.ModelInspectionOutcome.Ready,
            hash, evidence.ModelLengthBytes);
        Assert.IsTrue(OpenVinoCompatibilityInputProjector.TryPrepare(model, evidence, hardwareId, hardware, out var source));
        var prepared = source!;
        Assert.AreEqual(cache, prepared.Configuration.KvCache);
        Assert.IsFalse(OpenVinoCompatibilityInputProjector.TryPrepare(model,
            evidence with { SavedRuntimeConfiguration = evidence.SavedRuntimeConfiguration! with { ContextTokens = 8192 } },
            hardwareId, hardware, out _), "Do not silently change a saved context to fit the current estimator.");
        var released = new OpenVinoBuildEvidence(VerifiedOpenVinoOptimizationEvidence.RuntimeBuild,
            VerifiedOpenVinoOptimizationEvidence.GenAiBuild, VerifiedOpenVinoOptimizationEvidence.TokenizersBuild,
            VerifiedOpenVinoOptimizationEvidence.CurrentOfficialWorkerManifestSha256);
        var turbo = new OpenVinoBuildEvidence(VerifiedOpenVinoOptimizationEvidence.TurboRuntimeBuild,
            VerifiedOpenVinoOptimizationEvidence.TurboGenAiBuild, VerifiedOpenVinoOptimizationEvidence.TurboTokenizersBuild,
            VerifiedOpenVinoOptimizationEvidence.TurboWorkerManifestSha256,
            new GraniteEdgeAI.OpenVino.Contracts.TurboQuantBuildEvidence(
                "f5f594dc0c9e5961785f0d17743486d52eac87e7", "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
                VerifiedOpenVinoOptimizationEvidence.TurboPatchSeriesSha256,
                VerifiedOpenVinoOptimizationEvidence.TurboRuntimeManifestSha256));
        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(prepared, released, turbo, true, out var authority));
        var now = DateTimeOffset.UtcNow;
        var evaluation = authority!.Evaluate(CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(8UL << 30), null, 64UL << 30, now),
            new HashSet<string>(), now, CancellationToken.None);
        Assert.AreEqual(CompatibilityScreenState.EstimatedCompatible, evaluation.Screen.State);
        Assert.IsNull(evaluation.PlanningSession);
        using var custody = new ModelSourceCustodyRegistry();
        using var registry = new CurrentModelChatLaunchRegistry(custody);
        var launcher = new CapturingLauncher();
        registry.RegisterRoute(launcher);
        custody.Register(new ModelSourceCustodyRecord(new ModelSourceCustodyKey(model.ModelInspectionHandoffId,
            hash, evidence.ModelLengthBytes, OptimizationRoute.OpenVino), @"C:\fixture\restored"));
        var handoff = authority.ResolveCurrentModel(evaluation, registry);
        Assert.IsNotNull(handoff);
        Assert.IsTrue(registry.TryGetExecutionPayload(handoff, out var exactPayload));
        Assert.IsTrue(registry.TryGetOpenVinoRuntimeOptions(handoff, out var runtimeOptions));
        Assert.AreEqual(expected, runtimeOptions!.KvCachePrecision);
        using (var unregistered = new CurrentModelChatLaunchRegistry(custody))
        {
            Assert.IsFalse(unregistered.TryGetExecutionPayload(handoff, out _));
            Assert.IsFalse(unregistered.TryGetOpenVinoRuntimeOptions(handoff, out _),
                "A valid-looking handoff without its registered exact payload cannot select a runtime.");
        }
        Assert.IsTrue((await registry.LaunchAsync(handoff, CancellationToken.None)).Succeeded);
        Assert.AreSame(exactPayload, launcher.Context!.ExactExecutionPayload,
            "UI labels and session activation must consume the identical registered payload.");
        Assert.AreEqual(expected, launcher.Context!.ExactExecutionPayload.OpenVino!.KvCachePrecision switch
        {
            GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoKvCachePrecision.U4 => "u4",
            GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoKvCachePrecision.U8 => "u8",
            GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoKvCachePrecision.Tbq3 => "tbq3",
            GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoKvCachePrecision.Tbq4 => "tbq4",
            _ => "released-default"
        });
        var lowRam = authority.Evaluate(CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(128UL << 20), null, 64UL << 30, now),
            new HashSet<string>(), now, CancellationToken.None);
        Assert.IsFalse(lowRam.Screen.UseCurrentModelAvailable);
        if (cache is OpenVinoKvCacheFormat.TurboQuantTbq3 or OpenVinoKvCacheFormat.TurboQuantTbq4)
            Assert.IsFalse(OpenVinoOptimizationProductionAuthority.TryCreate(prepared, released, null, true, out _),
                "Never fall back from a saved TurboQuant selection to the official/default worker.");
        registry.Dispose();
        Assert.IsFalse(registry.TryGetOpenVinoRuntimeOptions(handoff, out _));
    }

    [TestMethod]
    [DataRow("int4", OpenVinoWeightPrecision.FourBit, false)]
    [DataRow("int8", OpenVinoWeightPrecision.EightBit, false)]
    [DataRow("mxfp4", OpenVinoWeightPrecision.MxFp4, false)]
    [DataRow("float16", OpenVinoWeightPrecision.Fp16, false)]
    [DataRow("int4", OpenVinoWeightPrecision.FourBit, true)]
    public async Task InspectedQuantisedReimportUsesExistingBytesAndOffersCurrentChatOnly(
        string weightPrecision, OpenVinoWeightPrecision expectedPrecision, bool actualOutputIdentity)
    {
        string hash = actualOutputIdentity ? VerifiedOpenVinoOptimizationEvidence.OptimizedModelSha256
            : "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        const string manifest = "a089cec936067e8ac2941b57fcf34a2e0cfef87219af8501fcab0946e6613a7d";
        var evidence = new OpenVinoStaticPackageEvidence(1, manifest, hash, 1_704_489_303,
            "granite", "GraniteForCausalLM", "text-generation-with-past", 131072,
            "float16", "PreTrainedTokenizerFast", 10, true,
            40, 2560, 40, 8, WeightPrecision: weightPrecision);
        Guid hardwareId = Guid.NewGuid();
        var model = new ModelInspectionHandoff(ModelInspectionHandoff.CurrentSchemaVersion,
            Guid.NewGuid(), Guid.NewGuid(), GraniteEdgeAI.Features.ModelInspection.Contracts.ModelInspectionOutcome.Ready, hash, evidence.ModelLengthBytes);
        var hardware = HardwareInspectionHandoff.Create(hardwareId, HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(hardwareId));
        Assert.IsTrue(OpenVinoCompatibilityInputProjector.TryPrepare(model, evidence, hardwareId,
            hardware, out var prepared));
        Assert.IsNotNull(prepared);
        Assert.AreEqual(OpenVinoWeightFormat.Original, prepared.Configuration.Weights,
            "Already compressed file bytes must not be multiplied by a second compression ratio.");
        Assert.AreEqual(expectedPrecision, prepared.SourceWeightPrecision);
        if (!actualOutputIdentity)
            Assert.IsNull(prepared.Model.ParameterCount, "Do not invent conversion evidence from model shape.");
        var builds = new OpenVinoBuildEvidence(VerifiedOpenVinoOptimizationEvidence.RuntimeBuild,
            VerifiedOpenVinoOptimizationEvidence.GenAiBuild, VerifiedOpenVinoOptimizationEvidence.TokenizersBuild,
            VerifiedOpenVinoOptimizationEvidence.CurrentOfficialWorkerManifestSha256);
        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(prepared, builds, true, out var authority));
        Assert.IsNotNull(authority);
        var now = DateTimeOffset.UtcNow;
        var fresh = CompatibilityFreshResourcesInput.Create(CurrentlyAvailableMemory.FromBytes(8UL << 30),
            null, 64UL << 30, now);
        var evaluated = authority.Evaluate(fresh, new HashSet<string>(), now, CancellationToken.None);
        Assert.AreEqual(CompatibilityScreenState.EstimatedCompatible, evaluated.Screen.State);
        Assert.IsTrue(evaluated.Screen.UseCurrentModelAvailable);
        Assert.IsNull(evaluated.PlanningSession, "No conversion quality evidence means no optimisation plan.");
        using var custody = new ModelSourceCustodyRegistry();
        using var registry = new CurrentModelChatLaunchRegistry(custody);
        var launcher = new CapturingLauncher();
        Assert.IsTrue(registry.RegisterRoute(launcher));
        var handoff = authority.ResolveCurrentModel(evaluated, registry);
        Assert.IsNotNull(handoff);
        Assert.AreEqual(hash, handoff.ModelSha256);
        Assert.AreEqual(evidence.ModelLengthBytes, handoff.ModelLengthBytes);
        Assert.AreEqual(CurrentModelChatSupportCode.SourceUnavailable,
            (await registry.LaunchAsync(handoff, CancellationToken.None)).SupportCode);
        Assert.IsTrue(custody.Register(new ModelSourceCustodyRecord(new ModelSourceCustodyKey(
            model.ModelInspectionHandoffId, hash, evidence.ModelLengthBytes, OptimizationRoute.OpenVino),
            @"C:\fixture\inspected-model")));
        Assert.IsTrue((await registry.LaunchAsync(handoff, CancellationToken.None)).Succeeded);
        var payload = launcher.Context!.ExactExecutionPayload.OpenVino!;
        Assert.AreEqual(expectedPrecision, payload.SourceWeightPrecision);
        Assert.AreEqual(expectedPrecision, payload.TargetWeightPrecision);
        Assert.AreEqual(GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoKvCachePrecision.ReleasedDefault,
            payload.KvCachePrecision, "Reimport must not invent a previously selected TurboQuant cache.");
        Assert.IsFalse(payload.CreatesCompletePackage);

        var reimport = new PreparedOpenVinoCompatibilityInput(Guid.NewGuid(), Guid.NewGuid(),
            prepared.ModelSha256, Guid.NewGuid(), prepared.HardwareSnapshotSha256, prepared.Model,
            prepared.Configuration, prepared.Hardware, prepared.SourcePrecision,
            prepared.SourceWeightPrecision, prepared.PackageManifestSha256);
        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(reimport, builds, true, out var nextAuthority));
        var nextEvaluation = nextAuthority!.Evaluate(fresh, new HashSet<string>(), now, CancellationToken.None);
        var nextHandoff = nextAuthority.ResolveCurrentModel(nextEvaluation, registry);
        Assert.IsNotNull(nextHandoff, "Reimporting the same format must not collide with an older inspection's launch context.");
        Assert.AreNotEqual(handoff.CompatibilityDecisionId, nextHandoff.CompatibilityDecisionId);
        Assert.AreEqual(handoff.RuntimeConfigurationSha256, nextHandoff.RuntimeConfigurationSha256);

        var insufficient = authority.Evaluate(CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(128UL << 20), null, 64UL << 30, now),
            new HashSet<string>(), now, CancellationToken.None);
        Assert.IsFalse(insufficient.Screen.UseCurrentModelAvailable);
        Assert.IsNull(authority.ResolveCurrentModel(insufficient, registry));

        var unknownBuild = builds with { WorkerManifestDigest = new string('a', 64) };
        Assert.IsTrue(OpenVinoOptimizationProductionAuthority.TryCreate(prepared, unknownBuild, true, out var untrusted));
        var rejected = untrusted!.Evaluate(fresh, new HashSet<string>(), now, CancellationToken.None);
        Assert.AreEqual(CompatibilityScreenState.NotEstablished, rejected.Screen.State);
        Assert.IsFalse(rejected.Screen.UseCurrentModelAvailable);
    }

    private sealed class CapturingLauncher : ICurrentModelChatRouteLauncher
    {
        public OptimizationRoute Route => OptimizationRoute.OpenVino;
        internal CurrentModelLaunchContext? Context { get; private set; }
        public Task<CurrentModelChatLaunchResult> LaunchAsync(CurrentModelLaunchContext context,
            ModelSourceLease sourceLease, CancellationToken cancellationToken)
        {
            Context = context;
            return Task.FromResult(CurrentModelChatLaunchResult.Success);
        }
    }
}
