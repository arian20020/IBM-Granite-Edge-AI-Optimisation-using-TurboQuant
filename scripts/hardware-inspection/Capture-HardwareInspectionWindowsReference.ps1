[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $CandidateRoot,

    [Parameter(Mandatory = $true)]
    [string] $OutputRoot
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

if ($null -eq ('HardwareInspectionCaptureNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

public static class HardwareInspectionCaptureNative
{
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint GetFinalPathNameByHandle(
        SafeFileHandle file,
        StringBuilder filePath,
        uint filePathCharacters,
        uint flags);
}
'@
}

function Test-ReparsePoint {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)

    $attributes = [System.IO.File]::GetAttributes($LiteralPath)
    return (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)
}

function Get-DirectoryPrefix {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)

    $separators = [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    return $LiteralPath.TrimEnd($separators) + [System.IO.Path]::DirectorySeparatorChar
}

function Test-SameOrDescendant {
    param(
        [Parameter(Mandatory = $true)][string] $Parent,
        [Parameter(Mandatory = $true)][string] $Candidate
    )

    return [string]::Equals($Parent, $Candidate, [System.StringComparison]::OrdinalIgnoreCase) -or
        $Candidate.StartsWith(
            (Get-DirectoryPrefix -LiteralPath $Parent),
            [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-LocalPath {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)

    if ([string]::IsNullOrWhiteSpace($LiteralPath) -or
        $LiteralPath.StartsWith('\\', [System.StringComparison]::Ordinal) -or
        $LiteralPath.StartsWith('\\?\', [System.StringComparison]::Ordinal) -or
        $LiteralPath.StartsWith('\\.\', [System.StringComparison]::Ordinal)) {
        throw 'The capture accepts only ordinary local paths.'
    }
}

function Get-StableDirectoryPath {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)

    $handle = [HardwareInspectionCaptureNative]::CreateFile(
        $LiteralPath,
        [uint32]0,
        [uint32]7,
        [System.IntPtr]::Zero,
        [uint32]3,
        [uint32]0x02000000,
        [System.IntPtr]::Zero)
    if ($handle.IsInvalid) {
        $handle.Dispose()
        throw 'A directory could not be opened for stable identity validation.'
    }

    try {
        $capacity = 32768
        $builder = New-Object System.Text.StringBuilder($capacity)
        $length = [HardwareInspectionCaptureNative]::GetFinalPathNameByHandle(
            $handle,
            $builder,
            [uint32]$capacity,
            [uint32]0)
        if ($length -eq 0 -or $length -ge $capacity) {
            throw 'A stable directory identity could not be resolved.'
        }

        $resolved = $builder.ToString()
        if ($resolved.StartsWith('\\?\UNC\', [System.StringComparison]::OrdinalIgnoreCase)) {
            $resolved = '\\' + $resolved.Substring(8)
        }
        elseif ($resolved.StartsWith('\\?\', [System.StringComparison]::OrdinalIgnoreCase)) {
            $resolved = $resolved.Substring(4)
        }

        Assert-LocalPath -LiteralPath $resolved
        return [System.IO.Path]::GetFullPath($resolved).TrimEnd(
            [char[]]@(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar))
    }
    finally {
        $handle.Dispose()
    }
}

function Assert-ExistingPathChainIsOrdinary {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string[]] $Segments
    )

    $current = [System.IO.Path]::GetFullPath($Root)
    if (-not [System.IO.Directory]::Exists($current) -or (Test-ReparsePoint -LiteralPath $current)) {
        throw 'The validated path root is missing or is a reparse point.'
    }

    foreach ($segment in $Segments) {
        $current = [System.IO.Path]::GetFullPath(
            [System.IO.Path]::Combine($current, $segment))
        if ([System.IO.Directory]::Exists($current) -or [System.IO.File]::Exists($current)) {
            if (Test-ReparsePoint -LiteralPath $current) {
                throw 'The validated path chain contains a reparse point.'
            }
        }
        else {
            break
        }
    }
}

function Get-StrictUtf8Text {
    param(
        [Parameter(Mandatory = $true)][string] $LiteralPath,
        [Parameter(Mandatory = $true)][int] $MaximumBytes
    )

    $stream = [System.IO.File]::Open(
        $LiteralPath,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::Read)
    try {
        if ($stream.Length -le 0 -or $stream.Length -gt $MaximumBytes) {
            throw 'A JSON artifact has an invalid byte length.'
        }

        $bytes = New-Object byte[] ([int]$stream.Length)
        $offset = 0
        while ($offset -lt $bytes.Length) {
            $read = $stream.Read($bytes, $offset, $bytes.Length - $offset)
            if ($read -le 0) {
                throw 'A JSON artifact ended unexpectedly.'
            }

            $offset += $read
        }

        if ($stream.ReadByte() -ne -1) {
            throw 'A JSON artifact changed while it was read.'
        }

        if ($bytes.Length -ge 3 -and
            $bytes[0] -eq 0xEF -and
            $bytes[1] -eq 0xBB -and
            $bytes[2] -eq 0xBF) {
            throw 'UTF-8 byte-order marks are forbidden.'
        }

        $encoding = New-Object System.Text.UTF8Encoding($false, $true)
        return $encoding.GetString($bytes)
    }
    finally {
        $stream.Dispose()
    }
}

function Get-Sha256Lower {
    param(
        [Parameter(Mandatory = $true)][string] $LiteralPath,
        [Parameter(Mandatory = $true)][long] $MaximumBytes
    )

    $stream = [System.IO.File]::Open(
        $LiteralPath,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::Read)
    try {
        if ($stream.Length -le 0 -or $stream.Length -gt $MaximumBytes) {
            throw 'A hash source has an invalid byte length.'
        }

        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            $bytes = $sha256.ComputeHash($stream)
        }
        finally {
            $sha256.Dispose()
        }

        return (($bytes | ForEach-Object { $_.ToString('x2') }) -join '')
    }
    finally {
        $stream.Dispose()
    }
}

function Get-RequiredPropertyValue {
    param(
        [Parameter(Mandatory = $true)] $InputObject,
        [Parameter(Mandatory = $true)][string] $Name
    )

    $properties = @($InputObject.PSObject.Properties | Where-Object { $_.Name -ceq $Name })
    if ($properties.Count -ne 1) {
        throw 'A required JSON property is missing or ambiguous.'
    }

    return $properties[0].Value
}

function Convert-ToUInt64 {
    param([Parameter(Mandatory = $true)] $Value)

    try {
        $converted = [System.Convert]::ToUInt64(
            $Value,
            [System.Globalization.CultureInfo]::InvariantCulture)
    }
    catch {
        throw 'A Windows numeric observation is invalid.'
    }

    return $converted
}

function Get-SafeHardwareNames {
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]] $Values)

    $names = New-Object System.Collections.Generic.List[string]
    $normalized = @{}
    foreach ($value in $Values) {
        if ($value -isnot [string] -or
            [string]::IsNullOrWhiteSpace([string]$value) -or
            ([string]$value).Length -gt 256 -or
            [regex]::IsMatch([string]$value, '[\x00-\x1F\x7F]')) {
            throw 'A Windows hardware name is invalid.'
        }

        $name = ([string]$value).Trim()
        $identity = Get-NormalizedHardwareIdentity -Value $name
        if (-not $normalized.ContainsKey($identity)) {
            $normalized.Add($identity, $true)
            $names.Add($name)
        }
    }

    return @($names.ToArray() | Sort-Object)
}

