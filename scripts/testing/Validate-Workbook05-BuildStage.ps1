[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [string]$PythonPath = 'python'
)

<#
.SYNOPSIS
Runs the complete repository-side Workbook 05 documented-build gate.

.DESCRIPTION
This gate validates every Workbook 05 Python contract, imports the reusable
PowerShell build module, executes any repository PowerShell test scripts, reruns
the focused build workflow contract, and checks the Git diff. It performs no
external source build and cannot execute a model.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$pythonCommand = Get-Command `
    -Name $PythonPath `
    -CommandType Application `
    -All `
    -ErrorAction Stop |
    Select-Object -First 1
$resolvedPythonPath = $pythonCommand.Source

Push-Location $RepositoryRoot
try {
    # Run every Workbook 05 Python test so a later stage cannot bypass an
    # earlier provenance, schema, security, orchestration, or route-boundary test.
    & $resolvedPythonPath -m unittest discover -v `
        -s tests/testing/workbook05 `
        -p 'test_*.py'
    if ($LASTEXITCODE -ne 0) {
        throw "Workbook 05 Python discovery exited with code $LASTEXITCODE."
    }

    # Import the real Windows build module in a clean scope. This catches parser,
    # export, and module-initialisation defects that static text tests cannot see.
    $modulePath = Join-Path $RepositoryRoot 'scripts/testing/workbook05/Workbook05.Build.psm1'
    Import-Module $modulePath -Force -ErrorAction Stop
    $expectedFunctions = @(
        'New-Wb05ExternalWorkspace',
        'Invoke-Wb05LoggedProcess',
        'Start-Wb05ResourceSampler',
        'Stop-Wb05ResourceSampler',
        'Write-Wb05Json',
        'Write-Wb05Manifest',
        'Assert-Wb05SafePath',
        'Get-Wb05BinaryRecords',
        'Restore-Wb05Environment'
    )
    $missingFunctions = @(
        $expectedFunctions |
            Where-Object {
                -not (Get-Command -Name $_ -CommandType Function -ErrorAction SilentlyContinue)
            }
    )
    if ($missingFunctions.Count -ne 0) {
        throw "Workbook05.Build.psm1 is missing exports: $($missingFunctions -join ', ')"
    }
    Remove-Module 'Workbook05.Build' -Force -ErrorAction Stop

    # Execute repository PowerShell test scripts when present. Each script is an
    # explicit file boundary; no dynamically constructed command string is used.
    $powerShellTests = @(
        Get-ChildItem `
            -LiteralPath (Join-Path $RepositoryRoot 'tests/testing/workbook05') `
            -Filter '*.Tests.ps1' `
            -File `
            -Recurse `
            -ErrorAction Stop |
        Sort-Object FullName
    )
    foreach ($powerShellTest in $powerShellTests) {
        & $powerShellTest.FullName
        if ($LASTEXITCODE -ne 0) {
            throw "PowerShell test exited with code $LASTEXITCODE: $($powerShellTest.FullName)"
        }
    }

    # Rerun the workflow/security contract explicitly so the final PASS line can
    # be traced to the exact staged-workflow rules as well as full discovery.
    & $resolvedPythonPath -m unittest -v `
        tests.testing.workbook05.test_build_workflow_contract `
        tests.testing.workbook05.test_build_workflow_bundle_contract
    if ($LASTEXITCODE -ne 0) {
        throw "test_build_workflow_contract exited with code $LASTEXITCODE."
    }

    # Reject whitespace errors and conflict markers across the complete branch.
    & git diff --check
    if ($LASTEXITCODE -ne 0) {
        throw "git diff --check exited with code $LASTEXITCODE."
    }

    # Print the controlled success marker only after every required check passes.
    Write-Host 'WORKBOOK05_BUILD_STAGE_GATE_PASS'
}
finally {
    Pop-Location
}
