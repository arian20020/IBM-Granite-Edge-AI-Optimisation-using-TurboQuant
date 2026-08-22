[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$PackageRoot,
    [Parameter(Mandatory)] [string]$ManifestPath,
    [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{40}$')] [string]$RuntimeSourceCommit,
    [Parameter(Mandatory)] [string]$RuntimeBuildId,
    [Parameter(Mandatory)] [string[]]$BuildFlags
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
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw 'The GGUF runtime package root does not exist.'
}

$files = @(Get-ChildItem -LiteralPath $root -File -Recurse | Sort-Object FullName)
if ($files.Count -eq 0) {
    throw 'The GGUF runtime package is empty.'
}

function Get-PackageRelativePath {
    param([Parameter(Mandatory)] [string]$BasePath, [Parameter(Mandatory)] [string]$FullPath)

    $baseUri = [Uri](([System.IO.Path]::GetFullPath($BasePath).TrimEnd('\') + '\'))
    $fileUri = [Uri][System.IO.Path]::GetFullPath($FullPath)
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($fileUri).ToString())
}

$entries = foreach ($file in $files) {
    $relative = Get-PackageRelativePath -BasePath $root -FullPath $file.FullName
    $role = if ($relative -ceq 'Worker/GraniteEdgeAI.GgufRuntime.Worker.exe') {
        'Supervisor'
    } elseif ($relative -ceq 'Adapter/GraniteEdgeAI.GgufRuntime.NativeAdapter.exe') {
        'Adapter'
    } elseif ($file.Name -match '(LICENSE|COPYING|NOTICE)') {
        'License'
    } else {
        'Dependency'
    }

    [ordered]@{
        relativePath = $relative
        length = $file.Length
        sha256 = Get-Sha256 -Path $file.FullName
        architecture = if ($role -in @('Supervisor', 'Adapter')) { 'X64' } else { 'Any' }
        role = $role
        licenseReference = 'Adapter/LICENSE.llama.cpp.txt'
    }
}

if (@($entries | Where-Object role -eq 'Supervisor').Count -ne 1) {
    throw 'The package must contain exactly one protected supervisor.'
}
if (@($entries | Where-Object role -eq 'Adapter').Count -ne 1) {
    throw 'The package must contain exactly one Granite Edge stdio adapter.'
}
if (@($entries | Where-Object role -eq 'License').Count -lt 1) {
    throw 'The package must contain adapter runtime license material.'
}

$manifest = [ordered]@{
    schemaVersion = 1
    runtimeBuildId = $RuntimeBuildId
    runtimeSourceCommit = $RuntimeSourceCommit.ToLowerInvariant()
    buildFlags = @($BuildFlags)
    files = @($entries)
}
$manifestDirectory = Split-Path -Parent ([System.IO.Path]::GetFullPath($ManifestPath))
New-Item -ItemType Directory -Path $manifestDirectory -Force | Out-Null
$json = $manifest | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText(
    [System.IO.Path]::GetFullPath($ManifestPath),
    $json + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))
