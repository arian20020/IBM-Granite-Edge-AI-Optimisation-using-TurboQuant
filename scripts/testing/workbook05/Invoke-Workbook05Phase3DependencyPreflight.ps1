[CmdletBinding()]
param(
    # Repository-controlled schemas, modules, and requirement inputs.
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    # Fresh private workspace for this rehearsal. No generated content from this
    # directory is uploaded as evidence.
    [Parameter(Mandatory = $true)]
    [string] $WorkspaceRoot,

    # Fresh text-only evidence directory. The hash manifest is written last.
    [Parameter(Mandatory = $true)]
    [string] $EvidenceRoot,

    # Exact Python application selected by the workflow or focused test.
    [string] $PythonPath = 'python',

    # The only executable mode in this revision. It performs no network, package,
    # model, or external CLI operation and always records a Blocked decision.
    [switch] $OfflineFixtureMode,

    # Test-only deterministic interruption point.
    [string] $FailureStage = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Write-Host 'WB05_DEP_FIXTURE:script-entered'

# Keep one reviewed causal order for both the offline rehearsal and a future,
# separately reviewed live collector.
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

$ExpectedPythonVersion = 'Python 3.12.10'
$OptimumIntelCommit = 'a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0'
$OptimumCommit = '982e495540364f95da1e4b6f62d2d4e5907d08fd'

if (
    -not [string]::IsNullOrWhiteSpace($FailureStage) -and
    $FailureStage -notin $ApprovedStageOrder
) {
    throw "FailureStage is not approved: $FailureStage"
}

function Assert-NormalDirectory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Label
    )

    # Links, junctions, mount points, and other reparse points are not valid
    # evidence or workspace boundaries.
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
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][object] $Value
    )

    # Publish a record only after its complete UTF-8 representation exists in a
    # same-directory temporary file. The closed C1 schemas are shallower than
    # twelve levels; using an unboundedly large depth on Windows PowerShell 5.1
    # causes pathological object expansion before any bytes are written.
    $Parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $Parent -PathType Container)) {
        New-Item -ItemType Directory -Path $Parent -Force:$false | Out-Null
    }
    $TemporaryPath = "$Path.tmp"
    if (
        (Test-Path -LiteralPath $Path) -or
        (Test-Path -LiteralPath $TemporaryPath)
    ) {
        throw "Evidence destination already exists: $Path"
    }
    [IO.File]::WriteAllText(
        $TemporaryPath,
        (($Value | ConvertTo-Json -Depth 12) + [Environment]::NewLine),
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

    # Evidence text is create-once and encoded as UTF-8 without a BOM.
    $Parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $Parent -PathType Container)) {
        New-Item -ItemType Directory -Path $Parent -Force:$false | Out-Null
    }
    if (Test-Path -LiteralPath $Path) {
        throw "Evidence destination already exists: $Path"
    }
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}

# Resolve and validate repository and interpreter identity before creating an
# evidence attempt.
$RepositoryRoot = Assert-NormalDirectory `
    -Path $RepositoryRoot `
    -Label 'Repository root'
$PythonCommand = Get-Command $PythonPath -ErrorAction Stop
$PythonPath = $PythonCommand.Source
if ([string]::IsNullOrWhiteSpace($PythonPath)) {
    throw "Python application could not be resolved: $($PythonCommand.Name)"
}
$ObservedPythonVersion = ((& $PythonPath --version 2>&1) | Out-String).Trim()
if (
    $LASTEXITCODE -ne 0 -or
    $ObservedPythonVersion -ne $ExpectedPythonVersion
) {
    throw (
        "Expected $ExpectedPythonVersion, observed: $ObservedPythonVersion"
    )
}
Write-Host 'WB05_DEP_FIXTURE:python-verified'

# Reuse and automatic cleanup are forbidden. Each attempt gets two previously
# absent sibling directories under a normal parent.
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
    Assert-NormalDirectory -Path $NewPath -Label 'New attempt directory' |
        Out-Null
}
$WorkspaceRoot = Assert-NormalDirectory `
    -Path $WorkspaceRoot `
    -Label 'Dependency workspace'
$EvidenceRoot = Assert-NormalDirectory `
    -Path $EvidenceRoot `
    -Label 'Dependency evidence root'
Write-Host 'WB05_DEP_FIXTURE:directories-ready'

# The workspace remains deliberately empty in fixture mode. All generated test
# evidence is text, JSON, or logs beneath the evidence root.
$StepRoot = Join-Path $EvidenceRoot 'steps'
$LockRoot = Join-Path $EvidenceRoot 'locks'
$ReportRoot = Join-Path $EvidenceRoot 'reports'
$SourceEvidenceRoot = Join-Path $EvidenceRoot 'sources'
$LogRoot = Join-Path $EvidenceRoot 'logs'
foreach ($Path in @(
    $StepRoot,
    $LockRoot,
    $ReportRoot,
    $SourceEvidenceRoot,
    $LogRoot
)) {
    New-Item -ItemType Directory -Path $Path -Force:$false | Out-Null
}

