using System.Security.Cryptography;
using System.Text.Json;
using GraniteEdgeAI.GgufQuantization.Contracts;
using GraniteEdgeAI.GgufQuantization.FakeQuantizer;
using GraniteEdgeAI.GgufQuantization.WorkerClient;

namespace GraniteEdgeAI.GgufQuantization.WorkerClient.Tests;

[TestClass]
public sealed class GgufQuantizationWorkerClientTests
{
    [TestMethod]
    public void ArgumentsAddRequantizeExactlyOnceOnlyWhenAuthorized()
    {
        string[] ordinary = GgufQuantizationWorkerClient.BuildArguments(
            Command(GgufQuantizationFormat.F16, GgufQuantizationFormat.Q4KM, null),
            "source.gguf", "output.gguf");
        string[] requantized = GgufQuantizationWorkerClient.BuildArguments(
            Command(GgufQuantizationFormat.Q4KM, GgufQuantizationFormat.Q3KM, Digest('a')),
            "source.gguf", "output.gguf");

        Assert.IsFalse(ordinary.Contains("--allow-requantize", StringComparer.Ordinal));
        Assert.AreEqual(1, requantized.Count(value => value == "--allow-requantize"));
        CollectionAssert.AreEqual(
            new[] { "--allow-requantize", "source.gguf", "output.gguf", "Q3_K_M" },
            requantized);
    }

    [TestMethod]
    public async Task VerifiedClientCreatesBoundOutputAndPreservesSource()
    {
        using var fixture = new QuantizerFixture();
        VerifiedGgufQuantizerPackage package =
            GgufQuantizerPackageVerifier.Verify(fixture.Stage, fixture.ManifestSha256);
        GgufQuantizationCommand command = Command(
            GgufQuantizationFormat.F16, GgufQuantizationFormat.Q4KM, null,
            fixture.ManifestSha256);
        using GgufQuantizationFileLease lease = GgufQuantizationFileLease.Create(
            fixture.SourcePath, fixture.SourceSha256, fixture.SourceLength,
            fixture.OutputPath);

        GgufQuantizationEvent result = await new GgufQuantizationWorkerClient(
            TimeSpan.FromSeconds(20)).ExecuteAsync(command, package, lease, CancellationToken.None);

        Assert.AreEqual(GgufQuantizationEventKind.Completed, result.Kind);
        Assert.IsTrue(File.Exists(fixture.OutputPath));
        Assert.AreEqual(fixture.SourceSha256, Sha(fixture.SourcePath));
    }

    [TestMethod]
    public async Task VerifiedClientDoesNotExposeUnapprovedParentEnvironment()
    {
        const string sentinel = "GRANITE_SECURITY_AUDIT_SENTINEL";
        string? previous = Environment.GetEnvironmentVariable(sentinel);
        try
        {
            Environment.SetEnvironmentVariable(sentinel, "parent-secret");
            using var fixture = new QuantizerFixture();
            VerifiedGgufQuantizerPackage package =
                GgufQuantizerPackageVerifier.Verify(
                    fixture.Stage,
                    fixture.ManifestSha256);
            using GgufQuantizationFileLease lease =
                GgufQuantizationFileLease.Create(
                    fixture.SourcePath,
                    fixture.SourceSha256,
                    fixture.SourceLength,
                    fixture.OutputPath);

            GgufQuantizationEvent result = await new GgufQuantizationWorkerClient(
                TimeSpan.FromSeconds(20)).ExecuteAsync(
                    Command(
                        GgufQuantizationFormat.F16,
                        GgufQuantizationFormat.Q4KM,
                        null,
                        fixture.ManifestSha256),
                    package,
                    lease,
                    CancellationToken.None);

            Assert.AreEqual(GgufQuantizationEventKind.Completed, result.Kind);
            byte observation = File.ReadAllBytes(fixture.OutputPath)[^1];
            Assert.AreEqual(
                (byte)'A',
                observation,
                "The verified child observed an unapproved parent environment variable.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(sentinel, previous);
        }
    }

    [TestMethod]
    public async Task CancellationDeletesOnlyThePendingOutput()
    {
        using var fixture = new QuantizerFixture();
        File.WriteAllText(fixture.FakeDelayMarker, "delay");
        VerifiedGgufQuantizerPackage package =
            GgufQuantizerPackageVerifier.Verify(fixture.Stage, fixture.ManifestSha256);
        using GgufQuantizationFileLease lease = GgufQuantizationFileLease.Create(
            fixture.SourcePath, fixture.SourceSha256, fixture.SourceLength,
            fixture.OutputPath);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            new GgufQuantizationWorkerClient(TimeSpan.FromSeconds(20)).ExecuteAsync(
                Command(GgufQuantizationFormat.F16, GgufQuantizationFormat.Q4KM, null, fixture.ManifestSha256),
                package, lease, cancellation.Token));

        Assert.IsFalse(File.Exists(fixture.OutputPath));
        Assert.AreEqual(fixture.SourceSha256, Sha(fixture.SourcePath));
    }

