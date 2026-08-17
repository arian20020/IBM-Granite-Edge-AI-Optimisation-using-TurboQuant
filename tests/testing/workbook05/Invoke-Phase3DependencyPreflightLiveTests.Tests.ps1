[CmdletBinding()]
param(
    [ValidateSet('all', 'workspace-source', 'bootstrap-lock', 'install-checks', 'finalisation')]
    [string]$FocusedArea = 'all'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$ScriptPath = Join-Path `
    $RepositoryRoot `
    'scripts\testing\workbook05\Invoke-Workbook05Phase3DependencyPreflightLive.ps1'
if (-not (Test-Path -LiteralPath $ScriptPath -PathType Leaf)) {
    throw "Dependency-preflight live script is missing: $ScriptPath"
}

$pythonCommand = Get-Command `
    -Name python `
    -CommandType Application `
    -All `
    -ErrorAction Stop |
    Select-Object -First 1
$PythonPath = $pythonCommand.Source

$TemporaryRoot = Join-Path `
    ([IO.Path]::GetTempPath()) `
    ('wb05-dependency-live-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $TemporaryRoot -Force:$false | Out-Null

function Assert-FalseClaims {
    param([Parameter(Mandatory = $true)][object]$Decision)

    foreach ($name in @(
        'model_download_authorised',
        'granite_model_test_authorised',
        'activation_claim_authorised',
        'packed_storage_claim_authorised',
        'performance_claim_authorised',
        'quality_claim_authorised'
    )) {
        if ($Decision.$name -ne $false) {
            throw "Dependency decision enabled a forbidden claim: $name"
        }
    }
}

try {
    if ($FocusedArea -in @('all', 'workspace-source', 'bootstrap-lock', 'install-checks', 'finalisation')) {
        & $ScriptPath `
            -RepositoryRoot $RepositoryRoot `
            -RunId 'fixture-success' `
            -RunAttempt 1 `
            -BasePythonPath $PythonPath `
            -SimulationMode `
            -SimulationRoot $TemporaryRoot
        if ($LASTEXITCODE -ne 0) {
            throw "Successful dependency fixture exited with code $LASTEXITCODE."
        }

        $AttemptRoot = Join-Path `
            $TemporaryRoot `
            'dependency-preflight-fixture-success-1'
        $EvidenceRoot = Join-Path $AttemptRoot 'evidence'
        foreach ($relative in @(
            'decision.json',
            'observation.json',
            'checks.json',
            'stage-order.json',
            'command-index.json',
            'summary.md',
            'manifest.sha256'
        )) {
            $path = Join-Path $EvidenceRoot $relative
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                throw "Successful fixture omitted evidence: $relative"
            }
        }

        $Decision = Get-Content `
            -LiteralPath (Join-Path $EvidenceRoot 'decision.json') `
            -Raw `
            -Encoding UTF8 |
            ConvertFrom-Json
        if ($Decision.status -ne 'Passed') {
            throw "Successful dependency fixture did not produce Passed: $($Decision.status)"
        }
        Assert-FalseClaims -Decision $Decision

        $ExpectedStages = @(
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
        $StageOrder = Get-Content `
            -LiteralPath (Join-Path $EvidenceRoot 'stage-order.json') `
            -Raw `
            -Encoding UTF8 |
            ConvertFrom-Json
        if (($StageOrder.stage_order -join '|') -ne ($ExpectedStages -join '|')) {
            throw 'Successful dependency fixture changed the approved stage order.'
        }
        if (($StageOrder.completed_stages -join '|') -ne ($ExpectedStages -join '|')) {
            throw 'Successful dependency fixture did not complete every approved stage.'
        }

        $TemporaryFiles = @(
            Get-ChildItem `
                -LiteralPath $AttemptRoot `
                -File `
                -Recurse `
                -Force |
                Where-Object { $_.Name.EndsWith('.tmp') }
        )
        if ($TemporaryFiles.Count -ne 0) {
            throw "Successful fixture left temporary evidence: $($TemporaryFiles.FullName -join ', ')"
        }

        $ManifestPath = Join-Path $EvidenceRoot 'manifest.sha256'
        $OtherFinalFiles = @(
            Get-ChildItem `
                -LiteralPath $EvidenceRoot `
                -File `
                -Recurse `
                -Force |
                Where-Object { $_.FullName -ne $ManifestPath }
        )
        $LatestOther = ($OtherFinalFiles | Measure-Object LastWriteTimeUtc -Maximum).Maximum
        if ((Get-Item -LiteralPath $ManifestPath).LastWriteTimeUtc -lt $LatestOther) {
            throw 'manifest.sha256 was not written after every other final evidence file.'
        }

        $ReuseRejected = $false
        try {
            & $ScriptPath `
                -RepositoryRoot $RepositoryRoot `
                -RunId 'fixture-success' `
                -RunAttempt 1 `
                -BasePythonPath $PythonPath `
                -SimulationMode `
                -SimulationRoot $TemporaryRoot
        }
        catch {
            $ReuseRejected = $_.Exception.Message -match 'already exists'
        }
        if (-not $ReuseRejected) {
            throw 'An existing dependency-preflight workspace was not rejected.'
        }
    }

    if ($FocusedArea -in @('all', 'finalisation')) {
        foreach ($classification in @(
            'Blocked',
            'IntegrityFailure',
            'InfrastructureInterrupted'
        )) {
            $runId = 'fixture-failure-' + $classification.ToLowerInvariant()
            $FailedAsExpected = $false
            try {
                & $ScriptPath `
                    -RepositoryRoot $RepositoryRoot `
                    -RunId $runId `
                    -RunAttempt 1 `
                    -BasePythonPath $PythonPath `
                    -SimulationMode `
                    -SimulationRoot $TemporaryRoot `
                    -FailureStage 'normal-install' `
                    -SimulationFailureClassification $classification
            }
            catch {
                $FailedAsExpected = $true
            }
            if (-not $FailedAsExpected) {
                throw "Injected $classification fixture did not fail."
            }

            $EvidenceRoot = Join-Path `
                (Join-Path $TemporaryRoot "dependency-preflight-$runId-1") `
                'evidence'
            $FailurePath = Join-Path $EvidenceRoot 'failure.json'
            if (-not (Test-Path -LiteralPath $FailurePath -PathType Leaf)) {
                throw "Injected $classification fixture omitted failure.json."
            }
            if (Test-Path -LiteralPath (Join-Path $EvidenceRoot 'manifest.sha256')) {
                throw "Injected $classification fixture incorrectly wrote a manifest."
            }
            $Failure = Get-Content `
                -LiteralPath $FailurePath `
                -Raw `
                -Encoding UTF8 |
                ConvertFrom-Json
            if ($Failure.classification -ne $classification) {
                throw "Failure classification drifted: $($Failure.classification)"
            }
            if ($Failure.current_stage -ne 'normal-install') {
                throw "Failure did not retain its first causal stage."
            }
            if ([string]::IsNullOrWhiteSpace($Failure.first_causal_message)) {
                throw "Failure did not retain its first causal message."
            }
            Assert-FalseClaims -Decision $Failure
        }
    }

    Write-Host 'Workbook 05 Phase 3 dependency-preflight live simulations passed.'
}
finally {
    if (Test-Path -LiteralPath $TemporaryRoot) {
        Remove-Item -LiteralPath $TemporaryRoot -Recurse -Force
    }
}
