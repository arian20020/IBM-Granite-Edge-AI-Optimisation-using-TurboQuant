[CmdletBinding()]
param(
    # Exact repository checkout bound to this workflow attempt.
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    # Fresh GitHub Actions identity for the new resume evidence workspace.
    [Parameter(Mandatory = $true)]
    [string] $RunId,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, [int]::MaxValue)]
    [int] $RunAttempt,

    # Extracted text-only artifact from failed live C1 run 32410714130.
    [Parameter(Mandatory = $true)]
    [string] $PriorBundleDirectory,

    # Independently accepted dependency decision supplied at dispatch.
    [Parameter(Mandatory = $true)]
    [string] $AcceptedDependencyDecisionSha256,

    # Fixed machine Python used for repository and evidence validation.
    [string] $BasePythonPath = 'C:\Program Files\Python312\python.exe'
)

<#
.SYNOPSIS
Resumes only the OpenVINO conversion portion of one failed Workbook 05 C1
attempt after revalidating its prior artifact and retained Granite source.

.DESCRIPTION
The prior artifact is treated as untrusted data and is bound to one exact failed
run before C:\w5m is accessed. Every retained Granite source file is then rehashed.
The previous evidence workspace and partial conversion remain unchanged. A new
attempt directory and a new conversion-output directory are created. Hugging Face
network access is disabled, and C1 still authorises no model execution, activation,
packed-storage, performance, or quality claim.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$AcceptedDecisionSha256 = (
    '429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49'
)
$AcceptedDependencyWorkspace = 'C:\w5c\dependency-preflight-32211117536-1'
$AcceptedDependencyEvidence = Join-Path $AcceptedDependencyWorkspace 'evidence'
$PriorRunId = '32410714130'
$PriorRunAttempt = 1
$PriorArtifactName = 'workbook-05-phase3-assets-32410714130-1'
$PriorArtifactDigest = (
    'sha256:33d3ca32236d6da973c21c5d95ba36d088c5ed7384d93eb3f353dbd081671257'
)
$PriorHeadSha = '80946fc2e06767a8aeff878deab7d31f10fd6676'
$RetainedSourceDirectory = 'C:\w5m\sources\granite41-3b-c0650403'
$ResolvedRevision = 'c0650403e44e78ec0262dab1c90914c65b196c4e'
$AggregateModelSha256 = (
    '58e7e6635ac57fbf201e4258cc17c01f9ac04a997bbd9cc5315ec52caba79956'
)
$AggregateTokenizerSha256 = (
    '21b6eb2dd3b049017077d62aaab88d38bbc639c6765e4bcb33c2bfc44e463b4e'
)
$ConversionMinimumFreeBytes = [int64]21474836480
$ResourceMinimumAvailableBytes = [int64]4294967296
$ResourceMaximumCommitPercent = [double]70

# This exact sequence distinguishes the recovery operation from a fresh C1 run.
$ApprovedStageOrder = @(
    'prerequisite-verification',
    'prior-artifact-validation',
    'retained-source-requalification',
    'conversion-disk-preflight',
    'resource-preflight',
    'conversion-new-output-directory',
    'converted-file-hash-inventory',
    'schema-validation',
    'manifest-generation'
)

function Assert-NormalDirectory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Label
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Label does not exist as a directory: $Path"
    }
    $Item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    if (
        -not $Item.PSIsContainer -or
        ($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
    ) {
        throw "$Label must be one normal directory: $Path"
    }
    return $Item.FullName
}

function Assert-RegularFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Label
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label does not exist as a regular file: $Path"
    }
    $Item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    if (
        $Item.PSIsContainer -or
        ($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
    ) {
        throw "$Label must be one normal regular file: $Path"
    }
    return $Item.FullName
}

