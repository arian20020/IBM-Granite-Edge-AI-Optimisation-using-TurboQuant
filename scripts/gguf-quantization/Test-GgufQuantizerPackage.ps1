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

function Get-ExactFileSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = [IO.FileStream]::new(
        $Path,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        [IO.FileShare]::Read)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString(
            $algorithm.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
        $stream.Dispose()
    }
}

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

function Test-ExactSequence {
    param(
        [AllowNull()][object[]]$Actual,
        [Parameter(Mandatory = $true)][string[]]$Expected
    )

    $values = @($Actual)
    if ($values.Count -ne $Expected.Count) { return $false }
    for ($index = 0; $index -lt $Expected.Count; $index++) {
        if ([string]$values[$index] -cne $Expected[$index]) { return $false }
    }
    return $true
}

$root = Resolve-SafeDirectory $StageDirectory
$manifestPath = Join-Path $root 'llama-quantize.package.manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'The quantizer package manifest is missing.'
}

$actualManifestSha = Get-ExactFileSha256 $manifestPath
if ($actualManifestSha -cne $ExpectedManifestSha256.ToLowerInvariant()) {
    throw "Quantizer manifest SHA-256 mismatch. Expected $ExpectedManifestSha256; got $actualManifestSha."
}

$manifestBytes = [IO.File]::ReadAllBytes($manifestPath)
if ($manifestBytes.Length -gt 262144) {
    throw 'The quantizer manifest exceeds the 256 KiB limit.'
}
$manifest = [Text.Encoding]::UTF8.GetString($manifestBytes) | ConvertFrom-Json

$isLegacy = $manifest.packageId -ceq 'granite-edge-ai-llama-quantize-x64' -and
    $manifest.source.url -ceq 'https://github.com/ggml-org/llama.cpp.git' -and
    $manifest.source.commit -ceq '3f7c29d318e317b63f54c558bc69803963d7d88c' -and
    $manifest.libraryLinkage -ceq 'static'

$atomicAppLocalDependencies = @(
    'ggml-base.dll',
    'ggml-cpu-alderlake.dll',
    'ggml-cpu-cannonlake.dll',
    'ggml-cpu-cascadelake.dll',
    'ggml-cpu-haswell.dll',
    'ggml-cpu-icelake.dll',
    'ggml-cpu-sandybridge.dll',
    'ggml-cpu-skylakex.dll',
    'ggml-cpu-sse42.dll',
    'ggml-cpu-x64.dll',
    'ggml.dll',
    'llama-common.dll',
    'llama-quantize-impl.dll',
    'llama.dll'
)
$atomicOsProvidedDependencies = @(
    'KERNEL32.dll',
    'VCRUNTIME140.dll',
    'api-ms-win-crt-heap-l1-1-0.dll',
    'api-ms-win-crt-locale-l1-1-0.dll',
    'api-ms-win-crt-math-l1-1-0.dll',
    'api-ms-win-crt-runtime-l1-1-0.dll',
    'api-ms-win-crt-stdio-l1-1-0.dll'
)
$isAtomicBot = $manifest.packageId -ceq 'granite-edge-ai-atomicbot-llama-quantize-x64' -and
    $manifest.source.url -ceq 'https://github.com/AtomicBot-ai/atomic-llama-cpp-turboquant' -and
    $manifest.source.commit -ceq '519f0c594a8e31467d2e2f2cf17054c9e7e11536' -and
    $manifest.libraryLinkage -ceq 'dynamic' -and
    @($manifest.cmakeFlags).Count -eq 0 -and
    $manifest.toolchain.visualStudio -ceq 'not-attested-by-upstream-release' -and
    $manifest.toolchain.msvc -ceq 'runtime-banner-msvc-19.51.36248.0_pe-linker-14.44' -and
    $manifest.toolchain.cmake -ceq 'not-attested-by-upstream-release' -and
    (Test-ExactSequence -Actual @($manifest.osProvidedDependencies) -Expected $atomicOsProvidedDependencies) -and
    (Test-ExactSequence -Actual @($manifest.appLocalDependencies) -Expected $atomicAppLocalDependencies) -and
    [long]$manifest.maximumSourceBytes -eq 68719476736 -and
    [long]$manifest.maximumOutputBytes -eq 68719476736 -and
    [int]$manifest.timeoutSeconds -eq 21600 -and
    [int]$manifest.standardOutputMaximumBytes -eq 1048576 -and
    [int]$manifest.standardErrorMaximumBytes -eq 1048576 -and
    $manifest.license.identity -ceq 'MIT' -and
    $manifest.license.relativePath -ceq 'licenses/LICENSE.atomicbot-llama.cpp.txt'

