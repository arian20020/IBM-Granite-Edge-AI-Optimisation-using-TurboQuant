<#
.SYNOPSIS
Orchestrates one read-only Workbook 05 preflight evidence capture.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RepositoryRoot,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$CampaignId = 'GTQ-WB05-MF-v1'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Set-Location -LiteralPath $RepositoryRoot
Import-Module (Join-Path $RepositoryRoot 'scripts/testing/workbook05/Workbook05.Preflight.psm1') -Force

# Recreate a clean runner-owned evidence directory without touching the repository.
if (Test-Path -LiteralPath $OutputDirectory) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$controlsDirectory = Join-Path $OutputDirectory 'controls'
New-Item -ItemType Directory -Path $controlsDirectory -Force | Out-Null

$campaignRoot = Join-Path $RepositoryRoot "experiments/granite_turboquant_intel/manifests/campaigns/$CampaignId"
foreach ($fileName in @('campaign-manifest.json', 'checkpoint.json', 'route-a-source-admission.json', 'route-b-source-admission.json')) {
    Copy-Item -LiteralPath (Join-Path $campaignRoot $fileName) -Destination (Join-Path $controlsDirectory $fileName)
}

$settingsPath = Join-Path $RepositoryRoot 'experiments/granite_turboquant_intel/configurations/workbook05/preflight-settings.json'
$settings = Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json
$observation = Get-Workbook05PreflightObservation -RepositoryRoot $RepositoryRoot -OutputDirectory $OutputDirectory
$evaluation = Test-Workbook05PreflightObservation -Observation $observation -Settings $settings

# Write the machine evidence before attempting external documentation capture.
Export-Workbook05PreflightEvidence -Observation $observation -Evaluation $evaluation -OutputDirectory $OutputDirectory

try {
    & 'C:\Program Files\Python312\python.exe' `
        -m scripts.testing.workbook05.capture_documented_commands `
        --config (Join-Path $RepositoryRoot 'experiments/granite_turboquant_intel/configurations/workbook05/pinned-document-sources.json') `
        --output-directory $OutputDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Document capture exited with code $LASTEXITCODE."
    }
}
catch {
    # Preserve the capture problem and convert the final preflight state to Failed.
    $_ | Out-String | Set-Content -LiteralPath (Join-Path $OutputDirectory 'document-capture-error.txt') -Encoding UTF8
    $evaluation.Checks += [pscustomobject][ordered]@{
        Name = 'Pinned document capture'
        Required = $true
        Passed = $false
        Expected = 'All allowlisted pinned documents captured'
        Actual = $_.Exception.Message
    }
    $evaluation.OverallStatus = 'Failed'
    Export-Workbook05PreflightEvidence -Observation $observation -Evaluation $evaluation -OutputDirectory $OutputDirectory
}

# Update only the runtime checkpoint copy and preserve the report hash as evidence.
$reportHash = (Get-FileHash -LiteralPath (Join-Path $OutputDirectory 'preflight-report.json') -Algorithm SHA256).Hash.ToLowerInvariant()
$checkpointStatus = if ($evaluation.OverallStatus -eq 'Passed') { 'Passed' } else { 'Failed' }
& 'C:\Program Files\Python312\python.exe' `
    -m scripts.testing.workbook05.checkpoint `
    --path (Join-Path $controlsDirectory 'checkpoint.json') `
    --expected-generation 0 `
    --step-id 'phase-0-preflight' `
    --status $checkpointStatus `
    --evidence-sha256 $reportHash
if ($LASTEXITCODE -ne 0) {
    throw "Runtime checkpoint update exited with code $LASTEXITCODE."
}

if ($evaluation.OverallStatus -ne 'Passed') {
    exit 1
}
