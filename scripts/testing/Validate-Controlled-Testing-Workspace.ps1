<#
.SYNOPSIS
Validates the controlled testing workspace before a hardware test is started.

.DESCRIPTION
Checks the required control files, exact test IDs, source manifest, canonical workbook
specifications, JSON prompt/rubric files, campaign folders and Git ignore behaviour.
It does not execute an inference test and must not be treated as proof that a route passed.
#>

[CmdletBinding()]
param()

# Stop immediately so a partial validation cannot be mistaken for success.
$ErrorActionPreference = "Stop"

# Catch misspelled or undefined variables in the validation script.
Set-StrictMode -Version Latest

# Resolve the repository root so the script works from any subdirectory.
$RepositoryRoot = (& git rev-parse --show-toplevel 2>$null).Trim()
if (-not $RepositoryRoot) {
    throw "Run this script from inside the Git repository."
}

# Enter the root so every controlled path is repository-relative and portable.
Set-Location -LiteralPath $RepositoryRoot

# Collect every failure so the operator receives one complete correction list.
$Failures = New-Object System.Collections.Generic.List[string]

# Check one required path and print a consistent result.
function Test-RequiredPath {
    param([Parameter(Mandatory)][string]$Path)

    # Record a missing path instead of stopping at the first problem.
    if (-not (Test-Path -LiteralPath $Path)) {
        $Failures.Add("Missing required path: $Path")
        return $false
    }

    # Confirm the successful structural check to the operator.
    Write-Host "PASS  $Path" -ForegroundColor Green
    return $true
}

# Define the minimum controls that must exist before any test can become Ready.
$RequiredPaths = @(
    "docs/testing/README.md",
    "docs/testing/Test-Strategy.md",
    "docs/testing/Master-Test-Plan.md",
    "docs/testing/Test-ID-Catalogue.md",
    "docs/testing/Workbook-Data-Requirements.md",
    "docs/testing/Execution-Checklist.md",
    "docs/testing/Metric-Definitions.md",
    "docs/testing/Test-Run-Register.csv",
    "docs/testing/Environment-Register.csv",
    "docs/testing/Failure-Register.csv",
    "docs/testing/Test-Traceability-Matrix.csv",
    "docs/testing/Workbook-Completion-Register.csv",
    "docs/testing/source-material/Source-Document-Manifest.csv",
    "docs/testing/workbooks/Controlled-Workbook-Manifest.csv",
    "experiments/granite_turboquant_intel/README.md",
    "experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json",
    "experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json",
    "experiments/granite_turboquant_intel/manifests/templates/run-manifest-template.json",
    "scripts/testing/New-Controlled-TestRun.ps1",
    "scripts/testing/New-EvidenceHashManifest.ps1",
    "docs/testing/Workbook-05-Memory-Frontier-Execution-Index-v1.csv",
    "experiments/granite_turboquant_intel/configurations/workbook05/preflight-settings.json",
    "experiments/granite_turboquant_intel/configurations/workbook05/pinned-document-sources.json",
    "experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1/campaign-manifest.json",
    "experiments/granite_turboquant_intel/schemas/workbook05/source-admission.schema.json",
    "scripts/testing/Validate-Workbook05-MemoryFrontier.ps1"
)

# Check each required file or directory.
foreach ($Path in $RequiredPaths) {
    [void](Test-RequiredPath -Path $Path)
}

# Stop the deeper checks when the files needed by those checks are missing.
if ($Failures.Count -gt 0) {
    throw ($Failures -join [Environment]::NewLine)
}

# Load the source manifest and verify the exact nineteen supplied DOCX records.
$SourceManifest = @(Import-Csv -LiteralPath "docs/testing/source-material/Source-Document-Manifest.csv")
if ($SourceManifest.Count -ne 19) {
    $Failures.Add("Expected 19 source-document records, found $($SourceManifest.Count).")
}

# Validate the recorded source hashes and any optional Git-reviewable extraction paths.
foreach ($Row in $SourceManifest) {
    # Reject missing or malformed SHA-256 values.
    if ($Row.Original_SHA256 -notmatch '^[0-9a-fA-F]{64}$') {
        $Failures.Add("Invalid source SHA-256 for: $($Row.Source_Path)")
    }

    # Validate an extracted text file when the manifest version includes that field.
    if ($Row.PSObject.Properties.Name -contains "Extracted_Text_Path") {
        if ($Row.Extracted_Text_Path) {
            if (-not (Test-Path -LiteralPath $Row.Extracted_Text_Path)) {
                $Failures.Add("Source text extraction missing: $($Row.Extracted_Text_Path)")
            }
            else {
                # Require the extraction header to retain the original document hash.
                $ExtractionText = Get-Content -Raw -LiteralPath $Row.Extracted_Text_Path
                if ($ExtractionText -notmatch [regex]::Escape($Row.Original_SHA256)) {
                    $Failures.Add("Source extraction does not contain its original SHA-256: $($Row.Extracted_Text_Path)")
                }
            }
        }
    }
}

# Load the controlled workbook manifest and require exactly one workbook per route.
$WorkbookManifest = @(Import-Csv -LiteralPath "docs/testing/workbooks/Controlled-Workbook-Manifest.csv")
if ($WorkbookManifest.Count -ne 6) {
    $Failures.Add("Expected 6 controlled workbook records, found $($WorkbookManifest.Count).")
}

# Check every canonical Markdown workbook specification.
foreach ($Row in $WorkbookManifest) {
    if (-not (Test-Path -LiteralPath $Row.Canonical_Text_Template)) {
        $Failures.Add("Canonical workbook template missing: $($Row.Canonical_Text_Template)")
    }
}

