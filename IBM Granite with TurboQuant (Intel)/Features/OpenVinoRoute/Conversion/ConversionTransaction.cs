using System.ComponentModel;
using System.Runtime.InteropServices;
using GraniteEdgeAI.OpenVino.Contracts;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Conversion;

public sealed class ConversionTransactionException : Exception
{
    public ConversionTransactionException(OpenVinoSupportCode supportCode)
        : base(supportCode.ToProtocolValue()) =>
        SupportCode = supportCode;

    public ConversionTransactionException(
        OpenVinoSupportCode supportCode,
        Exception innerException)
        : base(supportCode.ToProtocolValue(), innerException) =>
        SupportCode = supportCode;

    public OpenVinoSupportCode SupportCode { get; }
}

/// <summary>
/// Owns one absent-destination, sibling-staging publication. Cleanup is allowed
/// only while the current staging path still resolves to the directory identity
/// retained at creation.
/// </summary>
public sealed class ConversionTransaction : IDisposable
{
    private const uint FileReadAttributes = 0x00000080;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;

    private SafeFileHandle? stagingLease;
    private readonly DirectoryIdentity stagingIdentity;
    private bool disposed;

    private ConversionTransaction(
        Guid operationId,
        string sourceDirectory,
        string destinationDirectory,
        string stagingDirectory,
        SafeFileHandle stagingLease,
        DirectoryIdentity stagingIdentity)
    {
        OperationId = operationId;
        SourceDirectory = sourceDirectory;
        DestinationDirectory = destinationDirectory;
        StagingDirectory = stagingDirectory;
        this.stagingLease = stagingLease;
        this.stagingIdentity = stagingIdentity;
    }

    public Guid OperationId { get; }
    public string SourceDirectory { get; }
    public string DestinationDirectory { get; }
    public string StagingDirectory { get; }
    public bool IsPublished { get; private set; }

    public static ConversionTransaction Create(
        string sourceDirectory,
        string destinationDirectory,
        Guid operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("An operation identity is required.", nameof(operationId));
        }

        string source = Path.GetFullPath(sourceDirectory);
        string destination = Path.GetFullPath(destinationDirectory);
        string? destinationParent = Path.GetDirectoryName(destination);
        if (!Directory.Exists(source) || string.IsNullOrWhiteSpace(destinationParent) ||
            !Directory.Exists(destinationParent) ||
            Directory.Exists(destination) || File.Exists(destination))
        {
            throw Preflight();
        }

        RejectReparseAncestry(source);
        RejectReparseAncestry(destinationParent);
        string sourceFinal = GetFinalDirectoryPath(source);
        string parentFinal = GetFinalDirectoryPath(destinationParent);
        string destinationFinal = Path.Combine(parentFinal, Path.GetFileName(destination));
        if (PathsOverlap(source, destination) || PathsOverlap(sourceFinal, destinationFinal))
        {
            throw Preflight();
        }

        string staging = Path.Combine(
            destinationParent,
            $".granite-openvino-{operationId:N}.staging");
        if (Directory.Exists(staging) || File.Exists(staging))
        {
            throw Preflight();
        }

