<#
.SYNOPSIS
Validates the OpenVINO codec workbook extension before any codec run starts.

.DESCRIPTION
Checks the official/custom workbook templates, the 119-row traceability extension and the
minimum TBQ, QJL and PolarQuant test IDs. It does not execute a model or prove support.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$RepositoryRoot = (& git rev-parse --show-toplevel 2>$null).Trim()
if (-not $RepositoryRoot) { throw "Run this script from inside the Git repository." }
Set-Location -LiteralPath $RepositoryRoot

$Failures = New-Object System.Collections.Generic.List[string]
$RequiredPaths = @(
    "docs/testing/OpenVINO-Codec-Extension-v1.1.md",
    "docs/testing/OpenVINO-Codec-Test-Boundary.md",
    "docs/testing/Test-ID-Catalogue-v1.1.md",
    "docs/testing/OpenVINO-Codec-Traceability-Extension-v1.1.csv",
    "docs/testing/Workbook-05-Memory-Frontier-Execution-Index-v1.csv",
    "docs/testing/workbooks/text-templates/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md",
    "docs/testing/workbooks/text-templates/05_Custom_OpenVINO_TurboQuant_Controlled_Retest_Workbook_v1.md",
    "docs/testing/workbooks/text-templates/06_Cross_Route_Controlled_Comparison_Workbook_v1.md"
)

foreach ($Path in $RequiredPaths) {
    if (-not (Test-Path -LiteralPath $Path)) { $Failures.Add("Missing required path: $Path") }
}

if ($Failures.Count -eq 0) {
    $TraceRows = @(Import-Csv -LiteralPath "docs/testing/OpenVINO-Codec-Traceability-Extension-v1.1.csv")
    if ($TraceRows.Count -ne 119) { $Failures.Add("Expected 119 OpenVINO extension rows, found $($TraceRows.Count).") }

    $Ids = @($TraceRows | ForEach-Object { $_.Test_ID } | Sort-Object -Unique)
    if ($Ids.Count -ne 119) { $Failures.Add("Expected 119 unique OpenVINO extension IDs, found $($Ids.Count).") }

    $RequiredIds = @(
        "OV-TQ-03", "OV-TQ-04", "OV-TQ-05", "OV-TQ-06", "OV-TQ-19", "OV-TQ-20",
        "OVT-A03", "OVT-A04", "OVT-A05", "OVT-A06", "OVT-S01", "OVT-S36",
        "OVT-10", "OVT-11", "OVT-12", "OVT-13", "OVT-29", "OVT-30", "OVT-31"
    )
    foreach ($Id in $RequiredIds) {
        if ($Id -notin $Ids) { $Failures.Add("Required codec test ID missing: $Id") }
    }

    $OfficialText = Get-Content -Raw -LiteralPath $RequiredPaths[5]
    foreach ($Term in @("TBQ4", "TBQ3", "OV-TQS-12", "QJL", "PolarQuant")) {
        if ($OfficialText -notmatch [regex]::Escape($Term)) { $Failures.Add("WB-04 missing term: $Term") }
    }

    $CustomText = Get-Content -Raw -LiteralPath $RequiredPaths[6]
    foreach ($Term in @("TBQ4_QJL", "TBQ3_QJL", "POLAR4", "POLAR3", "OVT-S36", "OVT-A12")) {
        if ($CustomText -notmatch [regex]::Escape($Term)) { $Failures.Add("WB-05 missing term: $Term") }
    }

    # The execution index must preserve every WB-04 and WB-05 traceability ID once.
    $IndexRows = @(Import-Csv -LiteralPath "docs/testing/Workbook-05-Memory-Frontier-Execution-Index-v1.csv")
    $ExpectedExecutionIds = @(
        $TraceRows |
            Where-Object { $_.Workbook_ID -in @('WB-04', 'WB-05') } |
            ForEach-Object { $_.Test_ID } |
            Sort-Object -Unique
    )
    $ActualExecutionIds = @($IndexRows | ForEach-Object { $_.Test_ID })
    $UniqueExecutionIds = @($ActualExecutionIds | Sort-Object -Unique)
    if ($ActualExecutionIds.Count -ne $UniqueExecutionIds.Count) {
        $Failures.Add("Workbook 05 execution index contains duplicate test IDs.")
    }
    foreach ($Id in $ExpectedExecutionIds) {
        if ($Id -notin $UniqueExecutionIds) { $Failures.Add("Execution index is missing traceability ID: $Id") }
    }
    foreach ($Id in $UniqueExecutionIds) {
        if ($Id -notin $ExpectedExecutionIds) { $Failures.Add("Execution index contains unexpected test ID: $Id") }
    }

    $WorkbookManifest = @(Import-Csv -LiteralPath "docs/testing/workbooks/Controlled-Workbook-Manifest.csv")
    $Workbook05 = @($WorkbookManifest | Where-Object { $_.Workbook_ID -eq 'WB-05' })
    if ($Workbook05.Count -ne 1 -or $Workbook05[0].Revision -ne '1.4') {
        $Failures.Add("WB-05 controlled manifest revision must be 1.4.")
    }
}

if ($Failures.Count -gt 0) {
    Write-Host "OPENVINO CODEC EXTENSION: FAIL" -ForegroundColor Red
    $Failures | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "OPENVINO CODEC EXTENSION: PASS" -ForegroundColor Green
Write-Host "This validates structure and planned coverage only; it does not prove codec execution." -ForegroundColor Yellow
