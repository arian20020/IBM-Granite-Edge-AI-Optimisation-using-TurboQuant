[CmdletBinding()]
param(
    # Exact repository checkout bound to this workflow attempt.
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    # GitHub Actions identity used to create a fresh immutable C1 workspace.
    [Parameter(Mandatory = $true)]
    [string] $RunId,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, [int]::MaxValue)]
    [int] $RunAttempt,

    # Independently accepted dependency decision supplied at dispatch.
    [Parameter(Mandatory = $true)]
    [string] $AcceptedDependencyDecisionSha256,

    # Fixed machine Python used only to verify the accepted dependency boundary.
    [string] $BasePythonPath = 'C:\Program Files\Python312\python.exe'
)

<#
.SYNOPSIS
Locks the official IBM Granite 4.1 3B snapshot and one reviewed OpenVINO INT4
conversion after revalidating the exact accepted dependency workspace.

.DESCRIPTION
The dependency_acceptance verifier is deliberately invoked before the first
model repository or model-root operation. The script uploads text evidence only;
model and OpenVINO IR payloads remain under C:\w5m.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$AcceptedDecisionSha256 = (
    '429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49'
)
$AcceptedDependencyWorkspace = (
    'C:\w5c\dependency-preflight-32211117536-1'
)
$AcceptedDependencyEvidence = Join-Path `
    $AcceptedDependencyWorkspace `
    'evidence'

