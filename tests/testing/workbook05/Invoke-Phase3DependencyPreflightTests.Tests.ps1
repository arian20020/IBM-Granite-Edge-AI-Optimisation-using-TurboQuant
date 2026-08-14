[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$RepositoryRoot = '',

    [Parameter(Mandatory = $false)]
    [string]$PythonPath = 'python'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (
        Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\..') -ErrorAction Stop
    ).Path
}
else {
    $RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot -ErrorAction Stop).Path
}

$Script = Join-Path $RepositoryRoot 'scripts\testing\workbook05\Invoke-Workbook05Phase3DependencyPreflight.ps1'
if (-not (Test-Path -LiteralPath $Script -PathType Leaf)) {
    throw "Phase 3 dependency-preflight script is missing: $Script"
}

$ScriptText = Get-Content -LiteralPath $Script -Raw -ErrorAction Stop
foreach ($Required in @(
    'resolver'
    'install'
    'imports'
    'cli_help'
    'no_model_compatibility'
    'remote_code_disabled'
    'Python 3.12.10'
    'a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0'
    '982e495540364f95da1e4b6f62d2d4e5907d08fd'
    '--require-hashes'
    '--no-index'
    'manifest.sha256'
)) {
    if ($ScriptText.IndexOf($Required, [StringComparison]::Ordinal) -lt 0) {
        throw "Dependency-preflight script is missing required control: $Required"
    }
}
foreach ($Forbidden in @(
    '--trust-remote-code'
    'snapshot_download'
    'Invoke-Expression'
    'git reset --hard'
    'git clean'
)) {
    if ($ScriptText.IndexOf($Forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "Dependency-preflight script contains forbidden token: $Forbidden"
    }
}

$FixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("wb05-phase3-dependency-" + [guid]::NewGuid().ToString('N'))
$Success = Join-Path $FixtureRoot 'success'
$Blocked = Join-Path $FixtureRoot 'blocked'
try {
    & $Script `
        -RepositoryRoot $RepositoryRoot `
        -BootstrapPythonPath $PythonPath `
        -WorkspaceRoot $Success `
        -OfflineFixtureMode
    if ($LASTEXITCODE -ne 0) {
        throw "Offline dependency preflight exited with code $LASTEXITCODE."
    }

    foreach ($Relative in @(
        'dependency-preflight.json'
        'locks\requirements.phase3-assets.txt'
        'packages.csv'
        'sources\optimum.sha256'
        'sources\optimum-intel.sha256'
        'checks\resolver.txt'
        'checks\install.txt'
        'checks\imports.txt'
        'checks\cli_help.txt'
        'checks\no_model_compatibility.txt'
        'checks\remote_code_disabled.txt'
        'summary.md'
        'manifest.sha256'
    )) {
        if (-not (Test-Path -LiteralPath (Join-Path $Success $Relative) -PathType Leaf)) {
            throw "Successful dependency preflight is missing: $Relative"
        }
    }

    $Record = Get-Content -LiteralPath (Join-Path $Success 'dependency-preflight.json') -Raw | ConvertFrom-Json
    if ($Record.status -ne 'Passed') {
        throw "Offline dependency preflight did not pass: $($Record.status)"
    }
    if ($Record.model_download_authorised -ne $false) {
        throw 'Dependency preflight incorrectly authorised model download.'
    }
    if (@($Record.checks).Count -ne 6) {
        throw 'Dependency preflight did not record exactly six checks.'
    }
    if (@($Record.checks | Where-Object { $_.status -ne 'Passed' }).Count -ne 0) {
        throw 'Successful dependency preflight contains a non-passing check.'
    }

    $LockText = Get-Content -LiteralPath (Join-Path $Success 'locks\requirements.phase3-assets.txt') -Raw
    if ($LockText.IndexOf('--hash=sha256:', [StringComparison]::Ordinal) -lt 0) {
        throw 'Generated fixture lock does not contain SHA-256 hashes.'
    }
    if ($LockText.IndexOf('git+https://', [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw 'Final install lock contains a moving VCS install line instead of built wheel identities.'
    }

    $BlockedAsExpected = $false
    try {
        & $Script `
            -RepositoryRoot $RepositoryRoot `
            -BootstrapPythonPath $PythonPath `
            -WorkspaceRoot $Blocked `
            -OfflineFixtureMode `
            -FailCheck 'imports'
    }
    catch {
        $BlockedAsExpected = $true
    }
    if (-not $BlockedAsExpected) {
        throw 'Injected dependency check failure did not return nonzero.'
    }
    $BlockedRecordPath = Join-Path $Blocked 'dependency-preflight.json'
    if (-not (Test-Path -LiteralPath $BlockedRecordPath -PathType Leaf)) {
        throw 'Blocked dependency preflight did not retain its decision record.'
    }
    $BlockedRecord = Get-Content -LiteralPath $BlockedRecordPath -Raw | ConvertFrom-Json
    if ($BlockedRecord.status -ne 'Blocked') {
        throw "Injected dependency failure was not classified Blocked: $($BlockedRecord.status)"
    }
    if ($BlockedRecord.model_download_authorised -ne $false) {
        throw 'Blocked dependency preflight authorised model download.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $Blocked 'manifest.sha256') -PathType Leaf)) {
        throw 'Blocked dependency preflight did not retain a manifest.'
    }

    $global:LASTEXITCODE = 0
    Write-Host 'Workbook 05 Phase 3 dependency-preflight PowerShell tests passed.'
}
finally {
    if (Test-Path -LiteralPath $FixtureRoot) {
        Remove-Item -LiteralPath $FixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
