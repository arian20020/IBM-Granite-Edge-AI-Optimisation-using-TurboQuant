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

    $relativePath = $canonicalPath.Substring($directoryPrefix.Length)
    if ([string]::IsNullOrWhiteSpace($relativePath)) {
        throw 'A worker file has an empty relative path.'
    }

    return $relativePath.Replace(
        [System.IO.Path]::DirectorySeparatorChar,
        '/')
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
if ($manifestFullPath.Equals(
        $workerRoot,
        [System.StringComparison]::OrdinalIgnoreCase) -or
    (Test-IsWithinDirectory -CandidatePath $manifestFullPath `
        -DirectoryPath $workerRoot)) {
    throw 'The worker manifest must be stored outside the Worker directory.'
}

$manifestDirectory = [System.IO.Path]::GetDirectoryName($manifestFullPath)
if ([string]::IsNullOrWhiteSpace($manifestDirectory) -or
    -not [System.IO.Directory]::Exists($manifestDirectory)) {
    throw 'The worker manifest parent directory does not exist.'
}

$allItems = @(Get-ChildItem -LiteralPath $workerRoot -Force -Recurse)
$reparseItem = $allItems | Where-Object {
    ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
} | Select-Object -First 1
if ($null -ne $reparseItem) {
    throw 'The worker publish closure cannot contain reparse points.'
}

$publishedFiles = @($allItems | Where-Object { -not $_.PSIsContainer })
if ($publishedFiles.Count -eq 0) {
    throw 'The worker publish closure cannot be empty.'
}

$pathsByRelativeName =
    [System.Collections.Generic.Dictionary[string, string]]::new(
        [System.StringComparer]::Ordinal)
foreach ($file in $publishedFiles) {
    $relativePath = Get-ControlledRelativePath `
        -FullPath $file.FullName `
        -DirectoryPath $workerRoot
    if ($pathsByRelativeName.ContainsKey($relativePath)) {
        throw 'The worker publish closure contains duplicate relative paths.'
    }

    $pathsByRelativeName.Add($relativePath, $file.FullName)
}

$sortedRelativePaths = [string[]]$pathsByRelativeName.Keys
[System.Array]::Sort(
    $sortedRelativePaths,
    [System.StringComparer]::Ordinal)

$entries = [System.Collections.Generic.List[object]]::new()
foreach ($relativePath in $sortedRelativePaths) {
    $fullPath = $pathsByRelativeName[$relativePath]
    $fileInfo = [System.IO.FileInfo]::new($fullPath)
    $entries.Add([ordered]@{
        path = $relativePath
        length = [long]$fileInfo.Length
        sha256 = (Get-FileHash `
            -LiteralPath $fullPath `
            -Algorithm SHA256).Hash.ToLowerInvariant()
    })
}

$manifest = [ordered]@{
    schemaVersion = 1
    runtimeIdentifier = 'win-x64'
    files = [object[]]$entries.ToArray()
}
$json = $manifest | ConvertTo-Json -Depth 4
$temporaryPath = $manifestFullPath + '.' +
    [System.Guid]::NewGuid().ToString('N') + '.tmp'
try {
    $utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText(
        $temporaryPath,
        $json + "`n",
        $utf8WithoutBom)
    Move-Item -LiteralPath $temporaryPath `
        -Destination $manifestFullPath -Force
}
finally {
    if ([System.IO.File]::Exists($temporaryPath)) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
}
