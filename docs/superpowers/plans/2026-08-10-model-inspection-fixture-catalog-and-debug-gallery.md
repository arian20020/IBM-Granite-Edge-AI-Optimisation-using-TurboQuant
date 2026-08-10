# Model Inspection Fixture Catalogue and Debug Gallery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add one strict, complete, deterministic Model Inspection fixture catalogue that drives automated screen/lifecycle verification and a visible Debug-x64-only gallery without changing production classifier, worker, GGUF, or future-action behavior.

**Architecture:** A UI-independent `GraniteEdgeAI.ModelInspection.Fixtures` project owns strict JSON DTOs, parsing, validation, coverage policy, and deterministic report generation. Debug x64 application sources adapt validated descriptors to existing domain contracts, a deterministic `IModelInspectionService`, and the real `ModelInspectionPage`; the Debug gallery owns exactly one injected page/session at a time and observes the loaded UI independently from descriptor inputs. Release builds remove the project edge, sources, XAML, entry point, and fixture content completely.

**Tech Stack:** .NET 8, C# 12, `System.Text.Json`, WinUI 3 / Windows App SDK 2.2, MSTest AppContainer tests, MSBuild project conditions, PowerShell workflow/contract verification.

---

## Source of truth

- Approved design: `docs/superpowers/specs/2026-08-10-model-inspection-fixture-catalog-and-debug-gallery-design.md`.
- Implementation base: `8d22319328590c4371c94cdf3cf21e762c6d589a`.
- Existing production page: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`.
- Existing presentation mapper: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationFactory.cs`.
- Existing real-worker evidence boundary: N-001 in `tests/TestFixtures/GGUF/N-001-vocab-only-spm.gguf` and `PackagedN001_PageJourneyCompletesAllFiveStagesAsReady`.

## Non-negotiable implementation decisions

- All 49 gallery descriptors are synthetic. N-001 is report linkage only and is never gallery-loadable.
- JSON never contains an absolute path. The adapter constructs `C:\GraniteEdgeAI-Fixtures\<validated filename>`, never opens it, and never logs/displays it.
- The real `ModelInspectionViewModel` owns snapshots, attempts, Cancel, Retry/Restart, Choose another, and disclosure commands. The fake service only reports progress, observes cancellation, and completes one result per call.
- A Debug scenario runner executes descriptor `setupSteps` through those real controls/commands and stops at the descriptor's named observation checkpoint. Service release alone is never used to fake expansion, cancellation-requested, retry, or another interaction-owned state.
- `MI-001` is the real activated initial `0 of 5` page before the service call begins. A concrete Debug factory option disables only its Loaded auto-start; every other fixture keeps the normal Loaded start path.
- `MI-034-retry-stale-result-rejected` uses a Debug-only captured-old-snapshot injection into the real coordinator after Retry. This is necessary because one `Task<ModelInspectionExecutionResult>` cannot complete twice. The seam must be absent from Release.
- Presentable completed evidence always retains the approved X64, CPU-only, VocabOnly runtime identity and successful tokenizer smoke. `IncompletePackage` uses a missing ancillary package-member profile; `Invalid` uses a cross-source configuration contradiction; neither weakens `HasSupportedEvidence`.
- Current maximum rows mean exactly five Ready checks, one approved warning finding, and one Invalid technical-report row. Do not invent extra findings or report rows that production rejects.
- High Contrast, 200% text and reduced-motion selectors are visibly labelled `Preview`; they are deterministic resource/layout simulations, not controlled-OS evidence. The responsive preview persistently forces the real named page/model/content/action visual states because nested XamlRoot width does not follow the constrained preview surface.
- No fixture may execute conversion, Hardware Fit, report export, chat, configuration, file picking, process launch, network access, or arbitrary filesystem access.
- Every RED must fail for the named missing or wrong behavior before production implementation. Parse TRX counters; do not accept console-only evidence.
- Every Debug fixture packaged test class carries `[TestCategory("ModelInspectionFixtureGallery")]`; every class that owns a Window, dispatcher, compositor, shared static state, or gallery session also carries a real MSTest `[DoNotParallelize]`.
- Every new durable path is added to both cleanup registers before the next full Contracts run.

## Reusable verification commands

Use one serialized packaged-test owner. Before each packaged campaign, confirm no `vstest.console`, `testhost`, packaged test app, production worker, or protocol fixture worker is running. Define:

```powershell
$ErrorActionPreference = 'Stop'

function Invoke-ModelInspectionFixturePackagedTests {
  param(
    [Parameter(Mandatory)] [ValidateSet('Debug','Release')] [string] $Configuration,
    [Parameter(Mandatory)] [string] $Filter,
    [Parameter(Mandatory)] [string] $ResultName
  )

  $project = '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
  $results = ".\TestResults\ModelInspectionFixtures\$Configuration"
  $recipe = ".\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\$Configuration\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe"

  dotnet restore $project --runtime win-x64 -p:Platform=x64
  if ($LASTEXITCODE -ne 0) { throw 'Packaged restore failed.' }
  dotnet build $project -c $Configuration --no-restore --runtime win-x64 -p:Platform=x64
  if ($LASTEXITCODE -ne 0) { throw 'Packaged build failed.' }

  New-Item -ItemType Directory -Force -Path $results | Out-Null
  $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
  $vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
  if (-not $vstest) { throw 'AppContainer VSTest was not found.' }

  & $vstest (Resolve-Path -LiteralPath $recipe).Path `
    '/Platform:x64' `
    "/TestCaseFilter:$Filter" `
    "/Logger:trx;LogFileName=$ResultName" `
    "/ResultsDirectory:$((Resolve-Path -LiteralPath $results).Path)"
  if ($LASTEXITCODE -ne 0) { throw "Packaged campaign failed: $ResultName" }
}
```

For contract tests use:

```powershell
dotnet test .\tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj `
  --filter 'FullyQualifiedName~ModelInspectionFixture' `
  --logger 'trx;LogFileName=fixture-contracts.trx' `
  --results-directory .\TestResults\ModelInspectionFixtures\Contracts
