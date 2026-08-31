[CmdletBinding(DefaultParameterSetName = 'Open')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Open')]
    [string] $AuthorizationPhrase,

    [Parameter(Mandatory, ParameterSetName = 'Open')]
    [ValidatePattern('^[a-z0-9][a-z0-9-]{2,63}$')]
    [string] $CampaignId,

    [Parameter(Mandatory, ParameterSetName = 'Open')]
    [ValidateNotNullOrEmpty()]
    [string[]] $Surface,

    [Parameter(Mandatory, ParameterSetName = 'Open')]
    [ValidateNotNullOrEmpty()]
    [string] $ApprovedBaseRevision,

    [Parameter(Mandatory, ParameterSetName = 'Open')]
    [ValidateNotNullOrEmpty()]
    [string] $ApprovedVisualSource,

    [Parameter(Mandatory, ParameterSetName = 'Close')]
    [switch] $Close
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$requiredPhrase = 'AUTHORIZE GRANITE FRONTEND V2 IMPLEMENTATION'

function Invoke-Git([string[]] $Arguments) {
    $rawOutput = & git @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $output = $rawOutput | Out-String
    if ($exitCode -ne 0) {
        throw "git $($Arguments -join ' ') failed:`n$output"
    }
    return $output.Trim()
}

$root = Invoke-Git @('rev-parse', '--show-toplevel')
Push-Location $root
try {
    $policyPath = '.frontend-worker/v2/implementation-lock.yml'
    $templatePath = '.frontend-worker/v2/authorization.template.json'
    if (-not (Test-Path -LiteralPath $policyPath -PathType Leaf)) {
        throw "Missing implementation policy: $policyPath"
    }
    if (-not (Test-Path -LiteralPath $templatePath -PathType Leaf)) {
        throw "Missing authorization template: $templatePath"
    }

    $policyText = Get-Content -LiteralPath $policyPath -Raw
    $stateMatch = [regex]::Match($policyText, '(?m)^state_file:\s*(.+?)\s*$')
    if (-not $stateMatch.Success) {
        throw 'implementation-lock.yml does not declare state_file.'
    }
    $stateRelative = $stateMatch.Groups[1].Value.Trim().Trim('"').Trim("'")
    $statePath = Join-Path $root $stateRelative

    if ($Close) {
        if (Test-Path -LiteralPath $statePath) {
            Remove-Item -LiteralPath $statePath -Force
        }
        Write-Host 'Granite Native Frontend Worker v2 implementation state: CLOSED'
        return
    }

    if ($AuthorizationPhrase -cne $requiredPhrase) {
        throw "Authorization phrase must exactly equal: $requiredPhrase"
    }

    $cleanSurfaces = @(
        $Surface |
            ForEach-Object { $_.Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -Unique
    )
    if ($cleanSurfaces.Count -eq 0) {
        throw 'At least one bounded surface is required.'
    }

    $trackedStatus = Invoke-Git @('status', '--porcelain=v1', '--untracked-files=no')
    if (-not [string]::IsNullOrWhiteSpace($trackedStatus)) {
        throw 'Open authorization only from a clean tracked working tree. Commit, stash, or revert tracked changes first.'
    }

    $head = Invoke-Git @('rev-parse', 'HEAD')
    $approvedCommit = Invoke-Git @('rev-parse', '--verify', "$ApprovedBaseRevision^{commit}")
    if ($approvedCommit -cne $head) {
        throw "Approved base revision resolves to $approvedCommit but current HEAD is $head. Rebase/check out the approved revision before authorization."
    }

    $template = Get-Content -LiteralPath $templatePath -Raw | ConvertFrom-Json
    $template.phase = 'implementation-authorized'
    $template.implementationAuthorized = $true
    $template.authorizationPhrase = $requiredPhrase
    $template.campaignId = $CampaignId
    $template.authorizedSurfaces = $cleanSurfaces
    $template.approvedBaseRevision = $ApprovedBaseRevision.Trim()
    $template.approvedBaseCommit = $head
    $template.approvedVisualSource = $ApprovedVisualSource.Trim()
    $template.authorizedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $template.authorizationSource = 'active-human-conversation'

    $stateDirectory = Split-Path -Parent $statePath
    New-Item -ItemType Directory -Force -Path $stateDirectory | Out-Null
    $json = $template | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText(
        $statePath,
        $json + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))

    Write-Host 'Granite Native Frontend Worker v2 implementation state: OPEN'
    Write-Host "Campaign: $CampaignId"
    Write-Host "Base commit: $head"
    Write-Host "Authorized surface(s): $($cleanSurfaces -join ', ')"
    Write-Host 'Next required gate: frontend contract guardian baseline and exact allowed-file manifest.'
}
finally {
    Pop-Location
}
