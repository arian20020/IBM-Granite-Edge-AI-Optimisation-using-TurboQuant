using System.Reflection;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using ContractCompiledCachePolicy = GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino.OpenVinoCompiledCachePolicy;
using RouteCompiledCachePolicy = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoCompiledCachePolicy;
using ExecutionWeightPrecision = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoWeightPrecision;
using OpenVinoKvCachePrecision = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoKvCachePrecision;
using OpenVinoWeightPrecision = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoWeightPrecision;

namespace GraniteEdgeAI.OpenVino.Tests.Optimization;

[TestClass]
[TestCategory("OpenVinoRoute")]
public sealed class OpenVinoOptimizationPlanAdapterTests
{
    private static readonly string[] ExpectedOptimizerKeys =
    [
        "nncf", "openvino", "openvino-genai", "optimum", "optimum-intel",
        "transformers"
    ];

    [TestMethod]
    public void AdapterExposesOnlyTheStrictSourceBoundEntryPoint()
    {
        MethodInfo[] methods = typeof(OpenVinoOptimizationPlanAdapter)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(static method => method.Name == nameof(OpenVinoOptimizationPlanAdapter.Adapt))
            .ToArray();

        Assert.AreEqual(1, methods.Length);
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(OptimizationExecutionPlan),
                typeof(OptimizationCapabilitySnapshot),
                typeof(OpenVinoOptimizationCapabilityEvidence),
                typeof(string),
                typeof(ulong)
            },
            methods[0].GetParameters()
                .Select(static parameter => parameter.ParameterType)
                .ToArray());
    }

    [TestMethod]
    public void AdapterMapsTheExactPlanWithoutAnObjectiveLookup()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan(
            OpenVinoWeightFormat.Int8,
            OpenVinoKvCacheFormat.U8,
            ContractCompiledCachePolicy.Disabled,
            streams: 1,
            contextTokens: 4_096);

        OpenVinoOptimizationAdaptation result =
            OpenVinoOptimizationPlanAdapter.Adapt(
                plan,
                plan.CapabilitySnapshot,
                OpenVinoOptimizationTestData.CurrentEvidence(plan),
                OpenVinoOptimizationTestData.SourceDigest,
                OpenVinoOptimizationTestData.SourceLength);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.Ready, result.Status);
        Assert.AreEqual(OptimizationSupportCode.None, result.SupportCode);
        Assert.IsNotNull(result.Candidate);
        Assert.AreEqual(OpenVinoWeightPrecision.EightBit,
            result.Candidate.WeightPrecision);
        Assert.AreEqual(OpenVinoKvCachePrecision.U8,
            result.Candidate.Runtime.KvCachePrecision);
        Assert.IsFalse(result.Candidate.Runtime.CompiledCache.Enabled);
        Assert.AreEqual("CPU", result.Candidate.Device);
        Assert.AreEqual(OpenVinoCapabilityPerformanceHint.Latency,
            result.Candidate.PerformanceHint);
        Assert.AreEqual(1, result.Candidate.Streams);
        Assert.AreEqual(4_096, result.Candidate.ContextTokens);
        Assert.AreEqual("OV-EXACT-01", result.Candidate.EvidenceId);
        Assert.IsNull(typeof(OpenVinoOptimizationCandidate).GetProperty("Objective"));
    }

    [TestMethod]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq4,
        GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoKvCachePrecision.Tbq4,
        OpenVinoKvCachePrecision.Tbq4)]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq3,
        GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoKvCachePrecision.Tbq3,
        OpenVinoKvCachePrecision.Tbq3)]
    public void AdapterKeepsTurboQuantRepresentationalWithoutFallback(
        OpenVinoKvCacheFormat cache,
        GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoKvCachePrecision executionCache,
        OpenVinoKvCachePrecision expected)
    {
        (OptimizationExecutionPlan plan,
            OpenVinoOptimizationCapabilityEvidence evidence) =
            OpenVinoOptimizationTestData.TurboPlan(cache, executionCache);

        OpenVinoOptimizationAdaptation result =
            OpenVinoOptimizationPlanAdapter.Adapt(
                plan,
                plan.CapabilitySnapshot,
                evidence,
                OpenVinoOptimizationTestData.SourceDigest,
                OpenVinoOptimizationTestData.SourceLength);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.ReplanRequired, result.Status);
        Assert.AreEqual(OptimizationSupportCode.ToolNotAdmitted, result.SupportCode);
        Assert.IsNull(result.Candidate);
    }

    [TestMethod]
    public void AdapterDoesNotIssueTurboQuantPlanWithoutVerifiedPackageActivation()
    {
        (OptimizationExecutionPlan plan,
            OpenVinoOptimizationCapabilityEvidence evidence) =
            OpenVinoOptimizationTestData.TurboPlan(
                OpenVinoKvCacheFormat.TurboQuantTbq4,
                GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution
                    .OpenVinoKvCachePrecision.Tbq4);

        OpenVinoOptimizationAdaptation result = OpenVinoOptimizationPlanAdapter.Adapt(
            plan, plan.CapabilitySnapshot, evidence,
            OpenVinoOptimizationTestData.SourceDigest,
            OpenVinoOptimizationTestData.SourceLength);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.ReplanRequired, result.Status);
        Assert.AreEqual(OptimizationSupportCode.ToolNotAdmitted, result.SupportCode);
        Assert.IsNull(result.Candidate);
    }

    [TestMethod]
    public void AdapterDerivesRuntimeOnlyOnlyFromEqualPayloadPrecisions()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan(
            OpenVinoWeightFormat.Original,
            OpenVinoKvCacheFormat.RouteDefault,
            ContractCompiledCachePolicy.Disabled,
            streams: 1,
            contextTokens: 4_096);

        OpenVinoOptimizationAdaptation result =
            OpenVinoOptimizationPlanAdapter.Adapt(
                plan,
                plan.CapabilitySnapshot,
                OpenVinoOptimizationTestData.CurrentEvidence(plan),
                OpenVinoOptimizationTestData.SourceDigest,
                OpenVinoOptimizationTestData.SourceLength);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.Ready, result.Status);
        Assert.IsNotNull(result.Candidate);
        Assert.AreEqual(OpenVinoWeightPrecision.Fp16,
            result.Candidate.WeightPrecision);
        Assert.AreEqual(OpenVinoWeightPrecision.Fp16,
            result.Candidate.SourceWeightPrecision);
        Assert.IsNull(result.Candidate.PersistentArtifact);
        Assert.IsFalse(plan.ProducesPersistentArtifact);
    }

    [TestMethod]
    public void AdapterCarriesEveryV2PayloadFieldIntoExecution()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan(
            OpenVinoWeightFormat.Int8,
            OpenVinoKvCacheFormat.U8,
            ContractCompiledCachePolicy.Disabled);

        OpenVinoOptimizationAdaptation result = OpenVinoOptimizationPlanAdapter.Adapt(
            plan, plan.CapabilitySnapshot,
            OpenVinoOptimizationTestData.CurrentEvidence(plan),
            OpenVinoOptimizationTestData.SourceDigest,
            OpenVinoOptimizationTestData.SourceLength);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.Ready, result.Status);
        OpenVinoExecutionPayload payload = plan.ExecutionPayload.OpenVino!;
        OpenVinoOptimizationCandidate candidate = result.Candidate!;
        OpenVinoExecutionPayload mappedPayload = candidate.ExecutionPayload!;
        Assert.AreSame(payload, candidate.ExecutionPayload);
        Assert.AreEqual(payload.ConfigurationId, candidate.ConfigurationId);
        Assert.AreEqual(payload.Device, candidate.Device);
        Assert.AreEqual(payload.Maturity, candidate.Maturity);
        Assert.AreEqual(payload.EvidenceId, candidate.EvidenceId);
        Assert.AreEqual(OpenVinoWeightPrecision.Fp16, candidate.SourceWeightPrecision);
        Assert.AreEqual(OpenVinoWeightPrecision.EightBit, candidate.WeightPrecision);
        Assert.AreEqual(OpenVinoKvCachePrecision.U8,
            candidate.Runtime.KvCachePrecision);
        Assert.AreEqual(payload.CompiledCacheEnabled,
            candidate.Runtime.CompiledCache.Enabled);
        Assert.AreEqual(payload.CompiledCacheIsDisposable,
            candidate.Runtime.CompiledCache.IsDisposable);
        Assert.AreEqual(payload.CompiledCacheIsModelArtifact,
            candidate.Runtime.CompiledCache.IsModelArtifact);
        Assert.AreEqual(payload.CreatesCompletePackage,
            candidate.PersistentArtifact!.CreatesCompletePackage);
        Assert.AreEqual("2026.3.0-22451-8a17657b995-releases/2026/3",
            mappedPayload.BuildIdentity.RuntimeBuild);
        Assert.AreEqual("2026.3.0.0-3277-bd8d6542e3c",
            mappedPayload.BuildIdentity.GenAiBuild);
        Assert.AreEqual("2026.3.0.0-703-183c6f25cda",
            mappedPayload.BuildIdentity.TokenizersBuild);
        Assert.AreEqual(new string('1', 64),
            mappedPayload.BuildIdentity.WorkerManifestDigest);
        CollectionAssert.AreEqual(
            ExpectedOptimizerKeys,
            mappedPayload.OptimizerVersions.Keys.ToArray());
        Assert.IsNull(mappedPayload.TurboQuantBuild);
    }

    [TestMethod]
    public void FrozenV2PlanIsNotExecutableByAV1Executor()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan();
        Assert.IsFalse(plan.IsExecutableBy(1));
        Assert.IsTrue(plan.IsExecutableBy(2));
        Assert.AreEqual(2, plan.ContractVersion);
    }

    [TestMethod]
    public void AdapterUsesTheCurrentExecutorWhileRetainingTheFrozenV2Floor()
    {
        MethodInfo adapt = typeof(OpenVinoOptimizationPlanAdapter).GetMethod(
            nameof(OpenVinoOptimizationPlanAdapter.Adapt),
            BindingFlags.Public | BindingFlags.Static)!;
        MethodInfo executableBy = typeof(OptimizationExecutionPlan).GetMethod(
            nameof(OptimizationExecutionPlan.IsExecutableBy))!;
        byte[] il = adapt.GetMethodBody()!.GetILAsByteArray()!;
        int callIndex = FindCall(il, adapt.Module, executableBy);

        Assert.IsGreaterThanOrEqualTo(1, callIndex,
            "Adapt must call OptimizationExecutionPlan.IsExecutableBy.");
        Assert.AreEqual((byte)0x19, il[callIndex - 1],
            "Adapt must pass the current contract version 3 to IsExecutableBy; " +
            "the plan contract retains the executable V2 floor.");
    }

    [TestMethod]
    [DataRow(OpenVinoWeightFormat.Original, OpenVinoKvCacheFormat.RouteDefault,
        "openvino.standard.cpu.original.default.v1")]
    [DataRow(OpenVinoWeightFormat.Fp16, OpenVinoKvCacheFormat.RouteDefault,
        "openvino.standard.cpu.fp16.default.v1")]
    [DataRow(OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.RouteDefault,
        "openvino.standard.cpu.int8.default.v1")]
    [DataRow(OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.U8,
        "openvino.standard.cpu.int8.u8.v1")]
    [DataRow(OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.U8,
        "openvino.standard.cpu.int4.u8.v1")]
    public void AdapterMapsTheFivePublishedConfigurationIds(
        OpenVinoWeightFormat weights,
        OpenVinoKvCacheFormat kvCache,
        string expectedConfigurationId)
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan(
            weights, kvCache, ContractCompiledCachePolicy.Disabled);

        OpenVinoOptimizationAdaptation result = OpenVinoOptimizationPlanAdapter.Adapt(
            plan,
            plan.CapabilitySnapshot,
            OpenVinoOptimizationTestData.CurrentEvidence(plan),
            OpenVinoOptimizationTestData.SourceDigest,
            OpenVinoOptimizationTestData.SourceLength);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.Ready, result.Status);
        Assert.AreEqual(expectedConfigurationId, result.Candidate!.ConfigurationId);
    }

    [TestMethod]
    public void CandidateRejectsConfigurationOutsideThePublishedRouteNamespace()
    {
        OpenVinoOptimizationCandidate candidate = new(
            "some.other.route.configuration",
            "CPU",
            OpenVinoWeightPrecision.EightBit,
            OpenVinoPersistentArtifact.Create(OpenVinoWeightPrecision.EightBit),
            new OpenVinoRuntimeOptimization(
                OpenVinoKvCachePrecision.U8,
                GraniteEdgeAI.Features.OpenVinoRoute.Optimization
                    .OpenVinoCompiledCachePolicy.Disabled),
            OpenVinoCapabilityPerformanceHint.Latency,
            Streams: 1,
            ContextTokens: 4_096,
            "Standard candidate",
            "OV-EXACT-01");

        Assert.ThrowsExactly<OpenVinoOptimizationException>(candidate.Validate);
    }

    [TestMethod]
    public void AdapterRejectsV1StylePlanWithNoExecutionPayload()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan();
        SetBackingField<OptimizationExecutionPayload?>(plan, "ExecutionPayload", null);

        AssertRejected(plan, OptimizationSupportCode.ModelBindingMismatch);
    }

    [TestMethod]
    public void AdapterRejectsOpenVinoUnionWithMissingRoutePayload()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan();
        SetBackingField<OpenVinoExecutionPayload?>(
            plan.ExecutionPayload, "OpenVino", null);

        AssertRejected(plan, OptimizationSupportCode.ModelBindingMismatch);
    }

    [TestMethod]
    public void AdapterRejectsGgufExecutionPayload()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan();
        SetBackingField(plan, "ExecutionPayload", GgufExecutionUnion());

        AssertRejected(plan, OptimizationSupportCode.ModelBindingMismatch);
    }

    [TestMethod]
    public void AdapterRejectsMixedExecutionPayloadUnion()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan();
        SetBackingField(plan.ExecutionPayload, "Gguf", GgufExecutionUnion().Gguf);

        AssertRejected(plan, OptimizationSupportCode.ModelBindingMismatch);
    }

    [TestMethod]
    public void AdapterRejectsPayloadTamperingInsteadOfReinterpretingCandidate()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan();
        SetBackingField(plan.ExecutionPayload.OpenVino!, "TargetWeightPrecision",
            GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution
                .OpenVinoWeightPrecision.FourBit);

        OpenVinoOptimizationAdaptation result = OpenVinoOptimizationPlanAdapter.Adapt(
            plan, plan.CapabilitySnapshot,
            OpenVinoOptimizationTestData.CurrentEvidence(plan),
            OpenVinoOptimizationTestData.SourceDigest,
            OpenVinoOptimizationTestData.SourceLength);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.ReplanRequired, result.Status);
        Assert.AreEqual(OptimizationSupportCode.ModelBindingMismatch, result.SupportCode);
        Assert.IsNull(result.Candidate);
    }

    [TestMethod]
    public void AdapterRejectsPayloadToolVersionsOutsideCurrentCapabilityEvidence()
    {
        Dictionary<string, string> versions =
            new(OpenVinoV2TestPayload.OptimizerVersions, StringComparer.Ordinal)
            {
                ["unadmitted-tool"] = "1.0.0"
            };
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan(
            executionPayload: OpenVinoV2TestPayload.For(
                OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.U8,
                ContractCompiledCachePolicy.Disabled,
                "OV-EXACT-01",
                optimizerVersions: versions));

        AssertRejected(plan, OptimizationSupportCode.ToolNotAdmitted);
    }

    [TestMethod]
    [DataRow(nameof(OpenVinoBuildEvidence.RuntimeBuild))]
    [DataRow(nameof(OpenVinoBuildEvidence.GenAiBuild))]
    [DataRow(nameof(OpenVinoBuildEvidence.TokenizersBuild))]
    [DataRow(nameof(OpenVinoBuildEvidence.WorkerManifestDigest))]
    public void AdapterRejectsAnyCurrentBuildIdentityMismatch(string field)
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan();
        OpenVinoOptimizationCapabilityEvidence current =
            OpenVinoOptimizationTestData.CurrentEvidence(plan);
        OpenVinoBuildEvidence changed = field switch
        {
            nameof(OpenVinoBuildEvidence.RuntimeBuild) =>
                current.Builds with { RuntimeBuild = "2026.3.0-other" },
            nameof(OpenVinoBuildEvidence.GenAiBuild) =>
                current.Builds with { GenAiBuild = "2026.3.0.0-other" },
            nameof(OpenVinoBuildEvidence.TokenizersBuild) =>
                current.Builds with { TokenizersBuild = "2026.3.0.0-other" },
            nameof(OpenVinoBuildEvidence.WorkerManifestDigest) =>
                current.Builds with { WorkerManifestDigest = new string('9', 64) },
            _ => throw new AssertFailedException(field)
        };

        AssertRejected(
            plan,
            OptimizationSupportCode.ToolNotAdmitted,
            current with { Builds = changed });
    }

    [TestMethod]
    public void AdapterRejectsCurrentTurboQuantBuildIdentity()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan();
        OpenVinoOptimizationCapabilityEvidence current =
            OpenVinoOptimizationTestData.CurrentEvidence(plan);
        OpenVinoBuildEvidence changed = current.Builds with
        {
            TurboQuantBuild = new TurboQuantBuildEvidence(
                new string('a', 40), new string('b', 40),
                new string('c', 64), new string('d', 64))
        };

        AssertRejected(
            plan,
            OptimizationSupportCode.ToolNotAdmitted,
            current with { Builds = changed });
    }

    [TestMethod]
    public void AdapterRejectsCurrentOptimizerVersionMismatch()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan();
        OpenVinoOptimizationCapabilityEvidence current =
            OpenVinoOptimizationTestData.CurrentEvidence(plan);

        AssertRejected(
            plan,
            OptimizationSupportCode.ToolNotAdmitted,
            current with
            {
                Versions = current.Versions with { Transformers = "5.5.5" }
            });
    }

    [TestMethod]
    public void AdapterRejectsUnexpectedTurboQuantBuildIdentity()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan();
        SetBackingField(
            plan.ExecutionPayload.OpenVino!,
            "TurboQuantBuild",
            TurboQuantBuildIdentity.Create(
                new string('a', 40), new string('b', 40),
                new string('c', 64), new string('e', 64)));

        AssertRejected(plan, OptimizationSupportCode.ModelBindingMismatch);
    }

    private static void SetBackingField<T>(object target, string property, T value) =>
        target.GetType().GetField($"<{property}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static OptimizationExecutionPayload GgufExecutionUnion() =>
        OptimizationExecutionPayload.ForGguf(GgufExecutionPayload.Create(
            "runtime-build-1", new string('a', 40), GgufRuntimeBackend.Cpu,
            "CPU", 4_096, GgufCacheType.F16, GgufCacheType.F16,
            gpuLayerCount: 0, flashAttention: false, threadCount: 4,
            batchSize: 64, "measured", "profile-1", maximumGeneratedTokens: 64,
            GgufWeightFormat.Imported));

    private static int FindCall(byte[] il, Module module, MethodInfo expected)
    {
        for (int index = 0; index <= il.Length - 5; index++)
        {
            if (il[index] is not (0x28 or 0x6f))
            {
                continue;
            }

            try
            {
                MethodBase? called = module.ResolveMethod(
                    BitConverter.ToInt32(il, index + 1));
                if (called?.Name == expected.Name &&
                    called.DeclaringType == expected.DeclaringType)
                {
                    return index;
                }
            }
            catch (Exception exception) when (exception is ArgumentException or
                                              BadImageFormatException)
            {
                // this byte was part of another instruction's operand
            }
        }

        return -1;
    }

    private static void AssertRejected(
        OptimizationExecutionPlan plan,
        OptimizationSupportCode expectedSupportCode,
        OpenVinoOptimizationCapabilityEvidence? currentEvidence = null)
    {
        OpenVinoOptimizationAdaptation result = OpenVinoOptimizationPlanAdapter.Adapt(
            plan, plan.CapabilitySnapshot,
            currentEvidence ?? OpenVinoOptimizationTestData.CurrentEvidence(plan),
            OpenVinoOptimizationTestData.SourceDigest,
            OpenVinoOptimizationTestData.SourceLength);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.ReplanRequired,
            result.Status);
        Assert.AreEqual(expectedSupportCode, result.SupportCode);
        Assert.IsNull(result.Candidate);
    }

    [TestMethod]
    [DataRow(OpenVinoWeightFormat.Fp16, OpenVinoKvCacheFormat.RouteDefault,
        ContractCompiledCachePolicy.Disabled, OpenVinoWeightPrecision.Fp16)]
    [DataRow(OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.U8,
        ContractCompiledCachePolicy.Disabled, OpenVinoWeightPrecision.EightBit)]
    [DataRow(OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.U8,
        ContractCompiledCachePolicy.Disabled, OpenVinoWeightPrecision.FourBit)]
    public void AdapterMapsEveryReleasedWeightPath(
        OpenVinoWeightFormat weights,
        OpenVinoKvCacheFormat kvCache,
        ContractCompiledCachePolicy compiledCache,
        OpenVinoWeightPrecision expected)
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan(
            weights,
            kvCache,
            compiledCache);

        OpenVinoOptimizationAdaptation result =
            OpenVinoOptimizationPlanAdapter.Adapt(
                plan,
                plan.CapabilitySnapshot,
                OpenVinoOptimizationTestData.CurrentEvidence(plan),
                OpenVinoOptimizationTestData.SourceDigest,
                OpenVinoOptimizationTestData.SourceLength);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.Ready, result.Status);
        Assert.IsNotNull(result.Candidate);
        Assert.AreEqual(expected, result.Candidate.WeightPrecision);
        if (expected == OpenVinoWeightPrecision.Fp16)
            Assert.IsNull(result.Candidate.PersistentArtifact);
        else
            Assert.AreEqual(expected, result.Candidate.PersistentArtifact.WeightPrecision);
    }

    [TestMethod]
    public void AdapterRejectsCapabilityDigestDrift()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan();
        OptimizationCapabilitySnapshot current =
            OpenVinoOptimizationTestData.Snapshot(
                plan.CapabilitySnapshot.OpenVino!,
                new string('4', 64));

        OpenVinoOptimizationAdaptation result =
            OpenVinoOptimizationPlanAdapter.Adapt(
                plan,
                current,
                OpenVinoOptimizationTestData.CurrentEvidence(plan),
                OpenVinoOptimizationTestData.SourceDigest,
                OpenVinoOptimizationTestData.SourceLength);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.ReplanRequired,
            result.Status);
        Assert.AreEqual(OptimizationSupportCode.CapabilityDrift, result.SupportCode);
        Assert.IsNull(result.Candidate);
    }

    [TestMethod]
    public void AdapterRejectsCapabilitySnapshotIdDrift()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan();
        OptimizationCapabilitySnapshot current =
            OpenVinoOptimizationTestData.Snapshot(
                plan.CapabilitySnapshot.OpenVino!,
                plan.CapabilitySnapshot.CapabilitySnapshotSha256,
                snapshotId: "ov-capability-different");

        OpenVinoOptimizationAdaptation result =
            OpenVinoOptimizationPlanAdapter.Adapt(
                plan,
                current,
                OpenVinoOptimizationTestData.CurrentEvidence(plan),
                OpenVinoOptimizationTestData.SourceDigest,
                OpenVinoOptimizationTestData.SourceLength);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.ReplanRequired,
            result.Status);
        Assert.AreEqual(OptimizationSupportCode.CapabilityDrift, result.SupportCode);
        Assert.IsNull(result.Candidate);
    }

    [TestMethod]
    public void AdapterRejectsChangedPayloadEvenWhenItsClaimedDigestMatches()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan();
        OpenVinoCapabilityPayload changedPayload =
            OpenVinoOptimizationTestData.Payload(
                OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.U8,
                ContractCompiledCachePolicy.Disabled,
                streams: 3,
                evidenceId: "OV-EXACT-01");
        OptimizationCapabilitySnapshot current =
            OpenVinoOptimizationTestData.Snapshot(
                changedPayload,
                plan.CapabilitySnapshot.CapabilitySnapshotSha256);

        OpenVinoOptimizationAdaptation result =
            OpenVinoOptimizationPlanAdapter.Adapt(
                plan,
                current,
                OpenVinoOptimizationTestData.CurrentEvidence(plan),
                OpenVinoOptimizationTestData.SourceDigest,
                OpenVinoOptimizationTestData.SourceLength);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.ReplanRequired,
            result.Status);
        Assert.AreEqual(OptimizationSupportCode.CapabilityDrift, result.SupportCode);
        Assert.IsNull(result.Candidate);
    }

    [TestMethod]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public void AdapterRejectsSourceDigestOrLengthDrift(
        bool matchingDigest,
        bool matchingLength)
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan();

        OpenVinoOptimizationAdaptation result =
            OpenVinoOptimizationPlanAdapter.Adapt(
                plan,
                plan.CapabilitySnapshot,
                OpenVinoOptimizationTestData.CurrentEvidence(plan),
                matchingDigest
                    ? OpenVinoOptimizationTestData.SourceDigest
                    : new string('9', 64),
                matchingLength
                    ? OpenVinoOptimizationTestData.SourceLength
                    : OpenVinoOptimizationTestData.SourceLength + 1);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.ReplanRequired,
            result.Status);
        Assert.AreEqual(OptimizationSupportCode.SourceIdentityMismatch,
            result.SupportCode);
        Assert.IsNull(result.Candidate);
    }

    [TestMethod]
    public void AdapterRejectsCandidateWithoutItsExactEvidenceRecord()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan(
            candidateEvidenceId: "OV-CANDIDATE-01",
            admittedEvidenceId: "OV-OTHER-01");

        OpenVinoOptimizationAdaptation result =
            OpenVinoOptimizationPlanAdapter.Adapt(
                plan,
                plan.CapabilitySnapshot,
                OpenVinoOptimizationTestData.CurrentEvidence(plan),
                OpenVinoOptimizationTestData.SourceDigest,
                OpenVinoOptimizationTestData.SourceLength);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.ReplanRequired,
            result.Status);
        Assert.AreEqual(OptimizationSupportCode.ToolNotAdmitted, result.SupportCode);
        Assert.IsNull(result.Candidate);
    }

    [TestMethod]
    public void AdapterRecomputesAndRejectsAChangedConfigurationDigest()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan();
        FieldInfo digest = typeof(OptimizationExecutionPlan).GetField(
            "<ConfigurationSha256>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        digest.SetValue(plan, new string('f', 64));

        OpenVinoOptimizationAdaptation result =
            OpenVinoOptimizationPlanAdapter.Adapt(
                plan,
                plan.CapabilitySnapshot,
                OpenVinoOptimizationTestData.CurrentEvidence(plan),
                OpenVinoOptimizationTestData.SourceDigest,
                OpenVinoOptimizationTestData.SourceLength);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.ReplanRequired,
            result.Status);
        Assert.AreEqual(OptimizationSupportCode.ModelBindingMismatch,
            result.SupportCode);
        Assert.IsNull(result.Candidate);
    }

    [TestMethod]
    public async Task ServiceFailsClosedForExactPlanWithUnappliedCompiledCache()
    {
        using PackageFixture package = PackageFixture.Create();
        OpenVinoStaticPackageEvidence source =
            new OpenVinoStaticPackageInspector().Inspect(package.Source).Evidence!;
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan(
            OpenVinoWeightFormat.Int8,
            OpenVinoKvCacheFormat.U8,
            ContractCompiledCachePolicy.Enabled,
            streams: 1,
            contextTokens: 4_096,
            sourceDigest: source.ModelSha256,
            sourceLength: checked((ulong)source.ModelLengthBytes));
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(pipeline, _ => true);

        OpenVinoOptimizationResult result = await service.OptimizeAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                OpenVinoOptimizationTestData.CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OpenVinoOptimizationStatus.ReplanRequired, result.Status);
        Assert.AreEqual(OptimizationSupportCode.ToolNotAdmitted,
            result.ReplanSupportCode);
        Assert.AreEqual(0, pipeline.Calls.Count);
        Assert.IsNull(pipeline.Candidate);
    }

    [TestMethod]
    public async Task ServiceReturnsReplanRequiredForSourceDriftBeforeStaging()
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan(
            sourceDigest: new string('8', 64));
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(pipeline, _ => true);

        OpenVinoOptimizationResult result = await service.OptimizeAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                OpenVinoOptimizationTestData.CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OpenVinoOptimizationStatus.ReplanRequired, result.Status);
        Assert.AreEqual(OptimizationSupportCode.SourceIdentityMismatch,
            result.ReplanSupportCode);
        Assert.AreEqual(0, pipeline.Calls.Count);
        Assert.IsFalse(Directory.Exists(package.Destination));
    }

    private static class OpenVinoOptimizationTestData
    {
        internal const string SourceDigest =
            "1111111111111111111111111111111111111111111111111111111111111111";
        internal const ulong SourceLength = 4UL * 1024 * 1024 * 1024;
        private const string CapabilityDigest =
            "3333333333333333333333333333333333333333333333333333333333333333";

        internal static OptimizationExecutionPlan Plan(
            OpenVinoWeightFormat weights = OpenVinoWeightFormat.Int8,
            OpenVinoKvCacheFormat kvCache = OpenVinoKvCacheFormat.U8,
            ContractCompiledCachePolicy compiledCache =
                ContractCompiledCachePolicy.Disabled,
            int streams = 1,
            int contextTokens = 4_096,
            string candidateEvidenceId = "OV-EXACT-01",
            string? admittedEvidenceId = null,
            string sourceDigest = SourceDigest,
            ulong sourceLength = SourceLength,
            OptimizationExecutionPayload? executionPayload = null)
        {
            OpenVinoRouteConfiguration configuration =
                OpenVinoRouteConfiguration.Create(
                    weights,
                    kvCache,
                    DeviceRouteId.Cpu,
                    OpenVinoPerformanceHint.Latency,
                    compiledCache,
                    streams);
            bool persistent = weights is OpenVinoWeightFormat.Int8 or
                OpenVinoWeightFormat.Int4;
            OptimizationCandidate candidate = OptimizationCandidate.Create(
                configuration,
                OptimizationCandidateMetrics.Create(
                    EvidenceGrade.Estimated,
                    OptimizationAssessment.Good,
                    OptimizationAssessment.Good,
                    OptimizationAssessment.Good,
                    contextTokens,
                    predictedPeakBytes: 2UL * 1024 * 1024 * 1024,
                    safeBudgetBytes: 8UL * 1024 * 1024 * 1024,
                    headroomBytes: 6UL * 1024 * 1024 * 1024,
                    workingDiskBytes: persistent ? sourceLength : 0,
                    outputDiskBytes: persistent ? sourceLength / 2 : 0,
                    requiresPersistentChange: persistent),
                candidateEvidenceId,
                isExperimental: false);
            OpenVinoCapabilityPayload payload = Payload(
                weights,
                kvCache,
                compiledCache,
                streams,
                admittedEvidenceId ?? candidateEvidenceId);
            OptimizationCapabilitySnapshot snapshot = Snapshot(payload, CapabilityDigest);
            return OpenVinoV2PlanTestFactory.Issue(
                candidate,
                executionPayload ?? OpenVinoV2TestPayload.For(
                    weights, kvCache, compiledCache, candidateEvidenceId),
                snapshot,
                OptimizationWorkload.Create(
                    "chat",
                    minimumContextTokens: 512,
                    OptimizationAssessment.Poor,
                    [ContextTokenCount.FromTokens(contextTokens)]),
                OptimizationJourneyBinding.Create(
                    "mi-run-1",
                    "mi-handoff-1",
                    sourceDigest,
                    sourceLength,
                    "hw-run-1",
                    new string('2', 64)),
                modelLayerCount: 1,
                DateTimeOffset.UnixEpoch);
        }

        internal static OpenVinoCapabilityPayload Payload(
            OpenVinoWeightFormat weights,
            OpenVinoKvCacheFormat kvCache,
            ContractCompiledCachePolicy compiledCache,
            int streams,
            string evidenceId) =>
            OpenVinoCapabilityPayload.Create(
                OpenVinoV2TestPayload.CapabilityRuntimeVersion,
                [
                    OpenVinoAdmittedConfiguration.Create(
                        evidenceId,
                        DeviceRouteId.Cpu,
                        weights,
                        kvCache,
                        OpenVinoPerformanceHint.Latency,
                        compiledCache,
                        streams,
                        minimumContextTokens: 4_096,
                        maximumContextTokens: 4_096,
                        SupportLevel.DeclaredSupported,
                        requiresEvidence: false)
                ]);

        internal static OptimizationCapabilitySnapshot Snapshot(
            OpenVinoCapabilityPayload payload,
            string digest,
            string snapshotId = "ov-capability-test-1") =>
            OptimizationCapabilitySnapshot.ForOpenVino(
                snapshotId,
                digest,
                payload);

        internal static FixedCurrentStateProvider CurrentStateProvider(
            OptimizationExecutionPlan plan) => new FixedCurrentStateProvider(new(
                plan.CapabilitySnapshot,
                CurrentEvidence(plan),
                plan.Binding.ModelInspectionRunId,
                plan.Binding.ModelInspectionHandoffId,
                plan.Binding.ProductHardwareRunId,
                plan.Binding.HardwareSnapshotSha256));

        internal static OpenVinoOptimizationCapabilityEvidence CurrentEvidence(
            OptimizationExecutionPlan plan)
        {
            OpenVinoAdmittedConfiguration admission =
                plan.CapabilitySnapshot.OpenVino!.Admitted.Single();
            return OpenVinoV2TestPayload.CapabilityEvidenceFor(
                admission.Weights,
                admission.KvCache,
                admission.CompiledCache,
                admission.EvidenceId,
                admission.Streams);
        }

        internal static (OptimizationExecutionPlan Plan,
            OpenVinoOptimizationCapabilityEvidence Evidence) TurboPlan(
            OpenVinoKvCacheFormat cache,
            GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoKvCachePrecision executionCache)
        {
            const string evidenceId = "OV-TURBO-CPU-01";
            string configurationId = cache == OpenVinoKvCacheFormat.TurboQuantTbq3
                ? "openvino.turboquant.cpu.int4.tbq3.v1"
                : "openvino.turboquant.cpu.int4.tbq4.v1";
            var turboEvidence = new TurboQuantBuildEvidence(
                "f5f594dc0c9e5961785f0d17743486d52eac87e7",
                "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
                new string('a', 64), new string('b', 64));
            var turboIdentity = TurboQuantBuildIdentity.Create(
                turboEvidence.SourceCommit,
                turboEvidence.ImplementationCommit,
                turboEvidence.PatchSeriesDigest,
                turboEvidence.RuntimeManifestDigest);
            OpenVinoBuildIdentity buildIdentity = OpenVinoBuildIdentity.Create(
                "2026.5.0-22950-f5f594dc0c9",
                "6fbc103538d30d42da4b0b5130a4792a20f728ba",
                "2026.5.0",
                new string('1', 64));
            IReadOnlyDictionary<string, string> versions =
                OpenVinoV2TestPayload.OptimizerVersions;
            OpenVinoAdmittedConfiguration admission =
                OpenVinoAdmittedConfiguration.Create(
                    evidenceId,
                    DeviceRouteId.Cpu,
                    OpenVinoWeightFormat.Int4,
                    cache,
                    OpenVinoPerformanceHint.Latency,
                    ContractCompiledCachePolicy.Disabled,
                    1, 4_096, 4_096,
                    SupportLevel.Experimental,
                    requiresEvidence: true);
            OpenVinoCapabilityPayload capability = OpenVinoCapabilityPayload.Create(
                buildIdentity.RuntimeBuild,
                [admission],
                [OpenVinoExecutionAuthority.Create(
                    evidenceId,
                    configurationId,
                    ExecutionWeightPrecision.Fp16,
                    buildIdentity,
                    versions,
                    compiledCacheIsDisposable: true,
                    turboIdentity)]);
            OptimizationCapabilitySnapshot snapshot = Snapshot(
                capability, CapabilityDigest);
            OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
                "mi-run-1", "mi-handoff-1", SourceDigest, SourceLength,
                "hw-run-1", new string('2', 64));
            OptimizationWorkload workload = OptimizationWorkload.Create(
                "chat", 512, OptimizationAssessment.Poor,
                [ContextTokenCount.FromTokens(4_096)]);
            const ulong gib = 1024UL * 1024 * 1024;
            OptimizationHardwareAuthority hardware =
                OptimizationHardwareAuthority.Create(
                    new string('a', 64),
                    [DeviceRouteId.Cpu],
                    [CompatibilityBackend.OpenVinoCpu],
                    ByteCount.FromBytes(64 * gib),
                    DateTimeOffset.UnixEpoch,
                    DateTimeOffset.UnixEpoch,
                    OptimizationFreshnessPolicy.Version);
            CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
                snapshot,
                InspectedModelFacts.Create(
                    ByteCount.FromBytes(3 * gib), 32, 4_096, 32, 8, 8_192, 15, 2),
                workload,
                binding,
                ByteCount.FromBytes(32 * gib),
                ByteCount.FromBytes(64 * gib),
                EstimatorPolicy.ProvisionalV1(),
                new HashSet<string> { evidenceId },
                hardware);
            Assert.HasCount(0, generated.Candidates,
                "The production frontier must keep unmeasured TurboQuant candidates closed.");

            OpenVinoRouteConfiguration configuration =
                OpenVinoRouteConfiguration.Create(
                    OpenVinoWeightFormat.Int4,
                    cache,
                    DeviceRouteId.Cpu,
                    OpenVinoPerformanceHint.Latency,
                    ContractCompiledCachePolicy.Disabled,
                    streams: 1);
            OptimizationCandidateMetrics metrics =
                OptimizationCandidateMetrics.Create(
                    EvidenceGrade.Measured,
                    OptimizationAssessment.Acceptable,
                    OptimizationAssessment.Good,
                    OptimizationAssessment.Good,
                    contextTokens: 4_096,
                    predictedPeakBytes: 2 * gib,
                    safeBudgetBytes: 32 * gib,
                    headroomBytes: 30 * gib,
                    workingDiskBytes: 3 * gib,
                    outputDiskBytes: 2 * gib,
                    requiresPersistentChange: true,
                    availableDiskBytes: 64 * gib);
            OptimizationEvidenceRecord syntheticEvidence = new(
                evidenceId,
                new OptimizationEvidenceKey(
                    OptimizationEvidenceModelFamily.Granite,
                    SourceDigest,
                    3_000_000_000,
                    OptimizationRoute.OpenVino,
                    "synthetic-openvino-adapter-tbq-v1",
                    "int4",
                    "int4",
                    cache == OpenVinoKvCacheFormat.TurboQuantTbq3
                        ? "tbq3"
                        : "tbq4",
                    OptimizationEvidenceBackend.OpenVinoCpu,
                    OptimizationEvidenceDeviceClass.Cpu,
                    4_096,
                    "chat",
                    "synthetic-openvino-adapter-method-v1",
                    "synthetic-openvino-adapter-memory-v1",
                    "openvino-cpu"),
                new OptimizationQualityScore(4m),
                OutputHealthPassed: true,
                StabilityPassed: true,
                ActivationPassed: true,
                IntegrityPassed: true);
            OptimizationCandidate candidate = OptimizationCandidate.Create(
                configuration,
                metrics,
                syntheticEvidence,
                isExperimental: true);
            candidate = OptimizationCandidate.AttachAdmissionProof(
                candidate,
                OptimizationAdmissionProof.Create(
                    snapshot,
                    workload,
                    binding,
                    candidate,
                    admission.Level,
                    admission.RequiresEvidence,
                    new HashSet<string> { evidenceId },
                    OptimizationIssuanceAuthority.FromGeneration(
                        hardware,
                        ByteCount.FromBytes(32 * gib),
                        ByteCount.FromBytes(64 * gib))));
            OpenVinoExecutionPayload openVinoPayload = OpenVinoExecutionPayload.Create(
                configurationId,
                "CPU",
                "Experimental candidate",
                evidenceId,
                ExecutionWeightPrecision.Fp16,
                ExecutionWeightPrecision.FourBit,
                executionCache,
                compiledCacheEnabled: false,
                compiledCacheIsDisposable: true,
                compiledCacheIsModelArtifact: false,
                createsCompletePackage: true,
                buildIdentity,
                versions,
                turboIdentity,
                OpenVinoKvCacheAlgorithm.TurboQuant);
            OptimizationExecutionPayload payload =
                OptimizationExecutionPayload.ForOpenVino(openVinoPayload);
            string configurationSha = OptimizationCanonicalizer.ConfigurationSha256(
                candidate, payload, OptimizationExecutionPlan.CurrentContractVersion);
            OptimizationExecutionPlan plan = new(
                OptimizationExecutionPlan.CurrentContractVersion,
                Guid.NewGuid(),
                binding,
                snapshot,
                workload,
                candidate,
                payload,
                OptimizationPreferenceSelection.Manual(50),
                sharedWithAdjacentBand: false,
                configurationSha,
                DateTimeOffset.UnixEpoch);
            OpenVinoOptimizationCapabilityEvidence evidence = new(
                new OpenVinoBuildEvidence(
                    buildIdentity.RuntimeBuild,
                    buildIdentity.GenAiBuild,
                    buildIdentity.TokenizersBuild,
                    buildIdentity.WorkerManifestDigest,
                    turboEvidence),
                OpenVinoV2TestPayload.ToolVersions(),
                [new OpenVinoOptimizationCapabilityAdmission(
                    evidenceId,
                    "CPU",
                    OpenVinoWeightPrecision.FourBit,
                    new OpenVinoRuntimeOptimization(
                        cache == OpenVinoKvCacheFormat.TurboQuantTbq3
                            ? OpenVinoKvCachePrecision.Tbq3
                            : OpenVinoKvCachePrecision.Tbq4,
                        RouteCompiledCachePolicy.Disabled),
                    OpenVinoCapabilityPerformanceHint.Latency,
                    1, 4_096, 4_096,
                    OpenVinoCapabilityMaturity.Experimental)]);
            return (plan, evidence);
        }
    }

    private sealed class FixedCurrentStateProvider(
        OpenVinoOptimizationCurrentState currentState) :
        IOpenVinoOptimizationCurrentStateProvider
    {
        public ValueTask<OpenVinoOptimizationCurrentState> GetCurrentStateAsync(
            OpenVinoOptimizationCheckpoint checkpoint,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(currentState);
        }
    }

    private sealed class RecordingOptimizationPipeline : IOpenVinoOptimizationPipeline
    {
        internal List<string> Calls { get; } = [];
        internal OpenVinoOptimizationCandidate? Candidate { get; private set; }

        public Task<OpenVinoOptimizationCompletion> OptimizeAsync(
            OpenVinoOptimizationInvocation invocation,
            CancellationToken cancellationToken)
        {
            Calls.Add("optimize");
            Candidate = invocation.Candidate;
            File.Copy(
                Path.Combine(invocation.SourceDirectory, "openvino_model.xml"),
                Path.Combine(invocation.StagingDirectory, "openvino_model.xml"));
            File.WriteAllBytes(
                Path.Combine(invocation.StagingDirectory, "openvino_model.bin"),
                [1, 2, 3]);
            File.Copy(
                Path.Combine(invocation.SourceDirectory, "config.json"),
                Path.Combine(invocation.StagingDirectory, "config.json"));
            return Task.FromResult(OpenVinoOptimizationCompletion.CreateTestInstance(
                invocation.Candidate.WeightPrecision));
        }

        public Task<OpenVinoOptimizationValidation> ValidateAsync(
            string stagingDirectory,
            OpenVinoOptimizationCandidate candidate,
            CancellationToken cancellationToken)
        {
            Calls.Add("validate");
            return Task.FromResult(OpenVinoOptimizationValidation.CreateTestInstance());
        }

        public Task<OpenVinoRuntimeOptimizationEvidence> SmokeAsync(
            OpenVinoOptimizationValidation validation,
            string stagingDirectory,
            OpenVinoOptimizationCandidate candidate,
            CancellationToken cancellationToken)
        {
            Calls.Add("smoke");
            return Task.FromResult(new OpenVinoRuntimeOptimizationEvidence(
                "CPU",
                candidate.Runtime.KvCachePrecision,
                GenerationDisposition: "passed",
                QualityDisposition: "passed"));
        }

        public Task<Guid> ReinspectPublishedAsync(
            string destinationDirectory,
            CancellationToken cancellationToken)
        {
            Calls.Add("reinspect");
            return Task.FromResult(Guid.NewGuid());
        }
    }

    private sealed class PackageFixture : IDisposable
    {
        private PackageFixture(string root, string source, string destination)
        {
            Root = root;
            Source = source;
            Destination = destination;
        }

        internal string Root { get; }
        internal string Source { get; }
        internal string Destination { get; }

        internal static PackageFixture Create()
        {
            string fixture = Path.Combine(
                AppContext.BaseDirectory,
                "TestFixtures",
                "OpenVINO",
                "GenAI",
                "TinySyntheticV1",
                "package");
            string root = Path.Combine(
                Path.GetTempPath(),
                "ov-plan-adapter-" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "source");
            string output = Path.Combine(root, "output");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(output);
            foreach (string file in Directory.EnumerateFiles(
                fixture,
                "*",
                SearchOption.AllDirectories))
            {
                string destination = Path.Combine(
                    source,
                    Path.GetRelativePath(fixture, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }

            return new PackageFixture(
                root,
                source,
                Path.Combine(output, "optimized"));
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