```

For every GREEN TRX require `total == executed == passed` and every failed/error/timeout/aborted/inconclusive/notExecuted/notRunnable counter to be zero.

## Required RED checkpoints

| Task | Required RED anchor | Expected parent failure |
|---|---|---|
| 1 | `StrictJson_RejectsUnknownAndDuplicateProperties`; `Filename_RequiresExactIdTargetAndVariant` | fixture assembly/types absent |
| 2 | `Catalogue_RequiresEveryPolicyEntryAndProductionEnum`; `GeneratedReport_ByteMatchesCommittedEvidence` | policy/descriptors/report absent |
| 3 | `Adapter_MapsAllClosedProfilesToPresentableDomainGraphs`; `Service_ReleasesOnlyDeclaredStepsAndRetiresPendingCalls` | Debug adapter/service absent |
| 4 | `FixtureActivation_UsesSharedProductionPath`; `FixtureRetirement_AllExitSignalsCallSharedPathExactlyOnce` | page activation/retirement seams absent |
| 5 | `ReleaseEvaluationAndPackage_ContainZeroFixtureArtifacts`; `DebugX64Evaluation_ContainsExactFixtureClosure` | Debug/Release MSBuild boundary absent |
| 6 | `GalleryEntry_DebugX64IsVisibleAndLoadsAtomicCatalogue`; `GallerySelection_OwnsOneRealInjectedPage` | gallery/entry/host absent |
| 7 | `EveryDescriptor_ObservedLoadedScreenMatchesIndependentExpectedContract` | observer and complete mapping absent |
| 8 | `EveryRequiredPreset_SatisfiesSubstantiveLayoutAndAccessibilityExpectations` | preset engine/expectations absent |
| 9 | `AllDeclaredInteractions_UseRealCommandsAndRejectStaleCallbacks`; `AllExitRoutes_RetireExactlyOnce` | scripted interactions/lifetime evidence absent |
| 10 | `Workflow_RunsSerializedDebugFixtureCampaignAndPreservesReleaseFloor`; `FixtureEvidence_IsCompleteTruthfulAndPrivate` | permanent Debug campaign/docs gates absent |

---

## Task 1: Add the strict UI-independent fixture contract and loader

**Files:**

- Create: `shared/GraniteEdgeAI.ModelInspection.Fixtures/GraniteEdgeAI.ModelInspection.Fixtures.csproj`
- Create: `shared/GraniteEdgeAI.ModelInspection.Fixtures/ModelInspectionFixtureEnums.cs`
- Create: `shared/GraniteEdgeAI.ModelInspection.Fixtures/ModelInspectionFixtureDescriptor.cs`
- Create: `shared/GraniteEdgeAI.ModelInspection.Fixtures/ModelInspectionFixtureCoveragePolicy.cs`
- Create: `shared/GraniteEdgeAI.ModelInspection.Fixtures/ModelInspectionFixtureDocumentSource.cs`
- Create: `shared/GraniteEdgeAI.ModelInspection.Fixtures/ValidatedModelInspectionFixture.cs`
- Create: `shared/GraniteEdgeAI.ModelInspection.Fixtures/StrictModelInspectionFixtureJson.cs`
- Create: `shared/GraniteEdgeAI.ModelInspection.Fixtures/ModelInspectionFixtureValidator.cs`
- Create: `shared/GraniteEdgeAI.ModelInspection.Fixtures/ModelInspectionFixtureCatalogue.cs`
- Modify: `IBM Granite with TurboQuant (Intel).slnx`
- Modify: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureJsonContractTests.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureValidationContractTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write strict-parser and filename RED tests**

Create one minimal in-memory valid descriptor factory in the contract tests, then mutate it. Require:

```csharp
Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(
    () => ModelInspectionFixtureCatalogue.Load([source], policy));
```

Cover unknown members at every depth, unknown/case-mismatched/integer enums, null required fields, duplicate and escaped-duplicate keys, excessive bytes/depth, non-finite numeric tokens, and multiple JSON documents. The filename tests require exact `.fixture.json`, uppercase `MI-`, three digits, lowercase ASCII kebab slugs, document-ID equality, contiguous `targetCondition`, optional contiguous `variant`, and no extra suffix.

Run the focused contract filter. Expected RED: `CS0246` for the fixture types. Retain the compile log, then add compiling stubs and rerun until at least the duplicate-property or filename assertion fails behaviorally.

- [ ] **Step 2: Implement closed DTOs and duplicate-safe parsing**

Use immutable records with constructor-required properties and closed enums. The exact top-level shape is:

```csharp
public sealed record ModelInspectionFixtureDescriptor(
    string Schema,
    int SchemaVersion,
    string Id,
    string TargetCondition,
    string? Variant,
    string Title,
    ModelInspectionFixtureCategory Category,
    ModelInspectionFixtureCoverage Coverage,
    ModelInspectionFixtureInput Input,
    ModelInspectionExpectedScreen Expected,
    IReadOnlyDictionary<string, ModelInspectionPresetExpectation> PresetExpectations,
    IReadOnlyList<ModelInspectionFixtureInteraction> Interactions,
    IReadOnlyList<string> Presets);

public sealed record ModelInspectionFixtureInput(
    ModelInspectionFixtureRequestDescriptor Request,
    IReadOnlyList<ModelInspectionFixtureAttemptDescriptor> Attempts,
    IReadOnlyList<ModelInspectionFixtureSetupStepDescriptor> SetupSteps,
    string ObservationCheckpoint);

public sealed record ModelInspectionFixtureAttemptDescriptor(
    int Attempt,
    IReadOnlyList<ModelInspectionFixtureServiceStepDescriptor> ServiceSteps);
```

`ModelInspectionFixtureServiceStepDescriptor` is a closed trigger/effect union with optional progress/evidence/failure/checkpoint payloads. `ModelInspectionFixtureSetupStepDescriptor` is a closed sequence of `release-service-checkpoint`, `invoke-disclosure`, `invoke-cancel`, `invoke-retry`, `invoke-restart`, `invoke-choose-another`, `release-stale-progress`, `submit-stale-result-snapshot`, `release-stale-motion`, `release-stale-announcement`, and `observe`. Service/stale setup steps must reference declared attempt/deferred checkpoints; command/disclosure setup steps must reference a step-scoped declared interaction; exactly one final named observation checkpoint is required. The gallery exposes only interactions whose `sourceCheckpoint` is the current observation checkpoint, never setup-history actions.

`ModelInspectionExpectedScreen` owns nested exact records for Figma/geometry identity, outcome/model/content/action regions, five footer rows/status, focus, automation/control/live-region values, announcements, rows/scroll owner, and required retained identities. No input/evidence profile type is reused inside an expected record.

The strict document boundaries are:

```csharp
public sealed record ModelInspectionFixtureDocumentSource(
    string FileName,
    ReadOnlyMemory<byte> Utf8Json);

