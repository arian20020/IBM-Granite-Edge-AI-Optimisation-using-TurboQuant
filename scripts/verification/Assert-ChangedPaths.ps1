[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string[]]$AllowedPath,
    [string]$WritePathspec,
    [switch]$StageVerified,
    [Parameter(ValueFromRemainingArguments)][string[]]$RemainingAllowedPath
)

$ErrorActionPreference = 'Stop'
$modulePath = Join-Path -Path $PSScriptRoot -ChildPath 'CrossRouteImportManifest.Core.psm1'
Import-Module -Force -Name $modulePath

if (-not ('ChangedPathBoundPublisher' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

public static class ChangedPathBoundPublisher
{
    private const uint FILE_READ_ATTRIBUTES = 0x00000080;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint CREATE_NEW = 1;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
    private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    private const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint FileAttributes;
        public FILETIME CreationTime;
        public FILETIME LastAccessTime;
        public FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file, out BY_HANDLE_FILE_INFORMATION information);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle file, StringBuilder path, uint pathLength, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteFile(
        SafeFileHandle file, byte[] buffer, uint bytesToWrite, out uint bytesWritten, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushFileBuffers(SafeFileHandle file);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteFileW(string fileName);

    public static void WriteNew(string path, byte[] bytes)
    {
        if (path == null) throw new ArgumentNullException("path");
        if (bytes == null) throw new ArgumentNullException("bytes");
        string fullPath = Path.GetFullPath(path);
        string parent = Path.GetDirectoryName(fullPath);
        if (String.IsNullOrWhiteSpace(parent)) throw new InvalidOperationException("WritePathspec parent is invalid.");

        List<SafeFileHandle> directoryHandles = new List<SafeFileHandle>();
        SafeFileHandle output = null;
        bool outputCreated = false;
        Exception primaryFailure = null;
        try
        {
            List<string> ancestry = new List<string>();
            DirectoryInfo cursor = new DirectoryInfo(parent);
            while (cursor != null)
            {
                ancestry.Add(cursor.FullName);
                cursor = cursor.Parent;
            }
            ancestry.Reverse();
            foreach (string directory in ancestry)
            {
                SafeFileHandle handle = CreateFileW(directory, FILE_READ_ATTRIBUTES,
                    FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING,
                    FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, IntPtr.Zero);
                if (handle.IsInvalid)
                {
                    handle.Dispose();
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to bind pathspec output ancestry.");
                }
                directoryHandles.Add(handle);
                AssertDirectoryHandle(handle, directory);
            }

            if (File.Exists(fullPath) || Directory.Exists(fullPath))
                throw new IOException("WritePathspec target must be absent; a pre-existing sentinel is never overwritten.");

            output = CreateFileW(fullPath, GENERIC_WRITE | FILE_READ_ATTRIBUTES, 0, IntPtr.Zero,
                CREATE_NEW, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_WRITE_THROUGH,
                IntPtr.Zero);
            if (output.IsInvalid)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create bound pathspec output.");
            outputCreated = true;
            AssertOutputHandle(output, fullPath);

            int offset = 0;
            while (offset < bytes.Length)
            {
                int remaining = bytes.Length - offset;
                byte[] chunk;
                if (offset == 0)
                    chunk = bytes;
                else
                {
                    chunk = new byte[remaining];
                    Buffer.BlockCopy(bytes, offset, chunk, 0, remaining);
                }
                uint written;
                if (!WriteFile(output, chunk, (uint)remaining, out written, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to write bound pathspec output.");
                if (written == 0 || written > remaining)
                    throw new IOException("Bound pathspec output made no valid write progress.");
                offset += (int)written;
            }
            if (!FlushFileBuffers(output))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to flush bound pathspec output.");
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
            throw;
        }
        finally
        {
            if (output != null) output.Dispose();
            Exception cleanupFailure = null;
            if (primaryFailure != null && outputCreated && !DeleteFileW(fullPath))
                cleanupFailure = new Win32Exception(Marshal.GetLastWin32Error(), "Unable to remove rejected bound pathspec output.");
            for (int index = directoryHandles.Count - 1; index >= 0; index--)
                directoryHandles[index].Dispose();
            if (cleanupFailure != null)
                throw new AggregateException("Bound pathspec publication and cleanup failed.", primaryFailure, cleanupFailure);
        }
    }

    private static void AssertDirectoryHandle(SafeFileHandle handle, string expectedPath)
    {
        BY_HANDLE_FILE_INFORMATION information;
        if (!GetFileInformationByHandle(handle, out information))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to inspect bound pathspec ancestry.");
        if ((information.FileAttributes & FILE_ATTRIBUTE_DIRECTORY) == 0 ||
            (information.FileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0)
            throw new InvalidOperationException("ReparsePoint ancestry is forbidden: " + expectedPath);
        AssertResolvedPath(handle, expectedPath, "Pathspec output ancestry resolved to a different identity.");
    }

    private static void AssertOutputHandle(SafeFileHandle handle, string expectedPath)
    {
        BY_HANDLE_FILE_INFORMATION information;
        if (!GetFileInformationByHandle(handle, out information))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to inspect bound pathspec output.");
        if ((information.FileAttributes & (FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_REPARSE_POINT)) != 0)
            throw new InvalidOperationException("Bound pathspec output is not an exact regular file.");
        AssertResolvedPath(handle, expectedPath, "Bound pathspec output resolved to a different identity.");
    }

    private static void AssertResolvedPath(SafeFileHandle handle, string expectedPath, string message)
    {
        StringBuilder builder = new StringBuilder(32768);
        uint length = GetFinalPathNameByHandleW(handle, builder, (uint)builder.Capacity, 0);
        if (length == 0 || length >= builder.Capacity)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to resolve bound pathspec identity.");
        string actual = NormalizeFinalPath(builder.ToString());
        string expected = Path.GetFullPath(expectedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!StringComparer.OrdinalIgnoreCase.Equals(actual.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), expected))
            throw new InvalidOperationException(message);
    }

    private static string NormalizeFinalPath(string path)
    {
        const string uncPrefix = @"\\?\UNC\";
        const string localPrefix = @"\\?\";
        if (path.StartsWith(uncPrefix, StringComparison.OrdinalIgnoreCase))
            return @"\\" + path.Substring(uncPrefix.Length);
        if (path.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
            return path.Substring(localPrefix.Length);
        return path;
    }
}
'@
}

function Normalize-GitPath {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or $Path.Contains('\')) {
        throw 'A canonical repository-relative path without backslashes is required.'
    }
    if (-not $Path.IsNormalized([Text.NormalizationForm]::FormC)) {
        throw 'A path must use Unicode NormalizationForm.FormC.'
    }
    $value = $Path
    $segments = @($value.Split('/'))
    $invalidSegments = @($segments | Where-Object { $_ -eq '.' -or $_ -eq '..' -or $_ -eq '' }).Count
    if ($value.StartsWith('/') -or $value -match '^[A-Za-z]:' -or $value.Contains(':') -or $value -match '[<>"|?*]' -or $value.Contains([char]0) -or $value -match '[\x00-\x1f\x7f]' -or $invalidSegments -ne 0) {
        throw 'A repository-relative non-traversing path is required.'
    }
    foreach ($segment in $segments) {
        if ($segment.EndsWith('.', [StringComparison]::Ordinal) -or $segment.EndsWith(' ', [StringComparison]::Ordinal)) {
            throw 'A path segment cannot have a trailing dot or space.'
        }
        $deviceName = $segment.Split('.')[0]
        if ($deviceName -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$') {
            throw 'A path segment cannot use a reserved device name.'
        }
    }
    return $value
}

function Normalize-AllowedRule {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Rule)

    if ($Rule.EndsWith('/**', [StringComparison]::Ordinal)) {
        $prefix = Normalize-GitPath -Path $Rule.Substring(0, $Rule.Length - 3)
        return $prefix + '/**'
    }
    if ($Rule.Contains('*')) {
        throw 'Only a trailing /** owned prefix is supported.'
    }
    return Normalize-GitPath -Path $Rule
}

function Assert-NoRuleCollisions {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string[]]$Rule)

    $seen = New-Object -TypeName 'Collections.Generic.HashSet[string]' -ArgumentList ([StringComparer]::OrdinalIgnoreCase)
    foreach ($candidate in $Rule) {
        $normalized = $candidate.Normalize([Text.NormalizationForm]::FormC)
        if (-not $seen.Add($normalized)) {
            throw "Allowlist contains an OrdinalIgnoreCase or Unicode normalization collision: $candidate"
        }
    }
}

function Invoke-ChangedPathGit {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string[]]$Arguments,
        [switch]$AllowFailure,
        [switch]$InspectFsmonitorFlags,
        [hashtable]$Environment = @{}
    )

    $safeArguments = @(
        '-c'
        'core.fsmonitor=false'
        '-c'
        'core.hooksPath=NUL'
        '-c'
        'core.untrackedCache=false'
        '-c'
        'core.preloadIndex=false'
    )
    if ($InspectFsmonitorFlags) {
        $safeArguments += @('-c', 'core.fsmonitor=true')
    }
    $safeArguments += @('--literal-pathspecs') + $Arguments
    return Invoke-CrossRouteGit `
        -Repository $Repository `
        -Arguments $safeArguments `
        -AllowFailure:$AllowFailure `
        -Environment $Environment
}

