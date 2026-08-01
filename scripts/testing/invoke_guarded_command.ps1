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

  [Parameter(Mandatory = $false)]
  [ValidateRange(0, 1048576)]
  [int]$MinimumAvailableRamMiB = 2048,

  [Parameter(Mandatory = $true)]
  [string]$PythonExecutable,

  [Parameter(Mandatory = $true)]
  [ValidatePattern('^[0-9a-f]{64}$')]
  [string]$PythonSha256,

  [Parameter(Mandatory = $true)]
  [ValidatePattern('^[0-9a-f]{64}$')]
  [string]$PythonDllSha256,

  [Parameter(Mandatory = $false)]
  [string]$VerificationOutputPath,

  [Parameter(Mandatory = $true)]
  [ValidateNotNullOrEmpty()]
  [string[]]$Command
)

if (
  $PSVersionTable.PSEdition -cne 'Desktop' -or
  $PSVersionTable.PSVersion.Major -ne 5 -or
  $PSVersionTable.PSVersion.Minor -ne 1
) {
  throw 'invoke_guarded_command.ps1 requires Windows PowerShell 5.1'
}

# Resolve the Microsoft cmdlets by type through the engine API before using
# any command name. PowerShell permits caller aliases whose names contain a
# module qualifier, so a literal Module\Command invocation is not sufficient.
$bindTrustedCmdlet = {
  param(
    [string]$QualifiedName,
    [string]$ExpectedName,
    [string]$ExpectedModule,
    [string]$ExpectedImplementingType,
    [string]$ExpectedAssembly
  )

  $commandInfo = $ExecutionContext.InvokeCommand.GetCommand(
    $QualifiedName,
    [Management.Automation.CommandTypes]::Cmdlet
  )
  if (
    $null -eq $commandInfo -or
    $commandInfo -isnot [Management.Automation.CmdletInfo] -or
    $commandInfo.CommandType -ne
      [Management.Automation.CommandTypes]::Cmdlet -or
    $commandInfo.Name -cne $ExpectedName -or
    $commandInfo.ModuleName -cne $ExpectedModule -or
    $commandInfo.Source -cne $ExpectedModule -or
    $null -eq $commandInfo.ImplementingType -or
    $commandInfo.ImplementingType.FullName -cne
      $ExpectedImplementingType
  ) {
    throw "Trusted PowerShell cmdlet binding failed: $QualifiedName"
  }

  $assembly = $commandInfo.ImplementingType.Assembly
  $assemblyName = $assembly.GetName()
  $publicKeyToken = [BitConverter]::ToString(
    $assemblyName.GetPublicKeyToken()
  ).Replace('-', '').ToLowerInvariant()
  if (
    $assemblyName.Name -cne $ExpectedAssembly -or
    $publicKeyToken -cne '31bf3856ad364e35' -or
    $assembly.GlobalAssemblyCache -ne $true -or
    [string]::IsNullOrWhiteSpace($assembly.Location)
  ) {
    throw "Trusted PowerShell cmdlet assembly failed: $QualifiedName"
  }

  $windowsRoot = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::Windows
  )
  $trustedGacRoot = [IO.Path]::GetFullPath(
    [IO.Path]::Combine($windowsRoot, 'Microsoft.Net\assembly\GAC_MSIL')
  ).TrimEnd([char[]]'\/')
  $assemblyPath = [IO.Path]::GetFullPath($assembly.Location)
  $trustedGacPrefix = $trustedGacRoot + [IO.Path]::DirectorySeparatorChar
  if (
    -not $assemblyPath.StartsWith(
      $trustedGacPrefix,
      [StringComparison]::OrdinalIgnoreCase
    )
  ) {
    throw "Trusted PowerShell cmdlet assembly path failed: $QualifiedName"
  }

  if ($ExpectedModule -ceq 'Microsoft.PowerShell.Core') {
    if ($null -ne $commandInfo.Module) {
      throw "Trusted PowerShell core cmdlet module failed: $QualifiedName"
    }
  }
  else {
    if (
      $null -eq $commandInfo.Module -or
      [string]::IsNullOrWhiteSpace($commandInfo.Module.Path)
    ) {
      throw "Trusted PowerShell cmdlet module is missing: $QualifiedName"
    }
    $trustedModuleRoot = [IO.Path]::GetFullPath(
      [IO.Path]::Combine($PSHOME, 'Modules', $ExpectedModule)
    ).TrimEnd([char[]]'\/')
    $modulePath = [IO.Path]::GetFullPath($commandInfo.Module.Path)
    $trustedModulePrefix = (
      $trustedModuleRoot + [IO.Path]::DirectorySeparatorChar
    )
    if (
      -not $modulePath.StartsWith(
        $trustedModulePrefix,
        [StringComparison]::OrdinalIgnoreCase
      )
    ) {
      throw "Trusted PowerShell cmdlet module path failed: $QualifiedName"
    }
  }
  return $commandInfo
}

$setStrictMode = & $bindTrustedCmdlet `
  'Microsoft.PowerShell.Core\Set-StrictMode' `
  'Set-StrictMode' `
  'Microsoft.PowerShell.Core' `
  'Microsoft.PowerShell.Commands.SetStrictModeCommand' `
  'System.Management.Automation'
$convertFromJson = & $bindTrustedCmdlet `
  'Microsoft.PowerShell.Utility\ConvertFrom-Json' `
  'ConvertFrom-Json' `
  'Microsoft.PowerShell.Utility' `
  'Microsoft.PowerShell.Commands.ConvertFromJsonCommand' `
  'Microsoft.PowerShell.Commands.Utility'
