$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$sourceSha = '401259594fe5bca365bb9bfd2e4619f41bfc574d'
$phaseZeroSha = '8337d5861812b0f4a3867b1e2fb5889bff4e76fc'
$sourceRunId = '31234116989'
$sourceJobId = '93043440880'
$unitArtifactId = '9014868847'
$unitArtifactDigest = '9df702296add3199d8befd930bc80951745d324288831cafbaa423527dab4bf0'
$gate2ArtifactId = '9014868651'
$gate2ArtifactDigest = 'cca1de6c589abf2b526c7f6ac6e9cb10ded07686e4e07f2b8e51afa0cf65cf53'
$closureRunId = $env:GITHUB_RUN_ID
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$nl = "`n"

$sourceListPath = 'docs/reviews/model-inspection-cleanup-source-files.txt'
$inventoryPath = 'docs/reviews/model-inspection-cleanup-inventory.md'
$evidencePath = 'docs/testing/evidence/2026-08-07-model-inspection-cleanup-phase-1.md'
$evidenceIndexPath = 'docs/testing/evidence/README.md'

function Write-LfFile {
    param([string] $Path, [string] $Content)
    [System.IO.File]::WriteAllText($Path, ($Content -replace "`r`n", "`n"), $utf8NoBom)
}

function Sort-Ordinal {
    param([string[]] $Values)
    [string[]] $copy = @($Values)
    [Array]::Sort($copy, [System.StringComparer]::Ordinal)
    return $copy
}

function Get-Subsystem {
    param([string] $Path)

    switch -Regex ($Path) {
        '^\.github/workflows/' { return 'GitHub Actions' }
        '^IBM Granite with TurboQuant \(Intel\)/Features/ModelImport/' { return 'Model Import handoff' }
        '^IBM Granite with TurboQuant \(Intel\)/Features/Onboarding/' { return 'Onboarding navigation' }
        '^IBM Granite with TurboQuant \(Intel\)/Features/ModelInspection/Contracts/' { return 'WinUI application contracts' }
        '^IBM Granite with TurboQuant \(Intel\)/Features/ModelInspection/Controls/' { return 'WinUI Model Inspection controls' }
        '^IBM Granite with TurboQuant \(Intel\)/Features/ModelInspection/Models/' { return 'WinUI presentation models' }
        '^IBM Granite with TurboQuant \(Intel\)/Features/ModelInspection/Presentation/' { return 'WinUI presentation factory' }
        '^IBM Granite with TurboQuant \(Intel\)/Features/ModelInspection/' { return 'WinUI Model Inspection' }
        '^shared/GraniteEdgeAI\.ModelInspection\.Contracts/' { return 'worker protocol contracts' }
        '^shared/GraniteEdgeAI\.ModelInspection\.Transport/' { return 'worker transport' }
        '^infrastructure/GraniteEdgeAI\.ModelInspection\.WorkerClient/' { return 'worker client and process containment' }
        '^workers/GraniteEdgeAI\.ModelInspection\.Worker/' { return 'worker host' }
        '^tools/ModelInspection\.LlamaSharpSpike' { return 'LLamaSharp feasibility spike' }
        '^tests/ContractTests/' { return 'contract and architecture tests' }
        '^tests/UnitTests/GraniteEdgeAI\.ModelInspection\.Transport\.Tests/' { return 'transport tests' }
        '^tests/UnitTests/GraniteEdgeAI\.ModelInspection\.Worker\.Tests/' { return 'worker-host tests' }
        '^tests/UnitTests/GraniteEdgeAI\.ModelInspection\.WorkerClient\.Tests/' { return 'worker-client tests' }
        '^tests/IntegrationTests/' { return 'worker-process integration tests' }
        '^tests/ProcessFixtures/' { return 'worker-process test fixture' }
        '^tests/UnitTests/GraniteEdgeAI\.UnitTests/' { return 'packaged WinUI tests' }
        '^scripts/model-inspection/' { return 'Model Inspection verification tooling' }
        '^docs/reviews/' { return 'cleanup review governance' }
        '^docs/testing/evidence/' { return 'verification evidence' }
        '^docs/testing/' { return 'verification documentation' }
        '^docs/superpowers/' { return 'engineering plans and specifications' }
        '^(shared|infrastructure|workers|tools)/README\.md$' { return 'subsystem boundary documentation' }
        '^IBM Granite with TurboQuant \(Intel\)' { return 'connected application infrastructure' }
        default { return 'connected Model Inspection scope' }
    }
}

function Get-Risk {
    param([string] $Path, [string] $Subsystem)

    if ($Path -match 'WorkerClient|WorkerProcess|ProcessFixtures|shared/GraniteEdgeAI\.ModelInspection|workers/GraniteEdgeAI\.ModelInspection') {
        return 'critical'
    }
    if ($Subsystem -eq 'GitHub Actions' -or $Subsystem -eq 'Model Inspection verification tooling' -or $Subsystem -eq 'WinUI application contracts') {
        return 'high'
    }
    if ($Path.EndsWith('.md', [StringComparison]::OrdinalIgnoreCase) -or $Path.EndsWith('.txt', [StringComparison]::OrdinalIgnoreCase)) {
        return 'low'
    }
    return 'medium'
}

