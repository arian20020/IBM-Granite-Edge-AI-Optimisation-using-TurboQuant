using System.Collections.ObjectModel;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Windows;

public enum WindowsStorageEvidenceState
{
    Available,
    Unavailable,
}

public enum WindowsStorageDiagnosticCode
{
    SystemDirectoryUnavailable,
    InvalidVolumeRoot,
    DiskInformationUnavailable,
    InconsistentValues,
    NativeApiUnavailable,
}

public sealed class WindowsStorageEvidence
{
    private WindowsStorageEvidence(
        WindowsStorageEvidenceState state,
        ulong? capacityBytes,
        ulong? availableToCallerBytes,
        DateTimeOffset capturedAtUtc,
        IReadOnlyList<WindowsStorageDiagnosticCode> diagnostics)
    {
        State = state;
        CapacityBytes = capacityBytes;
        AvailableToCallerBytes = availableToCallerBytes;
        CapturedAtUtc = capturedAtUtc;
        Diagnostics = diagnostics;
    }

    public WindowsStorageEvidenceState State { get; }

    public ulong? CapacityBytes { get; }

    public ulong? AvailableToCallerBytes { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public IReadOnlyList<WindowsStorageDiagnosticCode> Diagnostics { get; }

    public static WindowsStorageEvidence Available(
        ulong capacityBytes,
        ulong availableToCallerBytes,
        DateTimeOffset capturedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfZero(capacityBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(availableToCallerBytes, capacityBytes);

        ValidateCaptureTime(capturedAtUtc);
        return new(
            WindowsStorageEvidenceState.Available,
            capacityBytes,
            availableToCallerBytes,
            capturedAtUtc,
            EmptyDiagnostics());
    }

    public static WindowsStorageEvidence Unavailable(
        WindowsStorageDiagnosticCode diagnostic,
        DateTimeOffset capturedAtUtc)
    {
        if (!Enum.IsDefined(diagnostic))
        {
            throw new ArgumentOutOfRangeException(nameof(diagnostic));
        }

        ValidateCaptureTime(capturedAtUtc);
        return new(
            WindowsStorageEvidenceState.Unavailable,
            null,
            null,
            capturedAtUtc,
            Array.AsReadOnly([diagnostic]));
    }

    private static ReadOnlyCollection<WindowsStorageDiagnosticCode> EmptyDiagnostics() =>
        Array.AsReadOnly(Array.Empty<WindowsStorageDiagnosticCode>());

    private static void ValidateCaptureTime(DateTimeOffset capturedAtUtc)
    {
        if (capturedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Capture time must use the UTC offset.", nameof(capturedAtUtc));
        }
    }
}
