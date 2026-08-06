[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [string]$PythonPath = 'python'
)

<#
.SYNOPSIS
Runs the complete repository-side Workbook 05 documented-build gate.

.DESCRIPTION
The gate installs only the repository-pinned Python validation dependency into
runner temporary storage, executes every Workbook 05 test, imports the real
Windows build module, reruns the staged-workflow contracts, and checks the Git
diff. It performs no external source build and cannot execute a model.
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

# Keep third-party validation packages outside both the repository and the
# machine-wide Python installation. RUNNER_TEMP is job-specific in Actions;
# local executions fall back to the operating-system temporary directory.
$temporaryRoot = if ($env:RUNNER_TEMP) {
    $env:RUNNER_TEMP
}
else {
    [IO.Path]::GetTempPath()
}
$dependencyDirectory = Join-Path `
    $temporaryRoot `
    'workbook05-build-stage-python'
$requirementsPath = Join-Path `
    $RepositoryRoot `
    'scripts/testing/workbook05/requirements.txt'
if (-not (Test-Path -LiteralPath $requirementsPath -PathType Leaf)) {
    throw "Workbook 05 requirements file is missing: $requirementsPath"
}
if (Test-Path -LiteralPath $dependencyDirectory) {
    Remove-Item `
        -LiteralPath $dependencyDirectory `
        -Recurse `
        -Force
}
New-Item `
    -ItemType Directory `
    -Path $dependencyDirectory `
    -Force:$false | Out-Null

& $resolvedPythonPath `
    -m pip install `
    --disable-pip-version-check `
    --target $dependencyDirectory `
    -r $requirementsPath
if ($LASTEXITCODE -ne 0) {
    throw "Pinned Workbook 05 dependency installation exited with code $LASTEXITCODE."
}

# PYTHONPATH is process-scoped and restored in the final boundary. This lets
# both hosted and self-hosted jobs import jsonschema without persistent drift.
$originalPythonPath = [Environment]::GetEnvironmentVariable(
    'PYTHONPATH',
    'Process'
)
if ([string]::IsNullOrWhiteSpace($originalPythonPath)) {
    $env:PYTHONPATH = $dependencyDirectory
}
else {
    $env:PYTHONPATH = (
        $dependencyDirectory +
        [IO.Path]::PathSeparator +
        $originalPythonPath
    )
}

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
    $modulePath = Join-Path `
        $RepositoryRoot `
        'scripts/testing/workbook05/Workbook05.Build.psm1'
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
                -not (
                    Get-Command `
                        -Name $_ `
                        -CommandType Function `
                        -ErrorAction SilentlyContinue
                )
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
            -LiteralPath (
                Join-Path $RepositoryRoot 'tests/testing/workbook05'
            ) `
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
    # be traced to the staged-workflow rules as well as full discovery.
    & $resolvedPythonPath -m unittest -v `
        tests.testing.workbook05.test_build_workflow_contract `
        tests.testing.workbook05.test_build_workflow_bundle_contract `
        tests.testing.workbook05.test_build_gate_dependency_contract
    if ($LASTEXITCODE -ne 0) {
        throw "Documented-build workflow contracts exited with code $LASTEXITCODE."
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
    if ([string]::IsNullOrWhiteSpace($originalPythonPath)) {
        Remove-Item -Path 'Env:PYTHONPATH' -ErrorAction SilentlyContinue
    }
    else {
        $env:PYTHONPATH = $originalPythonPath
    }
}