$convertToJson = & $bindTrustedCmdlet `
  'Microsoft.PowerShell.Utility\ConvertTo-Json' `
  'ConvertTo-Json' `
  'Microsoft.PowerShell.Utility' `
  'Microsoft.PowerShell.Commands.ConvertToJsonCommand' `
  'Microsoft.PowerShell.Commands.Utility'
$forEachObject = & $bindTrustedCmdlet `
  'Microsoft.PowerShell.Core\ForEach-Object' `
  'ForEach-Object' `
  'Microsoft.PowerShell.Core' `
  'Microsoft.PowerShell.Commands.ForEachObjectCommand' `
  'System.Management.Automation'
$getAuthenticodeSignature = & $bindTrustedCmdlet `
  'Microsoft.PowerShell.Security\Get-AuthenticodeSignature' `
  'Get-AuthenticodeSignature' `
  'Microsoft.PowerShell.Security' `
  'Microsoft.PowerShell.Commands.GetAuthenticodeSignatureCommand' `
  'Microsoft.PowerShell.Security'
$getChildItem = & $bindTrustedCmdlet `
  'Microsoft.PowerShell.Management\Get-ChildItem' `
  'Get-ChildItem' `
  'Microsoft.PowerShell.Management' `
  'Microsoft.PowerShell.Commands.GetChildItemCommand' `
  'Microsoft.PowerShell.Commands.Management'
$getItem = & $bindTrustedCmdlet `
  'Microsoft.PowerShell.Management\Get-Item' `
  'Get-Item' `
  'Microsoft.PowerShell.Management' `
  'Microsoft.PowerShell.Commands.GetItemCommand' `
  'Microsoft.PowerShell.Commands.Management'
$joinPath = & $bindTrustedCmdlet `
  'Microsoft.PowerShell.Management\Join-Path' `
  'Join-Path' `
  'Microsoft.PowerShell.Management' `
  'Microsoft.PowerShell.Commands.JoinPathCommand' `
  'Microsoft.PowerShell.Commands.Management'
$newObject = & $bindTrustedCmdlet `
  'Microsoft.PowerShell.Utility\New-Object' `
  'New-Object' `
  'Microsoft.PowerShell.Utility' `
  'Microsoft.PowerShell.Commands.NewObjectCommand' `
  'Microsoft.PowerShell.Commands.Utility'
$outNull = & $bindTrustedCmdlet `
  'Microsoft.PowerShell.Core\Out-Null' `
  'Out-Null' `
  'Microsoft.PowerShell.Core' `
  'Microsoft.PowerShell.Commands.OutNullCommand' `
  'System.Management.Automation'
$popLocation = & $bindTrustedCmdlet `
  'Microsoft.PowerShell.Management\Pop-Location' `
  'Pop-Location' `
  'Microsoft.PowerShell.Management' `
  'Microsoft.PowerShell.Commands.PopLocationCommand' `
  'Microsoft.PowerShell.Commands.Management'
$pushLocation = & $bindTrustedCmdlet `
  'Microsoft.PowerShell.Management\Push-Location' `
  'Push-Location' `
  'Microsoft.PowerShell.Management' `
  'Microsoft.PowerShell.Commands.PushLocationCommand' `
  'Microsoft.PowerShell.Commands.Management'
$resolvePath = & $bindTrustedCmdlet `
  'Microsoft.PowerShell.Management\Resolve-Path' `
  'Resolve-Path' `
  'Microsoft.PowerShell.Management' `
  'Microsoft.PowerShell.Commands.ResolvePathCommand' `
  'Microsoft.PowerShell.Commands.Management'
$whereObject = & $bindTrustedCmdlet `
  'Microsoft.PowerShell.Core\Where-Object' `
  'Where-Object' `
  'Microsoft.PowerShell.Core' `
  'Microsoft.PowerShell.Commands.WhereObjectCommand' `
  'System.Management.Automation'