function Get-Responsibility {
    param([string] $Path, [string] $Subsystem)

    $name = [System.IO.Path]::GetFileName($Path)
    switch -Regex ($Path) {
        '^\.github/workflows/build-and-test\.yml$' { return 'runs the permanent Windows build, Gate 2, packaged WinUI, process-cleanup, privacy-scan, and artifact gate' }
        '^\.github/workflows/' { return "runs the focused CI boundary represented by $name" }
        '^scripts/model-inspection/' { return "automates Model Inspection verification through $name" }
        '^tests/.+\.cs$' { return "verifies $Subsystem through $name" }
        '^tests/.+\.csproj$' { return "defines the build and dependency boundary for $Subsystem" }
        '^tests/ProcessFixtures/' { return "provides the isolated process fixture $name" }
        '^shared/GraniteEdgeAI\.ModelInspection\.Contracts/.+\.cs$' { return "defines the shared protocol contract $name" }
        '^shared/GraniteEdgeAI\.ModelInspection\.Transport/.+\.cs$' { return "implements the bounded protocol transport element $name" }
        '^infrastructure/GraniteEdgeAI\.ModelInspection\.WorkerClient/.+\.cs$' { return "implements the protected worker-client/process-boundary element $name" }
        '^workers/GraniteEdgeAI\.ModelInspection\.Worker/.+\.cs$' { return "implements the protected worker-host element $name" }
        '^tools/ModelInspection\.LlamaSharpSpike/.+\.cs$' { return "implements or verifies LLamaSharp feasibility through $name" }
        '^IBM Granite with TurboQuant \(Intel\)/Features/ModelInspection/Contracts/.+\.cs$' { return "defines the application-owned Model Inspection contract $name" }
        '^IBM Granite with TurboQuant \(Intel\)/Features/.+\.xaml$' { return "defines the XAML presentation or composition for $name" }
        '^IBM Granite with TurboQuant \(Intel\)/Features/.+\.cs$' { return "implements the feature behavior represented by $name" }
        '\.csproj$' { return "defines project build configuration for $Subsystem" }
        '\.slnx$' { return 'defines the solution-level project graph used by the Model Inspection build' }
        '\.(md|txt)$' { return "documents or records the $Subsystem boundary in $name" }
        default { return "supports $Subsystem through $name" }
    }
}

function Get-UnchangedFinding {
    param([string] $Subsystem)

    switch ($Subsystem) {
        'worker protocol contracts' { return 'Gate 2 protocol contract reviewed; Phase 1 required no change and serialized names, enum meaning, cancellation meaning, and failure semantics remain frozen' }
        'worker transport' { return 'Gate 2 bounded transport reviewed; Phase 1 required no change to framing, sequencing, limits, or cancellation semantics' }
        'worker client and process containment' { return 'Gate 2 process boundary reviewed; Phase 1 required no change to trusted resolution, handle inheritance, containment, environment policy, timeout, or cleanup semantics' }
        'worker host' { return 'Gate 2 worker shell reviewed; Phase 1 required no runtime change and the controlled unavailable engine remains deliberate' }
        'LLamaSharp feasibility spike' { return 'Feasibility code and retained Tier 1/Tier 2 evidence were outside the WinUI cleanup changes; no Phase 1 modification was required' }
        'Model Import handoff' { return 'Model Import handoff was reviewed; request validation and event ownership were already cohesive and required no production change' }
        'Onboarding navigation' { return 'Onboarding remains the owner of Frame navigation; no production navigation change was required unless explicitly recorded in a focused test row' }
        'GitHub Actions' { return 'Workflow boundary reviewed; no Phase 1 behavior change was required unless explicitly recorded in a focused row' }
        { $_ -like '*tests' } { return 'Retained regression coverage was reviewed and remained valid for the Phase 1 boundary' }
        default { return 'Phase 1 review found no cleanup defect requiring a change in this file' }
    }
}

function Get-PreservedBehavior {
    param([string] $Subsystem)

    switch ($Subsystem) {
        'worker protocol contracts' { return 'wire protocol version, JSON names, enums, diagnostics, cancellation, and terminal-result meaning are unchanged' }
        'worker transport' { return 'bounded stream framing, ordering, limits, and cancellation behavior are unchanged' }
        'worker client and process containment' { return 'trusted executable resolution, process containment, allowlisted environment, timeout, and cleanup behavior are unchanged' }
        'worker host' { return 'worker handshake and controlled unavailable-engine behavior are unchanged' }
        'LLamaSharp feasibility spike' { return 'Tier 1/Tier 2 feasibility behavior and evidence remain unchanged' }
        'Model Import handoff' { return 'validated request construction, stale-result protection, continue gating, and event handoff are unchanged' }
        'Onboarding navigation' { return 'shell-owned navigation, event subscription, stage timing, and exact request forwarding are unchanged' }
        'WinUI application contracts' { return 'application contract constructors, properties, validation, and exception meaning are unchanged' }
        'WinUI Model Inspection controls' { return 'approved layout, text, accessibility, and semantic state mapping are unchanged except for explicitly recorded defensive checks' }
        'WinUI presentation models' { return 'presentation values and binding semantics are unchanged except for explicitly recorded hidden-state isolation' }
        'WinUI presentation factory' { return 'five-stage initial wording, status, connector geometry, and automation meaning are unchanged' }
        'packaged WinUI tests' { return 'packaged application regression expectations remain aligned with the approved Phase 1 behavior' }
        default { return 'the existing subsystem contract and observable behavior are unchanged' }
    }
}

