[CmdletBinding()]
param(
    [string]$CampaignDate = "2026-07-19",
    [string]$ExpectedCommit = "ede283a88e35465f0d680dabbf1f44080f8fc387",
    [string]$UpstreamPath,
    [string]$DestinationPath,
    [string]$EvidencePath,
    [string]$PythonCommand = "python"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path

if (-not $UpstreamPath) {
    $commonGitDir = (& git -C $repoRoot rev-parse --path-format=absolute --git-common-dir).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Unable to resolve the shared controlling repository" }
    $sharedRepoRoot = Split-Path -Parent $commonGitDir
    $UpstreamPath = Join-Path $sharedRepoRoot "external/official-openvino/$CampaignDate/openvino"
}
if (-not $DestinationPath) {
    if (-not $env:SystemDrive -or $env:SystemDrive -notmatch "^[A-Za-z]:$") {
        throw "SystemDrive is unavailable or malformed"
    }
    $physicalSystemRoot = "$($env:SystemDrive)\"
    $DestinationPath = Join-Path $physicalSystemRoot "ov-wb04/$CampaignDate/openvino-cpu-state-observer"
}
if (-not $EvidencePath) {
    $EvidencePath = Join-Path $repoRoot "external/official-openvino/$CampaignDate/openvino-cpu-state-observer.identity.json"
}

Push-Location $repoRoot
try {
    & $PythonCommand -m scripts.testing.campaigns.openvino.patch_identity `
        --family openvino-cpu-observer `
        --upstream $UpstreamPath `
        --destination $DestinationPath `
        --expected-commit $ExpectedCommit `
        --evidence $EvidencePath
    if ($LASTEXITCODE -ne 0) {
        throw "OpenVINO CPU observer patch workspace preparation failed ($LASTEXITCODE)"
    }
}
finally {
    Pop-Location
}
