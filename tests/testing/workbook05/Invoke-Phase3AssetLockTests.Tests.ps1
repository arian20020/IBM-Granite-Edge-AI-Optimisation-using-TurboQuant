[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$RepositoryRoot = '',

    [Parameter(Mandatory = $false)]
    [string]$PythonPath = 'python'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (
        Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\..') -ErrorAction Stop
    ).Path
}
else {
    $RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot -ErrorAction Stop).Path
}

$Orchestrator = Join-Path $RepositoryRoot 'scripts\testing\workbook05\Invoke-Workbook05Phase3AssetLock.ps1'
if (-not (Test-Path -LiteralPath $Orchestrator -PathType Leaf)) {
    throw "Phase 3 asset-lock orchestrator is missing: $Orchestrator"
}

$ScriptText = Get-Content -LiteralPath $Orchestrator -Raw -ErrorAction Stop
$ExpectedStages = @(
    'prerequisite-verification'
    'path-root-verification'
    'disk-preflight'
    'immutable-revision-resolution'
    'source-snapshot-download'
    'source-file-hash-inventory'
    'conversion'
    'converted-file-hash-inventory'
    'schema-validation'
    'manifest-generation'
)
$PreviousIndex = -1
foreach ($Stage in $ExpectedStages) {
    $Index = $ScriptText.IndexOf("'$Stage'", [StringComparison]::Ordinal)
    if ($Index -lt 0) {
        throw "The orchestrator does not declare required stage: $Stage"
    }
    if ($Index -le $PreviousIndex) {
        throw "The orchestrator stage order is not deterministic at: $Stage"
    }
    $PreviousIndex = $Index
}

foreach ($Forbidden in @(
    'Invoke-Expression'
    'cmd /c'
    'cmd.exe /c'
    'git clean'
    'git reset --hard'
    'Remove-Item -LiteralPath $ModelRoot -Recurse'
)) {
    if ($ScriptText.IndexOf($Forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "The orchestrator contains a forbidden execution or cleanup token: $Forbidden"
    }
}

$FixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("wb05-phase3-asset-lock-" + [guid]::NewGuid().ToString('N'))
$ModelRoot = Join-Path $FixtureRoot 'models'
$WorkspaceRoot = Join-Path $FixtureRoot 'evidence-success'
$FailureWorkspace = Join-Path $FixtureRoot 'evidence-failure'
$PreflightPath = Join-Path $FixtureRoot 'accepted-preflight.json'
$Sentinel = Join-Path $ModelRoot 'owner-sentinel.txt'

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Value
    )
    New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force | Out-Null
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding UTF8
}

