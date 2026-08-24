using System.Security.Cryptography;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using ContractCompiledCachePolicy = GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino.OpenVinoCompiledCachePolicy;
using RouteCompiledCachePolicy = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoCompiledCachePolicy;

namespace GraniteEdgeAI.OpenVino.WorkerProcess.Tests;

[TestClass]
[TestCategory("OpenVinoRoute")]
[TestCategory("OfficialNative")]
[DoNotParallelize]
public sealed class OptimizationEndToEndTests
{
    private static readonly OpenVinoOptimizationObjective[] PersistentObjectives =
    [
        OpenVinoOptimizationObjective.Quality,
        OpenVinoOptimizationObjective.Balanced,
        OpenVinoOptimizationObjective.Efficiency
    ];
    private static readonly OpenVinoOptimizationObjective[] SupportedLegacyObjectives =
    [
        OpenVinoOptimizationObjective.Automatic,
        OpenVinoOptimizationObjective.Quality
    ];
    private static readonly OpenVinoOptimizationObjective[] UnsupportedLegacyCacheObjectives =
    [
        OpenVinoOptimizationObjective.Balanced,
        OpenVinoOptimizationObjective.Efficiency
    ];

    [TestMethod]
    [TestCategory("StableRouteAcceptance")]
    [Timeout(600_000)]
    public async Task SealedWorkerProducesDistinctFp16Int8Int4PackagesOffline()
    {
        string stage = RequireStage();
        OpenVinoRouteService unusedRoute = new(new UnusedWorkerClient());
        string manifestDigest = Digest(Path.Combine(stage, "converter-manifest.json"));
        SealedOpenVinoOptimizationPipeline pipeline = new(
            stage,
            manifestDigest,
            unusedRoute);
        SealedOpenVinoConversionPipeline converter = new(
            stage,
            manifestDigest,
            unusedRoute);
        Guid operationId = Guid.NewGuid();
        string root = Path.Combine(
            Path.GetTempPath(), "GraniteEdgeAI-OptimizationE2E-" + operationId.ToString("N"));
        string source = Path.Combine(root, "source");
        string baseline = Path.Combine(root, "baseline");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(baseline);
        CopySourceFixture(source);
        try
        {
            await converter.ConvertAsync(
                new OpenVinoConverterInvocation(
                    operationId,
                    source,
                    baseline,
                    new string('a', 64)),
                CancellationToken.None);
            Dictionary<OpenVinoOptimizationObjective, long> sizes = [];
            foreach (OpenVinoOptimizationObjective objective in PersistentObjectives)
            {
                string staging = Path.Combine(root, objective.ToString());
                Directory.CreateDirectory(staging);
                OpenVinoOptimizationCandidate candidate =
                    OpenVinoOptimizationLegacyRegistryV1.GetRequired(objective);
                OpenVinoOptimizationCompletion result = await pipeline.OptimizeAsync(
                    new OpenVinoOptimizationInvocation(
                        Guid.NewGuid(),
                        baseline,
                        staging,
                        new string('b', 64),
                        candidate),
                    CancellationToken.None);
                Assert.AreEqual(candidate.PersistentArtifact.WeightPrecision,
                    result.ActualWeightPrecision);
                Assert.IsTrue(File.Exists(Path.Combine(staging, "openvino_model.xml")));
                Assert.IsTrue(File.Exists(Path.Combine(staging, "openvino_tokenizer.xml")));
                sizes.Add(objective, new FileInfo(
                    Path.Combine(staging, "openvino_model.bin")).Length);
            }
            Assert.IsTrue(sizes[OpenVinoOptimizationObjective.Balanced] <
                sizes[OpenVinoOptimizationObjective.Quality]);
            Assert.IsTrue(sizes[OpenVinoOptimizationObjective.Efficiency] <
                sizes[OpenVinoOptimizationObjective.Balanced]);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("StableRouteAcceptance")]
    [Timeout(600_000)]
    public async Task SupportedLegacyCandidatesPublishAndEnabledCacheFailsClosed()
    {
        string converterStage = RequireStage();
        string? officialValue = Environment.GetEnvironmentVariable(
            "OPENVINO_OFFICIAL_WORKER_STAGE_A");
        if (string.IsNullOrWhiteSpace(officialValue))
        {
            Assert.Inconclusive("OPENVINO_OFFICIAL_WORKER_STAGE_A is required.");
        }
        string officialStage = Path.GetFullPath(officialValue!);
        string manifestDigest = Digest(Path.Combine(
            converterStage, "converter-manifest.json"));
        OpenVinoRouteService route = CreateRoute(officialStage);
        SealedOpenVinoConversionPipeline converter = new(
            converterStage, manifestDigest, route);
        OpenVinoOptimizationService service = new(
            new SealedOpenVinoOptimizationPipeline(
                converterStage, manifestDigest, route),
            _ => true);
        string root = Path.Combine(
            Path.GetTempPath(), "GraniteEdgeAI-OptimizationServiceE2E-" +
            Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "source");
        string baseline = Path.Combine(root, "baseline");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(baseline);
        CopySourceFixture(source);
        try
        {
            await converter.ConvertAsync(
                new OpenVinoConverterInvocation(
                    Guid.NewGuid(), source, baseline, new string('a', 64)),
                CancellationToken.None);
            foreach (OpenVinoOptimizationObjective objective in SupportedLegacyObjectives)
            {
                OpenVinoOptimizationCandidate candidate =
                    OpenVinoOptimizationLegacyRegistryV1.GetRequired(objective);
                string destination = Path.Combine(root, "published-" + objective);
                List<OpenVinoOptimizationStage> stages = [];
                OpenVinoOptimizationResult result = await service.OptimizeLegacyV1Async(
                    new OpenVinoOptimizationLegacyRequestV1(
                        baseline, destination, candidate, Confirmed: true),
                    new InlineProgress<OpenVinoOptimizationProgress>(value =>
                        stages.Add(value.Stage)),
                    CancellationToken.None);
                Assert.AreEqual(OpenVinoOptimizationStatus.Published, result.Status,
                    objective + ": " + result.SupportCode?.ToProtocolValue() +
                    "; stages=" + string.Join(',', stages));
                Assert.AreEqual(candidate.PersistentArtifact.WeightPrecision,
                    result.ActualWeightPrecision);
                Assert.AreEqual(candidate.Runtime.KvCachePrecision,
                    result.ActualKvCachePrecision);
                Assert.AreEqual("CPU", result.ActualDevice);
                Assert.IsTrue(File.Exists(Path.Combine(
                    destination, OpenVinoOptimizationProvenance.FileName)));
                Assert.IsFalse(File.Exists(Path.Combine(
                    destination, OpenVinoProvenance.FileName)));

                OpenVinoRouteInspectionResult independent = await route.InspectAsync(
                    destination, CancellationToken.None);
                try
                {
                    Assert.IsTrue(independent.Outcome is OpenVinoRouteInspectionOutcome.Ready or
                        OpenVinoRouteInspectionOutcome.ReadyWithWarnings,
                        independent.Failure?.SupportCode);
                }
                finally
                {
                    independent.HandoffLease?.Dispose();
                }
            }
            foreach (OpenVinoOptimizationObjective objective in
                     UnsupportedLegacyCacheObjectives)
            {
                OpenVinoOptimizationCandidate candidate =
                    OpenVinoOptimizationLegacyRegistryV1.GetRequired(objective);
                string destination = Path.Combine(root, "rejected-" + objective);
                List<OpenVinoOptimizationStage> stages = [];

                OpenVinoOptimizationResult result = await service.OptimizeLegacyV1Async(
                    new OpenVinoOptimizationLegacyRequestV1(
                        baseline, destination, candidate, Confirmed: true),
                    new InlineProgress<OpenVinoOptimizationProgress>(value =>
                        stages.Add(value.Stage)),
                    CancellationToken.None);

                Assert.AreEqual(OpenVinoOptimizationStatus.Failed, result.Status);
                Assert.AreEqual(OpenVinoSupportCode.OptimizationUnsupported,
                    result.SupportCode);
                CollectionAssert.AreEqual(
                    new[] { OpenVinoOptimizationStage.Preflight },
                    stages);
                Assert.IsFalse(Directory.Exists(destination));
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("StableRouteAcceptance")]
    [Timeout(600_000)]
    public async Task ProjectedC1PlanPublishesSchemaV2PackageThroughSealedPipeline()
    {
        string converterStage = RequireStage();
        string officialStage = RequireOfficialStage();
        string manifestDigest = Digest(Path.Combine(
            converterStage, "converter-manifest.json"));
        OpenVinoRouteService route = CreateRoute(officialStage);
        SealedOpenVinoConversionPipeline converter = new(
            converterStage, manifestDigest, route);
        OpenVinoOptimizationService service = new(
            new SealedOpenVinoOptimizationPipeline(
                converterStage, manifestDigest, route),
            _ => true);
        string root = Path.Combine(
            Path.GetTempPath(), "GraniteEdgeAI-C1-OptimizationE2E-" +
            Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "source");
        string baseline = Path.Combine(root, "baseline");
        string destination = Path.Combine(root, "published");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(baseline);
        CopySourceFixture(source);
        try
        {
            await converter.ConvertAsync(
                new OpenVinoConverterInvocation(
                    Guid.NewGuid(), source, baseline, new string('a', 64)),
                CancellationToken.None);
            OpenVinoStaticPackageEvidence baselineEvidence =
                new OpenVinoStaticPackageInspector().Inspect(baseline).Evidence!;
            OpenVinoCapabilityPayload payload =
                OpenVinoOptimizationCapabilityProjector.Project(
                    NativeCapabilityEvidence());
            OpenVinoAdmittedConfiguration admission = payload.Admitted.Single(
                static item => item.EvidenceId == "OV-STD-CPU-INT8-U8-01");
            OptimizationCapabilitySnapshot snapshot =
                OptimizationCapabilitySnapshot.ForOpenVino(
                    "ov-native-e2e-1",
                    DigestCapability(payload),
                    payload);
            OptimizationExecutionPlan plan = IssuePlan(
                admission,
                snapshot,
                baselineEvidence);
            OpenVinoOptimizationAdaptation adaptation =
                OpenVinoOptimizationPlanAdapter.Adapt(
                    plan,
                    snapshot,
                    baselineEvidence.ModelSha256,
                    checked((ulong)baselineEvidence.ModelLengthBytes));
            Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.Ready,
                adaptation.Status);
            Assert.AreEqual(admission.EvidenceId, adaptation.Candidate!.EvidenceId);

            OptimizationExecutionResult result = await service.ExecuteAsync(
                new OpenVinoOptimizationRequest(
                    baseline,
                    destination,
                    plan,
                    new FixedCurrentStateProvider(new(
                        snapshot,
                        plan.Binding.ModelInspectionRunId,
                        plan.Binding.ModelInspectionHandoffId,
                        plan.Binding.ProductHardwareRunId,
                        plan.Binding.HardwareSnapshotSha256)),
                    Confirmed: true),
                progress: null,
                CancellationToken.None);

            Assert.AreEqual(OptimizationExecutionStatus.SucceededPersistent,
                result.Status);
            Assert.AreEqual(plan.OptimizationPlanId, result.OptimizationPlanId);
            Assert.AreEqual(plan.ConfigurationSha256, result.ConfigurationSha256);
            Assert.IsNotNull(result.OutputIdentity);
            Assert.IsNotNull(result.OutputManifestSha256);
            Assert.IsGreaterThan(0UL, result.OutputSizeBytes);
            OpenVinoOptimizationProvenance provenance =
                OpenVinoOptimizationProvenance.Read(destination);
            Assert.AreEqual(OpenVinoOptimizationProvenance.CurrentSchemaVersion,
                provenance.SchemaVersion);
            Assert.AreEqual(plan.OptimizationPlanId, provenance.OptimizationPlanId);
            Assert.AreEqual(plan.ConfigurationSha256,
                provenance.ConfigurationSha256);
            Assert.AreEqual(snapshot.SnapshotId,
                provenance.CapabilitySnapshotId);
            Assert.AreEqual(snapshot.CapabilitySnapshotSha256,
                provenance.CapabilitySnapshotSha256);
            Assert.AreEqual(result.OutputManifestSha256,
                provenance.OutputManifestSha256);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static OpenVinoRouteService CreateRoute(string stage)
    {
        string manifestDigest = Digest(Path.Combine(stage, "worker-manifest.json"));
        OpenVinoBuildEvidence evidence = new(
            "2026.3.0-22451-8a17657b995-releases/2026/3",
            "2026.3.0.0-3277-bd8d6542e3c",
            "2026.3.0.0-703-183c6f25cda",
            manifestDigest);
        Dictionary<string, OpenVinoWorkerBinaryMachine> machines =
            new(StringComparer.Ordinal)
            {
                ["OpenVinoOfficial.Worker.exe"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_genai.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_intel_cpu_plugin.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_intel_gpu_plugin.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_ir_frontend.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_tokenizers.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["tbb12.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["tbbbind_2_5.dll"] = OpenVinoWorkerBinaryMachine.Amd64
            };
        OpenVinoWorkerInstallation installation = new(
            stage,
            "OpenVinoOfficial.Worker.exe",
            OpenVinoProtocol.OfficialProtocolId,
            evidence,
            machines);
        return new OpenVinoRouteService(
            new OpenVinoWorkerClient(
                OpenVinoWorkerClientOptions.CreateDefault(installation)),
            evidence);
    }

    private static string RequireOfficialStage()
    {
        string? value = Environment.GetEnvironmentVariable(
            "OPENVINO_OFFICIAL_WORKER_STAGE_A");
        if (string.IsNullOrWhiteSpace(value) || !Directory.Exists(value))
        {
            Assert.Inconclusive("OPENVINO_OFFICIAL_WORKER_STAGE_A is required.");
        }
        return Path.GetFullPath(value!);
    }

    private static OpenVinoOptimizationCapabilityEvidence NativeCapabilityEvidence() =>
        new(
            new OpenVinoOptimizationToolVersions(
                OpenVino: "2026.3.0",
                OpenVinoGenAi: "2026.3.0.0",
                Nncf: "3.3.0",
                Optimum: "2.3.0",
                OptimumIntel: "2.1.0",
                Transformers: "5.5.4"),
            [
                new OpenVinoOptimizationCapabilityAdmission(
                    "OV-STD-CPU-INT8-U8-01",
                    Device: "CPU",
                    OpenVinoWeightPrecision.EightBit,
                    new OpenVinoRuntimeOptimization(
                        OpenVinoKvCachePrecision.U8,
                        RouteCompiledCachePolicy.Disabled),
                    OpenVinoCapabilityPerformanceHint.Latency,
                    Streams: 1,
                    MinimumContextTokens: 4_096,
                    MaximumContextTokens: 4_096,
                    OpenVinoCapabilityMaturity.Released)
            ]);

    private static OptimizationExecutionPlan IssuePlan(
        OpenVinoAdmittedConfiguration admission,
        OptimizationCapabilitySnapshot snapshot,
        OpenVinoStaticPackageEvidence source)
    {
        OpenVinoRouteConfiguration configuration = OpenVinoRouteConfiguration.Create(
            admission.Weights,
            admission.KvCache,
            admission.Device,
            admission.PerformanceHint,
            admission.CompiledCache,
            admission.Streams);
        ulong sourceLength = checked((ulong)source.ModelLengthBytes);
        OptimizationCandidate candidate = OptimizationCandidate.Create(
            configuration,
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Measured,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                contextTokens: 4_096,
                predictedPeakBytes: 2UL * 1024 * 1024 * 1024,
                safeBudgetBytes: 8UL * 1024 * 1024 * 1024,
                headroomBytes: 6UL * 1024 * 1024 * 1024,
                workingDiskBytes: sourceLength,
                outputDiskBytes: Math.Max(1UL, sourceLength / 2),
                requiresPersistentChange: true),
            admission.EvidenceId,
            isExperimental: false);
        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            [candidate], OptimizationPreferenceSelection.Manual(50))!;
        return OptimizationPlanIssuer.Issue(
            selection,
            snapshot,
            OptimizationWorkload.Create(
                "chat",
                minimumContextTokens: 512,
                OptimizationAssessment.Poor,
                [ContextTokenCount.FromTokens(4_096)]),
            OptimizationJourneyBinding.Create(
                "mi-native-e2e-1",
                "mi-handoff-native-e2e-1",
                source.ModelSha256,
                sourceLength,
                "hw-native-e2e-1",
                new string('2', 64)),
            DateTimeOffset.UnixEpoch);
    }

    private static string DigestCapability(OpenVinoCapabilityPayload payload)
    {
        using MemoryStream canonical = new();
        using (BinaryWriter writer = new(canonical, System.Text.Encoding.UTF8, true))
        {
            writer.Write(payload.RuntimeVersion);
            foreach (OpenVinoAdmittedConfiguration admission in payload.Admitted)
            {
                writer.Write(admission.EvidenceId);
                writer.Write((int)admission.Device);
                writer.Write((int)admission.Weights);
                writer.Write((int)admission.KvCache);
                writer.Write((int)admission.PerformanceHint);
                writer.Write((int)admission.CompiledCache);
                writer.Write(admission.Streams);
                writer.Write(admission.MinimumContextTokens);
                writer.Write(admission.MaximumContextTokens);
                writer.Write((int)admission.Level);
                writer.Write(admission.RequiresEvidence);
            }
        }
        return Convert.ToHexString(SHA256.HashData(canonical.ToArray()))
            .ToLowerInvariant();
    }

    private static string RequireStage()
    {
        string? stage = Environment.GetEnvironmentVariable("GRANITE_OPENVINO_CONVERTER_STAGE");
        if (string.IsNullOrWhiteSpace(stage) || !Directory.Exists(stage))
        {
            Assert.Inconclusive("GRANITE_OPENVINO_CONVERTER_STAGE is required.");
        }
        return Path.GetFullPath(stage!);
    }

    private static string Digest(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static void CopySourceFixture(string destination)
    {
        string repository = FindRepositoryRoot();
        string source = Path.Combine(
            repository, "tests", "TestFixtures", "OpenVINO", "Converter",
            "TinyGraniteV1", "source");
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed class UnusedWorkerClient : IOpenVinoWorkerClient
    {
        public Task<IOpenVinoEvent> InspectAsync(
            StartInspectionCommand command,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException("Native inspection is not part of optimizer export.");

        public Task<OpenVinoConversation> StartSessionAsync(
            StartSessionCommand command,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException("Prompting is not part of optimizer export.");
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
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
}
