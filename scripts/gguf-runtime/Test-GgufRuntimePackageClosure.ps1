[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$PackageRoot,
    [Parameter(Mandatory)] [string]$ManifestPath
)

$ErrorActionPreference = 'Stop'
$verifier = Join-Path $PSScriptRoot 'Test-GgufRuntimeManifest.ps1'
& $verifier -PackageRoot $PackageRoot -ManifestPath $ManifestPath
if (-not $?) {
    throw 'GGUF runtime manifest verification failed.'
}

$forbiddenPatterns = @('*.pdb', '*.log', '*server*.exe', '*quantize*.exe')
foreach ($pattern in $forbiddenPatterns) {
    if (Get-ChildItem -LiteralPath $PackageRoot -File -Recurse -Filter $pattern) {
        throw "The runtime package closure contains forbidden content: $pattern"
    }
}

Write-Host 'GGUF runtime package closure verified.'
