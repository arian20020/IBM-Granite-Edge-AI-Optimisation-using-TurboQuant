[CmdletBinding()]
param(
    [switch] $StructureOnly,
    [string] $BaseRef
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot 'frontend-worker/Test-GraniteNativeFrontendWorkerV2.ps1'
$arguments = @('-NoProfile', '-File', $script)
if ($StructureOnly) { $arguments += '-StructureOnly' }
if (-not [string]::IsNullOrWhiteSpace($BaseRef)) {
    $arguments += @('-BaseRef', $BaseRef)
}

& pwsh @arguments
exit $LASTEXITCODE
