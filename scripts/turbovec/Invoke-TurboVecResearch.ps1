[CmdletBinding()]
param(
    [Parameter()]
    [string] $PythonPath,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $RemainingArgs
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($PythonPath)) {
    $PythonPath = [Environment]::GetEnvironmentVariable('GRANITE_TURBOVEC_PYTHON')
}

if ([string]::IsNullOrWhiteSpace($PythonPath) -or
    -not (Test-Path -LiteralPath $PythonPath -PathType Leaf)) {
    [Console]::Error.WriteLine('TurboVec Python interpreter is not configured or does not exist.')
    exit 2
}

$resolvedPython = (Resolve-Path -LiteralPath $PythonPath).Path
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$packageRoot = (Resolve-Path -LiteralPath (Join-Path $repoRoot 'scripts\turbovec')).Path

$env:HF_HUB_OFFLINE = '1'
$env:TRANSFORMERS_OFFLINE = '1'
$env:HF_HUB_DISABLE_TELEMETRY = '1'

if ([string]::IsNullOrEmpty($env:PYTHONPATH)) {
    $env:PYTHONPATH = $packageRoot
}
else {
    $env:PYTHONPATH = $packageRoot + [IO.Path]::PathSeparator + $env:PYTHONPATH
}

$pythonArguments = @('-m', 'granite_turbovec.cli') + @($RemainingArgs)
& $resolvedPython @pythonArguments
exit $LASTEXITCODE
