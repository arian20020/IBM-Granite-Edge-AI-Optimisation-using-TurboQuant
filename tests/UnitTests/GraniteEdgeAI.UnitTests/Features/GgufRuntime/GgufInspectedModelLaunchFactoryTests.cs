using System.Text;
using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime;

[TestClass]
public sealed class GgufInspectedModelLaunchFactoryTests
{
    private static readonly DateTimeOffset FixedUtc =
        new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void ReadyInspectionCreatesConservativeCpuLaunchFromExactEvidence()
    {
        using var fixture = new LaunchFixture();

        GgufChatLaunchRequest launch = GgufInspectedModelLaunchFactory.Create(
            fixture.Request,
            fixture.Completed(ModelInspectionOutcome.Ready),
            fixture.PackageRoot,
            processorCount: 12);

        Assert.AreEqual(fixture.Request.ModelPath, launch.ModelFile);
        Assert.AreEqual(fixture.ModelSha256, launch.Configuration.ModelSha256);
        Assert.AreEqual("llamasharp-test-cpu", launch.Configuration.RuntimeBuildId);
        Assert.AreEqual(GgufRuntimeBackend.Cpu, launch.Configuration.Backend);
        Assert.AreEqual("cpu", launch.Configuration.DeviceId);
        Assert.AreEqual(0, launch.Configuration.GpuLayerCount);
        Assert.AreEqual(GgufCacheType.F16, launch.Configuration.KeyCacheType);
        Assert.AreEqual(GgufCacheType.F16, launch.Configuration.ValueCacheType);
        Assert.AreEqual(4_096, launch.Configuration.ContextSize);
        Assert.AreEqual(8, launch.Configuration.ThreadCount);
        Assert.AreEqual(512, launch.Configuration.MaximumGeneratedTokens);
        CollectionAssert.AreEqual(fixture.Manifest, launch.TrustedManifest.ToArray());
    }

    [TestMethod]
    public void BlockingInspectionIsRejectedWithoutExposingModelPath()
    {
        using var fixture = new LaunchFixture();

        Exception error = Assert.ThrowsExactly<GgufChatLaunchException>(() =>
            GgufInspectedModelLaunchFactory.Create(
                fixture.Request,
                fixture.Completed(ModelInspectionOutcome.Unsupported),
                fixture.PackageRoot));

        Assert.IsFalse(
            error.ToString().Contains(
                fixture.Request.ModelPath,
                StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void EvidenceForAnotherFileIsRejected()
    {
        using var fixture = new LaunchFixture();
        ModelInspectionExecutionResult execution = fixture.Completed(
            ModelInspectionOutcome.Ready,
            fileName: "another.gguf");

        Assert.ThrowsExactly<GgufChatLaunchException>(() =>
            GgufInspectedModelLaunchFactory.Create(
                fixture.Request,
                execution,
                fixture.PackageRoot));
    }

    private sealed class LaunchFixture : IDisposable
    {
        internal LaunchFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            PackageRoot = Path.Combine(Root, "GgufRuntime");
            Directory.CreateDirectory(PackageRoot);
            string model = Path.Combine(Root, "granite.gguf");
            File.WriteAllBytes(model, [1, 2, 3, 4]);
            File.SetLastWriteTimeUtc(model, FixedUtc.UtcDateTime);
            ModelSha256 = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData([1, 2, 3, 4]));
            Manifest = Encoding.UTF8.GetBytes(
                $$"""
                {"schemaVersion":1,"runtimeBuildId":"llamasharp-test-cpu","runtimeSourceCommit":"{{new string('b', 40)}}","buildFlags":[],"files":[]}
                """);
            File.WriteAllBytes(Path.Combine(PackageRoot, "runtime-manifest.json"), Manifest);
            Request = new ModelInspectionRequest(
                model,
                "granite.gguf",
                new ExpectedModelFileIdentity(4, FixedUtc),
                ValidatedQuickScanSnapshot.CreateGguf(
                    "Granite 4.1 3B",
                    "granite",
                    "3B",
                    "Q4_K_M",
                    4,
                    131_072,
                    3));
        }

        internal string Root { get; }
        internal string PackageRoot { get; }
        internal byte[] Manifest { get; }
        internal string ModelSha256 { get; }
        internal ModelInspectionRequest Request { get; }

        internal ModelInspectionExecutionResult Completed(
            ModelInspectionOutcome outcome,
            string fileName = "granite.gguf")
        {
            var evidence = new ModelInspectionEvidence(
                new ModelInspectionFileEvidence(
                    fileName,
                    new string('a', 64),
                    4,
                    FixedUtc,
                    ModelSha256,
                    integrityPreserved: true),
                new ModelInspectionConfigurationEvidence(
                    "GGUF", 3, "Granite 4.1 3B", "granite", 15, 2,
                    131_072, 2_048, 40, 16, 8, 3_000_000_000),
                new ModelInspectionTokenizerEvidence(
                    "gpt2", 49_152, "BPE", true, 4,
                    new Dictionary<string, int>()),
                new ModelInspectionChatTemplateEvidence(true, 128, new string('c', 64)),
                new ModelInspectionRuntimeIdentity(
                    "worker", "1.0.0", 1, "cpu", "0.27.0", "0.27.0",
                    new string('b', 40), "llama.dll", "X64", "VocabOnly",
                    false, false, 0),
                []);
            var result = new ModelInspectionResult(
                outcome,
                evidence,
                [],
                "Inspection complete.",
                "Open the model in chat.",
                outcome == ModelInspectionOutcome.ConversionRequired ? "route" : null,
                FixedUtc,
                FixedUtc.AddSeconds(1));
            return ModelInspectionExecutionResult.Completed(result);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