if ($manifest.schemaVersion -ne 1 -or
    (-not $isLegacy -and -not $isAtomicBot) -or
    $manifest.target -cne 'llama-quantize' -or
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
if ([long]$manifest.maximumSourceBytes -le 0 -or
    [long]$manifest.maximumOutputBytes -le 0 -or
    [int]$manifest.timeoutSeconds -le 0 -or
    [int]$manifest.standardOutputMaximumBytes -le 0 -or
    [int]$manifest.standardErrorMaximumBytes -le 0) {
    throw 'The quantizer resource bounds are invalid.'
}

$atomicFiles = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
$atomicFiles.Add('bin/ggml-base.dll', [pscustomobject]@{ length = 684544L; sha256 = '06facaebf58c5178735b7747a1b38def0ade58d8b604b9d1a9ae036384026217' })
$atomicFiles.Add('bin/ggml-cpu-alderlake.dll', [pscustomobject]@{ length = 894464L; sha256 = '6c24ef9a6910698fc111644b934e741d3c0d1c37386ed6da6ca3279c2bc2e5e9' })
$atomicFiles.Add('bin/ggml-cpu-cannonlake.dll', [pscustomobject]@{ length = 998400L; sha256 = 'c33bd3e48e1681e60ad7a124e177b3d8afb9a4d726e4e766820e500125a5f0a1' })
$atomicFiles.Add('bin/ggml-cpu-cascadelake.dll', [pscustomobject]@{ length = 997376L; sha256 = '2f4d274df698bee5f963111a17344d6e1f511d1cc4e136bf556113887f538596' })
$atomicFiles.Add('bin/ggml-cpu-haswell.dll', [pscustomobject]@{ length = 896000L; sha256 = '032b0ee79134e08a8cb8eabd5f24389212f6507ddd787b34bc1465c8c5471d91' })
$atomicFiles.Add('bin/ggml-cpu-icelake.dll', [pscustomobject]@{ length = 997376L; sha256 = '380b22746796e26da54de6a7f3d516eb8b223fb1305ac38199e85a9966d4c541' })
$atomicFiles.Add('bin/ggml-cpu-sandybridge.dll', [pscustomobject]@{ length = 839168L; sha256 = 'fa794aa2a87c3558cd016b3e2ffc029aa3f12a55f916115886b38d3757f79a42' })
$atomicFiles.Add('bin/ggml-cpu-skylakex.dll', [pscustomobject]@{ length = 998400L; sha256 = '9953a96e8a8390636cc96a247f081a4a813843fea8405eb8bf466ce7215cc8fc' })
$atomicFiles.Add('bin/ggml-cpu-sse42.dll', [pscustomobject]@{ length = 758784L; sha256 = '04d9cb2f00c2be9cfa728e2c1d7cd78c99ffcccb83fccb7b4d7ff10844cfdd03' })
$atomicFiles.Add('bin/ggml-cpu-x64.dll', [pscustomobject]@{ length = 760832L; sha256 = 'c549f4ebd163c8bedb6a081b27472e20c494093321c064c352a3839c6700c09f' })
$atomicFiles.Add('bin/ggml.dll', [pscustomobject]@{ length = 66560L; sha256 = '5e10680be91cd5a2748d90fb56c56c29fbab1b8f5587beaeda23ae53c5a4f07d' })
$atomicFiles.Add('bin/llama-common.dll', [pscustomobject]@{ length = 9086976L; sha256 = 'b715e90d25ea63a554245eb485d6f869f59cd6ed0b6df236e3da52ab3dbeb6f7' })
$atomicFiles.Add('bin/llama-quantize-impl.dll', [pscustomobject]@{ length = 331264L; sha256 = 'a51b4b9371043e4fd8c88b320b38b9127a562dc4ebff025e75483ee3c789a6f2' })
$atomicFiles.Add('bin/llama-quantize.exe', [pscustomobject]@{ length = 10752L; sha256 = '0a17247d4807b520532df54f96ed73f3cf6fa921f879d8d465b44541b41d36e3' })
$atomicFiles.Add('bin/llama.dll', [pscustomobject]@{ length = 2382848L; sha256 = '9cb8142e207cc8b9fff8bf79daca4dc56367643cc615034db5836daf4e8168fc' })
$atomicFiles.Add('licenses/LICENSE.atomicbot-llama.cpp.txt', [pscustomobject]@{ length = 1099L; sha256 = 'bcd8ec749126d45cb06737d0690295d73df4b6e7e194205bcf91190368f27285' })

$listed = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in @($manifest.files)) {
    $relative = [string]$file.relativePath
    if (-not $listed.Add($relative)) {
        throw "Duplicate manifest member: $relative"
    }
    if ([string]$file.sha256 -cnotmatch '^[0-9a-f]{64}$') {
        throw "Manifest member has a non-canonical SHA-256: $relative"
    }
    if ($isAtomicBot) {
        if (-not $atomicFiles.ContainsKey($relative)) {
            throw "The AtomicBot quantizer manifest contains an unexpected member: $relative"
        }
        $expectedAtomicFile = $atomicFiles[$relative]
        if ([long]$file.length -ne [long]$expectedAtomicFile.length -or
            [string]$file.sha256 -cne [string]$expectedAtomicFile.sha256) {
            throw "The AtomicBot quantizer member identity changed: $relative"
        }
    }
    $path = Resolve-ContainedFile -Root $root -RelativePath $relative
    $actualLength = (Get-Item -LiteralPath $path).Length
    if ($actualLength -ne [long]$file.length) {
        throw "Manifest member length mismatch: $relative"
    }
    $actualSha = Get-ExactFileSha256 $path
    if ($actualSha -cne [string]$file.sha256) {
        throw "Manifest member SHA-256 mismatch: $relative"
    }
}

if ($isAtomicBot -and $listed.Count -ne $atomicFiles.Count) {
    throw 'The AtomicBot quantizer closure is incomplete.'
}
$requiredLicense = if ($isAtomicBot) {
    'licenses/LICENSE.atomicbot-llama.cpp.txt'
} else {
    'licenses/LICENSE.llama.cpp.txt'
}
if (-not $listed.Contains([string]$manifest.executableRelativePath) -or
    -not $listed.Contains($requiredLicense)) {
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
