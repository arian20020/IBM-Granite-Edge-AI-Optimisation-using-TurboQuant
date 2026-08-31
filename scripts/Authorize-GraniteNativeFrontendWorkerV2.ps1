[CmdletBinding(DefaultParameterSetName = 'Open')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Open')]
    [string]$AuthorizationPhrase,

    [Parameter(Mandatory, ParameterSetName = 'Open')]
    [ValidateNotNullOrEmpty()]
    [string[]]$Surface,

    [Parameter(Mandatory, ParameterSetName = 'Open')]
    [ValidateNotNullOrEmpty()]
    [string]$ApprovedBy,

    [Parameter(Mandatory, ParameterSetName = 'Open')]
    [ValidateNotNullOrEmpty()]
    [string]$ApprovedVisualSource,

    [Parameter(Mandatory, ParameterSetName = 'Open')]
    [ValidateNotNullOrEmpty()]
    [string]$AuthorizationRequest,

    [Parameter(Mandatory, ParameterSetName = 'Close')]
    [switch]$Close
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$requiredPhrase = 'AUTHORIZE GRANITE FRONTEND V2 IMPLEMENTATION'

$root = (& git rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($root)) {
    throw 'Run this script from inside the Granite Edge AI repository.'
}

Push-Location $root.Trim()
try {
    $path = '.frontend-worker/v2/authorization.json'
    if (-not (Test-Path $path -PathType Leaf)) {
        throw "Authorization record is missing: $path"
    }

    $record = Get-Content $path -Raw | ConvertFrom-Json

    if ($Close) {
        $record.phase = 'initialized'
        $record.implementation_authorized = $false
        $record.authorized_surfaces = @()
        $record.approved_by = $null
        $record.approved_at_utc = $null
        $record.approved_base_commit = $null
        $record.approved_visual_source = $null
        $record.authorization_request = $null
        $record.note = 'Implementation lock closed. A new exact user authorization is required for another campaign.'
    }
    else {
        if ($AuthorizationPhrase -cne $requiredPhrase) {
            throw "Authorization phrase must exactly equal: $requiredPhrase"
        }

        $cleanSurfaces = @($Surface | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Select-Object -Unique)
        if ($cleanSurfaces.Count -eq 0) {
            throw 'At least one bounded surface is required.'
        }

        $head = (& git rev-parse HEAD).Trim()
        if ([string]::IsNullOrWhiteSpace($head)) {
            throw 'Unable to resolve the current base commit.'
        }

        $record.phase = 'implementation-authorized'
        $record.implementation_authorized = $true
        $record.authorization_phrase = $requiredPhrase
        $record.authorized_surfaces = $cleanSurfaces
        $record.approved_by = $ApprovedBy.Trim()
        $record.approved_at_utc = [DateTime]::UtcNow.ToString('o')
        $record.approved_base_commit = $head
        $record.approved_visual_source = $ApprovedVisualSource.Trim()
        $record.authorization_request = $AuthorizationRequest.Trim()
        $record.note = 'Authorization is bounded to the recorded surfaces and base commit. Run the contract guardian before production edits.'
    }

    $json = $record | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText((Join-Path $root.Trim() $path), $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))

    if ($Close) {
        Write-Host 'Granite Native Frontend Worker v2 implementation lock: CLOSED'
    }
    else {
        Write-Host 'Granite Native Frontend Worker v2 implementation lock: OPEN'
        Write-Host "Authorized surface(s): $($cleanSurfaces -join ', ')"
        Write-Host 'Required next step: run the frontend contract guardian and create the allowed-file manifest before changing production UI.'
    }
}
finally {
    Pop-Location
}
