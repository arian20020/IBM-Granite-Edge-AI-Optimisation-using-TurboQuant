param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][string]$CpuOutputPath,
    [Parameter(Mandatory = $true)][string]$GpuOutputPath,
    [Parameter(Mandatory = $true)][string]$ReadyPath,
    [Parameter(Mandatory = $true)][string]$StartPath,
    [Parameter(Mandatory = $true)][string]$StopPath,
    [int]$CpuIntervalMilliseconds = 250,
    [int]$GpuIntervalMilliseconds = 1000
)

$ErrorActionPreference = 'Stop'
if ($CpuIntervalMilliseconds -lt 50) {
    throw 'CpuIntervalMilliseconds must be at least 50'
}
if ($GpuIntervalMilliseconds -lt $CpuIntervalMilliseconds) {
    throw 'GpuIntervalMilliseconds must be at least the CPU interval'
}

$logicalProcessors = [Environment]::ProcessorCount
$target = [Diagnostics.Process]::GetProcessById($ProcessId)
$encoding = [Text.UTF8Encoding]::new($true)
$cpuWriter = [IO.StreamWriter]::new($CpuOutputPath, $false, $encoding)
$gpuWriter = [IO.StreamWriter]::new($GpuOutputPath, $false, $encoding)
$cpuWriter.AutoFlush = $true
$gpuWriter.AutoFlush = $true
$cpuWriter.WriteLine('timestamp_utc,cpu_percent,cpu_sample_definition')
$gpuWriter.WriteLine('timestamp_utc,gpu_percent,gpu_engine_count,gpu_dedicated_mb,gpu_shared_mb,gpu_dedicated_bytes,gpu_shared_bytes,gpu_engine_query_ok,gpu_memory_query_ok')

$previousCpu = $null
$previousTime = $null
$engineJob = $null
$memoryJob = $null
$nextGpuDeadlineMilliseconds = 0.0

$finishGpuObservation = {
    param([bool]$Wait)
    if ($null -eq $engineJob -or $null -eq $memoryJob) {
        return $false
    }
    if ($Wait) {
        $null = Wait-Job -Job $engineJob -Timeout 5 -ErrorAction SilentlyContinue
        $null = Wait-Job -Job $memoryJob -Timeout 5 -ErrorAction SilentlyContinue
    }
    if ($engineJob.State -eq 'Running' -or $memoryJob.State -eq 'Running') {
        return $false
    }

    $engineOk = $false
    $memoryOk = $false
    $engines = @()
    $memoryRows = @()
    try {
        if ($engineJob.State -ne 'Completed') {
            throw 'GPU engine query did not complete'
        }
        $engines = @(
            Receive-Job -Job $engineJob -ErrorAction Stop |
                Where-Object { $_.Name -match ('^pid_' + $ProcessId + '_') }
        )
        $engineOk = $true
    }
    catch {
        $engines = @()
    }
    try {
        if ($memoryJob.State -ne 'Completed') {
            throw 'GPU memory query did not complete'
        }
        $memoryRows = @(
            Receive-Job -Job $memoryJob -ErrorAction Stop |
                Where-Object { $_.Name -match ('^pid_' + $ProcessId + '_') }
        )
        $memoryOk = $true
    }
    catch {
        $memoryRows = @()
    }

    $gpuPercent = ''
    $engineCount = ''
    if ($engineOk) {
        $value = 0.0
        if ($engines.Count -gt 0) {
            $value = [double]((
                $engines |
                    Measure-Object -Property UtilizationPercentage -Maximum
            ).Maximum)
        }
        $gpuPercent = [Math]::Min(100.0, [Math]::Max(0.0, $value)).ToString(
            'F6',
            [Globalization.CultureInfo]::InvariantCulture
        )
        $engineCount = $engines.Count.ToString(
            [Globalization.CultureInfo]::InvariantCulture
        )
    }

    $dedicated = ''
    $shared = ''
    $dedicatedByteText = ''
    $sharedByteText = ''
    if ($memoryOk) {
        $dedicatedBytes = [double]((
            $memoryRows |
                Measure-Object -Property DedicatedUsage -Sum
        ).Sum)
        $sharedBytes = [double]((
            $memoryRows |
                Measure-Object -Property SharedUsage -Sum
        ).Sum)
        $dedicated = ($dedicatedBytes / 1MB).ToString(
            'F6',
            [Globalization.CultureInfo]::InvariantCulture
        )
        $shared = ($sharedBytes / 1MB).ToString(
            'F6',
            [Globalization.CultureInfo]::InvariantCulture
        )
        $dedicatedByteText = $dedicatedBytes.ToString(
            'F0',
            [Globalization.CultureInfo]::InvariantCulture
        )
        $sharedByteText = $sharedBytes.ToString(
            'F0',
            [Globalization.CultureInfo]::InvariantCulture
        )
    }

    $line = '{0},{1},{2},{3},{4},{5},{6},{7},{8}' -f (
        [DateTime]::UtcNow.ToString('o'),
        $gpuPercent,
        $engineCount,
        $dedicated,
        $shared,
        $dedicatedByteText,
        $sharedByteText,
        $engineOk.ToString().ToLowerInvariant(),
        $memoryOk.ToString().ToLowerInvariant()
    )
    $gpuWriter.WriteLine($line)

    foreach ($job in @($engineJob, $memoryJob)) {
        if ($null -ne $job) {
            if ($job.State -eq 'Running') {
                Stop-Job -Job $job -ErrorAction SilentlyContinue
            }
            Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
        }
    }
    $script:engineJob = $null
    $script:memoryJob = $null
    return $true
}