function Assert-SafeRepositoryGitConfiguration {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Repository)

    foreach ($entry in @(
        [pscustomobject]@{
            Key = 'core.fsmonitor'
            Message = 'Repository-local core.fsmonitor is forbidden.'
        }
        [pscustomobject]@{
            Key = 'core.hooksPath'
            Message = 'Repository-local core.hooksPath is forbidden.'
        }
        [pscustomobject]@{
            Key = 'core.untrackedCache'
            Message = 'Repository-local core.untrackedCache is forbidden.'
        }
    )) {
        $result = Invoke-CrossRouteGit `
            -Repository $Repository `
            -Arguments @('config', '--local', '--null', '--get-all', [string]$entry.Key) `
            -AllowFailure
        if ($result.ExitCode -notin @(0, 1)) {
            throw "Repository-local Git configuration inspection failed: $($entry.Key)"
        }
        if ($result.StdoutBytes.Length -eq 0) {
            continue
        }
        [void](ConvertFrom-NulGitRecords -Bytes $result.StdoutBytes)
        throw [string]$entry.Message
    }
    $names = Invoke-CrossRouteGit -Repository $Repository -Arguments @('config', '--local', '--name-only', '--null', '--list')
    foreach ($key in @(ConvertFrom-NulGitRecords -Bytes $names.StdoutBytes)) {
        if ($key -imatch '^(?:filter\.|gpg\.|commit\.gpgsign$|user\.signingkey$)') {
            throw 'Repository-local executable Git configuration is forbidden.'
        }
    }
}

function ConvertFrom-NulGitRecords {
    [CmdletBinding()]
    param([Parameter(Mandatory)][AllowEmptyCollection()][byte[]]$Bytes)

    $strictUtf8 = New-Object -TypeName Text.UTF8Encoding -ArgumentList @($false, $true)
    $records = New-Object -TypeName 'Collections.Generic.List[string]'
    $recordStart = 0
    $byteIndex = 0
    while ($byteIndex -lt $Bytes.Length) {
        if ($Bytes[$byteIndex] -eq 0) {
            $records.Add($strictUtf8.GetString($Bytes, $recordStart, $byteIndex - $recordStart))
            $recordStart = $byteIndex + 1
        }
        $byteIndex++
    }
    if ($recordStart -ne $Bytes.Length) {
        throw 'Git output was not NUL terminated.'
    }
    return $records.ToArray()
}

function Assert-NoFsmonitorValidRecords {
    [CmdletBinding()]
    param([Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Record)

    foreach ($item in $Record) {
        if ($item.Length -lt 3 -or $item[1] -ne ' ') {
            throw 'Git fsmonitor-valid output was malformed.'
        }
        $tag = [string]$item[0]
        if ($tag -cnotmatch '^[A-Za-z]$') {
            throw 'Git fsmonitor-valid output was malformed.'
        }
        $path = Normalize-GitPath -Path $item.Substring(2)
        if ($tag -cmatch '^[a-z]$') {
            throw "Index contains fsmonitor-valid path: $path"
        }
    }
}

function Get-IndexSnapshot {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Repository)

    $fsmonitorFlags = Invoke-ChangedPathGit `
        -Repository $Repository `
        -Arguments @('ls-files', '-f', '-z') `
        -InspectFsmonitorFlags
    $fsmonitorRecords = @(ConvertFrom-NulGitRecords -Bytes $fsmonitorFlags.StdoutBytes)
    Assert-NoFsmonitorValidRecords -Record $fsmonitorRecords
    $entries = Invoke-ChangedPathGit `
        -Repository $Repository `
        -Arguments @('ls-files', '--stage', '-z')
    $flags = Invoke-ChangedPathGit `
        -Repository $Repository `
        -Arguments @('ls-files', '-v', '-z')
    foreach ($record in @(ConvertFrom-NulGitRecords -Bytes $flags.StdoutBytes)) {
        if ($record.Length -lt 3 -or $record[1] -ne ' ') {
            throw 'Git index flag output was malformed.'
        }
        $tag = [string]$record[0]
        $path = Normalize-GitPath -Path $record.Substring(2)
        if ($tag -ceq 'S') {
            throw "Index contains skip-worktree path: $path"
        }
        if ($tag -cmatch '^[a-z]$') {
            throw "Index contains assume-unchanged path: $path"
        }
    }
    return [pscustomobject]@{
        Entries = [Convert]::ToBase64String($entries.StdoutBytes)
        Flags = [Convert]::ToBase64String($flags.StdoutBytes)
        FsmonitorFlags = [Convert]::ToBase64String($fsmonitorFlags.StdoutBytes)
    }
}

