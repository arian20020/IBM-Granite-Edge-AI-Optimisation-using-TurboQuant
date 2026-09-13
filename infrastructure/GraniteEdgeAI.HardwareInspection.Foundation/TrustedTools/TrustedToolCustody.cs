using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

internal sealed class TrustedToolCustody : IDisposable
{
    private readonly IDisposable[] _resources;
    private readonly object _sync = new();
    private int _referenceCount = 1;
    private bool _ownerDisposed;

    internal TrustedToolCustody(IEnumerable<IDisposable> resources)
    {
        _resources = resources.ToArray();
    }

    public void Dispose()
    {
        IDisposable[]? resourcesToDispose = null;
        lock (_sync)
        {
            if (_ownerDisposed)
            {
                return;
            }

            _ownerDisposed = true;
            _referenceCount--;
            if (_referenceCount == 0)
            {
                resourcesToDispose = _resources;
            }
        }

        DisposeAllReverse(resourcesToDispose);
    }

    internal bool TryAcquire(out IDisposable? lease)
    {
        lock (_sync)
        {
            if (_ownerDisposed)
            {
                lease = null;
                return false;
            }

            _referenceCount++;
            lease = new CustodyLease(this);
            return true;
        }
    }

    private void Release()
    {
        IDisposable[]? resourcesToDispose = null;
        lock (_sync)
        {
            _referenceCount--;
            if (_referenceCount == 0)
            {
                resourcesToDispose = _resources;
            }
        }

        DisposeAllReverse(resourcesToDispose);
    }

    private static void DisposeAllReverse(IDisposable[]? resources)
    {
        if (resources is null)
        {
            return;
        }

        foreach (IDisposable resource in resources.Reverse())
        {
            resource.Dispose();
        }
    }

    internal static bool TryAcquireDirectories(
        string approvedRoot,
        string packageRoot,
        out IReadOnlyList<SafeFileHandle> handles,
        out CustodyOpenFailure failure)
    {
        List<SafeFileHandle> acquired = [];
        try
        {
            foreach (string directory in EnumerateCustodyDirectories(approvedRoot, packageRoot))
            {
                SafeFileHandle handle = CreateFile(
                    directory,
                    desiredAccess: 0,
                    shareMode: FileShare.Read,
                    securityAttributes: IntPtr.Zero,
                    creationDisposition: FileMode.Open,
                    flagsAndAttributes: FileFlagBackupSemantics | FileFlagOpenReparsePoint,
                    templateFile: IntPtr.Zero);
                if (handle.IsInvalid)
                {
                    handle.Dispose();
                    DisposeAll(acquired);
                    handles = [];
                    failure = CustodyOpenFailure.Unavailable;
                    return false;
                }

                if (!TryInspectHandle(handle, out FileAttributes attributes))
                {
                    handle.Dispose();
                    DisposeAll(acquired);
                    handles = [];
                    failure = CustodyOpenFailure.Unavailable;
                    return false;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    handle.Dispose();
                    DisposeAll(acquired);
                    handles = [];
                    failure = CustodyOpenFailure.ReparsePoint;
                    return false;
                }

                acquired.Add(handle);
            }

            handles = acquired;
            failure = CustodyOpenFailure.None;
            return true;
        }
        catch
        {
            DisposeAll(acquired);
            handles = [];
            failure = CustodyOpenFailure.Unavailable;
            return false;
        }
    }

    internal static bool TryAcquireFile(
        string path,
        out FileStream? stream,
        out CustodyOpenFailure failure)
    {
        SafeFileHandle handle = CreateFile(
            path,
            GenericRead,
            FileShare.Read,
            IntPtr.Zero,
            FileMode.Open,
            FileFlagOpenReparsePoint | FileFlagSequentialScan,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            stream = null;
            failure = CustodyOpenFailure.Unavailable;
            return false;
        }

        if (!TryInspectHandle(handle, out FileAttributes attributes))
        {
            handle.Dispose();
            stream = null;
            failure = CustodyOpenFailure.Unavailable;
            return false;
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0 ||
            (attributes & FileAttributes.Directory) != 0)
        {
            handle.Dispose();
            stream = null;
            failure = CustodyOpenFailure.ReparsePoint;
            return false;
        }

        stream = new FileStream(handle, FileAccess.Read, bufferSize: 4096, isAsync: false);
        failure = CustodyOpenFailure.None;
        return true;
    }

    private static bool TryInspectHandle(
        SafeFileHandle handle,
        out FileAttributes attributes)
    {
        if (GetFileInformationByHandleEx(
                handle,
                FileAttributeTagInfo,
                out FileAttributeTagInformation information,
                (uint)Marshal.SizeOf<FileAttributeTagInformation>()))
        {
            attributes = information.FileAttributes;
            return true;
        }

        attributes = default;
        return false;
    }

    private static IEnumerable<string> EnumerateCustodyDirectories(
        string approvedRoot,
        string packageRoot)
    {
        yield return approvedRoot;
        string relative = Path.GetRelativePath(approvedRoot, packageRoot);
        if (relative == ".")
        {
            yield break;
        }

        string current = approvedRoot;
        foreach (string segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            yield return current;
        }
    }

    private static void DisposeAll(IEnumerable<IDisposable> resources)
    {
        foreach (IDisposable resource in resources)
        {
            resource.Dispose();
        }
    }

    private sealed class CustodyLease(TrustedToolCustody owner) : IDisposable
    {
        private TrustedToolCustody? _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release();
    }

    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagSequentialScan = 0x08000000;
    private const uint GenericRead = 0x80000000;
    private const int FileAttributeTagInfo = 9;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        IntPtr securityAttributes,
        FileMode creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle file,
        int fileInformationClass,
        out FileAttributeTagInformation fileInformation,
        uint bufferSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileAttributeTagInformation
    {
        internal FileAttributes FileAttributes;
        internal uint ReparseTag;
    }
}

internal enum CustodyOpenFailure
{
    None,
    ReparsePoint,
    Unavailable,
}