function Get-TestEvidence {
    param([string] $Path, [string] $Subsystem)

    switch -Regex ($Path) {
        '^tools/ModelInspection\.LlamaSharpSpike' { return 'retained Tier 1 hosted and Tier 2 trusted real-model evidence; no Phase 1 source diff' }
        'GraniteEdgeAI\.ModelInspection\.Transport|ProtocolTestWorker' { return 'transport layer 20/20 passed in source run 31234116989' }
        'GraniteEdgeAI\.ModelInspection\.WorkerClient' { return 'WorkerClient layer 86/86 passed in source run 31234116989' }
        'workers/GraniteEdgeAI\.ModelInspection\.Worker|Worker\.Tests' { return 'worker-host layer 4/4 passed in source run 31234116989' }
        'tests/IntegrationTests|WorkerProcess' { return 'real-process layer 27/27 passed in source run 31234116989' }
        'shared/GraniteEdgeAI\.ModelInspection\.Contracts|tests/ContractTests' { return 'contract layer 82/82 passed in source run 31234116989' }
        'Features/ModelImport|Features/ModelInspection|Features/Onboarding|GraniteEdgeAI\.UnitTests' { return 'packaged WinUI layer 217/217 passed in source run 31234116989' }
        '^\.github/workflows/build-and-test\.yml$' { return 'all six permanent layers passed: 436/436 executed and passed' }
        '^scripts/model-inspection/|^docs/reviews/' { return "cleanup inventory contract tests pass in Task 9 closure run $closureRunId; permanent source run 31234116989 is green" }
        default { return 'permanent source run 31234116989 passed the applicable build/test boundary' }
    }
}

function Get-Verification {
    param([string] $Path, [string] $Subsystem)

    if ($Subsystem -eq 'LLamaSharp feasibility spike') {
        return 'unchanged from retained Tier 1/Tier 2 verified heads; Phase 1 source SHA 401259594fe5bca365bb9bfd2e4619f41bfc574d has no feasibility-source diff'
    }
    return "source SHA $sourceSha; permanent run $sourceRunId; job $sourceJobId; Task 9 ledger verifier run $closureRunId"
}

function Get-Deferred {
    param([string] $Subsystem)

    switch ($Subsystem) {
        'worker host' { return 'real LLamaSharp inspection engine remains a later gate; Phase 1 intentionally preserves the unavailable-engine boundary' }
        'worker client and process containment' { return 'none in Phase 1; application mapping/classification remains outside this protected process boundary' }
        'LLamaSharp feasibility spike' { return 'production integration remains later-gate work; feasibility evidence is not a production-runtime claim' }
        default { return 'none' }
    }
}

function New-Metadata {
    param(
        [string] $Subsystem,
        [string] $Responsibility,
        [string] $Risk,
        [string] $Findings,
        [string] $Changes,
        [string] $Behavior,
        [string] $Tests,
        [string] $Verification,
        [string] $Deferred
    )

    return [pscustomobject]@{
        Subsystem = $Subsystem
        Responsibility = $Responsibility
        Risk = $Risk
        Status = 'reviewed'
        Findings = $Findings
        Changes = $Changes
        Behavior = $Behavior
        Tests = $Tests
        Verification = $Verification
        Deferred = $Deferred
    }
}

$special = @{}
function Add-Special {
    param(
        [string] $Path,
        [string] $Subsystem,
        [string] $Responsibility,
        [string] $Risk,
        [string] $Findings,
        [string] $Changes,
        [string] $Behavior,
        [string] $Tests,
        [string] $Verification,
        [string] $Deferred = 'none'
    )
    $special[$Path] = New-Metadata $Subsystem $Responsibility $Risk $Findings $Changes $Behavior $Tests $Verification $Deferred
}

$sourceEvidence = "source SHA $sourceSha; permanent run $sourceRunId; job $sourceJobId"

