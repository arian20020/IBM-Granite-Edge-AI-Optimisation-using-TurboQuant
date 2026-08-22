using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HardwareInspection.LlmFitSpike.Candidate;
using HardwareInspection.LlmFitSpike.Command;
using HardwareInspection.LlmFitSpike.Evidence;
using HardwareInspection.LlmFitSpike.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HardwareInspection.LlmFitSpike.Tests.Evidence;

[TestClass]
[DoNotParallelize]
[TestCategory("Deterministic")]
#pragma warning disable CA1707 // Test names intentionally encode the required behavior.
public sealed class LlmFitGate1RunnerTests
{
    private const string ArchiveHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ApprovedVersionText = "llmfit 1.1.9";
    private const string ExecutableHash =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string ValidSystemJson =
        "{\"system\":{\"total_ram_gb\":32,\"available_ram_gb\":16," +
        "\"cpu_cores\":8,\"cpu_name\":\"Fixture CPU\",\"has_gpu\":false," +
        "\"gpu_count\":0,\"gpu_name\":null,\"gpus\":[]}}";
    private static readonly string[] ExpectedSuccessfulCalls =
    [
        "load manifest",
        "verify package",
        "observe and run --version",
        "verify package",
        "observe and run --no-dashboard --json system",
        "verify package",
        "write ignored raw capture",
        "write sanitized evidence",
    ];
    private static readonly string[] ExpectedVersionArguments = ["--version"];
    private static readonly string[] ExpectedSystemArguments = ["--no-dashboard", "--json", "system"];
    private static readonly string[] ExpectedVersionMismatchCalls =
        ["load manifest", "verify package", "observe and run --version"];

