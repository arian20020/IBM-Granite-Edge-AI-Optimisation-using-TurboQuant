[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$WorkspaceRoot,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ArchiveRoot,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$FixtureRoot,

    [Parameter(Mandatory)]
    [ValidateRange(1, [long]::MaxValue)]
    [long]$ExpectedModelLength,

    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{64}$')]
    [string]$ExpectedModelSha256,

    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{64}$')]
    [string]$ExpectedFixtureManifestSha256
)

$ErrorActionPreference = 'Stop'

function Stop-Invalid {
    [Console]::Out.WriteLine('trusted_inputs_invalid')
    exit 1
}

function Test-EqualOrContained {
    param(
        [Parameter(Mandatory)][string]$Candidate,
        [Parameter(Mandatory)][string]$Root
    )

    $normalizedRoot = $Root.TrimEnd('\', '/')
    return $Candidate.TrimEnd('\', '/') -ieq $normalizedRoot -or
        $Candidate.StartsWith(
            $normalizedRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)
}

try {
    if (-not ('OpenVinoTrustedPath.NativePath' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace OpenVinoTrustedPath
{
    public static class NativePath
    {
        private const uint ShareRead = 0x00000001;
        private const uint ShareWrite = 0x00000002;
        private const uint ShareDelete = 0x00000004;
        private const uint OpenExisting = 3;
        private const uint BackupSemantics = 0x02000000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string name,
            uint access,
            uint share,
            IntPtr security,
            uint creation,
            uint flags,
            IntPtr template);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(
            SafeFileHandle file,
            StringBuilder path,
            uint length,
            uint flags);

        public static string GetFinalPath(string path)
        {
            using (SafeFileHandle handle = CreateFile(
                path,
                0,
                ShareRead | ShareWrite | ShareDelete,
                IntPtr.Zero,
                OpenExisting,
                BackupSemantics,
                IntPtr.Zero))
            {
                if (handle.IsInvalid)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                StringBuilder buffer = new StringBuilder(32768);
                uint length = GetFinalPathNameByHandle(
                    handle,
                    buffer,
                    (uint)buffer.Capacity,
                    0);
                if (length == 0 || length >= buffer.Capacity)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                string value = buffer.ToString();
                if (value.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                    return @"\\" + value.Substring(8);
                if (value.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                    return value.Substring(4);
                return value;
            }
        }
    }
}
'@
    }

    $workspace = [IO.Path]::GetFullPath($WorkspaceRoot).TrimEnd('\', '/')
    $archives = [IO.Path]::GetFullPath($ArchiveRoot).TrimEnd('\', '/')
    $fixture = [IO.Path]::GetFullPath($FixtureRoot).TrimEnd('\', '/')
    foreach ($root in @($workspace, $archives, $fixture)) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) {
            Stop-Invalid
        }
        $rootItem = Get-Item -LiteralPath $root -Force
        if ($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            Stop-Invalid
        }
    }

    if ((Test-EqualOrContained $archives $workspace) -or
        (Test-EqualOrContained $fixture $workspace) -or
        (Test-EqualOrContained $workspace $archives) -or
        (Test-EqualOrContained $workspace $fixture)) {
        Stop-Invalid
    }

    $workspaceFinal = [OpenVinoTrustedPath.NativePath]::GetFinalPath($workspace)
    $archiveFinal = [OpenVinoTrustedPath.NativePath]::GetFinalPath($archives)
    $fixtureFinal = [OpenVinoTrustedPath.NativePath]::GetFinalPath($fixture)
    if ((Test-EqualOrContained $archiveFinal $workspaceFinal) -or
        (Test-EqualOrContained $fixtureFinal $workspaceFinal) -or
        (Test-EqualOrContained $workspaceFinal $archiveFinal) -or
        (Test-EqualOrContained $workspaceFinal $fixtureFinal)) {
        Stop-Invalid
    }

    $archiveFiles = @()
    foreach ($definition in @(
        @{ Root = $archives; FinalRoot = $archiveFinal; Kind = 'archive' },
        @{ Root = $fixture; FinalRoot = $fixtureFinal; Kind = 'fixture' })) {
        foreach ($entry in Get-ChildItem -LiteralPath $definition.Root -Recurse -Force) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                Stop-Invalid
            }
            $entryFinal = [OpenVinoTrustedPath.NativePath]::GetFinalPath($entry.FullName)
            if (-not (Test-EqualOrContained $entryFinal $definition.FinalRoot)) {
                Stop-Invalid
            }
            if (-not $entry.PSIsContainer) {
                if (-not $entry.IsReadOnly) {
                    Stop-Invalid
                }
                if ($definition.Kind -ceq 'archive') {
                    $archiveFiles += $entry
                }
            }
        }
    }
    if ($archiveFiles.Count -eq 0) {
        Stop-Invalid
    }

    $modelPath = Join-Path $fixture 'package\openvino_model.bin'
    $manifestPath = Join-Path $fixture 'manifest.json'
    foreach ($requiredFile in @($modelPath, $manifestPath)) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf) -or
            -not (Test-EqualOrContained (
                [OpenVinoTrustedPath.NativePath]::GetFinalPath($requiredFile)) $fixtureFinal)) {
            Stop-Invalid
        }
    }

    $model = Get-Item -LiteralPath $modelPath -Force
    $modelSha = (Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifestSha = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($model.Length -ne $ExpectedModelLength -or
        $modelSha -cne $ExpectedModelSha256 -or
        $manifestSha -cne $ExpectedFixtureManifestSha256) {
        Stop-Invalid
    }

    [Console]::Out.WriteLine('trusted_inputs_valid')
    exit 0
}
catch {
    Stop-Invalid
}
