[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$RepositoryRoot = '',

    [Parameter(Mandatory = $false)]
    [string]$PythonPath = 'python'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The repository gate executes every *.Tests.ps1 file without arguments. Resolve
# the root from this file in that mode, while preserving explicit injection for
# the Python behavior test and developer troubleshooting.
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (
        Resolve-Path `
            -LiteralPath (Join-Path $PSScriptRoot '..\..\..') `
            -ErrorAction Stop
    ).Path
}
else {
    $RepositoryRoot = (
        Resolve-Path -LiteralPath $RepositoryRoot -ErrorAction Stop
    ).Path
}

$Wrapper = Join-Path $RepositoryRoot 'scripts\testing\workbook05\Assert-Workbook05Phase3Prerequisites.ps1'
if (-not (Test-Path -LiteralPath $Wrapper -PathType Leaf)) {
    throw "Prerequisite wrapper is missing: $Wrapper"
}

$FixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("wb05-phase3-prerequisite-" + [guid]::NewGuid().ToString('N'))
$RuntimeInstall = Join-Path $FixtureRoot 'runtime\i-ov'
$GenAIInstall = Join-Path $FixtureRoot 'genai\i-genai'
$RuntimeDecision = Join-Path $FixtureRoot 'accepted-runtime\decision.json'
$GenAIDecision = Join-Path $FixtureRoot 'accepted-genai\decision.json'
$OutputPath = Join-Path $FixtureRoot 'evidence\phase3-prerequisite-proof.json'

