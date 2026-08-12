[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string]$Repository,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9]+$')]
    [string]$RunId,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$RunAttempt,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ArtifactName,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^sha256:[0-9a-f]{64}$')]
    [string]$ExpectedArtifactDigest,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^sha256:[0-9a-f]{64}$')]
    [string]$ActualArtifactDigest,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$ExpectedHeadSha,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$GitHubToken
)

<#
.SYNOPSIS
Verifies the immutable GitHub identity of the Runtime artifact used by the
controlled-resume workflow.

.DESCRIPTION
The prior artifact is treated as untrusted prerequisite data. This boundary
requires one exact, unexpired artifact whose GitHub-recorded digest and workflow
head match the project owner's separately recorded values. The read-only token
is used only in the HTTPS request header and is never written to evidence.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Two independently obtained digests must agree before GitHub metadata is read.
if ($ExpectedArtifactDigest -ne $ActualArtifactDigest) {
    throw (
        'The recorded and independently verified artifact digests do not match. ' +
        "Recorded: $ExpectedArtifactDigest; verified: $ActualArtifactDigest"
    )
}

# Query only the requested workflow run's artifact collection through the
# versioned GitHub Actions REST API.
$artifactApi = (
    "https://api.github.com/repos/$Repository/" +
    "actions/runs/$RunId/artifacts?per_page=100"
)
$headers = @{
    'Authorization' = "Bearer $GitHubToken"
    'Accept' = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
}
$response = Invoke-RestMethod `
    -Method Get `
    -Uri $artifactApi `
    -Headers $headers `
    -ErrorAction Stop

# Reject ambiguity rather than choosing one artifact from duplicate names.
$matches = @(
    $response.artifacts |
        Where-Object { [string]$_.name -ceq $ArtifactName }
)
if ($matches.Count -ne 1) {
    throw (
        "Expected exactly one artifact named '$ArtifactName' in run $RunId, " +
        "found $($matches.Count)."
    )
}

$artifact = $matches[0]
if ($artifact.expired -eq $true) {
    throw "The prerequisite artifact has expired: $ArtifactName"
}
if ([string]$artifact.digest -ne $ExpectedArtifactDigest) {
    throw (
        "GitHub recorded digest '$($artifact.digest)' but expected " +
        "'$ExpectedArtifactDigest'."
    )
}
if ([string]$artifact.workflow_run.head_sha -ne $ExpectedHeadSha) {
    throw (
        "The artifact workflow head '$($artifact.workflow_run.head_sha)' does " +
        "not match expected head '$ExpectedHeadSha'."
    )
}

# The API endpoint is already scoped to one run. Record the supplied attempt in
# the non-secret success marker so logs retain the prerequisite identity.
Write-Host 'WORKBOOK05_ARTIFACT_IDENTITY_ACCEPTED'
Write-Host "ArtifactName=$ArtifactName"
Write-Host "ArtifactId=$($artifact.id)"
Write-Host "RunId=$RunId"
Write-Host "RunAttempt=$RunAttempt"
Write-Host "HeadSha=$ExpectedHeadSha"
Write-Host "Digest=$ExpectedArtifactDigest"
