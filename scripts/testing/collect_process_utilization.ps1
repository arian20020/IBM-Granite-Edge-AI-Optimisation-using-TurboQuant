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
'timestamp_utc,cpu_percent,gpu_percent,gpu_engine_count,gpu_dedicated_mb,gpu_shared_mb' | Set-Content -LiteralPath $OutputPath -Encoding utf8
$readySent = $false

while (-not (Test-Path -LiteralPath $StopPath)) {
    $now = [DateTime]::UtcNow
    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($null -eq $process) { break }

    $cpuPercent = $null
    if ($null -ne $previousCpu) {
        $elapsed = ($now - $previousTime).TotalSeconds
        if ($elapsed -gt 0) {
            $cpuPercent = (($process.TotalProcessorTime.TotalSeconds - $previousCpu) / $elapsed / $logicalProcessors) * 100.0
        }
    }
    $previousCpu = $process.TotalProcessorTime.TotalSeconds
    $previousTime = $now

    $engines = @(Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match ('^pid_' + $ProcessId + '_') })
    # Windows reports separate engines. Task Manager-style process GPU usage is
    # the busiest engine at a timestamp; summing engines can exceed 100%.
    $gpuPercent = 0.0
    if ($engines.Count -gt 0) {
        $gpuPercent = [double](($engines | Measure-Object -Property UtilizationPercentage -Maximum).Maximum)
    }
    $gpuMemory = @(Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match ('^pid_' + $ProcessId + '_') })
    $dedicatedBytes = [double](($gpuMemory | Measure-Object -Property DedicatedUsage -Sum).Sum)
    $sharedBytes = [double](($gpuMemory | Measure-Object -Property SharedUsage -Sum).Sum)
    if ($null -ne $cpuPercent) {
        $line = '{0},{1:F6},{2:F6},{3},{4:F6},{5:F6}' -f $now.ToString('o'), [Math]::Min(100.0, [Math]::Max(0.0, $cpuPercent)), [Math]::Min(100.0, [Math]::Max(0.0, $gpuPercent)), $engines.Count, ($dedicatedBytes / 1MB), ($sharedBytes / 1MB)
        Add-Content -LiteralPath $OutputPath -Value $line -Encoding utf8
    }
    if (-not $readySent) {
        'ready' | Set-Content -LiteralPath $ReadyPath -Encoding ascii
        $readySent = $true
    }
    Start-Sleep -Milliseconds $IntervalMilliseconds
}
