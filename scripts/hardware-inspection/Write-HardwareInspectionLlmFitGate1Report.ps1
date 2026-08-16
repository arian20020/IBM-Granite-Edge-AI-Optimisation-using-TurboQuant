[CmdletBinding()]
param(
    [AllowNull()]
    [AllowEmptyString()]
    [string] $EvidenceJson,

    [AllowNull()]
    [AllowEmptyString()]
    [string] $WindowsReferenceJson,

    [AllowNull()]
    [AllowEmptyString()]
    [string] $OfflineEvidenceJson,

    [Parameter(Mandatory = $true)]
    [string] $DeterministicTrx,

    [AllowNull()]
    [AllowEmptyString()]
    [string] $TrustedWindowsTrx,

    [AllowNull()]
    [AllowEmptyString()]
    [string] $OfflineTrx,

    [Parameter(Mandatory = $true)]
    [string] $OutputMarkdown,

    [Parameter(Mandatory = $true)]
    [string] $RepositoryCommit
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$maximumJsonBytes = 128KB
$maximumTrxBytes = 16MB
$strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
$approvedDispositions = @(
    'Blocked',
    'Rejected',
    'FunctionalPassWithPackagingConcern',
    'AcceptedForFunctionalEvaluation')
$approvedDiagnostics = @(
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
$nonFailureDiagnostics = @(
    'HI-LLMFIT-SIGNATURE-CLAIM-MISMATCH',
    'HI-LLMFIT-SIGNATURE-STATUS-CHANGED',
    'HI-LLMFIT-DEPENDENCY-LICENSE-INVENTORY-PENDING',
    'HI-LLMFIT-WINDOWS-INTEL-MEMORY-SEMANTICS-GAP',
    'HI-LLMFIT-WINDOWS-INTEL-NPU-GAP',
    'HI-LLMFIT-SCHEMA-DOCUMENTATION-DRIFT')
$trustedTestNames = @(
    'TrustedCandidate_IdentityVersionAndCpuRamSchemaPass',
    'TrustedCandidate_CpuAndRamAgreeWithNearSimultaneousWindowsReference',
    'TrustedCandidate_LeavesNoDashboardListenerOrProcess')
$offlineTestName = 'OfflineCandidate_ProducesCpuRamWithoutAnyListenerOrResidualProcess'

function Assert-Condition {
    param([bool] $Condition, [string] $Message)
    if (-not $Condition) { throw $Message }
}

function Test-LowerHex {
    param([AllowNull()][string] $Value, [int] $Length)
    return $null -ne $Value -and $Value.Length -eq $Length -and
        $Value -cmatch ('\A[0-9a-f]{' + $Length + '}\z')
}

function Get-Sha256Lower {
    param([byte[]] $Bytes)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally { $sha.Dispose() }
}

function Assert-OrdinaryPathChain {
    param([string] $FullPath, [switch] $AllowMissingLeaf)
    $root = [System.IO.Path]::GetPathRoot($FullPath)
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($root)) 'Path root is invalid.'
    $current = $root
    $relative = $FullPath.Substring($root.Length)
    $segments = @($relative.Split(@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar), [System.StringSplitOptions]::RemoveEmptyEntries))
    for ($index = 0; $index -lt $segments.Count; $index++) {
        $current = [System.IO.Path]::Combine($current, $segments[$index])
        if (-not [System.IO.File]::Exists($current) -and -not [System.IO.Directory]::Exists($current)) {
            Assert-Condition ($AllowMissingLeaf -and $index -eq $segments.Count - 1) 'Path contains a missing parent.'
            continue
        }
        $attributes = [System.IO.File]::GetAttributes($current)
        Assert-Condition (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -eq 0) 'Path contains a reparse point.'
        if ($index -lt $segments.Count - 1) {
            Assert-Condition (($attributes -band [System.IO.FileAttributes]::Directory) -ne 0) 'Path parent is not a directory.'
        }
    }
}

function Read-BoundedArtifact {
    param(
        [AllowNull()][AllowEmptyString()][string] $Path,
        [int64] $MaximumBytes,
        [string] $Label,
        [switch] $Optional)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        if ($Optional) { return $null }
        throw "$Label is required."
    }

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    Assert-Condition (-not $fullPath.StartsWith('\\', [System.StringComparison]::Ordinal)) "$Label must be a local file."
    Assert-OrdinaryPathChain -FullPath $fullPath
    Assert-Condition ([System.IO.File]::Exists($fullPath)) "$Label is missing."
    $attributes = [System.IO.File]::GetAttributes($fullPath)
    Assert-Condition (($attributes -band [System.IO.FileAttributes]::Directory) -eq 0) "$Label is not a file."
    Assert-Condition (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -eq 0) "$Label is a reparse point."

    $stream = New-Object System.IO.FileStream(
        $fullPath,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::Read,
        4096,
        [System.IO.FileOptions]::SequentialScan)
    try {
        Assert-Condition ($stream.Length -gt 0 -and $stream.Length -le $MaximumBytes) "$Label length is invalid."
        $bytes = New-Object byte[] ([int]$stream.Length)
        $offset = 0
        while ($offset -lt $bytes.Length) {
            $read = $stream.Read($bytes, $offset, $bytes.Length - $offset)
            Assert-Condition ($read -gt 0) "$Label changed while it was read."
            $offset += $read
        }
        Assert-Condition ($stream.ReadByte() -eq -1) "$Label changed while it was read."
    }
    finally { $stream.Dispose() }

    return [pscustomobject]@{
        Label = $Label
        Path = $fullPath
        Bytes = $bytes
        Sha256 = Get-Sha256Lower -Bytes $bytes
    }
}

function Get-StrictText {
    param([object] $Artifact)
    Assert-Condition ($Artifact.Bytes.Length -lt 3 -or
        $Artifact.Bytes[0] -ne 0xEF -or $Artifact.Bytes[1] -ne 0xBB -or
        $Artifact.Bytes[2] -ne 0xBF) "$($Artifact.Label) contains a UTF-8 byte-order mark."
    return $strictUtf8.GetString($Artifact.Bytes)
}

