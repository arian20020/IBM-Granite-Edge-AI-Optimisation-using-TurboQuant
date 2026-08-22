using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerProcess.Tests;

/// <summary>
/// Pins the production worker publish closure and the detached integrity
/// manifest consumed by the packaged application.
/// </summary>
[TestClass]
public sealed partial class ProductionWorkerPackagingTests
{
    private const ushort Amd64PeMachine = 0x8664;
    private const string WorkerProjectRelativePath =
        "workers/GraniteEdgeAI.ModelInspection.Worker/" +
        "GraniteEdgeAI.ModelInspection.Worker.csproj";
    private const string ManifestGeneratorRelativePath =
        "scripts/model-inspection/New-ModelInspectionWorkerManifest.ps1";
    private const string ManifestVerifierRelativePath =
        "scripts/model-inspection/Test-ModelInspectionWorkerManifest.ps1";
    private const string OwnedDirectoryParentName =
        "GraniteEdgeAI-WorkerPackaging";

    private static readonly string[] ExpectedFiles =
    [
        "CommunityToolkit.HighPerformance.dll",
        "GraniteEdgeAI.ModelInspection.Contracts.dll",
        "GraniteEdgeAI.ModelInspection.LlamaSharp.dll",
        "GraniteEdgeAI.ModelInspection.Transport.dll",
        "GraniteEdgeAI.ModelInspection.Worker.deps.json",
        "GraniteEdgeAI.ModelInspection.Worker.dll",
        "GraniteEdgeAI.ModelInspection.Worker.exe",
        "GraniteEdgeAI.ModelInspection.Worker.runtimeconfig.json",
        "LLamaSharp.dll",
        "Microsoft.Bcl.AsyncInterfaces.dll",
        "Microsoft.Bcl.Memory.dll",
        "Microsoft.Extensions.AI.Abstractions.dll",
        "Microsoft.Extensions.DependencyInjection.Abstractions.dll",
        "Microsoft.Extensions.Logging.Abstractions.dll",
        "Microsoft.Windows.SDK.NET.dll",
        "System.Diagnostics.DiagnosticSource.dll",
        "System.IO.Pipelines.dll",
        "System.Interactive.Async.dll",
        "System.Linq.Async.dll",
        "System.Linq.AsyncEnumerable.dll",
        "System.Numerics.Tensors.dll",
        "System.Text.Encodings.Web.dll",
        "System.Text.Json.dll",
        "WinRT.Runtime.dll",
        "runtimes/win-x64/native/avx/ggml-base.dll",
        "runtimes/win-x64/native/avx/ggml-cpu.dll",
        "runtimes/win-x64/native/avx/ggml.dll",
        "runtimes/win-x64/native/avx/llama.dll",
        "runtimes/win-x64/native/avx/mtmd.dll",
        "runtimes/win-x64/native/avx2/ggml-base.dll",
        "runtimes/win-x64/native/avx2/ggml-cpu.dll",
        "runtimes/win-x64/native/avx2/ggml.dll",
        "runtimes/win-x64/native/avx2/llama.dll",
        "runtimes/win-x64/native/avx2/mtmd.dll",
        "runtimes/win-x64/native/avx512/ggml-base.dll",
        "runtimes/win-x64/native/avx512/ggml-cpu.dll",
        "runtimes/win-x64/native/avx512/ggml.dll",
        "runtimes/win-x64/native/avx512/llama.dll",
        "runtimes/win-x64/native/avx512/mtmd.dll",
        "runtimes/win-x64/native/noavx/ggml-base.dll",
        "runtimes/win-x64/native/noavx/ggml-cpu.dll",
        "runtimes/win-x64/native/noavx/ggml.dll",
        "runtimes/win-x64/native/noavx/llama.dll",
        "runtimes/win-x64/native/noavx/mtmd.dll"
    ];

    private static readonly string[] ManifestRootProperties =
        ["schemaVersion", "runtimeIdentifier", "files"];
    private static readonly string[] ManifestFileProperties =
        ["path", "length", "sha256"];

