[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ResumeBundleDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ResumeWorkspaceDirectory,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9]+$')]
    [string]$ResumeRunId,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$ResumeRunAttempt,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ResumeArtifactName,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^sha256:[0-9a-f]{64}$')]
    [string]$ResumeExpectedArtifactDigest,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^sha256:[0-9a-f]{64}$')]
    [string]$ResumeActualArtifactDigest,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$ResumeHeadSha,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{64}$')]
    [string]$ExpectedCacheSha256,

    [ValidateRange(60, 43200)]
    [int]$InternalDeadlineSeconds = 39600,

    [string]$PythonPath = 'C:\Program Files\Python312\python.exe'
)

<#
.SYNOPSIS
Continues one exact, independently validated Route A Runtime build workspace.

.DESCRIPTION
The script never guesses that an existing workspace is reusable. It validates the
prior timeout artifact as untrusted data, re-establishes local path, source,
submodule, build-document and CMake-cache identity, then runs the same incremental
CMake/MSBuild command. A controlled eleven-hour deadline returns control in time
to write a complete decision before GitHub's twelve-hour outer job boundary.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RouteId = 'route-a-merged-openvino'
$Component = 'runtime'
$CampaignId = 'GTQ-WB05-MF-v1'
$SourceRepository = 'https://github.com/openvinotoolkit/openvino.git'
$SourceCommit = 'b9a1f201c109e0bed74763934f79483cf6c4cbf4'
$BuildDocument = 'docs/dev/build_windows.md'
$Generator = 'Visual Studio 17 2022'
$WorkspaceRoot = 'C:\w5a'
$ExpectedPythonVersion = 'Python 3.12.10'
$CMakePath = (
    'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\' +
    'CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
)
$RequiredGenAIFrontendHeaders = @(
    'runtime/include/openvino/frontend/onnx/extension/conversion.hpp',
    'runtime/include/openvino/frontend/tensorflow/extension/conversion.hpp'
)

function Write-RouteARuntimeResumeDecision {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet(
            'Passed',
            'Failed',
            'Blocked',
            'Infrastructure interrupted'
        )]
        [string]$Status,

        [Parameter(Mandatory = $true)]
        [string[]]$Reasons
    )

    $decision = [ordered]@{
        schema_version = '1.0'
        campaign_id = $CampaignId
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
    Write-Wb05Json `
        -Path (Join-Path $OutputDirectory 'decision.json') `
        -Value $decision
}

function Complete-RouteARuntimeResumeEvidence {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet(
            'Passed',
            'Failed',
            'Blocked',
            'Infrastructure interrupted'
        )]
        [string]$Status,

        [Parameter(Mandatory = $true)]
        [string[]]$Reasons
    )

    Write-RouteARuntimeResumeDecision `
        -Status $Status `
        -Reasons $Reasons
    Write-Wb05Manifest -EvidenceDirectory $OutputDirectory | Out-Null
    Write-Host "WORKBOOK05_ROUTE_A_RUNTIME_RESUME_STATUS=$Status"
}

function Assert-Wb05ResumeNormalDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [switch]$AllowMissing
    )

    $safePath = Assert-Wb05SafePath `
        -Root $WorkspaceRoot `
        -Path $Path

    if (-not (Test-Path -LiteralPath $safePath)) {
        if ($AllowMissing) {
            return $safePath
        }
        throw "Required resume directory is missing: $safePath"
    }

    $item = Get-Item -LiteralPath $safePath -Force
    if (-not $item.PSIsContainer) {
        throw "Resume path is not a directory: $safePath"
    }
    if (
        ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
    ) {
        throw "Resume directory must not be a reparse point: $safePath"
    }
    return $item.FullName
}

function Invoke-RouteAResumeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandId,

        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory
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
        }
}

function Invoke-RouteAControlledCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandId,

        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [int]$MaximumElapsedSeconds
    )

    return Invoke-Wb05ControlledLoggedProcess `
        -CommandId $CommandId `
        -RouteId $RouteId `
        -Component $Component `
        -FilePath $FilePath `
        -ArgumentList $Arguments `
        -WorkingDirectory $WorkingDirectory `
        -EvidenceDirectory (Join-Path $OutputDirectory 'commands') `
        -EvidenceRoot $OutputDirectory `
        -MaximumElapsedSeconds $MaximumElapsedSeconds `
        -EnvironmentAllowlist @{
            RUNNER_NAME = [string]$env:RUNNER_NAME
            RUNNER_OS = [string]$env:RUNNER_OS
        }
}

