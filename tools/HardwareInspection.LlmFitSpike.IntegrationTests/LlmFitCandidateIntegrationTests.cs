using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HardwareInspection.LlmFitSpike.Candidate;
using HardwareInspection.LlmFitSpike.Evidence;
using HardwareInspection.LlmFitSpike.Inspection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;

namespace HardwareInspection.LlmFitSpike.IntegrationTests;

[TestClass]
[DoNotParallelize]
#pragma warning disable CA1707 // Test names intentionally encode the required operational behavior.
public sealed partial class LlmFitCandidateIntegrationTests
{
    private const string ArchiveHash =
        "a030269d7cc8a5bf40383f526a481655d698ec71dd792a25b06510cef9f8b738";
    private const string CandidateId = "llmfit-v1.1.9-win-x64";
    private const string CandidateRootVariable = "GRANITE_LLMFIT_CANDIDATE_ROOT";
    private const string EvidenceFileName = "llmfit-gate1.evidence.json";
    private const string ExecutableHash =
        "db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19";
    private const string GateOutputVariable = "GRANITE_LLMFIT_GATE1_OUTPUT";
    private const string ManifestFileName = "llmfit-v1.1.9-win-x64.json";
    private const string OfflineOutputVariable = "GRANITE_LLMFIT_OFFLINE_OUTPUT";
    private const string RawFileName = "llmfit-system.raw.json";
    private const string ReferenceFileName = "windows-reference.json";
    private const string ReleaseCommit = "a02e13f1013ed69889ff44426a651bf7c68c292e";
    private const string ReportedVersion = "llmfit 1.1.9";
    private const string Version = "1.1.9";
    private const string WindowsReferenceVariable = "GRANITE_LLMFIT_WINDOWS_REFERENCE";
    private const int MaximumEvidenceBytes = 128 * 1024;
    private const int MaximumReferenceBytes = 128 * 1024;
    private const int MaximumRawBytes = 2 * 1024 * 1024;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileReadAttributes = 0x00000080;
    private const uint OpenExisting = 3;
    private const uint ShareDelete = 0x00000004;
    private const uint ShareRead = 0x00000001;
    private const uint ShareWrite = 0x00000002;
    private const uint ToolhelpSnapshotProcess = 0x00000002;
    private static readonly string[] ApprovedSystemArguments =
        ["--no-dashboard", "--json", "system"];
    private static readonly string[] ApprovedVersionArguments = ["--version"];
    private static readonly string[] RequiredPackageFiles =
        ["llmfit.exe", "LICENSE", "README.md"];
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private static readonly string[] EvidenceProperties =
    [
        "schemaVersion", "disposition", "candidateId", "expectedVersion",
        "reportedVersion", "releaseCommit", "expectedArchiveSha256",
        "observedArchiveSha256", "expectedExecutableSha256",
        "observedExecutableSha256", "expectedPeMachine", "observedPeMachine",
        "authenticodePresent", "authenticodeStatus", "versionInvocationArguments",
        "systemInvocationArguments", "gateStartedAtUtc", "gateCompletedAtUtc",
        "durationMilliseconds", "versionExitCode", "systemExitCode",
        "processStartFailed", "socketObservationFailed", "timedOut", "cancelled",
        "standardOutputTruncated", "standardErrorTruncated", "jsonValid",
        "requiredCpuRamPresent", "cpuLogicalProcessorCount", "totalRamGiB",
        "availableRamGiB", "gpuReported", "reportedGpuCount", "intelGpuReported",
        "dedicatedSharedMemorySemanticsEstablished", "intelNpuDetectionState",
        "versionCandidateSocketObserved", "versionDashboardPortObserved",
        "systemCandidateSocketObserved", "systemDashboardPortObserved",
        "versionCandidateProcessRemainedAfterExit",
        "systemCandidateProcessRemainedAfterExit", "rawSystemJsonFileName",
        "rawSystemJsonSha256", "diagnosticCodes",
    ];
    private static readonly string[] ReferenceProperties =
    [
        "schemaVersion", "captureStatus", "capturedBeforeUtc", "gateStartedAtUtc",
        "gateCompletedAtUtc", "capturedAfterUtc", "captureIntervalMilliseconds",
        "captureWithinThirtySeconds", "windowsX64", "windowsCpuNames",
        "windowsLogicalProcessorCount", "windowsTotalPhysicalMemoryBytes",
        "windowsFreePhysicalMemoryBeforeKiB", "windowsFreePhysicalMemoryAfterKiB",
        "windowsGpuNames", "windowsIntelCpuObserved", "windowsIntelGpuObserved",
        "cpuIdentityMatched", "llmFitLogicalProcessorCount",
        "logicalProcessorCountMatched", "totalRamDeltaGiB", "totalRamToleranceGiB",
        "totalRamWithinTolerance", "availableRamDeltaGiB",
        "availableRamToleranceGiB", "availableRamWithinTolerance", "jsonValid",
        "requiredCpuRamPresent", "intelGpuIdentityMatched",
        "intelGpuComparisonStatus", "dedicatedSharedMemorySemanticsEstablished",
        "intelNpuDetectionState", "gateExitCode", "gateDisposition",
        "diagnosticCodes", "versionCandidateSocketObserved",
        "versionDashboardPortObserved", "systemCandidateSocketObserved",
        "systemDashboardPortObserved", "versionCandidateProcessRemainedAfterExit",
        "systemCandidateProcessRemainedAfterExit", "authenticodeObservationSha256",
    ];

    [TestMethod]
    [TestCategory("TrustedWindowsIntel")]
    public void TrustedCandidate_IdentityVersionAndCpuRamSchemaPass()
    {
        TrustedCapture capture = LoadTrustedCapture();
        AssertTrustedTarget(capture.Reference);

        LlmFitCandidateVerification verification = VerifyCandidate(
            capture.CandidateRoot,
            capture.Manifest);
        Assert.IsTrue(
            verification.IntegrityPassed,
            "HI-GATE1-REQUIRED-TEST-FAILURE: candidate integrity did not pass.");
        Assert.IsTrue(
            verification.MayExecuteForGate1,
            "HI-GATE1-REQUIRED-TEST-FAILURE: candidate is not an AMD64 Gate 1 executable.");
        Assert.IsTrue(
            string.Equals(ArchiveHash, verification.ArchiveSha256, StringComparison.Ordinal),
            "Candidate archive identity differs.");
        Assert.IsTrue(
            string.Equals(ExecutableHash, verification.ExecutableSha256, StringComparison.Ordinal),
            "Candidate executable identity differs.");
        Assert.IsTrue(
            string.Equals("AMD64", verification.PeMachine, StringComparison.Ordinal),
            "Candidate architecture differs.");

        LlmFitGate1Evidence evidence = capture.Gate.Evidence;
        AssertEvidenceMatchesVerification(evidence, verification);
        Assert.IsTrue(
            string.Equals("1.0", evidence.SchemaVersion, StringComparison.Ordinal),
            "Evidence schema differs.");
        Assert.IsTrue(
            string.Equals(
                "FunctionalPassWithPackagingConcern",
                evidence.Disposition,
                StringComparison.Ordinal),
            "Evidence disposition differs.");
        Assert.IsTrue(
            string.Equals(CandidateId, evidence.CandidateId, StringComparison.Ordinal),
            "Evidence candidate identity differs.");
        Assert.IsTrue(
            string.Equals(Version, evidence.ExpectedVersion, StringComparison.Ordinal),
            "Evidence expected version differs.");
        Assert.IsTrue(
            string.Equals(ReportedVersion, evidence.ReportedVersion, StringComparison.Ordinal),
            "Evidence reported version differs.");
        Assert.IsTrue(
            string.Equals(ReleaseCommit, evidence.ReleaseCommit, StringComparison.Ordinal),
            "Evidence release identity differs.");
        Assert.IsTrue(
            string.Equals(ArchiveHash, evidence.ExpectedArchiveSha256, StringComparison.Ordinal),
            "Evidence expected archive identity differs.");
        Assert.IsTrue(
            string.Equals(ArchiveHash, evidence.ObservedArchiveSha256, StringComparison.Ordinal),
            "Evidence observed archive identity differs.");
        Assert.IsTrue(
            string.Equals(ExecutableHash, evidence.ExpectedExecutableSha256, StringComparison.Ordinal),
            "Evidence expected executable identity differs.");
        Assert.IsTrue(
            string.Equals(ExecutableHash, evidence.ObservedExecutableSha256, StringComparison.Ordinal),
            "Evidence observed executable identity differs.");
        Assert.IsTrue(
            string.Equals("AMD64", evidence.ExpectedPeMachine, StringComparison.Ordinal),
            "Evidence expected architecture differs.");
        Assert.IsTrue(
            string.Equals("AMD64", evidence.ObservedPeMachine, StringComparison.Ordinal),
            "Evidence observed architecture differs.");
        Assert.IsTrue(
            evidence.VersionInvocationArguments.SequenceEqual(
                ApprovedVersionArguments,
                StringComparer.Ordinal),
            "Evidence version arguments differ.");
        Assert.IsTrue(
            evidence.SystemInvocationArguments.SequenceEqual(
                ApprovedSystemArguments,
                StringComparer.Ordinal),
            "Evidence system arguments differ.");
        Assert.AreEqual(0, evidence.VersionExitCode);
        Assert.AreEqual(0, evidence.SystemExitCode);
        Assert.IsFalse(evidence.ProcessStartFailed);
        Assert.IsFalse(evidence.SocketObservationFailed);
        Assert.IsFalse(evidence.TimedOut);
        Assert.IsFalse(evidence.Cancelled);
        Assert.IsFalse(evidence.StandardOutputTruncated);
        Assert.IsFalse(evidence.StandardErrorTruncated);
        Assert.IsTrue(evidence.JsonValid);
        Assert.IsTrue(evidence.RequiredCpuRamPresent);
        Assert.IsNotNull(evidence.CpuLogicalProcessorCount);
        Assert.IsNotNull(evidence.TotalRamGiB);
        Assert.IsNotNull(evidence.AvailableRamGiB);
        Assert.IsTrue(
            string.Equals(RawFileName, evidence.RawSystemJsonFileName, StringComparison.Ordinal),
            "Evidence raw filename differs.");
        Assert.IsTrue(
            string.Equals(
                capture.Gate.RawSha256,
                evidence.RawSystemJsonSha256,
                StringComparison.Ordinal),
            "Evidence raw identity differs.");
        Assert.AreEqual(0, capture.Reference.GateExitCode);
        Assert.IsTrue(
            string.Equals(
                evidence.Disposition,
                capture.Reference.GateDisposition,
                StringComparison.Ordinal),
            "Reference disposition differs from evidence.");
        Assert.IsTrue(capture.Reference.JsonValid);
        Assert.IsTrue(capture.Reference.RequiredCpuRamPresent);
    }

