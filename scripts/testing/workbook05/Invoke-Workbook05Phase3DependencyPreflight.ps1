[CmdletBinding()]
param(
    # Repository-controlled schemas, modules, and requirement inputs.
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    # Fresh work area for source trees and virtual environments. This path is
    # never uploaded as an artifact.
    [Parameter(Mandatory = $true)]
    [string] $WorkspaceRoot,

    # Text/JSON/CSV/log evidence only. The hash manifest is written last.
    [Parameter(Mandatory = $true)]
    [string] $EvidenceRoot,

    # Exact Python 3.12.10 application selected by the workflow or test.
    [string] $PythonPath = 'python',

    # Repository tests use this mode. It makes no network or package request and
    # its final decision is always Blocked rather than accepted.
    [switch] $OfflineFixtureMode,

    # Test-only deterministic fault injection.
    [string] $FailureStage = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The live implementation must retain this exact causal order. The current
# revision deliberately blocks live execution after repository verification;
# the accepted Windows lock will be enabled only by a later reviewed change.
$ApprovedStageOrder = @(
    'workspace-validation',
    'source-verification',
    'lock-generation',
    'normal-install',
    'vcs-install',
    'imports',
    'cli-help',
    'no-model-compatibility',
    'record-generation',
    'manifest-generation'
)

if (
    -not [string]::IsNullOrWhiteSpace($FailureStage) -and
    $FailureStage -notin $ApprovedStageOrder
) {
    throw "FailureStage is not approved: $FailureStage"
}

function Assert-NormalDirectory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Label
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

function Write-AtomicJson {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [object] $Value
    )

    $Parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $Parent -PathType Container)) {
        New-Item -ItemType Directory -Path $Parent -Force:$false | Out-Null
    }
    $Temporary = "$Path.tmp"
    if (Test-Path -LiteralPath $Path -or Test-Path -LiteralPath $Temporary) {
        throw "Evidence destination already exists: $Path"
    }
    [IO.File]::WriteAllText(
        $Temporary,
        (($Value | ConvertTo-Json -Depth 80) + [Environment]::NewLine),
        [Text.UTF8Encoding]::new($false)
    )
    [IO.File]::Move($Temporary, $Path)
}

function Write-Utf8Text {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string] $Text
    )

    $Parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $Parent -PathType Container)) {
        New-Item -ItemType Directory -Path $Parent -Force:$false | Out-Null
    }
    if (Test-Path -LiteralPath $Path) {
        throw "Evidence destination already exists: $Path"
    }
    [IO.File]::WriteAllText(
        $Path,
        $Text,
        [Text.UTF8Encoding]::new($false)
    )
}

$RepositoryRoot = Assert-NormalDirectory `
    -Path $RepositoryRoot `
    -Label 'Repository root'

if (Test-Path -LiteralPath $WorkspaceRoot) {
    throw "Dependency workspace already exists and will not be reused: $WorkspaceRoot"
}
if (Test-Path -LiteralPath $EvidenceRoot) {
    throw "Dependency evidence root already exists and will not be reused: $EvidenceRoot"
}

foreach ($NewPath in @($WorkspaceRoot, $EvidenceRoot)) {
    $Parent = Split-Path -Parent $NewPath
    if (-not (Test-Path -LiteralPath $Parent -PathType Container)) {
        New-Item -ItemType Directory -Path $Parent -Force:$false | Out-Null
    }
    Assert-NormalDirectory -Path $Parent -Label 'Attempt parent' | Out-Null
    New-Item -ItemType Directory -Path $NewPath -Force:$false | Out-Null
    Assert-NormalDirectory -Path $NewPath -Label 'New attempt directory' | Out-Null
}

$StepRoot = Join-Path $EvidenceRoot 'steps'
$LockRoot = Join-Path $EvidenceRoot 'locks'
$ReportRoot = Join-Path $EvidenceRoot 'reports'
$SourceEvidenceRoot = Join-Path $EvidenceRoot 'sources'
$LogRoot = Join-Path $EvidenceRoot 'logs'
foreach ($Path in @($StepRoot, $LockRoot, $ReportRoot, $SourceEvidenceRoot, $LogRoot)) {
    New-Item -ItemType Directory -Path $Path -Force:$false | Out-Null
}

