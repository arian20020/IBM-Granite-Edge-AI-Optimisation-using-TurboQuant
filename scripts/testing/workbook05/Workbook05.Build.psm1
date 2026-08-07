# Controlled Windows build primitives for Workbook 05.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Wb05Utf8NoBomText {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Text
    )

    # Create the parent directory only when the caller has not created it yet.
    $parent = Split-Path -Parent $Path
    if ($parent -and -not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    # Windows PowerShell 5.1 adds a BOM for its built-in UTF-8 encoding. Use a
    # .NET UTF-8 encoder instead so all evidence is deterministic and BOM-free.
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [IO.File]::WriteAllText($Path, $Text, $encoding)
}

function Write-Wb05Json {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [AllowNull()]
        [object]$Value
    )

    # A fixed deep JSON depth prevents nested command/evidence data from being
    # silently shortened by ConvertTo-Json's small default depth.
    $json = $Value | ConvertTo-Json -Depth 32
    Write-Wb05Utf8NoBomText -Path $Path -Text ($json + "`n")
}

function Assert-Wb05SafePath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter(Mandatory)]
        [string]$Path,

        [switch]$AllowRoot
    )

    # Canonicalise both paths before comparison so sibling prefixes such as
    # C:\w5a-other cannot be mistaken for children of the approved C:\w5a root.
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $fullPath = [IO.Path]::GetFullPath($Path)

    if (
        $AllowRoot -and
        $fullPath.Equals($fullRoot, [StringComparison]::OrdinalIgnoreCase)
    ) {
        return $fullPath
    }

    $rootPrefix = $fullRoot + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside its approved root: $fullPath"
    }

    return $fullPath
}

function Get-Wb05RelativePath {
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter(Mandatory)]
        [string]$Path
    )

    # Containment is proved before any relative path is written into evidence.
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $fullPath = Assert-Wb05SafePath -Root $fullRoot -Path $Path
    $prefixLength = ($fullRoot + [IO.Path]::DirectorySeparatorChar).Length
    return $fullPath.Substring($prefixLength).Replace('\', '/')
}

function Write-Wb05Manifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$EvidenceDirectory
    )

    # Hash every evidence file except the manifest itself. Sorting produces a
    # deterministic manifest for the independent hosted validator.
    $root = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
    $manifestPath = Join-Path $root 'manifest.sha256'
    $entries = Get-ChildItem -LiteralPath $root -File -Recurse |
        Where-Object { $_.FullName -ne $manifestPath } |
        ForEach-Object {
            $relative = Get-Wb05RelativePath -Root $root -Path $_.FullName
            $digest = (
                Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
            ).Hash.ToLowerInvariant()
            "$digest  $relative"
        } |
        Sort-Object -Unique

    Write-Wb05Utf8NoBomText `
        -Path $manifestPath `
        -Text (($entries -join "`n") + "`n")

    return $manifestPath
}

function New-Wb05ExternalWorkspace {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('C:\w5a', 'C:\w5b')]
        [string]$Root,

        [Parameter(Mandatory)]
        [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
        [string]$RunIdentity
    )

    # The short external root must be a normal directory, never a link, mount
    # point, or file that can redirect build writes outside the reviewed root.
    if (Test-Path -LiteralPath $Root) {
        $rootItem = Get-Item -LiteralPath $Root -Force
        if (-not $rootItem.PSIsContainer) {
            throw "External workspace root is not a directory: $Root"
        }
        if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "External workspace root must not be a reparse point: $Root"
        }
    }
    else {
        New-Item -ItemType Directory -Path $Root -Force:$false | Out-Null
    }

    # One workflow run/attempt receives one new directory. Existing content is
    # treated as possible contamination and is never silently reused or erased.
    $resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
    $workDirectory = Join-Path $resolvedRoot $RunIdentity
    if (Test-Path -LiteralPath $workDirectory) {
        throw "External run directory already exists and will not be reused: $workDirectory"
    }

    New-Item -ItemType Directory -Path $workDirectory -Force:$false | Out-Null
    $workItem = Get-Item -LiteralPath $workDirectory -Force
    if (($workItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "External run directory must not be a reparse point: $workDirectory"
    }

    return [pscustomobject]@{
        root = $resolvedRoot
        run_identity = $RunIdentity
        work_directory = $workItem.FullName
    }
}

