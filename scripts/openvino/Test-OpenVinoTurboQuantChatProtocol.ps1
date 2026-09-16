[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$StageDirectory
)

$ErrorActionPreference = 'Stop'
# This is a packaging compatibility check, not model or inference evidence.
# An invalid device deliberately stops the session before any model is opened.
try {
    $stage = [IO.Path]::GetFullPath($StageDirectory)
    $verification = & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'Test-OpenVinoTurboQuantWorkerManifest.ps1') -StageDirectory $stage
    if ($LASTEXITCODE -ne 0 -or [string]$verification -cne 'turboquant_worker_manifest_valid') {
        throw 'Worker closure verification failed.'
    }
    foreach ($validHistory in @($true, $false)) {
        $sessionId = [Guid]::NewGuid().ToString()
        $role = if ($validHistory) { 'user' } else { 'tool' }
        $command = @{
            commandType = 'startSession'; sessionId = $sessionId
            inspectionRunId = [Guid]::NewGuid().ToString()
            packagePath = (Join-Path $stage '__protocol_probe_not_a_model__')
            packageManifestDigest = ('a' * 64); modelSha256 = ('b' * 64); modelLengthBytes = 1
            device = @{ deviceId = '__protocol_probe__' }
            limits = @{ maximumContextTokens = 64; maximumNewTokens = 1 }
            runtime = @{ kvCachePrecision = 'tbq4' }
            initialHistory = @(@{ role = $role; content = 'hello' })
        }
        $start = New-Object Diagnostics.ProcessStartInfo
        $start.FileName = Join-Path $stage 'OpenVinoTurboQuant.Worker.exe'
        $start.Arguments = '--protocol openvino.turboquant/1'
        $start.WorkingDirectory = $stage
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardInput = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $process = New-Object Diagnostics.Process
        $process.StartInfo = $start
        $started = $false
        try {
            if (-not $process.Start()) { throw 'Worker did not start.' }
            $started = $true
            $stdout = $process.StandardOutput.ReadToEndAsync()
            $stderr = $process.StandardError.ReadToEndAsync()
            $process.StandardInput.WriteLine(($command | ConvertTo-Json -Depth 8 -Compress))
            $process.StandardInput.Close()
            if (-not $process.WaitForExit(15000)) { throw 'Protocol probe timed out.' }
            $output = $stdout.GetAwaiter().GetResult()
            $errorOutput = $stderr.GetAwaiter().GetResult()
            if ($output.Length -gt 65536 -or $errorOutput.Length -gt 4096) { throw 'Probe output exceeded its limit.' }
            $events = @($output -split '\r?\n' | Where-Object { $_ } | ForEach-Object { $_ | ConvertFrom-Json })
            if ($events.Count -lt 1 -or $events[0].eventType -cne 'hello' -or
                $events[0].protocolId -cne 'openvino.turboquant/1') { throw 'Handshake missing.' }
            $failures = @($events | Where-Object { $_.eventType -ceq 'sessionFailed' })
            if ($validHistory) {
                if ($events.Count -ne 2 -or $failures.Count -ne 1 -or
                    $failures[0].sessionId -cne $sessionId -or
                    $failures[0].supportCode -cne 'runtime_protocol_failed') {
                    throw 'Worker does not accept the current conversation-history protocol.'
                }
            } elseif ($events.Count -ne 1) {
                throw 'Worker accepted an invalid history role.'
            }
            if ($process.ExitCode -eq 0) { throw 'Invalid probe unexpectedly succeeded.' }
        }
        finally {
            if ($started -and -not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
            $process.Dispose()
        }
    }
    'turboquant_chat_protocol_valid'
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
