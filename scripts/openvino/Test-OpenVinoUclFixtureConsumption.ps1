[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ResultsPath,

    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{64}$')]
    [string]$ExpectedFixtureManifestSha256
)

$ErrorActionPreference = 'Stop'

function Stop-Invalid {
    [Console]::Out.WriteLine('ucl_fixture_consumption_invalid')
    exit 1
}

try {
    $fullPath = [IO.Path]::GetFullPath($ResultsPath)
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        Stop-Invalid
    }
    $file = Get-Item -LiteralPath $fullPath -Force
    if ($file.Length -le 0 -or $file.Length -gt 16777216 -or
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

    $results = @($document.SelectNodes(
        "//*[local-name()='UnitTestResult' and " +
        "@testName='UclOverrideRejectsRepositoryFixtureAndRecordsExactExternalIdentity']"))
    if ($results.Count -ne 1 -or $results[0].GetAttribute('outcome') -cne 'Passed') {
        Stop-Invalid
    }
    $stdout = [string]$results[0].SelectSingleNode(
        "./*[local-name()='Output']/*[local-name()='StdOut']").InnerText
    $markers = @([Regex]::Matches(
        $stdout,
        '(?m)^OPENVINO_UCL_FIXTURE_CONSUMED_SHA256=([0-9a-f]{64})\r?$'))
    if ($markers.Count -ne 1 -or
        $markers[0].Groups[1].Value -cne $ExpectedFixtureManifestSha256) {
        Stop-Invalid
    }

    [Console]::Out.WriteLine('ucl_fixture_consumption_valid')
    exit 0
}
catch {
    Stop-Invalid
}
