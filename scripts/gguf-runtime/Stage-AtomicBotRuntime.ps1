[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$BundledPackageRoot,
    [Parameter(Mandatory)] [string]$StageRoot
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-Sha256 {
    param([Parameter(Mandatory)] [string]$Path)

    $stream = [System.IO.File]::OpenRead($Path)
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

function Assert-ZipEntriesSafe {
    param([Parameter(Mandatory)] [string]$ArchivePath)

    $archive = [System.IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $seen = [System.Collections.Generic.HashSet[string]]::new(
            [System.StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $archive.Entries) {
            $relative = $entry.FullName.Replace('/', '\')
            $segments = @($relative.Split('\', [System.StringSplitOptions]::RemoveEmptyEntries))
            $canonical = [string]::Join('\', $segments)
            if ([string]::IsNullOrWhiteSpace($relative) -or
                [System.IO.Path]::IsPathRooted($relative) -or
                $relative.StartsWith('\\') -or
                $segments -contains '..' -or
                $segments -contains '.' -or
                [string]::IsNullOrWhiteSpace($canonical) -or
                -not $seen.Add($canonical)) {
                throw 'atomicbot_archive_member_path_invalid'
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

$packageRoot = [System.IO.Path]::GetFullPath($BundledPackageRoot)
$destination = [System.IO.Path]::GetFullPath($StageRoot)
$manifestPath = Join-Path $packageRoot 'package-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'atomicbot_package_manifest_missing'
}

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or
    $manifest.sourceCommit -cne '519f0c594a8e31467d2e2f2cf17054c9e7e11536') {
    throw 'atomicbot_package_identity_invalid'
}

$archives = @($manifest.archives)
if ($archives.Count -ne 2 -or
    @($archives | Where-Object role -ceq 'cpu').Count -ne 1 -or
    @($archives | Where-Object role -ceq 'vulkan').Count -ne 1) {
    throw 'atomicbot_package_roles_invalid'
}

$cpuDispatch = @(
    'ggml-cpu-alderlake.dll', 'ggml-cpu-cannonlake.dll',
    'ggml-cpu-cascadelake.dll', 'ggml-cpu-haswell.dll',
    'ggml-cpu-icelake.dll', 'ggml-cpu-sandybridge.dll',
    'ggml-cpu-skylakex.dll', 'ggml-cpu-sse42.dll', 'ggml-cpu-x64.dll'
)
$commonNames = @(
    'ggml-base.dll', 'ggml.dll', 'LICENSE', 'llama-common.dll',
    'llama.dll', 'llama-server.exe', 'llama-server-impl.dll', 'mtmd.dll'
)
$roleClosures = @{
    cpu = @($commonNames + @('llama-quantize.exe', 'llama-quantize-impl.dll') + $cpuDispatch)
    vulkan = @($commonNames + @('ggml-vulkan.dll') + $cpuDispatch)
}

foreach ($entry in $archives) {
    if ([System.IO.Path]::IsPathRooted($entry.file) -or $entry.file.Contains('..')) {
        throw 'atomicbot_archive_path_invalid'
    }

    $archivePath = [System.IO.Path]::GetFullPath((Join-Path $packageRoot $entry.file))
    $packagePrefix = $packageRoot.TrimEnd('\') + '\'
    if (-not $archivePath.StartsWith(
            $packagePrefix,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
        throw 'atomicbot_archive_missing'
    }

    $file = Get-Item -LiteralPath $archivePath
    if ($file.Length -ne [long]$entry.length -or
        (Get-Sha256 -Path $archivePath) -cne [string]$entry.sha256) {
        throw 'atomicbot_archive_integrity_failed'
    }
}

if (Test-Path -LiteralPath $destination) {
    Remove-Item -LiteralPath $destination -Recurse -Force
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null

foreach ($entry in $archives) {
    $archivePath = [System.IO.Path]::GetFullPath((Join-Path $packageRoot $entry.file))
    $roleDirectory = if ($entry.role -ceq 'cpu') { 'Cpu' } else { 'Vulkan' }
    $roleRoot = Join-Path $destination $roleDirectory
    $extractRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
        'granite-atomicbot-extract-' + [Guid]::NewGuid().ToString('N'))
    try {
        Assert-ZipEntriesSafe -ArchivePath $archivePath
        Expand-Archive -LiteralPath $archivePath -DestinationPath $extractRoot -Force
        $binRoot = Join-Path $extractRoot 'build\bin'
        if (-not (Test-Path -LiteralPath $binRoot -PathType Container)) {
            throw 'atomicbot_archive_layout_invalid'
        }

        New-Item -ItemType Directory -Path $roleRoot -Force | Out-Null
        foreach ($name in $roleClosures[$entry.role]) {
            $source = Join-Path $binRoot $name
            if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
                throw 'atomicbot_archive_layout_invalid'
            }
            Copy-Item -LiteralPath $source -Destination $roleRoot
        }
    }
    finally {
        if (Test-Path -LiteralPath $extractRoot) {
            Remove-Item -LiteralPath $extractRoot -Recurse -Force
        }
    }
}

$required = @(
    $roleClosures.cpu | ForEach-Object { "Cpu\$_" }
    $roleClosures.vulkan | ForEach-Object { "Vulkan\$_" }
)
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $destination $relative) -PathType Leaf)) {
        throw 'atomicbot_staged_role_missing'
    }
}

Write-Host 'atomicbot_runtime_staged'
