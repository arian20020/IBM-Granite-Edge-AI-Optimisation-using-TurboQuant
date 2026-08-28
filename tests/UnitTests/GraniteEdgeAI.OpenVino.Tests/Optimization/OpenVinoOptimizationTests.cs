using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.OpenVino.Contracts;
using ContractCompiledCachePolicy = GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino.OpenVinoCompiledCachePolicy;
using ExecutionWeightPrecision = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoWeightPrecision;

namespace GraniteEdgeAI.OpenVino.Tests.Optimization;

[TestClass]
[TestCategory("OpenVinoRoute")]
public sealed class OpenVinoOptimizationTests
{
    private static readonly string[] ExpectedPipelineCalls =
        ["optimize", "validate", "smoke", "reinspect"];
    private static readonly string[] ExpectedPrePublishCalls =
        ["optimize", "validate", "smoke"];
    private static readonly OpenVinoOptimizationCheckpoint[] ExpectedCheckpoints =
    [
        OpenVinoOptimizationCheckpoint.InitialPreflight,
        OpenVinoOptimizationCheckpoint.BeforeStaging,
        OpenVinoOptimizationCheckpoint.BeforePublish
    ];
    private static readonly string[] ExpectedBoundaryEvents =
    [
        "state:InitialPreflight",
        "state:BeforeStaging",
        "optimize",
        "validate",
        "smoke",
        "state:BeforePublish"
    ];

    [TestMethod]
    public void TrustedSourceVerificationDominatesPersistentAndRuntimeWork()
    {
        IReadOnlyList<ProductionCall> reveal = ProductionCalls(
            "RevealTrustedPackageRoot");
        AssertCallOrder(
            reveal,
            (typeof(GraniteEdgeAI.ModelHardwareCompatibility.Core.Application
                .Optimization.Execution.TrustedSourceContext), "Verify"),
            (typeof(GraniteEdgeAI.ModelHardwareCompatibility.Core.Application
                .Optimization.Execution.TrustedSourceContext), "RevealVerifiedPath"));

        IReadOnlyList<ProductionCall> revalidate = ProductionCalls(
            "RevalidatePlanAsync");
        AssertCallOrder(
            revalidate,
            (typeof(IOpenVinoOptimizationCurrentStateProvider),
                nameof(IOpenVinoOptimizationCurrentStateProvider.GetCurrentStateAsync)),
            (typeof(OpenVinoOptimizationService), "ValidateCurrentBinding"),
            (typeof(OpenVinoOptimizationService), "RevealTrustedPackageRoot"),
            (typeof(OpenVinoOptimizationPlanAdapter),
                nameof(OpenVinoOptimizationPlanAdapter.Adapt)));

        IReadOnlyList<ProductionCall> persistent = ProductionCalls(
            "OptimizeCoreAsync");
        int[] persistentRevalidations = CallOffsets(
            persistent,
            typeof(OpenVinoOptimizationService),
            "RevalidatePlanAsync");
        Assert.HasCount(2, persistentRevalidations);
        AssertOffsetsOrdered(
            persistentRevalidations[0],
            SingleCallOffset(
                persistent,
                typeof(GraniteEdgeAI.Features.OpenVinoRoute.Conversion
                    .ConversionTransaction),
                "Create"),
            SingleCallOffset(
                persistent,
                typeof(IOpenVinoOptimizationPipeline),
                nameof(IOpenVinoOptimizationPipeline.OptimizeAsync)),
            persistentRevalidations[1],
            SingleCallOffset(
                persistent,
                typeof(GraniteEdgeAI.Features.OpenVinoRoute.Conversion
                    .ConversionTransaction),
                "Publish"));

        IReadOnlyList<ProductionCall> runtime = ProductionCalls(
            "StoreRuntimeProfileAsync");
        int[] runtimeRevalidations = CallOffsets(
            runtime,
            typeof(OpenVinoOptimizationService),
            "RevalidatePlanAsync");
        Assert.HasCount(2, runtimeRevalidations);
        AssertOffsetsOrdered(
            runtimeRevalidations[0],
            SingleCallOffset(
                runtime,
                typeof(GraniteEdgeAI.Features.OpenVinoRoute.Conversion
                    .ConversionTransaction),
                "Create"),
            runtimeRevalidations[1],
            SingleCallOffset(
                runtime,
                typeof(GraniteEdgeAI.Features.OpenVinoRoute.Conversion
                    .ConversionTransaction),
                "Publish"));
    }

    [TestMethod]
    public void ObjectivesMapToExactClosedCpuCandidates()
    {
        (OpenVinoOptimizationObjective Objective,
            OpenVinoWeightPrecision Weight,
            OpenVinoKvCachePrecision Kv)[] expected =
        [
            (OpenVinoOptimizationObjective.Automatic,
                OpenVinoWeightPrecision.EightBit, OpenVinoKvCachePrecision.ReleasedDefault),
            (OpenVinoOptimizationObjective.Quality,
                OpenVinoWeightPrecision.Fp16, OpenVinoKvCachePrecision.ReleasedDefault),
            (OpenVinoOptimizationObjective.Balanced,
                OpenVinoWeightPrecision.EightBit, OpenVinoKvCachePrecision.U8),
            (OpenVinoOptimizationObjective.Efficiency,
                OpenVinoWeightPrecision.FourBit, OpenVinoKvCachePrecision.U8)
        ];

        foreach ((OpenVinoOptimizationObjective objective,
                     OpenVinoWeightPrecision weight,
                     OpenVinoKvCachePrecision kv) in expected)
        {
            OpenVinoOptimizationCandidate candidate =
                OpenVinoOptimizationLegacyRegistryV1.GetRequired(objective);
            Assert.AreEqual(objective, candidate.LegacyObjectiveV1);
            Assert.AreEqual(weight, candidate.PersistentArtifact.WeightPrecision);
            Assert.AreEqual(kv, candidate.Runtime.KvCachePrecision);
            Assert.AreEqual("CPU", candidate.Device);
            candidate.Validate();
        }
        Assert.AreEqual(4, OpenVinoOptimizationLegacyRegistryV1.Candidates.Count);
    }

    [TestMethod]
    public void PersistentWeightsRuntimeKvAndCompiledCacheRemainDistinct()
    {
        OpenVinoOptimizationCandidate candidate =
            OpenVinoOptimizationLegacyRegistryV1.GetRequired(
                OpenVinoOptimizationObjective.Balanced);

        Assert.IsTrue(candidate.PersistentArtifact.CreatesCompletePackage);
        Assert.IsFalse(candidate.Runtime.CreatesModelArtifact);
        Assert.IsTrue(candidate.Runtime.CompiledCache.IsDisposable);
        Assert.IsFalse(candidate.Runtime.CompiledCache.IsModelArtifact);
        Assert.AreEqual(OpenVinoKvCachePrecision.U8, candidate.Runtime.KvCachePrecision);
    }

