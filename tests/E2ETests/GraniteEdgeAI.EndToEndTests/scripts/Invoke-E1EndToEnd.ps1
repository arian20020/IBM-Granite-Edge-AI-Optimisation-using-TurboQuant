[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $CandidateManifest,
    [ValidateSet('List', 'Deterministic', 'Smoke', 'Failure', 'Acceptance', 'All')] [string] $Stage = 'List',
    [string] $AssetManifest,
    [string] $H1Manifest,
    [string] $M1Manifest,
    [string] $Q1Manifest
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repositoryRoot = (Resolve-Path (Join-Path $projectRoot '..\..\..')).Path
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { throw 'The x64 dotnet host is unavailable.' }
$requiredCommit = '4748fe04f19afdf6b27c4c12502b84db325e7294'
$requiredTree = 'fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91'
if ((& git -C $repositoryRoot rev-parse "$requiredCommit^{tree}").Trim() -ne $requiredTree) { throw 'E1 frozen source tree mismatch.' }
& git -C $repositoryRoot merge-base --is-ancestor $requiredCommit HEAD
if ($LASTEXITCODE -ne 0) { throw 'E1 branch does not preserve the frozen source ancestry.' }

$env:GRANITE_E2E_CANDIDATE_MANIFEST = (Resolve-Path -LiteralPath $CandidateManifest).Path
if ($AssetManifest) { $env:GRANITE_E2E_ASSET_MANIFEST = (Resolve-Path -LiteralPath $AssetManifest).Path }
if ($H1Manifest) { $env:GRANITE_E2E_H1_MANIFEST = (Resolve-Path -LiteralPath $H1Manifest).Path }
if ($M1Manifest) { $env:GRANITE_E2E_M1_MANIFEST = (Resolve-Path -LiteralPath $M1Manifest).Path }
if ($Q1Manifest) { $env:GRANITE_E2E_Q1_MANIFEST = (Resolve-Path -LiteralPath $Q1Manifest).Path }

$resultRoot = Join-Path $repositoryRoot 'TestResults\Audit-20260828\E1'
New-Item -ItemType Directory -Force -Path $resultRoot | Out-Null
$env:GRANITE_E2E_RESULTS_ROOT = $resultRoot
$project = Join-Path $projectRoot 'GraniteEdgeAI.EndToEndTests.csproj'
$appProject = Join-Path $repositoryRoot 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj'
& $dotnet build $appProject --configuration Debug -p:Platform=x64 -p:GenerateAppxPackageOnBuild=false --disable-build-servers -m:1
if ($LASTEXITCODE -ne 0) { throw "Candidate app build failed with exit code $LASTEXITCODE." }
$appxRecipe = Get-ChildItem (Join-Path $repositoryRoot 'IBM Granite with TurboQuant (Intel)\obj') -Filter '*.build.appxrecipe' -Recurse |
    Where-Object { $_.FullName -match '[\\/]x64[\\/]' -and $_.Length -gt 0 } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if (-not $appxRecipe) { throw 'Blocked: the Debug x64 app build produced no non-empty .build.appxrecipe.' }

& $dotnet build $project --configuration Debug --arch x64 --disable-build-servers -m:1
if ($LASTEXITCODE -ne 0) { throw "E1 build failed with exit code $LASTEXITCODE." }

$assembly = Get-ChildItem (Join-Path $projectRoot 'bin\Debug') -Filter 'GraniteEdgeAI.EndToEndTests.dll' -Recurse | Select-Object -First 1
if (-not $assembly) { throw 'E1 test assembly was not produced.' }
$vswhereCandidates = @((Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'), (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\Installer\vswhere.exe'))
$vswhere = $vswhereCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $vswhere) { throw 'Blocked: vswhere.exe is unavailable; VSTest discovery cannot be authoritative.' }
$visualStudio = (& $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.TestTools.BuildTools -property installationPath).Trim()
$vstest = Get-ChildItem $visualStudio -Filter 'vstest.console.exe' -Recurse | Select-Object -First 1
if (-not $vstest) { throw 'Blocked: vstest.console.exe is unavailable.' }
$runsettings = Join-Path $repositoryRoot 'tests\runsettings\OneWorker.runsettings'

& $vstest.FullName $assembly.FullName /ListTests /Platform:x64 "/Settings:$runsettings"
if ($LASTEXITCODE -ne 0) { throw 'E1 test discovery failed.' }
if ($Stage -eq 'List') { return }

$receiptRoot = 'C:\UCL-AUDIT-NATIVE-RECEIPTS'
foreach ($worker in @('H1', 'M1', 'Q1', 'F1')) {
    $receiptPath = Join-Path $receiptRoot "$worker.json"
    if (-not (Test-Path -LiteralPath $receiptPath)) { throw "Blocked: predecessor native receipt $worker.json is unavailable." }
    $receipt = Get-Content -Raw -LiteralPath $receiptPath | ConvertFrom-Json
    if ($receipt.workerId -ne $worker -or $receipt.phaseClosed -ne $true -or $receipt.processCleanupVerified -ne $true) {
        throw "Blocked: predecessor native receipt $worker.json is not closed and cleanup-verified."
    }
}

$lockPath = 'C:\UCL-AUDIT-NATIVE.lock'
try {
    New-Item -ItemType Directory -Path $lockPath -ErrorAction Stop | Out-Null
} catch {
    $owner = Get-Content -Raw -LiteralPath (Join-Path $lockPath 'owner.json') -ErrorAction SilentlyContinue
    throw "Blocked: the shared native lock is already held. $owner"
}
$ownerRecord = [ordered]@{
    workerId = 'E1'
    processId = $PID
    processStartUtc = (Get-Process -Id $PID).StartTime.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    worktree = $repositoryRoot
    lockAcquiredUtc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    intendedStage = "E1 $Stage"
}
$ownerRecord | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $lockPath 'owner.json')

$filters = @{ Deterministic = '(TestCategory!=NativeSmoke)&(TestCategory!=NativeFailure)&(TestCategory!=NativeAcceptance)'; Smoke = 'TestCategory=NativeSmoke'; Failure = 'TestCategory=NativeFailure'; Acceptance = 'TestCategory=NativeAcceptance'; All = '' }
$arguments = @($assembly.FullName, '/Platform:x64', "/Settings:$runsettings", "/Logger:trx;LogFileName=E1-$Stage.trx", "/ResultsDirectory:$resultRoot")
if ($filters[$Stage]) { $arguments += "/TestCaseFilter:$($filters[$Stage])" }
try {
    & $vstest.FullName @arguments
    $testExitCode = $LASTEXITCODE
} finally {
    $owner = Get-Content -Raw -LiteralPath (Join-Path $lockPath 'owner.json') | ConvertFrom-Json
    if ($owner.workerId -ne 'E1' -or $owner.processId -ne $PID) { throw 'Native lock ownership changed; preserving ambiguous lock evidence.' }
    Remove-Item -LiteralPath $lockPath -Recurse -Force
}
exit $testExitCode
