[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string]$RunIdentity,

    [string]$PythonPath = 'C:\Program Files\Python312\python.exe'
)

<#
.SYNOPSIS
Builds and installs the exact Workbook 05 Route A OpenVINO Runtime source.

.DESCRIPTION
The script follows the pinned Windows source-build sequence, records provenance,
commands, resource boundaries, dependencies, cache controls, and in-place binary
hashes. It does not download or execute a model and does not authorise any later
TurboQuant activation, packed-storage, performance, or quality claim.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Freeze the reviewed Route A source, build document, generator, short workspace,
# and tool entry points. Any later change requires a reviewed deviation record.
$RouteId = 'route-a-merged-openvino'
$Component = 'runtime'
$SourceRepository = 'https://github.com/openvinotoolkit/openvino.git'
$SourceCommit = 'b9a1f201c109e0bed74763934f79483cf6c4cbf4'
$BuildDocument = 'docs/dev/build_windows.md'
$Generator = 'Visual Studio 17 2022'
$WorkspaceRoot = 'C:\w5a'
$ExpectedPythonVersion = 'Python 3.12.10'
$CMakePath = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'

function Write-RouteARuntimeDecision {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Passed', 'Failed', 'Blocked', 'Infrastructure interrupted')]
        [string]$Status,

        [Parameter(Mandatory = $true)]
        [string[]]$Reasons
    )

    # This decision proves only the Runtime build boundary. Every later model or
    # scientific claim remains explicitly disabled regardless of build outcome.
    $decision = [ordered]@{
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
    }
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'decision.json') -Value $decision
}

function Complete-RouteARuntimeEvidence {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Passed', 'Failed', 'Blocked', 'Infrastructure interrupted')]
        [string]$Status,

        [Parameter(Mandatory = $true)]
        [string[]]$Reasons
    )

    # Write the decision before the manifest so the final status is covered by
    # the exact SHA-256 manifest sent to the independent hosted validator.
    Write-RouteARuntimeDecision -Status $Status -Reasons $Reasons
    Write-Wb05Manifest -EvidenceDirectory $OutputDirectory | Out-Null
    Write-Host "WORKBOOK05_ROUTE_A_RUNTIME_STATUS=$Status"
}

function Invoke-RouteACommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandId,

        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [switch]$MonitorResources
    )

    # Route every external command through the shared safe process adapter so
    # argument boundaries, logs, timing, and optional resource evidence agree.
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
    param(
        [Parameter(Mandatory = $true)]
        [object]$Result,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    # Provenance commands are integrity prerequisites; a non-zero exit cannot be
    # reclassified as a successful build-stage outcome.
    if ($Result.record.exit_code -ne 0) {
        throw "$Description exited with code $($Result.record.exit_code)."
    }
}

function Read-CommandOutput {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Result
    )

    # Read only the captured stdout file associated with the reviewed command.
    # Windows PowerShell can yield no object for a readable zero-byte file, which
    # is a legitimate success value for commands such as `git status --porcelain`.
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
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Lines,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    # CMake cache lines have NAME:TYPE=value form. Return null when the reviewed
    # control was not materialised so the caller can fail closed.
    $line = $Lines |
        Where-Object { $_ -match "^$([Regex]::Escape($Name)):[^=]+=" } |
        Select-Object -First 1
    if (-not $line) {
        return $null
    }
    return ($line -split '=', 2)[1]
}

function Test-Wb05SafetyStop {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Result
    )

    # A resource-triggered termination is an infrastructure/safety interruption,
    # not evidence that the upstream OpenVINO source itself failed to compile.
    return (
        $null -ne $Result.resource_summary -and
        $Result.resource_summary.safety_stop_triggered -eq $true
    )
}

# Resolve the repository and import only the reviewed shared build primitives.
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$modulePath = Join-Path $RepositoryRoot 'scripts/testing/workbook05/Workbook05.Build.psm1'
if (-not (Test-Path -LiteralPath $modulePath -PathType Leaf)) {
    throw "Workbook 05 build module is missing: $modulePath"
}
Import-Module $modulePath -Force -ErrorAction Stop