$RuntimeDecisionBase64 = 'ew0KICAgICJzY2hlbWFfdmVyc2lvbiI6ICAiMS4wIiwNCiAgICAiY2FtcGFpZ25faWQiOiAgIkdUUS1XQjA1LU1GLXYxIiwNCiAgICAicmVjb3JkX3R5cGUiOiAgImJ1aWxkLWRlY2lzaW9uIiwNCiAgICAicm91dGVfaWQiOiAgInJvdXRlLWEtbWVyZ2VkLW9wZW52aW5vIiwNCiAgICAiY29tcG9uZW50IjogICJydW50aW1lIiwNCiAgICAic291cmNlX2NvbW1pdCI6ICAiYjlhMWYyMDFjMTA5ZTBiZWQ3NDc2MzkzNGY3OTQ4M2NmNmM0Y2JmNCIsDQogICAgInN0YXR1cyI6ICAiUGFzc2VkIiwNCiAgICAicmVhc29ucyI6ICBbDQogICAgICAgICAgICAgICAgICAgICJUaGUgZXhhY3QgcGlubmVkIFJvdXRlIEEgT3BlblZJTk8gUnVudGltZSByZXN1bWVkIGZyb20gdGhlIGluZGVwZW5kZW50bHkgdmFsaWRhdGVkIHRpbWVvdXQgd29ya3NwYWNlLCBjb21wbGV0ZWQgaXRzIGluY3JlbWVudGFsIGJ1aWxkIGFuZCBpbnN0YWxsLCByZXRhaW5lZCBib3RoIHJlcXVpcmVkIEdlbkFJIGZyb250ZW5kIGhlYWRlcnMsIGFuZCBwcm9kdWNlZCBoYXNoYWJsZSBvdXRwdXRzLiBObyBtb2RlbCBvciBzY2llbnRpZmljIGNsYWltIGlzIGF1dGhvcmlzZWQuIg0KICAgICAgICAgICAgICAgIF0sDQogICAgInJlcXVpcmVkX2NvbXBvbmVudHMiOiAgWw0KDQogICAgICAgICAgICAgICAgICAgICAgICAgICAgXSwNCiAgICAiZ3Jhbml0ZV9tb2RlbF90ZXN0X2F1dGhvcmlzZWQiOiAgZmFsc2UsDQogICAgImFjdGl2YXRpb25fY2xhaW1fYXV0aG9yaXNlZCI6ICBmYWxzZSwNCiAgICAicGFja2VkX3N0b3JhZ2VfY2xhaW1fYXV0aG9yaXNlZCI6ICBmYWxzZSwNCiAgICAicGVyZm9ybWFuY2VfY2xhaW1fYXV0aG9yaXNlZCI6ICBmYWxzZSwNCiAgICAicXVhbGl0eV9jbGFpbV9hdXRob3Jpc2VkIjogIGZhbHNlDQp9Cg=='
$GenAIDecisionBase64 = 'ew0KICAgICJzY2hlbWFfdmVyc2lvbiI6ICAiMS4wIiwNCiAgICAiY2FtcGFpZ25faWQiOiAgIkdUUS1XQjA1LU1GLXYxIiwNCiAgICAicmVjb3JkX3R5cGUiOiAgImJ1aWxkLWRlY2lzaW9uIiwNCiAgICAicm91dGVfaWQiOiAgInJvdXRlLWEtbWVyZ2VkLW9wZW52aW5vIiwNCiAgICAiY29tcG9uZW50IjogICJnZW5haSIsDQogICAgInNvdXJjZV9jb21taXQiOiAgImJkOGQ2NTQyZTNjYTFhYzMwMDQyZDVkOGQ0MjAyY2UwMGI1ZjRhZjAiLA0KICAgICJzdGF0dXMiOiAgIlBhc3NlZCIsDQogICAgInJlYXNvbnMiOiAgWw0KICAgICAgICAgICAgICAgICAgICAiVGhlIGV4YWN0IEdlbkFJIHNvdXJjZSBidWlsdCBhZ2FpbnN0IHRoZSBleGFjdCBhY2NlcHRlZCBSb3V0ZSBBIFJ1bnRpbWUgcGFja2FnZS4gTm8gbW9kZWwsIGFjdGl2YXRpb24sIHN0b3JhZ2UsIGZhbGxiYWNrLCBwZXJmb3JtYW5jZSwgb3IgcXVhbGl0eSBjbGFpbSBpcyBhdXRob3Jpc2VkLiINCiAgICAgICAgICAgICAgICBdLA0KICAgICJyZXF1aXJlZF9jb21wb25lbnRzIjogIFsNCg0KICAgICAgICAgICAgICAgICAgICAgICAgICAgIF0sDQogICAgImdyYW5pdGVfbW9kZWxfdGVzdF9hdXRob3Jpc2VkIjogIGZhbHNlLA0KICAgICJhY3RpdmF0aW9uX2NsYWltX2F1dGhvcmlzZWQiOiAgZmFsc2UsDQogICAgInBhY2tlZF9zdG9yYWdlX2NsYWltX2F1dGhvcmlzZWQiOiAgZmFsc2UsDQogICAgInBlcmZvcm1hbmNlX2NsYWltX2F1dGhvcmlzZWQiOiAgZmFsc2UsDQogICAgInF1YWxpdHlfY2xhaW1fYXV0aG9yaXNlZCI6ICBmYWxzZQ0KfQo='

function Write-FixtureFile {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $Path = Join-Path $Root $RelativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force | Out-Null
    [IO.File]::WriteAllText($Path, $Content, [Text.UTF8Encoding]::new($false))
}

function Get-Sha256 {
    param(
        [Parameter(Mandatory = $true)][string]$LiteralPath
    )

    $Stream = [IO.File]::OpenRead($LiteralPath)
    $Hasher = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($Hasher.ComputeHash($Stream))).Replace('-', '')
    }
    finally {
        $Hasher.Dispose()
        $Stream.Dispose()
    }
}

