[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$RepositoryRoot,
    [Parameter(Mandatory = $true)] [string]$OutputDirectory,
    [Parameter(Mandatory = $true)] [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')] [string]$RunIdentity,
    [Parameter(Mandatory = $true)] [string]$Br8BundleDirectory,
    [Parameter(Mandatory = $true)] [ValidatePattern('^sha256:[0-9a-f]{64}$')] [string]$Br8ExpectedArtifactDigest,
    [Parameter(Mandatory = $true)] [ValidatePattern('^sha256:[0-9a-f]{64}$')] [string]$Br8ActualArtifactDigest,
    [Parameter(Mandatory = $true)] [bool]$Br8AcceptedByProjectOwner,
    [string]$PythonPath = 'C:\Program Files\Python312\python.exe'
)

<#
.SYNOPSIS
Builds Route B only after the exact BR8 artifact and owner acceptance pass.

.DESCRIPTION
The script validates BR8 as untrusted data before touching external source. It
reapplies only the reviewed CMake repair, repeats the narrow six-case repository
gate, then permits a full experimental Runtime build/install. It never downloads
or executes a model and never authorises activation, storage, performance, or
quality claims.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RouteId = 'route-b-experimental-qjl-polar'
$Component = 'runtime'
$SourceRepository = 'https://github.com/EgorDuplensky/openvino.git'
$SourceCommit = '1827f6458d049de11c1a8203c793af67c99935dc'
$WorkspaceRoot = 'C:\w5b'
$Generator = 'Visual Studio 17 2022'
$ExpectedRepairPath = 'src/plugins/intel_cpu/tests/functional/cmake/target_per_test.cmake'
$ExpectedTarget = 'ov_cpu_func_subgraph_concat_sdp_turboq'
$CMakePath = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
$ExpectedPythonVersion = 'Python 3.12.10'

function Write-RouteBDecision {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Passed', 'Failed', 'Blocked', 'Infrastructure interrupted')]
        [string]$Status,
        [Parameter(Mandatory = $true)] [string[]]$Reasons
    )

    # Build evidence cannot authorise model execution or any later scientific claim.
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'decision.json') -Value ([ordered]@{
        schema_version = '1.0'
        campaign_id = 'GTQ-WB05-MF-v1'
        record_type = 'build-decision'
        route_id = $RouteId
        component = $Component
        source_commit = $SourceCommit
        status = $Status
        reasons = @($Reasons)
        required_components = @()
        granite_model_test_authorised = $false
        activation_claim_authorised = $false
        packed_storage_claim_authorised = $false
        performance_claim_authorised = $false
        quality_claim_authorised = $false
    })
}

function Complete-RouteBEvidence {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Passed', 'Failed', 'Blocked', 'Infrastructure interrupted')]
        [string]$Status,
        [Parameter(Mandatory = $true)] [string[]]$Reasons
    )

    Write-RouteBDecision -Status $Status -Reasons $Reasons
    Write-Wb05Manifest -EvidenceDirectory $OutputDirectory | Out-Null
    Write-Host "WORKBOOK05_ROUTE_B_RUNTIME_STATUS=$Status"
}

function Invoke-RouteBCommand {
    param(
        [Parameter(Mandatory = $true)] [string]$CommandId,
        [Parameter(Mandatory = $true)] [string]$FilePath,
        [Parameter(Mandatory = $true)] [string[]]$Arguments,
        [Parameter(Mandatory = $true)] [string]$WorkingDirectory,
        [switch]$MonitorResources
    )

    return Invoke-Wb05LoggedProcess `
        -CommandId $CommandId `
        -RouteId $RouteId `
        -Component $Component `
        -FilePath $FilePath `
        -ArgumentList $Arguments `
        -WorkingDirectory $WorkingDirectory `
        -EvidenceDirectory (Join-Path $OutputDirectory 'commands') `
        -EvidenceRoot $OutputDirectory `
        -EnvironmentAllowlist @{
            RUNNER_NAME = [string]$env:RUNNER_NAME
            RUNNER_OS = [string]$env:RUNNER_OS
        } `
        -MonitorResources:$MonitorResources
}

