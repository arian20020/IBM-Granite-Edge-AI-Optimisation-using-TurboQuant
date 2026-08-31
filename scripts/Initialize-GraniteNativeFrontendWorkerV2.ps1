[CmdletBinding()]
param(
    [switch] $Install,
    [switch] $SkipOptionalProviders
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot 'frontend-worker/Initialize-GraniteNativeFrontendWorkerV2.ps1'
$arguments = @('-NoProfile', '-File', $script)
if ($Install) { $arguments += '-Install' }
if ($SkipOptionalProviders) { $arguments += '-SkipOptionalProviders' }

& pwsh @arguments
exit $LASTEXITCODE