try {
    New-Item -ItemType Directory -Path $RuntimeInstall -Force | Out-Null
    New-Item -ItemType Directory -Path $GenAIInstall -Force | Out-Null
    New-Item -ItemType Directory -Path (Split-Path -Parent $RuntimeDecision) -Force | Out-Null
    New-Item -ItemType Directory -Path (Split-Path -Parent $GenAIDecision) -Force | Out-Null
    New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null

    Write-FixtureFile -Root $RuntimeInstall -RelativePath 'runtime\cmake\OpenVINOConfig.cmake' -Content 'runtime-config'
    Write-FixtureFile -Root $RuntimeInstall -RelativePath 'runtime\include\openvino\frontend\onnx\extension\conversion.hpp' -Content 'onnx-header'
    Write-FixtureFile -Root $RuntimeInstall -RelativePath 'runtime\include\openvino\frontend\tensorflow\extension\conversion.hpp' -Content 'tensorflow-header'
    Write-FixtureFile -Root $GenAIInstall -RelativePath 'runtime\bin\intel64\Release\openvino_genai.dll' -Content 'genai-dll'
    Write-FixtureFile -Root $GenAIInstall -RelativePath 'runtime\bin\intel64\Release\openvino_genai_c.dll' -Content 'genai-c-dll'
    Write-FixtureFile -Root $GenAIInstall -RelativePath 'runtime\bin\intel64\Release\openvino_tokenizers.dll' -Content 'tokenizers-dll'
    Write-FixtureFile -Root $GenAIInstall -RelativePath 'python\openvino_genai\py_openvino_genai.cp312-win_amd64.pyd' -Content 'python-extension'

    [IO.File]::WriteAllBytes($RuntimeDecision, [Convert]::FromBase64String($RuntimeDecisionBase64))
    [IO.File]::WriteAllBytes($GenAIDecision, [Convert]::FromBase64String($GenAIDecisionBase64))

    & $Wrapper `
        -RepositoryRoot $RepositoryRoot `
        -RuntimeInstall $RuntimeInstall `
        -RuntimeDecision $RuntimeDecision `
        -GenAIInstall $GenAIInstall `
        -GenAIDecision $GenAIDecision `
        -OutputPath $OutputPath `
        -PythonPath $PythonPath > $null

    if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
        throw 'The valid fixture did not produce a prerequisite proof.'
    }
    $Proof = Get-Content -LiteralPath $OutputPath -Raw | ConvertFrom-Json
    if ($Proof.status -ne 'Passed') {
        throw "The valid fixture produced an unexpected status: $($Proof.status)"
    }
    $OriginalOutputHash = Get-Sha256 -LiteralPath $OutputPath

    [IO.File]::WriteAllText($RuntimeDecision, '{}', [Text.UTF8Encoding]::new($false))
    $FailedAsExpected = $false
    try {
        & $Wrapper `
            -RepositoryRoot $RepositoryRoot `
            -RuntimeInstall $RuntimeInstall `
            -RuntimeDecision $RuntimeDecision `
            -GenAIInstall $GenAIInstall `
            -GenAIDecision $GenAIDecision `
            -OutputPath $OutputPath `
            -PythonPath $PythonPath > $null
    }
    catch {
        $FailedAsExpected = $true
    }

    if (-not $FailedAsExpected) {
        throw 'The corrupted Runtime decision did not fail prerequisite verification.'
    }
    $PermanentOutputHash = Get-Sha256 -LiteralPath $OutputPath
    if ($PermanentOutputHash -ne $OriginalOutputHash) {
        throw 'The existing prerequisite proof changed after a failed verification.'
    }
    if (Test-Path -LiteralPath "$OutputPath.tmp") {
        throw 'The failed verification left its temporary output behind.'
    }

    # The failure above is deliberate and leaves LASTEXITCODE nonzero. Reset it
    # only after every assertion passes so the repository gate receives a
    # truthful success result instead of the expected child-process failure.
    $global:LASTEXITCODE = 0
    Write-Host 'Workbook 05 Phase 3 prerequisite PowerShell tests passed.'
}
finally {
    if (Test-Path -LiteralPath $FixtureRoot) {
        Remove-Item -LiteralPath $FixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
