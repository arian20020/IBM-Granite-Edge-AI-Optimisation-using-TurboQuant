[CmdletBinding()]
param(
    # Default from scripts/testing to the repository root.
    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,

    # The workflow supplies the reviewed Python 3.12.10 application.
    [string] $PythonPath = 'python'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$RepositoryItem = Get-Item -LiteralPath $RepositoryRoot -Force -ErrorAction Stop
if (
    -not $RepositoryItem.PSIsContainer -or
    ($RepositoryItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
) {
    throw "RepositoryRoot must be one normal directory: $RepositoryRoot"
}

# Reuse the established indivisible Workbook 05 gate. It installs only the
# repository-pinned validation requirements into runner temp, runs the complete
# Python discovery, runs every committed PowerShell test, scans forbidden code
# patterns, and checks the repository diff.
$ExistingGate = Join-Path `
    $RepositoryRoot `
    'scripts\testing\Validate-Workbook05-BuildStage.ps1'
if (-not (Test-Path -LiteralPath $ExistingGate -PathType Leaf)) {
    throw "The established Workbook 05 gate is missing: $ExistingGate"
}

& $ExistingGate `
    -RepositoryRoot $RepositoryRoot `
    -PythonPath $PythonPath
if ($LASTEXITCODE -ne 0) {
    throw "The established Workbook 05 gate exited with code $LASTEXITCODE."
}

# Generated model, IR, executable, library, wheel, and archive payloads must
# never enter repository-controlled evidence locations. The live workflow keeps
# those files under C:\w5m and uploads text/data evidence only.
$ForbiddenSuffixes = @(
    '.7z',
    '.a',
    '.bin',
    '.ckpt',
    '.dll',
    '.dylib',
    '.exe',
    '.gguf',
    '.gz',
    '.lib',
    '.onnx',
    '.pt',
    '.pth',
    '.pyd',
    '.safetensors',
    '.so',
    '.tar',
    '.tgz',
    '.whl',
    '.xml',
    '.zip'
)

$EvidenceRoots = @(
    (Join-Path $RepositoryRoot 'docs\testing\workbook05'),
    (Join-Path $RepositoryRoot 'experiments\granite_turboquant_intel\manifests'),
    (Join-Path $RepositoryRoot 'tests\testing\workbook05\fixtures\phase3\asset-bundles')
)

$ForbiddenFiles = [System.Collections.Generic.List[string]]::new()
foreach ($EvidenceRoot in $EvidenceRoots) {
    if (-not (Test-Path -LiteralPath $EvidenceRoot -PathType Container)) {
        continue
    }

    Get-ChildItem `
        -LiteralPath $EvidenceRoot `
        -File `
        -Recurse `
        -Force |
        ForEach-Object {
            if ($_.Extension.ToLowerInvariant() -in $ForbiddenSuffixes) {
                $ForbiddenFiles.Add($_.FullName)
            }
        }
}

if ($ForbiddenFiles.Count -gt 0) {
    throw (
        "Forbidden generated payloads were found in repository evidence roots:`n" +
        ($ForbiddenFiles -join "`n")
    )
}

# Re-run the whitespace/error check immediately before the controlled PASS line
# so a later step cannot mask it. No repository content is modified.
$PreviousLocation = Get-Location
try {
    Set-Location -LiteralPath $RepositoryRoot
    & git diff --check
    if ($LASTEXITCODE -ne 0) {
        throw "git diff --check exited with code $LASTEXITCODE."
    }
}
finally {
    Set-Location -LiteralPath $PreviousLocation
}

Write-Host 'WORKBOOK05_PHASE3_GATE_PASS'