    [TestMethod]
    public async Task PublishedWorkerHasExactVerifiedDeterministicWinX64Closure()
    {
        string repositoryRoot = FindRepositoryRoot();
        string ownedDirectory = CreateOwnedDirectory();
        string workerDirectory = Path.Combine(
            ownedDirectory,
            "ModelInspection",
            "Worker");
        string manifestPath = Path.Combine(
            ownedDirectory,
            "ModelInspection",
            "worker-manifest.json");
        string repeatedManifestPath = Path.Combine(
            ownedDirectory,
            "worker-manifest-repeat.json");

        try
        {
            Directory.CreateDirectory(workerDirectory);
            ProcessResult publish = await RunProcessAsync(
                    "dotnet",
                    repositoryRoot,
                    TimeSpan.FromMinutes(2),
                    "publish",
                    Path.Combine(
                        repositoryRoot,
                        WorkerProjectRelativePath.Replace(
                            '/',
                            Path.DirectorySeparatorChar)),
                    "--configuration",
                    "Release",
                    "--runtime",
                    "win-x64",
                    "--self-contained",
                    "false",
                    "--output",
                    workerDirectory,
                    "-p:Platform=x64",
                    "-p:UseAppHost=true")
                .ConfigureAwait(false);
            AssertProcessSucceeded(publish, "Production worker publish");

            string[] actualFiles = EnumerateRelativeFiles(workerDirectory);
            CollectionAssert.AreEqual(ExpectedFiles, actualFiles);
            Assert.AreEqual(24, actualFiles.Count(IsRootFile));
            Assert.AreEqual(20, actualFiles.Count(IsCpuNativeFile));
            Assert.IsFalse(actualFiles.Any(IsForbiddenPayload));
            Assert.IsFalse(
                actualFiles.Any(
                    path => path.EndsWith(
                        ".pdb",
                        StringComparison.OrdinalIgnoreCase)));

            Assert.AreEqual(
                Amd64PeMachine,
                ReadPeMachine(Path.Combine(
                    workerDirectory,
                    "GraniteEdgeAI.ModelInspection.Worker.exe")));
            foreach (string nativePath in actualFiles.Where(IsCpuNativeFile))
            {
                Assert.AreEqual(
                    Amd64PeMachine,
                    ReadPeMachine(ToNativePath(workerDirectory, nativePath)),
                    $"Native worker dependency is not AMD64: {nativePath}");
            }

            string generatorPath = Path.Combine(
                repositoryRoot,
                ManifestGeneratorRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            string verifierPath = Path.Combine(
                repositoryRoot,
                ManifestVerifierRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            AssertProcessSucceeded(
                await RunPowerShellScriptAsync(
                        repositoryRoot,
                        generatorPath,
                        workerDirectory,
                        manifestPath)
                    .ConfigureAwait(false),
                "Worker manifest generation");
            Assert.IsTrue(File.Exists(manifestPath));
            Assert.IsFalse(
                File.Exists(Path.Combine(
                    workerDirectory,
                    Path.GetFileName(manifestPath))),
                "The detached manifest must remain outside the Worker directory.");

            await AssertManifestMatchesPublishAsync(
                    workerDirectory,
                    manifestPath)
                .ConfigureAwait(false);
            AssertProcessSucceeded(
                await RunPowerShellScriptAsync(
                        repositoryRoot,
                        generatorPath,
                        workerDirectory,
                        repeatedManifestPath)
                    .ConfigureAwait(false),
                "Repeated worker manifest generation");
            CollectionAssert.AreEqual(
                await File.ReadAllBytesAsync(manifestPath).ConfigureAwait(false),
                await File.ReadAllBytesAsync(repeatedManifestPath)
                    .ConfigureAwait(false),
                "Manifest bytes must be deterministic for an unchanged closure.");

            await AssertVerificationSucceedsAsync(
                    repositoryRoot,
                    verifierPath,
                    workerDirectory,
                    manifestPath)
                .ConfigureAwait(false);
            await AssertFixedLayoutCompletesInspectionAsync(ownedDirectory)
                .ConfigureAwait(false);
            await AssertVerifierRejectsClosureMutationsAsync(
                    repositoryRoot,
                    verifierPath,
                    workerDirectory,
                    manifestPath,
                    ownedDirectory)
                .ConfigureAwait(false);
        }
        finally
        {
            DeleteOwnedDirectory(ownedDirectory);
        }
    }

    private static async Task AssertFixedLayoutCompletesInspectionAsync(
        string applicationRoot)
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "GGUF",
            "N-001-vocab-only-spm.gguf");
        Assert.IsTrue(File.Exists(fixturePath));
        FileInfo fixture = new(fixturePath);
        fixture.Refresh();

        WorkerClientOptions options = new(
            applicationRoot,
            StartupTimeout: TimeSpan.FromSeconds(3),
            OverallTimeout: TimeSpan.FromSeconds(10),
            CancellationGracePeriod: TimeSpan.FromSeconds(1),
            MaximumRetainedStandardErrorBytes: 4 * 1024,
            ProcessTreeCleanupTimeout: TimeSpan.FromSeconds(3));
        InspectionWorkerClient client = new(options);
        WorkerStartInspectionCommand command = WorkerProcessTestData
            .StartCommand() with
        {
            ModelPath = fixturePath,
            ExpectedFileIdentity = new WorkerExpectedFileIdentity
            {
                LengthBytes = fixture.Length,
                LastWriteTimeUtc = new DateTimeOffset(
                    fixture.LastWriteTimeUtc,
                    TimeSpan.Zero)
            },
            QuickScan = new WorkerQuickScanSnapshot
            {
                Format = "GGUF",
                ModelName = "Controlled packaged VocabOnly fixture",
                Architecture = "granite",
                FileSizeBytes = fixture.Length,
                GgufVersion = 3
            }
        };
        List<WorkerProgressMessage> progress = [];

        WorkerClientResult result = await client.ExecuteAsync(
                command,
                new DelegatingProgress<WorkerProgressMessage>(progress.Add),
                CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(30))
            .ConfigureAwait(false);

        result.Validate();
        Assert.IsNull(result.Failure);
        Assert.IsNotNull(result.TerminalMessage);
        Assert.AreEqual(
            WorkerCompletionStatus.Completed,
            result.TerminalMessage.CompletionStatus);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, result.RetainedStandardError);
        CollectionAssert.AreEquivalent(
            Enum.GetValues<WorkerStage>(),
            progress.Select(message => message.Stage).Distinct().ToArray());
        await WorkerProcessTestData
            .AssertNoProductionWorkerProcessRemainsAsync()
            .ConfigureAwait(false);
    }

    private static async Task AssertManifestMatchesPublishAsync(
        string workerDirectory,
        string manifestPath)
    {
        await using FileStream stream = File.OpenRead(manifestPath);
        using JsonDocument document = await JsonDocument
            .ParseAsync(stream)
            .ConfigureAwait(false);
        JsonElement root = document.RootElement;
        CollectionAssert.AreEquivalent(
            ManifestRootProperties,
            root.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual(
            "win-x64",
            root.GetProperty("runtimeIdentifier").GetString());

        JsonElement[] entries = root
            .GetProperty("files")
            .EnumerateArray()
            .ToArray();
        Assert.AreEqual(ExpectedFiles.Length, entries.Length);
        for (int index = 0; index < entries.Length; index++)
        {
            JsonElement entry = entries[index];
            CollectionAssert.AreEquivalent(
                ManifestFileProperties,
                entry.EnumerateObject()
                    .Select(property => property.Name)
                    .ToArray());
            string? relativePath = entry.GetProperty("path").GetString();
            Assert.AreEqual(ExpectedFiles[index], relativePath);
            Assert.IsNotNull(relativePath);
            string filePath = ToNativePath(workerDirectory, relativePath);
            Assert.AreEqual(
                new FileInfo(filePath).Length,
                entry.GetProperty("length").GetInt64());
            string? manifestHash = entry.GetProperty("sha256").GetString();
            Assert.IsNotNull(manifestHash);
            Assert.IsTrue(LowerSha256Regex().IsMatch(manifestHash));
            Assert.AreEqual(
                await ComputeSha256Async(filePath).ConfigureAwait(false),
                manifestHash);
        }
    }

    private static async Task AssertVerifierRejectsClosureMutationsAsync(
        string repositoryRoot,
        string verifierPath,
        string workerDirectory,
        string manifestPath,
        string ownedDirectory)
    {
        string selectedFile = Path.Combine(
            workerDirectory,
            "GraniteEdgeAI.ModelInspection.Contracts.dll");
        string heldFile = Path.Combine(ownedDirectory, "held-contracts.dll");
        File.Move(selectedFile, heldFile);
        try
        {
            await AssertVerificationFailsAsync(
                    repositoryRoot,
                    verifierPath,
                    workerDirectory,
                    manifestPath,
                    "a missing file")
                .ConfigureAwait(false);
        }
        finally
        {
            File.Move(heldFile, selectedFile);
        }

        string extraFile = Path.Combine(workerDirectory, "unexpected.bin");
        await File.WriteAllBytesAsync(extraFile, [0x01]).ConfigureAwait(false);
        try
        {
            await AssertVerificationFailsAsync(
                    repositoryRoot,
                    verifierPath,
                    workerDirectory,
                    manifestPath,
                    "an extra file")
                .ConfigureAwait(false);
        }
        finally
        {
            File.Delete(extraFile);
        }

        byte[] originalBytes = await File
            .ReadAllBytesAsync(selectedFile)
            .ConfigureAwait(false);
        byte[] hashMutation = (byte[])originalBytes.Clone();
        hashMutation[hashMutation.Length / 2] ^= 0xff;
        await File.WriteAllBytesAsync(selectedFile, hashMutation)
            .ConfigureAwait(false);
        try
        {
            await AssertVerificationFailsAsync(
                    repositoryRoot,
                    verifierPath,
                    workerDirectory,
                    manifestPath,
                    "a same-length hash mutation")
                .ConfigureAwait(false);
        }
        finally
        {
            await File.WriteAllBytesAsync(selectedFile, originalBytes)
                .ConfigureAwait(false);
        }

        await using (FileStream append = new(
            selectedFile,
            FileMode.Append,
            FileAccess.Write,
            FileShare.None))
        {
            await append.WriteAsync(new byte[] { 0x02 }).ConfigureAwait(false);
        }

        try
        {
            await AssertVerificationFailsAsync(
                    repositoryRoot,
                    verifierPath,
                    workerDirectory,
                    manifestPath,
                    "a length mutation")
                .ConfigureAwait(false);
        }
        finally
        {
            await File.WriteAllBytesAsync(selectedFile, originalBytes)
                .ConfigureAwait(false);
        }

        string unsafeManifest = Path.Combine(
            ownedDirectory,
            "unsafe-path-manifest.json");
        JsonObject unsafeRoot = JsonNode
            .Parse(await File.ReadAllTextAsync(manifestPath)
                .ConfigureAwait(false))!
            .AsObject();
        JsonArray unsafeFiles = unsafeRoot["files"]!.AsArray();
        unsafeFiles[0]!["path"] = "../escape.dll";
        await File.WriteAllTextAsync(
                unsafeManifest,
                unsafeRoot.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true
                }))
            .ConfigureAwait(false);
        await AssertVerificationFailsAsync(
                repositoryRoot,
                verifierPath,
                workerDirectory,
                unsafeManifest,
                "a traversal path")
            .ConfigureAwait(false);
    }

    private static async Task AssertVerificationSucceedsAsync(
        string repositoryRoot,
        string verifierPath,
        string workerDirectory,
        string manifestPath)
    {
        AssertProcessSucceeded(
            await RunPowerShellScriptAsync(
                    repositoryRoot,
                    verifierPath,
                    workerDirectory,
                    manifestPath)
                .ConfigureAwait(false),
            "Worker manifest verification");
    }

    private static async Task AssertVerificationFailsAsync(
        string repositoryRoot,
        string verifierPath,
        string workerDirectory,
        string manifestPath,
        string mutation)
    {
        ProcessResult result = await RunPowerShellScriptAsync(
                repositoryRoot,
                verifierPath,
                workerDirectory,
                manifestPath)
            .ConfigureAwait(false);
        Assert.AreNotEqual(
            0,
            result.ExitCode,
            $"Verification accepted {mutation}. Output: {result.Output} " +
            $"Error: {result.Error}");
    }

    private static Task<ProcessResult> RunPowerShellScriptAsync(
        string repositoryRoot,
        string scriptPath,
        string workerDirectory,
        string manifestPath) =>
        RunProcessAsync(
            "powershell.exe",
            repositoryRoot,
            TimeSpan.FromSeconds(30),
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            scriptPath,
            "-WorkerDirectory",
            workerDirectory,
            "-ManifestPath",
            manifestPath);

    private static async Task<ProcessResult> RunProcessAsync(
        string executable,
        string workingDirectory,
        TimeSpan timeout,
        params string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException(
                $"Process could not start: {executable}");
        }

        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeoutSource = new(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"Process exceeded {timeout}: {executable}");
        }

        return new ProcessResult(
            process.ExitCode,
            await output.ConfigureAwait(false),
            await error.ConfigureAwait(false));
    }

    private static void AssertProcessSucceeded(
        ProcessResult result,
        string operation)
    {
        Assert.AreEqual(
            0,
            result.ExitCode,
            $"{operation} failed. Output: {result.Output} Error: {result.Error}");
    }

    private static string[] EnumerateRelativeFiles(string workerDirectory)
    {
        string rootWithSeparator = Path.GetFullPath(workerDirectory) +
            Path.DirectorySeparatorChar;
        return Directory
            .EnumerateFiles(
                workerDirectory,
                "*",
                SearchOption.AllDirectories)
            .Select(path => Path.GetFullPath(path)
                .Substring(rootWithSeparator.Length)
                .Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsRootFile(string relativePath) =>
        !relativePath.Contains('/', StringComparison.Ordinal);

    private static bool IsCpuNativeFile(string relativePath) =>
        relativePath.StartsWith(
            "runtimes/win-x64/native/",
            StringComparison.Ordinal);

    private static bool IsForbiddenPayload(string relativePath) =>
        relativePath.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) ||
        relativePath.Contains("test", StringComparison.OrdinalIgnoreCase) ||
        relativePath.Contains("evidence", StringComparison.OrdinalIgnoreCase) ||
        relativePath.Contains("cuda", StringComparison.OrdinalIgnoreCase) ||
        relativePath.Contains("vulkan", StringComparison.OrdinalIgnoreCase) ||
        relativePath.Contains("openvino", StringComparison.OrdinalIgnoreCase) ||
        relativePath.Contains("turboquant", StringComparison.OrdinalIgnoreCase);

    private static ushort ReadPeMachine(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using BinaryReader reader = new(stream);
        Assert.AreEqual((ushort)0x5a4d, reader.ReadUInt16());
        stream.Position = 0x3c;
        int peHeaderOffset = reader.ReadInt32();
        Assert.IsTrue(peHeaderOffset >= 0x40);
        stream.Position = peHeaderOffset;
        Assert.AreEqual(0x00004550u, reader.ReadUInt32());
        return reader.ReadUInt16();
    }

    private static string ToNativePath(
        string workerDirectory,
        string relativePath) =>
        Path.Combine(
            workerDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string CreateOwnedDirectory()
    {
        string ownedParent = Path.Combine(
            Path.GetTempPath(),
            OwnedDirectoryParentName);
        string directory = Path.Combine(
            ownedParent,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteOwnedDirectory(string directory)
    {
        string fullDirectory = Path.GetFullPath(directory);
        string ownedParent = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            OwnedDirectoryParentName));
        DirectoryInfo? parent = Directory.GetParent(fullDirectory);
        bool isUniqueChild = parent is not null &&
            string.Equals(
                parent.FullName,
                ownedParent,
                StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParseExact(Path.GetFileName(fullDirectory), "N", out _);
        if (!isUniqueChild)
        {
            throw new InvalidOperationException(
                "Refusing to delete a worker-packaging directory that is not owned by this test.");
        }

        if (Directory.Exists(fullDirectory))
        {
            Directory.Delete(fullDirectory, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "global.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "The repository root containing global.json could not be found.");
    }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex LowerSha256Regex();

    private sealed record ProcessResult(
        int ExitCode,
        string Output,
        string Error);
}