function Get-RootJsonPropertyNames {
    param([string] $Json)
    $state = [pscustomobject]@{ Index = 0 }
    function Skip-Whitespace {
        while ($state.Index -lt $Json.Length -and [char]::IsWhiteSpace($Json[$state.Index])) {
            $state.Index++
        }
    }
    function Read-JsonStringToken {
        Assert-Condition ($state.Index -lt $Json.Length -and $Json[$state.Index] -eq '"') 'JSON property name is invalid.'
        $start = $state.Index
        $state.Index++
        $escaped = $false
        while ($state.Index -lt $Json.Length) {
            $character = $Json[$state.Index]
            $state.Index++
            if ($escaped) { $escaped = $false; continue }
            if ($character -eq '\') { $escaped = $true; continue }
            if ($character -eq '"') {
                $token = $Json.Substring($start, $state.Index - $start)
                return [string]($token | ConvertFrom-Json)
            }
            Assert-Condition (-not [char]::IsControl($character)) 'JSON string contains an unescaped control character.'
        }
        throw 'JSON string is unterminated.'
    }
    function Skip-JsonValue {
        $containerDepth = 0
        $inString = $false
        $escaped = $false
        while ($state.Index -lt $Json.Length) {
            $character = $Json[$state.Index]
            if ($inString) {
                $state.Index++
                if ($escaped) { $escaped = $false; continue }
                if ($character -eq '\') { $escaped = $true; continue }
                if ($character -eq '"') { $inString = $false }
                continue
            }
            if ($character -eq '"') { $inString = $true; $state.Index++; continue }
            if ($character -eq '{' -or $character -eq '[') { $containerDepth++; $state.Index++; continue }
            if ($character -eq '}' -or $character -eq ']') {
                if ($containerDepth -eq 0) { return }
                $containerDepth--; $state.Index++; continue
            }
            if ($character -eq ',' -and $containerDepth -eq 0) { return }
            $state.Index++
        }
    }

    Skip-Whitespace
    Assert-Condition ($state.Index -lt $Json.Length -and $Json[$state.Index] -eq '{') 'JSON root is not an object.'
    $state.Index++
    $names = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
    $orderedNames = New-Object 'System.Collections.Generic.List[string]'
    while ($true) {
        Skip-Whitespace
        if ($state.Index -lt $Json.Length -and $Json[$state.Index] -eq '}') { $state.Index++; break }
        $name = Read-JsonStringToken
        Assert-Condition ($names.Add($name)) 'JSON contains a duplicate property.'
        $orderedNames.Add($name)
        Skip-Whitespace
        Assert-Condition ($state.Index -lt $Json.Length -and $Json[$state.Index] -eq ':') 'JSON property separator is invalid.'
        $state.Index++
        Skip-Whitespace
        Skip-JsonValue
        Skip-Whitespace
        if ($state.Index -lt $Json.Length -and $Json[$state.Index] -eq ',') { $state.Index++; continue }
        Assert-Condition ($state.Index -lt $Json.Length -and $Json[$state.Index] -eq '}') 'JSON object terminator is invalid.'
        $state.Index++
        break
    }
    Skip-Whitespace
    Assert-Condition ($state.Index -eq $Json.Length) 'JSON contains trailing content.'
    Write-Output -NoEnumerate $orderedNames.ToArray()
}

function Open-StrictJson {
    param([object] $Artifact)
    $text = Get-StrictText -Artifact $Artifact
    $propertyNames = @(Get-RootJsonPropertyNames -Json $text)
    $value = $text | ConvertFrom-Json
    Assert-Condition ($null -ne $value -and $value -isnot [System.Array]) 'JSON root did not produce an object.'
    return [pscustomobject]@{ Value = $value; PropertyNames = $propertyNames }
}

function Assert-ExactProperties {
    param([object] $Value, [string[]] $Expected)
    $actual = @($Value.PSObject.Properties | ForEach-Object { $_.Name })
    Assert-Condition ($actual.Count -eq $Expected.Count) 'JSON properties differ from the privacy allowlist.'
    foreach ($name in $actual) {
        Assert-Condition ($Expected -ccontains $name) 'JSON properties differ from the privacy allowlist.'
    }
}

function Get-RequiredValue {
    param([object] $Value, [string] $Name)
    $property = $Value.PSObject.Properties | Where-Object { $_.Name -ceq $Name }
    Assert-Condition ($null -ne $property) "Required property is absent: $Name."
    return $property.Value
}

function Assert-String {
    param([object] $Value, [string] $Name, [switch] $Nullable)
    $item = Get-RequiredValue -Value $Value -Name $Name
    if ($null -eq $item -and $Nullable) { return $null }
    Assert-Condition ($item -is [string] -and -not [string]::IsNullOrWhiteSpace($item)) "$Name is invalid."
    return [string]$item
}

function Assert-Bool {
    param([object] $Value, [string] $Name)
    $item = Get-RequiredValue -Value $Value -Name $Name
    Assert-Condition ($item -is [bool]) "$Name is not Boolean."
    return [bool]$item
}

function Assert-Integer {
    param([object] $Value, [string] $Name, [switch] $Nullable)
    $item = Get-RequiredValue -Value $Value -Name $Name
    if ($null -eq $item -and $Nullable) { return $null }
    Assert-Condition ($item -is [sbyte] -or $item -is [byte] -or
        $item -is [int16] -or $item -is [uint16] -or $item -is [int32] -or
        $item -is [uint32] -or $item -is [int64] -or $item -is [uint64]) "$Name is not an integer."
    return [int64]$item
}

function Assert-Number {
    param([object] $Value, [string] $Name, [switch] $Nullable)
    $item = Get-RequiredValue -Value $Value -Name $Name
    if ($null -eq $item -and $Nullable) { return $null }
    Assert-Condition ($item -is [ValueType] -and $item -isnot [bool]) "$Name is not numeric."
    $number = [double]$item
    Assert-Condition (-not [double]::IsNaN($number) -and -not [double]::IsInfinity($number)) "$Name is not finite."
    return $number
}

function Assert-StringArray {
    param([object] $Value, [string] $Name, [switch] $AllowEmpty)
    $item = Get-RequiredValue -Value $Value -Name $Name
    Assert-Condition ($null -ne $item) "$Name is absent."
    $items = @($item)
    Assert-Condition ($AllowEmpty -or $items.Count -gt 0) "$Name is empty."
    foreach ($entry in $items) {
        Assert-Condition ($entry -is [string] -and -not [string]::IsNullOrWhiteSpace($entry)) "$Name contains an invalid string."
    }
    $items
}

function Get-UtcTimestamp {
    param([object] $Value, [string] $Name)
    $text = Assert-String -Value $Value -Name $Name
    $timestamp = [System.DateTimeOffset]::Parse(
        $text,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::RoundtripKind)
    Assert-Condition ($timestamp.Offset -eq [System.TimeSpan]::Zero) "$Name is not UTC."
    return $timestamp
}

function Assert-Diagnostics {
    param([object] $Value)
    $codes = @(Assert-StringArray -Value $Value -Name 'diagnosticCodes' -AllowEmpty)
    Assert-Condition (@($codes | Select-Object -Unique).Count -eq $codes.Count) 'Diagnostic codes contain duplicates.'
    foreach ($code in $codes) {
        Assert-Condition ($approvedDiagnostics -ccontains $code) 'A diagnostic code is not allowlisted.'
    }
    $codes
}

function Read-MinimalBlockedEnvelope {
    param([object] $Artifact, [string] $ExpectedDiagnostic)
    $parsed = Open-StrictJson -Artifact $Artifact
    try {
        Assert-ExactProperties -Value $parsed.Value -Expected @('schemaVersion','disposition','diagnosticCodes')
        Assert-Condition ((Assert-String $parsed.Value 'schemaVersion') -ceq '1.0') 'Blocked schema differs.'
        Assert-Condition ((Assert-String $parsed.Value 'disposition') -ceq 'Blocked') 'Blocked disposition differs.'
        $codes = @(Assert-Diagnostics -Value $parsed.Value)
        Assert-Condition ($codes.Count -eq 1 -and $codes[0] -ceq $ExpectedDiagnostic) 'Blocked diagnostic differs.'
        return [pscustomobject]@{ Disposition = 'Blocked'; DiagnosticCodes = $codes; Minimal = $true }
    }
    finally { }
}

function Read-FullEvidence {
    param([object] $Artifact)
    $properties = @(
        'schemaVersion','disposition','candidateId','expectedVersion','reportedVersion',
        'releaseCommit','expectedArchiveSha256','observedArchiveSha256',
        'expectedExecutableSha256','observedExecutableSha256','expectedPeMachine',
        'observedPeMachine','authenticodePresent','authenticodeStatus',
        'versionInvocationArguments','systemInvocationArguments','gateStartedAtUtc',
        'gateCompletedAtUtc','durationMilliseconds','versionExitCode','systemExitCode',
        'processStartFailed','socketObservationFailed','timedOut','cancelled',
        'standardOutputTruncated','standardErrorTruncated','jsonValid',
        'requiredCpuRamPresent','cpuLogicalProcessorCount','totalRamGiB',
        'availableRamGiB','gpuReported','reportedGpuCount','intelGpuReported',
        'dedicatedSharedMemorySemanticsEstablished','intelNpuDetectionState',
        'versionCandidateSocketObserved','versionDashboardPortObserved',
        'systemCandidateSocketObserved','systemDashboardPortObserved',
        'versionCandidateProcessRemainedAfterExit',
        'systemCandidateProcessRemainedAfterExit','rawSystemJsonFileName',
        'rawSystemJsonSha256','diagnosticCodes')
    $parsed = Open-StrictJson -Artifact $Artifact
    try {
        $value = $parsed.Value
        Assert-ExactProperties -Value $value -Expected $properties
        Assert-Condition ((Assert-String $value 'schemaVersion') -ceq '1.0') 'Evidence schema differs.'
        $disposition = Assert-String $value 'disposition'
        Assert-Condition ($approvedDispositions -ccontains $disposition -and $disposition -cne 'Blocked') 'Full evidence disposition is invalid.'
        Assert-Condition ((Assert-String $value 'candidateId') -ceq 'llmfit-v1.1.9-win-x64') 'Evidence candidate differs.'
        Assert-Condition ((Assert-String $value 'expectedVersion') -ceq '1.1.9') 'Evidence version differs.'
        $reportedVersion = Assert-String $value 'reportedVersion' -Nullable
        Assert-Condition ($null -eq $reportedVersion -or $reportedVersion -ceq 'llmfit 1.1.9') 'Reported version differs.'
        Assert-Condition ((Assert-String $value 'releaseCommit') -ceq 'a02e13f1013ed69889ff44426a651bf7c68c292e') 'Release commit differs.'
        $expectedArchive = Assert-String $value 'expectedArchiveSha256'
        $observedArchive = Assert-String $value 'observedArchiveSha256' -Nullable
        $expectedExecutable = Assert-String $value 'expectedExecutableSha256'
        $observedExecutable = Assert-String $value 'observedExecutableSha256' -Nullable
        Assert-Condition ($expectedArchive -ceq 'a030269d7cc8a5bf40383f526a481655d698ec71dd792a25b06510cef9f8b738') 'Expected archive hash differs.'
        Assert-Condition ($expectedExecutable -ceq 'db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19') 'Expected executable hash differs.'
        Assert-Condition ($null -eq $observedArchive -or (Test-LowerHex $observedArchive 64)) 'Observed archive hash is invalid.'
        Assert-Condition ($null -eq $observedExecutable -or (Test-LowerHex $observedExecutable 64)) 'Observed executable hash is invalid.'
        Assert-Condition ((Assert-String $value 'expectedPeMachine') -ceq 'AMD64') 'Expected PE machine differs.'
        $observedPe = Assert-String $value 'observedPeMachine' -Nullable
        Assert-Condition ($null -eq $observedPe -or $observedPe -cin @('AMD64','I386','ARM64','Unknown') -or $observedPe -cmatch '\A0x[0-9A-F]{4}\z') 'Observed PE machine is invalid.'
        $authenticodePresent = Assert-Bool $value 'authenticodePresent'
        $authenticodeStatus = Assert-String $value 'authenticodeStatus'
        Assert-Condition ($authenticodeStatus -cin @('NotSigned','PresentUnverified')) 'Authenticode status is invalid.'
        Assert-Condition ($authenticodePresent -eq ($authenticodeStatus -ceq 'PresentUnverified')) 'Authenticode fields disagree.'
        $versionArguments = @(Assert-StringArray $value 'versionInvocationArguments')
        $systemArguments = @(Assert-StringArray $value 'systemInvocationArguments')
        Assert-Condition ($versionArguments.Count -eq 1 -and $versionArguments[0] -ceq '--version') 'Version arguments differ.'
        Assert-Condition ($systemArguments.Count -eq 3 -and $systemArguments[0] -ceq '--no-dashboard' -and $systemArguments[1] -ceq '--json' -and $systemArguments[2] -ceq 'system') 'System arguments differ.'
        $started = Get-UtcTimestamp $value 'gateStartedAtUtc'
        $completed = Get-UtcTimestamp $value 'gateCompletedAtUtc'
        $duration = Assert-Integer $value 'durationMilliseconds'
        $expectedDuration = [int64][Math]::Truncate(($completed - $started).TotalMilliseconds)
        Assert-Condition ($completed -ge $started -and $duration -eq $expectedDuration) 'Evidence duration differs.'
        $versionExit = Assert-Integer $value 'versionExitCode' -Nullable
        $systemExit = Assert-Integer $value 'systemExitCode' -Nullable
        $processStartFailed = Assert-Bool $value 'processStartFailed'
        $socketObservationFailed = Assert-Bool $value 'socketObservationFailed'
        $timedOut = Assert-Bool $value 'timedOut'
        $cancelled = Assert-Bool $value 'cancelled'
        $stdoutTruncated = Assert-Bool $value 'standardOutputTruncated'
        $stderrTruncated = Assert-Bool $value 'standardErrorTruncated'
        $jsonValid = Assert-Bool $value 'jsonValid'
        $cpuRam = Assert-Bool $value 'requiredCpuRamPresent'
        $logical = Assert-Integer $value 'cpuLogicalProcessorCount' -Nullable
        $totalRam = Assert-Number $value 'totalRamGiB' -Nullable
        $availableRam = Assert-Number $value 'availableRamGiB' -Nullable
        Assert-Condition ($null -eq $logical -or $logical -gt 0) 'Logical processor count is invalid.'
        Assert-Condition ($null -eq $totalRam -or $totalRam -ge 0) 'Total RAM is invalid.'
        Assert-Condition ($null -eq $availableRam -or $availableRam -ge 0) 'Available RAM is invalid.'
        Assert-Condition (-not $cpuRam -or $null -ne $logical -and $null -ne $totalRam -and $null -ne $availableRam) 'Required CPU/RAM values are absent.'
        Assert-Condition (-not $cpuRam -or ($totalRam -gt 0 -and $availableRam -ge 0 -and $availableRam -le $totalRam)) 'Required CPU/RAM values are not sensible.'
        Assert-Condition ($null -eq $totalRam -or $null -eq $availableRam -or $availableRam -le $totalRam) 'Available RAM exceeds total RAM.'
        $gpuReported = Assert-Bool $value 'gpuReported'
        $gpuCount = Assert-Integer $value 'reportedGpuCount'
        $intelGpu = Assert-Bool $value 'intelGpuReported'
        $gpuBaselineValid = $gpuCount -ge 0 -and
            ($gpuReported -or ($gpuCount -eq 0 -and -not $intelGpu)) -and
            ((-not $intelGpu) -or ($gpuReported -and $gpuCount -gt 0))
        Assert-Condition $gpuBaselineValid 'GPU evidence violates the writer schema.'
        $gpuConsistent = $gpuReported -eq ($gpuCount -gt 0)
        $memorySemantics = Assert-Bool $value 'dedicatedSharedMemorySemanticsEstablished'
        Assert-Condition ((Assert-String $value 'intelNpuDetectionState') -ceq 'DetectionUnavailable') 'Intel NPU state is invalid.'
        $versionSocket = Assert-Bool $value 'versionCandidateSocketObserved'
        $versionDashboard = Assert-Bool $value 'versionDashboardPortObserved'
        $systemSocket = Assert-Bool $value 'systemCandidateSocketObserved'
        $systemDashboard = Assert-Bool $value 'systemDashboardPortObserved'
        Assert-Condition (-not $versionDashboard -or $versionSocket) 'Version dashboard evidence is inconsistent.'
        Assert-Condition (-not $systemDashboard -or $systemSocket) 'System dashboard evidence is inconsistent.'
        $versionResidual = Assert-Bool $value 'versionCandidateProcessRemainedAfterExit'
        $systemResidual = Assert-Bool $value 'systemCandidateProcessRemainedAfterExit'
        $rawName = Assert-String $value 'rawSystemJsonFileName' -Nullable
        $rawHash = Assert-String $value 'rawSystemJsonSha256' -Nullable
        Assert-Condition (($null -eq $rawName) -eq ($null -eq $rawHash)) 'Raw capture pair is incomplete.'
        Assert-Condition ($null -eq $rawName -or $rawName -ceq 'llmfit-system.raw.json' -and (Test-LowerHex $rawHash 64) -and $jsonValid) 'Raw capture pair is invalid.'
        $diagnostics = @(Assert-Diagnostics $value)
        $gpuInconsistentDiagnostic = $diagnostics -ccontains 'HI-LLMFIT-GPU-INCONSISTENT'
        if (-not $gpuConsistent) {
            Assert-Condition ($disposition -ceq 'Rejected' -and $gpuInconsistentDiagnostic) 'GPU aggregate inconsistency lacks a matching Rejected diagnostic.'
        }
        if ($gpuInconsistentDiagnostic) {
            Assert-Condition ($disposition -ceq 'Rejected') 'GPU inconsistency diagnostic requires a Rejected disposition.'
        }
        $hardDiagnostics = @($diagnostics | Where-Object { $nonFailureDiagnostics -cnotcontains $_ })
        $functional = $reportedVersion -ceq 'llmfit 1.1.9' -and
            $observedArchive -ceq $expectedArchive -and $observedExecutable -ceq $expectedExecutable -and
            $observedPe -ceq 'AMD64' -and $versionExit -eq 0 -and $systemExit -eq 0 -and
            -not $processStartFailed -and -not $socketObservationFailed -and -not $timedOut -and
            -not $cancelled -and -not $stdoutTruncated -and -not $stderrTruncated -and
            $jsonValid -and $cpuRam -and $gpuConsistent -and -not $memorySemantics -and
            -not $versionSocket -and -not $versionDashboard -and
            -not $systemSocket -and -not $systemDashboard -and -not $versionResidual -and
            -not $systemResidual -and $null -ne $rawName -and $hardDiagnostics.Count -eq 0
        if ($disposition -cin @('FunctionalPassWithPackagingConcern','AcceptedForFunctionalEvaluation')) {
            Assert-Condition $functional 'Accepted evidence contains a failing result.'
            Assert-Condition (($diagnostics -ccontains 'HI-LLMFIT-SIGNATURE-CLAIM-MISMATCH') -eq (-not $authenticodePresent)) 'Signature diagnostic disagrees.'
            Assert-Condition (($disposition -ceq 'FunctionalPassWithPackagingConcern') -eq ($diagnostics -ccontains 'HI-LLMFIT-DEPENDENCY-LICENSE-INVENTORY-PENDING')) 'Dependency disposition disagrees.'
        }
        return [pscustomobject]@{
            Disposition = $disposition; ReportedVersion = $reportedVersion
            ObservedArchiveSha256 = $observedArchive; ObservedExecutableSha256 = $observedExecutable
            ObservedPeMachine = $observedPe; AuthenticodePresent = $authenticodePresent
            AuthenticodeStatus = $authenticodeStatus; Started = $started; Completed = $completed
            VersionExitCode = $versionExit; SystemExitCode = $systemExit
            JsonValid = $jsonValid; RequiredCpuRamPresent = $cpuRam
            CpuLogicalProcessorCount = $logical; TotalRamGiB = $totalRam; AvailableRamGiB = $availableRam
            GpuReported = $gpuReported; ReportedGpuCount = $gpuCount; IntelGpuReported = $intelGpu
            DedicatedSharedMemorySemanticsEstablished = $memorySemantics
            VersionSocket = $versionSocket; VersionDashboard = $versionDashboard
            SystemSocket = $systemSocket; SystemDashboard = $systemDashboard
            VersionResidual = $versionResidual; SystemResidual = $systemResidual
            Diagnostics = $diagnostics; Functional = $functional; Minimal = $false
        }
    }
    finally { }
}

function Read-FullReference {
    param([object] $Artifact)
    $properties = @(
        'schemaVersion','captureStatus','capturedBeforeUtc','gateStartedAtUtc',
        'gateCompletedAtUtc','capturedAfterUtc','captureIntervalMilliseconds',
        'captureWithinThirtySeconds','windowsX64','windowsCpuNames',
        'windowsLogicalProcessorCount','windowsTotalPhysicalMemoryBytes',
        'windowsFreePhysicalMemoryBeforeKiB','windowsFreePhysicalMemoryAfterKiB',
        'windowsGpuNames','windowsIntelCpuObserved','windowsIntelGpuObserved',
        'cpuIdentityMatched','llmFitLogicalProcessorCount',
        'logicalProcessorCountMatched','totalRamDeltaGiB','totalRamToleranceGiB',
        'totalRamWithinTolerance','availableRamDeltaGiB',
        'availableRamToleranceGiB','availableRamWithinTolerance','jsonValid',
        'requiredCpuRamPresent','intelGpuIdentityMatched','intelGpuComparisonStatus',
        'dedicatedSharedMemorySemanticsEstablished','intelNpuDetectionState',
        'gateExitCode','gateDisposition','diagnosticCodes',
        'versionCandidateSocketObserved','versionDashboardPortObserved',
        'systemCandidateSocketObserved','systemDashboardPortObserved',
        'versionCandidateProcessRemainedAfterExit',
        'systemCandidateProcessRemainedAfterExit','authenticodeObservationSha256')
    $parsed = Open-StrictJson -Artifact $Artifact
    try {
        $value = $parsed.Value
        Assert-ExactProperties $value $properties
        Assert-Condition ((Assert-String $value 'schemaVersion') -ceq '1.0') 'Reference schema differs.'
        Assert-Condition ((Assert-String $value 'captureStatus') -ceq 'Captured') 'Reference capture status differs.'
        $before = Get-UtcTimestamp $value 'capturedBeforeUtc'
        $started = Get-UtcTimestamp $value 'gateStartedAtUtc'
        $completed = Get-UtcTimestamp $value 'gateCompletedAtUtc'
        $after = Get-UtcTimestamp $value 'capturedAfterUtc'
        Assert-Condition ($before -le $started -and $started -le $completed -and $completed -le $after) 'Reference timestamps are not bracketed.'
        $interval = Assert-Integer $value 'captureIntervalMilliseconds'
        $expectedInterval = [int64][Math]::Truncate(($after - $before).TotalMilliseconds)
        Assert-Condition ($interval -eq $expectedInterval) 'Reference interval differs.'
        $withinThirty = Assert-Bool $value 'captureWithinThirtySeconds'
        Assert-Condition ($withinThirty -eq (($after - $before) -le [System.TimeSpan]::FromSeconds(30))) 'Reference interval classification differs.'
        $windowsX64 = Assert-Bool $value 'windowsX64'
        $cpuNames = @(Assert-StringArray $value 'windowsCpuNames')
        $gpuNames = @(Assert-StringArray $value 'windowsGpuNames')
        foreach ($name in @($cpuNames + $gpuNames)) {
            Assert-Condition ($name.Length -le 256 -and -not ($name.ToCharArray() | Where-Object { [char]::IsControl($_) }) -and
                $name -cmatch '\A[\p{L}\p{Nd}\p{Zs}().,+_''@&\u00AE\u2122-]+\z') 'A Windows hardware name has an unsafe shape.'
        }
        $windowsLogical = Assert-Integer $value 'windowsLogicalProcessorCount'
        $windowsTotalBytes = Assert-Integer $value 'windowsTotalPhysicalMemoryBytes'
        $freeBefore = Assert-Integer $value 'windowsFreePhysicalMemoryBeforeKiB'
        $freeAfter = Assert-Integer $value 'windowsFreePhysicalMemoryAfterKiB'
        Assert-Condition ($windowsLogical -gt 0 -and $windowsTotalBytes -gt 0 -and $freeBefore -ge 0 -and $freeAfter -ge 0 -and
            $freeBefore -le [int64]($windowsTotalBytes / 1024) -and $freeAfter -le [int64]($windowsTotalBytes / 1024)) 'Reference memory/count fields are invalid.'
        $intelCpu = Assert-Bool $value 'windowsIntelCpuObserved'
        $intelGpuObserved = Assert-Bool $value 'windowsIntelGpuObserved'
        $cpuIdentity = Assert-Bool $value 'cpuIdentityMatched'
        $llmLogical = Assert-Integer $value 'llmFitLogicalProcessorCount'
        $logicalMatched = Assert-Bool $value 'logicalProcessorCountMatched'
        $totalDelta = Assert-Number $value 'totalRamDeltaGiB'
        $totalTolerance = Assert-Number $value 'totalRamToleranceGiB'
        $totalWithin = Assert-Bool $value 'totalRamWithinTolerance'
        $availableDelta = Assert-Number $value 'availableRamDeltaGiB'
        $availableTolerance = Assert-Number $value 'availableRamToleranceGiB'
        $availableWithin = Assert-Bool $value 'availableRamWithinTolerance'
        Assert-Condition ($llmLogical -gt 0 -and $totalDelta -ge 0 -and $totalTolerance -eq 1 -and
            $availableDelta -ge 0 -and $availableTolerance -ge 2) 'Reference derived values are invalid.'
        Assert-Condition ($logicalMatched -eq ($windowsLogical -eq $llmLogical)) 'Logical comparison is inconsistent.'
        Assert-Condition ($totalWithin -eq ($totalDelta -le $totalTolerance)) 'Total RAM comparison is inconsistent.'
        Assert-Condition ($availableWithin -eq ($availableDelta -le $availableTolerance)) 'Available RAM comparison is inconsistent.'
        $jsonValid = Assert-Bool $value 'jsonValid'
        $cpuRam = Assert-Bool $value 'requiredCpuRamPresent'
        $intelGpuMatched = Assert-Bool $value 'intelGpuIdentityMatched'
        $gpuComparison = Assert-String $value 'intelGpuComparisonStatus'
        Assert-Condition ($gpuComparison -cin @('Matched','FieldLevelGap') -and ($gpuComparison -ceq 'Matched') -eq $intelGpuMatched) 'GPU comparison is inconsistent.'
        $memorySemantics = Assert-Bool $value 'dedicatedSharedMemorySemanticsEstablished'
        Assert-Condition (-not $memorySemantics) 'Reference overclaims GPU memory semantics.'
        Assert-Condition ((Assert-String $value 'intelNpuDetectionState') -ceq 'DetectionUnavailable') 'Reference overclaims NPU detection.'
        $gateExit = Assert-Integer $value 'gateExitCode'
        $gateDisposition = Assert-String $value 'gateDisposition'
        Assert-Condition ($gateDisposition -cin @('FunctionalPassWithPackagingConcern','AcceptedForFunctionalEvaluation')) 'Reference gate disposition is invalid.'
        $diagnostics = @(Assert-Diagnostics $value)
        $versionSocket = Assert-Bool $value 'versionCandidateSocketObserved'
        $versionDashboard = Assert-Bool $value 'versionDashboardPortObserved'
        $systemSocket = Assert-Bool $value 'systemCandidateSocketObserved'
        $systemDashboard = Assert-Bool $value 'systemDashboardPortObserved'
        $versionResidual = Assert-Bool $value 'versionCandidateProcessRemainedAfterExit'
        $systemResidual = Assert-Bool $value 'systemCandidateProcessRemainedAfterExit'
        $authHash = Assert-String $value 'authenticodeObservationSha256' -Nullable
        Assert-Condition ($null -eq $authHash -or (Test-LowerHex $authHash 64)) 'Authenticode observation hash is invalid.'
        return [pscustomobject]@{
            Disposition = $gateDisposition; Before = $before; Started = $started
            Completed = $completed; After = $after; IntervalMilliseconds = $interval
            CaptureWithinThirtySeconds = $withinThirty; WindowsX64 = $windowsX64
            WindowsLogicalProcessorCount = $windowsLogical
            WindowsTotalPhysicalMemoryBytes = $windowsTotalBytes
            WindowsFreePhysicalMemoryBeforeKiB = $freeBefore
            WindowsFreePhysicalMemoryAfterKiB = $freeAfter
            WindowsIntelCpuObserved = $intelCpu
            WindowsIntelGpuObserved = $intelGpuObserved; CpuIdentityMatched = $cpuIdentity
            LlmFitLogicalProcessorCount = $llmLogical; LogicalProcessorCountMatched = $logicalMatched
            TotalRamDeltaGiB = $totalDelta; TotalRamToleranceGiB = $totalTolerance
            TotalRamWithinTolerance = $totalWithin; AvailableRamDeltaGiB = $availableDelta
            AvailableRamToleranceGiB = $availableTolerance; AvailableRamWithinTolerance = $availableWithin
            JsonValid = $jsonValid; RequiredCpuRamPresent = $cpuRam
            IntelGpuIdentityMatched = $intelGpuMatched; IntelGpuComparisonStatus = $gpuComparison
            DedicatedSharedMemorySemanticsEstablished = $memorySemantics
            GateExitCode = $gateExit; Diagnostics = $diagnostics
            VersionSocket = $versionSocket; VersionDashboard = $versionDashboard
            SystemSocket = $systemSocket; SystemDashboard = $systemDashboard
            VersionResidual = $versionResidual; SystemResidual = $systemResidual
            AuthenticodeObservationSha256 = $authHash; Minimal = $false
        }
    }
    finally { }
}

function Test-FullEvidenceReferenceConsistency {
    param([object] $Evidence, [object] $Reference)

    if ($null -eq $Evidence.TotalRamGiB -or $null -eq $Evidence.AvailableRamGiB) {
        return $false
    }

    $comparisonEpsilon = 0.000001
    $windowsTotalGiB = [double]$Reference.WindowsTotalPhysicalMemoryBytes / 1073741824.0
    $expectedTotalDeltaGiB = [Math]::Abs([double]$Evidence.TotalRamGiB - $windowsTotalGiB)
    $expectedTotalToleranceGiB = 1.0
    $midpointAvailableGiB = (
        ([double]$Reference.WindowsFreePhysicalMemoryBeforeKiB +
            [double]$Reference.WindowsFreePhysicalMemoryAfterKiB) / 2.0) / 1048576.0
    $expectedAvailableDeltaGiB = [Math]::Abs(
        [double]$Evidence.AvailableRamGiB - $midpointAvailableGiB)
    $expectedAvailableToleranceGiB = [Math]::Max(2.0, $windowsTotalGiB * 0.1)

    return $Evidence.Started -eq $Reference.Started -and
        $Evidence.Completed -eq $Reference.Completed -and
        $Evidence.Disposition -ceq $Reference.Disposition -and
        $Evidence.SystemExitCode -eq $Reference.GateExitCode -and
        $Evidence.JsonValid -eq $Reference.JsonValid -and
        $Evidence.RequiredCpuRamPresent -eq $Reference.RequiredCpuRamPresent -and
        $Evidence.CpuLogicalProcessorCount -eq $Reference.LlmFitLogicalProcessorCount -and
        [Math]::Abs($Reference.TotalRamDeltaGiB - $expectedTotalDeltaGiB) -le $comparisonEpsilon -and
        [Math]::Abs($Reference.TotalRamToleranceGiB - $expectedTotalToleranceGiB) -le $comparisonEpsilon -and
        $Reference.TotalRamWithinTolerance -eq ($expectedTotalDeltaGiB -le $expectedTotalToleranceGiB) -and
        [Math]::Abs($Reference.AvailableRamDeltaGiB - $expectedAvailableDeltaGiB) -le $comparisonEpsilon -and
        [Math]::Abs($Reference.AvailableRamToleranceGiB - $expectedAvailableToleranceGiB) -le $comparisonEpsilon -and
        $Reference.AvailableRamWithinTolerance -eq ($expectedAvailableDeltaGiB -le $expectedAvailableToleranceGiB) -and
        $Evidence.DedicatedSharedMemorySemanticsEstablished -eq $Reference.DedicatedSharedMemorySemanticsEstablished -and
        $Evidence.VersionSocket -eq $Reference.VersionSocket -and
        $Evidence.VersionDashboard -eq $Reference.VersionDashboard -and
        $Evidence.SystemSocket -eq $Reference.SystemSocket -and
        $Evidence.SystemDashboard -eq $Reference.SystemDashboard -and
        $Evidence.VersionResidual -eq $Reference.VersionResidual -and
        $Evidence.SystemResidual -eq $Reference.SystemResidual -and
        @($Evidence.Diagnostics).Count -eq @($Reference.Diagnostics).Count -and
        -not (@($Evidence.Diagnostics | Where-Object { $Reference.Diagnostics -cnotcontains $_ }).Count)
}

function Read-TrxSummary {
    param(
        [object] $Artifact,
        [ValidateSet('Deterministic','Trusted','Offline')]
        [string] $Kind)

    $settings = New-Object System.Xml.XmlReaderSettings
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.MaxCharactersInDocument = $maximumTrxBytes
    $memory = New-Object System.IO.MemoryStream(,$Artifact.Bytes)
    $reader = [System.Xml.XmlReader]::Create($memory, $settings)
    $document = New-Object System.Xml.XmlDocument
    $document.XmlResolver = $null
    try { $document.Load($reader) }
    finally { $reader.Dispose(); $memory.Dispose() }

    $namespace = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'
    Assert-Condition ($document.DocumentElement.LocalName -ceq 'TestRun' -and
        $document.DocumentElement.NamespaceURI -ceq $namespace) 'TRX root or namespace differs.'
    $manager = New-Object System.Xml.XmlNamespaceManager($document.NameTable)
    $manager.AddNamespace('t', $namespace)
    $nodes = @($document.SelectNodes('/t:TestRun/t:Results/t:UnitTestResult', $manager))
    Assert-Condition ($nodes.Count -gt 0) 'TRX contains no test results.'
    $names = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
    $results = @(
        foreach ($node in $nodes) {
            $name = [string]$node.GetAttribute('testName')
            $outcome = [string]$node.GetAttribute('outcome')
            $testId = [string]$node.GetAttribute('testId')
            $executionId = [string]$node.GetAttribute('executionId')
            $null = [guid]::Parse($testId)
            $null = [guid]::Parse($executionId)
            Assert-Condition (-not [string]::IsNullOrWhiteSpace($name) -and $name.Length -le 256 -and $names.Add($name)) 'TRX test identities are invalid or duplicated.'
            Assert-Condition ($outcome -cin @('Passed','Failed','Inconclusive','NotExecuted')) 'TRX contains an unsupported outcome.'
            $messageNode = $node.SelectSingleNode('t:Output/t:ErrorInfo/t:Message', $manager)
            $message = if ($null -eq $messageNode) { '' } else { [string]$messageNode.InnerText }
            [pscustomobject]@{
                Name = $name; Outcome = $outcome; Message = $message
                TestId = $testId; ExecutionId = $executionId
            }
        })
    $definitionNodes = @($document.SelectNodes('/t:TestRun/t:TestDefinitions/t:UnitTest', $manager))
    Assert-Condition ($definitionNodes.Count -eq $results.Count) 'TRX definition count differs from its results.'
    $definitions = @{}
    $definitionNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
    foreach ($definitionNode in $definitionNodes) {
        $testId = [string]$definitionNode.GetAttribute('id')
        $null = [guid]::Parse($testId)
        $definitionName = [string]$definitionNode.GetAttribute('name')
        Assert-Condition (-not $definitions.ContainsKey($testId) -and
            $definitionNames.Add($definitionName)) 'TRX definitions are duplicated.'
        $executionNode = $definitionNode.SelectSingleNode('t:Execution', $manager)
        $methodNode = $definitionNode.SelectSingleNode('t:TestMethod', $manager)
        Assert-Condition ($null -ne $executionNode -and $null -ne $methodNode) 'TRX definition is incomplete.'
        $executionId = [string]$executionNode.GetAttribute('id')
        $null = [guid]::Parse($executionId)
        $storageName = [System.IO.Path]::GetFileName([string]$definitionNode.GetAttribute('storage'))
        $codeBaseName = [System.IO.Path]::GetFileName([string]$methodNode.GetAttribute('codeBase'))
        $className = [string]$methodNode.GetAttribute('className')
        $methodName = [string]$methodNode.GetAttribute('name')
        Assert-Condition ($definitionName -ceq $methodName -or
            ($definitionName.StartsWith($methodName + ' (', [System.StringComparison]::Ordinal) -and
                $definitionName.EndsWith(')', [System.StringComparison]::Ordinal))) 'TRX definition and method names differ.'
        $definitions[$testId] = [pscustomobject]@{
            Name = $definitionName; ExecutionId = $executionId
            StorageName = $storageName; CodeBaseName = $codeBaseName
            ClassName = $className; MethodName = $methodName
        }
    }
    $executionIds = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($result in $results) {
        Assert-Condition ($definitions.ContainsKey($result.TestId) -and
            $executionIds.Add($result.ExecutionId)) 'TRX result provenance is missing or duplicated.'
        $definition = $definitions[$result.TestId]
        Assert-Condition ($result.Name -ceq $definition.Name -and
            $result.ExecutionId -ceq $definition.ExecutionId) 'TRX result does not match its definition.'
    }
    $entryNodes = @($document.SelectNodes('/t:TestRun/t:TestEntries/t:TestEntry', $manager))
    Assert-Condition ($entryNodes.Count -eq $results.Count) 'TRX entry count differs from its results.'
    $entryPairs = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($entryNode in $entryNodes) {
        $pair = ([string]$entryNode.GetAttribute('testId')) + '|' + ([string]$entryNode.GetAttribute('executionId'))
        Assert-Condition ($entryPairs.Add($pair)) 'TRX entries are duplicated.'
    }
    foreach ($result in $results) {
        Assert-Condition ($entryPairs.Contains($result.TestId + '|' + $result.ExecutionId)) 'TRX result lacks its entry binding.'
    }
    $expectedAssembly = if ($Kind -ceq 'Deterministic') {
        'HardwareInspection.LlmFitSpike.Tests.dll'
    }
    else {
        'HardwareInspection.LlmFitSpike.IntegrationTests.dll'
    }
    foreach ($definition in $definitions.Values) {
        Assert-Condition ([string]::Equals($definition.StorageName, $expectedAssembly, [System.StringComparison]::OrdinalIgnoreCase) -and
            [string]::Equals($definition.CodeBaseName, $expectedAssembly, [System.StringComparison]::OrdinalIgnoreCase)) 'TRX assembly provenance differs.'
        if ($Kind -ceq 'Deterministic') {
            Assert-Condition ($definition.ClassName.StartsWith('HardwareInspection.LlmFitSpike.Tests.', [System.StringComparison]::Ordinal)) 'Deterministic TRX class provenance differs.'
        }
        else {
            Assert-Condition ($definition.ClassName -ceq 'HardwareInspection.LlmFitSpike.IntegrationTests.LlmFitCandidateIntegrationTests') 'Operational TRX class provenance differs.'
        }
    }
    if ($Kind -ceq 'Trusted') {
        Assert-Condition ($results.Count -eq 3) 'Trusted TRX count differs.'
        foreach ($expected in $trustedTestNames) {
            Assert-Condition (@($results | Where-Object { $_.Name -ceq $expected }).Count -eq 1) 'Trusted TRX test identities differ.'
        }
    }
    elseif ($Kind -ceq 'Offline') {
        Assert-Condition ($results.Count -eq 1 -and $results[0].Name -ceq $offlineTestName) 'Offline TRX identity/count differs.'
    }
    else {
        Assert-Condition ($results.Count -ge 174) 'Deterministic TRX has fewer than 174 tests.'
    }

    $counters = $document.SelectSingleNode('/t:TestRun/t:ResultSummary/t:Counters', $manager)
    Assert-Condition ($null -ne $counters) 'TRX counters are absent.'
    $counterNames = @('total','executed','passed','failed','error','timeout','aborted','inconclusive','passedButRunAborted','notRunnable','notExecuted','disconnected','warning','completed','inProgress','pending')
    $counterValues = @{}
    foreach ($name in $counterNames) {
        $attribute = $counters.Attributes[$name]
        Assert-Condition ($null -ne $attribute -and $attribute.Value -cmatch '\A[0-9]+\z') "TRX counter is invalid: $name."
        $counterValues[$name] = [int]$attribute.Value
    }
    $passed = @($results | Where-Object Outcome -ceq 'Passed').Count
    $failed = @($results | Where-Object Outcome -ceq 'Failed').Count
    $inconclusive = @($results | Where-Object Outcome -ceq 'Inconclusive').Count
    $notExecuted = @($results | Where-Object Outcome -ceq 'NotExecuted').Count
    Assert-Condition ($counterValues.total -eq $results.Count -and
        $counterValues.passed -eq $passed -and $counterValues.failed -eq $failed -and
        $counterValues.inconclusive -eq $inconclusive -and
        $counterValues.notExecuted -eq $notExecuted -and
        $counterValues.executed -eq ($results.Count - $notExecuted)) 'TRX counters disagree with its results.'
    $otherFailures = 0
    foreach ($name in @('error','timeout','aborted','passedButRunAborted','notRunnable','disconnected','warning','completed','inProgress','pending')) {
        $otherFailures += $counterValues[$name]
    }
    return [pscustomobject]@{
        Kind = $Kind; Total = $results.Count; Passed = $passed; Failed = $failed
        Inconclusive = $inconclusive; NotExecuted = $notExecuted
        NotRun = $inconclusive + $notExecuted; OtherFailures = $otherFailures
        Results = @($results)
        AllPassed = $passed -eq $results.Count -and $otherFailures -eq 0
    }
}

function Test-TrustedPrerequisiteBlock {
    param([object] $Summary)
    if ($Summary.Total -ne 3 -or $Summary.Passed -ne 0 -or $Summary.Failed -ne 0 -or
        $Summary.NotRun -ne 3 -or $Summary.OtherFailures -ne 0) { return $false }
    foreach ($result in $Summary.Results) {
        if ($result.Outcome -cnotin @('Inconclusive','NotExecuted') -or
            $result.Message -cnotmatch '\A(?:Assert\.Inconclusive\.\s+)?TrustedWindowsIntel prerequisites are missing: (?:GRANITE_LLMFIT_CANDIDATE_ROOT|GRANITE_LLMFIT_WINDOWS_REFERENCE|GRANITE_LLMFIT_GATE1_OUTPUT)(?:, (?:GRANITE_LLMFIT_CANDIDATE_ROOT|GRANITE_LLMFIT_WINDOWS_REFERENCE|GRANITE_LLMFIT_GATE1_OUTPUT))*\. Configure the trusted target exactly as documented in the Gate 1 runbook\.\z') {
            return $false
        }
    }
    return $true
}

function Test-TrustedWrongTargetBlock {
    param([object] $Summary)
    return $Summary.Total -eq 3 -and $Summary.Passed -eq 0 -and
        @($Summary.Results | Where-Object {
            $_.Outcome -cne 'Failed' -or $_.Message -cnotmatch 'HI-GATE1-WRONG-TARGET'
        }).Count -eq 0
}

function Test-OfflinePrerequisiteBlock {
    param([object] $Summary)
    return $Summary.Total -eq 1 -and $Summary.Passed -eq 0 -and
        $Summary.Results[0].Outcome -ceq 'Failed' -and
        $Summary.Results[0].Message -cmatch 'HI-GATE1-OFFLINE-PRECONDITION-FAILED'
}

function Test-OfflineNotEvaluatedBlock {
    param([object] $Summary)
    return $Summary.Total -eq 1 -and $Summary.Passed -eq 0 -and $Summary.NotRun -eq 1 -and
        $Summary.Results[0].Message -cmatch '\ATrustedOffline prerequisites are missing: '
}

function Read-EvidenceArtifact {
    param([object] $Artifact, [ValidateSet('Trusted','Offline')] [string] $Kind)
    $parsed = Open-StrictJson $Artifact
    try {
        $disposition = Assert-String $parsed.Value 'disposition'
    }
    finally { }
    if ($disposition -ceq 'Blocked') {
        $diagnostic = if ($Kind -ceq 'Trusted') { 'HI-GATE1-WRONG-TARGET' } else { 'HI-GATE1-OFFLINE-PRECONDITION-FAILED' }
        return Read-MinimalBlockedEnvelope $Artifact $diagnostic
    }
    return Read-FullEvidence $Artifact
}

function Read-ReferenceArtifact {
    param([object] $Artifact)
    $parsed = Open-StrictJson $Artifact
    try { $disposition = Assert-String $parsed.Value 'disposition' -Nullable }
    catch { $disposition = $null }
    finally { }
    if ($disposition -ceq 'Blocked') {
        return Read-MinimalBlockedEnvelope $Artifact 'HI-GATE1-WRONG-TARGET'
    }
    return Read-FullReference $Artifact
}

function Format-YesNo {
    param([bool] $Value)
    if ($Value) { return 'Yes' }
    return 'No'
}

function Format-Number {
    param([double] $Value)
    return $Value.ToString('0.###', [System.Globalization.CultureInfo]::InvariantCulture)
}

function Add-ArtifactRow {
    param([System.Text.StringBuilder] $Builder, [string] $Label, [AllowNull()][object] $Artifact)
    if ($null -eq $Artifact) {
        [void]$Builder.AppendLine("| $Label | Unavailable |")
    }
    else {
        [void]$Builder.AppendLine("| $Label | ``$($Artifact.Sha256)`` |")
    }
}

function Write-AtomicMarkdown {
    param([string] $Path, [string] $Content, [object[]] $Artifacts)
    $markers = @('{{','}}','@@','TBD','TODO','<replace','[replace','PLACEHOLDER')
    foreach ($marker in $markers) {
        Assert-Condition ($Content.IndexOf($marker, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) 'Generated Markdown contains an unresolved template marker.'
    }
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    Assert-Condition (-not $fullPath.StartsWith('\\', [System.StringComparison]::Ordinal)) 'Output must be local.'
    foreach ($artifact in @($Artifacts | Where-Object { $null -ne $_ })) {
        Assert-Condition (-not [string]::Equals($fullPath, $artifact.Path, [System.StringComparison]::OrdinalIgnoreCase)) 'Output would overwrite an input artifact.'
    }
    $directory = [System.IO.Path]::GetDirectoryName($fullPath)
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($directory)) 'Output directory is invalid.'
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    Assert-OrdinaryPathChain -FullPath $directory
    $directoryAttributes = [System.IO.File]::GetAttributes($directory)
    Assert-Condition (($directoryAttributes -band [System.IO.FileAttributes]::ReparsePoint) -eq 0) 'Output directory is a reparse point.'
    if ([System.IO.File]::Exists($fullPath)) {
        $attributes = [System.IO.File]::GetAttributes($fullPath)
        Assert-Condition (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -eq 0) 'Output is a reparse point.'
    }
    $temporary = [System.IO.Path]::Combine($directory, [System.IO.Path]::GetFileName($fullPath) + '.tmp-' + [guid]::NewGuid().ToString('N'))
    try {
        $bytes = $strictUtf8.GetBytes($Content)
        $stream = New-Object System.IO.FileStream(
            $temporary, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write,
            [System.IO.FileShare]::None, 4096, [System.IO.FileOptions]::WriteThrough)
        try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) }
        finally { $stream.Dispose() }
        if ([System.IO.File]::Exists($fullPath)) {
            $backup = [System.IO.Path]::Combine(
                $directory,
                [System.IO.Path]::GetFileName($fullPath) + '.bak-' + [guid]::NewGuid().ToString('N'))
            try { [System.IO.File]::Replace($temporary, $fullPath, $backup, $true) }
            finally {
                if ([System.IO.File]::Exists($backup)) { [System.IO.File]::Delete($backup) }
            }
        }
        else {
            [System.IO.File]::Move($temporary, $fullPath)
        }
    }
    finally {
        if ([System.IO.File]::Exists($temporary)) { [System.IO.File]::Delete($temporary) }
    }
}

