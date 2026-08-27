[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$gateOrder = @(
    'dependency-locks',
    'contracts',
    'inspection',
    'official-native',
    'converter',
    'optimization',
    'turboquant',
    'app-accessibility',
    'gguf-regression',
    'release-package',
    'hosted-evidence',
    'ucl-evidence',
    'artifact-privacy',
    'cleanup-inventory',
    'traceability'
)

function Stop-Blocked {
    [Console]::Out.WriteLine('openvino_release_blocked')
    exit 1
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory)][string]$Gate,
        [Parameter(Mandatory)][scriptblock]$Command
    )
    if ($Gate -cne $script:gateOrder[$script:gateIndex]) {
        throw 'release-gate-order-invalid'
    }
    $global:LASTEXITCODE = 0
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "release-gate-failed:$Gate" }
    $script:gateIndex++
}

function Test-ExactProperties {
    param($Value, [string[]]$Names)
    if ($null -eq $Value -or $Value -isnot [pscustomobject]) { return $false }
    $actual = @($Value.PSObject.Properties.Name)
    return $actual.Count -eq $Names.Count -and
        ($actual -join "`n") -ceq ($Names -join "`n")
}

function Get-DescendantPath {
    param([string]$Root, [string]$Path)
    $normalizedRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/') +
        [IO.Path]::DirectorySeparatorChar
    $normalizedPath = [IO.Path]::GetFullPath($Path)
    if (-not $normalizedPath.StartsWith(
            $normalizedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'evidence-path-outside-root'
    }
    return $normalizedPath
}

try {
    $repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
    $requiredEnvironment = @(
        'GRANITE_OPENVINO_OFFICIAL_WORKER_STAGE',
        'GRANITE_OPENVINO_OFFICIAL_WORKER_MANIFEST_SHA256',
        'GRANITE_OPENVINO_TURBOQUANT_WORKER_STAGE',
        'GRANITE_OPENVINO_CONVERTER_STAGE',
        'GRANITE_OPENVINO_HOSTED_EVIDENCE_ROOT',
        'GRANITE_OPENVINO_UCL_EVIDENCE_ROOT',
        'GRANITE_OPENVINO_OPERATION_ROOT',
        'GRANITE_OPENVINO_EVIDENCE_COMMIT',
        'GRANITE_OPENVINO_MODEL_SHA256',
        'GRANITE_OPENVINO_MODEL_LENGTH',
        'GRANITE_OPENVINO_TURBOQUANT_PACKAGE_MANIFEST_SHA256',
        'GRANITE_OPENVINO_HOSTED_CPU_EVIDENCE_PATH',
        'GRANITE_OPENVINO_UCL_CPU_EVIDENCE_PATH',
        'GRANITE_OPENVINO_UCL_TURBOQUANT_EVIDENCE_PATH',
        'GRANITE_OPENVINO_TURBOQUANT_CAMPAIGN_EVIDENCE_PATH'
    )
    foreach ($name in $requiredEnvironment) {
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
            Stop-Blocked
        }
    }
    $officialStage = [IO.Path]::GetFullPath($env:GRANITE_OPENVINO_OFFICIAL_WORKER_STAGE)
    $turboStage = [IO.Path]::GetFullPath($env:GRANITE_OPENVINO_TURBOQUANT_WORKER_STAGE)
    $converterStage = [IO.Path]::GetFullPath($env:GRANITE_OPENVINO_CONVERTER_STAGE)
    $hostedEvidence = [IO.Path]::GetFullPath($env:GRANITE_OPENVINO_HOSTED_EVIDENCE_ROOT)
    $uclEvidence = [IO.Path]::GetFullPath($env:GRANITE_OPENVINO_UCL_EVIDENCE_ROOT)
    $operationRoot = [IO.Path]::GetFullPath($env:GRANITE_OPENVINO_OPERATION_ROOT)
    $hostedCpuPath = Get-DescendantPath $hostedEvidence `
        $env:GRANITE_OPENVINO_HOSTED_CPU_EVIDENCE_PATH
    $uclCpuPath = Get-DescendantPath $uclEvidence `
        $env:GRANITE_OPENVINO_UCL_CPU_EVIDENCE_PATH
    $uclTurboPath = Get-DescendantPath $uclEvidence `
        $env:GRANITE_OPENVINO_UCL_TURBOQUANT_EVIDENCE_PATH
    $campaignPath = Get-DescendantPath $uclEvidence `
        $env:GRANITE_OPENVINO_TURBOQUANT_CAMPAIGN_EVIDENCE_PATH
    $evidenceCommit = $env:GRANITE_OPENVINO_EVIDENCE_COMMIT
    $modelSha256 = $env:GRANITE_OPENVINO_MODEL_SHA256
    [long]$modelLength = 0
    $modelLengthValid = [long]::TryParse(
        $env:GRANITE_OPENVINO_MODEL_LENGTH,
        [Globalization.NumberStyles]::None,
        [Globalization.CultureInfo]::InvariantCulture,
        [ref]$modelLength)
    $packageManifestSha256 =
        $env:GRANITE_OPENVINO_TURBOQUANT_PACKAGE_MANIFEST_SHA256
    $actualCommit = [string](& git -C $repository rev-parse HEAD)
    $trackedChanges = @(& git -C $repository status --porcelain | Where-Object {
        $_ -notmatch '^\?\? ' })
    if ($officialStage -ieq $turboStage -or $officialStage -ieq $converterStage -or
        $turboStage -ieq $converterStage -or
        $env:GRANITE_OPENVINO_OFFICIAL_WORKER_MANIFEST_SHA256 -cnotmatch '^[0-9a-f]{64}$' -or
        $evidenceCommit -cnotmatch '^[0-9a-f]{40}$' -or
        $modelSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        $packageManifestSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        -not $modelLengthValid -or $modelLength -le 0 -or
        $actualCommit.Trim().ToLowerInvariant() -cne $evidenceCommit -or
        $trackedChanges.Count -ne 0) {
        Stop-Blocked
    }

    $script:gateIndex = 0
    Invoke-Checked 'dependency-locks' {
        & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
            -File (Join-Path $PSScriptRoot 'Test-OpenVinoDependencyLocks.ps1')
    }
    Invoke-Checked 'contracts' {
        & dotnet test (Join-Path $repository 'tests\ContractTests\GraniteEdgeAI.OpenVino.Contracts.Tests\GraniteEdgeAI.OpenVino.Contracts.Tests.csproj') `
            --configuration Release --minimum-expected-tests 1 --progress off --no-ansi
    }
    Invoke-Checked 'inspection' {
        & dotnet test (Join-Path $repository 'tests\UnitTests\GraniteEdgeAI.OpenVino.Tests\GraniteEdgeAI.OpenVino.Tests.csproj') `
            --configuration Release -p:Platform=x64 --filter 'FullyQualifiedName~Inspection' `
            --minimum-expected-tests 1 --progress off --no-ansi
    }
    Invoke-Checked 'official-native' {
        & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
            -File (Join-Path $PSScriptRoot 'Test-OpenVinoOfficialWorkerManifest.ps1') `
            -StageDirectory $officialStage
    }
    Invoke-Checked 'converter' {
        & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
            -File (Join-Path $PSScriptRoot 'Test-OpenVinoConverterWorkerManifest.ps1') `
            -StageDirectory $converterStage
    }
    Invoke-Checked 'optimization' {
        & dotnet test (Join-Path $repository 'tests\UnitTests\GraniteEdgeAI.OpenVino.Tests\GraniteEdgeAI.OpenVino.Tests.csproj') `
            --configuration Release -p:Platform=x64 --filter 'FullyQualifiedName~Optimization' `
            --minimum-expected-tests 1 --progress off --no-ansi
    }
    Invoke-Checked 'turboquant' {
        & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
            -File (Join-Path $PSScriptRoot 'Test-OpenVinoTurboQuantWorkerManifest.ps1') `
            -StageDirectory $turboStage
    }
    Invoke-Checked 'app-accessibility' {
        & dotnet test (Join-Path $repository 'tests\UnitTests\GraniteEdgeAI.OpenVino.Tests\GraniteEdgeAI.OpenVino.Tests.csproj') `
            --configuration Release -p:Platform=x64 --filter 'FullyQualifiedName~Accessibility' `
            --minimum-expected-tests 1 --progress off --no-ansi
    }
    Invoke-Checked 'gguf-regression' {
        & dotnet test (Join-Path $repository 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj') `
            --configuration Release -p:Platform=x64 --filter 'FullyQualifiedName~ModelInspection' `
            --minimum-expected-tests 1 --progress off --no-ansi
    }
    Invoke-Checked 'release-package' {
        & dotnet build (Join-Path $repository 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj') `
            --configuration Release --runtime win-x64 -p:Platform=x64 `
            "-p:OpenVinoOfficialWorkerStageDirectory=$officialStage" `
            "-p:OpenVinoOfficialWorkerManifestSha256=$env:GRANITE_OPENVINO_OFFICIAL_WORKER_MANIFEST_SHA256"
    }
    Invoke-Checked 'hosted-evidence' {
        & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
            -File (Join-Path $PSScriptRoot 'Test-OpenVinoArtifactPrivacy.ps1') `
            -ArtifactRoot $hostedEvidence
        if ($LASTEXITCODE -ne 0) { throw 'hosted-privacy-invalid' }
        & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
            -File (Join-Path $PSScriptRoot 'Test-OpenVinoEvidencePrivacy.ps1') `
            -EvidencePath $hostedCpuPath
        if ($LASTEXITCODE -ne 0 -or
            (Get-Content -LiteralPath $hostedCpuPath -Raw -Encoding UTF8 |
                ConvertFrom-Json).commitSha -cne $evidenceCommit) {
            throw 'hosted-cpu-commit-invalid'
        }
    }
    Invoke-Checked 'ucl-evidence' {
        & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
            -File (Join-Path $PSScriptRoot 'Test-OpenVinoArtifactPrivacy.ps1') `
            -ArtifactRoot $uclEvidence
        if ($LASTEXITCODE -ne 0) { throw 'ucl-privacy-invalid' }
        & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
            -File (Join-Path $PSScriptRoot 'Test-OpenVinoEvidencePrivacy.ps1') `
            -EvidencePath $uclCpuPath
        if ($LASTEXITCODE -ne 0 -or
            (Get-Content -LiteralPath $uclCpuPath -Raw -Encoding UTF8 |
                ConvertFrom-Json).commitSha -cne $evidenceCommit) {
            throw 'ucl-cpu-invalid'
        }
        & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
            -File (Join-Path $PSScriptRoot 'Test-OpenVinoTurboQuantActivation.ps1') `
            -EvidencePath $uclTurboPath `
            -StageDirectory $turboStage -ExpectedCommitSha $evidenceCommit `
            -ExpectedPackageManifestSha256 $packageManifestSha256 `
            -ExpectedModelSha256 $modelSha256 -ExpectedModelLength $modelLength
    }
    Invoke-Checked 'artifact-privacy' {
        if ($hostedEvidence -ieq $uclEvidence) { throw 'evidence-roots-aliased' }
        Import-Module (Join-Path $PSScriptRoot 'OpenVinoClosedJson.psm1') -Force
        $raw = Get-OpenVinoClosedJsonText -Path $campaignPath `
            -MaximumBytes 16384 -MaximumDepth 5
        $campaign = $raw | ConvertFrom-Json -ErrorAction Stop
        $campaignNames = @('schemaVersion','commitSha','modelSha256','modelLength',
            'packageManifestSha256','requestedDevice','actualExecutionDevices',
            'securityReviewApproved','licenseReviewApproved','qualityRubricId',
            'matchedOfficialBaseline','deterministicSmokePassed','memoryReductionPassed',
            'qualityPassed','performancePassed','repeatabilityPassed','contextScalingPassed',
            'cancellationPassed','cleanupPassed','corruptionPassed','streamingPassed',
            'completedTurnCount')
        if (-not (Test-ExactProperties $campaign $campaignNames) -or
            $campaign.schemaVersion -ne 1 -or $campaign.commitSha -cne $evidenceCommit -or
            $campaign.modelSha256 -cne $modelSha256 -or
            $campaign.modelLength -ne $modelLength -or
            $campaign.packageManifestSha256 -cne $packageManifestSha256 -or
            $campaign.requestedDevice -cne 'CPU' -or
            @($campaign.actualExecutionDevices).Count -ne 1 -or
            $campaign.actualExecutionDevices[0] -cne 'CPU' -or
            $campaign.qualityRubricId -cne 'GTQ-QUALITY-RUBRIC-v1' -or
            $campaign.completedTurnCount -lt 2) {
            throw 'turboquant-campaign-identity-invalid'
        }
        foreach ($name in @('securityReviewApproved','licenseReviewApproved',
                'matchedOfficialBaseline','deterministicSmokePassed','memoryReductionPassed',
                'qualityPassed','performancePassed','repeatabilityPassed','contextScalingPassed',
                'cancellationPassed','cleanupPassed','corruptionPassed','streamingPassed')) {
            if ($campaign.$name -ne $true) { throw 'turboquant-campaign-incomplete' }
        }
    }
    Invoke-Checked 'cleanup-inventory' {
        & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
            -File (Join-Path $PSScriptRoot 'Test-OpenVinoCleanupInventory.ps1') `
            -OperationRoot $operationRoot `
            -OfficialWorkerRoot $officialStage `
            -ConverterRoot $converterStage `
            -TurboQuantRoot $turboStage
    }
    Invoke-Checked 'traceability' {
        $catalogue = Join-Path $repository 'docs\evidence\openvino\README.md'
        if (-not (Test-Path -LiteralPath $catalogue -PathType Leaf)) { throw 'traceability-missing' }
        $text = Get-Content -LiteralPath $catalogue -Raw -Encoding UTF8
        foreach ($identity in @('F-M18','F-M20','F-M21','F-M22','N-M02','N-M11',
                'DR-WF-008','DR-WF-010','DR-WF-011','DR-WF-013','DR-WF-014','DR-WF-015')) {
            if ($text.IndexOf($identity, [StringComparison]::Ordinal) -lt 0) {
                throw 'traceability-identity-missing'
            }
        }
    }
    if ($script:gateIndex -ne $gateOrder.Count) { throw 'release-gate-incomplete' }
    $finalCommit = [string](& git -C $repository rev-parse HEAD)
    $finalTrackedChanges = @(& git -C $repository status --porcelain | Where-Object {
        $_ -notmatch '^\?\? ' })
    if ($finalCommit.Trim().ToLowerInvariant() -cne $evidenceCommit -or
        $finalTrackedChanges.Count -ne 0) {
        throw 'release-candidate-changed'
    }
    [Console]::Out.WriteLine('openvino_release_accepted')
    exit 0
}
catch {
    Stop-Blocked
}
