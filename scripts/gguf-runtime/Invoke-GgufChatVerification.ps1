[CmdletBinding()]
param(
    [switch]$SkipApplicationBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$projects = @(
    'tests\ContractTests\GraniteEdgeAI.GgufRuntime.Contracts.Tests\GraniteEdgeAI.GgufRuntime.Contracts.Tests.csproj',
    'tests\UnitTests\GraniteEdgeAI.GgufRuntime.Transport.Tests\GraniteEdgeAI.GgufRuntime.Transport.Tests.csproj',
    'tests\UnitTests\GraniteEdgeAI.GgufRuntime.Capabilities.Tests\GraniteEdgeAI.GgufRuntime.Capabilities.Tests.csproj',
    'tests\UnitTests\GraniteEdgeAI.GgufRuntime.Worker.Tests\GraniteEdgeAI.GgufRuntime.Worker.Tests.csproj',
    'tests\UnitTests\GraniteEdgeAI.GgufRuntime.WorkerClient.Tests\GraniteEdgeAI.GgufRuntime.WorkerClient.Tests.csproj',
    'tests\IntegrationTests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj'
)

Push-Location $repositoryRoot
try {
    foreach ($relativeProject in $projects) {
        & dotnet run --project $relativeProject -c Release -- --progress off
        if ($LASTEXITCODE -ne 0) {
            throw "Verification failed for $relativeProject."
        }
    }

    if (-not $SkipApplicationBuild) {
        $applicationProject = 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj'
        & dotnet build $applicationProject `
            -c Debug `
            -p:Platform=x64 `
            -p:RuntimeIdentifier=win-x64 `
            -p:SelfContained=true `
            -p:WindowsAppSDKSelfContained=true `
            -p:PublishReadyToRun=false `
            -p:AppxPackageSigningEnabled=false `
            -p:GenerateAppxPackageOnBuild=false `
            --nologo
        if ($LASTEXITCODE -ne 0) {
            throw 'The self-contained chat preview build failed.'
        }
    }
}
finally {
    Pop-Location
}

Write-Host 'GGUF chat verification completed.'
