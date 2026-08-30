[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $repositoryRoot 'tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj'

Push-Location $repositoryRoot
try {
    dotnet test --project $project `
        --configuration Release `
        --filter 'FullyQualifiedName~CleanupInventoryContractTests' `
        --minimum-expected-tests 3

    if ($LASTEXITCODE -ne 0) {
        throw "Model Inspection cleanup inventory verification failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