    [TestMethod]
    [TestCategory("TrustedWindowsIntel")]
    public void TrustedCandidate_CpuAndRamAgreeWithNearSimultaneousWindowsReference()
    {
        TrustedCapture capture = LoadTrustedCapture();
        WindowsReference reference = capture.Reference;
        LlmFitSystemAssessment assessment = capture.Gate.Assessment;
        AssertTrustedTarget(reference);

        bool cpuIdentityMatched = reference.WindowsCpuNames
            .Select(NormalizeHardwareIdentity)
            .Distinct(StringComparer.Ordinal)
            .Contains(NormalizeHardwareIdentity(assessment.CpuName!), StringComparer.Ordinal);
        Assert.AreEqual(cpuIdentityMatched, reference.CpuIdentityMatched);
        Assert.IsTrue(
            reference.CpuIdentityMatched,
            "HI-GATE1-WINDOWS-COMPARISON-FAILED: normalized CPU identities differ.");
        Assert.AreEqual(assessment.CpuLogicalProcessorCount, reference.LlmFitLogicalProcessorCount);
        Assert.AreEqual(
            reference.WindowsLogicalProcessorCount == reference.LlmFitLogicalProcessorCount,
            reference.LogicalProcessorCountMatched);
        Assert.IsTrue(
            reference.LogicalProcessorCountMatched,
            "HI-GATE1-WINDOWS-COMPARISON-FAILED: logical processor counts differ.");

        double windowsTotalGiB = reference.WindowsTotalPhysicalMemoryBytes / 1_073_741_824d;
        double expectedTotalDelta = Math.Abs(assessment.TotalRamGiB!.Value - windowsTotalGiB);
        Assert.AreEqual(expectedTotalDelta, reference.TotalRamDeltaGiB, 0.000_001d);
        Assert.AreEqual(1d, reference.TotalRamToleranceGiB, 0.000_001d);
        Assert.AreEqual(expectedTotalDelta <= 1d, reference.TotalRamWithinTolerance);
        Assert.IsTrue(
            reference.TotalRamWithinTolerance,
            "HI-GATE1-WINDOWS-COMPARISON-FAILED: total RAM exceeded the 1 GiB tolerance.");

        double midpointAvailableGiB =
            ((reference.WindowsFreePhysicalMemoryBeforeKiB +
                reference.WindowsFreePhysicalMemoryAfterKiB) / 2d) / 1_048_576d;
        double expectedAvailableDelta = Math.Abs(
            assessment.AvailableRamGiB!.Value - midpointAvailableGiB);
        double expectedAvailableTolerance = Math.Max(2d, windowsTotalGiB * 0.1d);
        Assert.AreEqual(expectedAvailableDelta, reference.AvailableRamDeltaGiB, 0.000_001d);
        Assert.AreEqual(
            expectedAvailableTolerance,
            reference.AvailableRamToleranceGiB,
            0.000_001d);
        Assert.AreEqual(
            expectedAvailableDelta <= expectedAvailableTolerance,
            reference.AvailableRamWithinTolerance);
        Assert.IsTrue(
            reference.AvailableRamWithinTolerance,
            "HI-GATE1-WINDOWS-COMPARISON-FAILED: available RAM exceeded its tolerance.");

        Assert.IsTrue(reference.CapturedBeforeUtc <= reference.GateStartedAtUtc);
        Assert.IsTrue(reference.GateStartedAtUtc <= reference.GateCompletedAtUtc);
        Assert.IsTrue(reference.GateCompletedAtUtc <= reference.CapturedAfterUtc);
        long expectedInterval = GetCaptureIntervalMilliseconds(
            reference.CapturedBeforeUtc,
            reference.CapturedAfterUtc);
        bool expectedWithinThirtySeconds = IsCaptureWithinThirtySeconds(
            reference.CapturedBeforeUtc,
            reference.CapturedAfterUtc);
        Assert.AreEqual(expectedInterval, reference.CaptureIntervalMilliseconds);
        Assert.AreEqual(expectedWithinThirtySeconds, reference.CaptureWithinThirtySeconds);
        Assert.IsTrue(
            reference.CaptureWithinThirtySeconds,
            "HI-GATE1-WINDOWS-COMPARISON-FAILED: reference and candidate capture exceeded 30 seconds.");

        bool gpuIdentityMatched = GetNormalizedIntelNames(reference.WindowsGpuNames)
            .Intersect(
                GetNormalizedIntelNames(capture.Gate.RawFacts.GpuNames),
                StringComparer.Ordinal)
            .Any();
        Assert.AreEqual(gpuIdentityMatched, reference.IntelGpuIdentityMatched);
        Assert.IsTrue(
            string.Equals(
                gpuIdentityMatched ? "Matched" : "FieldLevelGap",
                reference.IntelGpuComparisonStatus,
                StringComparison.Ordinal),
            "Reference Intel GPU comparison status differs.");
        Assert.IsFalse(reference.DedicatedSharedMemorySemanticsEstablished);
        Assert.IsFalse(capture.Gate.Evidence.DedicatedSharedMemorySemanticsEstablished);
        Assert.IsTrue(
            string.Equals(
                "DetectionUnavailable",
                reference.IntelNpuDetectionState,
                StringComparison.Ordinal),
            "Reference NPU status differs.");
        Assert.IsTrue(
            string.Equals(
                "DetectionUnavailable",
                capture.Gate.Evidence.IntelNpuDetectionState,
                StringComparison.Ordinal),
            "Evidence NPU status differs.");
    }

    [TestMethod]
    [TestCategory("TrustedWindowsIntel")]
    public void TrustedCandidate_LeavesNoDashboardListenerOrProcess()
    {
        TrustedCapture capture = LoadTrustedCapture();
        AssertTrustedTarget(capture.Reference);
        AssertNoSocketOrResidualEvidence(capture.Gate.Evidence);
        AssertReferenceMatchesSocketEvidence(capture.Reference, capture.Gate.Evidence);
        Assert.IsFalse(
            IsCandidateProcessPresent(capture.CandidateRoot, capture.Manifest),
            "HI-LLMFIT-RESIDUAL-PROCESS: a process from the pinned candidate root remains.");
    }

