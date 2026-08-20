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
    TextTooLarge,
    Changed,
    Unreadable
}

internal enum OpenVinoPackageCaptureStage
{
    BeforeAcquireFile,
    AfterAllHandlesAcquired,
    AfterHashesCompleted,
    BeforeInspectorFinalValidation
}

internal sealed record OpenVinoPackageSnapshotCapture(
    OpenVinoPackageSnapshot? Snapshot,
    OpenVinoSnapshotFailure Failure);

internal sealed class OpenVinoPackageSnapshotter
{
    private readonly Func<string, IEnumerable<string>> enumerateEntries;
    private readonly Action<OpenVinoPackageCaptureStage, string?> observer;

    internal OpenVinoPackageSnapshotter(
        Func<string, IEnumerable<string>>? enumerateEntries = null,
        Action<OpenVinoPackageCaptureStage, string?>? observer = null)
    {
        this.enumerateEntries = enumerateEntries ?? Directory.EnumerateFileSystemEntries;
        this.observer = observer ?? ((_, _) => { });
    }

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
            root = Path.GetFullPath(packageRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return Failed(OpenVinoSnapshotFailure.RootMissing);
        }

        SafeFileHandle rootHandle = OpenPath(
            root,
            FileReadAttributes | FileListDirectory,
            FileShareRead,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint);
        if (rootHandle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            rootHandle.Dispose();
            return Failed(error is ErrorFileNotFound or ErrorPathNotFound
                ? OpenVinoSnapshotFailure.RootMissing
                : OpenVinoSnapshotFailure.Unreadable);
        }

