param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [Parameter(Mandatory = $true)][string]$GpuOutputPath,
    [Parameter(Mandatory = $true)][string]$ReadyPath,
    [Parameter(Mandatory = $true)][string]$StartPath,
    [Parameter(Mandatory = $true)][string]$StopPath,
    [int]$IntervalMilliseconds = 250
)

$ErrorActionPreference = 'Stop'
$logicalProcessors = [Environment]::ProcessorCount
$previousCpu = $null
$previousTime = $null
$engineJob = $null
$memoryJob = $null
$engineQueryStartedUtc = $null
$engineQueryCompletedUtc = $null
$memoryQueryStartedUtc = $null
$memoryQueryCompletedUtc = $null
$gpuEngineQueryOk = $false
$gpuMemoryQueryOk = $false
$engines = @()
$gpuMemory = @()
$targetProcess = [Diagnostics.Process]::GetProcessById($ProcessId)
$utf8WithBom = [Text.UTF8Encoding]::new($true)
$cpuWriter = [IO.StreamWriter]::new($OutputPath, $false, $utf8WithBom)
$gpuWriter = [IO.StreamWriter]::new($GpuOutputPath, $false, $utf8WithBom)
$cpuWriter.AutoFlush = $true
$gpuWriter.AutoFlush = $true

$cpuWriter.WriteLine('timestamp_utc,cpu_percent,cpu_sample_definition,sample_deadline_elapsed_ms,sample_lateness_ms')
$gpuWriter.WriteLine('observation_utc,gpu_percent,gpu_engine_count,gpu_dedicated_mb,gpu_shared_mb,gpu_engine_query_ok,gpu_memory_query_ok,gpu_engine_query_started_utc,gpu_engine_query_completed_utc,gpu_memory_query_started_utc,gpu_memory_query_completed_utc')

