[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('RuntimeWorker','Debug','Release','FinalSource')]
    [string] $Phase,

    [Parameter(Mandatory)]
    [string] $RunRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$resolvedRunRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $RunRoot))
if (Test-Path -LiteralPath $RunRoot) {
    throw 'RunRoot must be fresh; evidence cannot be overwritten or rerun.'
}

git check-ignore -q -- $resolvedRunRoot
$ignoredExitCode = $LASTEXITCODE
if ($ignoredExitCode -ne 0) {
    throw 'RunRoot must be beneath a git-ignored evidence directory.'
}

$null = New-Item -ItemType Directory -Path $resolvedRunRoot
$phaseRoot = $resolvedRunRoot
$headBefore = (& git -C $repositoryRoot rev-parse HEAD).Trim()
$statusBefore = @(& git -C $repositoryRoot status --porcelain=v1 --untracked-files=all)
$startedUtc = [DateTime]::UtcNow
$executedCommands = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::Ordinal)
$commandMetadata = [Collections.Generic.List[object]]::new()

$appProject = Join-Path $repositoryRoot 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj'
$testProject = Join-Path $repositoryRoot 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
$contractsProject = Join-Path $repositoryRoot 'tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj'
$runtimeProject = Join-Path $repositoryRoot 'tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj'
$workerProject = Join-Path $repositoryRoot 'tests\UnitTests\GraniteEdgeAI.ModelInspection.Worker.Tests\GraniteEdgeAI.ModelInspection.Worker.Tests.csproj'
$releaseIsolationScript = Join-Path $repositoryRoot 'scripts\model-inspection\Test-ModelInspectionFixtureReleaseIsolation.ps1'
$releaseIsolationEvidenceRoot = Join-Path $repositoryRoot 'TestResults\ModelInspectionFixtures\ReleaseIsolation'
$cleanupScript = Join-Path $repositoryRoot 'scripts\model-inspection\Verify-ModelInspectionCleanupInventory.ps1'

$interactionLifetimeFilter = 'FullyQualifiedName~ModelInspectionFixtureInteractionTests|FullyQualifiedName~ModelInspectionFixtureLifetimeTests'
$fixtureCategoryFilter = 'TestCategory=ModelInspectionFixtureGallery'
$hostedReleaseFilter = 'TestCategory!=ModelInspectionVisualRegression&TestCategory!=ModelInspectionControlledOs'
$n001FullyQualifiedName = 'GraniteEdgeAI.UnitTests.ModelInspectionPageNavigationTests.PackagedN001_PageJourneyCompletesAllFiveStagesAsReady'
$polishFilter = 'FullyQualifiedName~ModelInspectionPresentationFactoryTests|FullyQualifiedName~InitialInspectionProgressPresentationTests|FullyQualifiedName~ModelInspectionViewModelTests|FullyQualifiedName~ModelInspectionPageNavigationTests|FullyQualifiedName~ModelInspectionWorkerCompositionTests|FullyQualifiedName~ModelInspectionMilestoneSequencerTests|FullyQualifiedName~ModelInspectionRenderCoordinatorTests|FullyQualifiedName~ModelInspectionAccessibilityTests|FullyQualifiedName~InspectionStatusGlyphTests|FullyQualifiedName~InspectionContentCardTests|FullyQualifiedName~InspectionModelCardTests|FullyQualifiedName~InspectionOutcomeCardTests|FullyQualifiedName~ModelInspectionMotionTests|FullyQualifiedName~OnboardingStageIndicatorTests|FullyQualifiedName~InspectionActionCardTests|FullyQualifiedName~ModelInspectionPageLayoutTests|FullyQualifiedName~ModelInspectionRenderedStateTests|FullyQualifiedName~ModelInspectionDisclosureTests|FullyQualifiedName~InspectionVisualStateGuardTests|FullyQualifiedName~InspectionProgressRowsTests|FullyQualifiedName~InspectionProgressPresentationFactoryTests|FullyQualifiedName~ModelInspectionRenderHarnessTests'
$werTargets = @(
    'GraniteEdgeAI.ModelInspection.Worker',
    'GraniteEdgeAI.ModelInspection.ProtocolTestWorker',
    'IBM Granite with TurboQuant (Intel)',
    'testhost',
    'vstest.console')