# Require the reproducible DOCX generator and its pinned dependency file.
[void](Test-RequiredPath -Path "scripts/testing/Generate-Controlled-Workbooks.py")
[void](Test-RequiredPath -Path "scripts/testing/requirements.txt")

# Validate generated DOCX files when the operator has already generated them.
$GeneratedDirectory = "docs/testing/workbooks/generated"
if (Test-Path -LiteralPath $GeneratedDirectory) {
    # Collect the generated Word workbooks.
    $GeneratedFiles = @(Get-ChildItem -LiteralPath $GeneratedDirectory -Filter "*.docx" -File)

    # Require one generated workbook for each route.
    if ($GeneratedFiles.Count -ne 6) {
        $Failures.Add("Expected 6 generated DOCX workbooks, found $($GeneratedFiles.Count).")
    }

    # Reject zero-byte files that only appear to exist.
    foreach ($GeneratedFile in $GeneratedFiles) {
        if ($GeneratedFile.Length -le 0) {
            $Failures.Add("Generated workbook is empty: $($GeneratedFile.FullName)")
        }
    }
}

# Parse the prompt JSON before a hardware run discovers malformed configuration.
try {
    # Read and decode the frozen prompt set.
    $PromptSet = Get-Content -Raw -LiteralPath "experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json" | ConvertFrom-Json

    # Require the controlled ID and all six P1-P6 prompts.
    $PromptIds = @($PromptSet.prompts | ForEach-Object { $_.prompt_id })
    if ($PromptSet.prompt_set_id -ne "GTQ-PROMPTS-v1" -or ($PromptIds -join ',') -ne "P1,P2,P3,P4,P5,P6") {
        $Failures.Add("Prompt set must be GTQ-PROMPTS-v1 with prompts P1 to P6 in order.")
    }
}
catch {
    # Record malformed JSON as a validation failure.
    $Failures.Add("Prompt-set JSON is invalid: $($_.Exception.Message)")
}

# Parse and validate the controlling quality rubric.
try {
    # Read and decode the 0-10 rubric.
    $Rubric = Get-Content -Raw -LiteralPath "experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json" | ConvertFrom-Json

    # Require the exact rubric ID and five weighted dimensions.
    if ($Rubric.rubric_id -ne "GTQ-QUALITY-RUBRIC-v1" -or $Rubric.dimensions.Count -ne 5) {
        $Failures.Add("Rubric must be GTQ-QUALITY-RUBRIC-v1 with exactly five dimensions.")
    }

    # Ensure the weights total one, allowing only a tiny floating-point tolerance.
    $WeightTotal = ($Rubric.dimensions | Measure-Object -Property weight -Sum).Sum
    if ([Math]::Abs($WeightTotal - 1.0) -gt 0.000001) {
        $Failures.Add("Rubric weights must total 1.0; found $WeightTotal.")
    }
}
catch {
    # Record malformed rubric JSON as a validation failure.
    $Failures.Add("Rubric JSON is invalid: $($_.Exception.Message)")
}

# Extract every exact workbook test ID from the catalogue's controlled inline lists.
$CatalogueText = Get-Content -Raw -LiteralPath "docs/testing/Test-ID-Catalogue.md"
$CatalogueIds = [regex]::Matches(
    $CatalogueText,
    '(?<![A-Za-z0-9-])(?:UL|AB|AH|OVT|OV)-(?:[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*)'
) | ForEach-Object { $_.Value } | Sort-Object -Unique

# Require the 105 unique IDs found during the source-workbook audit.
if ($CatalogueIds.Count -ne 105) {
    $Failures.Add("Expected 105 unique controlled test IDs, found $($CatalogueIds.Count).")
}

# Confirm that generic Visual Studio log ignores do not hide formal campaign logs.
$IgnoreProbe = "experiments/granite_turboquant_intel/logs/_validation/probe.log"
$IgnoreDetails = (& git check-ignore --no-index --verbose -- $IgnoreProbe 2>$null) -join ""
if ($IgnoreDetails -and $IgnoreDetails -notmatch ':\d+:!') {
    $Failures.Add("Campaign .log files are still ignored by Git: $IgnoreProbe")
}
else {
    Write-Host "PASS  Campaign log files can be committed." -ForegroundColor Green
}

# Define file types that must never be committed inside the evidence campaign.
$ForbiddenPatterns = @("*.gguf", "*.safetensors", "*.onnx", "*.pt", "*.pth", "*.ckpt", "*.exe", "*.dll")

# Search the campaign recursively for forbidden model weights or runtime binaries.
foreach ($Pattern in $ForbiddenPatterns) {
    $Found = Get-ChildItem -LiteralPath "experiments/granite_turboquant_intel" -Recurse -File -Filter $Pattern -ErrorAction SilentlyContinue
    foreach ($Item in $Found) {
        $Failures.Add("Forbidden large/binary artefact in evidence root: $($Item.FullName)")
    }
}

# Return one clear overall failure result after all checks have run.
if ($Failures.Count -gt 0) {
    Write-Host ""
    Write-Host "CONTROLLED TESTING WORKSPACE: FAIL" -ForegroundColor Red
    $Failures | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}

# Confirm structural readiness while preserving the distinction from test success.
Write-Host ""
Write-Host "CONTROLLED TESTING WORKSPACE: PASS" -ForegroundColor Green
Write-Host "The repository is structurally ready. This does not prove that any hardware test has passed." -ForegroundColor Yellow
