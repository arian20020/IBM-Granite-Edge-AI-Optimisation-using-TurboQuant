[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$TestResultsDirectory,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$NativeResultsPath,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$PerformanceDurationsMilliseconds,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$MeasurementsPath
)

$ErrorActionPreference = 'Stop'

function Stop-Invalid {
    [Console]::Out.WriteLine('openvino_measurements_invalid')
    exit 1
}

function Read-SafeXml {
    param([Parameter(Mandatory)][string]$Path)

    $file = Get-Item -LiteralPath $Path -Force
    if ($file.Length -le 0 -or $file.Length -gt 16777216 -or
        ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'measurement-xml-invalid'
    }
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

function Get-TrxMeasurement {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw 'measurement-trx-missing'
    }
    $document = Read-SafeXml $Path
    $counters = $document.SelectSingleNode(
        "/*[local-name()='TestRun']/*[local-name()='ResultSummary']/*[local-name()='Counters']")
    if ($null -eq $counters) {
        throw 'measurement-counters-missing'
    }
    foreach ($name in @('total', 'executed', 'passed', 'failed', 'notExecuted')) {
        if ($counters.GetAttribute($name) -cnotmatch '^(0|[1-9][0-9]{0,5})$') {
            throw 'measurement-count-invalid'
        }
    }
    $total = [int]$counters.GetAttribute('total')
    if ($total -le 0 -or
        [int]$counters.GetAttribute('executed') -ne $total -or
        [int]$counters.GetAttribute('passed') -ne $total -or
        [int]$counters.GetAttribute('failed') -ne 0 -or
        [int]$counters.GetAttribute('notExecuted') -ne 0) {
        throw 'measurement-trx-not-passing'
    }
    $stdout = (@($document.SelectNodes(
        "//*[local-name()='UnitTestResult' and @outcome='Passed']")) |
        ForEach-Object {
            [string]$_.SelectSingleNode(
                "./*[local-name()='Output']/*[local-name()='StdOut']").InnerText
        }) -join "`n"
    return [pscustomobject]@{ Count = $total; StandardOutput = $stdout }
}

function Get-ExactMarker {
    param(
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Pattern
    )

    $escaped = [Regex]::Escape($Name)
    $matches = @([Regex]::Matches(
        $Text,
        '(?m)^' + $escaped + '=(' + $Pattern + ')\r?$'))
    if ($matches.Count -ne 1) {
        throw 'measurement-marker-invalid'
    }
    return $matches[0].Groups[1].Value
}

