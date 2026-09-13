Set-StrictMode -Version Latest

if (-not ('OpenVinoTrustedLease.InputLease' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace OpenVinoTrustedLease
{
    public sealed class InputLease : IDisposable
    {
        private const uint GenericRead = 0x80000000;
        private const uint ShareRead = 0x00000001;
        private const uint OpenExisting = 3;
        private const uint BackupSemantics = 0x02000000;
        private readonly List<SafeFileHandle> handles = new List<SafeFileHandle>();
        private readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
        private int mutationObserved;
        private bool disposed;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string name, uint access, uint share, IntPtr security,
            uint creation, uint flags, IntPtr template);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(
            SafeFileHandle file, StringBuilder path, uint length, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle file, out ByHandleFileInformation information);

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        public InputLease(string[] roots, string[] paths, bool[] directories)
        {
            if (roots == null || paths == null || directories == null ||
                paths.Length != directories.Length)
                throw new ArgumentException("Lease topology is invalid.");
            try
            {
                HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
                foreach (string root in roots)
                {
                    FileSystemWatcher watcher = new FileSystemWatcher(root);
                    watcher.IncludeSubdirectories = true;
                    watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                        NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.Attributes |
                        NotifyFilters.Security;
                    watcher.Changed += OnMutation;
                    watcher.Created += OnMutation;
                    watcher.Deleted += OnMutation;
                    watcher.Renamed += OnRenamed;
                    watcher.Error += OnError;
                    watcher.EnableRaisingEvents = true;
                    watchers.Add(watcher);
                }
                for (int index = 0; index < paths.Length; index++)
                {
                    SafeFileHandle handle = CreateFile(
                        paths[index], GenericRead, ShareRead, IntPtr.Zero,
                        OpenExisting, directories[index] ? BackupSemantics : 0, IntPtr.Zero);
                    if (handle.IsInvalid)
                    {
                        int error = Marshal.GetLastWin32Error();
                        handle.Dispose();
                        throw new Win32Exception(error, "Cannot retain trusted input: " + paths[index]);
                    }
                    handles.Add(handle);
                    StringBuilder finalPath = new StringBuilder(32768);
                    uint finalLength = GetFinalPathNameByHandle(
                        handle, finalPath, (uint)finalPath.Capacity, 0);
                    if (finalLength == 0 || finalLength >= finalPath.Capacity)
                        throw new Win32Exception(Marshal.GetLastWin32Error(),
                            "Cannot resolve retained trusted input.");
                    string resolved = NormalizeFinalPath(finalPath.ToString());
                    string expected = Path.GetFullPath(paths[index]).TrimEnd('\\');
                    if (!string.Equals(resolved.TrimEnd('\\'), expected,
                        StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Retained trusted input final path changed.");
                    ByHandleFileInformation information;
                    if (!GetFileInformationByHandle(handle, out information))
                        throw new Win32Exception(Marshal.GetLastWin32Error(),
                            "Cannot identify retained trusted input.");
                    string identity = information.VolumeSerialNumber.ToString("x8") + ":" +
                        information.FileIndexHigh.ToString("x8") +
                        information.FileIndexLow.ToString("x8");
                    if (!identities.Add(identity) || information.NumberOfLinks != 1)
                        throw new IOException("Trusted input has an aliased file identity.");
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private static string NormalizeFinalPath(string path)
        {
            if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                return @"\\" + path.Substring(8);
            if (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                return path.Substring(4);
            return path;
        }

        public bool MutationObserved { get { return Volatile.Read(ref mutationObserved) != 0; } }

        private void OnMutation(object sender, FileSystemEventArgs args)
        {
            Interlocked.Exchange(ref mutationObserved, 1);
        }

        private void OnRenamed(object sender, RenamedEventArgs args)
        {
            Interlocked.Exchange(ref mutationObserved, 1);
        }

        private void OnError(object sender, ErrorEventArgs args)
        {
            Interlocked.Exchange(ref mutationObserved, 1);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (FileSystemWatcher watcher in watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            watchers.Clear();
            for (int index = handles.Count - 1; index >= 0; index--)
                handles[index].Dispose();
            handles.Clear();
        }
    }
}
'@
}

function Get-OpenVinoTrustedInputSnapshot {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string[]]$Roots)

    $snapshot = New-Object 'System.Collections.Generic.List[object]'
    foreach ($rootValue in $Roots) {
        $root = [IO.Path]::GetFullPath($rootValue).TrimEnd('\', '/')
        if (-not (Test-Path -LiteralPath $root -PathType Container)) {
            throw 'trusted-root-missing'
        }
        $rootItem = Get-Item -LiteralPath $root -Force
        if ($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'trusted-root-reparse'
        }
        $entries = @($rootItem) + @(Get-ChildItem -LiteralPath $root -Recurse -Force)
        foreach ($entry in $entries) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'trusted-entry-reparse'
            }
            $relative = if ($entry.FullName -ieq $root) {
                '.'
            } else {
                $entry.FullName.Substring($root.Length + 1).Replace('\', '/')
            }
            $isDirectory = [bool]$entry.PSIsContainer
            $streams = @(Get-Item -LiteralPath $entry.FullName -Stream * -ErrorAction Stop)
            if (@($streams | Where-Object { $_.Stream -cne ':$DATA' }).Count -ne 0) {
                throw 'trusted-entry-alternate-data-stream'
            }
            $length = if ($isDirectory) { 0L } else { [long]$entry.Length }
            $digest = if ($isDirectory) { '' } else {
                (Get-FileHash -LiteralPath $entry.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
            $snapshot.Add([pscustomobject]@{
                Root = $root
                Relative = $relative
                FullPath = [IO.Path]::GetFullPath($entry.FullName)
                IsDirectory = $isDirectory
                Length = $length
                Sha256 = $digest
            })
        }
    }
    return @($snapshot | Sort-Object Root, Relative)
}

function New-OpenVinoTrustedInputLease {
    [CmdletBinding()]
    param([Parameter(Mandatory)][object[]]$Snapshot)

    if ($Snapshot.Count -eq 0) { throw 'trusted-snapshot-empty' }
    [string[]]$paths = @($Snapshot | ForEach-Object { [string]$_.FullPath })
    [bool[]]$directories = @($Snapshot | ForEach-Object { [bool]$_.IsDirectory })
    [string[]]$roots = @($Snapshot | Select-Object -ExpandProperty Root -Unique)
    return [OpenVinoTrustedLease.InputLease]::new($roots, $paths, $directories)
}

function Assert-OpenVinoTrustedInputSnapshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string[]]$Roots,
        [Parameter(Mandatory)][object[]]$Expected
    )

    $actual = @(Get-OpenVinoTrustedInputSnapshot -Roots $Roots)
    if ($actual.Count -ne $Expected.Count) { throw 'trusted-topology-changed' }
    for ($index = 0; $index -lt $Expected.Count; $index++) {
        $left = $Expected[$index]
        $right = $actual[$index]
        if ($left.Root -ine $right.Root -or
            $left.Relative -cne $right.Relative -or
            $left.FullPath -ine $right.FullPath -or
            $left.IsDirectory -ne $right.IsDirectory -or
            $left.Length -ne $right.Length -or
            $left.Sha256 -cne $right.Sha256) {
            throw 'trusted-identity-changed'
        }
    }
}

function Assert-OpenVinoTrustedInputLeaseUnchanged {
    [CmdletBinding()]
    param([Parameter(Mandatory)][OpenVinoTrustedLease.InputLease]$Lease)
    if ($Lease.MutationObserved) { throw 'trusted-input-mutation-observed' }
}

Export-ModuleMember -Function Get-OpenVinoTrustedInputSnapshot,
    New-OpenVinoTrustedInputLease,Assert-OpenVinoTrustedInputSnapshot,
    Assert-OpenVinoTrustedInputLeaseUnchanged
