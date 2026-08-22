using System.Runtime.InteropServices;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Windows;

internal interface IWindowsMemoryApi
{
    bool TryGetPhysicallyInstalledKilobytes(out ulong value);

    bool TryGetMemoryStatus(out ulong totalPhysicalBytes, out ulong availablePhysicalBytes);
}

internal interface IWindowsOperatingSystemInfo
{
    (string Name, string Version, string Architecture) Capture();
}

public sealed class WindowsSystemSnapshotProvider
{
    internal const string MemoryUnavailableCode = "HI-WINDOWS-MEMORY-UNAVAILABLE";
    internal const string MemoryOverflowCode = "HI-WINDOWS-MEMORY-OVERFLOW";
    internal const string MemoryInconsistentCode = "HI-WINDOWS-MEMORY-INCONSISTENT";

    private readonly IWindowsMemoryApi _memoryApi;
    private readonly TimeProvider _timeProvider;
    private readonly IWindowsOperatingSystemInfo _operatingSystemInfo;

    public WindowsSystemSnapshotProvider()
        : this(
            new Kernel32WindowsMemoryApi(),
            TimeProvider.System,
            new RuntimeOperatingSystemInfo())
    {
    }

    internal WindowsSystemSnapshotProvider(
        IWindowsMemoryApi memoryApi,
        TimeProvider timeProvider,
        IWindowsOperatingSystemInfo operatingSystemInfo)
    {
        _memoryApi = memoryApi ?? throw new ArgumentNullException(nameof(memoryApi));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _operatingSystemInfo = operatingSystemInfo ??
            throw new ArgumentNullException(nameof(operatingSystemInfo));
    }

    public ValueTask<WindowsSystemSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_memoryApi.TryGetPhysicallyInstalledKilobytes(out ulong installedKilobytes) ||
            !_memoryApi.TryGetMemoryStatus(out ulong usableBytes, out ulong availableBytes))
        {
            throw new WindowsSystemSnapshotException(MemoryUnavailableCode);
        }

        ulong installedBytes;
        try
        {
            installedBytes = checked(installedKilobytes * 1024UL);
        }
        catch (OverflowException)
        {
            throw new WindowsSystemSnapshotException(MemoryOverflowCode);
        }

        if (installedBytes < usableBytes || usableBytes < availableBytes)
        {
            throw new WindowsSystemSnapshotException(MemoryInconsistentCode);
        }

        (string name, string version, string architecture) = _operatingSystemInfo.Capture();
        WindowsSystemSnapshot snapshot = new(
            installedBytes,
            usableBytes,
            availableBytes,
            _timeProvider.GetUtcNow(),
            name,
            version,
            architecture);
        return ValueTask.FromResult(snapshot);
    }

    private sealed class RuntimeOperatingSystemInfo : IWindowsOperatingSystemInfo
    {
        public (string Name, string Version, string Architecture) Capture() =>
            (
                RuntimeInformation.OSDescription.Trim(),
                Environment.OSVersion.Version.ToString(),
                RuntimeInformation.OSArchitecture.ToString());
    }
}