function ConvertTo-Wb05WindowsCommandLineArgument {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Argument
    )

    # ProcessStartInfo on Windows PowerShell 5.1 accepts a single command-line
    # string. Quote each reviewed argument with the Windows C-runtime rules so
    # whitespace, embedded quotes, and trailing backslashes keep their boundary.
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

    # Double trailing backslashes so none can escape the closing quote.
    if ($backslashCount -gt 0) {
        [void]$builder.Append([char]92, ($backslashCount * 2))
    }
    [void]$builder.Append([char]34)
    return $builder.ToString()
}

function Start-Wb05ResourceSampler {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [int]$RootProcessId,

        [Parameter(Mandatory)]
        [ValidateSet('route-a-merged-openvino', 'route-b-experimental-qjl-polar')]
        [string]$RouteId,

        [Parameter(Mandatory)]
        [ValidateSet('runtime', 'genai')]
        [string]$Component,

        [Parameter(Mandatory)]
        [string]$CommandId,

        [Parameter(Mandatory)]
        [string]$CsvPath,

        [Parameter(Mandatory)]
        [string]$SummaryPath,

        [int]$SampleIntervalSeconds = 2,
        [int64]$MinimumAvailableMemoryBytes = 1610612736,
        [double]$MaximumCommitPercent = 90,
        [int]$ConsecutiveSafetySamples = 5,
        [int]$HeartbeatTimeoutSeconds = 900,
        [string]$HeartbeatPath
    )

    # Prepare evidence paths before the sampler moves into an isolated job.
    foreach ($path in @($CsvPath, $SummaryPath)) {
        $parent = Split-Path -Parent $path
        if ($parent -and -not (Test-Path -LiteralPath $parent -PathType Container)) {
            New-Item -ItemType Directory -Path $parent -Force | Out-Null
        }
    }

    # The stop file is process-local coordination data and is removed on close.
    $stopPath = $SummaryPath + '.stop'
    if (Test-Path -LiteralPath $stopPath) {
        Remove-Item -LiteralPath $stopPath -Force
    }

    # Sample the complete descendant process tree because CMake/MSBuild spawn
    # compilers and linkers whose memory must count toward the safety boundary.
    $job = Start-Job `
        -ArgumentList @(
            $RootProcessId,
            $RouteId,
            $Component,
            $CommandId,
            $CsvPath,
            $SummaryPath,
            $stopPath,
            $SampleIntervalSeconds,
            $MinimumAvailableMemoryBytes,
            $MaximumCommitPercent,
            $ConsecutiveSafetySamples,
            $HeartbeatTimeoutSeconds,
            $HeartbeatPath
        ) `
        -ScriptBlock {
            param(
                $RootProcessId,
                $RouteId,
                $Component,
                $CommandId,
                $CsvPath,
                $SummaryPath,
                $StopPath,
                $SampleIntervalSeconds,
                $MinimumAvailableMemoryBytes,
                $MaximumCommitPercent,
                $ConsecutiveSafetySamples,
                $HeartbeatTimeoutSeconds,
                $HeartbeatPath
            )

            Set-StrictMode -Version Latest
            $ErrorActionPreference = 'Stop'

            function Get-ProcessTreeIds {
                param([int]$RootId)

                # Use one CIM snapshot per sample so the process tree is
                # internally consistent while child IDs are discovered.
                $rows = @(
                    Get-CimInstance Win32_Process |
                        Select-Object ProcessId, ParentProcessId
                )
                $ids = New-Object System.Collections.Generic.List[int]
                $ids.Add($RootId)
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

            $sampleCount = 0
            $peakWorkingSet = [int64]0
            $peakPrivateBytes = [int64]0
            $minimumAvailable = [int64]::MaxValue
            $maximumCommit = [double]0
            $lowMemorySamples = 0
            $highCommitSamples = 0
            $safetyStopTriggered = $false
            $safetyStopReason = $null
            $csvExists = Test-Path -LiteralPath $CsvPath

            while (-not (Test-Path -LiteralPath $StopPath)) {
                # End normally after the root process exits.
                if (-not (Get-Process -Id $RootProcessId -ErrorAction SilentlyContinue)) {
                    break
                }

                $process_tree_ids = @(Get-ProcessTreeIds -RootId $RootProcessId)
                $processes = @(
                    foreach ($processId in $process_tree_ids) {
                        Get-Process -Id $processId -ErrorAction SilentlyContinue
                    }
                )

                # A native process can exit after the first root-process check but
                # before this second process-tree snapshot resolves. That is a
                # normal end-of-sampling boundary, not a zero-memory measurement.
                if ($processes.Count -eq 0) {
                    break
                }

                $workingSet = [int64](
                    ($processes | Measure-Object -Property WorkingSet64 -Sum).Sum
                )
                $privateBytes = [int64](
                    ($processes | Measure-Object -Property PrivateMemorySize64 -Sum).Sum
                )

                $operatingSystem = Get-CimInstance Win32_OperatingSystem
                $memory = Get-CimInstance Win32_PerfFormattedData_PerfOS_Memory
                $availableMemory = [int64]$operatingSystem.FreePhysicalMemory * 1024
                $commitPercent = [double]$memory.PercentCommittedBytesInUse

                $sampleCount++
                $peakWorkingSet = [Math]::Max($peakWorkingSet, $workingSet)
                $peakPrivateBytes = [Math]::Max($peakPrivateBytes, $privateBytes)
                $minimumAvailable = [Math]::Min($minimumAvailable, $availableMemory)
                $maximumCommit = [Math]::Max($maximumCommit, $commitPercent)

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

                # The optional heartbeat is checked only when the caller supplies
                # a path. The elapsed age is still recorded in every CSV sample.
                $heartbeatAgeSeconds = 0
                if ($HeartbeatPath -and (Test-Path -LiteralPath $HeartbeatPath -PathType Leaf)) {
                    $heartbeatAgeSeconds = (
                        (Get-Date) - (Get-Item -LiteralPath $HeartbeatPath).LastWriteTime
                    ).TotalSeconds
                }

                [pscustomobject]@{
                    timestamp_utc = [DateTime]::UtcNow.ToString('o')
                    root_process_id = $RootProcessId
                    process_tree_ids = ($process_tree_ids -join ';')
                    working_set_bytes = $workingSet
                    private_bytes = $privateBytes
                    available_memory_bytes = $availableMemory
                    commit_percent = $commitPercent
                    heartbeat_age_seconds = $heartbeatAgeSeconds
                } |
                    Export-Csv -LiteralPath $CsvPath -NoTypeInformation -Append:$csvExists
                $csvExists = $true

                if ($lowMemorySamples -ge $ConsecutiveSafetySamples) {
                    $safetyStopTriggered = $true
                    $safetyStopReason = 'Available memory remained below 1.5 GiB for 10 seconds.'
                }
                elseif ($highCommitSamples -ge $ConsecutiveSafetySamples) {
                    $safetyStopTriggered = $true
                    $safetyStopReason = 'Windows commit usage remained above 90 percent for 10 seconds.'
                }
                elseif ($HeartbeatPath -and $heartbeatAgeSeconds -gt $HeartbeatTimeoutSeconds) {
                    $safetyStopTriggered = $true
                    $safetyStopReason = 'The native command heartbeat exceeded 900 seconds.'
                }

                if ($safetyStopTriggered) {
                    # Terminate descendants first and then the root so an MSBuild
                    # compiler/linker child is not left running after a safety stop.
                    foreach ($processId in @($process_tree_ids | Sort-Object -Descending)) {
                        Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
                    }
                    break
                }

                Start-Sleep -Seconds $SampleIntervalSeconds
            }

            if ($minimumAvailable -eq [int64]::MaxValue) {
                $minimumAvailable = 0
            }

            # Write a complete build-resource-summary record so the independent
            # hosted validator must schema-check every monitored native command.
            $summary = [ordered]@{
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
                heartbeat_timeout_seconds = $HeartbeatTimeoutSeconds
                safety_stop_triggered = $safetyStopTriggered
                safety_stop_reason = $safetyStopReason
            }

            $encoding = New-Object System.Text.UTF8Encoding($false)
            $json = $summary | ConvertTo-Json -Depth 8
            [IO.File]::WriteAllText($SummaryPath, ($json + "`n"), $encoding)
        }

    return [pscustomobject]@{
        job = $job
        stop_path = $stopPath
        csv_path = $CsvPath
        summary_path = $SummaryPath
    }
}