    private static GgufQuantizationCommand Command(
        GgufQuantizationFormat source,
        GgufQuantizationFormat target,
        string? authorization,
        string? manifest = null) =>
        GgufQuantizationCommand.Create(
            Guid.NewGuid(), Guid.NewGuid(), Digest('b'), "source-token", "output-token",
            source, target, manifest ?? Digest('c'),
            GgufQuantizationProtocol.RequantizationPolicyVersion, authorization);

    private static string Digest(char value) => new(value, 64);

    private static string Sha(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed class QuantizerFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "geai-quant-test-" + Guid.NewGuid().ToString("N"));

        internal QuantizerFixture()
        {
            Stage = Path.Combine(_root, "stage");
            Directory.CreateDirectory(Path.Combine(Stage, "bin"));
            Directory.CreateDirectory(Path.Combine(Stage, "licenses"));
            string fakeDirectory = Path.GetDirectoryName(typeof(FakeQuantizerMarker).Assembly.Location)!;
            foreach (string file in Directory.EnumerateFiles(fakeDirectory, "llama-quantize*"))
            {
                File.Copy(file, Path.Combine(Stage, "bin", Path.GetFileName(file)));
            }
            File.WriteAllText(Path.Combine(Stage, "licenses", "LICENSE.llama.cpp.txt"), "MIT");
            SourcePath = Path.Combine(_root, "source.gguf");
            OutputPath = Path.Combine(_root, "output.gguf");
            FakeDelayMarker = SourcePath + ".delay";
            File.WriteAllBytes(SourcePath, [1, 2, 3, 4]);
            SourceSha256 = Sha(SourcePath);
            SourceLength = (ulong)new FileInfo(SourcePath).Length;
            WriteManifest();
            ManifestSha256 = Sha(Path.Combine(Stage, "llama-quantize.package.manifest.json"));
        }

        internal string Stage { get; }
        internal string SourcePath { get; }
        internal string OutputPath { get; }
        internal string FakeDelayMarker { get; }
        internal string SourceSha256 { get; }
        internal ulong SourceLength { get; }
        internal string ManifestSha256 { get; private set; } = string.Empty;

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }

        private void WriteManifest()
        {
            var files = Directory.EnumerateFiles(Stage, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(path => new
                {
                    relativePath = Path.GetRelativePath(Stage, path).Replace('\\', '/'),
                    length = new FileInfo(path).Length,
                    sha256 = Sha(path),
                }).ToArray();
            var manifest = new
            {
                schemaVersion = 1,
                packageId = "granite-edge-ai-llama-quantize-x64",
                source = new { url = "https://github.com/ggml-org/llama.cpp.git", commit = "3f7c29d318e317b63f54c558bc69803963d7d88c" },
                target = "llama-quantize",
                architecture = "x64",
                configuration = "Release",
                libraryLinkage = "static",
                cmakeFlags = new[] { "GGML_NATIVE=OFF", "GGML_OPENMP=OFF", "LLAMA_CURL=OFF", "BUILD_SHARED_LIBS=OFF" },
                toolchain = new { visualStudio = "test", msvc = "test", cmake = "test" },
                osProvidedDependencies = Array.Empty<string>(),
                appLocalDependencies = Array.Empty<string>(),
                executableRelativePath = "bin/llama-quantize.exe",
                allowedTokens = new[] { "Q2_K", "Q3_K_M", "Q4_K_M", "Q5_K_M", "Q6_K", "Q8_0" },
                maximumSourceBytes = 1024L,
                maximumOutputBytes = 2048L,
                timeoutSeconds = 20,
                standardOutputMaximumBytes = 4096,
                standardErrorMaximumBytes = 4096,
                license = new { identity = "MIT", relativePath = "licenses/LICENSE.llama.cpp.txt" },
                files,
            };
            File.WriteAllText(
                Path.Combine(Stage, "llama-quantize.package.manifest.json"),
                JsonSerializer.Serialize(manifest));
        }
    }
}
