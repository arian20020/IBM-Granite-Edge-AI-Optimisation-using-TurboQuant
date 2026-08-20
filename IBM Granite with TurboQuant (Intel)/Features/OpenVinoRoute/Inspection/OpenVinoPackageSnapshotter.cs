using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Inspection;

internal enum OpenVinoSnapshotFailure
{
    None,
    RootMissing,
    EntryLimitExceeded,
    DepthLimitExceeded,
    ReparsePoint,
    EscapedRoot,
    CaseCollision,
    AlternateDataStream,
    NonRegularArtifact,
    ExecutableOrScript,
    UnrecognizedResource,
    JsonTooLarge,
    XmlTooLarge,
    Changed,
    Unreadable
}

internal sealed record OpenVinoPackageSnapshotCapture(
    OpenVinoPackageSnapshot? Snapshot,
    OpenVinoSnapshotFailure Failure);

internal sealed class OpenVinoPackageSnapshotter
{
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The instance boundary permits operation-scoped snapshotter composition.")]
    public OpenVinoPackageSnapshotCapture Capture(string packageRoot)
    {
        if (string.IsNullOrWhiteSpace(packageRoot))
        {
            return Failed(OpenVinoSnapshotFailure.RootMissing);
        }

        string root;
        try
        {
            root = Path.GetFullPath(packageRoot);
            if (!Directory.Exists(root))
            {
                return Failed(OpenVinoSnapshotFailure.RootMissing);
            }

            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            {
                return Failed(OpenVinoSnapshotFailure.ReparsePoint);
            }

            if (HasAlternateDataStream(root))
            {
                return Failed(OpenVinoSnapshotFailure.AlternateDataStream);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Failed(OpenVinoSnapshotFailure.Unreadable);
        }

        string rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        List<DiscoveredItem> discovered = [];
        Queue<DirectoryToVisit> directories = new();
        directories.Enqueue(new DirectoryToVisit(root, 0));

        try
        {
            while (directories.Count > 0)
            {
                DirectoryToVisit directory = directories.Dequeue();
                foreach (string path in Directory.EnumerateFileSystemEntries(directory.FullPath))
                {
                    if (discovered.Count == OpenVinoPackagePolicy.MaximumEntries)
                    {
                        return Failed(OpenVinoSnapshotFailure.EntryLimitExceeded);
                    }

                    string canonicalPath = Path.GetFullPath(path);
                    if (!canonicalPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return Failed(OpenVinoSnapshotFailure.EscapedRoot);
                    }

                    string relativeName = Path.GetRelativePath(root, canonicalPath)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    if (Path.IsPathRooted(relativeName) ||
                        relativeName.Equals("..", StringComparison.Ordinal) ||
                        relativeName.StartsWith("../", StringComparison.Ordinal))
                    {
                        return Failed(OpenVinoSnapshotFailure.EscapedRoot);
                    }

                    FileAttributes attributes = File.GetAttributes(canonicalPath);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        return Failed(OpenVinoSnapshotFailure.ReparsePoint);
                    }

                    if (HasAlternateDataStream(canonicalPath))
                    {
                        return Failed(OpenVinoSnapshotFailure.AlternateDataStream);
                    }

                    bool isDirectory = (attributes & FileAttributes.Directory) != 0;
                    int depth = directory.Depth + 1;
                    if (depth > OpenVinoPackagePolicy.MaximumDepth)
                    {
                        return Failed(OpenVinoSnapshotFailure.DepthLimitExceeded);
                    }

                    discovered.Add(new DiscoveredItem(canonicalPath, relativeName, isDirectory, attributes));
                    if (isDirectory)
                    {
                        directories.Enqueue(new DirectoryToVisit(canonicalPath, depth));
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or Win32Exception)
        {
            return Failed(OpenVinoSnapshotFailure.Unreadable);
        }

        OpenVinoSnapshotFailure relativeNameFailure = ValidateRelativeNames(
            discovered.Select(static item => item.RelativeName));
        if (relativeNameFailure != OpenVinoSnapshotFailure.None)
        {
            return Failed(relativeNameFailure);
        }

        foreach (DiscoveredItem item in discovered)
        {
            if (item.IsDirectory)
            {
                return Failed(OpenVinoSnapshotFailure.NonRegularArtifact);
            }

            if ((item.Attributes & FileAttributes.Device) != 0)
            {
                return Failed(OpenVinoSnapshotFailure.NonRegularArtifact);
            }

            if (OpenVinoPackagePolicy.IsExecutableOrScriptName(item.RelativeName))
            {
                return Failed(OpenVinoSnapshotFailure.ExecutableOrScript);
            }

            if (!OpenVinoPackagePolicy.IsAllowedResource(item.RelativeName))
            {
                return Failed(OpenVinoSnapshotFailure.UnrecognizedResource);
            }
        }

        List<OpenVinoPackageSnapshotEntry> entries = [];
        try
        {
            foreach (DiscoveredItem item in discovered.OrderBy(static item => item.RelativeName, StringComparer.Ordinal))
            {
                FileStream stream;
                try
                {
                    stream = new FileStream(
                        item.FullPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        128 * 1024,
                        FileOptions.SequentialScan);
                }
                catch (IOException)
                {
                    DisposeEntries(entries);
                    return Failed(CanOpenWithPermissiveSharing(item.FullPath)
                        ? OpenVinoSnapshotFailure.Changed
                        : OpenVinoSnapshotFailure.Unreadable);
                }
                catch (UnauthorizedAccessException)
                {
                    DisposeEntries(entries);
                    return Failed(OpenVinoSnapshotFailure.Unreadable);
                }

                FileIdentity before;
                try
                {
                    before = GetIdentity(stream.SafeFileHandle);
                    if (before.Length <= 0)
                    {
                        stream.Dispose();
                        DisposeEntries(entries);
                        return Failed(OpenVinoSnapshotFailure.NonRegularArtifact);
                    }

                    if (OpenVinoPackagePolicy.IsJsonResource(item.RelativeName) && before.Length > OpenVinoPackagePolicy.MaximumJsonBytes)
                    {
                        stream.Dispose();
                        DisposeEntries(entries);
                        return Failed(OpenVinoSnapshotFailure.JsonTooLarge);
                    }

                    if (OpenVinoPackagePolicy.IsXmlResource(item.RelativeName) && before.Length > OpenVinoPackagePolicy.MaximumXmlBytes)
                    {
                        stream.Dispose();
                        DisposeEntries(entries);
                        return Failed(OpenVinoSnapshotFailure.XmlTooLarge);
                    }

                    if (HasExecutableOrScriptMagic(stream))
                    {
                        stream.Dispose();
                        DisposeEntries(entries);
                        return Failed(OpenVinoSnapshotFailure.ExecutableOrScript);
                    }

                    string firstDigest = Hash(stream);
                    FileIdentity afterFirstHash = GetIdentity(stream.SafeFileHandle);
                    string secondDigest = Hash(stream);
                    FileIdentity afterSecondHash = GetIdentity(stream.SafeFileHandle);
                    if (before != afterFirstHash || before != afterSecondHash || !string.Equals(firstDigest, secondDigest, StringComparison.Ordinal))
                    {
                        stream.Dispose();
                        DisposeEntries(entries);
                        return Failed(OpenVinoSnapshotFailure.Changed);
                    }

                    entries.Add(new OpenVinoPackageSnapshotEntry(
                        item.RelativeName,
                        stream,
                        before.Length,
                        firstDigest));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception or CryptographicException)
                {
                    stream.Dispose();
                    DisposeEntries(entries);
                    return Failed(OpenVinoSnapshotFailure.Changed);
                }
            }

            return new OpenVinoPackageSnapshotCapture(new OpenVinoPackageSnapshot(entries), OpenVinoSnapshotFailure.None);
        }
        catch
        {
            DisposeEntries(entries);
            throw;
        }
    }

    private static OpenVinoPackageSnapshotCapture Failed(OpenVinoSnapshotFailure failure) => new(null, failure);

    internal static OpenVinoSnapshotFailure ValidateRelativeNames(IEnumerable<string> relativeNames)
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (string relativeName in relativeNames)
        {
            if (!names.Add(relativeName))
            {
                return OpenVinoSnapshotFailure.CaseCollision;
            }
        }

        return OpenVinoSnapshotFailure.None;
    }

    private static void DisposeEntries(IEnumerable<OpenVinoPackageSnapshotEntry> entries)
    {
        foreach (OpenVinoPackageSnapshotEntry entry in entries)
        {
            entry.Dispose();
        }
    }

    private static bool CanOpenWithPermissiveSharing(string path)
    {
        try
        {
            using FileStream _ = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool HasExecutableOrScriptMagic(FileStream stream)
    {
        Span<byte> prefix = stackalloc byte[4];
        stream.Position = 0;
        int length = stream.Read(prefix);
        stream.Position = 0;
        return (length >= 2 && prefix[0] == (byte)'M' && prefix[1] == (byte)'Z') ||
            (length >= 2 && prefix[0] == (byte)'#' && prefix[1] == (byte)'!') ||
            (length >= 4 && prefix[0] == 0x7f && prefix[1] == (byte)'E' && prefix[2] == (byte)'L' && prefix[3] == (byte)'F');
    }

    private static string Hash(FileStream stream)
    {
        stream.Position = 0;
        using SHA256 algorithm = SHA256.Create();
        byte[] hash = algorithm.ComputeHash(stream);
        stream.Position = 0;
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static FileIdentity GetIdentity(SafeFileHandle handle)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("OpenVINO package identity validation requires Windows.");
        }

        if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        long length = ((long)information.FileSizeHigh << 32) | information.FileSizeLow;
        long lastWrite = ((long)information.LastWriteTimeHigh << 32) | information.LastWriteTimeLow;
        ulong fileIndex = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
        return new FileIdentity(information.VolumeSerialNumber, fileIndex, length, lastWrite);
    }

    private static bool HasAlternateDataStream(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("OpenVINO package stream validation requires Windows.");
        }

        IntPtr handle = FindFirstStreamW(path, 0, out FindStreamData data, 0);
        if (handle == InvalidFindHandle)
        {
            int error = Marshal.GetLastWin32Error();
            if (error is ErrorHandleEof or ErrorNoMoreFiles)
            {
                return false;
            }

            throw new Win32Exception(error);
        }

        try
        {
            do
            {
                if (!string.Equals(data.StreamName, "::$DATA", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            while (FindNextStreamW(handle, out data));

            int error = Marshal.GetLastWin32Error();
            if (error is not ErrorHandleEof and not ErrorNoMoreFiles)
            {
                throw new Win32Exception(error);
            }

            return false;
        }
        finally
        {
            _ = FindClose(handle);
        }
    }

    private const int ErrorNoMoreFiles = 18;
    private const int ErrorHandleEof = 38;
    private static readonly IntPtr InvalidFindHandle = new(-1);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation information);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindFirstStreamW(
        string fileName,
        int informationLevel,
        out FindStreamData data,
        uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindNextStreamW(IntPtr findStream, out FindStreamData data);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindClose(IntPtr findFile);

    private sealed record DirectoryToVisit(string FullPath, int Depth);

    private sealed record DiscoveredItem(
        string FullPath,
        string RelativeName,
        bool IsDirectory,
        FileAttributes Attributes);

    private sealed record FileIdentity(uint VolumeSerialNumber, ulong FileIndex, long Length, long LastWriteTime);

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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FindStreamData
    {
        public long StreamSize;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)]
        public string StreamName;
    }
}

internal sealed class OpenVinoPackageSnapshot : IDisposable
{
    private readonly Dictionary<string, OpenVinoPackageSnapshotEntry> entries;

    public OpenVinoPackageSnapshot(IEnumerable<OpenVinoPackageSnapshotEntry> entries)
    {
        this.entries = entries.ToDictionary(static entry => entry.RelativeName, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<OpenVinoPackageSnapshotEntry> Entries => entries.Values;

    public bool TryGetEntry(string relativeName, out OpenVinoPackageSnapshotEntry entry) =>
        entries.TryGetValue(relativeName, out entry!);

    public void Dispose()
    {
        foreach (OpenVinoPackageSnapshotEntry entry in entries.Values)
        {
            entry.Dispose();
        }
    }
}

internal sealed class OpenVinoPackageSnapshotEntry : IDisposable
{
    public OpenVinoPackageSnapshotEntry(string relativeName, FileStream stream, long length, string sha256)
    {
        RelativeName = relativeName;
        Stream = stream;
        Length = length;
        Sha256 = sha256;
    }

    public string RelativeName { get; }

    public FileStream Stream { get; }

    public long Length { get; }

    public string Sha256 { get; }

    public void Dispose() => Stream.Dispose();
}
