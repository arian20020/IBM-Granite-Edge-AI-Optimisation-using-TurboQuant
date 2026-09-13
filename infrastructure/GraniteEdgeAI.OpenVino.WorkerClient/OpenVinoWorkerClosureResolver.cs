using System.Buffers;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.OpenVino.WorkerClient;

/// <summary>
/// Resolves one caller-authorized, closed worker manifest and retains every
/// accepted filesystem identity with write/delete sharing denied.
/// </summary>
internal sealed class OpenVinoWorkerClosureResolver
{
    private const int MaximumManifestBytes = 1024 * 1024;
    private const int MaximumManifestFiles = 128;
    private const uint GenericRead = 0x80000000;
    private const uint GenericReadAttributes = 0x00000080;
    private const uint ShareRead = 0x00000001;
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;
    private const uint OpenReparsePoint = 0x00200000;
    private const int ErrorNoMoreFiles = 18;
    private const int ErrorHandleEof = 38;
    private const int DosHeaderPeOffset = 0x3c;
    private const uint PortableExecutableSignature = 0x00004550;
    private static readonly IntPtr InvalidFindHandle = new(-1);
    private static readonly char[] Separators =
        [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    private readonly Action? _afterHandlesAcquired;
    private readonly Action<string>? _beforePathHandleOpened;

    internal OpenVinoWorkerClosureResolver(
        Action? afterHandlesAcquired = null,
        Action<string>? beforePathHandleOpened = null)
    {
        _afterHandlesAcquired = afterHandlesAcquired;
        _beforePathHandleOpened = beforePathHandleOpened;
    }

    internal VerifiedOpenVinoWorkerClosure Resolve(
        OpenVinoWorkerInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        installation.Validate();
        Dictionary<string, OpenVinoWorkerBinaryMachine> expectedBinaryMachines =
            installation.ExpectedBinaryMachines.ToDictionary(
                static item => item.Key,
                static item => item.Value,
                StringComparer.Ordinal);
        VerifiedWorkerExecutable? executable = null;
        List<SafeFileHandle> handles = [];
        Dictionary<string, OpenVinoWorkerFileIdentity> identities = new(
            StringComparer.Ordinal);
        try
        {
            string root = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(installation.ApprovedWorkerRoot));
            SafeFileHandle rootHandle = OpenDirectory(root);
            handles.Add(rootHandle);
            RequireHandleKind(rootHandle, directory: true);
            RequireFinalContained(root, GetFinalPath(rootHandle), allowRoot: true);
            RequireNoAlternateStreams(root);

            string manifestPath = Path.Combine(root, "worker-manifest.json");
            RequireContained(root, manifestPath);
            _beforePathHandleOpened?.Invoke("worker-manifest.json");
            SafeFileHandle manifestHandle = OpenFile(manifestPath);
            handles.Add(manifestHandle);
            RequireHandleKind(manifestHandle, directory: false);
            RequireFinalContained(root, GetFinalPath(manifestHandle));
            RequireNoAlternateStreams(manifestPath);
            byte[] manifestBytes = ReadBounded(manifestHandle, MaximumManifestBytes);
            identities.Add("worker-manifest.json", GetIdentity(manifestHandle));
            RequireDigest(
                manifestBytes,
                installation.ExpectedBuildEvidence.WorkerManifestDigest);
            IReadOnlyList<ManifestFile> manifest = ParseManifest(manifestBytes);
            RequireSetEqual(
                expectedBinaryMachines.Keys,
                manifest.Where(static file => IsBinaryPath(file.Path))
                    .Select(static file => file.Path));

            HashSet<string> expectedDirectories = new(
                StringComparer.OrdinalIgnoreCase);
            foreach (ManifestFile file in manifest)
            {
                string? directory = Path.GetDirectoryName(file.Path);
                while (!string.IsNullOrEmpty(directory))
                {
                    expectedDirectories.Add(directory.Replace('\\', '/'));
                    directory = Path.GetDirectoryName(directory);
                }
            }

            string[] orderedDirectories = expectedDirectories
                .OrderBy(static path => path.Count(static value => value == '/'))
                .ThenBy(static path => path, StringComparer.Ordinal)
                .ToArray();
            foreach (string relativeDirectory in orderedDirectories)
            {
                string directoryPath = Path.Combine(
                    root,
                    relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
                _beforePathHandleOpened?.Invoke(relativeDirectory);
                SafeFileHandle directoryHandle = OpenDirectory(directoryPath);
                handles.Add(directoryHandle);
                RequireHandleKind(directoryHandle, directory: true);
                RequireFinalContained(root, GetFinalPath(directoryHandle));
                RequireNoAlternateStreams(directoryPath);
                identities.Add(relativeDirectory + "/", GetIdentity(directoryHandle));
            }

            foreach (ManifestFile file in manifest)
            {
                string fullPath = Path.Combine(
                    root,
                    file.Path.Replace('/', Path.DirectorySeparatorChar));
                RequireContained(root, fullPath);
                _beforePathHandleOpened?.Invoke(file.Path);
                SafeFileHandle handle = OpenFile(fullPath);
                handles.Add(handle);
                RequireHandleKind(handle, directory: false);
                RequireFinalContained(root, GetFinalPath(handle));
                RequireNoAlternateStreams(fullPath);
                long actualLength = RandomAccess.GetLength(handle);
                if (actualLength != file.Length ||
                    !string.Equals(Hash(handle), file.Sha256,
                        StringComparison.Ordinal))
                {
                    throw Untrusted();
                }

                if (expectedBinaryMachines.TryGetValue(
                    file.Path,
                    out OpenVinoWorkerBinaryMachine expectedMachine))
                {
                    RequireMachine(handle, expectedMachine);
                }

                identities.Add(file.Path, GetIdentity(handle));
                if (string.Equals(
                    file.Path,
                    installation.WorkerExecutableRelativePath.Replace('\\', '/'),
                    StringComparison.Ordinal))
                {
                    VerifiedWorkerExecutable verifiedExecutable = new(
                        GetFinalPath(rootHandle),
                        GetFinalPath(handle),
                        handle);
                    handles.RemoveAt(handles.Count - 1);
                    executable = verifiedExecutable;
                }
            }

            if (executable is null)
            {
                throw Untrusted();
            }

            RequireClosedTopology(root, manifest, expectedDirectories, identities);
            _afterHandlesAcquired?.Invoke();
            RequireClosedTopology(root, manifest, expectedDirectories, identities);

            VerifiedOpenVinoWorkerClosure result = new(
                executable,
                handles,
                identities);
            executable = null;
            handles = [];
            return result;
        }
        catch (WorkerClientPolicyException)
        {
            throw;
        }
        catch (Exception error) when (error is ArgumentException or
            IOException or UnauthorizedAccessException or InvalidDataException or
            JsonException or Win32Exception or NotSupportedException or
            CryptographicException)
        {
            throw new WorkerClientPolicyException(
                new WorkerClientFailure(
                    WorkerClientFailureCodes.WorkerExecutableUntrusted,
                    "The OpenVINO worker closure could not be trusted."),
                error);
        }
        finally
        {
            foreach (SafeFileHandle handle in handles)
            {
                handle.Dispose();
            }

            executable?.Dispose();
        }
    }

    private static List<ManifestFile> ParseManifest(byte[] utf8)
    {
        using JsonDocument document = JsonDocument.Parse(utf8, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 4
        });
        JsonElement root = document.RootElement;
        RequireProperties(root, "schemaVersion", "files");
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
        {
            throw Untrusted();
        }

        JsonElement files = root.GetProperty("files");
        if (files.ValueKind != JsonValueKind.Array ||
            files.GetArrayLength() is <= 0 or > MaximumManifestFiles)
        {
            throw Untrusted();
        }

        List<ManifestFile> result = new(files.GetArrayLength());
        string? previous = null;
        foreach (JsonElement item in files.EnumerateArray())
        {
            RequireProperties(item, "path", "length", "sha256");
            string path = item.GetProperty("path").GetString() ?? string.Empty;
            long length = item.GetProperty("length").GetInt64();
            string sha256 = item.GetProperty("sha256").GetString() ?? string.Empty;
            RequireManifestPath(path);
            if (length <= 0 || !IsSha256(sha256) ||
                previous is not null &&
                StringComparer.Ordinal.Compare(previous, path) >= 0)
            {
                throw Untrusted();
            }

            result.Add(new ManifestFile(path, length, sha256));
            previous = path;
        }

        return result;
    }

