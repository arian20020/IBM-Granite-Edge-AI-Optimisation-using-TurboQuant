[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputRoot)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'

if (-not [IO.Path]::IsPathFullyQualified($OutputRoot)) { throw 'Output root must be absolute.' }
$Parent=(Resolve-Path -LiteralPath (Split-Path -Parent $OutputRoot) -ErrorAction Stop).Path
$Output=Join-Path $Parent (Split-Path -Leaf $OutputRoot)
if (Test-Path -LiteralPath $Output) { throw 'Output root already exists.' }
New-Item -ItemType Directory -Path $Output | Out-Null

$Project='tools/TurboVec.PdfExtractionSpike.Tests/TurboVec.PdfExtractionSpike.Tests.csproj'
$BuildLog=Join-Path $Output 'build.log'
& dotnet build $Project -c Release --nologo 2>&1 | Tee-Object -LiteralPath $BuildLog
$BuildExit=$LASTEXITCODE
if ($BuildExit -ne 0) {
    @{schema_version='2.0';status='failed';phase='build';exit_code=$BuildExit} |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $Output 'manifest.json') -Encoding utf8
    exit $BuildExit
}

$Executable=(Resolve-Path -LiteralPath 'tools/TurboVec.PdfExtractionSpike.Tests/bin/Release/net8.0-windows10.0.19041.0/win-x64/TurboVec.PdfExtractionSpike.Tests.exe').Path
$TestLog=Join-Path $Output 'test.log'
& $Executable --report-trx --results-directory $Output 2>&1 | Tee-Object -LiteralPath $TestLog
$TestExit=$LASTEXITCODE
$TrxFiles=@(Get-ChildItem -LiteralPath $Output -Filter '*.trx' -File)
if ($TrxFiles.Count -ne 1) { throw 'The PDF test runner did not produce exactly one TRX file.' }
$Trx=$TrxFiles[0]
[xml]$Document=Get-Content -LiteralPath $Trx.FullName -Raw
$Counters=$Document.TestRun.ResultSummary.Counters
$Total=[int]$Counters.total
$Passed=[int]$Counters.passed
$Failed=[int]$Counters.failed + [int]$Counters.error + [int]$Counters.timeout + [int]$Counters.aborted
$Skipped=$Total-[int]$Counters.executed
$Manifest=[ordered]@{
    schema_version='2.0'
    campaign_id='turbovec-production-scale-final-evaluation-v2'
    status=if($TestExit -eq 0){'passed'}else{'failed'}
    runner='Microsoft.Testing.Platform executable'
    tests=[ordered]@{discovered=$Total;executed=[int]$Counters.executed;passed=$Passed;failed=$Failed;skipped=$Skipped}
    exit_code=$TestExit
    trx=[ordered]@{name=$Trx.Name;bytes=$Trx.Length;sha256=(Get-FileHash -LiteralPath $Trx.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
    build_log=[ordered]@{name='build.log';bytes=(Get-Item -LiteralPath $BuildLog).Length;sha256=(Get-FileHash -LiteralPath $BuildLog -Algorithm SHA256).Hash.ToLowerInvariant()}
    test_log=[ordered]@{name='test.log';bytes=(Get-Item -LiteralPath $TestLog).Length;sha256=(Get-FileHash -LiteralPath $TestLog -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$Manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $Output 'manifest.json') -Encoding utf8
exit $TestExit
