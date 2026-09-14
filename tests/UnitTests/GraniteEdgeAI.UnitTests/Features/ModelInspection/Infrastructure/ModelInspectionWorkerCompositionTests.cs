using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Infrastructure;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Protects the application-owned selection of the approved worker root.
/// Package identity is authoritative; controlled unpackaged runs may use only
/// the application base directory.
/// </summary>
[TestClass]
public sealed class ModelInspectionWorkerCompositionTests
{
    [TestMethod]
    public void ResolveApprovedApplicationRoot_PackageIdentityWins()
    {
        using TemporaryDirectory installedRoot = new();
        using TemporaryDirectory applicationBase = new();

        string actual = ModelInspectionWorkerComposition
            .ResolveApprovedApplicationRoot(
                packageIdentityAvailable: true,
                installedRoot.Path,
                applicationBase.Path);

        Assert.AreEqual(
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(installedRoot.Path)),
            actual);
    }

    [TestMethod]
    public void ResolveApprovedApplicationRoot_UnpackagedUsesApplicationBase()
    {
        using TemporaryDirectory applicationBase = new();

        string actual = ModelInspectionWorkerComposition
            .ResolveApprovedApplicationRoot(
                packageIdentityAvailable: false,
                installedPackageRoot: null,
                applicationBase.Path);

        Assert.AreEqual(
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(applicationBase.Path)),
            actual);
    }

    [TestMethod]
    public void ResolveApprovedApplicationRoot_ActualProbeFindsPackagedWorker()
    {
        string actual = ModelInspectionWorkerComposition
            .ResolveApprovedApplicationRoot();

        Assert.IsTrue(Directory.Exists(actual));
        Assert.IsTrue(File.Exists(Path.Combine(
            actual,
            "ModelInspection",
            "Worker",
            "GraniteEdgeAI.ModelInspection.Worker.exe")));
    }

    [TestMethod]
    public async Task CreateDefaultClient_ActualPackagedRootCompletesControlledInspection()
    {
        string approvedRoot = ModelInspectionWorkerComposition
            .ResolveApprovedApplicationRoot();
        string fixturePath = Path.Combine(
            approvedRoot,
            "TestFixtures",
            "GGUF",
            "N-001-vocab-only-spm.gguf");
        Assert.IsTrue(File.Exists(fixturePath));
        FileInfo fixture = new(fixturePath);
        fixture.Refresh();
        using Process currentProcess = Process.GetCurrentProcess();
        WorkerStartInspectionCommand command = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            CommandType = WorkerCommandKind.StartInspection,
            RequestId = Guid.NewGuid(),
            ParentProcessId = currentProcess.Id,
            ParentProcessStartTimeUtc = new DateTimeOffset(
                currentProcess.StartTime.ToUniversalTime(),
                TimeSpan.Zero),
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
        IInspectionWorkerClient client = ModelInspectionWorkerComposition
            .CreateDefaultClient();

        WorkerClientResult result = await client.ExecuteAsync(
                command,
                new DelegatingProgress<WorkerProgressMessage>(progress.Add),
                CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(30));

        result.Validate();
        Assert.IsNull(result.Failure,
            $"ExitCode={result.ExitCode} (0x{result.ExitCode.GetValueOrDefault():X8}); " +
            $"ForcedTermination={result.ForcedTermination}; " +
            $"StandardErrorTruncated={result.StandardErrorTruncated}; " +
            $"StandardError={result.RetainedStandardError}; " +
            $"SecondaryDiagnostics={string.Join(", ", result.SecondaryDiagnostics)}; " +
            DescribeWorkerRuntimeContext(approvedRoot));
        Assert.IsNotNull(result.TerminalMessage);
        Assert.AreEqual(
            WorkerCompletionStatus.Completed,
            result.TerminalMessage.CompletionStatus);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, result.RetainedStandardError);
        Assert.IsTrue(progress.Count >= 10);
        WorkerProgressMessage[] core = progress
            .Where(message => message.StageFraction is null)
            .ToArray();
        Assert.HasCount(10, core);
        CollectionAssert.AreEqual(
            Enum.GetValues<WorkerStage>()
                .SelectMany(stage => new[] { stage, stage })
                .ToArray(),
            core.Select(message => message.Stage).ToArray());
        CollectionAssert.AreEqual(
            Enumerable.Range(0, 5)
                .SelectMany(completed => new[] { completed, completed + 1 })
                .ToArray(),
            core.Select(message => message.CompletedStageCount).ToArray());
        for (int index = 0; index < core.Length; index++)
        {
            Assert.AreEqual(
                index % 2 == 0
                    ? WorkerStageStatus.Active
                    : WorkerStageStatus.Completed,
                core[index].StageStatus);
        }
    }

    [TestMethod]
    public async Task CreateDefaultService_ActualPackagedRootReturnsApplicationReadyResult()
    {
        string approvedRoot = ModelInspectionWorkerComposition
            .ResolveApprovedApplicationRoot();
        string fixturePath = Path.Combine(
            approvedRoot,
            "TestFixtures",
            "GGUF",
            "N-001-vocab-only-spm.gguf");
        Assert.IsTrue(File.Exists(fixturePath));
        FileInfo fixture = new(fixturePath);
        fixture.Refresh();
        ModelInspectionRequest request = new(
            fixturePath,
            fixture.Name,
            new ExpectedModelFileIdentity(
                fixture.Length,
                new DateTimeOffset(fixture.LastWriteTimeUtc, TimeSpan.Zero)),
            ValidatedQuickScanSnapshot.CreateGguf(
                modelName: "Controlled packaged VocabOnly fixture",
                architecture: "granite",
                parameterSizeLabel: null,
                quantisation: null,
                fileSizeBytes: fixture.Length,
                declaredContextLength: null,
                ggufVersion: 3));
        List<ModelInspectionProgress> progress = [];
        IModelInspectionService service = ModelInspectionWorkerComposition
            .CreateDefaultService();

        ModelInspectionExecutionResult result = await service.InspectAsync(
                request,
                new DelegatingProgress<ModelInspectionProgress>(progress.Add),
                CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(30));

        Assert.AreEqual(ModelInspectionExecutionStatus.Completed, result.Status,
            $"Failure={result.Failure}; {DescribeWorkerRuntimeContext(approvedRoot)}");
        Assert.IsNotNull(result.Result);
        Assert.AreEqual(ModelInspectionOutcome.Ready, result.Result.Outcome);
        Assert.IsTrue(result.Result.CanContinueToHardwareFit);
        Assert.IsNull(result.Failure);
        Assert.IsTrue(progress.Count >= 10);
        CollectionAssert.AreEqual(
            Enum.GetValues<ModelInspectionStage>(),
            progress
                .Where(update =>
                    update.StageStatus == ModelInspectionStageStatus.Completed)
                .Select(update => update.Stage)
                .ToArray());
        Assert.AreEqual(5, progress[^1].CompletedStageCount);
        Assert.AreEqual(
            ModelInspectionStageStatus.Completed,
            progress[^1].StageStatus);
    }

    private static string DescribeWorkerRuntimeContext(string approvedRoot)
    {
        // The packaged host can inherit a different environment from the CI shell.
        // Report only runtime discovery inputs, never the complete environment.
        try
        {
            string standardRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
            string sharedRoot = Path.Combine(standardRoot, "shared", "Microsoft.NETCore.App");
            string versions = Directory.Exists(sharedRoot)
                ? string.Join(", ", Directory.GetDirectories(sharedRoot)
                    .Select(Path.GetFileName).OrderBy(name => name).Take(20))
                : "<missing>";
            string runtimeConfig = Path.Combine(approvedRoot, "ModelInspection", "Worker",
                "GraniteEdgeAI.ModelInspection.Worker.runtimeconfig.json");
            string configuration = File.Exists(runtimeConfig)
                ? File.ReadAllText(runtimeConfig)
                : "<missing>";
            return $"OS={Environment.OSVersion}; Architecture={System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}; " +
                $"HostRuntime={System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()}; " +
                $"DOTNET_ROOT={Environment.GetEnvironmentVariable("DOTNET_ROOT")}; " +
                $"DOTNET_ROOT_X64={Environment.GetEnvironmentVariable("DOTNET_ROOT_X64")}; " +
                $"StandardDotnetRoot={standardRoot}; InstalledCoreRuntimes={versions}; " +
                $"WorkerRoot={approvedRoot}; RuntimeConfig={configuration[..Math.Min(configuration.Length, 4096)]}";
        }
        catch (Exception exception)
        {
            return $"Runtime context could not be read: {exception.GetType().Name}: {exception.Message}";
        }
    }

    [TestMethod]
    public async Task IntegrityWrapperRejectsTamperingBeforeWorkerInvocation()
    {
        using SyntheticWorkerPackage dependencyTamper =
            SyntheticWorkerPackage.Create();
        RejectingWorkerClient dependencyInner = new();
        dependencyTamper.MutateDependencyWithoutChangingLength();
        IInspectionWorkerClient dependencyClient =
            new ManifestVerifyingInspectionWorkerClient(
                dependencyTamper.ApplicationRoot,
                dependencyTamper.TrustedManifest,
                dependencyInner);

        WorkerClientResult dependencyResult = await dependencyClient
            .ExecuteAsync(
                CreateValidCommand(dependencyTamper.ApplicationRoot),
                progress: null,
                CancellationToken.None);

        AssertIntegrityFailure(dependencyResult);
        Assert.AreEqual(0, dependencyInner.InvocationCount);

        using SyntheticWorkerPackage manifestTamper =
            SyntheticWorkerPackage.Create();
        RejectingWorkerClient manifestInner = new();
        manifestTamper.MutateDetachedManifest();
        IInspectionWorkerClient manifestClient =
            new ManifestVerifyingInspectionWorkerClient(
                manifestTamper.ApplicationRoot,
                manifestTamper.TrustedManifest,
                manifestInner);

        WorkerClientResult manifestResult = await manifestClient.ExecuteAsync(
            CreateValidCommand(manifestTamper.ApplicationRoot),
            progress: null,
            CancellationToken.None);

        AssertIntegrityFailure(manifestResult);
        Assert.AreEqual(0, manifestInner.InvocationCount);

        using SyntheticWorkerPackage malformedManifest =
            SyntheticWorkerPackage.Create();
        RejectingWorkerClient malformedInner = new();
        byte[] malformedTrustedManifest =
            """{"schemaVersion":"1","runtimeIdentifier":3,"files":[]}"""u8
                .ToArray();
        malformedManifest.ReplaceDetachedManifest(malformedTrustedManifest);
        IInspectionWorkerClient malformedClient =
            new ManifestVerifyingInspectionWorkerClient(
                malformedManifest.ApplicationRoot,
                malformedTrustedManifest,
                malformedInner);

        WorkerClientResult malformedResult = await malformedClient.ExecuteAsync(
            CreateValidCommand(malformedManifest.ApplicationRoot),
            progress: null,
            CancellationToken.None);

        AssertIntegrityFailure(malformedResult);
        Assert.AreEqual(0, malformedInner.InvocationCount);
    }

    [TestMethod]
    public void ResolveApprovedApplicationRoot_InvalidInstalledRootDoesNotFallback()
    {
        using TemporaryDirectory applicationBase = new();
        string missingInstalledRoot = Path.Combine(
            applicationBase.Path,
            "missing-package-root");

        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
            () => ModelInspectionWorkerComposition.ResolveApprovedApplicationRoot(
                packageIdentityAvailable: true,
                missingInstalledRoot,
                applicationBase.Path));

        Assert.AreEqual(
            "The installed Model Inspection worker root is unavailable.",
            error.Message);
    }

    [TestMethod]
    public void ResolveApprovedApplicationRoot_InvalidUnpackagedBaseFailsClosed()
    {
        using TemporaryDirectory parent = new();
        string missingApplicationBase = Path.Combine(
            parent.Path,
            "missing-application-base");

        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
            () => ModelInspectionWorkerComposition.ResolveApprovedApplicationRoot(
                packageIdentityAvailable: false,
                installedPackageRoot: null,
                missingApplicationBase));

        Assert.AreEqual(
            "The unpackaged Model Inspection worker root is unavailable.",
            error.Message);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(122)]
    public void InterpretPackageIdentityProbeResult_PackagedResultsAreTrue(
        int result)
    {
        Assert.IsTrue(ModelInspectionWorkerComposition
            .InterpretPackageIdentityProbeResult(result));
    }

    [TestMethod]
    public void InterpretPackageIdentityProbeResult_NoPackageIsFalse()
    {
        Assert.IsFalse(ModelInspectionWorkerComposition
            .InterpretPackageIdentityProbeResult(15_700));
    }

    [TestMethod]
    public void InterpretPackageIdentityProbeResult_UnexpectedResultFailsClosed()
    {
        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
            () => ModelInspectionWorkerComposition
                .InterpretPackageIdentityProbeResult(5));

        Assert.AreEqual(
            "The Model Inspection package identity could not be determined.",
            error.Message);
    }

    [TestMethod]
    public async Task PackagedTestOutputContainsExactDetachedWorkerManifest()
    {
        string applicationRoot = Path.GetFullPath(AppContext.BaseDirectory);
        string workerRoot = Path.Combine(
            applicationRoot,
            "ModelInspection",
            "Worker");
        string manifestPath = Path.Combine(
            applicationRoot,
            "ModelInspection",
            "worker-manifest.json");

        Assert.IsTrue(Directory.Exists(workerRoot));
        Assert.IsTrue(File.Exists(Path.Combine(
            workerRoot,
            "GraniteEdgeAI.ModelInspection.Worker.exe")));
        Assert.IsTrue(File.Exists(manifestPath));
        Assert.IsFalse(File.Exists(Path.Combine(
            applicationRoot,
            "LLamaSharp.dll")));
        Assert.HasCount(
            0,
            Directory.GetFiles(
                workerRoot,
                "*.gguf",
                SearchOption.AllDirectories));

        await using FileStream manifestStream = File.OpenRead(manifestPath);
        using JsonDocument manifest = await JsonDocument
            .ParseAsync(manifestStream)
            .ConfigureAwait(false);
        Assert.AreEqual(
            1,
            manifest.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual(
            "win-x64",
            manifest.RootElement.GetProperty("runtimeIdentifier").GetString());
        JsonElement[] entries = manifest.RootElement
            .GetProperty("files")
            .EnumerateArray()
            .ToArray();
        Assert.HasCount(44, entries);

        string workerPrefix = Path.TrimEndingDirectorySeparator(workerRoot) +
            Path.DirectorySeparatorChar;
        string[] actualRelativePaths = Directory
            .GetFiles(workerRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetFullPath(path)[workerPrefix.Length..]
                .Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(
            entries.Select(entry => entry.GetProperty("path").GetString()!)
                .ToArray(),
            actualRelativePaths);

        foreach (JsonElement entry in entries)
        {
            string relativePath = entry.GetProperty("path").GetString()!;
            string filePath = Path.Combine(
                workerRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.AreEqual(
                new FileInfo(filePath).Length,
                entry.GetProperty("length").GetInt64());
            await using FileStream file = File.OpenRead(filePath);
            byte[] hash = await SHA256.HashDataAsync(file).ConfigureAwait(false);
            Assert.AreEqual(
                Convert.ToHexString(hash).ToLowerInvariant(),
                entry.GetProperty("sha256").GetString());
        }
    }

    private static WorkerStartInspectionCommand CreateValidCommand(
        string root)
    {
        using Process currentProcess = Process.GetCurrentProcess();
        return new WorkerStartInspectionCommand
        {
            ProtocolVersion = WorkerProtocol.Version,
            CommandType = WorkerCommandKind.StartInspection,
            RequestId = Guid.NewGuid(),
            ParentProcessId = currentProcess.Id,
            ParentProcessStartTimeUtc = new DateTimeOffset(
                currentProcess.StartTime.ToUniversalTime(),
                TimeSpan.Zero),
            ModelPath = Path.Combine(root, "controlled.gguf"),
            ExpectedFileIdentity = new WorkerExpectedFileIdentity
            {
                LengthBytes = 1,
                LastWriteTimeUtc = DateTimeOffset.UnixEpoch
            },
            QuickScan = new WorkerQuickScanSnapshot
            {
                Format = "GGUF",
                ModelName = "Controlled integrity fixture",
                Architecture = "granite",
                FileSizeBytes = 1,
                GgufVersion = 3
            }
        };
    }

    private static void AssertIntegrityFailure(WorkerClientResult result)
    {
        result.Validate();
        Assert.IsNull(result.TerminalMessage);
        Assert.IsNotNull(result.Failure);
        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerPackageIntegrityFailed,
            result.Failure.Code);
        Assert.AreEqual(
            "The installed Model Inspection worker package failed its integrity check.",
            result.Failure.Message);
        Assert.IsNull(result.ExitCode);
        Assert.IsFalse(result.ForcedTermination);
        Assert.AreEqual(string.Empty, result.RetainedStandardError);
        Assert.HasCount(0, result.SecondaryDiagnostics);
    }

    private sealed class SyntheticWorkerPackage : IDisposable
    {
        private const int FileCount = 44;
        private readonly string _dependencyPath;
        private readonly string _manifestPath;

        private SyntheticWorkerPackage(
            string applicationRoot,
            string dependencyPath,
            string manifestPath,
            byte[] trustedManifest)
        {
            ApplicationRoot = applicationRoot;
            _dependencyPath = dependencyPath;
            _manifestPath = manifestPath;
            TrustedManifest = trustedManifest;
        }

        internal string ApplicationRoot { get; }

        internal byte[] TrustedManifest { get; }

        internal static SyntheticWorkerPackage Create()
        {
            string applicationRoot = Path.Combine(
                Path.GetTempPath(),
                "GraniteEdgeAI.ModelInspection.Integrity.Tests",
                Guid.NewGuid().ToString("N"));
            string workerRoot = Path.Combine(
                applicationRoot,
                "ModelInspection",
                "Worker");
            Directory.CreateDirectory(workerRoot);

            List<(string Path, long Length, string Sha256)> entries = [];
            string dependencyPath = string.Empty;
            for (int index = 0; index < FileCount; index++)
            {
                string relativePath = index == 0
                    ? "GraniteEdgeAI.ModelInspection.Worker.exe"
                    : $"dependency-{index:D2}.bin";
                string fullPath = Path.Combine(workerRoot, relativePath);
                byte[] content = [(byte)(index + 1), (byte)(index + 2)];
                File.WriteAllBytes(fullPath, content);
                if (index == 1)
                {
                    dependencyPath = fullPath;
                }

                entries.Add((
                    relativePath.Replace(
                        Path.DirectorySeparatorChar,
                        '/'),
                    content.LongLength,
                    Convert.ToHexString(SHA256.HashData(content))
                        .ToLowerInvariant()));
            }

            entries.Sort(static (left, right) =>
                StringComparer.Ordinal.Compare(left.Path, right.Path));
            byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                runtimeIdentifier = "win-x64",
                files = entries.Select(static entry => new
                {
                    path = entry.Path,
                    length = entry.Length,
                    sha256 = entry.Sha256
                })
            });
            string manifestPath = Path.Combine(
                applicationRoot,
                "ModelInspection",
                "worker-manifest.json");
            File.WriteAllBytes(manifestPath, manifest);
            return new SyntheticWorkerPackage(
                applicationRoot,
                dependencyPath,
                manifestPath,
                manifest);
        }

        internal void MutateDependencyWithoutChangingLength()
        {
            byte[] content = File.ReadAllBytes(_dependencyPath);
            content[0] ^= 0xFF;
            File.WriteAllBytes(_dependencyPath, content);
        }

        internal void MutateDetachedManifest()
        {
            using FileStream stream = new(
                _manifestPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.None);
            stream.WriteByte((byte)' ');
        }

        internal void ReplaceDetachedManifest(byte[] content)
        {
            File.WriteAllBytes(_manifestPath, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(ApplicationRoot))
            {
                Directory.Delete(ApplicationRoot, recursive: true);
            }
        }
    }

    private sealed class RejectingWorkerClient : IInspectionWorkerClient
    {
        internal int InvocationCount { get; private set; }

        public Task<WorkerClientResult> ExecuteAsync(
            WorkerStartInspectionCommand command,
            IProgress<WorkerProgressMessage>? progress,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            throw new InvalidOperationException(
                "The worker client must not be invoked after integrity failure.");
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GraniteEdgeAI.ModelInspection.Composition.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class DelegatingProgress<T>(Action<T> report)
        : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
