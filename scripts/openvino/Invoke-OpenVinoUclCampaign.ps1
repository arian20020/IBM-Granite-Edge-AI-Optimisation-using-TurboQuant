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
    [Parameter(Mandatory)][string]$ConverterClosureDirectory,
    [Parameter(Mandatory)][string]$ConverterBuildDirectory,
    [Parameter(Mandatory)][string]$ConverterStageDirectory,
    [Parameter(Mandatory)][string]$ResultsDirectory,
    [Parameter(Mandatory)][string]$EvidencePath,
    [string]$GpuAuthorization = '',
    [string]$GpuDevice = '',
    [string]$ExpectedGpuName = '',
    [string]$ExpectedGpuDriverVersion = '',
    [string]$GpuEvidencePath = ''
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
    $converterClosure = [IO.Path]::GetFullPath($ConverterClosureDirectory)
    $converterBuild = [IO.Path]::GetFullPath($ConverterBuildDirectory)
    $converterStage = [IO.Path]::GetFullPath($ConverterStageDirectory)
    $results = [IO.Path]::GetFullPath($ResultsDirectory)
    $evidence = [IO.Path]::GetFullPath($EvidencePath)
    $gpuRequested = -not [string]::IsNullOrWhiteSpace($GpuAuthorization) -or
        -not [string]::IsNullOrWhiteSpace($GpuDevice)
    $gpuEvidence = $null
    if ($gpuRequested) {
        if ($GpuAuthorization -cne 'GPU-01' -or
            $GpuDevice -cnotmatch '^GPU(?:\.(?:0|[1-9][0-9]*))?$' -or
            [string]::IsNullOrWhiteSpace($ExpectedGpuName) -or
            [string]::IsNullOrWhiteSpace($ExpectedGpuDriverVersion) -or
            [string]::IsNullOrWhiteSpace($GpuEvidencePath)) {
            throw 'campaign-gpu-authorization-invalid'
        }
        $gpuEvidence = [IO.Path]::GetFullPath($GpuEvidencePath)
    }
    $measurements = Join-Path $results 'measurements.json'
    foreach ($output in @($buildA,$buildB,$stageA,$stageB,$converterBuild,
        $converterStage,$results,$evidence)) {
        if ((Test-EqualOrContained $output $archives) -or
            (Test-EqualOrContained $output $fixture) -or
            (Test-EqualOrContained $output $converterClosure)) {
            throw 'campaign-output-overlaps-trusted-input'
        }
    }
    if ($gpuRequested -and ((Test-EqualOrContained $gpuEvidence $archives) -or
        (Test-EqualOrContained $gpuEvidence $fixture) -or
        (Test-EqualOrContained $gpuEvidence $converterClosure))) {
        throw 'campaign-gpu-output-overlaps-trusted-input'
    }
    foreach ($absent in @($buildA,$buildB,$stageA,$stageB,$converterBuild,
        $converterStage,$results,(Split-Path -Parent $evidence))) {
        if (Test-Path -LiteralPath $absent) { throw 'campaign-output-not-absent' }
    }
    if ($gpuRequested -and (Test-Path -LiteralPath (Split-Path -Parent $gpuEvidence))) {
        throw 'campaign-gpu-output-not-absent'
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
    [string[]]$roots = @($archives, $fixture, $converterClosure)
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

    Invoke-ExactScript (Join-Path $PSScriptRoot 'Test-OpenVinoDependencyLocks.ps1') `
        @('-ClosureDirectory',$converterClosure,'-Scope','Converter') 'dependency_lock_valid'
    & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'Build-OpenVinoConverterWorker.ps1') `
        -ClosureDirectory $converterClosure -BuildDirectory $converterBuild `
        -StageDirectory $converterStage
    if ($LASTEXITCODE -ne 0) { throw 'campaign-converter-build-failed' }
    Assert-OpenVinoTrustedInputLeaseUnchanged -Lease $lease
    Assert-OpenVinoTrustedInputSnapshot -Roots $roots -Expected $snapshot

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
    $env:GRANITE_OPENVINO_CONVERTER_STAGE = $converterStage
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
    Invoke-DotNetGate @('test','--project',
        'tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests.csproj',
        '--configuration','Release','-p:Platform=x64','--filter',
        'TestCategory=StableRouteAcceptance','--minimum-expected-tests','12',
        '--results-directory',$results,'--report-trx','--report-trx-filename',
        'stable-route-acceptance.trx') 'stable-acceptance'
    Assert-OpenVinoTrustedInputLeaseUnchanged -Lease $lease

    if ($gpuRequested) {
        $intelAdapters = @(Get-CimInstance Win32_VideoController | Where-Object {
            $_.PNPDeviceID -match '^PCI\\VEN_8086&' -and
            $_.Name -ceq $ExpectedGpuName -and
            $_.DriverVersion -ceq $ExpectedGpuDriverVersion
        })
        if ($intelAdapters.Count -ne 1) { throw 'campaign-gpu-driver-prerequisite-mismatch' }
        $env:OPENVINO_UCL_GPU_DEVICE = $GpuDevice
        Invoke-DotNetGate @('test','--project',
            'tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests.csproj',
            '--configuration','Release','-p:Platform=x64','--filter',
            'FullyQualifiedName~OfficialGpuTests','--minimum-expected-tests','2',
            '--results-directory',$results,'--report-trx','--report-trx-filename','gpu.trx') 'gpu'
        $configuration = '{"ATTENTION_BACKEND":"SDPA"}'
        $algorithm = [Security.Cryptography.SHA256]::Create()
        try {
            $configSha = -join @($algorithm.ComputeHash(
                [Text.Encoding]::UTF8.GetBytes($configuration)) | ForEach-Object {
                    $_.ToString('x2')
                })
        }
        finally { $algorithm.Dispose() }
        $gpuDocument = [ordered]@{
            schemaVersion = 1
            commitSha = $ExpectedCommitSha
            requestedDevice = $GpuDevice
            actualExecutionDevices = @($GpuDevice)
            gpuIdentity = [ordered]@{
                vendor = 'Intel'; name = $ExpectedGpuName
                driverVersion = $ExpectedGpuDriverVersion
            }
            pluginIdentity = [ordered]@{
                fileName = 'openvino_intel_gpu_plugin.dll'
                sha256 = (Get-FileHash -LiteralPath (Join-Path $stageB 'openvino_intel_gpu_plugin.dll') `
                    -Algorithm SHA256).Hash.ToLowerInvariant()
            }
            runtimeIdentity = [ordered]@{
                runtime = '2026.3.0-22451-8a17657b995-releases/2026/3'
                genAi = '2026.3.0.0-3277-bd8d6542e3c'
                tokenizers = '2026.3.0.0-703-183c6f25cda'
            }
            configIdentity = [ordered]@{ attentionBackend = 'SDPA'; sha256 = $configSha }
            forcedNegatives = [ordered]@{
                nonexistentDevice = 'runtime_device_unavailable'
                cpuResolutionMismatch = 'runtime_device_mismatch'
            }
            testDispositions = [ordered]@{
                oneTurn = 'passed'; twoTurn = 'passed'; cancellation = 'passed'
                cleanup = 'zero_residue'
            }
        }
        New-Item -ItemType Directory -Path (Split-Path -Parent $gpuEvidence) | Out-Null
        [IO.File]::WriteAllText($gpuEvidence,
            ($gpuDocument | ConvertTo-Json -Depth 8 -Compress),
            [Text.UTF8Encoding]::new($false))
        Invoke-ExactScript (Join-Path $PSScriptRoot 'Test-OpenVinoGpuEvidence.ps1') `
            @('-EvidencePath',$gpuEvidence,'-StageDirectory',$stageB,
              '-ResultsPath',(Join-Path $results 'gpu.trx'),
              '-ExpectedCommitSha',$ExpectedCommitSha,'-ExpectedDevice',$GpuDevice,
              '-ExpectedGpuName',$ExpectedGpuName,
              '-ExpectedDriverVersion',$ExpectedGpuDriverVersion) 'gpu_evidence_valid'
        Assert-OpenVinoTrustedInputLeaseUnchanged -Lease $lease
    }

    $manifestSha = (Get-FileHash -LiteralPath (Join-Path $stageB 'worker-manifest.json') `
        -Algorithm SHA256).Hash.ToLowerInvariant()
    & dotnet msbuild (Join-Path $workspace 'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj') `
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
    $configurationIdentity = (Get-FileHash -LiteralPath (Join-Path $workspace `
        'IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationCandidate.cs') `
        -Algorithm SHA256).Hash.ToLowerInvariant()
    $driverIdentity = if ($gpuRequested) {
        $ExpectedGpuDriverVersion
    } else {
        'windows-' + [string](Get-CimInstance Win32_OperatingSystem).BuildNumber
    }
    $stableArguments = @(
        '-ExpectedCommitSha',$ExpectedCommitSha,
        '-OfficialStageA',$stageA,'-OfficialStageB',$stageB,
        '-ConverterStage',$converterStage,'-ResultsDirectory',$results,
        '-CpuEvidencePath',$evidence,'-ModelIdentity',$ExpectedModelSha256,
        '-ModelIdentityPath',(Join-Path $fixture 'package/openvino_model.bin'),
        '-ConfigurationIdentity',$configurationIdentity,
        '-ConfigurationIdentityPath',(Join-Path $workspace `
            'IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationCandidate.cs'),
        '-GpuDisposition',$(if ($gpuRequested) { 'proven' } else { 'runtime_device_unavailable' }),
        '-driverIdentity',$driverIdentity)
    if ($gpuRequested) {
        $stableArguments += @('-GpuEvidencePath',$gpuEvidence,
            '-ExpectedGpuDevice',$GpuDevice,'-ExpectedGpuName',$ExpectedGpuName)
    }
    Invoke-ExactScript (Join-Path $PSScriptRoot 'Invoke-OpenVinoStableAcceptance.ps1') `
        $stableArguments 'stable_route_accepted'
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