$writeOutput = & $bindTrustedCmdlet `
  'Microsoft.PowerShell.Utility\Write-Output' `
  'Write-Output' `
  'Microsoft.PowerShell.Utility' `
  'Microsoft.PowerShell.Commands.WriteOutputCommand' `
  'Microsoft.PowerShell.Commands.Utility'

& $setStrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

if ($Command.Count -eq 0) {
  throw 'Command must contain at least one argv element'
}
foreach ($argument in $Command) {
  if ([string]::IsNullOrEmpty($argument)) {
    throw 'Command argv elements must be non-empty strings'
  }
}

$resolvedWorkingDirectory = (
  & $resolvePath -LiteralPath $WorkingDirectory
).Path
$resolvedEvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
[IO.Directory]::CreateDirectory($resolvedEvidenceRoot) |
  & $outNull
$logPath = & $joinPath $resolvedEvidenceRoot ($Label + '.log')
$evidencePath = & $joinPath $resolvedEvidenceRoot ($Label + '.json')
$verificationPath = $null
if (-not [string]::IsNullOrEmpty($VerificationOutputPath)) {
  if (-not [IO.Path]::IsPathRooted($VerificationOutputPath)) {
    throw 'VerificationOutputPath must be absolute'
  }
  $verificationPath = [IO.Path]::GetFullPath($VerificationOutputPath)
  $expectedVerificationPath = [IO.Path]::GetFullPath(
    (& $joinPath $resolvedEvidenceRoot ($Label + '.verification.json'))
  )
  if (-not $verificationPath.Equals(
      $expectedVerificationPath,
      [StringComparison]::OrdinalIgnoreCase)) {
    throw (
      'VerificationOutputPath must be the label verification file under ' +
      'EvidenceRoot'
    )
  }
}
if (
  [IO.File]::Exists($logPath) -or
  [IO.File]::Exists($evidencePath) -or
  ($null -ne $verificationPath -and
   [IO.File]::Exists($verificationPath))
) {
  throw "Guard evidence label already exists: $Label"
}

$expectedExitValue = if ($ExpectedExit -eq 'Zero') { 'zero' } else { 'nonzero' }
$timeoutValue = $TimeoutSeconds.ToString(
  'R',
  [Globalization.CultureInfo]::InvariantCulture
)
$minimumAvailableRamMiBValue = $MinimumAvailableRamMiB.ToString(
  [Globalization.CultureInfo]::InvariantCulture
)
$controllerRepositoryRoot = [IO.Path]::GetFullPath(
  (& $joinPath $PSScriptRoot '..\..')
)
$controllerModulePath = (
  & $joinPath $controllerRepositoryRoot `
    'scripts\testing\official_openvino\guarded_build.py'
)
if (-not [IO.File]::Exists($controllerModulePath)) {
  throw (
    'Guard controller module is not present under the wrapper repository ' +
    "root: $controllerModulePath"
  )
}
$controllerPycachePrefix = (
  & $joinPath ([IO.Path]::GetTempPath()) `
    (
      'official-openvino-controller-pycache-' +
      [Guid]::NewGuid().ToString('N')
    )
)
if (
  [IO.File]::Exists($controllerPycachePrefix) -or
  [IO.Directory]::Exists($controllerPycachePrefix)
) {
  throw 'Fresh controller pycache prefix unexpectedly already exists'
}
$expectedPythonSigner = (
  'CN=Python Software Foundation, O=Python Software Foundation, ' +
  'L=Beaverton, S=Oregon, C=US'
)
$assertNotReparsePoint = {
  param([string]$Path, [string]$Name)

  $item = & $getItem -LiteralPath $Path -Force -ErrorAction Stop
  if (
    ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
  ) {
    throw "Approved Python $Name is a reparse point: $Path"
  }
}

$assertNoReparseDirectoryAncestry = {
  param([string]$Path)

  $current = & $getItem -LiteralPath $Path -Force -ErrorAction Stop
  while ($null -ne $current) {
    if (
      ($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
    ) {
      throw (
        'Approved Python install ancestry contains a reparse point: ' +
        $current.FullName
      )
    }
    $parent = [IO.Directory]::GetParent($current.FullName)
    if ($null -eq $parent) {
      break
    }
    $current = & $getItem -LiteralPath $parent.FullName `
      -Force -ErrorAction Stop
  }
}

$getSha256Hex = {
  param([string]$Path)

  $stream = [IO.File]::Open(
    $Path,
    [IO.FileMode]::Open,
    [IO.FileAccess]::Read,
    [IO.FileShare]::Read
  )
  $sha256 = [Security.Cryptography.SHA256]::Create()
  try {
    $hashBytes = $sha256.ComputeHash($stream)
  }
  finally {
    $sha256.Dispose()
    $stream.Dispose()
  }
  return (
    [BitConverter]::ToString($hashBytes).Replace('-', '').ToLowerInvariant()
  )
}

if (-not [IO.Path]::IsPathRooted($PythonExecutable)) {
  throw 'Approved Python controller interpreter path must be absolute'
}
$requestedPython = [IO.Path]::GetFullPath($PythonExecutable)
if (-not [IO.File]::Exists($requestedPython)) {
  throw "Approved Python controller interpreter is not a file: $requestedPython"
}
& $assertNotReparsePoint `
  -Path $requestedPython `
  -Name 'controller interpreter'
& $assertNoReparseDirectoryAncestry -Path (
  [IO.Path]::GetDirectoryName($requestedPython)
)
$python = (
  & $resolvePath -LiteralPath $requestedPython
).ProviderPath
if (
  -not [StringComparer]::OrdinalIgnoreCase.Equals(
    $requestedPython,
    $python
  )
) {
  throw 'Approved Python controller interpreter path is not canonical'
}
$pythonInstallRoot = [IO.Path]::GetDirectoryName($python)
& $assertNotReparsePoint -Path $python -Name 'controller interpreter'
& $assertNoReparseDirectoryAncestry -Path $pythonInstallRoot
$pythonDllCandidates = @(
  & $getChildItem -LiteralPath $pythonInstallRoot -File |
    & $whereObject {
      $_.Name -cmatch '^python[0-9]{3}\.dll$'
    }
)
if ($pythonDllCandidates.Count -ne 1) {
  throw 'Approved Python install must contain exactly one versioned runtime DLL'
}
$pythonDll = (
  & $resolvePath -LiteralPath $pythonDllCandidates[0].FullName
).ProviderPath
& $assertNotReparsePoint -Path $pythonDll -Name 'runtime DLL'
$pythonHashBefore = & $getSha256Hex -Path $python
$pythonDllHashBefore = & $getSha256Hex -Path $pythonDll
if ($pythonHashBefore -cne $PythonSha256) {
  throw 'Approved Python controller interpreter SHA-256 does not match'
}
if ($pythonDllHashBefore -cne $PythonDllSha256) {
  throw 'Approved Python runtime DLL SHA-256 does not match'
}
try {
  $pythonSignature = (
    & $getAuthenticodeSignature -LiteralPath $python -ErrorAction Stop
  )
  $pythonDllSignature = (
    & $getAuthenticodeSignature -LiteralPath $pythonDll -ErrorAction Stop
  )
}
catch {
  throw 'Approved Python runtime signatures could not be validated'
}
foreach ($signatureRecord in @(
  [PSCustomObject]@{
    Name = 'controller interpreter'
    Signature = $pythonSignature
  },
  [PSCustomObject]@{
    Name = 'runtime DLL'
    Signature = $pythonDllSignature
  }
)) {
  if (
    [string]$signatureRecord.Signature.Status -cne 'Valid' -or
    $null -eq $signatureRecord.Signature.SignerCertificate -or
    $signatureRecord.Signature.SignerCertificate.Subject -cne
      $expectedPythonSigner
  ) {
    throw (
      "Approved Python $($signatureRecord.Name) signature is not " +
      'valid and PSF-signed; status=' +
      [string]$signatureRecord.Signature.Status
    )
  }
}
$pythonHash = & $getSha256Hex -Path $python
$pythonDllHash = & $getSha256Hex -Path $pythonDll
if (
  $pythonHash -cne $pythonHashBefore -or
  $pythonHash -cne $PythonSha256
) {
  throw 'Approved Python controller interpreter changed during validation'
}
if (
  $pythonDllHash -cne $pythonDllHashBefore -or
  $pythonDllHash -cne $PythonDllSha256
) {
  throw 'Approved Python runtime DLL changed during validation'
}
$pythonSignatureStatus = [string]$pythonSignature.Status
$pythonSignatureType = [string]$pythonSignature.SignatureType
$pythonSignerSubject = [string]$pythonSignature.SignerCertificate.Subject
$pythonSignerThumbprint = (
  [string]$pythonSignature.SignerCertificate.Thumbprint
).ToUpperInvariant()
$pythonDllSignatureStatus = [string]$pythonDllSignature.Status
$pythonDllSignatureType = [string]$pythonDllSignature.SignatureType
$pythonDllSignerSubject = (
  [string]$pythonDllSignature.SignerCertificate.Subject
)
$pythonDllSignerThumbprint = (
  [string]$pythonDllSignature.SignerCertificate.Thumbprint
).ToUpperInvariant()
$controllerIsolationFlags = @(
  '-E',
  '-s',
  '-S',
  '-B',
  '-X',
  "pycache_prefix=$controllerPycachePrefix",
  '-m'
)
$guardArguments = @($controllerIsolationFlags) + @(
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
  $minimumAvailableRamMiBValue,
  '--timeout-seconds',
  $timeoutValue,
  '--'
) + @($Command)