Assert-Condition (Test-LowerHex $RepositoryCommit 40) 'RepositoryCommit must be lowercase 40-character hexadecimal.'
$repositoryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, '..', '..'))
$head = (& git -C $repositoryRoot rev-parse HEAD)
Assert-Condition ($LASTEXITCODE -eq 0 -and $null -ne $head) 'Repository HEAD could not be resolved.'
$head = ([string]$head).Trim()
Assert-Condition ($head -ceq $RepositoryCommit) 'RepositoryCommit does not identify the evaluated source HEAD.'
$branch = (& git -C $repositoryRoot branch --show-current)
Assert-Condition ($LASTEXITCODE -eq 0 -and $null -ne $branch) 'Repository branch could not be resolved.'
$branch = ([string]$branch).Trim()
Assert-Condition ($branch.Length -ge 1 -and $branch.Length -le 128 -and
    $branch -cmatch '\A[A-Za-z0-9][A-Za-z0-9._/-]*\z' -and
    -not $branch.Contains('..')) 'Repository branch has an unsafe shape.'
$fullRequestedOutput = [System.IO.Path]::GetFullPath($OutputMarkdown)
$retainedEvidenceDirectory = [System.IO.Path]::GetFullPath(
    [System.IO.Path]::Combine($repositoryRoot, 'docs', 'testing', 'evidence'))
