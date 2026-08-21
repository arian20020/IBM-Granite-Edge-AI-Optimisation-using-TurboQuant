[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$EvidencePath
)

$ErrorActionPreference = 'Stop'

function Stop-Invalid {
    [Console]::Out.WriteLine('evidence_artifact_set_invalid')
    exit 1
}

try {
    $fullPath = [IO.Path]::GetFullPath($EvidencePath)
    $directory = Split-Path -Parent $fullPath
    if (-not (Test-Path -LiteralPath $directory -PathType Container) -or
        -not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        Stop-Invalid
    }

    $directoryItem = Get-Item -LiteralPath $directory -Force
    $file = Get-Item -LiteralPath $fullPath -Force
    if (($directoryItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        Stop-Invalid
    }

    $entries = @(Get-ChildItem -LiteralPath $directory -Force)
    if ($entries.Count -ne 1 -or
        $entries[0].PSIsContainer -or
        [IO.Path]::GetFullPath($entries[0].FullName) -cne $fullPath) {
        Stop-Invalid
    }

    $powerShell = (Get-Command powershell.exe -ErrorAction Stop).Source
    $privacy = & $powerShell -NoLogo -NoProfile -NonInteractive `
        -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'Test-OpenVinoEvidencePrivacy.ps1') `
        -EvidencePath $fullPath
    if ($LASTEXITCODE -ne 0 -or [string]$privacy -cne 'evidence_privacy_valid') {
        Stop-Invalid
    }

    $postEntries = @(Get-ChildItem -LiteralPath $directory -Force)
    if ($postEntries.Count -ne 1 -or
        [IO.Path]::GetFullPath($postEntries[0].FullName) -cne $fullPath) {
        Stop-Invalid
    }

    [Console]::Out.WriteLine('evidence_artifact_set_valid')
    exit 0
}
catch {
    Stop-Invalid
}