    [TestMethod]
    [TestCategory("TrustedOffline")]
    public async Task OfflineCandidate_ProducesCpuRamWithoutAnyListenerOrResidualProcess()
    {
        Dictionary<string, string> environment = RequirePrerequisites(
            "TrustedOffline",
            CandidateRootVariable,
            OfflineOutputVariable);

        bool networkAvailable;
        bool operationalNonLoopbackExists;
        try
        {
            networkAvailable = NetworkInterface.GetIsNetworkAvailable();
            operationalNonLoopbackExists = NetworkInterface.GetAllNetworkInterfaces().Any(
                adapter =>
                    adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    adapter.OperationalStatus == OperationalStatus.Up);
        }
        catch (NetworkInformationException)
        {
            Assert.Fail(
                "HI-GATE1-OFFLINE-PRECONDITION-FAILED: both network preconditions could not be evaluated.");
            throw;
        }

        string repositoryRoot = FindRepositoryRoot();
        string offlineOutput;
        try
        {
            offlineOutput = ResolveFreshOperationalOutput(
                environment[OfflineOutputVariable],
                repositoryRoot);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException or
            UnauthorizedAccessException)
        {
            Assert.Fail(
                "HI-GATE1-OFFLINE-PRECONDITION-FAILED: the configured offline output is invalid.");
            throw;
        }

        if (networkAvailable || operationalNonLoopbackExists)
        {
            try
            {
                WriteOfflineBlockedEvidence(offlineOutput);
            }
            catch (Exception exception) when (
                exception is ArgumentException or IOException or NotSupportedException or
                UnauthorizedAccessException)
            {
                Assert.Fail(
                    "HI-GATE1-OFFLINE-PRECONDITION-FAILED: blocked evidence could not be published safely.");
            }

            Assert.Fail(
                "HI-GATE1-OFFLINE-PRECONDITION-FAILED: disable all non-loopback network access before this gate.");
        }

        try
        {
            string candidateRoot = ResolveExistingOrdinaryDirectory(
                environment[CandidateRootVariable],
                "HI-GATE1-REQUIRED-TEST-FAILURE: the configured candidate root is invalid.");
            LlmFitCandidateManifest manifest = LoadPinnedManifest();
            LlmFitCandidateVerification verification = VerifyCandidate(candidateRoot, manifest);
            Assert.IsTrue(
                verification.MayExecuteForGate1,
                "HI-GATE1-REQUIRED-TEST-FAILURE: offline candidate integrity or architecture failed.");
            Assert.IsFalse(
                IsCandidateProcessPresent(candidateRoot, manifest),
                "HI-LLMFIT-RESIDUAL-PROCESS: the pinned candidate was already running before the offline gate.");

            int exitCode = await RunFixedCliAsync(
                    repositoryRoot,
                    candidateRoot,
                    offlineOutput,
                    manifest)
                .ConfigureAwait(false);
            Assert.AreEqual(
                0,
                exitCode,
                "HI-GATE1-REQUIRED-TEST-FAILURE: the fixed offline Gate 1 CLI failed.");

            GateCapture gate = LoadGateCapture(
                offlineOutput,
                expectedFileNames: [EvidenceFileName, RawFileName]);
            LlmFitCandidateVerification verificationAfterRun = VerifyCandidate(
                candidateRoot,
                manifest);
            AssertEvidenceMatchesVerification(gate.Evidence, verificationAfterRun);
            Assert.IsTrue(
                string.Equals(
                    "FunctionalPassWithPackagingConcern",
                    gate.Evidence.Disposition,
                    StringComparison.Ordinal),
                "Offline evidence disposition differs.");
            Assert.IsTrue(gate.Evidence.JsonValid);
            Assert.IsTrue(gate.Evidence.RequiredCpuRamPresent);
            Assert.IsNotNull(gate.Evidence.CpuLogicalProcessorCount);
            Assert.IsNotNull(gate.Evidence.TotalRamGiB);
            Assert.IsNotNull(gate.Evidence.AvailableRamGiB);
            AssertNoSocketOrResidualEvidence(gate.Evidence);
            Assert.IsFalse(
                IsCandidateProcessPresent(candidateRoot, manifest),
                "HI-LLMFIT-RESIDUAL-PROCESS: a process from the pinned candidate root remains.");
        }
        catch (AssertFailedException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or JsonException or
            NotSupportedException or UnauthorizedAccessException or TimeoutException or
            CryptographicException or Win32Exception or InvalidOperationException)
        {
            Assert.Fail(
                "HI-GATE1-REQUIRED-TEST-FAILURE: configured offline execution or evidence is invalid.");
            throw;
        }
    }

    [TestMethod]
    [TestCategory("Task8Deterministic")]
    public void ArtifactStringShape_RejectsPathsAndFreeTextWithGenericDiagnostics()
    {
        const string Tampered = @"X:\tampered\private-device-name.txt";
        ValidateEvidenceStringShapes(
            "FunctionalPassWithPackagingConcern",
            false,
            "NotSigned");
        ValidateEvidenceStringShapes(
            "FunctionalPassWithPackagingConcern",
            true,
            "PresentUnverified");
        ValidateHardwareNames(
        [
            "Intel(R) Core(TM) i7-1260P @ 2.10GHz",
            "Intel(R) Iris(R) Xe Graphics",
        ]);
        ValidateRawHardwareNames(
        [
            "AMD Ryzen 7 7840U w/ Radeon 780M Graphics",
            "Adapter: [field-level identity]",
        ]);

        InvalidDataException dispositionFailure = Assert.ThrowsExactly<InvalidDataException>(
            () => ValidateEvidenceStringShapes(Tampered, false, "NotSigned"));
        Assert.IsTrue(
            string.Equals(
                dispositionFailure.Message,
                "Evidence string fields failed privacy-safe validation.",
                StringComparison.Ordinal),
            "Artifact string validation did not return its fixed diagnostic.");

        InvalidDataException statusFailure = Assert.ThrowsExactly<InvalidDataException>(
            () => ValidateEvidenceStringShapes(
                "FunctionalPassWithPackagingConcern",
                false,
                Tampered));
        Assert.IsTrue(
            string.Equals(
                statusFailure.Message,
                "Evidence string fields failed privacy-safe validation.",
                StringComparison.Ordinal),
            "Authenticode validation did not return its fixed diagnostic.");

        InvalidDataException hardwareNameFailure = Assert.ThrowsExactly<InvalidDataException>(
            () => ValidateHardwareNames([Tampered]));
        Assert.IsTrue(
            string.Equals(
                hardwareNameFailure.Message,
                "A Windows hardware name has an unsafe shape.",
                StringComparison.Ordinal),
            "Hardware-name validation did not return its fixed diagnostic.");

        InvalidDataException rawHardwareNameFailure = Assert.ThrowsExactly<InvalidDataException>(
            () => ValidateRawHardwareNames([Tampered]));
        Assert.IsTrue(
            string.Equals(
                rawHardwareNameFailure.Message,
                "A raw hardware name has an unsafe shape.",
                StringComparison.Ordinal),
            "Raw hardware-name validation did not return its fixed diagnostic.");
    }

    [TestMethod]
    [TestCategory("Task8Deterministic")]
    public void CaptureInterval_ThirtySecondsPlusOneTickIsOutsideBoundary()
    {
        DateTimeOffset before = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset exactlyThirtySeconds = before.AddSeconds(30);
        DateTimeOffset oneTickLate = exactlyThirtySeconds.AddTicks(1);

        Assert.IsTrue(IsCaptureWithinThirtySeconds(before, exactlyThirtySeconds));
        Assert.IsFalse(IsCaptureWithinThirtySeconds(before, oneTickLate));
        Assert.AreEqual(30_000, GetCaptureIntervalMilliseconds(before, oneTickLate));
    }

