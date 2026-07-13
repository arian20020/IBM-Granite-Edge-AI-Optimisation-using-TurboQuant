<#
.SYNOPSIS
Creates the required controlled testing directories without overwriting evidence.

.DESCRIPTION
Git does not track empty directories. This script creates the exact application-test and
experimental-evidence hierarchy required by the supplied testing standard and operational
procedure. Existing files and directories are preserved.
#>

[CmdletBinding()]
param()

# Stop immediately when a directory cannot be created.
$ErrorActionPreference = "Stop"

# Catch misspelled or undefined variables.
Set-StrictMode -Version Latest

# Resolve the repository root so the script works from any repository subdirectory.
$RepositoryRoot = (& git rev-parse --show-toplevel 2>$null).Trim()
if (-not $RepositoryRoot) {
    throw "Run this script from inside the Git repository."
}

# Enter the root before creating repository-relative paths.
Set-Location -LiteralPath $RepositoryRoot

# Define the complete test hierarchy from the supplied testing standard.
$TestDirectories = @(
    "tests/Unit/ModelImport",
    "tests/Unit/ModelInspection",
    "tests/Unit/Classification",
    "tests/Unit/Hardware",
    "tests/Unit/MemoryEstimation",
    "tests/Unit/ConfigurationSelection",
    "tests/Unit/Conversion",
    "tests/Unit/Chat",
    "tests/Contracts/LlamaCpp",
    "tests/Contracts/OpenVINO",
    "tests/Contracts/LLMFit",
    "tests/Contracts/ConversionTools",
    "tests/Integration/ImportInspection",
    "tests/Integration/HardwareEstimation",
    "tests/Integration/SelectorBackend",
    "tests/Integration/UiBackend",
    "tests/Integration/ConversionExport",
    "tests/EndToEnd/CoreJourneys",
    "tests/EndToEnd/FailureJourneys",
    "tests/Fixtures/GGUF",
    "tests/Fixtures/OpenVINO",
    "tests/Fixtures/Safetensors",
    "tests/Fixtures/Malformed",
    "tests/Fixtures/ExpectedMetadata",
    "tests/Performance/LlamaCpp",
    "tests/Performance/OpenVINO",
    "tests/Performance/TurboQuant",
    "tests/Performance/Memory",
    "tests/Performance/ContextScaling",
    "tests/AIQuality/Prompts",
    "tests/AIQuality/References",
    "tests/AIQuality/Rubrics",
    "tests/AIQuality/Outputs",
    "tests/Security",
    "tests/Accessibility",
    "tests/Installation"
)

# Define the exact formal evidence hierarchy from the operational procedure.
$EvidenceDirectories = @(
    "experiments/granite_turboquant_intel/manifests/environments",
    "experiments/granite_turboquant_intel/manifests/repositories",
    "experiments/granite_turboquant_intel/manifests/builds",
    "experiments/granite_turboquant_intel/manifests/models",
    "experiments/granite_turboquant_intel/manifests/configurations",
    "experiments/granite_turboquant_intel/manifests/runs",
    "experiments/granite_turboquant_intel/configurations",
    "experiments/granite_turboquant_intel/scripts",
    "experiments/granite_turboquant_intel/logs",
    "experiments/granite_turboquant_intel/outputs",
    "experiments/granite_turboquant_intel/metrics",
    "experiments/granite_turboquant_intel/results",
    "experiments/granite_turboquant_intel/notes",
    "experiments/granite_turboquant_intel/prompts/fixtures",
    "experiments/granite_turboquant_intel/rubrics",
    "docs/testing/workbooks/generated"
)

# Combine both sets so the same creation rule applies everywhere.
$Directories = $TestDirectories + $EvidenceDirectories

# Create only missing directories and leave all existing evidence untouched.
foreach ($Directory in $Directories) {
    if (-not (Test-Path -LiteralPath $Directory)) {
        New-Item -ItemType Directory -Path $Directory -Force | Out-Null
        Write-Host "Created: $Directory" -ForegroundColor Green
    }
    else {
        Write-Host "Kept:    $Directory" -ForegroundColor DarkGray
    }
}

# Remind the operator that empty folders are local preparation, not test evidence.
Write-Host ""
Write-Host "Controlled testing directories are ready." -ForegroundColor Green
Write-Host "Directory creation does not prove that any test passed." -ForegroundColor Yellow
