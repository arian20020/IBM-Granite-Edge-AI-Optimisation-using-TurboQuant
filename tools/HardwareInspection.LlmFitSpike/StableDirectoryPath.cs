using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace HardwareInspection.LlmFitSpike;

internal sealed record StableDirectoryIdentity(
    string FinalPath,
    uint VolumeSerialNumber,
    ulong FileId)
{
    internal bool IsSameDirectory(StableDirectoryIdentity other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return VolumeSerialNumber == other.VolumeSerialNumber && FileId == other.FileId;
    }
}

internal static class StableDirectoryPath
{
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileReadAttributes = 0x00000080;
    private const int MaximumWindowsPathCharacters = 32_768;
    private const uint OpenExisting = 3;
    private const uint ShareDelete = 0x00000004;
    private const uint ShareRead = 0x00000001;
    private const uint ShareWrite = 0x00000002;

    internal static StableDirectoryIdentity Resolve(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        using SafeFileHandle handle = OpenDirectory(fullPath);
        ByHandleFileInformation information = GetInformation(handle);
        if ((information.FileAttributes & FileAttributes.Directory) == 0 ||
            (information.FileAttributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("The stable path must identify an ordinary directory.");
        }

        return new StableDirectoryIdentity(
            GetFinalPath(handle),
            information.VolumeSerialNumber,
            ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow);
    }

    private static SafeFileHandle OpenDirectory(string directory)
    {
        SafeFileHandle handle = CreateFile(
            directory,
            FileReadAttributes,
            ShareRead | ShareWrite | ShareDelete,
            0,
            OpenExisting,
            FileFlagBackupSemantics,
            0);
        if (handle.IsInvalid)
        {
            int errorCode = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new IOException(
                "The directory identity could not be opened safely.",
                new Win32Exception(errorCode));
        }

        return handle;
    }

    private static ByHandleFileInformation GetInformation(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information))
        {
            throw new IOException(
                "The directory identity could not be read safely.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        return information;
    }

    private static string GetFinalPath(SafeFileHandle handle)
    {
        var pathBuffer = new StringBuilder(MaximumWindowsPathCharacters);
        uint characterCount = GetFinalPathNameByHandle(
            handle,
            pathBuffer,
            (uint)pathBuffer.Capacity,
            0);
        if (characterCount == 0 || characterCount >= pathBuffer.Capacity)
        {
            throw new IOException(
                "The directory path could not be resolved safely.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        string finalPath = pathBuffer.ToString();
        if (finalPath.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return Path.TrimEndingDirectorySeparator(@"\\" + finalPath[8..]);
        }

        return Path.TrimEndingDirectorySeparator(
            finalPath.StartsWith(@"\\?\", StringComparison.Ordinal)
                ? finalPath[4..]
                : finalPath);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public FileAttributes FileAttributes;
        public uint CreationTimeLow;
        public uint CreationTimeHigh;
        public uint LastAccessTimeLow;
        public uint LastAccessTimeHigh;
        public uint LastWriteTimeLow;
        public uint LastWriteTimeHigh;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

#pragma warning disable SYSLIB1054 // Project policy forbids unsafe LibraryImport buffers.
#pragma warning disable CA1838 // Unsafe character buffers are forbidden by project policy.
    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateFileW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        nint securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "GetFileInformationByHandle",
        ExactSpelling = true,
        SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation fileInformation);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "GetFinalPathNameByHandleW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle file,
        StringBuilder filePath,
        uint filePathLength,
        uint flags);
#pragma warning restore CA1838
#pragma warning restore SYSLIB1054
}
