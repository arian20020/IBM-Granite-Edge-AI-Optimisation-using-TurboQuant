[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SourceRoot,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$GenAiSourceRoot,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$OfficialDistributionDirectory,
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
    $genAiSource = [IO.Path]::GetFullPath($GenAiSourceRoot)
    $distribution = [IO.Path]::GetFullPath($OfficialDistributionDirectory)
    $runtimeStage = [IO.Path]::GetFullPath($RuntimeStageDirectory)
    $buildRoot = [IO.Path]::GetFullPath($BuildDirectory)
    $stageRoot = [IO.Path]::GetFullPath($StageDirectory)
    $inputRoots = @($source, $genAiSource, $distribution, $runtimeStage)
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
    if (-not (Test-Path -LiteralPath $distribution -PathType Container)) { Stop-Build }
    $git = (Get-Command git.exe -ErrorAction Stop).Source
    $genAiHead = & $git -C $genAiSource rev-parse HEAD
    $genAiStatus = @(& $git -C $genAiSource status --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0 -or
        [string]$genAiHead -cne '6fbc103538d30d42da4b0b5130a4792a20f728ba' -or
        $genAiStatus.Count -ne 0) { Stop-Build }

    New-Item -ItemType Directory -Path $buildRoot | Out-Null
    New-Item -ItemType Directory -Path $stageRoot | Out-Null
    foreach ($entry in @(Get-ChildItem -LiteralPath $runtimeStage -Force)) {
        Copy-Item -LiteralPath $entry.FullName -Destination $stageRoot -Recurse
    }
    $runtime = Join-Path $distribution 'runtime'
    $jsonInclude = Join-Path $distribution 'samples\cpp\thirdparty\nlohmann_json\include'

    $importLibraryDirectory = Join-Path $buildRoot 'import-libs'
    $importLibraryOutput = & $powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'New-WindowsImportLibrary.ps1') `
        -DllPath (Join-Path $runtimeStage 'openvino.dll') `
        -OutputDirectory $importLibraryDirectory
    if ($LASTEXITCODE -ne 0 -or [string]$importLibraryOutput -cne (Join-Path $importLibraryDirectory 'openvino.lib')) { Stop-Build }
    $importLibraryOutput = & $powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'New-WindowsImportLibrary.ps1') `
        -DllPath (Join-Path $runtimeStage 'openvino_genai.dll') `
        -OutputDirectory $importLibraryDirectory
    if ($LASTEXITCODE -ne 0 -or [string]$importLibraryOutput -cne (Join-Path $importLibraryDirectory 'openvino_genai.lib')) { Stop-Build }

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
        "-DCMAKE_PREFIX_PATH=$runtime" "-DNLOHMANN_JSON_INCLUDE_DIR=$jsonInclude" `
        "-DOPENVINO_TURBOQUANT_SOURCE_ROOT=$source" `
        "-DOPENVINO_TURBOQUANT_GENAI_SOURCE_ROOT=$genAiSource" `
        "-DOPENVINO_TURBOQUANT_IMPORT_LIBRARY_DIR=$importLibraryDirectory"
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
        $env:PATH = (Join-Path $nativeBuild 'Release') + ';' + $stageRoot + ';' + $oldPath
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
