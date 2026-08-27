[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$EvidencePath,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$StageDirectory,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ResultsPath,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedCommitSha,
    [Parameter(Mandatory)][ValidatePattern('^GPU(?:\.(?:0|[1-9][0-9]*))?$')][string]$ExpectedDevice,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ExpectedGpuName,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ExpectedDriverVersion
)

$ErrorActionPreference = 'Stop'

function Stop-Invalid {
    [Console]::Out.WriteLine('gpu_evidence_invalid')
    exit 1
}

function Test-ExactProperties {
    param($Value, [string[]]$Names)
    if ($null -eq $Value -or $Value -isnot [pscustomobject]) { return $false }
    [string[]]$actual = @($Value.PSObject.Properties.Name)
    if ($actual.Count -ne $Names.Count) { return $false }
    for ($index = 0; $index -lt $Names.Count; $index++) {
        if ($actual[$index] -cne $Names[$index]) { return $false }
    }
    return $true
}

function Get-ExactMarker {
    param([string]$Text, [string]$Name, [string]$Pattern)
    $matches = @([Regex]::Matches(
        $Text,
        '(?m)^' + [Regex]::Escape($Name) + '=(' + $Pattern + ')\r?$'))
    if ($matches.Count -ne 1) { throw 'gpu-result-marker-invalid' }
    return $matches[0].Groups[1].Value
}

