[CmdletBinding()]
param(
    [switch] $Install,
    [switch] $SkipOptionalProviders
)

$script = Join-Path $PSScriptRoot 'frontend-worker/Initialize-GraniteNativeFrontendWorkerV2.ps1'
& $script -Install:$Install -SkipOptionalProviders:$SkipOptionalProviders
exit $LASTEXITCODE