function Assert-RouteAExitZero {
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

function Read-RouteACommandOutput {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Result
    )

    $captured = Get-Content `
        -LiteralPath $Result.stdout_path `
        -Raw `
        -ErrorAction Stop
    if ($null -eq $captured) {
        return ''
    }
    return $captured.Trim()
}

function Get-RouteARemainingDeadlineSeconds {
    param(
        [Parameter(Mandatory = $true)]
        [DateTime]$DeadlineUtc
    )

    return [int][Math]::Floor(
        ($DeadlineUtc - [DateTime]::UtcNow).TotalSeconds
    )
}

$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$baseModule = Join-Path `
    $RepositoryRoot `
    'scripts/testing/workbook05/Workbook05.Build.psm1'
$controlledModule = Join-Path `
    $RepositoryRoot `
    'scripts/testing/workbook05/Workbook05.ControlledProcess.psm1'
foreach ($modulePath in @($baseModule, $controlledModule)) {
    if (-not (Test-Path -LiteralPath $modulePath -PathType Leaf)) {
        throw "Workbook 05 module is missing: $modulePath"
    }
}
Import-Module $baseModule -Force -ErrorAction Stop
Import-Module $controlledModule -Force -ErrorAction Stop

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
    Get-Command `
        -Name 'git.exe' `
        -CommandType Application `
        -All `
        -ErrorAction Stop |
        Select-Object -First 1
).Source

if ($ResumeExpectedArtifactDigest -ne $ResumeActualArtifactDigest) {
    throw 'Recorded and independently verified resume artifact digests differ.'
}

# Assert-Wb05SafePath can only enforce containment after its trusted root has
# been established. Reject a missing, non-directory, or reparse-point C:\w5a
# root before any resumable source, build, or install path is evaluated.
if (-not (Test-Path -LiteralPath $WorkspaceRoot -PathType Container)) {
    throw "Resume workspace root is missing or is not a directory: $WorkspaceRoot"
}
$workspaceRootItem = Get-Item `
    -LiteralPath $WorkspaceRoot `
    -Force `
    -ErrorAction Stop
if (
    ($workspaceRootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
) {
    throw "Resume workspace root must not be a reparse point: $WorkspaceRoot"
}
$WorkspaceRoot = $workspaceRootItem.FullName.TrimEnd('\')

if (Test-Path -LiteralPath $OutputDirectory) {
    throw "Evidence directory already exists and will not be reused: $OutputDirectory"
}
New-Item `
    -ItemType Directory `
    -Path $OutputDirectory `
    -Force:$false | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
New-Item `
    -ItemType Directory `
    -Path (Join-Path $OutputDirectory 'commands') `
    -Force:$false | Out-Null

try {
    # Never resume while a compiler or build tool may still own the workspace.
    $activeBuildProcesses = @(
        Get-Process `
            -Name 'cmake', 'MSBuild', 'cl', 'ninja', 'vctip' `
            -ErrorAction SilentlyContinue
    )
    if ($activeBuildProcesses.Count -ne 0) {
        $activeNames = @(
            $activeBuildProcesses |
                ForEach-Object { "$($_.ProcessName):$($_.Id)" }
        )
        throw (
            'A compiler or build process is still active: ' +
            ($activeNames -join ', ')
        )
    }

    $resumeWorkspace = Assert-Wb05ResumeNormalDirectory `
        -Path $ResumeWorkspaceDirectory
    $sourceRoot = Assert-Wb05ResumeNormalDirectory `
        -Path (Join-Path $resumeWorkspace 'ov')
    $buildRoot = Assert-Wb05ResumeNormalDirectory `
        -Path (Join-Path $resumeWorkspace 'b-ov')
    $installRoot = Assert-Wb05ResumeNormalDirectory `
        -Path (Join-Path $resumeWorkspace 'i-ov') `
        -AllowMissing

    # A partial install is not a trustworthy starting state. The install
    # directory may be absent or present but empty before the resumed install.
    if (Test-Path -LiteralPath $installRoot -PathType Container) {
        $installChildren = @(
            Get-ChildItem `
                -LiteralPath $installRoot `
                -Force `
                -ErrorAction Stop
        )
        if ($installChildren.Count -ne 0) {
            throw "Resume install directory is not empty: $installRoot"
        }
    }

    $ResumeBundleDirectory = (
        Resolve-Path -LiteralPath $ResumeBundleDirectory
    ).Path
    $resumeReport = Join-Path `
        $OutputDirectory `
        'resume-prerequisite-validation.md'
    $resumeValidation = Invoke-RouteAResumeCommand `
        -CommandId 'route-a-runtime-resume-prerequisite-validation' `
        -FilePath $PythonPath `
        -Arguments @(
            '-m',
            'scripts.testing.workbook05.' +
                'route_a_runtime_resume_bundle_validation',
            '--bundle-root',
            $ResumeBundleDirectory,
            '--expected-run-id',
            $ResumeRunId,
            '--expected-run-attempt',
            [string]$ResumeRunAttempt,
            '--expected-workspace',
            $resumeWorkspace,
            '--expected-cache-sha256',
            $ExpectedCacheSha256,
            '--report',
            $resumeReport
        ) `
        -WorkingDirectory $RepositoryRoot
    Assert-RouteAExitZero `
        -Result $resumeValidation `
        -Description 'Route A Runtime resume prerequisite validation'

    $priorEnvironment = Get-Content `
        -LiteralPath (
            Join-Path $ResumeBundleDirectory 'environment.json'
        ) `
        -Raw |
        ConvertFrom-Json
    $priorProvenance = Get-Content `
        -LiteralPath (
            Join-Path $ResumeBundleDirectory 'source-provenance.json'
        ) `
        -Raw |
        ConvertFrom-Json
    $priorCache = Get-Content `
        -LiteralPath (
            Join-Path $ResumeBundleDirectory 'cmake-cache-summary.json'
        ) `
        -Raw |
        ConvertFrom-Json

    Write-Wb05Json `
        -Path (Join-Path $OutputDirectory 'resume-prerequisite.json') `
        -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = $CampaignId
            route_id = $RouteId
            component = $Component
            artifact_name = $ResumeArtifactName
            workflow_run = $ResumeRunId
            run_attempt = $ResumeRunAttempt
            workflow_head_sha = $ResumeHeadSha
            recorded_artifact_digest = $ResumeExpectedArtifactDigest
            independently_verified_artifact_digest = (
                $ResumeActualArtifactDigest
            )
            workspace = $resumeWorkspace
            cmake_cache_sha256 = $ExpectedCacheSha256
            validation_report = 'resume-prerequisite-validation.md'
            accepted_only_for_controlled_resume = $true
        })

    # Preserve the prior successful configure command and its logs/resources
    # as text evidence, without copying any source, object, or binary payload.
    $priorConfigureFiles = @(
        Get-ChildItem `
            -LiteralPath (Join-Path $ResumeBundleDirectory 'commands') `
            -File `
            -Filter 'route-a-runtime-configure.*' `
            -ErrorAction Stop
    )
    foreach ($priorConfigureFile in $priorConfigureFiles) {
        Copy-Item `
            -LiteralPath $priorConfigureFile.FullName `
            -Destination (
                Join-Path `
                    (Join-Path $OutputDirectory 'commands') `
                    $priorConfigureFile.Name
            ) `
            -Force:$false `
            -ErrorAction Stop
    }

    $remote = Invoke-RouteAResumeCommand `
        -CommandId 'route-a-runtime-resume-git-remote-verify' `
        -FilePath $gitPath `
        -Arguments @('-C', $sourceRoot, 'remote', 'get-url', 'origin') `
        -WorkingDirectory $resumeWorkspace
    $head = Invoke-RouteAResumeCommand `
        -CommandId 'route-a-runtime-resume-git-head-verify' `
        -FilePath $gitPath `
        -Arguments @('-C', $sourceRoot, 'rev-parse', 'HEAD') `
        -WorkingDirectory $resumeWorkspace
    $status = Invoke-RouteAResumeCommand `
        -CommandId 'route-a-runtime-resume-git-status-verify' `
        -FilePath $gitPath `
        -Arguments @('-C', $sourceRoot, 'status', '--porcelain=v1') `
        -WorkingDirectory $resumeWorkspace
    $submodules = Invoke-RouteAResumeCommand `
        -CommandId 'route-a-runtime-resume-git-submodules-verify' `
        -FilePath $gitPath `
        -Arguments @('-C', $sourceRoot, 'submodule', 'status', '--recursive') `
        -WorkingDirectory $resumeWorkspace
    foreach ($verification in @($remote, $head, $status, $submodules)) {
        Assert-RouteAExitZero `
            -Result $verification `
            -Description $verification.record.command_id
    }

    $actualRemote = Read-RouteACommandOutput -Result $remote
    $actualHead = Read-RouteACommandOutput -Result $head
    $sourceStatus = Read-RouteACommandOutput -Result $status
    $submoduleLines = @(
        (Read-RouteACommandOutput -Result $submodules) -split "`r?`n" |
            Where-Object { $_ }
    )
    if ($actualRemote -ne $SourceRepository) {
        throw "Runtime origin mismatch: $actualRemote"
    }
    if ($actualHead -ne $SourceCommit) {
        throw "Runtime source commit mismatch: $actualHead"
    }
    if ($sourceStatus) {
        throw 'Runtime source tree is dirty and cannot be resumed.'
    }
    if (@($submoduleLines | Where-Object { $_ -match '^[\-+U]' }).Count -ne 0) {
        throw 'Runtime recursive submodule provenance is incomplete.'
    }

    $documentPath = Join-Path $sourceRoot $BuildDocument
    if (-not (Test-Path -LiteralPath $documentPath -PathType Leaf)) {
        throw "Pinned Runtime build document is missing: $documentPath"
    }
    $documentSha = (
        Get-FileHash -LiteralPath $documentPath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    if ($documentSha -ne [string]$priorProvenance.build_document_sha256) {
        throw 'The local Runtime build document differs from timeout evidence.'
    }

    $cachePath = Join-Path $buildRoot 'CMakeCache.txt'
    if (-not (Test-Path -LiteralPath $cachePath -PathType Leaf)) {
        throw "Resume CMake cache is missing: $cachePath"
    }
    $cacheSha = (
        Get-FileHash -LiteralPath $cachePath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    if (
        $cacheSha -ne $ExpectedCacheSha256 -or
        $cacheSha -ne [string]$priorCache.sha256
    ) {
        throw (
            'The local CMake cache digest does not match both the explicit ' +
            'and prior-artifact identities.'
        )
    }

    $cacheLines = Get-Content -LiteralPath $cachePath
    $cacheValues = [ordered]@{}
    foreach ($name in @(
        'CMAKE_GENERATOR',
        'CMAKE_GENERATOR_PLATFORM',
        'ENABLE_INTEL_CPU',
        'ENABLE_INTEL_GPU',
        'ENABLE_INTEL_NPU',
        'ENABLE_TESTS',
        'ENABLE_FUNCTIONAL_TESTS',
        'ENABLE_SAMPLES',
        'ENABLE_PYTHON',
        'ENABLE_WHEEL',
        'ENABLE_JS',
        'ENABLE_OV_IR_FRONTEND',
        'ENABLE_OV_ONNX_FRONTEND',
        'ENABLE_OV_PADDLE_FRONTEND',
        'ENABLE_OV_TF_FRONTEND',
        'ENABLE_OV_TF_LITE_FRONTEND',
        'ENABLE_OV_PYTORCH_FRONTEND',
        'ENABLE_OV_JAX_FRONTEND',
        'ENABLE_SYSTEM_PROTOBUF',
        'Python3_EXECUTABLE'
    )) {
        $cacheValues[$name] = Get-Wb05CMakeCacheValue `
            -Lines $cacheLines `
            -Name $name
    }

    $cacheMatches = (
        $cacheValues.CMAKE_GENERATOR -eq $Generator -and
        $cacheValues.CMAKE_GENERATOR_PLATFORM -eq 'x64' -and
        $cacheValues.ENABLE_INTEL_CPU -eq 'ON' -and
        $cacheValues.ENABLE_INTEL_GPU -eq 'OFF' -and
        $cacheValues.ENABLE_INTEL_NPU -eq 'OFF' -and
        $cacheValues.ENABLE_TESTS -eq 'OFF' -and
        $cacheValues.ENABLE_FUNCTIONAL_TESTS -eq 'OFF' -and
        $cacheValues.ENABLE_SAMPLES -eq 'OFF' -and
        $cacheValues.ENABLE_PYTHON -eq 'ON' -and
        $cacheValues.ENABLE_WHEEL -eq 'OFF' -and
        $cacheValues.ENABLE_JS -eq 'OFF' -and
        $cacheValues.ENABLE_OV_IR_FRONTEND -eq 'ON' -and
        $cacheValues.ENABLE_OV_ONNX_FRONTEND -eq 'ON' -and
        $cacheValues.ENABLE_OV_PADDLE_FRONTEND -eq 'OFF' -and
        $cacheValues.ENABLE_OV_TF_FRONTEND -eq 'ON' -and
        $cacheValues.ENABLE_OV_TF_LITE_FRONTEND -eq 'OFF' -and
        $cacheValues.ENABLE_OV_PYTORCH_FRONTEND -eq 'OFF' -and
        $cacheValues.ENABLE_OV_JAX_FRONTEND -eq 'OFF' -and
        $cacheValues.ENABLE_SYSTEM_PROTOBUF -eq 'OFF' -and
        (
            Test-Wb05SameWindowsPath `
                -Left ([string]$cacheValues.Python3_EXECUTABLE) `
                -Right $PythonPath
        )
    )
    if (-not $cacheMatches) {
        throw (
            'Generated Route A Runtime CMake cache does not match the ' +
            'reviewed CPU and GenAI frontend hand-off controls.'
        )
    }

    $operatingSystem = Get-CimInstance Win32_OperatingSystem
    $processor = Get-CimInstance Win32_Processor | Select-Object -First 1
    Write-Wb05Json `
        -Path (Join-Path $OutputDirectory 'environment.json') `
        -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = $CampaignId
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
            parallelism = 1
            work_directory = $resumeWorkspace
            source_directory = $sourceRoot
            build_directory = $buildRoot
            install_directory = $installRoot
        })
    Write-Wb05Json `
        -Path (Join-Path $OutputDirectory 'source-provenance.json') `
        -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = $CampaignId
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
            build_document_sha256 = $documentSha
            source_root = $sourceRoot
        })
    Write-Wb05Json `
        -Path (Join-Path $OutputDirectory 'cmake-cache-summary.json') `
        -Value ([ordered]@{
            schema_version = '1.0'
            sha256 = $cacheSha
            values = $cacheValues
        })

    $tbbRoot = Join-Path $sourceRoot 'temp'
    $tbbFiles = @(
        Get-ChildItem `
            -LiteralPath $tbbRoot `
            -Filter 'tbb*.dll' `
            -File `
            -Recurse `
            -ErrorAction SilentlyContinue |
            Sort-Object FullName
    )
    if ($tbbFiles.Count -eq 0) {
        throw 'The resumed Runtime source exposed no TBB dependency.'
    }
    $dependencies = @(
        foreach ($tbbFile in $tbbFiles) {
            [ordered]@{
                schema_version = '1.0'
                campaign_id = $CampaignId
                record_type = 'build-dependency'
                route_id = $RouteId
                component = $Component
                name = $tbbFile.Name
                path = $tbbFile.FullName
                source = 'Pinned OpenVINO source temp directory produced during the accepted configure boundary.'
                version = $null
                sha256 = (Get-FileHash -LiteralPath $tbbFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                purpose = 'TBB runtime dependency identified by the pinned Windows build document.'
                producer_command_id = 'route-a-runtime-configure'
            }
        }
    )
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'dependencies.json') -Value $dependencies

    $deadlineUtc = [DateTime]::UtcNow.AddSeconds($InternalDeadlineSeconds)
    $remainingBuildSeconds = Get-RouteARemainingDeadlineSeconds -DeadlineUtc $deadlineUtc
    if ($remainingBuildSeconds -le 0) {
        Complete-RouteARuntimeResumeEvidence `
            -Status 'Infrastructure interrupted' `
            -Reasons @('The controlled Runtime resume exhausted its internal deadline before compilation could begin.')
        return
    }

    $build = Invoke-RouteAControlledCommand `
        -CommandId 'route-a-runtime-build' `
        -FilePath $CMakePath `
        -Arguments @(
            '--build',
            $buildRoot,
            '--config',
            'Release',
            '--parallel',
            '1',
            '--verbose',
            '--',
            '/p:CL_MPCount=1'
        ) `
        -WorkingDirectory $resumeWorkspace `
        -MaximumElapsedSeconds $remainingBuildSeconds
    if ($build.resource_summary.safety_stop_triggered -eq $true) {
        Complete-RouteARuntimeResumeEvidence `
            -Status 'Infrastructure interrupted' `
            -Reasons @([string]$build.resource_summary.safety_stop_reason)
        return
    }
    if ($build.record.exit_code -ne 0) {
        Complete-RouteARuntimeResumeEvidence `
            -Status 'Failed' `
            -Reasons @("Route A Runtime build exited with code $($build.record.exit_code).")
        return
    }

    $remainingInstallSeconds = Get-RouteARemainingDeadlineSeconds -DeadlineUtc $deadlineUtc
    if ($remainingInstallSeconds -le 0) {
        Complete-RouteARuntimeResumeEvidence `
            -Status 'Infrastructure interrupted' `
            -Reasons @('The controlled Runtime resume exhausted its internal deadline before installation could begin.')
        return
    }
    if (-not (Test-Path -LiteralPath $installRoot)) {
        New-Item -ItemType Directory -Path $installRoot -Force:$false | Out-Null
    }

    $install = Invoke-RouteAControlledCommand `
        -CommandId 'route-a-runtime-install' `
        -FilePath $CMakePath `
        -Arguments @(
            '--install',
            $buildRoot,
            '--config',
            'Release',
            '--prefix',
            $installRoot
        ) `
        -WorkingDirectory $resumeWorkspace `
        -MaximumElapsedSeconds $remainingInstallSeconds
    if ($install.resource_summary.safety_stop_triggered -eq $true) {
        Complete-RouteARuntimeResumeEvidence `
            -Status 'Infrastructure interrupted' `
            -Reasons @([string]$install.resource_summary.safety_stop_reason)
        return
    }
    if ($install.record.exit_code -ne 0) {
        Complete-RouteARuntimeResumeEvidence `
            -Status 'Failed' `
            -Reasons @("Route A Runtime install exited with code $($install.record.exit_code).")
        return
    }

    $missingHeaders = @(
        foreach ($relativePath in $RequiredGenAIFrontendHeaders) {
            $headerPath = Join-Path $installRoot $relativePath
            if (-not (Test-Path -LiteralPath $headerPath -PathType Leaf)) {
                $relativePath
            }
        }
    )
    if ($missingHeaders.Count -ne 0) {
        Complete-RouteARuntimeResumeEvidence `
            -Status 'Blocked' `
            -Reasons @(
                'Route A Runtime install is missing OpenVINO frontend headers required by the pinned GenAI tokenizer: ' +
                ($missingHeaders -join ', ') +
                '.'
            )
        return
    }

    $binaryRecords = Get-Wb05BinaryRecords `
        -Root $installRoot `
        -RouteId $RouteId `
        -Component $Component `
        -ProducerCommandId 'route-a-runtime-install'
    if ($binaryRecords.Count -eq 0) {
        Complete-RouteARuntimeResumeEvidence `
            -Status 'Blocked' `
            -Reasons @('Route A Runtime install completed but produced no hashable binary outputs.')
        return
    }
    Write-Wb05Json -Path (Join-Path $OutputDirectory 'binaries.json') -Value $binaryRecords

    Complete-RouteARuntimeResumeEvidence `
        -Status 'Passed' `
        -Reasons @(
            'The exact pinned Route A OpenVINO Runtime resumed from the independently validated timeout workspace, completed its incremental build and install, retained both required GenAI frontend headers, and produced hashable outputs. No model or scientific claim is authorised.'
        )
}
catch {
    # An identity, path, provenance, cache, or orchestration failure is not an
    # upstream source failure. Preserve it separately and fail the workflow.
    if (Test-Path -LiteralPath $OutputDirectory -PathType Container) {
        $decisionPath = Join-Path $OutputDirectory 'decision.json'
        if (-not (Test-Path -LiteralPath $decisionPath -PathType Leaf)) {
            Write-Wb05Json `
                -Path (Join-Path $OutputDirectory 'integrity-failure.json') `
                -Value ([ordered]@{
                    schema_version = '1.0'
                    campaign_id = $CampaignId
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
    }
    throw
}
finally {
    Remove-Module 'Workbook05.ControlledProcess' -Force -ErrorAction SilentlyContinue
    Remove-Module 'Workbook05.Build' -Force -ErrorAction SilentlyContinue
}