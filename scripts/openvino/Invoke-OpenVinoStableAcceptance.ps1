[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedCommitSha,
    [Parameter(Mandatory)][string]$OfficialStageA,
    [Parameter(Mandatory)][string]$OfficialStageB,
    [Parameter(Mandatory)][string]$ConverterStage,
    [Parameter(Mandatory)][string]$ResultsDirectory,
    [Parameter(Mandatory)][string]$CpuEvidencePath,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ModelIdentity,
    [Parameter(Mandatory)][string]$ModelIdentityPath,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ConfigurationIdentity,
    [Parameter(Mandatory)][string]$ConfigurationIdentityPath,
    [Parameter(Mandatory)][ValidateSet('proven','runtime_device_unavailable')]
    [string]$GpuDisposition,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9._ -]{1,128}$')]
    [string]$driverIdentity,
    [string]$GpuEvidencePath = '',
    [string]$ExpectedGpuDevice = '',
    [string]$ExpectedGpuName = ''
)

$ErrorActionPreference = 'Stop'

function Invoke-ClosedVerifier {
    param([string]$Path, [string[]]$Arguments, [string]$Expected)
    $output = & powershell.exe -NoLogo -NoProfile -NonInteractive `
        -ExecutionPolicy Bypass -File $Path @Arguments
    if ($LASTEXITCODE -ne 0 -or [string]$output -cne $Expected) {
        throw 'stable-verifier-failed'
    }
}

try {
    $repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
    $actualCommit = [string](& git -C $repository rev-parse HEAD)
    $status = @(& git -C $repository status --porcelain)
    if ($LASTEXITCODE -ne 0 -or
        $actualCommit.Trim().ToLowerInvariant() -cne $ExpectedCommitSha -or
        @($status | Where-Object { $_ -notmatch '^\?\? ' }).Count -ne 0) {
        throw 'stable-commit-not-immutable'
    }
    $stageA = [IO.Path]::GetFullPath($OfficialStageA)
    $stageB = [IO.Path]::GetFullPath($OfficialStageB)
    $converter = [IO.Path]::GetFullPath($ConverterStage)
    $results = [IO.Path]::GetFullPath($ResultsDirectory)
    $cpuEvidence = [IO.Path]::GetFullPath($CpuEvidencePath)
    if ($stageA -ieq $stageB -or -not (Test-Path -LiteralPath $results -PathType Container)) {
        throw 'stable-input-invalid'
    }
    Invoke-ClosedVerifier (Join-Path $PSScriptRoot 'Test-OpenVinoOfficialWorkerManifest.ps1') `
        @('-StageDirectory',$stageA) 'worker_manifest_valid'
    Invoke-ClosedVerifier (Join-Path $PSScriptRoot 'Test-OpenVinoOfficialWorkerManifest.ps1') `
        @('-StageDirectory',$stageB) 'worker_manifest_valid'
    Invoke-ClosedVerifier (Join-Path $PSScriptRoot 'Test-OpenVinoConverterWorkerManifest.ps1') `
        @('-StageDirectory',$converter) 'converter_manifest_valid'
    Invoke-ClosedVerifier (Join-Path $PSScriptRoot 'Test-OpenVinoEvidenceArtifactSet.ps1') `
        @('-EvidencePath',$cpuEvidence) 'evidence_artifact_set_valid'
    $cpu = Get-Content -LiteralPath $cpuEvidence -Raw -Encoding UTF8 | ConvertFrom-Json
    $stageBManifest = (Get-FileHash -LiteralPath (Join-Path $stageB 'worker-manifest.json') `
        -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($cpu.commitSha -cne $ExpectedCommitSha -or
        $cpu.workerManifestSha256 -cne $stageBManifest -or
        (Get-FileHash -LiteralPath ([IO.Path]::GetFullPath($ModelIdentityPath)) `
            -Algorithm SHA256).Hash.ToLowerInvariant() -cne $ModelIdentity -or
        (Get-FileHash -LiteralPath ([IO.Path]::GetFullPath($ConfigurationIdentityPath)) `
            -Algorithm SHA256).Hash.ToLowerInvariant() -cne $ConfigurationIdentity) {
        throw 'stable-identity-mismatch'
    }

    if ($GpuDisposition -ceq 'proven') {
        if ([string]::IsNullOrWhiteSpace($GpuEvidencePath) -or
            $ExpectedGpuDevice -cnotmatch '^GPU(?:\.(?:0|[1-9][0-9]*))?$' -or
            [string]::IsNullOrWhiteSpace($ExpectedGpuName)) {
            throw 'gpu-evidence-missing'
        }
        Invoke-ClosedVerifier (Join-Path $PSScriptRoot 'Test-OpenVinoGpuEvidence.ps1') `
            @('-EvidencePath',([IO.Path]::GetFullPath($GpuEvidencePath)),
              '-StageDirectory',$stageB,'-ResultsPath',(Join-Path $results 'gpu.trx'),
              '-ExpectedCommitSha',$ExpectedCommitSha,'-ExpectedDevice',$ExpectedGpuDevice,
              '-ExpectedGpuName',$ExpectedGpuName,'-ExpectedDriverVersion',$driverIdentity) `
            'gpu_evidence_valid'
    }
    elseif (-not [string]::IsNullOrWhiteSpace($GpuEvidencePath) -or
        -not [string]::IsNullOrWhiteSpace($ExpectedGpuDevice) -or
        -not [string]::IsNullOrWhiteSpace($ExpectedGpuName)) {
        throw 'gpu-evidence-substitution-rejected'
    }
    elseif ($driverIdentity -cne
        ('windows-' + [string](Get-CimInstance Win32_OperatingSystem).BuildNumber)) {
        throw 'stable-driver-identity-mismatch'
    }

    $trx = Join-Path $results 'stable-route-acceptance.trx'
    if (-not (Test-Path -LiteralPath $trx -PathType Leaf)) {
        throw 'stable-results-missing'
    }
    [xml]$document = Get-Content -LiteralPath $trx -Raw -Encoding UTF8
    $counters = $document.TestRun.ResultSummary.Counters
    if ($null -eq $counters -or [int]$counters.total -lt 12 -or
        [int]$counters.failed -ne 0 -or [int]$counters.error -ne 0 -or
        [int]$counters.aborted -ne 0 -or [int]$counters.notExecuted -ne 0 -or
        [int]$counters.passed -ne [int]$counters.total) {
        throw 'stable-results-invalid'
    }
    $rawResults = Get-Content -LiteralPath $trx -Raw -Encoding UTF8
    if ($rawResults -notmatch 'StableRouteAcceptance') {
        throw 'stable-filter-not-recorded'
    }
    if ($ModelIdentity -ceq $ConfigurationIdentity) {
        throw 'stable-model-configuration-alias'
    }
    $remaining = @(Get-Process -Name @('OpenVinoOfficial.Worker',
        'GraniteEdgeAI.OpenVino.ProtocolTestWorker',
        'GraniteEdgeAI.OpenVino.ParentExitFixture') -ErrorAction SilentlyContinue)
    $converterPrefix = $converter.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $remaining += @(Get-Process -Name 'python' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and [IO.Path]::GetFullPath($_.Path).StartsWith(
            $converterPrefix, [StringComparison]::OrdinalIgnoreCase) })
    if ($remaining.Count -ne 0) { throw 'stable-process-residue' }
    $staging = @(Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory -Force `
        -Filter '.granite-openvino-*.staging' -ErrorAction SilentlyContinue)
    if ($staging.Count -ne 0) { throw 'stable-staging-residue' }
    [Console]::Out.WriteLine('stable_route_accepted')
    exit 0
}
catch {
    [Console]::Out.WriteLine('stable_route_rejected')
    exit 1
}
