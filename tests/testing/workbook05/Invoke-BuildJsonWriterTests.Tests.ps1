[CmdletBinding()]
param()

<#
.SYNOPSIS
Exercises the shared Workbook 05 JSON writer with collection and object roots.

.DESCRIPTION
The GenAI build evidence discovered that Windows PowerShell can enumerate a
one-element collection before ConvertTo-Json when the value is passed through the
pipeline. These executable tests freeze the required JSON root shapes without
running any external build or model.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolve the repository and import the real shared build module rather than a
# copied helper, so this test protects the exact production serialization path.
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$modulePath = Join-Path `
    $repositoryRoot `
    'scripts/testing/workbook05/Workbook05.Build.psm1'
Import-Module $modulePath -Force -ErrorAction Stop

# Use one isolated temporary directory and remove it in the final boundary so a
# failed assertion cannot leave evidence-looking files in the repository.
$testDirectory = Join-Path `
    ([IO.Path]::GetTempPath()) `
    ("workbook05-json-writer-test-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory -Force:$false | Out-Null

try {
    # A one-element dependency collection must stay a JSON array because the
    # build-bundle schema and hosted validator require an array at the root.
    $arrayPath = Join-Path $testDirectory 'dependencies.json'
    $dependencyRecords = @(
        [ordered]@{
            name = 'OpenVINOConfig.cmake'
            component = 'genai'
        }
    )
    Write-Wb05Json -Path $arrayPath -Value $dependencyRecords

    $arrayText = Get-Content -LiteralPath $arrayPath -Raw
    if ($arrayText.TrimStart()[0] -ne '[') {
        throw 'Write-Wb05Json collapsed a one-element collection into a JSON object.'
    }
    $arrayValue = $arrayText | ConvertFrom-Json
    if (@($arrayValue).Count -ne 1) {
        throw 'Write-Wb05Json did not preserve exactly one dependency record.'
    }
    if (@($arrayValue)[0].name -ne 'OpenVINOConfig.cmake') {
        throw 'Write-Wb05Json changed the singleton dependency record content.'
    }

    # A normal ordered map must remain a JSON object, proving the repair does not
    # indiscriminately wrap every evidence record in an array.
    $objectPath = Join-Path $testDirectory 'environment.json'
    $environmentRecord = [ordered]@{
        component = 'genai'
        configuration = 'Release'
    }
    Write-Wb05Json -Path $objectPath -Value $environmentRecord

    $objectText = Get-Content -LiteralPath $objectPath -Raw
    if ($objectText.TrimStart()[0] -ne '{') {
        throw 'Write-Wb05Json changed an ordinary JSON object into another root type.'
    }
    $objectValue = $objectText | ConvertFrom-Json
    if ($objectValue.configuration -ne 'Release') {
        throw 'Write-Wb05Json changed the ordinary object record content.'
    }

    Write-Host 'Workbook 05 JSON writer PowerShell tests passed.'
}
finally {
    # Always restore the test environment and unload the production module.
    Remove-Module 'Workbook05.Build' -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $testDirectory) {
        Remove-Item -LiteralPath $testDirectory -Recurse -Force
    }
}
