using System.Text.Json;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.OpenVino.Contracts;
using ContractCompiledCachePolicy = GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino.OpenVinoCompiledCachePolicy;

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
        OpenVinoOptimizationProvenance provenance =
            OpenVinoOptimizationProvenance.Read(package.Destination);

        Assert.AreEqual(OptimizationExecutionStatus.SucceededPersistent, result.Status);
        Assert.AreEqual(plan.OptimizationPlanId, result.OptimizationPlanId);
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
            OpenVinoKvCacheFormat.U8);

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
            OpenVinoKvCacheFormat.U8);
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
            OpenVinoKvCacheFormat.U8);
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

    private static OptimizationExecutionPlan CreatePlan(
        string sourceDirectory,
        OpenVinoWeightFormat weights = OpenVinoWeightFormat.Int8,
        OpenVinoKvCacheFormat kvCache = OpenVinoKvCacheFormat.U8,
        ContractCompiledCachePolicy compiledCache = ContractCompiledCachePolicy.Disabled,
        int streams = 1,
        int contextTokens = 4_096)
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
                    minimumContextTokens: 512,
                    maximumContextTokens: 8_192,
                    SupportLevel.DeclaredSupported,
                    requiresEvidence: false)
            ]);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-capability-bound-1",
                new string('3', 64),
                payload);
        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            [candidate], OptimizationPreferenceSelection.Manual(50))!;
        return OptimizationPlanIssuer.Issue(
            selection,
            OpenVinoV2TestPayload.For(
                weights, kvCache, compiledCache, "OV-BOUND-01"),
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
            plan.Binding.ModelInspectionRunId,
            plan.Binding.ModelInspectionHandoffId,
            plan.Binding.ProductHardwareRunId,
            plan.Binding.HardwareSnapshotSha256);

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
        List<string>? events = null) : IOpenVinoOptimizationCurrentStateProvider
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
        internal Exception? ReinspectionFailure { get; init; }

        public Task<OpenVinoOptimizationCompletion> OptimizeAsync(
            OpenVinoOptimizationInvocation invocation,
            CancellationToken cancellationToken)
        {
            Calls.Add("optimize");
            events?.Add("optimize");
            File.Copy(
                Path.Combine(invocation.SourceDirectory, "openvino_model.xml"),
                Path.Combine(invocation.StagingDirectory, "openvino_model.xml"));
            File.WriteAllBytes(
                Path.Combine(invocation.StagingDirectory, "openvino_model.bin"), [1, 2, 3]);
            File.Copy(
                Path.Combine(invocation.SourceDirectory, "config.json"),
                Path.Combine(invocation.StagingDirectory, "config.json"));
            return Task.FromResult(OpenVinoOptimizationCompletion.CreateTestInstance(
                invocation.Candidate.PersistentArtifact.WeightPrecision));
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