try {
    $evidence = [IO.Path]::GetFullPath($EvidencePath)
    $stage = [IO.Path]::GetFullPath($StageDirectory)
    $results = [IO.Path]::GetFullPath($ResultsPath)
    if ((Split-Path -Leaf $evidence) -cne 'gpu.json' -or
        -not (Test-Path -LiteralPath $evidence -PathType Leaf) -or
        -not (Test-Path -LiteralPath $stage -PathType Container) -or
        -not (Test-Path -LiteralPath $results -PathType Leaf)) {
        Stop-Invalid
    }
    $file = Get-Item -LiteralPath $evidence -Force
    if ($file.Length -le 0 -or $file.Length -gt 16384 -or
        ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        Stop-Invalid
    }
    Import-Module (Join-Path $PSScriptRoot 'OpenVinoClosedJson.psm1') -Force
    $raw = Get-OpenVinoClosedJsonText -Path $evidence -MaximumBytes 16384 -MaximumDepth 8
    $value = $raw | ConvertFrom-Json -ErrorAction Stop
    $resultFile = Get-Item -LiteralPath $results -Force
    if ($resultFile.Length -le 0 -or $resultFile.Length -gt 16777216 -or
        ($resultFile.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        Stop-Invalid
    }
    $settings = New-Object Xml.XmlReaderSettings
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($results, $settings)
    try {
        $trx = New-Object Xml.XmlDocument
        $trx.XmlResolver = $null
        $trx.Load($reader)
    }
    finally { $reader.Dispose() }
    $counters = $trx.SelectSingleNode(
        "/*[local-name()='TestRun']/*[local-name()='ResultSummary']/*[local-name()='Counters']")
    if ($null -eq $counters -or
        $counters.GetAttribute('total') -cne '2' -or
        $counters.GetAttribute('executed') -cne '2' -or
        $counters.GetAttribute('passed') -cne '2' -or
        $counters.GetAttribute('failed') -cne '0' -or
        $counters.GetAttribute('notExecuted') -cne '0') {
        Stop-Invalid
    }
    $passedResults = @($trx.SelectNodes(
        "//*[local-name()='UnitTestResult' and @outcome='Passed']"))
    [string[]]$testNames = @($passedResults | ForEach-Object {
        [string]$_.GetAttribute('testName')
    })
    [Array]::Sort($testNames, [StringComparer]::Ordinal)
    [string[]]$expectedTestNames = @(
        'AuthorizedPhysicalIntelGpuCompilesAndGeneratesOnExactDevice',
        'NonexistentExplicitGpuFailsUnavailableWithoutCpuFallback')
    if ([string]::Join("`n", $testNames) -cne
        [string]::Join("`n", $expectedTestNames)) {
        Stop-Invalid
    }
    $stdout = ($passedResults |
        ForEach-Object {
            $node = $_.SelectSingleNode(
                "./*[local-name()='Output']/*[local-name()='StdOut']")
            if ($null -ne $node) { [string]$node.InnerText }
        }) -join "`n"
    $measuredRequested = Get-ExactMarker $stdout `
        'OPENVINO_MEASURED_REQUESTED_DEVICE' 'GPU(?:\.(?:0|[1-9][0-9]*))?'
    $measuredActual = Get-ExactMarker $stdout `
        'OPENVINO_MEASURED_ACTUAL_EXECUTION_DEVICES' 'GPU(?:\.(?:0|[1-9][0-9]*))?'
    $measuredBackend = Get-ExactMarker $stdout 'OPENVINO_MEASURED_ATTENTION_BACKEND' 'SDPA'
    $measuredOneTurn = Get-ExactMarker $stdout 'OPENVINO_MEASURED_GPU_ONE_TURN' 'passed'
    $measuredTwoTurn = Get-ExactMarker $stdout 'OPENVINO_MEASURED_GPU_TWO_TURN' 'passed'
    $measuredCancellation = Get-ExactMarker $stdout 'OPENVINO_MEASURED_GPU_CANCELLATION' 'passed'
    $measuredCleanup = Get-ExactMarker $stdout 'OPENVINO_MEASURED_GPU_CLEANUP' 'zero_residue'
    $rootNames = @('schemaVersion','commitSha','requestedDevice','actualExecutionDevices',
        'gpuIdentity','pluginIdentity','runtimeIdentity','configIdentity','forcedNegatives',
        'testDispositions')
    if (-not (Test-ExactProperties $value $rootNames) -or
        -not (Test-ExactProperties $value.gpuIdentity @('vendor','name','driverVersion')) -or
        -not (Test-ExactProperties $value.pluginIdentity @('fileName','sha256')) -or
        -not (Test-ExactProperties $value.runtimeIdentity @('runtime','genAi','tokenizers')) -or
        -not (Test-ExactProperties $value.configIdentity @('attentionBackend','sha256')) -or
        -not (Test-ExactProperties $value.forcedNegatives @('nonexistentDevice','cpuResolutionMismatch')) -or
        -not (Test-ExactProperties $value.testDispositions @('oneTurn','twoTurn','cancellation','cleanup'))) {
        Stop-Invalid
    }
    $shaPattern = '^[0-9a-f]{64}$'
    $plugin = Join-Path $stage 'openvino_intel_gpu_plugin.dll'
    $pluginSha = (Get-FileHash -LiteralPath $plugin -Algorithm SHA256).Hash.ToLowerInvariant()
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        $expectedConfigSha = -join @($algorithm.ComputeHash(
            [Text.Encoding]::UTF8.GetBytes('{"ATTENTION_BACKEND":"SDPA"}')) |
            ForEach-Object { $_.ToString('x2') })
    }
    finally { $algorithm.Dispose() }
    if ($value.schemaVersion -isnot [long] -and $value.schemaVersion -isnot [int]) { Stop-Invalid }
    if ([long]$value.schemaVersion -ne 1 -or
        $value.commitSha -cne $ExpectedCommitSha -or
        $value.requestedDevice -cne $ExpectedDevice -or
        $value.requestedDevice -cne $measuredRequested -or
        $value.actualExecutionDevices -isnot [array] -or
        @($value.actualExecutionDevices).Count -ne 1 -or
        @($value.actualExecutionDevices)[0] -cne $ExpectedDevice -or
        @($value.actualExecutionDevices)[0] -cne $measuredActual -or
        $value.gpuIdentity.vendor -cne 'Intel' -or
        $value.gpuIdentity.name -cne $ExpectedGpuName -or
        $value.gpuIdentity.driverVersion -cne $ExpectedDriverVersion -or
        $value.pluginIdentity.fileName -cne 'openvino_intel_gpu_plugin.dll' -or
        $value.pluginIdentity.sha256 -cnotmatch $shaPattern -or
        $value.pluginIdentity.sha256 -cne $pluginSha -or
        $value.runtimeIdentity.runtime -cne '2026.3.0-22451-8a17657b995-releases/2026/3' -or
        $value.runtimeIdentity.genAi -cne '2026.3.0.0-3277-bd8d6542e3c' -or
        $value.runtimeIdentity.tokenizers -cne '2026.3.0.0-703-183c6f25cda' -or
        $value.configIdentity.attentionBackend -cne 'SDPA' -or
        $value.configIdentity.attentionBackend -cne $measuredBackend -or
        $value.configIdentity.sha256 -cne $expectedConfigSha -or
        $value.forcedNegatives.nonexistentDevice -cne 'runtime_device_unavailable' -or
        $value.forcedNegatives.cpuResolutionMismatch -cne 'runtime_device_mismatch' -or
        $value.testDispositions.oneTurn -cne 'passed' -or
        $value.testDispositions.oneTurn -cne $measuredOneTurn -or
        $value.testDispositions.twoTurn -cne 'passed' -or
        $value.testDispositions.twoTurn -cne $measuredTwoTurn -or
        $value.testDispositions.cancellation -cne 'passed' -or
        $value.testDispositions.cancellation -cne $measuredCancellation -or
        $value.testDispositions.cleanup -cne 'zero_residue' -or
        $value.testDispositions.cleanup -cne $measuredCleanup) {
        Stop-Invalid
    }
    foreach ($forbidden in @('prompt','answer','credential','environment','account','hostname',
        'machineName','userName','rawOutput','stderr','stackTrace','pnpDeviceId')) {
        if ($raw -match ('(?i)"' + [Regex]::Escape($forbidden) + '"\s*:')) {
            Stop-Invalid
        }
    }
    [Console]::Out.WriteLine('gpu_evidence_valid')
    exit 0
}
catch {
    Stop-Invalid
}