$CompletedStages = [System.Collections.Generic.List[string]]::new()

function Start-ApprovedStage {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string] $Stage)

    # Reject any skipped, repeated, inserted, or reordered stage.
    $Index = $CompletedStages.Count
    if (
        $Index -ge $ApprovedStageOrder.Count -or
        $ApprovedStageOrder[$Index] -ne $Stage
    ) {
        throw "Dependency-preflight stage order violation at: $Stage"
    }
    Write-Host ("WB05_DEP_FIXTURE_STAGE:{0}" -f $Stage)
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

    # A live dependency operation must be introduced by a later reviewed change;
    # this revision never falls through to an implicit external operation.
    if (-not $OfflineFixtureMode) {
        throw (
            'Live dependency resolution is blocked in this revision. ' +
            'The repository-safe offline fixture must pass before a reviewed ' +
            'live Windows resolver and installer are enabled.'
        )
    }

    Start-ApprovedStage -Stage 'source-verification'

    # Record synthetic evidence using the exact reviewed immutable source
    # identities. No repository request is made.
    $OptimumIntelSource = [ordered]@{
        name = 'optimum-intel'
        repository = 'huggingface/optimum-intel'
        origin = 'https://github.com/huggingface/optimum-intel.git'
        commit = $OptimumIntelCommit
        clean = $true
        aggregate_sha256 = ('3' * 64)
        fixture_mode = $true
    }
    $OptimumSource = [ordered]@{
        name = 'optimum'
        repository = 'huggingface/optimum'
        origin = 'https://github.com/huggingface/optimum.git'
        commit = $OptimumCommit
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

    # Build a deterministic synthetic ordinary-distribution lock. The digests
    # exercise parser and relationship validation but are not live artifacts.
    $LockedRows = @(
        [pscustomobject]@{
            name = 'transformers'; version = '5.5.0'; hash = ('a' * 64)
        },
        [pscustomobject]@{
            name = 'huggingface-hub'; version = '1.21.0'; hash = ('b' * 64)
        },
        [pscustomobject]@{
            name = 'nncf'; version = '3.2.0'; hash = ('c' * 64)
        },
        [pscustomobject]@{
            name = 'openvino'; version = '2026.2.1'; hash = ('d' * 64)
        },
        [pscustomobject]@{
            name = 'openvino-tokenizers'; version = '2026.2.1.0'; hash = ('e' * 64)
        },
        [pscustomobject]@{
            name = 'requests'; version = '2.33.0'; hash = ('f' * 64)
        }
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

    # Model a pip-report-shaped relationship between the lock rows and exact
    # installed archive digests without contacting an index.
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
    Write-AtomicJson `
        -Path (Join-Path $ReportRoot 'normal-install-report.json') `
        -Value $InstallReport

    Start-ApprovedStage -Stage 'vcs-install'

    # Keep the two reviewed source-built distributions separate from the normal
    # archive lock, bound to full source commits.
    $VcsPackages = @(
        [ordered]@{
            name = 'optimum-intel'
            version = '2.3.0.dev0'
            commit = $OptimumIntelCommit
        },
        [ordered]@{
            name = 'optimum'
            version = '2.3.0'
            commit = $OptimumCommit
        }
    )
    Write-AtomicJson `
        -Path (Join-Path $ReportRoot 'vcs-packages.json') `
        -Value ([ordered]@{
            packages = $VcsPackages
            fixture_mode = $true
        })

    Start-ApprovedStage -Stage 'imports'

    # Retain explicit logs stating that no import process ran.
    Write-Utf8Text `
        -Path (Join-Path $LogRoot 'imports.stdout.txt') `
        -Text "Offline fixture: no module import was performed.`n"
    Write-Utf8Text `
        -Path (Join-Path $LogRoot 'imports.stderr.txt') `
        -Text ''

    Start-ApprovedStage -Stage 'cli-help'

    # Retain explicit logs stating that no external CLI process ran.
    Write-Utf8Text `
        -Path (Join-Path $LogRoot 'cli-help.stdout.txt') `
        -Text "Offline fixture: no CLI process was started.`n"
    Write-Utf8Text `
        -Path (Join-Path $LogRoot 'cli-help.stderr.txt') `
        -Text ''

    Start-ApprovedStage -Stage 'no-model-compatibility'

    # Retain explicit logs stating that only repository contracts were used.
    Write-Utf8Text `
        -Path (Join-Path $LogRoot 'no-model-compatibility.stdout.txt') `
        -Text "Offline fixture: repository command contracts only.`n"
    Write-Utf8Text `
        -Path (Join-Path $LogRoot 'no-model-compatibility.stderr.txt') `
        -Text ''

    # These statuses mean the fixture relationship was constructed correctly;
    # they do not claim that the named live operations executed.
    $Checks = @(
        [ordered]@{ name = 'resolver'; status = 'Passed'; exit_code = 0 },
        [ordered]@{ name = 'install'; status = 'Passed'; exit_code = 0 },
        [ordered]@{ name = 'imports'; status = 'Passed'; exit_code = 0 },
        [ordered]@{ name = 'cli_help'; status = 'Passed'; exit_code = 0 },
        [ordered]@{
            name = 'no_model_compatibility'; status = 'Passed'; exit_code = 0
        },
        [ordered]@{
            name = 'remote_code_disabled'; status = 'Passed'; exit_code = $null
        }
    )
    Write-AtomicJson `
        -Path (Join-Path $EvidenceRoot 'checks.json') `
        -Value ([ordered]@{
            checks = $Checks
            fixture_mode = $true
        })

    Start-ApprovedStage -Stage 'record-generation'

    # Read the reviewed direct requirement catalogue as data and build the exact
    # observation consumed by the Python decision constructor.
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

    # The controlled C:\w5c identity below is deliberately synthetic. It is not
    # the host temporary path and therefore cannot be confused with live proof.
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

    # Construct and schema-validate the final decision. The explicit fixture
    # switch forces a truthful Blocked result while returning process success.
    $DecisionPath = Join-Path $EvidenceRoot 'decision.json'
    $PreviousLocation = Get-Location
    try {
        Set-Location -LiteralPath $RepositoryRoot
        Write-Host 'WB05_DEP_FIXTURE:decision-cli:start'
        & $PythonPath `
            -m scripts.testing.workbook05.phase3.dependency_preflight_cli `
            --observation $ObservationPath `
            --repository-root $RepositoryRoot `
            --output $DecisionPath `
            --offline-fixture
        Write-Host 'WB05_DEP_FIXTURE:decision-cli:return'
        if ($LASTEXITCODE -ne 0) {
            throw "Dependency decision CLI exited with code $LASTEXITCODE."
        }
    }
    finally {
        Set-Location -LiteralPath $PreviousLocation
    }

    Start-ApprovedStage -Stage 'manifest-generation'

    # Write the remaining explanatory evidence before manifest.sha256, which is
    # the final file in a successful attempt.
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

    $ManifestPath = Join-Path $EvidenceRoot 'manifest.sha256'
    $PreviousLocation = Get-Location
    try {
        Set-Location -LiteralPath $RepositoryRoot
        Write-Host 'WB05_DEP_FIXTURE:manifest-cli:start'
        & $PythonPath `
            -m scripts.testing.workbook05.hash_manifest `
            --root $EvidenceRoot `
            --output $ManifestPath
        Write-Host 'WB05_DEP_FIXTURE:manifest-cli:return'
        if ($LASTEXITCODE -ne 0) {
            throw "Dependency manifest generation exited with code $LASTEXITCODE."
        }
    }
    finally {
        Set-Location -LiteralPath $PreviousLocation
    }

    # Return one explicit non-authorising result to the caller.
    $global:LASTEXITCODE = 0
    [pscustomobject]@{
        status = 'Blocked'
        reason = 'Offline fixture only; live Windows dependency evidence is pending.'
        evidence_root = $EvidenceRoot
        model_download_authorised = $false
        granite_model_test_authorised = $false
        activation_claim_authorised = $false
        packed_storage_claim_authorised = $false
        performance_claim_authorised = $false
        quality_claim_authorised = $false
    }
}
catch {
    # Capture the causal exception before pipeline variables can replace $_.
    $FailureMessage = $_.Exception.Message
    Write-Host ("WB05_DEP_FIXTURE:catch:{0}" -f $FailureMessage)

    # A failed attempt must not retain a manifest that resembles acceptance.
    $ManifestPath = Join-Path $EvidenceRoot 'manifest.sha256'
    if (Test-Path -LiteralPath $ManifestPath -PathType Leaf) {
        [IO.File]::Delete($ManifestPath)
    }

    # Remove only orphaned temporary files from this new evidence directory.
    Get-ChildItem `
        -LiteralPath $EvidenceRoot `
        -File `
        -Recurse `
        -Filter '*.tmp' `
        -ErrorAction SilentlyContinue |
        ForEach-Object { [IO.File]::Delete($_.FullName) }

    # Preserve one atomic failure record with every later authority disabled.
    $FailureClass = if ($OfflineFixtureMode) { 'Failed' } else { 'Blocked' }
    $FailurePath = Join-Path $EvidenceRoot 'failure.json'
    if (-not (Test-Path -LiteralPath $FailurePath)) {
        Write-AtomicJson -Path $FailurePath -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            route_id = 'route-a-merged-openvino'
            status = 'Failed'
            failure_class = $FailureClass
            completed_stages = @($CompletedStages)
            message = $FailureMessage
            model_download_authorised = $false
            granite_model_test_authorised = $false
            activation_claim_authorised = $false
            packed_storage_claim_authorised = $false
            performance_claim_authorised = $false
            quality_claim_authorised = $false
        })
    }

    $global:LASTEXITCODE = 1
    throw
}
