using System.Reflection;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using ContractCompiledCachePolicy = GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino.OpenVinoCompiledCachePolicy;

namespace GraniteEdgeAI.OpenVino.Tests.Optimization;

[TestClass]
[TestCategory("OpenVinoRoute")]
public sealed class OpenVinoOptimizationPlanAdapterTests
{
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
            ContractCompiledCachePolicy.Enabled,
            streams: 2,
            contextTokens: 2_048);

        OpenVinoOptimizationAdaptation result =
            OpenVinoOptimizationPlanAdapter.Adapt(
                plan,
                plan.CapabilitySnapshot,
                OpenVinoOptimizationTestData.SourceDigest,
                OpenVinoOptimizationTestData.SourceLength);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.Ready, result.Status);
        Assert.AreEqual(OptimizationSupportCode.None, result.SupportCode);
        Assert.IsNotNull(result.Candidate);
        Assert.AreEqual(OpenVinoWeightPrecision.EightBit,
            result.Candidate.WeightPrecision);
        Assert.AreEqual(OpenVinoKvCachePrecision.U8,
            result.Candidate.Runtime.KvCachePrecision);
        Assert.IsTrue(result.Candidate.Runtime.CompiledCache.Enabled);
        Assert.AreEqual("CPU", result.Candidate.Device);
        Assert.AreEqual(OpenVinoCapabilityPerformanceHint.Latency,
            result.Candidate.PerformanceHint);
        Assert.AreEqual(2, result.Candidate.Streams);
        Assert.AreEqual(2_048, result.Candidate.ContextTokens);
        Assert.AreEqual("OV-EXACT-01", result.Candidate.EvidenceId);
        Assert.IsNull(typeof(OpenVinoOptimizationCandidate).GetProperty("Objective"));
    }

    [TestMethod]
    public void AdapterMapsOriginalWeightsAsRuntimeOnly()
    {
        OptimizationExecutionPlan plan = OpenVinoOptimizationTestData.Plan(
            OpenVinoWeightFormat.Original,
            OpenVinoKvCacheFormat.RouteDefault,
            ContractCompiledCachePolicy.Disabled,
            streams: 1,
            contextTokens: 1_024);

        OpenVinoOptimizationAdaptation result =
            OpenVinoOptimizationPlanAdapter.Adapt(
                plan,
                plan.CapabilitySnapshot,
                OpenVinoOptimizationTestData.SourceDigest,
                OpenVinoOptimizationTestData.SourceLength);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.Ready, result.Status);
        Assert.IsNotNull(result.Candidate);
        Assert.AreEqual(OpenVinoWeightPrecision.Original,
            result.Candidate.WeightPrecision);
        Assert.IsNull(result.Candidate.PersistentArtifact);
        Assert.IsFalse(plan.ProducesPersistentArtifact);
    }

    [TestMethod]
    [DataRow(OpenVinoWeightFormat.Fp16, OpenVinoKvCacheFormat.RouteDefault,
        ContractCompiledCachePolicy.Disabled, OpenVinoWeightPrecision.Fp16)]
    [DataRow(OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.U8,
        ContractCompiledCachePolicy.Enabled, OpenVinoWeightPrecision.EightBit)]
    [DataRow(OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.U8,
        ContractCompiledCachePolicy.Enabled, OpenVinoWeightPrecision.FourBit)]
    public void AdapterMapsEveryReleasedPersistentWeightPath(
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
                OpenVinoOptimizationTestData.SourceDigest,
                OpenVinoOptimizationTestData.SourceLength);

        Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.Ready, result.Status);
        Assert.IsNotNull(result.Candidate);
        Assert.AreEqual(expected, result.Candidate.WeightPrecision);
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
                OpenVinoOptimizationTestData.CurrentState(plan),
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
                OpenVinoOptimizationTestData.CurrentState(plan),
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
            ulong sourceLength = SourceLength)
        {
            OpenVinoRouteConfiguration configuration =
                OpenVinoRouteConfiguration.Create(
                    weights,
                    kvCache,
                    DeviceRouteId.Cpu,
                    OpenVinoPerformanceHint.Latency,
                    compiledCache,
                    streams);
            bool persistent = weights != OpenVinoWeightFormat.Original;
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
            OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
                [candidate],
                OptimizationPreferenceSelection.Manual(50))!;

            return OptimizationPlanIssuer.Issue(
                selection,
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
                DateTimeOffset.UnixEpoch);
        }

        internal static OpenVinoCapabilityPayload Payload(
            OpenVinoWeightFormat weights,
            OpenVinoKvCacheFormat kvCache,
            ContractCompiledCachePolicy compiledCache,
            int streams,
            string evidenceId) =>
            OpenVinoCapabilityPayload.Create(
                "openvino-test-runtime-1",
                [
                    OpenVinoAdmittedConfiguration.Create(
                        evidenceId,
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

        internal static OptimizationCapabilitySnapshot Snapshot(
            OpenVinoCapabilityPayload payload,
            string digest,
            string snapshotId = "ov-capability-test-1") =>
            OptimizationCapabilitySnapshot.ForOpenVino(
                snapshotId,
                digest,
                payload);

        internal static OpenVinoOptimizationCurrentState CurrentState(
            OptimizationExecutionPlan plan) => new(
                plan.CapabilitySnapshot,
                plan.Binding.ModelInspectionRunId,
                plan.Binding.ModelInspectionHandoffId,
                plan.Binding.ProductHardwareRunId,
                plan.Binding.HardwareSnapshotSha256);
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
