[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$WorkspaceRoot,
    [Parameter(Mandatory)][string]$ArchiveRoot,
    [Parameter(Mandatory)][string]$FixtureRoot,
    [Parameter(Mandatory)][ValidateRange(1, [long]::MaxValue)][long]$ExpectedModelLength,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedModelSha256,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedFixtureManifestSha256,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedCommitSha,
    [Parameter(Mandatory)][string]$BuildDirectoryA,
    [Parameter(Mandatory)][string]$BuildDirectoryB,
    [Parameter(Mandatory)][string]$StageDirectoryA,
    [Parameter(Mandatory)][string]$StageDirectoryB,
    [Parameter(Mandatory)][string]$ResultsDirectory,
    [Parameter(Mandatory)][string]$EvidencePath
)

$ErrorActionPreference = 'Stop'
$lease = $null

function Invoke-ExactScript {
    param([string]$Script, [string[]]$Arguments, [string]$ExpectedOutput)
    $output = & powershell.exe -NoLogo -NoProfile -NonInteractive `
        -ExecutionPolicy Bypass -File $Script @Arguments
    if ($LASTEXITCODE -ne 0 -or [string]$output -cne $ExpectedOutput) {
        throw "campaign-script-failed:$Script"
    }
}

function Invoke-DotNetGate {
    param([string[]]$Arguments, [string]$Name)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "campaign-dotnet-failed:$Name" }
}

function Test-EqualOrContained([string]$Candidate, [string]$Root) {
    $normalizedRoot = $Root.TrimEnd('\', '/')
    return $Candidate.TrimEnd('\', '/') -ieq $normalizedRoot -or
        $Candidate.StartsWith($normalizedRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)
}

try {
    $workspace = [IO.Path]::GetFullPath($WorkspaceRoot).TrimEnd('\', '/')
    $archives = [IO.Path]::GetFullPath($ArchiveRoot).TrimEnd('\', '/')
    $fixture = [IO.Path]::GetFullPath($FixtureRoot).TrimEnd('\', '/')
    $buildA = [IO.Path]::GetFullPath($BuildDirectoryA)
    $buildB = [IO.Path]::GetFullPath($BuildDirectoryB)
    $stageA = [IO.Path]::GetFullPath($StageDirectoryA)
    $stageB = [IO.Path]::GetFullPath($StageDirectoryB)
    $results = [IO.Path]::GetFullPath($ResultsDirectory)
    $evidence = [IO.Path]::GetFullPath($EvidencePath)
    $measurements = Join-Path $results 'measurements.json'
    foreach ($output in @($buildA,$buildB,$stageA,$stageB,$results,$evidence)) {
        if ((Test-EqualOrContained $output $archives) -or
            (Test-EqualOrContained $output $fixture)) {
            throw 'campaign-output-overlaps-trusted-input'
        }
    }
    foreach ($absent in @($buildA,$buildB,$stageA,$stageB,$results,(Split-Path -Parent $evidence))) {
        if (Test-Path -LiteralPath $absent) { throw 'campaign-output-not-absent' }
    }
    $actualCommit = [string](& git -C $workspace rev-parse HEAD)
    if ($LASTEXITCODE -ne 0 -or $actualCommit.Trim().ToLowerInvariant() -cne $ExpectedCommitSha -or
        @(& git -C $workspace status --porcelain).Count -ne 0) {
        throw 'campaign-checkout-not-immutable'
    }
    if ($env:RUNNER_ARCH -cne 'X64') { throw 'campaign-runner-not-x64' }
    $manufacturer = [string](Get-CimInstance Win32_Processor |
        Select-Object -First 1 -ExpandProperty Manufacturer)
    if ($manufacturer -notmatch 'GenuineIntel|Intel') { throw 'campaign-cpu-not-intel' }

    Import-Module (Join-Path $PSScriptRoot 'OpenVinoTrustedInputLease.psm1') -Force
    [string[]]$roots = @($archives, $fixture)
    $snapshot = @(Get-OpenVinoTrustedInputSnapshot -Roots $roots)
    $lease = New-OpenVinoTrustedInputLease -Snapshot $snapshot

    Invoke-ExactScript (Join-Path $PSScriptRoot 'Test-OpenVinoTrustedInputs.ps1') `
        @('-WorkspaceRoot',$workspace,'-ArchiveRoot',$archives,'-FixtureRoot',$fixture,
          '-ExpectedModelLength',[string]$ExpectedModelLength,
          '-ExpectedModelSha256',$ExpectedModelSha256,
          '-ExpectedFixtureManifestSha256',$ExpectedFixtureManifestSha256) `
        'trusted_inputs_valid'
    Invoke-ExactScript (Join-Path $PSScriptRoot 'Test-OpenVinoDependencyLocks.ps1') `
        @('-ClosureDirectory',$archives,'-Scope','Official') 'dependency_lock_valid'
    Invoke-ExactScript (Join-Path $PSScriptRoot 'Test-OpenVinoGenAiFixture.ps1') `
        @('-FixtureRoot',$fixture) 'fixture_valid'
    Assert-OpenVinoTrustedInputLeaseUnchanged -Lease $lease

    New-Item -ItemType Directory -Path $results | Out-Null
    foreach ($closure in @(
        @{ Build = $buildA; Stage = $stageA },
        @{ Build = $buildB; Stage = $stageB })) {
        & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
            -File (Join-Path $PSScriptRoot 'Build-OpenVinoOfficialWorker.ps1') `
            -OfficialArchiveDirectory $archives -FixtureRoot $fixture `
            -ExpectedFixtureManifestSha256 $ExpectedFixtureManifestSha256 `
            -BuildDirectory $closure.Build -StageDirectory $closure.Stage
        if ($LASTEXITCODE -ne 0) { throw 'campaign-native-build-failed' }
        Assert-OpenVinoTrustedInputLeaseUnchanged -Lease $lease
        Assert-OpenVinoTrustedInputSnapshot -Roots $roots -Expected $snapshot
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $installation = & $vswhere -latest -products * `
        -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    $ctest = Join-Path $installation 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/ctest.exe'
    & $ctest --test-dir (Join-Path $buildA 'native') -C Release --output-on-failure `
        --output-junit (Join-Path $results 'native.xml')
    if ($LASTEXITCODE -ne 0) { throw 'campaign-native-tests-failed' }
    Invoke-ExactScript (Join-Path $PSScriptRoot 'Test-OpenVinoNativeJUnit.ps1') `
        @('-ResultsPath',(Join-Path $results 'native.xml'),'-MinimumCount','7') `
        'native_junit_valid:7'

    $env:GRANITE_OPENVINO_OFFICIAL_WORKER_STAGE = $stageB
    Invoke-DotNetGate @('test','--project',
        'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj',
        '--configuration','Release','-p:Platform=x64','--minimum-expected-tests','60',
        '--results-directory',$results,'--report-trx','--report-trx-filename','contracts.trx') 'contracts'
    Invoke-DotNetGate @('test','--project',
        'tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj',
        '--configuration','Release','-p:Platform=x64','--minimum-expected-tests','177',
        '--results-directory',$results,'--report-trx','--report-trx-filename','static-inspection.trx') 'static'
    Invoke-DotNetGate @('test','--project',
        'tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj',
        '--configuration','Release','-p:Platform=x64','--filter',
        'FullyQualifiedName~OpenVinoPromptAdapterTests|FullyQualifiedName~OpenVinoRouteStateMachineTests',
        '--minimum-expected-tests','35','--results-directory',$results,'--report-trx',
        '--report-trx-filename','app-adapter.trx') 'adapter'
    Invoke-DotNetGate @('test','--project',
        'tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj',
        '--configuration','Release','-p:Platform=x64','--filter',
        'FullyQualifiedName~OpenVinoWorkerPackagingTargetTests','--minimum-expected-tests','5',
        '--results-directory',$results,'--report-trx','--report-trx-filename','package-tamper.trx') 'tamper'
    Assert-OpenVinoTrustedInputLeaseUnchanged -Lease $lease

    $env:OPENVINO_OFFICIAL_WORKER_STAGE_A = $stageA
    $env:OPENVINO_OFFICIAL_WORKER_STAGE_B = $stageB
    $env:OPENVINO_UCL_CONTROLLED_FIXTURE_ROOT = $fixture
    $env:OPENVINO_UCL_CONTROLLED_FIXTURE_MANIFEST_SHA256 = $ExpectedFixtureManifestSha256
    Invoke-DotNetGate @('test','--project',
        'tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/GraniteEdgeAI.OpenVino.WorkerClient.Tests.csproj',
        '--configuration','Release','-p:Platform=x64','--minimum-expected-tests','13',
        '--results-directory',$results,'--report-trx','--report-trx-filename','worker-client.trx') 'client'
    $durations = New-Object 'System.Collections.Generic.List[long]'
    foreach ($run in 1..3) {
        $watch = [Diagnostics.Stopwatch]::StartNew()
        Invoke-DotNetGate @('test','--project',
            'tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests.csproj',
            '--configuration','Release','-p:Platform=x64','--minimum-expected-tests','41',
            '--results-directory',$results,'--report-trx','--report-trx-filename',
            "process-containment-$run.trx") "process-$run"
        $watch.Stop()
        $durations.Add($watch.ElapsedMilliseconds)
        Invoke-ExactScript (Join-Path $PSScriptRoot 'Test-OpenVinoUclFixtureConsumption.ps1') `
            @('-ResultsPath',(Join-Path $results "process-containment-$run.trx"),
              '-ExpectedFixtureManifestSha256',$ExpectedFixtureManifestSha256) `
            'ucl_fixture_consumption_valid'
        Assert-OpenVinoTrustedInputLeaseUnchanged -Lease $lease
    }
    Copy-Item -LiteralPath (Join-Path $results 'process-containment-3.trx') `
        -Destination (Join-Path $results 'process-containment.trx')

    $manifestSha = (Get-FileHash -LiteralPath (Join-Path $stageB 'worker-manifest.json') `
        -Algorithm SHA256).Hash.ToLowerInvariant()
    & msbuild (Join-Path $workspace 'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj') `
        /restore /target:Build /maxCpuCount /verbosity:minimal /property:Configuration=Release `
        /property:Platform=x64 /property:RuntimeIdentifier=win-x64 /property:PublishProfile= `
        /property:PublishTrimmed=false /property:PublishReadyToRun=false `
        /property:AppxPackageSigningEnabled=false /property:GenerateAppxPackageOnBuild=false `
        /property:OpenVinoOfficialWorkerStageDirectory=$stageB `
        /property:OpenVinoOfficialWorkerManifestSha256=$manifestSha
    if ($LASTEXITCODE -ne 0) { throw 'campaign-app-build-failed' }

    Invoke-ExactScript (Join-Path $PSScriptRoot 'New-OpenVinoEvidenceMeasurements.ps1') `
        @('-TestResultsDirectory',$results,'-NativeResultsPath',(Join-Path $results 'native.xml'),
          '-PerformanceDurationsMilliseconds',($durations -join ','),
          '-MeasurementsPath',$measurements) 'openvino_measurements_created'
    & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'Invoke-OpenVinoOfficialEvidence.ps1') `
        -ExpectedCommitSha $ExpectedCommitSha -OfficialArchiveDirectory $archives `
        -WorkerStageDirectory $stageB -FixtureRoot $fixture -CpuVendor Intel `
        -MeasurementsPath $measurements -EvidencePath $evidence
    if ($LASTEXITCODE -ne 0) { throw 'campaign-evidence-failed' }

    foreach ($stage in @($stageA,$stageB)) {
        Invoke-ExactScript (Join-Path $PSScriptRoot 'Test-OpenVinoOfficialWorkerManifest.ps1') `
            @('-StageDirectory',$stage) 'worker_manifest_valid'
    }
    Invoke-ExactScript (Join-Path $PSScriptRoot 'Test-OpenVinoDependencyLocks.ps1') `
        @('-ClosureDirectory',$archives,'-Scope','Official') 'dependency_lock_valid'
    Invoke-ExactScript (Join-Path $PSScriptRoot 'Test-OpenVinoGenAiFixture.ps1') `
        @('-FixtureRoot',$fixture) 'fixture_valid'
    Invoke-ExactScript (Join-Path $PSScriptRoot 'Test-OpenVinoEvidenceArtifactSet.ps1') `
        @('-EvidencePath',$evidence) 'evidence_artifact_set_valid'
    Assert-OpenVinoTrustedInputLeaseUnchanged -Lease $lease
    Assert-OpenVinoTrustedInputSnapshot -Roots $roots -Expected $snapshot

    $lease.Dispose()
    $lease = $null
    [Console]::Out.WriteLine('ucl_campaign_passed')
    exit 0
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    [Console]::Out.WriteLine('ucl_campaign_failed')
    exit 1
}
finally {
    if ($null -ne $lease) { $lease.Dispose() }
}
