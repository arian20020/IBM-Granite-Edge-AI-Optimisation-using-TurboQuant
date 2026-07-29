[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$SourcePath,

  [Parameter(Mandatory = $false)]
  [string]$IdentityPath = '',

  [Parameter(Mandatory = $true)]
  [string]$OpenVINODir,

  [Parameter(Mandatory = $true)]
  [string]$BuildPath,

  [Parameter(Mandatory = $false)]
  [ValidateRange(1, 1)]
  [int]$Parallelism = 1,

  [Parameter(Mandatory = $true)]
  [string]$EvidenceRoot,

  [Parameter(Mandatory = $false)]
  [string]$PythonExecutable = 'python',

  [Parameter(Mandatory = $false)]
  [string]$CMakeExecutable = 'cmake',

  [Parameter(Mandatory = $false)]
  [switch]$EnablePython,

  [Parameter(Mandatory = $false)]
  [ValidateNotNullOrEmpty()]
  [string[]]$Targets = @(
    'openvino_genai_obj',
    'turboquant_codec_tests',
    'turboquant_config_tests',
    'turboquant_state_update_decode_tests',
    'turboquant_stateful_graph_tests',
    'turboquant_pipeline_activation_tests'
  ),

  [Parameter(Mandatory = $false)]
  [ValidateRange(1, 86400)]
  [int]$TimeoutSeconds = 7200,

  [Parameter(Mandatory = $false)]
  [ValidateRange(2048, 1048576)]
  [int]$MinimumAvailableRamMiB = 2048
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$pythonOption = if ($EnablePython.IsPresent) { 'ON' } else { 'OFF' }
$effectiveTargets = @($Targets)
if (
  $EnablePython.IsPresent -and
  $effectiveTargets -notcontains 'py_openvino_genai'
) {
  $effectiveTargets += 'py_openvino_genai'
}

$requiredOptions = [ordered]@{
  ENABLE_TESTS = 'ON'
  ENABLE_SAMPLES = 'OFF'
  ENABLE_TOOLS = 'OFF'
  ENABLE_PYTHON = $pythonOption
  ENABLE_JS = 'OFF'
}

function Get-Sha256 {
  param(
    [Parameter(Mandatory = $true)]
    [string]$LiteralPath
  )

  $stream = [IO.File]::Open(
    $LiteralPath,
    [IO.FileMode]::Open,
    [IO.FileAccess]::Read,
    [IO.FileShare]::Read
  )
  try {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
      $digest = $algorithm.ComputeHash($stream)
    } finally {
      $algorithm.Dispose()
    }
  } finally {
    $stream.Dispose()
  }
  return (
    [BitConverter]::ToString($digest).Replace('-', '').ToLowerInvariant()
  )
}

function Test-SamePath {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Left,

    [Parameter(Mandatory = $true)]
    [string]$Right
  )

  $leftFull = [IO.Path]::GetFullPath($Left).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar
  )
  $rightFull = [IO.Path]::GetFullPath($Right).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar
  )
  return $leftFull.Equals(
    $rightFull,
    [StringComparison]::OrdinalIgnoreCase
  )
}

function Read-CMakeCache {
  param(
    [Parameter(Mandatory = $true)]
    [string]$LiteralPath
  )

  if (-not [IO.File]::Exists($LiteralPath)) {
    throw "CMake cache is missing: $LiteralPath"
  }
  $values = [Collections.Generic.Dictionary[string, string]]::new(
    [StringComparer]::Ordinal
  )
  foreach ($line in [IO.File]::ReadAllLines($LiteralPath)) {
    if (
      [string]::IsNullOrWhiteSpace($line) -or
      $line.StartsWith('#') -or
      $line.StartsWith('//')
    ) {
      continue
    }
    $equals = $line.IndexOf('=')
    if ($equals -lt 1) {
      continue
    }
    $keyWithType = $line.Substring(0, $equals)
    $colon = $keyWithType.IndexOf(':')
    $key = if ($colon -ge 0) {
      $keyWithType.Substring(0, $colon)
    } else {
      $keyWithType
    }
    $values[$key] = $line.Substring($equals + 1)
  }
  return $values
}

