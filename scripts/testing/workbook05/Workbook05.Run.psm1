Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-Wb05WindowsArgument {
    [CmdletBinding()]
    param([AllowEmptyString()][string] $Value)

    # ProcessStartInfo.ArgumentList is unavailable in Windows PowerShell 5.1.
    # Apply the documented CommandLineToArgvW escaping rules instead of joining
    # untrusted arguments with spaces or invoking a shell.
    if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]') {
        return $Value
    }

    $Builder = [Text.StringBuilder]::new()
    [void] $Builder.Append('"')
    $Backslashes = 0
    foreach ($Character in $Value.ToCharArray()) {
        if ($Character -eq '\') {
            $Backslashes++
            continue
        }
        if ($Character -eq '"') {
            [void] $Builder.Append(('\' * (($Backslashes * 2) + 1)))
            [void] $Builder.Append('"')
            $Backslashes = 0
            continue
        }
        if ($Backslashes -gt 0) {
            [void] $Builder.Append(('\' * $Backslashes))
            $Backslashes = 0
        }
        [void] $Builder.Append($Character)
    }
    if ($Backslashes -gt 0) {
        [void] $Builder.Append(('\' * ($Backslashes * 2)))
    }
    [void] $Builder.Append('"')
    return $Builder.ToString()
}

function Write-Wb05Utf8NoBom {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][AllowEmptyString()][string] $Text
    )

    $Parent = Split-Path -Parent $Path
    if ($Parent -and -not (Test-Path -LiteralPath $Parent -PathType Container)) {
        New-Item -ItemType Directory -Path $Parent -Force:$false | Out-Null
    }
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}

function Write-Wb05AtomicJson {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)] $Value
    )

    $Parent = Split-Path -Parent $Path
    if ($Parent -and -not (Test-Path -LiteralPath $Parent -PathType Container)) {
        New-Item -ItemType Directory -Path $Parent -Force:$false | Out-Null
    }
    $TemporaryPath = $Path + '.tmp'
    if (Test-Path -LiteralPath $TemporaryPath) {
        throw "Atomic JSON temporary path already exists: $TemporaryPath"
    }
    try {
        $Json = $Value | ConvertTo-Json -Depth 100
        Write-Wb05Utf8NoBom -Path $TemporaryPath -Text ($Json + "`n")
        Move-Item -LiteralPath $TemporaryPath -Destination $Path -Force:$false
    }
    finally {
        Remove-Item -LiteralPath $TemporaryPath -Force -ErrorAction SilentlyContinue
    }
}

function New-Wb05RunWorkspace {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$')][string] $RunIdentity,
        [switch] $AllowTestRoot
    )

    $RootItem = Get-Item -LiteralPath $Root -Force -ErrorAction Stop
    if (-not $RootItem.PSIsContainer -or ($RootItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Run root must be one normal directory: $Root"
    }
    if ($AllowTestRoot -and $env:WB05_PHASE3_TEST_MODE -ne '1') {
        throw 'AllowTestRoot requires WB05_PHASE3_TEST_MODE=1.'
    }

    $WorkspaceRoot = Join-Path $RootItem.FullName $RunIdentity
    if (Test-Path -LiteralPath $WorkspaceRoot) {
        throw "Run workspace already exists and cannot be reused: $WorkspaceRoot"
    }
    New-Item -ItemType Directory -Path $WorkspaceRoot -Force:$false | Out-Null

    $Directories = [ordered]@{ root = $WorkspaceRoot }
    foreach ($Name in @('logs', 'events', 'outputs', 'metrics', 'proof', 'requests', 'records')) {
        $Path = Join-Path $WorkspaceRoot $Name
        New-Item -ItemType Directory -Path $Path -Force:$false | Out-Null
        $Directories[$Name] = $Path
    }
    return [pscustomobject] $Directories
}

