[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [string] $PythonPath = 'python',
    [Parameter(Mandatory)][string] $BundleRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$BundleParent = Split-Path -Parent $BundleRoot
$BundleName = Split-Path -Leaf $BundleRoot
if (-not (Test-Path -LiteralPath $BundleParent -PathType Container)) {
    New-Item -ItemType Directory -Path $BundleParent -Force:$false | Out-Null
}
if (Test-Path -LiteralPath $BundleRoot) {
    throw "Fixture bundle already exists: $BundleRoot"
}

$env:WB05_PHASE3_TEST_MODE = '1'
$ModulePath = Join-Path $RepositoryRoot 'scripts\testing\workbook05\Workbook05.Run.psm1'
Import-Module -Name $ModulePath -Force -ErrorAction Stop
try {
    $Workspace = New-Wb05RunWorkspace `
        -Root $BundleParent `
        -RunIdentity $BundleName `
        -AllowTestRoot

    # Execute the real process boundary with arguments that exercise whitespace,
    # embedded quotes, an empty value, and a trailing backslash.
    $FixturePath = Join-Path `
        $RepositoryRoot `
        'tests\testing\workbook05\fixtures\phase3\process\normal_child.py'
    $Arguments = @(
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
    )
    $Result = Invoke-Wb05SupervisedProcess `
        -ExecutablePath $PythonPath `
        -Arguments $Arguments `
        -WorkingDirectory $RepositoryRoot `
        -Workspace $Workspace `
        -AttemptId 'fixture-attempt-1' `
        -StageTimeoutSeconds 30 `
        -SampleIntervalMilliseconds 100
    if ($Result.classification -ne 'Passed') {
        throw "Synthetic process fixture did not pass: $($Result.classification)"
    }

    $RootRawPath = Join-Path $Workspace.root 'raw.txt'
    $RawPath = Join-Path $Workspace.outputs 'raw.txt'
    Move-Item -LiteralPath $RootRawPath -Destination $RawPath -Force:$false

    # Emit a deterministic driver event sequence. These are fixture events, not
    # model evidence, and their only purpose is to prove parser/order handling.
    $EventsPath = Join-Path $Workspace.events 'events.jsonl'
    $EventRows = @(
        '{"event":"started","sequence":1}',
        '{"event":"first_token","sequence":2}',
        '{"event":"token","sequence":3,"token_index":1}',
        '{"event":"completed","sequence":4}'
    )
    [IO.File]::WriteAllText(
        $EventsPath,
        (($EventRows -join "`n") + "`n"),
        [Text.UTF8Encoding]::new($false)
    )

    $CommandPath = Join-Path $Workspace.records 'command.json'
    Write-Wb05AtomicJson -Path $CommandPath -Value ([ordered]@{
        executable = $PythonPath
        arguments = $Arguments
        working_directory = '.'
        environment = [ordered]@{ WB05_PHASE3_TEST_MODE = '1' }
    })

    $OperatingSystem = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction Stop
    $ResourcePath = Join-Path $Workspace.records 'resource-summary.json'
    Write-Wb05AtomicJson -Path $ResourcePath -Value ([ordered]@{
        record_type = 'resource-summary'
        schema_version = '1.0'
        attempt_id = 'fixture-attempt-1'
        sample_count = 1
        peak_working_set_bytes = 0
        peak_private_bytes = 0
        minimum_available_memory_bytes = ([int64] $OperatingSystem.FreePhysicalMemory * 1024L)
        maximum_commit_percent = 0.0
        mean_cpu_percent = 0.0
        maximum_cpu_percent = 0.0
    })

    $RawDigest = (
        Get-FileHash -LiteralPath $RawPath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    $AttemptPath = Join-Path $Workspace.records 'process-attempt.json'
    Write-Wb05AtomicJson -Path $AttemptPath -Value ([ordered]@{
        record_type = 'process-attempt'
        schema_version = '1.0'
        attempt_id = 'fixture-attempt-1'
        attempt_number = 1
        retry_of_attempt_id = $null
        classification = 'Passed'
        failure_ids = @()
        execution = [ordered]@{
            executable = $PythonPath
            arguments = $Arguments
            working_directory = '.'
            exit_code = 0
            started_utc = $Result.started_utc
            ended_utc = $Result.ended_utc
            elapsed_ms = $Result.elapsed_ms
        }
        watchdog = [ordered]@{
            safety_stop_triggered = $false
            safety_stop_reason = $null
            timeout_triggered = $false
            heartbeat_stale = $false
        }
        evidence = [ordered]@{
            stdout_path = 'logs/fixture-attempt-1.stdout.txt'
            stderr_path = 'logs/fixture-attempt-1.stderr.txt'
            events_path = 'events/events.jsonl'
            raw_output_path = 'outputs/raw.txt'
            raw_output_sha256 = $RawDigest
            termination_proof_path = 'proof/fixture-attempt-1.termination.json'
            resource_summary_path = 'records/resource-summary.json'
            command_path = 'records/command.json'
        }
    })

    # Generate the manifest last so every retained byte is covered exactly once.
    $ManifestRows = @(
        Get-ChildItem -LiteralPath $Workspace.root -File -Recurse -Force |
            Where-Object { $_.Name -ne 'manifest.sha256' } |
            Sort-Object FullName |
            ForEach-Object {
                $Relative = $_.FullName.Substring($Workspace.root.Length).TrimStart('\').Replace('\', '/')
                $Digest = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                "$Digest  $Relative"
            }
    )
    $ManifestPath = Join-Path $Workspace.root 'manifest.sha256'
    [IO.File]::WriteAllText(
        $ManifestPath,
        (($ManifestRows -join "`n") + "`n"),
        [Text.UTF8Encoding]::new($false)
    )

    Write-Host "WORKBOOK05_PHASE3_HARNESS_FIXTURE_PASS $($Workspace.root)"
}
finally {
    Remove-Module 'Workbook05.Run' -Force -ErrorAction SilentlyContinue
    Remove-Item Env:WB05_PHASE3_TEST_MODE -ErrorAction SilentlyContinue
}