$previousErrorActionPreference = $ErrorActionPreference
& $pushLocation -LiteralPath $controllerRepositoryRoot
try {
  # Windows PowerShell 5.1 can surface native stderr as non-terminating error
  # records. The native exit code, not ErrorAction, is authoritative here.
  $ErrorActionPreference = 'Continue'
  $guardNativeOutput = @(& $python @guardArguments)
  $guardExitCode = $LASTEXITCODE
}
finally {
  $ErrorActionPreference = $previousErrorActionPreference
  & $popLocation
}
if (
  [IO.File]::Exists($controllerPycachePrefix) -or
  [IO.Directory]::Exists($controllerPycachePrefix)
) {
  throw 'Isolated controller pycache prefix was unexpectedly materialized'
}

$assertJsonObjectShape = {
  param(
    [object]$Value,
    [string]$Name,
    [string[]]$Properties
  )

  if ($null -eq $Value -or $Value -isnot [PSCustomObject]) {
    throw "$Name must be a JSON object"
  }
  $actualProperties = @(
    $Value.PSObject.Properties |
      & $forEachObject { $_.Name }
  )
  if ($actualProperties.Count -ne $Properties.Count) {
    throw "$Name must contain exactly the defined properties"
  }
  foreach ($propertyName in $Properties) {
    if ($actualProperties -cnotcontains $propertyName) {
      throw "$Name is missing required property $propertyName"
    }
  }
}

$assertJsonString = {
  param([object]$Value, [string]$Name)

  if ($Value -isnot [string]) {
    throw "$Name must be a JSON string"
  }
}

$assertJsonBoolean = {
  param([object]$Value, [string]$Name)

  if ($Value -isnot [bool]) {
    throw "$Name must be a JSON boolean"
  }
}

$assertJsonInteger = {
  param([object]$Value, [string]$Name)

  if ($Value -isnot [int] -and $Value -isnot [long]) {
    throw "$Name must be a JSON integer"
  }
}

$assertJsonNumber = {
  param([object]$Value, [string]$Name)

  if (
    $Value -isnot [int] -and
    $Value -isnot [long] -and
    $Value -isnot [decimal] -and
    $Value -isnot [double]
  ) {
    throw "$Name must be a JSON number"
  }
  $number = [double]$Value
  if ([double]::IsNaN($number) -or [double]::IsInfinity($number)) {
    throw "$Name must be a finite JSON number"
  }
}

$assertJsonArray = {
  param([object]$Value, [string]$Name)

  if ($Value -isnot [object[]]) {
    throw "$Name must be a JSON array"
  }
}

$assertJsonNull = {
  param([object]$Value, [string]$Name)

  if ($null -ne $Value) {
    throw "$Name must be JSON null"
  }
}

$controllerResultLines = @($guardNativeOutput)
if (
  $controllerResultLines.Count -ne 1 -or
  [string]::IsNullOrWhiteSpace([string]$controllerResultLines[0])
) {
  throw 'Guard controller must emit exactly one controller result line'
}
$controllerResultText = [string]$controllerResultLines[0]
try {
  $controllerResult = (
    $controllerResultText |
      & $convertFromJson -ErrorAction Stop
  )
}
catch {
  throw 'Controller result is not valid JSON'
}
& $assertJsonObjectShape -Value $controllerResult -Name 'Controller result' `
  -Properties @(
    'schema',
    'record_schema',
    'run_id',
    'evidence_path',
    'evidence_sha256',
    'log_path',
    'log_sha256',
    'actual_exit_code',
    'valid'
  )
& $assertJsonString -Value $controllerResult.schema `
  -Name 'Controller result schema'
if (
  $controllerResult.schema -cne 'official-openvino-controller-result/v1'
) {
  throw "Unexpected controller-result schema: $($controllerResult.schema)"
}
& $assertJsonString -Value $controllerResult.record_schema `
  -Name 'Controller result record_schema'
if (
  $controllerResult.record_schema -cne
    'official-openvino-owned-process-guard/v1'
) {
  throw "Unexpected controller record schema: $($controllerResult.record_schema)"
}
& $assertJsonString -Value $controllerResult.run_id `
  -Name 'Controller result run_id'
