[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$EvidencePath
)

$ErrorActionPreference = 'Stop'

function Stop-Invalid {
    [Console]::Out.WriteLine('evidence_privacy_invalid')
    exit 1
}

function Test-ExactProperties {
    param(
        [Parameter(Mandatory)]$Value,
        [Parameter(Mandatory)][string[]]$Names
    )

    if ($null -eq $Value -or $Value -isnot [pscustomobject]) {
        return $false
    }

    $actual = @($Value.PSObject.Properties.Name)
    if ($actual.Count -ne $Names.Count) {
        return $false
    }

    for ($index = 0; $index -lt $Names.Count; $index++) {
        if ($actual[$index] -cne $Names[$index]) {
            return $false
        }
    }

    return $true
}

function Test-LowerSha256 {
    param($Value)
    return $Value -is [string] -and $Value -cmatch '^[0-9a-f]{64}$'
}

function Test-BoundedInteger {
    param(
        $Value,
        [long]$Minimum,
        [long]$Maximum
    )

    return ($Value -is [int] -or $Value -is [long]) -and
        [long]$Value -ge $Minimum -and
        [long]$Value -le $Maximum
}

try {
    $fullPath = [IO.Path]::GetFullPath($EvidencePath)
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        Stop-Invalid
    }

    $file = Get-Item -LiteralPath $fullPath -Force
    if ($file.Length -le 0 -or $file.Length -gt 16384 -or
        ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        Stop-Invalid
    }

    $bytes = [IO.File]::ReadAllBytes($fullPath)
    $strictUtf8 = New-Object Text.UTF8Encoding($false, $true)
    $raw = $strictUtf8.GetString($bytes)
    if ($raw.IndexOf([char]0) -ge 0 -or
        $raw -match '(?i)(?:[a-z]:[\\/]|\\\\[^\\\s]+\\[^\\\s]+)' -or
        $raw -match '(?i)"(?:[^"\\]|\\.)*"\s*:\s*"/' -or
        $raw -match '(?i)"(?:prompt|generatedText|environment|secret|password|username|hostname|runnerName|computerName|account|stdout|stderr|modelBytes|modelPath|packagePath)"\s*:') {
        Stop-Invalid
    }

    $evidence = $raw | ConvertFrom-Json -ErrorAction Stop
    $rootNames = @(
        'schemaVersion',
        'evidenceKind',
        'commitSha',
        'dependencyLockIdentities',
        'fixtureManifestSha256',
        'workerManifestSha256',
        'requestedDevice',
        'actualExecutionDevices',
        'cpuIdentity',
        'runtimeBuildIdentity',
        'testCounts',
        'performanceAggregates',
        'cancellationDisposition',
        'cleanupDisposition'
    )
    $dependencyNames = @(
        'runtimeLockSha256',
        'genAiLockSha256',
        'tokenizersLockSha256'
    )
    $cpuNames = @('architecture', 'vendor')
    $runtimeNames = @('runtime', 'genAi', 'tokenizers')
    $testNames = @(
        'contracts',
        'staticInspection',
        'nativeUnit',
        'workerClient',
        'processContainment',
        'appAdapter',
        'packageTamper'
    )
    $performanceNames = @(
        'sampleCount',
        'durationMillisecondsMinimum',
        'durationMillisecondsMedian',
        'durationMillisecondsMaximum'
    )

    if (-not (Test-ExactProperties $evidence $rootNames) -or
        -not (Test-ExactProperties $evidence.dependencyLockIdentities $dependencyNames) -or
        -not (Test-ExactProperties $evidence.cpuIdentity $cpuNames) -or
        -not (Test-ExactProperties $evidence.runtimeBuildIdentity $runtimeNames) -or
        -not (Test-ExactProperties $evidence.testCounts $testNames) -or
        -not (Test-ExactProperties $evidence.performanceAggregates $performanceNames)) {
        Stop-Invalid
    }

    foreach ($propertyName in @(
        $rootNames + $dependencyNames + $cpuNames + $runtimeNames +
        $testNames + $performanceNames)) {
        $escaped = [Regex]::Escape($propertyName)
        if ([Regex]::Matches($raw, '"' + $escaped + '"\s*:').Count -ne 1) {
            Stop-Invalid
        }
    }

    if (-not (Test-BoundedInteger $evidence.schemaVersion 1 1) -or
        $evidence.evidenceKind -isnot [string] -or
        $evidence.evidenceKind -notin @('hosted', 'ucl') -or
        $evidence.commitSha -isnot [string] -or
        $evidence.commitSha -cnotmatch '^[0-9a-f]{40}$' -or
        -not (Test-LowerSha256 $evidence.dependencyLockIdentities.runtimeLockSha256) -or
        -not (Test-LowerSha256 $evidence.dependencyLockIdentities.genAiLockSha256) -or
        -not (Test-LowerSha256 $evidence.dependencyLockIdentities.tokenizersLockSha256) -or
        -not (Test-LowerSha256 $evidence.fixtureManifestSha256) -or
        -not (Test-LowerSha256 $evidence.workerManifestSha256) -or
        $evidence.requestedDevice -isnot [string] -or
        $evidence.requestedDevice -cne 'CPU') {
        Stop-Invalid
    }

    if ($evidence.actualExecutionDevices -isnot [array]) {
        Stop-Invalid
    }
    $actualDevices = @($evidence.actualExecutionDevices)
    if ($actualDevices.Count -ne 1 -or
        $actualDevices[0] -isnot [string] -or
        $actualDevices[0] -cne 'CPU' -or
        $evidence.cpuIdentity.architecture -isnot [string] -or
        $evidence.cpuIdentity.architecture -cne 'X64' -or
        $evidence.cpuIdentity.vendor -isnot [string] -or
        $evidence.cpuIdentity.vendor -notin @('Intel', 'AMD', 'Other') -or
        ($evidence.evidenceKind -ceq 'ucl' -and
            $evidence.cpuIdentity.vendor -cne 'Intel')) {
        Stop-Invalid
    }

    if ($evidence.runtimeBuildIdentity.runtime -isnot [string] -or
        $evidence.runtimeBuildIdentity.runtime -cne
            '2026.3.0-22451-8a17657b995-releases/2026/3' -or
        $evidence.runtimeBuildIdentity.genAi -isnot [string] -or
        $evidence.runtimeBuildIdentity.genAi -cne
            '2026.3.0.0-3277-bd8d6542e3c' -or
        $evidence.runtimeBuildIdentity.tokenizers -isnot [string] -or
        $evidence.runtimeBuildIdentity.tokenizers -cne
            '2026.3.0.0-703-183c6f25cda') {
        Stop-Invalid
    }

    foreach ($name in $testNames) {
        $minimum = if ($name -ceq 'nativeUnit') { 7 } else { 1 }
        if (-not (Test-BoundedInteger $evidence.testCounts.$name $minimum 100000)) {
            Stop-Invalid
        }
    }

    $performance = $evidence.performanceAggregates
    if (-not (Test-BoundedInteger $performance.sampleCount 1 1000) -or
        -not (Test-BoundedInteger $performance.durationMillisecondsMinimum 0 600000) -or
        -not (Test-BoundedInteger $performance.durationMillisecondsMedian 0 600000) -or
        -not (Test-BoundedInteger $performance.durationMillisecondsMaximum 0 600000) -or
        [long]$performance.durationMillisecondsMinimum -gt
            [long]$performance.durationMillisecondsMedian -or
        [long]$performance.durationMillisecondsMedian -gt
            [long]$performance.durationMillisecondsMaximum -or
        $evidence.cancellationDisposition -isnot [string] -or
        $evidence.cancellationDisposition -cne 'passed' -or
        $evidence.cleanupDisposition -isnot [string] -or
        $evidence.cleanupDisposition -cne 'zero_residue') {
        Stop-Invalid
    }

    [Console]::Out.WriteLine('evidence_privacy_valid')
    exit 0
}
catch {
    Stop-Invalid
}
