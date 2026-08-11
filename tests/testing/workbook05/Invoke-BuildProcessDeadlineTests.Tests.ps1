[CmdletBinding()]
param()

<#
.SYNOPSIS
Proves that the resume-only Workbook 05 process adapter stops a native process
at its own controlled elapsed-time boundary and still writes complete evidence.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (
    Resolve-Path (Join-Path $PSScriptRoot '../../..')
).Path
$baseModulePath = Join-Path `
    $repositoryRoot `
    'scripts/testing/workbook05/Workbook05.Build.psm1'
$controlledModulePath = Join-Path `
    $repositoryRoot `
    'scripts/testing/workbook05/Workbook05.ControlledProcess.psm1'
Import-Module $baseModulePath -Force -ErrorAction Stop
Import-Module $controlledModulePath -Force -ErrorAction Stop

$temporaryRoot = Join-Path `
    ([IO.Path]::GetTempPath()) `
    ('workbook05-deadline-test-' + [Guid]::NewGuid().ToString('N'))
$evidenceDirectory = Join-Path $temporaryRoot 'commands'

try {
    New-Item `
        -ItemType Directory `
        -Path $evidenceDirectory `
        -Force:$false | Out-Null

    $powershellPath = Join-Path `
        $env:SystemRoot `
        'System32\WindowsPowerShell\v1.0\powershell.exe'

    $result = Invoke-Wb05ControlledLoggedProcess `
        -CommandId 'deadline-test' `
        -RouteId 'route-a-merged-openvino' `
        -Component 'runtime' `
        -FilePath $powershellPath `
        -ArgumentList @(
            '-NoLogo',
            '-NoProfile',
            '-Command',
            'Start-Sleep -Seconds 30'
        ) `
        -WorkingDirectory $temporaryRoot `
        -EvidenceDirectory $evidenceDirectory `
        -EvidenceRoot $temporaryRoot `
        -MaximumElapsedSeconds 3

    if ($null -eq $result.resource_summary) {
        throw 'The deadline test did not return a resource summary.'
    }
    if ($result.resource_summary.safety_stop_triggered -ne $true) {
        throw 'The controlled elapsed-time boundary did not trigger.'
    }
    if (
        [string]$result.resource_summary.safety_stop_reason -notmatch
        'controlled elapsed-time boundary'
    ) {
        throw (
            'The resource summary did not record the controlled ' +
            'elapsed-time reason.'
        )
    }
    if ($result.record.exit_code -eq 0) {
        throw 'A process stopped by the deadline must not report exit code zero.'
    }

    foreach ($path in @(
        $result.record_path,
        $result.stdout_path,
        $result.stderr_path,
        $result.resource_csv_path,
        $result.resource_summary_path
    )) {
        if ($path -and -not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Expected deadline evidence file is missing: $path"
        }
    }

    $global:LASTEXITCODE = 0
    Write-Host 'Workbook 05 controlled process deadline tests passed.'
}
finally {
    Remove-Module `
        'Workbook05.ControlledProcess' `
        -Force `
        -ErrorAction SilentlyContinue
    Remove-Module `
        'Workbook05.Build' `
        -Force `
        -ErrorAction SilentlyContinue

    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item `
            -LiteralPath $temporaryRoot `
            -Recurse `
            -Force `
            -ErrorAction SilentlyContinue
    }
}
