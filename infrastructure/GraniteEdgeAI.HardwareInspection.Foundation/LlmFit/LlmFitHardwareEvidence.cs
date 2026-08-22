using System.Collections.ObjectModel;
using GraniteEdgeAI.HardwareInspection.Foundation.Validation;

namespace GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;

public enum LlmFitEvidenceState
{
    Available,
    Invalid,
    Unavailable,
}

public enum LlmFitGpuDetectionState
{
    Reported,
    NotReported,
    Invalid,
}

public enum LlmFitDiagnosticCode
{
    ToolIdentityMismatch,
    CommandContractMismatch,
    VersionStartFailed,
    VersionTimedOut,
    VersionOutputLimitExceeded,
    VersionCleanupFailed,
    VersionNonZeroExit,
    VersionOutputMismatch,
    VersionCancelledUnexpectedly,
    SystemStartFailed,
    SystemTimedOut,
    SystemOutputLimitExceeded,
    SystemCleanupFailed,
    SystemNonZeroExit,
    SystemCancelledUnexpectedly,
    JsonInvalid,
    RequiredCpuRamMissing,
    RequiredCpuRamInvalid,
    GpuShapeMissing,
    GpuInconsistent,
}

public sealed record LlmFitReportedGpu
{
    public LlmFitReportedGpu(string name, int count)
    {
        LlmFitContractValidation.ValidateSafeName(name, nameof(name));
        if (count is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        Name = name;
        Count = count;
    }

    public string Name { get; }

    public int Count { get; }
}

public sealed class LlmFitHardwareEvidence
{
    private LlmFitHardwareEvidence(
        string toolId,
        string version,
        DateTimeOffset capturedAtUtc,
        LlmFitEvidenceState state,
        string? cpuName,
        int? cpuLogicalProcessorCount,
        double? totalRamGiB,
        double? availableRamGiB,
        LlmFitGpuDetectionState gpuState,
        IReadOnlyList<LlmFitReportedGpu> gpus,
        string? rawOutputSha256,
        IReadOnlyList<LlmFitDiagnosticCode> diagnostics)
    {
        ToolId = toolId;
        Version = version;
        CapturedAtUtc = capturedAtUtc;
        State = state;
        CpuName = cpuName;
        CpuLogicalProcessorCount = cpuLogicalProcessorCount;
        TotalRamGiB = totalRamGiB;
        AvailableRamGiB = availableRamGiB;
        GpuState = gpuState;
        Gpus = gpus;
        ReportedGpuCount = gpus.Sum(static gpu => gpu.Count);
        RawOutputSha256 = rawOutputSha256;
        Diagnostics = diagnostics;
    }

    public string ToolId { get; }

