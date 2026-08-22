using System.Collections.Frozen;

namespace HardwareInspection.LlmFitSpike.Evidence;

#pragma warning disable CA1819 // The approved Gate 1 evidence contract requires string arrays.
public sealed record LlmFitGate1Evidence(
    string SchemaVersion,
    string Disposition,
    string CandidateId,
    string ExpectedVersion,
    string? ReportedVersion,
    string ReleaseCommit,
    string ExpectedArchiveSha256,
    string? ObservedArchiveSha256,
    string ExpectedExecutableSha256,
    string? ObservedExecutableSha256,
    string ExpectedPeMachine,
    string? ObservedPeMachine,
    bool AuthenticodePresent,
    string AuthenticodeStatus,
    string[] VersionInvocationArguments,
    string[] SystemInvocationArguments,
    DateTimeOffset GateStartedAtUtc,
    DateTimeOffset GateCompletedAtUtc,
    long DurationMilliseconds,
    int? VersionExitCode,
    int? SystemExitCode,
    bool ProcessStartFailed,
    bool SocketObservationFailed,
    bool TimedOut,
    bool Cancelled,
    bool StandardOutputTruncated,
    bool StandardErrorTruncated,
    bool JsonValid,
    bool RequiredCpuRamPresent,
    int? CpuLogicalProcessorCount,
    double? TotalRamGiB,
    double? AvailableRamGiB,
    bool GpuReported,
    int ReportedGpuCount,
    bool IntelGpuReported,
    bool DedicatedSharedMemorySemanticsEstablished,
    string IntelNpuDetectionState,
    bool VersionCandidateSocketObserved,
    bool VersionDashboardPortObserved,
    bool SystemCandidateSocketObserved,
    bool SystemDashboardPortObserved,
    bool VersionCandidateProcessRemainedAfterExit,
    bool SystemCandidateProcessRemainedAfterExit,
    string? RawSystemJsonFileName,
    string? RawSystemJsonSha256,
    string[] DiagnosticCodes);
#pragma warning restore CA1819

public static class LlmFitGate1DiagnosticCodes
{
    public const string ManifestInvalid = "HI-LLMFIT-MANIFEST-INVALID";
    public const string PackageMissing = "HI-LLMFIT-PACKAGE-MISSING";
    public const string UnexpectedPackageMember = "HI-LLMFIT-UNEXPECTED-PACKAGE-MEMBER";
    public const string PackageChangedDuringRun = "HI-LLMFIT-PACKAGE-CHANGED-DURING-RUN";
    public const string PathEscape = "HI-LLMFIT-PATH-ESCAPE";
    public const string ReparsePoint = "HI-LLMFIT-REPARSE-POINT";
    public const string ArchiveLengthMismatch = "HI-LLMFIT-ARCHIVE-LENGTH-MISMATCH";
    public const string ArchiveHashMismatch = "HI-LLMFIT-ARCHIVE-HASH-MISMATCH";
    public const string ExecutableHashMismatch = "HI-LLMFIT-EXECUTABLE-HASH-MISMATCH";
    public const string PeInvalid = "HI-LLMFIT-PE-INVALID";
    public const string PeArchitectureMismatch = "HI-LLMFIT-PE-ARCHITECTURE-MISMATCH";
    public const string SignatureClaimMismatch = "HI-LLMFIT-SIGNATURE-CLAIM-MISMATCH";
    public const string SignatureStatusChanged = "HI-LLMFIT-SIGNATURE-STATUS-CHANGED";
    public const string DependencyLicenseInventoryPending =
        "HI-LLMFIT-DEPENDENCY-LICENSE-INVENTORY-PENDING";
    public const string ProcessStartFailed = "HI-LLMFIT-PROCESS-START-FAILED";
    public const string VersionMismatch = "HI-LLMFIT-VERSION-MISMATCH";
    public const string ProcessTimedOut = "HI-LLMFIT-PROCESS-TIMED-OUT";
    public const string ProcessCancelled = "HI-LLMFIT-PROCESS-CANCELLED";
    public const string ProcessExitNonzero = "HI-LLMFIT-PROCESS-EXIT-NONZERO";
    public const string StdoutTruncated = "HI-LLMFIT-STDOUT-TRUNCATED";
    public const string StderrTruncated = "HI-LLMFIT-STDERR-TRUNCATED";
    public const string SocketObservationFailed = "HI-LLMFIT-SOCKET-OBSERVATION-FAILED";
    public const string CandidateSocketObserved = "HI-LLMFIT-CANDIDATE-SOCKET-OBSERVED";
    public const string DashboardPortObserved = "HI-LLMFIT-DASHBOARD-PORT-OBSERVED";
    public const string ResidualProcess = "HI-LLMFIT-RESIDUAL-PROCESS";
    public const string JsonInvalid = "HI-LLMFIT-JSON-INVALID";
    public const string CpuRamMissing = "HI-LLMFIT-CPU-RAM-MISSING";
    public const string GpuInconsistent = "HI-LLMFIT-GPU-INCONSISTENT";
    public const string WindowsIntelMemorySemanticsGap =
        "HI-LLMFIT-WINDOWS-INTEL-MEMORY-SEMANTICS-GAP";
    public const string WindowsIntelNpuGap = "HI-LLMFIT-WINDOWS-INTEL-NPU-GAP";
    public const string SchemaDocumentationDrift = "HI-LLMFIT-SCHEMA-DOCUMENTATION-DRIFT";
    public const string WrongTarget = "HI-GATE1-WRONG-TARGET";
    public const string WindowsComparisonFailed = "HI-GATE1-WINDOWS-COMPARISON-FAILED";
    public const string OfflinePreconditionFailed = "HI-GATE1-OFFLINE-PRECONDITION-FAILED";
    public const string PrivacyValidationFailed = "HI-GATE1-PRIVACY-VALIDATION-FAILED";
    public const string RequiredTestFailure = "HI-GATE1-REQUIRED-TEST-FAILURE";

    private static readonly FrozenSet<string> AllowedCodes = new[]
    {
        ManifestInvalid,
        PackageMissing,
        UnexpectedPackageMember,
        PackageChangedDuringRun,
        PathEscape,
        ReparsePoint,
        ArchiveLengthMismatch,
        ArchiveHashMismatch,
        ExecutableHashMismatch,
        PeInvalid,
        PeArchitectureMismatch,
        SignatureClaimMismatch,
        SignatureStatusChanged,
        DependencyLicenseInventoryPending,
        ProcessStartFailed,
        VersionMismatch,
        ProcessTimedOut,
        ProcessCancelled,
        ProcessExitNonzero,
        StdoutTruncated,
        StderrTruncated,
        SocketObservationFailed,
        CandidateSocketObserved,
        DashboardPortObserved,
        ResidualProcess,
        JsonInvalid,
        CpuRamMissing,
        GpuInconsistent,
        WindowsIntelMemorySemanticsGap,
        WindowsIntelNpuGap,
        SchemaDocumentationDrift,
        WrongTarget,
        WindowsComparisonFailed,
        OfflinePreconditionFailed,
        PrivacyValidationFailed,
        RequiredTestFailure,
    }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => AllowedCodes;
}
