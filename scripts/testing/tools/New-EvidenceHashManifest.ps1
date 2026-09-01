<#
.SYNOPSIS
Creates a SHA-256 manifest for all evidence belonging to one controlled run.

.DESCRIPTION
Searches the formal campaign evidence roots for the supplied route, test and run,
hashes every evidence file and writes one portable JSON index beside the run manifest.
#>

[CmdletBinding()]
param(
    # Route folder containing the run evidence.
    [Parameter(Mandatory)][ValidateSet(
        "upstream-llama-cpp",
        "atomicbot-turboquant",
        "animehacker-tq3-0",
        "official-openvino",
        "custom-openvino-turboquant"
    )][string]$Route,

    # Exact workbook test ID.
    [Parameter(Mandatory)][ValidatePattern('^(UL|AB|AH|OV|OVT)-[A-Za-z0-9-]+$')][string]$TestId,

    # Exact run ID derived from the test ID.
    [Parameter(Mandatory)][ValidatePattern('^(UL|AB|AH|OV|OVT)-[A-Za-z0-9-]+-R\d{3}$')][string]$RunId
)

# Stop immediately so an incomplete hash index is not accepted.
$ErrorActionPreference = "Stop"

# Catch misspelled or undefined variables.
Set-StrictMode -Version Latest

# Require the run ID to begin with the supplied test ID.
if (-not $RunId.StartsWith("$TestId-R", [StringComparison]::Ordinal)) {
    throw "Run ID '$RunId' does not belong to test ID '$TestId'."
}

# Resolve the repository root before using portable relative paths.
$RepositoryRoot = (& git rev-parse --show-toplevel 2>$null).Trim()
if (-not $RepositoryRoot) {
    throw "Run this script from inside the Git repository."
}

# Enter the repository root.
Set-Location -LiteralPath $RepositoryRoot

# Define the raw and derived evidence categories included in the run index.
$EvidenceTypes = @("logs", "outputs", "metrics", "results", "notes")

# Collect every evidence file for the supplied run.
$Files = New-Object System.Collections.Generic.List[System.IO.FileInfo]
foreach ($EvidenceType in $EvidenceTypes) {
    # Build the category-specific run path.
    $Path = "experiments/granite_turboquant_intel/$EvidenceType/$Route/$TestId/$RunId"

    # Add files only when that category contains evidence.
    if (Test-Path -LiteralPath $Path) {
        Get-ChildItem -LiteralPath $Path -Recurse -File | ForEach-Object { $Files.Add($_) }
    }
}

# Refuse to create an empty manifest.
if ($Files.Count -eq 0) {
    throw "No evidence files were found for $RunId."
}

# Hash every file and store repository-relative paths for portability.
$Records = foreach ($File in ($Files | Sort-Object FullName)) {
    # Convert the absolute path into a portable Git path.
    $RelativePath = [System.IO.Path]::GetRelativePath($RepositoryRoot, $File.FullName).Replace('\', '/')

    # Create the file record with size and lowercase SHA-256.
    [ordered]@{
        path = $RelativePath
        size_bytes = $File.Length
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $File.FullName).Hash.ToLowerInvariant()
    }
}

# Select the controlled run-manifest directory.
$ManifestDirectory = "experiments/granite_turboquant_intel/manifests/runs/$Route/$TestId/$RunId"

# Require the run manifest to exist before final evidence hashing.
$RunManifestPath = Join-Path $ManifestDirectory "run-manifest.json"
if (-not (Test-Path -LiteralPath $RunManifestPath)) {
    throw "Run manifest is missing: $RunManifestPath"
}

# Build the immutable evidence-hash index.
$HashManifest = [ordered]@{
    schema_version = "1.0"
    run_id = $RunId
    created_at_utc = [DateTime]::UtcNow.ToString("o")
    algorithm = "SHA-256"
    files = @($Records)
    notes = "Generated after evidence capture. Later changes require a new run or a documented pre-commit regeneration."
}

# Write the hash manifest beside the run manifest.
$OutputPath = Join-Path $ManifestDirectory "evidence-hashes.json"
$HashManifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $OutputPath -Encoding UTF8

# Confirm the generated index and file count.
Write-Host "Created: $OutputPath" -ForegroundColor Green
Write-Host "Files hashed: $($Records.Count)" -ForegroundColor Cyan
