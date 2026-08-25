namespace GraniteEdgeAI.HardwareInspection.Foundation.Windows;

internal enum WindowsStorageApiStatus
{
    Success,
    SystemDirectoryUnavailable,
    InvalidVolumeRoot,
    DiskInformationUnavailable,
    NativeApiUnavailable,
}

internal sealed record WindowsStorageApiResult(
    WindowsStorageApiStatus Status,
    ulong CapacityBytes,
    ulong AvailableToCallerBytes);

internal interface IWindowsStorageApi
{
    WindowsStorageApiResult Capture();
}

public sealed class WindowsStorageEvidenceProvider
{
    private readonly IWindowsStorageApi _storageApi;
    private readonly TimeProvider _timeProvider;

    public WindowsStorageEvidenceProvider()
        : this(new Kernel32WindowsStorageApi(), TimeProvider.System)
    {
    }

    internal WindowsStorageEvidenceProvider(
        IWindowsStorageApi storageApi,
        TimeProvider timeProvider)
    {
        _storageApi = storageApi ?? throw new ArgumentNullException(nameof(storageApi));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public ValueTask<WindowsStorageEvidence> CaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WindowsStorageApiResult result = _storageApi.Capture();
        DateTimeOffset capturedAtUtc = _timeProvider.GetUtcNow().ToUniversalTime();

        if (result.Status != WindowsStorageApiStatus.Success)
        {
            return ValueTask.FromResult(WindowsStorageEvidence.Unavailable(
                MapFailure(result.Status),
                capturedAtUtc));
        }

        if (result.CapacityBytes == 0 || result.AvailableToCallerBytes > result.CapacityBytes)
        {
            return ValueTask.FromResult(WindowsStorageEvidence.Unavailable(
                WindowsStorageDiagnosticCode.InconsistentValues,
                capturedAtUtc));
        }

        return ValueTask.FromResult(WindowsStorageEvidence.Available(
            result.CapacityBytes,
            result.AvailableToCallerBytes,
            capturedAtUtc));
    }

    private static WindowsStorageDiagnosticCode MapFailure(WindowsStorageApiStatus status) =>
        status switch
        {
            WindowsStorageApiStatus.SystemDirectoryUnavailable =>
                WindowsStorageDiagnosticCode.SystemDirectoryUnavailable,
            WindowsStorageApiStatus.InvalidVolumeRoot =>
                WindowsStorageDiagnosticCode.InvalidVolumeRoot,
            WindowsStorageApiStatus.DiskInformationUnavailable =>
                WindowsStorageDiagnosticCode.DiskInformationUnavailable,
            WindowsStorageApiStatus.NativeApiUnavailable =>
                WindowsStorageDiagnosticCode.NativeApiUnavailable,
            _ => WindowsStorageDiagnosticCode.NativeApiUnavailable,
        };
}
