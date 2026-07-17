[CmdletBinding(SupportsShouldProcess = $true)]
param(
    # Path to the controlled RTM workbook that will be exported.
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$WorkbookPath,

    # Repository root. The current directory is used when this is omitted.
    [Parameter(Mandatory = $false)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string]$RepositoryPath = (Get-Location).Path,

    # Optional fixed generation date for reproducible historical snapshots.
    [Parameter(Mandatory = $false)]
    [ValidatePattern('^\d{4}-\d{2}-\d{2}$')]
    [string]$GeneratedOn = (Get-Date -Format 'yyyy-MM-dd'),

    # Check that each repository-relative evidence path currently exists.
    [Parameter(Mandatory = $false)]
    [switch]$CheckEvidencePaths
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolve the requested paths before running any generator.
$ResolvedWorkbook = (Resolve-Path -LiteralPath $WorkbookPath).Path
$ResolvedRepository = (Resolve-Path -LiteralPath $RepositoryPath).Path
$ScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path

# Locate a supported Python 3 command without relying on a virtual environment.
$PythonCommand = $null
$PythonArguments = @()

if (Get-Command py -ErrorAction SilentlyContinue) {
    $PythonCommand = 'py'
    $PythonArguments = @('-3')
}
elseif (Get-Command python -ErrorAction SilentlyContinue) {
    $PythonCommand = 'python'
}
else {
    throw 'Python 3 was not found. Install Python 3 or make py/python available on PATH.'
}

# Define the three controlled steps in the export pipeline.
$ExportScript = Join-Path $ScriptDirectory 'export_task_catalogue.py'
$GenerateScript = Join-Path $ScriptDirectory 'generate_traceability_documents.py'
$ValidateScript = Join-Path $ScriptDirectory 'validate_task_catalogue.py'

# Show exactly what will be updated before invoking the scripts.
Write-Host "Repository: $ResolvedRepository"
Write-Host "Workbook:   $ResolvedWorkbook"
Write-Host "Date:       $GeneratedOn"

if ($PSCmdlet.ShouldProcess($ResolvedRepository, 'Regenerate traceability catalogue and documents')) {
    # Export the workbook into the normalized machine-readable JSON catalogue.
    & $PythonCommand @PythonArguments $ExportScript `
        --workbook $ResolvedWorkbook `
        --repository-root $ResolvedRepository `
        --generated-on $GeneratedOn

    if ($LASTEXITCODE -ne 0) {
        throw "Task catalogue export failed with exit code $LASTEXITCODE."
    }

    # Generate all Markdown views from the normalized JSON catalogue.
    & $PythonCommand @PythonArguments $GenerateScript `
        --repository-root $ResolvedRepository

    if ($LASTEXITCODE -ne 0) {
        throw "Traceability document generation failed with exit code $LASTEXITCODE."
    }

    # Validate counts, IDs, relationships, evidence paths and generated outputs.
    $ValidationArguments = @(
        $ValidateScript,
        '--repository-root',
        $ResolvedRepository
    )

    if ($CheckEvidencePaths) {
        $ValidationArguments += '--check-evidence-paths'
    }

    & $PythonCommand @PythonArguments @ValidationArguments

    if ($LASTEXITCODE -ne 0) {
        throw "Traceability validation failed with exit code $LASTEXITCODE."
    }

    Write-Host ''
    Write-Host 'Traceability update completed successfully.' -ForegroundColor Green
    Write-Host 'Review the Git diff before committing the generated snapshot.'
}
