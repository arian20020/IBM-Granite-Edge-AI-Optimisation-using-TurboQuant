[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Matrix,
    [Parameter(Mandatory=$true)][string]$PromptSet,
    [Parameter(Mandatory=$true)][string]$CpuBuild,
    [Parameter(Mandatory=$true)][string]$SyclBuild,
    [Parameter(Mandatory=$true)][string]$DiagnosticModel,
    [Parameter(Mandatory=$true)][string]$Granite3Model,
    [Parameter(Mandatory=$true)][string]$Granite8Model,
    [Parameter(Mandatory=$true)][string]$OutputParent,
    [Parameter(Mandatory=$true)][ValidateSet('AH-06','AH-07','AH-10')][string[]]$Only
)

$ErrorActionPreference = 'Stop'
$arguments = @(
    'scripts/testing/run_animehacker_large_host.py',
    '--matrix', $Matrix, '--prompt-set', $PromptSet,
    '--cpu-build', $CpuBuild, '--sycl-build', $SyclBuild,
    '--diagnostic-model', $DiagnosticModel, '--granite3-model', $Granite3Model,
    '--granite8-model', $Granite8Model, '--output-parent', $OutputParent
)
foreach ($testId in $Only) { $arguments += @('--only', $testId) }
& python @arguments
if ($LASTEXITCODE -ne 0) { throw "Large-host completion failed with exit code $LASTEXITCODE" }