if (
  $controllerResult.run_id -cnotmatch '^[0-9a-f]{64}$'
) {
  throw 'Controller result run_id is not a lowercase 256-bit identifier'
}
foreach ($hashField in @('evidence_sha256', 'log_sha256')) {
  $hashValue = $controllerResult.$hashField
  & $assertJsonString -Value $hashValue `
    -Name "Controller result $hashField"
  if ($hashValue -cnotmatch '^[0-9a-f]{64}$') {
    throw "Controller result $hashField is not a lowercase SHA-256"
  }
}
foreach ($pathField in @('evidence_path', 'log_path')) {
  $pathValue = $controllerResult.$pathField
  & $assertJsonString -Value $pathValue `
    -Name "Controller result $pathField"
  if (
    [string]::IsNullOrWhiteSpace($pathValue) -or
    -not [IO.Path]::IsPathRooted($pathValue)
  ) {
    throw "Controller result $pathField is not an absolute path"
  }
}
& $assertJsonInteger -Value $controllerResult.actual_exit_code `
  -Name 'Controller result actual_exit_code'
& $assertJsonBoolean -Value $controllerResult.valid `
  -Name 'Controller result valid'

$controllerEvidencePath = [string]$controllerResult.evidence_path
$controllerLogPath = [string]$controllerResult.log_path
if (-not [IO.File]::Exists($controllerEvidencePath)) {
  throw "Controller-bound evidence is missing: $controllerEvidencePath"
}
if (-not [IO.File]::Exists($controllerLogPath)) {
  throw "Controller-bound log is missing: $controllerLogPath"
}
$canonicalEvidencePath = (
  & $resolvePath -LiteralPath $controllerEvidencePath
).Path
$canonicalLogPath = (
  & $resolvePath -LiteralPath $controllerLogPath
).Path
if (
  -not [StringComparer]::OrdinalIgnoreCase.Equals(
    $controllerEvidencePath,
    $canonicalEvidencePath
  )
) {
  throw 'Controller evidence path is not canonical'
}
if (
  -not [StringComparer]::OrdinalIgnoreCase.Equals(
    $controllerLogPath,
    $canonicalLogPath
  )
) {
  throw 'Controller log path is not canonical'
}

$evidenceBytes = [IO.File]::ReadAllBytes($canonicalEvidencePath)
$sha256 = [Security.Cryptography.SHA256]::Create()
try {
  $evidenceHashBytes = $sha256.ComputeHash($evidenceBytes)
}
finally {
  $sha256.Dispose()
}
$observedEvidenceHash = -join @(
  $evidenceHashBytes |
    & $forEachObject { $_.ToString('x2') }
)
if ($observedEvidenceHash -cne $controllerResult.evidence_sha256) {
  throw 'Controller-bound evidence SHA-256 does not match'
}
$utf8 = & $newObject Text.UTF8Encoding($false, $true)
try {
  $recordText = $utf8.GetString($evidenceBytes)
  $record = $recordText |
    & $convertFromJson -ErrorAction Stop
}
catch {
  throw 'Controller-bound evidence is not valid UTF-8 JSON'
}

$observedLogHash = & $getSha256Hex -Path $canonicalLogPath
if ($observedLogHash -cne $controllerResult.log_sha256) {
  throw 'Controller-bound log SHA-256 does not match'
}

& $assertJsonObjectShape -Value $record -Name 'Guard evidence record' `
  -Properties @(
    'schema',
    'run_id',
    'command',
    'working_directory',
    'log_path',
    'evidence_path',
    'requested_path_provenance',
    'path_identity_verified',
    'expected_exit',
    'started_utc',
    'ended_utc',
    'elapsed_seconds',
    'configured_minimum_available_ram_bytes',
    'poll_interval_seconds',
    'cleanup_timeout_seconds',
    'maximum_runtime_seconds',
    'observed_available_ram_bytes',
    'memory_sample_count',
    'peak_working_set_bytes',
    'peak_private_bytes',
    'memory_query_failed_pids',
    'root_pid',
    'observed_pids',
    'child_process_observed',
    'exit_code',
    'timed_out',
    'low_memory_stop',
    'termination_reason',
    'msbuild_disable_node_reuse',
    'environment_sha256',
    'bound_inputs',
    'launch_governance',
    'containing_job_assignment',
    'job_object',
    'cleanup_process_count',
    'emergency_actions',
    'validation_errors',
    'log_sha256',
    'valid'
  )

& $assertJsonString -Value $record.schema `
  -Name 'Guard evidence record schema'
if ($record.schema -cne 'official-openvino-owned-process-guard/v1') {
  throw "Unexpected guard schema: $($record.schema)"
}
& $assertJsonString -Value $record.environment_sha256 `
  -Name 'Guard environment_sha256'
if ($record.environment_sha256 -cnotmatch '^[0-9a-f]{64}$') {
  throw 'Guard environment_sha256 is not a lowercase SHA-256'
}
& $assertJsonArray -Value $record.bound_inputs -Name 'Guard bound_inputs'
if ($record.bound_inputs.Count -ne 0) {
  throw 'Guard wrapper does not accept bound inputs'
}
& $assertJsonString -Value $record.run_id -Name 'Guard run_id'
if ($record.run_id -cnotmatch '^[0-9a-f]{64}$') {
  throw 'Guard run_id is not a lowercase 256-bit identifier'
}
if (
  -not [StringComparer]::Ordinal.Equals(
    $record.run_id,
    $controllerResult.run_id
  )
) {
  throw 'Controller run_id does not match the bound evidence record'
}

& $assertJsonArray -Value $record.command -Name 'Guard command'
$recordCommand = $record.command
for ($index = 0; $index -lt $recordCommand.Count; $index++) {
  & $assertJsonString -Value $recordCommand[$index] `
    -Name "Guard command argv[$index]"
}
if ($recordCommand.Count -ne $Command.Count) {
  throw 'Guard command argv count does not match the requested argv'
}
for ($index = 0; $index -lt $Command.Count; $index++) {
  if (
    -not [StringComparer]::Ordinal.Equals(
      $recordCommand[$index],
      $Command[$index]
    )
  ) {
    throw "Guard command argv mismatch at index $index"
  }
}

