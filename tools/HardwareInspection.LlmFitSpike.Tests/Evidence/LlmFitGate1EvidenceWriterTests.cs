using System.Reflection;
using System.Text.Json;
using HardwareInspection.LlmFitSpike.Evidence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HardwareInspection.LlmFitSpike.Tests.Evidence;

[TestClass]
[DoNotParallelize]
[TestCategory("Deterministic")]
#pragma warning disable CA1707 // Test names intentionally encode the required behavior.
public sealed class LlmFitGate1EvidenceWriterTests
{
    private const string ArchiveHash =
        "a030269d7cc8a5bf40383f526a481655d698ec71dd792a25b06510cef9f8b738";
    private const string ExecutableHash =
        "db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19";
    private const string HashC = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string ReleaseCommit = "a02e13f1013ed69889ff44426a651bf7c68c292e";
    private static readonly string[] ExpectedSystemArguments = ["--no-dashboard", "--json", "system"];
    private static readonly string[] ExpectedVersionArguments = ["--version"];

    [TestMethod]
    public async Task WriteAsync_SanitizedEvidence_UsesAtomicReplacement()
    {
        using var directory = new OwnedTemporaryDirectory();
        string outputPath = Path.Combine(directory.Path, "llmfit-gate1.evidence.json");
        var writer = new LlmFitGate1EvidenceWriter();
        LlmFitGate1Evidence first = CreateEvidence() with { Disposition = "Blocked" };
        LlmFitGate1Evidence replacement = CreateEvidence() with
        {
            Disposition = "FunctionalPassWithPackagingConcern",
            DiagnosticCodes =
            [
                LlmFitGate1DiagnosticCodes.WindowsIntelNpuGap,
                LlmFitGate1DiagnosticCodes.DependencyLicenseInventoryPending,
                LlmFitGate1DiagnosticCodes.SignatureClaimMismatch,
            ],
        };

        _ = await writer.WriteAsync(first, outputPath, CancellationToken.None)
            .ConfigureAwait(false);
        string writtenPath = await writer.WriteAsync(
                replacement,
                outputPath,
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.AreEqual(Path.GetFullPath(outputPath), writtenPath);
        using JsonDocument document = JsonDocument.Parse(
            await File.ReadAllTextAsync(writtenPath).ConfigureAwait(false));
        JsonElement root = document.RootElement;
        Assert.AreEqual("FunctionalPassWithPackagingConcern", root.GetProperty("disposition").GetString());
        Assert.AreEqual("llmfit-v1.1.9-win-x64", root.GetProperty("candidateId").GetString());
        Assert.AreEqual("1.1.9", root.GetProperty("expectedVersion").GetString());
        Assert.AreEqual("llmfit 1.1.9", root.GetProperty("reportedVersion").GetString());
        Assert.AreEqual(ReleaseCommit, root.GetProperty("releaseCommit").GetString());
        Assert.AreEqual(ArchiveHash, root.GetProperty("expectedArchiveSha256").GetString());
        Assert.AreEqual(ArchiveHash, root.GetProperty("observedArchiveSha256").GetString());
        Assert.AreEqual(ExecutableHash, root.GetProperty("expectedExecutableSha256").GetString());
        Assert.AreEqual(ExecutableHash, root.GetProperty("observedExecutableSha256").GetString());
        CollectionAssert.AreEqual(
            ExpectedVersionArguments,
            root.GetProperty("versionInvocationArguments")
                .EnumerateArray()
                .Select(static item => item.GetString())
                .ToArray());
        CollectionAssert.AreEqual(
            ExpectedSystemArguments,
            root.GetProperty("systemInvocationArguments")
                .EnumerateArray()
                .Select(static item => item.GetString())
                .ToArray());
        Assert.AreEqual(1250L, root.GetProperty("durationMilliseconds").GetInt64());
        Assert.IsTrue(root.GetProperty("jsonValid").GetBoolean());
        Assert.IsTrue(root.GetProperty("requiredCpuRamPresent").GetBoolean());
        Assert.AreEqual(16, root.GetProperty("cpuLogicalProcessorCount").GetInt32());
        Assert.AreEqual(32.0, root.GetProperty("totalRamGiB").GetDouble());
        Assert.AreEqual(12.5, root.GetProperty("availableRamGiB").GetDouble());
        Assert.AreEqual("llmfit-system.raw.json", root.GetProperty("rawSystemJsonFileName").GetString());
        Assert.AreEqual(HashC, root.GetProperty("rawSystemJsonSha256").GetString());
        CollectionAssert.AreEqual(
            new[]
            {
                LlmFitGate1DiagnosticCodes.DependencyLicenseInventoryPending,
                LlmFitGate1DiagnosticCodes.SignatureClaimMismatch,
                LlmFitGate1DiagnosticCodes.WindowsIntelNpuGap,
            },
            root.GetProperty("diagnosticCodes")
                .EnumerateArray()
                .Select(static item => item.GetString())
                .ToArray());
        Assert.IsFalse(root.TryGetProperty("CandidateId", out _));
        Assert.HasCount(0, Directory.GetFiles(directory.Path, "*.tmp-*", SearchOption.TopDirectoryOnly));
    }

    [TestMethod]
    public async Task WriteAsync_SensitiveOrNonAllowlistedStrings_AreRejected()
    {
        PropertyInfo[] properties = typeof(LlmFitGate1Evidence).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (PropertyInfo property in properties)
        {
            Assert.IsFalse(property.Name.Contains("User", StringComparison.OrdinalIgnoreCase), property.Name);
            Assert.IsFalse(property.Name.Contains("Host", StringComparison.OrdinalIgnoreCase), property.Name);
            Assert.IsFalse(property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase), property.Name);
            Assert.IsFalse(property.Name.Contains("Serial", StringComparison.OrdinalIgnoreCase), property.Name);
            Assert.AreNotEqual("StandardOutput", property.Name, ignoreCase: true);
            Assert.AreNotEqual("StandardError", property.Name, ignoreCase: true);
            Assert.AreNotEqual("RawSystemJson", property.Name, ignoreCase: true);
        }

        CollectionAssert.AreEquivalent(
            ExpectedDiagnosticCodes,
            LlmFitGate1DiagnosticCodes.All.ToArray());

        using var directory = new OwnedTemporaryDirectory();
        var writer = new LlmFitGate1EvidenceWriter();
        string pathSeparatorOutput = Path.Combine(directory.Path, "path-separator.json");
        string freeFormOutput = Path.Combine(directory.Path, "free-form.json");

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
                () => writer.WriteAsync(
                    CreateEvidence() with { RawSystemJsonFileName = @"private\raw.json" },
                    pathSeparatorOutput,
                    CancellationToken.None))
            .ConfigureAwait(false);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(
                () => writer.WriteAsync(
                    CreateEvidence() with { DiagnosticCodes = ["HI-LLMFIT-JSON-INVALID: private detail"] },
                    freeFormOutput,
                    CancellationToken.None))
            .ConfigureAwait(false);