function Assert-CMakeCache {
  param(
    [Parameter(Mandatory = $true)]
    [string]$CachePath,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedSource,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedOpenVINODir,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedPythonExecutable
  )

  $values = Read-CMakeCache -LiteralPath $CachePath
  if (-not $values.ContainsKey('CMAKE_HOME_DIRECTORY')) {
    throw 'CMake cache has no CMAKE_HOME_DIRECTORY'
  }
  if (-not (Test-SamePath $values['CMAKE_HOME_DIRECTORY'] $ExpectedSource)) {
    throw 'Existing CMake cache names a different source directory'
  }
  if (-not $values.ContainsKey('OpenVINO_DIR')) {
    throw 'CMake cache has no OpenVINO_DIR'
  }
  if (-not (Test-SamePath $values['OpenVINO_DIR'] $ExpectedOpenVINODir)) {
    throw 'Existing CMake cache names a different OpenVINO_DIR'
  }
  foreach ($entry in $requiredOptions.GetEnumerator()) {
    if (
      -not $values.ContainsKey($entry.Key) -or
      $values[$entry.Key] -cne $entry.Value
    ) {
      throw (
        'CMake cache option mismatch: {0} must be {1}' -f
        $entry.Key,
        $entry.Value
      )
    }
  }
  if ($requiredOptions['ENABLE_PYTHON'] -ceq 'ON') {
    if (
      -not $values.ContainsKey('_Python3_EXECUTABLE') -or
      -not (
        Test-SamePath `
          $values['_Python3_EXECUTABLE'] `
          $ExpectedPythonExecutable
      )
    ) {
      throw 'CMake cache configured Python executable does not match'
    }
  }
  return $values
}

function Invoke-GuardedBuildCommand {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Label,

    [Parameter(Mandatory = $true)]
    [string[]]$Command
  )

  $logPath = [IO.Path]::Combine($resolvedEvidenceRoot, "$Label.log")
  $guardEvidencePath = [IO.Path]::Combine(
    $resolvedEvidenceRoot,
    "$Label.guard.json"
  )
  $guardArguments = @(
    '-m',
    'scripts.testing.official_openvino.guarded_build',
    '--cwd',
    $repoRoot,
    '--log',
    $logPath,
    '--evidence',
    $guardEvidencePath,
    '--expected-exit',
    'zero',
    '--minimum-available-ram-mib',
    [string]$MinimumAvailableRamMiB,
    '--timeout-seconds',
    [string]$TimeoutSeconds,
    '--'
  ) + $Command

  $controllerLines = @(& $resolvedPythonExecutable @guardArguments)
  $controllerExit = $LASTEXITCODE
  if ($controllerExit -ne 0) {
    throw "Guarded $Label command failed with exit $controllerExit"
  }
  if (-not [IO.File]::Exists($guardEvidencePath)) {
    throw "Guarded $Label evidence is missing"
  }
  $controller = (
    ($controllerLines -join [Environment]::NewLine) |
      ConvertFrom-Json
  )
  $record = (
    [IO.File]::ReadAllText($guardEvidencePath) |
      ConvertFrom-Json
  )
  $evidenceHash = Get-Sha256 -LiteralPath $guardEvidencePath
  if (
    $controller.valid -ne $true -or
    $record.valid -ne $true -or
    [int]$record.exit_code -ne 0
  ) {
    throw "Guarded $Label evidence is not valid"
  }
  if ([string]$controller.evidence_sha256 -cne $evidenceHash) {
    throw "Guarded $Label evidence hash mismatch"
  }
  if (-not [IO.File]::Exists([string]$record.log_path)) {
    throw "Guarded $Label log is missing"
  }
  $logHash = Get-Sha256 -LiteralPath ([string]$record.log_path)
  if ([string]$record.log_sha256 -cne $logHash) {
    throw "Guarded $Label log hash mismatch"
  }
  if (
    $record.job_object.query_ok -ne $true -or
    [int]$record.job_object.queried_active_process_count_after_cleanup -ne 0 -or
    @($record.job_object.survivor_pids_after_cleanup).Count -ne 0
  ) {
    throw "Guarded $Label cleanup did not prove zero survivors"
  }

  return [pscustomobject]@{
    record = $record
    evidence_path = $guardEvidencePath
    evidence_sha256 = $evidenceHash
    log_path = [string]$record.log_path
    log_sha256 = $logHash
  }
}