    public string Version { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public LlmFitEvidenceState State { get; }

    public string? CpuName { get; }

    public int? CpuLogicalProcessorCount { get; }

    public double? TotalRamGiB { get; }

    public double? AvailableRamGiB { get; }

    public LlmFitGpuDetectionState GpuState { get; }

    public int ReportedGpuCount { get; }

    public IReadOnlyList<LlmFitReportedGpu> Gpus { get; }

    public string? RawOutputSha256 { get; }

    public IReadOnlyList<LlmFitDiagnosticCode> Diagnostics { get; }

    public static LlmFitHardwareEvidence Available(
        string toolId,
        string version,
        DateTimeOffset capturedAtUtc,
        string cpuName,
        int cpuLogicalProcessorCount,
        double totalRamGiB,
        double availableRamGiB,
        LlmFitGpuDetectionState gpuState,
        IEnumerable<LlmFitReportedGpu> gpus,
        string rawOutputSha256)
    {
        ValidatePinnedIdentityAndTime(toolId, version, capturedAtUtc);
        LlmFitContractValidation.ValidateSafeName(cpuName, nameof(cpuName));
        ValidateProcessorCount(cpuLogicalProcessorCount, nameof(cpuLogicalProcessorCount));
        ValidateRam(totalRamGiB, availableRamGiB);
        ReadOnlyCollection<LlmFitReportedGpu> gpuCopy = CopyAndValidateGpus(gpuState, gpus);
        ValidateSha256(rawOutputSha256, nameof(rawOutputSha256));

        if (gpuState == LlmFitGpuDetectionState.Invalid)
        {
            throw new ArgumentException("Available evidence requires a consistent GPU state.", nameof(gpuState));
        }

        return new(
            toolId,
            version,
            capturedAtUtc,
            LlmFitEvidenceState.Available,
            cpuName,
            cpuLogicalProcessorCount,
            totalRamGiB,
            availableRamGiB,
            gpuState,
            gpuCopy,
            rawOutputSha256,
            Array.AsReadOnly(Array.Empty<LlmFitDiagnosticCode>()));
    }

    public static LlmFitHardwareEvidence Invalid(
        string toolId,
        string version,
        DateTimeOffset capturedAtUtc,
        string? cpuName,
        int? cpuLogicalProcessorCount,
        double? totalRamGiB,
        double? availableRamGiB,
        LlmFitGpuDetectionState gpuState,
        IEnumerable<LlmFitReportedGpu> gpus,
        string rawOutputSha256,
        IEnumerable<LlmFitDiagnosticCode> diagnostics)
    {
        ValidatePinnedIdentityAndTime(toolId, version, capturedAtUtc);
        if (cpuName is not null)
        {
            LlmFitContractValidation.ValidateSafeName(cpuName, nameof(cpuName));
        }

        if (cpuLogicalProcessorCount.HasValue)
        {
            ValidateProcessorCount(cpuLogicalProcessorCount.Value, nameof(cpuLogicalProcessorCount));
        }

        ValidateNullableRam(totalRamGiB, availableRamGiB);
        ReadOnlyCollection<LlmFitReportedGpu> gpuCopy = CopyAndValidateGpus(gpuState, gpus);
        ValidateSha256(rawOutputSha256, nameof(rawOutputSha256));
        ReadOnlyCollection<LlmFitDiagnosticCode> diagnosticCopy =
            CopyAndValidateDiagnostics(diagnostics, parserDiagnosticsRequired: true);

        return new(
            toolId,
            version,
            capturedAtUtc,
            LlmFitEvidenceState.Invalid,
            cpuName,
            cpuLogicalProcessorCount,
            totalRamGiB,
            availableRamGiB,
            gpuState,
            gpuCopy,
            rawOutputSha256,
            diagnosticCopy);
    }

    public static LlmFitHardwareEvidence Unavailable(
        string toolId,
        string version,
        DateTimeOffset capturedAtUtc,
        LlmFitDiagnosticCode diagnostic)
    {
        ValidateObservedIdentityAndTime(toolId, version, capturedAtUtc);
        if (!Enum.IsDefined(diagnostic))
        {
            throw new ArgumentOutOfRangeException(nameof(diagnostic));
        }

        if (IsParserDiagnostic(diagnostic))
        {
            throw new ArgumentException("Unavailable evidence requires a provider diagnostic.", nameof(diagnostic));
        }

        return new(
            toolId,
            version,
            capturedAtUtc,
            LlmFitEvidenceState.Unavailable,
            null,
            null,
            null,
            null,
            LlmFitGpuDetectionState.Invalid,
            Array.AsReadOnly(Array.Empty<LlmFitReportedGpu>()),
            null,
            Array.AsReadOnly([diagnostic]));
    }

    private static void ValidatePinnedIdentityAndTime(
        string toolId,
        string version,
        DateTimeOffset capturedAtUtc)
    {
        if (!string.Equals(toolId, LlmFitCommandContract.ToolId, StringComparison.Ordinal))
        {
            throw new ArgumentException("The evidence tool ID is not the pinned LLM Fit ID.", nameof(toolId));
        }

        if (!string.Equals(version, LlmFitCommandContract.Version, StringComparison.Ordinal))
        {
            throw new ArgumentException("The evidence version is not the pinned LLM Fit version.", nameof(version));
        }

        ValidateCaptureTime(capturedAtUtc);
    }

    private static void ValidateObservedIdentityAndTime(
        string toolId,
        string version,
        DateTimeOffset capturedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            throw new ArgumentException("The observed tool ID is required.", nameof(toolId));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("The observed tool version is required.", nameof(version));
        }

        ValidateCaptureTime(capturedAtUtc);
    }

    private static void ValidateCaptureTime(DateTimeOffset capturedAtUtc)
    {
        if (capturedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Capture time must use the UTC offset.", nameof(capturedAtUtc));
        }
    }

