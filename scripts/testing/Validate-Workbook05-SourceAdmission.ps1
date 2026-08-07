<#
.SYNOPSIS
Runs every repository-controlled Workbook 05 Phase 1 source-admission gate.

.DESCRIPTION
This command validates the complete Phase 1 repository control surface before
source evidence is collected or accepted. It runs tests, structural validators,
controlled schemas and workflow contracts, then checks the Git diff for whitespace
errors. It does not build OpenVINO, download a model, or run inference.
#>

[CmdletBinding()]
param(
    # The Intel runner supplies the pinned absolute interpreter. The hosted
    # validator supplies the Python command installed by actions/setup-python.
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$PythonPath = 'C:\Program Files\Python312\python.exe'
)

# Stop on the first PowerShell error so a partial gate cannot report success.
$ErrorActionPreference = 'Stop'

# Reject misspelled or uninitialised variables in this control script.
Set-StrictMode -Version Latest

# Resolve the repository root from Git and check the native exit code before
# another command can overwrite it.
$RepositoryRoot = (& git rev-parse --show-toplevel 2>$null).Trim()
$RepositoryRootExitCode = $LASTEXITCODE
if ($RepositoryRootExitCode -ne 0 -or -not $RepositoryRoot) {
    throw 'Run this script from inside the Git repository.'
}

# Use repository-relative paths consistently on the Intel and hosted runners.
Set-Location -LiteralPath $RepositoryRoot

# Resolve either the approved absolute interpreter or the hosted `python`
# command without searching for or executing a user-selected program. Select
# only the first application in PowerShell command-precedence order because a
# hosted PATH may expose both setup-python and WindowsApps shims.
$PythonCommand = Get-Command `
    -Name $PythonPath `
    -CommandType Application `
    -ErrorAction Stop |
    Select-Object -First 1
$ResolvedPythonPath = $PythonCommand.Source

# Verify the exact approved interpreter version before running any test code.
$PythonVersion = ((& $ResolvedPythonPath --version 2>&1) | Out-String).Trim()
$PythonVersionExitCode = $LASTEXITCODE
if ($PythonVersionExitCode -ne 0) {
    throw "Python version inspection failed with code $PythonVersionExitCode."
}
if ($PythonVersion -ne 'Python 3.12.10') {
    throw "Expected Python 3.12.10, found: $PythonVersion"
}

# Run the complete Workbook 05 Python suite first. This includes the source,
# schema, workflow, evidence and adversarial validation contracts.
& $ResolvedPythonPath `
    -m unittest discover `
    -s 'tests/testing/workbook05' `
    -p 'test_*.py' `
    -v
$AllPythonTestsExitCode = $LASTEXITCODE
if ($AllPythonTestsExitCode -ne 0) {
    throw "Workbook 05 Python tests failed with code $AllPythonTestsExitCode."
}

# Run the machine-observation PowerShell contract under Windows PowerShell 5.1.
& '.\tests\testing\workbook05\Invoke-PreflightModuleTests.ps1'

# Run the shell-free source-admission PowerShell contract before any live source
# tree can be inspected by the workflow.
& '.\tests\testing\workbook05\Invoke-SourceAdmissionModuleTests.ps1'

# Validate the complete controlled testing workspace and its immutable inputs.
& '.\scripts\testing\Validate-Controlled-Testing-Workspace.ps1'

# Validate the planned OpenVINO extension structure and all preserved test IDs.
& '.\scripts\testing\Validate-OpenVINO-Codec-Extension.ps1'

# Re-run the measurement-control contract explicitly so the repository-level
# gate records this required R5 category independently from broad discovery.
& $ResolvedPythonPath `
    -m unittest discover `
    -s 'tests/testing/workbook05' `
    -p 'test_measurement_controls.py' `
    -v
$MeasurementControlsExitCode = $LASTEXITCODE
if ($MeasurementControlsExitCode -ne 0) {
    throw "Measurement-control validation failed with code $MeasurementControlsExitCode."
}

# Re-run schema validation explicitly to prove controlled JSON instances and
# templates remain compatible with their closed repository schemas.
& $ResolvedPythonPath `
    -m unittest discover `
    -s 'tests/testing/workbook05' `
    -p 'test_schema_validation.py' `
    -v
$SchemaValidationExitCode = $LASTEXITCODE
if ($SchemaValidationExitCode -ne 0) {
    throw "Schema validation failed with code $SchemaValidationExitCode."
}

# Re-run source-admission settings and template checks as a named R5 gate.
& $ResolvedPythonPath `
    -m unittest discover `
    -s 'tests/testing/workbook05' `
    -p 'test_source_admission_settings.py' `
    -v
$SourceSettingsExitCode = $LASTEXITCODE
if ($SourceSettingsExitCode -ne 0) {
    throw "Source-admission settings validation failed with code $SourceSettingsExitCode."
}

# Re-run the dedicated workflow contract so neither the Intel nor hosted job
# can silently bypass this repository-level gate.
& $ResolvedPythonPath `
    -m unittest discover `
    -s 'tests/testing/workbook05' `
    -p 'test_source_workflow_contract.py' `
    -v
$WorkflowContractExitCode = $LASTEXITCODE
if ($WorkflowContractExitCode -ne 0) {
    throw "Source-admission workflow validation failed with code $WorkflowContractExitCode."
}

# Run git diff --check last so whitespace damage blocks the final success claim.
& git diff --check
$DiffCheckExitCode = $LASTEXITCODE
if ($DiffCheckExitCode -ne 0) {
    throw "Git diff validation failed with code $DiffCheckExitCode."
}

# Print the controlled success claim only after every required check completed.
Write-Host ''
Write-Host 'WORKBOOK 05 SOURCE-ADMISSION PHASE 1 GATE: PASS' -ForegroundColor Green
Write-Host 'No OpenVINO build, model download, inference, performance test or quality scoring was performed.' -ForegroundColor Yellow