function Assert-NonReparseAncestry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [switch]$IncludeLeaf
    )

    $fullPath = [IO.Path]::GetFullPath($Path)
    $cursor = if ($IncludeLeaf) { $fullPath } else { Split-Path -Parent $fullPath }
    while (-not [string]::IsNullOrWhiteSpace($cursor)) {
        if ([IO.Directory]::Exists($cursor) -or [IO.File]::Exists($cursor)) {
            $attributes = [IO.File]::GetAttributes($cursor)
            if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "ReparsePoint ancestry is forbidden: $cursor"
            }
        }
        $parent = Split-Path -Parent $cursor
        if ([string]::IsNullOrWhiteSpace($parent) -or [StringComparer]::OrdinalIgnoreCase.Equals($parent, $cursor)) {
            break
        }
        $cursor = $parent
    }
    return $fullPath
}

function Get-RepositorySnapshot {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Repository)

    $index = Get-IndexSnapshot -Repository $Repository
    return [pscustomobject]@{
        Head = (Invoke-ChangedPathGit -Repository $Repository -Arguments @('rev-parse', '--verify', '--end-of-options', 'HEAD')).Stdout.Trim()
        Tree = (Invoke-ChangedPathGit -Repository $Repository -Arguments @('rev-parse', '--verify', '--end-of-options', 'HEAD^{tree}')).Stdout.Trim()
        IndexEntries = $index.Entries
        IndexFlags = $index.Flags
        IndexFsmonitorFlags = $index.FsmonitorFlags
        Status = [Convert]::ToBase64String((Invoke-ChangedPathGit -Repository $Repository -Arguments @('status', '--porcelain=v1', '-z', '--untracked-files=all')).StdoutBytes)
    }
}

