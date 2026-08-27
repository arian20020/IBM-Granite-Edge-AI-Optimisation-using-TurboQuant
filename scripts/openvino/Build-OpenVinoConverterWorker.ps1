[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ClosureDirectory,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$BuildDirectory,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$StageDirectory
)

$ErrorActionPreference = 'Stop'

function Stop-Build {
    [Console]::Out.WriteLine('converter_build_failed')
    exit 1
}

function Test-PathOverlap {
    param(
        [Parameter(Mandatory)][string]$Left,
        [Parameter(Mandatory)][string]$Right
    )
    if ($Left -ieq $Right) { return $true }
    $separator = [IO.Path]::DirectorySeparatorChar
    $leftPrefix = if ($Left.EndsWith([string]$separator)) { $Left } else { $Left + $separator }
    $rightPrefix = if ($Right.EndsWith([string]$separator)) { $Right } else { $Right + $separator }
    return $Right.StartsWith($leftPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        $Left.StartsWith($rightPrefix, [StringComparison]::OrdinalIgnoreCase)
}

try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
    $closureRoot = [IO.Path]::GetFullPath($ClosureDirectory)
    $buildRoot = [IO.Path]::GetFullPath($BuildDirectory)
    $stageRoot = [IO.Path]::GetFullPath($StageDirectory)
    $roots = @($closureRoot, $buildRoot, $stageRoot)
    for ($left = 0; $left -lt $roots.Count; $left++) {
        for ($right = $left + 1; $right -lt $roots.Count; $right++) {
            if (Test-PathOverlap -Left $roots[$left] -Right $roots[$right]) { Stop-Build }
        }
    }
    foreach ($owned in @($buildRoot, $stageRoot)) {
        if (Test-Path -LiteralPath $owned) { Stop-Build }
        if ($owned -ieq $repositoryRoot -or
            $owned.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            Stop-Build
        }
    }

    $manifestPath = Join-Path $repositoryRoot 'third-party\openvino-converter\wheel-manifest.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.closureStatus -cne 'resolved' -or @($manifest.wheels).Count -eq 0) {
        Stop-Build
    }
    $windowsPowerShell = (Get-Command powershell.exe -ErrorAction Stop).Source
    $lockResult = & $windowsPowerShell -NoLogo -NoProfile -NonInteractive `
        -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-OpenVinoDependencyLocks.ps1') `
        -ClosureDirectory $closureRoot -Scope Converter
    if ($LASTEXITCODE -ne 0 -or [string]$lockResult -cne 'dependency_lock_valid') {
        Stop-Build
    }

    New-Item -ItemType Directory -Path $buildRoot | Out-Null
    New-Item -ItemType Directory -Path $stageRoot | Out-Null
    $runtimeArchive = Join-Path $closureRoot 'python-3.13.15-embed-amd64.zip'
    [IO.Compression.ZipFile]::ExtractToDirectory($runtimeArchive, $stageRoot)
    $packages = Join-Path $stageRoot 'packages'
    New-Item -ItemType Directory -Path $packages | Out-Null
    foreach ($wheel in @($manifest.wheels)) {
        $wheelPath = Join-Path $closureRoot ([string]$wheel.filename)
        [IO.Compression.ZipFile]::ExtractToDirectory($wheelPath, $packages)
    }
    $converterSource = Join-Path $repositoryRoot 'workers\OpenVinoConverter.Worker\converter'
    $converterStage = Join-Path $stageRoot 'converter'
    New-Item -ItemType Directory -Path $converterStage | Out-Null
    Get-ChildItem -LiteralPath $converterSource -File -Filter '*.py' | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $converterStage
    }

    $pathFile = Join-Path $stageRoot 'python313._pth'
    @('python313.zip', '.', 'packages') | Set-Content -LiteralPath $pathFile -Encoding ascii
    $runtimeIdentity = (Get-FileHash -LiteralPath $runtimeArchive -Algorithm SHA256).Hash.ToLowerInvariant()
    $requirementsLockPath = Join-Path $repositoryRoot 'third-party\openvino-converter\requirements.lock'
    $wheelManifestIdentity = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $requirementsLockIdentity = (Get-FileHash -LiteralPath $requirementsLockPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $stageManifest = [ordered]@{
        schemaVersion = 1
        protocol = 'granite.openvino.converter'
        protocolVersion = 1
        pythonSha256 = $runtimeIdentity
        wheelManifestSha256 = $wheelManifestIdentity
        requirementsLockSha256 = $requirementsLockIdentity
        wheelLockStatus = [string]$manifest.closureStatus
        maximumOperationMinutes = 120
        launchArguments = @('-I', '-s', '-E', '-S', '-B', '-m', 'converter')
    }
    $stageManifestJson = $stageManifest | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText(
        (Join-Path $stageRoot 'converter-manifest.json'),
        $stageManifestJson,
        [Text.UTF8Encoding]::new($false))

    $forbidden = @(
        'HF_TOKEN', 'HUGGING_FACE_HUB_TOKEN', 'HTTP_PROXY', 'HTTPS_PROXY',
        'ALL_PROXY', 'NO_PROXY', 'PIP_CONFIG_FILE', 'PYTHONHOME', 'PYTHONPATH', 'VIRTUAL_ENV'
    )
    foreach ($name in $forbidden) {
        Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
    }
    $scratchRoot = Join-Path $buildRoot 'scratch'
    $scratchLocal = Join-Path $scratchRoot 'local'
    $scratchRoaming = Join-Path $scratchRoot 'roaming'
    $scratchTemp = Join-Path $scratchRoot 'temp'
    $scratchHf = Join-Path $scratchRoot 'hf'
    $scratchXdg = Join-Path $scratchRoot 'xdg'
    $scratchTorch = Join-Path $scratchRoot 'torch'
    foreach ($directory in @(
        $scratchRoot, $scratchLocal, $scratchRoaming, $scratchTemp,
        $scratchHf, $scratchXdg, $scratchTorch)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    $telemetryDirectory = Join-Path $scratchLocal 'Intel Corporation'
    New-Item -ItemType Directory -Path $telemetryDirectory | Out-Null
    [IO.File]::WriteAllText(
        (Join-Path $telemetryDirectory 'openvino_telemetry'),
        '0',
        [Text.UTF8Encoding]::new($false))
    $env:HF_HUB_OFFLINE = '1'
    $env:TRANSFORMERS_OFFLINE = '1'
    $env:GRANITE_CONVERTER_ROOT = $stageRoot
    $env:GRANITE_CONVERTER_SCRATCH = $scratchRoot
    $env:HOME = $scratchRoot
    $env:USERPROFILE = $scratchRoot
    $env:APPDATA = $scratchRoaming
    $env:LOCALAPPDATA = $scratchLocal
    $env:HF_HOME = $scratchHf
    $env:XDG_CACHE_HOME = $scratchXdg
    $env:TORCH_HOME = $scratchTorch
    $env:TEMP = $scratchTemp
    $env:TMP = $scratchTemp
    $python = Join-Path $stageRoot 'python.exe'
    $isolationProbe = @'
import importlib
import pathlib
import socket
from converter.__main__ import _validate_runtime, install_network_guard
root = _validate_runtime()
modules = [importlib.import_module(name) for name in (
    'optimum', 'transformers', 'openvino', 'openvino_genai', 'nncf')]
paths = [path for module in modules for path in (
    [module.__file__] if getattr(module, '__file__', None) else list(module.__path__))]
if not all(pathlib.Path(path).resolve().is_relative_to(root) for path in paths):
    raise RuntimeError('runtime_integrity_failed')
install_network_guard()
try:
    socket.socket()
except PermissionError:
    print('converter_isolation_valid')
else:
    raise RuntimeError('runtime_integrity_failed')
'@
    $probe = & $python '-I' '-s' '-E' '-S' '-B' '-c' $isolationProbe 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]$probe -cne 'converter_isolation_valid') {
        Stop-Build
    }
    $arguments = @('-I', '-s', '-E', '-S', '-B', '-m', 'converter')
    $smoke = & $python @arguments 2>$null
    if ($smoke.Count -lt 1 -or ($smoke[0] | ConvertFrom-Json).type -cne 'hello') {
        Stop-Build
    }
    if (@(Get-ChildItem -LiteralPath $stageRoot -Directory -Recurse -Filter '__pycache__').Count -ne 0 -or
        @(Get-ChildItem -LiteralPath $stageRoot -File -Recurse -Filter '*.pyc').Count -ne 0) {
        Stop-Build
    }

    $stageFiles = @(
        Get-ChildItem -LiteralPath $stageRoot -File -Recurse |
            Where-Object { $_.FullName -cne (Join-Path $stageRoot 'converter-manifest.json') } |
            Sort-Object FullName |
            ForEach-Object {
                [ordered]@{
                    path = $_.FullName.Substring($stageRoot.Length + 1).Replace('\', '/')
                    length = $_.Length
                    sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                }
            }
    )
    $stageManifest['files'] = $stageFiles
    $stageManifestJson = $stageManifest | ConvertTo-Json -Depth 6
    [IO.File]::WriteAllText(
        (Join-Path $stageRoot 'converter-manifest.json'),
        $stageManifestJson,
        [Text.UTF8Encoding]::new($false))

    Get-ChildItem -LiteralPath $stageRoot -File -Recurse | ForEach-Object {
        $_.IsReadOnly = $true
    }
    [Console]::Out.WriteLine('converter_worker_built')
}
catch {
    Stop-Build
}