public static VerifiedModelInspectionFixtureSchema VerifySchema(
    ModelInspectionFixtureDocumentSource schemaSource);

public static ValidatedModelInspectionFixtureCoveragePolicy LoadPolicy(
    ModelInspectionFixtureDocumentSource policySource,
    VerifiedModelInspectionFixtureSchema schema);

public static ModelInspectionFixtureCatalogue LoadDescriptors(
    IReadOnlyList<ModelInspectionFixtureDocumentSource> descriptorSources,
    ValidatedModelInspectionFixtureCoveragePolicy policy,
    VerifiedModelInspectionFixtureSchema schema);

public static ValidatedModelInspectionFixture RevalidateDescriptor(
    ModelInspectionFixtureDocumentSource descriptorSource,
    ValidatedModelInspectionFixtureCoveragePolicy policy,
    VerifiedModelInspectionFixtureSchema schema,
    ModelInspectionFixtureCatalogueIndex catalogueIndex);
```

Each validated fixture retains immutable raw UTF-8 bytes plus SHA-256 for genuine second-boundary revalidation. It exposes separately branded `ValidatedModelInspectionFixtureInput Input` and `ModelInspectionExpectedScreen Expected`; consumers cannot obtain one by converting the other.

Pre-scan `Utf8JsonReader` object scopes for decoded duplicate property names before deserializing. Configure `JsonSerializerOptions` with `UnmappedMemberHandling = Disallow`, case-sensitive names/enums, maximum depth, no comments/trailing commas, and exact string/array size validation. Diagnostics may contain only fixture filename, JSON path, and stable rule code.

- [ ] **Step 3: Implement privacy, filename, and semantic validation**

Reject absolute/root-relative paths, slash/backslash path shapes, URI schemes, drive tokens, environment/user/machine identity, control/format/bidi/private-use/unassigned scalars, non-NFC text, unbounded values, unsafe display filenames, and diagnostics that echo input. Enforce:

- exact stage count 5;
- completed count equals stage ordinal for Completed/Warning and ordinal minus one for Active/Failed/Cancelled;
- fraction only on Active and finite in `[0,1]`;
- monotonic attempt scripts and at most one terminal effect per attempt;
- closed evidence/failure/interaction/preset profiles;
- Ready/ReadyWithWarnings chat-template and finding rules;
- ConversionRequired alone owns a verified route;
- no progress after terminal, contradictory current progress+terminal, unsupported interaction, or dangling transition;
- duplicate target conditions only for explicitly linked variants.
- Warning progress may continue only through the remaining ordered stages to the approved ReadyWithWarnings result; Failed progress must terminate on its declared blocking completed/failure route; Cancelled progress must terminate cooperatively as Cancelled. Mutation tests cross-wire all three and require rejection.

- [ ] **Step 4: Establish the fail-closed Debug-x64 compilation boundary before any Debug source exists**

In both app and packaged-test projects, unconditionally remove their dedicated `DebugFixtures` subtrees from `Compile`, `Page`, `None`, `Content`, `EmbeddedResource`, and `PRIResource`; conditionally re-include intended C#/XAML only under exact `Condition="'$(Configuration)|$(Platform)' == 'Debug|x64'"`. Define `MODEL_INSPECTION_FIXTURE_GALLERY` and add the shared fixture-project reference only under that condition in the app. The contract-test project references the neutral project unconditionally. Add the conditional fixture JSON `Content` glob now so Task 2 assets cannot leak when created. Task 5 will mutation-test and inspect the complete evaluated/package closure.

- [ ] **Step 5: Run GREEN and commit**

Run both focused classes, the complete fixture project build with warnings-as-errors, and cleanup 3/3. Commit:

```text
feat(model-inspection): add strict fixture catalogue contracts
```

---

## Task 2: Check in the authoritative policy, schema, 49 descriptors, and report

**Files:**

- Create: `tests/TestFixtures/ModelInspectionScenarios/model-inspection-fixture.schema.json`
- Create: `tests/TestFixtures/ModelInspectionScenarios/model-inspection-fixture-coverage-policy.json`
- Create: `tests/TestFixtures/ModelInspectionScenarios/README.md`
- Create the exact 49 fixture files listed below.
- Create: `shared/GraniteEdgeAI.ModelInspection.Fixtures/ModelInspectionFixtureCoverageValidator.cs`
- Create: `shared/GraniteEdgeAI.ModelInspection.Fixtures/ModelInspectionFixtureReportGenerator.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureCatalogueContractTests.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureReportContractTests.cs`
- Create: `docs/evidence/testing/Model-Inspection-Fixture-Catalog.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

The exact initial catalogue is:

| ID | Exact filename |
|---|---|
| MI-001 | `MI-001-inspection-progress-initial.fixture.json` |
| MI-002 | `MI-002-ready-clean-compatible-model-collapsed.fixture.json` |
| MI-003 | `MI-003-ready-clean-compatible-model-expanded.fixture.json` |
| MI-004 | `MI-004-ready-with-warnings-chat-template-missing-collapsed.fixture.json` |
| MI-005 | `MI-005-ready-with-warnings-chat-template-missing-expanded.fixture.json` |
| MI-006 | `MI-006-conversion-required-verified-incompatible-route-collapsed.fixture.json` |
| MI-007 | `MI-007-conversion-required-verified-incompatible-route-expanded.fixture.json` |
| MI-008 | `MI-008-incomplete-package-missing-package-member.fixture.json` |
| MI-009 | `MI-009-unsupported-model-architecture.fixture.json` |
| MI-010 | `MI-010-invalid-cross-source-evidence-contradiction-collapsed.fixture.json` |
| MI-011 | `MI-011-invalid-cross-source-evidence-contradiction-expanded.fixture.json` |
| MI-012 | `MI-012-cancelled-cooperative-user-cancellation.fixture.json` |
| MI-013 | `MI-013-operational-failure-worker-start-failure.fixture.json` |
| MI-014 | `MI-014-progress-check-model-package-active-fractionless.fixture.json` |
| MI-015 | `MI-015-progress-check-model-package-completed.fixture.json` |
| MI-016 | `MI-016-progress-read-model-configuration-active.fixture.json` |
| MI-017 | `MI-017-progress-read-model-configuration-completed.fixture.json` |
| MI-018 | `MI-018-progress-validate-tokenizer-chat-setup-active.fixture.json` |
| MI-019 | `MI-019-progress-validate-tokenizer-chat-setup-completed.fixture.json` |
| MI-020 | `MI-020-progress-validate-model-structure-active.fixture.json` |
| MI-021 | `MI-021-progress-validate-model-structure-completed.fixture.json` |
| MI-022 | `MI-022-progress-confirm-runtime-compatibility-active.fixture.json` |
| MI-023 | `MI-023-progress-confirm-runtime-compatibility-completed.fixture.json` |
| MI-024 | `MI-024-progress-cancel-requested.fixture.json` |
| MI-025 | `MI-025-progress-chat-setup-warning.fixture.json` |
| MI-026 | `MI-026-progress-model-structure-failed.fixture.json` |
| MI-027 | `MI-027-progress-runtime-compatibility-cancelled.fixture.json` |
| MI-028 | `MI-028-progress-read-model-configuration-active-bounded-fraction.fixture.json` |
| MI-029 | `MI-029-cancellation-requested-cooperative-cancelled.fixture.json` |
| MI-030 | `MI-030-cancellation-forced-operational-failure.fixture.json` |
| MI-031 | `MI-031-retry-after-cancellation.fixture.json` |
| MI-032 | `MI-032-retry-after-operational-failure.fixture.json` |
| MI-033 | `MI-033-retry-stale-progress-rejected.fixture.json` |
| MI-034 | `MI-034-retry-stale-result-rejected.fixture.json` |
| MI-035 | `MI-035-retry-stale-motion-completion-rejected.fixture.json` |
| MI-036 | `MI-036-retry-stale-announcement-rejected.fixture.json` |
| MI-037 | `MI-037-choose-another-page-retired.fixture.json` |
| MI-038 | `MI-038-gallery-switch-old-session-retired.fixture.json` |
| MI-039 | `MI-039-operational-failure-worker-timeout.fixture.json` |
| MI-040 | `MI-040-operational-failure-worker-crash-early-exit.fixture.json` |
| MI-041 | `MI-041-operational-failure-malformed-worker-response.fixture.json` |
| MI-042 | `MI-042-operational-failure-cancellation-unconfirmed.fixture.json` |
| MI-043 | `MI-043-ready-model-name-maximum-collapsed.fixture.json` |
| MI-044 | `MI-044-ready-missing-optional-metadata-not-reported-collapsed.fixture.json` |
| MI-045 | `MI-045-ready-check-rows-current-maximum-expanded.fixture.json` |
| MI-046 | `MI-046-ready-with-warnings-finding-rows-current-maximum-expanded.fixture.json` |
| MI-047 | `MI-047-invalid-report-rows-current-maximum-expanded.fixture.json` |
| MI-048 | `MI-048-progress-detail-copy-maximum.fixture.json` |
| MI-049 | `MI-049-operational-failure-detail-copy-maximum.fixture.json` |

- [ ] **Step 1: Write catalogue completeness and report RED tests**

Require exact stable ID ordering, all 13 `ModelInspectionFigmaState` values, all five stages with Active+Completed, the specified Warning/Failed/Cancelled progress cases, every supported interaction and lifecycle tag, the four distinct operational-failure profiles, all data-stress tags, and every policy preset/pairwise assignment. Mutations deleting/renaming one file, changing one targeted filename slug, removing one coverage tag, adding an enum, breaking a transition, or duplicating a target without a pair link must fail atomically.

The contract project cannot reference internal WinUI enums. Its source contract parses the exact enum member declarations in `ModelInspectionFigmaState.cs`, `ModelInspectionEnums.cs`, and the supported interaction enum, while the later Debug packaged coverage test independently uses `Enum.GetValues<T>()`. An added/removed/renamed member must fail both paths until the policy and catalogue change together.

The report test loads descriptors and a separately declared external-evidence join, generates UTF-8 LF bytes, and compares them byte-for-byte with the checked-in Markdown. Before emitting the N-001 join it requires the exact `tests/TestFixtures/GGUF/N-001-vocab-only-spm.gguf` file and inspects `ModelInspectionPageNavigationTests.PackagedN001_PageJourneyCompletesAllFiveStagesAsReady` for its five ordered stages plus Ready collapsed/expanded assertions. Removing either source makes the join fail rather than silently disappearing. Expected RED: policy, fixtures, and report are absent.

- [ ] **Step 2: Add schema and policy before descriptors**

Make schema version `1`. The policy lists exact filenames/IDs, canonical-state ownership, progress matrix, lifecycle/failure/stress tags, four disclosure pairs, required interactions, the copy registry, and the exact preset matrix. The policy alone owns the N-001 external link for MI-002/MI-003; descriptors remain synthetic.

The policy copy registry maps every allowed `copyKey` to one approved default-English string. Validation requires every descriptor key to exist and its expected text to match; contract mutations change the key and text independently. Task 7 then observes the real loaded default-English text, so the policy/descriptor cannot make a wrong production string pass together.

Use these exact preset IDs:

| ID | Width | Resources | Text | Motion |
|---|---|---|---|---|
| P01 | Desktop1440 | Light | Standard100 | Normal |
| P02 | Desktop1440 | Dark | Preview200 | Reduced |
| P03 | Desktop1440 | HighContrastPreview | Standard100 | Reduced |
| P04 | Medium600 | Light | Preview200 | Normal |
| P05 | Medium600 | Dark | Standard100 | Reduced |
| P06 | Medium600 | HighContrastPreview | Preview200 | Reduced |
| P07 | Narrow360 | Light | Standard100 | Reduced |
| P08 | Narrow360 | Dark | Preview200 | Normal |
| P09 | Narrow360 | HighContrastPreview | Standard100 | Normal |

Every MI-001..013 fixture requires P01. Assign P02..P08 respectively to MI-043..049 and P09 to MI-003 in addition to P01. The validator independently enumerates every pair of values across width/resource/text/motion and requires each pair at least once; it does not trust a policy `pairwiseComplete` flag.

- [ ] **Step 3: Add descriptors in four independently reviewable batches**