foreach ($pathField in @('working_directory', 'log_path', 'evidence_path')) {
  $recordPathValue = $record.$pathField
  & $assertJsonString -Value $recordPathValue -Name "Guard $pathField"
  if (
    [string]::IsNullOrWhiteSpace($recordPathValue) -or
    -not [IO.Path]::IsPathRooted($recordPathValue)
  ) {
    throw "Guard $pathField is not an absolute path"
  }
}
if (
  -not [StringComparer]::OrdinalIgnoreCase.Equals(
    $record.evidence_path,
    $canonicalEvidencePath
  )
) {
  throw 'Controller evidence path does not match the bound evidence record'
}
if (
  -not [StringComparer]::OrdinalIgnoreCase.Equals(
    $record.log_path,
    $canonicalLogPath
  )
) {
  throw 'Controller log path does not match the bound evidence record'
}

& $assertJsonObjectShape -Value $record.requested_path_provenance `
  -Name 'Guard requested_path_provenance' `
  -Properties @('working_directory', 'log_path', 'evidence_path')
foreach ($pathField in @('working_directory', 'log_path', 'evidence_path')) {
  & $assertJsonString `
    -Value $record.requested_path_provenance.$pathField `
    -Name "Guard requested_path_provenance.$pathField"
}
if (
  -not [StringComparer]::OrdinalIgnoreCase.Equals(
    $record.requested_path_provenance.working_directory,
    $resolvedWorkingDirectory
  )
) {
  throw 'Guard requested working-directory spelling does not match the request'
}
if (
  -not [StringComparer]::OrdinalIgnoreCase.Equals(
    $record.requested_path_provenance.log_path,
    $logPath
  )
) {
  throw 'Guard requested log-path spelling does not match the request'
}
if (
  -not [StringComparer]::OrdinalIgnoreCase.Equals(
    $record.requested_path_provenance.evidence_path,
    $evidencePath
  )
) {
  throw 'Guard requested evidence-path spelling does not match the request'
}

& $assertJsonObjectShape -Value $record.path_identity_verified `
  -Name 'Guard path_identity_verified' `
  -Properties @('working_directory', 'log_path', 'evidence_path')
