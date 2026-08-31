[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [ValidateNotNullOrEmpty()] [string] $CandidateManifest,
    [Parameter(Mandatory = $true)] [ValidateNotNullOrEmpty()] [string] $IntegrationCandidateRemote,
    [Parameter(Mandatory = $true)] [ValidateNotNullOrEmpty()] [string] $IntegrationCandidateRemoteRef,
    [Parameter(Mandatory = $true)] [ValidateNotNullOrEmpty()] [string] $R3ClosureManifest,
    [Parameter(Mandatory = $true)] [ValidateNotNullOrEmpty()] [string] $R3ClosureRelativePath,
    [Parameter(Mandatory = $true)] [ValidatePattern('^[0-9a-f]{40}$')] [string] $CandidateCommit,
    [Parameter(Mandatory = $true)] [ValidatePattern('^[0-9a-f]{40}$')] [string] $CandidateTree,
    [Parameter(Mandatory = $true)] [ValidatePattern('^[0-9a-f]{40}$')] [string] $ImplementationCommit,
    [Parameter(Mandatory = $true)] [ValidatePattern('^[0-9a-f]{40}$')] [string] $ImplementationTree,
    [ValidateSet('List', 'Evidence', 'Deterministic', 'Smoke', 'Failure', 'Acceptance', 'All')] [string] $Stage = 'List',
    [string] $AssetManifest,
    [string] $H1Manifest,
    [string] $M1Manifest,
    [string] $Q1Manifest,
    [string] $DotNetHostPath = $env:DOTNET_HOST_PATH,
    [string] $NativeLockPath = 'C:\UCL-AUDIT-NATIVE.lock'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repositoryRoot = (Resolve-Path (Join-Path $projectRoot '..\..\..')).Path
Import-Module (Join-Path $PSScriptRoot 'E1EvidenceSupport.psm1') -Force
foreach ($name in @('GRANITE_E2E_ASSET_MANIFEST', 'GRANITE_E2E_H1_MANIFEST', 'GRANITE_E2E_M1_MANIFEST', 'GRANITE_E2E_Q1_MANIFEST')) {
    Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
}
$dotnet = if ($DotNetHostPath) {
    (Resolve-Path -LiteralPath $DotNetHostPath).Path
} else {
    Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
}
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) { throw 'The x64 dotnet host is unavailable.' }
$requiredCommit = '4748fe04f19afdf6b27c4c12502b84db325e7294'
$requiredTree = 'fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91'
$issuedCandidateCommit = 'b5d2cd34c57368efb9b122cddf16c2ffa2d3895e'
$issuedCandidateTree = 'a3e4d82095caa9688f30d2463fa5971c788fd33f'

function Invoke-CheckedGit([string[]] $Arguments) {
    $output = @(& git -C $repositoryRoot @Arguments 2>&1 | ForEach-Object { "$_" })
    if ($LASTEXITCODE -ne 0) { throw "Git validation failed: git $($Arguments -join ' ')" }
    return ($output -join "`n").Trim()
}

function Assert-ExactJsonProperties([object] $Value, [string[]] $Required, [string[]] $Optional, [string] $Name) {
    $actual = @($Value.PSObject.Properties.Name)
    $allowed = @($Required) + @($Optional)
    if (@($actual | Where-Object { $_ -notin $allowed }).Count -gt 0 -or
        @($Required | Where-Object { $_ -notin $actual }).Count -gt 0) {
        throw "Blocked: $Name has missing or unexpected properties."
    }
}

if ($CandidateCommit -ne $issuedCandidateCommit -or $CandidateTree -ne $issuedCandidateTree) {
    throw 'Candidate commit/tree must equal the immutable E1 v2 issued candidate.'
}
if ($IntegrationCandidateRemote -ne 'origin' -or
    $IntegrationCandidateRemoteRef -ne 'refs/heads/integration/ucl-r4-e1-issued-base-v2') {
    throw 'Candidate remote/ref must equal the dispatched immutable origin ref.'
}
if ((Invoke-CheckedGit @('rev-parse', "$requiredCommit^{tree}")) -ne $requiredTree) { throw 'E1 frozen source tree mismatch.' }
if ((Invoke-CheckedGit @('rev-parse', "$CandidateCommit^{tree}")) -ne $CandidateTree) { throw 'Issued candidate commit/tree mismatch.' }
if ((Invoke-CheckedGit @('rev-parse', "$ImplementationCommit^{tree}")) -ne $ImplementationTree) { throw 'Implementation subject commit/tree mismatch.' }
[void](Invoke-CheckedGit @('merge-base', '--is-ancestor', $requiredCommit, $CandidateCommit))
[void](Invoke-CheckedGit @('merge-base', '--is-ancestor', $CandidateCommit, $ImplementationCommit))
[void](Invoke-CheckedGit @('merge-base', '--is-ancestor', $ImplementationCommit, 'HEAD'))
$advertised = Invoke-CheckedGit @('ls-remote', '--exit-code', $IntegrationCandidateRemote, $IntegrationCandidateRemoteRef)
$advertisedCommit = ($advertised -split '\s+')[0]
if ($advertisedCommit -ne $CandidateCommit) { throw 'Issued candidate remote ref does not advertise the exact candidate commit.' }

