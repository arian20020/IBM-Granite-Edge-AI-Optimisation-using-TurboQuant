[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$')]
  [string]$Label,

  [Parameter(Mandatory = $true)]
  [string]$WorkingDirectory,

  [Parameter(Mandatory = $true)]
  [string]$EvidenceRoot,

  [Parameter(Mandatory = $true)]
  [ValidateSet('Zero', 'NonZero')]
  [string]$ExpectedExit,

  [Parameter(Mandatory = $false)]
  [ValidateRange(0.001, 86400.0)]
  [double]$TimeoutSeconds = 7200.0,

  [Parameter(Mandatory = $true)]
  [ValidateNotNullOrEmpty()]
  [string[]]$Command
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 5) {
  throw 'invoke_guarded_command.ps1 requires Windows PowerShell 5.1 or newer'
}
if ($Command.Count -eq 0) {
  throw 'Command must contain at least one argv element'
}
foreach ($argument in $Command) {
  if ([string]::IsNullOrEmpty($argument)) {
    throw 'Command argv elements must be non-empty strings'
  }
}

$resolvedWorkingDirectory = (Resolve-Path -LiteralPath $WorkingDirectory).Path
$resolvedEvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
[IO.Directory]::CreateDirectory($resolvedEvidenceRoot) | Out-Null
$logPath = Join-Path $resolvedEvidenceRoot ($Label + '.log')
$evidencePath = Join-Path $resolvedEvidenceRoot ($Label + '.json')
if (
  [IO.File]::Exists($logPath) -or
  [IO.File]::Exists($evidencePath)
) {
  throw "Guard evidence label already exists: $Label"
}

$expectedExitValue = if ($ExpectedExit -eq 'Zero') { 'zero' } else { 'nonzero' }
$timeoutValue = $TimeoutSeconds.ToString(
  'R',
  [Globalization.CultureInfo]::InvariantCulture
)
$python = (Get-Command python -ErrorAction Stop).Source
$guardArguments = @(
  '-m',
  'scripts.testing.official_openvino.guarded_build',
  '--cwd',
  $resolvedWorkingDirectory,
  '--log',
  $logPath,
  '--evidence',
  $evidencePath,
  '--expected-exit',
  $expectedExitValue,
  '--minimum-available-ram-mib',
  '2048',
  '--timeout-seconds',
  $timeoutValue,
  '--'
) + @($Command)

$previousErrorActionPreference = $ErrorActionPreference
try {
  # Windows PowerShell 5.1 can surface native stderr as non-terminating error
  # records. The native exit code, not ErrorAction, is authoritative here.
  $ErrorActionPreference = 'Continue'
  $guardNativeOutput = @(& $python @guardArguments)
  $guardExitCode = $LASTEXITCODE
}
finally {
  $ErrorActionPreference = $previousErrorActionPreference
}
if (-not [IO.File]::Exists($evidencePath)) {
  throw "Guard did not publish evidence: $evidencePath"
}
if (-not [IO.File]::Exists($logPath)) {
  throw "Guard did not publish a log: $logPath"
}

$record = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json
if ($record.schema -ne 'official-openvino-owned-process-guard/v1') {
  throw "Unexpected guard schema: $($record.schema)"
}
if ($record.expected_exit -ne $expectedExitValue) {
  throw "Guard expected-exit mismatch: $($record.expected_exit)"
}
if ($null -eq $record.exit_code) {
  throw 'Guard did not record the actual child exit code'
}
$actualExitCode = [int]$record.exit_code
$actualExitMatches = if ($ExpectedExit -eq 'Zero') {
  $actualExitCode -eq 0
}
else {
  $actualExitCode -ne 0
}
if (-not $actualExitMatches) {
  throw (
    "Actual child exit $actualExitCode does not match ExpectedExit " +
    $ExpectedExit
  )
}