foreach ($pathField in @('working_directory', 'log_path', 'evidence_path')) {
  $identityValue = $record.path_identity_verified.$pathField
  & $assertJsonBoolean -Value $identityValue `
    -Name "Guard path_identity_verified.$pathField"
  if ($identityValue -ne $true) {
    throw "Guard did not prove $pathField filesystem identity"
  }
}

& $assertJsonString -Value $record.expected_exit `
  -Name 'Guard expected_exit'
if ($record.expected_exit -cne $expectedExitValue) {
  throw "Guard expected-exit mismatch: $($record.expected_exit)"
}
foreach ($timeField in @('started_utc', 'ended_utc')) {
  & $assertJsonString -Value $record.$timeField -Name "Guard $timeField"
  if ([string]::IsNullOrWhiteSpace($record.$timeField)) {
    throw "Guard $timeField must not be empty"
  }
}
foreach (
  $numberField in @(
    'elapsed_seconds',
    'poll_interval_seconds',
    'cleanup_timeout_seconds',
    'maximum_runtime_seconds'
  )
) {
  & $assertJsonNumber -Value $record.$numberField -Name "Guard $numberField"
}
if ([double]$record.elapsed_seconds -lt 0.0) {
  throw 'Guard elapsed_seconds must be non-negative'
}
if ([double]$record.poll_interval_seconds -ne 0.25) {
  throw 'Guard poll_interval_seconds does not match the configured value'
}
if ([double]$record.cleanup_timeout_seconds -ne 15.0) {
  throw 'Guard cleanup_timeout_seconds does not match the configured value'
}
if ([double]$record.maximum_runtime_seconds -ne $TimeoutSeconds) {
  throw "Guard timeout mismatch: $($record.maximum_runtime_seconds)"
}

& $assertJsonInteger `
  -Value $record.configured_minimum_available_ram_bytes `
  -Name 'Guard configured_minimum_available_ram_bytes'
$expectedConfiguredRamFloor = [int64]$MinimumAvailableRamMiB * 1MB
if ([int64]$record.configured_minimum_available_ram_bytes -ne
    $expectedConfiguredRamFloor) {
  throw 'Guard configured RAM floor does not match the requested value'
}
$configuredRamFloor = [int64]$record.configured_minimum_available_ram_bytes
& $assertJsonObjectShape -Value $record.observed_available_ram_bytes `
  -Name 'Guard observed_available_ram_bytes' `
  -Properties @('before', 'minimum', 'after')
foreach ($sampleField in @('before', 'minimum', 'after')) {
  $sampleValue = $record.observed_available_ram_bytes.$sampleField
  & $assertJsonInteger -Value $sampleValue `
    -Name "Guard observed_available_ram_bytes.$sampleField"
  if ([int64]$sampleValue -lt 0) {
    throw "Guard observed_available_ram_bytes.$sampleField is negative"
  }
}
$observedRamMinimum = (
  [int64]$record.observed_available_ram_bytes.minimum
)
if ($observedRamMinimum -lt $configuredRamFloor) {
  throw 'Guard observed RAM fell below the configured floor'
}
if (
  $observedRamMinimum -gt
    [int64]$record.observed_available_ram_bytes.before -or
  $observedRamMinimum -gt
    [int64]$record.observed_available_ram_bytes.after
) {
  throw 'Guard observed minimum RAM is inconsistent with endpoint samples'
}

foreach (
  $integerField in @(
    'memory_sample_count',
    'peak_working_set_bytes',
    'peak_private_bytes',
    'root_pid',
    'exit_code',
    'cleanup_process_count'
  )
) {
  & $assertJsonInteger `
    -Value $record.$integerField `
    -Name "Guard $integerField"
}
if ([int64]$record.cleanup_process_count -ne 0) {
  throw 'Guard cleanup_process_count must be zero'
}
if (
  [int64]$record.memory_sample_count -lt 1 -or
  [int64]$record.peak_working_set_bytes -le 0 -or
  [int64]$record.peak_private_bytes -le 0
) {
  throw 'Guard did not record complete Job-PID memory samples and peaks'
}
if ([int64]$record.root_pid -le 0) {
  throw 'Guard root_pid must be a positive integer'
}
foreach (
  $pidArrayField in @('memory_query_failed_pids', 'observed_pids')
) {
  $pidArray = $record.$pidArrayField
  & $assertJsonArray -Value $pidArray -Name "Guard $pidArrayField"
  for ($index = 0; $index -lt $pidArray.Count; $index++) {
    & $assertJsonInteger -Value $pidArray[$index] `
      -Name "Guard $pidArrayField[$index]"
    if ([int64]$pidArray[$index] -le 0) {
      throw "Guard $pidArrayField[$index] must be a positive integer"
    }
  }
}
if ($record.memory_query_failed_pids.Count -ne 0) {
  throw 'Guard memory_query_failed_pids must be empty'
}
if (
  $record.observed_pids -notcontains [int64]$record.root_pid
) {
  throw 'Guard observed_pids does not contain root_pid'
}
& $assertJsonBoolean -Value $record.child_process_observed `
  -Name 'Guard child_process_observed'

$actualExitCode = [int64]$record.exit_code
if ($actualExitCode -ne [int64]$controllerResult.actual_exit_code) {
  throw 'Controller actual exit does not match the bound evidence record'
}
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

foreach ($booleanField in @('timed_out', 'low_memory_stop', 'valid')) {
  & $assertJsonBoolean -Value $record.$booleanField `
    -Name "Guard $booleanField"
}
if ($record.valid -ne $controllerResult.valid) {
  throw 'Controller validity does not match the bound evidence record'
}
& $assertJsonNull -Value $record.termination_reason `
  -Name 'Guard termination_reason'
if ($record.timed_out -ne $false -or $record.low_memory_stop -ne $false) {
  throw 'Guarded command reported timeout or low memory'
}

& $assertJsonString -Value $record.msbuild_disable_node_reuse `
  -Name 'Guard msbuild_disable_node_reuse'
if ($record.msbuild_disable_node_reuse -cne '1') {
  throw 'MSBUILDDISABLENODEREUSE was not forced to 1'
}

& $assertJsonObjectShape -Value $record.launch_governance `
  -Name 'Guard launch_governance' `
  -Properties @(
    'created_suspended',
    'assigned_before_resume',
    'cpu_affinity_mask',
    'cpu_rate_hard_cap_percent'
  )
foreach (
  $launchBooleanField in @('created_suspended', 'assigned_before_resume')
) {
  & $assertJsonBoolean `
    -Value $record.launch_governance.$launchBooleanField `
    -Name "Guard launch_governance.$launchBooleanField"
}
if (
  $record.launch_governance.created_suspended -ne $true -or
  $record.launch_governance.assigned_before_resume -ne $true
) {
  throw 'Guard did not prove suspended launch and assignment before resume'
}
& $assertJsonNull `
  -Value $record.launch_governance.cpu_affinity_mask `
  -Name 'Guard launch_governance.cpu_affinity_mask'
& $assertJsonNull `
  -Value $record.launch_governance.cpu_rate_hard_cap_percent `
  -Name 'Guard launch_governance.cpu_rate_hard_cap_percent'

& $assertJsonObjectShape `
  -Value $record.containing_job_assignment `
  -Name 'Guard containing_job_assignment' `
  -Properties @(
    'requested',
    'assigned_before_fine_job',
    'assigned_pid',
    'query_ok_after_cleanup',
    'active_pids_after_cleanup'
  )
foreach (
  $containingBooleanField in @('requested', 'assigned_before_fine_job')
) {
  & $assertJsonBoolean `
    -Value $record.containing_job_assignment.$containingBooleanField `
    -Name "Guard containing_job_assignment.$containingBooleanField"
}
if (
  $record.containing_job_assignment.requested -ne $false -or
  $record.containing_job_assignment.assigned_before_fine_job -ne $false
) {
  throw 'Standalone guard unexpectedly used a containing Job Object'
}
foreach (
  $containingNullField in @(
    'assigned_pid',
    'query_ok_after_cleanup',
    'active_pids_after_cleanup'
  )
) {
  & $assertJsonNull `
    -Value $record.containing_job_assignment.$containingNullField `
    -Name "Guard containing_job_assignment.$containingNullField"
}

& $assertJsonObjectShape `
  -Value $record.job_object `
  -Name 'Guard job_object' `
  -Properties @(
    'setup_ok',
    'query_ok',
    'terminate_job_called',
    'queried_active_process_count_after_cleanup',
    'survivor_pids_after_cleanup'
  )
