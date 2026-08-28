[CmdletBinding()]
param(
    [string] $CandidateManifest,
    [string] $IntegrationCandidateCommit,
    [string] $IntegrationCandidateTree,
    [string] $IntegrationCandidateRemote = 'origin',
    [string] $IntegrationCandidateRemoteRef,
    [string] $R3ClosureManifest,
    [string] $R3ClosureRelativePath,
    [ValidateSet('List', 'Deterministic', 'Smoke', 'Failure', 'Acceptance', 'Restart', 'RealModel', 'All')] [string] $Stage = 'List',
    [string] $AssetManifest,
    [string] $H1Manifest,
    [string] $M1Manifest,
    [string] $Q1Manifest,
    [string] $HandoffRoot = 'C:\UCL-AUDIT-HANDOFFS',
    [string] $NativeReceiptRoot = 'C:\UCL-AUDIT-NATIVE-RECEIPTS',
    [string] $DotNetHostPath = $env:DOTNET_HOST_PATH
)

$ErrorActionPreference = 'Stop'
function Write-StageSummary {
    param([string] $TrxPath, [string] $SummaryPath, [string] $StageName)
    [xml] $trx = Get-Content -Raw -LiteralPath $TrxPath
    $counters = $trx.TestRun.ResultSummary.Counters
    $discovered = [int] $counters.total
    $trxExecuted = [int] $counters.executed
    $passed = [int] $counters.passed
    $failed = [int] $counters.failed
    $skipped = [int] $counters.notExecuted
    if ($discovered -le 0 -or $trxExecuted -ne ($passed + $failed) -or $discovered -ne ($trxExecuted + $skipped)) {
        throw "Stage $StageName produced zero discovery or inconsistent TRX arithmetic."
    }
    [ordered]@{
        stage = $StageName
        discovered = $discovered
        executed = $passed + $failed + $skipped
        passed = $passed
        failed = $failed
        skipped = $skipped
        trxSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $TrxPath).Hash.ToLowerInvariant()
    } | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath $SummaryPath
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repositoryRoot = (Resolve-Path (Join-Path $projectRoot '..\..\..')).Path
$dotnet = if ($DotNetHostPath) {
    (Resolve-Path -LiteralPath $DotNetHostPath).Path
} else {
    Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
}
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) { throw 'The x64 dotnet host is unavailable.' }
$requiredCommit = '4748fe04f19afdf6b27c4c12502b84db325e7294'
$requiredTree = 'fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91'
$previousC0Tip = 'a5ef3558334e50587889140dafba194853938765'
if ((& git -C $repositoryRoot rev-parse "$requiredCommit^{tree}").Trim() -ne $requiredTree) { throw 'E1 frozen source tree mismatch.' }
$nativeStage = $Stage -ne 'Deterministic'
$candidateRecord = $null
$candidateCommit = $null
$candidateTree = $null
if ($nativeStage) {
if (-not $CandidateManifest -or -not $IntegrationCandidateCommit -or -not $IntegrationCandidateTree -or
    -not $IntegrationCandidateRemoteRef -or -not $R3ClosureManifest -or -not $R3ClosureRelativePath) {
    throw 'Native List/campaign stages require the exact candidate manifest, pushed C0 identity, and committed R3 closure manifest.'
}
$gitIdentity = '^[0-9a-f]{40}$'
if ($IntegrationCandidateCommit -notmatch $gitIdentity -or $IntegrationCandidateTree -notmatch $gitIdentity) {
    throw 'Integrated candidate commit/tree must be exact lowercase Git identities.'
}
if ($IntegrationCandidateRemote -notmatch '^[A-Za-z0-9._-]+$' -or
    $IntegrationCandidateRemoteRef -notmatch '^refs/heads/[A-Za-z0-9._/-]+$' -or
    $IntegrationCandidateRemoteRef.Contains('..') -or $IntegrationCandidateRemoteRef.Contains('@{')) {
    throw 'Integrated candidate remote/ref syntax is invalid.'
}
if ($IntegrationCandidateCommit -eq $previousC0Tip) {
    throw 'Blocked: native acceptance cannot run against the previous C0 tip alone.'
}
& git -C $repositoryRoot cat-file -e "$IntegrationCandidateCommit^{commit}"
if ($LASTEXITCODE -ne 0) { throw 'Integrated candidate commit is unavailable in the local Git object database.' }
$actualCandidateTree = (& git -C $repositoryRoot rev-parse "$IntegrationCandidateCommit^{tree}").Trim()
if ($actualCandidateTree -ne $IntegrationCandidateTree) { throw 'Integrated candidate tree does not match its commit.' }
$remoteLine = @(& git -C $repositoryRoot ls-remote --exit-code $IntegrationCandidateRemote $IntegrationCandidateRemoteRef)
if ($LASTEXITCODE -ne 0 -or $remoteLine.Count -ne 1 -or $remoteLine[0] -ne "$IntegrationCandidateCommit`t$IntegrationCandidateRemoteRef") {
    throw 'Integrated candidate is not the exact pushed remote ref.'
}
foreach ($ancestor in @($requiredCommit, $previousC0Tip)) {
    & git -C $repositoryRoot merge-base --is-ancestor $ancestor $IntegrationCandidateCommit
    if ($LASTEXITCODE -ne 0) { throw "Integrated candidate does not descend from required ancestor $ancestor." }
}
& git -C $repositoryRoot merge-base --is-ancestor $IntegrationCandidateCommit HEAD
if ($LASTEXITCODE -ne 0) { throw 'E1 branch does not descend from the exact integrated candidate.' }
$e1Delta = @(& git -C $repositoryRoot diff --name-only $IntegrationCandidateCommit HEAD)
$outOfScopeDelta = @($e1Delta | Where-Object {
    $_ -notmatch '^tests/E2ETests/' -and
    $_ -ne 'docs/audits/2026-08-29/E1-native-end-to-end-tests-r3.md' -and
    $_ -ne 'docs/superpowers/specs/2026-08-29-e1-r3-release-veto-design.md' -and
    $_ -ne 'docs/superpowers/plans/2026-08-29-e1-r3-release-veto.md'
})
if ($outOfScopeDelta.Count -gt 0) {
    throw "E1 branch changes production/shared paths after the integrated candidate: $($outOfScopeDelta -join ', ')."
}
$candidateCommit = $IntegrationCandidateCommit
$candidateTree = $IntegrationCandidateTree
$env:GRANITE_E2E_CANDIDATE_COMMIT = $candidateCommit
$env:GRANITE_E2E_CANDIDATE_TREE = $candidateTree
$env:GRANITE_E2E_CANDIDATE_REMOTE = $IntegrationCandidateRemote
$env:GRANITE_E2E_CANDIDATE_REMOTE_REF = $IntegrationCandidateRemoteRef
$env:GRANITE_E2E_R3_CLOSURE_MANIFEST = (Resolve-Path -LiteralPath $R3ClosureManifest).Path
$env:GRANITE_E2E_R3_CLOSURE_RELATIVE_PATH = $R3ClosureRelativePath

$env:GRANITE_E2E_CANDIDATE_MANIFEST = (Resolve-Path -LiteralPath $CandidateManifest).Path
$candidateRecord = Get-Content -Raw -LiteralPath $env:GRANITE_E2E_CANDIDATE_MANIFEST | ConvertFrom-Json
if ($candidateRecord.sourceCommit -ne $candidateCommit -or $candidateRecord.sourceTree -ne $candidateTree) {
    throw 'The candidate manifest is not bound to the exact integrated branch tip/tree.'
}
$candidateExecutable = [IO.Path]::GetFullPath([string]$candidateRecord.executablePath)
$candidateFile = Get-Item -LiteralPath $candidateExecutable -ErrorAction Stop
if ($candidateFile.PSIsContainer -or ($candidateFile.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
    $candidateFile.Length -le 0 -or $candidateFile.Length -ne [long]$candidateRecord.executableBytes) {
    throw 'Candidate executable byte length or regular-file requirement failed.'
}
$candidateSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $candidateExecutable).Hash.ToLowerInvariant()
if ($candidateRecord.executableSha256 -notmatch '^[0-9a-f]{64}$' -or $candidateSha -ne $candidateRecord.executableSha256) {
    throw 'Candidate executable SHA-256 does not match its manifest.'
}
$installedPackages = @(Get-AppxPackage | Where-Object { $_.PackageFamilyName -eq [string]$candidateRecord.packageFamilyName })
if ($installedPackages.Count -ne 1) { throw 'Candidate package family is not installed exactly once.' }
$installedRoot = [IO.Path]::GetFullPath([string]$installedPackages[0].InstallLocation).TrimEnd('\') + '\'
if (-not $candidateExecutable.StartsWith($installedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Candidate executable is not inside the exact installed package root.'
}
$packageManifest = Get-AppxPackageManifest -Package $installedPackages[0].PackageFullName
$applicationIds = @($packageManifest.Package.Applications.Application | ForEach-Object { [string]$_.Id })
if ($applicationIds -notcontains [string]$candidateRecord.applicationId) {
    throw 'Candidate application ID is absent from the installed package manifest.'
}
if ($AssetManifest) { $env:GRANITE_E2E_ASSET_MANIFEST = (Resolve-Path -LiteralPath $AssetManifest).Path }
if ($H1Manifest) { $env:GRANITE_E2E_H1_MANIFEST = (Resolve-Path -LiteralPath $H1Manifest).Path }
if ($M1Manifest) { $env:GRANITE_E2E_M1_MANIFEST = (Resolve-Path -LiteralPath $M1Manifest).Path }
if ($Q1Manifest) { $env:GRANITE_E2E_Q1_MANIFEST = (Resolve-Path -LiteralPath $Q1Manifest).Path }
}

$env:GRANITE_E2E_REPOSITORY_ROOT = $repositoryRoot
if ($nativeStage) {
$env:GRANITE_E2E_HANDOFF_ROOT = (Resolve-Path -LiteralPath $HandoffRoot).Path
$env:GRANITE_E2E_NATIVE_RECEIPT_ROOT = if (Test-Path -LiteralPath $NativeReceiptRoot) { (Resolve-Path -LiteralPath $NativeReceiptRoot).Path } else { $NativeReceiptRoot }
}

$resultRoot = Join-Path $repositoryRoot 'TestResults\Audit-20260829\E1'
New-Item -ItemType Directory -Force -Path $resultRoot | Out-Null
$env:GRANITE_E2E_RESULTS_ROOT = $resultRoot
$project = Join-Path $projectRoot 'GraniteEdgeAI.EndToEndTests.csproj'
$appProject = Join-Path $repositoryRoot 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj'

$testBuildStartedUtc = [DateTime]::UtcNow
& $dotnet build $project --configuration Debug --arch x64 --disable-build-servers --no-incremental -m:1
if ($LASTEXITCODE -ne 0) { throw "E1 build failed with exit code $LASTEXITCODE." }

$assembly = Get-ChildItem (Join-Path $projectRoot 'bin') -Filter 'GraniteEdgeAI.EndToEndTests.dll' -Recurse |
    Where-Object {
        $_.FullName -match '[\\/]win-x64[\\/]' -and
        $_.LastWriteTimeUtc -ge $testBuildStartedUtc.AddSeconds(-2)
    } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if (-not $assembly) { throw 'E1 test assembly was not produced.' }
$vswhereCandidates = @((Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'), (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\Installer\vswhere.exe'))
$vswhere = $vswhereCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $vswhere) { throw 'Blocked: vswhere.exe is unavailable; VSTest discovery cannot be authoritative.' }
$visualStudio = (& $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.TestTools.BuildTools -property installationPath).Trim()
$vstest = Get-ChildItem $visualStudio -Filter 'vstest.console.exe' -Recurse | Select-Object -First 1
if (-not $vstest) { throw 'Blocked: vstest.console.exe is unavailable.' }
$runsettings = Join-Path $repositoryRoot 'tests\runsettings\OneWorker.runsettings'

$discoveryOutput = @(& $vstest.FullName $assembly.FullName /ListTests /Platform:x64 "/Settings:$runsettings" 2>&1 | ForEach-Object { "$_" })
$discoveryExitCode = $LASTEXITCODE
$discoveryOutput | Write-Output
if ($discoveryExitCode -ne 0) { throw 'E1 test discovery failed.' }
$discoveredTests = @($discoveryOutput | Where-Object {
    $_ -match '^\s{4,}\S' -and $_ -notmatch '^\s*(The following Tests|Informational|Starting test discovery|Microsoft|Copyright)'
})
if ($discoveredTests.Count -lt 67) {
    throw "E1 test discovery returned only $($discoveredTests.Count) tests; expected at least 67."
}
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')

$filters = @{
    Deterministic = '(TestCategory!=Preflight)&(TestCategory!=NativeSmoke)&(TestCategory!=NativeFailure)&(TestCategory!=NativeAcceptance)&(TestCategory!=NativeRestart)&(TestCategory!=NativeRealModel)'
    Smoke = 'TestCategory=NativeSmoke'
    Failure = 'TestCategory=NativeFailure'
    Acceptance = 'TestCategory=NativeAcceptance'
    Restart = 'TestCategory=NativeRestart'
    RealModel = 'TestCategory=NativeRealModel'
    All = 'TestCategory!=Preflight'
}
$trxName = "E1-$Stage-$runId.trx"
$trxPath = Join-Path $resultRoot $trxName
$summaryPath = Join-Path $resultRoot "E1-$Stage-$runId.json"
$arguments = @($assembly.FullName, '/Platform:x64', "/Settings:$runsettings", "/Logger:trx;LogFileName=$trxName", "/ResultsDirectory:$resultRoot")
if ($filters[$Stage]) { $arguments += "/TestCaseFilter:$($filters[$Stage])" }
if ($Stage -eq 'Deterministic') {
    & $vstest.FullName @arguments
    $deterministicExitCode = $LASTEXITCODE
    Write-StageSummary -TrxPath $trxPath -SummaryPath $summaryPath -StageName $Stage
    exit $deterministicExitCode
}

foreach ($manifest in @($H1Manifest, $M1Manifest, $Q1Manifest)) {
    if (-not $manifest) { throw 'Blocked: H1, M1 and Q1 sanitized evidence manifests are required.' }
}

foreach ($worker in @('A1', 'H1', 'M1', 'Q1', 'F1', 'S1', 'T1')) {
    $handoffPath = Join-Path $env:GRANITE_E2E_HANDOFF_ROOT "$worker.json"
    if (-not (Test-Path -LiteralPath $handoffPath -PathType Leaf)) { throw "Blocked: integrated specialist handoff $worker.json is unavailable." }
    $handoff = Get-Content -Raw -LiteralPath $handoffPath | ConvertFrom-Json
    if ($handoff.workerId -ne $worker -or $handoff.worktreeClean -ne $true -or $handoff.finalTip -notmatch $gitIdentity -or $handoff.finalTree -notmatch $gitIdentity) {
        throw "Blocked: integrated specialist handoff $worker.json is malformed or not clean."
    }
    $actualSpecialistTree = (& git -C $repositoryRoot rev-parse "$($handoff.finalTip)^{tree}").Trim()
    if ($LASTEXITCODE -ne 0 -or $actualSpecialistTree -ne $handoff.finalTree) {
        throw "Blocked: specialist $worker final tip/tree is unavailable or mismatched."
    }
    & git -C $repositoryRoot merge-base --is-ancestor $handoff.finalTip $candidateCommit
    if ($LASTEXITCODE -ne 0) {
        $specialistBase = (& git -C $repositoryRoot merge-base $previousC0Tip $handoff.finalTip).Trim()
        if ($LASTEXITCODE -ne 0 -or $specialistBase -notmatch $gitIdentity) {
            throw "Blocked: accepted $worker work has no verifiable integration base."
        }
        $cherry = @(& git -C $repositoryRoot cherry $candidateCommit $handoff.finalTip $specialistBase)
        if ($LASTEXITCODE -ne 0 -or $cherry.Count -eq 0 -or @($cherry | Where-Object { $_ -notmatch '^- ' }).Count -gt 0) {
            throw "Blocked: exact integrated candidate does not contain all accepted $worker patches."
        }
    }
}

$preflightArguments = @(
    $assembly.FullName,
    '/Platform:x64',
    "/Settings:$runsettings",
    '/Tests:GraniteEdgeAI.EndToEndTests.Tests.PredecessorEvidencePreflightTests.Exact_predecessor_chains_are_closed_and_cleanup_verified'
)
& $vstest.FullName @preflightArguments
if ($LASTEXITCODE -ne 0) { throw 'Blocked: predecessor evidence/native-receipt preflight failed.' }

$r3PreflightArguments = @(
    $assembly.FullName,
    '/Platform:x64',
    "/Settings:$runsettings",
    '/Tests:GraniteEdgeAI.EndToEndTests.Tests.R3ReleaseVetoPreflightTests.Exact_R3_candidate_and_issue_evidence_are_committed_and_pushed'
)
& $vstest.FullName @r3PreflightArguments
if ($LASTEXITCODE -ne 0) { throw 'CHANGES REQUIRED: exact R3 candidate/issue evidence preflight failed.' }

$appBuildStartedUtc = [DateTime]::UtcNow
& $dotnet build $appProject --configuration Debug -p:Platform=x64 -p:GenerateAppxPackageOnBuild=false "-p:DotNetHostPath=$dotnet" --disable-build-servers --no-incremental -m:1
if ($LASTEXITCODE -ne 0) { throw "Candidate app build failed with exit code $LASTEXITCODE." }
$appxRecipe = Get-ChildItem (Join-Path $repositoryRoot 'IBM Granite with TurboQuant (Intel)\obj') -Filter '*.build.appxrecipe' -Recurse |
    Where-Object {
        $_.FullName -match '[\\/]x64[\\/]' -and
        $_.Length -gt 0 -and
        $_.LastWriteTimeUtc -ge $appBuildStartedUtc.AddSeconds(-2)
    } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if (-not $appxRecipe) { throw 'CHANGES REQUIRED: the Debug x64 app build produced no non-empty .build.appxrecipe.' }

if ($Stage -eq 'List') {
    [ordered]@{ stage = 'List'; discovered = $discoveredTests.Count; executed = 0; passed = 0; failed = 0; skipped = 0 } |
        ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $resultRoot "E1-List-$runId.json")
    return
}

