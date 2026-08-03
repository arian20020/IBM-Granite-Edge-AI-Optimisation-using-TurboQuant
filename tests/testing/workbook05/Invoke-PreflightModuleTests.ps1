$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Resolve the repository so the test can be launched from any directory.
$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Import-Module (
    Join-Path $RepositoryRoot 'scripts/testing/workbook05/Workbook05.Preflight.psm1'
) -Force

# Keep the test harness dependency-free and give every assertion a clear message.
function Assert-Equal {
    param($Expected, $Actual, [string]$Message)
    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected' but got '$Actual'."
    }
}

$neverText = Get-Content -Raw -LiteralPath (
    Join-Path $PSScriptRoot 'fixtures/powercfg-ac-never.txt'
)
$timeoutText = Get-Content -Raw -LiteralPath (
    Join-Path $PSScriptRoot 'fixtures/powercfg-ac-timeout.txt'
)

Assert-Equal 0 (Get-Workbook05AcSleepTimeoutSeconds -PowerCfgOutput $neverText) 'Never-sleep parsing failed.'
Assert-Equal 900 (Get-Workbook05AcSleepTimeoutSeconds -PowerCfgOutput $timeoutText) 'Timeout parsing failed.'

$settings = Get-Content -Raw -LiteralPath (
    Join-Path $RepositoryRoot 'experiments/granite_turboquant_intel/configurations/workbook05/preflight-settings.json'
) | ConvertFrom-Json

$validObservation = [pscustomobject]@{
    RunnerName = 'lenovo-pf4hmd0t-wb05'
    RunnerOs = 'Windows'
    RunnerArch = 'X64'
    ComputerName = 'LENOVO-PF4HMD0T'
    ProcessorName = '12th Gen Intel(R) Core(TM) i5-12450H'
    ServiceAccount = 'nt authority\network service'
    Is64BitOperatingSystem = $true
    PhysicalMemoryGiB = 15.7
    AvailableMemoryGiB = 8.0
    SystemDriveFreeGiB = 100.0
    PowerLineStatus = 'Online'
    AcSleepTimeoutSeconds = 0
    PythonVersion = '3.12.10'
    PythonPath = 'C:\Program Files\Python312\python.exe'
    GitVersion = 'git version 2.53.0.windows.3'
    CMakeVersion = '3.31.6'
    MSBuildVersion = '17.14.51.32402'
    CompilerVersion = '19.44.35228.0'
    WindowsSdkVersion = '10.0.28000.0'
    Repository = 'arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant'
    RepositoryCommit = ('a' * 40)
    RepositoryClean = $true
    EvidenceWriteProbePassed = $true
}

$passed = Test-Workbook05PreflightObservation -Observation $validObservation -Settings $settings
Assert-Equal 'Passed' $passed.OverallStatus 'Valid observation should pass.'

$lowMemoryObservation = $validObservation.PSObject.Copy()
$lowMemoryObservation.AvailableMemoryGiB = 5.5
$failed = Test-Workbook05PreflightObservation -Observation $lowMemoryObservation -Settings $settings
Assert-Equal 'Failed' $failed.OverallStatus 'Low-memory observation should fail.'
if (-not ($failed.Checks | Where-Object { $_.Name -eq 'Available physical memory' -and -not $_.Passed })) {
    throw 'Low-memory failure did not identify the available-memory gate.'
}

Write-Host 'Workbook 05 preflight module tests passed.'