try {
    $resultsRoot = [IO.Path]::GetFullPath($TestResultsDirectory).TrimEnd('\', '/')
    if (-not (Test-Path -LiteralPath $resultsRoot -PathType Container)) {
        Stop-Invalid
    }
    $destination = [IO.Path]::GetFullPath($MeasurementsPath)
    $resultsPrefix = $resultsRoot + [IO.Path]::DirectorySeparatorChar
    if (-not $destination.StartsWith(
            $resultsPrefix,
            [StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path -Leaf $destination) -cne 'measurements.json' -or
        (Test-Path -LiteralPath $destination)) {
        Stop-Invalid
    }

    $nativeFullPath = [IO.Path]::GetFullPath($NativeResultsPath)
    if ($nativeFullPath -ine (Join-Path $resultsRoot 'native.xml')) {
        Stop-Invalid
    }
    $requiredNames = @(
        'contracts.trx',
        'static-inspection.trx',
        'worker-client.trx',
        'process-containment.trx',
        'app-adapter.trx',
        'package-tamper.trx',
        'native.xml')
    $campaignNames = @(
        'process-containment-1.trx',
        'process-containment-2.trx',
        'process-containment-3.trx')
    $entries = @(Get-ChildItem -LiteralPath $resultsRoot -Force)
    if (@($entries | Where-Object {
            $_.PSIsContainer -or
            ($_.Attributes -band [IO.FileAttributes]::ReparsePoint)
        }).Count -ne 0) {
        Stop-Invalid
    }
    $names = @($entries | ForEach-Object Name)
    $campaignCount = @($names | Where-Object { $campaignNames -ccontains $_ }).Count
    if ($campaignCount -notin @(0, 3)) {
        Stop-Invalid
    }
    $expectedNames = if ($campaignCount -eq 3) {
        @($requiredNames + $campaignNames)
    } else {
        @($requiredNames)
    }
    if ($names.Count -ne $expectedNames.Count -or
        @($names | Where-Object { $expectedNames -cnotcontains $_ }).Count -ne 0) {
        Stop-Invalid
    }
    if ($campaignCount -eq 3 -and
        (Get-FileHash -LiteralPath (Join-Path $resultsRoot 'process-containment.trx') `
            -Algorithm SHA256).Hash -cne
        (Get-FileHash -LiteralPath (Join-Path $resultsRoot 'process-containment-3.trx') `
            -Algorithm SHA256).Hash) {
        Stop-Invalid
    }

    $contracts = Get-TrxMeasurement (Join-Path $resultsRoot 'contracts.trx')
    $static = Get-TrxMeasurement (Join-Path $resultsRoot 'static-inspection.trx')
    $client = Get-TrxMeasurement (Join-Path $resultsRoot 'worker-client.trx')
    $process = Get-TrxMeasurement (Join-Path $resultsRoot 'process-containment.trx')
    $adapter = Get-TrxMeasurement (Join-Path $resultsRoot 'app-adapter.trx')
    $tamper = Get-TrxMeasurement (Join-Path $resultsRoot 'package-tamper.trx')
    if ($contracts.Count -lt 60 -or $static.Count -lt 177 -or
        $client.Count -lt 13 -or $process.Count -lt 41 -or
        $adapter.Count -lt 35 -or $tamper.Count -lt 5) {
        Stop-Invalid
    }

    $powerShell = (Get-Command powershell.exe -ErrorAction Stop).Source
    $nativeOutput = & $powerShell -NoLogo -NoProfile -NonInteractive `
        -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'Test-OpenVinoNativeJUnit.ps1') `
        -ResultsPath $nativeFullPath -MinimumCount 7
    if ($LASTEXITCODE -ne 0 -or
        [string]$nativeOutput -cnotmatch '^native_junit_valid:([1-9][0-9]{0,5})$') {
        Stop-Invalid
    }
    $nativeCount = [int]$Matches[1]

    $requestedDevice = Get-ExactMarker $process.StandardOutput `
        'OPENVINO_MEASURED_REQUESTED_DEVICE' 'CPU'
    $actualDevice = Get-ExactMarker $process.StandardOutput `
        'OPENVINO_MEASURED_ACTUAL_EXECUTION_DEVICES' 'CPU'
    $runtime = Get-ExactMarker $process.StandardOutput `
        'OPENVINO_MEASURED_RUNTIME_BUILD' '[A-Za-z0-9._/-]{1,128}'
    $genAi = Get-ExactMarker $process.StandardOutput `
        'OPENVINO_MEASURED_GENAI_BUILD' '[A-Za-z0-9._/-]{1,128}'
    $tokenizers = Get-ExactMarker $process.StandardOutput `
        'OPENVINO_MEASURED_TOKENIZERS_BUILD' '[A-Za-z0-9._/-]{1,128}'
    $cancellation = Get-ExactMarker $process.StandardOutput `
        'OPENVINO_MEASURED_CANCELLATION_DISPOSITION' 'passed'
    $cleanup = Get-ExactMarker $process.StandardOutput `
        'OPENVINO_MEASURED_CLEANUP_DISPOSITION' 'zero_residue'

    $durations = @($PerformanceDurationsMilliseconds.Split(',') |
        ForEach-Object {
            $parsed = 0L
            if (-not [long]::TryParse($_, [ref]$parsed) -or
                $parsed -lt 0 -or $parsed -gt 600000) {
                throw 'measurement-duration-invalid'
            }
            $parsed
        } | Sort-Object)
    if ($durations.Count -le 0 -or $durations.Count -gt 1000) {
        Stop-Invalid
    }

    $measurement = [ordered]@{
        schemaVersion = 1
        requestedDevice = $requestedDevice
        actualExecutionDevices = @($actualDevice)
        runtimeBuildIdentity = [ordered]@{
            runtime = $runtime
            genAi = $genAi
            tokenizers = $tokenizers
        }
        testCounts = [ordered]@{
            contracts = [int]$contracts.Count
            staticInspection = [int]$static.Count
            nativeUnit = $nativeCount
            workerClient = [int]$client.Count
            processContainment = [int]$process.Count
            appAdapter = [int]$adapter.Count
            packageTamper = [int]$tamper.Count
        }
        performanceAggregates = [ordered]@{
            sampleCount = [int]$durations.Count
            durationMillisecondsMinimum = [long]$durations[0]
            durationMillisecondsMedian = [long]$durations[[Math]::Floor($durations.Count / 2)]
            durationMillisecondsMaximum = [long]$durations[-1]
        }
        cancellationDisposition = $cancellation
        cleanupDisposition = $cleanup
    }
    $json = $measurement | ConvertTo-Json -Depth 6 -Compress
    if ([Text.Encoding]::UTF8.GetByteCount($json) -gt 8192) {
        Stop-Invalid
    }
    [IO.File]::WriteAllText(
        $destination,
        $json,
        (New-Object Text.UTF8Encoding($false, $true)))
    [Console]::Out.WriteLine('openvino_measurements_created')
    exit 0
}
catch {
    if ($destination -and (Test-Path -LiteralPath $destination -PathType Leaf)) {
        Remove-Item -LiteralPath $destination -Force -ErrorAction SilentlyContinue
    }
    Stop-Invalid
}