$recordCommand = @($record.command)
if ($recordCommand.Count -ne $Command.Count) {
  throw 'Guard command argv count does not match the requested argv'
}
for ($index = 0; $index -lt $Command.Count; $index++) {
  if (
    -not [StringComparer]::Ordinal.Equals(
      [string]$recordCommand[$index],
      [string]$Command[$index]
    )
  ) {
    throw "Guard command argv mismatch at index $index"
  }
}
$resolvedLogPath = (Resolve-Path -LiteralPath $logPath).Path
$resolvedEvidencePath = (Resolve-Path -LiteralPath $evidencePath).Path
if (
  -not [StringComparer]::OrdinalIgnoreCase.Equals(
    [string]$record.working_directory,
    $resolvedWorkingDirectory
  )
) {
  throw 'Guard working-directory evidence does not match the request'
}
if (
  -not [StringComparer]::OrdinalIgnoreCase.Equals(
    [string]$record.log_path,
    $resolvedLogPath
  )
) {
  throw 'Guard log-path evidence does not match the request'
}
if (
  -not [StringComparer]::OrdinalIgnoreCase.Equals(
    [string]$record.evidence_path,
    $resolvedEvidencePath
  )
) {
  throw 'Guard evidence-path evidence does not match the request'
}
if ([double]$record.maximum_runtime_seconds -ne $TimeoutSeconds) {
  throw "Guard timeout mismatch: $($record.maximum_runtime_seconds)"
}
if ([int64]$record.configured_minimum_available_ram_bytes -ne 2147483648) {
  throw 'Guard configured RAM floor is not exactly 2,048 MiB'
}
$configuredRamFloor = [int64]$record.configured_minimum_available_ram_bytes
if (
  $null -eq $record.observed_available_ram_bytes.before -or
  $null -eq $record.observed_available_ram_bytes.minimum -or
  $null -eq $record.observed_available_ram_bytes.after
) {
  throw 'Guard did not record complete observed available-RAM evidence'
}
$observedRamMinimum = [int64]$record.observed_available_ram_bytes.minimum
if ($observedRamMinimum -lt $configuredRamFloor) {
  throw 'Guard observed RAM fell below the configured 2,048 MiB floor'
}
if (
  $observedRamMinimum -gt
    [int64]$record.observed_available_ram_bytes.before -or
  $observedRamMinimum -gt
    [int64]$record.observed_available_ram_bytes.after
) {
  throw 'Guard observed minimum RAM is inconsistent with endpoint samples'
}
if (
  [int]$record.memory_sample_count -lt 1 -or
  [int64]$record.peak_working_set_bytes -le 0 -or
  [int64]$record.peak_private_bytes -le 0
) {
  throw 'Guard did not record complete Job-PID memory samples and peaks'
}
if (
  $record.launch_governance.created_suspended -ne $true -or
  $record.launch_governance.assigned_before_resume -ne $true
) {
  throw 'Guard did not prove suspended launch and assignment before resume'
}
if ($record.job_object.setup_ok -ne $true) {
  throw 'Guard did not prove Job Object setup'
}
if ($record.job_object.query_ok -ne $true) {
  throw 'Guard did not query the Job Object after cleanup'
}
if (
  [int]$record.job_object.queried_active_process_count_after_cleanup -ne 0
) {
  throw 'Guard reported active processes after cleanup'
}
if (@($record.job_object.survivor_pids_after_cleanup).Count -ne 0) {
  throw 'Guard reported survivor PIDs after cleanup'
}
if (
  $record.timed_out -ne $false -or
  $record.low_memory_stop -ne $false -or
  $null -ne $record.termination_reason
) {
  throw 'Guarded command reported timeout, low memory, or forced termination'
}
if (@($record.emergency_actions).Count -ne 0) {
  throw 'Guarded command required emergency cleanup'
}
if (@($record.validation_errors).Count -ne 0) {
  throw 'Guarded command persisted validation errors'
}
if ($null -ne $record.launch_governance.cpu_affinity_mask) {
  throw 'Generic guard unexpectedly applied CPU affinity'
}
if ($null -ne $record.launch_governance.cpu_rate_hard_cap_percent) {
  throw 'Generic guard unexpectedly applied a CPU-rate cap'
}
if ($record.msbuild_disable_node_reuse -ne '1') {
  throw 'MSBUILDDISABLENODEREUSE was not forced to 1'
}
$observedLogHash = (
  Get-FileHash -LiteralPath $logPath -Algorithm SHA256
).Hash.ToLowerInvariant()
if ($record.log_sha256 -ne $observedLogHash) {
  throw 'Guard log SHA-256 does not match the persisted log'
}
if ($record.valid -ne $true -or $guardExitCode -ne 0) {
  $failures = @($record.validation_errors) -join '; '
  $nativeText = @($guardNativeOutput) -join '; '
  throw "Guarded command failed validation: $failures; native: $nativeText"
}

$observedEvidenceHash = (
  Get-FileHash -LiteralPath $evidencePath -Algorithm SHA256
).Hash.ToLowerInvariant()
$verification = [ordered]@{
  schema = 'official-openvino-wrapper-verification/v1'
  command = @($Command)
  working_directory = $resolvedWorkingDirectory
  log_path = $resolvedLogPath
  evidence_path = $resolvedEvidencePath
  expected_exit = $expectedExitValue
  actual_exit_code = $actualExitCode
  log_sha256 = $observedLogHash
  evidence_sha256 = $observedEvidenceHash
  valid = $true
}
Write-Output ($verification | ConvertTo-Json -Depth 8 -Compress)
