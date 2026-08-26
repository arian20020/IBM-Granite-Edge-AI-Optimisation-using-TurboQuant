[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Component,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SourceRepository,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$DestinationRepository,
    [string]$WriteAllowedPathspec,
    [switch]$VerifyStaged,
    [switch]$StageVerified
)

$ErrorActionPreference = 'Stop'
$modulePath = Join-Path -Path $PSScriptRoot -ChildPath 'CrossRouteImportManifest.Core.psm1'
Import-Module -Force -Name $modulePath

try {
    $result = Invoke-CrossRouteImportVerification `
        -Component $Component `
        -SourceRepository $SourceRepository `
        -DestinationRepository $DestinationRepository `
        -ManifestPath (Join-Path -Path $DestinationRepository -ChildPath 'docs\handoffs\2026-08-26-cross-route-optimisation-import-manifest.json') `
        -WriteAllowedPathspec $WriteAllowedPathspec `
        -VerifyStaged:$VerifyStaged `
        -StageVerified:$StageVerified
    Write-Output "Import verification succeeded: component=$($result.Component); destinations=$($result.DestinationCount); patches=$($result.PatchCount); staged=$($result.VerifyStaged)."
}
catch {
    $safeMessage = [string]$_.Exception.Message
    foreach ($privateRoot in @($SourceRepository, $DestinationRepository, $PSScriptRoot)) {
        if (-not [string]::IsNullOrWhiteSpace($privateRoot)) {
            $safeMessage = [regex]::Replace($safeMessage, [regex]::Escape([IO.Path]::GetFullPath($privateRoot)), '[private-path]', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
        }
    }
    $safeMessage = [regex]::Replace($safeMessage, '(?i)(?<![a-z0-9])(?:[a-z]:[\\/]|\\\\)[^\r\n]*', '[private-path]')
    $safeMessage = [regex]::Replace($safeMessage, '[\x00-\x08\x0b\x0c\x0e-\x1f\x7f]', '')
    if ($safeMessage.Length -gt 512) {
        $safeMessage = $safeMessage.Substring(0, 509) + '...'
    }
    [Console]::Error.WriteLine('Import verification failed: ' + $safeMessage)
    exit 1
}