$CompletedStages = [System.Collections.Generic.List[string]]::new()

function Start-ApprovedStage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Stage
    )

    $Index = $CompletedStages.Count
    if ($Index -ge $ApprovedStageOrder.Count -or $ApprovedStageOrder[$Index] -ne $Stage) {
        throw "Dependency-preflight stage order violation at: $Stage"
    }
    $CompletedStages.Add($Stage)
    Write-AtomicJson `
        -Path (Join-Path $StepRoot ('{0:D2}-{1}.json' -f ($Index + 1), $Stage)) `
        -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            route_id = 'route-a-merged-openvino'
            stage = $Stage
            status = 'Started'
        })
    if ($FailureStage -eq $Stage) {
        throw "Injected fixture failure at stage: $Stage"
    }
}

try {
    Start-ApprovedStage -Stage 'workspace-validation'

    if (-not $OfflineFixtureMode) {
        throw (
            'Live dependency resolution is blocked in this revision. The ' +
            'repository-safe offline fixture must pass before a reviewed live ' +
            'Windows resolver and installer are enabled.'
        )
    }

    Start-ApprovedStage -Stage 'source-verification'
    $OptimumIntelSource = [ordered]@{
        name = 'optimum-intel'
        repository = 'huggingface/optimum-intel'
        origin = 'https://github.com/huggingface/optimum-intel.git'
        commit = 'a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0'
        clean = $true
        aggregate_sha256 = ('3' * 64)
        fixture_mode = $true
    }
    $OptimumSource = [ordered]@{
        name = 'optimum'
        repository = 'huggingface/optimum'
        origin = 'https://github.com/huggingface/optimum.git'
        commit = '982e495540364f95da1e4b6f62d2d4e5907d08fd'
        clean = $true
        aggregate_sha256 = ('4' * 64)
        fixture_mode = $true
    }
    Write-AtomicJson `
        -Path (Join-Path $SourceEvidenceRoot 'optimum-intel.json') `
        -Value $OptimumIntelSource
    Write-AtomicJson `
        -Path (Join-Path $SourceEvidenceRoot 'optimum.json') `
        -Value $OptimumSource

    Start-ApprovedStage -Stage 'lock-generation'
    $LockedRows = @(
        [pscustomobject]@{ name = 'transformers'; version = '5.5.0'; hash = ('a' * 64) },
        [pscustomobject]@{ name = 'huggingface-hub'; version = '1.21.0'; hash = ('b' * 64) },
        [pscustomobject]@{ name = 'nncf'; version = '3.2.0'; hash = ('c' * 64) },
        [pscustomobject]@{ name = 'openvino'; version = '2026.2.1'; hash = ('d' * 64) },
        [pscustomobject]@{ name = 'openvino-tokenizers'; version = '2026.2.1.0'; hash = ('e' * 64) },
        [pscustomobject]@{ name = 'requests'; version = '2.33.0'; hash = ('f' * 64) }
    )
    $LockText = (
        $LockedRows |
        ForEach-Object {
            "$($_.name)==$($_.version) --hash=sha256:$($_.hash)"
        }
    ) -join "`n"
    $LockText += "`n"
    $LockPath = Join-Path $LockRoot 'requirements.phase3-assets.txt'
    Write-Utf8Text -Path $LockPath -Text $LockText
    $LockHash = (
        Get-FileHash -LiteralPath $LockPath -Algorithm SHA256
    ).Hash.ToLowerInvariant()

    Start-ApprovedStage -Stage 'normal-install'
    $InstallRows = @(
        foreach ($Row in $LockedRows) {
            [ordered]@{
                download_info = [ordered]@{
                    url = "https://files.pythonhosted.org/$($Row.name).whl"
                    archive_info = [ordered]@{
                        hashes = [ordered]@{ sha256 = $Row.hash }
                    }
                }
                is_direct = $Row.name -ne 'requests'
                requested = $Row.name -ne 'requests'
                metadata = [ordered]@{
                    name = $Row.name
                    version = $Row.version
                }
            }
        }
    )
    $InstallReport = [ordered]@{
        version = '1'
        pip_version = '25.2'
        install = $InstallRows
        fixture_mode = $true
    }
    $InstallReportPath = Join-Path $ReportRoot 'normal-install-report.json'
    Write-AtomicJson -Path $InstallReportPath -Value $InstallReport

    Start-ApprovedStage -Stage 'vcs-install'
    $VcsPackages = @(
        [ordered]@{
            name = 'optimum-intel'
            version = '2.3.0.dev0'
            commit = 'a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0'
        },
        [ordered]@{
            name = 'optimum'
            version = '2.3.0'
            commit = '982e495540364f95da1e4b6f62d2d4e5907d08fd'
        }
    )
    Write-AtomicJson `
        -Path (Join-Path $ReportRoot 'vcs-packages.json') `
        -Value ([ordered]@{ packages = $VcsPackages; fixture_mode = $true })

    Start-ApprovedStage -Stage 'imports'
    Write-Utf8Text `
        -Path (Join-Path $LogRoot 'imports.stdout.txt') `
        -Text "Offline fixture: no module import was performed.`n"
    Write-Utf8Text `
        -Path (Join-Path $LogRoot 'imports.stderr.txt') `
        -Text ''

    Start-ApprovedStage -Stage 'cli-help'
    Write-Utf8Text `
        -Path (Join-Path $LogRoot 'cli-help.stdout.txt') `
        -Text "Offline fixture: no CLI process was started.`n"
    Write-Utf8Text `
        -Path (Join-Path $LogRoot 'cli-help.stderr.txt') `
        -Text ''

    Start-ApprovedStage -Stage 'no-model-compatibility'
    Write-Utf8Text `
        -Path (Join-Path $LogRoot 'no-model-compatibility.stdout.txt') `
        -Text "Offline fixture: repository command contracts only.`n"
    Write-Utf8Text `
        -Path (Join-Path $LogRoot 'no-model-compatibility.stderr.txt') `
        -Text ''

    $Checks = @(
        [ordered]@{ name = 'resolver'; status = 'Passed'; exit_code = 0 },
        [ordered]@{ name = 'install'; status = 'Passed'; exit_code = 0 },
        [ordered]@{ name = 'imports'; status = 'Passed'; exit_code = 0 },
        [ordered]@{ name = 'cli_help'; status = 'Passed'; exit_code = 0 },
        [ordered]@{ name = 'no_model_compatibility'; status = 'Passed'; exit_code = 0 },
        [ordered]@{ name = 'remote_code_disabled'; status = 'Passed'; exit_code = $null }
    )
    Write-AtomicJson `
        -Path (Join-Path $EvidenceRoot 'checks.json') `
        -Value ([ordered]@{ checks = $Checks; fixture_mode = $true })

    Start-ApprovedStage -Stage 'record-generation'
    $DirectRequirementsPath = Join-Path `
        $RepositoryRoot `
        'scripts\testing\workbook05\requirements.phase3-assets.in'
    $DirectRequirements = @(
        Get-Content -LiteralPath $DirectRequirementsPath -Encoding UTF8 |
        Where-Object {
            -not [string]::IsNullOrWhiteSpace($_) -and
            -not $_.TrimStart().StartsWith('#')
        }
    )

    # The schema intentionally requires a controlled C:\w5c identity. This is
    # a synthetic fixture identity and the CLI forcibly classifies it Blocked.
    $FixtureIdentity = 'C:\w5c\dependency-preflight-offline-fixture-00000000000-1'
    $Observation = [ordered]@{
        generated_at_utc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        workspace_root = $FixtureIdentity
        workspace_is_normal_local_directory = $true
        workspace_is_fresh = $true
        python_version = '3.12.10'
        python_executable_path = "$FixtureIdentity\venv\Scripts\python.exe"
        python_executable_sha256 = ('1' * 64)
        pip_version = '25.2'
        pip_executable_path = "$FixtureIdentity\venv\Scripts\pip.exe"
        pip_executable_sha256 = ('2' * 64)
        source_trees = @($OptimumIntelSource, $OptimumSource) |
            ForEach-Object {
                [ordered]@{
                    name = $_.name
                    repository = $_.repository
                    origin = $_.origin
                    commit = $_.commit
                    clean = $_.clean
                    aggregate_sha256 = $_.aggregate_sha256
                }
            }
        direct_requirements = $DirectRequirements
        lock_path = 'locks/requirements.phase3-assets.txt'
        lock_text = $LockText
        lock_sha256 = $LockHash
        lock_generator = 'pip-tools==7.5.0'
        normal_install_report = $InstallReport
        vcs_packages = $VcsPackages
        checks = $Checks
        import_modules = @(
            'optimum',
            'optimum.intel',
            'transformers',
            'nncf',
            'openvino'
        )
        cli_help_exit_code = 0
        no_model_compatibility_exit_code = 0
    }
    $ObservationPath = Join-Path $EvidenceRoot 'observation.json'
    Write-AtomicJson -Path $ObservationPath -Value $Observation

    $PreviousLocation = Get-Location
    try {
        Set-Location -LiteralPath $RepositoryRoot
        & $PythonPath `
            -m scripts.testing.workbook05.phase3.dependency_preflight_cli `
            --observation $ObservationPath `
            --repository-root $RepositoryRoot `
            --output (Join-Path $EvidenceRoot 'decision.json') `
            --offline-fixture
        if ($LASTEXITCODE -ne 0) {
            throw "Dependency decision CLI exited with code $LASTEXITCODE."
        }
    }
    finally {
        Set-Location -LiteralPath $PreviousLocation
    }

    Start-ApprovedStage -Stage 'manifest-generation'
    Write-AtomicJson `
        -Path (Join-Path $EvidenceRoot 'stage-order.json') `
        -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            stages = @($CompletedStages)
            fixture_mode = $true
        })
    Write-Utf8Text `
        -Path (Join-Path $EvidenceRoot 'summary.md') `
        -Text (
            "# C1 dependency-preflight offline fixture`n`n" +
            "Status: Blocked. No source clone, package resolution, package " +
            "installation, module import, CLI process, or model operation ran.`n"
        )

    $PreviousLocation = Get-Location
    try {
        Set-Location -LiteralPath $RepositoryRoot
        & $PythonPath `
            -m scripts.testing.workbook05.hash_manifest `
            --root $EvidenceRoot `
            --output (Join-Path $EvidenceRoot 'manifest.sha256')
        if ($LASTEXITCODE -ne 0) {
            throw "Dependency manifest generation exited with code $LASTEXITCODE."
        }
    }
    finally {
        Set-Location -LiteralPath $PreviousLocation
    }

    [pscustomobject]@{
        status = 'Blocked'
        reason = 'Offline fixture only; live Windows dependency evidence is pending.'
        evidence_root = $EvidenceRoot
        model_download_authorised = $false
        performance_claim_authorised = $false
        quality_claim_authorised = $false
    }
}
catch {
    Get-ChildItem `
        -LiteralPath $EvidenceRoot `
        -File `
        -Recurse `
        -Filter '*.tmp' `
        -ErrorAction SilentlyContinue |
        ForEach-Object { [IO.File]::Delete($_.FullName) }

    $FailurePath = Join-Path $EvidenceRoot 'failure.json'
    if (-not (Test-Path -LiteralPath $FailurePath)) {
        Write-AtomicJson -Path $FailurePath -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            route_id = 'route-a-merged-openvino'
            status = 'Failed'
            failure_class = if ($OfflineFixtureMode) { 'Failed' } else { 'Blocked' }
            completed_stages = @($CompletedStages)
            message = $_.Exception.Message
            model_download_authorised = $false
            granite_model_test_authorised = $false
            activation_claim_authorised = $false
            packed_storage_claim_authorised = $false
            performance_claim_authorised = $false
            quality_claim_authorised = $false
        })
    }
    throw
}