function Assert-RepositorySnapshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Before,
        [Parameter(Mandatory)][object]$After
    )

    if ($Before.Head -cne $After.Head -or $Before.Tree -cne $After.Tree -or $Before.IndexEntries -cne $After.IndexEntries -or $Before.IndexFlags -cne $After.IndexFlags -or $Before.IndexFsmonitorFlags -cne $After.IndexFsmonitorFlags -or $Before.Status -cne $After.Status) {
        throw 'Repository HEAD/tree/index/status mutated during allowlist verification.'
    }
}

function Resolve-PathspecTarget {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Repository
    )

    $fullPath = [IO.Path]::GetFullPath($Path)
    if ([IO.File]::Exists($fullPath) -or [IO.Directory]::Exists($fullPath)) {
        throw 'WritePathspec target must be absent; a pre-existing sentinel is never overwritten.'
    }
    $root = [IO.Path]::GetFullPath($Repository).TrimEnd('\', '/')
    if ([StringComparer]::OrdinalIgnoreCase.Equals($fullPath, $root) -or $fullPath.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'WritePathspec must be outside the repository worktree.'
    }
    $parent = Split-Path -Parent $fullPath
    if ([string]::IsNullOrWhiteSpace($parent) -or -not [IO.Directory]::Exists($parent)) {
        throw 'WritePathspec parent must already exist.'
    }
    [void](Assert-NonReparseAncestry -Path $parent -IncludeLeaf)
    return $fullPath
}

function Write-AtomicPathspec {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][AllowEmptyCollection()][byte[]]$Bytes
    )

    [ChangedPathBoundPublisher]::WriteNew([IO.Path]::GetFullPath($Path), $Bytes)
}

