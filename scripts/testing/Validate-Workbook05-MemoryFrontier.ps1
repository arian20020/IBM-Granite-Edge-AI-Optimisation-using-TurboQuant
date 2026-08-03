<#
.SYNOPSIS
Runs every repository-controlled Workbook 05 memory-frontier scaffold gate.

.DESCRIPTION
This command validates controls, schemas, source admission, checkpointing,
evidence handling, workflow contracts, and workbook traceability. It does not
build OpenVINO, download a model, or run inference.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepositoryRoot = (& git rev-parse --show-toplevel).Trim()
if (-not $RepositoryRoot) {
    throw 'Run this script from inside the Git repository.'
}
Set-Location -LiteralPath $RepositoryRoot
$Python = 'C:\Program Files\Python312\python.exe'
if (-not (Test-Path -LiteralPath $Python -PathType Leaf)) {
    throw "Controlled Python was not found at: $Python"
}

# Every native process is checked immediately so a later success cannot hide a failure.
& $Python -m pip install --disable-pip-version-check -r scripts/testing/workbook05/requirements.txt
if ($LASTEXITCODE -ne 0) {
    throw "Workbook 05 dependency installation failed with code $LASTEXITCODE."
}

& $Python -m unittest discover -s tests/testing/workbook05 -p 'test_*.py' -v
if ($LASTEXITCODE -ne 0) {
    throw "Workbook 05 Python controls failed with code $LASTEXITCODE."
}

powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File tests/testing/workbook05/Invoke-PreflightModuleTests.ps1
if ($LASTEXITCODE -ne 0) {
    throw "Workbook 05 PowerShell controls failed with code $LASTEXITCODE."
}

& .\scripts\testing\Validate-Controlled-Testing-Workspace.ps1
if ($LASTEXITCODE -ne 0) {
    throw "Controlled testing workspace validation failed with code $LASTEXITCODE."
}

& .\scripts\testing\Validate-OpenVINO-Codec-Extension.ps1
if ($LASTEXITCODE -ne 0) {
    throw "OpenVINO codec extension validation failed with code $LASTEXITCODE."
}

Write-Host 'WORKBOOK 05 MEMORY-FRONTIER SCAFFOLD: PASS' -ForegroundColor Green
Write-Host 'No OpenVINO build, model download or inference was performed.' -ForegroundColor Yellow
