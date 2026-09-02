[CmdletBinding()]
param()

<#
.SYNOPSIS
Proves that a native command which exits before the first resource sample still
publishes the resource CSV promised by the command index contract.
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
    ('workbook05-fast-command-test-' + [Guid]::NewGuid().ToString('N'))
$evidenceDirectory = Join-Path $temporaryRoot 'commands'

try {
    New-Item `
        -ItemType Directory `
        -Path $evidenceDirectory `
        -Force:$false | Out-Null

    $commandPath = Join-Path $env:SystemRoot 'System32\cmd.exe'
    $zeroSampleResult = $null

    # A process exit races the first sampler check. Repeat a tiny native command
    # so the test exercises the real zero-sample outcome without mocking process
    # supervision or changing the production interface.
    for ($attempt = 1; $attempt -le 20; $attempt++) {
        $candidate = Invoke-Wb05ControlledLoggedProcess `
            -CommandId "fast-command-$attempt" `
            -RouteId 'route-a-merged-openvino' `
            -Component 'dependency-preflight' `
            -FilePath $commandPath `
            -ArgumentList @('/d', '/c', 'exit 0') `
            -WorkingDirectory $temporaryRoot `
            -EvidenceDirectory $evidenceDirectory `
            -EvidenceRoot $temporaryRoot `
            -MaximumElapsedSeconds 30 `
            -LogFileExtension 'txt' `
            -AtomicJsonEvidence

        if ($candidate.record.exit_code -ne 0) {
            throw "Fast native command returned exit code $($candidate.record.exit_code)."
        }
        if ($candidate.resource_summary.sample_count -eq 0) {
            $zeroSampleResult = $candidate
            break
        }
    }

    if ($null -eq $zeroSampleResult) {
        throw 'The test could not observe a zero-sample fast native command.'
    }
    if (
        -not (
            Test-Path `
                -LiteralPath $zeroSampleResult.resource_csv_path `
                -PathType Leaf
        )
    ) {
        throw (
            'A zero-sample command did not publish its indexed resource CSV: ' +
            $zeroSampleResult.resource_csv_path
        )
    }

    $resourceLines = @(
        Get-Content `
            -LiteralPath $zeroSampleResult.resource_csv_path `
            -Encoding UTF8 `
            -ErrorAction Stop
    )
    $expectedHeader = (
        '"timestamp_utc","root_process_id","process_tree_ids",' +
        '"working_set_bytes","private_bytes","available_memory_bytes",' +
        '"commit_percent","heartbeat_age_seconds"'
    )
    if (
        $resourceLines.Count -ne 1 -or
        $resourceLines[0] -ne $expectedHeader
    ) {
        throw (
            'A zero-sample resource CSV must contain exactly the reviewed ' +
            'header and no fabricated sample rows.'
        )
    }

    $global:LASTEXITCODE = 0
    Write-Host 'Workbook 05 fast controlled-process evidence tests passed.'
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