foreach (
  $jobBooleanField in @('setup_ok', 'query_ok', 'terminate_job_called')
) {
  & $assertJsonBoolean -Value $record.job_object.$jobBooleanField `
    -Name "Guard job_object.$jobBooleanField"
}
if ($record.job_object.setup_ok -ne $true) {
  throw 'Guard did not prove Job Object setup'
}
if ($record.job_object.query_ok -ne $true) {
  throw 'Guard did not query the Job Object after cleanup'
}
& $assertJsonInteger `
  -Value $record.job_object.queried_active_process_count_after_cleanup `
  -Name 'Guard job_object.queried_active_process_count_after_cleanup'
if (
  [int64]$record.job_object.queried_active_process_count_after_cleanup -ne 0
) {
  throw 'Guard reported active processes after cleanup'
}
& $assertJsonArray `
  -Value $record.job_object.survivor_pids_after_cleanup `
  -Name 'Guard job_object.survivor_pids_after_cleanup'
if ($record.job_object.survivor_pids_after_cleanup.Count -ne 0) {
  throw 'Guard reported survivor PIDs after cleanup'
}

foreach ($emptyArrayField in @('emergency_actions', 'validation_errors')) {
  & $assertJsonArray -Value $record.$emptyArrayField `
    -Name "Guard $emptyArrayField"
  if ($record.$emptyArrayField.Count -ne 0) {
    throw "Guard $emptyArrayField must be empty"
  }
}

& $assertJsonString -Value $record.log_sha256 -Name 'Guard log_sha256'
if ($record.log_sha256 -cnotmatch '^[0-9a-f]{64}$') {
  throw 'Guard log_sha256 is not a lowercase SHA-256'
}
if ($record.log_sha256 -cne $observedLogHash) {
  throw 'Guard log SHA-256 does not match the controller-bound log'
}

if (
  $record.valid -ne $true -or
  $controllerResult.valid -ne $true -or
  $guardExitCode -ne 0
) {
  $failures = $record.validation_errors -join '; '
  throw "Guarded command failed validation: $failures"
}

$verification = [ordered]@{
  schema = 'official-openvino-wrapper-verification/v1'
  run_id = [string]$record.run_id
  command = @($Command)
  working_directory = [string]$record.working_directory
  log_path = [string]$record.log_path
  evidence_path = [string]$record.evidence_path
  requested_path_provenance = [ordered]@{
    working_directory = (
      [string]$record.requested_path_provenance.working_directory
    )
    log_path = [string]$record.requested_path_provenance.log_path
    evidence_path = [string]$record.requested_path_provenance.evidence_path
  }
  path_identity_verified = [ordered]@{
    working_directory = (
      [bool]$record.path_identity_verified.working_directory
    )
    log_path = [bool]$record.path_identity_verified.log_path
    evidence_path = [bool]$record.path_identity_verified.evidence_path
  }
  expected_exit = $expectedExitValue
  actual_exit_code = $actualExitCode
  log_sha256 = $observedLogHash
  evidence_sha256 = $observedEvidenceHash
  controller_runtime = [ordered]@{
    trusted_install_root = $pythonInstallRoot
    interpreter_path = $python
    interpreter_sha256 = $pythonHash
    signature_status = $pythonSignatureStatus
    signature_type = $pythonSignatureType
    signer_subject = $pythonSignerSubject
    signer_thumbprint = $pythonSignerThumbprint
    runtime_dll_path = $pythonDll
    runtime_dll_sha256 = $pythonDllHash
    runtime_dll_signature_status = $pythonDllSignatureStatus
    runtime_dll_signature_type = $pythonDllSignatureType
    runtime_dll_signer_subject = $pythonDllSignerSubject
    runtime_dll_signer_thumbprint = $pythonDllSignerThumbprint
    isolation_flags = @($controllerIsolationFlags)
  }
  controller_binding = [ordered]@{
    schema = [string]$controllerResult.schema
    record_schema = [string]$controllerResult.record_schema
    run_id = [string]$controllerResult.run_id
    evidence_path = $canonicalEvidencePath
    evidence_sha256 = $observedEvidenceHash
    log_path = $canonicalLogPath
    log_sha256 = $observedLogHash
    actual_exit_code = [int64]$controllerResult.actual_exit_code
    valid = [bool]$controllerResult.valid
  }
  valid = $true
}
$verificationJson = (
  $verification |
    & $convertToJson -Depth 8 -Compress
)
if ($null -ne $verificationPath) {
  $verificationTemporaryPath = [IO.Path]::Combine(
    $resolvedEvidenceRoot,
    (
      '.' + $Label + '.verification.' +
      [Guid]::NewGuid().ToString('N') + '.tmp'
    )
  )
  try {
    $utf8NoBom = [Text.UTF8Encoding]::new($false)
    $stream = [IO.FileStream]::new(
      $verificationTemporaryPath,
      [IO.FileMode]::CreateNew,
      [IO.FileAccess]::Write,
      [IO.FileShare]::None
    )
    try {
      $writer = [IO.StreamWriter]::new($stream, $utf8NoBom)
      try {
        $writer.Write($verificationJson)
        $writer.Flush()
        $stream.Flush($true)
      } finally {
        $writer.Dispose()
      }
    } finally {
      $stream.Dispose()
    }
    [IO.File]::Move($verificationTemporaryPath, $verificationPath)
  } finally {
    if ([IO.File]::Exists($verificationTemporaryPath)) {
      [IO.File]::Delete($verificationTemporaryPath)
    }
  }
  $persistedVerification = [IO.File]::ReadAllText(
    $verificationPath,
    [Text.UTF8Encoding]::new($false, $true)
  )
  if (-not [StringComparer]::Ordinal.Equals(
      $persistedVerification,
      $verificationJson)) {
    throw 'Persisted wrapper verification does not match stdout'
  }
}
& $writeOutput $verificationJson