        SafeFileHandle? lease = null;
        try
        {
            Directory.CreateDirectory(staging);
            lease = OpenDirectory(staging);
            DirectoryIdentity identity = GetIdentity(lease);
            if ((identity.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw Preflight();
            }
            return new ConversionTransaction(
                operationId,
                source,
                destination,
                staging,
                lease,
                identity);
        }
        catch (ConversionTransactionException)
        {
            lease?.Dispose();
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: false);
            }
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception)
        {
            lease?.Dispose();
            if (Directory.Exists(staging))
            {
                try { Directory.Delete(staging, recursive: false); } catch { }
            }
            throw new ConversionTransactionException(
                OpenVinoSupportCode.ConversionPreflightFailed,
                exception);
        }
    }

    public void Publish()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsPublished || Directory.Exists(DestinationDirectory) ||
            File.Exists(DestinationDirectory) || !RetainedStagingIsCurrent())
        {
            throw new ConversionTransactionException(
                OpenVinoSupportCode.ConversionPublishFailed);
        }

        try
        {
            Directory.Move(StagingDirectory, DestinationDirectory);
            IsPublished = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ConversionTransactionException(
                OpenVinoSupportCode.ConversionPublishFailed,
                exception);
        }
    }

    /// <summary>
    /// Reverses publication only when the destination still names the exact
    /// directory created by this transaction. The content remains in the
    /// operation-owned staging directory so normal cleanup can remove it.
    /// </summary>
    public bool TryRollbackPublished()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!IsPublished || Directory.Exists(StagingDirectory) ||
            File.Exists(StagingDirectory) || !RetainedDirectoryIsCurrent(DestinationDirectory))
        {
            return false;
        }

        try
        {
            Directory.Move(DestinationDirectory, StagingDirectory);
            IsPublished = false;
            return RetainedStagingIsCurrent();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool TryCleanup()
    {
        if (IsPublished || !RetainedStagingIsCurrent())
        {
            return false;
        }

        stagingLease?.Dispose();
        stagingLease = null;
        try
        {
            Directory.Delete(StagingDirectory, recursive: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        _ = TryCleanup();
        stagingLease?.Dispose();
        stagingLease = null;
        disposed = true;
    }

    private bool RetainedStagingIsCurrent()
        => RetainedDirectoryIsCurrent(StagingDirectory);

    private bool RetainedDirectoryIsCurrent(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }
        try
        {
            using SafeFileHandle current = OpenDirectory(path);
            DirectoryIdentity identity = GetIdentity(current);
            return identity.SameObject(stagingIdentity) &&
                (identity.Attributes & FileAttributes.ReparsePoint) == 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception)
        {
            return false;
        }
    }

    private static void RejectReparseAncestry(string path)
    {
        DirectoryInfo? current = new(path);
        while (current is not null)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw Preflight();
            }
            current = current.Parent;
        }
    }

    private static bool PathsOverlap(string left, string right)
    {
        string normalizedLeft = left.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedRight = right.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        string leftPrefix = normalizedLeft + Path.DirectorySeparatorChar;
        string rightPrefix = normalizedRight + Path.DirectorySeparatorChar;
        return normalizedRight.StartsWith(leftPrefix, StringComparison.OrdinalIgnoreCase) ||
            normalizedLeft.StartsWith(rightPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetFinalDirectoryPath(string path)
    {
        using SafeFileHandle handle = OpenDirectory(path);
        DirectoryIdentity identity = GetIdentity(handle);
        if ((identity.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw Preflight();
        }
        char[] buffer = new char[512];
        uint length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
        if (length >= buffer.Length)
        {
            buffer = new char[checked((int)length + 1)];
            length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
        }
        if (length == 0 || length >= buffer.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        string value = new(buffer, 0, checked((int)length));
        const string uncPrefix = @"\\?\UNC\";
        const string extendedPrefix = @"\\?\";
        return value.StartsWith(uncPrefix, StringComparison.OrdinalIgnoreCase)
            ? @"\\" + value[uncPrefix.Length..]
            : value.StartsWith(extendedPrefix, StringComparison.OrdinalIgnoreCase)
                ? value[extendedPrefix.Length..]
                : value;
    }

    private static SafeFileHandle OpenDirectory(string path)
    {
        SafeFileHandle handle = CreateFileW(
            path,
            FileReadAttributes,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error);
        }
        return handle;
    }

    private static DirectoryIdentity GetIdentity(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        return new DirectoryIdentity(
            information.VolumeSerialNumber,
            ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow,
            information.FileAttributes);
    }

    private static ConversionTransactionException Preflight() =>
        new(OpenVinoSupportCode.ConversionPreflightFailed);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation information);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle file,
        [Out] char[] path,
        uint pathLength,
        uint flags);

    private sealed record DirectoryIdentity(
        uint VolumeSerialNumber,
        ulong FileIndex,
        FileAttributes Attributes)
    {
        public bool SameObject(DirectoryIdentity other) =>
            VolumeSerialNumber == other.VolumeSerialNumber && FileIndex == other.FileIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public FileAttributes FileAttributes;
        public FileTime CreationTime;
        public FileTime LastAccessTime;
        public uint LastWriteTimeLow;
        public uint LastWriteTimeHigh;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
}
