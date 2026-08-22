[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$OfficialArchiveDirectory,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$BuildDirectory,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$StageDirectory,

    [string]$FixtureRoot = '',

    [string]$ExpectedFixtureManifestSha256 = ''
)

$ErrorActionPreference = 'Stop'

function Stop-Build {
    [Console]::Out.WriteLine('official_worker_build_failed')
    exit 1
}

try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
    $archiveRoot = [IO.Path]::GetFullPath($OfficialArchiveDirectory)
    $buildRoot = [IO.Path]::GetFullPath($BuildDirectory)
    $stageRoot = [IO.Path]::GetFullPath($StageDirectory)
    foreach ($owned in @($buildRoot, $stageRoot)) {
        if ($owned.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            (Test-Path -LiteralPath $owned)) {
            Stop-Build
        }
    }

    $windowsPowerShell = (Get-Command powershell.exe -ErrorAction Stop).Source
    $lockOutput = & $windowsPowerShell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-OpenVinoDependencyLocks.ps1') -ClosureDirectory $archiveRoot -Scope Official
    if ($LASTEXITCODE -ne 0 -or [string]$lockOutput -cne 'dependency_lock_valid') {
        Stop-Build
    }

    New-Item -ItemType Directory -Path $buildRoot | Out-Null
    New-Item -ItemType Directory -Path $stageRoot | Out-Null
    $extractRoot = Join-Path $buildRoot 'archive'
    New-Item -ItemType Directory -Path $extractRoot | Out-Null
    $genAiArchive = Join-Path $archiveRoot 'openvino_genai_windows_2026.3.0.0_x86_64.zip'
    [IO.Compression.ZipFile]::ExtractToDirectory($genAiArchive, $extractRoot)
    $distribution = Join-Path $extractRoot 'openvino_genai_windows_2026.3.0.0_x86_64'
    $runtime = Join-Path $distribution 'runtime'
    $jsonInclude = Join-Path $distribution 'samples\cpp\thirdparty\nlohmann_json\include'

    $cmake = $null
    $command = Get-Command cmake.exe -ErrorAction SilentlyContinue
    if ($command) {
        $cmake = $command.Source
    }
    if (-not $cmake) {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        $installation = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
        $candidate = Join-Path $installation 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            $cmake = $candidate
        }
    }
    if (-not $cmake) {
        Stop-Build
    }

    $nativeBuild = Join-Path $buildRoot 'native'
    if ([string]::IsNullOrWhiteSpace($FixtureRoot)) {
        $fixtureRootPath = Join-Path $repositoryRoot 'tests\TestFixtures\OpenVINO\GenAI\TinySyntheticV1'
    }
    else {
        $fixtureRootPath = [IO.Path]::GetFullPath($FixtureRoot)
        $repositoryPrefix = $repositoryRoot.TrimEnd('\', '/') +
            [IO.Path]::DirectorySeparatorChar
        if ($fixtureRootPath -ieq $repositoryRoot -or
            $fixtureRootPath.StartsWith(
                $repositoryPrefix,
                [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $fixtureRootPath -PathType Container) -or
            ((Get-Item -LiteralPath $fixtureRootPath -Force).Attributes -band
                [IO.FileAttributes]::ReparsePoint)) {
            Stop-Build
        }
        if ($ExpectedFixtureManifestSha256 -cnotmatch '^[0-9a-f]{64}$' -or
            (Get-FileHash -LiteralPath (Join-Path $fixtureRootPath 'manifest.json') `
                -Algorithm SHA256).Hash.ToLowerInvariant() -cne
                $ExpectedFixtureManifestSha256) {
            Stop-Build
        }
        $fixtureOutput = & $windowsPowerShell -NoLogo -NoProfile -NonInteractive `
            -ExecutionPolicy Bypass `
            -File (Join-Path $PSScriptRoot 'Test-OpenVinoGenAiFixture.ps1') `
            -FixtureRoot $fixtureRootPath
        if ($LASTEXITCODE -ne 0 -or [string]$fixtureOutput -cne 'fixture_valid') {
            Stop-Build
        }
    }
    $fixturePackage = Join-Path $fixtureRootPath 'package'
    & $cmake -S (Join-Path $repositoryRoot 'workers\OpenVinoOfficial.Worker') -B $nativeBuild -G 'Visual Studio 17 2022' -A x64 "-DCMAKE_PREFIX_PATH=$runtime" "-DNLOHMANN_JSON_INCLUDE_DIR=$jsonInclude" "-DOFFICIAL_FIXTURE_PACKAGE=$fixturePackage" "-DOFFICIAL_WORKER_STAGE=$stageRoot"
    if ($LASTEXITCODE -ne 0) { Stop-Build }
    & $cmake --build $nativeBuild --config Release --parallel
    if ($LASTEXITCODE -ne 0) { Stop-Build }
    $releaseBin = Join-Path $runtime 'bin\intel64\Release'
    $copyMap = @{
        (Join-Path $nativeBuild 'Release\OpenVinoOfficial.Worker.exe') = 'OpenVinoOfficial.Worker.exe'
        (Join-Path $releaseBin 'openvino.dll') = 'openvino.dll'
        (Join-Path $releaseBin 'openvino_genai.dll') = 'openvino_genai.dll'
        (Join-Path $releaseBin 'openvino_tokenizers.dll') = 'openvino_tokenizers.dll'
        (Join-Path $releaseBin 'openvino_intel_cpu_plugin.dll') = 'openvino_intel_cpu_plugin.dll'
        (Join-Path $releaseBin 'openvino_intel_gpu_plugin.dll') = 'openvino_intel_gpu_plugin.dll'
        (Join-Path $releaseBin 'openvino_ir_frontend.dll') = 'openvino_ir_frontend.dll'
        (Join-Path $runtime '3rdparty\tbb\bin\tbb12.dll') = 'tbb12.dll'
        (Join-Path $runtime '3rdparty\tbb\bin\tbbbind_2_5.dll') = 'tbbbind_2_5.dll'
    }
    foreach ($source in $copyMap.Keys) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $stageRoot $copyMap[$source])
    }

    $licenses = Join-Path $stageRoot 'licenses'
    New-Item -ItemType Directory -Path $licenses | Out-Null
    $licenseMap = @{
        (Join-Path $distribution 'docs\licensing\Apache_license.txt') = 'Apache_license.txt'
        (Join-Path $distribution 'docs\licensing\LICENSE-GENAI') = 'LICENSE-GENAI.txt'
        (Join-Path $distribution 'docs\licensing\runtime-third-party-programs.txt') = 'runtime-third-party-programs.txt'
        (Join-Path $distribution 'docs\licensing\third-party-programs-genai.txt') = 'third-party-programs-genai.txt'
        (Join-Path $distribution 'docs\openvino_tokenizers\LICENSE') = 'openvino-tokenizers-LICENSE.txt'
        (Join-Path $distribution 'docs\openvino_tokenizers\third-party-programs.txt') = 'openvino-tokenizers-third-party-programs.txt'
        (Join-Path $runtime '3rdparty\tbb\TBB-LICENSE') = 'TBB-LICENSE.txt'
        (Join-Path $distribution 'samples\cpp\thirdparty\nlohmann_json\LICENSE.MIT') = 'nlohmann-json-LICENSE.MIT.txt'
    }
    foreach ($source in $licenseMap.Keys) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $licenses $licenseMap[$source])
    }
    $manifestOutput = & $windowsPowerShell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'New-OpenVinoOfficialWorkerManifest.ps1') -StageDirectory $stageRoot
    if ($LASTEXITCODE -ne 0 -or [string]$manifestOutput -cne 'worker_manifest_created') { Stop-Build }
    $verifyOutput = & $windowsPowerShell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-OpenVinoOfficialWorkerManifest.ps1') -StageDirectory $stageRoot
    if ($LASTEXITCODE -ne 0 -or [string]$verifyOutput -cne 'worker_manifest_valid') { Stop-Build }
    & (Join-Path (Split-Path $cmake) 'ctest.exe') --test-dir $nativeBuild -C Release --output-on-failure
    if ($LASTEXITCODE -ne 0) { Stop-Build }
    [Console]::Out.WriteLine('official_worker_built')
}
catch {
    Stop-Build
}
