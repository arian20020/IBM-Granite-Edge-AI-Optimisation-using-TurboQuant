[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj'
$outputRoot = Join-Path $repositoryRoot 'IBM Granite with TurboQuant (Intel)\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64'
$applicationPath = Join-Path $outputRoot 'IBM Granite with TurboQuant (Intel).exe'

if (-not $SkipBuild) {
    & dotnet build $projectPath `
        -c Debug `
        -p:Platform=x64 `
        -p:RuntimeIdentifier=win-x64 `
        -p:SelfContained=true `
        -p:WindowsAppSDKSelfContained=true `
        -p:PublishReadyToRun=false `
        -p:AppxPackageSigningEnabled=false `
        -p:GenerateAppxPackageOnBuild=false

    if ($LASTEXITCODE -ne 0) {
        throw "The chat preview build failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $applicationPath -PathType Leaf)) {
    throw "The chat preview executable was not found. Run this script without -SkipBuild first."
}

Write-Host 'Opening Granite Edge AI.'
Write-Host 'For real local AI output: import a GGUF model, wait for inspection, then select Open in Chat.'
Write-Host 'Preview Chat remains a deterministic UI-only preview and does not run a model.'
Start-Process -FilePath $applicationPath -WorkingDirectory $outputRoot
