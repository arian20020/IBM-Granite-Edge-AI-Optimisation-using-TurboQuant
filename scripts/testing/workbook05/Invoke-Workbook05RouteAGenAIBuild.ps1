[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string]$RunIdentity,

    [Parameter(Mandatory = $true)]
    [string]$RuntimeInstallDirectory,

    [Parameter(Mandatory = $true)]
    [string]$RuntimeDecisionPath,

    [string]$PythonPath = 'C:\Program Files\Python312\python.exe'
)

<#
.SYNOPSIS
Builds OpenVINO GenAI against the exact accepted Workbook 05 Route A Runtime install.

.DESCRIPTION
The script accepts only a passed Runtime decision for the exact Route A source,
finds one installed OpenVINO CMake package, acquires the pinned GenAI source,
and configures, builds, and installs it in separate external directories. It
records a compatibility attempt and hashes outputs in place. It does not execute
a model or authorise activation, storage, performance, or quality claims.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Freeze the exact source and toolchain boundary approved at Checkpoint B1.
$RouteId = 'route-a-merged-openvino'
$Component = 'genai'
$RuntimeSourceCommit = 'b9a1f201c109e0bed74763934f79483cf6c4cbf4'
$GenAIRepository = 'https://github.com/openvinotoolkit/openvino.genai.git'
$GenAISourceCommit = '05e5c7670b597746f858946974d11f38e3baf42f'
$BuildDocument = 'src/docs/BUILD.md'
$Generator = 'Visual Studio 17 2022'
$WorkspaceRoot = 'C:\w5a'
$ExpectedPythonVersion = 'Python 3.12.10'
$CMakePath = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'

function Write-RouteAGenAIDecision {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Passed', 'Failed', 'Blocked', 'Infrastructure interrupted')]
        [string]$Status,

        [Parameter(Mandatory = $true)]
        [string[]]$Reasons
    )

    # Building GenAI proves only one source-matched compatibility boundary.
    # All later scientific and model permissions remain disabled.
    $decision = [ordered]@{
        schema_version = '1.0'
        campaign_id = 'GTQ-WB05-MF-v1'
        record_type = 'build-decision'
        route_id = $RouteId
        component = $Component
        source_commit = $GenAISourceCommit
        status = $Status
        reasons = @($Reasons)
        required_components = @()
        granite_model_test_authorised = $false
        activation_claim_authorised = $false
        packed_storage_claim_authorised = $false
        performance_claim_authorised = $false
        quality_claim_authorised = $false
    }
    Write-Wb05Json `
        -Path (Join-Path $OutputDirectory 'decision.json') `
        -Value $decision
}

function Write-RouteACompatibilityAttempt {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Passed', 'Failed', 'Blocked', 'Infrastructure interrupted')]
        [string]$Status,

        [Parameter(Mandatory = $true)]
        [bool]$Retained,

        [Parameter(Mandatory = $true)]
        [string]$Reason,

        [Parameter(Mandatory = $true)]
        [string]$OpenVINOConfigDirectory
    )

    # The compatibility record links the exact Runtime and GenAI revisions to
    # their configure/build/install command evidence without copying binaries.
    $attempt = [ordered]@{
        schema_version = '1.0'
        campaign_id = 'GTQ-WB05-MF-v1'
        record_type = 'build-compatibility-attempt'
        route_id = $RouteId
        component = $Component
        runtime_route_id = $RouteId
        runtime_source_commit = $RuntimeSourceCommit
        genai_source_commit = $GenAISourceCommit
        openvino_config_directory = $OpenVINOConfigDirectory
        configure_command_id = 'route-a-genai-configure'
        build_command_id = 'route-a-genai-build'
        install_command_id = 'route-a-genai-install'
        status = $Status
        retained = $Retained
        reason = $Reason
        evidence_paths = @('compatibility-attempt.json')
    }
    if ($Status -eq 'Passed') {
        # A passed compatibility record is always retained for later review.
        $attempt.retained = $true
    }
    Write-Wb05Json `
        -Path (Join-Path $OutputDirectory 'compatibility-attempt.json') `
        -Value $attempt
}

