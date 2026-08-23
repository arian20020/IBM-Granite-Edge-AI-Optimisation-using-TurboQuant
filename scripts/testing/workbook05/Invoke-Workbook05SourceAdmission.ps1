<#
.SYNOPSIS
Runs the read-only Workbook 05 Phase 1 source-admission pipeline.

.DESCRIPTION
The script connects the already-tested preflight, workspace, source, document,
capability, CMake-audit, configure-generation, decision, checkpoint, and hash
components in the approved order. It never compiles OpenVINO or runs a model.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [string]$SettingsPath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Resolve only repository-controlled inputs. The external workspace is validated
# separately and is never made relative to the repository checkout.
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
if ([string]::IsNullOrWhiteSpace($SettingsPath)) {
    $SettingsPath = Join-Path $RepositoryRoot `
        'experiments/granite_turboquant_intel/configurations/workbook05/source-admission-settings.json'
}
$SettingsPath = (Resolve-Path -LiteralPath $SettingsPath).Path

# Import only the two reviewed modules required for machine observation,
# workspace validation, argument-list process execution, and pipeline control.
Import-Module `
    (Join-Path $RepositoryRoot 'scripts/testing/workbook05/Workbook05.Preflight.psm1') `
    -Force
Import-Module `
    (Join-Path $RepositoryRoot 'scripts/testing/workbook05/Workbook05.SourceAdmission.psm1') `
    -Force

$SourceAdmissionSettings = Get-Content -LiteralPath $SettingsPath -Raw |
    ConvertFrom-Json
$PreflightSettingsPath = Join-Path $RepositoryRoot `
    'experiments/granite_turboquant_intel/configurations/workbook05/preflight-settings.json'
$PreflightSettings = Get-Content -LiteralPath $PreflightSettingsPath -Raw |
    ConvertFrom-Json
$PythonPath = [string]$SourceAdmissionSettings.python_path

# Keep the approved order visible and immutable for reviewers and static tests.
$StepOrder = @(
    'preflight-validation'
    'workspace-validation'
    'measurement-control-capture'
    'route-a-runtime-verification'
    'route-a-genai-verification'
    'route-b-verification'
    'document-capture'
    'capability-inspection'
    'route-b-cmake-audit'
    'route-a-configure-probe'
    'route-decisions'
    'hashes'
)

# These files form the minimum complete R2 bundle. Route A configure evidence is
# conditional, so its presence is governed by the calculated route decision.
$RequiredBundleFiles = @(
    'preflight/preflight-report.json'
    'workspace/workspace-validation.json'
    'measurement/measurement-controls.json'
    'controls/campaign-manifest.json'
    'controls/route-a-source-admission.json'
    'controls/route-b-source-admission.json'
    'routes/route-a/source-tree-runtime.json'
    'routes/route-a/source-tree-genai.json'
    'routes/route-a/source-capabilities.json'
    'routes/route-b/source-tree-runtime.json'
    'routes/route-b/source-capabilities.json'
    'routes/route-b/cmake-test-discovery.json'
    'commands/route-a-merged-openvino-documented-commands.json'
    'commands/route-b-experimental-qjl-polar-documented-commands.json'
    'summary/source-admission-summary.json'
    'summary/source-admission-summary.md'
    'checkpoint/checkpoint.json'
    'orchestration-report.json'
    'hash-manifest.sha256'
)
$RequiredBundleFiles += @(
    $StepOrder | ForEach-Object { "steps/$_.json" }
)

# Capture simple values into a closure so the module invokes the exact reviewed
# repository, settings, interpreter, and timeout values supplied by this script.
$RepositoryRootForSteps = $RepositoryRoot
$SettingsPathForSteps = $SettingsPath
$PythonPathForSteps = $PythonPath
$PreflightSettingsForSteps = $PreflightSettings
$WorkspaceRootForSteps = [string]$SourceAdmissionSettings.workspace_root
$AdapterTimeoutSeconds = [Math]::Max(
    [int]$SourceAdmissionSettings.clone_timeout_seconds,
    [Math]::Max(
        [int]$SourceAdmissionSettings.submodule_timeout_seconds,
        [int]$SourceAdmissionSettings.configure_timeout_seconds
    )
)

$StepExecutor = {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StepId,

        [Parameter(Mandatory = $true)]
        [string]$EvidenceRoot
    )

    # Preflight reads machine state, writes evidence, and advances only the
    # runtime checkpoint copy. It never changes the repository checkpoint.
    if ($StepId -eq 'preflight-validation') {
        $preflightDirectory = Join-Path $EvidenceRoot 'preflight'
        $checkpointDirectory = Join-Path $EvidenceRoot 'checkpoint'
        $controlsDirectory = Join-Path $EvidenceRoot 'controls'
        $stepsDirectory = Join-Path $EvidenceRoot 'steps'
        New-Item -ItemType Directory -Path $preflightDirectory -Force | Out-Null
        New-Item -ItemType Directory -Path $checkpointDirectory -Force | Out-Null
        New-Item -ItemType Directory -Path $controlsDirectory -Force | Out-Null
        New-Item -ItemType Directory -Path $stepsDirectory -Force | Out-Null

        $campaignRoot = Join-Path $RepositoryRootForSteps `
            'experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1'
        Copy-Item `
            -LiteralPath (Join-Path $campaignRoot 'checkpoint.json') `
            -Destination (Join-Path $checkpointDirectory 'checkpoint.json')
        Copy-Item `
            -LiteralPath (Join-Path $campaignRoot 'campaign-manifest.json') `
            -Destination (Join-Path $controlsDirectory 'campaign-manifest.json')

        $observation = Get-Workbook05PreflightObservation `
            -RepositoryRoot $RepositoryRootForSteps `
            -OutputDirectory $preflightDirectory
        $evaluation = Test-Workbook05PreflightObservation `
            -Observation $observation `
            -Settings $PreflightSettingsForSteps
        Export-Workbook05PreflightEvidence `
            -Observation $observation `
            -Evaluation $evaluation `
            -OutputDirectory $preflightDirectory

        $reportPath = Join-Path $preflightDirectory 'preflight-report.json'
        $reportHash = (Get-FileHash -LiteralPath $reportPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $checkpointStatus = if ($evaluation.OverallStatus -eq 'Passed') {
            'Passed'
        }
        else {
            'Failed'
        }
        $checkpointArguments = @(
            '-m'
            'scripts.testing.workbook05.checkpoint'
            '--path'
            (Join-Path $checkpointDirectory 'checkpoint.json')
            '--expected-generation'
            '0'
            '--step-id'
            'phase-0-preflight'
            '--status'
            $checkpointStatus
            '--evidence-sha256'
            $reportHash
        )
        $checkpointCommand = Invoke-Workbook05RecordedCommand `
            -FilePath $PythonPathForSteps `
            -ArgumentList $checkpointArguments `
            -WorkingDirectory $RepositoryRootForSteps `
            -EvidenceDirectory (Join-Path $EvidenceRoot 'orchestrator-commands') `
            -CommandId 'source-admission-preflight-checkpoint' `
            -TimeoutSeconds 120
        if ([int]$checkpointCommand.Record.exit_code -ne 0) {
            throw 'The runtime preflight checkpoint update failed.'
        }

        $kind = if ($evaluation.OverallStatus -eq 'Passed') {
            'Success'
        }
        else {
            'IntegrityFailure'
        }
        $result = [ordered]@{
            StepId = $StepId
            Kind = $kind
            Status = [string]$evaluation.OverallStatus
            Reason = if ($evaluation.OverallStatus -eq 'Passed') {
                'The Intel runner preflight passed.'
            }
            else {
                'One or more required Intel runner preflight checks failed.'
            }
            EvidencePath = 'preflight/preflight-report.json'
        }
        $result | ConvertTo-Json -Depth 20 |
            Set-Content `
                -LiteralPath (Join-Path $stepsDirectory "$StepId.json") `
                -Encoding UTF8
        return [pscustomobject]$result
    }

    # Workspace validation creates only known campaign parents. Existing source,
    # build, or evidence data is preserved and evaluated by the later steps.
    if ($StepId -eq 'workspace-validation') {
        $workspaceDecision = Test-Workbook05ExternalWorkspace `
            -WorkspaceRoot $WorkspaceRootForSteps `
            -AllowedRoot $WorkspaceRootForSteps `
            -CreateIfMissing
        $knownDirectories = @(
            'source\route-a'
            'source\route-b'
            'build'
            'install\route-a'
            'install\route-b'
            'temporary\route-a'
            'temporary\route-b'
        )
        foreach ($relativeDirectory in $knownDirectories) {
            New-Item `
                -ItemType Directory `
                -Path (Join-Path $WorkspaceRootForSteps $relativeDirectory) `
                -Force | Out-Null
        }

        $workspaceEvidenceDirectory = Join-Path $EvidenceRoot 'workspace'
        $stepsDirectory = Join-Path $EvidenceRoot 'steps'
        New-Item -ItemType Directory -Path $workspaceEvidenceDirectory -Force | Out-Null
        New-Item -ItemType Directory -Path $stepsDirectory -Force | Out-Null
        [ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            phase_id = 'phase-1-source-admission'
            permitted = [bool]$workspaceDecision.Permitted
            canonical_path = [string]$workspaceDecision.CanonicalPath
            root_created = [bool]$workspaceDecision.Created
            known_directories = $knownDirectories
            existing_data_preserved = $true
        } | ConvertTo-Json -Depth 20 |
            Set-Content `
                -LiteralPath (Join-Path $workspaceEvidenceDirectory 'workspace-validation.json') `
                -Encoding UTF8

        $result = [ordered]@{
            StepId = $StepId
            Kind = 'Success'
            Status = 'Passed'
            Reason = 'The exact external workspace boundary was validated without deletion.'
            EvidencePath = 'workspace/workspace-validation.json'
        }
        $result | ConvertTo-Json -Depth 20 |
            Set-Content `
                -LiteralPath (Join-Path $stepsDirectory "$StepId.json") `
                -Encoding UTF8
        return [pscustomobject]$result
    }

    # Every remaining component is invoked as an argument list through the
    # pinned Python interpreter. The final hash step is direct because writing a
    # command record after it would make the completed hash manifest stale.
    $adapterArguments = @(
        '-m'
        'scripts.testing.workbook05.source_admission_orchestration'
        '--step'
        $StepId
        '--repository-root'
        $RepositoryRootForSteps
        '--output-directory'
        $EvidenceRoot
        '--settings'
        $SettingsPathForSteps
    )
    if ($StepId -eq 'hashes') {
        $adapterOutput = & $PythonPathForSteps @adapterArguments 2>&1
        $adapterExitCode = $LASTEXITCODE
        $adapterOutput | ForEach-Object { Write-Host $_ }
        if ($adapterExitCode -ne 0) {
            throw "The final evidence-hash step exited with code $adapterExitCode."
        }
    }
    else {
        [void](Invoke-Workbook05RecordedCommand `
            -FilePath $PythonPathForSteps `
            -ArgumentList $adapterArguments `
            -WorkingDirectory $RepositoryRootForSteps `
            -EvidenceDirectory (Join-Path $EvidenceRoot 'orchestrator-commands') `
            -CommandId "source-admission-$StepId" `
            -TimeoutSeconds $AdapterTimeoutSeconds)
    }

    $stepResultPath = Join-Path $EvidenceRoot "steps\$StepId.json"
    if (-not (Test-Path -LiteralPath $stepResultPath -PathType Leaf)) {
        throw "The adapter did not produce the required step result: $stepResultPath"
    }
    return Get-Content -LiteralPath $stepResultPath -Raw | ConvertFrom-Json
}.GetNewClosure()

# The pipeline returns zero for successful execution even when a scientific
# source blocker is recorded. Integrity or orchestration failures are non-zero.
try {
    $result = Invoke-Workbook05SourceAdmissionPipeline `
        -OutputDirectory $OutputDirectory `
        -StepOrder $StepOrder `
        -StepExecutor $StepExecutor `
        -RequiredBundleFiles $RequiredBundleFiles
    $result | ConvertTo-Json -Depth 30
    exit 0
}
catch {
    Write-Error $_
    exit 1
}