function Get-NormalizedHardwareIdentity {
    param([Parameter(Mandatory = $true)][string] $Value)

    $normalized = $Value.Normalize([System.Text.NormalizationForm]::FormKC)
    $normalized = [regex]::Replace(
        $normalized,
        '\((?:R|TM)\)|[®™]',
        ' ',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
            [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    $normalized = [regex]::Replace($normalized, '[^\p{L}\p{Nd}]+', ' ')
    $normalized = [regex]::Replace($normalized, '\s+', ' ').Trim()
    return $normalized.ToUpperInvariant()
}

function Test-IntelToken {
    param([Parameter(Mandatory = $true)][string] $Value)

    return @((Get-NormalizedHardwareIdentity -Value $Value).Split(' ')) -ccontains 'INTEL'
}

function Get-ValidatedUtcTimestamp {
    param([Parameter(Mandatory = $true)] $Value)

    if ($Value -is [System.DateTime]) {
        if ($Value.Kind -ne [System.DateTimeKind]::Utc) {
            throw 'A gate timestamp is not an explicit UTC instant.'
        }

        return [System.DateTimeOffset]$Value
    }

    if ($Value -is [System.DateTimeOffset]) {
        if ($Value.Offset -ne [System.TimeSpan]::Zero) {
            throw 'A gate timestamp is not an explicit UTC instant.'
        }

        return $Value
    }

    if ($Value -isnot [string] -or
        ([string]$Value -cnotmatch '(?:Z|\+00:00)$')) {
        throw 'A gate timestamp is not an explicit UTC string.'
    }

    $parsed = [System.DateTimeOffset]::MinValue
    if (-not [System.DateTimeOffset]::TryParse(
            [string]$Value,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::RoundtripKind,
            [ref]$parsed) -or
        $parsed.Offset -ne [System.TimeSpan]::Zero) {
        throw 'A gate timestamp is invalid.'
    }

    return $parsed
}

function Write-AtomicJson {
    param(
        [Parameter(Mandatory = $true)] $Value,
        [Parameter(Mandatory = $true)][string] $Destination
    )

    if ([System.IO.File]::Exists($Destination) -or [System.IO.Directory]::Exists($Destination)) {
        throw 'The Windows reference destination already exists.'
    }

    $json = $Value | ConvertTo-Json -Depth 5
    $encoding = New-Object System.Text.UTF8Encoding($false, $true)
    $bytes = $encoding.GetBytes($json)
    $temporary = $Destination + '.tmp-' + [System.Guid]::NewGuid().ToString('N')
    try {
        $stream = New-Object System.IO.FileStream(
            $temporary,
            [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write,
            [System.IO.FileShare]::None,
            4096,
            [System.IO.FileOptions]::WriteThrough)
        try {
            $stream.Write($bytes, 0, $bytes.Length)
            $stream.Flush($true)
        }
        finally {
            $stream.Dispose()
        }

        if ([System.IO.File]::Exists($Destination) -or [System.IO.Directory]::Exists($Destination)) {
            throw 'The Windows reference destination changed before publication.'
        }

        [System.IO.File]::Move($temporary, $Destination)
    }
    finally {
        if ([System.IO.File]::Exists($temporary)) {
            [System.IO.File]::Delete($temporary)
        }
    }
}

$scriptRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$repositoryRoot = [System.IO.Path]::GetFullPath(
    [System.IO.Path]::Combine($scriptRoot, '..', '..'))
$projectPath = [System.IO.Path]::Combine(
    $repositoryRoot,
    'tools',
    'HardwareInspection.LlmFitSpike',
    'HardwareInspection.LlmFitSpike.csproj')
$globalJsonPath = [System.IO.Path]::Combine($repositoryRoot, 'global.json')
$manifestPath = [System.IO.Path]::Combine(
    $repositoryRoot,
    'tools',
    'HardwareInspection.LlmFitSpike',
    'Candidates',
    'llmfit-v1.1.9-win-x64.json')
if (-not [System.IO.File]::Exists($projectPath) -or
    -not [System.IO.File]::Exists($globalJsonPath) -or
    -not [System.IO.File]::Exists($manifestPath) -or
    (Test-ReparsePoint -LiteralPath $projectPath) -or
    (Test-ReparsePoint -LiteralPath $globalJsonPath) -or
    (Test-ReparsePoint -LiteralPath $manifestPath)) {
    throw 'The script location does not resolve to the fixed Gate 1 repository files.'
}

Assert-LocalPath -LiteralPath $CandidateRoot
Assert-LocalPath -LiteralPath $OutputRoot
if (-not [System.IO.Directory]::Exists($CandidateRoot) -or
    [System.IO.File]::Exists($CandidateRoot) -or
    (Test-ReparsePoint -LiteralPath $CandidateRoot)) {
    throw 'CandidateRoot must be an existing ordinary directory.'
}

$canonicalCandidateRoot = [System.IO.Path]::GetFullPath(
    (Get-Item -LiteralPath $CandidateRoot -Force).FullName).TrimEnd(
        [char[]]@(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar))
$canonicalOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot).TrimEnd(
    [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar))
$approvedOutputParent = [System.IO.Path]::GetFullPath(
    [System.IO.Path]::Combine(
        $repositoryRoot,
        'artifacts',
        'hardware-inspection',
        'llmfit')).TrimEnd(
            [char[]]@(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar))
$outputParent = [System.IO.Path]::GetDirectoryName($canonicalOutputRoot)
$outputLeaf = [System.IO.Path]::GetFileName($canonicalOutputRoot)
if (-not [string]::Equals(
        $outputParent,
        $approvedOutputParent,
        [System.StringComparison]::OrdinalIgnoreCase) -or
    $outputLeaf -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,95}$' -or
    $outputLeaf -eq '.' -or
    $outputLeaf -eq '..') {
    throw 'OutputRoot must be a uniquely named direct child of artifacts/hardware-inspection/llmfit.'
}

if ([System.IO.Directory]::Exists($canonicalOutputRoot) -or
    [System.IO.File]::Exists($canonicalOutputRoot)) {
    throw 'OutputRoot must not already exist.'
}

Assert-ExistingPathChainIsOrdinary `
    -Root $repositoryRoot `
    -Segments @('artifacts', 'hardware-inspection', 'llmfit', $outputLeaf)
$stableRepositoryRoot = Get-StableDirectoryPath -LiteralPath $repositoryRoot
$stableCandidateRoot = Get-StableDirectoryPath -LiteralPath $canonicalCandidateRoot
$stableOutputRoot = [System.IO.Path]::GetFullPath(
    [System.IO.Path]::Combine(
        $stableRepositoryRoot,
        'artifacts',
        'hardware-inspection',
        'llmfit',
        $outputLeaf)).TrimEnd(
            [char[]]@(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar))
if ((Test-SameOrDescendant -Parent $stableCandidateRoot -Candidate $stableOutputRoot) -or
    (Test-SameOrDescendant -Parent $stableOutputRoot -Candidate $stableCandidateRoot)) {
    throw 'CandidateRoot and OutputRoot are not physically disjoint.'
}

$dotnetCommand = Get-Command -Name 'dotnet.exe' -CommandType Application -ErrorAction Stop
$dotnetPath = [System.IO.Path]::GetFullPath($dotnetCommand.Source)
if (-not [System.IO.File]::Exists($dotnetPath) -or
    (Test-ReparsePoint -LiteralPath $dotnetPath) -or
    -not [string]::Equals(
        [System.IO.Path]::GetFileName($dotnetPath),
        'dotnet.exe',
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The .NET host is unavailable or unsafe.'
}

$capturedBefore = [System.DateTimeOffset]::UtcNow
$processors = @(Get-CimInstance `
    -ClassName Win32_Processor `
    -Property Name, NumberOfLogicalProcessors)
$computerBeforeRows = @(Get-CimInstance `
    -ClassName Win32_ComputerSystem `
    -Property TotalPhysicalMemory)
$operatingSystemBeforeRows = @(Get-CimInstance `
    -ClassName Win32_OperatingSystem `
    -Property FreePhysicalMemory)
$videoControllers = @(Get-CimInstance `
    -ClassName Win32_VideoController `
    -Property Name)
if ($processors.Count -eq 0 -or
    $computerBeforeRows.Count -ne 1 -or
    $operatingSystemBeforeRows.Count -ne 1 -or
    $videoControllers.Count -eq 0) {
    throw 'The approved Windows reference fields were not available.'
}

$windowsCpuNames = Get-SafeHardwareNames -Values @(
    $processors | ForEach-Object { $_.Name })
$windowsGpuNames = Get-SafeHardwareNames -Values @(
    $videoControllers | ForEach-Object { $_.Name })
$logicalProcessorTotal = [uint64]0
foreach ($processor in $processors) {
    $logicalProcessorTotal += Convert-ToUInt64 -Value $processor.NumberOfLogicalProcessors
}

if ($logicalProcessorTotal -le 0 -or $logicalProcessorTotal -gt [int]::MaxValue) {
    throw 'The Windows logical processor total is invalid.'
}

$totalPhysicalMemoryBefore = Convert-ToUInt64 `
    -Value $computerBeforeRows[0].TotalPhysicalMemory
$freePhysicalMemoryBefore = Convert-ToUInt64 `
    -Value $operatingSystemBeforeRows[0].FreePhysicalMemory
$windowsX64 = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() -eq 'X64'
$windowsIntelCpuObserved = @($windowsCpuNames | Where-Object {
        Test-IntelToken -Value $_
    }).Count -gt 0
$windowsIntelGpuObserved = @($windowsGpuNames | Where-Object {
        Test-IntelToken -Value $_
    }).Count -gt 0
if (-not $windowsX64 -or
    -not $windowsIntelCpuObserved -or
    -not $windowsIntelGpuObserved) {
    throw 'HI-GATE1-WRONG-TARGET: requires Windows x64 with Intel CPU and Intel graphics.'
}

& $dotnetPath `
    run `
    --project $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --no-restore `
    --no-build `
    -- `
    --candidate-root $canonicalCandidateRoot `
    --output $canonicalOutputRoot
$gateExitCode = $LASTEXITCODE

$computerAfterRows = @(Get-CimInstance `
    -ClassName Win32_ComputerSystem `
    -Property TotalPhysicalMemory)
$operatingSystemAfterRows = @(Get-CimInstance `
    -ClassName Win32_OperatingSystem `
    -Property FreePhysicalMemory)
$capturedAfter = [System.DateTimeOffset]::UtcNow
if ($computerAfterRows.Count -ne 1 -or $operatingSystemAfterRows.Count -ne 1) {
    throw 'The repeated Windows RAM reference fields were not available.'
}

$totalPhysicalMemoryAfter = Convert-ToUInt64 `
    -Value $computerAfterRows[0].TotalPhysicalMemory
$freePhysicalMemoryAfter = Convert-ToUInt64 `
    -Value $operatingSystemAfterRows[0].FreePhysicalMemory
if ($totalPhysicalMemoryBefore -le 0 -or
    $totalPhysicalMemoryBefore -ne $totalPhysicalMemoryAfter -or
    $freePhysicalMemoryBefore -gt ($totalPhysicalMemoryBefore / 1024) -or
    $freePhysicalMemoryAfter -gt ($totalPhysicalMemoryAfter / 1024)) {
    throw 'The repeated Windows RAM reference fields are inconsistent.'
}

if ($gateExitCode -ne 0) {
    exit $gateExitCode
}

if (-not [System.IO.Directory]::Exists($canonicalOutputRoot) -or
    (Test-ReparsePoint -LiteralPath $canonicalOutputRoot)) {
    throw 'The Gate 1 output directory was not created safely.'
}

$gateOutputEntries = @([System.IO.Directory]::EnumerateFileSystemEntries($canonicalOutputRoot))
$expectedGateFiles = @('llmfit-gate1.evidence.json', 'llmfit-system.raw.json')
if ($gateOutputEntries.Count -ne $expectedGateFiles.Count) {
    throw 'The Gate 1 output inventory is incomplete or unexpected.'
}

foreach ($entry in $gateOutputEntries) {
    $attributes = [System.IO.File]::GetAttributes($entry)
    if (($attributes -band [System.IO.FileAttributes]::Directory) -ne 0 -or
        ($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
        $expectedGateFiles -cnotcontains [System.IO.Path]::GetFileName($entry)) {
        throw 'The Gate 1 output inventory contains an unsafe or unexpected member.'
    }
}

$evidencePath = [System.IO.Path]::Combine(
    $canonicalOutputRoot,
    'llmfit-gate1.evidence.json')
$rawPath = [System.IO.Path]::Combine(
    $canonicalOutputRoot,
    'llmfit-system.raw.json')
$evidenceJson = Get-StrictUtf8Text -LiteralPath $evidencePath -MaximumBytes 131072
$rawJson = Get-StrictUtf8Text -LiteralPath $rawPath -MaximumBytes 2097152
try {
    $evidence = $evidenceJson | ConvertFrom-Json
    $raw = $rawJson | ConvertFrom-Json
}
catch {
    throw 'The Gate 1 output JSON could not be parsed.'
}

$rawHash = Get-Sha256Lower -LiteralPath $rawPath -MaximumBytes 2097152
$recordedRawFileName = Get-RequiredPropertyValue `
    -InputObject $evidence `
    -Name 'rawSystemJsonFileName'
$recordedRawHash = Get-RequiredPropertyValue `
    -InputObject $evidence `
    -Name 'rawSystemJsonSha256'
if ($recordedRawFileName -cne 'llmfit-system.raw.json' -or
    $recordedRawHash -isnot [string] -or
    $recordedRawHash -cne $rawHash -or
    -not [bool](Get-RequiredPropertyValue -InputObject $evidence -Name 'jsonValid') -or
    -not [bool](Get-RequiredPropertyValue -InputObject $evidence -Name 'requiredCpuRamPresent')) {
    throw 'The raw Gate 1 JSON is not bound to valid CPU/RAM evidence.'
}

$versionArguments = @(
    Get-RequiredPropertyValue -InputObject $evidence -Name 'versionInvocationArguments' |
        ForEach-Object { [string]$_ })
$systemArguments = @(
    Get-RequiredPropertyValue -InputObject $evidence -Name 'systemInvocationArguments' |
        ForEach-Object { [string]$_ })
if ([string](Get-RequiredPropertyValue -InputObject $evidence -Name 'candidateId') -cne
        'llmfit-v1.1.9-win-x64' -or
    [string](Get-RequiredPropertyValue -InputObject $evidence -Name 'expectedVersion') -cne
        '1.1.9' -or
    [string](Get-RequiredPropertyValue -InputObject $evidence -Name 'reportedVersion') -cne
        'llmfit 1.1.9' -or
    [string](Get-RequiredPropertyValue -InputObject $evidence -Name 'releaseCommit') -cne
        'a02e13f1013ed69889ff44426a651bf7c68c292e' -or
    [string](Get-RequiredPropertyValue -InputObject $evidence -Name 'expectedArchiveSha256') -cne
        'a030269d7cc8a5bf40383f526a481655d698ec71dd792a25b06510cef9f8b738' -or
    [string](Get-RequiredPropertyValue -InputObject $evidence -Name 'observedArchiveSha256') -cne
        'a030269d7cc8a5bf40383f526a481655d698ec71dd792a25b06510cef9f8b738' -or
    [string](Get-RequiredPropertyValue -InputObject $evidence -Name 'expectedExecutableSha256') -cne
        'db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19' -or
    [string](Get-RequiredPropertyValue -InputObject $evidence -Name 'observedExecutableSha256') -cne
        'db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19' -or
    [string](Get-RequiredPropertyValue -InputObject $evidence -Name 'expectedPeMachine') -cne
        'AMD64' -or
    [string](Get-RequiredPropertyValue -InputObject $evidence -Name 'observedPeMachine') -cne
        'AMD64' -or
    $versionArguments.Count -ne 1 -or
    $versionArguments[0] -cne '--version' -or
    $systemArguments.Count -ne 3 -or
    $systemArguments[0] -cne '--no-dashboard' -or
    $systemArguments[1] -cne '--json' -or
    $systemArguments[2] -cne 'system' -or
    [int](Get-RequiredPropertyValue -InputObject $evidence -Name 'versionExitCode') -ne 0 -or
    [int](Get-RequiredPropertyValue -InputObject $evidence -Name 'systemExitCode') -ne 0 -or
    [bool](Get-RequiredPropertyValue -InputObject $evidence -Name 'processStartFailed') -or
    [bool](Get-RequiredPropertyValue -InputObject $evidence -Name 'socketObservationFailed') -or
    [bool](Get-RequiredPropertyValue -InputObject $evidence -Name 'timedOut') -or
    [bool](Get-RequiredPropertyValue -InputObject $evidence -Name 'cancelled') -or
    [bool](Get-RequiredPropertyValue -InputObject $evidence -Name 'standardOutputTruncated') -or
    [bool](Get-RequiredPropertyValue -InputObject $evidence -Name 'standardErrorTruncated')) {
    throw 'The Gate 1 evidence does not match the pinned successful execution contract.'
}

$gateDisposition = [string](Get-RequiredPropertyValue `
    -InputObject $evidence `
    -Name 'disposition')
if ($gateDisposition -cne 'FunctionalPassWithPackagingConcern' -and
    $gateDisposition -cne 'AcceptedForFunctionalEvaluation') {
    throw 'The Gate 1 disposition does not permit trusted comparison.'
}

$gateStarted = Get-ValidatedUtcTimestamp -Value (
    Get-RequiredPropertyValue -InputObject $evidence -Name 'gateStartedAtUtc')
$gateCompleted = Get-ValidatedUtcTimestamp -Value (
    Get-RequiredPropertyValue -InputObject $evidence -Name 'gateCompletedAtUtc')
if ($capturedBefore -gt $gateStarted -or
    $gateStarted -gt $gateCompleted -or
    $gateCompleted -gt $capturedAfter) {
    throw 'The Windows and Gate 1 timestamps are not bracketed correctly.'
}

$system = Get-RequiredPropertyValue -InputObject $raw -Name 'system'
$llmFitCpuName = [string](Get-RequiredPropertyValue -InputObject $system -Name 'cpu_name')
$llmFitLogicalProcessors = [int](Get-RequiredPropertyValue -InputObject $system -Name 'cpu_cores')
$llmFitTotalRamGiB = [double](Get-RequiredPropertyValue -InputObject $system -Name 'total_ram_gb')
$llmFitAvailableRamGiB = [double](Get-RequiredPropertyValue -InputObject $system -Name 'available_ram_gb')
if ([string]::IsNullOrWhiteSpace($llmFitCpuName) -or
    $llmFitLogicalProcessors -le 0 -or
    $llmFitTotalRamGiB -le 0 -or
    $llmFitAvailableRamGiB -lt 0 -or
    $llmFitAvailableRamGiB -gt $llmFitTotalRamGiB) {
    throw 'The raw Gate 1 CPU/RAM fields are invalid.'
}

$normalizedLlmFitCpu = Get-NormalizedHardwareIdentity -Value $llmFitCpuName
$cpuIdentityMatched = @($windowsCpuNames | Where-Object {
        (Get-NormalizedHardwareIdentity -Value $_) -ceq $normalizedLlmFitCpu
    }).Count -gt 0
$logicalProcessorCountMatched =
    [int]$logicalProcessorTotal -eq $llmFitLogicalProcessors
$windowsTotalRamGiB = $totalPhysicalMemoryBefore / 1073741824.0
$totalRamDeltaGiB = [Math]::Abs($llmFitTotalRamGiB - $windowsTotalRamGiB)
$totalRamToleranceGiB = 1.0
$availableMidpointGiB =
    (($freePhysicalMemoryBefore + $freePhysicalMemoryAfter) / 2.0) / 1048576.0
$availableRamDeltaGiB = [Math]::Abs(
    $llmFitAvailableRamGiB - $availableMidpointGiB)
$availableRamToleranceGiB = [Math]::Max(2.0, $windowsTotalRamGiB * 0.1)

$llmFitGpuNames = New-Object System.Collections.Generic.List[string]
$gpuNameProperty = $system.PSObject.Properties | Where-Object { $_.Name -ceq 'gpu_name' }
if ($null -ne $gpuNameProperty -and
    $gpuNameProperty.Value -is [string] -and
    -not [string]::IsNullOrWhiteSpace([string]$gpuNameProperty.Value)) {
    $llmFitGpuNames.Add([string]$gpuNameProperty.Value)
}

$gpusProperty = $system.PSObject.Properties | Where-Object { $_.Name -ceq 'gpus' }
if ($null -ne $gpusProperty) {
    foreach ($gpu in @($gpusProperty.Value)) {
        $nameProperty = $gpu.PSObject.Properties | Where-Object { $_.Name -ceq 'name' }
        if ($null -ne $nameProperty -and
            $nameProperty.Value -is [string] -and
            -not [string]::IsNullOrWhiteSpace([string]$nameProperty.Value)) {
            $llmFitGpuNames.Add([string]$nameProperty.Value)
        }
    }
}

$normalizedWindowsIntelGpuNames = @($windowsGpuNames | Where-Object {
        Test-IntelToken -Value $_
    } | ForEach-Object {
        Get-NormalizedHardwareIdentity -Value $_
    } | Select-Object -Unique)
$normalizedLlmFitIntelGpuNames = @($llmFitGpuNames.ToArray() | Where-Object {
        Test-IntelToken -Value $_
    } | ForEach-Object {
        Get-NormalizedHardwareIdentity -Value $_
    } | Select-Object -Unique)
$intelGpuIdentityMatched = @($normalizedWindowsIntelGpuNames | Where-Object {
        $normalizedLlmFitIntelGpuNames -ccontains $_
    }).Count -gt 0

$allowedDiagnostics = @(
    'HI-LLMFIT-MANIFEST-INVALID',
    'HI-LLMFIT-PACKAGE-MISSING',
    'HI-LLMFIT-UNEXPECTED-PACKAGE-MEMBER',
    'HI-LLMFIT-PACKAGE-CHANGED-DURING-RUN',
    'HI-LLMFIT-PATH-ESCAPE',
    'HI-LLMFIT-REPARSE-POINT',
    'HI-LLMFIT-ARCHIVE-LENGTH-MISMATCH',
    'HI-LLMFIT-ARCHIVE-HASH-MISMATCH',
    'HI-LLMFIT-EXECUTABLE-HASH-MISMATCH',
    'HI-LLMFIT-PE-INVALID',
    'HI-LLMFIT-PE-ARCHITECTURE-MISMATCH',
    'HI-LLMFIT-SIGNATURE-CLAIM-MISMATCH',
    'HI-LLMFIT-SIGNATURE-STATUS-CHANGED',
    'HI-LLMFIT-DEPENDENCY-LICENSE-INVENTORY-PENDING',
    'HI-LLMFIT-PROCESS-START-FAILED',
    'HI-LLMFIT-VERSION-MISMATCH',
    'HI-LLMFIT-PROCESS-TIMED-OUT',
    'HI-LLMFIT-PROCESS-CANCELLED',
    'HI-LLMFIT-PROCESS-EXIT-NONZERO',
    'HI-LLMFIT-STDOUT-TRUNCATED',
    'HI-LLMFIT-STDERR-TRUNCATED',
    'HI-LLMFIT-SOCKET-OBSERVATION-FAILED',
    'HI-LLMFIT-CANDIDATE-SOCKET-OBSERVED',
    'HI-LLMFIT-DASHBOARD-PORT-OBSERVED',
    'HI-LLMFIT-RESIDUAL-PROCESS',
    'HI-LLMFIT-JSON-INVALID',
    'HI-LLMFIT-CPU-RAM-MISSING',
    'HI-LLMFIT-GPU-INCONSISTENT',
    'HI-LLMFIT-WINDOWS-INTEL-MEMORY-SEMANTICS-GAP',
    'HI-LLMFIT-WINDOWS-INTEL-NPU-GAP',
    'HI-LLMFIT-SCHEMA-DOCUMENTATION-DRIFT',
    'HI-GATE1-WRONG-TARGET',
    'HI-GATE1-WINDOWS-COMPARISON-FAILED',
    'HI-GATE1-OFFLINE-PRECONDITION-FAILED',
    'HI-GATE1-PRIVACY-VALIDATION-FAILED',
    'HI-GATE1-REQUIRED-TEST-FAILURE')
$diagnostics = @(
    Get-RequiredPropertyValue -InputObject $evidence -Name 'diagnosticCodes' |
        ForEach-Object { [string]$_ })
if ($diagnostics.Count -eq 0 -or
    $diagnostics.Count -ne @($diagnostics | Select-Object -Unique).Count -or
    @($diagnostics | Where-Object { $allowedDiagnostics -cnotcontains $_ }).Count -ne 0) {
    throw 'The Gate 1 diagnostics are not privacy allowlisted.'
}

$observationPath = [System.IO.Path]::Combine(
    $canonicalCandidateRoot,
    'authenticode-observation.json')
$observationHash = $null
if ([System.IO.File]::Exists($observationPath)) {
    if (Test-ReparsePoint -LiteralPath $observationPath) {
        throw 'The Authenticode observation is a reparse point.'
    }

    $observationHash = Get-Sha256Lower `
        -LiteralPath $observationPath `
        -MaximumBytes 16384
}

$captureInterval = $capturedAfter - $capturedBefore
$captureIntervalMilliseconds = [long][Math]::Floor(
    $captureInterval.TotalMilliseconds)
$captureWithinThirtySeconds =
    $captureInterval -le [System.TimeSpan]::FromSeconds(30)
$summary = [ordered]@{
    schemaVersion = '1.0'
    captureStatus = 'Captured'
    capturedBeforeUtc = $capturedBefore.ToString(
        'o',
        [System.Globalization.CultureInfo]::InvariantCulture)
    gateStartedAtUtc = $gateStarted.ToString(
        'o',
        [System.Globalization.CultureInfo]::InvariantCulture)
    gateCompletedAtUtc = $gateCompleted.ToString(
        'o',
        [System.Globalization.CultureInfo]::InvariantCulture)
    capturedAfterUtc = $capturedAfter.ToString(
        'o',
        [System.Globalization.CultureInfo]::InvariantCulture)
    captureIntervalMilliseconds = $captureIntervalMilliseconds
    captureWithinThirtySeconds = $captureWithinThirtySeconds
    windowsX64 = $windowsX64
    windowsCpuNames = @($windowsCpuNames)
    windowsLogicalProcessorCount = [int]$logicalProcessorTotal
    windowsTotalPhysicalMemoryBytes = [uint64]$totalPhysicalMemoryBefore
    windowsFreePhysicalMemoryBeforeKiB = [uint64]$freePhysicalMemoryBefore
    windowsFreePhysicalMemoryAfterKiB = [uint64]$freePhysicalMemoryAfter
    windowsGpuNames = @($windowsGpuNames)
    windowsIntelCpuObserved = $windowsIntelCpuObserved
    windowsIntelGpuObserved = $windowsIntelGpuObserved
    cpuIdentityMatched = $cpuIdentityMatched
    llmFitLogicalProcessorCount = $llmFitLogicalProcessors
    logicalProcessorCountMatched = $logicalProcessorCountMatched
    totalRamDeltaGiB = $totalRamDeltaGiB
    totalRamToleranceGiB = $totalRamToleranceGiB
    totalRamWithinTolerance = $totalRamDeltaGiB -le $totalRamToleranceGiB
    availableRamDeltaGiB = $availableRamDeltaGiB
    availableRamToleranceGiB = $availableRamToleranceGiB
    availableRamWithinTolerance = $availableRamDeltaGiB -le $availableRamToleranceGiB
    jsonValid = [bool](Get-RequiredPropertyValue -InputObject $evidence -Name 'jsonValid')
    requiredCpuRamPresent = [bool](
        Get-RequiredPropertyValue -InputObject $evidence -Name 'requiredCpuRamPresent')
    intelGpuIdentityMatched = $intelGpuIdentityMatched
    intelGpuComparisonStatus = if ($intelGpuIdentityMatched) { 'Matched' } else { 'FieldLevelGap' }
    dedicatedSharedMemorySemanticsEstablished = $false
    intelNpuDetectionState = 'DetectionUnavailable'
    gateExitCode = [int]$gateExitCode
    gateDisposition = $gateDisposition
    diagnosticCodes = @($diagnostics)
    versionCandidateSocketObserved = [bool](
        Get-RequiredPropertyValue -InputObject $evidence -Name 'versionCandidateSocketObserved')
    versionDashboardPortObserved = [bool](
        Get-RequiredPropertyValue -InputObject $evidence -Name 'versionDashboardPortObserved')
    systemCandidateSocketObserved = [bool](
        Get-RequiredPropertyValue -InputObject $evidence -Name 'systemCandidateSocketObserved')
    systemDashboardPortObserved = [bool](
        Get-RequiredPropertyValue -InputObject $evidence -Name 'systemDashboardPortObserved')
    versionCandidateProcessRemainedAfterExit = [bool](
        Get-RequiredPropertyValue `
            -InputObject $evidence `
            -Name 'versionCandidateProcessRemainedAfterExit')
    systemCandidateProcessRemainedAfterExit = [bool](
        Get-RequiredPropertyValue `
            -InputObject $evidence `
            -Name 'systemCandidateProcessRemainedAfterExit')
    authenticodeObservationSha256 = $observationHash
}

$referencePath = [System.IO.Path]::Combine(
    $canonicalOutputRoot,
    'windows-reference.json')
Write-AtomicJson -Value $summary -Destination $referencePath
Write-Output 'Trusted Windows Intel reference captured with privacy-safe Gate 1 comparisons.'
exit $gateExitCode
