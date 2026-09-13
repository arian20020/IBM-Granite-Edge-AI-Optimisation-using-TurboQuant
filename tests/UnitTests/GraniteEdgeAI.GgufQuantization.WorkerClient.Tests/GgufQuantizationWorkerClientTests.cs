using System.Security.Cryptography;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using GraniteEdgeAI.GgufQuantization.Contracts;
using GraniteEdgeAI.GgufQuantization.WorkerClient;

namespace GraniteEdgeAI.GgufQuantization.WorkerClient.Tests;

[TestClass]
[DoNotParallelize]
public sealed class GgufQuantizationWorkerClientTests
{
    private const string AtomicBotExecutableSha256 =
        "0a17247d4807b520532df54f96ed73f3cf6fa921f879d8d465b44541b41d36e3";

    [TestMethod]
    public void PackageVerifierAcceptsExactAtomicBotStandalonePackage()
    {
        using var fixture = new AtomicBotQuantizerFixture();

        VerifiedGgufQuantizerPackage package =
            GgufQuantizerPackageVerifier.Verify(
                fixture.Stage,
                fixture.ManifestSha256);

        Assert.AreEqual(fixture.ManifestSha256, package.ManifestSha256);
        Assert.AreEqual(AtomicBotExecutableSha256, package.ExecutableSha256);
    }

    [TestMethod]
    public async Task PackagingVerifierAcceptsExactAtomicBotStandalonePackage()
    {
        using var fixture = new AtomicBotQuantizerFixture();

        PackagingVerifierResult result = await RunPackagingVerifierAsync(
            fixture.Stage,
            fixture.ManifestSha256);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        StringAssert.Contains(
            result.StandardOutput,
            "granite-edge-ai-atomicbot-llama-quantize-x64");
    }