if ([IO.Path]::IsPathRooted($R3ClosureRelativePath) -or
    $R3ClosureRelativePath.Contains('\') -or
    @($R3ClosureRelativePath.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -gt 0) {
    throw 'R3 closure relative path is not canonical repository-relative syntax.'
}
$closurePath = (Resolve-Path -LiteralPath $R3ClosureManifest).Path
$expectedClosurePath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $R3ClosureRelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar)))
if (-not $closurePath.Equals($expectedClosurePath, [StringComparison]::OrdinalIgnoreCase)) { throw 'R3 closure manifest path/relative-path relationship is invalid.' }
$candidateClosureSpec = '{0}:{1}' -f $CandidateCommit, $R3ClosureRelativePath
$candidateClosureBlob = Invoke-CheckedGit @('rev-parse', $candidateClosureSpec)
$workingClosureBlob = Invoke-CheckedGit @('hash-object', '--', $closurePath)
if ($candidateClosureBlob -ne $workingClosureBlob) { throw 'R3 closure manifest differs from the immutable candidate blob.' }

$candidateManifestPath = (Resolve-Path -LiteralPath $CandidateManifest).Path
$candidateRecord = Get-Content -Raw -LiteralPath $candidateManifestPath | ConvertFrom-Json
Assert-ExactJsonProperties $candidateRecord @('schemaVersion', 'sourceCommit', 'sourceTree', 'packageFamilyName', 'applicationId', 'executablePath', 'executableSha256', 'executableBytes') @() 'candidate manifest'
$candidateExecutablePath = [string]$candidateRecord.executablePath
$candidateExecutableFullyQualified = [IO.Path]::IsPathRooted($candidateExecutablePath) -and
    $candidateExecutablePath -notmatch '^[\\/](?![\\/])' -and
    $candidateExecutablePath -notmatch '^[A-Za-z]:(?![\\/])'
