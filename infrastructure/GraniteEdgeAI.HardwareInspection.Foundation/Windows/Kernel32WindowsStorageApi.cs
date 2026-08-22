using System.Runtime.InteropServices;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Windows;

internal interface IKernel32WindowsStorageNative
{
    uint GetSystemWindowsDirectory(char[] buffer, uint capacityCharacters);

    bool TryGetDiskSpace(
        string volumeRoot,
        out ulong availableToCallerBytes,
        out ulong capacityBytes);
}

internal sealed class Kernel32WindowsStorageApi : IWindowsStorageApi
{
    internal const uint MaximumSystemDirectoryCharacters = 32_768;

    private readonly IKernel32WindowsStorageNative _native;

    internal Kernel32WindowsStorageApi()
        : this(new Kernel32WindowsStorageNative())
    {
    }

    internal Kernel32WindowsStorageApi(IKernel32WindowsStorageNative native)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public WindowsStorageApiResult Capture()
    {
        char[] systemDirectory = new char[MaximumSystemDirectoryCharacters];
        uint copiedLength = _native.GetSystemWindowsDirectory(
            systemDirectory,
            MaximumSystemDirectoryCharacters);
        if (copiedLength == 0 || copiedLength >= MaximumSystemDirectoryCharacters)
        {
            return Failure(WindowsStorageApiStatus.SystemDirectoryUnavailable);
        }

        string? volumeRoot;
        try
        {
            volumeRoot = Path.GetPathRoot(new string(systemDirectory, 0, checked((int)copiedLength)));
        }
        catch (ArgumentException)
        {
            return Failure(WindowsStorageApiStatus.InvalidVolumeRoot);
        }

        if (string.IsNullOrEmpty(volumeRoot) || !Path.IsPathFullyQualified(volumeRoot))
        {
            return Failure(WindowsStorageApiStatus.InvalidVolumeRoot);
        }

        if (!_native.TryGetDiskSpace(
            volumeRoot,
            out ulong availableToCallerBytes,
            out ulong capacityBytes))
        {
            return Failure(WindowsStorageApiStatus.DiskInformationUnavailable);
        }

        return new(WindowsStorageApiStatus.Success, capacityBytes, availableToCallerBytes);
    }

    private static WindowsStorageApiResult Failure(WindowsStorageApiStatus status) =>
        new(status, 0, 0);

    private sealed class Kernel32WindowsStorageNative : IKernel32WindowsStorageNative
    {
        public uint GetSystemWindowsDirectory(char[] buffer, uint capacityCharacters) =>
            NativeGetSystemWindowsDirectory(buffer, capacityCharacters);

        public bool TryGetDiskSpace(
            string volumeRoot,
            out ulong availableToCallerBytes,
            out ulong capacityBytes) =>
            GetDiskFreeSpaceEx(
                volumeRoot,
                out availableToCallerBytes,
                out capacityBytes,
                out _);

        [DllImport(
            "kernel32.dll",
            EntryPoint = "GetSystemWindowsDirectoryW",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern uint NativeGetSystemWindowsDirectory(
            [Out] char[] buffer,
            uint size);

        [DllImport(
            "kernel32.dll",
            EntryPoint = "GetDiskFreeSpaceExW",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetDiskFreeSpaceEx(
            string directoryName,
            out ulong freeBytesAvailableToCaller,
            out ulong totalNumberOfBytes,
            out ulong totalNumberOfFreeBytes);
    }
}