    [TestMethod]
    public async Task AtomicBotStagerCreatesAnExactStandalonePackageWithoutWritingTheArchive()
    {
        string repositoryRoot = FindRepositoryRoot();
        string script = Path.Combine(
            repositoryRoot,
            "scripts",
            "gguf-quantization",
            "Stage-AtomicBotQuantizer.ps1");
        Assert.IsTrue(File.Exists(script), "The admitted AtomicBot stager is missing.");
        string archive = Path.Combine(
            repositoryRoot,
            "runtime",
            "gguf",
            "atomicbot",
            "archives",
            "llama-turboquant-windows-x64-cpu.zip");
        string archiveSha256 = Sha(archive);
        string root = Path.Combine(
            Path.GetTempPath(),
            "geai-atomicbot-stage-test-" + Guid.NewGuid().ToString("N"));
        string destination = Path.Combine(root, "standalone");
        Directory.CreateDirectory(root);
        try
        {
            PackagingVerifierResult result = await RunPowerShellAsync(
                script,
                "-ArchivePath", archive,
                "-ExpectedArchiveSha256", archiveSha256,
                "-StageDirectory", destination);

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            Assert.AreEqual(archiveSha256, Sha(archive), "The source archive changed.");
            Assert.AreEqual(
                17,
                Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories).Count(),
                "The standalone package must contain sixteen payloads and one manifest.");
            string manifestPath = Path.Combine(
                destination,
                "llama-quantize.package.manifest.json");
            string manifestSha256 = Sha(manifestPath);
            StringAssert.Contains(
                result.StandardOutput,
                $"GGUF_QUANTIZER_MANIFEST_SHA256={manifestSha256}");
            VerifiedGgufQuantizerPackage package =
                GgufQuantizerPackageVerifier.Verify(destination, manifestSha256);
            Assert.AreEqual(AtomicBotExecutableSha256, package.ExecutableSha256);
            string receiptPath = destination + ".construction-receipt.json";
            Assert.IsTrue(File.Exists(receiptPath));
            using JsonDocument receipt = JsonDocument.Parse(
                File.ReadAllBytes(receiptPath));
            Assert.AreEqual(
                "atomicbot-standalone-quantizer-construction-v1",
                receipt.RootElement.GetProperty("evidenceKind").GetString());
            Assert.AreEqual(
                archiveSha256,
                receipt.RootElement.GetProperty("cpuArchiveSha256").GetString());
            Assert.AreEqual(
                manifestSha256,
                receipt.RootElement.GetProperty("standaloneManifestSha256").GetString());
            Assert.AreEqual(
                17,
                receipt.RootElement.GetProperty("members").GetArrayLength());
            JsonElement manifestMember = receipt.RootElement.GetProperty("members")
                .EnumerateArray()
                .Single(member => string.Equals(
                    member.GetProperty("relativePath").GetString(),
                    "llama-quantize.package.manifest.json",
                    StringComparison.Ordinal));
            Assert.AreEqual(new FileInfo(manifestPath).Length, manifestMember.GetProperty("length").GetInt64());
            Assert.AreEqual(manifestSha256, manifestMember.GetProperty("sha256").GetString());
            AssertCanonicalTranscript(receipt.RootElement, "peImportTranscript");
            AssertCanonicalTranscript(receipt.RootElement, "peHeaderTranscript");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task PackageVerifiersRejectEveryMixedAtomicBotIdentityTuple()
    {
        (string Original, string Mutation)[] mutations =
        [
            ("granite-edge-ai-atomicbot-llama-quantize-x64", "granite-edge-ai-llama-quantize-x64"),
            ("https://github.com/AtomicBot-ai/atomic-llama-cpp-turboquant", "https://github.com/ggml-org/llama.cpp.git"),
            ("519f0c594a8e31467d2e2f2cf17054c9e7e11536", "3f7c29d318e317b63f54c558bc69803963d7d88c"),
            ("\"libraryLinkage\":\"dynamic\"", "\"libraryLinkage\":\"static\""),
            ("not-attested-by-upstream-release", "Visual Studio 18"),
            ("runtime-banner-msvc-19.51.36248.0_pe-linker-14.44", "19.51.36248.0"),
            ("\"maximumSourceBytes\":68719476736", "\"maximumSourceBytes\":68719476735"),
            ("\"timeoutSeconds\":21600", "\"timeoutSeconds\":21599"),
            ("\"Q8_0\"]", "\"Q8_0\",\"TQ3_1S\"]"),
            ("licenses/LICENSE.atomicbot-llama.cpp.txt", "licenses/LICENSE.llama.cpp.txt"),
        ];

        foreach ((string original, string mutation) in mutations)
        {
            using var fixture = new AtomicBotQuantizerFixture();
            fixture.RewriteManifest(json => json.Replace(
                original,
                mutation,
                StringComparison.Ordinal));

            _ = Assert.ThrowsExactly<InvalidDataException>(() =>
                GgufQuantizerPackageVerifier.Verify(
                    fixture.Stage,
                    fixture.ManifestSha256),
                $"The managed verifier accepted mutation: {mutation}");
            PackagingVerifierResult scriptResult = await RunPackagingVerifierAsync(
                fixture.Stage,
                fixture.ManifestSha256);
            Assert.AreNotEqual(
                0,
                scriptResult.ExitCode,
                $"The packaging verifier accepted mutation: {mutation}");
        }
    }

    [TestMethod]
    public async Task AtomicBotStagerRejectsAnExistingDestinationWithoutChangingIt()
    {
        string repositoryRoot = FindRepositoryRoot();
        string script = Path.Combine(repositoryRoot, "scripts", "gguf-quantization", "Stage-AtomicBotQuantizer.ps1");
        string archive = Path.Combine(repositoryRoot, "runtime", "gguf", "atomicbot", "archives", "llama-turboquant-windows-x64-cpu.zip");
        string root = Path.Combine(Path.GetTempPath(), "geai-atomicbot-collision-test-" + Guid.NewGuid().ToString("N"));
        string destination = Path.Combine(root, "standalone");
        Directory.CreateDirectory(destination);
        string marker = Path.Combine(destination, "owned.txt");
        File.WriteAllText(marker, "preserve");
        try
        {
            PackagingVerifierResult result = await RunPowerShellAsync(
                script,
                "-ArchivePath", archive,
                "-ExpectedArchiveSha256", Sha(archive),
                "-StageDirectory", destination);

            Assert.AreNotEqual(0, result.ExitCode);
            Assert.AreEqual("preserve", File.ReadAllText(marker));
            Assert.IsFalse(File.Exists(destination + ".construction-receipt.json"));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task AtomicBotStagerRejectsWrongArchiveIdentityBeforeCreatingTemporaryState()
    {
        string repositoryRoot = FindRepositoryRoot();
        string script = Path.Combine(repositoryRoot, "scripts", "gguf-quantization", "Stage-AtomicBotQuantizer.ps1");
        string archive = Path.Combine(repositoryRoot, "runtime", "gguf", "atomicbot", "archives", "llama-turboquant-windows-x64-cpu.zip");
        string root = Path.Combine(Path.GetTempPath(), "geai-atomicbot-archive-test-" + Guid.NewGuid().ToString("N"));
        string destination = Path.Combine(root, "standalone");
        Directory.CreateDirectory(root);
        try
        {
            PackagingVerifierResult result = await RunPowerShellAsync(
                script,
                "-ArchivePath", archive,
                "-ExpectedArchiveSha256", Digest('0'),
                "-StageDirectory", destination);

            Assert.AreNotEqual(0, result.ExitCode);
            Assert.IsFalse(Directory.Exists(destination));
            Assert.IsFalse(File.Exists(destination + ".construction-receipt.json"));
            Assert.AreEqual(
                0,
                Directory.EnumerateFileSystemEntries(root).Count(),
                "Archive rejection left temporary staging state behind.");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task AtomicBotStagerDoesNotOverwriteAnExistingConstructionReceipt()
    {
        string repositoryRoot = FindRepositoryRoot();
        string script = Path.Combine(repositoryRoot, "scripts", "gguf-quantization", "Stage-AtomicBotQuantizer.ps1");
        string archive = Path.Combine(repositoryRoot, "runtime", "gguf", "atomicbot", "archives", "llama-turboquant-windows-x64-cpu.zip");
        string root = Path.Combine(Path.GetTempPath(), "geai-atomicbot-receipt-test-" + Guid.NewGuid().ToString("N"));
        string destination = Path.Combine(root, "standalone");
        string receipt = destination + ".construction-receipt.json";
        Directory.CreateDirectory(root);
        File.WriteAllText(receipt, "preserve");
        try
        {
            PackagingVerifierResult result = await RunPowerShellAsync(
                script,
                "-ArchivePath", archive,
                "-ExpectedArchiveSha256", Sha(archive),
                "-StageDirectory", destination);

            Assert.AreNotEqual(0, result.ExitCode);
            Assert.AreEqual("preserve", File.ReadAllText(receipt));
            Assert.IsFalse(Directory.Exists(destination));
            Assert.IsFalse(
                Directory.EnumerateDirectories(root, ".standalone.staging-*", SearchOption.TopDirectoryOnly).Any());
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task AtomicBotStagerCancellationRemovesOnlyItsReceiptAndKnownTemporaryState()
    {
        string repositoryRoot = FindRepositoryRoot();
        string stager = Path.Combine(repositoryRoot, "scripts", "gguf-quantization", "Stage-AtomicBotQuantizer.ps1");
        string archive = Path.Combine(repositoryRoot, "runtime", "gguf", "atomicbot", "archives", "llama-turboquant-windows-x64-cpu.zip");
        string root = Path.Combine(Path.GetTempPath(), "geai-atomicbot-cancel-test-" + Guid.NewGuid().ToString("N"));
        string destination = Path.Combine(root, "standalone");
        string controller = Path.Combine(root, "Cancel-AtomicBotStager.ps1");
        Directory.CreateDirectory(root);
        File.WriteAllText(controller, """
            [CmdletBinding()]
            param(
                [Parameter(Mandatory = $true)][string]$Stager,
                [Parameter(Mandatory = $true)][string]$Archive,
                [Parameter(Mandatory = $true)][string]$ArchiveSha256,
                [Parameter(Mandatory = $true)][string]$Destination)

            $ErrorActionPreference = 'Stop'
            $receipt = $Destination + '.construction-receipt.json'
            $job = Start-Job -ScriptBlock {
                param($Script, $Source, $Sha256, $Stage)
                & $Script -ArchivePath $Source -ExpectedArchiveSha256 $Sha256 -StageDirectory $Stage
            } -ArgumentList $Stager, $Archive, $ArchiveSha256, $Destination
            try {
                $deadline = [DateTime]::UtcNow.AddSeconds(20)
                while (-not (Test-Path -LiteralPath $receipt)) {
                    if ($job.State -ne 'Running') {
                        throw "The stager exited before reserving its receipt: $($job.State)"
                    }
                    if ([DateTime]::UtcNow -ge $deadline) {
                        throw 'The stager did not reserve its receipt before the cancellation deadline.'
                    }
                    Start-Sleep -Milliseconds 10
                }
                Stop-Job -Job $job
                Wait-Job -Job $job | Out-Null
            }
            finally {
                Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
            }

            if (Test-Path -LiteralPath $Destination) {
                throw 'Cancellation published the AtomicBot destination.'
            }
            if (Test-Path -LiteralPath $receipt) {
                throw 'Cancellation stranded the AtomicBot construction receipt.'
            }
            if (Get-ChildItem -LiteralPath ([IO.Path]::GetDirectoryName($Destination)) -Directory -Filter '.standalone.staging-*') {
                throw 'Cancellation stranded known AtomicBot temporary staging state.'
            }
            """);
        try
        {
            PackagingVerifierResult result = await RunPowerShellAsync(
                controller,
                "-Stager", stager,
                "-Archive", archive,
                "-ArchiveSha256", Sha(archive),
                "-Destination", destination);

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            Assert.IsFalse(Directory.Exists(destination));
            Assert.IsFalse(File.Exists(destination + ".construction-receipt.json"));
            CollectionAssert.AreEqual(
                new[] { controller },
                Directory.EnumerateFileSystemEntries(root).ToArray(),
                "Cancellation removed unrelated test-owned state or retained stager-owned state.");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task AtomicBotStagerDoesNotOverwriteOrStrandReceiptWhenDestinationAppearsDuringPublication()
    {
        string repositoryRoot = FindRepositoryRoot();
        string script = Path.Combine(repositoryRoot, "scripts", "gguf-quantization", "Stage-AtomicBotQuantizer.ps1");
        string archive = Path.Combine(repositoryRoot, "runtime", "gguf", "atomicbot", "archives", "llama-turboquant-windows-x64-cpu.zip");
        string root = Path.Combine(Path.GetTempPath(), "geai-atomicbot-publish-race-test-" + Guid.NewGuid().ToString("N"));
        string destination = Path.Combine(root, "standalone");
        string receipt = destination + ".construction-receipt.json";
        Directory.CreateDirectory(root);
        try
        {
            using Process process = StartPowerShell(
                script,
                "-ArchivePath", archive,
                "-ExpectedArchiveSha256", Sha(archive),
                "-StageDirectory", destination);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (!File.Exists(receipt))
            {
                if (process.HasExited)
                {
                    Assert.Fail("The stager published the destination before reserving its receipt.");
                }
                await Task.Delay(10, timeout.Token);
            }

            Directory.CreateDirectory(destination);
            string marker = Path.Combine(destination, "owned.txt");
            File.WriteAllText(marker, "preserve");
            await process.WaitForExitAsync(timeout.Token);

            Assert.AreNotEqual(0, process.ExitCode);
            Assert.AreEqual("preserve", File.ReadAllText(marker));
            Assert.IsFalse(File.Exists(receipt), "A failed publication stranded its reserved receipt.");
            Assert.IsFalse(
                Directory.EnumerateDirectories(root, ".standalone.staging-*", SearchOption.TopDirectoryOnly).Any(),
                "A failed publication stranded known temporary staging state.");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task AtomicBotStagerQuarantinesUnexpectedTemporaryContentIntactOnFailure()
    {
        string repositoryRoot = FindRepositoryRoot();
        string script = Path.Combine(repositoryRoot, "scripts", "gguf-quantization", "Stage-AtomicBotQuantizer.ps1");
        string archive = Path.Combine(repositoryRoot, "runtime", "gguf", "atomicbot", "archives", "llama-turboquant-windows-x64-cpu.zip");
        string root = Path.Combine(Path.GetTempPath(), "geai-atomicbot-quarantine-test-" + Guid.NewGuid().ToString("N"));
        string destination = Path.Combine(root, "standalone");
        string receipt = destination + ".construction-receipt.json";
        Directory.CreateDirectory(root);
        try
        {
            using Process process = StartPowerShell(
                script,
                "-ArchivePath", archive,
                "-ExpectedArchiveSha256", Sha(archive),
                "-StageDirectory", destination);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (!File.Exists(receipt))
            {
                if (process.HasExited)
                {
                    Assert.Fail("The stager exited before reserving its receipt.");
                }
                await Task.Delay(10, timeout.Token);
            }

            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "owned.txt"), "preserve");
            string? temporary = null;
            while (temporary is null)
            {
                temporary = Directory.EnumerateDirectories(
                    root,
                    ".standalone.staging-*",
                    SearchOption.TopDirectoryOnly).SingleOrDefault();
                if (temporary is null)
                {
                    if (process.HasExited)
                    {
                        Assert.Fail("The stager exited before creating temporary staging state.");
                    }
                    await Task.Delay(10, timeout.Token);
                }
            }
            string unexpected = Path.Combine(temporary, "unexpected.marker");
            File.WriteAllText(unexpected, "quarantine-intact");
            await process.WaitForExitAsync(timeout.Token);

            Assert.AreNotEqual(0, process.ExitCode);
            Assert.IsFalse(File.Exists(receipt), "Failed publication retained its owned receipt reservation.");
            Assert.AreEqual("quarantine-intact", File.ReadAllText(unexpected));
            Assert.IsTrue(
                Directory.EnumerateFiles(temporary, "*", SearchOption.AllDirectories).Any(
                    path => !string.Equals(path, unexpected, StringComparison.Ordinal)),
                "Cleanup partially deleted known payloads before quarantining the unexpected member.");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task PackageVerifiersRejectAtomicBotMissingExtraAndChangedMembers()
    {
        foreach (Action<AtomicBotQuantizerFixture> mutate in new Action<AtomicBotQuantizerFixture>[]
        {
            fixture => fixture.DeleteMember("bin/ggml.dll"),
            fixture => fixture.AddExtraMember("bin/unlisted.dll"),
            fixture => fixture.ChangeMember("bin/llama.dll"),
        })
        {
            using var fixture = new AtomicBotQuantizerFixture();
            mutate(fixture);
            await AssertAtomicBotPackageRejectedByBothAsync(fixture);
        }
    }

    [TestMethod]
    public async Task PackageVerifiersRejectAtomicBotReparseDirectory()
    {
        using var fixture = new AtomicBotQuantizerFixture();
        try
        {
            fixture.ReplaceBinWithDirectorySymbolicLink();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Assert.Inconclusive(
                $"Directory symbolic links are unavailable: {exception.GetType().Name}.");
            return;
        }

        await AssertAtomicBotPackageRejectedByBothAsync(fixture);
    }

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

    private static void AssertCanonicalTranscript(JsonElement receipt, string propertyName)
    {
        JsonElement transcript = receipt.GetProperty(propertyName);
        string text = transcript.GetProperty("canonicalText").GetString()
            ?? throw new AssertFailedException($"{propertyName} did not retain its canonical text.");
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        Assert.IsTrue(text.EndsWith('\n'), $"{propertyName} is not newline-canonicalized.");
        Assert.AreEqual(
            bytes.Length,
            transcript.GetProperty("utf8Length").GetInt32(),
            $"{propertyName} length does not bind its retained preimage.");
        Assert.AreEqual(
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            transcript.GetProperty("sha256").GetString(),
            $"{propertyName} hash does not bind its retained preimage.");
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

    private static async Task<PackagingVerifierResult> RunPowerShellAsync(
        string script,
        params string[] arguments)
    {
        using Process process = StartPowerShell(script, arguments);
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
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

    private static Process StartPowerShell(string script, params string[] arguments)
    {
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
        }.Concat(arguments))
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

        return Process.Start(start)
            ?? throw new InvalidOperationException("The PowerShell verifier did not start.");
    }

    private static async Task AssertAtomicBotPackageRejectedByBothAsync(
        AtomicBotQuantizerFixture fixture)
    {
        _ = Assert.ThrowsExactly<InvalidDataException>(() =>
            GgufQuantizerPackageVerifier.Verify(
                fixture.Stage,
                fixture.ManifestSha256));
        PackagingVerifierResult scriptResult = await RunPackagingVerifierAsync(
            fixture.Stage,
            fixture.ManifestSha256);
        Assert.AreNotEqual(
            0,
            scriptResult.ExitCode,
            "The packaging verifier accepted a changed AtomicBot closure.");
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("The repository root is unavailable.");
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

    private sealed class AtomicBotQuantizerFixture : IDisposable
    {
        private static readonly string[] AppLocalDependencies =
        [
            "ggml-base.dll",
            "ggml-cpu-alderlake.dll",
            "ggml-cpu-cannonlake.dll",
            "ggml-cpu-cascadelake.dll",
            "ggml-cpu-haswell.dll",
            "ggml-cpu-icelake.dll",
            "ggml-cpu-sandybridge.dll",
            "ggml-cpu-skylakex.dll",
            "ggml-cpu-sse42.dll",
            "ggml-cpu-x64.dll",
            "ggml.dll",
            "llama-common.dll",
            "llama-quantize-impl.dll",
            "llama.dll",
        ];
        private static readonly string[] OsProvidedDependencies =
        [
            "KERNEL32.dll",
            "VCRUNTIME140.dll",
            "api-ms-win-crt-heap-l1-1-0.dll",
            "api-ms-win-crt-locale-l1-1-0.dll",
            "api-ms-win-crt-math-l1-1-0.dll",
            "api-ms-win-crt-runtime-l1-1-0.dll",
            "api-ms-win-crt-stdio-l1-1-0.dll",
        ];
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "geai-atomicbot-quant-test-" + Guid.NewGuid().ToString("N"));

        internal AtomicBotQuantizerFixture()
        {
            Stage = Path.Combine(_root, "stage");
            Directory.CreateDirectory(Path.Combine(Stage, "bin"));
            Directory.CreateDirectory(Path.Combine(Stage, "licenses"));
            string repositoryRoot = FindRepositoryRoot();
            string archive = Path.Combine(
                repositoryRoot,
                "runtime",
                "gguf",
                "atomicbot",
                "archives",
                "llama-turboquant-windows-x64-cpu.zip");
            using ZipArchive zip = ZipFile.OpenRead(archive);
            Copy(zip, "build/bin/llama-quantize.exe", "bin/llama-quantize.exe");
            foreach (string dependency in AppLocalDependencies)
            {
                Copy(zip, $"build/bin/{dependency}", $"bin/{dependency}");
            }
            Copy(
                zip,
                "build/bin/LICENSE",
                "licenses/LICENSE.atomicbot-llama.cpp.txt");
            WriteManifest();
            ManifestSha256 = Sha(Path.Combine(
                Stage,
                "llama-quantize.package.manifest.json"));
        }

        internal string Stage { get; }
        internal string ManifestSha256 { get; private set; }

        internal void RewriteManifest(Func<string, string> transform)
        {
            string manifestPath = Path.Combine(
                Stage,
                "llama-quantize.package.manifest.json");
            string original = File.ReadAllText(manifestPath);
            string changed = transform(original);
            Assert.AreNotEqual(original, changed, "The requested manifest mutation was not applied.");
            File.WriteAllText(manifestPath, changed);
            ManifestSha256 = Sha(manifestPath);
        }

        internal void DeleteMember(string relativePath) =>
            File.Delete(Member(relativePath));

        internal void AddExtraMember(string relativePath) =>
            File.WriteAllText(Member(relativePath), "unlisted");

        internal void ChangeMember(string relativePath)
        {
            using FileStream stream = new(Member(relativePath), FileMode.Append, FileAccess.Write);
            stream.WriteByte(0);
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

        private void Copy(ZipArchive zip, string entryName, string relativeDestination)
        {
            ZipArchiveEntry entry = zip.GetEntry(entryName)
                ?? throw new InvalidDataException($"Archive member missing: {entryName}");
            string destination = Path.Combine(
                Stage,
                relativeDestination.Replace('/', Path.DirectorySeparatorChar));
            using Stream source = entry.Open();
            using FileStream target = File.Create(destination);
            source.CopyTo(target);
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
                packageId = "granite-edge-ai-atomicbot-llama-quantize-x64",
                source = new
                {
                    url = "https://github.com/AtomicBot-ai/atomic-llama-cpp-turboquant",
                    commit = "519f0c594a8e31467d2e2f2cf17054c9e7e11536",
                },
                target = "llama-quantize",
                architecture = "x64",
                configuration = "Release",
                libraryLinkage = "dynamic",
                cmakeFlags = Array.Empty<string>(),
                toolchain = new
                {
                    visualStudio = "not-attested-by-upstream-release",
                    msvc = "runtime-banner-msvc-19.51.36248.0_pe-linker-14.44",
                    cmake = "not-attested-by-upstream-release",
                },
                osProvidedDependencies = OsProvidedDependencies,
                appLocalDependencies = AppLocalDependencies,
                executableRelativePath = "bin/llama-quantize.exe",
                allowedTokens = new[] { "Q2_K", "Q3_K_M", "Q4_K_M", "Q5_K_M", "Q6_K", "Q8_0" },
                maximumSourceBytes = 68_719_476_736L,
                maximumOutputBytes = 68_719_476_736L,
                timeoutSeconds = 21_600,
                standardOutputMaximumBytes = 1_048_576,
                standardErrorMaximumBytes = 1_048_576,
                license = new
                {
                    identity = "MIT",
                    relativePath = "licenses/LICENSE.atomicbot-llama.cpp.txt",
                },
                files,
            };
            File.WriteAllText(
                Path.Combine(Stage, "llama-quantize.package.manifest.json"),
                JsonSerializer.Serialize(manifest));
        }

        private string Member(string relativePath) =>
            Path.Combine(
                Stage,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

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