$retainedPrefix = $retainedEvidenceDirectory.TrimEnd(@(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar)) + [System.IO.Path]::DirectorySeparatorChar
if ($fullRequestedOutput.StartsWith($retainedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    $dirty = @(& git -C $repositoryRoot status --porcelain=v1 --untracked-files=all)
    Assert-Condition ($LASTEXITCODE -eq 0) 'Repository cleanliness could not be established.'
    Assert-Condition ($dirty.Count -eq 0) 'Retained evidence requires a clean evaluated-source worktree.'
}

$deterministicArtifact = Read-BoundedArtifact $DeterministicTrx $maximumTrxBytes 'Deterministic TRX'
$trustedArtifact = Read-BoundedArtifact $TrustedWindowsTrx $maximumTrxBytes 'Trusted Windows TRX' -Optional
$offlineTrxArtifact = Read-BoundedArtifact $OfflineTrx $maximumTrxBytes 'Offline TRX' -Optional
$evidenceArtifact = Read-BoundedArtifact $EvidenceJson $maximumJsonBytes 'Trusted candidate evidence' -Optional
$referenceArtifact = Read-BoundedArtifact $WindowsReferenceJson $maximumJsonBytes 'Windows reference evidence' -Optional
$offlineEvidenceArtifact = Read-BoundedArtifact $OfflineEvidenceJson $maximumJsonBytes 'Offline candidate evidence' -Optional
$allArtifacts = @($evidenceArtifact, $referenceArtifact, $offlineEvidenceArtifact,
    $deterministicArtifact, $trustedArtifact, $offlineTrxArtifact)

$deterministicSummary = Read-TrxSummary $deterministicArtifact 'Deterministic'
$trustedSummary = if ($null -eq $trustedArtifact) { $null } else { Read-TrxSummary $trustedArtifact 'Trusted' }
$offlineSummary = if ($null -eq $offlineTrxArtifact) { $null } else { Read-TrxSummary $offlineTrxArtifact 'Offline' }
$trustedEvidence = if ($null -eq $evidenceArtifact) { $null } else { Read-EvidenceArtifact $evidenceArtifact 'Trusted' }
$reference = if ($null -eq $referenceArtifact) { $null } else { Read-ReferenceArtifact $referenceArtifact }
$offlineEvidence = if ($null -eq $offlineEvidenceArtifact) { $null } else { Read-EvidenceArtifact $offlineEvidenceArtifact 'Offline' }

$trustedPrerequisiteBlocked = $null -ne $trustedSummary -and (Test-TrustedPrerequisiteBlock $trustedSummary)
$trustedWrongTargetBlocked = $null -ne $trustedSummary -and (Test-TrustedWrongTargetBlock $trustedSummary)
$offlinePrerequisiteBlocked = $null -ne $offlineSummary -and (Test-OfflinePrerequisiteBlock $offlineSummary)
$offlineNotEvaluatedBlocked = $null -ne $offlineSummary -and (Test-OfflineNotEvaluatedBlock $offlineSummary)

if ($null -ne $trustedEvidence -and $trustedEvidence.Minimal) {
    Assert-Condition ($trustedWrongTargetBlocked) 'Minimal trusted Blocked evidence lacks matching TRX proof.'
}
if ($null -ne $reference -and $reference.Minimal) {
    Assert-Condition ($trustedWrongTargetBlocked) 'Minimal reference Blocked evidence lacks matching TRX proof.'
}
if ($offlinePrerequisiteBlocked) {
    Assert-Condition ($null -ne $offlineEvidence -and $offlineEvidence.Minimal) 'Offline precondition failure lacks its strict minimal envelope.'
}
if ($null -ne $offlineEvidence -and $offlineEvidence.Minimal) {
    Assert-Condition ($offlinePrerequisiteBlocked) 'Minimal offline Blocked evidence lacks matching TRX proof.'
}

$hasFullTrusted = $null -ne $trustedEvidence -and -not $trustedEvidence.Minimal
$hasFullReference = $null -ne $reference -and -not $reference.Minimal
$hasFullOffline = $null -ne $offlineEvidence -and -not $offlineEvidence.Minimal
$positiveTrustedBlock = $trustedPrerequisiteBlocked -or $trustedWrongTargetBlocked
$positiveOfflineBlock = $offlinePrerequisiteBlocked
$deterministicFailed = -not $deterministicSummary.AllPassed
$trustedFailureProof = $null -ne $trustedSummary -and -not $trustedSummary.AllPassed -and
    -not $positiveTrustedBlock
$offlineFailureProof = $null -ne $offlineSummary -and -not $offlineSummary.AllPassed -and
    -not $positiveOfflineBlock

if (-not $hasFullTrusted -and -not $positiveTrustedBlock -and -not $deterministicFailed -and
    -not $trustedFailureProof) {
    throw 'Trusted evidence is absent without positive prerequisite-block proof.'
}
if ($hasFullTrusted -and $null -eq $trustedSummary) { throw 'Full trusted evidence requires its TRX.' }
if ($hasFullReference -and -not $hasFullTrusted) { throw 'A full Windows reference requires full trusted evidence.' }
if ($hasFullTrusted -and -not $hasFullReference -and $trustedEvidence.Disposition -cne 'Rejected' -and -not $deterministicFailed) {
    Assert-Condition $trustedFailureProof 'Candidate-judging evidence requires the Windows reference.'
}
if ($hasFullTrusted -and $trustedEvidence.Disposition -cne 'Rejected' -and
    $trustedEvidence.Functional -and -not $positiveOfflineBlock -and -not $offlineFailureProof -and
    -not $hasFullOffline) {
    throw 'A functional candidate decision requires full offline evidence or positive offline-block proof.'
}
if ($hasFullOffline -and $null -eq $offlineSummary) { throw 'Full offline evidence requires its TRX.' }
if (-not $hasFullOffline -and -not $positiveOfflineBlock -and $hasFullTrusted -and
    $trustedEvidence.Disposition -cne 'Rejected' -and $trustedEvidence.Functional -and
    -not $deterministicFailed -and -not $offlineFailureProof) {
    throw 'Offline evidence is absent without positive prerequisite-block proof.'
}

$claimsConsistent = $true
if ($hasFullTrusted -and $hasFullReference) {
    $claimsConsistent = Test-FullEvidenceReferenceConsistency $trustedEvidence $reference
}
$referencePassed = $hasFullReference -and $reference.WindowsX64 -and
    $reference.WindowsIntelCpuObserved -and $reference.WindowsIntelGpuObserved -and
    $reference.CaptureWithinThirtySeconds -and $reference.CpuIdentityMatched -and
    $reference.LogicalProcessorCountMatched -and $reference.TotalRamWithinTolerance -and
    $reference.AvailableRamWithinTolerance -and $reference.JsonValid -and
    $reference.RequiredCpuRamPresent -and $reference.GateExitCode -eq 0 -and
    $null -ne $reference.AuthenticodeObservationSha256 -and
    -not $reference.VersionSocket -and -not $reference.VersionDashboard -and
    -not $reference.SystemSocket -and -not $reference.SystemDashboard -and
    -not $reference.VersionResidual -and -not $reference.SystemResidual
$offlinePassed = $hasFullOffline -and $offlineEvidence.Functional -and
    -not $offlineEvidence.VersionSocket -and -not $offlineEvidence.VersionDashboard -and
    -not $offlineEvidence.SystemSocket -and -not $offlineEvidence.SystemDashboard -and
    -not $offlineEvidence.VersionResidual -and -not $offlineEvidence.SystemResidual
$trustedTestsPassed = $null -ne $trustedSummary -and $trustedSummary.AllPassed
$offlineTestsPassed = $null -ne $offlineSummary -and $offlineSummary.AllPassed

$unexpectedRequiredFailure = $false
if ($null -ne $trustedSummary -and -not $trustedTestsPassed -and -not $positiveTrustedBlock) { $unexpectedRequiredFailure = $true }
if ($null -ne $offlineSummary -and -not $offlineTestsPassed -and -not $positiveOfflineBlock) { $unexpectedRequiredFailure = $true }
$evidenceRejected = $hasFullTrusted -and $trustedEvidence.Disposition -ceq 'Rejected'
$offlineRejected = $hasFullOffline -and $offlineEvidence.Disposition -ceq 'Rejected'
$passingDispositionMismatch = $hasFullTrusted -and $hasFullReference -and $hasFullOffline -and
    @(@($trustedEvidence.Disposition, $reference.Disposition, $offlineEvidence.Disposition) |
        Select-Object -Unique).Count -ne 1
$acceptedWithoutControlledDisposition =
    ($hasFullTrusted -and $trustedEvidence.Disposition -ceq 'AcceptedForFunctionalEvaluation') -or
    ($hasFullReference -and $reference.Disposition -ceq 'AcceptedForFunctionalEvaluation') -or
    ($hasFullOffline -and $offlineEvidence.Disposition -ceq 'AcceptedForFunctionalEvaluation')
$staleEvidenceAlongsideBlock =
    ($positiveTrustedBlock -and ($hasFullTrusted -or $hasFullReference)) -or
    ($positiveOfflineBlock -and $hasFullOffline)
$candidateFailure = $evidenceRejected -or $offlineRejected -or
    ($hasFullTrusted -and -not $trustedEvidence.Functional) -or
    ($hasFullReference -and -not $referencePassed) -or
    ($hasFullOffline -and -not $offlinePassed) -or -not $claimsConsistent -or
    $passingDispositionMismatch -or $acceptedWithoutControlledDisposition -or
    $staleEvidenceAlongsideBlock

$decisionCodes = New-Object 'System.Collections.Generic.List[string]'
if ($deterministicFailed -or $unexpectedRequiredFailure -or $candidateFailure) {
    $finalDisposition = 'Rejected'
    $decisionCodes.Add('HI-GATE1-REQUIRED-TEST-FAILURE')
}
elseif ($positiveTrustedBlock -or $positiveOfflineBlock) {
    $finalDisposition = 'Blocked'
    if ($positiveTrustedBlock) { $decisionCodes.Add('HI-GATE1-WRONG-TARGET') }
    if ($positiveOfflineBlock) { $decisionCodes.Add('HI-GATE1-OFFLINE-PRECONDITION-FAILED') }
}
else {
    Assert-Condition ($hasFullTrusted -and $hasFullReference -and $hasFullOffline -and
        $trustedTestsPassed -and $offlineTestsPassed) 'Complete passing inputs are required for a functional decision.'
    $inputDispositions = @($trustedEvidence.Disposition, $reference.Disposition, $offlineEvidence.Disposition)
    Assert-Condition (@($inputDispositions | Select-Object -Unique).Count -eq 1) 'Passing evidence dispositions disagree.'
    $finalDisposition = $inputDispositions[0]
    Assert-Condition ($finalDisposition -ceq 'FunctionalPassWithPackagingConcern') 'Passing disposition is invalid.'
}

foreach ($artifact in @($allArtifacts | Where-Object { $null -ne $_ })) {
    $limit = if ($artifact.Label.EndsWith('TRX', [System.StringComparison]::Ordinal)) { $maximumTrxBytes } else { $maximumJsonBytes }
    $fresh = Read-BoundedArtifact $artifact.Path $limit $artifact.Label
    Assert-Condition ($fresh.Sha256 -ceq $artifact.Sha256) "$($artifact.Label) changed during report generation."
}

$builder = New-Object System.Text.StringBuilder
[void]$builder.AppendLine('# Hardware Inspection LLM Fit Gate 1 verification')
[void]$builder.AppendLine()
[void]$builder.AppendLine('This record was generated from bounded, strict input artifacts. It records the Gate 1 decision only; it is not production approval.')
[void]$builder.AppendLine()
[void]$builder.AppendLine('## Decision')
[void]$builder.AppendLine()
[void]$builder.AppendLine('| Field | Result |')
[void]$builder.AppendLine('|---|---|')
[void]$builder.AppendLine("| Branch | ``$branch`` |")
[void]$builder.AppendLine("| Evaluated-source commit | ``$RepositoryCommit`` |")
[void]$builder.AppendLine("| Final disposition | **$finalDisposition** |")
$decisionText = if ($decisionCodes.Count -eq 0) { 'None' } else { ($decisionCodes.ToArray() | Sort-Object) -join ', ' }
[void]$builder.AppendLine("| Decision code | $decisionText |")
$candidateDecisionMade = $finalDisposition -cne 'Blocked' -and $hasFullTrusted
[void]$builder.AppendLine("| Candidate decision made | $(Format-YesNo $candidateDecisionMade) |")
if ($finalDisposition -ceq 'Blocked') {
    [void]$builder.AppendLine('| Gate 1 satisfied | No; Gate 2 must not start |')
}
elseif ($finalDisposition -ceq 'Rejected') {
    [void]$builder.AppendLine('| Gate 1 satisfied | No; Gate 2 must not start |')
}
else {
    [void]$builder.AppendLine('| Gate 1 behavioral gate | Satisfied for the scope stated here |')
}

[void]$builder.AppendLine()
[void]$builder.AppendLine('## Source artifact integrity')
[void]$builder.AppendLine()
[void]$builder.AppendLine('| Artifact | SHA-256 or availability |')
[void]$builder.AppendLine('|---|---|')
Add-ArtifactRow $builder 'Trusted candidate evidence' $evidenceArtifact
Add-ArtifactRow $builder 'Windows reference evidence' $referenceArtifact
Add-ArtifactRow $builder 'Offline candidate evidence' $offlineEvidenceArtifact
Add-ArtifactRow $builder 'Deterministic TRX' $deterministicArtifact
Add-ArtifactRow $builder 'Trusted Windows TRX' $trustedArtifact
Add-ArtifactRow $builder 'Offline TRX' $offlineTrxArtifact
[void]$builder.AppendLine()
[void]$builder.AppendLine('- TRX freshness and execution against the evaluated-source commit were established operationally; the artifact hashes do not cryptographically prove freshness or source binding.')

[void]$builder.AppendLine()
[void]$builder.AppendLine('## Test outcomes')
[void]$builder.AppendLine()
[void]$builder.AppendLine('| Route | Result |')
[void]$builder.AppendLine('|---|---|')
[void]$builder.AppendLine("| Deterministic | $($deterministicSummary.Passed) passed, $($deterministicSummary.Failed + $deterministicSummary.OtherFailures) failed, $($deterministicSummary.NotRun) not run |")
if ($null -eq $trustedSummary) {
    [void]$builder.AppendLine('| Trusted Windows Intel | Not run; TRX unavailable |')
}
else {
    [void]$builder.AppendLine("| Trusted Windows Intel | $($trustedSummary.Passed) passed, $($trustedSummary.Failed + $trustedSummary.OtherFailures) failed, $($trustedSummary.NotRun) not run |")
}
if ($null -eq $offlineSummary) {
    [void]$builder.AppendLine('| Controlled offline | Not run; TRX unavailable |')
}
else {
    [void]$builder.AppendLine("| Controlled offline | $($offlineSummary.Passed) passed, $($offlineSummary.Failed + $offlineSummary.OtherFailures) failed, $($offlineSummary.NotRun) not run |")
}

if ($positiveTrustedBlock -or $positiveOfflineBlock) {
    [void]$builder.AppendLine()
    [void]$builder.AppendLine('## Blocking prerequisites')
    [void]$builder.AppendLine()
    if ($positiveTrustedBlock) {
        [void]$builder.AppendLine('- The named Windows Intel trusted-target prerequisites were unavailable or the configured target was rejected before candidate evaluation.')
    }
    if ($positiveOfflineBlock) {
        [void]$builder.AppendLine('- The controlled offline prerequisite was unavailable, so offline behavior was not accepted.')
    }
    if ($null -eq $evidenceArtifact) { [void]$builder.AppendLine('- Trusted candidate evidence | Unavailable') }
    if ($null -eq $referenceArtifact) { [void]$builder.AppendLine('- Windows reference evidence | Unavailable') }
    if ($null -eq $offlineEvidenceArtifact) { [void]$builder.AppendLine('- Offline candidate evidence | Unavailable') }
    if ($offlinePrerequisiteBlocked) {
        [void]$builder.AppendLine('- Offline prerequisite evidence and TRX have no intrinsic run-binding field; their same-run creation was controlled operationally, and their separate hashes do not independently prove pairing or freshness.')
    }
}

if ($hasFullTrusted) {
    [void]$builder.AppendLine()
    [void]$builder.AppendLine('## Candidate identity and fixed execution boundary')
    [void]$builder.AppendLine()
    [void]$builder.AppendLine('| Field | Result |')
    [void]$builder.AppendLine('|---|---|')
    [void]$builder.AppendLine('| Candidate tag | `v1.1.9` |')
    [void]$builder.AppendLine('| Candidate ID | `llmfit-v1.1.9-win-x64` |')
    [void]$builder.AppendLine('| Upstream release commit | `a02e13f1013ed69889ff44426a651bf7c68c292e` |')
    [void]$builder.AppendLine('| Expected archive SHA-256 | `a030269d7cc8a5bf40383f526a481655d698ec71dd792a25b06510cef9f8b738` |')
    $archiveResult = if ($null -eq $trustedEvidence.ObservedArchiveSha256) { 'Not observed' } else { "``$($trustedEvidence.ObservedArchiveSha256)``" }
    [void]$builder.AppendLine("| Observed archive SHA-256 | $archiveResult |")
    [void]$builder.AppendLine('| Expected executable SHA-256 | `db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19` |')
    $executableResult = if ($null -eq $trustedEvidence.ObservedExecutableSha256) { 'Not observed' } else { "``$($trustedEvidence.ObservedExecutableSha256)``" }
    [void]$builder.AppendLine("| Observed executable SHA-256 | $executableResult |")
    $versionResult = if ($null -eq $trustedEvidence.ReportedVersion) { 'Not reported' } else { "``$($trustedEvidence.ReportedVersion)``" }
    [void]$builder.AppendLine("| Reported version | $versionResult |")
    $peResult = if ($null -eq $trustedEvidence.ObservedPeMachine) { 'Not observed' } else { "``$($trustedEvidence.ObservedPeMachine)``" }
    [void]$builder.AppendLine("| PE architecture | $peResult |")
    [void]$builder.AppendLine('| License | MIT; the upstream `LICENSE` must be retained for any permitted redistribution |')
    [void]$builder.AppendLine('| Version command | `--version` |')
    [void]$builder.AppendLine('| System command | `--no-dashboard --json system` |')
    [void]$builder.AppendLine('| Other commands | Prohibited: `serve`, dashboard, REST, model recommendation, and arbitrary arguments |')

    [void]$builder.AppendLine()
    [void]$builder.AppendLine('## Candidate observations')
    [void]$builder.AppendLine()
    [void]$builder.AppendLine('| Check | Result |')
    [void]$builder.AppendLine('|---|---|')
    [void]$builder.AppendLine("| JSON and required CPU/RAM schema | $(Format-YesNo ($trustedEvidence.JsonValid -and $trustedEvidence.RequiredCpuRamPresent)) |")
    [void]$builder.AppendLine('| Windows Intel GPU dedicated/shared memory semantics | Not established; candidate VRAM must not be interpreted as either |')
    [void]$builder.AppendLine('| Intel NPU detection | `DetectionUnavailable`; no presence or absence conclusion |')
    [void]$builder.AppendLine("| Candidate-owned socket/listener observed | $(Format-YesNo ($trustedEvidence.VersionSocket -or $trustedEvidence.SystemSocket)) |")
    [void]$builder.AppendLine("| Dashboard port 8787 observed | $(Format-YesNo ($trustedEvidence.VersionDashboard -or $trustedEvidence.SystemDashboard)) |")
    [void]$builder.AppendLine("| Residual candidate process observed | $(Format-YesNo ($trustedEvidence.VersionResidual -or $trustedEvidence.SystemResidual)) |")
    [void]$builder.AppendLine("| Authenticode observation | ``$($trustedEvidence.AuthenticodeStatus)``; upstream signing claim mismatch $(Format-YesNo ($trustedEvidence.Diagnostics -ccontains 'HI-LLMFIT-SIGNATURE-CLAIM-MISMATCH')) |")
    [void]$builder.AppendLine("| Transitive dependency-license inventory | $(if ($trustedEvidence.Diagnostics -ccontains 'HI-LLMFIT-DEPENDENCY-LICENSE-INVENTORY-PENDING') { 'Pending; production redistribution remains blocked' } else { 'No pending diagnostic recorded; independent dependency/license disposition not established' }) |")
    [void]$builder.AppendLine("| Schema-documentation drift | $(Format-YesNo ($trustedEvidence.Diagnostics -ccontains 'HI-LLMFIT-SCHEMA-DOCUMENTATION-DRIFT')) |")
}

if ($hasFullReference) {
    [void]$builder.AppendLine()
    [void]$builder.AppendLine('## Windows comparison (derived values only)')
    [void]$builder.AppendLine()
    [void]$builder.AppendLine('| Comparison | Result |')
    [void]$builder.AppendLine('|---|---|')
    [void]$builder.AppendLine("| Named Windows Intel x64 target | $(Format-YesNo ($reference.WindowsX64 -and $reference.WindowsIntelCpuObserved -and $reference.WindowsIntelGpuObserved)) |")
    [void]$builder.AppendLine("| Capture within 30 seconds | $(Format-YesNo $reference.CaptureWithinThirtySeconds); $($reference.IntervalMilliseconds) ms |")
    [void]$builder.AppendLine("| CPU identity matched | $(Format-YesNo $reference.CpuIdentityMatched) |")
    [void]$builder.AppendLine("| Logical processors | Windows $($reference.WindowsLogicalProcessorCount), candidate $($reference.LlmFitLogicalProcessorCount), matched $(Format-YesNo $reference.LogicalProcessorCountMatched) |")
    [void]$builder.AppendLine("| Total RAM | delta $(Format-Number $reference.TotalRamDeltaGiB) GiB, tolerance $(Format-Number $reference.TotalRamToleranceGiB) GiB, passed $(Format-YesNo $reference.TotalRamWithinTolerance) |")
    [void]$builder.AppendLine("| Available RAM | delta $(Format-Number $reference.AvailableRamDeltaGiB) GiB, tolerance $(Format-Number $reference.AvailableRamToleranceGiB) GiB, passed $(Format-YesNo $reference.AvailableRamWithinTolerance) |")
    [void]$builder.AppendLine("| Intel GPU identity | ``$($reference.IntelGpuComparisonStatus)``; memory semantics remain unaccepted |")
}

[void]$builder.AppendLine()
[void]$builder.AppendLine('## Privacy result')
[void]$builder.AppendLine()
[void]$builder.AppendLine('- The report generator accepted only exact JSON property sets, fixed diagnostic enums, fixed test identities, bounded strict UTF-8/DTD-free XML, counts, comparison booleans/deltas, and SHA-256 values.')
[void]$builder.AppendLine('- Processor/GPU names, raw JSON, local paths, stdout, stderr, host/user names, serials, device IDs, and network addresses were not copied into this Markdown.')

[void]$builder.AppendLine()
[void]$builder.AppendLine('## Exact non-claims')
[void]$builder.AppendLine()
[void]$builder.AppendLine('- No production binary has been approved or committed.')
[void]$builder.AppendLine('- No WinUI or `HardwareSnapshot` implementation exists.')
[void]$builder.AppendLine('- No Windows Intel dedicated/shared GPU memory conclusion was established.')
[void]$builder.AppendLine('- No Intel NPU presence or absence was established.')
[void]$builder.AppendLine('- No model compatibility conclusion was made.')
if ($offlinePrerequisiteBlocked -or $offlineNotEvaluatedBlocked -or $null -eq $offlineSummary) {
    [void]$builder.AppendLine('- No offline candidate run occurred; no candidate network behavior was evaluated.')
}
elseif ($offlineFailureProof) {
    [void]$builder.AppendLine('- No candidate network-behavior conclusion was established from the failed offline evaluation.')
}
elseif (-not $hasFullOffline) {
    [void]$builder.AppendLine('- No candidate network-behavior conclusion was established from incomplete offline evidence.')
}
else {
    [void]$builder.AppendLine('- No network-syscall proof is claimed beyond the controlled offline run and listener/process observations.')
}
[void]$builder.AppendLine('- This Gate 1 record does not verify `F-M07`, `HE-01`, or `HE-02`; complete Block 2 evidence remains for the controlled Gate 9 traceability workflow.')

$content = $builder.ToString()
Write-AtomicMarkdown -Path $OutputMarkdown -Content $content -Artifacts $allArtifacts
Write-Output "Hardware Inspection LLM Fit Gate 1 report written with disposition $finalDisposition."