Add-Special 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Contracts/README.md' 'WinUI application contracts' 'documents application-contract ownership and navigation handoff' 'low' 'Documentation still described Gate 2 as future and referenced the removed SelectedModelPath alias' 'updated Phase 1 boundary facts and documented Request as the single authoritative navigation state' 'contract API and navigation behavior are unchanged' 'contract 82/82 plus packaged WinUI 217/217 passed' "$sourceEvidence; Task 8 docs commit 53b9ef86d7e504e258f6ddfb57d13b4d91977824" 'application mapping and runtime orchestration remain later gates'
Add-Special 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml.cs' 'WinUI Model Inspection controls' 'applies action-card visibility, required visual states, and compiled-binding refresh' 'medium' 'The control already failed fast on missing states; touched comments narrated obvious operations' 'kept the existing guard, simplified touched comments, and made local types explicit' 'InspectingState/ResultState mapping and Bindings.Update behavior are unchanged' 'visual-state guard tests and packaged WinUI 217/217 passed' $sourceEvidence
Add-Special 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml' 'WinUI Model Inspection controls' 'defines progress, findings, report, and disclosure templates' 'medium' 'Compiler reported 19 WMC1506 warnings from OneWay modes on immutable snapshot bindings' 'aligned immutable x:Bind expressions with snapshot semantics while keeping IsExpanded TwoWay' 'layout, text, accessibility, and disclosure behavior are unchanged' 'application build and packaged WinUI 217/217 passed; final source run has no WMC1506 annotation' $sourceEvidence 'fixed ARGB status brushes remain deferred because changing them would alter theme/high-contrast rendering'
Add-Special 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml.cs' 'WinUI Model Inspection controls' 'owns content-card presentation, visibility, disclosure state, and status helpers' 'medium' 'A shared mutable hidden presentation could leak IsExpanded state between controls' 'installs an independent hidden presentation after XAML initialization and keeps null-safe fallback behavior' 'hidden/status mapping and user-visible behavior are unchanged; hidden instances no longer share mutable disclosure state' 'InspectionContentCardPresentationTests plus packaged WinUI 217/217 passed' $sourceEvidence 'fixed ARGB status brushes remain deferred for a dedicated UX/accessibility decision'
Add-Special 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentTemplateSelector.cs' 'WinUI Model Inspection controls' 'selects progress or findings templates across item and container selector entry routes' 'medium' 'Item/container and ContentControl/ContentPresenter extraction logic was duplicated' 'centralized content extraction in one small helper without changing route semantics' 'bootstrap/null selects progress; Progress selects progress; non-progress selects findings; missing templates still fail clearly' 'eight selector tests plus packaged WinUI 217/217 passed' $sourceEvidence
Add-Special 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml' 'WinUI Model Inspection controls' 'defines compact/detailed model presentation and inspection-details disclosure' 'medium' 'Compiler reported 27 WMC1506 warnings on immutable forwarding/check bindings' 'aligned immutable binding modes while keeping IsInspectionDetailsExpanded TwoWay' 'compact/detailed geometry, text, accessibility, and disclosure behavior are unchanged' 'application build and packaged WinUI 217/217 passed; final source run has no WMC1506 annotation' $sourceEvidence 'fixed ARGB check brushes remain deferred because changing them would alter approved theme behavior'
Add-Special 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml.cs' 'WinUI Model Inspection controls' 'maps presentation snapshots to compact/detailed state, badge/check formatting, and local expansion state' 'medium' 'VisualStateManager.GoToState return value was ignored, allowing XAML/code state drift to fail silently' 'added a local fail-fast InvalidOperationException guard and cleaned touched comments/types' 'state names, badge/check mapping, expansion reset, and Bindings.Update behavior are unchanged' 'visual-state guard tests; red run 31232156250 then green run 31232612493; packaged WinUI 217/217 passed in source run' $sourceEvidence 'fixed ARGB check-brush theming remains deferred'
Add-Special 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml.cs' 'WinUI Model Inspection controls' 'maps outcome presentation to visibility and semantic tone visual state' 'medium' 'VisualStateManager.GoToState return value was ignored for required tone states' 'added a local fail-fast InvalidOperationException guard and cleaned touched narration' 'hidden visibility and exact tone-to-state mapping are unchanged' 'visual-state guard tests; red run 31232156250 then green run 31232612493; packaged WinUI 217/217 passed in source run' $sourceEvidence
Add-Special 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/README.md' 'WinUI Model Inspection controls' 'documents reusable control contracts, tests, and known limitations' 'low' 'Selector test path and visual-state guard documentation were stale' 'updated test ownership/path and fail-fast control facts' 'production control behavior is unchanged' 'Task 8 documentation verifier and permanent source run passed' "$sourceEvidence; Task 8 commit 53b9ef86d7e504e258f6ddfb57d13b4d91977824" 'fixed-brush theme debt remains explicitly deferred'
Add-Special 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs' 'WinUI Model Inspection' 'captures the immutable navigation request and applies initial presentation after Loaded' 'medium' 'SelectedModelPath duplicated Request state and initial presentation re-parsed filename/format facts already validated by the request' 'removed the redundant alias, passed the request into initial state, and used Request.FileName plus QuickScan.Format' 'exact request identity, Loaded re-entry guard, initial UI values, and absence of runtime/process logic are preserved' 'page, onboarding, and Model Import navigation tests plus packaged WinUI 217/217 passed' $sourceEvidence 'runtime execution remains a later gate'
Add-Special 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionContentCardPresentation.cs' 'WinUI presentation models' 'represents content-card mode, text, disclosure state, and visibility-safe defaults' 'medium' 'Static Hidden was a shared mutable instance because IsExpanded is mutable' 'changed Hidden to return a fresh presentation instance' 'all hidden default values are unchanged while mutable disclosure state is isolated per consumer' 'InspectionContentCardPresentationTests plus packaged WinUI 217/217 passed' $sourceEvidence
Add-Special 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/README.md' 'WinUI presentation models' 'documents presentation snapshots, defaults, and model-level test ownership' 'low' 'README still warned about a shared hidden singleton after that issue was fixed and linked the selector test to the old folder' 'documented fresh hidden snapshots/independent control state and corrected test links' 'no production presentation behavior changed by the documentation update' 'Task 8 documentation verifier and permanent source run passed' "$sourceEvidence; Task 8 commit 53b9ef86d7e504e258f6ddfb57d13b4d91977824" 'runtime presentation factories remain later-gate work'
Add-Special 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InitialInspectionProgressPresentationFactory.cs' 'WinUI presentation factory' 'builds the fixed five-stage initial inspection tracker snapshot' 'medium' 'Helper arguments carried correlated/derived status, detail, and automation data that could drift apart' 'centralized derivation of status text, detail visibility, and automation name from stage state' 'five titles, statuses, connector geometry, detail visibility, summary, and accessibility meaning are unchanged' 'InitialInspectionProgressPresentationTests plus packaged WinUI 217/217 passed' $sourceEvidence 'real backend-driven stage progression remains later-gate work'
Add-Special 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/README.md' 'WinUI presentation factory' 'documents initial presentation ownership and characterization coverage' 'low' 'README branch/coverage facts were stale after Phase 1 characterization work' 'updated current branch and explicit detail/automation characterization facts' 'production presentation behavior is unchanged' 'Task 8 documentation verifier and permanent source run passed' "$sourceEvidence; Task 8 commit 53b9ef86d7e504e258f6ddfb57d13b4d91977824"
Add-Special 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md' 'WinUI Model Inspection' 'documents the feature boundary, implemented gates, non-claims, and next production work' 'low' 'README described the protected Gate 2 worker/process boundary as future and referenced SelectedModelPath' 'aligned documented implementation state with Gate 2 and current Request-only page state' 'no protocol, security, runtime, navigation, or visible UI behavior changed' 'Task 8 documentation verifier and permanent source run passed' "$sourceEvidence; Task 8 commit 53b9ef86d7e504e258f6ddfb57d13b4d91977824" 'real LLamaSharp engine, application mapping/classifier/service/ViewModel, fixed-brush theming, and visible copy typo remain later/separately approved work'
Add-Special 'docs/reviews/model-inspection-cleanup-inventory.md' 'cleanup review governance' 'records one review disposition and evidence trail for every file in the cleanup scope' 'high' 'Phase 0/working ledger still contained generic pending rows after Tasks 1-8' 'Task 9 replaces all pending/generic dispositions with closed subsystem-specific review evidence and adds the Phase 1 evidence row' 'inventory remains one-to-one and ordinally aligned with the source list' 'CleanupInventoryContractTests pass in closure run; final row/source count asserted at 372' "Task 9 closure run $closureRunId plus $sourceEvidence"
Add-Special 'docs/reviews/model-inspection-cleanup-source-files.txt' 'cleanup review governance' 'defines the sorted unique file boundary for the cleanup audit' 'high' 'Phase 1 added a final evidence file that must become part of the permanent audited scope' 'adds the Phase 1 evidence path and regenerates ordinal ordering' 'complete scope stays generated-output-free and each path appears exactly once' 'CleanupInventoryContractTests pass in closure run; final source count asserted at 372' "Task 9 closure run $closureRunId plus $sourceEvidence"
Add-Special 'docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-1-winui-contracts.md' 'engineering plans and specifications' 'records the approved Phase 1 audit findings, constraints, task sequence, tests, and closure gate' 'low' 'Plan was added to make the cleanup executable and auditable before implementation' 'added the committed Phase 1 implementation plan; no production behavior in this document' 'global constraints freeze visible UI, protocol/security meaning, and Gate 2 runtime boundary' 'all planned focused tests and permanent source workflow are green' $sourceEvidence 'later phases remain outside this Phase 1 closure'
Add-Special 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentTemplateSelectorTests.cs' 'packaged WinUI tests' 'characterizes selector bootstrap, item, wrapper, and container routes' 'medium' 'Test ownership was misplaced under Onboarding and three ContentPresenter/ContentControl routes lacked characterization' 'moved the test to ModelInspection/Controls and added the three missing wrapper/container cases' 'selector semantics are characterized without changing production outcomes' 'eight selector tests pass; packaged WinUI 217/217 passed' $sourceEvidence
Add-Special 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionVisualStateGuardTests.cs' 'packaged WinUI tests' 'proves required ActionCard, ModelCard, and OutcomeCard visual states fail clearly when XAML/code names drift' 'medium' 'ModelCard and OutcomeCard silently accepted missing required states before Task 7' 'added three focused UI-thread regression tests before the production guards' 'normal state application remains unchanged; only missing-state drift now fails fast consistently' 'red run 31232156250 had 215/217 passed with the two intended failures; green run 31232612493 and source run both pass 217/217' $sourceEvidence
Add-Special 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/InitialInspectionProgressPresentationTests.cs' 'packaged WinUI tests' 'characterizes the initial five-stage progress snapshot including detail visibility and automation names' 'medium' 'Detail visibility/default and accessibility-name behavior needed explicit characterization before factory simplification' 'extended characterization while preserving all approved strings and geometry' 'five-stage initial UX remains unchanged' 'focused initial-progress tests and packaged WinUI 217/217 passed' $sourceEvidence
Add-Special 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs' 'packaged WinUI tests' 'verifies ModelInspectionPage accepts and retains the exact navigation request and initializes from validated request facts' 'medium' 'Test depended on the redundant SelectedModelPath alias' 'removed alias assertion and retained exact Request/source-of-truth assertions' 'navigation request identity and initial presentation semantics are unchanged' 'page navigation tests and packaged WinUI 217/217 passed' $sourceEvidence
Add-Special 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Models/InspectionContentCardPresentationTests.cs' 'packaged WinUI tests' 'proves hidden presentation defaults are fresh and independent across content-card controls' 'medium' 'Shared mutable hidden-state risk lacked direct regression coverage' 'added focused fresh-instance and independent-control characterization' 'hidden default values remain unchanged while mutable IsExpanded state is isolated' 'focused presentation tests and packaged WinUI 217/217 passed' $sourceEvidence
Add-Special 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingModelInspectionNavigationTests.cs' 'packaged WinUI tests' 'verifies onboarding shell forwards the exact ModelInspectionRequest and advances stage only after navigation' 'medium' 'Test asserted the removed page alias in addition to the real request contract' 'removed alias dependence and kept the exact-request handoff assertion' 'shell ownership, request identity, event wiring, and stage timing are unchanged' 'onboarding navigation tests and packaged WinUI 217/217 passed' $sourceEvidence
Add-Special $evidenceIndexPath 'verification evidence' 'indexes retained runtime and cleanup verification records' 'low' 'Phase 1 closure evidence was not yet linked' 'adds the Phase 1 closure evidence entry' 'earlier evidence records remain unchanged and independently address their original gates' 'Task 9 closure verifier plus permanent source run are green' "Task 9 closure run $closureRunId plus $sourceEvidence"
Add-Special $evidencePath 'verification evidence' 'records exact Phase 1 source-build-test-artifact and non-claim evidence' 'high' 'Task 9 requires a durable exact-source evidence record before declaring Phase 1 complete' 'adds source SHA/run/job, six-layer test counts, warning annotations, artifact digests, process/privacy results, counts, non-claims, and deferrals' 'this is evidence only; it changes no production behavior' 'evidence facts are derived from permanent run 31234116989 and independently parsed retained TRX artifacts' "Task 9 closure run $closureRunId plus $sourceEvidence" 'final closure-tree CI is executed after this evidence commit as a separate exact-head gate'