    [TestMethod]
    public async Task RunAsync_ValidUnsignedCandidate_ReturnsFunctionalPassWithPackagingConcern()
    {
        using var scope = new RunnerScope();
        LlmFitCandidateManifest manifest = await BuildRealCandidatePackageAsync(scope)
            .ConfigureAwait(false);
        var boundary = new RealBoundarySet(manifest);
        var runner = boundary.CreateRunner();

        LlmFitGate1RunResult result = await runner.RunAsync(
                scope.CreateOptions(),
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.AreEqual(
            LlmFitGate1Disposition.FunctionalPassWithPackagingConcern,
            result.Disposition,
            "codes=" + string.Join(',', result.DiagnosticCodes) +
                "; calls=" + string.Join(',', boundary.Calls));
        CollectionAssert.Contains(
            result.DiagnosticCodes.ToArray(),
            LlmFitGate1DiagnosticCodes.DependencyLicenseInventoryPending);
        CollectionAssert.Contains(
            result.DiagnosticCodes.ToArray(),
            LlmFitGate1DiagnosticCodes.SignatureClaimMismatch);
        CollectionAssert.AreEqual(ExpectedSuccessfulCalls, boundary.Calls.ToArray());
        Assert.IsTrue(File.Exists(Path.Combine(scope.OutputDirectory, "llmfit-system.raw.json")));
        Assert.IsTrue(File.Exists(result.EvidencePath));
        string evidence = await File.ReadAllTextAsync(result.EvidencePath).ConfigureAwait(false);
        Assert.IsFalse(evidence.Contains(scope.CandidateRoot, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(evidence.Contains("\"system\":", StringComparison.Ordinal));
        using JsonDocument document = JsonDocument.Parse(evidence);
        JsonElement root = document.RootElement;
        Assert.IsFalse(root.GetProperty("versionCandidateSocketObserved").GetBoolean());
        Assert.IsFalse(root.GetProperty("systemCandidateSocketObserved").GetBoolean());
        Assert.IsFalse(root.GetProperty("versionCandidateProcessRemainedAfterExit").GetBoolean());
        Assert.IsFalse(root.GetProperty("systemCandidateProcessRemainedAfterExit").GetBoolean());
    }

    [TestMethod]
    public async Task RunAsync_VersionMismatch_DoesNotRunSystemCommand()
    {
        using var scope = new RunnerScope();
        var boundary = BoundarySet.CreateDefault(scope.OutputDirectory);
        boundary.ProcessResults.Enqueue(CreateProcessResult("llmfit 1.1.8\r\n"));
        boundary.Observations.Enqueue(CleanObservation());

        LlmFitGate1RunResult result = await boundary.CreateRunner().RunAsync(
                scope.CreateOptions(),
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.AreEqual(LlmFitGate1Disposition.Rejected, result.Disposition);
        CollectionAssert.Contains(result.DiagnosticCodes.ToArray(), LlmFitGate1DiagnosticCodes.VersionMismatch);
        Assert.HasCount(1, boundary.Commands);
        CollectionAssert.AreEqual(ExpectedVersionArguments, boundary.Commands[0].Arguments);
        CollectionAssert.AreEqual(ExpectedVersionMismatchCalls, boundary.Calls.ToArray());
        Assert.AreEqual(string.Empty, result.EvidencePath);
    }

    [TestMethod]
    public async Task RunAsync_PackageChangesBetweenCommands_RejectsBeforeSystemCommand()
    {
        using var scope = new RunnerScope();
        var boundary = BoundarySet.CreateDefault(scope.OutputDirectory);
        boundary.Verifications.Clear();
        boundary.Verifications.Enqueue(CreateVerification());
        boundary.Verifications.Enqueue(CreateVerification() with
        {
            ArchiveSha256 = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
        });
        boundary.ProcessResults.Enqueue(CreateProcessResult("llmfit 1.1.9\n"));
        boundary.Observations.Enqueue(CleanObservation());

        LlmFitGate1RunResult result = await boundary.CreateRunner().RunAsync(
                scope.CreateOptions(),
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.AreEqual(LlmFitGate1Disposition.Rejected, result.Disposition);
        CollectionAssert.Contains(
            result.DiagnosticCodes.ToArray(),
            LlmFitGate1DiagnosticCodes.PackageChangedDuringRun);
        Assert.HasCount(1, boundary.Commands);
        Assert.AreEqual(2, boundary.VerifierCallCount);
        Assert.IsFalse(boundary.Calls.Contains("write ignored raw capture", StringComparer.Ordinal));
    }

    [TestMethod]
    public async Task RunAsync_SchemaOrLeakFailure_ReturnsRejectedAndSanitizedEvidence()
    {
        foreach (bool leak in new[] { false, true })
        {
            using var scope = new RunnerScope();
            var boundary = BoundarySet.CreateDefault(scope.OutputDirectory);
            boundary.ProcessResults.Enqueue(CreateProcessResult("llmfit 1.1.9\r\n"));
            boundary.ProcessResults.Enqueue(CreateProcessResult(
                leak ? ValidSystemJson : "{\"privatePath\":\"C:\\\\Users\\\\Arian\"}"));
            boundary.Observations.Enqueue(CleanObservation());
            boundary.Observations.Enqueue(
                leak
                    ? new LlmFitProcessObservation(true, false, Array.Empty<int>(), false)
                    : CleanObservation());

            LlmFitGate1RunResult result = await boundary.CreateRunner().RunAsync(
                    scope.CreateOptions(),
                    CancellationToken.None)
                .ConfigureAwait(false);

            Assert.AreEqual(LlmFitGate1Disposition.Rejected, result.Disposition);
            Assert.IsTrue(File.Exists(result.EvidencePath));
            string evidence = await File.ReadAllTextAsync(result.EvidencePath).ConfigureAwait(false);
            Assert.IsFalse(evidence.Contains("privatePath", StringComparison.Ordinal));
            Assert.IsFalse(evidence.Contains(@"C:\\Users\\Arian", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(evidence.Contains(scope.CandidateRoot, StringComparison.OrdinalIgnoreCase));
            CollectionAssert.Contains(
                result.DiagnosticCodes.ToArray(),
                leak
                    ? LlmFitGate1DiagnosticCodes.CandidateSocketObserved
                    : LlmFitGate1DiagnosticCodes.JsonInvalid);
        }
    }

    [TestMethod]
    public async Task WriteRawSystemJsonAsync_VerbatimUtf8_UsesFixedAtomicFile()
    {
        using var scope = new RunnerScope();
        const string RawJson = "{\"system\":{\"cpu_name\":\"Gránite\"}}\r\n";
        var store = new GateOutputStore();
        string outputPath = Path.Combine(scope.OutputDirectory, "llmfit-system.raw.json");
        GateRawOutput result;
        using (IGateOutputSession session = store.AcquireFreshRun(scope.OutputDirectory))
        {
            result = await store.WriteRawSystemJsonAsync(
                    session,
                    RawJson,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }

        byte[] expected = new UTF8Encoding(false, true).GetBytes(RawJson);
        CollectionAssert.AreEqual(expected, await File.ReadAllBytesAsync(outputPath).ConfigureAwait(false));
        Assert.AreEqual("llmfit-system.raw.json", result.FileName);
        Assert.AreEqual(
            Convert.ToHexString(SHA256.HashData(expected)).ToLowerInvariant(),
            result.Sha256);
        Assert.HasCount(0, Directory.GetFiles(scope.OutputDirectory, "*.tmp-*"));
    }

    [TestMethod]
    public async Task RunAsync_VersionTerminalFailures_StopBeforeSystemWithStableCodes()
    {
        var cases = new (LlmFitProcessResult Process, LlmFitProcessObservation Observation, string Code, bool EnterObserver)[]
        {
            (CreateProcessResult(string.Empty) with { ProcessId = null, ExitCode = null, ProcessStartFailed = true },
                CleanObservation(), LlmFitGate1DiagnosticCodes.ProcessStartFailed, true),
            (CreateProcessResult(string.Empty) with { ObserverFailed = true },
                CleanObservation(), LlmFitGate1DiagnosticCodes.SocketObservationFailed, true),
            (CreateProcessResult(string.Empty) with { ExitCode = null, TimedOut = true },
                CleanObservation(), LlmFitGate1DiagnosticCodes.ProcessTimedOut, true),
            (CreateProcessResult(string.Empty) with { ExitCode = null, Cancelled = true },
                CleanObservation(), LlmFitGate1DiagnosticCodes.ProcessCancelled, true),
            (CreateProcessResult(string.Empty) with { ExitCode = 23 },
                CleanObservation(), LlmFitGate1DiagnosticCodes.ProcessExitNonzero, true),
            (CreateProcessResult(ApprovedVersionText) with { StandardOutputTruncated = true },
                CleanObservation(), LlmFitGate1DiagnosticCodes.StdoutTruncated, true),
            (CreateProcessResult(ApprovedVersionText) with { StandardErrorTruncated = true },
                CleanObservation(), LlmFitGate1DiagnosticCodes.StderrTruncated, true),
            (CreateProcessResult(ApprovedVersionText),
                new LlmFitProcessObservation(true, false, Array.Empty<int>(), false),
                LlmFitGate1DiagnosticCodes.CandidateSocketObserved, true),
            (CreateProcessResult(ApprovedVersionText),
                new LlmFitProcessObservation(true, true, [8787], false),
                LlmFitGate1DiagnosticCodes.DashboardPortObserved, true),
            (CreateProcessResult(ApprovedVersionText),
                new LlmFitProcessObservation(false, false, Array.Empty<int>(), true),
                LlmFitGate1DiagnosticCodes.ResidualProcess, true),
            (CreateProcessResult(ApprovedVersionText),
                CleanObservation(), LlmFitGate1DiagnosticCodes.SocketObservationFailed, false),
            (CreateProcessResult(ApprovedVersionText + "\r\n\r\n"),
                CleanObservation(), LlmFitGate1DiagnosticCodes.VersionMismatch, true),
        };

        foreach ((LlmFitProcessResult process, LlmFitProcessObservation observation, string code, bool enterObserver) in cases)
        {
            using var scope = new RunnerScope();
            var boundary = BoundarySet.CreateDefault(scope.OutputDirectory);
            boundary.ProcessResults.Enqueue(process);
            boundary.Observations.Enqueue(observation);
            boundary.InvokeObserver = enterObserver;

            LlmFitGate1RunResult result = await boundary.CreateRunner().RunAsync(
                    scope.CreateOptions(),
                    CancellationToken.None)
                .ConfigureAwait(false);

            CollectionAssert.Contains(result.DiagnosticCodes.ToArray(), code);
            Assert.HasCount(1, boundary.Commands, code);
            Assert.IsFalse(
                boundary.Commands.Any(static command => command.Arguments.SequenceEqual(
                    ExpectedSystemArguments,
                    StringComparer.Ordinal)),
                code);
            Assert.AreEqual(
                code == LlmFitGate1DiagnosticCodes.ProcessCancelled
                    ? LlmFitGate1Disposition.Blocked
                    : LlmFitGate1Disposition.Rejected,
                result.Disposition,
                code);
        }
    }

    [TestMethod]
    public async Task RunAsync_VerifierDiagnostics_AreExhaustivelyNormalizedToAllowlist()
    {
        var cases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HI-LLMFIT-ARCHIVE-HASH-MISMATCH"] = LlmFitGate1DiagnosticCodes.ArchiveHashMismatch,
            ["HI-LLMFIT-ARCHIVE-LENGTH-MISMATCH"] = LlmFitGate1DiagnosticCodes.ArchiveLengthMismatch,
            ["HI-LLMFIT-EXECUTABLE-HASH-MISMATCH"] = LlmFitGate1DiagnosticCodes.ExecutableHashMismatch,
            ["HI-LLMFIT-MISSING-REQUIRED-FILE"] = LlmFitGate1DiagnosticCodes.PackageMissing,
            ["HI-LLMFIT-OBSERVATION-INVALID"] = LlmFitGate1DiagnosticCodes.SignatureStatusChanged,
            ["HI-LLMFIT-OBSERVATION-MISMATCH"] = LlmFitGate1DiagnosticCodes.SignatureStatusChanged,
            ["HI-LLMFIT-PACKAGE-ACCESS-DENIED"] = LlmFitGate1DiagnosticCodes.PackageMissing,
            ["HI-LLMFIT-PACKAGE-INVALID"] = LlmFitGate1DiagnosticCodes.UnexpectedPackageMember,
            ["HI-LLMFIT-PACKAGE-IO-ERROR"] = LlmFitGate1DiagnosticCodes.PackageMissing,
            ["HI-LLMFIT-PACKAGE-PATH-INVALID"] = LlmFitGate1DiagnosticCodes.PathEscape,
            ["HI-LLMFIT-PACKAGE-ROOT-MISSING"] = LlmFitGate1DiagnosticCodes.PackageMissing,
            ["HI-LLMFIT-PE-INVALID"] = LlmFitGate1DiagnosticCodes.PeInvalid,
            ["HI-LLMFIT-PE-MACHINE-MISMATCH"] = LlmFitGate1DiagnosticCodes.PeArchitectureMismatch,
            ["HI-LLMFIT-REPARSE-POINT"] = LlmFitGate1DiagnosticCodes.ReparsePoint,
            ["HI-LLMFIT-SIGNATURE-CLAIM-MISMATCH"] = LlmFitGate1DiagnosticCodes.SignatureClaimMismatch,
            ["HI-LLMFIT-UNEXPECTED-PACKAGE-MEMBER"] = LlmFitGate1DiagnosticCodes.UnexpectedPackageMember,
            ["HI-LLMFIT-PRIVATE-FREE-FORM"] = LlmFitGate1DiagnosticCodes.RequiredTestFailure,
        };

        foreach ((string internalCode, string expectedCode) in cases)
        {
            using var scope = new RunnerScope();
            var boundary = BoundarySet.CreateDefault(scope.OutputDirectory);
            boundary.Verifications.Clear();
            boundary.Verifications.Enqueue(CreateVerification() with
            {
                IntegrityPassed = false,
                DiagnosticCodes = [internalCode],
            });

            LlmFitGate1RunResult result = await boundary.CreateRunner().RunAsync(
                    scope.CreateOptions(),
                    CancellationToken.None)
                .ConfigureAwait(false);

            CollectionAssert.Contains(result.DiagnosticCodes.ToArray(), expectedCode, internalCode);
            if (string.Equals(
                    internalCode,
                    "HI-LLMFIT-OBSERVATION-MISMATCH",
                    StringComparison.Ordinal))
            {
                CollectionAssert.Contains(
                    result.DiagnosticCodes.ToArray(),
                    LlmFitGate1DiagnosticCodes.SignatureStatusChanged,
                    internalCode);
            }
            Assert.IsTrue(result.DiagnosticCodes.All(LlmFitGate1DiagnosticCodes.All.Contains), internalCode);
            if (!string.Equals(internalCode, expectedCode, StringComparison.Ordinal))
            {
                Assert.IsFalse(
                    result.DiagnosticCodes.Contains(internalCode, StringComparer.Ordinal),
                    internalCode);
            }
            Assert.HasCount(0, boundary.Commands, internalCode);
        }
    }

    [TestMethod]
    public async Task WriteRawSystemJsonAsync_AncestorReparsePoint_IsRejectedWithoutTemporaryFile()
    {
        using var scope = new RunnerScope();
        string physicalDirectory = Path.Combine(scope.Root, "physical-output");
        string linkedDirectory = Path.Combine(scope.Root, "linked-output");
        Directory.CreateDirectory(physicalDirectory);
        Directory.CreateSymbolicLink(linkedDirectory, physicalDirectory);
        try
        {
            await Assert.ThrowsExactlyAsync<InvalidDataException>(
                    () => Task.Run(() =>
                        new GateOutputStore().AcquireFreshRun(linkedDirectory)))
                .ConfigureAwait(false);
            Assert.HasCount(0, Directory.GetFiles(physicalDirectory));
        }
        finally
        {
            Directory.Delete(linkedDirectory);
        }
    }

    [TestMethod]
    public async Task WriteRawSystemJsonAsync_HoldsEveryAncestorAgainstSwapUntilLeaseDisposal()
    {
        using var scope = new RunnerScope();
        string parent = Path.Combine(scope.Root, "leased-parent");
        string output = Path.Combine(parent, "nested", "output");
        string moved = Path.Combine(scope.Root, "moved-parent");
        Directory.CreateDirectory(output);
        bool swapWasBlocked = false;
        var store = new GateOutputStore(() =>
        {
            try
            {
                Directory.Move(parent, moved);
            }
            catch (IOException)
            {
                swapWasBlocked = true;
            }
            catch (UnauthorizedAccessException)
            {
                swapWasBlocked = true;
            }
        });

        using (IGateOutputSession session = store.AcquireFreshRun(output))
        {
            _ = await store.WriteRawSystemJsonAsync(
                    session,
                    ValidSystemJson,
                    CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsTrue(swapWasBlocked);
            Assert.IsTrue(File.Exists(Path.Combine(output, "llmfit-system.raw.json")));
        }

        Directory.Move(parent, moved);
        Assert.IsTrue(File.Exists(Path.Combine(
            moved,
            "nested",
            "output",
            "llmfit-system.raw.json")));
    }

    [TestMethod]
    public void AcquireFreshRun_ConcurrentOrNonemptyOutput_IsRejectedWithoutDeletingEntries()
    {
        using var scope = new RunnerScope();
        var firstStore = new GateOutputStore();
        using IGateOutputSession first = firstStore.AcquireFreshRun(scope.OutputDirectory);

        Assert.ThrowsExactly<InvalidDataException>(
            () => new GateOutputStore().AcquireFreshRun(scope.OutputDirectory));

        string unknownPath = Path.Combine(scope.OutputDirectory, "unknown.private");
        first.Dispose();
        File.WriteAllText(unknownPath, "owned by somebody else");
        Assert.ThrowsExactly<InvalidDataException>(
            () => new GateOutputStore().AcquireFreshRun(scope.OutputDirectory));
        Assert.IsTrue(File.Exists(unknownPath));
    }

    [TestMethod]
    public async Task RunAsync_HardVerifierDiagnostic_StopsBeforeCommandDespitePassingIntegrityFlag()
    {
        using var scope = new RunnerScope();
        var boundary = BoundarySet.CreateDefault(scope.OutputDirectory);
        boundary.Verifications.Clear();
        boundary.Verifications.Enqueue(CreateVerification() with
        {
            DiagnosticCodes = [LlmFitGate1DiagnosticCodes.ArchiveHashMismatch],
        });
        boundary.ProcessResults.Enqueue(CreateProcessResult(ApprovedVersionText));
        boundary.Observations.Enqueue(CleanObservation());

        LlmFitGate1RunResult result = await boundary.CreateRunner().RunAsync(
                scope.CreateOptions(),
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.AreEqual(LlmFitGate1Disposition.Rejected, result.Disposition);
        CollectionAssert.Contains(
            result.DiagnosticCodes.ToArray(),
            LlmFitGate1DiagnosticCodes.ArchiveHashMismatch);
        CollectionAssert.DoesNotContain(
            result.DiagnosticCodes.ToArray(),
            LlmFitGate1DiagnosticCodes.PrivacyValidationFailed);
        Assert.AreEqual(string.Empty, result.EvidencePath);
        Assert.HasCount(0, boundary.Commands);
        Assert.IsFalse(File.Exists(Path.Combine(scope.OutputDirectory, "llmfit-system.raw.json")));
    }

    [TestMethod]
    public async Task RunAsync_InconsistentUnsignedVerification_StopsBeforeCommand()
    {
        using var scope = new RunnerScope();
        var boundary = BoundarySet.CreateDefault(scope.OutputDirectory);
        boundary.Verifications.Clear();
        boundary.Verifications.Enqueue(CreateVerification() with
        {
            AuthenticodePresent = false,
            AuthenticodeStatus = "PresentUnverified",
            AuthenticodeSubject = "CN=Inconsistent",
            DiagnosticCodes = [LlmFitGate1DiagnosticCodes.SignatureClaimMismatch],
        });
        boundary.ProcessResults.Enqueue(CreateProcessResult(ApprovedVersionText));
        boundary.Observations.Enqueue(CleanObservation());

        LlmFitGate1RunResult result = await boundary.CreateRunner().RunAsync(
                scope.CreateOptions(),
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.AreEqual(LlmFitGate1Disposition.Rejected, result.Disposition);
        CollectionAssert.Contains(
            result.DiagnosticCodes.ToArray(),
            LlmFitGate1DiagnosticCodes.SignatureClaimMismatch);
        Assert.HasCount(0, boundary.Commands);
        Assert.AreEqual(string.Empty, result.EvidencePath);
    }

    [TestMethod]
    public async Task RunAsync_RawIdentityAndBytes_AreHeldThroughEvidencePublication()
    {
        using var scope = new RunnerScope();
        var boundary = BoundarySet.CreateDefault(scope.OutputDirectory);
        boundary.ProductionOutputStore = new GateOutputStore();
        boundary.ProcessResults.Enqueue(CreateProcessResult(ApprovedVersionText));
        boundary.ProcessResults.Enqueue(CreateProcessResult(ValidSystemJson));
        boundary.Observations.Enqueue(CleanObservation());
        boundary.Observations.Enqueue(CleanObservation());
        bool tamperWasBlocked = false;
        boundary.BeforeEvidenceWrite = () =>
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(scope.OutputDirectory, "llmfit-system.raw.json"),
                    "tampered");
            }
            catch (IOException)
            {
                tamperWasBlocked = true;
            }
            catch (UnauthorizedAccessException)
            {
                tamperWasBlocked = true;
            }
        };

        LlmFitGate1RunResult result = await boundary.CreateRunner().RunAsync(
                scope.CreateOptions(),
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.AreEqual(
            LlmFitGate1Disposition.FunctionalPassWithPackagingConcern,
            result.Disposition);
        Assert.IsTrue(tamperWasBlocked);
        string rawPath = Path.Combine(scope.OutputDirectory, "llmfit-system.raw.json");
        string actualHash = Convert.ToHexString(
                SHA256.HashData(await File.ReadAllBytesAsync(rawPath).ConfigureAwait(false)))
            .ToLowerInvariant();
        using JsonDocument evidence = JsonDocument.Parse(
            await File.ReadAllTextAsync(result.EvidencePath).ConfigureAwait(false));
        string? recordedHash = evidence.RootElement
            .GetProperty("rawSystemJsonSha256")
            .GetString();
        Assert.AreEqual(
            recordedHash,
            actualHash);
    }

    [TestMethod]
    public async Task RunAsync_DashboardObservation_ProducesConsistentSanitizedEvidence()
    {
        using var scope = new RunnerScope();
        var boundary = BoundarySet.CreateDefault(scope.OutputDirectory);
        boundary.ProcessResults.Enqueue(CreateProcessResult(ApprovedVersionText));
        boundary.ProcessResults.Enqueue(CreateProcessResult(ValidSystemJson));
        boundary.Observations.Enqueue(CleanObservation());
        boundary.Observations.Enqueue(
            new LlmFitProcessObservation(false, true, [8787], false));

        LlmFitGate1RunResult result = await boundary.CreateRunner().RunAsync(
                scope.CreateOptions(),
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.AreEqual(LlmFitGate1Disposition.Rejected, result.Disposition);
        CollectionAssert.Contains(
            result.DiagnosticCodes.ToArray(),
            LlmFitGate1DiagnosticCodes.CandidateSocketObserved);
        CollectionAssert.Contains(
            result.DiagnosticCodes.ToArray(),
            LlmFitGate1DiagnosticCodes.DashboardPortObserved);
        Assert.IsTrue(File.Exists(result.EvidencePath));
        using JsonDocument document = JsonDocument.Parse(
            await File.ReadAllTextAsync(result.EvidencePath).ConfigureAwait(false));
        Assert.IsTrue(document.RootElement.GetProperty("systemCandidateSocketObserved").GetBoolean());
        Assert.IsTrue(document.RootElement.GetProperty("systemDashboardPortObserved").GetBoolean());
    }

    [TestMethod]
    public async Task RunAsync_PreexistingPair_IsNeverMixedWithANewFailedRun()
    {
        foreach (string failure in new[] { "cancellation", "evidence", "system" })
        {
            using var scope = new RunnerScope();
            var first = BoundarySet.CreateDefault(scope.OutputDirectory);
            first.ProductionOutputStore = new GateOutputStore();
            first.ProcessResults.Enqueue(CreateProcessResult(ApprovedVersionText));
            first.ProcessResults.Enqueue(CreateProcessResult(ValidSystemJson));
            first.Observations.Enqueue(CleanObservation());
            first.Observations.Enqueue(CleanObservation());
            LlmFitGate1RunResult accepted = await first.CreateRunner().RunAsync(
                    scope.CreateOptions(),
                    CancellationToken.None)
                .ConfigureAwait(false);
            Assert.AreEqual(
                LlmFitGate1Disposition.FunctionalPassWithPackagingConcern,
                accepted.Disposition);

            string rawPath = Path.Combine(scope.OutputDirectory, "llmfit-system.raw.json");
            string evidencePath = Path.Combine(scope.OutputDirectory, "llmfit-gate1.evidence.json");
            byte[] originalRaw = await File.ReadAllBytesAsync(rawPath).ConfigureAwait(false);
            byte[] originalEvidence = await File.ReadAllBytesAsync(evidencePath).ConfigureAwait(false);

            var second = BoundarySet.CreateDefault(scope.OutputDirectory);
            second.ProductionOutputStore = new GateOutputStore();
            second.ProcessResults.Enqueue(CreateProcessResult(ApprovedVersionText));
            second.Observations.Enqueue(CleanObservation());
            using var cancellation = new CancellationTokenSource();
            if (failure == "cancellation")
            {
                cancellation.Cancel();
            }
            else
            {
                second.ProcessResults.Enqueue(
                    failure == "system"
                        ? CreateProcessResult(string.Empty) with { ExitCode = 23 }
                        : CreateProcessResult(ValidSystemJson));
                second.Observations.Enqueue(CleanObservation());
                second.EvidenceWriterFails = failure == "evidence";
            }

            LlmFitGate1RunResult failed = await second.CreateRunner().RunAsync(
                    scope.CreateOptions(),
                    cancellation.Token)
                .ConfigureAwait(false);

            if (failure == "cancellation")
            {
                Assert.AreEqual(LlmFitGate1Disposition.Blocked, failed.Disposition);
                CollectionAssert.Contains(
                    failed.DiagnosticCodes.ToArray(),
                    LlmFitGate1DiagnosticCodes.ProcessCancelled);
            }

            CollectionAssert.AreEqual(
                originalRaw,
                await File.ReadAllBytesAsync(rawPath).ConfigureAwait(false),
                failure);
            CollectionAssert.AreEqual(
                originalEvidence,
                await File.ReadAllBytesAsync(evidencePath).ConfigureAwait(false),
                failure);
            Assert.HasCount(0, second.Commands, failure);
        }
    }

    [TestMethod]
    public void Load_TamperedOrMissingOutputManifest_UsesEmbeddedCommittedIdentity()
    {
        string outputManifest = Path.Combine(
            AppContext.BaseDirectory,
            "Candidates",
            "llmfit-v1.1.9-win-x64.json");
        string backupManifest = outputManifest + ".owned-backup-" + Guid.NewGuid().ToString("N");
        byte[] original = File.ReadAllBytes(outputManifest);
        try
        {
            File.WriteAllText(
                outputManifest,
                "{\"schemaVersion\":\"private-tamper\"}",
                new UTF8Encoding(false));
            LlmFitCandidateManifest tampered = new AssemblyCandidateManifestSource().Load();
            Assert.AreEqual("llmfit-v1.1.9-win-x64", tampered.CandidateId);
            Assert.AreEqual("1.1.9", tampered.Version);

            File.WriteAllBytes(outputManifest, original);
            File.Move(outputManifest, backupManifest);
            LlmFitCandidateManifest missing = new AssemblyCandidateManifestSource().Load();
            Assert.AreEqual("llmfit-v1.1.9-win-x64", missing.CandidateId);
            Assert.AreEqual("1.1.9", missing.Version);
        }
        finally
        {
            if (File.Exists(backupManifest))
            {
                File.Move(backupManifest, outputManifest, overwrite: true);
            }
            else
            {
                File.WriteAllBytes(outputManifest, original);
            }
        }
    }

    private static LlmFitCandidateManifest CreateManifest(
        string archiveHash = ArchiveHash,
        string executableHash = ExecutableHash,
        long archiveLength = 7)
    {
        return new LlmFitCandidateManifest(
            "1.0",
            "llmfit-v1.1.9-win-x64",
            "1.1.9",
            "v1.1.9",
            "a02e13f1013ed69889ff44426a651bf7c68c292e",
            new DateTimeOffset(2026, 8, 9, 17, 7, 55, TimeSpan.Zero),
            new LlmFitCandidateArchive(
                "fake-mode.txt",
                new Uri("https://github.com/example/llmfit/releases/fake-mode.txt"),
                archiveLength,
                archiveHash),
            new LlmFitCandidateExecutable("llmfit.exe", executableHash, "AMD64", "ObserveAndRecord"),
            ["llmfit.exe", "LICENSE", "README.md"],
            new LlmFitCandidateCommands(["--version"], ["--no-dashboard", "--json", "system"]),
            new LlmFitCandidateLicense("MIT", "LICENSE"));
    }

    private static LlmFitCandidateVerification CreateVerification()
    {
        return new LlmFitCandidateVerification(
            true,
            ArchiveHash,
            ExecutableHash,
            "AMD64",
            false,
            "NotSigned",
            null,
            [LlmFitGate1DiagnosticCodes.SignatureClaimMismatch]);
    }

    private static LlmFitProcessResult CreateProcessResult(string standardOutput)
    {
        DateTimeOffset started = new(2026, 8, 16, 10, 0, 0, TimeSpan.Zero);
        return new LlmFitProcessResult(
            started,
            started.AddMilliseconds(25),
            1234,
            0,
            false,
            false,
            false,
            false,
            standardOutput,
            string.Empty,
            false,
            false);
    }

    private static LlmFitProcessObservation CleanObservation()
    {
        return new LlmFitProcessObservation(false, false, Array.Empty<int>(), false);
    }

    private static async Task<LlmFitCandidateManifest> BuildRealCandidatePackageAsync(
        RunnerScope scope)
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectPath = Path.Combine(
            repositoryRoot,
            "tests",
            "ProcessFixtures",
            "GraniteEdgeAI.HardwareInspection.LlmFitFakeTool",
            "GraniteEdgeAI.HardwareInspection.LlmFitFakeTool.csproj");
        string publishDirectory = Path.Combine(scope.Root, "single-file-publish");
        Directory.CreateDirectory(publishDirectory);

        string[] publishArguments =
        [
            "publish",
            projectPath,
            "--configuration",
            "Release",
            "--runtime",
            "win-x64",
            "--self-contained",
            "false",
            "--no-restore",
            "--output",
            publishDirectory,
            "-p:PublishSingleFile=true",
            "-p:UseAppHost=true",
            "-p:DebugType=None",
            "-p:DebugSymbols=false",
        ];
        var publishCommand = new LlmFitCommand("dotnet", repositoryRoot, publishArguments);
        LlmFitProcessResult publish = await new LlmFitProcessRunner().ExecuteAsync(
                publishCommand,
                TimeSpan.FromSeconds(120),
                whileRunningObserver: null,
                CancellationToken.None)
            .ConfigureAwait(false);
        Assert.IsTrue(publish.Succeeded, "The real-boundary fake tool publish failed.");
        Assert.IsFalse(publish.StandardOutputTruncated);
        Assert.IsFalse(publish.StandardErrorTruncated);

        string publishedExecutable = Path.Combine(
            publishDirectory,
            "GraniteEdgeAI.HardwareInspection.LlmFitFakeTool.exe");
        string candidateExecutable = Path.Combine(scope.CandidateRoot, "llmfit.exe");
        File.Copy(publishedExecutable, candidateExecutable, overwrite: false);
        await File.WriteAllTextAsync(Path.Combine(scope.CandidateRoot, "fake-mode.txt"), "success")
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(scope.CandidateRoot, "LICENSE"), "MIT")
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(scope.CandidateRoot, "README.md"), "fixture")
            .ConfigureAwait(false);

        string executableHash = await ComputeFileSha256Async(candidateExecutable).ConfigureAwait(false);
        string archivePath = Path.Combine(scope.CandidateRoot, "fake-mode.txt");
        string archiveHash = await ComputeFileSha256Async(archivePath).ConfigureAwait(false);
        string observation = JsonSerializer.Serialize(new
        {
            executableSha256 = executableHash,
            rawStatus = "NotSigned",
            signaturePresent = false,
            signerSubject = (string?)null,
            signerThumbprint = (string?)null,
            checkedAtUtc = "2026-08-16T10:00:00Z",
        });
        await File.WriteAllTextAsync(
                Path.Combine(scope.CandidateRoot, "authenticode-observation.json"),
                observation,
                new UTF8Encoding(false))
            .ConfigureAwait(false);

        return CreateManifest(archiveHash, executableHash, new FileInfo(archivePath).Length);
    }

    private static async Task<string> ComputeFileSha256Async(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new AssertFailedException("The repository root could not be found.");
    }

    private sealed class RealBoundarySet :
        ICandidateManifestSource,
        ILlmFitCandidateVerifier,
        ILlmFitProcessRunner,
        ILlmFitSocketObserver,
        IClock,
        IGateOutputStore,
        ILlmFitGate1EvidenceWriter
    {
        private readonly CandidateVerifierBoundary _verifier =
            new(new LlmFitCandidateVerifier());
        private readonly ProcessRunnerBoundary _processRunner =
            new(new LlmFitProcessRunner());
        private readonly TcpListenerObserverBoundary _observer = new();
        private readonly SystemClock _clock = new();
        private readonly GateOutputStore _outputStore = new();
        private readonly EvidenceWriterBoundary _evidenceWriter =
            new(new LlmFitGate1EvidenceWriter());
        private readonly LlmFitCandidateManifest _manifest;

        internal RealBoundarySet(LlmFitCandidateManifest manifest)
        {
            _manifest = manifest;
        }

        internal List<string> Calls { get; } = [];

        public DateTimeOffset UtcNow => _clock.UtcNow;

        internal LlmFitGate1Runner CreateRunner()
        {
            return new LlmFitGate1Runner(this, this, this, this, this, this, this);
        }

        public LlmFitCandidateManifest Load()
        {
            Calls.Add("load manifest");
            return _manifest;
        }

        public LlmFitCandidateVerification Verify(
            string candidateRoot,
            LlmFitCandidateManifest manifest)
        {
            Calls.Add("verify package");
            return _verifier.Verify(candidateRoot, manifest);
        }

        public Task<LlmFitProcessResult> ExecuteAsync(
            LlmFitCommand command,
            TimeSpan timeout,
            Func<int, CancellationToken, Task>? observer,
            CancellationToken cancellationToken)
        {
            Calls.Add("observe and run " + string.Join(' ', command.Arguments));
            return _processRunner.ExecuteAsync(command, timeout, observer, cancellationToken);
        }

        public LlmFitSocketObservationSession Create(string candidateImageName)
        {
            return _observer.Create(candidateImageName);
        }

        public IGateOutputSession AcquireFreshRun(string outputDirectory)
        {
            return _outputStore.AcquireFreshRun(outputDirectory);
        }

        public async Task<GateRawOutput> WriteRawSystemJsonAsync(
            IGateOutputSession session,
            string rawJson,
            CancellationToken cancellationToken)
        {
            Calls.Add("write ignored raw capture");
            return await _outputStore.WriteRawSystemJsonAsync(
                    session,
                    rawJson,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<string> WriteAsync(
            LlmFitGate1Evidence evidence,
            string outputPath,
            CancellationToken cancellationToken)
        {
            Calls.Add("write sanitized evidence");
            return await _evidenceWriter.WriteAsync(evidence, outputPath, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private sealed class BoundarySet :
        ICandidateManifestSource,
        ILlmFitCandidateVerifier,
        ILlmFitProcessRunner,
        ILlmFitSocketObserver,
        IClock,
        IGateOutputStore,
        ILlmFitGate1EvidenceWriter
    {
        private readonly string _outputDirectory;
        private int _clockReadCount;

        private BoundarySet(string outputDirectory)
        {
            _outputDirectory = outputDirectory;
        }

        internal List<string> Calls { get; } = [];

        internal List<LlmFitCommand> Commands { get; } = [];

        internal Queue<LlmFitCandidateVerification> Verifications { get; } = [];

        internal Queue<LlmFitProcessResult> ProcessResults { get; } = [];

        internal Queue<LlmFitProcessObservation> Observations { get; } = [];

        internal bool InvokeObserver { get; set; } = true;

        internal GateOutputStore? ProductionOutputStore { get; set; }

        internal bool EvidenceWriterFails { get; set; }

        internal Action? BeforeEvidenceWrite { get; set; }

        internal int VerifierCallCount { get; private set; }

        public DateTimeOffset UtcNow
        {
            get
            {
                int read = Interlocked.Increment(ref _clockReadCount);
                return new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero)
                    .AddMilliseconds(read == 1 ? 0 : 250);
            }
        }

        internal static BoundarySet CreateDefault(string outputDirectory)
        {
            var boundary = new BoundarySet(outputDirectory);
            boundary.Verifications.Enqueue(CreateVerification());
            boundary.Verifications.Enqueue(CreateVerification());
            boundary.Verifications.Enqueue(CreateVerification());
            return boundary;
        }

        internal LlmFitGate1Runner CreateRunner()
        {
            return new LlmFitGate1Runner(this, this, this, this, this, this, this);
        }

        public LlmFitCandidateManifest Load()
        {
            Calls.Add("load manifest");
            return CreateManifest();
        }

        public LlmFitCandidateVerification Verify(
            string candidateRoot,
            LlmFitCandidateManifest manifest)
        {
            _ = candidateRoot;
            _ = manifest;
            Calls.Add("verify package");
            VerifierCallCount++;
            return Verifications.Dequeue();
        }

        public async Task<LlmFitProcessResult> ExecuteAsync(
            LlmFitCommand command,
            TimeSpan timeout,
            Func<int, CancellationToken, Task>? observer,
            CancellationToken cancellationToken)
        {
            _ = timeout;
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            Calls.Add("observe and run " + string.Join(' ', command.Arguments));
            if (observer is not null && InvokeObserver)
            {
                await observer(1234, cancellationToken).ConfigureAwait(false);
            }

            return ProcessResults.Dequeue();
        }

        public LlmFitSocketObservationSession Create(string candidateImageName)
        {
            _ = candidateImageName;
            LlmFitProcessObservation observation = Observations.Dequeue();
            return new LlmFitSocketObservationSession(
                static (_, _) => Task.CompletedTask,
                _ => Task.FromResult(observation));
        }

        public IGateOutputSession AcquireFreshRun(string outputDirectory)
        {
            Assert.AreEqual(_outputDirectory, outputDirectory);
            return ProductionOutputStore is null
                ? new TestGateOutputSession(outputDirectory)
                : ProductionOutputStore.AcquireFreshRun(outputDirectory);
        }

        public async Task<GateRawOutput> WriteRawSystemJsonAsync(
            IGateOutputSession session,
            string rawJson,
            CancellationToken cancellationToken)
        {
            string outputDirectory = session.OutputDirectory;
            Assert.AreEqual(_outputDirectory, outputDirectory);
            Calls.Add("write ignored raw capture");
            if (ProductionOutputStore is not null)
            {
                return await ProductionOutputStore.WriteRawSystemJsonAsync(
                        session,
                        rawJson,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            string path = Path.Combine(outputDirectory, "llmfit-system.raw.json");
            await File.WriteAllTextAsync(path, rawJson, new UTF8Encoding(false), cancellationToken)
                .ConfigureAwait(false);
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawJson)))
                .ToLowerInvariant();
            return new GateRawOutput("llmfit-system.raw.json", hash);
        }

        public async Task<string> WriteAsync(
            LlmFitGate1Evidence evidence,
            string outputPath,
            CancellationToken cancellationToken)
        {
            Calls.Add("write sanitized evidence");
            BeforeEvidenceWrite?.Invoke();
            if (EvidenceWriterFails)
            {
                throw new IOException("C:\\private\\evidence-failure");
            }

            return await new LlmFitGate1EvidenceWriter()
                .WriteNewAsync(evidence, outputPath, cancellationToken)
                .ConfigureAwait(false);
        }

    }

    private sealed class TestGateOutputSession : IGateOutputSession
    {
        internal TestGateOutputSession(string outputDirectory)
        {
            OutputDirectory = outputDirectory;
        }

        public string OutputDirectory { get; }

        public void Dispose()
        {
        }

        public void ValidateBeforeEvidence(bool rawPublished)
        {
            _ = rawPublished;
        }

        public void ValidateCompleted(bool rawPublished)
        {
            _ = rawPublished;
        }
    }

    private sealed class RunnerScope : IDisposable
    {
        private const string Prefix = "GraniteEdgeAI-LlmFit-Runner-";

        internal RunnerScope()
        {
            Root = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
            CandidateRoot = Path.Combine(Root, "candidate");
            OutputDirectory = Path.Combine(Root, "output");
            Directory.CreateDirectory(CandidateRoot);
            Directory.CreateDirectory(OutputDirectory);
        }

        internal string Root { get; }

        internal string CandidateRoot { get; }

        internal string OutputDirectory { get; }

        internal SpikeOptions CreateOptions()
        {
            return SpikeOptions.Parse(
            [
                "--candidate-root",
                CandidateRoot,
                "--output",
                OutputDirectory,
                "--timeout-seconds",
                "5",
            ]);
        }

        public void Dispose()
        {
            string name = Path.GetFileName(Root);
            if (name.StartsWith(Prefix, StringComparison.Ordinal) &&
                Guid.TryParseExact(name[Prefix.Length..], "N", out _) &&
                Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
#pragma warning restore CA1707