$ExpectedTotals = [ordered]@{
    Runtime = 189
    Worker = 77
    InteractionLifetime = 19
    FixtureCategory = 220
    FocusedPolish = 326
    HostedRelease = 858
    N001 = 1
    Contracts = 357
}

$ExpectedTestMaps = [ordered]@{
    Runtime = [ordered]@{
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.ArtifactPrivacyScannerTests' = 6
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.ChatTemplateEvidenceFactoryTests' = 7
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.EvidenceContractTests' = 4
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.JsonEvidenceWriterTests' = 9
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.ModelFileIntegrityComparisonTests' = 7
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.ModelFileSnapshotServiceTests' = 10
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.ModelProbeSafetyValidatorTests' = 12
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.NativeLoadProgressRecorderTests' = 10
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.ProbeFailureMapperTests' = 10
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.ProbeProcessRunnerTests' = 10
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.ProbeResultFinalizerTests' = 8
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.ProductionRuntimeArchitectureTests' = 6
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.RuntimeDependencyPolicyTests' = 6
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.SensitiveTextRedactorTests' = 8
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.SocketObservationTests' = 6
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.SpikeOptionsParserTests' = 35
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.VocabOnlyCollectorSourceContractTests' = 2
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.VocabOnlyMetadataProjectionTests' = 17
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.VocabOnlyModelProbeContinuityTests' = 4
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.VocabOnlyModelProbeFailureTests' = 1
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.VocabOnlyProbeCancellationConfigurationTests' = 3
        'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.VocabOnlyProbeStageBoundaryTests' = 8
    }
    Worker = [ordered]@{
        'GraniteEdgeAI.ModelInspection.Worker.Tests.LlamaSharpInspectionEngineTests' = 67
        'GraniteEdgeAI.ModelInspection.Worker.Tests.WorkerHostTests' = 10
    }
    InteractionLifetime = [ordered]@{
        'GraniteEdgeAI.UnitTests.ModelInspectionFixtureInteractionTests' = 6
        'GraniteEdgeAI.UnitTests.ModelInspectionFixtureLifetimeTests' = 13
    }
    FixtureCategory = [ordered]@{
        'GraniteEdgeAI.UnitTests.DebugModelInspectionServiceTests' = 41
        'GraniteEdgeAI.UnitTests.ModelInspectionFixtureAdapterTests' = 12
        'GraniteEdgeAI.UnitTests.ModelInspectionFixtureGalleryTests' = 73
        'GraniteEdgeAI.UnitTests.ModelInspectionFixtureInteractionTests' = 6
        'GraniteEdgeAI.UnitTests.ModelInspectionFixtureLifetimeTests' = 13
        'GraniteEdgeAI.UnitTests.ModelInspectionFixturePageLifecycleTests' = 7
        'GraniteEdgeAI.UnitTests.ModelInspectionFixturePresetTests' = 25
        'GraniteEdgeAI.UnitTests.ModelInspectionFixtureScreenContractTests' = 37
        'GraniteEdgeAI.UnitTests.ModelInspectionFixtureViewModelIntegrationTests' = 6
    }
    FocusedPolish = [ordered]@{
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionActionCardTests' = 4
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionContentCardTests' = 25
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionModelCardTests' = 9
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionOutcomeCardTests' = 7
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionStatusGlyphTests' = 16
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.ModelInspectionDisclosureTests' = 5
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.ModelInspectionPageLayoutTests' = 4
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.InspectionProgressPresentationFactoryTests' = 13
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.InspectionProgressRowsTests' = 16
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionMilestoneSequencerTests' = 10
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionMotionTests' = 29
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionPresentationFactoryTests' = 20
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionRenderCoordinatorTests' = 27
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionAccessibilityTests' = 9
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionRenderedStateTests' = 21
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionRenderHarnessTests' = 2
        'GraniteEdgeAI.UnitTests.InitialInspectionProgressPresentationTests' = 6
        'GraniteEdgeAI.UnitTests.InspectionVisualStateGuardTests' = 13
        'GraniteEdgeAI.UnitTests.ModelInspectionPageNavigationTests' = 41
        'GraniteEdgeAI.UnitTests.ModelInspectionViewModelTests' = 24
        'GraniteEdgeAI.UnitTests.ModelInspectionWorkerCompositionTests' = 13
        'GraniteEdgeAI.UnitTests.OnboardingStageIndicatorTests' = 12
    }
    ReleaseProtected = [ordered]@{
        'GraniteEdgeAI.UnitTests.ModelInspectionRequestFactoryTests' = 10
        'GraniteEdgeAI.UnitTests.ModelInspectionContractTests' = 33
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Runtime.ModelInspectionProbeResultTests' = 4
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Runtime.WorkerRequestMapperTests' = 3
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Runtime.WorkerResultMapperTests' = 9
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Runtime.WorkerProcessLlamaModelProbeTests' = 7
        'GraniteEdgeAI.UnitTests.ModelInspectionClassifierTests' = 5
        'GraniteEdgeAI.UnitTests.ModelInspectionServiceTests' = 4
        'GraniteEdgeAI.UnitTests.DelegateCommandTests' = 2
        'GraniteEdgeAI.UnitTests.ModelInspectionViewModelTests' = 24
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.InspectionProgressPresentationFactoryTests' = 13
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionPresentationFactoryTests' = 20
        'GraniteEdgeAI.UnitTests.InspectionVisualStateGuardTests' = 13
        'GraniteEdgeAI.UnitTests.ModelInspectionPageNavigationTests' = 41
        'GraniteEdgeAI.UnitTests.OnboardingModelInspectionNavigationTests' = 15
        'GraniteEdgeAI.UnitTests.ModelInspectionWorkerCompositionTests' = 13
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionAssetContractTests' = 3
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionDisplayTextPolicyTests' = 46
        'GraniteEdgeAI.UnitTests.ModelInspectionViewSnapshotTests' = 6
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionFigmaStatePresentationTests' = 77
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.InspectionProgressRowsTests' = 16
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionActionCardTests' = 4
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionContentCardTests' = 25
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionModelCardTests' = 9
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionOutcomeCardTests' = 7
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionRenderCoordinatorTests' = 27
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionMotionTests' = 29
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.ModelInspectionDisclosureTests' = 5
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionRenderHarnessTests' = 2
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionRenderedStateTests' = 21
        'GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionAccessibilityTests' = 9
    }
    N001 = [ordered]@{
        'GraniteEdgeAI.UnitTests.ModelInspectionPageNavigationTests' = 1
    }
    Contracts = [ordered]@{
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.BuildWorkflowContractTests' = 16
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.CleanupInventoryContractTests' = 3
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.ContractGraphTests' = 1
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.Gate2ArchitectureFitnessTests' = 5
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.Gate2ProjectGraphTests' = 5
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.Gate4PackagingContractTests' = 4
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.Gate5ApplicationBoundaryContractTests' = 6
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureBuildBoundaryContractTests' = 53
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureCatalogueContractTests' = 32
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureJsonContractTests' = 16
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureLifecycleBatchContractTests' = 3
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureOperationalFailureBatchContractTests' = 3
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureReportContractTests' = 11
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureRetryRestartContractTests' = 27
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureStressBatchContractTests' = 6
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureValidationContractTests' = 37
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureWorkflowContractTests' = 2
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionVisualSourceContractTests' = 8
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol.WorkerCommandSequenceValidatorTests' = 9
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol.WorkerEvidenceValidationTests' = 41
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol.WorkerMessageSequenceValidatorTests' = 17
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol.WorkerProtocolJsonTests' = 19
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol.WorkerProtocolTests' = 33
    }
}