$phaseChangedPaths = @(
    'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Contracts/README.md',
    'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml.cs',
    'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml',
    'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml.cs',
    'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentTemplateSelector.cs',
    'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml',
    'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml.cs',
    'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml.cs',
    'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/README.md',
    'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs',
    'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionContentCardPresentation.cs',
    'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/README.md',
    'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InitialInspectionProgressPresentationFactory.cs',
    'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/README.md',
    'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md',
    'docs/reviews/model-inspection-cleanup-inventory.md',
    'docs/reviews/model-inspection-cleanup-source-files.txt',
    'docs/superpowers/plans/2026-08-06-model-inspection-cleanup-phase-1-winui-contracts.md',
    'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentTemplateSelectorTests.cs',
    'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionVisualStateGuardTests.cs',
    'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/InitialInspectionProgressPresentationTests.cs',
    'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs',
    'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Models/InspectionContentCardPresentationTests.cs',
    'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingModelInspectionNavigationTests.cs'
)

[string[]] $actualChangedPaths = @(git diff --name-only $phaseZeroSha $sourceSha)
[string[]] $expectedChangedPaths = Sort-Ordinal $phaseChangedPaths
[string[]] $actualChangedSorted = Sort-Ordinal $actualChangedPaths
if ($actualChangedSorted.Count -ne $expectedChangedPaths.Count -or
    (Compare-Object -ReferenceObject $expectedChangedPaths -DifferenceObject $actualChangedSorted -SyncWindow 0)) {
    throw "Phase 1 changed-file map is incomplete or stale.`nExpected:`n$($expectedChangedPaths -join $nl)`nActual:`n$($actualChangedSorted -join $nl)"
}

