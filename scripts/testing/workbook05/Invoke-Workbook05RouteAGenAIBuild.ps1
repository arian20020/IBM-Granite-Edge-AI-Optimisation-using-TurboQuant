[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$RepositoryRoot,
    [Parameter(Mandatory = $true)] [string]$OutputDirectory,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string]$RunIdentity,
    [Parameter(Mandatory = $true)] [string]$RuntimeInstallDirectory,
    [Parameter(Mandatory = $true)] [string]$RuntimeDecisionPath,
    [string]$PythonPath = 'C:\Program Files\Python312\python.exe'
)

<#
Build the exact OpenVINO GenAI revision against one previously accepted Route A
Runtime installation. The Runtime installation is consumed read-only; this GenAI
attempt always receives a fresh C:\w5a workspace. No model is downloaded or run.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RouteId = 'route-a-merged-openvino'
$Component = 'genai'
$RuntimeSourceCommit = 'b9a1f201c109e0bed74763934f79483cf6c4cbf4'
$GenAIRepository = 'https://github.com/openvinotoolkit/openvino.genai.git'
$GenAISourceCommit = 'bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0'
$BuildDocument = 'src/docs/BUILD.md'
$Generator = 'Visual Studio 17 2022'
$WorkspaceRoot = 'C:\w5a'
$ExpectedPythonVersion = 'Python 3.12.10'
$CMakePath = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'

function Write-GenAIDecision {
    param([string]$Status, [string[]]$Reasons)

    # This record is build-only. Every scientific/model claim stays disabled even
    # when the source-compatible GenAI stage itself succeeds.
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'decision.json') -Value ([ordered]@{
        schema_version = '1.0'; campaign_id = 'GTQ-WB05-MF-v1'
        record_type = 'build-decision'; route_id = $RouteId
        component = $Component; source_commit = $GenAISourceCommit
        status = $Status; reasons = @($Reasons); required_components = @()
        granite_model_test_authorised = $false
        activation_claim_authorised = $false
        packed_storage_claim_authorised = $false
        performance_claim_authorised = $false
        quality_claim_authorised = $false
    })
}

function Complete-GenAIEvidence {
    param([string]$Status, [string[]]$Reasons, [bool]$Retained, [string]$ConfigDirectory)

    # Keep the compatibility attempt and component decision under the same final
    # manifest so the hosted validator sees one consistent outcome.
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'compatibility-attempt.json') -Value ([ordered]@{
        schema_version = '1.0'; campaign_id = 'GTQ-WB05-MF-v1'
        record_type = 'build-compatibility-attempt'; route_id = $RouteId
        component = $Component; runtime_route_id = $RouteId
        runtime_source_commit = $RuntimeSourceCommit
        genai_source_commit = $GenAISourceCommit
        openvino_config_directory = $ConfigDirectory
        configure_command_id = 'route-a-genai-configure'
        build_command_id = 'route-a-genai-build'
        install_command_id = 'route-a-genai-install'
        status = $Status; retained = ($Retained -or $Status -eq 'Passed')
        reason = ($Reasons -join ' ')
        evidence_paths = @('compatibility-attempt.json')
    })
    Write-GenAIDecision -Status $Status -Reasons $Reasons
    Write-Wb05Manifest -EvidenceDirectory $OutputDirectory | Out-Null
    Write-Host "WORKBOOK05_ROUTE_A_GENAI_STATUS=$Status"
}

