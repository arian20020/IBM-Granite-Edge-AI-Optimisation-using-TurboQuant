<#
.SYNOPSIS
Creates the complete folder and manifest skeleton for one controlled test execution.

.PARAMETER TestId
The exact ID from docs/testing/Test-ID-Catalogue.md, for example UL-04.

.PARAMETER Route
One of the five controlled execution-route folder names.

.PARAMETER RunNumber
The 1-based execution number. Every retest must use a new number.
#>

[CmdletBinding()]
param(
    # Require an exact workbook-style test ID.
    [Parameter(Mandatory)][ValidatePattern('^(UL|AB|AH|OV|OVT)-[A-Za-z0-9-]+$')][string]$TestId,

    # Restrict the route to the five executable feasibility routes.
    [Parameter(Mandatory)][ValidateSet(
        "upstream-llama-cpp",
        "atomicbot-turboquant",
        "animehacker-tq3-0",
        "official-openvino",
        "custom-openvino-turboquant"
    )][string]$Route,

    # Keep run numbering predictable and sortable.
    [Parameter(Mandatory)][ValidateRange(1, 999)][int]$RunNumber
)

# Stop immediately to avoid leaving a partially created run.
$ErrorActionPreference = "Stop"

# Catch misspelled or undefined variables.
Set-StrictMode -Version Latest

# Resolve the repository root so the script works from any subdirectory.
$RepositoryRoot = (& git rev-parse --show-toplevel 2>$null).Trim()
if (-not $RepositoryRoot) {
    throw "Run this script from inside the Git repository."
}

# Enter the root before reading controlled paths.
Set-Location -LiteralPath $RepositoryRoot

# Read the exact controlled test-ID catalogue.
$Catalogue = Get-Content -Raw -LiteralPath "docs/testing/Test-ID-Catalogue.md"

# Extract all test IDs but exclude example run IDs ending in -R followed by three digits.
$CatalogueIds = [regex]::Matches(
    $Catalogue,
    '(?<![A-Za-z0-9-])(?:UL|AB|AH|OVT|OV)-(?:[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*)'
) | ForEach-Object { $_.Value } | Where-Object { $_ -notmatch '-R\d{3}$' } | Sort-Object -Unique

# Reject an ID not present in the audited workbook catalogue.
if ($TestId -notin $CatalogueIds) {
    throw "Test ID '$TestId' is not present in the controlling catalogue."
}

# Build a stable run ID that never overwrites an earlier execution.
$RunId = "{0}-R{1:D3}" -f $TestId, $RunNumber

# Define all evidence categories that receive a matching run directory.
$EvidenceTypes = @("logs", "outputs", "metrics", "results", "notes")

# Check all target directories before creating any of them.
foreach ($EvidenceType in $EvidenceTypes) {
    $RunDirectory = Join-Path "experiments/granite_turboquant_intel/$EvidenceType/$Route/$TestId" $RunId
    if (Test-Path -LiteralPath $RunDirectory) {
        throw "Run directory already exists: $RunDirectory"
    }
}

# Check the manifest directory for the same run-ID collision.
$ManifestDirectory = Join-Path "experiments/granite_turboquant_intel/manifests/runs/$Route/$TestId" $RunId
if (Test-Path -LiteralPath $ManifestDirectory) {
    throw "Run manifest directory already exists: $ManifestDirectory"
}

# Create each evidence directory only after every collision check passed.
foreach ($EvidenceType in $EvidenceTypes) {
    $RunDirectory = Join-Path "experiments/granite_turboquant_intel/$EvidenceType/$Route/$TestId" $RunId
    New-Item -ItemType Directory -Path $RunDirectory -Force | Out-Null
}

# Create the matching manifest directory.
New-Item -ItemType Directory -Path $ManifestDirectory -Force | Out-Null

# Load the controlled run-manifest schema.
$TemplatePath = "experiments/granite_turboquant_intel/manifests/templates/run-manifest-template.json"
$Manifest = Get-Content -Raw -LiteralPath $TemplatePath | ConvertFrom-Json

# Populate the run identity and operator fields.
$Manifest.test_id = $TestId
$Manifest.run_id = $RunId
$Manifest.route = $Route
$Manifest.operator = $env:USERNAME

# Populate timestamps at run preparation time; execution end time remains blank.
$Manifest.timestamps.start_utc = [DateTime]::UtcNow.ToString("o")
$Manifest.timestamps.start_local = [DateTime]::Now.ToString("o")
$Manifest.timestamps.timezone = [TimeZoneInfo]::Local.Id

# Save the new manifest without modifying the template.
$ManifestPath = Join-Path $ManifestDirectory "run-manifest.json"
$Manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $ManifestPath -Encoding UTF8

# Create an operator instruction file beside the manifest.
$Instruction = @"
Run ID: $RunId
Test ID: $TestId
Route: $Route

Before execution:
1. Complete every manifest reference and the exact command path.
2. Save the command and environment variables before running it.
3. Capture stdout and stderr separately.
4. Record requested and actual backend, device, placement and optimisation.
5. Label pilot, warm-up and measured repetitions correctly.
6. Do not edit raw evidence after capture.
"@
Set-Content -LiteralPath (Join-Path $ManifestDirectory "RUN-INSTRUCTIONS.txt") -Value $Instruction -Encoding UTF8

# Confirm the controlled run identity and manifest path.
Write-Host "Created controlled run: $RunId" -ForegroundColor Green
Write-Host "Manifest: $ManifestPath" -ForegroundColor Cyan
Write-Host "Complete the manifest before executing the test." -ForegroundColor Yellow