[string[]] $sourcePaths = @(
    Get-Content $sourceListPath |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { $_.Trim().Replace('\\', '/') }
)
if ($sourcePaths.Count -ne 371) {
    throw "Expected 371 Phase 1 source paths before closure evidence, found $($sourcePaths.Count)"
}
if ($sourcePaths -contains $evidencePath) {
    throw 'Phase 1 evidence path is already in the source list before Task 9 closure'
}
if ($sourcePaths -contains 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/Controls/InspectionContentTemplateSelectorTests.cs') {
    throw 'Old selector-test path remains in the source list'
}

$sourcePaths = @($sourcePaths + $evidencePath)
$sourcePaths = Sort-Ordinal $sourcePaths
if (($sourcePaths | Select-Object -Unique).Count -ne 372) {
    throw 'Closure source list is not unique'
}
if ($sourcePaths.Count -ne 372) {
    throw "Expected 372 closure source paths, found $($sourcePaths.Count)"
}
if ($sourcePaths | Where-Object { $_ -match '(^|/)(bin|obj)(/|$)' }) {
    throw 'Generated bin/obj output entered the closure source list'
}
Write-LfFile $sourceListPath (($sourcePaths -join $nl) + $nl)

$evidence = @"
# Model Inspection cleanup Phase 1 evidence