function Set-VerifiedChangedPathIndex {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Repository, [Parameter(Mandatory)][string[]]$Path)

    $repositoryIdentity = Get-RepositoryIdentity -Repository $Repository -Prefix 'destination' -IncludeIndex
    $transaction = New-VerifiedTemporaryIndex -Repository $Repository
    $indexEnvironment = @{ GIT_INDEX_FILE = [string]$transaction.TemporaryPath }
    try {
        foreach ($candidate in $Path) {
            $fullPath = [IO.Path]::GetFullPath((Join-Path $Repository ($candidate -replace '/', [IO.Path]::DirectorySeparatorChar)))
            if (-not [IO.File]::Exists($fullPath)) {
                [void](Invoke-ChangedPathGit -Repository $Repository -Arguments @('update-index', '--remove', '--', $candidate) -Environment $indexEnvironment)
                continue
            }
            [void](Assert-NonReparseAncestry -Path $fullPath -IncludeLeaf)
            $attributes = [IO.File]::GetAttributes($fullPath)
            if (($attributes -band ([IO.FileAttributes]::Directory -bor [IO.FileAttributes]::ReparsePoint)) -ne 0) {
                throw "Verified staging input is not a regular non-reparse file: $candidate"
            }
            $existing = Invoke-ChangedPathGit -Repository $Repository -Arguments @('ls-files', '--stage', '-z', '--', $candidate) -Environment $indexEnvironment
            $records = @(ConvertFrom-NulGitRecords -Bytes $existing.StdoutBytes)
            $mode = '100644'
            if ($records.Count -ne 0) {
                if ($records.Count -ne 1 -or $records[0] -cnotmatch '^(?<mode>[0-9]{6}) [0-9a-f]{40} 0\t(?<path>.+)$' -or $Matches.path -cne $candidate -or $Matches.mode -cnotin @('100644', '100755')) {
                    throw "Verified staging input has an unsupported index identity: $candidate"
                }
                $mode = $Matches.mode
            }
            $oid = (Invoke-ChangedPathGit -Repository $Repository -Arguments @('hash-object', '-w', '--no-filters', '--', $fullPath)).Stdout.Trim()
            if ($oid -cnotmatch '^[0-9a-f]{40}$') { throw 'Verified staging blob OID is malformed.' }
            $worktreeBytes = [IO.File]::ReadAllBytes($fullPath)
            $blobBytes = (Invoke-ChangedPathGit -Repository $Repository -Arguments @('cat-file', 'blob', $oid)).StdoutBytes
            if ((Get-Sha256 -Bytes $worktreeBytes) -cne (Get-Sha256 -Bytes $blobBytes)) {
                throw "No-filter staged blob differs from verified bytes: $candidate"
            }
            [void](Invoke-ChangedPathGit -Repository $Repository -Arguments @('update-index', '--add', '--cacheinfo', ($mode + ',' + $oid + ',' + $candidate)) -Environment $indexEnvironment)
        }
        $staged = @(ConvertFrom-NulGitRecords -Bytes (Invoke-ChangedPathGit -Repository $Repository -Arguments @('diff', '--cached', '--name-only', '--no-renames', '-z') -Environment $indexEnvironment).StdoutBytes)
        $expected = @($Path | Sort-Object -CaseSensitive -Unique)
        $actual = @($staged | Sort-Object -CaseSensitive -Unique)
        if ($expected.Count -ne $actual.Count -or [string]::Join("`n", $expected) -cne [string]::Join("`n", $actual)) {
            throw 'Verified temporary index does not contain the exact changed-path endpoint set.'
        }
        Assert-NoHiddenIndexState -Repository $Repository -Environment $indexEnvironment
        Assert-RepositorySnapshot -Before $script:repositoryBefore -After (Get-RepositorySnapshot -Repository $Repository)
        $temporaryState = Get-VerifiedIndexFileState -Path $transaction.TemporaryPath
        Publish-VerifiedTemporaryIndex -Repository $Repository -Transaction $transaction -ExpectedRepositoryIdentity $repositoryIdentity -ExpectedTemporaryState $temporaryState
    }
    finally {
        Remove-VerifiedTemporaryIndex -Transaction $transaction
    }
}

function Test-Allowed {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    foreach ($ruleValue in $script:rules) {
        if ($ruleValue.EndsWith('/**', [StringComparison]::Ordinal)) {
            $prefix = $ruleValue.Substring(0, $ruleValue.Length - 3).TrimEnd('/')
            if ($Path -eq $prefix -or $Path.StartsWith($prefix + '/', [StringComparison]::Ordinal)) {
                return $true
            }
        }
        elseif ($Path -ceq $ruleValue) {
            return $true
        }
    }
    return $false
}