# Keep the approved sequence visible and fail closed if a later edit reorders it.
$ApprovedStageOrder = @(
    'prerequisite-verification',
    'path-root-verification',
    'disk-preflight',
    'immutable-revision-resolution',
    'source-snapshot-download',
    'source-file-hash-inventory',
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

    # Reject absent directories, symbolic links, junctions and reparse points.
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

    # A controlled executable or evidence input cannot be redirected by a link.
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

    # Publish through a sibling temporary file and never overwrite evidence.
    $TemporaryPath = "$Path.tmp"
    if (
        (Test-Path -LiteralPath $Path) -or
        (Test-Path -LiteralPath $TemporaryPath)
    ) {
        throw "Atomic C1 evidence destination already exists: $Path"
    }
    [IO.File]::WriteAllText(
        $TemporaryPath,
        (($Value | ConvertTo-Json -Depth 64) + "`n"),
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
        throw "C1 evidence text already exists: $Path"
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

    # A zero exit is not enough when the resource watchdog stopped the process.
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
        throw "C1 stage-order violation. Expected $Expected, observed $Stage."
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

# Verify immutable dispatch inputs before creating any attempt directory.
if ($RunId -notmatch '^[0-9]{8,}$') {
    throw "RunId is not a GitHub Actions run identity: $RunId"
}
if ($AcceptedDependencyDecisionSha256 -ne $AcceptedDecisionSha256) {
    throw (
        'The supplied dependency decision does not match the independently ' +
        'accepted C1 prerequisite.'
    )
}
$RepositoryRoot = Assert-NormalDirectory `
    -Path $RepositoryRoot `
    -Label 'Repository root'
$BasePythonPath = Assert-RegularFile `
    -Path $BasePythonPath `
    -Label 'Pinned machine Python'
$ObservedBasePython = ((& $BasePythonPath --version 2>&1) | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $ObservedBasePython -ne 'Python 3.12.10') {
    throw "Expected Python 3.12.10, observed: $ObservedBasePython"
}

# Create one fresh C1 evidence workspace. Existing attempts are never reused.
$C1Root = Assert-NormalDirectory -Path 'C:\w5c' -Label 'C1 controlled root'
$AttemptRoot = Join-Path $C1Root "phase3-assets-$RunId-$RunAttempt"
if (Test-Path -LiteralPath $AttemptRoot) {
    throw "C1 attempt already exists and will not be reused: $AttemptRoot"
}
New-Item -ItemType Directory -Path $AttemptRoot -Force:$false | Out-Null
$AttemptRoot = Assert-NormalDirectory -Path $AttemptRoot -Label 'C1 attempt'
$EvidenceRoot = Join-Path $AttemptRoot 'evidence'
$CommandDirectory = Join-Path $EvidenceRoot 'commands'
$LogDirectory = Join-Path $EvidenceRoot 'logs'
$DependencyDirectory = Join-Path $EvidenceRoot 'dependency'
$StepDirectory = Join-Path $EvidenceRoot 'steps'
foreach ($Directory in @(
    $EvidenceRoot,
    $CommandDirectory,
    $LogDirectory,
    $DependencyDirectory,
    $StepDirectory
)) {
    if (-not (Test-Path -LiteralPath $Directory)) {
        New-Item -ItemType Directory -Path $Directory -Force:$false | Out-Null
    }
    Assert-NormalDirectory -Path $Directory -Label 'C1 evidence directory' |
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

# Preserve the workflow process environment. Accepted-environment children must
# not inherit the temporary hosted/repository validator PYTHONPATH.
$OriginalPythonPath = $env:PYTHONPATH
$OriginalHfTelemetry = $env:HF_HUB_DISABLE_TELEMETRY
$OriginalTokenizersParallelism = $env:TOKENIZERS_PARALLELISM

try {
    Start-ApprovedStage -Stage 'prerequisite-verification'

    # Revalidate the exact dependency bundle, decision, Python, and Optimum CLI
    # before touching C:\w5m or contacting the model repository.
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

    # Preserve the exact accepted decision in the text-only C1 bundle.
    Copy-Item `
        -LiteralPath (Join-Path $AcceptedDependencyEvidence 'decision.json') `
        -Destination (Join-Path $DependencyDirectory 'decision.json') `
        -ErrorAction Stop

    # Revalidate the accepted source-built Runtime and GenAI hand-offs.
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

    Start-ApprovedStage -Stage 'path-root-verification'

    # Create only the approved normal model root. No prior asset child is reused.
    if (-not (Test-Path -LiteralPath 'C:\w5m')) {
        New-Item -ItemType Directory -Path 'C:\w5m' -Force:$false | Out-Null
    }
    $ModelRoot = Assert-NormalDirectory -Path 'C:\w5m' -Label 'Model root'

    Start-ApprovedStage -Stage 'disk-preflight'

    Remove-Item Env:PYTHONPATH -ErrorAction SilentlyContinue
    $env:HF_HUB_DISABLE_TELEMETRY = '1'
    $env:TOKENIZERS_PARALLELISM = 'false'
    $DiskResult = Invoke-Wb05ControlledLoggedProcess `
        -CommandId 'disk-preflight' `
        -RouteId 'route-a-merged-openvino' `
        -Component 'assets' `
        -FilePath $AcceptedPythonPath `
        -ArgumentList @(
            '-m',
            'scripts.testing.workbook05.phase3.live_asset_lock',
            'disk-preflight',
            '--evidence-root',
            $EvidenceRoot,
            '--model-root',
            $ModelRoot,
            '--probe-root',
            'C:\w5c',
            '--run-root',
            'C:\w5r',
            '--drive-root',
            'C:\'
        ) `
        -WorkingDirectory $RepositoryRoot `
        -EvidenceDirectory $CommandDirectory `
        -EvidenceRoot $EvidenceRoot `
        -MaximumElapsedSeconds 1800 `
        -LogFileExtension 'txt' `
        -AtomicJsonEvidence
    Assert-ControlledSuccess -Result $DiskResult -Label 'C1 disk preflight'

    Start-ApprovedStage -Stage 'immutable-revision-resolution'
    Start-ApprovedStage -Stage 'source-snapshot-download'

    # The one controlled Python process resolves `main` to a full Hub commit and
    # passes only that immutable commit to snapshot_download.
    $DownloadResult = Invoke-Wb05ControlledLoggedProcess `
        -CommandId 'granite41-3b-download' `
        -RouteId 'route-a-merged-openvino' `
        -Component 'assets' `
        -FilePath $AcceptedPythonPath `
        -ArgumentList @(
            '-m',
            'scripts.testing.workbook05.phase3.live_asset_lock',
            'resolve-download',
            '--model-root',
            $ModelRoot,
            '--evidence-root',
            $EvidenceRoot
        ) `
        -WorkingDirectory $RepositoryRoot `
        -EvidenceDirectory $CommandDirectory `
        -EvidenceRoot $EvidenceRoot `
        -MaximumElapsedSeconds 14400 `
        -LogFileExtension 'txt' `
        -AtomicJsonEvidence `
        -MinimumAvailableMemoryBytes 1610612736 `
        -MaximumCommitPercent 90
    Assert-ControlledSuccess `
        -Result $DownloadResult `
        -Label 'Granite 4.1 3B immutable snapshot download'

    Start-ApprovedStage -Stage 'source-file-hash-inventory'

    $ResolvedModel = Get-Content `
        -LiteralPath (Join-Path $EvidenceRoot 'resolved-model.json') `
        -Raw `
        -Encoding UTF8 |
        ConvertFrom-Json
    $SourceDirectory = Assert-NormalDirectory `
        -Path ([string]$ResolvedModel.source_directory) `
        -Label 'Immutable Granite source directory'

    Start-ApprovedStage -Stage 'conversion-new-output-directory'

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
    $RevisionPrefix = ([string]$ResolvedModel.resolved_revision).Substring(0, 8)
    $ConvertedDirectory = Join-Path `
        $ConvertedParent `
        "granite41-3b-int4a-g128-r100-$RevisionPrefix"
    if (Test-Path -LiteralPath $ConvertedDirectory) {
        throw (
            'Converted model destination already exists and will not be reused: ' +
            $ConvertedDirectory
        )
    }

    # Remote model code is intentionally absent. Arguments are one structured
    # array passed through the reviewed Windows CRT quoting adapter.
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
        -MaximumElapsedSeconds 21600 `
        -LogFileExtension 'txt' `
        -AtomicJsonEvidence `
        -MinimumAvailableMemoryBytes 1610612736 `
        -MaximumCommitPercent 90

    # Preserve exact native output at the paths required by the C1 bundle.
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
        })
    Assert-ControlledSuccess `
        -Result $ConversionResult `
        -Label 'Granite 4.1 3B OpenVINO conversion'

    Start-ApprovedStage -Stage 'converted-file-hash-inventory'
    Start-ApprovedStage -Stage 'schema-validation'

    $RecordResult = Invoke-Wb05ControlledLoggedProcess `
        -CommandId 'asset-records' `
        -RouteId 'route-a-merged-openvino' `
        -Component 'assets' `
        -FilePath $AcceptedPythonPath `
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
    Assert-ControlledSuccess -Result $RecordResult -Label 'C1 record generation'

    Start-ApprovedStage -Stage 'manifest-generation'

    Write-AtomicJson `
        -Path (Join-Path $EvidenceRoot 'stage-order.json') `
        -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            route_id = 'route-a-merged-openvino'
            run_id = $RunId
            run_attempt = $RunAttempt
            stages = @($CompletedStages)
            status = 'Passed'
        })
    Write-Utf8Text `
        -Path (Join-Path $EvidenceRoot 'summary.md') `
        -Text (
            "# Workbook 05 C1 live asset lock`n`n" +
            "The official Granite 4.1 3B source and reviewed OpenVINO INT4 " +
            "conversion were identity-locked. C1 authorises no model execution, " +
            "codec activation, packed-storage, performance, or quality claim.`n"
        )

    # Hash every text record last. Model and IR bytes remain outside this bundle.
    & $BasePythonPath `
        -m scripts.testing.workbook05.hash_manifest `
        --root $EvidenceRoot `
        --output (Join-Path $EvidenceRoot 'manifest.sha256')
    if ($LASTEXITCODE -ne 0) {
        throw "C1 manifest generation exited with code $LASTEXITCODE."
    }

    $global:LASTEXITCODE = 0
    [pscustomobject]@{
        status = 'Passed'
        workspace = $AttemptRoot
        evidence = $EvidenceRoot
        source_directory = $SourceDirectory
        converted_directory = $ConvertedDirectory
        model_execution_authorised = $false
        activation_claim_authorised = $false
        packed_storage_claim_authorised = $false
        performance_claim_authorised = $false
        quality_claim_authorised = $false
    }
}
catch {
    $FailureMessage = $_.Exception.Message

    # Never retain a success-shaped manifest after a failed attempt.
    $ManifestPath = Join-Path $EvidenceRoot 'manifest.sha256'
    if (Test-Path -LiteralPath $ManifestPath -PathType Leaf) {
        [IO.File]::Delete($ManifestPath)
    }

    # Retain a create-once failure record. No model directory is deleted.
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
                status = 'Failed'
                completed_stages = @($CompletedStages)
                message = $FailureMessage
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
    # Restore only the caller's process environment; retained assets stay intact.
    if ($null -eq $OriginalPythonPath) {
        Remove-Item Env:PYTHONPATH -ErrorAction SilentlyContinue
    }
    else {
        $env:PYTHONPATH = $OriginalPythonPath
    }
    if ($null -eq $OriginalHfTelemetry) {
        Remove-Item Env:HF_HUB_DISABLE_TELEMETRY -ErrorAction SilentlyContinue
    }
    else {
        $env:HF_HUB_DISABLE_TELEMETRY = $OriginalHfTelemetry
    }
    if ($null -eq $OriginalTokenizersParallelism) {
        Remove-Item Env:TOKENIZERS_PARALLELISM -ErrorAction SilentlyContinue
    }
    else {
        $env:TOKENIZERS_PARALLELISM = $OriginalTokenizersParallelism
    }
    Remove-Module 'Workbook05.ControlledProcess' -Force -ErrorAction SilentlyContinue
    Remove-Module 'Workbook05.Build' -Force -ErrorAction SilentlyContinue
}