    [TestMethod]
    [TestCategory("Task8Deterministic")]
    public void StableFileIdentity_HardLinkAliasMatchesPhysicalFileAndIsRemoved()
    {
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "granite-llmfit-identity-" + Guid.NewGuid().ToString("N"));
        string physicalFile = Path.Combine(temporaryDirectory, "identity-probe.bin");
        string aliasFile = Path.Combine(temporaryDirectory, "identity-alias.bin");
        bool aliasCreated = false;
        bool identityMatched = false;
        bool cleanupSucceeded = false;
        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            File.WriteAllBytes(physicalFile, [0x47]);
            aliasCreated = CreateHardLink(aliasFile, physicalFile, 0);
            if (aliasCreated)
            {
                identityMatched = ResolveStableRegularFileIdentity(physicalFile) ==
                    ResolveStableRegularFileIdentity(aliasFile);
            }
        }
        finally
        {
            if (File.Exists(aliasFile))
            {
                File.Delete(aliasFile);
            }

            if (File.Exists(physicalFile))
            {
                File.Delete(physicalFile);
            }

            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: false);
            }

            cleanupSucceeded = !File.Exists(aliasFile) && !File.Exists(physicalFile) &&
                !Directory.Exists(temporaryDirectory);
        }

        Assert.IsTrue(aliasCreated, "The deterministic hard-link alias could not be created.");
        Assert.IsTrue(identityMatched, "Stable file identity did not recognize a hard-link alias.");
        Assert.IsTrue(cleanupSucceeded, "The deterministic hard-link alias was not removed exactly.");
    }

    private static TrustedCapture LoadTrustedCapture()
    {
        Dictionary<string, string> environment = RequirePrerequisites(
            "TrustedWindowsIntel",
            CandidateRootVariable,
            WindowsReferenceVariable,
            GateOutputVariable);
        try
        {
            string candidateRoot = ResolveExistingOrdinaryDirectory(
                environment[CandidateRootVariable],
                "The configured candidate root is invalid.");
            string output = ResolveExistingOperationalOutput(
                environment[GateOutputVariable],
                FindRepositoryRoot());
            string expectedReference = Path.GetFullPath(Path.Combine(output, ReferenceFileName));
            string configuredReference = Path.GetFullPath(environment[WindowsReferenceVariable]);
            Require(
                string.Equals(expectedReference, configuredReference, StringComparison.OrdinalIgnoreCase),
                "The Windows reference must be the fixed file inside the gate output.");
            EnsureExactOutputInventory(
                output,
                [EvidenceFileName, RawFileName, ReferenceFileName]);

            LlmFitCandidateManifest manifest = LoadPinnedManifest();
            GateCapture gate = LoadGateCapture(
                output,
                expectedFileNames: [EvidenceFileName, RawFileName, ReferenceFileName]);
            WindowsReference reference = ReadStrictJson<WindowsReference>(
                configuredReference,
                MaximumReferenceBytes,
                ReferenceProperties);
            ValidateReferenceShape(reference);
            Require(reference.GateStartedAtUtc == gate.Evidence.GateStartedAtUtc, "Gate start differs.");
            Require(reference.GateCompletedAtUtc == gate.Evidence.GateCompletedAtUtc, "Gate end differs.");
            Require(reference.JsonValid == gate.Evidence.JsonValid, "JSON result differs.");
            Require(
                reference.RequiredCpuRamPresent == gate.Evidence.RequiredCpuRamPresent,
                "CPU/RAM result differs.");
            Require(
                reference.DiagnosticCodes.SequenceEqual(
                    gate.Evidence.DiagnosticCodes,
                    StringComparer.Ordinal),
                "Diagnostics differ.");

            string observationPath = Path.Combine(candidateRoot, "authenticode-observation.json");
            string observationHash = ComputeFileSha256(observationPath, 16 * 1024);
            Require(
                string.Equals(
                    observationHash,
                    reference.AuthenticodeObservationSha256,
                    StringComparison.Ordinal),
                "Authenticode observation hash differs.");
            return new TrustedCapture(candidateRoot, manifest, gate, reference);
        }
        catch (AssertFailedException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or InvalidDataException or
            JsonException or NotSupportedException or UnauthorizedAccessException)
        {
            Assert.Fail(
                "HI-GATE1-REQUIRED-TEST-FAILURE: configured trusted artifacts are missing, unsafe, or invalid.");
            throw;
        }
    }

    private static GateCapture LoadGateCapture(
        string outputDirectory,
        IReadOnlyCollection<string> expectedFileNames)
    {
        EnsureExactOutputInventory(outputDirectory, expectedFileNames);
        string evidencePath = Path.Combine(outputDirectory, EvidenceFileName);
        string rawPath = Path.Combine(outputDirectory, RawFileName);
        LlmFitGate1Evidence evidence = ReadStrictJson<LlmFitGate1Evidence>(
            evidencePath,
            MaximumEvidenceBytes,
            EvidenceProperties);
        ValidateEvidenceShape(evidence);
        string rawJson = ReadStrictUtf8(rawPath, MaximumRawBytes);
        string rawSha256 = ComputeSha256(rawJson);
        Require(
            string.Equals(rawSha256, evidence.RawSystemJsonSha256, StringComparison.Ordinal),
            "The raw capture hash differs from the evidence.");
        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);
        Require(assessment.JsonValid, "The raw capture is not valid Gate 1 JSON.");
        Require(assessment.RequiredCpuRamPresent, "The raw capture lacks required CPU/RAM.");
        Require(
            string.Equals(assessment.RawJsonSha256, rawSha256, StringComparison.Ordinal),
            "The independent raw capture hash differs.");
        Require(evidence.JsonValid == assessment.JsonValid, "Evidence JSON result differs from raw JSON.");
        Require(
            evidence.RequiredCpuRamPresent == assessment.RequiredCpuRamPresent,
            "Evidence CPU/RAM result differs from raw JSON.");
        Require(
            evidence.CpuLogicalProcessorCount == assessment.CpuLogicalProcessorCount,
            "Evidence logical processors differ from raw JSON.");
        Require(evidence.TotalRamGiB == assessment.TotalRamGiB, "Evidence total RAM differs from raw JSON.");
        Require(
            evidence.AvailableRamGiB == assessment.AvailableRamGiB,
            "Evidence available RAM differs from raw JSON.");
        Require(evidence.GpuReported == assessment.GpuReported, "Evidence GPU presence differs from raw JSON.");
        Require(
            evidence.ReportedGpuCount == assessment.ReportedGpuCount,
            "Evidence GPU count differs from raw JSON.");
        Require(
            evidence.IntelGpuReported == assessment.IntelGpuReported,
            "Evidence Intel GPU result differs from raw JSON.");
        Require(
            evidence.DedicatedSharedMemorySemanticsEstablished ==
                assessment.DedicatedSharedMemorySemanticsEstablished,
            "Evidence GPU memory semantics differ from raw JSON.");
        Require(
            evidence.IntelNpuDetectionState == assessment.IntelNpuDetectionState,
            "Evidence NPU result differs from raw JSON.");
        string[] expectedDiagnostics =
        [
            .. assessment.DiagnosticCodes,
            evidence.AuthenticodePresent
                ? LlmFitGate1DiagnosticCodes.SignatureStatusChanged
                : LlmFitGate1DiagnosticCodes.SignatureClaimMismatch,
            LlmFitGate1DiagnosticCodes.DependencyLicenseInventoryPending,
        ];
        expectedDiagnostics = expectedDiagnostics
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Require(
            evidence.DiagnosticCodes.SequenceEqual(expectedDiagnostics, StringComparer.Ordinal),
            "Evidence diagnostics differ from the accepted raw/signature contract.");
        RawHardwareFacts rawFacts = ReadRawHardwareFacts(rawJson);
        return new GateCapture(evidence, assessment, rawFacts, rawSha256);
    }

    private static LlmFitCandidateManifest LoadPinnedManifest()
    {
        string manifestPath = Path.Combine(
            AppContext.BaseDirectory,
            "Candidates",
            ManifestFileName);
        LlmFitCandidateManifest manifest = LlmFitCandidateManifestLoader.Load(manifestPath);
        Require(manifest.SchemaVersion == "1.0", "Manifest schema differs.");
        Require(manifest.CandidateId == CandidateId, "Manifest candidate differs.");
        Require(manifest.Version == Version, "Manifest version differs.");
        Require(manifest.ReleaseTag == "v1.1.9", "Manifest release tag differs.");
        Require(manifest.ReleaseCommit == ReleaseCommit, "Manifest release commit differs.");
        Require(
            manifest.PublishedAtUtc == new DateTimeOffset(2026, 8, 9, 17, 7, 55, TimeSpan.Zero),
            "Manifest publication time differs.");
        Require(manifest.Archive.FileName == "llmfit-v1.1.9-x86_64-pc-windows-msvc.zip", "Archive name differs.");
        Require(manifest.Archive.LengthBytes == 5_255_910, "Archive length differs.");
        Require(manifest.Archive.Sha256 == ArchiveHash, "Archive hash differs.");
        Require(manifest.Executable.RelativePath == "llmfit.exe", "Executable name differs.");
        Require(manifest.Executable.Sha256 == ExecutableHash, "Executable hash differs.");
        Require(manifest.Executable.PeMachine == "AMD64", "Executable architecture differs.");
        Require(
            manifest.Executable.AuthenticodePolicy == "ObserveAndRecord",
            "Authenticode policy differs.");
        Require(
            manifest.RequiredFiles.SequenceEqual(
                RequiredPackageFiles,
                StringComparer.Ordinal),
            "Required package files differ.");
        Require(
            manifest.Commands.Version.SequenceEqual(ApprovedVersionArguments, StringComparer.Ordinal),
            "Version command differs.");
        Require(
            manifest.Commands.System.SequenceEqual(
                ApprovedSystemArguments,
                StringComparer.Ordinal),
            "System command differs.");
        Require(manifest.License.Spdx == "MIT", "License differs.");
        Require(manifest.License.RelativePath == "LICENSE", "License path differs.");
        return manifest;
    }

    private static LlmFitCandidateVerification VerifyCandidate(
        string candidateRoot,
        LlmFitCandidateManifest manifest)
    {
        try
        {
            LlmFitCandidateVerification verification =
                new LlmFitCandidateVerifier().Verify(candidateRoot, manifest);
            Require(
                IsLowercaseSha256(verification.ArchiveSha256) &&
                    IsLowercaseSha256(verification.ExecutableSha256) &&
                    string.Equals(verification.PeMachine, "AMD64", StringComparison.Ordinal) &&
                    verification.AuthenticodeStatus is "NotSigned" or "PresentUnverified" &&
                    verification.AuthenticodePresent == string.Equals(
                        verification.AuthenticodeStatus,
                        "PresentUnverified",
                        StringComparison.Ordinal),
                "Candidate verification strings failed privacy-safe validation.");
            return verification;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or InvalidDataException or
            NotSupportedException or UnauthorizedAccessException)
        {
            Assert.Fail(
                "HI-GATE1-REQUIRED-TEST-FAILURE: candidate verification could not complete.");
            throw;
        }
    }

    private static void AssertTrustedTarget(WindowsReference reference)
    {
        Assert.IsTrue(
            OperatingSystem.IsWindows() &&
                RuntimeInformation.OSArchitecture == Architecture.X64 &&
                reference.WindowsX64 &&
                reference.WindowsIntelCpuObserved &&
                reference.WindowsIntelGpuObserved,
            "HI-GATE1-WRONG-TARGET: requires Windows x64 with Intel CPU and Intel graphics.");
        Assert.IsTrue(reference.WindowsCpuNames.Any(ContainsIntelToken));
        Assert.IsTrue(reference.WindowsGpuNames.Any(ContainsIntelToken));
    }

    private static void AssertNoSocketOrResidualEvidence(LlmFitGate1Evidence evidence)
    {
        Assert.IsFalse(evidence.SocketObservationFailed);
        Assert.IsFalse(evidence.VersionCandidateSocketObserved);
        Assert.IsFalse(evidence.VersionDashboardPortObserved);
        Assert.IsFalse(evidence.SystemCandidateSocketObserved);
        Assert.IsFalse(evidence.SystemDashboardPortObserved);
        Assert.IsFalse(evidence.VersionCandidateProcessRemainedAfterExit);
        Assert.IsFalse(evidence.SystemCandidateProcessRemainedAfterExit);
    }

    private static void AssertEvidenceMatchesVerification(
        LlmFitGate1Evidence evidence,
        LlmFitCandidateVerification verification)
    {
        Assert.IsTrue(verification.IntegrityPassed);
        Assert.IsTrue(
            string.Equals(
                verification.ArchiveSha256,
                evidence.ObservedArchiveSha256,
                StringComparison.Ordinal),
            "Evidence archive identity differs from fresh verification.");
        Assert.IsTrue(
            string.Equals(
                verification.ExecutableSha256,
                evidence.ObservedExecutableSha256,
                StringComparison.Ordinal),
            "Evidence executable identity differs from fresh verification.");
        Assert.IsTrue(
            string.Equals(
                verification.PeMachine,
                evidence.ObservedPeMachine,
                StringComparison.Ordinal),
            "Evidence architecture differs from fresh verification.");
        Assert.AreEqual(verification.AuthenticodePresent, evidence.AuthenticodePresent);
        Assert.IsTrue(
            string.Equals(
                verification.AuthenticodeStatus,
                evidence.AuthenticodeStatus,
                StringComparison.Ordinal),
            "Evidence Authenticode status differs from fresh verification.");
    }

    private static void AssertReferenceMatchesSocketEvidence(
        WindowsReference reference,
        LlmFitGate1Evidence evidence)
    {
        Assert.AreEqual(evidence.VersionCandidateSocketObserved, reference.VersionCandidateSocketObserved);
        Assert.AreEqual(evidence.VersionDashboardPortObserved, reference.VersionDashboardPortObserved);
        Assert.AreEqual(evidence.SystemCandidateSocketObserved, reference.SystemCandidateSocketObserved);
        Assert.AreEqual(evidence.SystemDashboardPortObserved, reference.SystemDashboardPortObserved);
        Assert.AreEqual(
            evidence.VersionCandidateProcessRemainedAfterExit,
            reference.VersionCandidateProcessRemainedAfterExit);
        Assert.AreEqual(
            evidence.SystemCandidateProcessRemainedAfterExit,
            reference.SystemCandidateProcessRemainedAfterExit);
    }

    private static Dictionary<string, string> RequirePrerequisites(
        string category,
        params string[] variableNames)
    {
        string[] missing = variableNames
            .Where(name => Environment.GetEnvironmentVariable(name) is null)
            .ToArray();
        if (missing.Length != 0)
        {
            Assert.Inconclusive(
                $"{category} prerequisites are missing: {string.Join(", ", missing)}. " +
                "Configure the trusted target exactly as documented in the Gate 1 runbook.");
        }

        return variableNames.ToDictionary(
            name => name,
            name => Environment.GetEnvironmentVariable(name)!,
            StringComparer.Ordinal);
    }

    private static T ReadStrictJson<T>(
        string path,
        int maximumBytes,
        IReadOnlyCollection<string> allowedProperties)
        where T : class
    {
        string json = ReadStrictUtf8(path, maximumBytes);
        using JsonDocument document = JsonDocument.Parse(
            json,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16,
            });
        Require(document.RootElement.ValueKind == JsonValueKind.Object, "JSON root is not an object.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            Require(seen.Add(property.Name), "JSON contains a duplicate property.");
        }

        Require(seen.SetEquals(allowedProperties), "JSON properties differ from the privacy allowlist.");
        return JsonSerializer.Deserialize<T>(json, SerializerOptions) ??
            throw new InvalidDataException("JSON did not produce a record.");
    }

    private static string ReadStrictUtf8(string path, int maximumBytes)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);
        Require(stream.Length is > 0 && stream.Length <= maximumBytes, "File length is invalid.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        Require(stream.ReadByte() == -1, "File changed while it was read.");
        Require(
            bytes.Length < 3 || bytes[0] != 0xEF || bytes[1] != 0xBB || bytes[2] != 0xBF,
            "UTF-8 byte-order marks are forbidden.");
        return StrictUtf8.GetString(bytes);
    }

    private static void ValidateEvidenceShape(LlmFitGate1Evidence evidence)
    {
        ValidateEvidenceStringShapes(
            evidence.Disposition,
            evidence.AuthenticodePresent,
            evidence.AuthenticodeStatus);
        Require(evidence.SchemaVersion == "1.0", "Evidence schema differs.");
        Require(evidence.CandidateId == CandidateId, "Evidence candidate differs.");
        Require(evidence.ExpectedVersion == Version, "Evidence expected version differs.");
        Require(evidence.ReportedVersion == ReportedVersion, "Evidence reported version differs.");
        Require(evidence.ReleaseCommit == ReleaseCommit, "Evidence release commit differs.");
        Require(evidence.ExpectedArchiveSha256 == ArchiveHash, "Evidence archive hash differs.");
        Require(evidence.ObservedArchiveSha256 == ArchiveHash, "Observed archive hash differs.");
        Require(evidence.ExpectedExecutableSha256 == ExecutableHash, "Evidence executable hash differs.");
        Require(evidence.ObservedExecutableSha256 == ExecutableHash, "Observed executable hash differs.");
        Require(evidence.ExpectedPeMachine == "AMD64", "Expected PE architecture differs.");
        Require(evidence.ObservedPeMachine == "AMD64", "Observed PE architecture differs.");
        Require(evidence.GateStartedAtUtc.Offset == TimeSpan.Zero, "Gate start is not UTC.");
        Require(evidence.GateCompletedAtUtc.Offset == TimeSpan.Zero, "Gate end is not UTC.");
        Require(evidence.GateCompletedAtUtc >= evidence.GateStartedAtUtc, "Gate timestamps are reversed.");
        Require(evidence.DurationMilliseconds >= 0, "Gate duration is negative.");
        Require(
            evidence.DurationMilliseconds == checked((long)(
                evidence.GateCompletedAtUtc - evidence.GateStartedAtUtc).TotalMilliseconds),
            "Gate duration differs from its timestamps.");
        Require(
            evidence.VersionInvocationArguments is not null &&
                evidence.VersionInvocationArguments.SequenceEqual(
                    ApprovedVersionArguments,
                    StringComparer.Ordinal),
            "Evidence version arguments differ.");
        Require(
            evidence.SystemInvocationArguments is not null &&
                evidence.SystemInvocationArguments.SequenceEqual(
                    ApprovedSystemArguments,
                    StringComparer.Ordinal),
            "Evidence system arguments differ.");
        Require(evidence.VersionExitCode == 0, "Version command did not succeed.");
        Require(evidence.SystemExitCode == 0, "System command did not succeed.");
        Require(!evidence.ProcessStartFailed, "A candidate process did not start.");
        Require(!evidence.SocketObservationFailed, "Socket observation failed.");
        Require(!evidence.TimedOut, "A candidate process timed out.");
        Require(!evidence.Cancelled, "A candidate process was cancelled.");
        Require(!evidence.StandardOutputTruncated, "Candidate stdout was truncated.");
        Require(!evidence.StandardErrorTruncated, "Candidate stderr was truncated.");
        Require(evidence.JsonValid, "Evidence JSON result failed.");
        Require(evidence.RequiredCpuRamPresent, "Evidence CPU/RAM result failed.");
        Require(evidence.CpuLogicalProcessorCount > 0, "Evidence logical processor count is invalid.");
        Require(evidence.TotalRamGiB > 0, "Evidence total RAM is invalid.");
        Require(evidence.AvailableRamGiB >= 0, "Evidence available RAM is invalid.");
        Require(
            evidence.AvailableRamGiB <= evidence.TotalRamGiB,
            "Evidence available RAM exceeds total RAM.");
        Require(
            !evidence.DedicatedSharedMemorySemanticsEstablished,
            "Evidence overclaims Intel GPU memory semantics.");
        Require(
            evidence.IntelNpuDetectionState == "DetectionUnavailable",
            "Evidence overclaims Intel NPU detection.");
        Require(evidence.RawSystemJsonFileName == RawFileName, "Raw filename differs.");
        Require(IsLowercaseSha256(evidence.RawSystemJsonSha256), "Raw hash is invalid.");
        string[] diagnosticCodes = evidence.DiagnosticCodes ??
            throw new InvalidDataException("Evidence diagnostics are absent.");
        Require(diagnosticCodes.Length > 0, "Evidence diagnostics are empty.");
        Require(
            diagnosticCodes.All(LlmFitGate1DiagnosticCodes.All.Contains),
            "Evidence contains an unknown diagnostic.");
        Require(
            diagnosticCodes.Distinct(StringComparer.Ordinal).Count() == diagnosticCodes.Length,
            "Evidence diagnostics contain duplicates.");
    }

    private static void ValidateEvidenceStringShapes(
        string disposition,
        bool authenticodePresent,
        string authenticodeStatus)
    {
        Require(
            string.Equals(
                disposition,
                "FunctionalPassWithPackagingConcern",
                StringComparison.Ordinal),
            "Evidence string fields failed privacy-safe validation.");
        bool statusAllowed = authenticodeStatus is "NotSigned" or "PresentUnverified";
        bool statusMatchesPresence = authenticodePresent == string.Equals(
            authenticodeStatus,
            "PresentUnverified",
            StringComparison.Ordinal);
        Require(
            statusAllowed && statusMatchesPresence,
            "Evidence string fields failed privacy-safe validation.");
    }

    private static bool IsCaptureWithinThirtySeconds(
        DateTimeOffset capturedBefore,
        DateTimeOffset capturedAfter)
    {
        TimeSpan interval = capturedAfter - capturedBefore;
        return interval >= TimeSpan.Zero && interval <= TimeSpan.FromSeconds(30);
    }

    private static long GetCaptureIntervalMilliseconds(
        DateTimeOffset capturedBefore,
        DateTimeOffset capturedAfter)
    {
        TimeSpan interval = capturedAfter - capturedBefore;
        return interval.Ticks / TimeSpan.TicksPerMillisecond;
    }

    private static void ValidateReferenceShape(WindowsReference reference)
    {
        Require(reference.SchemaVersion == "1.0", "Reference schema differs.");
        Require(reference.CaptureStatus == "Captured", "Reference status differs.");
        Require(reference.CapturedBeforeUtc.Offset == TimeSpan.Zero, "Before timestamp is not UTC.");
        Require(reference.GateStartedAtUtc.Offset == TimeSpan.Zero, "Gate start is not UTC.");
        Require(reference.GateCompletedAtUtc.Offset == TimeSpan.Zero, "Gate end is not UTC.");
        Require(reference.CapturedAfterUtc.Offset == TimeSpan.Zero, "After timestamp is not UTC.");
        Require(
            reference.CapturedBeforeUtc <= reference.GateStartedAtUtc &&
                reference.GateStartedAtUtc <= reference.GateCompletedAtUtc &&
                reference.GateCompletedAtUtc <= reference.CapturedAfterUtc,
            "Reference timestamps are not bracketed.");
        Require(
            reference.CaptureIntervalMilliseconds == GetCaptureIntervalMilliseconds(
                reference.CapturedBeforeUtc,
                reference.CapturedAfterUtc),
            "Reference interval differs from its timestamps.");
        Require(
            reference.CaptureWithinThirtySeconds == IsCaptureWithinThirtySeconds(
                reference.CapturedBeforeUtc,
                reference.CapturedAfterUtc),
            "Reference interval classification differs from its timestamps.");
        Require(reference.WindowsCpuNames is { Length: > 0 }, "Windows CPU names are absent.");
        Require(reference.WindowsGpuNames is { Length: > 0 }, "Windows GPU names are absent.");
        ValidateHardwareNames(reference.WindowsCpuNames);
        ValidateHardwareNames(reference.WindowsGpuNames);
        Require(reference.WindowsLogicalProcessorCount > 0, "Windows logical processor count is invalid.");
        Require(reference.WindowsTotalPhysicalMemoryBytes > 0, "Windows total RAM is invalid.");
        Require(
            reference.WindowsFreePhysicalMemoryBeforeKiB <= reference.WindowsTotalPhysicalMemoryBytes / 1024,
            "Windows before-free RAM is invalid.");
        Require(
            reference.WindowsFreePhysicalMemoryAfterKiB <= reference.WindowsTotalPhysicalMemoryBytes / 1024,
            "Windows after-free RAM is invalid.");
        Require(reference.LlmFitLogicalProcessorCount > 0, "LLM Fit logical count is invalid.");
        Require(reference.TotalRamDeltaGiB >= 0, "Total RAM delta is invalid.");
        Require(reference.TotalRamToleranceGiB == 1d, "Total RAM tolerance differs.");
        Require(reference.AvailableRamDeltaGiB >= 0, "Available RAM delta is invalid.");
        Require(reference.AvailableRamToleranceGiB >= 2d, "Available RAM tolerance is invalid.");
        Require(
            reference.IntelGpuComparisonStatus is "Matched" or "FieldLevelGap",
            "GPU comparison status is invalid.");
        Require(!reference.DedicatedSharedMemorySemanticsEstablished, "GPU memory semantics were overclaimed.");
        Require(reference.IntelNpuDetectionState == "DetectionUnavailable", "NPU detection was overclaimed.");
        Require(
            reference.GateDisposition is "FunctionalPassWithPackagingConcern" or
                "AcceptedForFunctionalEvaluation",
            "Gate disposition is invalid.");
        string[] diagnosticCodes = reference.DiagnosticCodes ??
            throw new InvalidDataException("Reference diagnostics are absent.");
        Require(diagnosticCodes.Length > 0, "Reference diagnostics are empty.");
        Require(
            diagnosticCodes.All(LlmFitGate1DiagnosticCodes.All.Contains),
            "Reference contains an unknown diagnostic.");
        Require(
            diagnosticCodes.Distinct(StringComparer.Ordinal).Count() == diagnosticCodes.Length,
            "Reference diagnostics contain duplicates.");
        Require(
            IsLowercaseSha256(reference.AuthenticodeObservationSha256),
            "Authenticode observation hash is invalid.");
    }

    private static void ValidateHardwareNames(IEnumerable<string> names)
    {
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (string name in names)
        {
            Require(
                !string.IsNullOrWhiteSpace(name) &&
                    name.Length <= 256 &&
                    !name.Any(char.IsControl) &&
                    HardwareNameRegex().IsMatch(name),
                "A Windows hardware name has an unsafe shape.");
            Require(normalized.Add(NormalizeHardwareIdentity(name)), "Windows hardware names are duplicated.");
        }
    }

    private static void ValidateRawHardwareNames(IEnumerable<string> names)
    {
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (string name in names)
        {
            Require(
                !string.IsNullOrWhiteSpace(name) &&
                    name.Length <= 256 &&
                    !name.Any(char.IsControl) &&
                    !Path.IsPathFullyQualified(name) &&
                    !name.StartsWith(@"\\", StringComparison.Ordinal),
                "A raw hardware name has an unsafe shape.");
            Require(
                normalized.Add(NormalizeHardwareIdentity(name)),
                "Raw hardware names are duplicated.");
        }
    }

    private static RawHardwareFacts ReadRawHardwareFacts(string rawJson)
    {
        using JsonDocument document = JsonDocument.Parse(rawJson);
        JsonElement system = document.RootElement.GetProperty("system");
        string cpuName = system.GetProperty("cpu_name").GetString() ??
            throw new InvalidDataException("CPU name is absent.");
        var gpuNames = new List<string>();
        if (system.TryGetProperty("gpu_name", out JsonElement topLevelGpu) &&
            topLevelGpu.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(topLevelGpu.GetString()))
        {
            gpuNames.Add(topLevelGpu.GetString()!);
        }

        if (system.TryGetProperty("gpus", out JsonElement gpus) && gpus.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement gpu in gpus.EnumerateArray())
            {
                if (gpu.TryGetProperty("name", out JsonElement name) &&
                    name.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(name.GetString()))
                {
                    gpuNames.Add(name.GetString()!);
                }
            }
        }

        string[] distinctGpuNames = gpuNames.Distinct(StringComparer.Ordinal).ToArray();
        ValidateRawHardwareNames([cpuName]);
        if (distinctGpuNames.Length != 0)
        {
            ValidateRawHardwareNames(distinctGpuNames);
        }

        return new RawHardwareFacts(cpuName, distinctGpuNames);
    }

    private static string NormalizeHardwareIdentity(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = TrademarkMarkerRegex().Replace(
            value.Normalize(NormalizationForm.FormKC),
            " ");
        normalized = PunctuationRegex().Replace(normalized, " ");
        normalized = RepeatedWhitespaceRegex().Replace(normalized, " ").Trim();
        return normalized.ToUpperInvariant();
    }

    private static bool ContainsIntelToken(string value)
    {
        return NormalizeHardwareIdentity(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains("INTEL", StringComparer.Ordinal);
    }

    private static IEnumerable<string> GetNormalizedIntelNames(IEnumerable<string> names)
    {
        return names
            .Where(ContainsIntelToken)
            .Select(NormalizeHardwareIdentity)
            .Distinct(StringComparer.Ordinal);
    }

    private static StableFileIdentity ResolveStableRegularFileIdentity(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using SafeFileHandle handle = CreateFile(
            path,
            FileReadAttributes,
            ShareRead | ShareWrite | ShareDelete,
            0,
            OpenExisting,
            FileFlagOpenReparsePoint,
            0);
        if (handle.IsInvalid)
        {
            int errorCode = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new IOException(
                "A process image identity could not be opened safely.",
                new Win32Exception(errorCode));
        }

        if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information))
        {
            throw new IOException(
                "A process image identity could not be read safely.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        if ((information.FileAttributes &
            (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new InvalidDataException(
                "A process image identity must refer to an ordinary file.");
        }

        return new StableFileIdentity(
            information.VolumeSerialNumber,
            ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow);
    }

    private static bool IsCandidateProcessPresent(
        string candidateRoot,
        LlmFitCandidateManifest manifest)
    {
        StableFileIdentity expectedIdentity;
        try
        {
            expectedIdentity = ResolveStableRegularFileIdentity(
                Path.Combine(candidateRoot, manifest.Executable.RelativePath));
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or InvalidDataException or
            NotSupportedException or UnauthorizedAccessException or
            System.Security.SecurityException)
        {
            throw new InvalidOperationException(
                "Candidate process identity could not be inspected safely.");
        }

        string processName = Path.GetFileNameWithoutExtension(manifest.Executable.RelativePath);
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            throw new InvalidOperationException(
                "Candidate process identity could not be inspected safely.",
                exception);
        }

        foreach (Process process in processes)
        {
            using (process)
            {
                try
                {
                    if (!TryObserveProcessImage(process, out ProcessImageObservation observation))
                    {
                        continue;
                    }

                    if (observation.FileIdentity == expectedIdentity)
                    {
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                    throw new InvalidOperationException(
                        "Candidate process identity could not be inspected safely.");
                }
            }
        }

        return false;
    }

    private static bool TryObserveProcessImage(
        Process process,
        out ProcessImageObservation observation)
    {
        try
        {
            SafeProcessHandle processHandle = process.SafeHandle;
            if (processHandle.IsInvalid || processHandle.IsClosed)
            {
                if (TryGetHasExited(process) == true)
                {
                    observation = default;
                    return false;
                }

                throw new InvalidOperationException(
                    "Process image identity could not be established safely.");
            }

            int processId = process.Id;
            DateTime startTimeUtc = process.StartTime.ToUniversalTime();
            string? processPath = process.MainModule?.FileName;
            if (processPath is null)
            {
                throw new InvalidOperationException(
                    "Process image identity could not be established safely.");
            }

            observation = new ProcessImageObservation(
                processId,
                startTimeUtc,
                ResolveStableRegularFileIdentity(processPath));
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or InvalidDataException or
            InvalidOperationException or NotSupportedException or ObjectDisposedException or
            UnauthorizedAccessException or Win32Exception or
            System.Security.SecurityException)
        {
            if (TryGetHasExited(process) == true)
            {
                observation = default;
                return false;
            }

            throw new InvalidOperationException(
                "Process image identity could not be established safely.");
        }
    }

    private static async Task<int> RunFixedCliAsync(
        string repositoryRoot,
        string candidateRoot,
        string outputDirectory,
        LlmFitCandidateManifest manifest)
    {
        string projectPath = Path.Combine(
            repositoryRoot,
            "tools",
            "HardwareInspection.LlmFitSpike",
            "HardwareInspection.LlmFitSpike.csproj");
        Require(File.Exists(projectPath), "The fixed Gate 1 project is absent.");
        Require(
            !Directory.Exists(outputDirectory) && !File.Exists(outputDirectory),
            "The offline output already exists.");

        string dotnetPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet",
            "dotnet.exe");
        Require(File.Exists(dotnetPath), "The fixed .NET host is unavailable.");
        var startInfo = new ProcessStartInfo
        {
            FileName = dotnetPath,
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in new[]
        {
            "run", "--project", projectPath, "--configuration", "Release",
            "--runtime", "win-x64", "--no-restore", "--no-build", "--",
            "--candidate-root", candidateRoot, "--output", outputDirectory,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            Require(process.Start(), "The fixed Gate 1 CLI did not start.");
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or Win32Exception)
        {
            throw new InvalidOperationException("The fixed Gate 1 CLI did not start.", exception);
        }

        Task[] drainTasks =
        [
            DrainAsync(process.StandardOutput),
            DrainAsync(process.StandardError),
        ];
        int? exitCode = null;
        Exception? operationFailure = null;
        bool cleanupHealthy = false;
        try
        {
            Task exit = process.WaitForExitAsync();
            Task completed = await Task.WhenAny(exit, Task.Delay(TimeSpan.FromSeconds(90)))
                .ConfigureAwait(false);
            if (!ReferenceEquals(completed, exit))
            {
                throw new TimeoutException("The fixed Gate 1 CLI exceeded its deadline.");
            }

            await exit.ConfigureAwait(false);
            if (!await AwaitDrainTasksAsync(drainTasks).ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    "The fixed Gate 1 CLI output could not be drained safely.");
            }

            exitCode = process.ExitCode;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or NotSupportedException or
            TimeoutException or Win32Exception)
        {
            operationFailure = exception;
        }
        finally
        {
            bool outerProcessCleaned = await EnsureProcessExitedAsync(process)
                .ConfigureAwait(false);
            bool outputDrained = await AwaitDrainTasksAsync(drainTasks).ConfigureAwait(false);
            CandidateProcessCleanup cleanup = await CleanupCandidateProcessesAsync(
                    candidateRoot,
                    manifest)
                .ConfigureAwait(false);
            cleanupHealthy = outerProcessCleaned && outputDrained &&
                !cleanup.ResidualObserved && !cleanup.ObservationUncertain &&
                cleanup.CleanupSucceeded;
        }

        if (!cleanupHealthy)
        {
            throw new InvalidOperationException(
                "The fixed Gate 1 CLI or pinned candidate did not clean up safely.");
        }

        if (operationFailure is not null)
        {
            ExceptionDispatchInfo.Capture(operationFailure).Throw();
        }

        return exitCode ?? throw new InvalidOperationException(
            "The fixed Gate 1 CLI did not produce an exit code.");
    }

    private static async Task<bool> AwaitDrainTasksAsync(Task[] drainTasks)
    {
        try
        {
            await Task.WhenAll(drainTasks)
                .WaitAsync(TimeSpan.FromSeconds(10))
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or InvalidOperationException or
            ObjectDisposedException or TimeoutException)
        {
            return false;
        }
    }

    private static async Task<bool> EnsureProcessExitedAsync(Process process)
    {
        if (TryGetHasExited(process) == true)
        {
            return true;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return TryGetHasExited(process) == true;
        }

        try
        {
            await process.WaitForExitAsync()
                .WaitAsync(TimeSpan.FromSeconds(10))
                .ConfigureAwait(false);
            return TryGetHasExited(process) == true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or NotSupportedException or
            TimeoutException or Win32Exception)
        {
            return false;
        }
    }

    private static async Task<CandidateProcessCleanup> CleanupCandidateProcessesAsync(
        string candidateRoot,
        LlmFitCandidateManifest manifest)
    {
        StableFileIdentity expectedIdentity;
        try
        {
            expectedIdentity = ResolveStableRegularFileIdentity(
                Path.Combine(candidateRoot, manifest.Executable.RelativePath));
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or InvalidDataException or
            NotSupportedException or UnauthorizedAccessException or
            System.Security.SecurityException)
        {
            return new CandidateProcessCleanup(false, true, false);
        }

        string processName = Path.GetFileNameWithoutExtension(manifest.Executable.RelativePath);
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return new CandidateProcessCleanup(false, true, false);
        }

        bool residualObserved = false;
        bool observationUncertain = false;
        bool cleanupSucceeded = true;
        foreach (Process process in processes)
        {
            using (process)
            {
                ProcessImageObservation observation;
                try
                {
                    if (!TryObserveProcessImage(process, out observation))
                    {
                        continue;
                    }
                }
                catch (InvalidOperationException)
                {
                    observationUncertain = true;
                    continue;
                }

                if (observation.FileIdentity != expectedIdentity)
                {
                    continue;
                }

                residualObserved = true;
                VerifiedProcessCleanup verifiedCleanup = await StopVerifiedCandidateProcessAsync(
                        process,
                        observation,
                        expectedIdentity)
                    .ConfigureAwait(false);
                cleanupSucceeded &= verifiedCleanup.CleanupSucceeded;
                observationUncertain |= verifiedCleanup.ObservationUncertain;
                if (!verifiedCleanup.CleanupSucceeded)
                {
                    cleanupSucceeded = false;
                }
            }
        }

        try
        {
            if (IsCandidateProcessPresent(candidateRoot, manifest))
            {
                cleanupSucceeded = false;
            }
        }
        catch (InvalidOperationException)
        {
            observationUncertain = true;
        }

        return new CandidateProcessCleanup(
            residualObserved,
            observationUncertain,
            cleanupSucceeded);
    }

    private static async Task<VerifiedProcessCleanup> StopVerifiedCandidateProcessAsync(
        Process process,
        ProcessImageObservation originalObservation,
        StableFileIdentity expectedIdentity)
    {
        ProcessImageObservation currentObservation;
        try
        {
            if (!TryObserveProcessImage(process, out currentObservation))
            {
                return new VerifiedProcessCleanup(true, false);
            }
        }
        catch (InvalidOperationException)
        {
            return new VerifiedProcessCleanup(false, true);
        }

        if (currentObservation.ProcessId != originalObservation.ProcessId ||
            currentObservation.StartTimeUtc != originalObservation.StartTimeUtc ||
            currentObservation.FileIdentity != expectedIdentity)
        {
            return new VerifiedProcessCleanup(false, true);
        }

        try
        {
            SafeProcessHandle processHandle = process.SafeHandle;
            if (processHandle.IsInvalid || processHandle.IsClosed)
            {
                return TryGetHasExited(process) == true
                    ? new VerifiedProcessCleanup(true, false)
                    : new VerifiedProcessCleanup(false, true);
            }

            process.Kill(entireProcessTree: true);

            await process.WaitForExitAsync()
                .WaitAsync(TimeSpan.FromSeconds(10))
                .ConfigureAwait(false);
            bool rootExited = TryGetHasExited(process) == true;
            bool descendantsExited = await WaitForDescendantProcessesToExitAsync(
                    originalObservation.ProcessId)
                .ConfigureAwait(false);
            return new VerifiedProcessCleanup(rootExited && descendantsExited, false);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or NotSupportedException or
            ObjectDisposedException or TimeoutException or Win32Exception)
        {
            return TryGetHasExited(process) == true
                ? new VerifiedProcessCleanup(true, false)
                : new VerifiedProcessCleanup(false, false);
        }
    }

    private static async Task<bool> WaitForDescendantProcessesToExitAsync(int rootProcessId)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (true)
        {
            if (!TryGetDescendantProcessIds(rootProcessId, out HashSet<uint> descendants))
            {
                return false;
            }

            if (descendants.Count == 0)
            {
                return true;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
        }
    }

    private static bool TryGetDescendantProcessIds(
        int rootProcessId,
        out HashSet<uint> descendants)
    {
        descendants = [];
        using SafeFileHandle snapshot = CreateToolhelp32Snapshot(ToolhelpSnapshotProcess, 0);
        if (snapshot.IsInvalid)
        {
            return false;
        }

        var parentByProcess = new Dictionary<uint, uint>();
        var entry = new ProcessEntry32();
        if (!Process32First(snapshot, ref entry))
        {
            return false;
        }

        do
        {
            parentByProcess[entry.ProcessId] = entry.ParentProcessId;
            entry = new ProcessEntry32();
        }
        while (Process32Next(snapshot, ref entry));

        int lastError = Marshal.GetLastWin32Error();
        const int ErrorNoMoreFiles = 18;
        if (lastError != ErrorNoMoreFiles)
        {
            return false;
        }

        var tree = new HashSet<uint> { checked((uint)rootProcessId) };
        bool added;
        do
        {
            added = false;
            foreach (KeyValuePair<uint, uint> process in parentByProcess)
            {
                if (tree.Contains(process.Value) && tree.Add(process.Key))
                {
                    added = true;
                }
            }
        }
        while (added);

        tree.Remove(checked((uint)rootProcessId));
        descendants = tree;
        return true;
    }

    private static bool? TryGetHasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return null;
        }
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        char[] buffer = new char[4096];
        while (await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false) != 0)
        {
            // Drain without retaining operator-controlled paths in test results.
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string globalJson = Path.Combine(current.FullName, "global.json");
            string project = Path.Combine(
                current.FullName,
                "tools",
                "HardwareInspection.LlmFitSpike",
                "HardwareInspection.LlmFitSpike.csproj");
            if (File.Exists(globalJson) && File.Exists(project))
            {
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(current.FullName));
            }

            current = current.Parent;
        }

        throw new InvalidDataException("The fixed repository root could not be located.");
    }

    private static string ResolveFreshOperationalOutput(string value, string repositoryRoot)
    {
        string approvedParent = Path.GetFullPath(Path.Combine(
            repositoryRoot,
            "artifacts",
            "hardware-inspection",
            "llmfit"));
        string output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        string relative = Path.GetRelativePath(approvedParent, output);
        Require(
            !Path.IsPathRooted(relative) &&
                !string.Equals(relative, ".", StringComparison.Ordinal) &&
                !relative.Contains(Path.DirectorySeparatorChar) &&
                !relative.Contains(Path.AltDirectorySeparatorChar) &&
                !string.Equals(relative, "..", StringComparison.Ordinal),
            "The offline output must be a new direct child of the approved artifact root.");
        Require(
            !Directory.Exists(output) && !File.Exists(output),
            "The configured offline output already exists.");
        EnsureExistingPathComponentsAreOrdinary(repositoryRoot, output);
        return output;
    }

    private static string ResolveExistingOperationalOutput(string value, string repositoryRoot)
    {
        string approvedParent = Path.GetFullPath(Path.Combine(
            repositoryRoot,
            "artifacts",
            "hardware-inspection",
            "llmfit"));
        string output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        string relative = Path.GetRelativePath(approvedParent, output);
        Require(
            !Path.IsPathRooted(relative) &&
                !string.Equals(relative, ".", StringComparison.Ordinal) &&
                !relative.Contains(Path.DirectorySeparatorChar) &&
                !relative.Contains(Path.AltDirectorySeparatorChar) &&
                !string.Equals(relative, "..", StringComparison.Ordinal),
            "The trusted output is outside the approved artifact root.");
        Require(Directory.Exists(output) && !File.Exists(output), "The trusted output is invalid.");
        EnsureExistingPathComponentsAreOrdinary(repositoryRoot, output);
        Require(
            (File.GetAttributes(output) & FileAttributes.ReparsePoint) == 0,
            "The trusted output is a reparse point.");
        return output;
    }

    private static string ResolveExistingOrdinaryDirectory(string value, string message)
    {
        string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        Require(Directory.Exists(path), message);
        Require(!File.Exists(path), message);
        Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0, message);
        return path;
    }

    private static void EnsureExistingPathComponentsAreOrdinary(string root, string path)
    {
        string canonicalRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string canonicalPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string relative = Path.GetRelativePath(canonicalRoot, canonicalPath);
        Require(
            !Path.IsPathRooted(relative) && !relative.StartsWith("..", StringComparison.Ordinal),
            "Path escaped.");
        string current = canonicalRoot;
        Require(
            (File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0,
            "The repository root is a reparse point.");
        foreach (string segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current) && !File.Exists(current))
            {
                break;
            }

            Require(
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0,
                "The output chain contains a reparse point.");
        }
    }

    private static void WriteOfflineBlockedEvidence(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        Require(
            (File.GetAttributes(outputDirectory) & FileAttributes.ReparsePoint) == 0,
            "The offline output became a reparse point.");
        string destination = Path.Combine(outputDirectory, EvidenceFileName);
        Require(
            !File.Exists(destination) && !Directory.Exists(destination),
            "Blocked evidence already exists.");
        string temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        const string Json =
            "{\n" +
            "  \"schemaVersion\": \"1.0\",\n" +
            "  \"disposition\": \"Blocked\",\n" +
            "  \"diagnosticCodes\": [\"HI-GATE1-OFFLINE-PRECONDITION-FAILED\"]\n" +
            "}";
        byte[] bytes = StrictUtf8.GetBytes(Json);
        try
        {
            using (var stream = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            Require(
                !File.Exists(destination) && !Directory.Exists(destination),
                "Blocked evidence destination changed.");
            File.Move(temporary, destination);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static void EnsureExactOutputInventory(
        string outputDirectory,
        IReadOnlyCollection<string> expectedFileNames)
    {
        var expected = new HashSet<string>(expectedFileNames, StringComparer.Ordinal);
        string[] entries = Directory.EnumerateFileSystemEntries(outputDirectory).ToArray();
        Require(entries.Length == expected.Count, "Gate output inventory differs.");
        foreach (string entry in entries)
        {
            FileAttributes attributes = File.GetAttributes(entry);
            Require(
                (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0,
                "Gate output contains a directory or reparse point.");
            Require(expected.Remove(Path.GetFileName(entry)), "Gate output contains an unexpected file.");
        }

        Require(expected.Count == 0, "Gate output is missing a required file.");
    }

    private static string ComputeFileSha256(string path, int maximumBytes)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);
        Require(stream.Length is > 0 && stream.Length <= maximumBytes, "Hash source length is invalid.");
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ComputeSha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(value))).ToLowerInvariant();
    }

    private static bool IsLowercaseSha256(string? value)
    {
        return value is { Length: 64 } && value.All(
            character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public FileAttributes FileAttributes;
        public uint CreationTimeLow;
        public uint CreationTimeHigh;
        public uint LastAccessTimeLow;
        public uint LastAccessTimeHigh;
        public uint LastWriteTimeLow;
        public uint LastWriteTimeHigh;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public ProcessEntry32()
        {
            this = default;
            Size = checked((uint)Marshal.SizeOf<ProcessEntry32>());
            ExecutableFile = string.Empty;
        }

        public uint Size;
        public uint UsageCount;
        public uint ProcessId;
        public nint DefaultHeapId;
        public uint ModuleId;
        public uint ThreadCount;
        public uint ParentProcessId;
        public int BasePriority;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExecutableFile;
    }

#pragma warning disable SYSLIB1054 // Project policy forbids unsafe LibraryImport buffers.
    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateFileW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        nint securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateHardLinkW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(
        string fileName,
        string existingFileName,
        nint securityAttributes);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateToolhelp32Snapshot",
        ExactSpelling = true,
        SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateToolhelp32Snapshot(
        uint flags,
        uint processId);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "GetFileInformationByHandle",
        ExactSpelling = true,
        SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation fileInformation);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "Process32FirstW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(
        SafeFileHandle snapshot,
        ref ProcessEntry32 entry);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "Process32NextW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(
        SafeFileHandle snapshot,
        ref ProcessEntry32 entry);

