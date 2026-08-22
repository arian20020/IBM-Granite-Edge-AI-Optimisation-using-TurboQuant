[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$SourceRoot,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$OfficialArchiveDirectory,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$BuildDirectory,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$StageDirectory
)

$ErrorActionPreference = 'Stop'

function Stop-Build {
    [Console]::Out.WriteLine('turboquant_runtime_build_failed')
    exit 1
}

try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
    $source = [IO.Path]::GetFullPath($SourceRoot)
    $archiveRoot = [IO.Path]::GetFullPath($OfficialArchiveDirectory)
    $buildRoot = [IO.Path]::GetFullPath($BuildDirectory)
    $stageRoot = [IO.Path]::GetFullPath($StageDirectory)
    foreach ($owned in @($buildRoot, $stageRoot)) {
        if ($owned -ieq $repositoryRoot -or
            $owned.StartsWith($repositoryRoot.TrimEnd('\', '/') + '\', [StringComparison]::OrdinalIgnoreCase) -or
            (Test-Path -LiteralPath $owned)) {
            Stop-Build
        }
    }

    $windowsPowerShell = (Get-Command powershell.exe -ErrorAction Stop).Source
    $verifySource = & $windowsPowerShell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'Test-OpenVinoTurboQuantPatchClosure.ps1') `
        -SourceRoot $source
    if ($LASTEXITCODE -ne 0 -or [string]$verifySource -cne 'turboquant_patch_closure_valid') {
        Stop-Build
    }
    $verifyArchives = & $windowsPowerShell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'Test-OpenVinoDependencyLocks.ps1') `
        -ClosureDirectory $archiveRoot -Scope Official
    if ($LASTEXITCODE -ne 0 -or [string]$verifyArchives -cne 'dependency_lock_valid') {
        Stop-Build
    }

    $cmake = $null
    $command = Get-Command cmake.exe -ErrorAction SilentlyContinue
    if ($command) { $cmake = $command.Source }
    if (-not $cmake) {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        $installation = & $vswhere -latest -products * `
            -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
        $candidate = Join-Path $installation 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { $cmake = $candidate }
    }
    if (-not $cmake) { Stop-Build }

    New-Item -ItemType Directory -Path $buildRoot | Out-Null
    New-Item -ItemType Directory -Path $stageRoot | Out-Null
    $extractRoot = Join-Path $buildRoot 'archive'
    New-Item -ItemType Directory -Path $extractRoot | Out-Null
    $archive = Join-Path $archiveRoot 'openvino_genai_windows_2026.3.0.0_x86_64.zip'
    [IO.Compression.ZipFile]::ExtractToDirectory($archive, $extractRoot)
    $distribution = Join-Path $extractRoot 'openvino_genai_windows_2026.3.0.0_x86_64'
    $runtime = Join-Path $distribution 'runtime'
    $nativeBuild = Join-Path $buildRoot 'native'

    & $cmake -S (Join-Path $repositoryRoot 'workers\OpenVinoTurboQuant.Worker\native') `
        -B $nativeBuild -G 'Visual Studio 17 2022' -A x64 `
        "-DCMAKE_PREFIX_PATH=$runtime" "-DOPENVINO_SOURCE_ROOT=$source"
    if ($LASTEXITCODE -ne 0) { Stop-Build }
    & $cmake --build $nativeBuild --config Release --parallel
    if ($LASTEXITCODE -ne 0) { Stop-Build }

    $releaseBin = Join-Path $runtime 'bin\intel64\Release'
    $copyMap = [ordered]@{
        (Join-Path $nativeBuild 'Release\tbq4_codec_tests.exe') = 'tests\tbq4_codec_tests.exe'
        (Join-Path $nativeBuild 'Release\tbq4_dispatch_tests.exe') = 'tests\tbq4_dispatch_tests.exe'
        (Join-Path $releaseBin 'openvino.dll') = 'openvino.dll'
        (Join-Path $releaseBin 'openvino_genai.dll') = 'openvino_genai.dll'
        (Join-Path $releaseBin 'openvino_tokenizers.dll') = 'openvino_tokenizers.dll'
        (Join-Path $releaseBin 'openvino_intel_cpu_plugin.dll') = 'openvino_intel_cpu_plugin.dll'
        (Join-Path $releaseBin 'openvino_ir_frontend.dll') = 'openvino_ir_frontend.dll'
        (Join-Path $runtime '3rdparty\tbb\bin\tbb12.dll') = 'tbb12.dll'
        (Join-Path $runtime '3rdparty\tbb\bin\tbbbind_2_5.dll') = 'tbbbind_2_5.dll'
    }
    foreach ($entry in $copyMap.GetEnumerator()) {
        $destination = Join-Path $stageRoot $entry.Value
        New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
        Copy-Item -LiteralPath $entry.Key -Destination $destination
    }

    $licenseMap = [ordered]@{
        (Join-Path $distribution 'docs\licensing\Apache_license.txt') = 'licenses\Apache_license.txt'
        (Join-Path $distribution 'docs\licensing\LICENSE-GENAI') = 'licenses\LICENSE-GENAI.txt'
        (Join-Path $distribution 'docs\licensing\runtime-third-party-programs.txt') = 'licenses\runtime-third-party-programs.txt'
        (Join-Path $distribution 'docs\licensing\third-party-programs-genai.txt') = 'licenses\third-party-programs-genai.txt'
        (Join-Path $distribution 'docs\openvino_tokenizers\LICENSE') = 'licenses\openvino-tokenizers-LICENSE.txt'
        (Join-Path $distribution 'docs\openvino_tokenizers\third-party-programs.txt') = 'licenses\openvino-tokenizers-third-party-programs.txt'
        (Join-Path $runtime '3rdparty\tbb\TBB-LICENSE') = 'licenses\TBB-LICENSE.txt'
    }
    foreach ($entry in $licenseMap.GetEnumerator()) {
        $destination = Join-Path $stageRoot $entry.Value
        New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
        Copy-Item -LiteralPath $entry.Key -Destination $destination
    }
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'third-party\openvino-turboquant\upstream.lock.json') `
        -Destination (Join-Path $stageRoot 'source-manifest.json')
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'third-party\openvino-turboquant\patches\series.json') `
        -Destination (Join-Path $stageRoot 'patch-manifest.json')
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'third-party\openvino-turboquant\LICENSES.md') `
        -Destination (Join-Path $stageRoot 'licenses\TurboQuant-LICENSES.md')

    $oldPath = $env:PATH
    try {
        $env:PATH = $stageRoot + ';' + $oldPath
        & (Join-Path $stageRoot 'tests\tbq4_codec_tests.exe')
        if ($LASTEXITCODE -ne 0) { Stop-Build }
        & (Join-Path $stageRoot 'tests\tbq4_dispatch_tests.exe')
        if ($LASTEXITCODE -ne 0) { Stop-Build }
    }
    finally {
        $env:PATH = $oldPath
    }

    $files = @(
        Get-ChildItem -LiteralPath $stageRoot -File -Recurse |
            Sort-Object FullName |
            ForEach-Object {
                $relative = $_.FullName.Substring($stageRoot.TrimEnd('\', '/').Length + 1).Replace('\', '/')
                if ($relative.StartsWith('licenses/', [StringComparison]::Ordinal)) {
                    $kind = 'license'
                }
                elseif ($relative.StartsWith('tests/', [StringComparison]::Ordinal)) {
                    $kind = 'conformance-test'
                }
                elseif ($relative.EndsWith('manifest.json', [StringComparison]::Ordinal)) {
                    $kind = 'source-ledger'
                }
                else {
                    $kind = 'runtime-binary'
                }
                [ordered]@{
                    path = $relative
                    length = $_.Length
                    sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                    kind = $kind
                }
            })
    $manifest = [ordered]@{
        schemaVersion = 1
        component = 'openvino-turboquant-runtime'
        platform = 'windows-x86_64'
        configuration = 'Release'
        sourceCommit = '8a17657b995fd3b4a52f8484acfcf2bb61214623'
        implementationCommit = 'b9a1f201c109e0bed74763934f79483cf6c4cbf4'
        genAiCommit = 'bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0'
        acceptedTuple = [ordered]@{
            codec = 'TBQ4/TBQ4'
            device = 'CPU'
            attention = 'SDPA'
            headDimension = 64
        }
        files = $files
    }
    $manifestJson = $manifest | ConvertTo-Json -Depth 8
    [IO.File]::WriteAllText(
        (Join-Path $stageRoot 'turboquant-runtime.manifest.json'),
        $manifestJson,
        [Text.UTF8Encoding]::new($false))

    $verifyStage = & $windowsPowerShell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'Test-OpenVinoTurboQuantPatchClosure.ps1') `
        -SourceRoot $source -StageDirectory $stageRoot
    if ($LASTEXITCODE -ne 0 -or [string]$verifyStage -cne 'turboquant_patch_closure_valid') {
        Stop-Build
    }
    [Console]::Out.WriteLine('turboquant_runtime_built')
}
catch {
    Stop-Build
}