**Evidence date:** 2026-08-08  
**Phase 0 closure source:** `$phaseZeroSha`  
**Phase 1 source under test:** `$sourceSha`  
**Permanent workflow run:** `$sourceRunId`  
**Permanent job:** `$sourceJobId`  
**Task 9 ledger-verification run:** `$closureRunId`

## Scope proven by this record

This record closes the Phase 1 WinUI/application-contract cleanup against the exact source tree at `$sourceSha`. That SHA is a no-content verification anchor with the same tree as the completed Task 8 source. Phase 1 changes are limited to the audited WinUI presentation/navigation boundary, focused packaged tests, and cleanup documentation/governance. No production worker, WorkerClient, transport, shared worker-protocol, or LLamaSharp feasibility source file changed between Phase 0 closure and this source SHA.

The protected Gate 2 process boundary therefore retains its existing security and protocol meaning. Worker protocol version 1, serialized JSON names, enum and diagnostic meaning, cancellation/timeout meaning, process containment, handle inheritance, environment allowlisting, evidence privacy, and production/test-fixture separation were not modified by Phase 1.

## Permanent build and warning result

Permanent Windows `Build and test` run `$sourceRunId`, job `$sourceJobId`, completed successfully on `$sourceSha`. Application and packaged-test Release x64 builds both passed.

The check run reported exactly two annotations. Both were the same pre-existing publish-profile warning: `A publish profile with the name 'win-x64.pubxml' was not found in the project. Set the PublishProfile property to a valid file name.` Phase 1 did not change packaging configuration. No WMC1506 annotation was reported in the final source run, closing the compiler-proven immutable `x:Bind` warning cleanup.

## Six permanent test layers

| Layer | Executed | Passed | Failed | Not executed/skipped |
|---|---:|---:|---:|---:|
| Application contract tests | 82 | 82 | 0 | 0 |
| Transport tests | 20 | 20 | 0 | 0 |
| Worker-host tests | 4 | 4 | 0 | 0 |
| WorkerClient tests | 86 | 86 | 0 | 0 |
| Real worker-process tests | 27 | 27 | 0 | 0 |
| Packaged WinUI/UI-thread tests | 217 | 217 | 0 | 0 |
| **Total** | **436** | **436** | **0** | **0** |

The packaged WinUI TRX independently reported 217 total, 217 executed, 217 passed, and zero failed/error/timeout/aborted/inconclusive/not-runnable/not-executed/warning/pending tests.

## Retained artifacts and independent integrity check

| Artifact | GitHub artifact ID | GitHub SHA-256 | Independently downloaded ZIP SHA-256 |
|---|---:|---|---|
| `unit-test-results-$sourceRunId-1` | `$unitArtifactId` | `$unitArtifactDigest` | `$unitArtifactDigest` |
| `gate2-verification-$sourceRunId-1` | `$gate2ArtifactId` | `$gate2ArtifactDigest` | `$gate2ArtifactDigest` |

The Gate 2 publish manifest recorded Release x64 output at `D:/g2/publish` and these retained identities:

- worker host DLL `GraniteEdgeAI.ModelInspection.Worker.dll`: `95FDD542AC7FDADB08693EC68536E2F92CEDF4FBC2E7DBF84BE962E9652BA4DD`
- resolver sentinel `GraniteEdgeAI.ModelInspection.Worker.HostSentinel.txt`: `B0E6113EC785FDD7FACF92B2398A6F5885704E3603E1323F39946370609CAC17`
- process fixture `WorkerFixture.dll`: `4ED3ECA9B744CCD520EDA9A3042F139B2ECFEFBF5393211056C26D715CE41B4B`

## Process containment and evidence privacy

The permanent run step `Check for orphaned Gate 2 processes` completed successfully. The step `Scan Gate 2 evidence for sensitive content` also completed successfully. This record does not infer additional stdout details beyond those observed successful gate conclusions.

## Cleanup inventory closure

Task 9 adds this evidence file to the permanent cleanup boundary. The generated closure source list contains **372** sorted, unique, existing paths and the review inventory contains **372** rows in the identical ordinal order. Generated `bin`/`obj` outputs and the old Onboarding selector-test path are excluded. The repository `CleanupInventoryContractTests` are required to pass before the closure commit can be created.

## Phase 1 production changes

The behavior-changing cleanup is intentionally small:

- `InspectionContentTemplateSelector` removes duplicated content extraction without changing route outcomes.
- `InspectionContentCardPresentation.Hidden` becomes a fresh snapshot and each content card owns independent mutable disclosure state.
- initial five-stage presentation derives correlated status/detail/automation values centrally while preserving approved text and geometry.
- `ModelInspectionPage` removes redundant `SelectedModelPath` state and uses validated request facts directly while retaining the exact request object.
- compiler-proven immutable `x:Bind` modes are corrected while the genuine mutable disclosure bindings remain `TwoWay`.
- ModelCard and OutcomeCard now fail fast when required XAML visual-state names drift, matching the existing ActionCard behavior.

No new MVVM layer, runtime service, classifier, worker engine, OpenVINO route, TurboQuant execution path, or packaging integration is introduced by Phase 1.

