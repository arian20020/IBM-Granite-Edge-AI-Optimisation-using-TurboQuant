[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('ValidateLedger', 'ValidateClosure', 'WriteLedger')]
    [string] $Operation,

    [string] $RepositoryRoot,
    [string] $LedgerPath,
    [string] $InputSummaryPath,
    [string] $OutputLedgerPath,
    [string] $SubjectTree,
    [string] $ManifestPath,
    [string] $ManagedLedgerPath,
    [string] $NativeLedgerPath,
    [string] $ReportPath,
    [string] $HandoffPath,
    [string] $NativeReceiptPath,
    [string] $ExpectedBase,
    [string] $ExpectedSubjectCommit,
    [string] $ExpectedSubjectTree,
    [string] $ExpectedFinalTip,
    [string] $ExpectedFinalTree,
    [string] $RemoteRef,
    [switch] $AllowDirty
)

$ErrorActionPreference = 'Stop'
$python = Get-Command python.exe -ErrorAction SilentlyContinue
if ($null -eq $python) {
    Write-Output 'H1R3-PYTHON-MISSING'
    exit 2
}

$scriptPath = Join-Path $PSScriptRoot 'h1_r3_closure.py'
if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
    Write-Output 'H1R3-VALIDATOR-MISSING'
    exit 2
}

$arguments = @($scriptPath)
switch ($Operation) {
    'ValidateLedger' {
        $arguments += @(
            'validate-ledger', '--repository', $RepositoryRoot,
            '--ledger', $LedgerPath,
            '--expected-subject-commit', $ExpectedSubjectCommit,
            '--expected-subject-tree', $ExpectedSubjectTree
        )
    }
    'WriteLedger' {
        $arguments += @(
            'write-ledger', '--input-summary', $InputSummaryPath,
            '--output-ledger', $OutputLedgerPath,
            '--subject-tree', $SubjectTree
        )
    }
    'ValidateClosure' {
        $arguments += @(
            'validate-closure', '--repository', $RepositoryRoot,
            '--manifest', $ManifestPath,
            '--expected-base', $ExpectedBase,
            '--expected-subject-commit', $ExpectedSubjectCommit,
            '--expected-subject-tree', $ExpectedSubjectTree,
            '--expected-final-tip', $ExpectedFinalTip,
            '--expected-final-tree', $ExpectedFinalTree
        )
        foreach ($optional in @(
                @('--managed-ledger', $ManagedLedgerPath),
                @('--native-ledger', $NativeLedgerPath),
                @('--report', $ReportPath),
                @('--handoff', $HandoffPath),
                @('--native-receipt', $NativeReceiptPath),
                @('--remote-ref', $RemoteRef)
            )) {
            if (-not [string]::IsNullOrWhiteSpace($optional[1])) {
                $arguments += $optional
            }
        }
        if ($AllowDirty) { $arguments += '--allow-dirty' }
    }
}

$output = & $python.Source @arguments 2>&1
$exitCode = $LASTEXITCODE
if ($output) { Write-Output $output }
exit $exitCode
