[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$StageDirectory,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{64}$')]
    [string]$ExpectedManifestSha256
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-SafeDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    $item = Get-Item -LiteralPath $Path -Force
    if (-not $item.PSIsContainer) {
        throw "Quantizer stage is not a directory: $Path"
    }
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Quantizer stage must not be a reparse point: $Path"
    }
    return $item.FullName.TrimEnd([IO.Path]::DirectorySeparatorChar)
}

function Resolve-ContainedFile {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    if ([IO.Path]::IsPathRooted($RelativePath) -or $RelativePath.Contains('..') -or $RelativePath.Contains(':')) {
        throw "Unsafe manifest path: $RelativePath"
    }

    $candidate = [IO.Path]::GetFullPath((Join-Path $Root $RelativePath))
    $prefix = $Root + [IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Manifest path escaped the stage: $RelativePath"
    }

    $parent = [IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName($candidate))
    while ($null -ne $parent) {
        $directory = Get-Item -LiteralPath $parent.FullName -Force
        if (-not $directory.PSIsContainer -or
            (($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
            throw 'quantizer_package_directory_redirected'
        }
        if ($directory.FullName.TrimEnd([IO.Path]::DirectorySeparatorChar) -ceq $Root) {
            break
        }
        $parent = $parent.Parent
    }
    if ($null -eq $parent) {
        throw "Manifest path escaped the stage: $RelativePath"
    }

    $item = Get-Item -LiteralPath $candidate -Force
    if ($item.PSIsContainer -or (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
        throw "Manifest member is not a regular file: $RelativePath"
    }
    return $item.FullName
}

function Get-SafePackageFiles {
    param([Parameter(Mandatory = $true)][string]$Root)

    $pending = [Collections.Generic.Stack[string]]::new()
    $files = [Collections.Generic.List[string]]::new()
    $pending.Push($Root)
    $directoryCount = 0

    while ($pending.Count -ne 0) {
        $current = $pending.Pop()
        foreach ($entry in @(Get-ChildItem -LiteralPath $current -Force)) {
            if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'quantizer_package_directory_redirected'
            }
            if ($entry.PSIsContainer) {
                $directoryCount++
                if ($directoryCount -gt 256) {
                    throw 'The quantizer package directory count exceeds the limit.'
                }
                $pending.Push($entry.FullName)
                continue
            }

            $files.Add($entry.FullName)
            if ($files.Count -gt 257) {
                throw 'The quantizer package file count exceeds the limit.'
            }
        }
    }

    return $files.ToArray()
}

$root = Resolve-SafeDirectory $StageDirectory
$manifestPath = Join-Path $root 'llama-quantize.package.manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'The quantizer package manifest is missing.'
}

$actualManifestSha = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualManifestSha -cne $ExpectedManifestSha256.ToLowerInvariant()) {
    throw "Quantizer manifest SHA-256 mismatch. Expected $ExpectedManifestSha256; got $actualManifestSha."
}

$manifestBytes = [IO.File]::ReadAllBytes($manifestPath)
if ($manifestBytes.Length -gt 262144) {
    throw 'The quantizer manifest exceeds the 256 KiB limit.'
}
$manifest = [Text.Encoding]::UTF8.GetString($manifestBytes) | ConvertFrom-Json

if ($manifest.schemaVersion -ne 1 -or
    $manifest.packageId -cne 'granite-edge-ai-llama-quantize-x64' -or
    $manifest.source.url -cne 'https://github.com/ggml-org/llama.cpp.git' -or
    $manifest.source.commit -cne '3f7c29d318e317b63f54c558bc69803963d7d88c' -or
    $manifest.architecture -cne 'x64' -or
    $manifest.configuration -cne 'Release' -or
    $manifest.executableRelativePath -cne 'bin/llama-quantize.exe') {
    throw 'The quantizer package identity is not the approved locked identity.'
}

$expectedTokens = @('Q2_K', 'Q3_K_M', 'Q4_K_M', 'Q5_K_M', 'Q6_K', 'Q8_0')
if (@($manifest.allowedTokens).Count -ne $expectedTokens.Count -or
    (Compare-Object -ReferenceObject $expectedTokens -DifferenceObject @($manifest.allowedTokens))) {
    throw 'The quantizer token allowlist is not exact.'
}
if ([long]$manifest.maximumSourceBytes -le 0 -or [long]$manifest.maximumOutputBytes -le 0 -or [int]$manifest.timeoutSeconds -le 0) {
    throw 'The quantizer resource bounds are invalid.'
}

$listed = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in @($manifest.files)) {
    $relative = [string]$file.relativePath
    if (-not $listed.Add($relative)) {
        throw "Duplicate manifest member: $relative"
    }
    if ([string]$file.sha256 -cnotmatch '^[0-9a-f]{64}$') {
        throw "Manifest member has a non-canonical SHA-256: $relative"
    }
    $path = Resolve-ContainedFile -Root $root -RelativePath $relative
    $actualLength = (Get-Item -LiteralPath $path).Length
    if ($actualLength -ne [long]$file.length) {
        throw "Manifest member length mismatch: $relative"
    }
    $actualSha = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualSha -cne [string]$file.sha256) {
        throw "Manifest member SHA-256 mismatch: $relative"
    }
}

if (-not $listed.Contains([string]$manifest.executableRelativePath) -or
    -not $listed.Contains('licenses/LICENSE.llama.cpp.txt')) {
    throw 'The quantizer executable or required license is missing from the manifest.'
}

$rootPrefix = $root + [IO.Path]::DirectorySeparatorChar
$actualFiles = Get-SafePackageFiles -Root $root | ForEach-Object {
    $item = Get-Item -LiteralPath $_ -Force
    if ($item.PSIsContainer) {
        throw 'The quantizer package contains an unsupported entry.'
    }
    if (-not $_.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Package member escaped the stage: $_"
    }
    $_.Substring($rootPrefix.Length).Replace('\', '/')
} | Where-Object { $_ -cne 'llama-quantize.package.manifest.json' }
$extras = @($actualFiles | Where-Object { -not $listed.Contains($_) })
if ($extras.Count -ne 0) {
    throw "Unlisted files exist in the quantizer stage: $($extras -join ', ')"
}
if (@($actualFiles).Count -ne $listed.Count) {
    throw 'One or more manifest members are missing from the quantizer stage.'
}

Write-Output "Verified GGUF quantizer package $($manifest.packageId) at $actualManifestSha"
