[CmdletBinding()]
param(
    [string]$CampaignDate = "2026-07-19",
    [string]$ExpectedCommit = "7dea0459b2ac7d8dfd877fd9df6737674fd8371d",
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
    $UpstreamPath = Join-Path $sharedRepoRoot "external/official-openvino/$CampaignDate/openvino.genai"
}
if (-not $DestinationPath) {
    $DestinationPath = Join-Path $repoRoot "external/official-openvino/$CampaignDate/openvino.genai-turboquant"
}
if (-not $EvidencePath) {
    $EvidencePath = Join-Path (Split-Path -Parent $DestinationPath) "openvino.genai-turboquant.identity.json"
}

& $PythonCommand -m scripts.testing.official_openvino.patch_identity `
    --upstream $UpstreamPath `
    --destination $DestinationPath `
    --expected-commit $ExpectedCommit `
    --evidence $EvidencePath
if ($LASTEXITCODE -ne 0) {
    throw "OpenVINO TurboQuant patch workspace preparation failed ($LASTEXITCODE)"
}
