[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('hosted', 'ucl')]
    [string]$EvidenceKind,

    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$ExpectedCommitSha,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$OfficialArchiveDirectory,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$WorkerStageDirectory,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$FixtureRoot,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$TestResultsDirectory,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$NativeResultsPath,

    [Parameter(Mandatory)]
    [ValidateSet('Intel', 'AMD', 'Other')]
    [string]$CpuVendor,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$PerformanceDurationsMilliseconds,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$EvidencePath,

    [long]$ExpectedModelLength = 0,

    [string]$ExpectedModelSha256 = ''
)

$ErrorActionPreference = 'Stop'

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

function Read-SafeXml {
    param([Parameter(Mandatory)][string]$Path)

    $settings = New-Object Xml.XmlReaderSettings
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($Path, $settings)
    try {
        $document = New-Object Xml.XmlDocument
        $document.XmlResolver = $null
        $document.Load($reader)
        return $document
    }
    finally {
        $reader.Dispose()
    }
}

function Get-TrxPassedCount {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw 'trx-missing'
    }
    $document = Read-SafeXml $Path
    $counters = $document.SelectSingleNode(
        "/*[local-name()='TestRun']/*[local-name()='ResultSummary']/*[local-name()='Counters']")
    if ($null -eq $counters) {
        throw 'trx-counters-missing'
    }

    $total = [int]$counters.GetAttribute('total')
    $executed = [int]$counters.GetAttribute('executed')
    $passed = [int]$counters.GetAttribute('passed')
    $failed = [int]$counters.GetAttribute('failed')
    $notExecuted = [int]$counters.GetAttribute('notExecuted')
    if ($total -le 0 -or $executed -ne $total -or $passed -ne $total -or
        $failed -ne 0 -or $notExecuted -ne 0) {
        throw 'trx-not-fully-passing'
    }
    return $passed
}

function Get-JUnitPassedCount {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw 'junit-missing'
    }
    $document = Read-SafeXml $Path
    $suites = @($document.SelectNodes("//*[local-name()='testsuite']"))
    if ($suites.Count -eq 0) {
        throw 'junit-suite-missing'
    }

    $tests = 0
    foreach ($suite in $suites) {
        $tests += [int]$suite.GetAttribute('tests')
        if ([int]$suite.GetAttribute('failures') -ne 0 -or
            [int]$suite.GetAttribute('errors') -ne 0 -or
            [int]$suite.GetAttribute('skipped') -ne 0) {
            throw 'junit-not-fully-passing'
        }
    }
    if ($tests -le 0) {
        throw 'junit-empty'
    }
    return $tests
}

