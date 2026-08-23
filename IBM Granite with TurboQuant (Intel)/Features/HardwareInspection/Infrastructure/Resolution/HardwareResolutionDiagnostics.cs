using System;

namespace GraniteEdgeAI.Features.HardwareInspection.Resolution;

internal enum HardwareResolutionDiagnosticCode
{
    ClockFuture,
    EvidenceStale,
    CaptureSpanExceeded,
    NormalizationInvalid,
    NormalizationOverflow,
    LlmFitUnavailable,
    WindowsProcessorUnavailable,
    WindowsSystemUnavailable,
    StorageUnavailable,
    DxgiUnavailable,
    GraphicsUnresolved,
    NeuralProcessorUnavailable,
    LlamaCppUnavailable,
    ProcessorNameConflict,
    LogicalProcessorConflict,
    ProcessorTopologyConflict,
    TotalMemoryConflict,
    AvailableMemoryConflict,
    InstructionSetsUnavailable,
}

internal static class HardwareResolutionDiagnosticTokens
{
    internal static string Get(HardwareResolutionDiagnosticCode diagnostic) => diagnostic switch
    {
        HardwareResolutionDiagnosticCode.ClockFuture => "clock.future",
        HardwareResolutionDiagnosticCode.EvidenceStale => "evidence.stale",
        HardwareResolutionDiagnosticCode.CaptureSpanExceeded => "capture-span.exceeded",
        HardwareResolutionDiagnosticCode.NormalizationInvalid => "normalization.invalid",
        HardwareResolutionDiagnosticCode.NormalizationOverflow => "normalization.overflow",
        HardwareResolutionDiagnosticCode.LlmFitUnavailable => "llmfit.unavailable",
        HardwareResolutionDiagnosticCode.WindowsProcessorUnavailable => "windows-processor.unavailable",
        HardwareResolutionDiagnosticCode.WindowsSystemUnavailable => "windows-system.unavailable",
        HardwareResolutionDiagnosticCode.StorageUnavailable => "storage.unavailable",
        HardwareResolutionDiagnosticCode.DxgiUnavailable => "dxgi.unavailable",
        HardwareResolutionDiagnosticCode.GraphicsUnresolved => "graphics.unresolved",
        HardwareResolutionDiagnosticCode.NeuralProcessorUnavailable => "neural-processor.unavailable",
        HardwareResolutionDiagnosticCode.LlamaCppUnavailable => "llama-cpp.unavailable",
        HardwareResolutionDiagnosticCode.ProcessorNameConflict => "processor-name.conflict",
        HardwareResolutionDiagnosticCode.LogicalProcessorConflict => "logical-processors.conflict",
        HardwareResolutionDiagnosticCode.ProcessorTopologyConflict => "processor-topology.conflict",
        HardwareResolutionDiagnosticCode.TotalMemoryConflict => "total-memory.conflict",
        HardwareResolutionDiagnosticCode.AvailableMemoryConflict => "available-memory.conflict",
        HardwareResolutionDiagnosticCode.InstructionSetsUnavailable => "instruction-sets.unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(diagnostic)),
    };
}
