param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [Parameter(Mandatory = $true)][string]$ReadyPath,
    [Parameter(Mandatory = $true)][string]$StopPath,
    [int]$IntervalMilliseconds = 250
)

$ErrorActionPreference = 'Stop'
$logicalProcessors = [Environment]::ProcessorCount
$previousCpu = $null
$previousTime = $null
'timestamp_utc,cpu_percent,cpu_sample_definition,gpu_percent,gpu_engine_count,gpu_dedicated_mb,gpu_shared_mb,gpu_engine_query_ok,gpu_memory_query_ok' | Set-Content -LiteralPath $OutputPath -Encoding utf8
$readySent = $false

while (-not (Test-Path -LiteralPath $StopPath)) {
    $now = [DateTime]::UtcNow
    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($null -eq $process) { break }

    $cpuPercent = $null
    $cpuSampleDefinition = $null
    if ($null -ne $previousCpu) {
        $elapsed = ($now - $previousTime).TotalSeconds
        if ($elapsed -gt 0) {
            $cpuPercent = (($process.TotalProcessorTime.TotalSeconds - $previousCpu) / $elapsed / $logicalProcessors) * 100.0
            $cpuSampleDefinition = 'interval_delta'
        }
    }
    else {
        $lifetimeElapsed = ($now - $process.StartTime.ToUniversalTime()).TotalSeconds
        if ($lifetimeElapsed -gt 0) {
            $cpuPercent = ($process.TotalProcessorTime.TotalSeconds / $lifetimeElapsed / $logicalProcessors) * 100.0
            $cpuSampleDefinition = 'lifetime_average_since_process_start'
        }
    }
    $previousCpu = $process.TotalProcessorTime.TotalSeconds
    $previousTime = $now

    $gpuEngineQueryOk = $false
    $engines = @()
    try {
        $engines = @(Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine -ErrorAction Stop |
            Where-Object { $_.Name -match ('^pid_' + $ProcessId + '_') })
        $gpuEngineQueryOk = $true
    }
    catch {
        $engines = @()
    }
    # Windows reports separate engines. Task Manager-style process GPU usage is
    # the busiest engine at a timestamp; summing engines can exceed 100%.
    $gpuPercent = $null
    if ($gpuEngineQueryOk) {
        $gpuPercent = 0.0
        if ($engines.Count -gt 0) {
            $gpuPercent = [double](($engines | Measure-Object -Property UtilizationPercentage -Maximum).Maximum)
        }
    }

    $gpuMemoryQueryOk = $false
    $gpuMemory = @()
    $dedicatedBytes = $null
    $sharedBytes = $null
    try {
        $gpuMemory = @(Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory -ErrorAction Stop |
            Where-Object { $_.Name -match ('^pid_' + $ProcessId + '_') })
        $dedicatedBytes = [double](($gpuMemory | Measure-Object -Property DedicatedUsage -Sum).Sum)
        $sharedBytes = [double](($gpuMemory | Measure-Object -Property SharedUsage -Sum).Sum)
        $gpuMemoryQueryOk = $true
    }
    catch {
        $gpuMemory = @()
        $dedicatedBytes = $null
        $sharedBytes = $null
    }

    if ($null -ne $cpuPercent) {
        $culture = [Globalization.CultureInfo]::InvariantCulture
        $cpuText = ([Math]::Min(100.0, [Math]::Max(0.0, $cpuPercent))).ToString('F6', $culture)
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
        $line = '{0},{1},{2},{3},{4},{5},{6},{7},{8}' -f $now.ToString('o'), $cpuText, $cpuSampleDefinition, $gpuText, $engineCountText, $dedicatedText, $sharedText, $gpuEngineQueryOk.ToString().ToLowerInvariant(), $gpuMemoryQueryOk.ToString().ToLowerInvariant()
        Add-Content -LiteralPath $OutputPath -Value $line -Encoding utf8
    }
    if (-not $readySent) {
        'ready' | Set-Content -LiteralPath $ReadyPath -Encoding ascii
        $readySent = $true
    }
    Start-Sleep -Milliseconds $IntervalMilliseconds
}
