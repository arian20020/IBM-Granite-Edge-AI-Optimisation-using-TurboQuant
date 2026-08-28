[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $CandidateManifest,
    [ValidateSet('List', 'Deterministic', 'Smoke', 'Failure', 'Acceptance', 'All')] [string] $Stage = 'List',
    [string] $AssetManifest,
    [string] $H1Manifest,
    [string] $M1Manifest,
    [string] $Q1Manifest,
    [string] $DotNetHostPath = $env:DOTNET_HOST_PATH
)

$ErrorActionPreference = 'Stop'
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
if ((& git -C $repositoryRoot rev-parse "$requiredCommit^{tree}").Trim() -ne $requiredTree) { throw 'E1 frozen source tree mismatch.' }
& git -C $repositoryRoot merge-base --is-ancestor $requiredCommit HEAD
if ($LASTEXITCODE -ne 0) { throw 'E1 branch does not preserve the frozen source ancestry.' }
$candidateCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
$candidateTree = (& git -C $repositoryRoot rev-parse 'HEAD^{tree}').Trim()
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
if ($AssetManifest) { $env:GRANITE_E2E_ASSET_MANIFEST = (Resolve-Path -LiteralPath $AssetManifest).Path }
if ($H1Manifest) { $env:GRANITE_E2E_H1_MANIFEST = (Resolve-Path -LiteralPath $H1Manifest).Path }
if ($M1Manifest) { $env:GRANITE_E2E_M1_MANIFEST = (Resolve-Path -LiteralPath $M1Manifest).Path }
if ($Q1Manifest) { $env:GRANITE_E2E_Q1_MANIFEST = (Resolve-Path -LiteralPath $Q1Manifest).Path }

$resultRoot = Join-Path $repositoryRoot 'TestResults\Audit-20260829\E1'
New-Item -ItemType Directory -Force -Path $resultRoot | Out-Null
$env:GRANITE_E2E_RESULTS_ROOT = $resultRoot
$project = Join-Path $projectRoot 'GraniteEdgeAI.EndToEndTests.csproj'
$appProject = Join-Path $repositoryRoot 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj'
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
if (-not $appxRecipe) { throw 'Blocked: the Debug x64 app build produced no non-empty .build.appxrecipe.' }

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
if ($Stage -eq 'List') { return }

$filters = @{ Deterministic = '(TestCategory!=NativeSmoke)&(TestCategory!=NativeFailure)&(TestCategory!=NativeAcceptance)'; Smoke = 'TestCategory=NativeSmoke'; Failure = 'TestCategory=NativeFailure'; Acceptance = 'TestCategory=NativeAcceptance'; All = '' }
$arguments = @($assembly.FullName, '/Platform:x64', "/Settings:$runsettings", "/Logger:trx;LogFileName=E1-$Stage.trx", "/ResultsDirectory:$resultRoot")
if ($filters[$Stage]) { $arguments += "/TestCaseFilter:$($filters[$Stage])" }
if ($Stage -eq 'Deterministic') {
    & $vstest.FullName @arguments
    exit $LASTEXITCODE
}

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
