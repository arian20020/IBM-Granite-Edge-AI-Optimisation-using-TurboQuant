[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$RepositoryRoot,
    [Parameter(Mandatory = $true)] [string]$BundleDirectory,
    [Parameter(Mandatory = $true)]
    [ValidateSet('route-a-merged-openvino', 'route-b-experimental-qjl-polar')]
    [string]$RouteId,
    [Parameter(Mandatory = $true)] [ValidateSet('runtime', 'genai')] [string]$Component,
    [Parameter(Mandatory = $true)] [ValidatePattern('^[0-9a-f]{40}$')] [string]$SourceCommit,
    [Parameter(Mandatory = $true)] [string]$RunId,
    [Parameter(Mandatory = $true)] [int]$RunAttempt,
    [bool]$Br8Accepted = $false,
    [string]$Br8Status = '',
    [string]$Br8SourceCommit = '',
    [string]$Br8ArtifactDigest = ''
)

<#
.SYNOPSIS
Binds a Workbook 05 build evidence directory to one exact workflow attempt.

.DESCRIPTION
The helper writes bundle.json only after the build stage has produced its text
evidence, then regenerates manifest.sha256 so the independent hosted validator
can prove exact route, component, source, run, attempt, and conditional BR8
identity. It never copies or executes build outputs.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$BundleDirectory = (Resolve-Path -LiteralPath $BundleDirectory).Path
$modulePath = Join-Path $RepositoryRoot 'scripts/testing/workbook05/Workbook05.Build.psm1'
Import-Module $modulePath -Force -ErrorAction Stop

$metadata = [ordered]@{
    schema_version = '1.0'
    campaign_id = 'GTQ-WB05-MF-v1'
    route_id = $RouteId
    component = $Component
    source_commit = $SourceCommit
    run_id = $RunId
    run_attempt = $RunAttempt
}

# Route B is conditional on one accepted, independently validated BR8 artifact.
# Route A does not receive this property because its source/build boundary is
# independent of the experimental repair decision.
if ($RouteId -eq 'route-b-experimental-qjl-polar') {
    if (
        -not $Br8Accepted -or
        $Br8Status -ne 'ExecutableCandidate' -or
        $Br8SourceCommit -notmatch '^[0-9a-f]{40}$' -or
        $Br8ArtifactDigest -notmatch '^sha256:[0-9a-f]{64}$'
    ) {
        throw 'Route B bundle metadata requires an accepted, digest-bound BR8 ExecutableCandidate prerequisite.'
    }

    $metadata.br8_prerequisite = [ordered]@{
        accepted = $Br8Accepted
        status = $Br8Status
        source_commit = $Br8SourceCommit
        artifact_digest = $Br8ArtifactDigest
    }
}

Write-Wb05Json -Path (Join-Path $BundleDirectory 'bundle.json') -Value $metadata
Write-Wb05Manifest -EvidenceDirectory $BundleDirectory | Out-Null