function Invoke-GenAICommand {
    param(
        [string]$Id,
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$WorkingDirectory,
        [switch]$MonitorResources
    )

    # All native commands use the shared argument-preserving, text-evidence and
    # resource-monitoring adapter instead of PowerShell command-string execution.
    Invoke-Wb05LoggedProcess -CommandId $Id -RouteId $RouteId -Component $Component `
        -FilePath $FilePath -ArgumentList $Arguments -WorkingDirectory $WorkingDirectory `
        -EvidenceDirectory (Join-Path $OutputDirectory 'commands') `
        -EvidenceRoot $OutputDirectory `
        -EnvironmentAllowlist @{ RUNNER_NAME = [string]$env:RUNNER_NAME; RUNNER_OS = [string]$env:RUNNER_OS } `
        -MonitorResources:$MonitorResources
}

function Assert-Passed {
    param([object]$Result, [string]$Name)

    # Source/provenance commands do not use resource monitoring. Any non-zero
    # result at this boundary is therefore an integrity prerequisite failure.
    if ($Result.record.exit_code -ne 0) {
        throw "$Name exited with code $($Result.record.exit_code)."
    }
}

function Read-Result {
    param([object]$Result)

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

function Get-CacheValue {
    param([string[]]$Lines, [string]$Name)

    # CMake materialises NAME:TYPE=value lines; missing controls return null and
    # therefore fail the later exact-cache comparison.
    $line = $Lines |
        Where-Object { $_ -match "^$([Regex]::Escape($Name)):[^=]+=" } |
        Select-Object -First 1
    if ($line) {
        return ($line -split '=', 2)[1]
    }
    return $null
}

function Test-Wb05SafetyStop {
    param([object]$Result)

    # A deliberate memory/commit/heartbeat safety termination is an execution-
    # environment interruption, not evidence that upstream GenAI source failed.
    return (
        $null -ne $Result.resource_summary -and
        $Result.resource_summary.safety_stop_triggered -eq $true
    )
}

function Assert-RouteARuntimeInstall {
    param([string]$Root, [string]$InstallDirectory)

    # The accepted Runtime must be a normal i-ov directory contained by C:\w5a;
    # no link/junction in its ancestry may redirect GenAI to unrelated content.
    $resolved = (Resolve-Path -LiteralPath $InstallDirectory).Path
    Assert-Wb05SafePath -Root $Root -Path $resolved | Out-Null
    if ((Split-Path -Leaf $resolved) -ne 'i-ov') {
        throw "RuntimeInstallDirectory must be an accepted Route A i-ov directory: $resolved"
    }
    $item = Get-Item -LiteralPath $resolved -Force
    if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Accepted Route A Runtime install is missing, not a directory, or a reparse point: $resolved"
    }
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $cursor = $item.Parent
    while (
        $null -ne $cursor -and
        -not $cursor.FullName.TrimEnd('\', '/').Equals(
            $rootFull,
            [StringComparison]::OrdinalIgnoreCase
        )
    ) {
        if (($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Accepted Route A Runtime install has a reparse-point ancestor: $($cursor.FullName)"
        }
        $cursor = $cursor.Parent
    }
    return $item.FullName
}

# Import the reviewed shared build module and freeze the expected Windows tools.
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$modulePath = Join-Path $RepositoryRoot 'scripts/testing/workbook05/Workbook05.Build.psm1'
if (-not (Test-Path -LiteralPath $modulePath -PathType Leaf)) {
    throw "Workbook 05 build module is missing: $modulePath"
}
Import-Module $modulePath -Force

foreach ($requiredFile in @($PythonPath, $CMakePath, $RuntimeDecisionPath)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required file is missing: $requiredFile"
    }
}
if (-not (Test-Path -LiteralPath $RuntimeInstallDirectory -PathType Container)) {
    throw "Route A Runtime install directory was not found: $RuntimeInstallDirectory"
}
$pythonVersion = ((& $PythonPath --version 2>&1) | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $pythonVersion -ne $ExpectedPythonVersion) {
    throw "Expected $ExpectedPythonVersion, found: $pythonVersion"
}
$gitPath = (
    Get-Command -Name 'git.exe' -CommandType Application -All -ErrorAction Stop |
        Select-Object -First 1
).Source

# Trust only a schema-shaped Runtime decision with the exact route/source and all
# later scientific authorisations still disabled.
$runtimeDecision = Get-Content -LiteralPath $RuntimeDecisionPath -Raw | ConvertFrom-Json
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
foreach ($flag in @(
    'granite_model_test_authorised',
    'activation_claim_authorised',
    'packed_storage_claim_authorised',
    'performance_claim_authorised',
    'quality_claim_authorised'
)) {
    if ($runtimeDecision.$flag -ne $false) {
        throw "Route A Runtime prerequisite unexpectedly authorises $flag."
    }
}

# Consume the accepted Runtime read-only and allocate an unrelated fresh GenAI
# source/build/install workspace for this workflow attempt.
$resolvedRuntimeInstall = Assert-RouteARuntimeInstall `
    -Root $WorkspaceRoot `
    -InstallDirectory $RuntimeInstallDirectory
$workspace = New-Wb05ExternalWorkspace -Root $WorkspaceRoot -RunIdentity $RunIdentity
$workDirectory = $workspace.work_directory
$sourceRoot = Join-Path $workDirectory 'genai'
$buildRoot = Join-Path $workDirectory 'b-genai'
$installRoot = Join-Path $workDirectory 'i-genai'
foreach ($path in @($sourceRoot, $buildRoot, $installRoot)) {
    if (Test-Path -LiteralPath $path) {
        throw "External GenAI path already exists and will not be reused: $path"
    }
}

# Bind CMake to exactly one package configuration from the accepted Runtime.
$openvinoConfigs = @(
    Get-ChildItem `
        -LiteralPath $resolvedRuntimeInstall `
        -Filter 'OpenVINOConfig.cmake' `
        -File `
        -Recurse
)
if ($openvinoConfigs.Count -ne 1) {
    throw "Expected exactly one installed OpenVINOConfig.cmake, found $($openvinoConfigs.Count)."
}
$openvinoConfigDirectory = $openvinoConfigs[0].DirectoryName

# Evidence directories are immutable per attempt and never silently reused.
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "Evidence directory already exists and will not be reused: $OutputDirectory"
}
New-Item -ItemType Directory -Path $OutputDirectory -Force:$false | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path

# Snapshot exactly the process-scoped variables that this stage may modify.
$environmentSnapshot = @{
    PATH = [Environment]::GetEnvironmentVariable('PATH', 'Process')
    PYTHONPATH = [Environment]::GetEnvironmentVariable('PYTHONPATH', 'Process')
    OPENVINO_LIB_PATHS = [Environment]::GetEnvironmentVariable('OPENVINO_LIB_PATHS', 'Process')
    OpenVINO_DIR = [Environment]::GetEnvironmentVariable('OpenVINO_DIR', 'Process')
}

try {
    # The pinned upstream BUILD.md explicitly permits this manual Windows setup as
    # an alternative to setupvars.ps1. Derive it only from the accepted install.
    $runtimeDllDirectories = @(
        Get-ChildItem -LiteralPath $resolvedRuntimeInstall -Filter '*.dll' -File -Recurse -ErrorAction SilentlyContinue |
            ForEach-Object DirectoryName |
            Sort-Object -Unique
    )
    $runtimePythonDirectories = @(
        Get-ChildItem -LiteralPath $resolvedRuntimeInstall -Filter '*.pyd' -File -Recurse -ErrorAction SilentlyContinue |
            ForEach-Object DirectoryName |
            Sort-Object -Unique
    )
    $env:OpenVINO_DIR = $openvinoConfigDirectory
    if ($runtimeDllDirectories.Count -gt 0) {
        $env:OPENVINO_LIB_PATHS = $runtimeDllDirectories -join ';'
        $env:PATH = $env:OPENVINO_LIB_PATHS + ';' + [string]$environmentSnapshot.PATH
    }
    if ($runtimePythonDirectories.Count -gt 0) {
        $env:PYTHONPATH = ($runtimePythonDirectories -join ';') + ';' + [string]$environmentSnapshot.PYTHONPATH
    }

    # Retain reproducibility metadata without serialising arbitrary environment
    # variables or credentials.
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'environment.json') -Value ([ordered]@{
        schema_version = '1.0'
        campaign_id = 'GTQ-WB05-MF-v1'
        route_id = $RouteId
        component = $Component
        runner_name = $env:RUNNER_NAME
        runner_os = $env:RUNNER_OS
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
    })
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'dependencies.json') -Value @([ordered]@{
        schema_version = '1.0'
        campaign_id = 'GTQ-WB05-MF-v1'
        record_type = 'build-dependency'
        route_id = $RouteId
        component = $Component
        name = 'OpenVINOConfig.cmake'
        path = $openvinoConfigs[0].FullName
        source = 'Exact accepted Route A Runtime install'
        version = $null
        sha256 = (Get-FileHash -LiteralPath $openvinoConfigs[0].FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        purpose = 'CMake package used to configure source-built OpenVINO GenAI.'
        producer_command_id = $null
    })

    # Acquire the exact GenAI revision and recursively initialise its submodules.
    $commands = @(
        @('route-a-genai-git-init', @('-c', 'core.longpaths=true', 'init', $sourceRoot), $workDirectory),
        @('route-a-genai-git-config-longpaths', @('-C', $sourceRoot, 'config', 'core.longpaths', 'true'), $workDirectory),
        @('route-a-genai-git-remote-add', @('-C', $sourceRoot, 'remote', 'add', 'origin', $GenAIRepository), $workDirectory),
        @('route-a-genai-git-fetch', @('-c', 'core.longpaths=true', '-C', $sourceRoot, 'fetch', '--depth=1', 'origin', $GenAISourceCommit), $workDirectory),
        @('route-a-genai-git-checkout', @('-c', 'core.longpaths=true', '-C', $sourceRoot, 'checkout', '--detach', $GenAISourceCommit), $workDirectory),
        @('route-a-genai-git-submodules', @('-c', 'core.longpaths=true', '-C', $sourceRoot, 'submodule', 'update', '--init', '--recursive'), $workDirectory)
    )
    foreach ($command in $commands) {
        $result = Invoke-GenAICommand `
            -Id $command[0] `
            -FilePath $gitPath `
            -Arguments $command[1] `
            -WorkingDirectory $command[2]
        Assert-Passed -Result $result -Name $command[0]
    }

    # Independently verify origin, exact detached commit, clean tree, and recursive
    # submodules before configuration begins.
    $remoteResult = Invoke-GenAICommand -Id 'route-a-genai-git-remote-verify' -FilePath $gitPath -Arguments @('-C', $sourceRoot, 'remote', 'get-url', 'origin') -WorkingDirectory $workDirectory
    $headResult = Invoke-GenAICommand -Id 'route-a-genai-git-head-verify' -FilePath $gitPath -Arguments @('-C', $sourceRoot, 'rev-parse', 'HEAD') -WorkingDirectory $workDirectory
    $statusResult = Invoke-GenAICommand -Id 'route-a-genai-git-status-verify' -FilePath $gitPath -Arguments @('-C', $sourceRoot, 'status', '--porcelain=v1') -WorkingDirectory $workDirectory
    $submoduleResult = Invoke-GenAICommand -Id 'route-a-genai-git-submodules-verify' -FilePath $gitPath -Arguments @('-C', $sourceRoot, 'submodule', 'status', '--recursive') -WorkingDirectory $workDirectory
    foreach ($pair in @(
        @($remoteResult, 'origin verification'),
        @($headResult, 'HEAD verification'),
        @($statusResult, 'status verification'),
        @($submoduleResult, 'submodule verification')
    )) {
        Assert-Passed -Result $pair[0] -Name $pair[1]
    }
    $actualRemote = Read-Result $remoteResult
    $actualHead = Read-Result $headResult
    $sourceStatus = Read-Result $statusResult
    $submoduleLines = @(
        (Read-Result $submoduleResult) -split "`r?`n" |
            Where-Object { $_ }
    )
    if ($actualRemote -ne $GenAIRepository -or $actualHead -ne $GenAISourceCommit -or $sourceStatus) {
        throw 'GenAI source provenance verification failed.'
    }
    if (@($submoduleLines | Where-Object { $_ -match '^[\-+U]' }).Count -ne 0) {
        throw 'GenAI recursive submodules are incomplete.'
    }

    # Hash the exact controlling GenAI BUILD.md and the exact Runtime prerequisite
    # records/configuration used by this compatibility build.
    $documentPath = Join-Path $sourceRoot $BuildDocument
    if (-not (Test-Path -LiteralPath $documentPath -PathType Leaf)) {
        throw "Pinned GenAI build document is missing: $documentPath"
    }
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'source-provenance.json') -Value ([ordered]@{
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
        build_document_sha256 = (Get-FileHash $documentPath -Algorithm SHA256).Hash.ToLowerInvariant()
        runtime_source_commit = $RuntimeSourceCommit
        runtime_decision_sha256 = (Get-FileHash $RuntimeDecisionPath -Algorithm SHA256).Hash.ToLowerInvariant()
        openvino_config_sha256 = (Get-FileHash $openvinoConfigs[0].FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        source_root = $sourceRoot
    })

    # Configure against one exact installed Runtime package and preserve the
    # argument array in evidence.
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
    $configure = Invoke-GenAICommand `
        -Id 'route-a-genai-configure' `
        -FilePath $CMakePath `
        -Arguments $configureArguments `
        -WorkingDirectory $workDirectory `
        -MonitorResources
    if (Test-Wb05SafetyStop -Result $configure) {
        Complete-GenAIEvidence `
            -Status 'Infrastructure interrupted' `
            -Reasons @('GenAI configure was terminated by the reviewed resource-safety boundary.') `
            -Retained $false `
            -ConfigDirectory $openvinoConfigDirectory
        return
    }
    if ($configure.record.exit_code -ne 0) {
        Complete-GenAIEvidence `
            -Status 'Failed' `
            -Reasons @("Route A GenAI configure exited with code $($configure.record.exit_code).") `
            -Retained $false `
            -ConfigDirectory $openvinoConfigDirectory
        return
    }

    # Verify the generated cache reflects the accepted package and Windows build
    # controls rather than trusting configure command intent alone.
    $cachePath = Join-Path $buildRoot 'CMakeCache.txt'
    if (-not (Test-Path -LiteralPath $cachePath -PathType Leaf)) {
        throw 'GenAI configure returned success without CMakeCache.txt.'
    }
    $cacheLines = Get-Content -LiteralPath $cachePath
    $cacheValues = [ordered]@{}
    foreach ($name in @(
        'CMAKE_GENERATOR',
        'CMAKE_GENERATOR_PLATFORM',
        'OpenVINO_DIR',
        'ENABLE_PYTHON',
        'ENABLE_JS',
        'Python3_EXECUTABLE'
    )) {
        $cacheValues[$name] = Get-CacheValue -Lines $cacheLines -Name $name
    }
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'cmake-cache-summary.json') -Value ([ordered]@{
        schema_version = '1.0'
        sha256 = (Get-FileHash $cachePath -Algorithm SHA256).Hash.ToLowerInvariant()
        values = $cacheValues
    })

    # A missing cache path remains a failure. Otherwise compare canonical Windows
    # path identity so equivalent separator/case forms do not create a false block.
    $openvinoConfigPathMatches = (
        -not [string]::IsNullOrWhiteSpace([string]$cacheValues.OpenVINO_DIR) -and
        (Test-Wb05SameWindowsPath `
            -Left $cacheValues.OpenVINO_DIR `
            -Right $openvinoConfigDirectory)
    )

    if (
        $cacheValues.CMAKE_GENERATOR -ne $Generator -or
        $cacheValues.CMAKE_GENERATOR_PLATFORM -ne 'x64' -or
        -not $openvinoConfigPathMatches -or
        $cacheValues.ENABLE_PYTHON -ne 'ON' -or
        $cacheValues.ENABLE_JS -ne 'OFF'
    ) {
        Complete-GenAIEvidence `
            -Status 'Blocked' `
            -Reasons @('Generated GenAI CMake cache does not match the approved source-built Runtime compatibility controls.') `
            -Retained $false `
            -ConfigDirectory $openvinoConfigDirectory
        return
    }

    # Build conservatively. A resource-safety stop is classified separately from
    # a genuine source/toolchain non-zero build result.
    $build = Invoke-GenAICommand `
        -Id 'route-a-genai-build' `
        -FilePath $CMakePath `
        -Arguments @('--build', $buildRoot, '--config', 'Release', '--parallel', '2', '--verbose') `
        -WorkingDirectory $workDirectory `
        -MonitorResources
    if (Test-Wb05SafetyStop -Result $build) {
        Complete-GenAIEvidence `
            -Status 'Infrastructure interrupted' `
            -Reasons @('GenAI build was terminated by the reviewed resource-safety boundary.') `
            -Retained $false `
            -ConfigDirectory $openvinoConfigDirectory
        return
    }
    if ($build.record.exit_code -ne 0) {
        Complete-GenAIEvidence `
            -Status 'Failed' `
            -Reasons @("Route A GenAI build exited with code $($build.record.exit_code).") `
            -Retained $false `
            -ConfigDirectory $openvinoConfigDirectory
        return
    }

    # Install into a separate i-genai tree. Again distinguish a safety stop from
    # an upstream build/install failure.
    $install = Invoke-GenAICommand `
        -Id 'route-a-genai-install' `
        -FilePath $CMakePath `
        -Arguments @('--install', $buildRoot, '--config', 'Release', '--prefix', $installRoot) `
        -WorkingDirectory $workDirectory `
        -MonitorResources
    if (Test-Wb05SafetyStop -Result $install) {
        Complete-GenAIEvidence `
            -Status 'Infrastructure interrupted' `
            -Reasons @('GenAI install was terminated by the reviewed resource-safety boundary.') `
            -Retained $false `
            -ConfigDirectory $openvinoConfigDirectory
        return
    }
    if ($install.record.exit_code -ne 0) {
        Complete-GenAIEvidence `
            -Status 'Failed' `
            -Reasons @("Route A GenAI install exited with code $($install.record.exit_code).") `
            -Retained $false `
            -ConfigDirectory $openvinoConfigDirectory
        return
    }

    # Hash installed outputs in place; executable payloads never enter GitHub
    # evidence artifacts.
    $binaryRecords = Get-Wb05BinaryRecords `
        -Root $installRoot `
        -RouteId $RouteId `
        -Component $Component `
        -ProducerCommandId 'route-a-genai-install'
    if ($binaryRecords.Count -eq 0) {
        throw 'GenAI install produced no hashable binary outputs.'
    }
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'binaries.json') -Value $binaryRecords

    Complete-GenAIEvidence `
        -Status 'Passed' `
        -Reasons @('The exact GenAI source built against the exact accepted Route A Runtime package. No model, activation, storage, fallback, performance, or quality claim is authorised.') `
        -Retained $true `
        -ConfigDirectory $openvinoConfigDirectory
}
catch {
    # Unexpected integrity/tooling exceptions remain distinct from an ordinary
    # recorded source failure and keep all later authorisations false.
    if (Test-Path -LiteralPath $OutputDirectory -PathType Container) {
        Write-Wb05Json -Path (Join-Path $OutputDirectory 'integrity-failure.json') -Value ([ordered]@{
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
        })
        Write-Wb05Manifest -EvidenceDirectory $OutputDirectory | Out-Null
    }
    throw
}
finally {
    # Always return the runner process environment to its incoming state so one
    # staged build cannot contaminate a later run.
    Restore-Wb05Environment -Snapshot $environmentSnapshot
}
