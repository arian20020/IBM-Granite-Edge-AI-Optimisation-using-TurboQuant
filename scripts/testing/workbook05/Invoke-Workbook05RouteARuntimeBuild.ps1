[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string]$RunIdentity,

    [string]$PythonPath = 'C:\Program Files\Python312\python.exe'
)

<#
.SYNOPSIS
Builds and installs the exact Workbook 05 Route A OpenVINO Runtime source.

.DESCRIPTION
The script follows the pinned Windows source-build sequence, captures every
native command and resource boundary, hashes outputs in place, and uploads no
binary or source payload. It does not download or execute a model and it does
not claim that any TurboQuant codec activated.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Freeze the source, document, toolchain, generator, and route identity reviewed
# at Checkpoint B1. Any later change requires a separately approved deviation.
$RouteId = 'route-a-merged-openvino'
$Component = 'runtime'
$SourceRepository = 'https://github.com/openvinotoolkit/openvino.git'
$SourceCommit = 'b9a1f201c109e0bed74763934f79483cf6c4cbf4'
$BuildDocument = 'docs/dev/build_windows.md'
$Generator = 'Visual Studio 17 2022'
$WorkspaceRoot = 'C:\w5a'
$ExpectedPythonVersion = 'Python 3.12.10'
$CMakePath = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'

function Write-RouteARuntimeDecision {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Passed', 'Failed', 'Blocked', 'Infrastructure interrupted')]
        [string]$Status,

        [Parameter(Mandatory = $true)]
        [string[]]$Reasons
    )

    # A build decision proves only the component build boundary. Every later
    # model, activation, storage, performance, and quality permission stays off.
    $decision = [ordered]@{
        schema_version = '1.0'
        campaign_id = 'GTQ-WB05-MF-v1'
        record_type = 'build-decision'
        route_id = $RouteId
        component = $Component
        source_commit = $SourceCommit
        status = $Status
        reasons = @($Reasons)
        required_components = @()
        granite_model_test_authorised = $false
        activation_claim_authorised = $false
        packed_storage_claim_authorised = $false
        performance_claim_authorised = $false
        quality_claim_authorised = $false
    }
    Write-Wb05Json `
        -Path (Join-Path $OutputDirectory 'decision.json') `
        -Value $decision
}

function Complete-RouteARuntimeEvidence {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Passed', 'Failed', 'Blocked', 'Infrastructure interrupted')]
        [string]$Status,

        [Parameter(Mandatory = $true)]
        [string[]]$Reasons
    )

    # Write the decision before hashing so the manifest covers the final
    # scientific outcome as well as every command and metadata record.
    Write-RouteARuntimeDecision -Status $Status -Reasons $Reasons
    Write-Wb05Manifest -EvidenceDirectory $OutputDirectory | Out-Null
    Write-Host "WORKBOOK05_ROUTE_A_RUNTIME_STATUS=$Status"
}

function Invoke-RouteACommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandId,

        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [switch]$MonitorResources
    )

    # Keep the repeated route/component/evidence parameters in one wrapper so
    # each native call uses the same reviewed evidence boundary.
    return Invoke-Wb05LoggedProcess `
        -CommandId $CommandId `
        -RouteId $RouteId `
        -Component $Component `
        -FilePath $FilePath `
        -ArgumentList $Arguments `
        -WorkingDirectory $WorkingDirectory `
        -EvidenceDirectory (Join-Path $OutputDirectory 'commands') `
        -EnvironmentAllowlist @{
            RUNNER_NAME = [string]$env:RUNNER_NAME
            RUNNER_OS = [string]$env:RUNNER_OS
        } `
        -MonitorResources:$MonitorResources
}

function Assert-ExitZero {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Result,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    # Source-provenance commands are integrity prerequisites. A non-zero result
    # cannot be converted into a successful or merely scientific build outcome.
    if ($Result.record.exit_code -ne 0) {
        throw "$Description exited with code $($Result.record.exit_code)."
    }
}

function Read-CommandOutput {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Result
    )

    return (
        Get-Content -LiteralPath $Result.stdout_path -Raw -ErrorAction Stop
    ).Trim()
}

function Get-CMakeCacheValue {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Lines,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $line = $Lines |
        Where-Object {
            $_ -match "^$([Regex]::Escape($Name)):[^=]+="
        } |
        Select-Object -First 1

    if (-not $line) {
        return $null
    }
    return ($line -split '=', 2)[1]
}

$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$modulePath = Join-Path $RepositoryRoot 'scripts/testing/workbook05/Workbook05.Build.psm1'
if (-not (Test-Path -LiteralPath $modulePath -PathType Leaf)) {
    throw "Workbook 05 build module is missing: $modulePath"
}
Import-Module $modulePath -Force

# Refuse missing or unexpected tools before creating an external source tree.
if (-not (Test-Path -LiteralPath $PythonPath -PathType Leaf)) {
    throw "Pinned Python was not found at: $PythonPath"
}
if (-not (Test-Path -LiteralPath $CMakePath -PathType Leaf)) {
    throw "Pinned CMake was not found at: $CMakePath"
}
$pythonVersion = ((& $PythonPath --version 2>&1) | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $pythonVersion -ne $ExpectedPythonVersion) {
    throw "Expected $ExpectedPythonVersion, found: $pythonVersion"
}
$gitCommand = Get-Command `
    -Name 'git.exe' `
    -CommandType Application `
    -All `
    -ErrorAction Stop |
    Select-Object -First 1
$gitPath = $gitCommand.Source

# Evidence is immutable for one workflow attempt; an existing directory is a
# contamination risk and is never silently removed or reused.
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "Evidence directory already exists and will not be reused: $OutputDirectory"
}
New-Item -ItemType Directory -Path $OutputDirectory -Force:$false | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path

try {
    $workspace = New-Wb05ExternalWorkspace `
        -Root $WorkspaceRoot `
        -RunIdentity $RunIdentity
    $sourceRoot = Join-Path $workspace.work_directory 'ov'
    $buildRoot = Join-Path $workspace.work_directory 'b-ov'
    $installRoot = Join-Path $workspace.work_directory 'i-ov'

    # Record the target machine and pinned tool entry points without exposing the
    # complete process environment or copying any tool binary.
    $operatingSystem = Get-CimInstance Win32_OperatingSystem
    $processor = Get-CimInstance Win32_Processor | Select-Object -First 1
    Write-Wb05Json `
        -Path (Join-Path $OutputDirectory 'environment.json') `
        -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            route_id = $RouteId
            component = $Component
            computer_name = $env:COMPUTERNAME
            runner_name = $env:RUNNER_NAME
            runner_os = $env:RUNNER_OS
            operating_system = $operatingSystem.Caption
            operating_system_version = $operatingSystem.Version
            processor = $processor.Name
            logical_processors = $processor.NumberOfLogicalProcessors
            total_visible_memory_kib = [int64]$operatingSystem.TotalVisibleMemorySize
            free_physical_memory_kib_before = [int64]$operatingSystem.FreePhysicalMemory
            python = $pythu˜û4r´≤⁄Óù∆≠y