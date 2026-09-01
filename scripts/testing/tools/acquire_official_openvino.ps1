[CmdletBinding()]
param(
    [string]$CampaignDate = "2026-07-19",
    [string]$OpenVINOTag = "2026.2.1",
    [string]$GenAITag = "2026.2.1.0",
    [string]$PythonCommand = "py",
    [string[]]$PythonArguments = @("-3.11"),
    [switch]$SkipInstall
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$venvRoot = Join-Path $repoRoot ".venv-official-openvino-$OpenVINOTag"
$sourceRoot = Join-Path $repoRoot "external/official-openvino/$CampaignDate"
$evidenceRoot = Join-Path $repoRoot "experiments/raw-results/official-openvino/$CampaignDate"
$acquisitionRoot = Join-Path $evidenceRoot "acquisition"
$environmentRoot = Join-Path $evidenceRoot "environment"
$venvPython = Join-Path $venvRoot "Scripts/python.exe"

New-Item -ItemType Directory -Force -Path $sourceRoot, $acquisitionRoot, $environmentRoot | Out-Null

function Write-Utf8NoBom {
    param([string]$Path, [object]$Content)
    $text = if ($Content -is [array]) { $Content -join [Environment]::NewLine } else { [string]$Content }
    [System.IO.File]::WriteAllText($Path, $text + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}

function Invoke-Checked {
    param([string]$Program, [string[]]$Arguments, [string]$LogPath)
    # Windows PowerShell 5.1 wraps any native stderr line as a PowerShell error.
    # Git and pip use stderr for ordinary progress, so trust their exit code.
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = & $Program @Arguments 2>&1 | ForEach-Object { $_.ToString() }
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }
    Write-Utf8NoBom $LogPath $output
    if ($exitCode -ne 0) {
        throw "Command failed ($exitCode): $Program $($Arguments -join ' '). See $LogPath"
    }
    return $output
}

function Get-ReleaseCheckout {
    param([string]$Name, [string]$Remote, [string]$Tag)
    $path = Join-Path $sourceRoot $Name
    $cloneLog = Join-Path $acquisitionRoot "$Name-clone.log"
    if (-not (Test-Path (Join-Path $path ".git"))) {
        Invoke-Checked git @("-c", "core.longpaths=true", "clone", "--branch", $Tag, "--depth", "1", "--recurse-submodules", $Remote, $path) $cloneLog | Out-Null
        Invoke-Checked git @("-C", $path, "config", "core.longpaths", "true") (Join-Path $acquisitionRoot "$Name-longpaths.log") | Out-Null
    }
    else {
        Invoke-Checked git @("-C", $path, "config", "core.longpaths", "true") (Join-Path $acquisitionRoot "$Name-longpaths.log") | Out-Null
        if ([bool]((& git -c core.longpaths=true -C $path status --porcelain).Count)) {
            throw "$Name has a dirty checkout; acquisition will not overwrite it"
        }
        Invoke-Checked git @("-c", "core.longpaths=true", "-C", $path, "fetch", "--depth", "1", "origin", "tag", $Tag) $cloneLog | Out-Null
        Invoke-Checked git @("-c", "core.longpaths=true", "-C", $path, "checkout", "--detach", $Tag) (Join-Path $acquisitionRoot "$Name-checkout.log") | Out-Null
        Invoke-Checked git @("-c", "core.longpaths=true", "-C", $path, "submodule", "update", "--init", "--recursive") (Join-Path $acquisitionRoot "$Name-submodules.log") | Out-Null
    }

    $commit = (& git -c core.longpaths=true -C $path rev-parse HEAD).Trim()
    $resolvedTag = (& git -c core.longpaths=true -C $path describe --tags --exact-match).Trim()
    $resolvedRemote = (& git -c core.longpaths=true -C $path remote get-url origin).Trim()
    $dirty = [bool]((& git -c core.longpaths=true -C $path status --porcelain).Count)
    $submodules = @(& git -c core.longpaths=true -C $path submodule status --recursive)
    $tree = (& git -c core.longpaths=true -C $path rev-parse 'HEAD^{tree}').Trim()
    $archive = Join-Path $env:TEMP "$Name-$commit.tar"
    & git -c core.longpaths=true -C $path archive --format=tar --output=$archive HEAD
    if ($LASTEXITCODE -ne 0) { throw "Unable to archive $Name for hashing" }
    $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant()
    Remove-Item -LiteralPath $archive -Force

    $signatureLog = Join-Path $acquisitionRoot "$Name-tag-signature.log"
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $signatureOutput = & git -c core.longpaths=true -C $path tag -v $Tag 2>&1 | ForEach-Object { $_.ToString() }
        $signatureExit = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }
    Write-Utf8NoBom $signatureLog $signatureOutput

    $record = [ordered]@{
        name = $Name; tag = $resolvedTag; commit = $commit; remote = $resolvedRemote
        dirty = $dirty; submodules = $submodules; git_tree = $tree
        tree_sha256 = $sourceHash; tag_signature_exit_code = $signatureExit
        tag_signature_log = (Resolve-Path -Relative $signatureLog)
    }
    Write-Utf8NoBom (Join-Path $acquisitionRoot "$Name-checkout.json") ($record | ConvertTo-Json -Depth 8)
    if ($resolvedTag -ne $Tag) { throw "$Name expected tag $Tag, found $resolvedTag" }
    if ($resolvedRemote -ne $Remote) { throw "$Name expected remote $Remote, found $resolvedRemote" }
    if ($dirty) { throw "$Name has a dirty checkout" }
    return $record
}