    [TestMethod]
    public void SparseLargeOpenVinoArtifactHashingUsesBoundedMemory()
    {
        const long sparseLength = 64L * 1024 * 1024;
        string root = Path.Combine(
            Path.GetTempPath(),
            "ov-sparse-hash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using (FileStream model = new(
                       Path.Combine(root, "openvino_model.bin"),
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                model.SetLength(sparseLength);
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetTotalAllocatedBytes(precise: true);

            IReadOnlyList<OpenVinoOutputArtifact> output =
                OpenVinoOptimizationProvenance.CaptureOutput(root);
            long allocated = GC.GetTotalAllocatedBytes(precise: true) - before;

            Assert.HasCount(1, output);
            Assert.AreEqual(sparseLength, output[0].Length);
            Assert.IsLessThan(8L * 1024 * 1024, allocated);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void CandidatesContainNoGgufQuantizationOrLlamaCppFlags()
    {
        string json = JsonSerializer.Serialize(
            OpenVinoOptimizationLegacyRegistryV1.Candidates);
        foreach (string forbidden in new[]
        {
            "gguf", "q4_k", "q8_0", "llama.cpp", "--cache-type-k", "--cache-type-v"
        })
        {
            Assert.IsFalse(json.Contains(forbidden, StringComparison.OrdinalIgnoreCase), forbidden);
        }
    }

    [TestMethod]
    public void CompiledCacheKeyBindsEveryRequiredIdentityWithoutDisclosingIt()
    {
        OpenVinoCompiledCacheIdentity identity = new(
            "openvino-2026.3.0",
            "cpu-plugin-2026.3.0",
            "CPU",
            "driver-10.0.1",
            new string('a', 64),
            "openvino.standard.cpu.int8.u8.v1");

        string key = identity.ComputeDirectoryKey();
        OpenVinoCompiledCacheIdentity changed = identity with { DriverIdentity = "driver-10.0.2" };

        Assert.AreEqual(64, key.Length);
        Assert.IsTrue(key.All(static character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f')));
        Assert.AreNotEqual(key, changed.ComputeDirectoryKey());
        Assert.IsFalse(key.Contains("driver", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void CompressedPackageCannotBePresentedAsRestoredFp16()
    {
        Assert.ThrowsExactly<OpenVinoOptimizationException>(() =>
            OpenVinoPersistentArtifact.Create(
                OpenVinoWeightPrecision.Fp16,
                sourcePrecision: OpenVinoWeightPrecision.EightBit));
    }

    [TestMethod]
    public async Task LegacyEnabledCacheCandidateFailsClosedBeforePipeline()
    {
        using PackageFixture package = PackageFixture.Create();
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(pipeline, _ => true);
        OpenVinoOptimizationCandidate candidate =
            OpenVinoOptimizationLegacyRegistryV1.GetRequired(
                OpenVinoOptimizationObjective.Balanced);

        OpenVinoOptimizationResult result = await service.OptimizeLegacyV1Async(
            new OpenVinoOptimizationLegacyRequestV1(
                package.Source,
                package.Destination,
                candidate,
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OpenVinoOptimizationStatus.Failed, result.Status);
        Assert.AreEqual(OpenVinoSupportCode.OptimizationUnsupported,
            result.SupportCode);
        Assert.AreEqual(0, pipeline.Calls.Count);
        Assert.IsFalse(Directory.Exists(package.Destination));
        Assert.AreEqual(0, Directory.EnumerateDirectories(
            Path.GetDirectoryName(package.Destination)!,
            ".granite-openvino-*.staging").Count());
    }

    [TestMethod]
    public async Task PublishedProvenanceBindsEveryPlanIdentityAndReturnsPersistentC1Result()
    {
        using PackageFixture package = PackageFixture.Create();
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(pipeline, _ => true);
        OptimizationExecutionPlan plan = CreatePlan(package.Source);

        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);
        Assert.AreEqual(
            OptimizationExecutionStatus.SucceededPersistent,
            result.Status,
            $"Execution={result.SupportCode}");
        OpenVinoOptimizationProvenance provenance =
            OpenVinoOptimizationProvenance.Read(package.Destination);
        Assert.AreEqual(plan.OptimizationPlanId, result.OptimizationPlanId);
        Assert.AreEqual(provenance.OperationId, result.ExecutionId);
        Assert.AreEqual(plan.ConfigurationSha256, result.ConfigurationSha256);
        Assert.AreEqual(plan.Binding.ModelSha256, result.SourceSha256);
        Assert.IsTrue(result.SourceUnchanged);
        Assert.IsNotNull(result.OutputIdentity);
        Assert.IsNotNull(result.OutputManifestSha256);
        Assert.IsGreaterThan(0UL, result.OutputSizeBytes);
        Assert.AreEqual(plan.OptimizationPlanId, provenance.OptimizationPlanId);
        Assert.AreEqual(plan.ConfigurationSha256, provenance.ConfigurationSha256);
        Assert.AreEqual(plan.CapabilitySnapshot.SnapshotId,
            provenance.CapabilitySnapshotId);
        Assert.AreEqual(plan.CapabilitySnapshot.CapabilitySnapshotSha256,
            provenance.CapabilitySnapshotSha256);
        Assert.AreEqual(plan.Binding.ModelInspectionRunId,
            provenance.ModelInspectionRunId);
        Assert.AreEqual(plan.Binding.ModelInspectionHandoffId,
            provenance.ModelInspectionHandoffId);
        Assert.AreEqual(plan.Binding.ModelSha256, provenance.ModelSha256);
        Assert.AreEqual(plan.Binding.ModelLengthBytes, provenance.ModelLengthBytes);
        Assert.AreEqual(plan.Binding.ProductHardwareRunId,
            provenance.ProductHardwareRunId);
        Assert.AreEqual(plan.Binding.HardwareSnapshotSha256,
            provenance.HardwareSnapshotSha256);
        CollectionAssert.AreEqual(ExpectedPipelineCalls, pipeline.Calls.ToArray());
        Assert.AreEqual(plan.ContractVersion, provenance.PlanContext!.ContractVersion);
        Assert.AreEqual(plan.Route, provenance.PlanContext.Route);
        Assert.AreEqual(plan.Workload.WorkloadId, provenance.PlanContext.WorkloadId);
        Assert.AreEqual(plan.Preference.Kind, provenance.PlanContext.PreferenceKind);
        Assert.AreEqual(plan.Preference.PreferenceValue,
            provenance.PlanContext.PreferenceValue);
        Assert.AreEqual(plan.CreatedAtUtc, provenance.PlanContext.PlanCreatedAtUtc);
        Assert.AreEqual(64, provenance.ExecutionConfigurationSha256!.Length);
        Assert.AreEqual(64, provenance.PlanBindingSha256!.Length);
        Assert.AreSame(
            plan.ExecutionPayload.OpenVino,
            pipeline.Invocations.Single().Candidate.ExecutionPayload);
        Assert.AreEqual(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(package.Source)),
            pipeline.Invocations.Single().SourceDirectory);
        Assert.AreEqual(2, provenance.ExecutorContractVersion);
        AssertPayloadBinding(
            plan.ExecutionPayload.OpenVino!,
            provenance.OpenVinoExecutionPayloadJson,
            provenance.OpenVinoExecutionPayloadSha256);
        Assert.IsFalse(File.Exists(Path.Combine(
            package.Destination, OpenVinoRuntimeOptimizationProfile.FileName)));

        string provenancePath = Path.Combine(
            package.Destination, OpenVinoOptimizationProvenance.FileName);
        File.WriteAllText(
            provenancePath,
            File.ReadAllText(provenancePath).Replace(
                "\"compiledCacheEnabled\": false",
                "\"compiledCacheEnabled\": true",
                StringComparison.Ordinal));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            OpenVinoOptimizationProvenance.Read(package.Destination));

        Assert.ThrowsExactly<InvalidDataException>(() =>
            (provenance with
            {
                OpenVinoExecutionPayloadJson =
                    provenance.OpenVinoExecutionPayloadJson + " "
            }).Write(package.Destination));
    }

    [TestMethod]
    public async Task RawEightBitV2PlanUsesAuthoritativePayloadSourcePrecision()
    {
        using PackageFixture package = PackageFixture.Create();
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(pipeline, _ => true);
        OptimizationExecutionPlan plan = CreatePlan(
            package.Source,
            OpenVinoWeightFormat.Int4,
            OpenVinoKvCacheFormat.U8,
            sourcePrecision: ExecutionWeightPrecision.EightBit);

        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.SucceededPersistent, result.Status);
        OpenVinoOptimizationProvenance provenance =
            OpenVinoOptimizationProvenance.Read(package.Destination);
        Assert.AreEqual(OpenVinoWeightPrecision.EightBit,
            provenance.SourceWeightPrecision);
        Assert.AreEqual(OpenVinoWeightPrecision.FourBit,
            provenance.TargetWeightPrecision);
        Assert.AreEqual(OpenVinoWeightPrecision.EightBit,
            pipeline.Invocations.Single().Candidate.SourceWeightPrecision);
        AssertPayloadBinding(
            plan.ExecutionPayload.OpenVino!,
            provenance.OpenVinoExecutionPayloadJson,
            provenance.OpenVinoExecutionPayloadSha256);
    }

    [TestMethod]
    public async Task RawFourBitV2RuntimePlanUsesAuthoritativePayloadSourcePrecision()
    {
        using PackageFixture package = PackageFixture.Create();
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(pipeline, _ => true);
        OptimizationExecutionPlan plan = CreatePlan(
            package.Source,
            OpenVinoWeightFormat.Int4,
            OpenVinoKvCacheFormat.U8,
            sourcePrecision: ExecutionWeightPrecision.FourBit);

        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.SucceededRuntimeProfile,
            result.Status);
        OpenVinoRuntimeOptimizationProfile profile =
            OpenVinoRuntimeOptimizationProfile.Read(package.Destination);
        Assert.AreEqual(result.ExecutionId, profile.ExecutionId);
        Assert.AreEqual(OpenVinoWeightPrecision.FourBit, profile.WeightPrecision);
        var registry = new OpenVinoPublishedOutputRegistry(
            Path.GetDirectoryName(package.Destination)!);
        Assert.IsTrue(registry.TryRegister(result, package.Destination));
        Assert.IsTrue(registry.TryResolve(
            result,
            out OpenVinoPublishedOutput? published));
        Assert.IsNotNull(published);
        Assert.AreEqual(OpenVinoRuntimeOptions.U8, published.RuntimeOptions);
        using var sourceLease = new CancellationTokenSource();
        using var chatTarget = new OpenVinoOptimizationChatTarget(
            result,
            published.Kind,
            package.Source,
            published.RuntimeOptions,
            sourceLease);
        Assert.AreEqual(result.ExecutionId, chatTarget.ExecutionId);
        Assert.AreEqual(OpenVinoRuntimeOptions.U8, chatTarget.RuntimeOptions);
        Assert.IsFalse(chatTarget.CanExportModel);
        AssertPayloadBinding(
            plan.ExecutionPayload.OpenVino!,
            profile.OpenVinoExecutionPayloadJson,
            profile.OpenVinoExecutionPayloadSha256);
        Assert.AreEqual(0, pipeline.Calls.Count);
    }

    [TestMethod]
    public async Task ExistingProvenancePrecisionMismatchStillFailsClosed()
    {
        using PackageFixture package = PackageFixture.Create();
        WriteValidOptimizationProvenance(package.Source);
        OptimizationExecutionPlan plan = CreatePlan(
            package.Source,
            OpenVinoWeightFormat.Int4,
            OpenVinoKvCacheFormat.U8,
            sourcePrecision: ExecutionWeightPrecision.EightBit);
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(pipeline, _ => true);

        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.ReplanRequired, result.Status);
        Assert.AreEqual(OptimizationSupportCode.SourceIdentityMismatch,
            result.SupportCode);
        Assert.AreEqual(0, pipeline.Calls.Count);
        Assert.IsFalse(Directory.Exists(package.Destination));
    }

    [TestMethod]
    public async Task RuntimeOnlyOriginalStoresBoundProfileWithoutModelPublication()
    {
        using PackageFixture package = PackageFixture.Create();
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(pipeline, _ => false);
        List<OpenVinoOptimizationStage> stages = [];
        OptimizationExecutionPlan plan = CreatePlan(
            package.Source,
            OpenVinoWeightFormat.Original,
            OpenVinoKvCacheFormat.RouteDefault);

        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            new InlineProgress<OpenVinoOptimizationProgress>(
                value => stages.Add(value.Stage)),
            CancellationToken.None);
        Assert.AreEqual(OptimizationExecutionStatus.SucceededRuntimeProfile,
            result.Status);
        OpenVinoRuntimeOptimizationProfile profile =
            OpenVinoRuntimeOptimizationProfile.Read(package.Destination);

        Assert.IsFalse(result.ProducedPersistentArtifact);
        Assert.AreEqual(plan.OptimizationPlanId, profile.OptimizationPlanId);
        Assert.AreEqual(plan.ConfigurationSha256, profile.ConfigurationSha256);
        Assert.AreEqual(plan.Binding.ModelSha256, profile.ModelSha256);
        Assert.AreEqual(plan.Binding.ProductHardwareRunId,
            profile.ProductHardwareRunId);
        Assert.AreEqual(plan.CapabilitySnapshot.CapabilitySnapshotSha256,
            profile.CapabilitySnapshotSha256);
        Assert.AreEqual(plan.ContractVersion, profile.PlanContext.ContractVersion);
        Assert.AreEqual(plan.Workload.WorkloadId, profile.PlanContext.WorkloadId);
        Assert.AreEqual(plan.Preference.Kind, profile.PlanContext.PreferenceKind);
        Assert.AreEqual(plan.CreatedAtUtc, profile.PlanContext.PlanCreatedAtUtc);
        Assert.AreEqual(2, profile.ExecutorContractVersion);
        AssertPayloadBinding(
            plan.ExecutionPayload.OpenVino!,
            profile.OpenVinoExecutionPayloadJson,
            profile.OpenVinoExecutionPayloadSha256);
        Assert.AreEqual(0, pipeline.Calls.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                OpenVinoOptimizationStage.Preflight,
                OpenVinoOptimizationStage.Completed
            },
            stages);
        Assert.IsFalse(File.Exists(Path.Combine(
            package.Destination, "openvino_model.bin")));
        Assert.IsFalse(File.Exists(Path.Combine(
            package.Destination, OpenVinoOptimizationProvenance.FileName)));
    }

