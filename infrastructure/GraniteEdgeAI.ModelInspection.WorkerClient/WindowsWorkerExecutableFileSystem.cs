using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Implements executable verification against Windows file handles and the
/// Portable Executable header. Raw paths are never included in public errors.
/// </summary>
internal sealed class WindowsWorkerExecutableFileSystem :
    IWorkerExecutableFileSystem
{
    private const uint GenericReadAttributes = 0x00000080;
    private const uint ShareRead = 0x00000001;
    private const uint ShareWrite = 0x00000002;
    private const uint ShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;
    private const int DosHeaderPeOffset = 0x3c;
    private const uint PortableExecutableSignature = 0x00004550;

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public FileAttributes GetAttributes(string path) =>
        File.GetAttributes(path);

    public ushort ReadPortableExecutableMachine(string path)
    {
        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.RandomAccess);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);

        if (stream.Length < DosHeaderPeOffset + sizeof(int))
        {
            throw new InvalidDataException(
                "The worker executable has an incomplete DOS header.");
        }

        if (reader.ReadUInt16() != 0x5A4D)
        {
            throw new InvalidDataException(
                "The worker executable does not have an MZ header.");
        }

        stream.Position = DosHeaderPeOffset;
        int peOffset = reader.ReadInt32();
        if (peOffset < 0 || peOffset > stream.Length - 6)
        {
            throw new InvalidDataException(
                "The worker executable has an invalid PE offset.");
        }

        stream.Position = peOffset;
        if (reader.ReadUInt32() != PortableExecutableSignature)
        {
            throw new InvalidDataException(
                "The worker executable does not have a PE signature.");
        }

        return reader.ReadUInt16();
    }

    public SafeFileHandle OpenReadHandle(string path) =>
        File.OpenHandle(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileOptions.RandomAccess);

    public string GetFinalDirectoryPath(string path)
    {
        SafeFileHandle handle = NativeMethods.CreateFile(
            path,
            GenericReadAttributes,
            ShareRead | ShareWrite | ShareDelete,
            IntPtr.Zero,
            OpenExisting,
            BackupSemantics,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            throw new Win32Exception(error);
        }

        using (handle)
        {
            return GetFinalPath(handle);
        }
    }

    public string GetFinalFilePath(SafeFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        return GetFinalPath(handle);
    }

    private static string GetFinalPath(SafeFileHandle handle)
    {
        char[] buffer = new char[512];

        while (true)
        {
            uint length = NativeMethods.GetFinalPathNameByHandle(
                handle,
                buffer,
                checked((uint)buffer.Length),
                0);
            if (length == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            if (length < buffer.Length)
            {
                return new string(buffer, 0, checked((int)length));
            }

            buffer = new char[checked((int)length + 1)];
        }
    }

    private static class NativeMethods
    {
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(
            "kernel32.dll",
            EntryPoint = "CreateFileW",
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport(
            "kernel32.dll",
            EntryPoint = "GetFinalPathNameByHandleW",
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            SetLastError = true)]
        internal static extern uint GetFinalPathNameByHandle(
            SafeFileHandle file,
            [Out] char[] filePath,
            uint filePathLength,
            uint flags);
    }
}
