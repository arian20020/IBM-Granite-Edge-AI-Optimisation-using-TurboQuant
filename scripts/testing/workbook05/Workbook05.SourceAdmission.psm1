Set-StrictMode -Version Latest

function ConvertTo-Workbook05ProcessArgument {
    <#
    .SYNOPSIS
    Quotes one argument for ProcessStartInfo on Windows PowerShell 5.1.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Argument
    )

    if ($Argument.Length -eq 0) {
        return '""'
    }
    if ($Argument -notmatch '[\s"]') {
        return $Argument
    }

    # Follow the Windows command-line quoting rules used by CommandLineToArgvW.
    $builder = [System.Text.StringBuilder]::new()
    [void]$builder.Append('"')
    $backslashCount = 0
    foreach ($character in $Argument.ToCharArray()) {
        if ($character -eq '\') {
            $backslashCount++
            continue
        }
        if ($character -eq '"') {
            [void]$builder.Append(('\' * (($backslashCount * 2) + 1)))
            [void]$builder.Append('"')
            $backslashCount = 0
            continue
        }
        if ($backslashCount -gt 0) {
            [void]$builder.Append(('\' * $backslashCount))
            $backslashCount = 0
        }
        [void]$builder.Append($character)
    }
    if ($backslashCount -gt 0) {
        [void]$builder.Append(('\' * ($backslashCount * 2)))
    }
    [void]$builder.Append('"')
    return $builder.ToString()
}

function Test-Workbook05ExternalWorkspace {
    <#
    .SYNOPSIS
    Verifies the controlled external workspace without deleting existing data.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$WorkspaceRoot,

        [string]$AllowedRoot = 'C:\wb05',

        [switch]$CreateIfMissing
    )

    $requestedPath = [IO.Path]::GetFullPath($WorkspaceRoot).TrimEnd('\')
    $allowedPath = [IO.Path]::GetFullPath($AllowedRoot).TrimEnd('\')
    if (-not [string]::Equals(
        $requestedPath,
        $allowedPath,
        [StringComparison]::OrdinalIgnoreCase
    )) {
        throw "The external workspace must be exactly '$allowedPath'; found '$requestedPath'."
    }

    $created = $false
    if (Test-Path -LiteralPath $requestedPath) {
        $item = Get-Item -LiteralPath $requestedPath -Force
        if (-not $item.PSIsContainer) {
            throw "The external workspace exists but is not a directory: $requestedPath"
        }
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "The external workspace must not be a reparse point: $requestedPath"
        }
    }
    elseif ($CreateIfMissing) {
        New-Item -ItemType Directory -Path $requestedPath -Force | Out-Null
        $created = $true
    }
    else {
        throw "The external workspace does not exist: $requestedPath"
    }

    [pscustomobject]@{
        Permitted = $true
        CanonicalPath = $requestedPath
        Created = $created
    }
}

function Stop-Workbook05ProcessTree {
    <#
    .SYNOPSIS
    Terminates a timed-out native process and its descendants.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process
    )

    if ($Process.HasExited) {
        return
    }

    $taskKill = Join-Path $env:SystemRoot 'System32\taskkill.exe'
    & $taskKill '/PID' ([string]$Process.Id) '/T' '/F' 2>$null | Out-Null
    if (-not $Process.WaitForExit(10000)) {
        $Process.Kill()
        [void]$Process.WaitForExit(5000)
    }
}

function Invoke-Workbook05RecordedCommand {
    <#
    .SYNOPSIS
    Runs one native executable without a command shell and records its evidence.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [string]$EvidenceDirectory,

        [Parameter(Mandatory = $true)]
        [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$')]
        [string]$CommandId,

        [Parameter(Mandatory = $true)]
        [ValidateRange(1, 86400)]
        [int]$TimeoutSeconds
    )

    if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) {
        throw "Native executable was not found: $FilePath"
    }
    if (-not (Test-Path -LiteralPath $WorkingDirectory -PathType Container)) {
        throw "Native working directory was not found: $WorkingDirectory"
    }

    New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
    $stdoutPath = Join-Path $EvidenceDirectory "$CommandId.stdout.txt"
    $stderrPath = Join-Path $EvidenceDirectory "$CommandId.stderr.txt"
    $recordPath = Join-Path $EvidenceDirectory "$CommandId.command.json"

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = [IO.Path]::GetFullPath($FilePath)
    $startInfo.WorkingDirectory = [IO.Path]::GetFullPath($WorkingDirectory)
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Arguments = (($ArgumentList | ForEach-Object {
        ConvertTo-Workbook05ProcessArgument -Argument $_
    }) -join ' ')

    $startedUtc = [DateTime]::UtcNow
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "Native command could not be started: $FilePath"
    }

    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
    if ($timedOut) {
        Stop-Workbook05ProcessTree -Process $process
    }
    else {
        # WaitForExit without a timeout flushes asynchronous stream events.
        $process.WaitForExit()
    }

    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    $endedUtc = [DateTime]::UtcNow
    $exitCode = if ($timedOut) { -1 } else { $process.ExitCode }

    $utf8 = [System.Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText($stdoutPath, $stdout, $utf8)
    [IO.File]::WriteAllText($stderrPath, $stderr, $utf8)

    $record = [ordered]@{
        command_id = $CommandId
        file_path = $startInfo.FileName
        argv = @($ArgumentList)
        working_directory = $startInfo.WorkingDirectory
        started_utc = $startedUtc.ToString('o')
        ended_utc = $endedUtc.ToString('o')
        timeout_seconds = $TimeoutSeconds
        timed_out = $timedOut
        exit_code = $exitCode
        stdout_path = [IO.Path]::GetFileName($stdoutPath)
        stderr_path = [IO.Path]::GetFileName($stderrPath)
    }
    [IO.File]::WriteAllText(
        $recordPath,
        (($record | ConvertTo-Json -Depth 8) + [Environment]::NewLine),
        $utf8
    )

    [pscustomobject]@{
        Record = [pscustomobject]$record
        RecordPath = $recordPath
        StandardOutputPath = $stdoutPath
        StandardErrorPath = $stderrPath
    }
}

function Test-Workbook05RelativeBundlePath {
    <#
    .SYNOPSIS
    Rejects absolute, traversal, empty-segment, and backslash evidence paths.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }
    if ([IO.Path]::IsPathRooted($Path) -or $Path.Contains('\')) {
        return $false
    }

    $segments = $Path.Split('/')
    return @($segments | Where-Object { $_ -in @('', '.', '..') }).Count -eq 0
}

function Invoke-Workbook05SourceAdmissionPipeline {
    <#
    .SYNOPSIS
    Executes ordered source-admission steps and separates blockers from failures.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory,

        [Parameter(Mandatory = $true)]
        [string[]]$StepOrder,

        [Parameter(Mandatory = $true)]
        [scriptblock]$StepExecutor,

        [Parameter(Mandatory = $true)]
        [string[]]$RequiredBundleFiles
    )

    # Refuse contaminated evidence roots instead of deleting or silently reusing them.
    if (Test-Path -LiteralPath $OutputDirectory) {
        if (-not (Test-Path -LiteralPath $OutputDirectory -PathType Container)) {
            throw "The evidence output path is not a directory: $OutputDirectory"
        }
        if (@(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count -ne 0) {
            throw "The evidence output directory must be empty: $OutputDirectory"
        }
    }
    else {
        New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    }

    # Validate the declared pipeline before the first external operation begins.
    if ($StepOrder.Count -eq 0) {
        throw 'The source-admission pipeline must contain at least one step.'
    }
    if (($StepOrder | Select-Object -Unique).Count -ne $StepOrder.Count) {
        throw 'The source-admission pipeline contains a duplicate step identifier.'
    }

    $executedSteps = New-Object System.Collections.Generic.List[string]
    $stepResults = New-Object System.Collections.Generic.List[object]
    $routeAStatus = $null
    $routeBStatus = $null
    $checkpointStatus = $null
    $currentStep = ''

    try {
        # Execute each reviewed stage exactly once and preserve its reported outcome.
        foreach ($stepId in $StepOrder) {
            $currentStep = $stepId
            $result = & $StepExecutor $stepId $OutputDirectory
            if ($null -eq $result) {
                throw "Step '$stepId' returned no result."
            }
            if ($result.StepId -ne $stepId) {
                throw "Step '$stepId' returned the mismatched identifier '$($result.StepId)'."
            }
            if ($result.Kind -notin @('Success', 'ScientificBlocker')) {
                throw "Step '$stepId' reported $($result.Kind): $($result.Reason)"
            }

            $executedSteps.Add($stepId)
            $stepResults.Add($result)

            # Only the decision step is allowed to set final route/checkpoint states.
            if ($stepId -eq 'route-decisions') {
                $routeAStatus = [string]$result.RouteAStatus
                $routeBStatus = [string]$result.RouteBStatus
                $checkpointStatus = [string]$result.CheckpointStatus
            }
        }

        # Verify every expected artifact using traversal-free relative paths.
        foreach ($relativePath in $RequiredBundleFiles) {
            if (-not (Test-Workbook05RelativeBundlePath -Path $relativePath)) {
                throw "Unsafe required bundle file path: $relativePath"
            }
            $candidate = Join-Path $OutputDirectory ($relativePath -replace '/', '\')
            if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
                throw "Missing required bundle file: $relativePath"
            }
        }

        # Write one reviewer-facing orchestration record after all required files exist.
        $reportPath = Join-Path $OutputDirectory 'orchestration-report.json'
        $report = [ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            phase_id = 'phase-1-source-admission'
            outcome = 'Completed'
            route_a_status = $routeAStatus
            route_b_status = $routeBStatus
            checkpoint_status = $checkpointStatus
            executed_steps = $executedSteps.ToArray()
            step_results = $stepResults.ToArray()
        }
        $report | ConvertTo-Json -Depth 30 |
            Set-Content -LiteralPath $reportPath -Encoding UTF8

        return [pscustomobject]@{
            Outcome = 'Completed'
            RouteAStatus = $routeAStatus
            RouteBStatus = $routeBStatus
            CheckpointStatus = $checkpointStatus
            ExecutedSteps = $executedSteps.ToArray()
            ReportPath = $reportPath
        }
    }
    catch {
        # Preserve an integrity/orchestration failure before returning a non-zero exit.
        $errorPath = Join-Path $OutputDirectory 'orchestration-error.json'
        [ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            phase_id = 'phase-1-source-admission'
            failed_step = $currentStep
            reason = $_.Exception.Message
            executed_steps = $executedSteps.ToArray()
        } | ConvertTo-Json -Depth 20 |
            Set-Content -LiteralPath $errorPath -Encoding UTF8
        throw
    }
}

Export-ModuleMember -Function @(
    'Test-Workbook05ExternalWorkspace',
    'Invoke-Workbook05RecordedCommand',
    'Invoke-Workbook05SourceAdmissionPipeline'
)