#pragma warning restore SYSLIB1054

    [GeneratedRegex(@"\((?:R|TM)\)|[®™]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TrademarkMarkerRegex();

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+", RegexOptions.CultureInvariant)]
    private static partial Regex PunctuationRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex RepeatedWhitespaceRegex();

    [GeneratedRegex(
        @"^[\p{L}\p{Nd}\p{Zs}().,+_'@&\u00AE\u2122-]+$",
        RegexOptions.CultureInvariant)]
    private static partial Regex HardwareNameRegex();

    private sealed record TrustedCapture(
        string CandidateRoot,
        LlmFitCandidateManifest Manifest,
        GateCapture Gate,
        WindowsReference Reference);

    private sealed record GateCapture(
        LlmFitGate1Evidence Evidence,
        LlmFitSystemAssessment Assessment,
        RawHardwareFacts RawFacts,
        string RawSha256);

    private sealed record RawHardwareFacts(string CpuName, string[] GpuNames);

    private readonly record struct CandidateProcessCleanup(
        bool ResidualObserved,
        bool ObservationUncertain,
        bool CleanupSucceeded);

    private readonly record struct ProcessImageObservation(
        int ProcessId,
        DateTime StartTimeUtc,
        StableFileIdentity FileIdentity);

    private readonly record struct StableFileIdentity(
        uint VolumeSerialNumber,
        ulong FileId);

    private readonly record struct VerifiedProcessCleanup(
        bool CleanupSucceeded,
        bool ObservationUncertain);

    private sealed record WindowsReference(
        string SchemaVersion,
        string CaptureStatus,
        DateTimeOffset CapturedBeforeUtc,
        DateTimeOffset GateStartedAtUtc,
        DateTimeOffset GateCompletedAtUtc,
        DateTimeOffset CapturedAfterUtc,
        long CaptureIntervalMilliseconds,
        bool CaptureWithinThirtySeconds,
        bool WindowsX64,
        string[] WindowsCpuNames,
        int WindowsLogicalProcessorCount,
        ulong WindowsTotalPhysicalMemoryBytes,
        ulong WindowsFreePhysicalMemoryBeforeKiB,
        ulong WindowsFreePhysicalMemoryAfterKiB,
        string[] WindowsGpuNames,
        bool WindowsIntelCpuObserved,
        bool WindowsIntelGpuObserved,
        bool CpuIdentityMatched,
        int LlmFitLogicalProcessorCount,
        bool LogicalProcessorCountMatched,
        double TotalRamDeltaGiB,
        double TotalRamToleranceGiB,
        bool TotalRamWithinTolerance,
        double AvailableRamDeltaGiB,
        double AvailableRamToleranceGiB,
        bool AvailableRamWithinTolerance,
        bool JsonValid,
        bool RequiredCpuRamPresent,
        bool IntelGpuIdentityMatched,
        string IntelGpuComparisonStatus,
        bool DedicatedSharedMemorySemanticsEstablished,
        string IntelNpuDetectionState,
        int GateExitCode,
        string GateDisposition,
        string[] DiagnosticCodes,
        bool VersionCandidateSocketObserved,
        bool VersionDashboardPortObserved,
        bool SystemCandidateSocketObserved,
        bool SystemDashboardPortObserved,
        bool VersionCandidateProcessRemainedAfterExit,
        bool SystemCandidateProcessRemainedAfterExit,
        string AuthenticodeObservationSha256);
}
#pragma warning restore CA1707
