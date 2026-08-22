[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$PackageRoot,
    [Parameter(Mandatory)] [string]$ManifestPath
)

$ErrorActionPreference = 'Stop'

function Get-Sha256 {
    param([Parameter(Mandatory)] [string]$Path)

    $stream = [System.IO.File]::Open(
        $Path,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::Read)
    try {
        $algorithm = [System.Security.Cryptography.SHA256]::Create()
        try {
            return ([System.BitConverter]::ToString(
                $algorithm.ComputeHash($stream))).Replace('-', '')
        }
        finally {
            $algorithm.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

$root = [System.IO.Path]::GetFullPath($PackageRoot)
$manifestFile = [System.IO.Path]::GetFullPath($ManifestPath)
if (-not (Test-Path -LiteralPath $root -PathType Container) -or
    -not (Test-Path -LiteralPath $manifestFile -PathType Leaf)) {
    throw 'The runtime package or manifest is missing.'
}

$manifest = Get-Content -Raw -LiteralPath $manifestFile | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or
    $manifest.runtimeSourceCommit -notmatch '^[0-9a-f]{40}$' -or
    [string]::IsNullOrWhiteSpace($manifest.runtimeBuildId)) {
    throw 'The runtime manifest header is invalid.'
}

$forbiddenNames = @('llama-server.exe', 'llama-quantize.exe')
$listed = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
$roleCounts = @{}
foreach ($entry in @($manifest.files)) {
    if ([string]::IsNullOrWhiteSpace($entry.relativePath) -or
        [System.IO.Path]::IsPathRooted($entry.relativePath) -or
        $entry.relativePath.Contains('..')) {
        throw 'The runtime manifest contains an unsafe path.'
    }

    $fullPath = [System.IO.Path]::GetFullPath((Join-Path $root $entry.relativePath))
    $prefix = $root.TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $listed.Add($fullPath)) {
        throw 'The runtime manifest path is outside the package or duplicated.'
    }
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw 'A listed runtime package member is missing.'
    }
    if ((Get-Item -LiteralPath $fullPath).Length -ne [long]$entry.length -or
        (Get-Sha256 -Path $fullPath) -cne $entry.sha256) {
        throw 'A runtime package member does not match its manifest.'
    }
    if ([System.IO.Path]::GetExtension($fullPath) -ieq '.pdb' -or
        $forbiddenNames -icontains [System.IO.Path]::GetFileName($fullPath)) {
        throw 'The runtime package contains a forbidden development or server tool.'
    }

    $roleCounts[$entry.role] = 1 + [int]($roleCounts[$entry.role])
}

$actual = @(Get-ChildItem -LiteralPath $root -File -Recurse | ForEach-Object FullName)
if ($actual.Count -ne $listed.Count -or @($actual | Where-Object { -not $listed.Contains($_) }).Count -ne 0) {
    throw 'The runtime package contains an unlisted member.'
}
if ($roleCounts['Supervisor'] -ne 1 -or $roleCounts['Adapter'] -ne 1 -or $roleCounts['License'] -lt 1) {
    throw 'The runtime package roles are incomplete.'
}

Write-Host 'GGUF runtime manifest verified.'
