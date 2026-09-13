using System.Collections.ObjectModel;
using GraniteEdgeAI.HardwareInspection.Foundation.Validation;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Windows;

public enum WindowsProcessorEvidenceState
{
    Available,
    Unavailable,
}

public enum WindowsProcessorArchitecture
{
    X86,
    X64,
    Arm64,
}

public enum WindowsProcessorDiagnosticCode
{
    NameUnavailable,
    TopologyUnavailable,
    TopologyInconsistent,
    UnsupportedArchitecture,
    NativeApiUnavailable,
}

public sealed class WindowsProcessorEvidence
{
    private WindowsProcessorEvidence(
        WindowsProcessorEvidenceState state,
        string? name,
        WindowsProcessorArchitecture? architecture,
        int? physicalCoreCount,
        int? logicalProcessorCount,
        DateTimeOffset capturedAtUtc,
        IReadOnlyList<WindowsProcessorDiagnosticCode> diagnostics)
    {
        State = state;
        Name = name;
        Architecture = architecture;
        PhysicalCoreCount = physicalCoreCount;
        LogicalProcessorCount = logicalProcessorCount;
        CapturedAtUtc = capturedAtUtc;
        Diagnostics = diagnostics;
    }

    public WindowsProcessorEvidenceState State { get; }

    public string? Name { get; }

    public WindowsProcessorArchitecture? Architecture { get; }

    public int? PhysicalCoreCount { get; }

    public int? LogicalProcessorCount { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public IReadOnlyList<WindowsProcessorDiagnosticCode> Diagnostics { get; }

    public static WindowsProcessorEvidence Available(
        string name,
        WindowsProcessorArchitecture architecture,
        int physicalCoreCount,
        int logicalProcessorCount,
        DateTimeOffset capturedAtUtc)
    {
        HardwareText.Validate(name, 256, nameof(name));
        if (!Enum.IsDefined(architecture))
        {
            throw new ArgumentOutOfRangeException(nameof(architecture));
        }

        if (physicalCoreCount is < 1 or > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalCoreCount));
        }

        if (logicalProcessorCount is < 1 or > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalProcessorCount));
        }

        if (physicalCoreCount > logicalProcessorCount)
        {
            throw new ArgumentException("Physical cores cannot exceed logical processors.");
        }

        ValidateCaptureTime(capturedAtUtc);
        return new(
            WindowsProcessorEvidenceState.Available,
            name,
            architecture,
            physicalCoreCount,
            logicalProcessorCount,
            capturedAtUtc,
            EmptyDiagnostics());
    }

    public static WindowsProcessorEvidence Unavailable(
        WindowsProcessorDiagnosticCode diagnostic,
        DateTimeOffset capturedAtUtc)
    {
        if (!Enum.IsDefined(diagnostic))
        {
            throw new ArgumentOutOfRangeException(nameof(diagnostic));
        }

        ValidateCaptureTime(capturedAtUtc);
        return new(
            WindowsProcessorEvidenceState.Unavailable,
            null,
            null,
            null,
            null,
            capturedAtUtc,
            Array.AsReadOnly([diagnostic]));
    }

    private static ReadOnlyCollection<WindowsProcessorDiagnosticCode> EmptyDiagnostics() =>
        Array.AsReadOnly(Array.Empty<WindowsProcessorDiagnosticCode>());

    private static void ValidateCaptureTime(DateTimeOffset capturedAtUtc)
    {
        if (capturedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Capture time must use the UTC offset.", nameof(capturedAtUtc));
        }
    }
}