$repositoryRoot = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Repository).Path).TrimEnd('\', '/')
[void](Assert-NonReparseAncestry -Path $repositoryRoot -IncludeLeaf)
$rootResult = Invoke-ChangedPathGit -Repository $repositoryRoot -Arguments @('rev-parse', '--show-toplevel')
$gitRoot = [IO.Path]::GetFullPath(($rootResult.Stdout.Trim() -replace '/', [IO.Path]::DirectorySeparatorChar)).TrimEnd('\', '/')
if (-not [StringComparer]::OrdinalIgnoreCase.Equals($repositoryRoot, $gitRoot)) {
    throw 'Repository must be an exact Git worktree root.'
}
Assert-SafeRepositoryGitConfiguration -Repository $repositoryRoot
$allAllowedPaths = @($AllowedPath) + @($RemainingAllowedPath | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
$script:rules = @($allAllowedPaths | ForEach-Object { Normalize-AllowedRule -Rule ([string]$_) })
Assert-NoRuleCollisions -Rule ([string[]]$script:rules)
if (-not $StageVerified -and [string]::IsNullOrWhiteSpace($WritePathspec)) {
    throw 'Either StageVerified or WritePathspec must be selected.'
}
$pathspecTarget = if ([string]::IsNullOrWhiteSpace($WritePathspec)) { $null } else { Resolve-PathspecTarget -Path $WritePathspec -Repository $repositoryRoot }
$repositoryBefore = Get-RepositorySnapshot -Repository $repositoryRoot
$script:repositoryBefore = $repositoryBefore

$rawBytes = (Invoke-ChangedPathGit -Repository $repositoryRoot -Arguments @('status', '--porcelain=v1', '-z', '--untracked-files=all')).StdoutBytes
$records = New-Object -TypeName 'Collections.Generic.List[string]'
$strictUtf8 = New-Object -TypeName Text.UTF8Encoding -ArgumentList @($false, $true)
$recordStart = 0
$byteIndex = 0
while ($byteIndex -lt $rawBytes.Length) {
    if ($rawBytes[$byteIndex] -eq 0) {
        if ($byteIndex -gt $recordStart) {
            $records.Add($strictUtf8.GetString($rawBytes, $recordStart, $byteIndex - $recordStart))
        }
        $recordStart = $byteIndex + 1
    }
    $byteIndex++
}
if ($recordStart -ne $rawBytes.Length) {
    throw 'Git porcelain output was not NUL terminated.'
}

$changed = New-Object -TypeName 'Collections.Generic.List[string]'
$index = 0
while ($index -lt $records.Count) {
    $record = $records[$index]
    if ($record.Length -lt 4 -or $record[2] -ne ' ') {
        throw 'Unexpected Git porcelain record.'
    }
    $status = $record.Substring(0, 2)
    $path = Normalize-GitPath -Path $record.Substring(3)
    $paths = @($path)
    if ($status.Contains('R') -or $status.Contains('C')) {
        $index++
        if ($index -ge $records.Count) {
            throw 'Incomplete rename/copy record.'
        }
        $paths += Normalize-GitPath -Path $records[$index]
    }
    foreach ($candidate in $paths) {
        if (-not (Test-Allowed -Path $candidate)) {
            throw "Changed path is outside the allowlist: $candidate"
        }
        if (-not $changed.Contains($candidate)) {
            $changed.Add($candidate)
        }
    }
    $index++
}

$bytes = New-Object -TypeName 'Collections.Generic.List[byte]'
foreach ($path in $changed) {
    $bytes.AddRange([Text.Encoding]::UTF8.GetBytes($path))
    $bytes.Add(0)
}
Assert-SafeRepositoryGitConfiguration -Repository $repositoryRoot
$repositoryAfter = Get-RepositorySnapshot -Repository $repositoryRoot
Assert-RepositorySnapshot -Before $repositoryBefore -After $repositoryAfter
if ($StageVerified) {
    Set-VerifiedChangedPathIndex -Repository $repositoryRoot -Path ([string[]]$changed.ToArray())
}
if (-not [string]::IsNullOrWhiteSpace($WritePathspec)) {
    Write-AtomicPathspec -Path $pathspecTarget -Bytes $bytes.ToArray()
}
Write-Output "Allowed changed paths: $($changed.Count); staged=$([bool]$StageVerified)"
