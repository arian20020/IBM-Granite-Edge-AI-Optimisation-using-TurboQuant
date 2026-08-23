[CmdletBinding()]
param()

<#
.SYNOPSIS
Exercises the shared Workbook 05 Windows-path identity boundary.

.DESCRIPTION
The Route A GenAI build proved that CMake and .NET can write the same absolute
Windows directory with different separator conventions. These tests use the real
shared build module and require equivalent paths to compare equal without allowing
different sibling directories to pass.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolve the repository root from this test file and import the production module,
# so the regression protects the same function used by the real build workflow.
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$modulePath = Join-Path `
    $repositoryRoot `
    'scripts/testing/workbook05/Workbook05.Build.psm1'
Import-Module $modulePath -Force -ErrorAction Stop

try {
    # Reproduce run 31322416220: the accepted directory used backslashes while
    # the comparison logic had converted the expected value to forward slashes.
    if (-not (Test-Wb05SameWindowsPath `
        -Left 'C:\w5a\phase2\i-ov\runtime\cmake' `
        -Right 'C:/w5a/phase2/i-ov/runtime/cmake')) {
        throw 'Equivalent Windows paths with different separators must compare equal.'
    }

    # Windows path identity is case-insensitive for this controlled local build
    # root, and one trailing separator must not create a different directory.
    if (-not (Test-Wb05SameWindowsPath `
        -Left 'C:\W5A\phase2\i-ov\runtime\cmake' `
        -Right 'c:\w5a\phase2\i-ov\runtime\cmake\')) {
        throw 'Equivalent Windows paths must ignore case and one trailing separator.'
    }

    # The repair must remain fail-closed: a similarly named sibling directory is
    # not the accepted Runtime package and must never compare as the same path.
    if (Test-Wb05SameWindowsPath `
        -Left 'C:\w5a\phase2\i-ov\runtime\cmake' `
        -Right 'C:\w5a\phase2\i-ov-other\runtime\cmake') {
        throw 'Distinct sibling paths must not compare equal.'
    }

    $global:LASTEXITCODE = 0
    Write-Host 'Workbook 05 Windows path comparison PowerShell tests passed.'
}
finally {
    # Unload the production module so this executable test leaves the host clean
    # for the remaining Workbook 05 PowerShell tests in the same gate process.
    Remove-Module 'Workbook05.Build' -Force -ErrorAction SilentlyContinue
}