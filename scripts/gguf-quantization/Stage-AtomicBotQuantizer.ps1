[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{64}$')]
    [string]$ExpectedArchiveSha256,

    [Parameter(Mandatory = $true)]
    [string]$StageDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$pinnedArchiveSha256 = '21b751142163cfb15738cb5084abcb83444ce76453bd4aa33e9faeca1966ad3c'
$pinnedArchiveLength = 11551252L
$pinnedDeclarationSha256 = 'c4b6969bf59e05aab486d0192aa57b2c4c5740f2b48b8b6289d21ddbcb4531be'
$pinnedBannerSha256 = '55c9395a51a6512229529f77f23577f9b1f1fbb5b0b6804aa82da0aa6ef99275'
$appLocalDependencies = @(
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
$osProvidedDependencies = @(
    'KERNEL32.dll',
    'VCRUNTIME140.dll',
    'api-ms-win-crt-heap-l1-1-0.dll',
    'api-ms-win-crt-locale-l1-1-0.dll',
    'api-ms-win-crt-math-l1-1-0.dll',
    'api-ms-win-crt-runtime-l1-1-0.dll',
    'api-ms-win-crt-stdio-l1-1-0.dll'
)
$payload = [ordered]@{
    'bin/ggml-base.dll' = 'build/bin/ggml-base.dll'
    'bin/ggml-cpu-alderlake.dll' = 'build/bin/ggml-cpu-alderlake.dll'
    'bin/ggml-cpu-cannonlake.dll' = 'build/bin/ggml-cpu-cannonlake.dll'
    'bin/ggml-cpu-cascadelake.dll' = 'build/bin/ggml-cpu-cascadelake.dll'
    'bin/ggml-cpu-haswell.dll' = 'build/bin/ggml-cpu-haswell.dll'
    'bin/ggml-cpu-icelake.dll' = 'build/bin/ggml-cpu-icelake.dll'
    'bin/ggml-cpu-sandybridge.dll' = 'build/bin/ggml-cpu-sandybridge.dll'
    'bin/ggml-cpu-skylakex.dll' = 'build/bin/ggml-cpu-skylakex.dll'
    'bin/ggml-cpu-sse42.dll' = 'build/bin/ggml-cpu-sse42.dll'
    'bin/ggml-cpu-x64.dll' = 'build/bin/ggml-cpu-x64.dll'
    'bin/ggml.dll' = 'build/bin/ggml.dll'
    'bin/llama-common.dll' = 'build/bin/llama-common.dll'
    'bin/llama-quantize-impl.dll' = 'build/bin/llama-quantize-impl.dll'
    'bin/llama-quantize.exe' = 'build/bin/llama-quantize.exe'
    'bin/llama.dll' = 'build/bin/llama.dll'
    'licenses/LICENSE.atomicbot-llama.cpp.txt' = 'build/bin/LICENSE'
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

function Assert-RegularPathChain {
    param([Parameter(Mandatory = $true)][string]$Path)

    for ($item = Get-Item -LiteralPath $Path -Force;
         $null -ne $item;
         $item = $item.Parent) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "A required path is redirected: $($item.FullName)"
        }
    }
}

function Remove-KnownTemporaryStage {
    param([Parameter(Mandatory = $true)][string]$Root)

    if (-not (Test-Path -LiteralPath $Root)) { return }
    $allowedFiles = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($relative in @($payload.Keys) + 'llama-quantize.package.manifest.json') {
        [void]$allowedFiles.Add([string]$relative)
    }
    $files = [Collections.Generic.List[string]]::new()
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push($Root)
    while ($pending.Count -ne 0) {
        $current = $pending.Pop()
        foreach ($entry in @(Get-ChildItem -LiteralPath $current -Force)) {
            if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { return }
            $relative = $entry.FullName.Substring($Root.Length + 1).Replace('\', '/')
            if ($entry.PSIsContainer) {
                if ($relative -cnotin @('bin', 'licenses')) { return }
                $pending.Push($entry.FullName)
            }
            elseif (-not $allowedFiles.Contains($relative)) {
                return
            }
            else {
                $files.Add($entry.FullName)
            }
        }
    }

    foreach ($path in $files) {
        Remove-Item -LiteralPath $path -Force
    }
    foreach ($relative in @('licenses', 'bin')) {
        $directory = Join-Path $Root $relative
        if (Test-Path -LiteralPath $directory -PathType Container) {
            if (Get-ChildItem -LiteralPath $directory -Force | Select-Object -First 1) { return }
            [IO.Directory]::Delete($directory, $false)
        }
    }
    if (-not (Get-ChildItem -LiteralPath $Root -Force | Select-Object -First 1)) {
        [IO.Directory]::Delete($Root, $false)
    }
}

function Remove-OwnedReceipt {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return }
    $item = Get-Item -LiteralPath $Path -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { return }
    Remove-Item -LiteralPath $Path -Force
}

function Get-TextBytes {
    param([Parameter(Mandatory = $true)][string]$Text)
    return [Text.UTF8Encoding]::new($false).GetBytes($Text)
}

function Write-ReceiptStream {
    param(
        [Parameter(Mandatory = $true)][IO.FileStream]$Stream,
        [Parameter(Mandatory = $true)][string]$Text
    )

    $bytes = Get-TextBytes $Text
    $Stream.Write($bytes, 0, $bytes.Length)
    $Stream.Flush($true)
}

function Invoke-NativeInspection {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string]$Arguments
    )

    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $FilePath
    $start.Arguments = $Arguments
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    if ($null -eq $process) { throw 'The PE inspection process did not start.' }
    try {
        $standardOutput = $process.StandardOutput.ReadToEnd()
        $standardError = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            throw "PE inspection failed: $standardError"
        }
        return @($standardOutput -split "`r?`n")
    }
    finally {
        $process.Dispose()
    }
}