try {
    New-Item -ItemType Directory -Path $ModelRoot -Force | Out-Null
    [IO.File]::WriteAllText($Sentinel, 'owner data', [Text.UTF8Encoding]::new($false))

    $Preflight = [ordered]@{
        schema_version = '1.0'
        campaign_id = 'GTQ-WB05-MF-v1'
        record_type = 'conversion-dependency-preflight'
        route_id = 'route-a-merged-openvino'
        generated_at_utc = '2026-08-14T12:00:00Z'
        workspace = [ordered]@{
            root = 'C:\w5c\dependency-preflight-31800000000-1'
            normal_local_directory = $true
            fresh = $true
        }
        python = [ordered]@{
            version = '3.12.10'
            executable_path = 'C:\w5c\dependency-preflight-3180000000-1\venv\Scripts\python.exe'
            executable_sha256 = ('a' * 64)
        }
        pip = [ordered]@{
            version = '25.2'
            executable_path = 'C:\w5c\dependency-preflight-31800000000-1\venv\Scripts\pip.exe'
            executable_sha256 = ('b' * 64)
        }
        source_trees = @(
            [ordered]@{ name='optimum-intel'; repository='huggingface/optimum-intel'; origin='https://github.com/huggingface/optimum-intel.git'; commit='a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0'; clean=$true; aggregate_sha256=('c' * 64) }
            [ordered]@{ name='optimum'; repository='huggingface/optimum'; origin='https://github.com/huggingface/optimum.git'; commit='982e495540364f95da1e4b6f62d2d4e5907d08fd'; clean=$true; aggregate_sha256=('d' * 64) }
        )
        direct_requirements = @(
            'optimum-intel @ git+https://github.com/huggingface/optimum-intel.git@a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0'
            'optimum @ git+https://github.com/huggingface/optimum.git@982e495540364f95da1e4b6f62d2d4e5907d08fd'
            'transformers==5.5.0'
            'huggingface-hub==1.21.0'
            'nncf==3.2.0'
            'openvino==2026.2.1'
            'openvino-tokenizers==2026.2.1.0'
        )
        lock = [ordered]@{
            path = 'locks/requirements.phase3-assets.txt'
            sha256 = ('e' * 64)
            generator = 'fixture'
            normal_distribution_count = 7
            all_normal_artifacts_hashed = $true
            vcs_sources_bound_separately = $true
        }
        packages = @(
            [ordered]@{ name='optimum-intel'; version='2.3.0.dev0'; source_identity='a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0'; direct=$true }
            [ordered]@{ name='optimum'; version='2.3.0'; source_identity='982e495540364f95da1e4b6f62d2d4e5907d08fd'; direct=$true }
            [ordered]@{ name='transformers'; version='5.5.0'; source_identity=('sha256:' + ('1' * 64)); direct=$true }
            [ordered]@{ name='huggingface-hub'; version='1.21.0'; source_identity=('sha256:' + ('2' * 64)); direct=$true }
            [ordered]@{ name='nncf'; version='3.2.0'; source_identity=('sha256:' + ('3' * 64)); direct=$true }
            [ordered]@{ name='openvino'; version='2026.2.1'; source_identity=('sha256:' + ('4' * 64)); direct=$true }
            [ordered]@{ name='openvino-tokenizers'; version='2026.2.1.0'; source_identity=('sha256:' + ('5' * 64)); direct=$true }
        )
        checks = @(
            [ordered]@{ name='resolver'; status='Passed'; exit_code=0 }
            [ordered]@{ name='install'; status='Passed'; exit_code=0 }
            [ordered]@{ name='imports'; status='Passed'; exit_code=0 }
            [ordered]@{ name='cli_help'; status='Passed'; exit_code=0 }
            [ordered]@{ name='no_model_compatibility'; status='Passed'; exit_code=0 }
            [ordered]@{ name='remote_code_disabled'; status='Passed'; exit_code=$null }
        )
        import_modules = @('optimum','optimum.intel','transformers','nncf','openvino')
        cli_help_exit_code = 0
        no_model_compatibility_exit_code = 0
        status = 'Passed'
        reasons = @('Offline fixture preflight passed.')
        model_download_authorised = $false
        granite_model_test_authorised = $false
        activation_claim_authorised = $false
        packed_storage_claim_authorised = $false
        performance_claim_authorised = $false
        quality_claim_authorised = $false
    }
    Write-JsonFile -Path $PreflightPath -Value $Preflight
    $PreflightHash = (Get-FileHash -LiteralPath $PreflightPath -Algorithm SHA256).Hash.ToLowerInvariant()

    & $Orchestrator `
        -RepositoryRoot $RepositoryRoot `
        -PythonPath $PythonPath `
        -WorkspaceRoot $WorkspaceRoot `
        -ModelRoot $ModelRoot `
        -DependencyPreflightRecord $PreflightPath `
        -DependencyPreflightSha256 $PreflightHash `
        -OfflineFixtureMode
    if ($LASTEXITCODE -ne 0) {
        throw "Offline asset-lock fixture exited with code $LASTEXITCODE."
    }

    foreach ($Required in @(
        'prerequisite-proof.json'
        'dependency-preflight.json'
        'disk-preflight.json'
        'asset-lock.json'
        'conversion-record.json'
        'source-files.csv'
        'converted-files.csv'
        'commands\conversion.json'
        'logs\conversion.stdout.txt'
        'logs\conversion.stderr.txt'
        'summary.md'
        'orchestration-report.json'
        'manifest.sha256'
    )) {
        if (-not (Test-Path -LiteralPath (Join-Path $WorkspaceRoot $Required) -PathType Leaf)) {
            throw "Successful offline orchestration is missing: $Required"
        }
    }

    $Report = Get-Content -LiteralPath (Join-Path $WorkspaceRoot 'orchestration-report.json') -Raw | ConvertFrom-Json
    $ObservedStages = @($Report.stages | ForEach-Object { $_.stage_id })
    if (($ObservedStages -join '|') -ne ($ExpectedStages -join '|')) {
        throw "Observed stage order does not match the approved sequence: $($ObservedStages -join ', ')"
    }
    if ($Report.status -ne 'Candidate') {
        throw "Offline orchestration did not end as Candidate: $($Report.status)"
    }
    if (-not (Test-Path -LiteralPath $Sentinel -PathType Leaf)) {
        throw 'Successful orchestration deleted unrelated owner data.'
    }

    $FailedAsExpected = $false
    try {
        & $Orchestrator `
            -RepositoryRoot $RepositoryRoot `
            -PythonPath $PythonPath `
            -WorkspaceRoot $FailureWorkspace `
            -ModelRoot $ModelRoot `
            -DependencyPreflightRecord $PreflightPath `
            -DependencyPreflightSha256 $PreflightHash `
            -OfflineFixtureMode `
            -FailAfterStage 'source-file-hash-inventory'
    }
    catch {
        $FailedAsExpected = $true
    }
    if (-not $FailedAsExpected) {
        throw 'The injected offline failure did not stop orchestration.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $FailureWorkspace 'failure.json') -PathType Leaf)) {
        throw 'Failed orchestration did not retain failure.json.'
    }
    if (Test-Path -LiteralPath (Join-Path $FailureWorkspace 'manifest.sha256')) {
        throw 'Failed orchestration created an acceptance manifest.'
    }
    if (Test-Path -LiteralPath (Join-Path $FailureWorkspace 'asset-lock.json')) {
        throw 'Failed orchestration created a final asset lock.'
    }
    if (-not (Test-Path -LiteralPath $Sentinel -PathType Leaf)) {
        throw 'Failed orchestration deleted unrelated owner data.'
    }
    if (@(Get-ChildItem -LiteralPath $FailureWorkspace -Filter '*.tmp' -Recurse -File -ErrorAction SilentlyContinue).Count -ne 0) {
        throw 'Failed orchestration left temporary record files behind.'
    }

    $global:LASTEXITCODE = 0
    Write-Host 'Workbok 05 Phase 3 asset-lock PowerShell tests passed.'
}
finally {
    if (Test-Path -LiteralPath $FixtureRoot) {
        Remove-Item -LiteralPath $FixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