function Stop-Wb05ResourceSampler {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object]$Sampler,

        [int]$WaitSeconds = 90
    )

    # Ask the sampler to close, then surface job errors rather than discarding a
    # failed measurement process and pretending the evidence is complete.
    Write-Wb05Utf8NoBomText -Path $Sampler.stop_path -Text "stop`n"

    if (-not (Wait-Job -Job $Sampler.job -Timeout $WaitSeconds)) {
        Stop-Job -Job $Sampler.job -ErrorAction SilentlyContinue
    }

    Receive-Job -Job $Sampler.job -ErrorAction Stop | Out-Null
    Remove-Job -Job $Sampler.job -Force
    Remove-Item -LiteralPath $Sampler.stop_path -Force -ErrorAction SilentlyContinue

    if (-not (Test-Path -LiteralPath $Sampler.summary_path -PathType Leaf)) {
        throw "Resource sampler did not produce its summary: $($Sampler.summary_path)"
    }

    return (
        Get-Content -LiteralPath $Sampler.summary_path -Raw |
            ConvertFrom-Json
    )
}

function Invoke-Wb05LoggedProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$CommandId,

        [Parameter(Mandatory)]
        [ValidateSet('route-a-merged-openvino', 'route-b-experimental-qjl-polar')]
        [string]$RouteId,

        [Parameter(Mandatory)]
        [ValidateSet('runtime', 'genai')]
        [string]$Component,

        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$ArgumentList,

        [Parameter(Mandatory)]
        [string]$WorkingDirectory,

        [Parameter(Mandatory)]
        [string]$EvidenceDirectory,

        [Parameter(Mandatory)]
        [string]$EvidenceRoot,

        [hashtable]$EnvironmentAllowlist = @{},
        [switch]$MonitorResources
    )

    # Keep every command boundary in a dedicated text-only evidence set.
    if (-not (Test-Path -LiteralPath $EvidenceDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
    }

    $safeId = $CommandId -replace '[^A-Za-z0-9._-]', '-'
    $stdoutPath = Join-Path $EvidenceDirectory "$safeId.stdout.log"
    $stderrPath = Join-Path $EvidenceDirectory "$safeId.stderr.log"
    $recordPath = Join-Path $EvidenceDirectory "$safeId.command.json"

    # Build one native process without asking PowerShell to interpret a command
    # string as executable code. Every argument remains an explicit array item.
    $processStartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $processStartInfo.FileName = $FilePath
    $processStartInfo.Arguments = @(
        $ArgumentList |
            ForEach-Object { ConvertTo-Wb05WindowsCommandLineArgument -Argument $_ }
    ) -join ' '
    $processStartInfo.WorkingDirectory = $WorkingDirectory
    $processStartInfo.UseShellExecute = $false
    $processStartInfo.RedirectStandardOutput = $true
    $processStartInfo.RedirectStandardError = $true
    $processStartInfo.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $processStartInfo
    $sampler = $null
    $resourceSummary = $null
    $startedUtc = [DateTime]::UtcNow
    $exitCode = -1
    $stdout = ''
    $stderr = ''

    try {
        if (-not $process.Start()) {
            throw "Native command did not start: $CommandId"
        }

        if ($MonitorResources) {
            $sampler = Start-Wb05ResourceSampler `
                -RootProcessId $process.Id `
                -RouteId $RouteId `
                -Component $Component `
                -CommandId $CommandId `
                -CsvPath (Join-Path $EvidenceDirectory "$safeId.resources.csv") `
                -SummaryPath (Join-Path $EvidenceDirectory "$safeId.resources.json")
        }

        # Drain stdout and stderr concurrently; reading either stream to the end
        # synchronously first can deadlock when the child fills the other pipe.
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        $exitCode = $process.ExitCode
    }
    finally {
        if ($sampler) {
            $resourceSummary = Stop-Wb05ResourceSampler -Sampler $sampler
        }
        $process.Dispose()
    }

    Write-Wb05Utf8NoBomText -Path $stdoutPath -Text $stdout
    Write-Wb05Utf8NoBomText -Path $stderrPath -Text $stderr
    $endedUtc = [DateTime]::UtcNow

    # Record only the explicitly allowed environment fields; never serialize the
    # complete process environment where credentials could be present.
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
        elapsed_seconds = [Math]::Round(($endedUtc - $startedUtc).TotalSeconds, 3)
        exit_code = $exitCode
        stdout_path = Get-Wb05RelativePath -Root $EvidenceRoot -Path $stdoutPath
        stderr_path = Get-Wb05RelativePath -Root $EvidenceRoot -Path $stderrPath
    }
    Write-Wb05Json -Path $recordPath -Value $record

    return [pscustomobject]@{
        record = [pscustomobject]$record
        record_path = $recordPath
        stdout_path = $stdoutPath
        stderr_path = $stderrPath
        resource_summary = $resourceSummary
    }
}

