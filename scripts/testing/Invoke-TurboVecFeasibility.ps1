[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateSet('Preflight','Measured')][string]$Mode,
    [Parameter(Mandatory=$true)][string]$PythonExe,
    [Parameter(Mandatory=$true)][string]$ModelRoot,
    [Parameter(Mandatory=$true)][string]$TurboVecWheel,
    [Parameter(Mandatory=$true)][string]$OutputRoot,
    [Parameter(Mandatory=$true)][string]$RunId
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-SafeAbsolutePath {
    param([string]$Path,[string]$Label,[switch]$Directory)
    if (-not [IO.Path]::IsPathFullyQualified($Path)) { throw "$Label must be absolute." }
    $Resolved=(Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
    $Item=Get-Item -LiteralPath $Resolved -Force
    if (($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "$Label cannot be a reparse point." }
    if ($Directory -and -not $Item.PSIsContainer) { throw "$Label must be a directory." }
    if (-not $Directory -and $Item.PSIsContainer) { throw "$Label must be a file." }
    return $Resolved
}

if ($RunId -notmatch '^EXP-TV-COMP-001-\d{8}T\d{6}Z-\d{3}$') { throw 'Invalid run ID.' }
$Python=Resolve-SafeAbsolutePath -Path $PythonExe -Label 'Python executable'
$Model=Resolve-SafeAbsolutePath -Path $ModelRoot -Label 'Model root' -Directory
$Wheel=Resolve-SafeAbsolutePath -Path $TurboVecWheel -Label 'TurboVec wheel'
$EvidenceRoot=Resolve-SafeAbsolutePath -Path $OutputRoot -Label 'Output root' -Directory
if ((Get-Item -LiteralPath $Wheel).Length -ne 611788) { throw 'TurboVec wheel byte count mismatch.' }
if ((Get-FileHash -LiteralPath $Wheel -Algorithm SHA256).Hash -ne 'CD855E0B318A57DC57C733F9A62AE98DE5192F4F6C2C760E305523E8CEB1B090') { throw 'TurboVec wheel hash mismatch.' }
if (Test-Path -LiteralPath (Join-Path $EvidenceRoot $RunId)) { throw 'Run directory already exists.' }
$Status=& git status --porcelain=v1
if ($LASTEXITCODE -ne 0 -or $Status) { throw 'Source worktree must be clean.' }
$Manifest=Get-Content -LiteralPath 'experiments/manifests/turbovec/feasibility-v1.json' -Raw | ConvertFrom-Json
if ($Mode -eq 'Measured' -and $Manifest.dependencies.embedding.status -ne 'locked') { throw 'Measured execution requires a locked immutable model revision.' }
$Arguments=@('scripts/testing/run_turbovec_feasibility.py','preflight','--model-root',$Model)
& $Python @Arguments
if ($LASTEXITCODE -ne 0) { throw 'TurboVec preflight failed.' }
Write-Host "TurboVec $Mode preflight passed for $RunId."