if ($candidateRecord.schemaVersion -ne 1 -or
    $candidateRecord.sourceCommit -ne $CandidateCommit -or $candidateRecord.sourceTree -ne $CandidateTree -or
    [string]$candidateRecord.packageFamilyName -notmatch '^[A-Za-z0-9._-]{3,255}$' -or
    [string]$candidateRecord.applicationId -notmatch '^[A-Za-z0-9._-]{1,255}$' -or
    -not $candidateExecutableFullyQualified -or
    [string]$candidateRecord.executableSha256 -notmatch '^[0-9a-f]{64}$' -or
    [long]$candidateRecord.executableBytes -le 0) {
    throw 'The candidate manifest schema or immutable identity is invalid.'
}
$candidateExecutable = Get-Item -LiteralPath ([string]$candidateRecord.executablePath) -ErrorAction Stop
if ($candidateExecutable.PSIsContainer -or $candidateExecutable.Length -ne [long]$candidateRecord.executableBytes -or
    ($candidateExecutable.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
    (Get-FileHash -Algorithm SHA256 -LiteralPath $candidateExecutable.FullName).Hash.ToLowerInvariant() -ne $candidateRecord.executableSha256) {
    throw 'The candidate manifest executable hash, bytes, or file type is invalid.'
}

$env:GRANITE_E2E_REPOSITORY_ROOT = $repositoryRoot
$env:GRANITE_E2E_CANDIDATE_COMMIT = $CandidateCommit
$env:GRANITE_E2E_CANDIDATE_TREE = $CandidateTree
$env:GRANITE_E2E_CANDIDATE_REMOTE = $IntegrationCandidateRemote
$env:GRANITE_E2E_CANDIDATE_REMOTE_REF = $IntegrationCandidateRemoteRef
$env:GRANITE_E2E_R3_CLOSURE_MANIFEST = $closurePath
$env:GRANITE_E2E_R3_CLOSURE_RELATIVE_PATH = $R3ClosureRelativePath
$env:GRANITE_E2E_IMPLEMENTATION_COMMIT = $ImplementationCommit
$env:GRANITE_E2E_IMPLEMENTATION_TREE = $ImplementationTree

$env:GRANITE_E2E_CANDIDATE_MANIFEST = $candidateManifestPath
if ($AssetManifest) { $env:GRANITE_E2E_ASSET_MANIFEST = (Resolve-Path -LiteralPath $AssetManifest).Path }
if ($H1Manifest) { $env:GRANITE_E2E_H1_MANIFEST = (Resolve-Path -LiteralPath $H1Manifest).Path }
if ($M1Manifest) { $env:GRANITE_E2E_M1_MANIFEST = (Resolve-Path -LiteralPath $M1Manifest).Path }
if ($Q1Manifest) { $env:GRANITE_E2E_Q1_MANIFEST = (Resolve-Path -LiteralPath $Q1Manifest).Path }

if ($Stage -eq 'Evidence') {
    Assert-E1ImplementationBoundary -RepositoryRoot $repositoryRoot -ImplementationCommit $ImplementationCommit
    $evidenceDotNet = Get-E1TrustedDotNet
    $evidenceMSBuild = Get-E1TrustedMSBuild
    $evidenceSdkRoot = Get-E1TrustedMSBuildSdkRoot -MSBuildPath $evidenceMSBuild
    $dotnetItem = Get-Item -LiteralPath $evidenceDotNet -Force -ErrorAction Stop
    if ($dotnetItem.PSIsContainer -or ($dotnetItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        $dotnetItem.Name -ne 'dotnet.exe') { throw 'The approved x64 dotnet host is invalid.' }
    $project = Join-Path $projectRoot 'GraniteEdgeAI.EndToEndTests.csproj'
    # Keep generated obj/bin trees beneath the SDK-excluded project bin subtree.
    # A repository-level TestResults root is not excluded by the default Compile
    # globs and would cause generated assembly attributes to be compiled twice.
    $buildRoot = Join-Path (Split-Path -Parent $project) "bin\E1-Evidence\$([Guid]::NewGuid().ToString('N'))"
    $buildOutputRoot = Join-Path $buildRoot 'bin'
    $buildIntermediateRoot = Join-Path $buildRoot 'obj'
    [void](New-E1SafeDirectoryChain -Path $buildOutputRoot -RepositoryRoot $repositoryRoot)
    [void](New-E1SafeDirectoryChain -Path $buildIntermediateRoot -RepositoryRoot $repositoryRoot)
    $buildStartedUtc = [DateTime]::UtcNow
    $buildEnvironmentNames = @(@(
        'MSBuildSDKsPath', 'MSBuildExtensionsPath', 'MSBuildExtensionsPath32', 'MSBuildExtensionsPath64',
        'MSBuildUserExtensionsPath', 'MSBUILD_EXE_PATH', 'MSBUILDUSESERVER', 'MSBUILDLEGACYEXTENSIONSPATH',
        'DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR', 'DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER',
        'DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR', 'DOTNET_ROOT', 'DOTNET_ROOT_X64', 'DOTNET_ROOT(x86)',
        'DOTNET_HOST_PATH', 'DOTNET_ADDITIONAL_DEPS', 'DOTNET_SHARED_STORE', 'DOTNET_STARTUP_HOOKS', 'DOTNET_MULTILEVEL_LOOKUP',
        'DOTNET_CLI_HOME', 'NUGET_PLUGIN_PATHS', 'NUGET_CREDENTIALPROVIDERS_PATH', 'NUGET_PACKAGES',
        'NUGET_HTTP_CACHE_PATH', 'NUGET_FALLBACK_PACKAGES', 'RestoreSources'
    ) + @(Get-E1UnsafeManagedEnvironmentNames) | Sort-Object -Unique)
    $savedBuildEnvironment = @{}
    foreach ($name in $buildEnvironmentNames) {
        $savedBuildEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    try {
        $env:MSBuildSDKsPath = $evidenceSdkRoot
        $env:DOTNET_ROOT = [IO.Path]::GetDirectoryName($dotnetItem.FullName)
        $env:DOTNET_MULTILEVEL_LOOKUP = '0'
        & $dotnetItem.FullName $evidenceMSBuild $project /restore /t:Rebuild /m:1 /p:Configuration=Debug /p:RuntimeIdentifier=win-x64 "/p:SourceRevisionId=$ImplementationCommit" "/p:BaseOutputPath=$buildOutputRoot\" "/p:BaseIntermediateOutputPath=$buildIntermediateRoot\" "/p:MSBuildProjectExtensionsPath=$buildIntermediateRoot\" /v:minimal
        if ($LASTEXITCODE -ne 0) { throw "E1 evidence build failed with exit code $LASTEXITCODE." }
    }
    catch {
        if (Test-Path -LiteralPath $buildRoot) { Remove-E1SafeDirectoryTree -Path $buildRoot -RepositoryRoot $repositoryRoot }
        throw
    }
    finally {
        foreach ($name in $buildEnvironmentNames) {
            [Environment]::SetEnvironmentVariable($name, $savedBuildEnvironment[$name], 'Process')
        }
    }
    $expectedOutputRoot = $buildOutputRoot
    [void](Assert-E1SafeDirectoryChain -Path $expectedOutputRoot -RepositoryRoot $repositoryRoot)
    $assemblyCandidates = @(Get-ChildItem $expectedOutputRoot -Filter 'GraniteEdgeAI.EndToEndTests.dll' -Recurse -File |
        Where-Object { $_.FullName -match '[\\/]net8\.0-windows10\.0\.19041\.0[\\/]win-x64[\\/]' -and $_.LastWriteTimeUtc -ge $buildStartedUtc.AddSeconds(-2) })
    if ($assemblyCandidates.Count -ne 1) { throw 'Evidence build did not produce exactly one fresh expected E1 assembly.' }
    $assemblyPath = Assert-E1EvidenceAssembly -Path $assemblyCandidates[0].FullName -ExpectedOutputRoot $expectedOutputRoot -ImplementationCommit $ImplementationCommit -FreshSinceUtc $buildStartedUtc
    $vstestExecutable = Get-E1TrustedVSTest
    $evidenceResultRoot = Join-Path $repositoryRoot 'TestResults\Audit-20260830\E1-Evidence'
    [void](New-E1SafeDirectoryChain -Path $evidenceResultRoot -RepositoryRoot $repositoryRoot)
    $authoritative = @(
        @('GraniteEdgeAI.EndToEndTests.Tests.R4TwoPhaseIssueEvidenceVerifierTests', 'Exact_schema_v4_candidate_closure_and_catalog_are_committed_and_pushed', 'Preflight'),
        @('GraniteEdgeAI.EndToEndTests.Tests.R4TwoPhaseIssueEvidenceVerifierTests', 'Exact_E1_post_acceptance_evidence_is_candidate_bound_and_fail_closed', 'PostAcceptance')
    )
    $runtimeEnvironmentNames = @(Get-E1UnsafeManagedEnvironmentNames)
    $savedRuntimeEnvironment = @{}
    foreach ($name in $runtimeEnvironmentNames) {
        $savedRuntimeEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    try {
        foreach ($test in $authoritative) {
            $trxName = "E1-$($test[2])-$([Guid]::NewGuid().ToString('N')).trx"
            $trxPath = Join-Path $evidenceResultRoot $trxName
            $startedUtc = [DateTime]::UtcNow
            $arguments = New-E1AuthoritativeVSTestArguments -AssemblyPath $assemblyPath -ExpectedClass $test[0] -ExpectedMethod $test[1] -ResultsRoot $evidenceResultRoot -TrxFileName $trxName
            & $vstestExecutable @arguments
            if ($LASTEXITCODE -ne 0) { throw "Authoritative $($test[2]) evaluator failed with exit code $LASTEXITCODE." }
            Assert-E1AuthoritativeTrx -Path $trxPath -ExpectedResultsRoot $evidenceResultRoot -RepositoryRoot $repositoryRoot -ExpectedClass $test[0] -ExpectedMethod $test[1] -InvocationStartedUtc $startedUtc
        }
    }
    finally {
        foreach ($name in $runtimeEnvironmentNames) {
            [Environment]::SetEnvironmentVariable($name, $savedRuntimeEnvironment[$name], 'Process')
        }
        if (Test-Path -LiteralPath $buildRoot) { Remove-E1SafeDirectoryTree -Path $buildRoot -RepositoryRoot $repositoryRoot }
    }
    return
}

if ($Stage -in @('Smoke', 'Failure', 'Acceptance', 'All')) {
    $nativeInputs = [ordered]@{
        Asset = $AssetManifest
        H1 = $H1Manifest
        M1 = $M1Manifest
        Q1 = $Q1Manifest
    }
    foreach ($entry in $nativeInputs.GetEnumerator()) {
        if ([string]::IsNullOrWhiteSpace([string]$entry.Value)) {
            throw "Blocked: native stage requires the exact $($entry.Key) manifest before build or lock."
        }
    }
    $asset = Get-Content -Raw -LiteralPath $env:GRANITE_E2E_ASSET_MANIFEST | ConvertFrom-Json
    Assert-ExactJsonProperties $asset @('schemaVersion', 'assets') @() 'asset manifest'
    if ($asset.schemaVersion -ne 1 -or -not $asset.assets -or @($asset.assets).Count -eq 0) {
        throw 'Blocked: asset manifest schema or asset inventory is invalid.'
    }
    $assetIds = @{}
    foreach ($assetRow in @($asset.assets)) {
        Assert-ExactJsonProperties $assetRow @('id', 'route', 'sha256', 'bytes') @() 'asset row'
        if ([string]::IsNullOrWhiteSpace([string]$assetRow.id) -or $assetIds.ContainsKey([string]$assetRow.id) -or
            $assetRow.route -notin @('gguf', 'openvino') -or
            [string]$assetRow.sha256 -notmatch '^[0-9a-f]{64}$' -or [long]$assetRow.bytes -lt 0) {
            throw 'Blocked: asset manifest contains a duplicate id or invalid route, digest, or byte count.'
        }
        $assetIds[[string]$assetRow.id] = $true
    }
    $requiredProducerKinds = @{
        H1 = @('hardwareSnapshot', 'availableMemory', 'safetyBudget')
        M1 = @('modelSource', 'modelInspectionResult', 'modelInspectionHandoff')
        Q1 = @('optimizationPlan', 'executionResult', 'chatTarget', 'exportTarget')
    }
    foreach ($worker in @('H1', 'M1', 'Q1')) {
        $manifestVariable = "GRANITE_E2E_${worker}_MANIFEST"
        $producer = Get-Content -Raw -LiteralPath ([Environment]::GetEnvironmentVariable($manifestVariable)) | ConvertFrom-Json
        Assert-ExactJsonProperties $producer @('schemaVersion', 'workerId', 'frozenSourceCommit', 'evidenceSubjectCommit', 'evidenceSubjectTree', 'createdAtUtc', 'route', 'evidenceStatus', 'report', 'inputs', 'outputs', 'commands', 'blockers', 'nonClaims') @() "$worker producer manifest"
        if ($producer.schemaVersion -ne 2 -or $producer.workerId -ne $worker -or $producer.frozenSourceCommit -ne $requiredCommit -or
            [string]$producer.evidenceSubjectCommit -notmatch '^[0-9a-f]{40}$' -or
            [string]$producer.evidenceSubjectTree -notmatch '^[0-9a-f]{40}$' -or
            @($producer.inputs).Count -eq 0 -or @($producer.outputs).Count -eq 0 -or @($producer.commands).Count -eq 0 -or
            (Invoke-CheckedGit @('rev-parse', "$($producer.evidenceSubjectCommit)^{tree}")) -ne $producer.evidenceSubjectTree) {
            throw "Blocked: $worker producer evidence identity is invalid."
        }
        [void](Invoke-CheckedGit @('merge-base', '--is-ancestor', [string]$producer.evidenceSubjectCommit, $CandidateCommit))
        $producerKinds = @(@($producer.inputs) + @($producer.outputs) | ForEach-Object { [string]$_.kind })
        foreach ($requiredKind in $requiredProducerKinds[$worker]) {
            if ($requiredKind -notin $producerKinds) { throw "Blocked: $worker producer evidence is missing kind '$requiredKind'." }
        }
        foreach ($command in @($producer.commands)) {
            Assert-ExactJsonProperties $command @('id', 'exitCode', 'discovered', 'executed', 'passed', 'failed', 'skipped', 'disposition') @('resultSha256') "$worker command"
            $discovered = [long]$command.discovered
            $executed = [long]$command.executed
            $passed = [long]$command.passed
            $failed = [long]$command.failed
            $skipped = [long]$command.skipped
            if ($discovered -lt 0 -or $executed -lt 0 -or $passed -lt 0 -or $failed -lt 0 -or $skipped -lt 0 -or
                $discovered -ne $executed -or $executed -ne ($passed + $failed + $skipped)) {
                throw "Blocked: $worker producer command arithmetic is invalid."
            }
        }
    }
}

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
$vstest = Get-Item -LiteralPath (Get-E1TrustedVSTest) -Force
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

$filters = @{ Deterministic = '(TestCategory!=NativeSmoke)&(TestCategory!=NativeFailure)&(TestCategory!=NativeAcceptance)&(TestCategory!=Preflight)&(TestCategory!=PostAcceptance)&(TestCategory!=AuthoritativeIntegration)'; Smoke = 'TestCategory=NativeSmoke'; Failure = 'TestCategory=NativeFailure'; Acceptance = 'TestCategory=NativeAcceptance'; All = '' }
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

$lockPath = $NativeLockPath
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