Add MI-001..013, then MI-014..028, then MI-029..042, then MI-043..049. After each batch run the catalogue filter and retain the exact missing-policy-entry RED count until the final batch is GREEN. Every `expected` object declares the fixture's currently loadable region, footer, focus, accessibility, announcement, identity-retention, and action contracts rather than naming a production factory preset. Every fixture has explicit `setupSteps` ending at one observation checkpoint: expanded fixtures invoke the real disclosure, MI-024 invokes the real Cancel command and stops before service completion, and non-destructive retry/stale fixtures execute only the sequence needed to reach their observed post-rejection screen. Destructive lifetime actions are not auto-run: MI-037 stops at its loadable Ready screen and exposes step-scoped Choose another leading to `gallery:no-active-fixture`; the Debug host intercepts the real page event, retires the nested page/session, and leaves the production onboarding shell unchanged. MI-038 stops at its source Ready screen; the coverage policy owns its external gallery-switch source/destination pair, and the Task 9 gallery harness selects that destination directly. Gallery selection is not added to the closed descriptor interaction vocabulary or interaction panel. Only the gallery's explicit Close command returns the shell to a fresh Model Import page.

For MI-001 use the Debug factory's explicit `startInspectionOnLoaded: false` option, producing the real initial `0 of 5` page with no service call. For MI-034 declare a captured-old-terminal-snapshot release after Retry; Task 9 will supply the Debug-only injection. Current-max stress fixtures must assert 5/1/1 rows rather than invent unsupported rows.

- [ ] **Step 4: Generate and verify the truthful report**

Generate `Model-Inspection-Fixture-Catalog.md` in stable ID order. Mark every descriptor `synthetic deterministic fixture`; mark only MI-002/MI-003 with separate `N-001 real-worker coverage`; explicitly state that ReadyWithWarnings, ConversionRequired, IncompletePackage, Unsupported, Invalid and failure variants are not real-worker-classified fixtures. No absolute path, environment identity, raw model content, or unverified evidence enters the report.

- [ ] **Step 5: Run GREEN and commit**

Run catalogue/report contracts, privacy mutations, exact 49-file checks, cleanup 3/3, and `git diff --check`. Commit:

```text
test(model-inspection): add complete synthetic fixture catalogue
```

---

## Task 3: Map fixtures to valid domain graphs and a deterministic service

**Files:**

- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/ModelInspectionFixtureExecutionPlan.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/ModelInspectionFixtureAdapter.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/DebugModelInspectionService.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/ModelInspectionFixtureSession.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/ModelInspectionFixtureSessionEvidence.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureAdapterTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/DebugModelInspectionServiceTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureViewModelIntegrationTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write adapter and service RED tests**

Test all closed evidence profiles and failure profiles. Require the fixed synthetic root, matching request/file identity, fixed UTC timestamps, exact approved runtime pins, successful tokenizer smoke, correct chat-template/warning relationship, ConversionRequired-only route, zero findings on other outcomes, and safe operational-failure codes. Source/IL contracts forbid process, picker, arbitrary filesystem, HTTP/network, worker-composition, and production-service-composition references anywhere in the Debug subtree; runtime tests inject and count only the permitted package-resource boundary added in Task 6.

Test service event order, exact request identity, one result per attempt, cancellation observation, cooperative cancellation, unconfirmed cancellation, named checkpoints, retained stale-progress reporters only when declared, excess-call rejection, and silent completion of pending calls during retirement. Expected RED: Debug runtime types absent.

- [ ] **Step 2: Implement the immutable adapter**

Map only closed profiles; do not let JSON specify arbitrary runtime strings, hashes, finding codes, route IDs, or exceptions. Build a fixed valid domain graph through existing constructors. Structurally accept only the branded input, never a full fixture or expected contract:

```csharp
internal static ModelInspectionFixtureExecutionPlan CreatePlan(
    ValidatedModelInspectionFixtureInput input);

internal sealed record ModelInspectionFixtureExecutionPlan(
    ModelInspectionRequest Request,
    IReadOnlyList<ModelInspectionFixtureAttemptPlan> Attempts,
    IReadOnlyList<ModelInspectionFixtureDeferredEvent> DeferredEvents);
```

`ModelInspectionFixtureAttemptPlan` is the immutable mapped attempt number plus ordered service effects. `ModelInspectionFixtureDeferredEvent` is a closed event kind, owner attempt, and named release checkpoint. The screen comparer is the only runtime component that accepts `ModelInspectionExpectedScreen`.

Fail before page creation if any mapped constructor or presentation precondition disagrees with the validated descriptor.

- [ ] **Step 3: Implement the deterministic service and session owner**

Use `TaskCompletionSource<ModelInspectionExecutionResult>(RunContinuationsAsynchronously)` and explicit checkpoint releases, never `Task.Delay`. `DebugModelInspectionService` implements `IModelInspectionService` and `IDisposable`; `ModelInspectionFixtureSession` owns it, fixed motion settings, deferred-event handles, audit counters, and idempotent `Retire`/`Dispose`. The manual gallery's Normal preset creates the approved production `WinUiModelInspectionAnimationDriver` wrapped by a non-mutating audit decorator; tests may inject a controllable driver. Reduced motion uses the same endpoint path with animations disabled and must record zero animation starts.

- [ ] **Step 4: Prove real ViewModel command ownership**

Run a plain ViewModel integration around the fake service. Invoke actual Cancel/Retry/Restart/Choose-another commands; prove generation changes, cancellation-requested state, exact service-call count, and rejected unsupported descriptor actions. Do not add commands to `IModelInspectionService`.

- [ ] **Step 5: Run GREEN and commit**

Run Debug x64 packaged filters for the three classes and the existing ViewModel suite. Commit:

```text
feat(model-inspection): add deterministic fixture sessions
```

---

## Task 4: Share page activation and retirement without changing production behavior

**Files:**

- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/ModelInspectionPage.DebugFixtures.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixturePageLifecycleTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write shared activation/retirement RED tests**

Require production `OnNavigatedTo` and Debug `CreateForFixture` to enter the same page-owned activation path and produce equal initial presentation/coordinator ownership. Require production `OnNavigatedFrom` and `RetireForFixture` to enter the same retirement path. Count page subscriptions, dispatcher work, motion batches, disclosure operations, focus requests, and live-region callbacks. Call `RetireForFixture` twice and require true then false with no second mutation. Host/gallery exit routes are tested only after the host exists in Tasks 6 and 9. Expected RED: Debug factory/retirement seams do not exist.

- [ ] **Step 2: Extract production activation and retirement bodies**

Make `OnNavigatedTo` validate its parameter and call private `ActivateRequest(request)`. Rename/extract `RetireNavigationLifetime` to private `bool RetirePageLifetime()`; return `false` only when no lifetime remains, and have both `OnNavigatedFrom` and activation-before-replacement call it. Preserve the existing order and semantics exactly.

