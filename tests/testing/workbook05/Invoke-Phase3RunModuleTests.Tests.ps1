[CmdletBinding()]
param(
    [string] $RepositoryRoot = (
        Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
    ).Path,
    [string] $PythonPath = 'python'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ModulePath = Join-Path $RepositoryRoot 'scripts\testing\workbook05\Workbook05.Run.psm1'
if (-not (Test-Path -LiteralPath $ModulePath -PathType Leaf)) {
    throw "Missing Phase 3 run module: $ModulePath"
}

Import-Module -Name $ModulePath -Force -ErrorAction Stop

$ExpectedFunctions = @(
    'New-Wb05RunWorkspace',
    'Invoke-Wb05SupervisedProcess',
    'Get-Wb05ProcessTreeIds',
    'Stop-Wb05ProcessTree',
    'Write-Wb05AtomicJson',
    'Start-Wb05RunSampler',
    'Stop-Wb05RunSampler',
    'Test-Wb05RunWatchdog',
    'Test-Wb05CooldownHealth',
    'Invoke-Wb05AttemptSequence'
)

foreach ($Name in $ExpectedFunctions) {
    if (-not (Get-Command -Name $Name -CommandType Function -ErrorAction SilentlyContinue)) {
        throw "Missing Phase 3 run function: $Name"
    }
}

$TemporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("wb05-c2-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $TemporaryRoot -Force:$false | Out-Null
$env:WB05_PHASE3_TEST_MODE = '1'

try {
    # A new child workspace must be created once and must never be silently reused.
    $Workspace = New-Wb05RunWorkspace `
        -Root $TemporaryRoot `
        -RunIdentity 'fixture-001-1' `
        -AllowTestRoot

    foreach ($Name in @('logs', 'events', 'outputs', 'metrics', 'proof', 'requests', 'records')) {
        $Path = $Workspace.$Name
        if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
            throw "Missing workspace subdirectory: $Name"
        }
    }

    $ReuseRejected = $false
    try {
        New-Wb05RunWorkspace `
            -Root $TemporaryRoot `
            -RunIdentity 'fixture-001-1' `
            -AllowTestRoot | Out-Null
    }
    catch {
        $ReuseRejected = $true
    }
    if (-not $ReuseRejected) {
        throw 'Existing C2 run workspace was silently reused.'
    }

    # Atomic JSON writes must be BOM-free and leave no sibling temporary file.
    $AtomicPath = Join-Path $Workspace.records 'atomic.json'
    Write-Wb05AtomicJson -Path $AtomicPath -Value ([ordered]@{ status = 'Passed' })
    $Bytes = [IO.File]::ReadAllBytes($AtomicPath)
    if ($Bytes.Length -ge 3 -and $Bytes[0] -eq 0xEF -and $Bytes[1] -eq 0xBB -and $Bytes[2] -eq 0xBF) {
        throw 'Atomic JSON contains a UTF-8 BOM.'
    }
    if (Test-Path -LiteralPath ($AtomicPath + '.tmp')) {
        throw 'Atomic JSON left a temporary file behind.'
    }

    # Five consecutive low-memory observations stop; four followed by health reset.
    $LowSamples = 1..5 | ForEach-Object {
        [pscustomobject]@{
            available_memory_bytes = 1610612735
            commit_percent = 50.0
            heartbeat_age_seconds = 1
        }
    }
    $LowDecision = Test-Wb05RunWatchdog -Samples $LowSamples
    if ($LowDecision.reason -ne 'LOW_AVAILABLE_MEMORY') {
        throw 'Expected the reviewed low-memory safety stop.'
    }

    $ResetSamples = @(
        (1..4 | ForEach-Object {
            [pscustomobject]@{
                available_memory_bytes = 1
                commit_percent = 50.0
                heartbeat_age_seconds = 1
            }
        })
        [pscustomobject]@{
            available_memory_bytes = 4294967296
            commit_percent = 50.0
            heartbeat_age_seconds = 1
        }
        (1..4 | ForEach-Object {
            [pscustomobject]@{
                available_memory_bytes = 1
                commit_percent = 50.0
                heartbeat_age_seconds = 1
            }
        })
    )
    if ((Test-Wb05RunWatchdog -Samples $ResetSamples).triggered) {
        throw 'A healthy observation did not reset the safety streak.'
    }

    $CommitSamples = 1..5 | ForEach-Object {
        [pscustomobject]@{
            available_memory_bytes = 4294967296
            commit_percent = 90.1
            heartbeat_age_seconds = 1
        }
    }
    if ((Test-Wb05RunWatchdog -Samples $CommitSamples).reason -ne 'HIGH_COMMIT_PERCENT') {
        throw 'Expected the reviewed high-commit safety stop.'
    }

    $HeartbeatSamples = 1..5 | ForEach-Object {
        [pscustomobject]@{
            available_memory_bytes = 4294967296
            commit_percent = 50.0
            heartbeat_age_seconds = 901
        }
    }
    if ((Test-Wb05RunWatchdog -Samples $HeartbeatSamples).reason -ne 'HEARTBEAT_STALE') {
        throw 'Expected the reviewed heartbeat safety stop.'
    }

    # Cooldown must require elapsed time, healthy resources, and no descendants.
    $Cooldown = Test-Wb05CooldownHealth `
        -RemainingProcessIds @() `
        -AvailableMemoryBytes 4294967296 `
        -CommitPercent 50 `
        -ElapsedSeconds 30 `
        -RequiredCooldownSeconds 30
    if ($Cooldown.status -ne 'Passed') {
        throw 'Expected the injected cooldown observation to pass.'
    }

    $BlockedCooldown = Test-Wb05CooldownHealth `
        -RemainingProcessIds @(1234) `
        -AvailableMemoryBytes 4294967296 `
        -CommitPercent 50 `
        -ElapsedSeconds 30 `
        -RequiredCooldownSeconds 30
    if ($BlockedCooldown.status -ne 'Failed') {
        throw 'Cooldown accepted a remaining descendant process.'
    }

    # Exercise the real System.Diagnostics.Process boundary with difficult arguments.
    $FixturePath = Join-Path $RepositoryRoot 'tests\testing\workbook05\fixtures\phase3\process\normal_child.py'
    $Result = Invoke-Wb05SupervisedProcess `
        -ExecutablePath $PythonPath `
        -Arguments @(
            $FixturePath,
            '--workspace',
            $Workspace.root,
            '--echo-arg',
            'value with spaces',
            '--echo-arg',
            'quote"inside',
            '--echo-arg',
            '',
            '--echo-arg',
            'trailing\'
        ) `
        -WorkingDirectory $RepositoryRoot `
        -Workspace $Workspace `
        -AttemptId 'fixture-001-1' `
        -StageTimeoutSeconds 30 `
        -SampleIntervalMilliseconds 100

    if ($Result.exit_code -ne 0 -or $Result.classification -ne 'Passed') {
        throw "Normal supervised fixture failed: $($Result | ConvertTo-Json -Depth 10)"
    }
    if ((Get-Content -LiteralPath $Result.stdout_path -Raw) -notmatch 'normal-child-stdout') {
        throw 'Supervised stdout was not retained separately.'
    }
    if ((Get-Content -LiteralPath $Result.stderr_path -Raw).Length -ne 0) {
        throw 'Normal fixture unexpectedly populated stderr.'
    }

    # Descendant discovery and termination must include the child and stop it first.
    $ParentFixture = Join-Path $RepositoryRoot 'tests\testing\workbook05\fixtures\phase3\process\descendant_parent.py'
    $PidFile = Join-Path $TemporaryRoot 'descendant-pids.json'
    $StartInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $StartInfo.FileName = $PythonPath
    $StartInfo.Arguments = ('"{0}" --workspace "{1}" --pid-file "{2}"' -f $ParentFixture, $TemporaryRoot, $PidFile)
    $StartInfo.UseShellExecute = $false
    $StartInfo.CreateNoWindow = $true
    $Parent = [System.Diagnostics.Process]::Start($StartInfo)
    try {
        $Deadline = [DateTime]::UtcNow.AddSeconds(10)
        while (-not (Test-Path -LiteralPath $PidFile -PathType Leaf)) {
            if ([DateTime]::UtcNow -ge $Deadline) {
                throw 'Descendant fixture did not publish PID evidence.'
            }
            Start-Sleep -Milliseconds 100
        }

        $Tree = @(Get-Wb05ProcessTreeIds -RootProcessId $Parent.Id)
        if ($Tree.Count -lt 2) {
            throw 'Full descendant process tree was not discovered.'
        }
        $TerminationPath = Join-Path $TemporaryRoot 'termination.json'
        $Proof = @(Stop-Wb05ProcessTree -RootProcessId $Parent.Id -ProofPath $TerminationPath)
        if ($Proof.Count -lt 2) {
            throw 'Full descendant process tree was not terminated.'
        }
        $Depths = @($Proof | ForEach-Object { [int] $_.depth })
        if ($Depths[0] -lt $Depths[$Depths.Count - 1]) {
            throw 'Processes were not terminated descendant-first.'
        }
    }
    finally {
        if (-not $Parent.HasExited) {
            Stop-Process -Id $Parent.Id -Force -ErrorAction SilentlyContinue
        }
        $Parent.Dispose()
    }

    # A scripted attempt sequence may execute only one infrastructure retry.
    $AttemptCounter = 0
    $Sequence = Invoke-Wb05AttemptSequence `
        -AttemptRunner {
            param($AttemptNumber, $RetryOf)
            $script:AttemptCounter++
            if ($AttemptNumber -eq 1) {
                return [pscustomobject]@{
                    attempt_id = 'attempt-1'
                    classification = 'InfrastructureInterrupted'
                }
            }
            return [pscustomobject]@{
                attempt_id = 'attempt-2'
                classification = 'Passed'
            }
        } `
        -CooldownEvaluator {
            return [pscustomobject]@{ status = 'Passed'; reasons = @() }
        }

    if ($Sequence.Count -ne 2) {
        throw 'Expected exactly one controlled infrastructure retry.'
    }
    if ($Sequence[1].retry_of_attempt_id -ne 'attempt-1') {
        throw 'Retry relation was not preserved.'
    }

    Write-Host 'WORKBOOK05_PHASE3_RUN_MODULE_TEST_PASS'
}
finally {
    Remove-Item Env:WB05_PHASE3_TEST_MODE -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $TemporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
}
