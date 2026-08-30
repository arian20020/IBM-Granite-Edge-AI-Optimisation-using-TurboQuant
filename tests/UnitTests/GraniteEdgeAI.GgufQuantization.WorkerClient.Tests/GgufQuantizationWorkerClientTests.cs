using System.Security.Cryptography;
using System.Diagnostics;
using System.Text.Json;
using GraniteEdgeAI.GgufQuantization.Contracts;
using GraniteEdgeAI.GgufQuantization.WorkerClient;

namespace GraniteEdgeAI.GgufQuantization.WorkerClient.Tests;

[TestClass]
[DoNotParallelize]
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

        OperationCanceledException failure =
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            new GgufQuantizationWorkerClient(TimeSpan.FromSeconds(20)).ExecuteAsync(
                Command(GgufQuantizationFormat.F16, GgufQuantizationFormat.Q4KM, null, fixture.ManifestSha256),
                package, lease, cancellation.Token));

        Assert.AreEqual(cancellation.Token, failure.CancellationToken);
        Assert.IsFalse(File.Exists(fixture.OutputPath));
        Assert.AreEqual(fixture.SourceSha256, Sha(fixture.SourcePath));
    }

    [TestMethod]
    public async Task TimeoutTerminatesContainedTreeAndDeletesPendingOutput()
    {
        using var fixture = new QuantizerFixture();
        File.WriteAllText(fixture.FakeDelayMarker, "delay");
        VerifiedGgufQuantizerPackage package =
            GgufQuantizerPackageVerifier.Verify(fixture.Stage, fixture.ManifestSha256);
        using GgufQuantizationFileLease lease = GgufQuantizationFileLease.Create(
            fixture.SourcePath, fixture.SourceSha256, fixture.SourceLength,
            fixture.OutputPath);

        GgufQuantizationEvent result = await new GgufQuantizationWorkerClient(
            TimeSpan.FromMilliseconds(100)).ExecuteAsync(
                Command(GgufQuantizationFormat.F16, GgufQuantizationFormat.Q4KM, null,
                    fixture.ManifestSha256),
                package,
                lease,
                CancellationToken.None);

        Assert.AreEqual(GgufQuantizationEventKind.Failed, result.Kind);
        Assert.AreEqual(GgufQuantizationSupportCode.TimedOut, result.SupportCode);
        Assert.IsFalse(File.Exists(fixture.OutputPath));
    }

    [TestMethod]
    public async Task NonzeroExitIsContainedAndMappedWithoutPublishingOutput()
    {
        using var fixture = new QuantizerFixture();
        File.WriteAllText(fixture.FakeFailureMarker, "fail");
        VerifiedGgufQuantizerPackage package =
            GgufQuantizerPackageVerifier.Verify(fixture.Stage, fixture.ManifestSha256);
        using GgufQuantizationFileLease lease = GgufQuantizationFileLease.Create(
            fixture.SourcePath, fixture.SourceSha256, fixture.SourceLength,
            fixture.OutputPath);

        GgufQuantizationEvent result = await new GgufQuantizationWorkerClient(
            TimeSpan.FromSeconds(20)).ExecuteAsync(
                Command(GgufQuantizationFormat.F16, GgufQuantizationFormat.Q4KM, null,
                    fixture.ManifestSha256),
                package,
                lease,
                CancellationToken.None);

        Assert.AreEqual(GgufQuantizationSupportCode.ProcessFailed, result.SupportCode);
        Assert.IsFalse(File.Exists(fixture.OutputPath));
    }

    [TestMethod]
    public async Task OversizedStandardOutputIsContainedAndMappedAsProtocolViolation()
    {
        using var fixture = new QuantizerFixture();
        File.WriteAllText(fixture.FakeNoiseMarker, "noise");
        VerifiedGgufQuantizerPackage package =
            GgufQuantizerPackageVerifier.Verify(fixture.Stage, fixture.ManifestSha256);
        using GgufQuantizationFileLease lease = GgufQuantizationFileLease.Create(
            fixture.SourcePath, fixture.SourceSha256, fixture.SourceLength,
            fixture.OutputPath);

        GgufQuantizationEvent result = await new GgufQuantizationWorkerClient(
            TimeSpan.FromSeconds(20)).ExecuteAsync(
                Command(GgufQuantizationFormat.F16, GgufQuantizationFormat.Q4KM, null,
                    fixture.ManifestSha256),
                package,
                lease,
                CancellationToken.None);

        Assert.AreEqual(GgufQuantizationSupportCode.ProtocolViolation, result.SupportCode);
        Assert.IsFalse(File.Exists(fixture.OutputPath));
    }

    [TestMethod]
    public async Task OversizedOutputImmediatelyTerminatesChildThatWouldOtherwiseHang()
    {
        using var fixture = new QuantizerFixture();
        File.WriteAllText(fixture.FakeNoiseMarker, "noise");
        File.WriteAllText(fixture.FakeHangAfterNoiseMarker, "hang");
        VerifiedGgufQuantizerPackage package =
            GgufQuantizerPackageVerifier.Verify(fixture.Stage, fixture.ManifestSha256);
        using GgufQuantizationFileLease lease = GgufQuantizationFileLease.Create(
            fixture.SourcePath, fixture.SourceSha256, fixture.SourceLength,
            fixture.OutputPath);
        var elapsed = System.Diagnostics.Stopwatch.StartNew();

        GgufQuantizationEvent result = await new GgufQuantizationWorkerClient(
            TimeSpan.FromSeconds(30)).ExecuteAsync(
                Command(GgufQuantizationFormat.F16, GgufQuantizationFormat.Q4KM, null,
                    fixture.ManifestSha256),
                package,
                lease,
                CancellationToken.None);

        Assert.AreEqual(GgufQuantizationSupportCode.ProtocolViolation, result.SupportCode);
        Assert.IsTrue(elapsed.Elapsed < TimeSpan.FromSeconds(15));
        Assert.IsFalse(File.Exists(fixture.OutputPath));
    }

    [TestMethod]
    public void PackageVerifierRejectsDuplicateJsonProperties()
    {
        using var fixture = new QuantizerFixture();
        fixture.RewriteManifest(json => json.Replace(
            "\"schemaVersion\":1",
            "\"schemaVersion\":1,\"schemaVersion\":1",
            StringComparison.Ordinal));

        _ = Assert.ThrowsExactly<InvalidDataException>(() =>
            GgufQuantizerPackageVerifier.Verify(
                fixture.Stage,
                fixture.ManifestSha256));
    }

    [TestMethod]
    public void PackageVerifierRejectsCaseConfusedManifestMemberIdentity()
    {
        using var fixture = new QuantizerFixture();
        fixture.RewriteManifest(json => json.Replace(
            "\"relativePath\":\"bin/llama-quantize.exe\"",
            "\"relativePath\":\"bin/LLAMA-quantize.exe\"",
            StringComparison.Ordinal));

        _ = Assert.ThrowsExactly<InvalidDataException>(() =>
            GgufQuantizerPackageVerifier.Verify(
                fixture.Stage,
                fixture.ManifestSha256));
    }

    [TestMethod]
    public void PackageVerifierRejectsReparsePointManifest()
    {
        using var fixture = new QuantizerFixture();
        try
        {
            fixture.ReplaceManifestWithSymbolicLink();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Assert.Inconclusive($"File symbolic links are unavailable: {exception.GetType().Name}.");
        }

        _ = Assert.ThrowsExactly<InvalidDataException>(() =>
            GgufQuantizerPackageVerifier.Verify(
                fixture.Stage,
                fixture.ManifestSha256));
    }

    [TestMethod]
    public void PackageVerifierRejectsReparsePointInStageAncestor()
    {
        using var fixture = new QuantizerFixture();
        string linkedStage;
        try
        {
            linkedStage = fixture.CreateStageThroughReparseAncestor();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Assert.Inconclusive(
                $"Directory symbolic links are unavailable: {exception.GetType().Name}.");
            return;
        }

        _ = Assert.ThrowsExactly<InvalidDataException>(() =>
            GgufQuantizerPackageVerifier.Verify(
                linkedStage,
                fixture.ManifestSha256));
    }

    [TestMethod]
    public void PackageVerifierRejectsReparsePointInIntermediatePackageDirectory()
    {
        using var fixture = new QuantizerFixture();
        fixture.ReplaceBinWithDirectorySymbolicLink();

        _ = Assert.ThrowsExactly<InvalidDataException>(() =>
            GgufQuantizerPackageVerifier.Verify(
                fixture.Stage,
                fixture.ManifestSha256));
    }

    [TestMethod]
    public async Task PackagingScriptRejectsReparsePointInIntermediatePackageDirectory()
    {
        using var fixture = new QuantizerFixture();
        fixture.ReplaceBinWithDirectorySymbolicLink();

        PackagingVerifierResult result = await RunPackagingVerifierAsync(
            fixture.Stage,
            fixture.ManifestSha256);

        Assert.AreNotEqual(
            0,
            result.ExitCode,
            "The production packaging verifier accepted a redirected package directory.");
        StringAssert.Contains(
            result.StandardError,
            "quantizer_package_directory_redirected",
            "The packaging verifier did not reject the directory before traversal.");
    }

    [TestMethod]
    public void PackageVerifierRejectsOversizedManifestBeforeReadingItsBytes()
    {
        using var fixture = new QuantizerFixture();
        fixture.ReplaceManifestWithOversizedSparseFile();

        InvalidDataException failure = Assert.ThrowsExactly<InvalidDataException>(() =>
            GgufQuantizerPackageVerifier.Verify(fixture.Stage, Digest('0')));

        Assert.AreEqual(
            "The quantizer manifest exceeds the size limit.",
            failure.Message);
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

    private static async Task<PackagingVerifierResult> RunPackagingVerifierAsync(
        string stage,
        string manifestSha256)
    {
        string script = FindRepositoryFile(
            "scripts",
            "gguf-quantization",
            "Test-GgufQuantizerPackage.ps1");
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(
                Environment.SystemDirectory,
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in new[]
        {
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            script,
            "-StageDirectory",
            stage,
            "-ExpectedManifestSha256",
            manifestSha256,
        })
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment.Clear();
        foreach (string key in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP" })
        {
            string? value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                start.Environment[key] = value;
            }
        }
        start.Environment["DOTNET_EnableDiagnostics"] = "0";
        start.Environment["DOTNET_EnableDiagnostics_IPC"] = "0";
        start.Environment["DOTNET_EnableDiagnostics_Debugger"] = "0";
        start.Environment["DOTNET_EnableDiagnostics_Profiler"] = "0";

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("The packaging verifier did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await Task.WhenAll(
                process.WaitForExitAsync(timeout.Token),
                output,
                error);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return new PackagingVerifierResult(
            process.ExitCode,
            await output,
            await error);
    }

    private static string FindRepositoryFile(params string[] relativeSegments)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            string candidate = Path.Combine(
                new[] { directory.FullName }.Concat(relativeSegments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("The repository packaging verifier is unavailable.");
    }

    private sealed record PackagingVerifierResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);

    private sealed class QuantizerFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "geai-quant-test-" + Guid.NewGuid().ToString("N"));

        internal QuantizerFixture()
        {
            Stage = Path.Combine(_root, "stage");
            Directory.CreateDirectory(Path.Combine(Stage, "bin"));
            Directory.CreateDirectory(Path.Combine(Stage, "licenses"));
            string fakeDirectory = AppContext.BaseDirectory;
            foreach (string file in Directory.EnumerateFiles(fakeDirectory, "llama-quantize*"))
            {
                File.Copy(file, Path.Combine(Stage, "bin", Path.GetFileName(file)));
            }
            File.WriteAllText(Path.Combine(Stage, "licenses", "LICENSE.llama.cpp.txt"), "MIT");
            SourcePath = Path.Combine(_root, "source.gguf");
            OutputPath = Path.Combine(_root, "output.gguf");
            FakeDelayMarker = SourcePath + ".delay";
            FakeFailureMarker = SourcePath + ".fail";
            FakeNoiseMarker = SourcePath + ".noise";
            FakeHangAfterNoiseMarker = SourcePath + ".hang-after-noise";
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
        internal string FakeFailureMarker { get; }
        internal string FakeNoiseMarker { get; }
        internal string FakeHangAfterNoiseMarker { get; }
        internal string SourceSha256 { get; }
        internal ulong SourceLength { get; }
        internal string ManifestSha256 { get; private set; } = string.Empty;

        internal void RewriteManifest(Func<string, string> transform)
        {
            string manifestPath = ManifestPath;
            File.WriteAllText(
                manifestPath,
                transform(File.ReadAllText(manifestPath)));
            ManifestSha256 = Sha(manifestPath);
        }

        internal void ReplaceManifestWithSymbolicLink()
        {
            string target = Path.Combine(_root, "manifest-target.json");
            File.Move(ManifestPath, target);
            File.CreateSymbolicLink(ManifestPath, target);
            ManifestSha256 = Sha(target);
        }

        internal void ReplaceManifestWithOversizedSparseFile()
        {
            using FileStream manifest = new(
                ManifestPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);
            manifest.SetLength(262_145);
            ManifestSha256 = string.Empty;
        }

        internal string CreateStageThroughReparseAncestor()
        {
            string link = Path.Combine(_root, "linked-parent");
            Directory.CreateSymbolicLink(link, _root);
            return Path.Combine(link, "stage");
        }

        internal void ReplaceBinWithDirectorySymbolicLink()
        {
            string bin = Path.Combine(Stage, "bin");
            string target = Path.Combine(_root, "external-bin");
            Directory.Move(bin, target);
            Directory.CreateSymbolicLink(bin, target);
        }

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

        private string ManifestPath =>
            Path.Combine(Stage, "llama-quantize.package.manifest.json");
    }
}
