[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$PackageRoot,
    [Parameter(Mandatory)] [string]$ManifestPath
)

$ErrorActionPreference = 'Stop'

function Assert-NoReparseDirectory {
    param([Parameter(Mandatory)] [string]$Path)

    if ((Test-Path -LiteralPath $Path -PathType Container) -and
        ((Get-Item -LiteralPath $Path -Force).Attributes -band
            [System.IO.FileAttributes]::ReparsePoint)) {
        throw 'The runtime package contains a reparse-point directory.'
    }
}

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
Assert-NoReparseDirectory -Path $root

$manifest = Get-Content -Raw -LiteralPath $manifestFile | ConvertFrom-Json
if ($manifest.schemaVersion -notin @(1, 2) -or
    $manifest.runtimeSourceCommit -notmatch '^[0-9a-f]{40}$' -or
    [string]::IsNullOrWhiteSpace($manifest.runtimeBuildId)) {
    throw 'The runtime manifest header is invalid.'
}

$listed = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
$entriesByRelative = @{}
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
    $parent = Split-Path -Parent $fullPath
    while ($null -ne $parent) {
        Assert-NoReparseDirectory -Path $parent
        if ($parent -ceq $root) { break }
        $parent = Split-Path -Parent $parent
    }
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw 'A listed runtime package member is missing.'
    }
    if ((Get-Item -LiteralPath $fullPath).Length -ne [long]$entry.length -or
        (Get-Sha256 -Path $fullPath) -cne $entry.sha256) {
        throw 'A runtime package member does not match its manifest.'
    }
    if ([System.IO.Path]::GetExtension($fullPath) -ieq '.pdb') {
        throw 'The runtime package contains a forbidden development artifact.'
    }
    $fileName = [System.IO.Path]::GetFileName($fullPath)
    if ($fileName -ieq 'llama-server.exe' -and
        $entry.role -notin @('NativeRuntimeCpu', 'NativeRuntimeVulkan')) {
        throw 'The runtime package contains an unclassified native runtime.'
    }
    if ($fileName -ieq 'llama-quantize.exe' -and $entry.role -ne 'Quantizer') {
        throw 'The runtime package contains an unclassified native quantizer.'
    }

    $roleCounts[$entry.role] = 1 + [int]($roleCounts[$entry.role])
    $entriesByRelative[$entry.relativePath] = $entry
}

$actual = @(Get-ChildItem -LiteralPath $root -File -Recurse | ForEach-Object FullName)
if ($actual.Count -ne $listed.Count -or @($actual | Where-Object { -not $listed.Contains($_) }).Count -ne 0) {
    throw 'The runtime package contains an unlisted member.'
}
if ($roleCounts['Supervisor'] -ne 1 -or $roleCounts['Adapter'] -ne 1 -or $roleCounts['License'] -lt 1) {
    throw 'The runtime package roles are incomplete.'
}

if ($manifest.schemaVersion -eq 2 -and
    ($roleCounts['NativeRuntimeCpu'] -ne 1 -or
     $roleCounts['NativeRuntimeVulkan'] -ne 1 -or
     $roleCounts['Quantizer'] -ne 1)) {
    throw 'The AtomicBot runtime package roles are incomplete.'
}
if ($manifest.schemaVersion -eq 2) {
    $rolePaths = @{
        'Native/Cpu/llama-server.exe' = 'NativeRuntimeCpu'
        'Native/Vulkan/llama-server.exe' = 'NativeRuntimeVulkan'
        'Native/Cpu/llama-quantize.exe' = 'Quantizer'
    }
    foreach ($path in $rolePaths.Keys) {
        if (-not $entriesByRelative.ContainsKey($path) -or
            $entriesByRelative[$path].role -cne $rolePaths[$path]) {
            throw 'The AtomicBot runtime package role path is invalid.'
        }
    }
    foreach ($entry in @($manifest.files | Where-Object {
        $_.role -in @('NativeRuntimeCpu', 'NativeRuntimeVulkan', 'Quantizer')
    })) {
        if (-not $rolePaths.ContainsKey($entry.relativePath) -or
            $rolePaths[$entry.relativePath] -cne $entry.role) {
            throw 'The AtomicBot runtime package role path is invalid.'
        }
    }
    $cpuClosure = @(
        'Native/Cpu/LICENSE', 'Native/Cpu/ggml-base.dll', 'Native/Cpu/ggml.dll',
        'Native/Cpu/llama-common.dll', 'Native/Cpu/llama.dll',
        'Native/Cpu/llama-server-impl.dll', 'Native/Cpu/llama-quantize-impl.dll',
        'Native/Cpu/mtmd.dll', 'Native/Cpu/ggml-cpu-alderlake.dll',
        'Native/Cpu/ggml-cpu-cannonlake.dll', 'Native/Cpu/ggml-cpu-cascadelake.dll',
        'Native/Cpu/ggml-cpu-haswell.dll', 'Native/Cpu/ggml-cpu-icelake.dll',
        'Native/Cpu/ggml-cpu-sandybridge.dll', 'Native/Cpu/ggml-cpu-skylakex.dll',
        'Native/Cpu/ggml-cpu-sse42.dll', 'Native/Cpu/ggml-cpu-x64.dll'
    )
    $vulkanClosure = @(
        'Native/Vulkan/LICENSE', 'Native/Vulkan/ggml-base.dll', 'Native/Vulkan/ggml.dll',
        'Native/Vulkan/ggml-vulkan.dll', 'Native/Vulkan/llama-common.dll',
        'Native/Vulkan/llama.dll', 'Native/Vulkan/llama-server-impl.dll',
        'Native/Vulkan/mtmd.dll', 'Native/Vulkan/ggml-cpu-alderlake.dll',
        'Native/Vulkan/ggml-cpu-cannonlake.dll', 'Native/Vulkan/ggml-cpu-cascadelake.dll',
        'Native/Vulkan/ggml-cpu-haswell.dll', 'Native/Vulkan/ggml-cpu-icelake.dll',
        'Native/Vulkan/ggml-cpu-sandybridge.dll', 'Native/Vulkan/ggml-cpu-skylakex.dll',
        'Native/Vulkan/ggml-cpu-sse42.dll', 'Native/Vulkan/ggml-cpu-x64.dll'
    )
    foreach ($path in @($cpuClosure + $vulkanClosure)) {
        if (-not $entriesByRelative.ContainsKey($path)) {
            throw 'The AtomicBot runtime package dependency closure is incomplete.'
        }
    }
}
if ($manifest.schemaVersion -eq 1 -and
    ($roleCounts['NativeRuntimeCpu'] -or
     $roleCounts['NativeRuntimeVulkan'] -or
     $roleCounts['Quantizer'])) {
    throw 'A schema-one package cannot declare AtomicBot native roles.'
}

Write-Host 'GGUF runtime manifest verified.'