## Explicitly deferred work

- Fixed ARGB status/check brushes remain recorded theming/high-contrast debt; changing them would alter approved rendering and belongs to a dedicated UX/accessibility decision.
- The visible `runtime is support` copy error remains recorded but unchanged because Phase 1 freezes visible wording unless separately approved as a UX correction.
- The production worker still uses the controlled unavailable inspection engine. Real LLamaSharp evidence extraction, worker-to-application mapping, classification/service orchestration, ViewModel execution, and dynamic runtime UI remain later gates.
- The pre-existing missing `win-x64.pubxml` publish-profile warning is outside this WinUI/application-contract cleanup scope.

## Final closure rule

This evidence commit is not by itself the final completion claim. After the ledger/source-list/evidence closure commit is created, the identical closure tree must receive a new exact-head permanent Windows `Build and test` run. Phase 1 is only declared closed if that final run is green and the whole PR diff against base `a4138a613dd643abe12858eec5d1c3beb09e95e7` contains no unintended changes.
"@
Write-LfFile $evidencePath ($evidence.TrimEnd() + $nl)

$index = [System.IO.File]::ReadAllText($evidenceIndexPath)
$baselineEntry = '- [Model Inspection cleanup Phase 0 baseline — 2026-08-07](./2026-08-06-model-inspection-cleanup-baseline.md)'
$phaseOneEntry = '- [Model Inspection cleanup Phase 1 evidence — 2026-08-08](./2026-08-07-model-inspection-cleanup-phase-1.md)'
if (-not $index.Contains($baselineEntry)) {
    throw 'Evidence README baseline anchor not found'
}
if ($index.Contains($phaseOneEntry)) {
    throw 'Phase 1 evidence index entry already exists before closure'
}
$index = $index.Replace($baselineEntry, "$baselineEntry$nl$phaseOneEntry")
Write-LfFile $evidenceIndexPath $index

$inventoryHeader = @"
# Model Inspection Cleanup Review Inventory

**Branch:** `refactor/model-inspection-cleanup`  
**Base:** `a4138a613dd643abe12858eec5d1c3beb09e95e7`  
**Phase 1 source evidence:** `$sourceSha` / permanent run `$sourceRunId`  
**Source list:** `docs/reviews/model-inspection-cleanup-source-files.txt`  

This ledger records one closed Phase 1 review disposition for every file in the complete cleanup scope. A `none in Phase 1` change means the file was inspected and deliberately left unchanged, not skipped.

| File | Subsystem | Primary responsibility | Risk | Review status | Findings | Changes made | Behaviour preserved | Tests | Verification evidence | Deferred work and reason |
|---|---|---|---|---|---|---|---|---|---|---|
"@

$rows = [System.Collections.Generic.List[string]]::new()
foreach ($path in $sourcePaths) {
    if ($special.ContainsKey($path)) {
        $m = $special[$path]
    }
    else {
        $subsystem = Get-Subsystem $path
        $m = New-Metadata `
            $subsystem `
            (Get-Responsibility $path $subsystem) `
            (Get-Risk $path $subsystem) `
            (Get-UnchangedFinding $subsystem) `
            'none in Phase 1' `
            (Get-PreservedBehavior $subsystem) `
            (Get-TestEvidence $path $subsystem) `
            (Get-Verification $path $subsystem) `
            (Get-Deferred $subsystem)
    }

    foreach ($value in @($m.Subsystem, $m.Responsibility, $m.Risk, $m.Status, $m.Findings, $m.Changes, $m.Behavior, $m.Tests, $m.Verification, $m.Deferred)) {
        if ([string]$value -match '\|') {
            throw "Inventory metadata for $path contains an unescaped table delimiter"
        }
    }

    $rows.Add("| ``$path`` | $($m.Subsystem) | $($m.Responsibility) | $($m.Risk) | $($m.Status) | $($m.Findings) | $($m.Changes) | $($m.Behavior) | $($m.Tests) | $($m.Verification) | $($m.Deferred) |")
}

if ($rows.Count -ne 372) {
    throw "Expected 372 inventory rows, found $($rows.Count)"
}
$inventory = $inventoryHeader.TrimEnd() + $nl + ($rows -join $nl) + $nl
foreach ($forbidden in @('pending review', 'not reviewed', 'baseline pending', 'serve the recorded subsystem boundary', 'to be mapped during subsystem audit')) {
    if ($inventory.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Forbidden open/generic inventory marker remains: $forbidden"
    }
}
Write-LfFile $inventoryPath $inventory

[string[]] $inventoryPaths = @(
    Get-Content $inventoryPath |
        Where-Object { $_.StartsWith('| `', [StringComparison]::Ordinal) } |
        ForEach-Object { ($_ -split '`')[1] }
)
if ($inventoryPaths.Count -ne 372) {
    throw "Expected 372 parsed inventory paths, found $($inventoryPaths.Count)"
}
if (Compare-Object -ReferenceObject $sourcePaths -DifferenceObject $inventoryPaths -SyncWindow 0) {
    throw 'Inventory row order does not exactly match the closure source list'
}

Write-Host "Task 9 closure metadata prepared: $($sourcePaths.Count) source paths / $($inventoryPaths.Count) inventory rows"