# Refuse a missing or drifting toolchain before creating any external source tree.
if (-not (Test-Path -LiteralPath $PythonPath -PathType Leaf)) {
    throw "Pinned Python was not found at: $PythonPath"
}
if (-not (Test-Path -LiteralPath $CMakePath -PathType Leaf)) {
    throw "Pinned CMake was not found at: $CMakePath"
}
$pythonVersion = ((& $PythonPath --version 2>&1) | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $pythonVersion -ne $ExpectedPythonVersion) {
    throw "Expected $ExpectedPythonVersion, found: $pythonVersion"
}
$gitPath = (
    Get-Command -Name 'git.exe' -CommandType Application -All -ErrorAction Stop |
        Select-Object -First 1
).Source

# Evidence is immutable for one workflow attempt. Existing content is a possible
# contamination source, so the orchestrator refuses to reuse it.
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "Evidence directory already exists and will not be reused: $OutputDirectory"
}
New-Item -ItemType Directory -Path $OutputDirectory -Force:$false | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path

try {
    # Allocate a new short Windows workspace and derive independent source, build,
    # and install roots exactly as approved by the Phase 2 design.
    $workspace = New-Wb05ExternalWorkspace -Root $WorkspaceRoot -RunIdentity $RunIdentity
    $sourceRoot = Join-Path $workspace.work_directory 'ov'
    $buildRoot = Join-Path $workspace.work_directory 'b-ov'
    $installRoot = Join-Path $workspace.work_directory 'i-ov'

    # Capture only non-secret machine/tool metadata needed to reproduce the build.
    $operatingSystem = Get-CimInstance Win32_OperatingSystem
    $processor = Get-CimInstance Win32_Processor | Select-Object -First 1
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'environment.json') -Value ([ordered]@{
        schema_version = '1.0'
        campaign_id = 'GTQ-WB05-MF-v1'
        route_id = $RouteId
        component = $Component
        computer_name = $env:COMPUTERNAME
        runner_name = $env:RUNNER_NAME
        runner_os = $env:RUNNER_OS
        operating_system = $operatingSystem.Caption
        operating_system_version = $operatingSystem.Version
        processor = $processor.Name
        logical_processors = $processor.NumberOfLogicalProcessors
        total_visible_memory_kib = [int64]$operatingSystem.TotalVisibleMemorySize
        free_physical_memory_kib_before = [int64]$operatingSystem.FreePhysicalMemory
        python = $pythonVersion
        python_path = $PythonPath
        git_path = $gitPath
        cmake_path = $CMakePath
        generator = $Generator
        platform = 'x64'
        configuration = 'Release'
        parallelism = 2
        work_directory = $workspace.work_directory
        source_directory = $sourceRoot
        build_directory = $buildRoot
        install_directory = $installRoot
    })

    # Acquire the exact Runtime commit without moving branches, global Git state,
    # or destructive cleanup. Long-path support is repository-scoped only.
    $sourceCommands = @(
        @('route-a-runtime-git-init', @('-c', 'core.longpaths=true', 'init', $sourceRoot)),
        @('route-a-runtime-git-config-longpaths', @('-C', $sourceRoot, 'config', 'core.longpaths', 'true')),
        @('route-a-runtime-git-remote-add', @('-C', $sourceRoot, 'remote', 'add', 'origin', $SourceRepository)),
        @('route-a-runtime-git-fetch', @('-c', 'core.longpaths=true', '-C', $sourceRoot, 'fetch', '--depth=1', 'origin', $SourceCommit)),
        @('route-a-runtime-git-checkout', @('-c', 'core.longpaths=true', '-C', $sourceRoot, 'checkout', '--detach', $SourceCommit)),
        @('route-a-runtime-git-submodules', @('-c', 'core.longpaths=true', '-C', $sourceRoot, 'submodule', 'update', '--init', '--recursive'))
    )
    foreach ($command in $sourceCommands) {
        $result = Invoke-RouteACommand `
            -CommandId $command[0] `
            -FilePath $gitPath `
            -Arguments $command[1] `
            -WorkingDirectory $workspace.work_directory
        Assert-ExitZero -Result $result -Description $command[0]
    }

    # Verify origin, detached HEAD, clean status, and every recursive submodule
    # before any configure command is allowed to run.
    $remoteResult = Invoke-RouteACommand -CommandId 'route-a-runtime-git-remote-verify' -FilePath $gitPath -Arguments @('-C', $sourceRoot, 'remote', 'get-url', 'origin') -WorkingDirectory $workspace.work_directory
    $headResult = Invoke-RouteACommand -CommandId 'route-a-runtime-git-head-verify' -FilePath $gitPath -Arguments @('-C', $sourceRoot, 'rev-parse', 'HEAD') -WorkingDirectory $workspace.work_directory
    $statusResult = Invoke-RouteACommand -CommandId 'route-a-runtime-git-status-verify' -FilePath $gitPath -Arguments @('-C', $sourceRoot, 'status', '--porcelain=v1') -WorkingDirectory $workspace.work_directory
    $submoduleResult = Invoke-RouteACommand -CommandId 'route-a-runtime-git-submodules-verify' -FilePath $gitPath -Arguments @('-C', $sourceRoot, 'submodule', 'status', '--recursive') -WorkingDirectory $workspace.work_directory
    foreach ($verification in @($remoteResult, $headResult, $statusResult, $submoduleResult)) {
        Assert-ExitZero -Result $verification -Description $verification.record.command_id
    }

    $actualRemote = Read-CommandOutput $remoteResult
    $actualHead = Read-CommandOutput $headResult
    $sourceStatus = Read-CommandOutput $statusResult
    $submoduleLines = @(
        (Read-CommandOutput $submoduleResult) -split "`r?`n" |
            Where-Object { $_ }
    )
    if ($actualRemote -ne $SourceRepository -or $actualHead -ne $SourceCommit -or $sourceStatus) {
        throw 'Route A Runtime source provenance does not match the exact clean source boundary.'
    }
    if (@($submoduleLines | Where-Object { $_ -match '^[\-+U]' }).Count -ne 0) {
        throw 'Route A Runtime recursive submodule provenance is incomplete.'
    }

    # Hash the exact controlling build document from the exact checked-out source.
    $documentPath = Join-Path $sourceRoot $BuildDocument
    if (-not (Test-Path -LiteralPath $documentPath -PathType Leaf)) {
        throw "Pinned Runtime build document is missing: $documentPath"
    }
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'source-provenance.json') -Value ([ordered]@{
        schema_version = '1.0'
        campaign_id = 'GTQ-WB05-MF-v1'
        route_id = $RouteId
        component = $Component
        requested_repository = $SourceRepository
        actual_repository = $actualRemote
        requested_commit = $SourceCommit
        actual_commit = $actualHead
        clean_before_configure = $true
        recursive_submodule_count = $submoduleLines.Count
        recursive_submodules_complete = $true
        build_document = $BuildDocument
        build_document_sha256 = (Get-FileHash -LiteralPath $documentPath -Algorithm SHA256).Hash.ToLowerInvariant()
        source_root = $sourceRoot
    })

    # Configure with the exact pre-approved CPU-only/resource-control values.
    $configureArguments = @(
        '-S', $sourceRoot,
        '-B', $buildRoot,
        '-G', $Generator,
        '-A', 'x64',
        '-DCMAKE_BUILD_TYPE=Release',
        '-DENABLE_INTEL_GPU=OFF',
        '-DENABLE_INTEL_NPU=OFF',
        '-DENABLE_TESTS=OFF',
        '-DENABLE_FUNCTIONAL_TESTS=OFF',
        '-DENABLE_SAMPLES=ON',
        '-DENABLE_PYTHON=ON',
        '-DENABLE_WHEEL=OFF',
        "-DPython3_EXECUTABLE=$PythonPath"
    )
    $configure = Invoke-RouteACommand -CommandId 'route-a-runtime-configure' -FilePath $CMakePath -Arguments $configureArguments -WorkingDirectory $workspace.work_directory -MonitorResources
    if (Test-Wb05SafetyStop -Result $configure) {
        Complete-RouteARuntimeEvidence -Status 'Infrastructure interrupted' -Reasons @('Route A Runtime configure was terminated by the reviewed resource-safety boundary.')
        return
    }
    if ($configure.record.exit_code -ne 0) {
        Complete-RouteARuntimeEvidence -Status 'Failed' -Reasons @("Route A Runtime configure exited with code $($configure.record.exit_code).")
        return
    }

    # Require a generated cache and verify that the materialised build controls
    # still match the reviewed command rather than trusting command intent alone.
    $cachePath = Join-Path $buildRoot 'CMakeCache.txt'
    if (-not (Test-Path -LiteralPath $cachePath -PathType Leaf)) {
        throw 'Route A Runtime configure returned success without CMakeCache.txt.'
    }
    $cacheLines = Get-Content -LiteralPath $cachePath
    $cacheValues = [ordered]@{}
    foreach ($name in @(
        'CMAKE_GENERATOR',
        'CMAKE_GENERATOR_PLATFORM',
        'ENABLE_INTEL_GPU',
        'ENABLE_INTEL_NPU',
        'ENABLE_TESTS',
        'ENABLE_FUNCTIONAL_TESTS',
        'ENABLE_SAMPLES',
        'ENABLE_PYTHON',
        'ENABLE_WHEEL',
        'Python3_EXECUTABLE'
    )) {
        $cacheValues[$name] = Get-CMakeCacheValue -Lines $cacheLines -Name $name
    }
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'cmake-cache-summary.json') -Value ([ordered]@{
        schema_version = '1.0'
        sha256 = (Get-FileHash -LiteralPath $cachePath -Algorithm SHA256).Hash.ToLowerInvariant()
        values = $cacheValues
    })

    $cacheMatches = (
        $cacheValues.CMAKE_GENERATOR -eq $Generator -and
        $cacheValues.CMAKE_GENERATOR_PLATFORM -eq 'x64' -and
        $cacheValues.ENABLE_INTEL_GPU -eq 'OFF' -and
        $cacheValues.ENABLE_INTEL_NPU -eq 'OFF' -and
        $cacheValues.ENABLE_TESTS -eq 'OFF' -and
        $cacheValues.ENABLE_FUNCTIONAL_TESTS -eq 'OFF' -and
        $cacheValues.ENABLE_SAMPLES -eq 'ON' -and
        $cacheValues.ENABLE_PYTHON -eq 'ON' -and
        $cacheValues.ENABLE_WHEEL -eq 'OFF'
    )
    if (-not $cacheMatches) {
        Complete-RouteARuntimeEvidence -Status 'Blocked' -Reasons @('Generated Route A Runtime CMake cache does not match the reviewed CPU-only build controls.')
        return
    }

    # Record TBB files materialised by the pinned Runtime configuration. Missing
    # TBB evidence blocks the build because the approved Windows document names
    # this runtime dependency explicitly.
    $tbbRoot = Join-Path $sourceRoot 'temp'
    $tbbFiles = @(
        Get-ChildItem -LiteralPath $tbbRoot -Filter 'tbb*.dll' -File -Recurse -ErrorAction SilentlyContinue |
            Sort-Object FullName
    )
    if ($tbbFiles.Count -eq 0) {
        Complete-RouteARuntimeEvidence -Status 'Blocked' -Reasons @('The configured Runtime source exposed no TBB DLL to record as the documented runtime dependency.')
        return
    }
    $dependencies = @(
        foreach ($tbbFile in $tbbFiles) {
            [ordered]@{
                schema_version = '1.0'
                campaign_id = 'GTQ-WB05-MF-v1'
                record_type = 'build-dependency'
                route_id = $RouteId
                component = $Component
                name = $tbbFile.Name
                path = $tbbFile.FullName
                source = 'Pinned OpenVINO source temp directory produced during configure.'
                version = $null
                sha256 = (Get-FileHash -LiteralPath $tbbFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                purpose = 'TBB runtime dependency identified by the pinned Windows build document.'
                producer_command_id = 'route-a-runtime-configure'
            }
        }
    )
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'dependencies.json') -Value $dependencies

    # Build the full Runtime Release configuration with conservative parallelism.
    $build = Invoke-RouteACommand -CommandId 'route-a-runtime-build' -FilePath $CMakePath -Arguments @('--build', $buildRoot, '--config', 'Release', '--parallel', '2', '--verbose') -WorkingDirectory $workspace.work_directory -MonitorResources
    if (Test-Wb05SafetyStop -Result $build) {
        Complete-RouteARuntimeEvidence -Status 'Infrastructure interrupted' -Reasons @('Route A Runtime build was terminated by the reviewed resource-safety boundary.')
        return
    }
    if ($build.record.exit_code -ne 0) {
        Complete-RouteARuntimeEvidence -Status 'Failed' -Reasons @("Route A Runtime build exited with code $($build.record.exit_code).")
        return
    }

    # Install into the distinct i-ov directory so GenAI can later consume the
    # accepted Runtime package read-only without sharing its build directory.
    $install = Invoke-RouteACommand -CommandId 'route-a-runtime-install' -FilePath $CMakePath -Arguments @('--install', $buildRoot, '--config', 'Release', '--prefix', $installRoot) -WorkingDirectory $workspace.work_directory -MonitorResources
    if (Test-Wb05SafetyStop -Result $install) {
        Complete-RouteARuntimeEvidence -Status 'Infrastructure interrupted' -Reasons @('Route A Runtime install was terminated by the reviewed resource-safety boundary.')
        return
    }
    if ($install.record.exit_code -ne 0) {
        Complete-RouteARuntimeEvidence -Status 'Failed' -Reasons @("Route A Runtime install exited with code $($install.record.exit_code).")
        return
    }

    # Hash retained install outputs in place; no DLL/EXE/LIB/PDB/PYD is copied
    # into the evidence artifact or repository.
    $binaryRecords = Get-Wb05BinaryRecords -Root $installRoot -RouteId $RouteId -Component $Component -ProducerCommandId 'route-a-runtime-install'
    if ($binaryRecords.Count -eq 0) {
        Complete-RouteARuntimeEvidence -Status 'Blocked' -Reasons @('Route A Runtime install completed but produced no hashable binary outputs.')
        return
    }
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'binaries.json') -Value $binaryRecords

    # The final pass is intentionally build-only. Model execution and all later
    # activation/storage/performance/quality claims remain disabled.
    Complete-RouteARuntimeEvidence -Status 'Passed' -Reasons @('The exact pinned Route A OpenVINO Runtime configured, built, installed, and produced hashable outputs under the reviewed Windows build controls. No model or scientific claim is authorised.')
}
catch {
    # Integrity failures are recorded separately from a normal upstream build
    # failure so provenance/tooling defects cannot be misreported as source data.
    if (Test-Path -LiteralPath $OutputDirectory -PathType Container) {
        Write-Wb05Json -Path (Join-Path $OutputDirectory 'integrity-failure.json') -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            route_id = $RouteId
            component = $Component
            status = 'IntegrityFailure'
            source_commit = $SourceCommit
            message = $_.Exception.Message
            granite_model_test_authorised = $false
            activation_claim_authorised = $false
            packed_storage_claim_authorised = $false
            performance_claim_authorised = $false
            quality_claim_authorised = $false
        })
        Write-Wb05Manifest -EvidenceDirectory $OutputDirectory | Out-Null
    }
    throw
}
