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
    private const string HashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string HashB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
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
        Assert.AreEqual(HashA, root.GetProperty("expectedArchiveSha256").GetString());
        Assert.AreEqual(HashA, root.GetProperty("observedArchiveSha256").GetString());
        Assert.AreEqual(HashB, root.GetProperty("expectedExecutableSha256").GetString());
        Assert.AreEqual(HashB, root.GetProperty("observedExecutableSha256").GetString());
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
            ExpectedArchiveSha256: HashA,
            ObservedArchiveSha256: HashA,
            ExpectedExecutableSha256: HashB,
            ObservedExecutableSha256: HashB,
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