if (-not (Test-Path $venvPython)) {
    & $PythonCommand @PythonArguments -m venv $venvRoot
    if ($LASTEXITCODE -ne 0) { throw "Unable to create $venvRoot" }
}

if (-not $SkipInstall) {
    Invoke-Checked $venvPython @("-m", "pip", "install", "--upgrade", "pip") (Join-Path $environmentRoot "pip-upgrade.log") | Out-Null
    Invoke-Checked $venvPython @("-m", "pip", "install", "openvino==$OpenVINOTag", "openvino-genai==$GenAITag", "optimum-intel", "nncf", "psutil", "pandas", "openpyxl") (Join-Path $environmentRoot "pip-install.log") | Out-Null
}

$openvino = Get-ReleaseCheckout "openvino" "https://github.com/openvinotoolkit/openvino.git" $OpenVINOTag
$genai = Get-ReleaseCheckout "openvino.genai" "https://github.com/openvinotoolkit/openvino.genai.git" $GenAITag

$probe = @'
import importlib.metadata as md
import json, os, platform, sys
import openvino as ov
packages = {name: md.version(name) for name in ("openvino", "openvino-genai", "optimum-intel", "nncf", "psutil", "pandas", "openpyxl")}
record = {
    "python_version": platform.python_version(), "python_executable": sys.executable,
    "packages": packages, "devices": list(ov.Core().available_devices),
    "installed_ram_bytes": __import__("psutil").virtual_memory().total,
    "os": platform.platform(), "machine": platform.machine(),
}
print(json.dumps(record, sort_keys=True))
'@
$escapedProbe = $probe.Replace('"', '\"')
$environment = (& $venvPython -c $escapedProbe | ConvertFrom-Json)
if ($LASTEXITCODE -ne 0) { throw "OpenVINO import/device probe failed" }
if ($environment.packages.openvino -ne $OpenVINOTag) { throw "Installed openvino version is not $OpenVINOTag" }
if ($environment.packages.'openvino-genai' -ne $GenAITag) { throw "Installed openvino-genai version is not $GenAITag" }
if (-not ($environment.devices -contains "CPU")) { throw "OpenVINO CPU device was not detected" }
if (-not ($environment.devices | Where-Object { $_ -like "GPU*" })) { throw "OpenVINO GPU device was not detected" }

Write-Utf8NoBom (Join-Path $environmentRoot "python-openvino.json") ($environment | ConvertTo-Json -Depth 8)
$previousPythonUtf8 = $env:PYTHONUTF8
try {
    $env:PYTHONUTF8 = "1"
    Invoke-Checked $venvPython @("-m", "pip", "freeze", "--all") (Join-Path $environmentRoot "pip-freeze.txt") | Out-Null
    Invoke-Checked $venvPython @("-m", "pip", "inspect") (Join-Path $environmentRoot "pip-inspect.json") | Out-Null
}
finally {
    $env:PYTHONUTF8 = $previousPythonUtf8
}

$compiler = Get-Command cl.exe -ErrorAction SilentlyContinue
$cmake = Get-Command cmake.exe -ErrorAction SilentlyContinue
$system = [ordered]@{
    campaign_date = $CampaignDate
    powershell_version = $PSVersionTable.PSVersion.ToString()
    cmake = if ($cmake) { (& $cmake.Source --version | Select-Object -First 1) } else { "not-found" }
    compiler = if ($compiler) { (& $compiler.Source 2>&1 | Select-Object -First 1).ToString() } else { "not-found" }
    computer_system = Get-CimInstance Win32_ComputerSystem | Select-Object Manufacturer, Model, TotalPhysicalMemory
    processors = @(Get-CimInstance Win32_Processor | Select-Object Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, DriverVersion)
    graphics = @(Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion, AdapterRAM)
    openvino = $openvino
    openvino_genai = $genai
}
Write-Utf8NoBom (Join-Path $environmentRoot "system.json") ($system | ConvertTo-Json -Depth 8)

Write-Host "Acquisition complete: $evidenceRoot"
Write-Host "Environment: $venvRoot"
Write-Host "Devices: $($environment.devices -join ', ')"