try {
    # Warm both Windows performance providers while the workload is still
    # suspended. Provider startup is allowed to be slow, but it must never
    # consume a scheduled CPU sampling interval.
    try {
        @(Get-WmiObject -Class Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine -ErrorAction Stop) | Out-Null
    }
    catch {
        # The timed asynchronous observation below records the authoritative
        # success/failure status. Warm-up failure is intentionally non-fatal.
    }
    try {
        @(Get-WmiObject -Class Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory -ErrorAction Stop) | Out-Null
    }
    catch {
        # See the GPU engine warm-up comment above.
    }

    [IO.File]::WriteAllText($ReadyPath, "ready`n", [Text.Encoding]::ASCII)

    while (
        -not [IO.File]::Exists($StartPath) -and
        -not [IO.File]::Exists($StopPath)
    ) {
        [Threading.Thread]::Sleep(1)
    }
    if ([IO.File]::Exists($StopPath)) { return }

    $workloadResumeText = [IO.File]::ReadAllText($StartPath).Trim()
    $workloadResumeUtc = [DateTimeOffset]::Parse(
        $workloadResumeText,
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::RoundtripKind
    ).UtcDateTime
    $sampleClock = [Diagnostics.Stopwatch]::StartNew()
    $resumeElapsedAtClockStartMilliseconds = [Math]::Max(
        0.0,
        ([DateTime]::UtcNow - $workloadResumeUtc).TotalMilliseconds
    )
    $nextDeadlineMilliseconds = [double]$IntervalMilliseconds

    while (-not [IO.File]::Exists($StopPath)) {
        $elapsedSinceResumeMilliseconds = $resumeElapsedAtClockStartMilliseconds + $sampleClock.Elapsed.TotalMilliseconds
        $remainingMilliseconds = $nextDeadlineMilliseconds - $elapsedSinceResumeMilliseconds
        if ($remainingMilliseconds -gt 0) {
            [Threading.Thread]::Sleep([int][Math]::Ceiling($remainingMilliseconds))
        }
        if ([IO.File]::Exists($StopPath)) { break }

        $sampleDeadlineMilliseconds = $nextDeadlineMilliseconds
        try {
            $targetProcess.Refresh()
            if ($targetProcess.HasExited) { break }
            $totalProcessorSeconds = $targetProcess.TotalProcessorTime.TotalSeconds
        }
        catch {
            break
        }
        $sampleObservedElapsedMilliseconds = $resumeElapsedAtClockStartMilliseconds + $sampleClock.Elapsed.TotalMilliseconds
        $sampleLatenessMilliseconds = [Math]::Max(
            0.0,
            $sampleObservedElapsedMilliseconds - $sampleDeadlineMilliseconds
        )
        $now = [DateTime]::UtcNow

        $cpuPercent = $null
        $cpuSampleDefinition = $null
        if ($null -ne $previousCpu) {
            $elapsed = ($now - $previousTime).TotalSeconds
            if ($elapsed -gt 0) {
                $cpuPercent = (($totalProcessorSeconds - $previousCpu) / $elapsed / $logicalProcessors) * 100.0
                $cpuSampleDefinition = 'interval_delta'
            }
        }
        else {
            $lifetimeElapsed = ($now - $workloadResumeUtc).TotalSeconds
            if ($lifetimeElapsed -gt 0) {
                $cpuPercent = ($totalProcessorSeconds / $lifetimeElapsed / $logicalProcessors) * 100.0
                $cpuSampleDefinition = 'lifetime_average_since_workload_resume'
            }
        }
        $previousCpu = $totalProcessorSeconds
        $previousTime = $now

        if ($null -ne $cpuPercent) {
            $culture = [Globalization.CultureInfo]::InvariantCulture
            $cpuText = ([Math]::Min(100.0, [Math]::Max(0.0, $cpuPercent))).ToString('F6', $culture)
            $deadlineText = $sampleDeadlineMilliseconds.ToString('F6', $culture)
            $latenessText = $sampleLatenessMilliseconds.ToString('F6', $culture)
            $line = '{0},{1},{2},{3},{4}' -f $now.ToString('o'), $cpuText, $cpuSampleDefinition, $deadlineText, $latenessText
            $cpuWriter.WriteLine($line)
        }

        # Launch one real GPU observation asynchronously after the first CPU
        # deadline. Get-WmiObject -AsJob returns promptly and the CPU cadence
        # loop never waits for either provider.
        if ($null -eq $engineQueryStartedUtc) {
            $engineQueryStartedUtc = [DateTime]::UtcNow
            try {
                $engineJob = Get-WmiObject -Class Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine -AsJob -ErrorAction Stop
            }
            catch {
                $engineQueryCompletedUtc = [DateTime]::UtcNow
            }
            $memoryQueryStartedUtc = [DateTime]::UtcNow
            try {
                $memoryJob = Get-WmiObject -Class Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory -AsJob -ErrorAction Stop
            }
            catch {
                $memoryQueryCompletedUtc = [DateTime]::UtcNow
            }
        }

        do {
            $nextDeadlineMilliseconds += $IntervalMilliseconds
        } while (
            $nextDeadlineMilliseconds -le (
                $resumeElapsedAtClockStartMilliseconds +
                $sampleClock.Elapsed.TotalMilliseconds
            )
        )
    }
}
finally {
    if ($null -ne $engineJob) {
        try {
            $null = Wait-Job -Job $engineJob -Timeout 5 -ErrorAction Stop
            if ($engineJob.State -ne 'Completed') {
                throw "GPU engine query did not complete"
            }
            $engines = @(
                Receive-Job -Job $engineJob -ErrorAction Stop |
                    Where-Object { $_.Name -match ('^pid_' + $ProcessId + '_') }
            )
            $gpuEngineQueryOk = $true
        }
        catch {
            $engines = @()
            $gpuEngineQueryOk = $false
        }
        finally {
            $engineQueryCompletedUtc = if ($null -ne $engineJob.PSEndTime) {
                $engineJob.PSEndTime.ToUniversalTime()
            }
            else {
                [DateTime]::UtcNow
            }
            if ($engineJob.State -eq 'Running') {
                Stop-Job -Job $engineJob -ErrorAction SilentlyContinue
            }
            Remove-Job -Job $engineJob -Force -ErrorAction SilentlyContinue
        }
    }
    if ($null -ne $memoryJob) {
        try {
            $null = Wait-Job -Job $memoryJob -Timeout 5 -ErrorAction Stop
            if ($memoryJob.State -ne 'Completed') {
                throw "GPU memory query did not complete"
            }
            $gpuMemory = @(
                Receive-Job -Job $memoryJob -ErrorAction Stop |
                    Where-Object { $_.Name -match ('^pid_' + $ProcessId + '_') }
            )
            $gpuMemoryQueryOk = $true
        }
        catch {
            $gpuMemory = @()
            $gpuMemoryQueryOk = $false
        }
        finally {
            $memoryQueryCompletedUtc = if ($null -ne $memoryJob.PSEndTime) {
                $memoryJob.PSEndTime.ToUniversalTime()
            }
            else {
                [DateTime]::UtcNow
            }
            if ($memoryJob.State -eq 'Running') {
                Stop-Job -Job $memoryJob -ErrorAction SilentlyContinue
            }
            Remove-Job -Job $memoryJob -Force -ErrorAction SilentlyContinue
        }
    }

    $gpuPercent = $null
    if ($gpuEngineQueryOk) {
        $gpuPercent = 0.0
        if ($engines.Count -gt 0) {
            $gpuPercent = [double](($engines | Measure-Object -Property UtilizationPercentage -Maximum).Maximum)
        }
    }
    $dedicatedBytes = $null
    $sharedBytes = $null
    if ($gpuMemoryQueryOk) {
        $dedicatedBytes = [double](($gpuMemory | Measure-Object -Property DedicatedUsage -Sum).Sum)
        $sharedBytes = [double](($gpuMemory | Measure-Object -Property SharedUsage -Sum).Sum)
    }

    $culture = [Globalization.CultureInfo]::InvariantCulture
    $gpuText = ''
    $engineCountText = ''
    if ($gpuEngineQueryOk) {
        $gpuText = ([Math]::Min(100.0, [Math]::Max(0.0, $gpuPercent))).ToString('F6', $culture)
        $engineCountText = $engines.Count.ToString($culture)
    }
    $dedicatedText = ''
    $sharedText = ''
    if ($gpuMemoryQueryOk) {
        $dedicatedText = ($dedicatedBytes / 1MB).ToString('F6', $culture)
        $sharedText = ($sharedBytes / 1MB).ToString('F6', $culture)
    }
    $engineStartedText = if ($null -ne $engineQueryStartedUtc) { $engineQueryStartedUtc.ToString('o') } else { '' }
    $engineCompletedText = if ($null -ne $engineQueryCompletedUtc) { $engineQueryCompletedUtc.ToString('o') } else { '' }
    $memoryStartedText = if ($null -ne $memoryQueryStartedUtc) { $memoryQueryStartedUtc.ToString('o') } else { '' }
    $memoryCompletedText = if ($null -ne $memoryQueryCompletedUtc) { $memoryQueryCompletedUtc.ToString('o') } else { '' }
    $gpuLine = '{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}' -f [DateTime]::UtcNow.ToString('o'), $gpuText, $engineCountText, $dedicatedText, $sharedText, $gpuEngineQueryOk.ToString().ToLowerInvariant(), $gpuMemoryQueryOk.ToString().ToLowerInvariant(), $engineStartedText, $engineCompletedText, $memoryStartedText, $memoryCompletedText
    $gpuWriter.WriteLine($gpuLine)
    $targetProcess.Dispose()
    $cpuWriter.Dispose()
    $gpuWriter.Dispose()
}