- [ ] **Step 3: Add the Debug-only partial page surface**

Under `MODEL_INSPECTION_FIXTURE_GALLERY`, add:

```csharp
internal static ModelInspectionPage CreateForFixture(
    ModelInspectionFixtureSession session,
    bool startInspectionOnLoaded,
    Action<ResourceDictionary>? configureResourcesBeforeInitialize = null)
{
    ArgumentNullException.ThrowIfNull(session);
    var page = new ModelInspectionPage(
        session.Service,
        CreateProductionDispatcher,
        session.CreateAnimationDriver,
        session.CreateMotionSettings,
        startInspectionOnLoaded,
        configureResourcesBeforeInitialize);
    page.ActivateRequest(session.Request);
    return page;
}

internal bool RetireForFixture() => RetirePageLifetime();
```

Add one private generalized constructor accepting the two final parameters; existing production constructors pass `true` and `null`, so product behavior is unchanged. The Loaded handler starts only when that immutable flag is true. Also add narrowly scoped fixture methods to capture the old snapshot and submit it to the real coordinator, and to release captured animation/announcement callbacks. These methods are used only by declared stale-event descriptors and are compiled out of Release.

- [ ] **Step 4: Prove idempotent page ownership**

Call the page retirement seam after activation, during a synchronous motion/service callback, and repeatedly. Require coordinator invalidation before callback-producing cancellation, then exactly one unsubscribe/cancel/dispose sequence and a false no-op result on every later call. Do not create or test the host guard in this task.

- [ ] **Step 5: Run GREEN and regress production navigation**

Run the Debug lifecycle class and the complete existing `ModelInspectionPageNavigationTests` in Release. Commit:

```text
refactor(model-inspection): share fixture page lifetime paths
```

---

## Task 5: Make the Debug-x64 inclusion and Release-zero-artifact boundary fail closed

**Files:**

- Modify: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureBuildBoundaryContractTests.cs`
- Create: `scripts/model-inspection/Test-ModelInspectionFixtureReleaseIsolation.ps1`
- Modify: `IBM Granite with TurboQuant (Intel).slnx`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write project-evaluation and package RED tests**

Require exactly one dedicated `MODEL_INSPECTION_FIXTURE_GALLERY` definition for `Condition="'$(Configuration)|$(Platform)' == 'Debug|x64'"`, and zero fixture project references, Compile/Page/PRI/Content/None items, labels, JSON/schema/policy/report assets, or entry source in Release x64/x86/ARM64 evaluation. Require Debug x64 to contain the exact shared-project edge, Debug sources/XAML, and exact policy-declared 49 JSON files plus schema/policy.

Mutation tests must catch `#if DEBUG` without the platform gate, default-glob leakage, `None`/`Content Update` leakage, alternate slashes/case, property indirection, and an extra fixture item. Expected RED: current project has no explicit boundary.

- [ ] **Step 2: Harden and reconcile the conditional item ownership established in Task 1**

Require the existing fail-closed removes to cover `Features\ModelInspection\DebugFixtures\**` and `Features\Onboarding\DebugFixtures\**` across Compile/Page/None/Content/EmbeddedResource/PRIResource, then re-include exact source/XAML only under the one Debug|x64 condition. Require the constant, project reference, and `TestFixtures/ModelInspectionScenarios/` package content to use that same condition in app and packaged-test projects. Remove any broader, duplicated, or property-indirected condition found by the RED mutations.

- [ ] **Step 3: Inspect real Release and Debug build outputs**

The isolation script builds/evaluates Release x64 and opens the produced app package/layout as data, failing on any `fixture`, `ModelInspectionScenarios`, gallery type/XAML, `GraniteEdgeAI.ModelInspection.Fixtures.dll`, or entry label. A contract additionally reads the Release application assembly through `PEReader`/`MetadataReader` and rejects every fixture/gallery type or assembly reference, so a type hidden inside the main DLL cannot escape a path-only scan. The script separately evaluates Debug x64 and requires the exact closure. It must not delete or inspect outside its owned output directory.

- [ ] **Step 4: Run GREEN and commit**

Run boundary contracts, the script, Release x64 app build, Debug x64 app build, and cleanup. Commit:

```text
build(model-inspection): isolate fixture gallery to debug x64
```

---

## Task 6: Add the visible Debug gallery entry, catalogue browser, and one-page host

**Files:**

- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/DebugFixtures/OnboardingShellPage.FixtureGallery.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/ModelInspectionFixtureGalleryPage.xaml`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/ModelInspectionFixtureGalleryPage.xaml.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/ModelInspectionFixtureHostPage.xaml`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/ModelInspectionFixtureHostPage.xaml.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/ModelInspectionFixturePackageLoader.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/IModelInspectionFixturePackageResourceReader.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/ModelInspectionFixtureScenarioRunner.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/ModelInspectionFixtureGalleryViewModel.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/ModelInspectionFixtureListItem.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Presets/ModelInspectionFixturePreset.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureGalleryTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write loaded-gallery RED tests**

In a visible test Window, require a visible `Fixture gallery` button in Debug x64, no production Stage change, a split gallery layout, 49 stable sorted list rows, search by ID/filename/target, category filtering, filename/ID/target/category header, `Synthetic fixture` badge, MI-002/003-only read-only N-001 link, preset and declared-interaction controls, Reset, Close, and concise validation status. Every new class is explicitly categorized `ModelInspectionFixtureGallery`; Window/session classes are `DoNotParallelize`. Expected RED: entry and pages absent.

- [ ] **Step 2: Add the zero-Release entry hook**

Declare `partial void InitializeFixtureGalleryEntry();` in the existing shell constructor after `InitializeComponent`; the unimplemented Release call is erased. The Debug partial implementation programmatically adds the button to the root Grid and navigates `StageFrame` to `ModelInspectionFixtureGalleryPage`. Do not modify Release shell XAML or add a generic Release tools host.

- [ ] **Step 3: Load package resources atomically**

