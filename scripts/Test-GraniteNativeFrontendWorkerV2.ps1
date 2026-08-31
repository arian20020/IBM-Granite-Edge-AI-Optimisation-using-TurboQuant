[CmdletBinding()]
param(
    [switch] $StructureOnly,
    [string] $BaseRef
)

$script = Join-Path $PSScriptRoot 'frontend-worker/Test-GraniteNativeFrontendWorkerV2.ps1'
$arguments = @{}
if ($StructureOnly) { $arguments.StructureOnly = $true }
if (-not [string]::IsNullOrWhiteSpace($BaseRef)) { $arguments.BaseRef = $BaseRef }
& $script @arguments
exit $LASTEXITCODE
