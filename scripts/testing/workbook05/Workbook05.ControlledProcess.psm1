# Resume-only monitored native-process adapter for Workbook 05.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Wb05ControlledUtf8Text {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Text
    )

    $parent = Split-Path -Parent $Path
    if ($parent -and -not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [IO.File]::WriteAllText($Path, $Text, $encoding)
}

function Write-Wb05ControlledJsonEvidence {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [object]$Value,

        [switch]$Atomic
    )

    if (-not $Atomic) {
        Write-Wb05Json -Path $Path -Value $Value
        return
    }

    $temporaryPath = "$Path.tmp"
    if (Test-Path -LiteralPath $Path) {
        throw "Final controlled JSON evidence already exists: $Path"
    }
    if (Test-Path -LiteralPath $temporaryPath) {
        throw "Temporary controlled JSON evidence already exists: $temporaryPath"
    }

    $temporaryCreated = $false
    try {
        Write-Wb05Json -Path $temporaryPath -Value $Value
        $temporaryCreated = $true
        [IO.File]::Move($temporaryPath, $Path)
    }
    catch {
        if ($temporaryCreated -and (Test-Path -LiteralPath $temporaryPath)) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
        throw
    }
}

function ConvertTo-Wb05ControlledArgument {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Argument
    )

    # Windows PowerShell 5.1 supplies one command-line string to
    # ProcessStartInfo. Quote each explicit argument with Windows CRT rules.
    if ($Argument.Length -gt 0 -and $Argument -notmatch '[\s"]') {
        return $Argument
    }

    $builder = New-Object System.Text.StringBuilder
    [void]$builder.Append([char]34)
    $backslashCount = 0

    foreach ($character in $Argument.ToCharArray()) {
        if ($character -eq [char]92) {
            $backslashCount++
            continue
        }
        if ($character -eq [char]34) {
            [void]$builder.Append([char]92, (($backslashCount * 2) + 1))
            [void]$builder.Append([char]34)
            $backslashCount = 0
            continue
        }
        if ($backslashCount -gt 0) {
            [void]$builder.Append([char]92, $backslashCount)
            $backslashCount = 0
        }
        [void]$builder.Append($character)
    }

    if ($backslashCount -gt 0) {
        [void]$builder.Append([char]92, ($backslashCount * 2))
    }
    [void]$builder.Append([char]34)
    return $builder.ToString()
}

function Get-Wb05ControlledRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $fullPath = Assert-Wb05SafePath -Root $fullRoot -Path $Path
    $prefixLength = (
        $fullRoot + [IO.Path]::DirectorySeparatorChar
    ).Length
    return $fullPath.Substring($prefixLength).Replace('\', '/')
}

function Get-Wb05ControlledProcessTreeIds {
    param(
        [Parameter(Mandatory = $true)]
        [int]$RootProcessId
    )

    # One CIM snapshot keeps parent/child relationships internally consistent.
    $rows = @(
        Get-CimInstance Win32_Process |
            Select-Object ProcessId, ParentProcessId
    )
    $ids = New-Object System.Collections.Generic.List[int]
    $ids.Add($RootProcessId)
    $cursor = 0

    while ($cursor -lt $ids.Count) {
        $parentId = $ids[$cursor]
        foreach (
            $row in $rows |
                Where-Object { [int]$_.ParentProcessId -eq $parentId }
        ) {
            $childId = [int]$row.ProcessId
            if (-not $ids.Contains($childId)) {
                $ids.Add($childId)
            }
        }
        $cursor++
    }

    return @($ids)
}