Read the fixed schema, policy, and 49 descriptor resources from `ms-appx:///TestFixtures/ModelInspectionScenarios/` through `IModelInspectionFixturePackageResourceReader`. Package reads are the only fixture I/O. Call `VerifySchema` for the schema document, `LoadPolicy` for the policy document, and `LoadDescriptors` only for the 49 descriptor documents; do not mix their types. Retain immutable raw descriptor bytes for second-boundary `RevalidateDescriptor` and show no scenario if any entry fails. The production reader accepts only exact policy-listed package URIs; tests inject an in-memory counting reader. Source/IL contracts reject file/directory/picker/process/worker/HTTP APIs and the public production page constructor in every Debug source. No directory enumeration, network, temporary storage, fallback fixture, or intercepting-static-API fiction.

- [ ] **Step 4: Own exactly one real injected page**

Gallery selection first retires current host/session, validates the selected fixture raw bytes again, creates a fresh session, and navigates the inner Frame to `ModelInspectionFixtureHostPage` with a Debug activation object. The host calls `ModelInspectionPage.CreateForFixture`, sets that real page as content, and uses an `Interlocked.Exchange` exactly-once guard on replacement/navigation/unload/close. Page retirement occurs before session retirement/disposal and content clearing.

The `ModelInspectionFixtureScenarioRunner` consumes only validated input/setup steps, releases service checkpoints, invokes real controls/commands for disclosure/Cancel/Retry/Restart/Choose another, releases declared Debug stale callbacks, and stops at exactly one named observation checkpoint. It never receives `expected`. Selection does not report success until this runner reaches the checkpoint.

Define the closed preset record now (desktop/medium/narrow, Light/Dark/HighContrastPreview, Standard100/Preview200, Normal/Reduced) so gallery requests compile before the applier is implemented in Task 8. The initial selection uses the canonical desktop/Light/100/normal combination.

After each successful replacement clear both the nested Frame BackStack and ForwardStack so a retired host cannot be resurrected. Reset creates a fresh host/session for the same fixture. Close retires the active host, clears both journals, and asks the onboarding shell to navigate to a fresh Model Import page; it is distinct from Window/app close.

Add exact tests for host navigation away, Unloaded, gallery Close, Window close, invalid selection after an active page, Reset, fixture switch, and simultaneous navigation-away+Unloaded. Every route calls the nested page's `RetireForFixture()` and session disposal exactly once; invalid selection retires first and shows the atomic error with no replacement page.

- [ ] **Step 5: Run GREEN and commit**

Run the Debug packaged gallery class, Release boundary contract, and onboarding Release regressions. Commit:

```text
feat(model-inspection): add debug fixture gallery shell
```

---

## Task 7: Observe and compare every loaded screen independently

**Files:**

- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Observation/ModelInspectionObservedScreen.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Observation/ModelInspectionFixtureScreenObserver.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Observation/ModelInspectionFixtureScreenComparer.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/ModelInspectionPage.DebugFixtures.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureScreenContractTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write the all-descriptor observed-screen RED**

For each descriptor, create a fresh page/session, run its validated setup steps through `ModelInspectionFixtureScenarioRunner`, stop at its observation checkpoint, load the real controls, drain dispatcher/composition boundaries, observe without passing `expected`, then compare every expected field. Include one mutation per region, Figma state, copy, action, footer, focus, accessible name/control type/live setting, announcement count, row count/order, and retained identity. Expanded/cancel-requested/retry/lifecycle fixtures must prove the real action ran before observation. Expected RED: no independent observer/comparer.

- [ ] **Step 2: Implement the observer over real UI state**

The observer may read `CurrentPresentation` only for render identity/state cross-checks; visible text, Visibility, enabled state, row order, focus, automation properties, live settings, target size, and scroll ownership come from the loaded visual/automation tree. The observer accepts no descriptor or expected object.

- [ ] **Step 3: Compare exact contracts and surface diagnostics**

Return stable rule-coded differences containing only fixture filename, expected field path, safe expected token, and safe observed token. Gallery displays pass or concise differences. One mismatching fixture does not silently render as passing, but catalogue-load failures remain atomic.

- [ ] **Step 4: Prove all canonical and progress states**

Use `Enum.GetValues<ModelInspectionFigmaState>()`, `Enum.GetValues<ModelInspectionStage>()`, `Enum.GetValues<ModelInspectionOutcome>()`, and the closed Debug interaction enum to independently require policy/catalogue coverage at runtime. Require exact 13-state coverage, four disclosure owners with retained ItemsSource/container/scroll identity, all progress stages/status/counts/fraction semantics, no fraction-only repeated announcement, and exact 5/1/1 current maxima.

- [ ] **Step 5: Run GREEN and commit**

Run the Debug screen-contract class and Release Figma/presentation/render/accessibility regressions. Commit:

```text
test(model-inspection): verify every fixture screen contract
```

---

## Task 8: Add substantive width/resource/text/motion preview presets

**Files:**

- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Presets/ModelInspectionFixturePresetApplier.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Presets/ModelInspectionFixturePreviewResources.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/ModelInspectionPage.DebugFixtures.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Presets/InspectionModelCard.FixtureResponsive.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Presets/InspectionContentCard.FixtureResponsive.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Presets/InspectionActionCard.FixtureResponsive.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/ModelInspectionFixtureSession.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/ModelInspectionFixtureHostPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/ModelInspectionFixtureGalleryPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/ModelInspectionFixtureGalleryPage.xaml.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixturePresetTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write preset-expectation RED tests**

Enumerate policy-required descriptor/preset pairs and require exact responsive profile/content bounds, no clipped/overlapping/unreachable required content, declared wrapping/truncation, bounded scroll owner and retained rows, interactive targets at least 44x44 effective pixels, unchanged logical reading/tab order, valid focus, resolved semantic brushes without color-only meaning, 200% natural reflow, and reduced-motion final-state equivalence with zero animation starts. Expected RED: preset applier/observations absent.

- [ ] **Step 2: Apply width presets through real layout**

Set exact approved desktop/medium/narrow host widths and wait LayoutUpdated/dispatcher/render boundaries; never scale a bitmap or use `Viewbox`. Because AdaptiveTriggers read XamlRoot width, add Debug-only partial methods that call the real named states on the page (`DesktopPageState`, `MediumPageState`, `NarrowPageState`) and on the model/content/action controls' corresponding production visual-state groups. The host subscribes to SizeChanged/LayoutUpdated/render boundaries, reapplies the selected exact 1440/600/360 state after each relevant change, and detaches every handler on retirement. Tests mutate each control override independently. Compare the existing responsive geometry profiles with ±1 effective-pixel tolerance only where approved layout tests already do.

- [ ] **Step 3: Apply resource and text previews locally**