try {
    # Provider warm-up occurs before the workload resumes.
    try {
        @(
            Get-WmiObject `
                -Class Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine `
                -ErrorAction Stop
        ) | Out-Null
    }
    catch {
        # Authoritative query success is recorded for each in-window row.
    }
    try {
        @(
            Get-WmiObject `
                -Class Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory `
                -ErrorAction Stop
        ) | Out-Null
    }
    catch {
        # Authoritative query success is recorded for each in-window row.
    }

    [IO.File]::WriteAllText($ReadyPath, "ready`n", [Text.Encoding]::ASCII)
    while (
        -not [IO.File]::Exists($StartPath) -and
        -not [IO.File]::Exists($StopPath)
    ) {
        [Threading.Thread]::Sleep(2)
    }
    if ([IO.File]::Exists($StopPath)) {
        return
    }

    $resumeUtc = [DateTime]::UtcNow
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $nextCpuDeadlineMilliseconds = [double]$CpuIntervalMilliseconds

    while (-not [IO.File]::Exists($StopPath)) {
        $remaining = $nextCpuDeadlineMilliseconds - $clock.Elapsed.TotalMilliseconds
        if ($remaining -gt 0) {
            [Threading.Thread]::Sleep([int][Math]::Ceiling($remaining))
        }
        if ([IO.File]::Exists($StopPath)) {
            break
        }

        try {
            $target.Refresh()
            if ($target.HasExited) {
                break
            }
            $totalCpuSeconds = $target.TotalProcessorTime.TotalSeconds
        }
        catch {
            break
        }
        $now = [DateTime]::UtcNow
        if ($null -eq $previousCpu) {
            $elapsed = ($now - $resumeUtc).TotalSeconds
            $definition = 'lifetime_average_since_workload_resume'
            $cpuPercent = if ($elapsed -gt 0) {
                ($totalCpuSeconds / $elapsed / $logicalProcessors) * 100.0
            }
            else {
                0.0
            }
        }
        else {
            $elapsed = ($now - $previousTime).TotalSeconds
            $definition = 'interval_delta'
            $cpuPercent = if ($elapsed -gt 0) {
                (
                    ($totalCpuSeconds - $previousCpu) /
                    $elapsed /
                    $logicalProcessors
                ) * 100.0
            }
            else {
                0.0
            }
        }
        $previousCpu = $totalCpuSeconds
        $previousTime = $now
        $boundedCpu = [Math]::Min(100.0, [Math]::Max(0.0, $cpuPercent))
        $cpuWriter.WriteLine(
            '{0},{1},{2}' -f (
                $now.ToString('o'),
                $boundedCpu.ToString(
                    'F6',
                    [Globalization.CultureInfo]::InvariantCulture
                ),
                $definition
            )
        )

        if (& $finishGpuObservation $false) {
            $nextGpuDeadlineMilliseconds = (
                $clock.Elapsed.TotalMilliseconds +
                $GpuIntervalMilliseconds
            )
        }
        if (
            $null -eq $engineJob -and
            $clock.Elapsed.TotalMilliseconds -ge $nextGpuDeadlineMilliseconds
        ) {
            $engineJob = Get-WmiObject `
                -Class Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine `
                -AsJob `
                -ErrorAction Stop
            $memoryJob = Get-WmiObject `
                -Class Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory `
                -AsJob `
                -ErrorAction Stop
        }

        do {
            $nextCpuDeadlineMilliseconds += $CpuIntervalMilliseconds
        } while (
            $nextCpuDeadlineMilliseconds -le $clock.Elapsed.TotalMilliseconds
        )
    }
}
finally {
    $null = & $finishGpuObservation $true
    foreach ($job in @($engineJob, $memoryJob)) {
        if ($null -ne $job) {
            if ($job.State -eq 'Running') {
                Stop-Job -Job $job -ErrorAction SilentlyContinue
            }
            Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
        }
    }
    $target.Dispose()
    $cpuWriter.Dispose()
    $gpuWriter.Dispose()
}