function Stop-Wb05ControlledProcessTree {
    param(
        [Parameter(Mandatory = $true)]
        [int[]]$ProcessIds
    )

    # Get-Wb05ControlledProcessTreeIds records every parent before its known
    # descendants. Walking that recorded order backwards therefore terminates
    # compiler and linker children before their MSBuild or CMake parent.
    for ($index = $ProcessIds.Count - 1; $index -ge 0; $index--) {
        Stop-Process -Id $ProcessIds[$index] -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-Wb05ControlledLoggedProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandId,

        [Parameter(Mandatory = $true)]
        [ValidateSet(
            'route-a-merged-openvino',
            'route-b-experimental-qjl-polar'
        )]
        [string]$RouteId,

        [Parameter(Mandatory = $true)]
        [ValidateSet('runtime', 'genai', 'dependency-preflight')]
        [string]$Component,

        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [string]$EvidenceDirectory,

        [Parameter(Mandatory = $true)]
        [string]$EvidenceRoot,

        [Parameter(Mandatory = $true)]
        [ValidateRange(1, [int]::MaxValue)]
        [int]$MaximumElapsedSeconds,

        [hashtable]$EnvironmentAllowlist = @{},

        [ValidateSet('log', 'txt')]
        [string]$LogFileExtension = 'log',

        [switch]$AtomicJsonEvidence,

        [ValidateRange(1, 60)]
        [int]$SampleIntervalSeconds = 2,

        [int64]$MinimumAvailableMemoryBytes = 1610612736,

        [ValidateRange(1, 100)]
        [double]$MaximumCommitPercent = 90,

        [ValidateRange(1, 100)]
        [int]$ConsecutiveSafetySamples = 5
    )

    if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) {
        throw "Controlled native executable is missing: $FilePath"
    }
    if (-not (Test-Path -LiteralPath $WorkingDirectory -PathType Container)) {
        throw "Controlled native working directory is missing: $WorkingDirectory"
    }
    if (-not (Test-Path -LiteralPath $EvidenceDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
    }

    $safeId = $CommandId -replace '[^A-Za-z0-9._-]', '-'
    $stdoutPath = Join-Path $EvidenceDirectory "$safeId.stdout.$LogFileExtension"
    $stderrPath = Join-Path $EvidenceDirectory "$safeId.stderr.$LogFileExtension"
    $recordPath = Join-Path $EvidenceDirectory "$safeId.command.json"
    $resourceCsvPath = Join-Path $EvidenceDirectory "$safeId.resources.csv"
    $resourceSummaryPath = Join-Path $EvidenceDirectory "$safeId.resources.json"

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $FilePath
    $startInfo.Arguments = @(
        $ArgumentList |
            ForEach-Object {
                ConvertTo-Wb05ControlledArgument -Argument $_
            }
    ) -join ' '
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    $processStarted = $false
    $startedUtc = [DateTime]::UtcNow
    $endedUtc = $startedUtc
    $stdout = ''
    $stderr = ''
    $exitCode = -1
    $sampleCount = 0
    $peakWorkingSet = [int64]0
    $peakPrivateBytes = [int64]0
    $minimumAvailable = [int64]::MaxValue
    $maximumCommit = [double]0
    $lowMemorySamples = 0
    $highCommitSamples = 0
    $safetyStopTriggered = $false
    $safetyStopReason = $null
    $csvExists = $false

    try {
        if (-not $process.Start()) {
            throw "Controlled native command did not start: $CommandId"
        }
        $processStarted = $true

        # Drain both redirected streams concurrently while the main thread
        # samples the process tree and controls its deadline.
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()

        while (-not $process.HasExited) {
            $processTreeIds = @(
                Get-Wb05ControlledProcessTreeIds `
                    -RootProcessId $process.Id
            )
            $processes = @(
                foreach ($processId in $processTreeIds) {
                    Get-Process `
                        -Id $processId `
                        -ErrorAction SilentlyContinue
                }
            )

            if ($processes.Count -gt 0) {
                $workingSet = [int64](
                    (
                        $processes |
                            Measure-Object -Property WorkingSet64 -Sum
                    ).Sum
                )
                $privateBytes = [int64](
                    (
                        $processes |
                            Measure-Object -Property PrivateMemorySize64 -Sum
                    ).Sum
                )
                $operatingSystem = Get-CimInstance Win32_OperatingSystem
                $memory = Get-CimInstance `
                    Win32_PerfFormattedData_PerfOS_Memory
                $availableMemory = (
                    [int64]$operatingSystem.FreePhysicalMemory * 1024
                )
                $commitPercent = (
                    [double]$memory.PercentCommittedBytesInUse
                )
                $elapsedSeconds = (
                    [DateTime]::UtcNow - $startedUtc
                ).TotalSeconds

                $sampleCount++
                $peakWorkingSet = [Math]::Max(
                    $peakWorkingSet,
                    $workingSet
                )
                $peakPrivateBytes = [Math]::Max(
                    $peakPrivateBytes,
                    $privateBytes
                )
                $minimumAvailable = [Math]::Min(
                    $minimumAvailable,
                    $availableMemory
                )
                $maximumCommit = [Math]::Max(
                    $maximumCommit,
                    $commitPercent
                )

                if ($availableMemory -lt $MinimumAvailableMemoryBytes) {
                    $lowMemorySamples++
                }
                else {
                    $lowMemorySamples = 0
                }
                if ($commitPercent -gt $MaximumCommitPercent) {
                    $highCommitSamples++
                }
                else {
                    $highCommitSamples = 0
                }

                [pscustomobject]@{
                    timestamp_utc = [DateTime]::UtcNow.ToString('o')
                    root_process_id = $process.Id
                    process_tree_ids = ($processTreeIds -join ';')
                    working_set_bytes = $workingSet
                    private_bytes = $privateBytes
                    available_memory_bytes = $availableMemory
                    commit_percent = $commitPercent
                    heartbeat_age_seconds = 0
                } |
                    Export-Csv `
                        -LiteralPath $resourceCsvPath `
                        -NoTypeInformation `
                        -Append:$csvExists
                $csvExists = $true

                if (
                    $lowMemorySamples -ge
                    $ConsecutiveSafetySamples
                ) {
                    $safetyStopTriggered = $true
                    $safetyStopReason = (
                        'Available memory remained below 1.5 GiB ' +
                        'for 10 seconds.'
                    )
                }
                elseif (
                    $highCommitSamples -ge
                    $ConsecutiveSafetySamples
                ) {
                    $safetyStopTriggered = $true
                    $safetyStopReason = (
                        'Windows commit usage remained above 90 ' +
                        'percent for 10 seconds.'
                    )
                }
                elseif (
                    $elapsedSeconds -ge $MaximumElapsedSeconds
                ) {
                    $safetyStopTriggered = $true
                    $safetyStopReason = (
                        'The native command reached the controlled elapsed-time boundary of ' +
                        "$MaximumElapsedSeconds seconds."
                    )
                }

                if ($safetyStopTriggered) {
                    Stop-Wb05ControlledProcessTree `
                        -ProcessIds $processTreeIds
                    break
                }
            }

            Start-Sleep -Seconds $SampleIntervalSeconds
        }

        $process.WaitForExit()
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        $exitCode = $process.ExitCode
    }
    finally {
        if ($processStarted -and -not $process.HasExited) {
            $remainingIds = @(
                Get-Wb05ControlledProcessTreeIds `
                    -RootProcessId $process.Id
            )
            Stop-Wb05ControlledProcessTree -ProcessIds $remainingIds
            $process.WaitForExit()
        }
        $endedUtc = [DateTime]::UtcNow
        $process.Dispose()
    }

    if ($minimumAvailable -eq [int64]::MaxValue) {
        $minimumAvailable = 0
    }

    Write-Wb05ControlledUtf8Text -Path $stdoutPath -Text $stdout
    Write-Wb05ControlledUtf8Text -Path $stderrPath -Text $stderr

    $resourceSummary = [ordered]@{
        schema_version = '1.0'
        campaign_id = 'GTQ-WB05-MF-v1'
        record_type = 'build-resource-summary'
        route_id = $RouteId
        component = $Component
        command_id = $CommandId
        sample_interval_seconds = $SampleIntervalSeconds
        sample_count = $sampleCount
        peak_working_set_bytes = $peakWorkingSet
        peak_private_bytes = $peakPrivateBytes
        minimum_available_memory_bytes = $minimumAvailable
        maximum_commit_percent = $maximumCommit
        heartbeat_timeout_seconds = 900
        safety_stop_triggered = $safetyStopTriggered
        safety_stop_reason = $safetyStopReason
    }
    Write-Wb05ControlledJsonEvidence `
        -Path $resourceSummaryPath `
        -Value $resourceSummary `
        -Atomic:$AtomicJsonEvidence

    $environment = [ordered]@{}
    foreach ($name in $EnvironmentAllowlist.Keys) {
        $environment[$name] = [string]$EnvironmentAllowlist[$name]
    }

    $record = [ordered]@{
        schema_version = '1.0'
        campaign_id = 'GTQ-WB05-MF-v1'
        record_type = 'build-command'
        command_id = $CommandId
        route_id = $RouteId
        component = $Component
        executable = $FilePath
        arguments = @($ArgumentList)
        working_directory = $WorkingDirectory
        environment_allowlist = $environment
        started_utc = $startedUtc.ToString('o')
        ended_utc = $endedUtc.ToString('o')
        elapsed_seconds = [Math]::Round(
            ($endedUtc - $startedUtc).TotalSeconds,
            3
        )
        exit_code = $exitCode
        stdout_path = Get-Wb05ControlledRelativePath `
            -Root $EvidenceRoot `
            -Path $stdoutPath
        stderr_path = Get-Wb05ControlledRelativePath `
            -Root $EvidenceRoot `
            -Path $stderrPath
    }
    Write-Wb05ControlledJsonEvidence `
        -Path $recordPath `
        -Value $record `
        -Atomic:$AtomicJsonEvidence

    return [pscustomobject]@{
        record = [pscustomobject]$record
        record_path = $recordPath
        stdout_path = $stdoutPath
        stderr_path = $stderrPath
        resource_summary = [pscustomobject]$resourceSummary
        resource_csv_path = $resourceCsvPath
        resource_summary_path = $resourceSummaryPath
    }
}

Export-ModuleMember -Function @(
    'Invoke-Wb05ControlledLoggedProcess'
)