function Write-AtomicJson {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][object] $Value
    )

    $TemporaryPath = "$Path.tmp"
    if (
        (Test-Path -LiteralPath $Path) -or
        (Test-Path -LiteralPath $TemporaryPath)
    ) {
        throw "Atomic C1 resume evidence destination already exists: $Path"
    }
    [IO.File]::WriteAllText(
        $TemporaryPath,
        ((ConvertTo-Json -InputObject $Value -Depth 64) + "`n"),
        [Text.UTF8Encoding]::new($false)
    )
    [IO.File]::Move($TemporaryPath, $Path)
}

function Write-Utf8Text {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string] $Text
    )

    if (Test-Path -LiteralPath $Path) {
        throw "C1 resume evidence text already exists: $Path"
    }
    [IO.File]::WriteAllText(
        $Path,
        $Text,
        [Text.UTF8Encoding]::new($false)
    )
}

function Assert-ControlledSuccess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][object] $Result,
        [Parameter(Mandatory = $true)][string] $Label
    )

    if ($Result.resource_summary.safety_stop_triggered -ne $false) {
        throw (
            "$Label triggered the resource watchdog: " +
            [string]$Result.resource_summary.safety_stop_reason
        )
    }
    if ([int]$Result.record.exit_code -ne 0) {
        throw "$Label exited with code $($Result.record.exit_code)."
    }
}

$CompletedStages = [System.Collections.Generic.List[string]]::new()
$StepDirectory = $null

function Start-ApprovedStage {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string] $Stage)

    $Index = $CompletedStages.Count
    if (
        $Index -ge $ApprovedStageOrder.Count -or
        $ApprovedStageOrder[$Index] -ne $Stage
    ) {
        $Expected = if ($Index -lt $ApprovedStageOrder.Count) {
            $ApprovedStageOrder[$Index]
        }
        else {
            '<none>'
        }
        throw (
            "C1 source-resume stage-order violation. Expected $Expected, " +
            "observed $Stage."
        )
    }
    $CompletedStages.Add($Stage)
    Write-AtomicJson `
        -Path (Join-Path $StepDirectory ('{0:D2}-{1}.json' -f ($Index + 1), $Stage)) `
        -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            route_id = 'route-a-merged-openvino'
            stage = $Stage
            status = 'Started'
            run_id = $RunId
            run_attempt = $RunAttempt
        })
}

# Validate immutable inputs before creating a new evidence workspace.
if ($RunId -notmatch '^[0-9]{8,}$') {
    throw "RunId is not a GitHub Actions run identity: $RunId"
}
if ($AcceptedDependencyDecisionSha256 -ne $AcceptedDecisionSha256) {
    throw 'The supplied dependency decision is not the accepted C1 prerequisite.'
}
$RepositoryRoot = Assert-NormalDirectory -Path $RepositoryRoot -Label 'Repository root'
$PriorBundleDirectory = Assert-NormalDirectory `
    -Path $PriorBundleDirectory `
    -Label 'Prior C1 artifact bundle'
$BasePythonPath = Assert-RegularFile `
    -Path $BasePythonPath `
    -Label 'Pinned machine Python'
$ObservedBasePython = ((& $BasePythonPath --version 2>&1) | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $ObservedBasePython -ne 'Python 3.12.10') {
    throw "Expected Python 3.12.10, observed: $ObservedBasePython"
}

$C1Root = Assert-NormalDirectory -Path 'C:\w5c' -Label 'C1 controlled root'
$AttemptRoot = Join-Path $C1Root "phase3-assets-$RunId-$RunAttempt-resume"
if (Test-Path -LiteralPath $AttemptRoot) {
    throw "C1 resume attempt already exists and will not be reused: $AttemptRoot"
}
New-Item -ItemType Directory -Path $AttemptRoot -Force:$false | Out-Null
$AttemptRoot = Assert-NormalDirectory -Path $AttemptRoot -Label 'C1 resume attempt'
$EvidenceRoot = Join-Path $AttemptRoot 'evidence'
$CommandDirectory = Join-Path $EvidenceRoot 'commands'
$LogDirectory = Join-Path $EvidenceRoot 'logs'
$DependencyDirectory = Join-Path $EvidenceRoot 'dependency'
$StepDirectory = Join-Path $EvidenceRoot 'steps'
$PriorEvidenceDirectory = Join-Path $EvidenceRoot 'prior-attempt'
$OfflineHfHome = Join-Path $AttemptRoot 'offline-hf-home'
foreach ($Directory in @(
    $EvidenceRoot,
    $CommandDirectory,
    $LogDirectory,
    $DependencyDirectory,
    $StepDirectory,
    $PriorEvidenceDirectory,
    $OfflineHfHome
)) {
    New-Item -ItemType Directory -Path $Directory -Force:$false | Out-Null
    Assert-NormalDirectory -Path $Directory -Label 'C1 resume directory' |
        Out-Null
}

