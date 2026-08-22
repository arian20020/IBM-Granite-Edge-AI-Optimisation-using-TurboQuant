[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$WorkerDirectory,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ManifestPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $stream = [System.IO.File]::Open(
        $Path,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::Read)
    try {
        $algorithm = [System.Security.Cryptography.SHA256]::Create()
        try {
            return ([System.BitConverter]::ToString(
                $algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
        }
        finally {
            $algorithm.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Test-IsWithinDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CandidatePath,

        [Parameter(Mandatory = $true)]
        [string]$DirectoryPath
    )

    $directoryPrefix = $DirectoryPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    return $CandidatePath.StartsWith(
        $directoryPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-ControlledRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FullPath,

        [Parameter(Mandatory = $true)]
        [string]$DirectoryPath
    )

    $directoryPrefix = $DirectoryPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    $canonicalPath = [System.IO.Path]::GetFullPath($FullPath)
    if (-not $canonicalPath.StartsWith(
            $directoryPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'A worker file escaped the controlled publish directory.'
    }

    return $canonicalPath.Substring($directoryPrefix.Length).Replace(
        [System.IO.Path]::DirectorySeparatorChar,
        '/')
}

function Assert-ExactProperties {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Value,

        [Parameter(Mandatory = $true)]
        [string[]]$ExpectedNames,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $actualNames = @($Value.PSObject.Properties | ForEach-Object Name)
    if ($actualNames.Count -ne $ExpectedNames.Count) {
        throw "$Description has an unexpected property count."
    }

    foreach ($expectedName in $ExpectedNames) {
        if (-not ($actualNames -ccontains $expectedName)) {
            throw "$Description is missing an exact required property."
        }
    }
}

function Assert-SafeRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        $RelativePath -cne $RelativePath.Trim() -or
        $RelativePath.Contains('\') -or
        $RelativePath.StartsWith('/') -or
        [System.IO.Path]::IsPathRooted($RelativePath)) {
        throw 'The worker manifest contains a non-canonical relative path.'
    }

    foreach ($character in $RelativePath.ToCharArray()) {
        if ([char]::IsControl($character)) {
            throw 'The worker manifest path contains a control character.'
        }
    }

    $segments = $RelativePath.Split('/')
    foreach ($segment in $segments) {
        if ([string]::IsNullOrEmpty($segment) -or
            $segment -ceq '.' -or
            $segment -ceq '..' -or
            $segment -cne $segment.Trim() -or
            $segment.EndsWith('.') -or
            $segment.IndexOfAny(
                [System.IO.Path]::GetInvalidFileNameChars()) -ge 0) {
            throw 'The worker manifest contains an unsafe path segment.'
        }
    }

    if (($segments -join '/') -cne $RelativePath) {
        throw 'The worker manifest path is not normalized.'
    }
}

$workerRoot = [System.IO.Path]::GetFullPath($WorkerDirectory)
if (-not [System.IO.Directory]::Exists($workerRoot)) {
    throw 'The worker publish directory does not exist.'
}

$workerRootInfo = [System.IO.DirectoryInfo]::new($workerRoot)
if (($workerRootInfo.Attributes -band
        [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'The worker publish directory cannot be a reparse point.'
}

$manifestFullPath = [System.IO.Path]::GetFullPath($ManifestPath)
if (-not [System.IO.File]::Exists($manifestFullPath)) {
    throw 'The detached worker manifest does not exist.'
}

if ($manifestFullPath.Equals(
        $workerRoot,
        [System.StringComparison]::OrdinalIgnoreCase) -or
    (Test-IsWithinDirectory -CandidatePath $manifestFullPath `
        -DirectoryPath $workerRoot)) {
    throw 'The worker manifest must be stored outside the Worker directory.'
}

$manifestInfo = [System.IO.FileInfo]::new($manifestFullPath)
if (($manifestInfo.Attributes -band
        [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'The worker manifest cannot be a reparse point.'
}

if ($manifestInfo.Length -le 0 -or $manifestInfo.Length -gt 1048576) {
    throw 'The worker manifest size is outside the allowed bounds.'
}

try {
    $manifest = Get-Content -LiteralPath $manifestFullPath -Raw |
        ConvertFrom-Json
}
catch {
    throw 'The worker manifest is not valid JSON.'
}

if ($null -eq $manifest -or $manifest -is [System.Array] -or
    $manifest -isnot [System.Management.Automation.PSCustomObject]) {
    throw 'The worker manifest root must be an object.'
}

Assert-ExactProperties -Value $manifest `
    -ExpectedNames @('schemaVersion', 'runtimeIdentifier', 'files') `
    -Description 'The worker manifest root'
if (($manifest.schemaVersion -isnot [int] -and
        $manifest.schemaVersion -isnot [long]) -or
    [long]$manifest.schemaVersion -ne 1) {
    throw 'The worker manifest schema version is unsupported.'
}

if ($manifest.runtimeIdentifier -isnot [string] -or
    $manifest.runtimeIdentifier -cne 'win-x64') {
    throw 'The worker manifest runtime identifier is unsupported.'
}

if ($manifest.files -isnot [System.Array] -or
    $manifest.files.Count -le 0 -or
    $manifest.files.Count -gt 1024) {
    throw 'The worker manifest must contain a bounded file array.'
}

$manifestPaths = [System.Collections.Generic.List[string]]::new()
$entriesByPath =
    [System.Collections.Generic.Dictionary[string, object]]::new(
        [System.StringComparer]::Ordinal)
$previousPath = $null
foreach ($entry in $manifest.files) {
    if ($null -eq $entry -or $entry -is [System.Array] -or
        $entry -isnot [System.Management.Automation.PSCustomObject]) {
        throw 'Each worker manifest file entry must be an object.'
    }

    Assert-ExactProperties -Value $entry `
        -ExpectedNames @('path', 'length', 'sha256') `
        -Description 'A worker manifest file entry'
    if ($entry.path -isnot [string]) {
        throw 'A worker manifest path must be a string.'
    }

    Assert-SafeRelativePath -RelativePath $entry.path
    if ($null -ne $previousPath -and
        [System.StringComparer]::Ordinal.Compare(
            $previousPath,
            $entry.path) -ge 0) {
        throw 'Worker manifest paths must be unique and ordinal-sorted.'
    }

    if (($entry.length -isnot [int] -and
            $entry.length -isnot [long]) -or
        [long]$entry.length -lt 0) {
        throw 'A worker manifest length must be a non-negative integer.'
    }

    if ($entry.sha256 -isnot [string] -or
        $entry.sha256 -cnotmatch '^[0-9a-f]{64}$') {
        throw 'A worker manifest hash must be lowercase SHA-256.'
    }

    $manifestPaths.Add($entry.path)
    $entriesByPath.Add($entry.path, $entry)
    $previousPath = $entry.path
}

$allItems = @(Get-ChildItem -LiteralPath $workerRoot -Force -Recurse)
$reparseItem = $allItems | Where-Object {
    ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
} | Select-Object -First 1
if ($null -ne $reparseItem) {
    throw 'The worker publish closure cannot contain reparse points.'
}

$publishedFiles = @($allItems | Where-Object { -not $_.PSIsContainer })
$actualPaths = [System.Collections.Generic.List[string]]::new()
$fullPathsByRelativeName =
    [System.Collections.Generic.Dictionary[string, string]]::new(
        [System.StringComparer]::Ordinal)
foreach ($file in $publishedFiles) {
    $relativePath = Get-ControlledRelativePath `
        -FullPath $file.FullName `
        -DirectoryPath $workerRoot
    Assert-SafeRelativePath -RelativePath $relativePath
    if ($fullPathsByRelativeName.ContainsKey($relativePath)) {
        throw 'The worker publish closure contains duplicate relative paths.'
    }

    $actualPaths.Add($relativePath)
    $fullPathsByRelativeName.Add($relativePath, $file.FullName)
}

$sortedActualPaths = [string[]]$actualPaths.ToArray()
[System.Array]::Sort(
    $sortedActualPaths,
    [System.StringComparer]::Ordinal)
if ($sortedActualPaths.Count -ne $manifestPaths.Count) {
    throw 'The worker publish closure does not match the manifest file count.'
}

for ($index = 0; $index -lt $sortedActualPaths.Count; $index++) {
    if ($sortedActualPaths[$index] -cne $manifestPaths[$index]) {
        throw 'The worker publish closure has a missing or extra file.'
    }
}

foreach ($relativePath in $manifestPaths) {
    $entry = $entriesByPath[$relativePath]
    $fullPath = $fullPathsByRelativeName[$relativePath]
    $before = [System.IO.FileInfo]::new($fullPath)
    if ($before.Length -ne [long]$entry.length) {
        throw 'A worker file length does not match the detached manifest.'
    }

    $actualHash = Get-Sha256 -Path $fullPath
    $after = [System.IO.FileInfo]::new($fullPath)
    if ($after.Length -ne $before.Length -or
        $actualHash -cne $entry.sha256) {
        throw 'A worker file hash does not match the detached manifest.'
    }
}
