using System.Collections.ObjectModel;

namespace HardwareInspection.LlmFitSpike.Inspection;

public sealed record LlmFitSystemAssessment(
    bool JsonValid,
    bool RequiredCpuRamPresent,
    bool CpuNamePresent,
    string? CpuName,
    int? CpuLogicalProcessorCount,
    double? TotalRamGiB,
    double? AvailableRamGiB,
    bool GpuReported,
    int ReportedGpuCount,
    bool IntelGpuReported,
    bool DedicatedSharedMemorySemanticsEstablished,
    string IntelNpuDetectionState,
    string RawJsonSha256,
    IReadOnlyList<string> DiagnosticCodes)
{
    private readonly IReadOnlyList<string> diagnosticCodes = CopyDiagnostics(DiagnosticCodes);

    public IReadOnlyList<string> DiagnosticCodes
    {
        get => diagnosticCodes;
        init => diagnosticCodes = CopyDiagnostics(value);
    }

    public bool Gate1SchemaPassed => JsonValid && RequiredCpuRamPresent;

    private static ReadOnlyCollection<string> CopyDiagnostics(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new ReadOnlyCollection<string>(values.ToArray());
    }
}
