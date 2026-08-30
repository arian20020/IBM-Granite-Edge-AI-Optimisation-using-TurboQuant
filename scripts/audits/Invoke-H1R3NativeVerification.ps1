[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string] $OutputLedgerPath,

    [string] $LockPath = 'C:\UCL-AUDIT-NATIVE.lock',
    [string] $ProbeDirectory,
    [string] $ManifestPath,
    [ValidateRange(1, 30)]
    [int] $TimeoutSeconds = 30,
    [int] $VerifyCleanupProcessId = 0
)

$ErrorActionPreference = 'Stop'
$lockOwned = $false
$cleanupVerified = $false
$ownerPath = $null

function Stop-H1R3Native {
    param([string] $Code, [bool] $ReleaseLock = $true)
    if ($ReleaseLock -and $script:lockOwned -and $script:cleanupVerified) {
        if ($script:ownerPath -and (Test-Path -LiteralPath $script:ownerPath -PathType Leaf)) {
            Remove-Item -LiteralPath $script:ownerPath -Force
        }
        if (Test-Path -LiteralPath $LockPath -PathType Container) {
            Remove-Item -LiteralPath $LockPath -Force
        }
        $script:lockOwned = $false
    }
    Write-Output $Code
    exit 2
}

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$destination = [IO.Path]::GetFullPath($OutputLedgerPath)
if (Test-Path -LiteralPath $destination) {
    Stop-H1R3Native 'H1R3-DESTINATION-EXISTS'
}
if (Test-Path -LiteralPath $LockPath) {
    Stop-H1R3Native 'H1R3-NATIVE-LOCKED'
}

try {
    $null = New-Item -ItemType Directory -Path $LockPath -ErrorAction Stop
    $lockOwned = $true
}
catch {
    Stop-H1R3Native 'H1R3-NATIVE-LOCKED'
}

$ownerPath = Join-Path $LockPath 'owner.json'
$owner = [ordered]@{
    workerId = 'H1'
    campaign = 'R3'
    processId = $PID
    processStartUtc = (Get-Process -Id $PID).StartTime.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    worktreeToken = 'H1-R3'
    acquiredAtUtc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    intendedStage = 'native-hardware-probe'
}
[IO.File]::WriteAllText(
    $ownerPath,
    (($owner | ConvertTo-Json -Depth 4) + "`n"),
    [Text.UTF8Encoding]::new($false))

if ([string]::IsNullOrWhiteSpace($ProbeDirectory)) {
    $ProbeDirectory = Join-Path $root 'obj\hi-lcp\package\Debug\win-x64'
}
if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $root 'obj\hi-lcp\package\Debug\llamacpp-probe-manifest.json'
}
$probeRoot = [IO.Path]::GetFullPath($ProbeDirectory)
$manifest = [IO.Path]::GetFullPath($ManifestPath)
$probePath = Join-Path $probeRoot 'GraniteEdgeAI.HardwareInspection.LlamaCppProbe.exe'
$verifier = Join-Path $root 'scripts\hardware-inspection\Test-LlamaCppProbeManifest.ps1'

try {
    & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File $verifier -ProbeDirectory $probeRoot -ManifestPath $manifest *> $null
    if ($LASTEXITCODE -ne 0) { throw 'manifest' }
}
catch {
    $cleanupVerified = $true
    Stop-H1R3Native 'H1R3-MANIFEST'
}

if ($VerifyCleanupProcessId -gt 0) {
    $observed = Get-Process -Id $VerifyCleanupProcessId -ErrorAction SilentlyContinue
    if ($null -ne $observed -and -not $observed.HasExited) {
        $cleanupVerified = $false
        Stop-H1R3Native 'H1R3-NATIVE-CLEANUP' $false
    }
}

function Get-H1R3ProbeProcesses {
    $target = [IO.Path]::GetFullPath($probePath)
    return @(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object {
            -not [string]::IsNullOrWhiteSpace($_.ExecutablePath) -and
            [string]::Equals([IO.Path]::GetFullPath($_.ExecutablePath), $target, [StringComparison]::OrdinalIgnoreCase)
        })
}

try { $preexisting = @(Get-H1R3ProbeProcesses) }
catch {
    $cleanupVerified = $false
    Stop-H1R3Native 'H1R3-NATIVE-CLEANUP' $false
}
if ($preexisting.Count -ne 0) {
    $cleanupVerified = $false
    Stop-H1R3Native 'H1R3-NATIVE-PROCESS' $false
}

