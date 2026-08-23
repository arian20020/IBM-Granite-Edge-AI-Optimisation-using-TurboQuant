using System.Text.Json;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.Tests.Optimization;

[TestClass]
[TestCategory("OpenVinoRoute")]
public sealed class OpenVinoOptimizationTests
{
    private static readonly string[] ExpectedPipelineCalls =
        ["optimize", "validate", "smoke", "reinspect"];

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
                OpenVinoOptimizationRegistry.GetRequired(objective);
            Assert.AreEqual(objective, candidate.Objective);
            Assert.AreEqual(weight, candidate.PersistentArtifact.WeightPrecision);
            Assert.AreEqual(kv, candidate.Runtime.KvCachePrecision);
            Assert.AreEqual("CPU", candidate.Device);
            candidate.Validate();
        }
        Assert.AreEqual(4, OpenVinoOptimizationRegistry.Candidates.Count);
    }

    [TestMethod]
    public void PersistentWeightsRuntimeKvAndCompiledCacheRemainDistinct()
    {
        OpenVinoOptimizationCandidate candidate =
            OpenVinoOptimizationRegistry.GetRequired(OpenVinoOptimizationObjective.Balanced);

        Assert.IsTrue(candidate.PersistentArtifact.CreatesCompletePackage);
        Assert.IsFalse(candidate.Runtime.CreatesModelArtifact);
        Assert.IsTrue(candidate.Runtime.CompiledCache.IsDisposable);
        Assert.IsFalse(candidate.Runtime.CompiledCache.IsModelArtifact);
        Assert.AreEqual(OpenVinoKvCachePrecision.U8, candidate.Runtime.KvCachePrecision);
    }

    [TestMethod]
    public void CandidatesContainNoGgufQuantizationOrLlamaCppFlags()
    {
        string json = JsonSerializer.Serialize(OpenVinoOptimizationRegistry.Candidates);
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
    public async Task ServiceExecutesOnlyRegisteredCandidateAndPublishesAtomically()
    {
        using PackageFixture package = PackageFixture.Create();
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(pipeline, _ => true);
        OpenVinoOptimizationCandidate candidate = OpenVinoOptimizationRegistry.GetRequired(
            OpenVinoOptimizationObjective.Balanced);

        OpenVinoOptimizationResult result = await service.OptimizeAsync(
            new OpenVinoOptimizationRequest(
                package.Source,
                package.Destination,
                candidate,
                Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OpenVinoOptimizationStatus.Published, result.Status);
        Assert.AreEqual(OpenVinoWeightPrecision.EightBit, result.ActualWeightPrecision);
        Assert.AreEqual(OpenVinoKvCachePrecision.U8, result.ActualKvCachePrecision);
        Assert.AreEqual("CPU", result.ActualDevice);
        CollectionAssert.AreEqual(
            ExpectedPipelineCalls,
            pipeline.Calls.ToArray());
        Assert.IsTrue(File.Exists(Path.Combine(
            package.Destination, OpenVinoOptimizationProvenance.FileName)));
        Assert.AreEqual(0, Directory.EnumerateDirectories(
            Path.GetDirectoryName(package.Destination)!,
            ".granite-openvino-*.staging").Count());
    }

    [TestMethod]
    public async Task UnregisteredCandidateFailsBeforeStagingOrOptimization()
    {
        using PackageFixture package = PackageFixture.Create();
        RecordingOptimizationPipeline pipeline = new();
        OpenVinoOptimizationService service = new(pipeline, _ => true);
        OpenVinoOptimizationCandidate registered = OpenVinoOptimizationRegistry.GetRequired(
            OpenVinoOptimizationObjective.Quality);
        OpenVinoOptimizationCandidate counterfeit = registered with
        {
            ConfigurationId = "openvino.standard.cpu.counterfeit.v1"
        };

        OpenVinoOptimizationResult result = await service.OptimizeAsync(
            new OpenVinoOptimizationRequest(
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
            OpenVinoOptimizationProvenance.CurrentSchemaVersion,
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

    private sealed class RecordingOptimizationPipeline : IOpenVinoOptimizationPipeline
    {
        internal List<string> Calls { get; } = [];

        public Task<OpenVinoOptimizationCompletion> OptimizeAsync(
            OpenVinoOptimizationInvocation invocation,
            CancellationToken cancellationToken)
        {
            Calls.Add("optimize");
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
            return Task.FromResult(OpenVinoOptimizationValidation.CreateTestInstance());
        }

        public Task<OpenVinoRuntimeOptimizationEvidence> SmokeAsync(
            OpenVinoOptimizationValidation validation,
            string stagingDirectory,
            OpenVinoRuntimeOptimization runtime,
            CancellationToken cancellationToken)
        {
            Calls.Add("smoke");
            return Task.FromResult(new OpenVinoRuntimeOptimizationEvidence(
                "CPU",
                runtime.KvCachePrecision,
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
