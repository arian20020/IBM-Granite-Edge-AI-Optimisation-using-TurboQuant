[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$StageDirectory
)

$ErrorActionPreference = 'Stop'

try {
    $output = & (Get-Command powershell.exe -ErrorAction Stop).Source `
        -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'New-OpenVinoOfficialWorkerManifest.ps1') `
        -StageDirectory $StageDirectory -AllowedDirectoryList 'licenses,tests'
    if ($LASTEXITCODE -ne 0 -or [string]$output -cne 'worker_manifest_created') {
        throw 'Manifest creation failed.'
    }
    [Console]::Out.WriteLine('turboquant_worker_manifest_created')
}
catch {
    [Console]::Out.WriteLine('turboquant_worker_manifest_invalid')
    exit 1
}