function Get-ValidatedSourceIdentity {
  $identityLines = @(
    & $resolvedPythonExecutable `
      -m scripts.testing.verify_openvino_turboquant_build `
      --identity $resolvedIdentity `
      --source $resolvedSource
  )
  if ($LASTEXITCODE -ne 0) {
    throw 'Source identity validation failed'
  }
  return (
    ($identityLines -join [Environment]::NewLine) |
      ConvertFrom-Json
  )
}

function Write-AtomicUtf8Json {
  param(
    [Parameter(Mandatory = $true)]
    [string]$LiteralPath,

    [Parameter(Mandatory = $true)]
    [object]$Value
  )

  $directory = [IO.Path]::GetDirectoryName($LiteralPath)
  $temporaryPath = [IO.Path]::Combine(
    $directory,
    (
      '.' + [IO.Path]::GetFileName($LiteralPath) + '.' +
      [Guid]::NewGuid().ToString('N') + '.tmp'
    )
  )
  $json = $Value | ConvertTo-Json -Depth 16
  $encoding = [Text.UTF8Encoding]::new($false)
  $stream = [IO.FileStream]::new(
    $temporaryPath,
    [IO.FileMode]::CreateNew,
    [IO.FileAccess]::Write,
    [IO.FileShare]::None
  )
  try {
    $writer = [IO.StreamWriter]::new($stream, $encoding)
    try {
      $writer.Write($json)
      $writer.Write([Environment]::NewLine)
      $writer.Flush()
      $stream.Flush($true)
    } finally {
      $writer.Dispose()
    }
  } finally {
    $stream.Dispose()
  }
  try {
    if ([IO.File]::Exists($LiteralPath)) {
      [IO.File]::Replace($temporaryPath, $LiteralPath, $null)
    } else {
      [IO.File]::Move($temporaryPath, $LiteralPath)
    }
    $temporaryPath = $null
  } finally {
    if (
      $null -ne $temporaryPath -and
      [IO.File]::Exists($temporaryPath)
    ) {
      [IO.File]::Delete($temporaryPath)
    }
  }
}

$repoRoot = [IO.Path]::GetFullPath(
  [IO.Path]::Combine($PSScriptRoot, '..', '..')
)
$resolvedPythonExecutable = if ([IO.File]::Exists($PythonExecutable)) {
  (Resolve-Path -LiteralPath $PythonExecutable).Path
} else {
  $pythonCommand = Get-Command `
    -Name $PythonExecutable `
    -CommandType Application `
    -ErrorAction Stop |
      Select-Object -First 1
  [IO.Path]::GetFullPath($pythonCommand.Source)
}
$resolvedSource = (
  Resolve-Path -LiteralPath $SourcePath
).Path
if (-not [IO.Directory]::Exists($resolvedSource)) {
  throw "Source directory is missing: $resolvedSource"
}
$resolvedOpenVINODir = (
  Resolve-Path -LiteralPath $OpenVINODir
).Path
if (-not [IO.Directory]::Exists($resolvedOpenVINODir)) {
  throw "OpenVINO_DIR is missing: $resolvedOpenVINODir"
}
if ([string]::IsNullOrWhiteSpace($IdentityPath)) {
  $IdentityPath = [IO.Path]::Combine(
    [IO.Path]::GetDirectoryName($resolvedSource),
    'openvino.genai-turboquant.identity.json'
  )
}
$resolvedIdentity = (
  Resolve-Path -LiteralPath $IdentityPath
).Path
if (-not [IO.File]::Exists($resolvedIdentity)) {
  throw "Source identity is missing: $resolvedIdentity"
}

$resolvedBuild = [IO.Path]::GetFullPath($BuildPath)
$resolvedEvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
[IO.Directory]::CreateDirectory($resolvedEvidenceRoot) | Out-Null
foreach ($reservedEvidenceName in @(
  'configure.log',
  'configure.guard.json',
  'build.log',
  'build.guard.json',
  'build-provenance.json'
)) {
  $reservedEvidencePath = [IO.Path]::Combine(
    $resolvedEvidenceRoot,
    $reservedEvidenceName
  )
  if ([IO.File]::Exists($reservedEvidencePath)) {
    throw (
      'Evidence root already contains build evidence; use a new root: ' +
      $reservedEvidencePath
    )
  }
}
$cachePath = [IO.Path]::Combine($resolvedBuild, 'CMakeCache.txt')
if ([IO.Directory]::Exists($resolvedBuild)) {
  if ([IO.File]::Exists($cachePath)) {
    $null = Assert-CMakeCache `
      -CachePath $cachePath `
      -ExpectedSource $resolvedSource `
      -ExpectedOpenVINODir $resolvedOpenVINODir `
      -ExpectedPythonExecutable $resolvedPythonExecutable
  } elseif (
    [IO.Directory]::EnumerateFileSystemEntries($resolvedBuild).GetEnumerator().MoveNext()
  ) {
    throw 'Existing build directory is nonempty but has no CMakeCache.txt'
  }
} else {
  [IO.Directory]::CreateDirectory($resolvedBuild) | Out-Null
}

Push-Location -LiteralPath $repoRoot
try {
  $sourceIdentity = Get-ValidatedSourceIdentity

  $configureCommand = @(
    $CMakeExecutable,
    '-S',
    $resolvedSource,
    '-B',
    $resolvedBuild,
    "-DOpenVINO_DIR=$resolvedOpenVINODir",
    "-DPython3_EXECUTABLE=$resolvedPythonExecutable",
    '-DENABLE_TESTS=ON',
    '-DENABLE_SAMPLES=OFF',
    '-DENABLE_TOOLS=OFF',
    "-DENABLE_PYTHON=$pythonOption",
    '-DENABLE_JS=OFF'
  )
  $configureGuard = Invoke-GuardedBuildCommand `
    -Label 'configure' `
    -Command $configureCommand

  $cacheValues = Assert-CMakeCache `
    -CachePath $cachePath `
    -ExpectedSource $resolvedSource `
    -ExpectedOpenVINODir $resolvedOpenVINODir `
    -ExpectedPythonExecutable $resolvedPythonExecutable
  $cacheHashAfterConfigure = Get-Sha256 -LiteralPath $cachePath

  $buildCommand = @(
    $CMakeExecutable,
    '--build',
    $resolvedBuild,
    '--config',
    'Release',
    '--parallel',
    [string]$Parallelism,
    '--target'
  ) + $effectiveTargets
  $buildGuard = Invoke-GuardedBuildCommand `
    -Label 'build' `
    -Command $buildCommand

  $cacheValues = Assert-CMakeCache `
    -CachePath $cachePath `
    -ExpectedSource $resolvedSource `
    -ExpectedOpenVINODir $resolvedOpenVINODir `
    -ExpectedPythonExecutable $resolvedPythonExecutable
  $cacheHash = Get-Sha256 -LiteralPath $cachePath
  $sourceIdentityAfterBuild = Get-ValidatedSourceIdentity
  foreach ($identityField in @(
    'identity_sha256',
    'base_commit',
    'upstream_commit',
    'patch_commit',
    'upstream_tree',
    'derived_tree',
    'patch_series_sha256',
    'status_porcelain_sha256'
  )) {
    if (
      [string]$sourceIdentityAfterBuild.$identityField -cne
      [string]$sourceIdentity.$identityField
    ) {
      throw "Source identity changed during build: $identityField"
    }
  }
} finally {
  Pop-Location
}

$outputPaths = @(
  [IO.Directory]::EnumerateFiles(
    $resolvedBuild,
    '*',
    [IO.SearchOption]::AllDirectories
  ) |
    Where-Object {
      @('.dll', '.lib', '.exe', '.pyd') -contains (
        [IO.Path]::GetExtension($_).ToLowerInvariant()
      )
    } |
    Sort-Object
)
if ($outputPaths.Count -eq 0) {
  throw 'Build produced no DLL, LIB, EXE, or PYD outputs to hash'
}
if ($EnablePython.IsPresent) {
  $pythonOutputs = @(
    $outputPaths |
      Where-Object {
        [IO.Path]::GetExtension($_).Equals(
          '.pyd',
          [StringComparison]::OrdinalIgnoreCase
        ) -and
        [IO.Path]::GetFileName($_).StartsWith(
          'py_openvino_genai',
          [StringComparison]::OrdinalIgnoreCase
        )
      }
  )
  if ($pythonOutputs.Count -eq 0) {
    throw 'Python build produced no py_openvino_genai module'
  }
}
$outputs = @(
  foreach ($outputPath in $outputPaths) {
    $item = [IO.FileInfo]::new($outputPath)
    if ($item.Length -le 0) {
      throw "Build output is empty: $outputPath"
    }
    [ordered]@{
      path = $item.FullName
      relative_path = $item.FullName.Substring($resolvedBuild.Length).TrimStart(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar
      )
      size_bytes = [int64]$item.Length
      sha256 = Get-Sha256 -LiteralPath $item.FullName
    }
  }
)

$recordedOptions = [ordered]@{}
foreach ($entry in $requiredOptions.GetEnumerator()) {
  $recordedOptions[$entry.Key] = [string]$cacheValues[$entry.Key]
}
if ($EnablePython.IsPresent) {
  $recordedOptions['Python3_EXECUTABLE'] = (
    [string]$cacheValues['_Python3_EXECUTABLE']
  )
}
$patchRecords = @(
  foreach ($patch in @($sourceIdentity.patches)) {
    [ordered]@{
      name = [string]$patch.name
      blob_id = [string]$patch.blob_id
      sha256 = [string]$patch.sha256
    }
  }
)
$manifest = [ordered]@{
  schema = 'openvino-turboquant-build-provenance/v1'
  status = 'passed'
  created_utc = [DateTime]::UtcNow.ToString(
    'yyyy-MM-ddTHH:mm:ss.fffffffZ',
    [Globalization.CultureInfo]::InvariantCulture
  )
  source = [ordered]@{
    path = $resolvedSource
    identity_path = $resolvedIdentity
    identity_sha256 = [string]$sourceIdentity.identity_sha256
    patch_directory = [string]$sourceIdentity.patch_directory
    branch = [string]$sourceIdentity.branch
    base_commit = [string]$sourceIdentity.base_commit
    upstream_commit = [string]$sourceIdentity.upstream_commit
    patch_commit = [string]$sourceIdentity.patch_commit
    upstream_tree = [string]$sourceIdentity.upstream_tree
    derived_tree = [string]$sourceIdentity.derived_tree
    applied_patches = @($sourceIdentity.applied_patches)
    patches = $patchRecords
    patch_series_sha256 = [string]$sourceIdentity.patch_series_sha256
    dirty = $false
    status_porcelain_sha256 = [string]$sourceIdentity.status_porcelain_sha256
  }
  configure = [ordered]@{
    command = $configureCommand
    exit_code = [int]$configureGuard.record.exit_code
    source_directory = $resolvedSource
    build_directory = $resolvedBuild
    openvino_dir = $resolvedOpenVINODir
    python_executable = $resolvedPythonExecutable
    options = $recordedOptions
    cache_path = $cachePath
    cache_sha256 = $cacheHash
    cache_sha256_after_configure = $cacheHashAfterConfigure
    log_path = $configureGuard.log_path
    log_sha256 = $configureGuard.log_sha256
    guard_evidence_path = $configureGuard.evidence_path
    guard_evidence_sha256 = $configureGuard.evidence_sha256
    minimum_available_ram_bytes = (
      [int64]$configureGuard.record.observed_available_ram_bytes.minimum
    )
    cleanup = $configureGuard.record.job_object
  }
  build = [ordered]@{
    command = $buildCommand
    exit_code = [int]$buildGuard.record.exit_code
    parallelism = $Parallelism
    targets = $effectiveTargets
    log_path = $buildGuard.log_path
    log_sha256 = $buildGuard.log_sha256
    guard_evidence_path = $buildGuard.evidence_path
    guard_evidence_sha256 = $buildGuard.evidence_sha256
    minimum_available_ram_bytes = (
      [int64]$buildGuard.record.observed_available_ram_bytes.minimum
    )
    cleanup = $buildGuard.record.job_object
  }
  outputs = $outputs
}

$manifestPath = [IO.Path]::Combine(
  $resolvedEvidenceRoot,
  'build-provenance.json'
)
Write-AtomicUtf8Json -LiteralPath $manifestPath -Value $manifest
$manifestHash = Get-Sha256 -LiteralPath $manifestPath
[ordered]@{
  schema = 'openvino-turboquant-build-provenance-result/v1'
  status = 'passed'
  manifest_path = $manifestPath
  manifest_sha256 = $manifestHash
  identity_sha256 = [string]$sourceIdentity.identity_sha256
  patch_commit = [string]$sourceIdentity.patch_commit
  derived_tree = [string]$sourceIdentity.derived_tree
  cache_sha256 = $cacheHash
  output_count = $outputs.Count
} | ConvertTo-Json -Depth 5
