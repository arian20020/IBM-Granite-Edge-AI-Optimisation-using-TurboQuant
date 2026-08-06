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

    # Create the parent folder only when the caller has not created it yet.
    $parent = Split-Path -Parent $Path
    if ($parent -and -not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    # Windows PowerShell 5.1 writes a BOM for its built-in UTF-8 encoding.
    # The .NET encoding object keeps evidence deterministic and BOM-free.
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

    # Use a stable depth so nested commands, decisions, and resource records
    # cannot be silently shortened by ConvertTo-Json's default depth.
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

    # Resolve both paths without following an unapproved sibling prefix such as
    # C:\w5a-other when C:\w5a is the intended containment root.
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

    # Derive a slash-normalised relative evidence path only after containment
    # has been proved by Assert-Wb05SafePath.
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

    # Hash every evidence file except the manifest itself, then sort the lines
    # so an independent validator receives a deterministic record.
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

    # The short root must be a normal directory, never a link, mount point, or
    # regular file that could redirect writes outside the controlled boundary.
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

    # A run/attempt receives one new directory. Existing content is evidence of
    # possible contamination, so the function fails instead of cleaning it.
    $resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
    $workDirectory = Join-Path $resolvedRoot $RunIdentity
    if (Test-Path -LiteralPath $workDirectory) { throw "External run directory already exists and will not be reused: $workDirectory" }

    New-Item `
        -ItemType Directory `
        -Path $workDirectory `
        -Force:$false | Out-Null

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

    # ProcessStartInfo on Windows PowerShell 5.1 accepts one command-line
    # string. Quote each reviewed argument with the Windows C-runtime rules so
    # spaces, quotes, and trailing backslashes cannot change its boundary.
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

    # Prepare text evidence paths before the sampler moves into a background
    # PowerShell job with its own process and variable scope.
    foreach ($path in @($CsvPath, $SummaryPath)) {
        $parent = Split-Path -Parent $path
        if ($parent -and -not (Test-Path -LiteralPath $parent -PathType Container)) {
            New-Item -ItemType Directory -Path $parent -Force | Out-Null
        }
    }

    $stopPath = $SummaryPath + '.stop'
    if (Test-Path -LiteralPath $stopPath) {
        Remove-Item -LiteralPath $stopPath -Force
    }

    # The job samples the full descendant process tree, not merely cmake.exe,
    # because Visual Studio builds spawn compiler and linker child processes.
    $job = Start-Job `
        -ArgumentList @(
            $RootProcessId,
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

                # Build the descendant set from one CIM snapshot so all metrics
                # in a sample refer to the same process topology.
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
                            Where-Object {
                                [int]$_.ParentProcessId -eq $parentId
                            }
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
                if (
                    -not (
                        Get-Process `
                            -Id $RootProcessId `
                            -ErrorAction SilentlyContinue
                    )
                ) {
                    break
                }

                $process_tree_ids = @(Get-ProcessTreeIds -RootId $RootProcessId)
                $processes = @(
                    foreach ($processId in $process_tree_ids) {
                        Get-Process `
                            -Id $processId `
                            -ErrorAction SilentlyContinue
                    }
                )

                $workingSet = [int64](
                    (
                        $processes |
                            Measure-Object `
                                -Property WorkingSet64 `
                                -Sum
                    ).Sum
                )
                $privateBytes = [int64](
                    (
                        $processes |
                            Measure-Object `
                                -Property PrivateMemorySize64 `
                              -Sum
                    ).Sum
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

                $heartbeatAgeSeconds = 0
                if (
                    $HeartbeatPath -and
                    (Test-Path -LiteralPath $HeartbeatPath -PathType Leaf)
                ) {
                    $heartbeatAgeSeconds = (
                        (Get-Date) -
                        (Get-Item -LiteralPath $HeartbeatPath).LastWriteTime
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
                    Export-Csv `
                        -LiteralPath $CsvPath `
                        -NoTypeInformation `
                        -Append:$csvExists
                $csvExists = $true

                if ($lowMemorySamples -ge $ConsecutiveSafetySamples) {
                    $safetyStopTriggered = $true
                    $safetyStopReason = (
                        'Available memory remained below 1.5 GiB for ' +
                        '10 seconds.'
                    )
                }
                elseif ($highCommitSamples -ge $ConsecutiveSafetySamples) {
                    $safetyStopTriggered = $true
                    $safetyStopReason = (
                        'Windows commit usage remained above 90 percent for ' +
                        '10 seconds.'
                    )
                }
                elseif (
                    $HeartbeatPath -and
                    $heartbeatAgeSeconds -gt $HeartbeatTimeoutSeconds
                ) {
                    $safetyStopTriggered = $true
                    $safetyStopReason = (
                        'The native command heartbeat exceeded 900 seconds.'
                    )
                }

                if ($safetyStopTriggered) {
                    # Kill descendants first, then the root, to avoid leaving a
                    # compiler or linker orphan after the safety boundary fires.
                    foreach (
                        $processId in @(
                            $process_tree_ids |
                                Sort-Object -Descending
                        )
                    ) {
                        Stop-Process `
                            -Id $processId `
                            -Force `
                            -ErrorAction SilentlyContinue
                    }
                    break
                }

                Start-Sleep -Seconds $SampleIntervalSeconds
            }

            if ($minimumAvailable -eq [int64]::MaxValue) {
                $minimumAvailable = 0
            }

            $summary = [ordered]@{
                schema_version = '1.0'
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
            [IO.File]::WriteAllText(
                $SummaryPath,
                ($json + "`n"),
                $encoding
            )
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

    # Ask the sampler to finish, then surface job errors rather than silently
    # discarding a failed measurement process.
    Write-Wb05Utf8NoBomText `
        -Path $Sampler.stop_path `
        -Text "stop`n"

    if (-not (Wait-Job -Job $Sampler.job -Timeout $WaitSeconds)) {
        Stop-Job -Job $Sampler.job -ErrorAction SilentlyContinue
    }

    Receive-Job -Job $Sampler.job -ErrorAction Stop | Out-Null
    Remove-Job -Job $Sampler.job -Force
    Remove-Item `
        -LiteralPath $Sampler.stop_path `
        -Force `
        -ErrorAction SilentlyContinue

    if (-not (Test-Path -LiteralPath $Sampler.summary_path -PathType Leaf)) {
        throw (
            'Resource sampler did not produce its summary: ' +
            $Sampler.summary_path
        )
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
        [ValidateSet(
            'route-a-merged-openvino',
            'route-b-experimental-qjl-polar'
        )]
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

        [hashtable]$EnvironmentAllowlist = @{},
        [switch]$MonitorResources
    )

    # Keep every command boundary in a dedicated set of text evidence files.
    if (-not (Test-Path -LiteralPath $EvidenceDirectory -PathType Container)) {
        New-Item `
            -ItemType Directory `
            -Path $EvidenceDirectory `
            -Force | Out-Null
    }

    $safeId = $CommandId -replace '[^A-Za-z0-9._-]', '-'
    $stdoutPath = Join-Path $EvidenceDirectory "$safeId.stdout.log"
    $stderrPath = Join-Path $EvidenceDirectory "$safeId.stderr.log"
    $recordPath = Join-Path $EvidenceDirectory "$safeId.command.json"

    # Build one ProcessStartInfo object without asking PowerShell to interpret
    # a command string as code.
    $processStartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $processStartInfo.FileName = $FilePath
    $processStartInfo.Arguments = @(
        $ArgumentList |
            ForEach-Object {
                ConvertTo-Wb05WindowsCommandLineArgument -Argument $_
            }
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
                -CsvPath (Join-Path $EvidenceDirectory "$safeId.resources.csv") `
                -SummaryPath (Join-Path $EvidenceDirectory "$safeId.resources.json")
        }

        # Drain both redirected streams concurrently. Reading only one stream
        # synchronously can deadlock when the child fills the other pipe.
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

    # Record only explicitly allowlisted environment values, never the entire
    # process environment where credentials might be present.
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
        stdout_path = Get-Wb05RelativePath `
            -Root $EvidenceDirectory `
            -Path $stdoutPath
        stderr_path = Get-Wb05RelativePath `
            -Root $EvidenceDirectory `
            -Path $stderrPath
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
        [ValidateSet(
            'route-a-merged-openvino',
            'route-b-experimental-qjl-polar'
        )]
        [string]$RouteId,

        [Parameter(Mandatory)]
        [ValidateSet('runtime', 'genai')]
        [string]$Component,

        [Parameter(Mandatory)]
        [string]$ProducerCommandId,

        [string[]]$AllowedExtensions = @(
            '.exe',
            '.dll',
            '.lib',
            '.pdb',
            '.pyd'
        )
    )

    # Hash outputs in place. The records prove identity while the executable
    # payloads remain outside Git and outside uploaded evidence.
    $resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
    $extensions = @(
        $AllowedExtensions |
            ForEach-Object { $_.ToLowerInvariant() }
    )

    return @(
        Get-ChildItem -LiteralPath $resolvedRoot -File -Recurse |
            Where-Object {
                $extensions -contains $_.Extension.ToLowerInvariant()
            } |
            Sort-Object FullName |
            ForEach-Object {
                [ordered]@{
                    schema_version = '1.0'
                    campaign_id = 'GTQ-WB05-MF-v1'
                    record_type = 'build-binary'
                    route_id = $RouteId
                    component = $Component
                    relative_path = Get-Wb05RelativePath `
                        -Root $resolvedRoot `
                        -Path $_.FullName
                    size_bytes = [int64]$_.Length
                    sha256 = (
                        Get-FileHash `
                            -LiteralPath $_.FullName `
                            -Algorithm SHA256
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

    # Restore previous values and remove variables that were absent before the
    # build stage, preventing one route from contaminating the next route.
    foreach ($name in $Snapshot.Keys) {
        $value = $Snapshot[8„çÖ™Ï∂ªßq´^