function Get-Wb05BinaryRecords {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Root,

        [Parameter(Mandatory)]
        [ValidateSet('route-a-merged-openvino', 'route-b-experimental-qjl-polar')]
        [string]$RouteId,

        [Parameter(Mandatory)]
        [ValidateSet('runtime', 'genai')]
        [string]$Component,

        [Parameter(Mandatory)]
        [string]$ProducerCommandId,

        [string[]]$AllowedExtensions = @('.exe', '.dll', '.lib', '.pdb', '.pyd')
    )

    # Hash installed binaries in place. The evidence contains identity metadata,
    # never the executable payload itself.
    $resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
    $extensions = @($AllowedExtensions | ForEach-Object { $_.ToLowerInvariant() })

    return @(
        Get-ChildItem -LiteralPath $resolvedRoot -File -Recurse |
            Where-Object { $extensions -contains $_.Extension.ToLowerInvariant() } |
            Sort-Object FullName |
            ForEach-Object {
                [ordered]@{
                    schema_version = '1.0'
                    campaign_id = 'GTQ-WB05-MF-v1'
                    record_type = 'build-binary'
                    route_id = $RouteId
                    component = $Component
                    relative_path = Get-Wb05RelativePath -Root $resolvedRoot -Path $_.FullName
                    size_bytes = [int64]$_.Length
                    sha256 = (
                        Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
                    ).Hash.ToLowerInvariant()
                    producer_command_id = $ProducerCommandId
                    configuration = 'Release'
                    copied_to_artifact = $false
                }
            }
    )
}

function Restore-Wb05Environment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [hashtable]$Snapshot
    )

    # Restore previous process-scoped environment values and remove variables
    # that did not exist before the build stage.
    foreach ($name in $Snapshot.Keys) {
        $value = $Snapshot[$name]
        if ($null -eq $value) {
            Remove-Item -Path "Env:$name" -ErrorAction SilentlyContinue
        }
        else {
            Set-Item -Path "Env:$name" -Value ([string]$value)
        }
    }
}

# Export only the nine reviewed public primitives. All quoting and relative-path
# helpers remain private implementation details of the module.
Export-ModuleMember -Function @(
    'New-Wb05ExternalWorkspace',
    'Invoke-Wb05LoggedProcess',
    'Start-Wb05ResourceSampler',
    'Stop-Wb05ResourceSampler',
    'Write-Wb05Json',
    'Write-Wb05Manifest',
    'Assert-Wb05SafePath',
    'Get-Wb05BinaryRecords',
    'Restore-Wb05Environment'
)
