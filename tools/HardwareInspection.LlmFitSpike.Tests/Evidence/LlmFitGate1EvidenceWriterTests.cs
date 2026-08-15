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
    private const string HashD = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private const string HashE = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
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
        LlmFitGate1Evidence accepted = CreateAcceptedEvidence(
            "FunctionalPassWithPackagingConcern");
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
            accepted with { ReportedVersion = "llmfit 1.1.8" },
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
    public async Task WriteAsync_PrivacySafeBoundedIdentityShapes_AreAccepted()
    {
        using var directory = new OwnedTemporaryDirectory();
        LlmFitGate1Evidence baseline = CreateEvidence();
        LlmFitGate1Evidence[] validValues =
        [
            baseline with
            {
                CandidateId = "llmfit-v2.3.4-win-arm64",
                ExpectedVersion = "2.3.4",
                ReportedVersion = "llmfit 2.3.5-rc.1+build-meta.7",
                ReleaseCommit = "dddddddddddddddddddddddddddddddddddddddd",
                ExpectedArchiveSha256 = HashD,
                ObservedArchiveSha256 = HashC,
                ExpectedExecutableSha256 = HashE,
                ObservedExecutableSha256 = ArchiveHash,
                ExpectedPeMachine = "ARM64",
                ObservedPeMachine = "I386",
            },
            baseline with { ObservedPeMachine = "Unknown" },
            baseline with { ObservedPeMachine = "0x01AF" },
            baseline with { ReportedVersion = "llmfit 2.3.5+build-meta.7" },
            baseline with { ReportedVersion = null },
        ];

        for (int index = 0; index < validValues.Length; index++)
        {
            string outputPath = Path.Combine(directory.Path, $"valid-identity-{index}.json");
            _ = await new LlmFitGate1EvidenceWriter().WriteAsync(
                    validValues[index],
                    outputPath,
                    CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsTrue(File.Exists(outputPath));
        }
    }

    [TestMethod]
    public async Task WriteAsync_UnsafeOrUnboundedIdentityShapes_AreRejected()
    {
        using var directory = new OwnedTemporaryDirectory();
        LlmFitGate1Evidence baseline = CreateEvidence();
        LlmFitGate1Evidence[] invalidValues =
        [
            baseline with { SchemaVersion = "2.0" },
            baseline with { CandidateId = "arian-private-host" },
            baseline with { CandidateId = "llmfit-v1.1.9-win-x64/private" },
            baseline with { CandidateId = $"llmfit-v{new string('1', 100)}.1.1-win-x64" },
            baseline with { ExpectedVersion = "01.1.9" },
            baseline with { ExpectedVersion = "1.1" },
            baseline with { ReportedVersion = "Arian Secret Host" },
            baseline with { ReportedVersion = "llmfit 01.1.9" },
            baseline with { ReportedVersion = "llmfit 1.1.9\r\nprivate" },
            baseline with { ReleaseCommit = ReleaseCommit.ToUpperInvariant() },
            baseline with { ReleaseCommit = ReleaseCommit[..^1] },
            baseline with { ExpectedArchiveSha256 = ArchiveHash.ToUpperInvariant() },
            baseline with { ExpectedExecutableSha256 = ExecutableHash[..^1] },
            baseline with { ExpectedPeMachine = "Unknown" },
            baseline with { ObservedPeMachine = "SECRET" },
            baseline with { ObservedPeMachine = "0x01af" },
            baseline with { ObservedPeMachine = "0x12345" },
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
    public async Task WriteAsync_AcceptedDispositionsWithHardFailureDiagnostics_AreRejected()
    {
        using var directory = new OwnedTemporaryDirectory();
        string[] dispositions =
        [
            "FunctionalPassWithPackagingConcern",
            "AcceptedForFunctionalEvaluation",
        ];

        foreach (string disposition in dispositions)
        {
            foreach (string hardFailureCode in HardFailureDiagnosticCodes)
            {
                LlmFitGate1Evidence evidence = CreateAcceptedEvidence(disposition) with
                {
                    DiagnosticCodes =
                    [
                        .. CreateAcceptedEvidence(disposition).DiagnosticCodes,
                        hardFailureCode,
                    ],
                };
                string outputPath = Path.Combine(
                    directory.Path,
                    $"hard-failure-{disposition}-{hardFailureCode}.json");

                await Assert.ThrowsExactlyAsync<InvalidDataException>(
                        () => new LlmFitGate1EvidenceWriter().WriteAsync(
                            evidence,
                            outputPath,
                            CancellationToken.None))
                    .ConfigureAwait(false);
                Assert.IsFalse(File.Exists(outputPath), hardFailureCode);
            }
        }
    }

    [TestMethod]
    public async Task WriteAsync_BlockedAndRejectedDispositions_MayRecordHardFailures()
    {
        using var directory = new OwnedTemporaryDirectory();
        string[] dispositions = ["Blocked", "Rejected"];

        foreach (string disposition in dispositions)
        {
            LlmFitGate1Evidence evidence = CreateEvidence() with
            {
                Disposition = disposition,
                ReportedVersion = null,
                ObservedArchiveSha256 = null,
                ObservedExecutableSha256 = null,
                ObservedPeMachine = null,
                VersionExitCode = null,
                SystemExitCode = null,
                ProcessStartFailed = true,
                JsonValid = false,
                RequiredCpuRamPresent = false,
                RawSystemJsonFileName = null,
                RawSystemJsonSha256 = null,
                DiagnosticCodes = [LlmFitGate1DiagnosticCodes.ProcessStartFailed],
            };
            string outputPath = Path.Combine(directory.Path, $"{disposition}.json");

            _ = await new LlmFitGate1EvidenceWriter().WriteAsync(
                    evidence,
                    outputPath,
                    CancellationToken.None)
                .ConfigureAwait(false);

            Assert.IsTrue(File.Exists(outputPath));
        }
    }

    [TestMethod]
    public async Task WriteAsync_FunctionalPackagingConcern_RequiresInventoryAndSignatureFactConsistency()
    {
        using var directory = new OwnedTemporaryDirectory();
        LlmFitGate1Evidence unsigned = CreateAcceptedEvidence(
            "FunctionalPassWithPackagingConcern");
        LlmFitGate1Evidence signed = unsigned with
        {
            AuthenticodePresent = true,
            AuthenticodeStatus = "PresentUnverified",
            DiagnosticCodes =
            [
                LlmFitGate1DiagnosticCodes.DependencyLicenseInventoryPending,
                LlmFitGate1DiagnosticCodes.WindowsIntelNpuGap,
            ],
        };
        LlmFitGate1Evidence[] invalidValues =
        [
            unsigned with
            {
                DiagnosticCodes = [LlmFitGate1DiagnosticCodes.SignatureClaimMismatch],
            },
            unsigned with
            {
                DiagnosticCodes = [LlmFitGate1DiagnosticCodes.DependencyLicenseInventoryPending],
            },
            unsigned with
            {
                DiagnosticCodes =
                [
                    LlmFitGate1DiagnosticCodes.DependencyLicenseInventoryPending,
                    LlmFitGate1DiagnosticCodes.SignatureStatusChanged,
                ],
            },
            signed with
            {
                DiagnosticCodes =
                [
                    LlmFitGate1DiagnosticCodes.DependencyLicenseInventoryPending,
                    LlmFitGate1DiagnosticCodes.SignatureClaimMismatch,
                ],
            },
        ];

        for (int index = 0; index < invalidValues.Length; index++)
        {
            string outputPath = Path.Combine(directory.Path, $"packaging-invalid-{index}.json");
            await Assert.ThrowsExactlyAsync<InvalidDataException>(
                    () => new LlmFitGate1EvidenceWriter().WriteAsync(
                        invalidValues[index],
                        outputPath,
                        CancellationToken.None))
                .ConfigureAwait(false);
            Assert.IsFalse(File.Exists(outputPath));
        }

        string signedOutputPath = Path.Combine(directory.Path, "packaging-signed.json");
        _ = await new LlmFitGate1EvidenceWriter().WriteAsync(
                signed,
                signedOutputPath,
                CancellationToken.None)
            .ConfigureAwait(false);
        Assert.IsTrue(File.Exists(signedOutputPath));

        string unsignedChangedOutputPath = Path.Combine(
            directory.Path,
            "packaging-unsigned-status-changed.json");
        _ = await new LlmFitGate1EvidenceWriter().WriteAsync(
                unsigned with
                {
                    DiagnosticCodes =
                    [
                        .. unsigned.DiagnosticCodes,
                        LlmFitGate1DiagnosticCodes.SignatureStatusChanged,
                    ],
                },
                unsignedChangedOutputPath,
                CancellationToken.None)
            .ConfigureAwait(false);
        Assert.IsTrue(File.Exists(unsignedChangedOutputPath));
    }

    [TestMethod]
    public async Task WriteAsync_AcceptedForEvaluation_RequiresSignatureFactAndResolvedInventory()
    {
        using var directory = new OwnedTemporaryDirectory();
        LlmFitGate1Evidence unsigned = CreateAcceptedEvidence(
            "AcceptedForFunctionalEvaluation");
        LlmFitGate1Evidence signed = unsigned with
        {
            AuthenticodePresent = true,
            AuthenticodeStatus = "PresentUnverified",
            DiagnosticCodes = [LlmFitGate1DiagnosticCodes.WindowsIntelNpuGap],
        };
        LlmFitGate1Evidence[] invalidValues =
        [
            unsigned with
            {
                DiagnosticCodes = [LlmFitGate1DiagnosticCodes.WindowsIntelNpuGap],
            },
            signed with
            {
                DiagnosticCodes =
                [
                    LlmFitGate1DiagnosticCodes.SignatureClaimMismatch,
                    LlmFitGate1DiagnosticCodes.WindowsIntelNpuGap,
                ],
            },
            unsigned with
            {
                DiagnosticCodes =
                [
                    LlmFitGate1DiagnosticCodes.DependencyLicenseInventoryPending,
                    LlmFitGate1DiagnosticCodes.SignatureClaimMismatch,
                    LlmFitGate1DiagnosticCodes.WindowsIntelNpuGap,
                ],
            },
        ];

        for (int index = 0; index < invalidValues.Length; index++)
        {
            string outputPath = Path.Combine(directory.Path, $"accepted-invalid-{index}.json");
            await Assert.ThrowsExactlyAsync<InvalidDataException>(
                    () => new LlmFitGate1EvidenceWriter().WriteAsync(
                        invalidValues[index],
                        outputPath,
                        CancellationToken.None))
                .ConfigureAwait(false);
            Assert.IsFalse(File.Exists(outputPath));
        }

        LlmFitGate1Evidence[] validValues =
        [
            unsigned,
            unsigned with
            {
                DiagnosticCodes =
                [
                    .. unsigned.DiagnosticCodes,
                    LlmFitGate1DiagnosticCodes.SignatureStatusChanged,
                ],
            },
            signed,
            signed with
            {
                DiagnosticCodes =
                [
                    .. signed.DiagnosticCodes,
                    LlmFitGate1DiagnosticCodes.SignatureStatusChanged,
                ],
            },
        ];
        for (int index = 0; index < validValues.Length; index++)
        {
            string outputPath = Path.Combine(directory.Path, $"accepted-valid-{index}.json");
            _ = await new LlmFitGate1EvidenceWriter().WriteAsync(
                    validValues[index],
                    outputPath,
                    CancellationToken.None)
                .ConfigureAwait(false);
            Assert.IsTrue(File.Exists(outputPath));
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

    [TestMethod]
    public async Task WriteAsync_DeletionFailure_DisposesHandlePreservesDestinationAndPropagatesCleanupError()
    {
        using var directory = new OwnedTemporaryDirectory();
        string outputPath = Path.Combine(directory.Path, "evidence.json");
        const string Original = "{\"original\":true}";
        await File.WriteAllTextAsync(outputPath, Original).ConfigureAwait(false);
        string? temporaryPath = null;
        var cleanupError = new IOException("controlled owned-file cleanup failure");
        var writer = new LlmFitGate1EvidenceWriter(
            path => temporaryPath = path,
            _ => throw cleanupError);
        await using var destinationLock = new FileStream(
            outputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        IOException thrown = await Assert.ThrowsExactlyAsync<IOException>(
                () => writer.WriteAsync(CreateEvidence(), outputPath, CancellationToken.None))
            .ConfigureAwait(false);

        Assert.AreSame(cleanupError, thrown);
        destinationLock.Position = 0;
        using var reader = new StreamReader(destinationLock, leaveOpen: true);
        Assert.AreEqual(Original, await reader.ReadToEndAsync().ConfigureAwait(false));
        Assert.IsNotNull(temporaryPath);
        Assert.IsTrue(File.Exists(temporaryPath));
        await using (var exclusive = new FileStream(
            temporaryPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None))
        {
            Assert.IsTrue(exclusive.CanWrite);
        }

        File.Delete(temporaryPath);
        Assert.IsFalse(File.Exists(temporaryPath));
    }

    private static LlmFitGate1Evidence CreateAcceptedEvidence(string disposition)
    {
        LlmFitGate1Evidence evidence = CreateEvidence() with { Disposition = disposition };
        return disposition == "FunctionalPassWithPackagingConcern"
            ? evidence with
            {
                DiagnosticCodes =
                [
                    LlmFitGate1DiagnosticCodes.DependencyLicenseInventoryPending,
                    LlmFitGate1DiagnosticCodes.SignatureClaimMismatch,
                    LlmFitGate1DiagnosticCodes.WindowsIntelNpuGap,
                ],
            }
            : evidence with
            {
                DiagnosticCodes =
                [
                    LlmFitGate1DiagnosticCodes.SignatureClaimMismatch,
                    LlmFitGate1DiagnosticCodes.WindowsIntelNpuGap,
                ],
            };
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

    private static readonly string[] HardFailureDiagnosticCodes =
    [
        LlmFitGate1DiagnosticCodes.ManifestInvalid,
        LlmFitGate1DiagnosticCodes.PackageMissing,
        LlmFitGate1DiagnosticCodes.UnexpectedPackageMember,
        LlmFitGate1DiagnosticCodes.PackageChangedDuringRun,
        LlmFitGate1DiagnosticCodes.PathEscape,
        LlmFitGate1DiagnosticCodes.ReparsePoint,
        LlmFitGate1DiagnosticCodes.ArchiveLengthMismatch,
        LlmFitGate1DiagnosticCodes.ArchiveHashMismatch,
        LlmFitGate1DiagnosticCodes.ExecutableHashMismatch,
        LlmFitGate1DiagnosticCodes.PeInvalid,
        LlmFitGate1DiagnosticCodes.PeArchitectureMismatch,
        LlmFitGate1DiagnosticCodes.ProcessStartFailed,
        LlmFitGate1DiagnosticCodes.VersionMismatch,
        LlmFitGate1DiagnosticCodes.ProcessTimedOut,
        LlmFitGate1DiagnosticCodes.ProcessCancelled,
        LlmFitGate1DiagnosticCodes.ProcessExitNonzero,
        LlmFitGate1DiagnosticCodes.StdoutTruncated,
        LlmFitGate1DiagnosticCodes.StderrTruncated,
        LlmFitGate1DiagnosticCodes.SocketObservationFailed,
        LlmFitGate1DiagnosticCodes.CandidateSocketObserved,
        LlmFitGate1DiagnosticCodes.DashboardPortObserved,
        LlmFitGate1DiagnosticCodes.ResidualProcess,
        LlmFitGate1DiagnosticCodes.JsonInvalid,
        LlmFitGate1DiagnosticCodes.CpuRamMissing,
        LlmFitGate1DiagnosticCodes.GpuInconsistent,
        LlmFitGate1DiagnosticCodes.WrongTarget,
        LlmFitGate1DiagnosticCodes.WindowsComparisonFailed,
        LlmFitGate1DiagnosticCodes.OfflinePreconditionFailed,
        LlmFitGate1DiagnosticCodes.PrivacyValidationFailed,
        LlmFitGate1DiagnosticCodes.RequiredTestFailure,
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