function Stop-H1R3OwnedTree {
    param([Diagnostics.Process] $Process)
    if ($Process.HasExited) { return }
    $killTree = $Process.GetType().GetMethod('Kill', [type[]]@([bool]))
    if ($null -ne $killTree) { $killTree.Invoke($Process, @($true)); return }
    $Process.Kill()
}

function Invoke-H1R3ProbeCommand {
    param([string] $Identity, [string] $Arguments)
    $stdoutPath = Join-Path $LockPath ($Identity + '.stdout')
    $stderrPath = Join-Path $LockPath ($Identity + '.stderr')
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $probePath
    $startInfo.Arguments = $Arguments
    $startInfo.WorkingDirectory = $probeRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $timedOut = $false
    $stdoutFile = $null
    $stderrFile = $null
    try {
        if (-not $process.Start()) { throw 'start' }
        $stdoutFile = [IO.File]::Create($stdoutPath)
        $stderrFile = [IO.File]::Create($stderrPath)
        $stdoutTask = $process.StandardOutput.BaseStream.CopyToAsync($stdoutFile)
        $stderrTask = $process.StandardError.BaseStream.CopyToAsync($stderrFile)
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $timedOut = $true
            Stop-H1R3OwnedTree $process
            $process.WaitForExit(5000) | Out-Null
        }
        [Threading.Tasks.Task]::WaitAll(@($stdoutTask, $stderrTask), 5000) | Out-Null
        $stdoutFile.Dispose()
        $stdoutFile = $null
        $stderrFile.Dispose()
        $stderrFile = $null
        $exitCode = if ($timedOut -or -not $process.HasExited) { 1 } else { $process.ExitCode }
        $stdoutBytes = if (Test-Path -LiteralPath $stdoutPath) { ([IO.FileInfo]$stdoutPath).Length } else { 0 }
        $stderrBytes = if (Test-Path -LiteralPath $stderrPath) { ([IO.FileInfo]$stderrPath).Length } else { 0 }
        return [pscustomobject][ordered]@{
            command = $Identity
            exitCode = $exitCode
            stdoutBytes = [long]$stdoutBytes
            stderrBytes = [long]$stderrBytes
            timeoutSeconds = $TimeoutSeconds
            timedOut = $timedOut
        }
    }
    finally {
        if ($null -ne $stdoutFile) { $stdoutFile.Dispose() }
        if ($null -ne $stderrFile) { $stderrFile.Dispose() }
        $process.Dispose()
        Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

try {
    $commands = @(
        (Invoke-H1R3ProbeCommand 'identity' 'identity --format json-v1'),
        (Invoke-H1R3ProbeCommand 'capabilities' 'capabilities --format json-v1')
    )
}
catch {
    $cleanupVerified = $false
    Stop-H1R3Native 'H1R3-NATIVE-EXECUTION' $false
}

try { $remaining = @(Get-H1R3ProbeProcesses) }
catch {
    $cleanupVerified = $false
    Stop-H1R3Native 'H1R3-NATIVE-CLEANUP' $false
}
if ($remaining.Count -ne 0) {
    $cleanupVerified = $false
    Stop-H1R3Native 'H1R3-NATIVE-CLEANUP' $false
}
$cleanupVerified = $true

if (@($commands | Where-Object { $_.exitCode -ne 0 -or $_.stderrBytes -ne 0 -or $_.timedOut }).Count -ne 0) {
    Stop-H1R3Native 'H1R3-NATIVE-EXECUTION'
}

$ledger = [ordered]@{
    schemaVersion = 1
    workerId = 'H1'
    campaign = 'R3'
    phaseClosed = $true
    nativeDisposition = 'passed'
    startedAtUtc = $owner.acquiredAtUtc
    completedAtUtc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    cleanupVerified = $true
    postProcessCount = 0
    commands = $commands
}
$parent = Split-Path -Parent $destination
if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    New-Item -ItemType Directory -Path $parent | Out-Null
}
$temporary = Join-Path $parent (([IO.Path]::GetFileName($destination)) + '.' + [Guid]::NewGuid().ToString('N') + '.tmp')
[IO.File]::WriteAllText(
    $temporary,
    (($ledger | ConvertTo-Json -Depth 8) + "`n"),
    [Text.UTF8Encoding]::new($false))
if (Test-Path -LiteralPath $destination) {
    Remove-Item -LiteralPath $temporary -Force
    Stop-H1R3Native 'H1R3-DESTINATION-EXISTS'
}
[IO.File]::Move($temporary, $destination)

Remove-Item -LiteralPath $ownerPath -Force
Remove-Item -LiteralPath $LockPath -Force
$lockOwned = $false
Write-Output 'H1R3-OK'
exit 0