function Complete-RouteAGenAIEvidence {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Passed', 'Failed', 'Blocked', 'Infrastructure interrupted')]
        [string]$Status,

        [Parameter(Mandatory = $true)]
        [string[]]$Reasons,

        [Parameter(Mandatory = $true)]
        [bool]$Retained,

        [Parameter(Mandatory = $true)]
        [string]$OpenVINOConfigDirectory
    )

    Write-RouteACompatibilityAttempt `
        -Status $Status `
        -Retained $Retained `
        -Reason ($Reasons -join ' ') `
        -OpenVINOConfigDirectory $OpenVINOConfigDirectory
    Write-RouteAGenAIDecision -Status $Status -Reasons $Reasons
    Write-Wb05Manifest -EvidenceDirectory $OutputDirectory | Out-Null
    Write-Host "WORKBOOK05_ROUTE_A_GENAI_STATUS=$Status"
}

function Invoke-RouteAGenAICommand {
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

    return Invoke-Wb05LoggedProcess `
        -CommandId $CommandId `
        -RouteId $RouteId `
        -Component $Component `
        -FilePath $FilePath `
        -ArgumentList $Arguments `
        -WorkingDirectory $WorkingDirectory `
        -EvidenceDirectory (Join-Path $OutputDirectory 'commands') `
        -EnvironmentAllowlist @{
            RUNNER_NAME = [string]$env:RUNNER_NAME
            RUNNER_OS = [string]$env:RUNNER_OS
        } `
        -MonitorResources:$MonitorResources
}

function Assert-CommandPassed {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Result,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if ($Result.record.exit_code -ne 0) {
        throw "$Description exited with code $($Result.record.exit_code)."
    }
}

function Read-CommandText {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Result
    )

    return (
        Get-Content -LiteralPath $Result.stdout_path -Raw -ErrorAction Stop
    ).Trim()
}

function Get-CMakeCacheValue {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Lines,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $line = $Lines |
        Where-Object {
            $_ -match "^$([Regex]::Escape($Name)):[^=]+="
        } |
        Select-Object -First 1

    if (-not $line) {
        return $null
    }
    return ($line -split '=', 2)[1]
}

$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$modulePath = Join-Path $RepositoryRoot 'scripts/testing/workbook05/Workbook05.Build.psm1'
if (-not (Test-Path -LiteralPath $modulePath -PathType Leaf)) {
    throw "Workbook 05 build module is missing: $modulePath"
}
Import-Module $modulePath -Force

# Refuse missing tools and prerequisite records before touching the external
# source workspace or changing any process environment variable.
if (-not (Test-Path -LiteralPath $PythonPath -PathType Leaf)) {
    throw "Pinned Python was not found at: $PythonPath"
}
if (-not (Test-Path -LiteralPath $CMakePath -PathType Leaf)) {
    throw "Pinned CMake was not found at: $CMakePath"
}
if (-not (Test-Path -LiteralPath $RuntimeDecisionPath -PathType Leaf)) {
    throw "Route A Runtime decision was not found: $RuntimeDecisionPath"
}
if (-not (Test-Path -LiteralPath $RuntimeInstallDirectory -PathType Container)) {
    throw "Route A Runtime install directory was not found: $RuntimeInstallDirectory"
}

$pythonVersion = ((& $PythonPath --version 2>&1) | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $pythonVersion -ne $ExpectedPythonVersion) {
    throw "Expected $ExpectedPythonVersion, found: $pythonVersion"
}
$gitCommand = Get-Command `
    -Name 'git.exe' `
    -CommandType Application `
    -All `
    -ErrorAction Stop |
    Select-Object -First 1
$gitPath = $gitCommand.Source

$runtimeDecision = Get-Content `
    -LiteralPath $RuntimeDecisionPath `
    -Raw `
    -ErrorAction Stop |
    ConvertFrom-Json
if ($runtimeDecision.status -ne 'Passed') {
    throw "Route A Runtime prerequisite is not Passed: $($runtimeDecision.status)"
}
if ($runtimeDecision.component -ne 'runtime') {
    throw "Route A Runtime prerequisite has the wrong component: $($runtimeDecision.component)"
}
if ($runtimeDecision.source_commit -ne $RuntimeSourceCommit) {
    throw "Route A Runtime prerequisite has the wrong source commit: $($runtimeDecision.source_commit)"
}
if ($runtimeDecision.route_id -ne $RouteId) {
    throw "Route A Runtime prerequisite has the wrong route: $($runtimeDecision.route_id)"
}
foreach ($claimFlag in @(
    'granite_model_test_authorised',
    'activation_claim_authorised',
    'packed_storage_claim_authorised',
    'performance_claim_authorised',
    'quality_claim_authorised'
)) {
    if ($runtimeDecision.$claimFlag -ne $false) {
        throw "Route A Runtime prerequisite unexpectedly authorises $claimFlag."
    }
}

# Resolve the existing Runtime workspace exactly; GenAI extends the same run
# identity but uses independent source, build, and install directories.
$resolvedWorkspaceRoot = (Resolve-Path -LiteralPath $WorkspaceRoot).Path
$rootItem = Get-Item -LiteralPath $resolvedWorkspaceRoot -Force
if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw "Route A workspace root must not be a reparse point: $resolvedWorkspaceRoot"
}
$workDirectory = Join-Path $resolvedWorkspaceRoot $RunIdentity
if (-not (Test-Path -LiteralPath $workDirectory -PathType Container)) {
    throw "Route A Runtime work directory does not exist: $workDirectory"
}
$workItem = Get-Item -LiteralPath $workDirectory -Force
if (($workItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw "Route A work directory must not be a reparse point: $workDirectory"
}
$workDirectory = $workItem.FullName

$expectedRuntimeInstall = Join-Path $workDirectory 'i-ov'
$resolvedRuntimeInstall = (Resolve-Path -LiteralPath $RuntimeInstallDirectory).Path
if (-not $resolvedRuntimeInstall.Equals(
    [IO.Path]::GetFullPath($expectedRuntimeInstall),
    [StringComparison]::OrdinalIgnoreCase
)) {
    throw "RuntimeInstallDirectory is not the exact Route A Runtime install for this run."
}

$sourceRoot = Join-Path $workDirectory 'genai'
$buildRoot = Join-Path $workDirectory 'b-genai'
$installRoot = Join-Path $workDirectory 'i-genai'
foreach ($path in @($sourceRoot, $buildRoot, $installRoot)) {
    if (Test-Path -LiteralPath $path) {
        throw "External GenAI path already exists and will not be reused: $path"
    }
}

# GenAI must resolve against exactly one CMake package from the accepted Runtime
# install. Zero or multiple results would make the compatibility attempt ambiguous.
$openvinoConfigs = @(
    Get-ChildItem `
        -LiteralPath $resolvedRuntimeInstall `
        -Filter 'OpenVINOConfig.cmake' `
        -File `
        -Recurse `
        -ErrorAction Stop
)
if ($openvinoConfigs.Count -ne 1) {
    throw "Expected exactly one installed OpenVINOConfig.cmake, found $($openvinoConfigs.Count)."
}
$openvinoConfigDirectory = $openvinoConfigs[0].DirectoryName

# Evidence is immutable for one attempt and never silently overwritten.
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "Evidence directory already exists and will not be reused: $OutputDirectory"
}
New-Item -ItemType Directory -Path $OutputDirectory -Force:$false | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path

# Snapshot every Runtime-related variable before adding the accepted Runtime
# installation to the GenAI build environment. The finally block restores it.
$environmentSnapshot = @{
    'PATH' = [Environment]::GetEnvironmentVariable('PATH', 'Process')
    'PYTHONPATH' = [Environment]::GetEnvironmentVariable('PYTHONPATH', 'Process')
    'OPENVINO_LIB_PATHS' = [Environment]::GetEnvironmentVariable('OPENVINO_LIB_PATHS', 'Process')
    'OpenVINO_DIR' = [Environment]::GetEnvironmentVariable('OpenVINO_DIR', 'Process')
}

try {
    $runtimeDllDirectories = @(
        Get-ChildItem `
            -LiteralPath $resolvedRuntimeInstall `
            -Filter '*.dll' `
            -File `
            -Recurse `
            -ErrorAction SilentlyContinue |
        ForEach-Object { $_.DirectoryName } |
        Sort-Object -Unique
    )
    $runtimePythonDirectories = @(
        Get-ChildItem `
            -LiteralPath $resolvedRuntimeInstall `
            -Filter '*.pyd' `
            -File `
            -Recurse `
            -ErrorAction SilentlyContinue |
        ForEach-Object { $_.DirectoryName } |
        Sort-Object -Unique
    )

    $env:OpenVINO_DIR = $openvinoConfigDirectory
    if ($runtimeDllDirectories.Count -gt 0) {
        $runtimeDllPath = $runtimeDllDirectories -join ';'
        $env:OPENVINO_LIB_PATHS = $runtimeDllPath
        $env:PATH = $runtimeDllPath + ';' + [string]$environmentSnapshot['PATH']
    }
    if ($runtimePythonDirectories.Count -gt 0) {
        $env:PYTHONPATH = (
            ($runtimePythonDirectories -join ';') + ';' +
            [string]$environmentSnapshot['PYTHONPATH']
        )
    }

    # Record the exact Runtime prerequisite and environment boundary without
    # exposing unrelated environment values or copying installed binaries.
    $operatingSystem = Get-CimInstance Win32_OperatingSystem
    $processor = Get-CimInstance Win32_Processor | Select-Object -First 1
    Write-Wb05Json `
        -Path (Join-Path $OutputDirectory 'environment.json') `
        -Value ([ordered]@{
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
            work_directory = $workDirectory
            runtime_install_directory = $resolvedRuntimeInstall
            openvino_config_directory = $openvinoConfigDirectory
            runtime_library_directory_count = $runtimeDllDirectories.Count
            runtime_python_directory_count = $runtimePythonDirectories.Count
        })

    $runtimeDependency = [ordered]@{
        schema_version = '1.0'
        campaign_id = 'GTQ-WB05-MF-v1'
        record_type = 'build-dependency'
        route_id = $RouteId
        component = $Component
        name = 'OpenVINOConfig.cmake'
        path = $openvinoConfigs[0].FullName
        source = 'Exact accepted Route A Runtime install'
        version = $null
        sha256 = (
            Get-FileHash -LiteralPath $openvinoConfigs[0].FullName -Algorithm SHA256
        ).Hash.ToLowerInvariant()
        purpose = 'CMake package used to configure source-built OpenVINO GenAI.'
        producer_command_id = $null
    }
    Write-Wb05Json `
        -Path (Join-Path $OutputDirectory 'dependencies.json') `
        -Value @($runtimeDependency)

    # Capture tool versions as explicit command records before source acquisition.
    $gitVersionResult = Invoke-RouteAGenAICommand `
        -CommandId 'route-a-genai-git-version' `
        -FilePath $gitPath `
        -Arguments @('--version') `
        -WorkingDirectory $workDirectory
    Assert-CommandPassed -Result $gitVersionResult -Description 'Git version check'

    $cmakeVersionResult = Invoke-RouteAGenAICommand `
        -CommandId 'route-a-genai-cmake-version' `
        -FilePath $CMakePath `
        -Arguments @('--version') `
        -WorkingDirectory $workDirectory
    Assert-CommandPassed -Result $cmakeVersionResult -Description 'CMake version check'

    # Acquire only the exact approved GenAI commit and initialise all recursive
    # submodules before configuration, matching the pinned build document order.
    $gitInit = Invoke-RouteAGenAICommand `
        -CommandId 'route-a-genai-git-init' `
        -FilePath $gitPath `
        -Arguments @('-c', 'core.longpaths=true', 'init', $sourceRoot) `
        -WorkingDirectory $workDirectory
    Assert-CommandPassed -Result $gitInit -Description 'GenAI Git init'

    $gitLongPaths = Invoke-RouteAGenAICommand `
        -CommandId 'route-a-genai-git-config-longpaths' `
        -FilePath $gitPath `
        -Arguments @('-C', $sourceRoot, 'config', 'core.longpaths', 'true') `
        -WorkingDirectory $workDirectory
    Assert-CommandPassed -Result $gitLongPaths -Description 'GenAI long-path configuration'

    $gitRemote = Invoke-RouteAGenAICommand `
        -CommandId 'route-a-genai-git-remote-add' `
        -FilePath $gitPath `
        -Arguments @('-C', $sourceRoot, 'remote', 'add', 'origin', $GenAIRepository) `
        -WorkingDirectory $workDirectory
    Assert-CommandPassed -Result $gitRemote -Description 'GenAI remote registration'

    $gitFetch = Invoke-RouteAGenAICommand `
        -CommandId 'route-a-genai-git-fetch' `
        -FilePath $gitPath `
        -Arguments @('-c', 'core.longpaths=true', '-C', $sourceRoot, 'fetch', '--depth=1', 'origin', $GenAISourceCommit) `
        -WorkingDirectory $workDirectory
    Assert-CommandPassed -Result $gitFetch -Description 'GenAI exact-commit fetch'

    $gitCheckout = Invoke-RouteAGenAICommand `
        -CommandId 'route-a-genai-git-checkout' `
        -FilePath $gitPath `
        -Arguments @('-c', 'core.longpaths=true', '-C', $sourceRoot, 'checkout', '--detach', $GenAISourceCommit) `
        -WorkingDirectory $workDirectory
    Assert-CommandPassed -Result $gitCheckout -Description 'GenAI detached checkout'

    $gitSubmodules = Invoke-RouteAGenAICommand `
        -CommandId 'route-a-genai-git-submodules' `
        -FilePath $gitPath `
        -Arguments @('-c', 'core.longpaths=true', '-C', $sourceRoot, 'submodule', 'update', '--init', '--recursive') `
        -WorkingDirectory $workDirectory
    Assert-CommandPassed -Result $gitSubmodules -Description 'GenAI recursive submodules'

    # Verify actual origin, HEAD, clean status, and recursive submodule state from
    # the acquired tree rather than trusting requested command arguments.
    $remoteVerify = Invoke-RouteAGenAICommand `
        -CommandId 'route-a-genai-git-remote-verify' `
        -FilePath $gitPath `
        -Arguments @('-C', $sourceRoot, 'remote', 'get-url', 'origin') `
        -WorkingDirectory $workDirectory
    Assert-CommandPassed -Result $remoteVerify -Description 'GenAI origin verification'

    $headVerify = Invoke-RouteAGenAICommand `
        -CommandId 'route-a-genai-git-head-verify' `
        -FilePath $gitPath `
        -Arguments @('-C', $sourceRoot, 'rev-parse', 'HEAD') `
        -WorkingDirectory $workDirectory
    Assert-CommandPassed -Result $headVerify -Description 'GenAI HEAD verification'

    $statusVerify = Invoke-RouteAGenAICommand `
        -CommandId 'route-a-genai-git-status-verify' `
        -FilePath $gitPath `
        -Arguments @('-C', $sourceRoot, 'status', '--porcelain=v1') `
        -WorkingDirectory $workDirectory
    Assert-CommandPassed -Result $statusVerify -Description 'GenAI clean status verification'

    $submodulesVerify = Invoke-RouteAGenAICommand `
        -CommandId 'route-a-genai-git-submodules-verify' `
        -FilePath $gitPath `
        -Arguments @('-C', $sourceRoot, 'submodule', 'status', '--recursive') `
        -WorkingDirectory $workDirectory
    Assert-CommandPassed -Result $submodulesVerify -Description 'GenAI submodule verification'

    $actualRemote = Read-CommandText -Result $remoteVerify
    $actualHead = Read-CommandText -Result $headVerify
    $sourceStatus = Read-CommandText -Result $statusVerify
    $submoduleText = Read-CommandText -Result $submodulesVerify
    $submoduleLines = @($submoduleText -split "`r?`n" | Where-Object { $_ })
    $invalidSubmodules = @($submoduleLines | Where-Object { $_ -match '^[\-+U]' })

    if ($actualRemote -ne $GenAIRepository) {
        throw "Unexpected Route A GenAI origin: $actualRemote"
    }
    if ($actualHead -ne $GenAISourceCommit) {
        throw "Unexpected Route A GenAI commit: $actualHead"
    }
    if ($sourceStatus) {
        throw "Route A GenAI source tree is not clean: $sourceStatus"
    }
    if ($invalidSubmodules.Count -ne 0) {
        throw "Route A GenAI recursive submodules are incomplete: $($invalidSubmodules -join '; ')"
    }

    $buildDocumentPath = Join-Path $sourceRoot $BuildDocument
    if (-not (Test-Path -LiteralPath $buildDocumentPath -PathType Leaf)) {
        throw "Pinned GenAI build document is missing: $buildDocumentPath"
    }

    Write-Wb05Json `
        -Path (Join-Path $OutputDirectory 'source-provenance.json') `
        -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            route_id = $RouteId
            component = $Component
            requested_repository = $GenAIRepository
            actual_repository = $actualRemote
            requested_commit = $GenAISourceCommit
            actual_commit = $actualHead
            clean_before_configure = $true
            recursive_submodule_count = $submoduleLines.Count
            recursive_submodules_complete = $true
            build_document = $BuildDocument
            build_document_sha256 = (
                Get-FileHash -LiteralPath $buildDocumentPath -Algorithm SHA256
            ).Hash.ToLowerInvariant()
            runtime_source_commit = $RuntimeSourceCommit
            runtime_decision_sha256 = (
                Get-FileHash -LiteralPath $RuntimeDecisionPath -Algorithm SHA256
            ).Hash.ToLowerInvariant()
            openvino_config_sha256 = (
                Get-FileHash -LiteralPath $openvinoConfigs[0].FullName -Algorithm SHA256
            ).Hash.ToLowerInvariant()
            git_core_longpaths = $true
            source_root = $sourceRoot
        })

    # Configure source-built GenAI against the exact accepted Runtime CMake
    # package. JavaScript is outside Workbook 05 and is explicitly disabled.
    $configureArguments = @(
        '-S', $sourceRoot,
        '-B', $buildRoot,
        '-G', $Generator,
        '-A', 'x64',
        '-DCMAKE_BUILD_TYPE=Release',
        "-DOpenVINO_DIR=$openvinoConfigDirectory",
        '-DENABLE_PYTHON=ON',
        '-DENABLE_JS=OFF',
        "-DPython3_EXECUTABLE=$PythonPath"
    )
    $configure = Invoke-RouteAGenAICommand `
        -CommandId 'route-a-genai-configure' `
        -FilePath $CMakePath `
        -Arguments $configureArguments `
        -WorkingDirectory $workDirectory `
        -MonitorResources
    if ($configure.record.exit_code -ne 0) {
        Complete-RouteAGenAIEvidence `
            -Status 'Failed' `
            -Reasons @("Route A GenAI configure exited with code $($configure.record.exit_code).") `
            -Retained $false `
            -OpenVINOConfigDirectory $openvinoConfigDirectory
        return
    }

    $cachePath = Join-Path $buildRoot 'CMakeCache.txt'
    if (-not (Test-Path -LiteralPath $cachePath -PathType Leaf)) {
        throw 'GenAI configure returned success without CMakeCache.txt.'
    }
    $cacheLines = Get-Content -LiteralPath $cachePath
    $cacheValues = [ordered]@{}
    foreach ($name in @(
        'CMAKE_HOME_DIRECTORY',
        'CMAKE_GENERATOR',
        'CMAKE_GENERATOR_PLATFORM',
        'CMAKE_BUILD_TYPE',
        'CMAKE_C_COMPILER',
        'CMAKE_CXX_COMPILER',
        'CMAKE_VS_WINDOWS_TARGET_PLATFORM_VERSION',
        'OpenVINO_DIR',
        'ENABLE_PYTHON',
        'ENABLE_JS',
        'Python3_EXECUTABLE'
    )) {
        $cacheValues[$name] = Get-CMakeCacheValue -Lines $cacheLines -Name $name
    }
    Write-Wb05Json `
        -Path (Join-Path $OutputDirectory 'cmake-cache-summary.json') `
        -Value ([ordered]@{
            schema_version = '1.0'
            sha256 = (
                Get-FileHash -LiteralPath $cachePath -Algorithm SHA256
            ).Hash.ToLowerInvariant()
            values = $cacheValues
        })

    $normalisedExpectedOpenVINO = $openvinoConfigDirectory.Replace('\', '/')
    if (
        $cacheValues.CMAKE_GENERATOR -ne $Generator -or
        $cacheValues.CMAKE_GENERATOR_PLATFORM -ne 'x64' -or
        $cacheValues.OpenVINO_DIR -ne $normalisedExpectedOpenVINO -or
        $cacheValues.ENABLE_PYTHON -ne 'ON' -or
        $cacheValues.ENABLE_JS -ne 'OFF'
    ) {
        Complete-RouteAGenAIEvidence `
            -Status 'Blocked' `
            -Reasons @('Generated GenAI CMake cache does not match the approved source-built Runtime compatibility controls.') `
            -Retained $false `
            -OpenVINOConfigDirectory $openvinoConfigDirectory
        return
    }

    $build = Invoke-RouteAGenAICommand `
        -CommandId 'route-a-genai-build' `
        -FilePath $CMakePath `
        -Arguments @('--build', $buildRoot, '--config', 'Release', '--parallel', '2', '--verbose') `
        -WorkingDirectory $workDirectory `
        -MonitorResources
    if ($build.record.exit_code -ne 0) {
        Complete-RouteAGenAIEvidence `
            -Status 'Failed' `
            -Reasons @("Route A GenAI build exited with code $($build.record.exit_code).") `
            -Retained $false `
            -OpenVINOConfigDirectory $openvinoConfigDirectory
        return
    }

    $install = Invoke-RouteAGenAICommand `
        -CommandId 'route-a-genai-install' `
        -FilePath $CMakePath `
        -Arguments @('--install', $buildRoot, '--config', 'Release', '--prefix', $installRoot) `
        -WorkingDirectory $workDirectory `
        -MonitorResources
    if ($install.record.exit_code -ne 0) {
        Complete-RouteAGenAIEvidence `
            -Status 'Failed' `
            -Reasons @("Route A GenAI install exited with code $($install.record.exit_code).") `
            -Retained $false `
            -OpenVINOConfigDirectory $openvinoConfigDirectory
        return
    }

    if (-not (Test-Path -LiteralPath $installRoot -PathType Container)) {
        throw 'GenAI install command passed but the install root is missing.'
    }
    $binaryRecords = Get-Wb05BinaryRecords `
        -Root $installRoot `
        -RouteId $RouteId `
        -Component $Component `
        -ProducerCommandId 'route-a-genai-install'
    if ($binaryRecords.Count -eq 0) {
        throw 'GenAI install produced no hashable binary outputs.'
    }
    Write-Wb05Json `
        -Path (Join-Path $OutputDirectory 'binaries.json') `
        -Value $binaryRecords

    Complete-RouteAGenAIEvidence `
        -Status 'Passed' `
        -Reasons @(
            'The exact GenAI source configured, built, and installed against the exact accepted Route A Runtime CMake package. This does not prove model execution, codec activation, packed allocation, fallback behaviour, performance, or quality.'
        ) `
        -Retained $true `
        -OpenVINOConfigDirectory $openvinoConfigDirectory
}
catch {
    # Preserve partial text evidence for integrity failures, while distinguishing
    # them from a captured compiler or compatibility outcome.
    if (Test-Path -LiteralPath $OutputDirectory -PathType Container) {
        $failure = [ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            route_id = $RouteId
            component = $Component
            status = 'IntegrityFailure'
            runtime_source_commit = $RuntimeSourceCommit
            genai_source_commit = $GenAISourceCommit
            message = $_.Exception.Message
            granite_model_test_authorised = $false
            activation_claim_authorised = $false
            packed_storage_claim_authorised = $false
            performance_claim_authorised = $false
            quality_claim_authorised = $false
        }
        Write-Wb05Json `
            -Path (Join-Path $OutputDirectory 'integrity-failure.json') `
            -Value $failure
        Write-Wb05Manifest -EvidenceDirectory $OutputDirectory | Out-Null
    }
    throw
}
finally { Restore-Wb05Environment -Snapshot $environmentSnapshot }
