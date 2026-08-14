[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string]$RuntimeInstall,

    [Parameter(Mandatory = $true)]
    [string]$RuntimeDecision,

    [Parameter(Mandatory = $true)]
    [string]$GenAIInstall,

    [Parameter(Mandatory = $true)]
    [string]$GenAIDecision,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [Parameter(Mandatory = $false)]
    [string]$PythonPath = 'python'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolve only the repository and output parent. The Python verifier owns all
# accepted-prerequisite path and identity checks and never modifies those inputs.
$RepositoryRootPath = (Resolve-Path -LiteralPath $RepositoryRoot -ErrorAction Stop).Path
$OutputParent = Split-Path -Parent $OutputPath
if ([string]::IsNullOrWhiteSpace($OutputParent)) {
    throw "OutputPath must include an existing parent directory: $OutputPath"
}
$OutputParentPath = (Resolve-Path -LiteralPath $OutputParent -ErrorAction Stop).Path
$OutputName = Split-Path -Leaf $OutputPath
$ResolvedOutputPath = Join-Path $OutputParentPath $OutputName
$TemporaryOutputPath = "$ResolvedOutputPath.tmp"
$BackupOutputPath = "$ResolvedOutputPath.bak"

if (Test-Path -LiteralPath $TemporaryOutputPath) {
    throw "Refusing to reuse an existing prerequisite temporary output: $TemporaryOutputPath"
}
if (Test-Path -LiteralPath $BackupOutputPath) {
    throw "Refusing to reuse an existing prerequisite backup output: $BackupOutputPath"
}

$PythonArguments = @(
    '-m',
    'scripts.testing.workbook05.phase3.prerequisites',
    '--runtime-install',
    $RuntimeInstall,
    '--runtime-decision',
    $RuntimeDecision,
    '--genai-install',
    $GenAIInstall,
    '--genai-decision',
    $GenAIDecision,
    '--repository-root',
    $RepositoryRootPath,
    '--output',
    $TemporaryOutputPath
)

Push-Location $RepositoryRootPath
try {
    # The Python module computes the proof, validates it through
    # assert_phase3_record, and writes only the temporary output.
    & $PythonPath @PythonArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Phase 3 prerequisite verification exited with code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $TemporaryOutputPath -PathType Leaf)) {
        throw "Phase 3 prerequisite verification did not create: $TemporaryOutputPath"
    }

    if (Test-Path -LiteralPath $ResolvedOutputPath -PathType Leaf) {
        [IO.File]::Replace(
            $TemporaryOutputPath,
            $ResolvedOutputPath,
            $BackupOutputPath,
            $true
        )
        Remove-Item -LiteralPath $BackupOutputPath -Force -ErrorAction Stop
    }
    else {
        Move-Item `
            -LiteralPath $TemporaryOutputPath `
            -Destination $ResolvedOutputPath `
            -ErrorAction Stop
    }

    Write-Output $ResolvedOutputPath
}
finally {
    Pop-Location

    # Remove only files created for this atomic write attempt. Existing output
    # remains untouched whenever verification or schema validation fails.
    if (Test-Path -LiteralPath $TemporaryOutputPath) {
        Remove-Item -LiteralPath $TemporaryOutputPath -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $BackupOutputPath) {
        Remove-Item -LiteralPath $BackupOutputPath -Force -ErrorAction SilentlyContinue
    }
}
