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
        Resolve-Path `
            -LiteralPath (Join-Path $PSScriptRoot '..\..\..') `
            -ErrorAction Stop
    ).Path
}
else {
    $RepositoryRoot = (
        Resolve-Path -LiteralPath $RepositoryRoot -ErrorAction Stop
    ).Path
}

$Orchestrator = Join-Path `
    $RepositoryRoot `
    'scripts\testing\workbook05\Invoke-Workbook05Phase3AssetLock.ps1'
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
    'conversion-new-output-directory'
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
    if (
        $ScriptText.IndexOf(
            $Forbidden,
            [StringComparison]::OrdinalIgnoreCase
        ) -ge 0
    ) {
        throw (
            'The orchestrator contains a forbidden execution or cleanup ' +
            "token: $Forbidden"
        )
    }
}

$FixtureRoot = Join-Path `
    ([IO.Path]::GetTempPath()) `
    ("wb05-phase3-asset-lock-" + [guid]::NewGuid().ToString('N'))
$SuccessModelRoot = Join-Path $FixtureRoot 'models-success'
$FailureModelRoot = Join-Path $FixtureRoot 'models-failure'
$WorkspaceRoot = Join-Path $FixtureRoot 'evidence-success'
$FailureWorkspace = Join-Path $FixtureRoot 'evidence-failure'
$SuccessSentinel = Join-Path $SuccessModelRoot 'owner-sentinel.txt'
$FailureSentinel = Join-Path $FailureModelRoot 'owner-sentinel.txt'

try {
    foreach ($ModelRoot in @($SuccessModelRoot, $FailureModelRoot)) {
        New-Item -ItemType Directory -Path $ModelRoot -Force | Out-Null
    }
    [IO.File]::WriteAllText(
        $SuccessSentinel,
        'owner data',
        [Text.UTF8Encoding]::new($false)
    )
    [IO.File]::WriteAllText(
        $FailureSentinel,
        'owner data',
        [Text.UTF8Encoding]::new($false)
    )

    $Result = & $Orchestrator `
        -RepositoryRoot $RepositoryRoot `
        -PythonPath $PythonPath `
        -WorkspaceRoot $WorkspaceRoot `
        -ModelRoot $SuccessModelRoot `
        -OfflineFixtureMode
    if ($LASTEXITCODE -ne 0) {
        throw "Offline asset-lock fixture exited with code $LASTEXITCODE."
    }

    foreach ($Required in @(
        'prerequisite-proof.json'
        'disk-preflight.json'
        'resolved-model.json'
        'asset-lock.json'
        'conversion-record.json'
        'source-files.csv'
        'converted-files.csv'
        'commands\conversion.json'
        'logs\conversion.stdout.txt'
        'logs\conversion.stderr.txt'
        'summary.md'
        'stage-order.json'
        'manifest.sha256'
    )) {
        $RequiredPath = Join-Path $WorkspaceRoot $Required
        if (-not (Test-Path -LiteralPath $RequiredPath -PathType Leaf)) {
            throw "Successful offline orchestration is missing: $Required"
        }
    }

    $StageOrder = Get-Content `
        -LiteralPath (Join-Path $WorkspaceRoot 'stage-order.json') `
        -Raw |
        ConvertFrom-Json
    $ObservedStages = @($StageOrder.stages)
    if (($ObservedStages -join '|') -ne ($ExpectedStages -join '|')) {
        throw (
            'Observed stage order does not match the approved sequence: ' +
            ($ObservedStages -join ', ')
        )
    }

    $FinalResult = @($Result) | Select-Object -Last 1
    if ($FinalResult.status -ne 'Passed') {
        throw "Offline orchestration did not end as Passed: $($FinalResult.status)"
    }
    if ($FinalResult.model_execution_authorised -ne $false) {
        throw 'Offline orchestration unexpectedly authorised model execution.'
    }
    if (-not (Test-Path -LiteralPath $SuccessSentinel -PathType Leaf)) {
        throw 'Successful orchestration deleted unrelated owner data.'
    }
    if (
        @(Get-ChildItem `
            -LiteralPath $WorkspaceRoot `
            -Filter '*.tmp' `
            -Recurse `
            -File `
            -ErrorAction SilentlyContinue).Count -ne 0
    ) {
        throw 'Successful orchestration left temporary record files behind.'
    }

    $FailedAsExpected = $false
    try {
        & $Orchestrator `
            -RepositoryRoot $RepositoryRoot `
            -PythonPath $PythonPath `
            -WorkspaceRoot $FailureWorkspace `
            -ModelRoot $FailureModelRoot `
            -OfflineFixtureMode `
            -FailureStage 'source-file-hash-inventory' > $null
    }
    catch {
        $FailedAsExpected = $true
    }

    if (-not $FailedAsExpected) {
        throw 'The injected offline failure did not stop orchestration.'
    }
    if (
        -not (Test-Path `
            -LiteralPath (Join-Path $FailureWorkspace 'failure.json') `
            -PathType Leaf)
    ) {
        throw 'Failed orchestration did not retain failure.json.'
    }
    if (Test-Path -LiteralPath (Join-Path $FailureWorkspace 'manifest.sha256')) {
        throw 'Failed orchestration created an acceptance manifest.'
    }
    if (Test-Path -LiteralPath (Join-Path $FailureWorkspace 'asset-lock.json')) {
        throw 'Failed orchestration created a final asset lock.'
    }
    if (-not (Test-Path -LiteralPath $FailureSentinel -PathType Leaf)) {
        throw 'Failed orchestration deleted unrelated owner data.'
    }
    if (
        @(Get-ChildItem `
            -LiteralPath $FailureWorkspace `
            -Filter '*.tmp' `
            -Recurse `
            -File `
            -ErrorAction SilentlyContinue).Count -ne 0
    ) {
        throw 'Failed orchestration left temporary record files behind.'
    }

    $global:LASTEXITCODE = 0
    Write-Host 'Workbook 05 Phase 3 asset-lock PowerShell tests passed.'
}
finally {
    if (Test-Path -LiteralPath $FixtureRoot) {
        Remove-Item `
            -LiteralPath $FixtureRoot `
            -Recurse `
            -Force `
            -ErrorAction SilentlyContinue
    }
}