Light/Dark use scoped `RequestedTheme`. High-Contrast preview materializes the existing Model Inspection HighContrast semantic dictionary into a Debug host scope. The preset applier supplies those resources and 200% doubled typography values through `CreateForFixture(..., configureResourcesBeforeInitialize)` before `InitializeComponent`, so later-realized rows also reflow. Labels must include `Preview`; no compliance badge or OS claim.

- [ ] **Step 4: Apply motion through injected settings**

Normal and reduced motion are selected before session/page construction. Normal uses the approved production driver with an audit decorator in the gallery; test construction may inject the controllable driver. Reduced uses injected disabled settings, must start zero animations, and reaches byte-equivalent observed semantics plus identical final geometry after normal motion completes.

- [ ] **Step 5: Run GREEN and commit**

Run the serialized Debug preset class and existing Release layout/motion/accessibility classes. Commit:

```text
feat(model-inspection): add fixture stress previews
```

---

## Task 9: Drive real interactions and close every lifetime/stale-event route

**Files:**

- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/ModelInspectionFixtureSession.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/DebugModelInspectionService.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/ModelInspectionFixtureHostPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/ModelInspectionFixtureGalleryPage.xaml.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureInteractionTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureLifetimeTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write interaction and retirement RED tests**

Run all four disclosure round trips plus Cancel, Retry, Restart, Choose another, Reset, fixture switch, navigation away/back replacement, invalid-selection replacement, gallery close, and Window close through actual controls/commands. Require exact expected destination/focus/footer/announcement/lifetime effects.

Release stale progress from the old reporter, submit the captured old terminal snapshot after Retry, complete an old motion callback, and replay an old announcement callback. Prove none changes the new screen, focus, footer, selection, announcement count, outgoing overlay, or retained rows.

- [ ] **Step 2: Implement only declared action dispatch**

Resolve descriptor actions to the real control/command path. Omitted actions are unavailable. Disabled `Coming later` controls remain visible but never dispatch. Unsupported calls return a rule-coded error and produce no state change.

- [ ] **Step 3: Finish exact-once retirement evidence**

Track activation, service calls, cancellation registrations, page retirements, session retirement/disposal, dispatcher callbacks, motion batches, focus requests, disclosure operations, and live notifications. Every exit route must end at exactly one page retirement and one session disposal with all pending counts zero.

- [ ] **Step 4: Prove the closed I/O and composition boundary**

Load and interact with every fixture through an injected counting `IModelInspectionFixturePackageResourceReader`; require only the schema, policy, and 49 exact package-resource reads. Source and IL contracts scan the entire Debug closure and reject `Process`, `Process.Start`, worker/service production composition, `File`, `Directory`, `FileStream`, file/folder pickers, report writers, sockets, `HttpClient`, and network types. A constructor/metadata contract proves `CreateForFixture` requires the concrete Debug service/session and that no Debug call site invokes `new ModelInspectionPage()` or `ModelInspectionServiceComposition.CreateDefault()`. This separates executable boundaries from APIs that cannot be meaningfully monkey-patched.

- [ ] **Step 5: Run GREEN and commit**

Run both Debug classes, all Debug fixture classes together, then Release page/coordinator/motion/navigation regressions and N-001. Commit:

```text
test(model-inspection): close fixture interaction lifetimes
```

---

## Task 10: Add the permanent Debug campaign, evidence contracts, and final reconciliation

**Files:**

- Modify: `.github/workflows/build-and-test.yml`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureWorkflowContractTests.cs`
- Modify: `tests/README.md`
- Modify: `tests/TestFixtures/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/README.md`
- Modify: `docs/testing/Model-Inspection-Test-Completeness-Matrix.md`
- Modify: `docs/evidence/testing/Model-Inspection-Fixture-Catalog.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write workflow/evidence RED mutations**

Require a serialized Debug+x64 packaged campaign filtered exactly to `TestCategory=ModelInspectionFixtureGallery`; exact discovered class counts and total; no overlap with the existing Release hosted-equivalent campaign; no raw TRX/artifact upload; Release filter/floor/protected counts unchanged except independently discovered legitimate Release tests. Require Release isolation script success before workflow completion.

Mutation tests remove/change the category, configuration, platform, serialization, exact count, package fixture set, Release isolation step, or raw-TRX prohibition. Expected RED: permanent Debug campaign absent.

- [ ] **Step 2: Add and discover the Debug campaign floor**

Build Debug x64, run the exact category, parse the fresh TRX, and pin the observed exact total plus per-class counts in workflow and contracts in the same change. This is an evidence-derived value, not a guessed count. Keep the existing Release 686 filter/floor until a fresh Release run proves a real count change; Debug-only test files must not change it.

- [ ] **Step 3: Reconcile truthful documentation**

Document how to open the gallery (Debug, x64, launch packaged app, click `Fixture gallery`), the exact 49-file catalogue, filename rule, synthetic provenance, supported safe interactions, preset preview nonclaims, fixed package I/O, and N-001 external linkage. Keep strict Figma PNG, actual OS High Contrast/200%, Narrator, and real-worker coverage for synthetic outcomes explicitly open.

- [ ] **Step 4: Run the final ladder**

Run, serialized:

1. fixture contract classes;
2. complete Contracts and discover/pin its exact new floor;
3. Debug x64 `ModelInspectionFixtureGallery` campaign;
4. Release x64 hosted-equivalent permanent filter;
5. focused page/presentation/layout/motion/accessibility/navigation regressions;
6. N-001 real production-worker/page journey;
7. Release-isolation script;
8. cleanup verifier 3/3;
9. Release x64 app build and Debug x64 app/test build;
10. `git diff --check`, conflict scan, privacy/path scan, process-orphan scan, staged-scope audit.

- [ ] **Step 5: Request independent reviews and commit only after approval**

Freeze the exact staged Git tree and obtain separate spec and quality reviews. Resolve every Critical/Important finding with mutation-first RED/GREEN and rerun affected/full gates. Commit with the honest final subject:

```text
feat(model-inspection): add complete debug fixture gallery
```

## Final completion report

Report:

- exact commit/tree and path count;
- exact catalogue count and ID range;
- Debug campaign, Release suite, Contracts, N-001, cleanup, and build counters;
- how the user opens the gallery;
- zero Release fixture artifacts and zero worker/external-I/O evidence;
- the explicit remaining strict Figma/controlled-OS/Narrator blockers.