        List<AcquiredEntry> acquired = [];
        try
        {
            FileIdentity rootIdentity = GetIdentity(rootHandle);
            if ((rootIdentity.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return Failed(OpenVinoSnapshotFailure.ReparsePoint);
            }

            if ((rootIdentity.Attributes & FileAttributes.Directory) == 0)
            {
                return Failed(OpenVinoSnapshotFailure.RootMissing);
            }

            string rootFinalPath = GetFinalPath(rootHandle);
            if (HasAlternateDataStream(root))
            {
                return Failed(OpenVinoSnapshotFailure.AlternateDataStream);
            }

            TopologyCapture discovery = DiscoverTopology(root, rootFinalPath, repeatCapture: false);
            if (discovery.Failure != OpenVinoSnapshotFailure.None)
            {
                return Failed(discovery.Failure);
            }

            OpenVinoSnapshotFailure policyFailure = ValidateDiscoveredPolicy(discovery.Items);
            if (policyFailure != OpenVinoSnapshotFailure.None)
            {
                return Failed(policyFailure);
            }

            foreach (DiscoveredItem item in discovery.Items.OrderBy(static item => item.RelativeName, StringComparer.Ordinal))
            {
                observer(OpenVinoPackageCaptureStage.BeforeAcquireFile, item.RelativeName);
                SafeFileHandle handle = OpenPath(
                    item.FullPath,
                    GenericRead,
                    FileShareRead,
                    FileFlagOpenReparsePoint | FileFlagSequentialScan);
                if (handle.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    handle.Dispose();
                    DisposeEntries(acquired);
                    return Failed(error switch
                    {
                        ErrorFileNotFound or ErrorPathNotFound => OpenVinoSnapshotFailure.Changed,
                        ErrorSharingViolation => CanOpenWithPermissiveSharing(item.FullPath)
                            ? OpenVinoSnapshotFailure.Changed
                            : OpenVinoSnapshotFailure.Unreadable,
                        _ => OpenVinoSnapshotFailure.Unreadable
                    });
                }

                FileIdentity identity;
                string finalPath;
                try
                {
                    identity = GetIdentity(handle);
                    finalPath = GetFinalPath(handle);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception)
                {
                    handle.Dispose();
                    DisposeEntries(acquired);
                    return Failed(OpenVinoSnapshotFailure.Changed);
                }

                if ((identity.Attributes & FileAttributes.ReparsePoint) != 0 ||
                    !IsDescendant(rootFinalPath, finalPath))
                {
                    handle.Dispose();
                    DisposeEntries(acquired);
                    return Failed((identity.Attributes & FileAttributes.ReparsePoint) != 0
                        ? OpenVinoSnapshotFailure.ReparsePoint
                        : OpenVinoSnapshotFailure.EscapedRoot);
                }

                if ((identity.Attributes & (FileAttributes.Directory | FileAttributes.Device)) != 0)
                {
                    handle.Dispose();
                    DisposeEntries(acquired);
                    return Failed(OpenVinoSnapshotFailure.NonRegularArtifact);
                }

                if (!identity.SameObjectAndContentMetadata(item.Identity) ||
                    !string.Equals(finalPath, item.FinalPath, StringComparison.OrdinalIgnoreCase))
                {
                    handle.Dispose();
                    DisposeEntries(acquired);
                    return Failed(OpenVinoSnapshotFailure.Changed);
                }

                try
                {
                    FileStream stream = new(handle, FileAccess.Read, 128 * 1024, isAsync: false);
                    acquired.Add(new AcquiredEntry(item, stream, identity));
                }
                catch
                {
                    handle.Dispose();
                    DisposeEntries(acquired);
                    throw;
                }
            }

            observer(OpenVinoPackageCaptureStage.AfterAllHandlesAcquired, null);
            OpenVinoSnapshotFailure validation = ValidateCurrentTopology(
                root,
                rootFinalPath,
                rootIdentity,
                rootHandle,
                discovery.Items,
                acquired);
            if (validation != OpenVinoSnapshotFailure.None)
            {
                DisposeEntries(acquired);
                return Failed(validation);
            }

            List<OpenVinoPackageSnapshotEntry> entries = [];
            foreach (AcquiredEntry item in acquired)
            {
                OpenVinoSnapshotFailure contentFailure = ValidateLengthAndMagic(item);
                if (contentFailure != OpenVinoSnapshotFailure.None)
                {
                    DisposeEntries(acquired);
                    return Failed(contentFailure);
                }

                string firstDigest = Hash(item.Stream);
                FileIdentity afterFirstHash = GetIdentity(item.Stream.SafeFileHandle);
                string secondDigest = Hash(item.Stream);
                FileIdentity afterSecondHash = GetIdentity(item.Stream.SafeFileHandle);
                if (!item.Identity.SameObjectAndContentMetadata(afterFirstHash) ||
                    !item.Identity.SameObjectAndContentMetadata(afterSecondHash) ||
                    !string.Equals(firstDigest, secondDigest, StringComparison.Ordinal))
                {
                    DisposeEntries(acquired);
                    return Failed(OpenVinoSnapshotFailure.Changed);
                }

                entries.Add(new OpenVinoPackageSnapshotEntry(
                    item.Discovered.RelativeName,
                    item.Stream,
                    item.Identity.Length,
                    firstDigest,
                    item.Discovered.FinalPath,
                    item.Identity));
            }

            acquired.Clear();
            observer(OpenVinoPackageCaptureStage.AfterHashesCompleted, null);
            validation = ValidateCurrentTopology(root, rootFinalPath, rootIdentity, rootHandle, discovery.Items, entries);
            if (validation != OpenVinoSnapshotFailure.None)
            {
                DisposeEntries(entries);
                return Failed(validation);
            }

            SafeFileHandle retainedRoot = rootHandle;
            rootHandle = new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
            return new OpenVinoPackageSnapshotCapture(
                new OpenVinoPackageSnapshot(
                    root,
                    rootFinalPath,
                    rootIdentity,
                    retainedRoot,
                    discovery.Items,
                    entries,
                    ValidateCurrentTopology,
                    observer),
                OpenVinoSnapshotFailure.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception or CryptographicException)
        {
            DisposeEntries(acquired);
            return Failed(OpenVinoSnapshotFailure.Changed);
        }
        finally
        {
            rootHandle.Dispose();
        }
    }

    private TopologyCapture DiscoverTopology(string root, string rootFinalPath, bool repeatCapture)
    {
        string rootPrefix = root + Path.DirectorySeparatorChar;
        List<DiscoveredItem> discovered = [];
        Queue<DirectoryToVisit> directories = new();
        directories.Enqueue(new DirectoryToVisit(root, 0));

        try
        {
            while (directories.Count > 0)
            {
                DirectoryToVisit directory = directories.Dequeue();
                foreach (string path in enumerateEntries(directory.FullPath))
                {
                    if (discovered.Count == OpenVinoPackagePolicy.MaximumEntries)
                    {
                        return TopologyCapture.Failed(OpenVinoSnapshotFailure.EntryLimitExceeded);
                    }

                    string canonicalPath = Path.GetFullPath(path);
                    if (!canonicalPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return TopologyCapture.Failed(OpenVinoSnapshotFailure.EscapedRoot);
                    }

                    string relativeName = Path.GetRelativePath(root, canonicalPath).Replace(Path.DirectorySeparatorChar, '/');
                    if (Path.IsPathRooted(relativeName) || relativeName == ".." || relativeName.StartsWith("../", StringComparison.Ordinal))
                    {
                        return TopologyCapture.Failed(OpenVinoSnapshotFailure.EscapedRoot);
                    }

                    SafeFileHandle handle = OpenPath(
                        canonicalPath,
                        FileReadAttributes,
                        FileShareRead | FileShareWrite | FileShareDelete,
                        FileFlagBackupSemantics | FileFlagOpenReparsePoint);
                    if (handle.IsInvalid)
                    {
                        handle.Dispose();
                        return TopologyCapture.Failed(repeatCapture
                            ? OpenVinoSnapshotFailure.Changed
                            : OpenVinoSnapshotFailure.Unreadable);
                    }

                    using (handle)
                    {
                        FileIdentity identity = GetIdentity(handle);
                        if ((identity.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            return TopologyCapture.Failed(OpenVinoSnapshotFailure.ReparsePoint);
                        }

                        string finalPath = GetFinalPath(handle);
                        if (!IsDescendant(rootFinalPath, finalPath))
                        {
                            return TopologyCapture.Failed(OpenVinoSnapshotFailure.EscapedRoot);
                        }

                        if (HasAlternateDataStream(canonicalPath))
                        {
                            return TopologyCapture.Failed(OpenVinoSnapshotFailure.AlternateDataStream);
                        }

                        int depth = directory.Depth + 1;
                        if (depth > OpenVinoPackagePolicy.MaximumDepth)
                        {
                            return TopologyCapture.Failed(OpenVinoSnapshotFailure.DepthLimitExceeded);
                        }

                        bool isDirectory = (identity.Attributes & FileAttributes.Directory) != 0;
                        discovered.Add(new DiscoveredItem(canonicalPath, finalPath, relativeName, isDirectory, identity));
                        if (isDirectory)
                        {
                            directories.Enqueue(new DirectoryToVisit(canonicalPath, depth));
                        }
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or Win32Exception)
        {
            return TopologyCapture.Failed(repeatCapture ? OpenVinoSnapshotFailure.Changed : OpenVinoSnapshotFailure.Unreadable);
        }

        OpenVinoSnapshotFailure nameFailure = ValidateRelativeNames(discovered.Select(static item => item.RelativeName));
        return nameFailure == OpenVinoSnapshotFailure.None
            ? new TopologyCapture(discovered, OpenVinoSnapshotFailure.None)
            : TopologyCapture.Failed(nameFailure);
    }

    private OpenVinoSnapshotFailure ValidateCurrentTopology(
        string root,
        string rootFinalPath,
        FileIdentity rootIdentity,
        SafeFileHandle retainedRoot,
        IReadOnlyCollection<DiscoveredItem> expected,
        IReadOnlyCollection<AcquiredEntry> lockedEntries)
    {
        return ValidateCurrentTopology(root, rootFinalPath, rootIdentity, retainedRoot, expected, lockedEntries.Cast<ILockedSnapshotEntry>().ToArray());
    }

    private OpenVinoSnapshotFailure ValidateCurrentTopology(
        string root,
        string rootFinalPath,
        FileIdentity rootIdentity,
        SafeFileHandle retainedRoot,
        IReadOnlyCollection<DiscoveredItem> expected,
        IReadOnlyCollection<OpenVinoPackageSnapshotEntry> lockedEntries)
    {
        return ValidateCurrentTopology(root, rootFinalPath, rootIdentity, retainedRoot, expected, lockedEntries.Cast<ILockedSnapshotEntry>().ToArray());
    }

    private OpenVinoSnapshotFailure ValidateCurrentTopology(
        string root,
        string rootFinalPath,
        FileIdentity rootIdentity,
        SafeFileHandle retainedRoot,
        IReadOnlyCollection<DiscoveredItem> expected,
        IReadOnlyCollection<ILockedSnapshotEntry> lockedEntries)
    {
        try
        {
            FileIdentity retainedIdentity = GetIdentity(retainedRoot);
            if (!retainedIdentity.SameObjectAndContentMetadata(rootIdentity) ||
                (retainedIdentity.Attributes & FileAttributes.ReparsePoint) != 0 ||
                !string.Equals(GetFinalPath(retainedRoot), rootFinalPath, StringComparison.OrdinalIgnoreCase))
            {
                return OpenVinoSnapshotFailure.Changed;
            }

            using SafeFileHandle currentRoot = OpenPath(
                root,
                FileReadAttributes | FileListDirectory,
                FileShareRead | FileShareWrite | FileShareDelete,
                FileFlagBackupSemantics | FileFlagOpenReparsePoint);
            if (currentRoot.IsInvalid)
            {
                return OpenVinoSnapshotFailure.Changed;
            }

            FileIdentity currentRootIdentity = GetIdentity(currentRoot);
            if ((currentRootIdentity.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return OpenVinoSnapshotFailure.ReparsePoint;
            }

            if (!currentRootIdentity.SameObjectAndContentMetadata(rootIdentity) ||
                !string.Equals(GetFinalPath(currentRoot), rootFinalPath, StringComparison.OrdinalIgnoreCase))
            {
                return OpenVinoSnapshotFailure.Changed;
            }

            if (HasAlternateDataStream(root))
            {
                return OpenVinoSnapshotFailure.AlternateDataStream;
            }

            TopologyCapture current = DiscoverTopology(root, rootFinalPath, repeatCapture: true);
            if (current.Failure != OpenVinoSnapshotFailure.None)
            {
                return current.Failure;
            }

            Dictionary<string, DiscoveredItem> expectedByName = expected.ToDictionary(static item => item.RelativeName, StringComparer.Ordinal);
            if (current.Items.Count != expectedByName.Count)
            {
                return OpenVinoSnapshotFailure.Changed;
            }

            foreach (DiscoveredItem item in current.Items)
            {
                if (!expectedByName.TryGetValue(item.RelativeName, out DiscoveredItem? original) ||
                    !item.Identity.SameObjectAndContentMetadata(original.Identity) ||
                    item.IsDirectory != original.IsDirectory ||
                    !string.Equals(item.FinalPath, original.FinalPath, StringComparison.OrdinalIgnoreCase))
                {
                    return OpenVinoSnapshotFailure.Changed;
                }
            }

            foreach (ILockedSnapshotEntry entry in lockedEntries)
            {
                FileIdentity identity = GetIdentity(entry.Stream.SafeFileHandle);
                if (!identity.SameObjectAndContentMetadata(entry.Identity))
                {
                    return OpenVinoSnapshotFailure.Changed;
                }

                if ((identity.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return OpenVinoSnapshotFailure.ReparsePoint;
                }

                if ((identity.Attributes & (FileAttributes.Directory | FileAttributes.Device)) != 0)
                {
                    return OpenVinoSnapshotFailure.NonRegularArtifact;
                }

                if (!IsDescendant(rootFinalPath, GetFinalPath(entry.Stream.SafeFileHandle)))
                {
                    return OpenVinoSnapshotFailure.EscapedRoot;
                }

                if (HasAlternateDataStream(Path.Combine(root, entry.RelativeName)))
                {
                    return OpenVinoSnapshotFailure.AlternateDataStream;
                }
            }

            return OpenVinoSnapshotFailure.None;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception)
        {
            return OpenVinoSnapshotFailure.Changed;
        }
    }

    private static OpenVinoSnapshotFailure ValidateDiscoveredPolicy(IEnumerable<DiscoveredItem> discovered)
    {
        foreach (DiscoveredItem item in discovered)
        {
            if (item.IsDirectory || (item.Identity.Attributes & FileAttributes.Device) != 0)
            {
                return OpenVinoSnapshotFailure.NonRegularArtifact;
            }

            if (OpenVinoPackagePolicy.IsExecutableOrScriptName(item.RelativeName))
            {
                return OpenVinoSnapshotFailure.ExecutableOrScript;
            }

            if (!OpenVinoPackagePolicy.IsAllowedResource(item.RelativeName))
            {
                return OpenVinoSnapshotFailure.UnrecognizedResource;
            }
        }

        return OpenVinoSnapshotFailure.None;
    }

    private static OpenVinoSnapshotFailure ValidateLengthAndMagic(AcquiredEntry entry)
    {
        if (entry.Identity.Length <= 0)
        {
            return OpenVinoSnapshotFailure.NonRegularArtifact;
        }

        if (OpenVinoPackagePolicy.IsJsonResource(entry.Discovered.RelativeName) && entry.Identity.Length > OpenVinoPackagePolicy.MaximumJsonBytes)
        {
            return OpenVinoSnapshotFailure.JsonTooLarge;
        }

        if (OpenVinoPackagePolicy.IsXmlResource(entry.Discovered.RelativeName) && entry.Identity.Length > OpenVinoPackagePolicy.MaximumXmlBytes)
        {
            return OpenVinoSnapshotFailure.XmlTooLarge;
        }

        if (OpenVinoPackagePolicy.IsTextResource(entry.Discovered.RelativeName) && entry.Identity.Length > OpenVinoPackagePolicy.MaximumTextBytes)
        {
            return OpenVinoSnapshotFailure.TextTooLarge;
        }

        return HasExecutableOrScriptMagic(entry.Stream)
            ? OpenVinoSnapshotFailure.ExecutableOrScript
            : OpenVinoSnapshotFailure.None;
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

    private static void DisposeEntries<T>(IEnumerable<T> entries) where T : IDisposable
    {
        foreach (T entry in entries)
        {
            entry.Dispose();
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

    private static bool CanOpenWithPermissiveSharing(string path)
    {
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return stream.CanRead;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string Hash(FileStream stream)
    {
        stream.Position = 0;
        using SHA256 algorithm = SHA256.Create();
        byte[] hash = algorithm.ComputeHash(stream);
        stream.Position = 0;
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static SafeFileHandle OpenPath(string path, uint access, uint share, uint flags) =>
        CreateFileW(path, access, share, IntPtr.Zero, OpenExisting, flags, IntPtr.Zero);

    internal static FileIdentity GetIdentity(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        long length = ((long)information.FileSizeHigh << 32) | information.FileSizeLow;
        long lastWrite = ((long)information.LastWriteTimeHigh << 32) | information.LastWriteTimeLow;
        ulong fileIndex = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
        return new FileIdentity(information.VolumeSerialNumber, fileIndex, length, lastWrite, information.FileAttributes);
    }

    private static string GetFinalPath(SafeFileHandle handle)
    {
        char[] buffer = new char[512];
        uint length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
        if (length == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (length >= (uint)buffer.Length)
        {
            buffer = new char[checked((int)length + 1)];
            length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
            if (length == 0 || length >= (uint)buffer.Length)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        string path = new(buffer, 0, checked((int)length));
        const string uncPrefix = @"\\?\UNC\";
        const string extendedPrefix = @"\\?\";
        return path.StartsWith(uncPrefix, StringComparison.OrdinalIgnoreCase)
            ? @"\\" + path[uncPrefix.Length..]
            : path.StartsWith(extendedPrefix, StringComparison.OrdinalIgnoreCase)
                ? path[extendedPrefix.Length..]
                : path;
    }

    private static bool IsDescendant(string root, string candidate)
    {
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAlternateDataStream(string path)
    {
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

    private const uint GenericRead = 0x80000000;
    private const uint FileReadAttributes = 0x00000080;
    private const uint FileListDirectory = 0x00000001;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagSequentialScan = 0x08000000;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorSharingViolation = 32;
    private const int ErrorNoMoreFiles = 18;
    private const int ErrorHandleEof = 38;
    private static readonly IntPtr InvalidFindHandle = new(-1);

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
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation information);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, [Out] char[] path, uint pathLength, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindFirstStreamW(string fileName, int informationLevel, out FindStreamData data, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindNextStreamW(IntPtr findStream, out FindStreamData data);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindClose(IntPtr findFile);

    private sealed record DirectoryToVisit(string FullPath, int Depth);

    internal sealed record DiscoveredItem(
        string FullPath,
        string FinalPath,
        string RelativeName,
        bool IsDirectory,
        FileIdentity Identity);

    internal sealed record FileIdentity(
        uint VolumeSerialNumber,
        ulong FileIndex,
        long Length,
        long LastWriteTime,
        FileAttributes Attributes)
    {
        public bool SameObjectAndContentMetadata(FileIdentity other) =>
            VolumeSerialNumber == other.VolumeSerialNumber &&
            FileIndex == other.FileIndex &&
            Length == other.Length &&
            LastWriteTime == other.LastWriteTime;
    }

    private sealed record TopologyCapture(IReadOnlyCollection<DiscoveredItem> Items, OpenVinoSnapshotFailure Failure)
    {
        public static TopologyCapture Failed(OpenVinoSnapshotFailure failure) => new([], failure);
    }

    internal interface ILockedSnapshotEntry
    {
        string RelativeName { get; }

        FileStream Stream { get; }

        FileIdentity Identity { get; }
    }

    private sealed record AcquiredEntry(DiscoveredItem Discovered, FileStream Stream, FileIdentity Identity) : IDisposable, ILockedSnapshotEntry
    {
        public string RelativeName => Discovered.RelativeName;

        public void Dispose() => Stream.Dispose();
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
    private readonly string root;
    private readonly string rootFinalPath;
    private readonly OpenVinoPackageSnapshotter.FileIdentity rootIdentity;
    private readonly SafeFileHandle rootHandle;
    private readonly IReadOnlyCollection<OpenVinoPackageSnapshotter.DiscoveredItem> discovery;
    private readonly Dictionary<string, OpenVinoPackageSnapshotEntry> entries;
    private readonly Func<string, string, OpenVinoPackageSnapshotter.FileIdentity, SafeFileHandle, IReadOnlyCollection<OpenVinoPackageSnapshotter.DiscoveredItem>, IReadOnlyCollection<OpenVinoPackageSnapshotEntry>, OpenVinoSnapshotFailure> validator;
    private readonly Action<OpenVinoPackageCaptureStage, string?> observer;

    public OpenVinoPackageSnapshot(
        string root,
        string rootFinalPath,
        OpenVinoPackageSnapshotter.FileIdentity rootIdentity,
        SafeFileHandle rootHandle,
        IReadOnlyCollection<OpenVinoPackageSnapshotter.DiscoveredItem> discovery,
        IEnumerable<OpenVinoPackageSnapshotEntry> entries,
        Func<string, string, OpenVinoPackageSnapshotter.FileIdentity, SafeFileHandle, IReadOnlyCollection<OpenVinoPackageSnapshotter.DiscoveredItem>, IReadOnlyCollection<OpenVinoPackageSnapshotEntry>, OpenVinoSnapshotFailure> validator,
        Action<OpenVinoPackageCaptureStage, string?> observer)
    {
        this.root = root;
        this.rootFinalPath = rootFinalPath;
        this.rootIdentity = rootIdentity;
        this.rootHandle = rootHandle;
        this.discovery = discovery;
        this.entries = entries.ToDictionary(static entry => entry.RelativeName, StringComparer.Ordinal);
        this.validator = validator;
        this.observer = observer;
    }

    public IReadOnlyCollection<OpenVinoPackageSnapshotEntry> Entries => entries.Values;

    public bool TryGetEntry(string relativeName, out OpenVinoPackageSnapshotEntry entry) => entries.TryGetValue(relativeName, out entry!);

    public void Notify(OpenVinoPackageCaptureStage stage) => observer(stage, null);

    public OpenVinoSnapshotFailure ValidateStillCurrent() =>
        validator(root, rootFinalPath, rootIdentity, rootHandle, discovery, entries.Values);

    public void Dispose()
    {
        foreach (OpenVinoPackageSnapshotEntry entry in entries.Values)
        {
            entry.Dispose();
        }

        rootHandle.Dispose();
    }
}

internal sealed class OpenVinoPackageSnapshotEntry : IDisposable, OpenVinoPackageSnapshotter.ILockedSnapshotEntry
{
    public OpenVinoPackageSnapshotEntry(
        string relativeName,
        FileStream stream,
        long length,
        string sha256,
        string finalPath,
        OpenVinoPackageSnapshotter.FileIdentity identity)
    {
        RelativeName = relativeName;
        Stream = stream;
        Length = length;
        Sha256 = sha256;
        FinalPath = finalPath;
        Identity = identity;
    }

    public string RelativeName { get; }

    public FileStream Stream { get; }

    public long Length { get; }

    public string Sha256 { get; }

    internal string FinalPath { get; }

    internal OpenVinoPackageSnapshotter.FileIdentity Identity { get; }

    OpenVinoPackageSnapshotter.FileIdentity OpenVinoPackageSnapshotter.ILockedSnapshotEntry.Identity => Identity;

    public void Dispose() => Stream.Dispose();
}
