[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PythonExe,
    [Parameter(Mandatory=$true)][string]$OutputRoot,
    [Parameter(Mandatory=$true)][string]$ConditionsJson,
    [ValidateRange(1,3600)][int]$DurationSeconds=60
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'

function Resolve-SafePath {
    param([string]$Path,[string]$Label,[switch]$MayNotExist)
    if (-not [IO.Path]::IsPathFullyQualified($Path)) { throw "$Label must be absolute." }
    if ($MayNotExist) {
        $Parent=Split-Path -Parent $Path
        $ResolvedParent=(Resolve-Path -LiteralPath $Parent -ErrorAction Stop).Path
        return Join-Path $ResolvedParent (Split-Path -Leaf $Path)
    }
    return (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
}

$Python=Resolve-SafePath -Path $PythonExe -Label 'Python executable'
$Conditions=Resolve-SafePath -Path $ConditionsJson -Label 'conditions JSON'
$Output=Resolve-SafePath -Path $OutputRoot -Label 'output root' -MayNotExist
if (Test-Path -LiteralPath $Output) { throw 'Output root already exists.' }

& $Python -m scripts.testing.turbovec.machine_state `
    --output-root $Output `
    --conditions-json $Conditions `
    --duration-seconds $DurationSeconds
exit $LASTEXITCODE