$lockPath = 'C:\UCL-AUDIT-NATIVE.lock'
try {
    New-Item -ItemType Directory -Path $lockPath -ErrorAction Stop | Out-Null
} catch {
    $owner = Get-Content -Raw -LiteralPath (Join-Path $lockPath 'owner.json') -ErrorAction SilentlyContinue
    throw "Blocked: the shared native lock is already held. $owner"
}
$lockAcquiredUtc = [DateTime]::UtcNow
$ownerRecord = [ordered]@{
    workerId = 'E1'
    processId = $PID
    processStartUtc = (Get-Process -Id $PID).StartTime.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    worktree = $repositoryRoot
    lockAcquiredUtc = $lockAcquiredUtc.ToString('yyyy-MM-ddTHH:mm:ssZ')
    intendedStage = "E1 $Stage"
}
$ownerRecord | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $lockPath 'owner.json')

$cleanupFailure = $null
try {
    & $vstest.FullName @arguments
    $testExitCode = $LASTEXITCODE
    Write-StageSummary -TrxPath $trxPath -SummaryPath $summaryPath -StageName $Stage
} finally {
    $owner = Get-Content -Raw -LiteralPath (Join-Path $lockPath 'owner.json') | ConvertFrom-Json
    if ($owner.workerId -ne 'E1' -or $owner.processId -ne $PID) { throw 'Native lock ownership changed; preserving ambiguous lock evidence.' }
    $candidateExecutable = [IO.Path]::GetFullPath([string]$candidateRecord.executablePath)
    $candidateRoot = [IO.Path]::GetDirectoryName($candidateExecutable).TrimEnd('\') + '\'
    $remaining = @(Get-CimInstance Win32_Process | Where-Object {
        if (-not $_.ExecutablePath) { return $false }
        try {
            $path = [IO.Path]::GetFullPath([string]$_.ExecutablePath)
            $created = if ($_.CreationDate -is [DateTime]) {
                ([DateTime]$_.CreationDate).ToUniversalTime()
            } else {
                [Management.ManagementDateTimeConverter]::ToDateTime([string]$_.CreationDate).ToUniversalTime()
            }
            return $path.StartsWith($candidateRoot, [StringComparison]::OrdinalIgnoreCase) -and $created -ge $lockAcquiredUtc
        } catch {
            return $false
        }
    })
    if ($remaining.Count -gt 0) {
        $cleanupFailure = "Native descendant cleanup is incomplete for $($remaining.Count) candidate process(es); preserving the lock."
        Set-Content -LiteralPath (Join-Path $lockPath 'cleanup-failure.txt') -Encoding UTF8 -Value $cleanupFailure
    } else {
        Remove-Item -LiteralPath $lockPath -Recurse -Force
    }
}
if ($cleanupFailure) { throw $cleanupFailure }
exit $testExitCode