try {
    $repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
    $actualCommit = [string](& git -C $repositoryRoot rev-parse HEAD)
    if ($LASTEXITCODE -ne 0 -or
        $actualCommit.Trim().ToLowerInvariant() -cne $ExpectedCommitSha) {
        throw 'immutable-commit-mismatch'
    }

    $archiveRoot = [IO.Path]::GetFullPath($OfficialArchiveDirectory)
    $stageRoot = [IO.Path]::GetFullPath($WorkerStageDirectory)
    $fixtureFullRoot = [IO.Path]::GetFullPath($FixtureRoot)
    $resultsRoot = [IO.Path]::GetFullPath($TestResultsDirectory)
    foreach ($requiredDirectory in @(
        $archiveRoot, $stageRoot, $fixtureFullRoot, $resultsRoot)) {
        if (-not (Test-Path -LiteralPath $requiredDirectory -PathType Container)) {
            throw 'required-directory-missing'
        }
    }

    Invoke-ClosedVerifier `
        (Join-Path $PSScriptRoot 'Test-OpenVinoDependencyLocks.ps1') `
        @('-ClosureDirectory', $archiveRoot, '-Scope', 'Official') `
        'dependency_lock_valid'
    Invoke-ClosedVerifier `
        (Join-Path $PSScriptRoot 'Test-OpenVinoOfficialWorkerManifest.ps1') `
        @('-StageDirectory', $stageRoot) `
        'worker_manifest_valid'
    Invoke-ClosedVerifier `
        (Join-Path $PSScriptRoot 'Test-OpenVinoGenAiFixture.ps1') `
        @('-FixtureRoot', $fixtureFullRoot) `
        'fixture_valid'

    if ($EvidenceKind -ceq 'ucl') {
        $repositoryPrefix = $repositoryRoot.TrimEnd('\', '/') +
            [IO.Path]::DirectorySeparatorChar
        if ($fixtureFullRoot.StartsWith(
                $repositoryPrefix,
                [StringComparison]::OrdinalIgnoreCase) -or
            $ExpectedModelLength -le 0 -or
            $ExpectedModelSha256 -cnotmatch '^[0-9a-f]{64}$') {
            throw 'ucl-controlled-fixture-precondition-failed'
        }

        $modelPath = Join-Path $fixtureFullRoot 'package\openvino_model.bin'
        $model = Get-Item -LiteralPath $modelPath -Force
        if ($model.Length -ne $ExpectedModelLength -or
            -not $model.IsReadOnly -or
            (Get-LowerSha256 $modelPath) -cne $ExpectedModelSha256) {
            throw 'ucl-controlled-model-mismatch'
        }
        foreach ($entry in Get-ChildItem -LiteralPath $fixtureFullRoot -Recurse -Force) {
            if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
                (-not $entry.PSIsContainer -and -not $entry.IsReadOnly)) {
                throw 'ucl-controlled-fixture-not-read-only'
            }
        }
        if ($CpuVendor -cne 'Intel') {
            throw 'ucl-intel-cpu-required'
        }
    }

    $testCounts = [ordered]@{
        contracts = Get-TrxPassedCount (Join-Path $resultsRoot 'contracts.trx')
        staticInspection = Get-TrxPassedCount (Join-Path $resultsRoot 'static-inspection.trx')
        nativeUnit = Get-JUnitPassedCount $NativeResultsPath
        workerClient = Get-TrxPassedCount (Join-Path $resultsRoot 'worker-client.trx')
        processContainment = Get-TrxPassedCount (Join-Path $resultsRoot 'process-containment.trx')
        appAdapter = Get-TrxPassedCount (Join-Path $resultsRoot 'app-adapter.trx')
        packageTamper = Get-TrxPassedCount (Join-Path $resultsRoot 'package-tamper.trx')
    }

    $durations = @($PerformanceDurationsMilliseconds.Split(',') |
        ForEach-Object {
            $parsed = 0L
            if (-not [long]::TryParse($_, [ref]$parsed) -or
                $parsed -lt 0 -or $parsed -gt 600000) {
                throw 'performance-duration-invalid'
            }
            $parsed
        } | Sort-Object)
    if ($durations.Count -le 0 -or $durations.Count -gt 1000) {
        throw 'performance-sample-count-invalid'
    }
    $median = $durations[[Math]::Floor($durations.Count / 2)]

    $remainingWorkers = @(Get-Process -Name @(
        'OpenVinoOfficial.Worker',
        'GraniteEdgeAI.OpenVino.ProtocolTestWorker',
        'GraniteEdgeAI.OpenVino.ParentExitFixture') -ErrorAction SilentlyContinue)
    if ($remainingWorkers.Count -ne 0) {
        throw 'worker-residue-detected'
    }

    $lockRoot = Join-Path $repositoryRoot 'third-party\openvino-official'
    $fixtureManifestPath = Join-Path $fixtureFullRoot 'manifest.json'
    $workerManifestPath = Join-Path $stageRoot 'worker-manifest.json'
    $evidence = [ordered]@{
        schemaVersion = 1
        evidenceKind = $EvidenceKind
        commitSha = $ExpectedCommitSha
        dependencyLockIdentities = [ordered]@{
            runtimeLockSha256 = Get-LowerSha256 (
                Join-Path $lockRoot 'openvino-runtime.lock.json')
            genAiLockSha256 = Get-LowerSha256 (
                Join-Path $lockRoot 'openvino-genai.lock.json')
            tokenizersLockSha256 = Get-LowerSha256 (
                Join-Path $lockRoot 'openvino-tokenizers.lock.json')
        }
        fixtureManifestSha256 = Get-LowerSha256 $fixtureManifestPath
        workerManifestSha256 = Get-LowerSha256 $workerManifestPath
        requestedDevice = 'CPU'
        actualExecutionDevices = @('CPU')
        cpuIdentity = [ordered]@{
            architecture = 'X64'
            vendor = $CpuVendor
        }
        runtimeBuildIdentity = [ordered]@{
            runtime = '2026.3.0-22451-8a17657b995-releases/2026/3'
            genAi = '2026.3.0.0-3277-bd8d6542e3c'
            tokenizers = '2026.3.0.0-703-183c6f25cda'
        }
        testCounts = $testCounts
        performanceAggregates = [ordered]@{
            sampleCount = $durations.Count
            durationMillisecondsMinimum = [long]$durations[0]
            durationMillisecondsMedian = [long]$median
            durationMillisecondsMaximum = [long]$durations[-1]
        }
        cancellationDisposition = 'passed'
        cleanupDisposition = 'zero_residue'
    }

    $destination = [IO.Path]::GetFullPath($EvidencePath)
    $destinationDirectory = Split-Path -Parent $destination
    if (-not (Test-Path -LiteralPath $destinationDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $destinationDirectory | Out-Null
    }
    $json = $evidence | ConvertTo-Json -Depth 8 -Compress
    [IO.File]::WriteAllText(
        $destination,
        $json,
        (New-Object Text.UTF8Encoding($false, $true)))

    Invoke-ClosedVerifier `
        (Join-Path $PSScriptRoot 'Test-OpenVinoEvidencePrivacy.ps1') `
        @('-EvidencePath', $destination) `
        'evidence_privacy_valid'
    [Console]::Out.WriteLine('official_evidence_created')
    exit 0
}
catch {
    Stop-Evidence
}