$BaseModulePath = Join-Path `
    $RepositoryRoot `
    'scripts\testing\workbook05\Workbook05.Build.psm1'
$ProcessModulePath = Join-Path `
    $RepositoryRoot `
    'scripts\testing\workbook05\Workbook05.ControlledProcess.psm1'
Import-Module $BaseModulePath -Force -ErrorAction Stop
Import-Module $ProcessModulePath -Force -ErrorAction Stop

$OriginalPythonPath = $env:PYTHONPATH
$OriginalEnvironment = @{
    'HF_TOKEN' = $env:HF_TOKEN
    'HUGGING_FACE_HUB_TOKEN' = $env:HUGGING_FACE_HUB_TOKEN
    'HUGGINGFACE_HUB_TOKEN' = $env:HUGGINGFACE_HUB_TOKEN
    'HF_TOKEN_PATH' = $env:HF_TOKEN_PATH
    'HF_HOME' = $env:HF_HOME
    'HF_HUB_CACHE' = $env:HF_HUB_CACHE
    'HF_HUB_DISABLE_TELEMETRY' = $env:HF_HUB_DISABLE_TELEMETRY
    'HF_HUB_OFFLINE' = $env:HF_HUB_OFFLINE
    'TRANSFORMERS_OFFLINE' = $env:TRANSFORMERS_OFFLINE
    'TOKENIZERS_PARALLELISM' = $env:TOKENIZERS_PARALLELISM
}

