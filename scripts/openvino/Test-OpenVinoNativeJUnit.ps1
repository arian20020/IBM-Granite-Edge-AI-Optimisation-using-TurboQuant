[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ResultsPath,

    [Parameter(Mandatory)]
    [ValidateRange(1, 100000)]
    [int]$MinimumCount
)

$ErrorActionPreference = 'Stop'

function Stop-Invalid {
    [Console]::Out.WriteLine('native_junit_invalid')
    exit 1
}

try {
    $fullPath = [IO.Path]::GetFullPath($ResultsPath)
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        Stop-Invalid
    }

    $file = Get-Item -LiteralPath $fullPath -Force
    if ($file.Length -le 0 -or $file.Length -gt 4194304 -or
        ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        Stop-Invalid
    }

    $settings = New-Object Xml.XmlReaderSettings
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($fullPath, $settings)
    try {
        $document = New-Object Xml.XmlDocument
        $document.XmlResolver = $null
        $document.Load($reader)
    }
    finally {
        $reader.Dispose()
    }

    $suites = @($document.SelectNodes(
        "/*[local-name()='testsuite'] | " +
        "/*[local-name()='testsuites']/*[local-name()='testsuite']"))
    if ($suites.Count -eq 0) {
        Stop-Invalid
    }

    [long]$count = 0
    foreach ($suite in $suites) {
        foreach ($name in @('tests', 'failures', 'skipped')) {
            if ($suite.GetAttribute($name) -cnotmatch '^(0|[1-9][0-9]{0,5})$') {
                Stop-Invalid
            }
        }
        foreach ($optionalName in @('errors', 'disabled')) {
            if ($suite.HasAttribute($optionalName) -and
                $suite.GetAttribute($optionalName) -cnotmatch '^(0|[1-9][0-9]{0,5})$') {
                Stop-Invalid
            }
        }
        $count += [long]$suite.GetAttribute('tests')
        if ($count -gt 100000) {
            Stop-Invalid
        }
        $testCases = @($suite.SelectNodes("./*[local-name()='testcase']"))
        $errors = if ($suite.HasAttribute('errors')) {
            [long]$suite.GetAttribute('errors')
        } else { 0L }
        $disabled = if ($suite.HasAttribute('disabled')) {
            [long]$suite.GetAttribute('disabled')
        } else { 0L }
        if ($testCases.Count -ne [long]$suite.GetAttribute('tests') -or
            [long]$suite.GetAttribute('failures') -ne 0 -or
            $errors -ne 0 -or
            $disabled -ne 0 -or
            [long]$suite.GetAttribute('skipped') -ne 0 -or
            @($suite.SelectNodes(
                "./*[local-name()='testcase']/*[local-name()='failure' or " +
                "local-name()='error' or local-name()='skipped']")).Count -ne 0) {
            Stop-Invalid
        }
    }

    if ($count -lt $MinimumCount -or $count -gt 100000) {
        Stop-Invalid
    }

    [Console]::Out.WriteLine("native_junit_valid:$count")
    exit 0
}
catch {
    Stop-Invalid
}