        Assert.IsFalse(File.Exists(pathSeparatorOutput));
        Assert.IsFalse(File.Exists(freeFormOutput));
        Assert.HasCount(0, Directory.GetFiles(directory.Path, "*.tmp-*", SearchOption.TopDirectoryOnly));
    }

    [TestMethod]
    public async Task WriteAsync_AncestorReparsePoint_IsRejectedBeforeCreation()
    {
        using var directory = new OwnedTemporaryDirectory();
        string physicalDirectory = Path.Combine(directory.Path, "physical");
        string linkedDirectory = Path.Combine(directory.Path, "linked");
        Directory.CreateDirectory(physicalDirectory);
        Directory.CreateSymbolicLink(linkedDirectory, physicalDirectory);

        try
        {
            string outputPath = Path.Combine(linkedDirectory, "nested", "evidence.json");
            await Assert.ThrowsExactlyAsync<InvalidDataException>(
                    () => new LlmFitGate1EvidenceWriter().WriteAsync(
                        CreateEvidence(),
                        outputPath,
                        CancellationToken.None))
                .ConfigureAwait(false);

            Assert.IsFalse(Directory.Exists(Path.Combine(physicalDirectory, "nested")));
            Assert.IsFalse(File.Exists(outputPath));
        }
        finally
        {
            if (Directory.Exists(linkedDirectory))
            {
                Directory.Delete(linkedDirectory);
            }
        }
    }

    [TestMethod]
    public async Task WriteAsync_AcceptedDispositionWithTerminalFailure_IsRejected()
    {
        using var directory = new OwnedTemporaryDirectory();
        LlmFitGate1Evidence accepted = CreateEvidence() with
        {
            Disposition = "FunctionalPassWithPackagingConcern",
        };
        LlmFitGate1Evidence[] contradictions =
        [
            accepted with { ProcessStartFailed = true },
            accepted with { SocketObservationFailed = true },
            accepted with { TimedOut = true },
            accepted with { Cancelled = true },
            accepted with { StandardOutputTruncated = true },
            accepted with { StandardErrorTruncated = true },
            accepted with { VersionCandidateSocketObserved = true },
            accepted with { SystemCandidateSocketObserved = true },
            accepted with { VersionCandidateProcessRemainedAfterExit = true },
            accepted with { SystemCandidateProcessRemainedAfterExit = true },
            accepted with { JsonValid = false, RawSystemJsonFileName = null, RawSystemJsonSha256 = null },
            accepted with { RequiredCpuRamPresent = false },
        ];

        for (int index = 0; index < contradictions.Length; index++)
        {
            string outputPath = Path.Combine(directory.Path, $"contradiction-{index}.json");
            await Assert.ThrowsExactlyAsync<InvalidDataException>(
                    () => new LlmFitGate1EvidenceWriter().WriteAsync(
                        contradictions[index],
                        outputPath,
                        CancellationToken.None))
                .ConfigureAwait(false);
            Assert.IsFalse(File.Exists(outputPath));
        }
    }

    [TestMethod]
    public async Task WriteAsync_GateIdentityAndSensitiveStringFields_ArePinned()
    {
        using var directory = new OwnedTemporaryDirectory();
        LlmFitGate1Evidence baseline = CreateEvidence();
        LlmFitGate1Evidence[] invalidValues =
        [
            baseline with { SchemaVersion = "2.0" },
            baseline with { CandidateId = "arian-private-host" },
            baseline with { ExpectedVersion = "9.9.9" },
            baseline with { ReportedVersion = "Arian Secret Host" },
            baseline with { ExpectedPeMachine = "SECRET" },
            baseline with { ObservedPeMachine = "SECRET" },
            baseline with { RawSystemJsonFileName = "arian-private.json" },
            baseline with { RawSystemJsonFileName = null },
            baseline with { RawSystemJsonSha256 = null },
        ];

        for (int index = 0; index < invalidValues.Length; index++)
        {
            string outputPath = Path.Combine(directory.Path, $"identity-{index}.json");
            await Assert.ThrowsExactlyAsync<InvalidDataException>(
                    () => new LlmFitGate1EvidenceWriter().WriteAsync(
                        invalidValues[index],
                        outputPath,
                        CancellationToken.None))
                .ConfigureAwait(false);
            Assert.IsFalse(File.Exists(outputPath));
        }
    }

    [TestMethod]
    public async Task WriteAsync_PreCancelledToken_PreservesExistingEvidence()
    {
        using var directory = new OwnedTemporaryDirectory();
        string outputPath = Path.Combine(directory.Path, "evidence.json");
        const string Original = "{\"original\":true}";
        await File.WriteAllTextAsync(outputPath, Original).ConfigureAwait(false);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
                () => new LlmFitGate1EvidenceWriter().WriteAsync(
                    CreateEvidence(),
                    outputPath,
                    cancellation.Token))
            .ConfigureAwait(false);

        Assert.AreEqual(Original, await File.ReadAllTextAsync(outputPath).ConfigureAwait(false));
    }

    [TestMethod]
    public async Task WriteAsync_TemporaryPathSwap_PublishesHeldEvidenceNotReplacement()
    {
        using var directory = new OwnedTemporaryDirectory();
        string outputPath = Path.Combine(directory.Path, "evidence.json");
        string? replacementPath = null;
        string? displacedOwnedPath = null;
        var writer = new LlmFitGate1EvidenceWriter(
            temporaryPath =>
            {
                replacementPath = temporaryPath;
                displacedOwnedPath = temporaryPath + ".displaced";
                File.Move(temporaryPath, displacedOwnedPath);
                File.WriteAllText(temporaryPath, "attacker replacement");
            });

        _ = await writer.WriteAsync(CreateEvidence(), outputPath, CancellationToken.None)
            .ConfigureAwait(false);

        Assert.IsTrue(
            File.Exists(outputPath),
            string.Join(",", Directory.GetFiles(directory.Path).Select(Path.GetFileName)));
        using JsonDocument document = JsonDocument.Parse(
            await File.ReadAllTextAsync(outputPath).ConfigureAwait(false));
        Assert.AreEqual("llmfit-v1.1.9-win-x64", document.RootElement.GetProperty("candidateId").GetString());
        Assert.IsNotNull(replacementPath);
        Assert.AreEqual("attacker replacement", await File.ReadAllTextAsync(replacementPath).ConfigureAwait(false));
        Assert.IsNotNull(displacedOwnedPath);
        Assert.IsFalse(File.Exists(displacedOwnedPath));
    }

    [TestMethod]
    public async Task WriteAsync_PrePublicationFailure_PreservesDestinationAndDeletesOnlyOwnedFile()
    {
        using var directory = new OwnedTemporaryDirectory();
        string outputPath = Path.Combine(directory.Path, "evidence.json");
        const string Original = "{\"original\":true}";
        await File.WriteAllTextAsync(outputPath, Original).ConfigureAwait(false);
        string? replacementPath = null;
        string? displacedOwnedPath = null;
        var writer = new LlmFitGate1EvidenceWriter(
            temporaryPath =>
            {
                replacementPath = temporaryPath;
                displacedOwnedPath = temporaryPath + ".displaced";
                File.Move(temporaryPath, displacedOwnedPath);
                File.WriteAllText(temporaryPath, "attacker replacement");
                throw new InvalidOperationException("controlled pre-publication failure");
            });

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => writer.WriteAsync(CreateEvidence(), outputPath, CancellationToken.None))
            .ConfigureAwait(false);

        Assert.AreEqual(Original, await File.ReadAllTextAsync(outputPath).ConfigureAwait(false));
        Assert.IsNotNull(replacementPath);
        Assert.AreEqual("attacker replacement", await File.ReadAllTextAsync(replacementPath).ConfigureAwait(false));
        Assert.IsNotNull(displacedOwnedPath);
        Assert.IsFalse(File.Exists(displacedOwnedPath));
    }

    [TestMethod]
    public async Task WriteAsync_LockedDestination_PreservesExistingEvidenceAndRemovesOwnedTemporaryFile()
    {
        using var directory = new OwnedTemporaryDirectory();
        string outputPath = Path.Combine(directory.Path, "evidence.json");
        const string Original = "{\"original\":true}";
        await File.WriteAllTextAsync(outputPath, Original).ConfigureAwait(false);
        await using var destinationLock = new FileStream(
            outputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        await Assert.ThrowsAsync<IOException>(
                () => new LlmFitGate1EvidenceWriter().WriteAsync(
                    CreateEvidence(),
                    outputPath,
                    CancellationToken.None))
            .ConfigureAwait(false);

        destinationLock.Position = 0;
        using var reader = new StreamReader(destinationLock, leaveOpen: true);
        Assert.AreEqual(Original, await reader.ReadToEndAsync().ConfigureAwait(false));
        Assert.HasCount(0, Directory.GetFiles(directory.Path, "*.tmp-*", SearchOption.TopDirectoryOnly));
    }

    private static LlmFitGate1Evidence CreateEvidence()
    {
        DateTimeOffset startedAt = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        return new LlmFitGate1Evidence(
            SchemaVersion: "1.0",
            Disposition: "Rejected",
            CandidateId: "llmfit-v1.1.9-win-x64",
            ExpectedVersion: "1.1.9",
            ReportedVersion: "llmfit 1.1.9",
            ReleaseCommit,
            ExpectedArchiveSha256: ArchiveHash,
            ObservedArchiveSha256: ArchiveHash,
            ExpectedExecutableSha256: ExecutableHash,
            ObservedExecutableSha256: ExecutableHash,
            ExpectedPeMachine: "AMD64",
            ObservedPeMachine: "AMD64",
            AuthenticodePresent: false,
            AuthenticodeStatus: "NotSigned",
            VersionInvocationArguments: ["--version"],
            SystemInvocationArguments: ["--no-dashboard", "--json", "system"],
            GateStartedAtUtc: startedAt,
            GateCompletedAtUtc: startedAt.AddMilliseconds(1250),
            DurationMilliseconds: 1250,
            VersionExitCode: 0,
            SystemExitCode: 0,
            ProcessStartFailed: false,
            SocketObservationFailed: false,
            TimedOut: false,
            Cancelled: false,
            StandardOutputTruncated: false,
            StandardErrorTruncated: false,
            JsonValid: true,
            RequiredCpuRamPresent: true,
            CpuLogicalProcessorCount: 16,
            TotalRamGiB: 32,
            AvailableRamGiB: 12.5,
            GpuReported: true,
            ReportedGpuCount: 1,
            IntelGpuReported: true,
            DedicatedSharedMemorySemanticsEstablished: false,
            IntelNpuDetectionState: "DetectionUnavailable",
            VersionCandidateSocketObserved: false,
            VersionDashboardPortObserved: false,
            SystemCandidateSocketObserved: false,
            SystemDashboardPortObserved: false,
            VersionCandidateProcessRemainedAfterExit: false,
            SystemCandidateProcessRemainedAfterExit: false,
            RawSystemJsonFileName: "llmfit-system.raw.json",
            RawSystemJsonSha256: HashC,
            DiagnosticCodes: [LlmFitGate1DiagnosticCodes.WindowsIntelNpuGap]);
    }

    private static readonly string[] ExpectedDiagnosticCodes =
    [
        "HI-LLMFIT-MANIFEST-INVALID",
        "HI-LLMFIT-PACKAGE-MISSING",
        "HI-LLMFIT-UNEXPECTED-PACKAGE-MEMBER",
        "HI-LLMFIT-PACKAGE-CHANGED-DURING-RUN",
        "HI-LLMFIT-PATH-ESCAPE",
        "HI-LLMFIT-REPARSE-POINT",
        "HI-LLMFIT-ARCHIVE-LENGTH-MISMATCH",
        "HI-LLMFIT-ARCHIVE-HASH-MISMATCH",
        "HI-LLMFIT-EXECUTABLE-HASH-MISMATCH",
        "HI-LLMFIT-PE-INVALID",
        "HI-LLMFIT-PE-ARCHITECTURE-MISMATCH",
        "HI-LLMFIT-SIGNATURE-CLAIM-MISMATCH",
        "HI-LLMFIT-SIGNATURE-STATUS-CHANGED",
        "HI-LLMFIT-DEPENDENCY-LICENSE-INVENTORY-PENDING",
        "HI-LLMFIT-PROCESS-START-FAILED",
        "HI-LLMFIT-VERSION-MISMATCH",
        "HI-LLMFIT-PROCESS-TIMED-OUT",
        "HI-LLMFIT-PROCESS-CANCELLED",
        "HI-LLMFIT-PROCESS-EXIT-NONZERO",
        "HI-LLMFIT-STDOUT-TRUNCATED",
        "HI-LLMFIT-STDERR-TRUNCATED",
        "HI-LLMFIT-SOCKET-OBSERVATION-FAILED",
        "HI-LLMFIT-CANDIDATE-SOCKET-OBSERVED",
        "HI-LLMFIT-DASHBOARD-PORT-OBSERVED",
        "HI-LLMFIT-RESIDUAL-PROCESS",
        "HI-LLMFIT-JSON-INVALID",
        "HI-LLMFIT-CPU-RAM-MISSING",
        "HI-LLMFIT-GPU-INCONSISTENT",
        "HI-LLMFIT-WINDOWS-INTEL-MEMORY-SEMANTICS-GAP",
        "HI-LLMFIT-WINDOWS-INTEL-NPU-GAP",
        "HI-LLMFIT-SCHEMA-DOCUMENTATION-DRIFT",
        "HI-GATE1-WRONG-TARGET",
        "HI-GATE1-WINDOWS-COMPARISON-FAILED",
        "HI-GATE1-OFFLINE-PRECONDITION-FAILED",
        "HI-GATE1-PRIVACY-VALIDATION-FAILED",
        "HI-GATE1-REQUIRED-TEST-FAILURE",
    ];

    private sealed class OwnedTemporaryDirectory : IDisposable
    {
        private const string Prefix = "GraniteEdgeAI-LlmFit-Evidence-Test-";

        internal OwnedTemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                Prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            string fullPath = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(Path));
            string temporaryRoot = System.IO.Path.TrimEndingDirectorySeparator(
                System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()));
            string name = System.IO.Path.GetFileName(fullPath);
            if (!string.Equals(Directory.GetParent(fullPath)?.FullName, temporaryRoot, StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith(Prefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(name[Prefix.Length..], "N", out _))
            {
                throw new InvalidOperationException("The owned evidence test directory identity was lost.");
            }

            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }
        }
    }
}
#pragma warning restore CA1707