try {
    Start-ApprovedStage -Stage 'prerequisite-verification'

    # Revalidate the exact accepted dependency environment and its decision.
    $DependencyProofPath = Join-Path `
        $EvidenceRoot `
        'dependency-acceptance-proof.json'
    $DependencyResult = Invoke-Wb05ControlledLoggedProcess `
        -CommandId 'dependency-acceptance' `
        -RouteId 'route-a-merged-openvino' `
        -Component 'assets' `
        -FilePath $BasePythonPath `
        -ArgumentList @(
            '-m',
            'scripts.testing.workbook05.phase3.dependency_acceptance',
            '--repository-root',
            $RepositoryRoot,
            '--accepted-decision-sha256',
            $AcceptedDecisionSha256,
            '--output',
            $DependencyProofPath
        ) `
        -WorkingDirectory $RepositoryRoot `
        -EvidenceDirectory $CommandDirectory `
        -EvidenceRoot $EvidenceRoot `
        -MaximumElapsedSeconds 900 `
        -LogFileExtension 'txt' `
        -AtomicJsonEvidence
    Assert-ControlledSuccess `
        -Result $DependencyResult `
        -Label 'Dependency acceptance verification'

    $DependencyProof = Get-Content `
        -LiteralPath $DependencyProofPath `
        -Raw `
        -Encoding UTF8 |
        ConvertFrom-Json
    if ($DependencyProof.status -ne 'Passed') {
        throw 'Dependency acceptance proof is not Passed.'
    }
    $AcceptedPythonPath = Assert-RegularFile `
        -Path ([string]$DependencyProof.python_executable_path) `
        -Label 'Accepted dependency Python'
    $OptimumCliPath = Assert-RegularFile `
        -Path ([string]$DependencyProof.optimum_cli_path) `
        -Label 'Accepted Optimum CLI'

    Copy-Item `
        -LiteralPath (Join-Path $AcceptedDependencyEvidence 'decision.json') `
        -Destination (Join-Path $DependencyDirectory 'decision.json') `
        -ErrorAction Stop

    # Revalidate accepted source-built Runtime and GenAI hand-offs.
    $PrerequisiteArguments = @(
        '-m',
        'scripts.testing.workbook05.phase3.prerequisites',
        '--runtime-install',
        'C:\w5a\phase2-31391119557-4\i-ov',
        '--runtime-decision',
        'C:\w5a\accepted-route-a-runtime-31656417607-1\decision.json',
        '--genai-install',
        'C:\w5a\phase2-31661571860-1\i-genai',
        '--genai-decision',
        'C:\w5a\accepted-route-a-genai-31661571860-1\decision.json',
        '--repository-root',
        $RepositoryRoot,
        '--output',
        (Join-Path $EvidenceRoot 'prerequisite-proof.json')
    )
    $PrerequisiteResult = Invoke-Wb05ControlledLoggedProcess `
        -CommandId 'phase2-prerequisites' `
        -RouteId 'route-a-merged-openvino' `
        -Component 'assets' `
        -FilePath $BasePythonPath `
        -ArgumentList $PrerequisiteArguments `
        -WorkingDirectory $RepositoryRoot `
        -EvidenceDirectory $CommandDirectory `
        -EvidenceRoot $EvidenceRoot `
        -MaximumElapsedSeconds 900 `
        -LogFileExtension 'txt' `
        -AtomicJsonEvidence
    Assert-ControlledSuccess `
        -Result $PrerequisiteResult `
        -Label 'Phase 2 prerequisite verification'

    Start-ApprovedStage -Stage 'prior-artifact-validation'

    # The downloaded prior artifact is untrusted until every failure, source,
    # command, resource, and non-claim relationship passes the strict validator.
    $PriorValidationPath = Join-Path `
        $EvidenceRoot `
        'prior-attempt-validation.json'
    $PriorValidationResult = Invoke-Wb05ControlledLoggedProcess `
        -CommandId 'validate-prior-bundle' `
        -RouteId 'route-a-merged-openvino' `
        -Component 'assets' `
        -FilePath $BasePythonPath `
        -ArgumentList @(
            '-m',
            'scripts.testing.workbook05.phase3.c1_resume',
            'validate-prior-bundle',
            '--bundle-root',
            $PriorBundleDirectory,
            '--expected-model-sha256',
            $AggregateModelSha256,
            '--expected-tokenizer-sha256',
            $AggregateTokenizerSha256,
            '--output',
            $PriorValidationPath
        ) `
        -WorkingDirectory $RepositoryRoot `
        -EvidenceDirectory $CommandDirectory `
        -EvidenceRoot $EvidenceRoot `
        -MaximumElapsedSeconds 1800 `
        -LogFileExtension 'txt' `
        -AtomicJsonEvidence
    Assert-ControlledSuccess `
        -Result $PriorValidationResult `
        -Label 'Prior C1 artifact validation'

    Write-AtomicJson `
        -Path (Join-Path $EvidenceRoot 'prior-artifact-identity.json') `
        -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            record_type = 'c1-prior-artifact-identity'
            route_id = 'route-a-merged-openvino'
            status = 'Passed'
            prior_run_id = $PriorRunId
            prior_run_attempt = $PriorRunAttempt
            prior_artifact_name = $PriorArtifactName
            prior_artifact_digest = $PriorArtifactDigest
            prior_head_sha = $PriorHeadSha
            model_download_authorised = $false
            granite_model_test_authorised = $false
            model_execution_authorised = $false
            activation_claim_authorised = $false
            packed_storage_claim_authorised = $false
            performance_claim_authorised = $false
            quality_claim_authorised = $false
        })

    # Preserve the exact validated text bundle inside the new evidence package.
    Get-ChildItem -LiteralPath $PriorBundleDirectory -Force |
        Copy-Item `
            -Destination $PriorEvidenceDirectory `
            -Recurse `
            -ErrorAction Stop

    Start-ApprovedStage -Stage 'retained-source-requalification'

    $SourceDirectory = Assert-NormalDirectory `
        -Path $RetainedSourceDirectory `
        -Label 'Retained immutable Granite source'
    $SourceProofPath = Join-Path $EvidenceRoot 'retained-source-proof.json'
    $SourceProofResult = Invoke-Wb05ControlledLoggedProcess `
        -CommandId 'rehash-retained-source' `
        -RouteId 'route-a-merged-openvino' `
        -Component 'assets' `
        -FilePath $BasePythonPath `
        -ArgumentList @(
            '-m',
            'scripts.testing.workbook05.phase3.c1_resume',
            'rehash-retained-source',
            '--bundle-root',
            $PriorBundleDirectory,
            '--source-directory',
            $SourceDirectory,
            '--expected-source-directory',
            $RetainedSourceDirectory,
            '--output',
            $SourceProofPath
        ) `
        -WorkingDirectory $RepositoryRoot `
        -EvidenceDirectory $CommandDirectory `
        -EvidenceRoot $EvidenceRoot `
        -MaximumElapsedSeconds 7200 `
        -LogFileExtension 'txt' `
        -AtomicJsonEvidence
    Assert-ControlledSuccess `
        -Result $SourceProofResult `
        -Label 'Retained Granite source requalification'

    # Reuse only the validated source identity records, never an earlier success.
    Copy-Item `
        -LiteralPath (Join-Path $PriorBundleDirectory 'resolved-model.json') `
        -Destination (Join-Path $EvidenceRoot 'resolved-model.json') `
        -ErrorAction Stop
    Copy-Item `
        -LiteralPath (Join-Path $PriorBundleDirectory 'source-files.csv') `
        -Destination (Join-Path $EvidenceRoot 'source-files.csv') `
        -ErrorAction Stop

    Start-ApprovedStage -Stage 'conversion-disk-preflight'

    $DiskResult = Invoke-Wb05ControlledLoggedProcess `
        -CommandId 'conversion-disk-preflight' `
        -RouteId 'route-a-merged-openvino' `
        -Component 'assets' `
        -FilePath $BasePythonPath `
        -ArgumentList @(
            '-m',
            'scripts.testing.workbook05.phase3.c1_resume',
            'conversion-disk-preflight',
            '--drive-root',
            'C:\',
            '--minimum-free-bytes',
            [string]$ConversionMinimumFreeBytes,
            '--output',
            (Join-Path $EvidenceRoot 'disk-preflight.json')
        ) `
        -WorkingDirectory $RepositoryRoot `
        -EvidenceDirectory $CommandDirectory `
        -EvidenceRoot $EvidenceRoot `
        -MaximumElapsedSeconds 900 `
        -LogFileExtension 'txt' `
        -AtomicJsonEvidence
    Assert-ControlledSuccess `
        -Result $DiskResult `
        -Label 'Conversion-only disk preflight'

    Start-ApprovedStage -Stage 'resource-preflight'

    # Require an idle baseline before starting the memory-intensive conversion.
    $OperatingSystem = Get-CimInstance Win32_OperatingSystem
    $Memory = Get-CimInstance Win32_PerfFormattedData_PerfOS_Memory
    $AvailableMemoryBytes = [int64]$OperatingSystem.FreePhysicalMemory * 1024
    $CommitPercent = [double]$Memory.PercentCommittedBytesInUse
    $ConflictingNames = @('cmake', 'MSBuild', 'cl', 'link', 'ninja', 'devenv', 'optimum-cli')
    $ConflictingProcesses = @(
        Get-Process -ErrorAction SilentlyContinue |
            Where-Object { $_.ProcessName -in $ConflictingNames } |
            Select-Object -ExpandProperty ProcessName -Unique
    )
    $ResourceStatus = if (
        $AvailableMemoryBytes -ge $ResourceMinimumAvailableBytes -and
        $CommitPercent -le $ResourceMaximumCommitPercent -and
        $ConflictingProcesses.Count -eq 0
    ) {
        'Passed'
    }
    else {
        'Blocked'
    }
    Write-AtomicJson `
        -Path (Join-Path $EvidenceRoot 'resource-preflight.json') `
        -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            record_type = 'c1-resume-resource-preflight'
            route_id = 'route-a-merged-openvino'
            status = $ResourceStatus
            available_memory_bytes = $AvailableMemoryBytes
            minimum_available_memory_bytes = $ResourceMinimumAvailableBytes
            commit_percent = $CommitPercent
            maximum_commit_percent = $ResourceMaximumCommitPercent
            conflicting_processes = @($ConflictingProcesses)
            model_download_authorised = $false
            granite_model_test_authorised = $false
            model_execution_authorised = $false
            activation_claim_authorised = $false
            packed_storage_claim_authorised = $false
            performance_claim_authorised = $false
            quality_claim_authorised = $false
        })
    if ($ResourceStatus -ne 'Passed') {
        throw (
            'C1 conversion resource preflight is blocked. Close memory-intensive ' +
            'applications and ensure at least 4 GiB physical memory is available, ' +
            'Windows commit is at most 70 percent, and no conflicting build or ' +
            'conversion process is active.'
        )
    }

    Start-ApprovedStage -Stage 'conversion-new-output-directory'

    $ModelRoot = Assert-NormalDirectory -Path 'C:\w5m' -Label 'Model root'
    $ConvertedParent = Join-Path $ModelRoot 'converted'
    if (-not (Test-Path -LiteralPath $ConvertedParent)) {
        New-Item `
            -ItemType Directory `
            -Path $ConvertedParent `
            -Force:$false | Out-Null
    }
    $ConvertedParent = Assert-NormalDirectory `
        -Path $ConvertedParent `
        -Label 'Converted-model parent'
    $ConvertedDirectory = Join-Path `
        $ConvertedParent `
        "granite41-3b-int4a-g128-r100-c0650403-resume-$RunId-$RunAttempt"
    if (Test-Path -LiteralPath $ConvertedDirectory) {
        throw (
            'New C1 resume conversion destination already exists: ' +
            $ConvertedDirectory
        )
    }

    # Use only the retained local source. Public Hub and Transformers network
    # access are disabled, credentials are cleared, and remote code is forbidden.
    Remove-Item Env:PYTHONPATH -ErrorAction SilentlyContinue
    Remove-Item Env:HF_TOKEN -ErrorAction SilentlyContinue
    Remove-Item Env:HUGGING_FACE_HUB_TOKEN -ErrorAction SilentlyContinue
    Remove-Item Env:HUGGINGFACE_HUB_TOKEN -ErrorAction SilentlyContinue
    Remove-Item Env:HF_TOKEN_PATH -ErrorAction SilentlyContinue
    $env:HF_HOME = $OfflineHfHome
    $env:HF_HUB_CACHE = Join-Path $OfflineHfHome 'hub'
    $env:HF_HUB_DISABLE_TELEMETRY = '1'
    $env:HF_HUB_OFFLINE = '1'
    $env:TRANSFORMERS_OFFLINE = '1'
    $env:TOKENIZERS_PARALLELISM = 'false'

    $ConversionArguments = @(
        'export',
        'openvino',
        '--model',
        $SourceDirectory,
        '--task',
        'text-generation-with-past',
        '--weight-format',
        'int4',
        '--group-size',
        '128',
        '--ratio',
        '1.0',
        $ConvertedDirectory
    )
    $ConversionResult = Invoke-Wb05ControlledLoggedProcess `
        -CommandId 'conversion-native' `
        -RouteId 'route-a-merged-openvino' `
        -Component 'assets' `
        -FilePath $OptimumCliPath `
        -ArgumentList $ConversionArguments `
        -WorkingDirectory $RepositoryRoot `
        -EvidenceDirectory $CommandDirectory `
        -EvidenceRoot $EvidenceRoot `
        -EnvironmentAllowlist @{
            HF_HUB_OFFLINE = '1'
            TRANSFORMERS_OFFLINE = '1'
        } `
        -MaximumElapsedSeconds 21600 `
        -LogFileExtension 'txt' `
        -AtomicJsonEvidence `
        -MinimumAvailableMemoryBytes 536870912 `
        -LowMemoryCommitPercent 80 `
        -MaximumCommitPercent 90 `
        -ConsecutiveSafetySamples 5 `
        -RequireHighCommitForLowMemoryStop

    Copy-Item `
        -LiteralPath $ConversionResult.stdout_path `
        -Destination (Join-Path $LogDirectory 'conversion.stdout.txt') `
        -ErrorAction Stop
    Copy-Item `
        -LiteralPath $ConversionResult.stderr_path `
        -Destination (Join-Path $LogDirectory 'conversion.stderr.txt') `
        -ErrorAction Stop
    Write-AtomicJson `
        -Path (Join-Path $CommandDirectory 'conversion.json') `
        -Value ([ordered]@{
            file_path = $OptimumCliPath
            arguments = $ConversionArguments
            working_directory = $RepositoryRoot
            started_utc = [string]$ConversionResult.record.started_utc
            ended_utc = [string]$ConversionResult.record.ended_utc
            elapsed_seconds = [double]$ConversionResult.record.elapsed_seconds
            exit_code = [int]$ConversionResult.record.exit_code
            safety_stop_triggered = [bool](
                $ConversionResult.resource_summary.safety_stop_triggered
            )
            resource_summary_path = 'commands/conversion-native.resources.json'
            resource_csv_path = 'commands/conversion-native.resources.csv'
            stdout_path = 'logs/conversion.stdout.txt'
            stderr_path = 'logs/conversion.stderr.txt'
            hf_hub_offline = $true
            transformers_offline = $true
            remote_model_code_enabled = $false
        })
    Assert-ControlledSuccess `
        -Result $ConversionResult `
        -Label 'Granite 4.1 3B resumed OpenVINO conversion'

    Start-ApprovedStage -Stage 'converted-file-hash-inventory'
    Start-ApprovedStage -Stage 'schema-validation'

    # Record materialisation uses the repository validator, not the conversion venv.
    if ([string]::IsNullOrWhiteSpace($OriginalPythonPath)) {
        throw 'Repository validator PYTHONPATH is missing before C1 record generation.'
    }
    $env:PYTHONPATH = $OriginalPythonPath
    try {
        $RecordResult = Invoke-Wb05ControlledLoggedProcess `
            -CommandId 'asset-records' `
            -RouteId 'route-a-merged-openvino' `
            -Component 'assets' `
            -FilePath $BasePythonPath `
            -ArgumentList @(
                '-m',
                'scripts.testing.workbook05.phase3.live_asset_lock',
                'build-records',
                '--repository-root',
                $RepositoryRoot,
                '--evidence-root',
                $EvidenceRoot,
                '--source-directory',
                $SourceDirectory,
                '--converted-directory',
                $ConvertedDirectory,
                '--dependency-evidence',
                $AcceptedDependencyEvidence
            ) `
            -WorkingDirectory $RepositoryRoot `
            -EvidenceDirectory $CommandDirectory `
            -EvidenceRoot $EvidenceRoot `
            -MaximumElapsedSeconds 3600 `
            -LogFileExtension 'txt' `
            -AtomicJsonEvidence
        Assert-ControlledSuccess `
            -Result $RecordResult `
            -Label 'C1 resumed record generation'
    }
    finally {
        Remove-Item Env:PYTHONPATH -ErrorAction SilentlyContinue
    }

    Start-ApprovedStage -Stage 'manifest-generation'

    Write-AtomicJson `
        -Path (Join-Path $EvidenceRoot 'stage-order.json') `
        -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            route_id = 'route-a-merged-openvino'
            run_id = $RunId
            run_attempt = $RunAttempt
            operation = 'controlled-source-resume'
            stages = @($CompletedStages)
            status = 'Passed'
            model_execution_authorised = $false
            activation_claim_authorised = $false
            packed_storage_claim_authorised = $false
            performance_claim_authorised = $false
            quality_claim_authorised = $false
        })
    Write-Utf8Text `
        -Path (Join-Path $EvidenceRoot 'summary.md') `
        -Text (
            "# Workbook 05 controlled C1 source resume`n`n" +
            "The source from failed run $PriorRunId was validated as untrusted " +
            "data and rehashed before a new OpenVINO INT4 conversion was created " +
            "in a new directory. The failed workspace and partial output were " +
            "preserved. C1 authorises no model execution, activation, packed " +
            "storage, performance, or quality claim.`n"
        )

    & $BasePythonPath `
        -m scripts.testing.workbook05.hash_manifest `
        --root $EvidenceRoot `
        --output (Join-Path $EvidenceRoot 'manifest.sha256')
    if ($LASTEXITCODE -ne 0) {
        throw "C1 resume manifest generation exited with code $LASTEXITCODE."
    }

    $global:LASTEXITCODE = 0
    [pscustomobject]@{
        status = 'Passed'
        workspace = $AttemptRoot
        evidence = $EvidenceRoot
        source_directory = $SourceDirectory
        converted_directory = $ConvertedDirectory
        prior_run_id = $PriorRunId
        resolved_revision = $ResolvedRevision
        model_execution_authorised = $false
        activation_claim_authorised = $false
        packed_storage_claim_authorised = $false
        performance_claim_authorised = $false
        quality_claim_authorised = $false
    }
}
catch {
    $FailureMessage = $_.Exception.Message
    $ManifestPath = Join-Path $EvidenceRoot 'manifest.sha256'
    if (Test-Path -LiteralPath $ManifestPath -PathType Leaf) {
        [IO.File]::Delete($ManifestPath)
    }

    $FailurePath = Join-Path $EvidenceRoot 'failure.json'
    if (-not (Test-Path -LiteralPath $FailurePath)) {
        Write-AtomicJson `
            -Path $FailurePath `
            -Value ([ordered]@{
                schema_version = '1.0'
                campaign_id = 'GTQ-WB05-MF-v1'
                route_id = 'route-a-merged-openvino'
                run_id = $RunId
                run_attempt = $RunAttempt
                operation = 'controlled-source-resume'
                status = 'Failed'
                completed_stages = @($CompletedStages)
                message = $FailureMessage
                prior_failed_workspace_preserved = $true
                prior_partial_conversion_preserved = $true
                model_execution_authorised = $false
                activation_claim_authorised = $false
                packed_storage_claim_authorised = $false
                performance_claim_authorised = $false
                quality_claim_authorised = $false
            })
    }
    $global:LASTEXITCODE = 1
    throw
}
finally {
    if ($null -eq $OriginalPythonPath) {
        Remove-Item Env:PYTHONPATH -ErrorAction SilentlyContinue
    }
    else {
        $env:PYTHONPATH = $OriginalPythonPath
    }
    foreach ($Name in $OriginalEnvironment.Keys) {
        if ($null -eq $OriginalEnvironment[$Name]) {
            Remove-Item "Env:$Name" -ErrorAction SilentlyContinue
        }
        else {
            Set-Item "Env:$Name" -Value $OriginalEnvironment[$Name]
        }
    }
    Remove-Module 'Workbook05.ControlledProcess' -Force -ErrorAction SilentlyContinue
    Remove-Module 'Workbook05.Build' -Force -ErrorAction SilentlyContinue
}
