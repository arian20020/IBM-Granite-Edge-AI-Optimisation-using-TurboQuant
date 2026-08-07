Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$modulePath = Join-Path $repositoryRoot 'scripts\testing\workbook05\Workbook05.SourceAdmission.psm1'
Import-Module $modulePath -Force

function Assert-True {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Throws {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Action,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )
    $threw = $false
    try {
        & $Action
    }
    catch {
        $threw = $true
    }
    if (-not $threw) {
        throw $Message
    }
}

$tempRoot = Join-Path $env:RUNNER_TEMP ("wb05-source-module-" + [Guid]::NewGuid().ToString('N'))
try {
    $workspace = Join-Path $tempRoot 'workspace'
    $decision = Test-Workbook05ExternalWorkspace `
        -WorkspaceRoot $workspace `
        -AllowedRoot $workspace `
        -CreateIfMissing

    Assert-True -Condition $decision.Permitted -Message 'A normal controlled directory must be accepted.'
    Assert-True -Condition $decision.Created -Message 'The missing controlled directory must be created explicitly.'

    # Existing unexpected children are observed but never removed by validation.
    $sentinelPath = Join-Path $workspace 'keep-me.txt'
    Set-Content -LiteralPath $sentinelPath -Value 'preserve' -Encoding UTF8
    [void](Test-Workbook05ExternalWorkspace -WorkspaceRoot $workspace -AllowedRoot $workspace)
    Assert-True -Condition (Test-Path -LiteralPath $sentinelPath -PathType Leaf) `
        -Message 'Workspace validation must not delete unexpected existing data.'

    $filePath = Join-Path $tempRoot 'not-a-directory.txt'
    Set-Content -LiteralPath $filePath -Value 'file' -Encoding UTF8
    Assert-Throws -Action {
        Test-Workbook05ExternalWorkspace -WorkspaceRoot $filePath -AllowedRoot $filePath
    } -Message 'A file at the controlled root must be rejected.'

    # A junction is a reparse point and must not be accepted as the workspace root.
    $junctionTarget = Join-Path $tempRoot 'junction-target'
    $junctionPath = Join-Path $tempRoot 'junction-root'
    New-Item -ItemType Directory -Path $junctionTarget -Force | Out-Null
    try {
        New-Item -ItemType Junction -Path $junctionPath -Target $junctionTarget -Force | Out-Null
        Assert-Throws -Action {
            Test-Workbook05ExternalWorkspace -WorkspaceRoot $junctionPath -AllowedRoot $junctionPath
        } -Message 'A reparse-point workspace must be rejected.'
    }
    catch {
        if (Test-Path -LiteralPath $junctionPath) {
            throw
        }
        Write-Host 'Junction creation was unavailable; the reparse-point assertion was skipped.'
    }

    $evidenceDirectory = Join-Path $tempRoot 'evidence'
    $nativeResult = Invoke-Workbook05RecordedCommand `
        -FilePath (Join-Path $PSHOME 'powershell.exe') `
        -ArgumentList @(
            '-NoLogo',
            '-NoProfile',
            '-Command',
            "Write-Output 'source-admission-out'; [Console]::Error.WriteLine('source-admission-error'); exit 3"
        ) `
        -WorkingDirectory $tempRoot `
        -EvidenceDirectory $evidenceDirectory `
        -CommandId 'recorded-command' `
        -TimeoutSeconds 30

    Assert-True -Condition ($nativeResult.Record.exit_code -eq 3) `
        -Message 'The native exit code must be preserved.'
    Assert-True -Condition (-not $nativeResult.Record.timed_out) `
        -Message 'The short native command must not time out.'
    Assert-True -Condition ((Get-Content -LiteralPath $nativeResult.StandardOutputPath -Raw) -match 'source-admission-out') `
        -Message 'Standard output must be preserved.'
    Assert-True -Condition ((Get-Content -LiteralPath $nativeResult.StandardErrorPath -Raw) -match 'source-admission-error') `
        -Message 'Standard error must be preserved.'
    Assert-True -Condition (Test-Path -LiteralPath $nativeResult.RecordPath -PathType Leaf) `
        -Message 'The JSON command record must be written.'

    $record = Get-Content -LiteralPath $nativeResult.RecordPath -Raw | ConvertFrom-Json
    Assert-True -Condition ($record.argv.Count -eq 4) `
        -Message 'The complete argument list must be recorded as data.'
    Assert-True -Condition ($record.working_directory -eq [IO.Path]::GetFullPath($tempRoot)) `
        -Message 'The exact working directory must be recorded.'

    Write-Host 'Workbook 05 source-admission PowerShell module tests passed.'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