function Get-TextSha256 {
    param([Parameter(Mandatory = $true)][string]$Text)

    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString(
            $algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$declarationPath = Join-Path $repositoryRoot 'runtime\gguf\atomicbot\package-manifest.json'
$bannerPath = Join-Path $repositoryRoot 'experiments\raw-results\atomicbot-turboquant\2026-07-16\safety-bypass\AB-KV8-F16-4K\warmup\stderr.txt'
$archive = [IO.Path]::GetFullPath($ArchivePath)
$stage = [IO.Path]::GetFullPath($StageDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)
if (-not [IO.Path]::IsPathRooted($StageDirectory)) {
    throw 'The AtomicBot stage destination must be fully qualified.'
}
if (Test-Path -LiteralPath $stage) {
    throw 'The AtomicBot stage destination must be absent.'
}
$parent = [IO.Path]::GetDirectoryName($stage)
if ([string]::IsNullOrWhiteSpace($parent) -or -not (Test-Path -LiteralPath $parent -PathType Container)) {
    throw 'The AtomicBot stage parent must be an existing directory.'
}
Assert-RegularPathChain $parent

$receiptPath = $stage + '.construction-receipt.json'
$archiveItem = Get-Item -LiteralPath $archive -Force
if ($archiveItem.PSIsContainer -or
    (($archiveItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) -or
    $archiveItem.Length -ne $pinnedArchiveLength) {
    throw 'The AtomicBot CPU archive is unavailable or changed.'
}
Assert-RegularPathChain $archiveItem.Directory.FullName
$actualArchiveSha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
if ($ExpectedArchiveSha256.ToLowerInvariant() -cne $pinnedArchiveSha256 -or
    $actualArchiveSha256 -cne $pinnedArchiveSha256) {
    throw 'The AtomicBot CPU archive identity changed.'
}
if ((Get-FileHash -LiteralPath $declarationPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $pinnedDeclarationSha256) {
    throw 'The tracked AtomicBot archive declaration changed.'
}
if ((Get-FileHash -LiteralPath $bannerPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $pinnedBannerSha256 -or
    -not (Select-String -LiteralPath $bannerPath -SimpleMatch 'build 9964 (519f0c594) with MSVC 19.51.36248.0 for Windows AMD64')) {
    throw 'The retained AtomicBot compiler-banner evidence changed.'
}

$visualStudioRoots = @(
    [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles),
    [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique
$dumpbin = $visualStudioRoots | ForEach-Object {
    $visualStudio = Join-Path $_ 'Microsoft Visual Studio'
    if (Test-Path -LiteralPath $visualStudio -PathType Container) {
        Get-ChildItem -LiteralPath $visualStudio -Filter dumpbin.exe -File -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\Hostx64\\x64\\dumpbin\.exe$' }
    }
} | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $dumpbin) { throw 'Visual Studio dumpbin.exe was not found for PE inspection.' }

$temporary = Join-Path $parent ('.' + [IO.Path]::GetFileName($stage) + '.staging-' + [Guid]::NewGuid().ToString('N'))
$receiptStream = $null
$receiptOwned = $false
$published = $false
try {
    $receiptStream = [IO.FileStream]::new(
        $receiptPath,
        [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write,
        [IO.FileShare]::None)
    $receiptOwned = $true
    [IO.Directory]::CreateDirectory($temporary) | Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $temporary 'bin')) | Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $temporary 'licenses')) | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        foreach ($relative in $payload.Keys) {
            $entryName = [string]$payload[$relative]
            $matches = @($zip.Entries | Where-Object { $_.FullName -ceq $entryName })
            if ($matches.Count -ne 1 -or
                (($matches[0].ExternalAttributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
                throw "The AtomicBot archive member is missing, duplicated, or redirected: $entryName"
            }
            $destination = Join-Path $temporary ([string]$relative).Replace('/', [IO.Path]::DirectorySeparatorChar)
            $source = $matches[0].Open()
            try {
                $target = [IO.FileStream]::new($destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
                try { $source.CopyTo($target) } finally { $target.Dispose() }
            } finally { $source.Dispose() }
        }
    } finally { $zip.Dispose() }

    $executable = Join-Path $temporary 'bin\llama-quantize.exe'
    $dependencyTranscript = @(Invoke-NativeInspection -FilePath $dumpbin -Arguments "/nologo /dependents `"$executable`"")
    $directDependencies = @($dependencyTranscript | ForEach-Object {
        if ($_ -match '^\s+([A-Za-z0-9_.-]+\.dll)\s*$') { $Matches[1] }
    } | Sort-Object -CaseSensitive -Unique)
    $expectedDirectDependencies = @('KERNEL32.dll', 'VCRUNTIME140.dll',
        'api-ms-win-crt-heap-l1-1-0.dll', 'api-ms-win-crt-locale-l1-1-0.dll',
        'api-ms-win-crt-math-l1-1-0.dll', 'api-ms-win-crt-runtime-l1-1-0.dll',
        'api-ms-win-crt-stdio-l1-1-0.dll', 'llama-quantize-impl.dll')
    if ($directDependencies.Count -ne $expectedDirectDependencies.Count -or
        (Compare-Object -CaseSensitive -ReferenceObject $expectedDirectDependencies -DifferenceObject $directDependencies)) {
        throw 'The AtomicBot quantizer direct PE dependency set changed.'
    }
    $headerTranscript = @(Invoke-NativeInspection -FilePath $dumpbin -Arguments "/nologo /headers `"$executable`"")
    if (-not ($headerTranscript -match '^\s+14\.44 linker version\s*$')) {
        throw 'The AtomicBot quantizer PE linker identity changed.'
    }

    $files = @($payload.Keys | ForEach-Object {
        $path = Join-Path $temporary ([string]$_).Replace('/', [IO.Path]::DirectorySeparatorChar)
        $item = Get-Item -LiteralPath $path -Force
        [ordered]@{
            relativePath = [string]$_
            length = $item.Length
            sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
    $manifest = [ordered]@{
        schemaVersion = 1
        packageId = 'granite-edge-ai-atomicbot-llama-quantize-x64'
        source = [ordered]@{
            url = 'https://github.com/AtomicBot-ai/atomic-llama-cpp-turboquant'
            commit = '519f0c594a8e31467d2e2f2cf17054c9e7e11536'
        }
        target = 'llama-quantize'
        architecture = 'x64'
        configuration = 'Release'
        libraryLinkage = 'dynamic'
        cmakeFlags = @()
        toolchain = [ordered]@{
            visualStudio = 'not-attested-by-upstream-release'
            msvc = 'runtime-banner-msvc-19.51.36248.0_pe-linker-14.44'
            cmake = 'not-attested-by-upstream-release'
        }
        osProvidedDependencies = $osProvidedDependencies
        appLocalDependencies = $appLocalDependencies
        executableRelativePath = 'bin/llama-quantize.exe'
        allowedTokens = @('Q2_K', 'Q3_K_M', 'Q4_K_M', 'Q5_K_M', 'Q6_K', 'Q8_0')
        maximumSourceBytes = 68719476736
        maximumOutputBytes = 68719476736
        timeoutSeconds = 21600
        standardOutputMaximumBytes = 1048576
        standardErrorMaximumBytes = 1048576
        license = [ordered]@{
            identity = 'MIT'
            relativePath = 'licenses/LICENSE.atomicbot-llama.cpp.txt'
        }
        files = $files
    }
    $manifestPath = Join-Path $temporary 'llama-quantize.package.manifest.json'
    [IO.File]::WriteAllText(
        $manifestPath,
        ($manifest | ConvertTo-Json -Depth 10) + "`n",
        [Text.UTF8Encoding]::new($false))
    $manifestSha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    & (Join-Path $PSScriptRoot 'Test-GgufQuantizerPackage.ps1') -StageDirectory $temporary -ExpectedManifestSha256 $manifestSha256
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -cne $pinnedArchiveSha256) {
        throw 'The AtomicBot CPU archive changed during construction.'
    }

    $dependencyText = ($dependencyTranscript -join "`n") + "`n"
    $headerText = ($headerTranscript -join "`n") + "`n"
    $receiptMembers = @($files) + [ordered]@{
        relativePath = 'llama-quantize.package.manifest.json'
        length = (Get-Item -LiteralPath $manifestPath).Length
        sha256 = $manifestSha256
    }
    $receipt = [ordered]@{
        schemaVersion = 1
        evidenceKind = 'atomicbot-standalone-quantizer-construction-v1'
        trackedArchiveDeclarationSha256 = $pinnedDeclarationSha256
        cpuArchiveSha256 = $pinnedArchiveSha256
        stagingScriptSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
        peInspectorSha256 = (Get-FileHash -LiteralPath $dumpbin -Algorithm SHA256).Hash.ToLowerInvariant()
        peImportTranscript = [ordered]@{
            utf8Length = (Get-TextBytes $dependencyText).Length
            sha256 = Get-TextSha256 $dependencyText
            canonicalText = $dependencyText
        }
        peHeaderTranscript = [ordered]@{
            utf8Length = (Get-TextBytes $headerText).Length
            sha256 = Get-TextSha256 $headerText
            canonicalText = $headerText
        }
        runtimeBannerEvidenceSha256 = $pinnedBannerSha256
        standaloneManifestSha256 = $manifestSha256
        members = $receiptMembers
    }
    Write-ReceiptStream $receiptStream (($receipt | ConvertTo-Json -Depth 10) + "`n")
    $receiptStream.Dispose()
    $receiptStream = $null

    [IO.Directory]::Move($temporary, $stage)
    $published = $true
    Write-Output "GGUF_QUANTIZER_STAGE=$stage"
    Write-Output "GGUF_QUANTIZER_MANIFEST_SHA256=$manifestSha256"
    Write-Output "GGUF_QUANTIZER_CONSTRUCTION_RECEIPT=$receiptPath"
}
finally {
    if ($null -ne $receiptStream) {
        $receiptStream.Dispose()
        $receiptStream = $null
    }
    if (-not $published) {
        Remove-KnownTemporaryStage $temporary
        if ($receiptOwned) {
            Remove-OwnedReceipt $receiptPath
        }
    }
}
