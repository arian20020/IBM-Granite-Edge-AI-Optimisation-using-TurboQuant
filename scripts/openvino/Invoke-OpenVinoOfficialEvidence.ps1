[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedCommitSha,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$OfficialArchiveDirectory,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$WorkerStageDirectory,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$FixtureRoot,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$MeasurementsPath,
    [Parameter(Mandatory)][ValidateSet('Intel', 'AMD', 'Other')][string]$CpuVendor,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$EvidencePath
)

$ErrorActionPreference = 'Stop'
$destination = $null
$destinationDirectory = $null
$evidenceDirectoryCreated = $false

function Stop-Evidence {
    [Console]::Out.WriteLine('official_evidence_failed')
    exit 1
}

function Get-LowerSha256 {
    param([Parameter(Mandatory)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Invoke-ClosedVerifier {
    param(
        [Parameter(Mandatory)][string]$Script,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$ExpectedOutput
    )
    $powerShell = (Get-Command powershell.exe -ErrorAction Stop).Source
    $output = & $powerShell -NoLogo -NoProfile -NonInteractive `
        -ExecutionPolicy Bypass -File $Script @Arguments
    if ($LASTEXITCODE -ne 0 -or [string]$output -cne $ExpectedOutput) {
        throw 'closed-verifier-failed'
    }
}

function Test-ExactProperties {
    param([Parameter(Mandatory)]$Value, [Parameter(Mandatory)][string[]]$Names)
    if ($null -eq $Value -or $Value -isnot [pscustomobject]) { return $false }
    $actual = @($Value.PSObject.Properties.Name)
    if ($actual.Count -ne $Names.Count) { return $false }
    for ($index = 0; $index -lt $Names.Count; $index++) {
        if ($actual[$index] -cne $Names[$index]) { return $false }
    }
    return $true
}

function Test-BoundedInteger {
    param($Value, [long]$Minimum, [long]$Maximum)
    return ($Value -is [int] -or $Value -is [long]) -and
        [long]$Value -ge $Minimum -and [long]$Value -le $Maximum
}

try {
    $repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
    $actualCommit = [string](& git -C $repositoryRoot rev-parse HEAD)
    if ($LASTEXITCODE -ne 0 -or
        $actualCommit.Trim().ToLowerInvariant() -cne $ExpectedCommitSha) {
        throw 'immutable-commit-mismatch'
    }

    $destination = [IO.Path]::GetFullPath($EvidencePath)
    $destinationDirectory = Split-Path -Parent $destination
    if ((Split-Path -Leaf $destination) -cne 'cpu.json' -or
        (Test-Path -LiteralPath $destinationDirectory) -or
        (Test-Path -LiteralPath $destination)) {
        throw 'evidence-directory-not-initially-absent'
    }

    $archiveRoot = [IO.Path]::GetFullPath($OfficialArchiveDirectory)
    $stageRoot = [IO.Path]::GetFullPath($WorkerStageDirectory)
    $fixtureFullRoot = [IO.Path]::GetFullPath($FixtureRoot)
    $measurementFullPath = [IO.Path]::GetFullPath($MeasurementsPath)
    foreach ($requiredDirectory in @($archiveRoot, $stageRoot, $fixtureFullRoot)) {
        if (-not (Test-Path -LiteralPath $requiredDirectory -PathType Container)) {
            throw 'required-directory-missing'
        }
    }
    if (-not (Test-Path -LiteralPath $measurementFullPath -PathType Leaf)) {
        throw 'measurement-file-missing'
    }

    Invoke-ClosedVerifier (Join-Path $PSScriptRoot 'Test-OpenVinoDependencyLocks.ps1') `
        @('-ClosureDirectory', $archiveRoot, '-Scope', 'Official') 'dependency_lock_valid'
    Invoke-ClosedVerifier (Join-Path $PSScriptRoot 'Test-OpenVinoOfficialWorkerManifest.ps1') `
        @('-StageDirectory', $stageRoot) 'worker_manifest_valid'
    Invoke-ClosedVerifier (Join-Path $PSScriptRoot 'Test-OpenVinoGenAiFixture.ps1') `
        @('-FixtureRoot', $fixtureFullRoot) 'fixture_valid'

    $measurementFile = Get-Item -LiteralPath $measurementFullPath -Force
    if ($measurementFile.Length -le 0 -or $measurementFile.Length -gt 8192 -or
        ($measurementFile.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'measurement-file-invalid'
    }
    Import-Module (Join-Path $PSScriptRoot 'OpenVinoClosedJson.psm1') -Force
    $measurementRaw = Get-OpenVinoClosedJsonText -Path $measurementFullPath `
        -MaximumBytes 8192 -MaximumDepth 8
    $measurement = $measurementRaw | ConvertFrom-Json -ErrorAction Stop
    $rootNames = @('schemaVersion', 'requestedDevice', 'actualExecutionDevices',
        'runtimeBuildIdentity', 'testCounts', 'performanceAggregates',
        'cancellationDisposition', 'cleanupDisposition')
    $runtimeNames = @('runtime', 'genAi', 'tokenizers')
    $testNames = @('contracts', 'staticInspection', 'nativeUnit', 'workerClient',
        'processContainment', 'appAdapter', 'packageTamper')
    $performanceNames = @('sampleCount', 'durationMillisecondsMinimum',
        'durationMillisecondsMedian', 'durationMillisecondsMaximum')
    if (-not (Test-ExactProperties $measurement $rootNames) -or
        -not (Test-ExactProperties $measurement.runtimeBuildIdentity $runtimeNames) -or
        -not (Test-ExactProperties $measurement.testCounts $testNames) -or
        -not (Test-ExactProperties $measurement.performanceAggregates $performanceNames)) {
        throw 'measurement-schema-invalid'
    }
    foreach ($propertyName in @($rootNames + $runtimeNames + $testNames + $performanceNames)) {
        if ([Regex]::Matches($measurementRaw,
                '"' + [Regex]::Escape($propertyName) + '"\s*:').Count -ne 1) {
            throw 'measurement-property-invalid'
        }
    }
    if (-not (Test-BoundedInteger $measurement.schemaVersion 1 1) -or
        $measurement.requestedDevice -isnot [string] -or
        $measurement.requestedDevice -cne 'CPU' -or
        $measurement.actualExecutionDevices -isnot [array] -or
        @($measurement.actualExecutionDevices).Count -ne 1 -or
        @($measurement.actualExecutionDevices)[0] -isnot [string] -or
        @($measurement.actualExecutionDevices)[0] -cne 'CPU' -or
        $measurement.runtimeBuildIdentity.runtime -isnot [string] -or
        $measurement.runtimeBuildIdentity.runtime -cne '2026.3.0-22451-8a17657b995-releases/2026/3' -or
        $measurement.runtimeBuildIdentity.genAi -isnot [string] -or
        $measurement.runtimeBuildIdentity.genAi -cne '2026.3.0.0-3277-bd8d6542e3c' -or
        $measurement.runtimeBuildIdentity.tokenizers -isnot [string] -or
        $measurement.runtimeBuildIdentity.tokenizers -cne '2026.3.0.0-703-183c6f25cda') {
        throw 'measurement-value-invalid'
    }
    foreach ($name in $testNames) {
        $minimum = switch ($name) {
            'contracts' { 60 }
            'staticInspection' { 177 }
            'nativeUnit' { 7 }
            'workerClient' { 13 }
            'processContainment' { 41 }
            'appAdapter' { 35 }
            'packageTamper' { 5 }
        }
        if (-not (Test-BoundedInteger $measurement.testCounts.$name $minimum 100000)) {
            throw 'measurement-test-count-invalid'
        }
    }
    $performance = $measurement.performanceAggregates
    if (-not (Test-BoundedInteger $performance.sampleCount 1 1000) -or
        -not (Test-BoundedInteger $performance.durationMillisecondsMinimum 0 600000) -or
        -not (Test-BoundedInteger $performance.durationMillisecondsMedian 0 600000) -or
        -not (Test-BoundedInteger $performance.durationMillisecondsMaximum 0 600000) -or
        [long]$performance.durationMillisecondsMinimum -gt [long]$performance.durationMillisecondsMedian -or
        [long]$performance.durationMillisecondsMedian -gt [long]$performance.durationMillisecondsMaximum -or
        $measurement.cancellationDisposition -isnot [string] -or
        $measurement.cancellationDisposition -cne 'passed' -or
        $measurement.cleanupDisposition -isnot [string] -or
        $measurement.cleanupDisposition -cne 'zero_residue') {
        throw 'measurement-disposition-invalid'
    }

    $remainingWorkers = @(Get-Process -Name @('OpenVinoOfficial.Worker',
        'GraniteEdgeAI.OpenVino.ProtocolTestWorker',
        'GraniteEdgeAI.OpenVino.ParentExitFixture') -ErrorAction SilentlyContinue)
    if ($remainingWorkers.Count -ne 0) { throw 'worker-residue-detected' }

    $lockRoot = Join-Path $repositoryRoot 'third-party\openvino-official'
    $evidence = [ordered]@{
        schemaVersion = 1
        commitSha = $ExpectedCommitSha
        dependencyLockIdentities = [ordered]@{
            runtimeLockSha256 = Get-LowerSha256 (Join-Path $lockRoot 'openvino-runtime.lock.json')
            genAiLockSha256 = Get-LowerSha256 (Join-Path $lockRoot 'openvino-genai.lock.json')
            tokenizersLockSha256 = Get-LowerSha256 (Join-Path $lockRoot 'openvino-tokenizers.lock.json')
        }
        fixtureManifestSha256 = Get-LowerSha256 (Join-Path $fixtureFullRoot 'manifest.json')
        workerManifestSha256 = Get-LowerSha256 (Join-Path $stageRoot 'worker-manifest.json')
        requestedDevice = $measurement.requestedDevice
        actualExecutionDevices = @($measurement.actualExecutionDevices)
        cpuIdentity = [ordered]@{ architecture = 'X64'; vendor = $CpuVendor }
        runtimeBuildIdentity = [ordered]@{
            runtime = $measurement.runtimeBuildIdentity.runtime
            genAi = $measurement.runtimeBuildIdentity.genAi
            tokenizers = $measurement.runtimeBuildIdentity.tokenizers
        }
        testCounts = $measurement.testCounts
        performanceAggregates = $measurement.performanceAggregates
        cancellationDisposition = $measurement.cancellationDisposition
        cleanupDisposition = $measurement.cleanupDisposition
    }

    New-Item -ItemType Directory -Path $destinationDirectory | Out-Null
    $evidenceDirectoryCreated = $true
    $json = $evidence | ConvertTo-Json -Depth 8 -Compress
    [IO.File]::WriteAllText($destination, $json,
        (New-Object Text.UTF8Encoding($false, $true)))
    Invoke-ClosedVerifier (Join-Path $PSScriptRoot 'Test-OpenVinoEvidenceArtifactSet.ps1') `
        @('-EvidencePath', $destination) 'evidence_artifact_set_valid'
    [Console]::Out.WriteLine('official_evidence_created')
    exit 0
}
catch {
    if ($evidenceDirectoryCreated) {
        if ($destination -and (Test-Path -LiteralPath $destination -PathType Leaf)) {
            Remove-Item -LiteralPath $destination -Force -ErrorAction SilentlyContinue
        }
        if ($destinationDirectory -and
            (Test-Path -LiteralPath $destinationDirectory -PathType Container) -and
            @(Get-ChildItem -LiteralPath $destinationDirectory -Force).Count -eq 0) {
            Remove-Item -LiteralPath $destinationDirectory -Force -ErrorAction SilentlyContinue
        }
    }
    Stop-Evidence
}
