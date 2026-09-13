using System.Collections.ObjectModel;

namespace GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;

internal sealed class LlmFitSystemParseResult
{
    internal LlmFitSystemParseResult(
        LlmFitEvidenceState state,
        string? cpuName,
        int? cpuLogicalProcessorCount,
        double? totalRamGiB,
        double? availableRamGiB,
        LlmFitGpuDetectionState gpuState,
        IEnumerable<LlmFitReportedGpu> gpus,
        string rawOutputSha256,
        IEnumerable<LlmFitDiagnosticCode> diagnostics)
    {
        LlmFitReportedGpu[] gpuCopy = gpus.ToArray();
        LlmFitDiagnosticCode[] diagnosticCopy = diagnostics.Distinct().Order().ToArray();

        State = state;
        CpuName = cpuName;
        CpuLogicalProcessorCount = cpuLogicalProcessorCount;
        TotalRamGiB = totalRamGiB;
        AvailableRamGiB = availableRamGiB;
        GpuState = gpuState;
        Gpus = Array.AsReadOnly(gpuCopy);
        ReportedGpuCount = gpuCopy.Sum(static gpu => gpu.Count);
        RawOutputSha256 = rawOutputSha256;
        Diagnostics = Array.AsReadOnly(diagnosticCopy);
    }

    internal LlmFitEvidenceState State { get; }

    internal string? CpuName { get; }

    internal int? CpuLogicalProcessorCount { get; }

    internal double? TotalRamGiB { get; }

    internal double? AvailableRamGiB { get; }

    internal LlmFitGpuDetectionState GpuState { get; }

    internal int ReportedGpuCount { get; }

    internal ReadOnlyCollection<LlmFitReportedGpu> Gpus { get; }

    internal string RawOutputSha256 { get; }

    internal ReadOnlyCollection<LlmFitDiagnosticCode> Diagnostics { get; }
}
