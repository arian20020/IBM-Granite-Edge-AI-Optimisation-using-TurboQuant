[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$StageDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$lockPath = Join-Path $repositoryRoot 'third-party\llama-quantize\source.lock.json'
$lock = Get-Content -Raw -LiteralPath $lockPath | ConvertFrom-Json
$stage = [IO.Path]::GetFullPath($StageDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)

if (Test-Path -LiteralPath $stage) {
    $stageItem = Get-Item -LiteralPath $stage -Force
    if (-not $stageItem.PSIsContainer -or (($stageItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
        throw 'The stage must be a regular directory.'
    }
    if (Get-ChildItem -LiteralPath $stage -Force | Select-Object -First 1) {
        throw 'The stage directory must be empty.'
    }
} else {
    [IO.Directory]::CreateDirectory($stage) | Out-Null
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw 'Visual Studio Installer vswhere.exe was not found.'
}
$vsRoot = (& $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Select-Object -First 1)
if (-not $vsRoot) {
    throw 'A Visual Studio installation with the x64 C++ toolchain is required.'
}
$cmake = Get-ChildItem -LiteralPath (Join-Path $vsRoot 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin') -Filter cmake.exe -File | Select-Object -First 1 -ExpandProperty FullName
if (-not $cmake) {
    throw 'Visual Studio CMake was not found.'
}
$dumpbin = Get-ChildItem -LiteralPath (Join-Path $vsRoot 'VC\Tools\MSVC') -Filter dumpbin.exe -File -Recurse |
    Where-Object { $_.FullName -match '\\Hostx64\\x64\\dumpbin\.exe$' } |
    Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $dumpbin) {
    throw 'Visual Studio dumpbin.exe was not found.'
}
$masm = Get-ChildItem -LiteralPath (Join-Path $vsRoot 'VC\Tools\MSVC') -Filter ml64.exe -File -Recurse |
    Where-Object { $_.FullName -match '\\Hostx64\\x64\\ml64\.exe$' } |
    Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $masm) {
    throw 'Visual Studio x64 MASM was not found.'
}

$operationRoot = Join-Path ([IO.Path]::GetTempPath()) ('geai-llama-quantize-' + [Guid]::NewGuid().ToString('N'))
$sourceRoot = Join-Path $operationRoot 'source'
$buildRoot = Join-Path $operationRoot 'build'
[IO.Directory]::CreateDirectory($operationRoot) | Out-Null

try {
    & git clone --no-checkout --filter=blob:none --no-tags --config advice.detachedHead=false -- $lock.sourceUrl $sourceRoot
    if ($LASTEXITCODE -ne 0) { throw 'The locked llama.cpp source clone failed.' }
    & git -C $sourceRoot checkout --detach --force $lock.sourceCommit
    if ($LASTEXITCODE -ne 0) { throw 'The locked llama.cpp commit checkout failed.' }
    $actualCommit = (& git -C $sourceRoot rev-parse HEAD).Trim()
    if ($actualCommit -cne $lock.sourceCommit) { throw "Unexpected source commit: $actualCommit" }

    $arguments = @(
        '-S', $sourceRoot,
        '-B', $buildRoot,
        '-A', 'x64',
        "-DCMAKE_ASM_COMPILER=$masm",
        '-DGGML_NATIVE=OFF',
        '-DGGML_OPENMP=OFF',
        '-DLLAMA_CURL=OFF',
        '-DBUILD_SHARED_LIBS=OFF'
    )
    & $cmake @arguments
    if ($LASTEXITCODE -ne 0) { throw 'CMake configuration failed.' }
    & $cmake --build $buildRoot --config Release --target llama-quantize --parallel 1
    if ($LASTEXITCODE -ne 0) { throw 'The locked llama-quantize build failed.' }

    $executable = Get-ChildItem -LiteralPath $buildRoot -Filter llama-quantize.exe -File -Recurse | Select-Object -First 1
    if (-not $executable) { throw 'The build did not produce llama-quantize.exe.' }

    $binDirectory = Join-Path $stage 'bin'
    $licenseDirectory = Join-Path $stage 'licenses'
    [IO.Directory]::CreateDirectory($binDirectory) | Out-Null
    [IO.Directory]::CreateDirectory($licenseDirectory) | Out-Null
    Copy-Item -LiteralPath $executable.FullName -Destination (Join-Path $binDirectory 'llama-quantize.exe')
    Copy-Item -LiteralPath (Join-Path $repositoryRoot $lock.licensePath) -Destination (Join-Path $licenseDirectory 'LICENSE.llama.cpp.txt')

    $dependencyOutput = & $dumpbin /dependents $executable.FullName
    if ($LASTEXITCODE -ne 0) { throw 'dumpbin dependency inspection failed.' }
    $dependencies = @($dependencyOutput | ForEach-Object {
        if ($_ -match '^\s+([A-Za-z0-9_.-]+\.dll)\s*$') { $Matches[1] }
    } | Sort-Object -Unique)
    $systemDependencies = @()
    $appLocalDependencies = @()
    foreach ($dependency in $dependencies) {
        if ($dependency -match '^(api-ms-win-|ext-ms-win-)' -or $dependency -in @(
            'KERNEL32.dll','USER32.dll','ADVAPI32.dll','SHELL32.dll','OLE32.dll','OLEAUT32.dll',
            'WS2_32.dll','BCRYPT.dll','CRYPT32.dll','NTDLL.dll','UCRTBASE.dll')) {
            $systemDependencies += $dependency
            continue
        }

        $candidate = Get-ChildItem -LiteralPath $buildRoot -Filter $dependency -File -Recurse | Select-Object -First 1
        if (-not $candidate -and $dependency -match '^(VCRUNTIME|MSVCP|CONCRT)') {
            $candidate = Get-ChildItem -LiteralPath (Join-Path $vsRoot 'VC\Redist\MSVC') -Filter $dependency -File -Recurse |
                Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
        }
        if (-not $candidate) { throw "Unresolved non-system dependency: $dependency" }
        Copy-Item -LiteralPath $candidate.FullName -Destination (Join-Path $binDirectory $dependency)
        $appLocalDependencies += $dependency
    }

    $stagePrefix = $stage + [IO.Path]::DirectorySeparatorChar
    $files = Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName | ForEach-Object {
        if (-not $_.FullName.StartsWith($stagePrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Package member escaped the stage: $($_.FullName)"
        }
        [ordered]@{
            relativePath = $_.FullName.Substring($stagePrefix.Length).Replace('\', '/')
            length = $_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
    $msvcRoot = ($dumpbin -split '\\bin\\', 2)[0]
    $msvcVersion = Split-Path $msvcRoot -Leaf
    $manifest = [ordered]@{
        schemaVersion = 1
        packageId = 'granite-edge-ai-llama-quantize-x64'
        source = [ordered]@{ url = [string]$lock.sourceUrl; commit = [string]$lock.sourceCommit }
        target = [string]$lock.target
        architecture = 'x64'
        configuration = 'Release'
        libraryLinkage = 'static'
        cmakeFlags = @($lock.cmakeFlags)
        toolchain = [ordered]@{
            visualStudio = (Split-Path $vsRoot -Leaf)
            msvc = $msvcVersion
            cmake = (& $cmake --version | Select-Object -First 1).Trim()
        }
        osProvidedDependencies = @($systemDependencies | Sort-Object -Unique)
        appLocalDependencies = @($appLocalDependencies | Sort-Object -Unique)
        executableRelativePath = 'bin/llama-quantize.exe'
        allowedTokens = @('Q2_K','Q3_K_M','Q4_K_M','Q5_K_M','Q6_K','Q8_0')
        maximumSourceBytes = 68719476736
        maximumOutputBytes = 68719476736
        timeoutSeconds = 21600
        standardOutputMaximumBytes = 1048576
        standardErrorMaximumBytes = 1048576
        license = [ordered]@{ identity = 'MIT'; relativePath = 'licenses/LICENSE.llama.cpp.txt' }
        files = @($files)
    }
    $manifestJson = $manifest | ConvertTo-Json -Depth 10
    [IO.File]::WriteAllText(
        (Join-Path $stage 'llama-quantize.package.manifest.json'),
        $manifestJson + "`n",
        [Text.UTF8Encoding]::new($false))

    $manifestPath = Join-Path $stage 'llama-quantize.package.manifest.json'
    $manifestSha = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    & (Join-Path $PSScriptRoot 'Test-GgufQuantizerPackage.ps1') -StageDirectory $stage -ExpectedManifestSha256 $manifestSha
    Write-Output "GGUF_QUANTIZER_STAGE=$stage"
    Write-Output "GGUF_QUANTIZER_MANIFEST_SHA256=$manifestSha"
}
finally {
    if (Test-Path -LiteralPath $operationRoot) {
        Remove-Item -LiteralPath $operationRoot -Recurse -Force
    }
}