    private static void ValidateProcessorCount(int value, string parameterName)
    {
        if (value is < 1 or > 4096)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateRam(double totalRamGiB, double availableRamGiB)
    {
        if (!double.IsFinite(totalRamGiB) || totalRamGiB is <= 0 or > 16384)
        {
            throw new ArgumentOutOfRangeException(nameof(totalRamGiB));
        }

        if (!double.IsFinite(availableRamGiB) ||
            availableRamGiB < 0 ||
            availableRamGiB > totalRamGiB)
        {
            throw new ArgumentOutOfRangeException(nameof(availableRamGiB));
        }
    }

    private static void ValidateNullableRam(double? totalRamGiB, double? availableRamGiB)
    {
        if (totalRamGiB.HasValue &&
            (!double.IsFinite(totalRamGiB.Value) || totalRamGiB.Value is <= 0 or > 16384))
        {
            throw new ArgumentOutOfRangeException(nameof(totalRamGiB));
        }

        if (availableRamGiB.HasValue &&
            (!double.IsFinite(availableRamGiB.Value) || availableRamGiB.Value is < 0 or > 16384))
        {
            throw new ArgumentOutOfRangeException(nameof(availableRamGiB));
        }

        if (totalRamGiB.HasValue && availableRamGiB > totalRamGiB)
        {
            throw new ArgumentOutOfRangeException(nameof(availableRamGiB));
        }
    }

    private static ReadOnlyCollection<LlmFitReportedGpu> CopyAndValidateGpus(
        LlmFitGpuDetectionState gpuState,
        IEnumerable<LlmFitReportedGpu> gpus)
    {
        ArgumentNullException.ThrowIfNull(gpus);
        List<LlmFitReportedGpu> copy = new(capacity: 64);
        foreach (LlmFitReportedGpu gpu in gpus)
        {
            if (copy.Count == 64)
            {
                throw new ArgumentException("The reported GPU collection exceeds 64 entries.", nameof(gpus));
            }

            if (gpu is null)
            {
                throw new ArgumentException("The reported GPU collection contains a null entry.", nameof(gpus));
            }

            copy.Add(gpu);
        }

        if (copy.Select(static gpu => gpu.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != copy.Count)
        {
            throw new ArgumentException("Reported GPU names must be unique.", nameof(gpus));
        }

        int totalCount = copy.Sum(static gpu => gpu.Count);
        bool shapeIsValid = gpuState switch
        {
            LlmFitGpuDetectionState.Reported => copy.Count > 0 && totalCount is >= 1 and <= 64,
            LlmFitGpuDetectionState.NotReported => copy.Count == 0,
            LlmFitGpuDetectionState.Invalid => copy.Count == 0,
            _ => false,
        };

        if (!shapeIsValid)
        {
            throw new ArgumentException("GPU state and reported entries are inconsistent.", nameof(gpus));
        }

        return Array.AsReadOnly(copy.ToArray());
    }

    private static ReadOnlyCollection<LlmFitDiagnosticCode> CopyAndValidateDiagnostics(
        IEnumerable<LlmFitDiagnosticCode> diagnostics,
        bool parserDiagnosticsRequired)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        int maximumInputCount = Enum.GetValues<LlmFitDiagnosticCode>().Length;
        int inputCount = 0;
        HashSet<LlmFitDiagnosticCode> unique = [];
        foreach (LlmFitDiagnosticCode diagnostic in diagnostics)
        {
            if (++inputCount > maximumInputCount)
            {
                throw new ArgumentException("The diagnostic collection exceeds its closed bound.", nameof(diagnostics));
            }

            if (!Enum.IsDefined(diagnostic) ||
                (parserDiagnosticsRequired && !IsParserDiagnostic(diagnostic)))
            {
                throw new ArgumentException("Invalid evidence requires parser diagnostics only.", nameof(diagnostics));
            }

            unique.Add(diagnostic);
        }

        LlmFitDiagnosticCode[] copy = unique.Order().ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("Invalid evidence requires parser diagnostics only.", nameof(diagnostics));
        }

        return Array.AsReadOnly(copy);
    }

    private static void ValidateSha256(string sha256, string parameterName)
    {
        if (sha256 is null ||
            sha256.Length != 64 ||
            sha256.Any(static character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("SHA-256 must be 64 lowercase hexadecimal characters.", parameterName);
        }
    }

    private static bool IsParserDiagnostic(LlmFitDiagnosticCode diagnostic) =>
        diagnostic is >= LlmFitDiagnosticCode.JsonInvalid and <= LlmFitDiagnosticCode.GpuInconsistent;
}

internal static class LlmFitContractValidation
{
    internal static void ValidateSafeName(string name, string parameterName)
        => HardwareText.Validate(name, 256, parameterName);

    internal static bool IsSafeName(string? name) => HardwareText.IsSafe(name, 256);
}
