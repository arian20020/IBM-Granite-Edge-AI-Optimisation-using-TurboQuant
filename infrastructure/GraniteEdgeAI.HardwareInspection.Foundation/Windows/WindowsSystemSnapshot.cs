using GraniteEdgeAI.HardwareInspection.Foundation.Validation;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Windows;

public enum WindowsSystemSnapshotDiagnosticCode
{
    MemoryUnavailable,
    MemoryOverflow,
    MemoryInconsistent,
}

public sealed class WindowsSystemSnapshot
{
    public WindowsSystemSnapshot(
        ulong physicallyInstalledBytes,
        ulong osUsablePhysicalBytes,
        ulong availablePhysicalBytes,
        DateTimeOffset capturedAtUtc,
        string operatingSystemName,
        string operatingSystemVersion,
        string operatingSystemArchitecture)
    {
        if (physicallyInstalledBytes < osUsablePhysicalBytes ||
            osUsablePhysicalBytes < availablePhysicalBytes)
        {
            throw new ArgumentException("Physical memory values are inconsistent.");
        }

        if (capturedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Capture time must be UTC.", nameof(capturedAtUtc));
        }

        HardwareText.Validate(operatingSystemName, 256, nameof(operatingSystemName));
        HardwareText.Validate(operatingSystemVersion, 256, nameof(operatingSystemVersion));
        HardwareText.Validate(operatingSystemArchitecture, 256, nameof(operatingSystemArchitecture));

        PhysicallyInstalledBytes = physicallyInstalledBytes;
        OsUsablePhysicalBytes = osUsablePhysicalBytes;
        AvailablePhysicalBytes = availablePhysicalBytes;
        CapturedAtUtc = capturedAtUtc;
        OperatingSystemName = operatingSystemName;
        OperatingSystemVersion = operatingSystemVersion;
        OperatingSystemArchitecture = operatingSystemArchitecture;
    }

    public ulong PhysicallyInstalledBytes { get; }

    public ulong OsUsablePhysicalBytes { get; }

    public ulong AvailablePhysicalBytes { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public string OperatingSystemName { get; }

    public string OperatingSystemVersion { get; }

    public string OperatingSystemArchitecture { get; }
}

public sealed class WindowsSystemSnapshotException : InvalidOperationException
{
    internal WindowsSystemSnapshotException(string diagnosticCode)
        : base(GetSafeMessage(diagnosticCode))
    {
        DiagnosticCode = diagnosticCode;
        ClosedDiagnostic = GetClosedDiagnostic(diagnosticCode);
    }

    public string DiagnosticCode { get; }

    public WindowsSystemSnapshotDiagnosticCode ClosedDiagnostic { get; }

    private static WindowsSystemSnapshotDiagnosticCode GetClosedDiagnostic(
        string diagnosticCode) => diagnosticCode switch
        {
            WindowsSystemSnapshotProvider.MemoryUnavailableCode =>
                WindowsSystemSnapshotDiagnosticCode.MemoryUnavailable,
            WindowsSystemSnapshotProvider.MemoryOverflowCode =>
                WindowsSystemSnapshotDiagnosticCode.MemoryOverflow,
            WindowsSystemSnapshotProvider.MemoryInconsistentCode =>
                WindowsSystemSnapshotDiagnosticCode.MemoryInconsistent,
            _ => throw new ArgumentOutOfRangeException(nameof(diagnosticCode)),
        };

    private static string GetSafeMessage(string diagnosticCode) => diagnosticCode switch
    {
        WindowsSystemSnapshotProvider.MemoryUnavailableCode =>
            "Windows memory information is unavailable.",
        WindowsSystemSnapshotProvider.MemoryOverflowCode =>
            "Windows memory information exceeded the supported range.",
        WindowsSystemSnapshotProvider.MemoryInconsistentCode =>
            "Windows memory information is internally inconsistent.",
        _ => "Windows system information is unavailable.",
    };
}