function Write-JsonFile {
    param([Parameter(Mandatory)] [string] $Path, [Parameter(Mandatory)] $Value)
    ConvertTo-Json -InputObject $Value -Depth 8 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Get-SourceFreeze {
    $lines = [Collections.Generic.List[string]]::new()
    $trackedPaths = @(& git -C $repositoryRoot ls-files)
    if ($LASTEXITCODE -ne 0) {
        throw 'Tracked-source discovery failed.'
    }
    $untrackedPaths = @(
        & git -C $repositoryRoot ls-files --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) {
        throw 'Untracked-source discovery failed.'
    }
    $relativePaths = @($trackedPaths + $untrackedPaths | Sort-Object -Unique)
    foreach ($relativePath in $relativePaths) {
        if ([string]::IsNullOrWhiteSpace($relativePath)) { continue }
        $absolutePath = Join-Path $repositoryRoot $relativePath
        if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) {
            throw "Tracked source is missing: $relativePath"
        }
        $item = Get-Item -LiteralPath $absolutePath
        $hash = (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash
        $lines.Add("$relativePath|$($item.Length)|$hash")
    }
    return @($lines)
}

function Assert-SourceFreeze {
    param([Parameter(Mandatory)] [string[]] $Before)
    $after = @(Get-SourceFreeze)
    $after | Set-Content -LiteralPath (Join-Path $phaseRoot 'source-freeze-after.sha256') -Encoding utf8
    if ($null -ne (Compare-Object -ReferenceObject $Before -DifferenceObject $after)) {
        throw 'Tracked source changed while the evidence gate was running.'
    }
    $headAfter = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $statusAfter = @(& git -C $repositoryRoot status --porcelain=v1 --untracked-files=all)
    if ($headAfter -cne $headBefore -or
        $null -ne (Compare-Object -ReferenceObject $statusBefore -DifferenceObject $statusAfter)) {
        throw 'HEAD or repository status changed while the evidence gate was running.'
    }
}

function Get-RelevantProcesses {
    $names = @(
        'GraniteEdgeAI.ModelInspection.Worker',
        'GraniteEdgeAI.ModelInspection.ProtocolTestWorker',
        'IBM Granite with TurboQuant (Intel)',
        'testhost',
        'vstest.console')
    return @(Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $names -contains $_.ProcessName } |
        Sort-Object ProcessName, Id |
        Select-Object Id, ProcessName, StartTime)
}

