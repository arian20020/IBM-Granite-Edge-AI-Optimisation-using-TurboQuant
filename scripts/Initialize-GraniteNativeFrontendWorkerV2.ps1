[CmdletBinding()]
param(
    [switch] $Install,
    [switch] $SkipOptionalProviders
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot 'frontend-worker/Initialize-GraniteNativeFrontendWorkerV2.ps1'

try {
    & $script -Install:$Install -SkipOptionalProviders:$SkipOptionalProviders
    if (-not $?) { exit 1 }
    exit 0
}
catch {
    Write-Error $_
    exit 1
}