function Get-Wb05ProcessTreeRecords {
    [CmdletBinding()]
    param([Parameter(Mandatory)][int] $RootProcessId)

    $Rows = @(
        Get-CimInstance -ClassName Win32_Process -ErrorAction Stop |
            Select-Object ProcessId, ParentProcessId, Name
    )
    $ByParent = @{}
    foreach ($Row in $Rows) {
        $Parent = [int] $Row.ParentProcessId
        if (-not $ByParent.ContainsKey($Parent)) {
            $ByParent[$Parent] = [Collections.Generic.List[object]]::new()
        }
        $ByParent[$Parent].Add($Row)
    }

    $Result = [Collections.Generic.List[object]]::new()
    $Queue = [Collections.Generic.Queue[object]]::new()
    $Queue.Enqueue([pscustomobject]@{ process_id = $RootProcessId; depth = 0; name = $null })
    $Seen = [Collections.Generic.HashSet[int]]::new()
    while ($Queue.Count -gt 0) {
        $Current = $Queue.Dequeue()
        $ProcessId = [int] $Current.process_id
        if (-not $Seen.Add($ProcessId)) {
            continue
        }
        $Matching = @($Rows | Where-Object { [int] $_.ProcessId -eq $ProcessId } | Select-Object -First 1)
        $Name = if ($Matching.Count -eq 1) { [string] $Matching[0].Name } else { [string] $Current.name }
        $Record = [pscustomobject]@{
            process_id = $ProcessId
            depth = [int] $Current.depth
            name = $Name
        }
        $Result.Add($Record)
        if ($ByParent.ContainsKey($ProcessId)) {
            foreach ($Child in $ByParent[$ProcessId]) {
                $Queue.Enqueue([pscustomobject]@{
                    process_id = [int] $Child.ProcessId
                    depth = ([int] $Current.depth) + 1
                    name = [string] $Child.Name
                })
            }
        }
    }
    return @($Result)
}

function Get-Wb05ProcessTreeIds {
    [CmdletBinding()]
    param([Parameter(Mandatory)][int] $RootProcessId)

    return @(
        Get-Wb05ProcessTreeRecords -RootProcessId $RootProcessId |
            ForEach-Object { [int] $_.process_id }
    )
}

function Stop-Wb05ProcessTree {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int] $RootProcessId,
        [Parameter(Mandatory)][string] $ProofPath
    )

    $Records = @(
        Get-Wb05ProcessTreeRecords -RootProcessId $RootProcessId |
            Sort-Object @{ Expression = 'depth'; Descending = $true }, process_id
    )
    $Proof = [Collections.Generic.List[object]]::new()
    foreach ($Record in $Records) {
        $Status = 'AlreadyExited'
        try {
            $Process = Get-Process -Id ([int] $Record.process_id) -ErrorAction Stop
            Stop-Process -Id $Process.Id -Force -ErrorAction Stop
            try { $Process.WaitForExit(5000) | Out-Null } catch { }
            $Status = 'Terminated'
        }
        catch [Microsoft.PowerShell.Commands.ProcessCommandException] {
            $Status = 'AlreadyExited'
        }
        $Proof.Add([pscustomobject]@{
            process_id = [int] $Record.process_id
            depth = [int] $Record.depth
            name = [string] $Record.name
            status = $Status
        })
    }

    Write-Wb05AtomicJson -Path $ProofPath -Value ([ordered]@{
        reason = 'CONTROLLER_TERMINATION'
        root_process_id = $RootProcessId
        terminated_processes = @($Proof)
    })
    return @($Proof)
}

