[CmdletBinding()]
param(
    [switch] $StructureOnly,
    [string] $BaseRef
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot 'frontend-worker/Test-GraniteNativeFrontendWorkerV2.ps1'
$arguments = @{}
if ($StructureOnly) { $arguments.StructureOnly = $true }
if (-not [string]::IsNullOrWhiteSpace($BaseRef)) { $arguments.BaseRef = $BaseRef }

try {
    & $script @arguments
    if (-not $?) { exit 1 }
    exit 0
}
catch {
    Write-Error $_
    exit 1
}