function Assert-ExitZero {
    param([Parameter(Mandatory = $true)] [object]$Result, [Parameter(Mandatory = $true)] [string]$Description)
    if ($Result.record.exit_code -ne 0) {
        throw "$Description exited with code $($Result.record.exit_code)."
    }
}

function Read-CommandText {
    param([Parameter(Mandatory = $true)] [object]$Result)

    # Preserve a legitimate readable zero-byte stdout as an empty string while
    # keeping missing or unreadable evidence fail-closed through ErrorAction Stop.
    $capturedOutput = Get-Content `
        -LiteralPath $Result.stdout_path `
        -Raw `
        -ErrorAction Stop
    if ($null -eq $capturedOutput) {
        return ''
    }
    return $capturedOutput.Trim()
}

function Get-CMakeCacheValue {
    param([Parameter(Mandatory = $true)] [string[]]$Lines, [Parameter(Mandatory = $true)] [string]$Name)
    $line = $Lines | Where-Object { $_ -match "^$([Regex]::Escape($Name)):[^=]+=" } | Select-Object -First 1
    if (-not $line) { return $null }
    return ($line -split '=', 2)[1]
}

$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$modulePath = Join-Path $RepositoryRoot 'scripts/testing/workbook05/Workbook05.Build.psm1'
if (-not (Test-Path -LiteralPath $modulePath -PathType Leaf)) { throw "Workbook 05 build module is missing: $modulePath" }
Import-Module $modulePath -Force

if (-not (Test-Path -LiteralPath $PythonPath -PathType Leaf)) { throw "Pinned Python was not found at: $PythonPath" }
if (-not (Test-Path -LiteralPath $CMakePath -PathType Leaf)) { throw "Pinned CMake was not found at: $CMakePath" }
if (-not (Test-Path -LiteralPath $Br8BundleDirectory -PathType Container)) { throw "BR8 bundle was not found: $Br8BundleDirectory" }
if ($Br8ActualArtifactDigest -ne $Br8ExpectedArtifactDigest) { throw 'BR8 artifact digest does not match the reviewed workflow artifact.' }
if (-not $Br8AcceptedByProjectOwner) { throw 'Route B BR8 outcome has not been accepted by the project owner.' }
if (Test-Path -LiteralPath $OutputDirectory) { throw "Evidence directory already exists and will not be reused: $OutputDirectory" }
New-Item -ItemType Directory -Path $OutputDirectory -Force:$false | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path

$pythonVersion = ((& $PythonPath --version 2>&1) | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $pythonVersion -ne $ExpectedPythonVersion) { throw "Expected $ExpectedPythonVersion, found: $pythonVersion" }
$gitPath = (Get-Command -Name 'git.exe' -CommandType Application -All -ErrorAction Stop | Select-Object -First 1).Source
$environmentSnapshot = @{
    'PATH' = [Environment]::GetEnvironmentVariable('PATH', 'Process')
    'OV_TURBOQ_ROTATION' = [Environment]::GetEnvironmentVariable('OV_TURBOQ_ROTATION', 'Process')
}

try {
    # Independently validate the BR8 artifact before Git, repair, or CMake runs.
    $br8Report = Join-Path $OutputDirectory 'br8-validation-report.md'
    Push-Location $RepositoryRoot
    try {
        & $PythonPath -m scripts.testing.workbook05.route_b_repair_bundle_validation `
            --bundle-root $Br8BundleDirectory `
            --report $br8Report
        $br8ValidationExitCode = $LASTEXITCODE
    }
    finally { Pop-Location }
    if ($br8ValidationExitCode -ne 0) { throw "BR8 artifact validation failed with code $br8ValidationExitCode." }

    $br8Decision = Get-Content -LiteralPath (Join-Path $Br8BundleDirectory 'decision.json') -Raw | ConvertFrom-Json
    $changedFiles = Get-Content -LiteralPath (Join-Path $Br8BundleDirectory 'changed-files.json') -Raw | ConvertFrom-Json
    $br8Results = @(Get-Content -LiteralPath (Join-Path $Br8BundleDirectory 'test-results.json') -Raw | ConvertFrom-Json)
    $br8Membership = Get-Content -LiteralPath (Join-Path $Br8BundleDirectory 'target-membership.json') -Raw | ConvertFrom-Json

    if ($br8Decision.status -ne 'ExecutableCandidate') { throw "BR8 decision is not ExecutableCandidate: $($br8Decision.status)" }
    if ($br8Decision.source_commit -ne $SourceCommit) { throw "BR8 source commit differs: $($br8Decision.source_commit)" }
    if ($br8Decision.route_id -ne $RouteId) { throw "BR8 route differs: $($br8Decision.route_id)" }
    if ($br8Decision.granite_model_test_authorised -ne $false) { throw 'BR8 unexpectedly authorises Granite model testing.' }
    if ($br8Decision.performance_claim_authorised -ne $false) { throw 'BR8 unexpectedly authorises performance claims.' }
    if ($br8Decision.quality_claim_authorised -ne $false) { throw 'BR8 unexpectedly authorises quality claims.' }
    if ($changedFiles.algorithm_files_changed -ne $false) { throw 'BR8 reports algorithm source drift.' }
    if (@($changedFiles.actual).Count -ne 1 -or [string]$changedFiles.actual[0] -ne $ExpectedRepairPath) { throw 'BR8 changed-file boundary differs from the reviewed one-file repair.' }
    if ($br8Membership.class_source_present -ne $true -or $br8Membership.x64_instance_present -ne $true) { throw 'BR8 target membership is incomplete.' }

    $requiredBr8Cases = @('baseline_f32', 'qjl4', 'qjl3', 'polar4', 'polar3', 'asymmetric_f32_tbq4')
    foreach ($caseId in $requiredBr8Cases) {
        $matches = @($br8Results | Where-Object { $_.id -eq $caseId })
        if ($matches.Count -ne 1) { throw "BR8 is missing one exact result for $caseId." }
        $result = $matches[0]
        if ($result.run_count -le 0 -or $result.skipped_count -ne 0 -or $result.passed -ne $true -or $result.exit_code -ne 0) {
            throw "BR8 result is not a non-zero unskipped pass: $caseId"
        }
    }
    Write-Host 'ROUTE_B_BR8_PREREQUISITE_ACCEPTED'

    # Create a new short workspace only after all BR8 prerequisites pass.
    $workspace = New-Wb05ExternalWorkspace -Root $WorkspaceRoot -RunIdentity $RunIdentity
    $sourceRoot = Join-Path $workspace.work_directory 'ov'
    $buildRoot = Join-Path $workspace.work_directory 'b-ov'
    $installRoot = Join-Path $workspace.work_directory 'i-ov'

    $os = Get-CimInstance Win32_OperatingSystem
    $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'environment.json') -Value ([ordered]@{
        schema_version = '1.0'; campaign_id = 'GTQ-WB05-MF-v1'; route_id = $RouteId; component = $Component
        runner_name = $env:RUNNER_NAME; runner_os = $env:RUNNER_OS; operating_system = $os.Caption
        operating_system_version = $os.Version; processor = $cpu.Name; logical_processors = $cpu.NumberOfLogicalProcessors
        total_visible_memory_kib = [int64]$os.TotalVisibleMemorySize; free_physical_memory_kib_before = [int64]$os.FreePhysicalMemory
        python = $pythonVersion; python_path = $PythonPath; git_path = $gitPath; cmake_path = $CMakePath
        generator = $Generator; platform = 'x64'; configuration = 'Release'; parallelism = 2
        br8_expected_artifact_digest = $Br8ExpectedArtifactDigest; br8_actual_artifact_digest = $Br8ActualArtifactDigest
        br8_accepted_by_project_owner = $Br8AcceptedByProjectOwner; work_directory = $workspace.work_directory
    })

    $commands = @(
        @('route-b-git-init', @('-c', 'core.longpaths=true', 'init', $sourceRoot)),
        @('route-b-git-config-longpaths', @('-C', $sourceRoot, 'config', 'core.longpaths', 'true')),
        @('route-b-git-remote-add', @('-C', $sourceRoot, 'remote', 'add', 'origin', $SourceRepository)),
        @('route-b-git-fetch', @('-c', 'core.longpaths=true', '-C', $sourceRoot, 'fetch', '--depth=1', 'origin', $SourceCommit)),
        @('route-b-git-checkout', @('-c', 'core.longpaths=true', '-C', $sourceRoot, 'checkout', '--detach', $SourceCommit)),
        @('route-b-git-submodules', @('-c', 'core.longpaths=true', '-C', $sourceRoot, 'submodule', 'update', '--init', '--recursive'))
    )
    foreach ($command in $commands) {
        $record = Invoke-RouteBCommand -CommandId $command[0] -FilePath $gitPath -Arguments $command[1] -WorkingDirectory $workspace.work_directory
        Assert-ExitZero -Result $record -Description $command[0]
    }

    $remote = Invoke-RouteBCommand -CommandId 'route-b-git-remote-verify' -FilePath $gitPath -Arguments @('-C', $sourceRoot, 'remote', 'get-url', 'origin') -WorkingDirectory $workspace.work_directory
    $head = Invoke-RouteBCommand -CommandId 'route-b-git-head-verify' -FilePath $gitPath -Arguments @('-C', $sourceRoot, 'rev-parse', 'HEAD') -WorkingDirectory $workspace.work_directory
    $status = Invoke-RouteBCommand -CommandId 'route-b-git-status-verify' -FilePath $gitPath -Arguments @('-C', $sourceRoot, 'status', '--porcelain=v1') -WorkingDirectory $workspace.work_directory
    $submodules = Invoke-RouteBCommand -CommandId 'route-b-git-submodules-verify' -FilePath $gitPath -Arguments @('-C', $sourceRoot, 'submodule', 'status', '--recursive') -WorkingDirectory $workspace.work_directory
    foreach ($verification in @($remote, $head, $status, $submodules)) { Assert-ExitZero -Result $verification -Description $verification.record.command_id }
    $actualRemote = Read-CommandText $remote
    $actualHead = Read-CommandText $head
    $actualStatus = Read-CommandText $status
    $submoduleLines = @(Read-CommandText $submodules -split "`r?`n" | Where-Object { $_ })
    if ($actualRemote -ne $SourceRepository -or $actualHead -ne $SourceCommit -or $actualStatus) { throw 'Route B source provenance does not match the exact clean source boundary.' }
    if (@($submoduleLines | Where-Object { $_ -match '^[\-+U]' }).Count -ne 0) { throw 'Route B recursive submodule provenance is incomplete.' }

    Write-Wb05Json -Path (Join-Path $OutputDirectory 'source-provenance.json') -Value ([ordered]@{
        schema_version = '1.0'; campaign_id = 'GTQ-WB05-MF-v1'; route_id = $RouteId; component = $Component
        requested_repository = $SourceRepository; actual_repository = $actualRemote; requested_commit = $SourceCommit; actual_commit = $actualHead
        clean_before_repair = $true; recursive_submodule_count = $submoduleLines.Count; recursive_submodules_complete = $true
        expected_repair_path = $ExpectedRepairPath; br8_artifact_digest = $Br8ActualArtifactDigest
    })

    # Reapply the exact reviewed repair; no algorithm file may change.
    $repairEvidence = Join-Path $OutputDirectory 'repair'
    Push-Location $RepositoryRoot
    try {
        & $PythonPath -m scripts.testing.workbook05.route_b_repair `
            --source-root $sourceRoot `
            --evidence-root $repairEvidence `
            --source-commit $SourceCommit
        $repairExitCode = $LASTEXITCODE
    }
    finally { Pop-Location }
    if ($repairExitCode -ne 0) { throw "Route B repair failed with code $repairExitCode." }

    $actualChangedFiles = @(& $gitPath -C $sourceRoot diff --name-only)
    if ($actualChangedFiles.Count -ne 1 -or $actualChangedFiles[0].Replace('\', '/') -ne $ExpectedRepairPath) {
        throw "Repair changed an unexpected external file set: $($actualChangedFiles -join ', ')"
    }
    & $gitPath -C $sourceRoot diff --check
    if ($LASTEXITCODE -ne 0) { throw 'Route B repair failed git diff --check.' }
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'changed-files.json') -Value ([ordered]@{
        expected = @($ExpectedRepairPath); actual = @($actualChangedFiles | ForEach-Object { $_.Replace('\', '/') }); algorithm_files_changed = $false
    })

    $configure = Invoke-RouteBCommand -CommandId 'route-b-runtime-configure' -FilePath $CMakePath -Arguments @(
        '-S', $sourceRoot, '-B', $buildRoot, '-G', $Generator, '-A', 'x64', '-DCMAKE_BUILD_TYPE=Release',
        '-DENABLE_INTEL_GPU=OFF', '-DENABLE_INTEL_NPU=OFF', '-DENABLE_TESTS=ON', '-DENABLE_FUNCTIONAL_TESTS=ON',
        '-DENABLE_CPU_SPECIFIC_TARGET_PER_TEST=ON', '-DENABLE_PYTHON=ON', '-DENABLE_WHEEL=OFF', "-DPython3_EXECUTABLE=$PythonPath"
    ) -WorkingDirectory $workspace.work_directory -MonitorResources
    if ($configure.record.exit_code -ne 0) { Complete-RouteBEvidence -Status 'Failed' -Reasons @("Route B configure exited with code $($configure.record.exit_code)."); return }

    $cachePath = Join-Path $buildRoot 'CMakeCache.txt'
    if (-not (Test-Path -LiteralPath $cachePath -PathType Leaf)) { throw 'Route B configure passed without CMakeCache.txt.' }
    $cacheLines = Get-Content -LiteralPath $cachePath
    $cacheValues = [ordered]@{}
    foreach ($name in @('CMAKE_GENERATOR', 'CMAKE_GENERATOR_PLATFORM', 'ENABLE_TESTS', 'ENABLE_FUNCTIONAL_TESTS', 'ENABLE_CPU_SPECIFIC_TARGET_PER_TEST', 'ENABLE_INTEL_GPU', 'ENABLE_INTEL_NPU')) {
        $cacheValues[$name] = Get-CMakeCacheValue -Lines $cacheLines -Name $name
    }
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'cmake-cache-summary.json') -Value ([ordered]@{
        schema_version = '1.0'; sha256 = (Get-FileHash -LiteralPath $cachePath -Algorithm SHA256).Hash.ToLowerInvariant(); values = $cacheValues
    })
    if ($cacheValues.CMAKE_GENERATOR -ne $Generator -or $cacheValues.CMAKE_GENERATOR_PLATFORM -ne 'x64' -or $cacheValues.ENABLE_TESTS -ne 'ON' -or $cacheValues.ENABLE_FUNCTIONAL_TESTS -ne 'ON' -or $cacheValues.ENABLE_CPU_SPECIFIC_TARGET_PER_TEST -ne 'ON') {
        Complete-RouteBEvidence -Status 'Blocked' -Reasons @('Route B generated cache does not match the reviewed test exposure controls.'); return
    }

    $projectFile = Get-ChildItem -LiteralPath $buildRoot -Filter "$ExpectedTarget.vcxproj" -File -Recurse | Select-Object -First 1
    if (-not $projectFile) { Complete-RouteBEvidence -Status 'Blocked' -Reasons @("Generated target project was not found: $ExpectedTarget"); return }
    $projectText = Get-Content -LiteralPath $projectFile.FullName -Raw
    $membership = @([Regex]::Matches($projectText, 'Include="([^"]*concat_sdp_turboq\.cpp)"') | ForEach-Object { $_.Groups[1].Value.Replace('\', '/') } | Sort-Object -Unique)
    $classPresent = @($membership | Where-Object { $_ -match '/classes/concat_sdp_turboq\.cpp$' }).Count -gt 0
    $x64Present = @($membership | Where-Object { $_ -match '/x64/concat_sdp_turboq\.cpp$' }).Count -gt 0
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'target-membership.json') -Value ([ordered]@{
        target = $ExpectedTarget; matching_sources = $membership; class_source_present = $classPresent; x64_instance_present = $x64Present
    })
    if (-not $classPresent -or -not $x64Present) { Complete-RouteBEvidence -Status 'Blocked' -Reasons @('Generated target membership is incomplete.'); return }

    $narrowBuild = Invoke-RouteBCommand -CommandId 'route-b-narrow-build' -FilePath $CMakePath -Arguments @('--build', $buildRoot, '--config', 'Release', '--target', $ExpectedTarget, '--parallel', '2', '--verbose') -WorkingDirectory $workspace.work_directory -MonitorResources
    if ($narrowBuild.record.exit_code -ne 0) { Complete-RouteBEvidence -Status 'Failed' -Reasons @("Route B narrow target exited with code $($narrowBuild.record.exit_code)."); return }

    $testExecutable = Get-ChildItem -LiteralPath $sourceRoot -Filter "$ExpectedTarget.exe" -File -Recurse | Select-Object -First 1
    if (-not $testExecutable) { $testExecutable = Get-ChildItem -LiteralPath $buildRoot -Filter "$ExpectedTarget.exe" -File -Recurse | Select-Object -First 1 }
    if (-not $testExecutable) { Complete-RouteBEvidence -Status 'Blocked' -Reasons @('Route B narrow target built but its executable was not found.'); return }

    $runtimeDirectories = @($testExecutable.DirectoryName)
    $runtimeDirectories += @(Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'temp') -Filter 'tbb*.dll' -File -Recurse -ErrorAction SilentlyContinue | ForEach-Object { $_.DirectoryName })
    $env:PATH = (@($runtimeDirectories | Sort-Object -Unique) -join ';') + ';' + [string]$environmentSnapshot['PATH']
    $env:OV_TURBOQ_ROTATION = 'wht'

    $discovery = Invoke-RouteBCommand -CommandId 'route-b-gtest-discovery' -FilePath $testExecutable.FullName -Arguments @('--gtest_list_tests', '--gtest_color=no') -WorkingDirectory $testExecutable.DirectoryName
    if ($discovery.record.exit_code -ne 0) { Complete-RouteBEvidence -Status 'Failed' -Reasons @('Route B GTest discovery failed.'); return }
    $discoveryText = Get-Content -LiteralPath $discovery.stdout_path -Raw
    $discoveredCount = @($discoveryText -split "`r?`n" | Where-Object { $_ -match '^\s{2,}\S' }).Count
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'test-discovery-summary.json') -Value ([ordered]@{ discovered_test_count = $discoveredCount; target = $ExpectedTarget })
    if ($discoveredCount -le 0) { Complete-RouteBEvidence -Status 'Blocked' -Reasons @('Route B narrow target discovered zero tests.'); return }

    $selectedCases = @(
        [ordered]@{ id = 'baseline_f32'; filter = '*Prc=f32*K=none_V=none*' },
        [ordered]@{ id = 'qjl4'; filter = '*Prc=f32*K=tbq4_qjl_V=tbq4_qjl*' },
        [ordered]@{ id = 'qjl3'; filter = '*Prc=f32*K=tbq3_qjl_V=tbq3_qjl*' },
        [ordered]@{ id = 'polar4'; filter = '*Prc=f32*K=polar4_V=polar4*' },
        [ordered]@{ id = 'polar3'; filter = '*Prc=f32*K=polar3_V=polar3*' },
        [ordered]@{ id = 'asymmetric_f32_tbq4'; filter = '*Prc=f32*K=none_V=tbq4*' }
    )
    $testResults = @()
    $caseFailure = $false
    foreach ($selectedCase in $selectedCases) {
        $caseResult = Invoke-RouteBCommand -CommandId "route-b-test-$($selectedCase.id)" -FilePath $testExecutable.FullName -Arguments @("--gtest_filter=$($selectedCase.filter)", '--gtest_color=no') -WorkingDirectory $testExecutable.DirectoryName
        $outputText = Get-Content -LiteralPath $caseResult.stdout_path -Raw
        $runMatch = [Regex]::Match($outputText, 'Running\s+(\d+)\s+tests?')
        $skipMatch = [Regex]::Match($outputText, '(?m)^\[\s*SKIPPED\s*\]\s+(\d+)\s+tests?')
        $runCount = if ($runMatch.Success) { [int]$runMatch.Groups[1].Value } else { 0 }
        $skippedCount = if ($skipMatch.Success) { [int]$skipMatch.Groups[1].Value } else { 0 }
        $passed = $caseResult.record.exit_code -eq 0 -and $runCount -gt 0 -and $skippedCount -eq 0
        if (-not $passed) { $caseFailure = $true }
        $testResults += [ordered]@{ id = $selectedCase.id; filter = $selectedCase.filter; exit_code = $caseResult.record.exit_code; run_count = $runCount; skipped_count = $skippedCount; passed = $passed }
    }
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'test-results.json') -Value $testResults
    if ($caseFailure) { Complete-RouteBEvidence -Status 'Failed' -Reasons @('One or more repeated Route B repository cases failed, skipped, or ran zero tests.'); return }

    # Only the repeated narrow gate permits the full Runtime build and install.
    $fullBuild = Invoke-RouteBCommand -CommandId 'route-b-runtime-build' -FilePath $CMakePath -Arguments @('--build', $buildRoot, '--config', 'Release', '--parallel', '2', '--verbose') -WorkingDirectory $workspace.work_directory -MonitorResources
    if ($fullBuild.record.exit_code -ne 0) { Complete-RouteBEvidence -Status 'Failed' -Reasons @("Route B full Runtime build exited with code $($fullBuild.record.exit_code)."); return }
    $install = Invoke-RouteBCommand -CommandId 'route-b-runtime-install' -FilePath $CMakePath -Arguments @('--install', $buildRoot, '--config', 'Release', '--prefix', $installRoot) -WorkingDirectory $workspace.work_directory -MonitorResources
    if ($install.record.exit_code -ne 0) { Complete-RouteBEvidence -Status 'Failed' -Reasons @("Route B Runtime install exited with code $($install.record.exit_code)."); return }

    $binaryRecords = Get-Wb05BinaryRecords -Root $installRoot -RouteId $RouteId -Component $Component -ProducerCommandId 'route-b-runtime-install'
    if ($binaryRecords.Count -eq 0) { throw 'Route B install produced no hashable binary outputs.' }
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'binaries.json') -Value $binaryRecords
    Complete-RouteBEvidence -Status 'Passed' -Reasons @('The accepted BR8 candidate repeated its narrow gate, then the exact experimental Runtime built and installed. This does not prove model activation, packed storage, no fallback, performance, or quality.')
}
catch {
    if (Test-Path -LiteralPath $OutputDirectory -PathType Container) {
        Write-Wb05Json -Path (Join-Path $OutputDirectory 'integrity-failure.json') -Value ([ordered]@{
            schema_version = '1.0'; campaign_id = 'GTQ-WB05-MF-v1'; route_id = $RouteId; component = $Component
            status = 'IntegrityFailure'; source_commit = $SourceCommit; message = $_.Exception.Message
            granite_model_test_authorised = $false; activation_claim_authorised = $false; packed_storage_claim_authorised = $false
            performance_claim_authorised = $false; quality_claim_authorised = $false
        })
        Write-Wb05Manifest -EvidenceDirectory $OutputDirectory | Out-Null
    }
    throw
}
finally { Restore-Wb05Environment -Snapshot $environmentSnapshot }