    private static void RequireProperties(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Untrusted();
        }

        string[] actual = element.EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        if (actual.Length != names.Length ||
            !actual.Order(StringComparer.Ordinal)
                .SequenceEqual(names.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw Untrusted();
        }
    }

    private static void RequireManifestPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512 ||
            Path.IsPathRooted(value) || value.Contains('\\') ||
            value.Contains('\0') || value.Split('/').Any(segment =>
                string.IsNullOrEmpty(segment) || segment is "." or ".."))
        {
            throw Untrusted();
        }
    }

    private static byte[] ReadBounded(SafeFileHandle handle, int maximum)
    {
        long length = RandomAccess.GetLength(handle);
        if (length <= 0 || length > maximum)
        {
            throw Untrusted();
        }

        byte[] bytes = new byte[checked((int)length)];
        int total = 0;
        while (total < bytes.Length)
        {
            int read = RandomAccess.Read(handle, bytes.AsSpan(total), total);
            if (read == 0)
            {
                throw Untrusted();
            }

            total += read;
        }

        return bytes;
    }

    private static string Hash(SafeFileHandle handle)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            long offset = 0;
            while (true)
            {
                int read = RandomAccess.Read(handle, buffer, offset);
                if (read == 0)
                {
                    break;
                }

                hash.AppendData(buffer, 0, read);
                offset += read;
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void RequireDigest(byte[] bytes, string expected)
    {
        string actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(expected)))
        {
            throw Untrusted();
        }
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireMachine(
        SafeFileHandle handle,
        OpenVinoWorkerBinaryMachine expected)
    {
        Span<byte> header = stackalloc byte[64];
        if (RandomAccess.Read(handle, header, 0) < header.Length ||
            header[0] != (byte)'M' || header[1] != (byte)'Z')
        {
            throw Untrusted();
        }

        int peOffset = BitConverter.ToInt32(header[DosHeaderPeOffset..]);
        Span<byte> pe = stackalloc byte[6];
        if (peOffset < header.Length ||
            RandomAccess.Read(handle, pe, peOffset) != pe.Length ||
            BitConverter.ToUInt32(pe) != PortableExecutableSignature ||
            BitConverter.ToUInt16(pe[4..]) != (ushort)expected)
        {
            throw Untrusted();
        }
    }

    private static bool IsBinaryPath(string path) =>
        string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase);

    private static SafeFileHandle OpenFile(string path) =>
        OpenPath(path, directory: false);

    private static SafeFileHandle OpenDirectory(string path) =>
        OpenPath(path, directory: true);

    private static SafeFileHandle OpenPath(string path, bool directory)
    {
        SafeFileHandle handle = NativeMethods.CreateFile(
            ToExtendedLengthPath(path),
            directory ? GenericReadAttributes : GenericRead,
            ShareRead,
            IntPtr.Zero,
            OpenExisting,
            OpenReparsePoint |
                (directory ? BackupSemantics : (uint)FileOptions.RandomAccess),
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            throw new Win32Exception(error);
        }

        return handle;
    }

    private static void RequireHandleKind(
        SafeFileHandle handle,
        bool directory)
    {
        if (!NativeMethods.GetFileInformationByHandleEx(
                handle,
                9,
                out FileAttributeTagInfo info,
                Marshal.SizeOf<FileAttributeTagInfo>()))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        bool isDirectory = (info.FileAttributes & FileAttributes.Directory) != 0;
        if (isDirectory != directory ||
            (info.FileAttributes & FileAttributes.ReparsePoint) != 0 ||
            info.ReparseTag != 0)
        {
            throw Untrusted();
        }
    }

    private static OpenVinoWorkerFileIdentity GetIdentity(SafeFileHandle handle)
    {
        if (!NativeMethods.GetFileInformationByHandle(handle, out
            ByHandleFileInformation info))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return new OpenVinoWorkerFileIdentity(
            info.VolumeSerialNumber,
            ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow);
    }

    private static void RequireNoAlternateStreams(string path)
    {
        IntPtr find = NativeMethods.FindFirstStream(
            ToExtendedLengthPath(path),
            0,
            out FindStreamData data,
            0);
        if (find == InvalidFindHandle)
        {
            int error = Marshal.GetLastPInvokeError();
            if (error is ErrorNoMoreFiles or ErrorHandleEof)
            {
                return;
            }

            throw new Win32Exception(error);
        }

        try
        {
            do
            {
                if (!string.Equals(data.StreamName, "::$DATA", StringComparison.Ordinal))
                {
                    throw Untrusted();
                }
            }
            while (NativeMethods.FindNextStream(find, out data));

            int error = Marshal.GetLastPInvokeError();
            if (error is not ErrorNoMoreFiles and not ErrorHandleEof)
            {
                throw new Win32Exception(error);
            }
        }
        finally
        {
            _ = NativeMethods.FindClose(find);
        }
    }

    private static string GetFinalPath(SafeFileHandle handle)
    {
        char[] buffer = new char[512];
        while (true)
        {
            uint length = NativeMethods.GetFinalPathNameByHandle(
                handle, buffer, checked((uint)buffer.Length), 0);
            if (length == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            if (length < buffer.Length)
            {
                return NormalizeFinalPath(new string(buffer, 0, checked((int)length)));
            }

            buffer = new char[checked((int)length + 1)];
        }
    }

    private static string ToExtendedLengthPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        const string extendedPrefix = @"\\?\";
        const string uncPrefix = @"\\";
        if (fullPath.StartsWith(extendedPrefix, StringComparison.Ordinal))
        {
            return fullPath;
        }

        return fullPath.StartsWith(uncPrefix, StringComparison.Ordinal)
            ? @"\\?\UNC\" + fullPath[uncPrefix.Length..]
            : extendedPrefix + fullPath;
    }

    private static string NormalizeFinalPath(string path)
    {
        const string uncPrefix = @"\\?\UNC\";
        const string prefix = @"\\?\";
        string value = path.StartsWith(uncPrefix, StringComparison.OrdinalIgnoreCase)
            ? @"\\" + path[uncPrefix.Length..]
            : path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? path[prefix.Length..]
                : path;
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
    }

    private static void RequireFinalContained(
        string root,
        string finalPath,
        bool allowRoot = false)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(finalPath));
        if (allowRoot && string.Equals(normalizedRoot, normalizedPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RequireContained(normalizedRoot, normalizedPath);
    }

    private static void RequireContained(string root, string path)
    {
        string prefix = Path.TrimEndingDirectorySeparator(root) +
            Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw Untrusted();
        }
    }

    private static void RequireClosedTopology(
        string root,
        IReadOnlyList<ManifestFile> manifest,
        HashSet<string> expectedDirectories,
        Dictionary<string, OpenVinoWorkerFileIdentity> identities)
    {
        HashSet<string> actualFiles = new(StringComparer.Ordinal);
        HashSet<string> actualDirectories = new(StringComparer.Ordinal);
        IEnumerable<string> parents = new[] { string.Empty }.Concat(
            expectedDirectories
                .OrderBy(static path => path.Count(static value => value == '/'))
                .ThenBy(static path => path, StringComparer.Ordinal));
        foreach (string parent in parents)
        {
            string parentPath = string.IsNullOrEmpty(parent)
                ? root
                : Path.Combine(
                    root,
                    parent.Replace('/', Path.DirectorySeparatorChar));
            foreach (string entry in Directory.EnumerateFileSystemEntries(
                parentPath,
                "*",
                SearchOption.TopDirectoryOnly))
            {
                string relative = NormalizeRelative(root, entry);
                bool expectedDirectory = expectedDirectories.Contains(relative);
                string identityKey = expectedDirectory ? relative + "/" : relative;
                using SafeFileHandle handle = expectedDirectory
                    ? OpenDirectory(entry)
                    : OpenFile(entry);
                RequireHandleKind(handle, expectedDirectory);
                RequireFinalContained(root, GetFinalPath(handle));
                RequireNoAlternateStreams(entry);
                if (!identities.TryGetValue(identityKey, out
                    OpenVinoWorkerFileIdentity expectedIdentity) ||
                    GetIdentity(handle) != expectedIdentity)
                {
                    throw Untrusted();
                }

                if (expectedDirectory)
                {
                    actualDirectories.Add(relative);
                }
                else if (!string.Equals(
                    relative,
                    "worker-manifest.json",
                    StringComparison.Ordinal))
                {
                    actualFiles.Add(relative);
                }
            }
        }

        RequireSetEqual(expectedDirectories, actualDirectories);
        RequireSetEqual(manifest.Select(static file => file.Path), actualFiles);
    }

    private static string NormalizeRelative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static void RequireSetEqual(
        IEnumerable<string> expected,
        IEnumerable<string> actual)
    {
        if (!expected.Order(StringComparer.Ordinal).SequenceEqual(
            actual.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw Untrusted();
        }
    }

    private static WorkerClientPolicyException Untrusted() =>
        WorkerClientPolicyException.For(
            WorkerClientFailureCodes.WorkerExecutableUntrusted,
            "The OpenVINO worker closure could not be trusted.");

    private sealed record ManifestFile(string Path, long Length, string Sha256);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileAttributeTagInfo
    {
        internal FileAttributes FileAttributes;
        internal uint ReparseTag;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        internal uint LowDateTime;
        internal uint HighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        internal uint FileAttributes;
        internal NativeFileTime CreationTime;
        internal NativeFileTime LastAccessTime;
        internal NativeFileTime LastWriteTime;
        internal uint VolumeSerialNumber;
        internal uint FileSizeHigh;
        internal uint FileSizeLow;
        internal uint NumberOfLinks;
        internal uint FileIndexHigh;
        internal uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FindStreamData
    {
        internal long StreamSize;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)]
        internal string StreamName;
    }

    private static class NativeMethods
    {
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW",
            CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx",
            ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileInformationByHandleEx(
            SafeFileHandle file,
            int informationClass,
            out FileAttributeTagInfo information,
            int informationSize);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandle",
            ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileInformationByHandle(
            SafeFileHandle file,
            out ByHandleFileInformation information);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW",
            CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        internal static extern uint GetFinalPathNameByHandle(
            SafeFileHandle file,
            [Out] char[] filePath,
            uint filePathLength,
            uint flags);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", EntryPoint = "FindFirstStreamW",
            CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        internal static extern IntPtr FindFirstStream(
            string fileName,
            int infoLevel,
            out FindStreamData data,
            uint flags);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", EntryPoint = "FindNextStreamW",
            CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FindNextStream(
            IntPtr findStream,
            out FindStreamData data);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", EntryPoint = "FindClose",
            ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FindClose(IntPtr findFile);
    }
}