    [TestMethod]
    public async Task RuntimeProfileRegistryResolvesOnlyTheExactSuccessfulResult()
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = CreatePlan(
            package.Source,
            OpenVinoWeightFormat.Original,
            OpenVinoKvCacheFormat.RouteDefault);
        OpenVinoOptimizationService service = new(
            new RecordingOptimizationPipeline(), _ => false);
        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);
        var registry = new OpenVinoPublishedOutputRegistry(
            Path.GetDirectoryName(package.Destination)!);

        Assert.IsTrue(registry.TryRegister(result, package.Destination));
        Assert.IsFalse(registry.TryRegister(result, package.Destination));
        Assert.IsTrue(registry.TryResolve(
            result,
            out OpenVinoPublishedOutput? publication));
        Assert.IsNotNull(publication);
        Assert.AreEqual(
            OpenVinoPublishedOutputKind.RuntimeConfiguration,
            publication.Kind);
        Assert.IsNull(publication.PersistentDirectory);
        Assert.IsNotNull(publication.RuntimeProfile);
        Assert.AreEqual(
            plan.ConfigurationSha256,
            publication.RuntimeProfile.ConfigurationSha256);
        Assert.AreEqual(
            OpenVinoRuntimeOptions.ReleasedDefault,
            publication.RuntimeOptions);
        Assert.IsFalse(publication.IsPersistentArtifact);

        OptimizationExecutionResult substitutedExecution =
            OptimizationExecutionResult.Succeeded(
                plan,
                result.OutputIdentity!,
                result.OutputManifestSha256!,
                result.OutputSizeBytes,
                sourceUnchanged: true,
                DateTimeOffset.UtcNow);
        Assert.AreNotEqual(result.ExecutionId, substitutedExecution.ExecutionId);
        Assert.IsFalse(registry.TryResolve(substitutedExecution, out _));
        var freshRegistry = new OpenVinoPublishedOutputRegistry(
            Path.GetDirectoryName(package.Destination)!);
        Assert.IsFalse(freshRegistry.TryRegister(
            substitutedExecution,
            package.Destination));

        OptimizationExecutionResult differentLength =
            OptimizationExecutionResult.Succeeded(
                plan,
                result.OutputIdentity!,
                result.OutputManifestSha256!,
                checked(result.OutputSizeBytes + 1),
                sourceUnchanged: true,
                DateTimeOffset.UtcNow);
        Assert.IsFalse(registry.TryResolve(differentLength, out _));
    }

    [TestMethod]
    public async Task RuntimeProfileRegistryRejectsPublicationMutationAtConsumption()
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = CreatePlan(
            package.Source,
            OpenVinoWeightFormat.Original,
            OpenVinoKvCacheFormat.RouteDefault);
        OpenVinoOptimizationService service = new(
            new RecordingOptimizationPipeline(), _ => false);
        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);
        var registry = new OpenVinoPublishedOutputRegistry(
            Path.GetDirectoryName(package.Destination)!);
        Assert.IsTrue(registry.TryRegister(result, package.Destination));
        Assert.IsTrue(registry.TryResolve(result, out _));

        string profilePath = Path.Combine(
            package.Destination,
            OpenVinoRuntimeOptimizationProfile.FileName);
        byte[] profile = File.ReadAllBytes(profilePath);
        profile[^1] ^= 1;
        File.WriteAllBytes(profilePath, profile);

        Assert.IsFalse(registry.TryResolve(result, out _));
    }

    [TestMethod]
    public async Task RuntimeConfigurationCannotBeExportedAsAModelArtifact()
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = CreatePlan(
            package.Source,
            OpenVinoWeightFormat.Original,
            OpenVinoKvCacheFormat.RouteDefault);
        OpenVinoOptimizationService service = new(
            new RecordingOptimizationPipeline(), _ => false);
        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);
        var registry = new OpenVinoPublishedOutputRegistry(
            Path.GetDirectoryName(package.Destination)!);
        Assert.IsTrue(registry.TryRegister(result, package.Destination));
        string export = Path.Combine(package.Root, "output", "runtime-export");

        Assert.IsFalse(await registry.ExportPersistentAsync(
            result,
            export,
            maximumBytes: 1024 * 1024,
            CancellationToken.None));
        Assert.IsFalse(Directory.Exists(export));
    }

    [TestMethod]
    public async Task PersistentExportRequiresExactExecutionAndAtomicVerification()
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = CreatePlan(package.Source);
        OpenVinoOptimizationService service = new(
            new RecordingOptimizationPipeline(), _ => true);
        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);
        var registry = new OpenVinoPublishedOutputRegistry(
            Path.GetDirectoryName(package.Destination)!);
        Assert.IsTrue(registry.TryRegister(result, package.Destination));
        OptimizationExecutionResult substituted = OptimizationExecutionResult.Succeeded(
            plan,
            result.OutputIdentity!,
            result.OutputManifestSha256!,
            result.OutputSizeBytes,
            sourceUnchanged: true,
            DateTimeOffset.UtcNow);
        string export = Path.Combine(package.Root, "output", "persistent-export");

        Assert.IsFalse(await registry.ExportPersistentAsync(
            substituted,
            export,
            maximumBytes: 1024 * 1024,
            CancellationToken.None));
        Assert.IsFalse(Directory.Exists(export));
        Assert.IsTrue(await registry.ExportPersistentAsync(
            result,
            export,
            maximumBytes: 1024 * 1024,
            CancellationToken.None));
        Assert.IsTrue(Directory.Exists(export));
        Assert.IsFalse(Directory.EnumerateDirectories(
            Path.GetDirectoryName(export)!,
            ".export-*.tmp").Any());
        var exportedRegistry = new OpenVinoPublishedOutputRegistry(
            Path.GetDirectoryName(export)!);
        Assert.IsTrue(exportedRegistry.TryRegister(result, export));
        Assert.IsTrue(exportedRegistry.TryResolve(result, out _));
    }

    [TestMethod]
    public async Task CancelledPersistentExportLeavesNoDestinationOrTemporaryDirectory()
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = CreatePlan(package.Source);
        OpenVinoOptimizationService service = new(
            new RecordingOptimizationPipeline(), _ => true);
        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);
        string outputRoot = Path.GetDirectoryName(package.Destination)!;
        var registry = new OpenVinoPublishedOutputRegistry(outputRoot);
        Assert.IsTrue(registry.TryRegister(result, package.Destination));
        string export = Path.Combine(outputRoot, "cancelled-export");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await registry.ExportPersistentAsync(
                result,
                export,
                maximumBytes: 1024 * 1024,
                cancellation.Token));

        Assert.IsFalse(Directory.Exists(export));
        Assert.IsFalse(Directory.EnumerateDirectories(
            outputRoot,
            ".export-*.tmp").Any());
    }

    [TestMethod]
    public async Task PersistentRegistryRejectsChangedModelBytesAtConsumption()
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = CreatePlan(package.Source);
        OpenVinoOptimizationService service = new(
            new RecordingOptimizationPipeline(), _ => true);
        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);
        var registry = new OpenVinoPublishedOutputRegistry(
            Path.GetDirectoryName(package.Destination)!);

        Assert.AreEqual(
            OptimizationExecutionStatus.SucceededPersistent,
            result.Status);
        OptimizationExecutionResult substitutedExecution =
            OptimizationExecutionResult.Succeeded(
                plan,
                result.OutputIdentity!,
                result.OutputManifestSha256!,
                result.OutputSizeBytes,
                sourceUnchanged: true,
                DateTimeOffset.UtcNow);
        Assert.IsFalse(registry.TryRegister(
            substitutedExecution,
            package.Destination));
        Assert.IsTrue(registry.TryRegister(result, package.Destination));
        Assert.IsTrue(registry.TryResolve(
            result,
            out OpenVinoPublishedOutput? publication));
        Assert.IsNotNull(publication);
        Assert.IsTrue(publication.IsPersistentArtifact);
        Assert.AreEqual(
            OpenVinoPublishedOutputKind.PersistentPackage,
            publication.Kind);
        Assert.AreEqual(package.Destination, publication.PersistentDirectory);
        Assert.IsNull(publication.RuntimeProfile);

        OpenVinoOptimizationProvenance provenance =
            OpenVinoOptimizationProvenance.Read(package.Destination);
        string outputPath = Path.Combine(
            package.Destination,
            provenance.OutputFiles[0].Path);
        byte[] bytes = File.ReadAllBytes(outputPath);
        bytes[^1] ^= 1;
        File.WriteAllBytes(outputPath, bytes);

        Assert.IsFalse(registry.TryResolve(result, out _));
    }

    [TestMethod]
    public async Task PersistentRegistryRejectsChangedProvenanceBytesAtConsumption()
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = CreatePlan(package.Source);
        OpenVinoOptimizationService service = new(
            new RecordingOptimizationPipeline(), _ => true);
        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);
        var registry = new OpenVinoPublishedOutputRegistry(
            Path.GetDirectoryName(package.Destination)!);

        Assert.IsTrue(registry.TryRegister(result, package.Destination));
        Assert.IsTrue(registry.TryResolve(result, out _));

        File.AppendAllText(
            Path.Combine(
                package.Destination,
                OpenVinoOptimizationProvenance.FileName),
            " ");

        Assert.IsFalse(registry.TryResolve(result, out _));
    }

    [TestMethod]
    public async Task TrustedSourceMismatchWinsBeforePackageInspectionOrPipelineAccess()
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = CreatePlan(package.Source);
        string modelPath = Path.Combine(package.Source, "openvino_model.bin");
        byte[] changed = File.ReadAllBytes(modelPath);
        changed[0] ^= 0xff;
        File.WriteAllBytes(modelPath, changed);
        File.Delete(Path.Combine(package.Source, "config.json"));
        RecordingOptimizationPipeline pipeline = new();
        SequenceCurrentStateProvider currentStateProvider = CurrentStateProvider(plan);
        OpenVinoOptimizationService service = new(pipeline, _ => true);

        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                currentStateProvider,
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.ReplanRequired, result.Status);
        Assert.AreEqual(OptimizationSupportCode.SourceIdentityMismatch,
            result.SupportCode);
        CollectionAssert.AreEqual(
            new[] { OpenVinoOptimizationCheckpoint.InitialPreflight },
            currentStateProvider.Checkpoints.ToArray());
        Assert.AreEqual(0, pipeline.Calls.Count);
        Assert.IsFalse(Directory.Exists(package.Destination));
    }

    [TestMethod]
    [DataRow(OpenVinoOptimizationCheckpoint.BeforeStaging, 0)]
    [DataRow(OpenVinoOptimizationCheckpoint.BeforePublish, 3)]
    public async Task TrustedSourceBoundaryValidationRunsImmediatelyBeforeNativeOrPublish(
        OpenVinoOptimizationCheckpoint observedAt,
        int expectedPipelineCalls)
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = CreatePlan(package.Source);
        OpenVinoOptimizationCurrentState current = CurrentState(plan);
        RecordingOptimizationPipeline pipeline = new();
        bool boundaryObserved = false;
        SequenceCurrentStateProvider currentStateProvider = new(
            [current, current, current],
            beforeReturn: checkpoint =>
            {
                if (checkpoint != observedAt)
                {
                    return;
                }
                boundaryObserved = true;
                Assert.AreEqual(expectedPipelineCalls, pipeline.Calls.Count);
                Assert.IsFalse(Directory.Exists(package.Destination));
            });
        OpenVinoOptimizationService service = new(pipeline, _ => true);

        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                currentStateProvider,
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.SucceededPersistent, result.Status);
        Assert.IsTrue(boundaryObserved);
        CollectionAssert.AreEqual(ExpectedPipelineCalls, pipeline.Calls.ToArray());
        Assert.IsTrue(Directory.Exists(package.Destination));
    }

    [TestMethod]
    [DataRow(2, 4_096, ContractCompiledCachePolicy.Disabled)]
    [DataRow(1, 2_048, ContractCompiledCachePolicy.Disabled)]
    [DataRow(1, 4_096, ContractCompiledCachePolicy.Enabled)]
    public async Task UnappliedRuntimeTechnicalFieldsFailClosed(
        int streams,
        int contextTokens,
        ContractCompiledCachePolicy compiledCache)
    {
        using PackageFixture package = PackageFixture.Create();
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(pipeline, _ => true);
        OptimizationExecutionPlan plan = CreatePlan(
            package.Source,
            streams: streams,
            contextTokens: contextTokens,
            compiledCache: compiledCache);

        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.ReplanRequired, result.Status);
        Assert.AreEqual(OptimizationSupportCode.ToolNotAdmitted, result.SupportCode);
        Assert.IsNull(result.OutputIdentity);
        Assert.IsNull(result.OutputManifestSha256);
        Assert.AreEqual(0UL, result.OutputSizeBytes);
        Assert.AreEqual(0, pipeline.Calls.Count);
        Assert.IsFalse(Directory.Exists(package.Destination));
    }

    [TestMethod]
    public async Task CapabilityDriftAfterSmokeReplansBeforePublish()
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = CreatePlan(package.Source);
        OpenVinoOptimizationCurrentState initial = CurrentState(plan);
        OptimizationCapabilitySnapshot changedCapabilities =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-capability-drifted",
                plan.CapabilitySnapshot.CapabilitySnapshotSha256,
                plan.CapabilitySnapshot.OpenVino!);
        OpenVinoOptimizationCurrentState changed = initial with
        {
            Capabilities = changedCapabilities
        };
        List<string> events = [];
        SequenceCurrentStateProvider currentStateProvider = new(
            [initial, initial, changed], events);
        RecordingOptimizationPipeline pipeline = new(events);
        OpenVinoOptimizationService service = new(pipeline, _ => true);

        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                currentStateProvider,
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.ReplanRequired, result.Status);
        Assert.AreEqual(OptimizationSupportCode.CapabilityDrift, result.SupportCode);
        Assert.IsNull(result.OutputIdentity);
        Assert.IsNull(result.OutputManifestSha256);
        CollectionAssert.AreEqual(
            ExpectedPrePublishCalls,
            pipeline.Calls.ToArray());
        CollectionAssert.AreEqual(
            ExpectedCheckpoints,
            currentStateProvider.Checkpoints.ToArray());
        CollectionAssert.AreEqual(
            ExpectedBoundaryEvents,
            events);
        Assert.AreSame(changedCapabilities,
            currentStateProvider.ReturnedStates[2].Capabilities);
        Assert.IsFalse(Directory.Exists(package.Destination));
    }

    [TestMethod]
    [DataRow("model-run", OptimizationSupportCode.ModelBindingMismatch)]
    [DataRow("model-handoff", OptimizationSupportCode.ModelBindingMismatch)]
    [DataRow("hardware-run", OptimizationSupportCode.HardwareBindingMismatch)]
    [DataRow("hardware-hash", OptimizationSupportCode.HardwareBindingMismatch)]
    public async Task JourneyBindingDriftAfterSmokeReplansImmediatelyBeforePublish(
        string changedBinding,
        OptimizationSupportCode expected)
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = CreatePlan(package.Source);
        OpenVinoOptimizationCurrentState initial = CurrentState(plan);
        OpenVinoOptimizationCurrentState changed = ChangedCurrentBinding(
            initial, changedBinding);
        SequenceCurrentStateProvider currentStateProvider = new(
            [initial, initial, changed]);
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(pipeline, _ => true);

        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                currentStateProvider,
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.ReplanRequired, result.Status);
        Assert.AreEqual(expected, result.SupportCode);
        CollectionAssert.AreEqual(ExpectedPrePublishCalls, pipeline.Calls.ToArray());
        CollectionAssert.AreEqual(
            ExpectedCheckpoints,
            currentStateProvider.Checkpoints.ToArray());
        Assert.AreSame(changed, currentStateProvider.ReturnedStates[2]);
        Assert.IsFalse(Directory.Exists(package.Destination));
    }

    [TestMethod]
    public async Task PersistentDiskFailureReturnsBoundedC1FailureWithoutOutput()
    {
        using PackageFixture package = PackageFixture.Create();
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(pipeline, _ => false);
        OptimizationExecutionPlan plan = CreatePlan(package.Source);

        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.Failed, result.Status);
        Assert.AreEqual(OptimizationSupportCode.InsufficientDiskSpace,
            result.SupportCode);
        Assert.IsNull(result.OutputIdentity);
        Assert.IsNull(result.OutputManifestSha256);
        Assert.AreEqual(0UL, result.OutputSizeBytes);
        Assert.AreEqual(0, pipeline.Calls.Count);
        Assert.IsFalse(Directory.Exists(package.Destination));
    }

    [TestMethod]
    [DataRow("precision")]
    [DataRow("optimizer-version")]
    [DataRow("device")]
    [DataRow("kv-cache")]
    public async Task ActualCompletionEvidenceMustMatchTheAuthoritativePayload(
        string mismatch)
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = CreatePlan(package.Source);
        Dictionary<string, string> versions = new(
            OpenVinoV2TestPayload.OptimizerVersions,
            StringComparer.Ordinal);
        if (mismatch == "optimizer-version")
        {
            versions["nncf"] = "3.3.1";
        }
        RecordingOptimizationPipeline pipeline = new()
        {
            CompletionWeightPrecision = mismatch == "precision"
                ? OpenVinoWeightPrecision.FourBit
                : null,
            CompletionVersions = versions,
            SmokeDevice = mismatch == "device" ? "GPU" : "CPU",
            SmokeKvCachePrecision = mismatch == "kv-cache"
                ? OpenVinoKvCachePrecision.ReleasedDefault
                : null
        };
        OpenVinoOptimizationService service = new(pipeline, _ => true);

        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.Failed, result.Status);
        Assert.AreEqual(OptimizationSupportCode.ValidationFailed, result.SupportCode);
        Assert.IsNull(result.OutputIdentity);
        Assert.IsNull(result.OutputManifestSha256);
        Assert.IsFalse(Directory.Exists(package.Destination));
        Assert.IsFalse(File.Exists(Path.Combine(
            package.Destination, OpenVinoRuntimeOptimizationProfile.FileName)));
    }

    [TestMethod]
    public void SchemaV2ProvenanceCopiesPayloadVersionsWithoutLegacyTableLookup()
    {
        using PackageFixture package = PackageFixture.Create();
        Dictionary<string, string> payloadVersions = new(
            OpenVinoV2TestPayload.OptimizerVersions,
            StringComparer.Ordinal)
        {
            ["nncf"] = "3.3.9"
        };
        OptimizationExecutionPlan plan = CreatePlan(
            package.Source,
            optimizerVersions: payloadVersions);
        GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution
            .OpenVinoExecutionPayload payload = plan.ExecutionPayload.OpenVino!;
        OpenVinoOptimizationCandidate candidate = new(
            payload.ConfigurationId,
            payload.Device,
            OpenVinoWeightPrecision.EightBit,
            OpenVinoPersistentArtifact.Create(
                OpenVinoWeightPrecision.EightBit,
                OpenVinoWeightPrecision.Fp16),
            new OpenVinoRuntimeOptimization(
                OpenVinoKvCachePrecision.U8,
                GraniteEdgeAI.Features.OpenVinoRoute.Optimization
                    .OpenVinoCompiledCachePolicy.Disabled),
            OpenVinoCapabilityPerformanceHint.Latency,
            Streams: 1,
            ContextTokens: 4_096,
            payload.Maturity,
            payload.EvidenceId)
        {
            SourceWeightPrecision = OpenVinoWeightPrecision.Fp16,
            ExecutionPayload = payload
        };
        candidate.Validate();
        IReadOnlyList<GraniteEdgeAI.Features.OpenVinoRoute.Conversion
            .OpenVinoOutputArtifact> output =
            OpenVinoOptimizationProvenance.CaptureOutput(package.Source);
        OpenVinoOptimizationProvenance provenance =
            OpenVinoOptimizationProvenance.CreatePlanBound(
                plan,
                Guid.NewGuid(),
                Guid.NewGuid(),
                new string('a', 64),
                candidate,
                new OpenVinoRuntimeOptimizationEvidence(
                    "CPU",
                    OpenVinoKvCachePrecision.U8,
                    "passed",
                    "passed"),
                payloadVersions,
                output,
                GraniteEdgeAI.Features.OpenVinoRoute.Conversion.OpenVinoProvenance
                    .ComputeOutputManifestDigest(output));
        Directory.CreateDirectory(package.Destination);

        provenance.Write(package.Destination);
        OpenVinoOptimizationProvenance read =
            OpenVinoOptimizationProvenance.Read(package.Destination);

        Assert.AreEqual("3.3.9", read.OptimizerVersions["nncf"]);
        AssertPayloadBinding(
            payload,
            read.OpenVinoExecutionPayloadJson,
            read.OpenVinoExecutionPayloadSha256);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task PostPublicationReinspectionFailureRollsBackAndReturnsTypedC1Failure(
        bool rejected)
    {
        using PackageFixture package = PackageFixture.Create();
        RecordingOptimizationPipeline pipeline = new()
        {
            ReinspectionFailure = rejected
                ? new OpenVinoOptimizationException(
                    OpenVinoSupportCode.PackageInconsistentResource)
                : new InvalidOperationException("reinspection failed")
        };
        OpenVinoOptimizationService service = new(pipeline, _ => true);
        OptimizationExecutionPlan plan = CreatePlan(package.Source);

        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.Failed, result.Status);
        Assert.AreEqual(OptimizationSupportCode.ReinspectionFailed,
            result.SupportCode);
        Assert.IsNull(result.OutputIdentity);
        Assert.IsNull(result.OutputManifestSha256);
        Assert.AreEqual(0UL, result.OutputSizeBytes);
        CollectionAssert.AreEqual(ExpectedPipelineCalls, pipeline.Calls.ToArray());
        Assert.IsFalse(Directory.Exists(package.Destination));
        Assert.AreEqual(0, Directory.EnumerateDirectories(
            Path.GetDirectoryName(package.Destination)!,
            ".granite-openvino-*.staging").Count());
    }

    [TestMethod]
    [DataRow("model-run", OptimizationSupportCode.ModelBindingMismatch)]
    [DataRow("model-handoff", OptimizationSupportCode.ModelBindingMismatch)]
    [DataRow("hardware-run", OptimizationSupportCode.HardwareBindingMismatch)]
    [DataRow("hardware-hash", OptimizationSupportCode.HardwareBindingMismatch)]
    public async Task IndependentCurrentJourneyBindingDriftFailsBeforePipeline(
        string changedBinding,
        OptimizationSupportCode expected)
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = CreatePlan(package.Source);
        OpenVinoOptimizationCurrentState current = CurrentState(plan);
        current = changedBinding switch
        {
            "model-run" => current with { ModelInspectionRunId = "mi-run-drifted" },
            "model-handoff" => current with
            {
                ModelInspectionHandoffId = "mi-handoff-drifted"
            },
            "hardware-run" => current with
            {
                ProductHardwareRunId = "hw-run-drifted"
            },
            "hardware-hash" => current with
            {
                HardwareSnapshotSha256 = new string('9', 64)
            },
            _ => throw new InvalidOperationException()
        };
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(pipeline, _ => true);

        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                new SequenceCurrentStateProvider([current]),
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.ReplanRequired, result.Status);
        Assert.AreEqual(expected, result.SupportCode);
        Assert.AreEqual(0, pipeline.Calls.Count);
        Assert.IsFalse(Directory.Exists(package.Destination));
    }

    [TestMethod]
    public async Task HardwareDriftAfterInitialCheckReplansImmediatelyBeforeStaging()
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = CreatePlan(package.Source);
        OpenVinoOptimizationCurrentState initial = CurrentState(plan);
        OpenVinoOptimizationCurrentState changed = initial with
        {
            ProductHardwareRunId = "hw-run-after-preflight"
        };
        SequenceCurrentStateProvider currentStateProvider = new([initial, changed]);
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(pipeline, _ => true);

        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                currentStateProvider,
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.ReplanRequired, result.Status);
        Assert.AreEqual(OptimizationSupportCode.HardwareBindingMismatch,
            result.SupportCode);
        CollectionAssert.AreEqual(
            new[]
            {
                OpenVinoOptimizationCheckpoint.InitialPreflight,
                OpenVinoOptimizationCheckpoint.BeforeStaging
            },
            currentStateProvider.Checkpoints.ToArray());
        Assert.AreSame(changed, currentStateProvider.ReturnedStates[1]);
        Assert.AreEqual(0, pipeline.Calls.Count);
        Assert.IsFalse(Directory.Exists(package.Destination));
    }

    [TestMethod]
    public async Task RuntimeProfileUsesHardenedTransactionOverlapPolicy()
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = CreatePlan(
            package.Source,
            OpenVinoWeightFormat.Original,
            OpenVinoKvCacheFormat.RouteDefault);
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(pipeline, _ => false);

        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                Path.Combine(package.Source, "profile"),
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.Failed, result.Status);
        Assert.AreEqual(OptimizationSupportCode.StagingUnavailable,
            result.SupportCode);
        Assert.IsFalse(Directory.Exists(Path.Combine(package.Source, "profile")));
    }

    [TestMethod]
    public async Task RuntimeProfileUsesStandardOperationOwnedStagingCollision()
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = CreatePlan(
            package.Source,
            OpenVinoWeightFormat.Original,
            OpenVinoKvCacheFormat.RouteDefault);
        Guid operationId = Guid.NewGuid();
        string collision = Path.Combine(
            Path.GetDirectoryName(package.Destination)!,
            $".granite-openvino-{operationId:N}.staging");
        Directory.CreateDirectory(collision);
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(
            pipeline, _ => false, () => operationId);

        OptimizationExecutionResult result = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OptimizationExecutionStatus.Failed, result.Status);
        Assert.AreEqual(OptimizationSupportCode.StagingUnavailable,
            result.SupportCode);
        Assert.IsTrue(Directory.Exists(collision));
        Assert.IsFalse(Directory.Exists(package.Destination));
    }

    [TestMethod]
    public async Task PlanContextAndExecutionConfigurationTamperingFailValidation()
    {
        using PackageFixture package = PackageFixture.Create();
        OptimizationExecutionPlan plan = CreatePlan(package.Source);
        OpenVinoOptimizationService service = new(
            new RecordingOptimizationPipeline(), _ => true);
        _ = await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                plan,
                CurrentStateProvider(plan),
                Confirmed: true),
            progress: null,
            CancellationToken.None);
        string path = Path.Combine(
            package.Destination, OpenVinoOptimizationProvenance.FileName);
        string original = File.ReadAllText(path);
        string configurationId = OpenVinoOptimizationProvenance.Read(
            package.Destination).ConfigurationId;

        File.WriteAllText(path, original.Replace(
            "\"workloadId\": \"chat\"",
            "\"workloadId\": \"talk\"",
            StringComparison.Ordinal));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            OpenVinoOptimizationProvenance.Read(package.Destination));

        File.WriteAllText(path, original.Replace(
            $"\"configurationId\": \"{configurationId}\"",
            $"\"configurationId\": \"{configurationId}.tampered\"",
            StringComparison.Ordinal));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            OpenVinoOptimizationProvenance.Read(package.Destination));
    }

    [TestMethod]
    public async Task UnregisteredCandidateFailsBeforeStagingOrOptimization()
    {
        using PackageFixture package = PackageFixture.Create();
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(pipeline, _ => true);
        OpenVinoOptimizationCandidate registered =
            OpenVinoOptimizationLegacyRegistryV1.GetRequired(
                OpenVinoOptimizationObjective.Quality);
        OpenVinoOptimizationCandidate counterfeit = registered with
        {
            ConfigurationId = "openvino.standard.cpu.counterfeit.v1"
        };

        OpenVinoOptimizationResult result = await service.OptimizeLegacyV1Async(
            new OpenVinoOptimizationLegacyRequestV1(
                package.Source,
                package.Destination,
                counterfeit,
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OpenVinoOptimizationStatus.Failed, result.Status);
        Assert.AreEqual(GraniteEdgeAI.OpenVino.Contracts.OpenVinoSupportCode.OptimizationUnsupported,
            result.SupportCode);
        Assert.AreEqual(0, pipeline.Calls.Count);
        Assert.IsFalse(Directory.Exists(package.Destination));
    }

    [TestMethod]
    public void StrictOptimizationProvenanceIsBoundToThePackageSnapshot()
    {
        using PackageFixture package = PackageFixture.Create();
        WriteValidOptimizationProvenance(package.Source);

        OpenVinoStaticPackageInspectionResult accepted =
            new OpenVinoStaticPackageInspector().Inspect(package.Source);
        Assert.AreEqual(OpenVinoStaticInspectionStatus.NativeValidationRequired, accepted.Status);

        string provenancePath = Path.Combine(
            package.Source, OpenVinoOptimizationProvenance.FileName);
        string json = File.ReadAllText(provenancePath).Replace(
            "openvino.standard.cpu.fp16.default.v1",
            "openvino.standard.cpu.counterfeit.v1",
            StringComparison.Ordinal);
        File.WriteAllText(provenancePath, json);

        OpenVinoStaticPackageInspectionResult rejected =
            new OpenVinoStaticPackageInspector().Inspect(package.Source);
        Assert.AreEqual(OpenVinoStaticInspectionStatus.Rejected, rejected.Status);
        Assert.AreEqual(OpenVinoSupportCode.PackageInconsistentResource, rejected.SupportCode);
    }

    [TestMethod]
    public void MalformedTypesAndUnpinnedOptimizerVersionsFailClosed()
    {
        using PackageFixture malformed = PackageFixture.Create();
        WriteValidOptimizationProvenance(malformed.Source);
        string malformedPath = Path.Combine(
            malformed.Source, OpenVinoOptimizationProvenance.FileName);
        File.WriteAllText(
            malformedPath,
            File.ReadAllText(malformedPath).Replace(
                "\"actualDevice\": \"CPU\"",
                "\"actualDevice\": 7",
                StringComparison.Ordinal));
        OpenVinoStaticPackageInspectionResult malformedResult =
            new OpenVinoStaticPackageInspector().Inspect(malformed.Source);
        Assert.AreEqual(OpenVinoStaticInspectionStatus.Rejected, malformedResult.Status);
        Assert.AreEqual(OpenVinoSupportCode.PackageInconsistentResource,
            malformedResult.SupportCode);

        using PackageFixture unpinned = PackageFixture.Create();
        WriteValidOptimizationProvenance(unpinned.Source);
        string unpinnedPath = Path.Combine(
            unpinned.Source, OpenVinoOptimizationProvenance.FileName);
        File.WriteAllText(
            unpinnedPath,
            File.ReadAllText(unpinnedPath).Replace(
                "\"nncf\": \"3.3.0\"",
                "\"nncf\": \"3.3.1\"",
                StringComparison.Ordinal));
        OpenVinoStaticPackageInspectionResult unpinnedResult =
            new OpenVinoStaticPackageInspector().Inspect(unpinned.Source);
        Assert.AreEqual(OpenVinoStaticInspectionStatus.Rejected, unpinnedResult.Status);
        Assert.AreEqual(OpenVinoSupportCode.PackageInconsistentResource,
            unpinnedResult.SupportCode);
    }

    private static void WriteValidOptimizationProvenance(string root)
    {
        IReadOnlyList<GraniteEdgeAI.Features.OpenVinoRoute.Conversion.OpenVinoOutputArtifact>
            output = OpenVinoOptimizationProvenance.CaptureOutput(root);
        OpenVinoOptimizationCompletion completion =
            OpenVinoOptimizationCompletion.CreateTestInstance(OpenVinoWeightPrecision.Fp16);
        OpenVinoOptimizationProvenance provenance = new(
            OpenVinoOptimizationProvenance.LegacySchemaVersion,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new string('a', 64),
            "openvino.standard.cpu.fp16.default.v1",
            OpenVinoWeightPrecision.Fp16,
            OpenVinoWeightPrecision.Fp16,
            OpenVinoKvCachePrecision.ReleasedDefault,
            "CPU",
            OpenVinoKvCachePrecision.ReleasedDefault,
            completion.Versions,
            output,
            GraniteEdgeAI.Features.OpenVinoRoute.Conversion.OpenVinoProvenance
                .ComputeOutputManifestDigest(output),
            "passed",
            "passed",
            "passed");
        provenance.Write(root);
    }

    private static void AssertPayloadBinding(
        GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution
            .OpenVinoExecutionPayload expected,
        string? actualJson,
        string? actualSha256)
    {
        string json = actualJson ?? throw new AssertFailedException(
            "The authoritative OpenVINO payload serialization was absent.");
        string sha256 = actualSha256 ?? throw new AssertFailedException(
            "The authoritative OpenVINO payload digest was absent.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(json));
        string digest = Convert.ToHexString(SHA256.HashData(
            new UTF8Encoding(false, true).GetBytes(json))).ToLowerInvariant();
        Assert.AreEqual(digest, sha256);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement payload = document.RootElement;
        Assert.AreEqual(expected.ConfigurationId,
            payload.GetProperty("configurationId").GetString());
        Assert.AreEqual(expected.Device,
            payload.GetProperty("device").GetString());
        Assert.AreEqual(expected.Maturity,
            payload.GetProperty("maturity").GetString());
        Assert.AreEqual(expected.EvidenceId,
            payload.GetProperty("evidenceId").GetString());
        Assert.AreEqual(expected.SourceWeightPrecision.ToString(),
            payload.GetProperty("sourceWeightPrecision").GetString());
        Assert.AreEqual(expected.TargetWeightPrecision.ToString(),
            payload.GetProperty("targetWeightPrecision").GetString());
        Assert.AreEqual(expected.KvCachePrecision.ToString(),
            payload.GetProperty("kvCachePrecision").GetString());
        Assert.AreEqual(expected.CompiledCacheEnabled,
            payload.GetProperty("compiledCacheEnabled").GetBoolean());
        Assert.AreEqual(expected.CompiledCacheIsDisposable,
            payload.GetProperty("compiledCacheIsDisposable").GetBoolean());
        Assert.AreEqual(expected.CompiledCacheIsModelArtifact,
            payload.GetProperty("compiledCacheIsModelArtifact").GetBoolean());
        Assert.AreEqual(expected.CreatesCompletePackage,
            payload.GetProperty("createsCompletePackage").GetBoolean());
        Assert.AreEqual(expected.RequiresPersistentConversion,
            payload.GetProperty("requiresPersistentConversion").GetBoolean());

        JsonElement builds = payload.GetProperty("buildIdentity");
        Assert.AreEqual(expected.BuildIdentity.RuntimeBuild,
            builds.GetProperty("runtimeBuild").GetString());
        Assert.AreEqual(expected.BuildIdentity.GenAiBuild,
            builds.GetProperty("genAiBuild").GetString());
        Assert.AreEqual(expected.BuildIdentity.TokenizersBuild,
            builds.GetProperty("tokenizersBuild").GetString());
        Assert.AreEqual(expected.BuildIdentity.WorkerManifestDigest,
            builds.GetProperty("workerManifestDigest").GetString());

        JsonElement versions = payload.GetProperty("optimizerVersions");
        Assert.AreEqual(expected.OptimizerVersions.Count,
            versions.EnumerateObject().Count());
        foreach ((string name, string version) in expected.OptimizerVersions)
        {
            Assert.AreEqual(version, versions.GetProperty(name).GetString(), name);
        }
        Assert.AreEqual(JsonValueKind.Null,
            payload.GetProperty("turboQuantBuild").ValueKind);
    }

    private static List<ProductionCall> ProductionCalls(string methodName)
    {
        MethodInfo method = typeof(OpenVinoOptimizationService).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic) ??
            throw new AssertFailedException(
                $"Production method OpenVinoOptimizationService.{methodName} is absent.");
        MethodInfo bodyOwner = method.GetCustomAttribute<AsyncStateMachineAttribute>()?
            .StateMachineType.GetMethod(
                "MoveNext",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
            method;
        byte[] il = bodyOwner.GetMethodBody()?.GetILAsByteArray() ??
            throw new AssertFailedException(
                $"Production method {methodName} has no inspectable body.");
        List<ProductionCall> calls = [];
        int offset = 0;
        while (offset < il.Length)
        {
            int instructionOffset = offset;
            OpCode opcode = il[offset++] == 0xfe
                ? MultiByteOpCodes[il[offset++]]
                : SingleByteOpCodes[il[offset - 1]];
            int operandSize = OperandSize(opcode.OperandType, il, offset);
            if (opcode.OperandType == OperandType.InlineMethod &&
                (opcode == OpCodes.Call || opcode == OpCodes.Callvirt))
            {
                try
                {
                    MethodBase? target = bodyOwner.Module.ResolveMethod(
                        BitConverter.ToInt32(il, offset),
                        bodyOwner.DeclaringType?.IsGenericType is true
                            ? bodyOwner.DeclaringType.GetGenericArguments()
                            : null,
                        bodyOwner.IsGenericMethod
                            ? bodyOwner.GetGenericArguments()
                            : null);
                    if (target is not null)
                    {
                        calls.Add(new ProductionCall(instructionOffset, target));
                    }
                }
                catch (Exception exception) when (exception is ArgumentException or
                                                  BadImageFormatException)
                {
                    throw new AssertFailedException(
                        $"Unable to resolve a production call in {methodName}.",
                        exception);
                }
            }
            offset += operandSize;
        }
        return calls;
    }

    private static void AssertCallOrder(
        IReadOnlyList<ProductionCall> calls,
        params (Type DeclaringType, string Name)[] expected)
    {
        int previous = -1;
        foreach ((Type declaringType, string name) in expected)
        {
            ProductionCall? found = calls.FirstOrDefault(call =>
                call.Offset > previous &&
                call.Target.DeclaringType == declaringType &&
                string.Equals(call.Target.Name, name, StringComparison.Ordinal));
            Assert.IsNotNull(found,
                $"No {declaringType.Name}.{name} call follows IL offset {previous}.");
            previous = found.Offset;
        }
    }

    private static int[] CallOffsets(
        IReadOnlyList<ProductionCall> calls,
        Type declaringType,
        string name) => calls
            .Where(call => call.Target.DeclaringType == declaringType &&
                string.Equals(call.Target.Name, name, StringComparison.Ordinal))
            .Select(static call => call.Offset)
            .ToArray();

    private static int SingleCallOffset(
        IReadOnlyList<ProductionCall> calls,
        Type declaringType,
        string name)
    {
        int[] offsets = CallOffsets(calls, declaringType, name);
        Assert.HasCount(1, offsets,
            $"Expected exactly one {declaringType.Name}.{name} production call.");
        return offsets[0];
    }

    private static void AssertOffsetsOrdered(params int[] offsets)
    {
        Assert.IsTrue(offsets.All(static offset => offset >= 0),
            "Every required production call must exist.");
        for (int index = 1; index < offsets.Length; index++)
        {
            Assert.IsGreaterThan(offsets[index - 1], offsets[index],
                "Required production calls are not in trusted boundary order.");
        }
    }

    private static int OperandSize(
        OperandType operandType,
        byte[] il,
        int offset) => operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or
                OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget or OperandType.InlineField or
                OperandType.InlineI or OperandType.InlineMethod or
                OperandType.InlineSig or OperandType.InlineString or
                OperandType.InlineTok or OperandType.InlineType or
                OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch =>
                4 + (BitConverter.ToInt32(il, offset) * 4),
            _ => throw new AssertFailedException(
                $"Unknown IL operand type {operandType}.")
        };

    private static OpCode[] BuildOpCodes(bool multiByte)
    {
        OpCode[] result = new OpCode[256];
        foreach (FieldInfo field in typeof(OpCodes).GetFields(
                     BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opcode)
            {
                continue;
            }
            ushort value = unchecked((ushort)opcode.Value);
            if (multiByte == value > byte.MaxValue)
            {
                result[value & byte.MaxValue] = opcode;
            }
        }
        return result;
    }

    private static readonly OpCode[] SingleByteOpCodes = BuildOpCodes(
        multiByte: false);
    private static readonly OpCode[] MultiByteOpCodes = BuildOpCodes(
        multiByte: true);

    private sealed record ProductionCall(int Offset, MethodBase Target);

    private static OptimizationExecutionPlan CreatePlan(
        string sourceDirectory,
        OpenVinoWeightFormat weights = OpenVinoWeightFormat.Int8,
        OpenVinoKvCacheFormat kvCache = OpenVinoKvCacheFormat.U8,
        ContractCompiledCachePolicy compiledCache = ContractCompiledCachePolicy.Disabled,
        int streams = 1,
        int contextTokens = 4_096,
        IReadOnlyDictionary<string, string>? optimizerVersions = null,
        ExecutionWeightPrecision sourcePrecision = ExecutionWeightPrecision.Fp16)
    {
        OpenVinoStaticPackageEvidence source =
            new OpenVinoStaticPackageInspector().Inspect(sourceDirectory).Evidence!;
        OpenVinoRouteConfiguration configuration = OpenVinoRouteConfiguration.Create(
            weights,
            kvCache,
            DeviceRouteId.Cpu,
            OpenVinoPerformanceHint.Latency,
            compiledCache,
            streams);
        ExecutionWeightPrecision targetPrecision = weights switch
        {
            OpenVinoWeightFormat.Original or OpenVinoWeightFormat.Fp16 =>
                ExecutionWeightPrecision.Fp16,
            OpenVinoWeightFormat.Int8 => ExecutionWeightPrecision.EightBit,
            OpenVinoWeightFormat.Int4 => ExecutionWeightPrecision.FourBit,
            _ => throw new ArgumentOutOfRangeException(nameof(weights))
        };
        bool persistent = sourcePrecision != targetPrecision;
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
                workingDiskBytes: persistent ? 1_024UL : 0,
                outputDiskBytes: persistent ? 512UL : 0,
                requiresPersistentChange: persistent),
            "OV-BOUND-01",
            isExperimental: false);
        OpenVinoCapabilityPayload payload = OpenVinoCapabilityPayload.Create(
            OpenVinoV2TestPayload.CapabilityRuntimeVersion,
            [
                OpenVinoAdmittedConfiguration.Create(
                    "OV-BOUND-01",
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
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-capability-bound-1",
                new string('3', 64),
                payload);
        return OpenVinoV2PlanTestFactory.Issue(
            candidate,
            OpenVinoV2TestPayload.For(
                weights, kvCache, compiledCache, "OV-BOUND-01",
                optimizerVersions: optimizerVersions,
                sourceWeightPrecision: sourcePrecision),
            snapshot,
            OptimizationWorkload.Create(
                "chat",
                minimumContextTokens: 512,
                OptimizationAssessment.Poor,
                [ContextTokenCount.FromTokens(contextTokens)]),
            OptimizationJourneyBinding.Create(
                "mi-run-bound-1",
                "mi-handoff-bound-1",
                source.ModelSha256,
                checked((ulong)source.ModelLengthBytes),
                "hw-run-bound-1",
                new string('2', 64)),
            modelLayerCount: 1,
            DateTimeOffset.UnixEpoch);
    }

    private static OpenVinoOptimizationCurrentState CurrentState(
        OptimizationExecutionPlan plan,
        OptimizationCapabilitySnapshot? capabilities = null) => new(
            capabilities ?? plan.CapabilitySnapshot,
            CurrentEvidence(plan),
            plan.Binding.ModelInspectionRunId,
            plan.Binding.ModelInspectionHandoffId,
            plan.Binding.ProductHardwareRunId,
            plan.Binding.HardwareSnapshotSha256);

    private static OpenVinoOptimizationCapabilityEvidence CurrentEvidence(
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

    private static SequenceCurrentStateProvider CurrentStateProvider(
        OptimizationExecutionPlan plan,
        OptimizationCapabilitySnapshot? capabilities = null) =>
        new([CurrentState(plan, capabilities)]);

    private static OpenVinoOptimizationCurrentState ChangedCurrentBinding(
        OpenVinoOptimizationCurrentState current,
        string changedBinding)
    {
        return changedBinding switch
        {
            "model-run" => current with
            {
                ModelInspectionRunId = "model-run-drifted"
            },
            "model-handoff" => current with
            {
                ModelInspectionHandoffId = "model-handoff-drifted"
            },
            "hardware-run" => current with
            {
                ProductHardwareRunId = "hardware-run-drifted"
            },
            "hardware-hash" => current with
            {
                HardwareSnapshotSha256 = new string('9', 64)
            },
            _ => throw new InvalidOperationException()
        };
    }

    private sealed class SequenceCurrentStateProvider(
        IEnumerable<OpenVinoOptimizationCurrentState> states,
        List<string>? events = null,
        Action<OpenVinoOptimizationCheckpoint>? beforeReturn = null) :
        IOpenVinoOptimizationCurrentStateProvider
    {
        private readonly Queue<OpenVinoOptimizationCurrentState> remaining =
            new(states);
        private OpenVinoOptimizationCurrentState? last;

        internal List<OpenVinoOptimizationCheckpoint> Checkpoints { get; } = [];
        internal List<OpenVinoOptimizationCurrentState> ReturnedStates { get; } = [];

        public ValueTask<OpenVinoOptimizationCurrentState> GetCurrentStateAsync(
            OpenVinoOptimizationCheckpoint checkpoint,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Checkpoints.Add(checkpoint);
            events?.Add("state:" + checkpoint);
            beforeReturn?.Invoke(checkpoint);
            if (remaining.Count > 0)
            {
                last = remaining.Dequeue();
            }
            OpenVinoOptimizationCurrentState current = last ??
                throw new AssertFailedException("No current state was supplied.");
            ReturnedStates.Add(current);
            return ValueTask.FromResult(current);
        }
    }

    private sealed class RecordingOptimizationPipeline : IOpenVinoOptimizationPipeline
    {
        private readonly List<string>? events;

        internal RecordingOptimizationPipeline(List<string>? events = null)
        {
            this.events = events;
        }

        internal List<string> Calls { get; } = [];
        internal List<OpenVinoOptimizationInvocation> Invocations { get; } = [];
        internal Exception? ReinspectionFailure { get; init; }
        internal OpenVinoWeightPrecision? CompletionWeightPrecision { get; init; }
        internal IReadOnlyDictionary<string, string>? CompletionVersions { get; init; }
        internal string SmokeDevice { get; init; } = "CPU";
        internal OpenVinoKvCachePrecision? SmokeKvCachePrecision { get; init; }

        public Task<OpenVinoOptimizationCompletion> OptimizeAsync(
            OpenVinoOptimizationInvocation invocation,
            CancellationToken cancellationToken)
        {
            Calls.Add("optimize");
            Invocations.Add(invocation);
            events?.Add("optimize");
            File.Copy(
                Path.Combine(invocation.SourceDirectory, "openvino_model.xml"),
                Path.Combine(invocation.StagingDirectory, "openvino_model.xml"));
            File.WriteAllBytes(
                Path.Combine(invocation.StagingDirectory, "openvino_model.bin"), [1, 2, 3]);
            File.Copy(
                Path.Combine(invocation.SourceDirectory, "config.json"),
                Path.Combine(invocation.StagingDirectory, "config.json"));
            OpenVinoOptimizationCompletion defaults =
                OpenVinoOptimizationCompletion.CreateTestInstance(
                    invocation.Candidate.PersistentArtifact.WeightPrecision);
            return Task.FromResult(new OpenVinoOptimizationCompletion(
                CompletionWeightPrecision ?? defaults.ActualWeightPrecision,
                CompletionVersions ?? defaults.Versions));
        }

        public Task<OpenVinoOptimizationValidation> ValidateAsync(
            string stagingDirectory,
            CancellationToken cancellationToken)
        {
            Calls.Add("validate");
            events?.Add("validate");
            return Task.FromResult(OpenVinoOptimizationValidation.CreateTestInstance());
        }

        public Task<OpenVinoRuntimeOptimizationEvidence> SmokeAsync(
            OpenVinoOptimizationValidation validation,
            string stagingDirectory,
            OpenVinoOptimizationCandidate candidate,
            CancellationToken cancellationToken)
        {
            Calls.Add("smoke");
            events?.Add("smoke");
            return Task.FromResult(new OpenVinoRuntimeOptimizationEvidence(
                SmokeDevice,
                SmokeKvCachePrecision ?? candidate.Runtime.KvCachePrecision,
                GenerationDisposition: "passed",
                QualityDisposition: "passed"));
        }

        public Task<Guid> ReinspectPublishedAsync(
            string destinationDirectory,
            CancellationToken cancellationToken)
        {
            Calls.Add("reinspect");
            events?.Add("reinspect");
            if (ReinspectionFailure is not null)
            {
                throw ReinspectionFailure;
            }
            return Task.FromResult(Guid.NewGuid());
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
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
                AppContext.BaseDirectory, "TestFixtures", "OpenVINO", "GenAI",
                "TinySyntheticV1", "package");
            string root = Path.Combine(
                Path.GetTempPath(), "ov-optimization-" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "source");
            string output = Path.Combine(root, "output");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(output);
            foreach (string file in Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(source, Path.GetRelativePath(fixture, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
            return new PackageFixture(root, source, Path.Combine(output, "optimized"));
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