function Get-Wb05MachineResourceSample {
    [CmdletBinding()]
    param([int] $RootProcessId = 0)

    $OperatingSystem = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction Stop
    $Available = [int64] $OperatingSystem.FreePhysicalMemory * 1024L
    $TotalVirtual = [double] $OperatingSystem.TotalVirtualMemorySize
    $FreeVirtual = [double] $OperatingSystem.FreeVirtualMemory
    $CommitPercent = if ($TotalVirtual -gt 0) {
        (($TotalVirtual - $FreeVirtual) / $TotalVirtual) * 100.0
    }
    else { 0.0 }

    $WorkingSet = 0L
    $PrivateBytes = 0L
    if ($RootProcessId -gt 0) {
        foreach ($Id in @(Get-Wb05ProcessTreeIds -RootProcessId $RootProcessId)) {
            try {
                $Process = Get-Process -Id $Id -ErrorAction Stop
                $WorkingSet += [int64] $Process.WorkingSet64
                $PrivateBytes += [int64] $Process.PrivateMemorySize64
            }
            catch { }
        }
    }
    return [pscustomobject]@{
        observed_utc = [DateTime]::UtcNow.ToString('o')
        available_memory_bytes = $Available
        commit_percent = [math]::Round($CommitPercent, 4)
        process_tree_working_set_bytes = $WorkingSet
        process_tree_private_bytes = $PrivateBytes
        heartbeat_age_seconds = 0.0
    }
}

function Test-Wb05RunWatchdog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object[]] $Samples,
        [int64] $MinimumAvailableMemoryBytes = 1610612736,
        [double] $MaximumCommitPercent = 90.0,
        [double] $MaximumHeartbeatAgeSeconds = 900.0,
        [int] $ConsecutiveSampleCount = 5
    )

    if ($ConsecutiveSampleCount -le 0) {
        throw 'ConsecutiveSampleCount must be positive.'
    }
    $LowMemory = 0
    $HighCommit = 0
    $StaleHeartbeat = 0
    foreach ($Sample in $Samples) {
        $LowMemory = if ([int64] $Sample.available_memory_bytes -lt $MinimumAvailableMemoryBytes) { $LowMemory + 1 } else { 0 }
        $HighCommit = if ([double] $Sample.commit_percent -gt $MaximumCommitPercent) { $HighCommit + 1 } else { 0 }
        $StaleHeartbeat = if ([double] $Sample.heartbeat_age_seconds -gt $MaximumHeartbeatAgeSeconds) { $StaleHeartbeat + 1 } else { 0 }

        if ($HighCommit -ge $ConsecutiveSampleCount) {
            return [pscustomobject]@{ triggered = $true; reason = 'HIGH_COMMIT_PERCENT'; consecutive_samples = $HighCommit }
        }
        if ($LowMemory -ge $ConsecutiveSampleCount) {
            return [pscustomobject]@{ triggered = $true; reason = 'LOW_AVAILABLE_MEMORY'; consecutive_samples = $LowMemory }
        }
        if ($StaleHeartbeat -ge $ConsecutiveSampleCount) {
            return [pscustomobject]@{ triggered = $true; reason = 'HEARTBEAT_STALE'; consecutive_samples = $StaleHeartbeat }
        }
    }
    return [pscustomobject]@{ triggered = $false; reason = $null; consecutive_samples = 0 }
}

function Test-Wb05CooldownHealth {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [int[]] $RemainingProcessIds,
        [Parameter(Mandatory)][int64] $AvailableMemoryBytes,
        [Parameter(Mandatory)][double] $CommitPercent,
        [Parameter(Mandatory)][double] $ElapsedSeconds,
        [double] $RequiredCooldownSeconds = 30,
        [int64] $MinimumAvailableMemoryBytes = 4294967296,
        [double] $MaximumCommitPercent = 70.0
    )

    $Reasons = [Collections.Generic.List[string]]::new()
    if ($RemainingProcessIds.Count -ne 0) { $Reasons.Add('PROCESS_TREE_REMAINS') }
    if ($AvailableMemoryBytes -lt $MinimumAvailableMemoryBytes) { $Reasons.Add('AVAILABLE_MEMORY_NOT_RECOVERED') }
    if ($CommitPercent -gt $MaximumCommitPercent) { $Reasons.Add('COMMIT_NOT_RECOVERED') }
    if ($ElapsedSeconds -lt $RequiredCooldownSeconds) { $Reasons.Add('COOLDOWN_DURATION_INCOMPLETE') }
    return [pscustomobject]@{
        status = if ($Reasons.Count -eq 0) { 'Passed' } else { 'Failed' }
        reasons = @($Reasons)
    }
}