function Assert-NoRelevantProcesses {
    param([Parameter(Mandatory)] [string] $EvidenceName)
    $processes = @(Get-RelevantProcesses)
    Write-JsonFile -Path (Join-Path $phaseRoot "$EvidenceName.json") -Value $processes
    if ($processes.Count -ne 0) {
        throw "Relevant app, worker, or test processes remain at $EvidenceName."
    }
}

function Test-RelevantWerMessage {
    param([AllowEmptyString()] [string] $Message)
    foreach ($target in $werTargets) {
        if ($Message.IndexOf($target, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }
    return $false
}

function Get-RelevantWerEvents {
    param([Parameter(Mandatory)] [DateTime] $StartTime)
    try {
        $events = @(Get-WinEvent -FilterHashtable @{
            LogName = 'Application'
            Id = 1000
            StartTime = $StartTime
        } -ErrorAction Stop)
    }
    catch {
        if ($_.FullyQualifiedErrorId -notlike 'NoMatchingEventsFound*') { throw }
        $events = @()
    }
    return @($events |
        Where-Object { Test-RelevantWerMessage -Message ([string]$_.Message) } |
        Sort-Object RecordId |
        Select-Object RecordId, TimeCreated, ProviderName, Id, Message)
}

function Assert-NoNewWerEvents {
    $events = @(Get-RelevantWerEvents -StartTime $startedUtc)
    Write-JsonFile -Path (Join-Path $phaseRoot 'wer-after.json') -Value $events
    if ($events.Count -ne 0) {
        throw 'A relevant Windows Error Reporting event was recorded during the gate.'
    }
}

function Save-CommandMetadata {
    Write-JsonFile `
        -Path (Join-Path $phaseRoot 'command-metadata.json') `
        -Value @($commandMetadata)
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [scriptblock] $Command)
    if (-not $executedCommands.Add($Name)) {
        throw "A gate command was invoked more than once: $Name"
    }
    $logPath = Join-Path $phaseRoot "$Name.log"
    $commandStarted = [DateTime]::UtcNow
    $previousErrorActionPreference = $ErrorActionPreference
    $commandError = $null
    $exitCode = 1
    try {
        $ErrorActionPreference = 'Continue'
        & $Command *>&1 | Tee-Object -FilePath $logPath
        $exitCode = $LASTEXITCODE
    }
    catch {
        $commandError = $_
        $_ | Out-String | Tee-Object -FilePath $logPath -Append
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    $commandMetadata.Add([ordered]@{
        name = $Name
        startedUtc = $commandStarted.ToString('O')
        completedUtc = [DateTime]::UtcNow.ToString('O')
        exitCode = $exitCode
        log = [IO.Path]::GetFileName($logPath)
    })
    Save-CommandMetadata
    if ($null -ne $commandError -or $exitCode -ne 0) { throw "Gate command failed: $Name ($exitCode)" }
}

function Assert-ZeroAdverseCounters {
    param([Parameter(Mandatory)] $Counters)
    foreach ($name in @(
            'failed', 'error', 'timeout', 'aborted', 'inconclusive',
            'notExecuted', 'notRunnable', 'disconnected', 'warning')) {
        $property = $Counters.PSObject.Properties[$name]
        if ($null -ne $property -and [int]$property.Value -ne 0) {
            throw "TRX adverse counter is nonzero: $name=$($property.Value)"
        }
    }
}

function Assert-TrxIdentityClosure {
    param(
        [Parameter(Mandatory)] [object[]] $Definitions,
        [Parameter(Mandatory)] [object[]] $Results,
        [Parameter(Mandatory)] [int] $ExpectedTotal)
    $definitionIds = @($Definitions | ForEach-Object { [string]$_.id })
    $resultIds = @($Results | ForEach-Object { [string]$_.testId })
    $uniqueDefinitionIds = @($definitionIds | Sort-Object -Unique)
    $uniqueResultIds = @($resultIds | Sort-Object -Unique)
    if ($definitions.Count -ne $ExpectedTotal -or
        $results.Count -ne $ExpectedTotal -or
        $uniqueDefinitionIds.Count -ne $ExpectedTotal -or
        $uniqueResultIds.Count -ne $ExpectedTotal -or
        @($definitionIds | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -ne 0 -or
        @($resultIds | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -ne 0 -or
        $null -ne (Compare-Object -ReferenceObject $uniqueDefinitionIds -DifferenceObject $uniqueResultIds) -or
        @($Results | Where-Object { [string]$_.outcome -cne 'Passed' }).Count -ne 0) {
        throw 'TRX definition/result identity closure drifted.'
    }
}

function Read-TrxEvidence {
    param([Parameter(Mandatory)] [string] $Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "TRX was not created: $Path"
    }
    [xml]$trx = Get-Content -LiteralPath $Path -Raw
    $counters = $trx.TestRun.ResultSummary.Counters
    Assert-ZeroAdverseCounters -Counters $counters
    $definitions = @($trx.TestRun.TestDefinitions.UnitTest)
    $results = @($trx.TestRun.Results.UnitTestResult)
    Assert-TrxIdentityClosure -Definitions $definitions -Results $results -ExpectedTotal ([int]$counters.total)
    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    $summary = [ordered]@{
        path = [IO.Path]::GetFileName($Path)
        sha256 = $hash
        total = [int]$counters.total
        executed = [int]$counters.executed
        passed = [int]$counters.passed
        definitions = $definitions.Count
        results = $results.Count
    }
    Write-JsonFile -Path "$Path.identity.json" -Value $summary
    return $trx
}

function Assert-TrxExactMap {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [int] $ExpectedTotal,
        [Parameter(Mandatory)] [Collections.IDictionary] $ExpectedMap)
    [xml]$trx = Read-TrxEvidence -Path $Path
    $counters = $trx.TestRun.ResultSummary.Counters
    if ([int]$counters.total -ne $ExpectedTotal -or
        [int]$counters.executed -ne $ExpectedTotal -or
        [int]$counters.passed -ne $ExpectedTotal) {
        throw "TRX is not exact all-pass: expected=$ExpectedTotal actual=$($counters.total)/$($counters.executed)/$($counters.passed)"
    }
    $definitions = @($trx.TestRun.TestDefinitions.UnitTest)
    $results = @($trx.TestRun.Results.UnitTestResult)
    $expectedSum = [int](($ExpectedMap.Values | Measure-Object -Sum).Sum)
    if ($expectedSum -ne $ExpectedTotal) {
        throw "Expected class map sums to $expectedSum instead of $ExpectedTotal."
    }
    $actualClasses = @($definitions | ForEach-Object { [string]$_.TestMethod.className } | Sort-Object -Unique)
    $expectedClasses = @($ExpectedMap.Keys | Sort-Object)
    if ($null -ne (Compare-Object -ReferenceObject $expectedClasses -DifferenceObject $actualClasses)) {
        throw 'TRX exact class set drifted.'
    }
    foreach ($entry in $ExpectedMap.GetEnumerator()) {
        $ids = @($definitions |
            Where-Object { [string]$_.TestMethod.className -ceq [string]$entry.Key } |
            ForEach-Object { [string]$_.id })
        $passed = @($results | Where-Object {
            [string]$_.testId -in $ids -and [string]$_.outcome -ceq 'Passed'
        })
        if ($ids.Count -ne [int]$entry.Value -or $passed.Count -ne [int]$entry.Value) {
            throw "TRX class map drifted: $($entry.Key)."
        }
    }
}

function Assert-TrxProtectedMap {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [int] $ExpectedTotal,
        [Parameter(Mandatory)] [Collections.IDictionary] $ProtectedMap)
    [xml]$trx = Read-TrxEvidence -Path $Path
    $counters = $trx.TestRun.ResultSummary.Counters
    if ([int]$counters.total -ne $ExpectedTotal -or
        [int]$counters.executed -ne $ExpectedTotal -or
        [int]$counters.passed -ne $ExpectedTotal) {
        throw 'Hosted Release TRX total or all-pass counters drifted.'
    }
    $definitions = @($trx.TestRun.TestDefinitions.UnitTest)
    $results = @($trx.TestRun.Results.UnitTestResult)
    foreach ($entry in $ProtectedMap.GetEnumerator()) {
        $ids = @($definitions |
            Where-Object { [string]$_.TestMethod.className -ceq [string]$entry.Key } |
            ForEach-Object { [string]$_.id })
        $passed = @($results | Where-Object {
            [string]$_.testId -in $ids -and [string]$_.outcome -ceq 'Passed'
        })
        if ($ids.Count -ne [int]$entry.Value -or $passed.Count -ne [int]$entry.Value) {
            throw "Hosted protected class drifted: $($entry.Key)."
        }
    }
}

function Find-VisualStudioTools {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
        throw 'vswhere was not found.'
    }
    $script:vstestPath = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
    $script:msbuildPath = & $vswhere -latest -products * -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    if (-not $script:vstestPath -or -not $script:msbuildPath) {
        throw 'Visual Studio VSTest/MSBuild tools were not found.'
    }
}

function Set-PackagedRecipe {
    param([Parameter(Mandatory)] [ValidateSet('Debug','Release')] [string] $Configuration)
    $script:currentConfiguration = $Configuration
    $script:packagedRecipe = Join-Path $repositoryRoot (
        "tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\$Configuration\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe")
    if (-not (Test-Path -LiteralPath $script:packagedRecipe -PathType Leaf)) {
        throw "Packaged test recipe is missing: $script:packagedRecipe"
    }
}

function Invoke-DebugBuild {
    param([Parameter(Mandatory)] [string] $EvidenceDirectory)
    Invoke-CheckedCommand -Name 'debug-app-build' -Command {
        & $script:msbuildPath $appProject /target:Restore,Build /property:Configuration=Debug /property:Platform=x64
    }
    Invoke-CheckedCommand -Name 'debug-test-build' -Command {
        dotnet build $testProject --configuration Debug --runtime win-x64 -p:Platform=x64
    }
    Set-PackagedRecipe -Configuration Debug
    Write-JsonFile -Path (Join-Path $EvidenceDirectory 'debug-binaries.json') -Value @{
        recipeSha256 = (Get-FileHash -LiteralPath $script:packagedRecipe -Algorithm SHA256).Hash
    }
}

function Invoke-ReleaseBuild {
    param([Parameter(Mandatory)] [string] $EvidenceDirectory)
    Invoke-CheckedCommand -Name 'release-app-restore' -Command {
        & $script:msbuildPath $appProject /target:Restore /property:Configuration=Release /property:Platform=x64 /property:RuntimeIdentifier=win-x64
    }
    Invoke-CheckedCommand -Name 'release-test-restore' -Command {
        dotnet restore $testProject --runtime win-x64 -p:Platform=x64
    }
    Invoke-CheckedCommand -Name 'release-app-build' -Command {
        & $script:msbuildPath $appProject /target:Build /maxCpuCount /verbosity:minimal /property:Configuration=Release /property:Platform=x64 /property:RuntimeIdentifier=win-x64 /property:PublishProfile= /property:PublishTrimmed=false /property:PublishReadyToRun=false /property:AppxPackageSigningEnabled=false /property:GenerateAppxPackageOnBuild=false
    }
    Invoke-CheckedCommand -Name 'release-test-build' -Command {
        dotnet build $testProject --configuration Release --no-restore --runtime win-x64 -p:Platform=x64
    }
    Set-PackagedRecipe -Configuration Release
    Write-JsonFile -Path (Join-Path $EvidenceDirectory 'release-binaries.json') -Value @{
        recipeSha256 = (Get-FileHash -LiteralPath $script:packagedRecipe -Algorithm SHA256).Hash
    }
}

function Invoke-PackagedTests {
    param(
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [string] $Filter)
    $resultDirectory = Join-Path $phaseRoot $Name
    $null = New-Item -ItemType Directory -Path $resultDirectory
    $trxName = "$Name.trx"
    Invoke-CheckedCommand -Name "packaged-$Name" -Command {
        & $script:vstestPath $script:packagedRecipe /Platform:x64 "/Logger:trx;LogFileName=$trxName" "/ResultsDirectory:$resultDirectory" "/TestCaseFilter:$Filter"
    }
    $trxPath = Join-Path $resultDirectory $trxName
    if ($Name -ceq 'HostedRelease') {
        Assert-TrxProtectedMap -Path $trxPath -ExpectedTotal $ExpectedTotals[$Name] -ProtectedMap $ExpectedTestMaps.ReleaseProtected
    }
    else {
        Assert-TrxExactMap -Path $trxPath -ExpectedTotal $ExpectedTotals[$Name] -ExpectedMap $ExpectedTestMaps[$Name]
    }
}

function Invoke-ReleaseIsolation {
    param([Parameter(Mandatory)] [string] $EvidenceDirectory)
    $isolationReceiptRoot = Join-Path $EvidenceDirectory 'ReleaseIsolation'
    $null = New-Item -ItemType Directory -Path $isolationReceiptRoot
    $evidenceName =
        "progress-polish-$([Guid]::NewGuid().ToString('N')).json"
    $evidencePath = Join-Path $releaseIsolationEvidenceRoot $evidenceName
    if (Test-Path -LiteralPath $evidencePath) {
        throw 'The unique Release-isolation evidence path already exists.'
    }
    Invoke-CheckedCommand -Name 'release-isolation' -Command {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $releaseIsolationScript -EvidencePath $evidencePath
    }
    $evidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json
    if ($evidence.status -cne 'passed' -or $evidence.sourceCommit -cne $headBefore) {
        throw 'Release-isolation evidence did not pass against the frozen source commit.'
    }
    Write-JsonFile `
        -Path (Join-Path $isolationReceiptRoot 'release-isolation-receipt.json') `
        -Value ([ordered]@{
            externalEvidencePath = $evidencePath
            externalEvidenceSha256 =
                (Get-FileHash -LiteralPath $evidencePath -Algorithm SHA256).Hash
            status = $evidence.status
            sourceCommit = $evidence.sourceCommit
        })
}

function Invoke-ContractsGate {
    param([Parameter(Mandatory)] [string] $EvidenceDirectory)
    $resultDirectory = Join-Path $EvidenceDirectory 'Contracts'
    $null = New-Item -ItemType Directory -Path $resultDirectory
    $trxPath = Join-Path $resultDirectory 'Contracts.trx'
    Invoke-CheckedCommand -Name 'contracts' -Command {
        dotnet test $contractsProject --configuration Release --minimum-expected-tests 357 --results-directory $resultDirectory --report-trx --report-trx-filename 'Contracts.trx'
    }
    Assert-TrxExactMap -Path $trxPath -ExpectedTotal $ExpectedTotals.Contracts -ExpectedMap $ExpectedTestMaps.Contracts
}

function Invoke-CleanupGate {
    param([Parameter(Mandatory)] [string] $EvidenceDirectory)
    Invoke-CheckedCommand -Name 'cleanup-inventory' -Command {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $cleanupScript
    }
}

function Invoke-DiffGate {
    param([Parameter(Mandatory)] [string] $EvidenceDirectory)
    Invoke-CheckedCommand -Name 'git-diff-cached-check' -Command { git -C $repositoryRoot diff --cached --check }
    Invoke-CheckedCommand -Name 'git-diff-cached-exit-code' -Command { git -C $repositoryRoot diff --cached --exit-code }
    Invoke-CheckedCommand -Name 'git-diff-check' -Command { git -C $repositoryRoot diff --check }
    Invoke-CheckedCommand -Name 'git-diff-exit-code' -Command { git -C $repositoryRoot diff --exit-code }
}

Find-VisualStudioTools
$sourceFreezeBefore = @(Get-SourceFreeze)
$sourceFreezeBefore | Set-Content -LiteralPath (Join-Path $phaseRoot 'source-freeze-before.sha256') -Encoding utf8
Write-JsonFile -Path (Join-Path $phaseRoot 'run-freeze.json') -Value @{
    phase = $Phase
    sourceCommit = $headBefore
    startedUtc = $startedUtc.ToString('O')
    status = @($statusBefore)
}
Write-JsonFile -Path (Join-Path $phaseRoot 'wer-before.json') -Value @()
Assert-NoRelevantProcesses -EvidenceName 'process-before'

$phaseFailure = $null
try {
    switch ($Phase) {
        'RuntimeWorker' {
            $runtimeResults = Join-Path $phaseRoot 'Runtime'
            $workerResults = Join-Path $phaseRoot 'Worker'
            $null = New-Item -ItemType Directory -Path $runtimeResults
            $null = New-Item -ItemType Directory -Path $workerResults
            Invoke-CheckedCommand -Name 'runtime-project' -Command {
                dotnet test $runtimeProject --configuration Release --minimum-expected-tests $ExpectedTotals.Runtime --results-directory $runtimeResults --report-trx --report-trx-filename 'Runtime.trx'
            }
            Assert-TrxExactMap -Path (Join-Path $runtimeResults 'Runtime.trx') -ExpectedTotal $ExpectedTotals.Runtime -ExpectedMap $ExpectedTestMaps.Runtime
            Invoke-CheckedCommand -Name 'worker-project' -Command {
                dotnet test $workerProject --configuration Release --minimum-expected-tests $ExpectedTotals.Worker --results-directory $workerResults --report-trx --report-trx-filename 'Worker.trx'
            }
            Assert-TrxExactMap -Path (Join-Path $workerResults 'Worker.trx') -ExpectedTotal $ExpectedTotals.Worker -ExpectedMap $ExpectedTestMaps.Worker
            if ([int]$ExpectedTestMaps.Worker['GraniteEdgeAI.ModelInspection.Worker.Tests.LlamaSharpInspectionEngineTests'] -ne 67) {
                throw 'Expected exactly 67 passing worker engine executions.'
            }
        }
        'Debug' {
            Invoke-DebugBuild -EvidenceDirectory $phaseRoot
            Invoke-PackagedTests -Name 'InteractionLifetime' -Filter $interactionLifetimeFilter
            Invoke-PackagedTests -Name 'FixtureCategory' -Filter $fixtureCategoryFilter
            Invoke-PackagedTests -Name 'FocusedPolish' -Filter $polishFilter
        }
        'Release' {
            Invoke-ReleaseBuild -EvidenceDirectory $phaseRoot
            Invoke-PackagedTests -Name 'HostedRelease' -Filter $hostedReleaseFilter
            Invoke-PackagedTests -Name 'N001' -Filter $n001FullyQualifiedName
            Invoke-ReleaseIsolation -EvidenceDirectory $phaseRoot
        }
        'FinalSource' {
            Invoke-ContractsGate -EvidenceDirectory $phaseRoot
            Invoke-CleanupGate -EvidenceDirectory $phaseRoot
            Invoke-DiffGate -EvidenceDirectory $phaseRoot
            Invoke-ReleaseIsolation -EvidenceDirectory $phaseRoot
        }
    }
}
catch {
    $phaseFailure = $_
}
finally {
    try {
        Save-CommandMetadata
        Assert-NoRelevantProcesses -EvidenceName 'process-after'
        Assert-NoNewWerEvents
        Assert-SourceFreeze -Before $sourceFreezeBefore
    }
    catch {
        if ($null -eq $phaseFailure) { $phaseFailure = $_ }
    }
}

if ($null -ne $phaseFailure) {
    throw $phaseFailure
}

Write-JsonFile -Path (Join-Path $phaseRoot 'phase-result.json') -Value @{
    phase = $Phase
    status = 'passed'
    sourceCommit = $headBefore
    completedUtc = [DateTime]::UtcNow.ToString('O')
    commands = @($executedCommands | Sort-Object)
}
