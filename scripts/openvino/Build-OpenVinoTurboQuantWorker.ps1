[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SourceRoot,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$OfficialArchiveDirectory,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$RuntimeStageDirectory,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$BuildDirectory,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$StageDirectory
)

$ErrorActionPreference = 'Stop'

function Stop-Build {
    [Console]::Out.WriteLine('turboquant_worker_build_failed')
    exit 1
}

function Test-PathOverlap {
    param([string]$Left, [string]$Right)
    $leftRoot = $Left.TrimEnd('\', '/')
    $rightRoot = $Right.TrimEnd('\', '/')
    return $leftRoot -ieq $rightRoot -or
        $leftRoot.StartsWith($rightRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
        $rightRoot.StartsWith($leftRoot + '\', [StringComparison]::OrdinalIgnoreCase)
}

try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
    $source = [IO.Path]::GetFullPath($SourceRoot)
    $archiveRoot = [IO.Path]::GetFullPath($OfficialArchiveDirectory)
    $runtimeStage = [IO.Path]::GetFullPath($RuntimeStageDirectory)
    $buildRoot = [IO.Path]::GetFullPath($BuildDirectory)
    $stageRoot = [IO.Path]::GetFullPath($StageDirectory)
    $inputRoots = @($source, $archiveRoot, $runtimeStage)
    $ownedRoots = @($buildRoot, $stageRoot)
    if (Test-PathOverlap $buildRoot $stageRoot) { Stop-Build }
    foreach ($owned in $ownedRoots) {
        if ($owned -ieq $repositoryRoot -or
            $owned.StartsWith($repositoryRoot.TrimEnd('\', '/') + '\', [StringComparison]::OrdinalIgnoreCase) -or
            (Test-Path -LiteralPath $owned)) {
            Stop-Build
        }
        foreach ($inputRoot in $inputRoots) {
            if (Test-PathOverlap $owned $inputRoot) { Stop-Build }
        }
    }

    $powershell = (Get-Command powershell.exe -ErrorAction Stop).Source
    $runtimeVerification = & $powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'Test-OpenVinoTurboQuantPatchClosure.ps1') `
        -SourceRoot $source -StageDirectory $runtimeStage
    if ($LASTEXITCODE -ne 0 -or [string]$runtimeVerification -cne 'turboquant_patch_closure_valid') {
        Stop-Build
    }
    $archiveVerification = & $powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'Test-OpenVinoDependencyLocks.ps1') `
        -ClosureDirectory $archiveRoot -Scope Official
    if ($LASTEXITCODE -ne 0 -or [string]$archiveVerification -cne 'dependency_lock_valid') {
        Stop-Build
    }

    New-Item -ItemType Directory -Path $buildRoot | Out-Null
    New-Item -ItemType Directory -Path $stageRoot | Out-Null
    foreach ($entry in @(Get-ChildItem -LiteralPath $runtimeStage -Force)) {
        Copy-Item -LiteralPath $entry.FullName -Destination $stageRoot -Recurse
    }
    $extractRoot = Join-Path $buildRoot 'archive'
    New-Item -ItemType Directory -Path $extractRoot | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory(
        (Join-Path $archiveRoot 'openvino_genai_windows_2026.3.0.0_x86_64.zip'),
        $extractRoot)
    $distribution = Join-Path $extractRoot 'openvino_genai_windows_2026.3.0.0_x86_64'
    $runtime = Join-Path $distribution 'runtime'
    $jsonInclude = Join-Path $distribution 'samples\cpp\thirdparty\nlohmann_json\include'

    $cmake = (Get-Command cmake.exe -ErrorAction SilentlyContinue).Source
    if (-not $cmake) {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        $installation = & $vswhere -latest -products * `
            -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
        $cmake = Join-Path $installation 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
    }
    if (-not (Test-Path -LiteralPath $cmake -PathType Leaf)) { Stop-Build }

    $nativeBuild = Join-Path $buildRoot 'native'
    & $cmake -S (Join-Path $repositoryRoot 'workers\OpenVinoTurboQuant.Worker') `
        -B $nativeBuild -G 'Visual Studio 17 2022' -A x64 `
        "-DCMAKE_PREFIX_PATH=$runtime" "-DNLOHMANN_JSON_INCLUDE_DIR=$jsonInclude"
    if ($LASTEXITCODE -ne 0) { Stop-Build }
    & $cmake --build $nativeBuild --config Release --parallel
    if ($LASTEXITCODE -ne 0) { Stop-Build }

    Copy-Item -LiteralPath (Join-Path $nativeBuild 'Release\OpenVinoTurboQuant.Worker.exe') `
        -Destination (Join-Path $stageRoot 'OpenVinoTurboQuant.Worker.exe')
    Copy-Item -LiteralPath (Join-Path $nativeBuild 'Release\OpenVinoTurboQuant.Probe.dll') `
        -Destination (Join-Path $stageRoot 'OpenVinoTurboQuant.Probe.dll')
    Copy-Item -LiteralPath (Join-Path $distribution 'samples\cpp\thirdparty\nlohmann_json\LICENSE.MIT') `
        -Destination (Join-Path $stageRoot 'licenses\nlohmann-json-LICENSE.MIT.txt')
    $manifestOutput = & $powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'New-OpenVinoTurboQuantWorkerManifest.ps1') `
        -StageDirectory $stageRoot
    if ($LASTEXITCODE -ne 0 -or [string]$manifestOutput -cne 'turboquant_worker_manifest_created') {
        Stop-Build
    }
    $verifyOutput = & $powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'Test-OpenVinoTurboQuantWorkerManifest.ps1') `
        -StageDirectory $stageRoot
    if ($LASTEXITCODE -ne 0 -or [string]$verifyOutput -cne 'turboquant_worker_manifest_valid') {
        Stop-Build
    }
    $oldPath = $env:PATH
    try {
        $env:PATH = (Join-Path $nativeBuild 'Release') + ';' +
            (Join-Path $runtime 'bin\intel64\Release') + ';' +
            (Join-Path $runtime '3rdparty\tbb\bin') + ';' + $oldPath
        & (Join-Path (Split-Path $cmake) 'ctest.exe') `
            --test-dir $nativeBuild -C Release --output-on-failure
        if ($LASTEXITCODE -ne 0) { Stop-Build }
    }
    finally {
        $env:PATH = $oldPath
    }
    [Console]::Out.WriteLine('turboquant_worker_built')
}
catch {
    Stop-Build
}