function Start-Wb05RunSampler {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int] $RootProcessId,
        [Parameter(Mandatory)][string] $OutputPath,
        [int] $IntervalMilliseconds = 1000
    )

    # The returned controller is intentionally explicit.  The supervisor owns
    # sampling in-process so no untracked helper process survives a failed run.
    return [pscustomobject]@{
        root_process_id = $RootProcessId
        output_path = $OutputPath
        interval_milliseconds = $IntervalMilliseconds
        samples = [Collections.Generic.List[object]]::new()
        started_utc = [DateTime]::UtcNow
    }
}

function Stop-Wb05RunSampler {
    [CmdletBinding()]
    param([Parameter(Mandatory)] $Sampler)

    $Samples = @($Sampler.samples)
    $Rows = @(
        $Samples | ForEach-Object {
            '{0},{1},{2},{3},{4}' -f `
                $_.observed_utc,
                $_.available_memory_bytes,
                $_.commit_percent,
                $_.process_tree_working_set_bytes,
                $_.process_tree_private_bytes
        }
    )
    $Header = 'observed_utc,available_memory_bytes,commit_percent,process_tree_working_set_bytes,process_tree_private_bytes'
    $Content = @($Header) + @($Rows)
    Write-Wb05Utf8NoBom -Path $Sampler.output_path -Text (($Content -join "`n") + "`n")
    return $Samples
}

function Invoke-Wb05SupervisedProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $ExecutablePath,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]] $Arguments,
        [Parameter(Mandatory)][string] $WorkingDirectory,
        [Parameter(Mandatory)] $Workspace,
        [Parameter(Mandatory)][string] $AttemptId,
        [int] $StageTimeoutSeconds = 900,
        [int] $SampleIntervalMilliseconds = 1000
    )

    $StartInfo = [Diagnostics.ProcessStartInfo]::new()
    $StartInfo.FileName = $ExecutablePath
    $StartInfo.Arguments = (@($Arguments | ForEach-Object { ConvertTo-Wb05WindowsArgument -Value $_ }) -join ' ')
    $StartInfo.WorkingDirectory = $WorkingDirectory
    $StartInfo.UseShellExecute = $false
    $StartInfo.RedirectStandardOutput = $true
    $StartInfo.RedirectStandardError = $true
    $StartInfo.CreateNoWindow = $true

    $StdoutPath = Join-Path $Workspace.logs ($AttemptId + '.stdout.txt')
    $StderrPath = Join-Path $Workspace.logs ($AttemptId + '.stderr.txt')
    $ResourcePath = Join-Path $Workspace.metrics ($AttemptId + '.resources.csv')
    $TerminationPath = Join-Path $Workspace.proof ($AttemptId + '.termination.json')
    $Process = [Diagnostics.Process]::new()
    $Process.StartInfo = $StartInfo
    $ProcessStarted = $false
    $StartedUtc = [DateTime]::UtcNow
    $Stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $SafetyReason = $null
    $TimedOut = $false

    try {
        if (-not $Process.Start()) {
            throw 'System.Diagnostics.Process.Start returned false.'
        }
        $ProcessStarted = $true
        $StdoutTask = $Process.StandardOutput.ReadToEndAsync()
        $StderrTask = $Process.StandardError.ReadToEndAsync()
        $Sampler = Start-Wb05RunSampler `
            -RootProcessId $Process.Id `
            -OutputPath $ResourcePath `
            -IntervalMilliseconds $SampleIntervalMilliseconds

        while (-not $Process.WaitForExit($SampleIntervalMilliseconds)) {
            $Sample = Get-Wb05MachineResourceSample -RootProcessId $Process.Id
            $HeartbeatPath = Join-Path $Workspace.root 'heartbeat.txt'
            if (Test-Path -LiteralPath $HeartbeatPath -PathType Leaf) {
                $Sample.heartbeat_age_seconds = [math]::Max(
                    0.0,
                    ([DateTime]::UtcNow - (Get-Item -LiteralPath $HeartbeatPath).LastWriteTimeUtc).TotalSeconds
                )
            }
            $Sampler.samples.Add($Sample)
            $Decision = Test-Wb05RunWatchdog -Samples @($Sampler.samples)
            if ($Decision.triggered) {
                $SafetyReason = $Decision.reason
                Stop-Wb05ProcessTree -RootProcessId $Process.Id -ProofPath $TerminationPath | Out-Null
                break
            }
            if ($Stopwatch.Elapsed.TotalSeconds -ge $StageTimeoutSeconds) {
                $TimedOut = $true
                Stop-Wb05ProcessTree -RootProcessId $Process.Id -ProofPath $TerminationPath | Out-Null
                break
            }
        }

        try { $Process.WaitForExit(5000) | Out-Null } catch { }
        $Stdout = $StdoutTask.GetAwaiter().GetResult()
        $Stderr = $StderrTask.GetAwaiter().GetResult()
        Write-Wb05Utf8NoBom -Path $StdoutPath -Text $Stdout
        Write-Wb05Utf8NoBom -Path $StderrPath -Text $Stderr
        Stop-Wb05RunSampler -Sampler $Sampler | Out-Null

        if (-not (Test-Path -LiteralPath $TerminationPath -PathType Leaf)) {
            Write-Wb05AtomicJson -Path $TerminationPath -Value ([ordered]@{
                reason = if ($Process.ExitCode -eq 0) { 'NORMAL_EXIT' } else { 'PROCESS_EXIT' }
                root_process_id = $Process.Id
                terminated_processes = @()
            })
        }

        $Classification = if ($SafetyReason) {
            'ResourceSafetyStop'
        }
        elseif ($TimedOut) {
            'Timeout'
        }
        elseif ($Process.ExitCode -eq 0) {
            'Passed'
        }
        else {
            'NativeProcessFailure'
        }
        return [pscustomobject]@{
            attempt_id = $AttemptId
            classification = $Classification
            exit_code = [int] $Process.ExitCode
            started_utc = $StartedUtc.ToString('o')
            ended_utc = [DateTime]::UtcNow.ToString('o')
            elapsed_ms = [int64] $Stopwatch.ElapsedMilliseconds
            stdout_path = $StdoutPath
            stderr_path = $StderrPath
            resource_path = $ResourcePath
            termination_path = $TerminationPath
            safety_stop_reason = $SafetyReason
            timeout_triggered = $TimedOut
        }
    }
    finally {
        $Stopwatch.Stop()
        if ($ProcessStarted -and -not $Process.HasExited) {
            try { Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue } catch { }
        }
        $Process.Dispose()
    }
}

function Invoke-Wb05AttemptSequence {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][scriptblock] $AttemptRunner,
        [Parameter(Mandatory)][scriptblock] $CooldownEvaluator
    )

    $Results = [Collections.Generic.List[object]]::new()
    $First = & $AttemptRunner 1 $null
    $FirstRow = [pscustomobject]@{
        attempt_id = [string] $First.attempt_id
        classification = [string] $First.classification
        retry_of_attempt_id = $null
    }
    $Results.Add($FirstRow)
    if ($FirstRow.classification -ne 'InfrastructureInterrupted') {
        return @($Results)
    }

    $Cooldown = & $CooldownEvaluator
    if ([string] $Cooldown.status -ne 'Passed') {
        return @($Results)
    }

    $Second = & $AttemptRunner 2 $FirstRow.attempt_id
    $SecondRow = [pscustomobject]@{
        attempt_id = [string] $Second.attempt_id
        classification = [string] $Second.classification
        retry_of_attempt_id = $FirstRow.attempt_id
    }
    $Results.Add($SecondRow)
    return @($Results)
}

Export-ModuleMember -Function @(
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
