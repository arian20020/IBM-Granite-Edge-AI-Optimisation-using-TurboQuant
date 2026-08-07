# Model Inspection Phase 1 WinUI and Application Contracts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Clean the Model Inspection WinUI, navigation handoff, and application-contract boundary without changing its approved visible behaviour, protocol/security meaning, or Gate 2 runtime boundary.

**Architecture:** Keep the current WinUI presentation pattern rather than introducing a new ViewModel or service layer during cleanup. The page remains a thin presentation/navigation consumer, application contracts remain immutable and framework-bounded, reusable controls continue to receive presentation snapshots, and onboarding remains the owner of `Frame` navigation. Refactoring is limited to concrete problems found during the Phase 1 audit: unnecessary derived page state, correlated factory arguments, shared mutable hidden presentation state, duplicated template-selector extraction logic, missing visual-state failure checks, compiler-proven binding-mode misuse, misplaced tests, stale Phase 1 documentation, and excessive explanatory comments in files already being changed.

**Tech Stack:** C# 12 / .NET 8, WinUI 3 / Windows App SDK, XAML compiled binding (`x:Bind`), MSTest packaged WinUI tests, GitHub Actions Windows CI.

## Global Constraints

- Base commit remains `a4138a613dd643abe12858eec5d1c3beb09e95e7` through the cleanup programme.
- Phase 0 exact behaviour baseline is `502f396daa213d857d86a656a2cd31e6adb93b9d`.
- Phase 0 closure head before this plan is `8337d5861812b0f4a3867b1e2fb5889bff4e76fc`.
- Phase 0 exact-head CI run `31189122046` passed all 426 tests with zero failed and zero skipped tests.
- Worker protocol version 1, serialized JSON names, enum meanings, diagnostic-code meaning, cancellation meaning, timeout meaning, process containment, handle inheritance, environment allowlisting, evidence privacy, and production/test-fixture separation are frozen.
- No Gate 3+ inspection engine, classifier, application service, ViewModel execution loop, OpenVINO route, or packaging integration is implemented in this phase.
- Existing Model Inspection layout geometry, control order, accessibility labels, initial five-stage wording, and navigation semantics are preserved unless this plan explicitly names a defect correction.
- Do not introduce a new MVVM layer merely to satisfy a pattern. The current page/presentation split is retained because Phase 1 has no complex mutable workflow state yet.
- Prefer explicit types when the initializer does not make the type immediately obvious.
- New or rewritten code comments must explain why, security, lifecycle, or non-obvious framework behaviour. They use simple English, begin with a lowercase letter, and do not end with a full stop.
- Do not rewrite an otherwise-correct file only to change comment punctuation or formatting.
- Public/application contract constructors, properties, validation semantics, and exception meaning remain unchanged unless an exact change is named below.
- Every code refactor runs its focused characterization/regression test before and after the change.
- Every Phase 1 commit must leave the permanent cleanup inventory structurally valid.
- The final Phase 1 head must pass the complete permanent Windows `Build and test` workflow and the full packaged WinUI/application suite.

---

## Professional and textbook basis

### Project reference: `windows-apps.pdf`

The Microsoft Windows-apps reference bundled with the project was checked directly for this phase:

- pp. 283-284: `{x:Bind}` defaults to `OneTime`; `OneWay`/`TwoWay` should be chosen only when the source needs observation or write-back. It also presents data binding as a way to separate UI and data for readability, testability, and maintainability.
- p. 284: MVVM is a separation pattern, but the tutorial explicitly stops after separating view and model rather than forcing a full MVVM implementation.
- pp. 296-298: WinUI page navigation uses `Frame.Navigate`, can pass an object as the navigation parameter, and consumes it through `OnNavigatedTo`.

These points match the current project boundary: onboarding owns the `Frame`, `ModelInspectionRequest` is passed as one immutable object, and the destination page consumes it through `OnNavigatedTo`.

### Current Microsoft Learn verification

- **Windows data binding in depth**: one-time binding is intended for values that do not change in place; `Bindings.Update()` is the supported way to refresh one-time compiled bindings after a specific update and is cheaper than observing values continuously when fine-grained changes are not needed.
- **`{x:Bind}` markup extension**: compiled binding provides compile-time validation and can optionally observe a source with `OneWay` or push changes back with `TwoWay`.
- **WinUI navigation between pages**: `Frame.Navigate` is the standard page-navigation mechanism and its second parameter is the object supplied to the destination page.
- **`Page.OnNavigatedTo`**: navigation parameters should be captured there; visual-tree manipulation belongs after loading.
- **MVVM performance tips**: separation of concerns is useful, but unnecessary layers and allocations should not be introduced simply to claim MVVM.
- **C# coding conventions / identifier naming**: prefer clarity, simplicity, descriptive names, and types that a reader can understand without IDE hover information.

### Textbooks

- **Refactoring: Improving the Design of Existing Code** - behaviour-preserving small steps, self-checking tests, Extract Function, and guard-oriented simplification.
- **Code Complete: A Practical Handbook of Software Construction** - cohesive routines, defensive programming, meaningful names, limited cognitive load, and comments that add information rather than narrating obvious code.
- **The Art of Unit Testing** - trustworthy, readable tests with clear reasons to fail; characterization before structural change; test helpers should reduce noise without hiding intent.
- **Why Programs Fail** - preserve the observable cause/effect chain and reproduce the actual warning/failure before changing code.
- **Designing Secure Software** - do not weaken validated input boundaries or fail-closed behaviour during maintenance.
- **Fundamentals of Software Architecture** - preserve explicit boundaries and use tests as fitness functions rather than allowing coupling to drift.
- **Systems Engineering: Principles and Practice** - staged verification and evidence before declaring a subsystem gate complete.

---

# Phase 1 audit

## Audit rules

Every file below was reviewed against the Phase 0 baseline. `no change` means the current design is already appropriate for Phase 1 and changing it would create churn without a concrete maintainability or correctness benefit. `defer` means a real issue exists, but changing it here would alter visible behaviour, cross a later architectural gate, or require a dedicated UX/accessibility decision.

## A. Model Inspection application contracts

| File | Primary responsibility | Current strengths | Specific findings | Behaviour to preserve | Existing protection | Required characterization | Disposition |
|---|---|---|---|---|---|---|---|
| `Contracts/ExpectedModelFileIdentity.cs` | immutable expected file length/time identity | small, explicit UTC/length validation | no concrete Phase 1 defect | exact identity semantics and exceptions | `ModelInspectionContractTests` | none | reviewed / no change |
| `Contracts/ModelInspectionChatTemplateEvidence.cs` | privacy-minimized chat-template evidence | stores presence/length/digest without template text | no defect | no raw template retention | contract tests | none | reviewed / no change |
| `Contracts/ModelInspectionConfigurationEvidence.cs` | validated configuration evidence shape | constructor mirrors evidence explicitly | long constructor is justified by evidence shape; splitting would add ceremony | all optional/required field meaning | contract tests | none | reviewed / no change |
| `Contracts/ModelInspectionContractValidation.cs` | centralized guard helpers | cohesive validation and privacy-safe digest errors | no duplication worth extracting | exact exception/validation behaviour | contract tests | none | reviewed / no change |
| `Contracts/ModelInspectionEnums.cs` | application inspection vocabulary | explicit semantic enums | renaming/renumbering would cross frozen meaning | enum names and values | contract tests | none | reviewed / no change |
| `Contracts/ModelInspectionEvidence.cs` | complete application evidence aggregate | defensive copies and no runtime/native types | no defect | copy semantics and framework boundary | contract tests | none | reviewed / no change |
| `Contracts/ModelInspectionExecutionResult.cs` | mutually exclusive completed/cancelled/failure result | named factories enforce terminal-state invariants | no defect | cooperative cancellation remains distinct from forced termination | contract tests + additional tests | none | reviewed / no change |
| `Contracts/ModelInspectionFileEvidence.cs` | privacy-minimized verified file evidence | preserves file name/integrity without directory leakage | no defect | no full path in retained evidence | contract tests | none | reviewed / no change |
| `Contracts/ModelInspectionFinding.cs` | classified user-facing finding | compact validated value object | no defect | diagnostic/severity meaning | contract tests | none | reviewed / no change |
| `Contracts/ModelInspectionObservation.cs` | bounded technical observation | small and explicit | no defect | observation semantics | contract tests | none | reviewed / no change |
| `Contracts/ModelInspectionOperationalFailure.cs` | operational failure description | separates operational failure from model classification | no defect | failure/diagnostic meaning | contract tests | none | reviewed / no change |
| `Contracts/ModelInspectionProgress.cs` | five-stage progress update | validates stage/fraction and allows genuinely unknown fraction | `ExpectedStageCount = 5` is intentional and should not be generalized | five-stage contract | contract tests | none | reviewed / no change |
| `Contracts/ModelInspectionRequest.cs` | immutable handoff from validated import to inspection | full path exists only where execution needs it; file name, identity and quick-scan facts cross-check | page currently re-derives two already-validated facts from `ModelPath`; fix belongs in page, not contract | request shape/constructor/validation | request factory + contract/navigation tests | none | reviewed / no contract change |
| `Contracts/ModelInspectionResult.cs` | classified terminal result | conversion/continuation invariants explicit | no defect | continuation and conversion-route meaning | contract tests | none | reviewed / no change |
| `Contracts/ModelInspectionRuntimeIdentity.cs` | approved runtime identity/evidence | explicit closure fields | long constructor is evidence shape, not accidental complexity | identity semantics | contract tests | none | reviewed / no change |
| `Contracts/ModelInspectionTokenizerEvidence.cs` | tokenizer evidence | defensive ordinal dictionary and invariant checks | no defect | token IDs/evidence semantics | contract tests | none | reviewed / no change |
| `Contracts/ValidatedQuickScanSnapshot.cs` | bounded immutable snapshot from successful GGUF quick scan | prevents scanner-object leakage and fixes first production format to GGUF | page should use `Format` rather than re-reading extension | GGUF-only first route and quick-scan facts | contract/request tests | none | reviewed / no contract change |
| `Contracts/README.md` | explains application-contract boundary | strong privacy and ownership rationale | current-state text still references the removed/obsolete page alias and pre-Gate-2 status | contract meaning | documentation review | none | modify only directly stale Phase 1 facts; full prose cleanup deferred to Phase 7 |

## B. Model Inspection presentation models

| File | Primary responsibility | Current strengths | Specific findings | Behaviour to preserve | Existing protection | Required characterization | Disposition |
|---|---|---|---|---|---|---|---|
| `Models/InspectionActionCardMode.cs` | action-card structural mode | tiny semantic enum | no defect | `Hidden`, `Inspecting`, `Result` | control behaviour | none | reviewed / no change |
| `Models/InspectionActionCardPresentation.cs` | action-card snapshot | fixed non-null action slots keep XAML simple | no concrete defect | defaults and slot meaning | packaged UI regression | none | reviewed / no structural change |
| `Models/InspectionActionPresentation.cs` | one action/button slot | complete text/command/accessibility state | no defect | visibility/enablement/command fields | packaged UI regression | none | reviewed / no change |
| `Models/InspectionCheckPresentation.cs` | one completed inspection check | focused shape separate from progress-stage row | no defect | status/text/accessibility data | packaged regression | none | reviewed / no change |
| `Models/InspectionCheckStatus.cs` | completed-check status vocabulary | minimal enum | no defect | enum meaning | compiled usage | none | reviewed / no change |
| `Models/InspectionContentCardMode.cs` | content-card layout identity | one progress mode plus shared findings modes | no defect | mode/template mapping | selector tests | none beyond selector cases below | reviewed / no enum change |
| `Models/InspectionContentCardPresentation.cs` | content-card root presentation | mostly immutable; only disclosure state is observable | **real shared mutable default hazard:** static `Hidden` is one instance while `IsExpanded` is mutable | default values, two-way disclosure behaviour, `INotifyPropertyChanged` semantics | current selector/packaged tests | add independent-hidden/default-control tests | **modify**: return fresh hidden snapshots and ensure each control owns a fresh hidden presentation |
| `Models/InspectionContentItemPresentation.cs` | progress/finding/report row snapshot | one semantic row shape reused by three templates | immutable values do not notify, so `OneWay` template bindings are unnecessary | row fields and accessibility | progress tests | compiler warning regression below | no model change; XAML binding change only |
| `Models/InspectionContentStatus.cs` | status vocabulary for progress/findings | explicit semantic states | no defect | enum meaning | compiled usage | none | reviewed / no change |
| `Models/InspectionModelBadgeState.cs` | compact model badge semantics | explicit states | no defect | badge mapping | packaged regression | visual-state/binding regression only | reviewed / no enum change |
| `Models/InspectionModelCardMode.cs` | compact/detailed layout mode | minimal enum | no defect | compact/detailed meaning | packaged regression | missing-state guard test below | reviewed / no enum change |
| `Models/InspectionModelCardPresentation.cs` | model-card snapshot | immutable default and non-null check list | no defect | snapshot fields/defaults | packaged regression | compiler warning regression below | reviewed / no model change |
| `Models/InspectionOutcomePresentation.cs` | outcome-banner snapshot | separates semantic outcome kind from tone | no defect | kind/tone/text/accessibility | packaged regression | missing-tone-state guard test below | reviewed / no model change |
| `Models/InspectionOutcomePresentationKind.cs` | semantic outcome identity | explicit result set | no defect | enum meaning | compiled usage | none | reviewed / no change |
| `Models/InspectionOutcomeTone.cs` | visual tone identity | allows several outcomes to share styling | no defect | tone mapping | packaged regression | missing-state guard below | reviewed / no enum change |
| `Models/README.md` | explains presentation model design | documents snapshot strategy and existing risk | explicitly records the shared mutable `Hidden` problem; test path becomes stale after selector-test move | architectural rationale | documentation review | none | update only the resolved risk and moved test path; broader doc polish Phase 7 |

## C. Model Inspection presentation construction

| File | Primary responsibility | Current strengths | Specific findings | Behaviour to preserve | Existing protection | Required characterization | Disposition |
|---|---|---|---|---|---|---|---|
| `Presentation/InitialInspectionProgressPresentationFactory.cs` | constructs the initial five-stage progress snapshot | centralizes exact approved wording/order | private helper takes eight parameters even though status/status text/detail visibility are determined by `isActive` | exact titles, details, statuses, connectors, automation names and `StageCount = 5` | `InitialInspectionProgressPresentationTests` | add detail-visibility and automation-name characterization | **modify**: derive correlated values inside helper |
| `Presentation/README.md` | explains presentation construction boundary | correctly places construction outside runtime adapter | branch/current-state wording is stale | presentation/runtime separation | documentation review | none | minimal touched-fact update; broad docs Phase 7 |

## D. Model Inspection reusable controls and XAML

| File | Primary responsibility | Current strengths | Specific findings | Behaviour to preserve | Existing protection | Required characterization | Disposition |
|---|---|---|---|---|---|---|---|
| `Controls/InspectionActionCard.xaml` | inspecting/result action layouts | stable named visual states and accessibility | no functional defect found; large comment-only cleanup would create noisy XAML churn | geometry, state names, actions | packaged regression | none | reviewed / no XAML change |
| `Controls/InspectionActionCard.xaml.cs` | presentation DP and required action visual-state application | already fails fast if XAML state is missing | inline comments narrate obvious lines | exact state mapping, `Bindings.Update()`, fail-fast behaviour | new shared guard test can include this control | characterize existing missing-state failure | modify comments only while adjacent guard tests are added |
| `Controls/InspectionContentCard.xaml` | typed progress/findings/report templates | good template reuse; text status prevents color-only meaning | compiler reports **19 WMC1506 locations** caused by `OneWay` on immutable snapshot values/functions | all geometry/text/accessibility; `IsExpanded` remains `TwoWay`; disclosure text must still update | packaged tests + compiler | build warning baseline and zero-warning check | **modify** only compiler-proven immutable bindings and nearby comment noise |
| `Controls/InspectionContentCard.xaml.cs` | presentation DP, visibility and status presentation helpers | centralized bounded helper functions and explicit status semantics | shared hidden default is passed through DP metadata; constructor currently leaves controls on same instance | status mapping and card visibility | new hidden-default tests | independent control defaults | **modify** constructor/default fallback handling; preserve current fixed brushes for behaviour stability |
| `Controls/InspectionContentTemplateSelector.cs` | maps content presentation mode to progress/findings template across WinUI selector entry routes | explicitly handles bootstrap/null routes | duplicated item/container + ContentControl/ContentPresenter extraction branches | null bootstrap -> progress; progress -> progress; all non-progress -> findings; missing template throws | selector tests | add ContentPresenter item + ContentControl container + ContentPresenter container routes | **modify** with one small extraction helper |
| `Controls/InspectionModelCard.xaml` | compact/detailed model presentation | strong layout and accessible non-color status text | compiler reports **27 WMC1506 locations** on immutable forwarding/check bindings | compact/detailed geometry; `IsInspectionDetailsExpanded` remains `TwoWay` | packaged regression + compiler | build warning baseline and zero-warning check | **modify** compiler-proven immutable bindings and nearby comments only |
| `Controls/InspectionModelCard.xaml.cs` | presentation DP, expansion state, badge/check formatting and visual-state selection | control-local expansion state is a good pattern; theme-resource lookup fails clearly | `GoToState` result is ignored, unlike ActionCard; some local `var` declarations reduce clarity; fixed ARGB check brushes are known theme debt | state names, badge/check mapping, expansion reset, `Bindings.Update()` | new guard tests | missing `DetailedState` must fail clearly | **modify** fail-fast state check and touched readability; fixed brush theming deferred |
| `Controls/InspectionOutcomeCard.xaml` | themed outcome banner | dedicated Light/Dark/HighContrast dictionaries and semantic visual states | no compiler warning defect found; large comments are harmless and a pure XAML cleanup would be churn | visual geometry/tone resources/accessibility | packaged regression | state guard is code-behind test | reviewed / no XAML change |
| `Controls/InspectionOutcomeCard.xaml.cs` | outcome visibility and tone-state selection | compact switch from semantic tone to XAML state | `GoToState` result is ignored; comments narrate obvious operations | hidden visibility and exact tone mapping | new guard tests | missing `SuccessTone` must fail clearly | **modify** fail-fast state check and touched comments |
| `Controls/README.md` | control contracts and known limitations | detailed ownership/visual-state documentation | selector test link is in wrong feature folder; baseline/status wording needs targeted correction | ownership and warning/debt notes | documentation review | none | update test path/resolved items only; fixed-brush UX debt remains explicitly deferred |

### Deliberate Phase 1 deferral: fixed ARGB status/check brushes

`InspectionContentCard.xaml.cs` and `InspectionModelCard.xaml.cs` contain fixed status/check brushes while other resources use theme dictionaries. This is a real theming/accessibility debt, but changing it would intentionally alter Dark/HighContrast rendering and therefore is **not** a behaviour-preserving cleanup. It remains recorded for a dedicated UX/accessibility decision instead of being silently changed here.

## E. Model Inspection page

| File | Primary responsibility | Current strengths | Specific findings | Behaviour to preserve | Existing protection | Required characterization | Disposition |
|---|---|---|---|---|---|---|---|
| `ModelInspectionPage.xaml` | page composition and initial explanatory UI | page is composition-only and exposes four named reusable controls | visible typo `runtime is support` exists, but fixing user-facing copy would change approved visible behaviour; comment noise alone does not justify a large XAML diff | requested theme, geometry, control order, current wording | packaged regression | none | reviewed / no XAML change; copy defect explicitly deferred |
| `ModelInspectionPage.xaml.cs` | capture immutable navigation request and apply initial presentations after `Loaded` | correctly uses `OnNavigatedTo` for request capture and `Loaded` for control state | redundant `SelectedModelPath`; re-parses file name/extension from raw path even though request already validates `FileName` and `QuickScan.Format`; comments over-explain | exact request object, load guard, initial card values, no runtime/process logic | page + onboarding navigation tests | exact-request assertions already exist; update tests to remove alias dependency | **modify**: remove alias, pass request to initial state, use validated request facts, simplify comments |
| `README.md` | feature status/boundary documentation | contains strong separation/non-claim material | materially stale: describes Gate 2 worker/process work as future and documents `SelectedModelPath` | accurate implementation boundary and non-claims | documentation review | none | **modify** current-state facts only; broad prose normalization Phase 7 |

### Deliberate Phase 1 deferral: visible copy typo

The sentence containing `runtime is support` is grammatically incorrect. It is recorded, not ignored. Because the approved cleanup design freezes visible UI behaviour unless a change is explicitly approved as a UX correction, this Phase 1 plan does **not** silently alter the wording. It can be handled in the later UX/docs phase or as a separately approved copy fix.

## F. Model Import handoff rows

| File | Primary responsibility | Current strengths | Specific findings | Behaviour to preserve | Existing protection | Required characterization | Disposition |
|---|---|---|---|---|---|---|---|
| `ModelImport/Controls/ImportModelCard.xaml` | imported-model visual card | downstream continue state remains page-owned | only references inspection context; no Phase 1 handoff defect | current card UI | `ImportModelCardTests` | none | reviewed / no change |
| `ModelImport/Controls/README.md` | import-card documentation | boundary is understandable | broad docs cleanup belongs Phase 7 | documented ownership | docs review | none | reviewed / no change |
| `ModelImport/FileImport/PickerRoute/README.md` | picker-route documentation | distinguishes file selection from inspection | no Phase 1 defect | route boundary | docs review | none | reviewed / no change |
| `ModelImport/FileImport/README.md` | file-import documentation | separates picker/import/scan responsibilities | no Phase 1 defect | boundary text | docs review | none | reviewed / no change |
| `ModelImport/ModelDownload/ModelDownloadCard.xaml` | recommended-download card | does not own inspection navigation | reference to later inspection does not create coupling | current UI | `ModelDownloadCardTests` | none | reviewed / no change |
| `ModelImport/ModelDownload/README.md` | download-card documentation | keeps download separate from local validation | no Phase 1 defect | documented scope | docs review | none | reviewed / no change |
| `ModelImport/ModelImportPage.xaml` | model-import page composition and continue button | continue action remains disabled until validated model exists | no handoff XAML defect; stale comments elsewhere are outside Phase 1 | control order and continue gating | composition/navigation tests | none | reviewed / no change |
| `ModelImport/ModelImportPage.xaml.cs` | import state machine and raises inspection request event | stale-result protection, cancellation and event handoff are strong | handoff comments are verbose but code is cohesive; no structural change justified | request is revalidated immediately before event; page never manipulates onboarding `Frame` | navigation + state-machine tests | none | reviewed / no functional change |
| `ModelImport/ModelInspectionRequestFactory.cs` | fail-closed conversion from validated scan + current file identity into request | reopens file, excludes concurrent writer, verifies length, catches only expected filesystem/security exceptions | repeated catch/false branches are explicit and security-readable; collapsing them would reduce clarity | exact fail-closed behaviour and exception handling | `ModelInspectionRequestFactoryTests` | none | reviewed / no change |
| `ModelImport/ModelInspectionRequestedEventArgs.cs` | typed event payload | tiny and clear | no defect | exact request reference | navigation tests | none | reviewed / no change |
| `ModelImport/QuickScan/README.md` | quick-scan documentation | clearly separates bounded quick scan from full inspection | no Phase 1 code issue | boundary | docs review | none | reviewed / no change |
| `ModelImport/README.md` | Model Import architecture documentation | describes handoff and validation | broad documentation cleanup deferred | handoff semantics | docs review | none | reviewed / no change |

## G. Onboarding navigation rows

| File | Primary responsibility | Current strengths | Specific findings | Behaviour to preserve | Existing protection | Required characterization | Disposition |
|---|---|---|---|---|---|---|---|
| `Onboarding/Controls/OnboardingStageIndicator.xaml` | five-stage progress indicator UI | clear responsive geometry and polite live region | no Model Inspection handoff defect | all stage labels/geometry/accessibility | stage-indicator tests | none | reviewed / no change |
| `Onboarding/Controls/OnboardingStageIndicator.xaml.cs` | maps `OnboardingStage` to visual progress | validates undefined values and restores prior state; accessibility notification is explicit | repeated `5` is minor but changing already-clean, heavily-tested code creates scope creep | stage mapping and live region | thorough stage-indicator tests | none | reviewed / no change |
| `Onboarding/Controls/README.md` | onboarding control docs | ownership clear | no direct Phase 1 defect | current docs | docs review | none | reviewed / no change |
| `Onboarding/OnboardingShellPage.xaml` | hosts stage indicator and stage `Frame` | simple two-region composition | no defect | layout | shell tests | none | reviewed / no change |
| `Onboarding/OnboardingShellPage.xaml.cs` | owns stage navigation and import-page event subscription | correct owner of `Frame.Navigate`; stage changes only after successful navigation | no structural defect; tests currently depend indirectly on removed page alias | exact same request passed to destination, event detach/attach, stage timing | onboarding navigation tests | update assertion to use `Request` only | no production change |
| `Onboarding/OnboardingStage.cs` | explicit five-stage onboarding enum | readable fixed progression | no defect | values/order | stage-indicator tests | none | reviewed / no change |
| `Onboarding/README.md` | onboarding ownership docs | documents shell as navigation owner | broad docs work belongs Phase 7 | ownership description | docs review | none | reviewed / no change |

The existing navigation design is intentionally retained. Microsoft WinUI guidance uses `Frame.Navigate(..., parameter)` for page-to-page parameter transfer, and `OnNavigatedTo` for consuming that parameter. Moving navigation into `ModelInspectionPage` or letting Model Import manipulate the shell `Frame` would increase coupling rather than clean it.

## H. Affected packaged WinUI/application tests

| Test file | What it protects | Audit finding | Phase 1 disposition |
|---|---|---|---|
| `Features/ModelImport/Controls/ImportModelCardTests.cs` | import-card state/visual behaviour | unrelated to proposed changes but part of handoff regression surface | run unchanged |
| `Features/ModelImport/Controls/ModelImportPageStateMachineTests.cs` | import scan/cancel/stale-result state | strong state-machine protection | run unchanged |
| `Features/ModelImport/FileImport/ModelFilePickerTests.cs` | picker route | no Phase 1 change | run unchanged |
| `Features/ModelImport/ModelDownload/ModelDownloadCardTests.cs` | recommended card | no Phase 1 change | run unchanged |
| `Features/ModelImport/ModelImportNavigationRequestTests.cs` | continue gating, exact request event, mutation/deletion revalidation | strong handoff protection | run unchanged |
| `Features/ModelImport/ModelImportPageCompositionTests.cs` | page composition | no Phase 1 change | run unchanged |
| `Features/ModelImport/ModelInspectionRequestFactoryTests.cs` | fail-closed request construction | strong edge-case coverage | run unchanged |
| `Features/ModelInspection/InitialInspectionProgressPresentationTests.cs` | five-stage initial tracker | missing detail-visibility and automation-name characterization | **modify** |
| `Features/ModelInspection/ModelInspectionContractTests.cs` | application contracts | comprehensive contract invariants | run unchanged |
| `Features/ModelInspection/ModelInspectionExecutionResultAdditionalTests.cs` | extra terminal-result invariants | focused and clear | run unchanged |
| `Features/ModelInspection/ModelInspectionPageNavigationTests.cs` | exact request navigation | currently asserts redundant `SelectedModelPath` alias | **modify** to protect exact request only |
| `Features/Onboarding/Controls/InspectionContentTemplateSelectorTests.cs` | selector bootstrap/routes | **misplaced under Onboarding** and missing three wrapper/container routes | **move to `Features/ModelInspection/Controls/` and extend** |
| `Features/Onboarding/Controls/OnboardingStageIndicatorTests.cs` | stage geometry/accessibility/recovery | thorough; no Phase 1 production change | run unchanged |
| `Features/Onboarding/OnboardingEntryPointTests.cs` | onboarding entry | no Phase 1 production change | run unchanged |
| `Features/Onboarding/OnboardingModelInspectionNavigationTests.cs` | shell handoff to page | currently asserts page alias in addition to exact request | **modify** to assert exact request only |
| `Features/Onboarding/OnboardingShellPageTests.cs` | shell composition | no Phase 1 production change | run unchanged |

### New tests required by the audit

Create:

- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Models/InspectionContentCardPresentationTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionVisualStateGuardTests.cs`

No new generic test helper is introduced. The tests are small enough to keep their setup local and obvious.

---

# Exact implementation sequence

## Task 1: Move and complete template-selector characterization

**Files:**
- Move: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/Controls/InspectionContentTemplateSelectorTests.cs`
  → `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentTemplateSelectorTests.cs`
- Modify after move: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentTemplateSelectorTests.cs`
- Production later in Task 2: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentTemplateSelector.cs`

**Interfaces:**
- Consumes: `InspectionContentTemplateSelector.SelectTemplate(object item)` and `SelectTemplate(object item, DependencyObject container)`
- Preserves: null/bootstrap -> progress template; `Progress` -> progress; all completed/non-ready modes -> findings

- [ ] **Step 1: Move the test to the feature it actually tests without changing its namespace or assertions**

The class remains `GraniteEdgeAI.UnitTests.InspectionContentTemplateSelectorTests`; only repository ownership changes.

- [ ] **Step 2: Run the moved class before adding new cases**

Build the packaged test project, then run the class through the same Visual Studio app-container runner used by CI with a `FullyQualifiedName` filter.

Expected: the existing five tests pass unchanged.

- [ ] **Step 3: Add the missing wrapper/container characterization cases**

Add these tests before production refactoring:

```csharp
[UITestMethod]
[TestCategory("WinUI")]
public void SelectTemplate_WithContentPresenterAsItem_UsesPresenterContent()
{
    DataTemplate progressTemplate = new();
    DataTemplate findingsTemplate = new();
    InspectionContentTemplateSelector selector =
        CreateSelector(progressTemplate, findingsTemplate);
    ContentPresenter presenter = new()
    {
        Content = CreateProgressPresentation()
    };

    DataTemplate selectedTemplate = selector.SelectTemplate(presenter);

    Assert.AreSame(progressTemplate, selectedTemplate);
}

[UITestMethod]
[TestCategory("WinUI")]
public void SelectTemplate_WithContentControlContainer_UsesContainerContent()
{
    DataTemplate progressTemplate = new();
    DataTemplate findingsTemplate = new();
    InspectionContentTemplateSelector selector =
        CreateSelector(progressTemplate, findingsTemplate);
    ContentControl container = new()
    {
        Content = CreateProgressPresentation()
    };

    DataTemplate selectedTemplate = selector.SelectTemplate(new object(), container);

    Assert.AreSame(progressTemplate, selectedTemplate);
}

[UITestMethod]
[TestCategory("WinUI")]
public void SelectTemplate_WithContentPresenterContainer_UsesContainerContent()
{
    DataTemplate progressTemplate = new();
    DataTemplate findingsTemplate = new();
    InspectionContentTemplateSelector selector =
        CreateSelector(progressTemplate, findingsTemplate);
    ContentPresenter container = new()
    {
        Content = CreateProgressPresentation()
    };

    DataTemplate selectedTemplate = selector.SelectTemplate(new object(), container);

    Assert.AreSame(progressTemplate, selectedTemplate);
}
```

- [ ] **Step 4: Run all eight selector tests**

Expected: PASS before refactoring. These are characterization tests; they prove the current behavior we will simplify.

- [ ] **Step 5: Commit the test ownership/characterization change**

Suggested commit:

```text
test(model-inspection): complete selector route characterization
```

---

## Task 2: Simplify `InspectionContentTemplateSelector` without changing routes

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentTemplateSelector.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentTemplateSelectorTests.cs`

**Interfaces:**
- Produces no new public API.
- `ProgressTemplate` and `FindingsTemplate` properties remain unchanged.
- Both `SelectTemplateCore` overrides remain unchanged.

- [ ] **Step 1: Confirm all selector characterization tests are green**

Expected: 8/8 selector tests pass.

- [ ] **Step 2: Replace duplicated wrapper extraction with one helper**

Use this shape:

```csharp
private static InspectionContentCardPresentation? ResolvePresentation(
    object? item,
    DependencyObject? container)
{
    return ResolvePresentation(item)
        ?? ResolvePresentation(container);
}

private static InspectionContentCardPresentation? ResolvePresentation(
    object? candidate)
{
    return candidate switch
    {
        InspectionContentCardPresentation presentation => presentation,
        ContentControl
        {
            Content: InspectionContentCardPresentation presentation
        } => presentation,
        ContentPresenter
        {
            Content: InspectionContentCardPresentation presentation
        } => presentation,
        _ => null
    };
}
```

Keep the existing bootstrap fallback to `Progress` and the existing required-template exception behavior. Do not introduce reflection, `DataContext` probing, or another selector abstraction.

- [ ] **Step 3: Remove only comments that narrate the obvious branch mechanics**

Keep the reason for the null/bootstrap route because it documents non-obvious WinUI behavior.

- [ ] **Step 4: Run the eight selector tests again**

Expected: 8/8 pass.

- [ ] **Step 5: Run the full packaged WinUI/application suite**

Expected: all packaged tests pass; no selector regression.

- [ ] **Step 6: Commit**

Suggested commit:

```text
refactor(model-inspection): simplify content template selection
```

---

## Task 3: Remove shared hidden disclosure state

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Models/InspectionContentCardPresentationTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionContentCardPresentation.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml.cs`
- Modify later documentation: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/README.md`

**Interfaces:**
- `InspectionContentCardPresentation.Hidden` keeps the same property name and type.
- `IsExpanded` keeps its current two-way/`INotifyPropertyChanged` behavior.
- `InspectionContentCard.Presentation` keeps the same dependency-property API.

- [ ] **Step 1: Add failing presentation isolation test**

```csharp
[UITestMethod]
[TestCategory("WinUI")]
public void Hidden_ReturnsIndependentExpansionState()
{
    InspectionContentCardPresentation first =
        InspectionContentCardPresentation.Hidden;
    InspectionContentCardPresentation second =
        InspectionContentCardPresentation.Hidden;

    first.IsExpanded = true;

    Assert.AreNotSame(first, second);
    Assert.IsFalse(second.IsExpanded);
}
```

- [ ] **Step 2: Add failing control-default isolation test**

```csharp
[UITestMethod]
[TestCategory("WinUI")]
public void Constructor_UsesIndependentHiddenPresentation()
{
    InspectionContentCard first = new();
    InspectionContentCard second = new();

    first.Presentation.IsExpanded = true;

    Assert.AreNotSame(first.Presentation, second.Presentation);
    Assert.IsFalse(second.Presentation.IsExpanded);
}
```

- [ ] **Step 3: Run the new tests and verify the current shared singleton causes the intended failure**

Expected: at least `Hidden_ReturnsIndependentExpansionState` fails because the current `Hidden` property returns the same mutable instance.

- [ ] **Step 4: Make `Hidden` a fresh safe snapshot**

Change only the property implementation:

```csharp
public static InspectionContentCardPresentation Hidden => new();
```

Do not change the remaining public presentation fields or remove `INotifyPropertyChanged` in this phase.

- [ ] **Step 5: Give each control instance its own hidden presentation after XAML initialization**

The constructor becomes:

```csharp
public InspectionContentCard()
{
    InitializeComponent();
    _isInitialized = true;
    Presentation = InspectionContentCardPresentation.Hidden;
}
```

The dependency-property metadata can continue to provide a construction-safe hidden object while XAML initializes; the constructor then replaces it with a per-control instance. The null setter fallback also receives a new hidden snapshot because `Hidden` is no longer a singleton.

- [ ] **Step 6: Run the two isolation tests**

Expected: PASS.

- [ ] **Step 7: Run selector tests and the full packaged suite**

Expected: unchanged disclosure/template behavior.

- [ ] **Step 8: Commit**

Suggested commit:

```text
fix(model-inspection): isolate hidden content presentation state
```

---

## Task 4: Simplify the initial progress factory with characterization first

**Files:**
- Modify tests: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/InitialInspectionProgressPresentationTests.cs`
- Modify production: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InitialInspectionProgressPresentationFactory.cs`

**Interfaces:**
- `InitialInspectionProgressPresentationFactory.StageCount` remains `5`.
- `Create()` keeps the same return type and all visible strings.
- No stage enum or backend-specific concept is introduced.

- [ ] **Step 1: Add exact detail-visibility characterization**

```csharp
[UITestMethod]
[TestCategory("WinUI")]
public void Create_ShowsDetailOnlyForTheActiveStage()
{
    InspectionContentCardPresentation presentation =
        InitialInspectionProgressPresentationFactory.Create();

    Assert.AreEqual(Visibility.Visible, presentation.Items[0].DetailVisibility);

    foreach (InspectionContentItemPresentation waitingStage in
             presentation.Items.Skip(1))
    {
        Assert.AreEqual(Visibility.Collapsed, waitingStage.DetailVisibility);
    }
}
```

- [ ] **Step 2: Add stable automation-name characterization**

```csharp
[UITestMethod]
[TestCategory("WinUI")]
public void Create_UsesTitleAndStatusForStageAutomationNames()
{
    InspectionContentCardPresentation presentation =
        InitialInspectionProgressPresentationFactory.Create();

    foreach (InspectionContentItemPresentation item in presentation.Items)
    {
        Assert.AreEqual(
            $"{item.Title}. {item.StatusText}.",
            item.AutomationName);
    }
}
```

- [ ] **Step 3: Run all initial-progress tests before refactoring**

Expected: all characterization tests pass against the current implementation.

- [ ] **Step 4: Remove correlated helper parameters**

Change the helper signature from eight arguments to five:

```csharp
private static InspectionContentItemPresentation CreateStage(
    string stageNumber,
    string title,
    string detail,
    bool isActive,
    bool showConnector)
```

Derive the correlated values once:

```csharp
InspectionContentStatus status = isActive
    ? InspectionContentStatus.Active
    : InspectionContentStatus.Waiting;
string statusText = isActive
    ? "Checking"
    : "Waiting";
Visibility detailVisibility = isActive
    ? Visibility.Visible
    : Visibility.Collapsed;
```

Then construct the same presentation values, including:

```csharp
AutomationName = $"{title}. {statusText}."
```

Each `CreateStage(...)` call keeps only `stageNumber`, `title`, `detail`, `isActive`, and `showConnector`.

- [ ] **Step 5: Keep comments only where they explain the core-inspection/backend boundary**

Remove comments such as “select the running-inspection template” and “no stage has completed” where the assignment already says that. Any rewritten `//` comments follow the project lowercase/no-full-stop rule.

- [ ] **Step 6: Run all initial-progress tests and full packaged regression**

Expected: exact strings/statuses/connectors/accessibility remain unchanged.

- [ ] **Step 7: Commit**

Suggested commit:

```text
refactor(model-inspection): simplify initial progress construction
```

---

## Task 5: Clean the page/navigation boundary around the immutable request

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingModelInspectionNavigationTests.cs`
- Regression only: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelImportNavigationRequestTests.cs`

**Interfaces:**
- `internal ModelInspectionRequest? Request { get; private set; }` remains the single page handoff state.
- `SelectedModelPath` is removed.
- `ShowInitialInspectionState` changes from `string modelPath` to `ModelInspectionRequest request`.
- `CreateInitialModelPresentation` changes from `string modelPath` to `ModelInspectionRequest request`.

- [ ] **Step 1: Update navigation tests so the request itself is the only authoritative page state**

Rename the page test:

```csharp
public void FrameNavigation_WithRequest_StoresExactRequest()
```

Keep:

```csharp
Assert.IsTrue(navigationSucceeded);
Assert.IsNotNull(inspectionPage);
Assert.AreSame(request, inspectionPage.Request);
```

Delete the assertion against `inspectionPage.SelectedModelPath`.

In `OnboardingModelInspectionNavigationTests`, retain the `Assert.AreSame(request, inspectionPage.Request)` assertion and remove any dependency on `SelectedModelPath`.

- [ ] **Step 2: Run both navigation test classes before production change**

Expected: PASS. These tests already establish the exact object handoff that must survive the refactor.

- [ ] **Step 3: Remove the redundant derived alias**

Delete:

```csharp
internal string? SelectedModelPath => Request?.ModelPath;
```

No replacement property is added.

- [ ] **Step 4: Pass the complete request into initial presentation composition**

Change:

```csharp
ShowInitialInspectionState(request.ModelPath);
```

to:

```csharp
ShowInitialInspectionState(request);
```

Change the method signature to:

```csharp
private void ShowInitialInspectionState(ModelInspectionRequest request)
```

and call:

```csharp
InspectionModelCardControl.Presentation =
    CreateInitialModelPresentation(request);
```

- [ ] **Step 5: Stop re-parsing facts already validated by the request**

Change the model-presentation helper signature to:

```csharp
private static InspectionModelCardPresentation CreateInitialModelPresentation(
    ModelInspectionRequest request)
```

Use:

```csharp
string modelFileName = request.FileName;
string formatName = request.QuickScan.Format;
```

Delete `using System.IO;` from the page. Do not substitute `QuickScan.ModelName` for `FileName`; the current visible card intentionally shows the selected file name and this phase preserves that behavior.

- [ ] **Step 6: Keep the current lifecycle split**

`OnNavigatedTo` continues to capture/validate the parameter. `Loaded` continues to apply visual presentations because current Microsoft WinUI guidance states that `OnNavigatedTo` occurs before the visual tree is loaded.

Do not move control mutation into `OnNavigatedTo` and do not introduce a ViewModel during this cleanup.

- [ ] **Step 7: Reduce only obvious narration comments in this touched file**

Keep comments explaining the `Loaded` re-entry guard and the reason the validated request is preserved. Remove comments that simply restate assignments.

- [ ] **Step 8: Run page, onboarding and Model Import navigation tests**

Expected: all pass; exact same request object travels Model Import -> onboarding -> Model Inspection page.

- [ ] **Step 9: Run full packaged regression**

Expected: all packaged tests pass.

- [ ] **Step 10: Commit**

Suggested commit:

```text
refactor(model-inspection): use request as page source of truth
```

---

## Task 6: Remove compiler-proven unnecessary `OneWay` bindings

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml`
- Verify code-behind refresh points in:
  - `InspectionContentCard.xaml.cs`
  - `InspectionModelCard.xaml.cs`

**Interfaces:**
- No presentation property, dependency property, control name, template name, accessibility value, visual-state name, or command changes.
- `IsExpanded` and `IsInspectionDetailsExpanded` remain the only two-way mutable disclosure bindings.

### Baseline warning to reproduce

The Phase 0 exact-head build contains 46 distinct `WMC1506` locations across these two XAML files. The test-project build also emits one separate existing `NETSDK1198` publish-profile warning, which is outside this Phase 1 Model Inspection scope and must not be confused with the binding warnings.

- [ ] **Step 1: Run a Release x64 WinUI build and capture the warning log before editing**

Use the same application build shape as permanent CI:

```powershell
msbuild "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj" `
  /target:Build `
  /maxCpuCount `
  /verbosity:minimal `
  /property:Configuration=Release `
  /property:Platform=x64 `
  /property:RuntimeIdentifier=win-x64 `
  /property:PublishProfile= `
  /property:PublishTrimmed=false `
  /property:PublishReadyToRun=false `
  /property:AppxPackageSigningEnabled=false `
  /property:GenerateAppxPackageOnBuild=false 2>&1 |
  Tee-Object -FilePath phase1-winui-build-before.log
```

Then:

```powershell
$wmcWarnings = @(
  Select-String `
    -Path phase1-winui-build-before.log `
    -Pattern 'WMC1506'
)

if ($wmcWarnings.Count -eq 0) {
    throw 'Expected the Phase 0 WMC1506 baseline before binding cleanup'
}
```

Expected: baseline quality check is RED because `WMC1506` is present.

- [ ] **Step 2: Fix only compiler-reported bindings in `InspectionContentCard.xaml`**

For the compiler-reported bindings whose source values are immutable `InspectionContentItemPresentation`/snapshot properties, remove `Mode=OneWay` so compiled binding uses its default `OneTime` mode.

Example before:

```xml
Background="{x:Bind local:InspectionContentCard.GetStatusBackground(Status), Mode=OneWay}"
```

After:

```xml
Background="{x:Bind local:InspectionContentCard.GetStatusBackground(Status)}"
```

Do **not** change:

```xml
IsExpanded="{x:Bind IsExpanded, Mode=TwoWay}"
```

Do not remove observation from a function binding if the compiler proves it depends on mutable `IsExpanded`.

- [ ] **Step 3: Fix only compiler-reported bindings in `InspectionModelCard.xaml`**

Remove `Mode=OneWay` from forwarding values/functions that change only when the complete `Presentation` snapshot is replaced and are already refreshed by `Bindings.Update()`.

Example before:

```xml
Text="{x:Bind ModelName, Mode=OneWay}"
```

After:

```xml
Text="{x:Bind ModelName}"
```

Keep:

```xml
IsExpanded="{x:Bind IsInspectionDetailsExpanded, Mode=TwoWay}"
```

- [ ] **Step 4: Rebuild with the same command and make the warning gate green**

```powershell
$wmcWarnings = @(
  Select-String `
    -Path phase1-winui-build-after.log `
    -Pattern 'WMC1506'
)

if ($wmcWarnings.Count -ne 0) {
    $wmcWarnings | ForEach-Object { Write-Host $_.Line }
    throw 'Model Inspection WMC1506 bindings remain after cleanup'
}
```

Expected: **0 WMC1506 warnings**.

This follows current Microsoft guidance: when data is replaced only at specific actions rather than changing fine-grained in place, use one-time compiled bindings and call `Bindings.Update()` at the replacement point instead of paying for `OneWay` observation that cannot receive notifications.

- [ ] **Step 5: Run selector, hidden-state and full packaged tests**

Expected: bindings still render the replacement snapshots and both expanders remain interactive.

- [ ] **Step 6: Commit**

Suggested commit:

```text
refactor(model-inspection): align compiled binding modes with snapshot state
```

---

## Task 7: Make required visual-state drift fail fast consistently

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionVisualStateGuardTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml.cs`
- Touched-comment cleanup: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml.cs`

**Interfaces:**
- Visual state names remain exactly:
  - Action: `InspectingState`, `ResultState`
  - Model: `CompactState`, `DetailedState`
  - Outcome: `SuccessTone`, `WarningTone`, `InformationTone`, `ErrorTone`, `NeutralTone`
- No new shared visual-state utility is introduced; each control remains self-contained.

- [ ] **Step 1: Add helper in the test file that removes one named state from one group**

```csharp
private static void RemoveVisualState(
    FrameworkElement layoutRoot,
    string groupName,
    string stateName)
{
    VisualStateGroup group = VisualStateManager
        .GetVisualStateGroups(layoutRoot)
        .Single(candidate => candidate.Name == groupName);
    VisualState state = group.States
        .Single(candidate => candidate.Name == stateName);

    group.States.Remove(state);
}
```

- [ ] **Step 2: Characterize the already-correct ActionCard failure behavior**

Create the control, remove `ResultState` from `LayoutRoot` / its action layout group, then assign a `Result` presentation. Assert `InvalidOperationException` and that the message identifies `ResultState`.

Expected before production edits: PASS.

- [ ] **Step 3: Add failing ModelCard state-drift test**

Create an `InspectionModelCard`, remove `DetailedState` from `DisplayModeStates`, assign:

```csharp
new InspectionModelCardPresentation
{
    DisplayMode = InspectionModelCardMode.Detailed
}
```

Assert `InvalidOperationException` containing `DetailedState`.

Expected before fix: FAIL because current code ignores the `false` return from `VisualStateManager.GoToState`.

- [ ] **Step 4: Add failing OutcomeCard state-drift test**

Create an `InspectionOutcomeCard`, remove `SuccessTone` from `OutcomeToneStates`, assign:

```csharp
new InspectionOutcomePresentation
{
    Kind = InspectionOutcomePresentationKind.Ready,
    Tone = InspectionOutcomeTone.Success,
    Title = "Ready",
    Message = "Ready",
    AutomationName = "Ready"
}
```

Assert `InvalidOperationException` containing `SuccessTone`.

Expected before fix: FAIL for the same reason.

- [ ] **Step 5: Add the same local fail-fast pattern to ModelCard**

```csharp
bool stateApplied = VisualStateManager.GoToState(
    this,
    stateName,
    false);

if (!stateApplied)
{
    throw new InvalidOperationException(
        $"The model-card visual state '{stateName}' was not found.");
}
```

- [ ] **Step 6: Add the same local fail-fast pattern to OutcomeCard**

```csharp
bool stateApplied = VisualStateManager.GoToState(
    this,
    stateName,
    false);

if (!stateApplied)
{
    throw new InvalidOperationException(
        $"The outcome-card visual state '{stateName}' was not found.");
}
```

- [ ] **Step 7: Clean touched inline comments in all three control code-behind files**

Keep only comments that explain XAML/code state-contract drift, lifecycle timing, binding refresh, or another non-obvious reason. Do not create a base control or generic state helper for three short call sites.

- [ ] **Step 8: Run all visual-state guard tests**

Expected: Action, Model and Outcome missing-state cases all pass with clear failure messages.

- [ ] **Step 9: Run full packaged regression**

Expected: normal XAML states exist and no production control throws.

- [ ] **Step 10: Commit**

Suggested commit:

```text
fix(model-inspection): fail fast on missing visual states
```

---

## Task 8: Refresh only Phase 1 documentation that became incorrect

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Contracts/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/README.md`

**Interfaces:** documentation only.

- [ ] **Step 1: Correct Gate 2 status without claiming later gates are done**

The root README must say the protected worker/process boundary exists, but the production worker still uses the controlled unavailable inspection engine and the real classifier/service/UI execution path is not connected.

- [ ] **Step 2: Remove documentation of `SelectedModelPath`**

`Request` is the page's only authoritative navigation state.

- [ ] **Step 3: Update presentation-model documentation**

Remove the resolved shared-singleton warning and explain that `Hidden` returns a fresh safe snapshot while each `InspectionContentCard` installs its own hidden presentation instance.

- [ ] **Step 4: Update selector-test path**

Point controls/models documentation to:

```text
tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentTemplateSelectorTests.cs
```

- [ ] **Step 5: Keep deferred issues explicit**

Do not erase the fixed status/check brush theming debt or the fact that final runtime-driven presentation factories/ViewModel state are later work.

- [ ] **Step 6: Do not perform the full Phase 7 documentation rewrite early**

Only text made wrong by Phase 1 or already materially wrong about Gate 2 is changed.

- [ ] **Step 7: Commit**

Suggested commit:

```text
docs(model-inspection): align Phase 1 boundary documentation
```

---

## Task 9: Close the Phase 1 inventory and exact-head evidence gate

**Files:**
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`
- Create: `docs/testing/evidence/2026-08-07-model-inspection-cleanup-phase-1.md`
- Modify: `docs/testing/evidence/README.md`
- The newly created Phase 1 plan, new tests and evidence record must be folded back into the permanent inventory using true ordinal ordering.

**Interfaces:** cleanup governance only.

- [ ] **Step 1: Update every audited Phase 1 ledger row**

For each row, record:

- its real primary responsibility;
- concrete findings or `no issues found`;
- `no change`, changed paths, or moved test path;
- exact behavior preserved;
- focused test class(es);
- exact commit/run evidence;
- deferred work with the reason, not a vague `later` marker.

Files reviewed and intentionally unchanged are still closed as reviewed; they are not left `pending review`.

- [ ] **Step 2: Regenerate the source list with ordinal sorting**

Include the Phase 1 plan, new test files, moved selector test path and Phase 1 evidence file. Remove the old selector-test path after the move. Exclude `bin/` and `obj/` as before.

- [ ] **Step 3: Run the permanent inventory verifier**

```powershell
pwsh -NoProfile -File scripts/model-inspection/Verify-ModelInspectionCleanupInventory.ps1
```

Expected: PASS with source list and ledger exactly aligned.

- [ ] **Step 4: Build application and packaged tests from clean Release x64 inputs**

Use the permanent workflow command set rather than a simplified local approximation.

Expected:

- WinUI application build succeeds;
- packaged test project builds;
- no `WMC1506` remains;
- the unrelated `NETSDK1198` baseline warning, if still emitted by the test-project dependency build, is recorded as out-of-scope rather than misreported as a Model Inspection binding warning.

- [ ] **Step 5: Run all permanent test layers**

At minimum:

```text
Model Inspection contract project
Transport project
Worker host project
WorkerClient project
Worker-process integration project
Packaged WinUI/application project
```

The contract minimum floor must be updated only if Phase 1 genuinely adds contract-project tests. This plan does not currently require a new contract-project test, so the expected floor remains 82.

- [ ] **Step 6: Re-run Gate 2 post-test security/process checks**

Require:

- no production worker process left running;
- no protocol fixture process left running;
- retained Gate 2 evidence passes the privacy scan;
- both artifact uploads succeed.

- [ ] **Step 7: Download and inspect all final TRX artifacts**

Do not rely only on the workflow badge. Record per-layer total/executed/passed/failed/skipped counts and artifact SHA-256 digests.

- [ ] **Step 8: Write the Phase 1 evidence record**

The evidence record must name:

- final Phase 1 source SHA;
- exact workflow run/job IDs;
- build result and warning result;
- every test count;
- inventory/source counts;
- artifact IDs/digests;
- orphan/privacy outcomes;
- production files changed;
- production files explicitly reviewed/no-change;
- deferred fixed-brush and visible-copy issues;
- statement that protocol/security/runtime meaning remained unchanged.

- [ ] **Step 9: Run one final exact-head permanent CI after evidence/index/inventory closure**

Expected: green on the final documentation/inventory head.

- [ ] **Step 10: Whole-diff review against `a4138a613dd643abe12858eec5d1c3beb09e95e7`**

Confirm no unexpected worker protocol, transport, process-containment, LLamaSharp runtime, or later-gate implementation file entered the Phase 1 production diff.

- [ ] **Step 11: Update draft PR #54 with Phase 1 context**

Add a detailed section covering:

- audit scope;
- exact cleaned files;
- why each change was made;
- red/green evidence;
- WMC1506 before/after;
- test moves/additions;
- all final test counts;
- documentation corrections;
- explicit unchanged contracts/security/process behavior;
- deferred UX/accessibility debt;
- exact final SHA/run/artifact hashes.

Do not mark the PR ready for review unless the user explicitly asks.

- [ ] **Step 12: Commit Phase 1 closure**

Suggested commit:

```text
test(model-inspection): close Phase 1 cleanup evidence
```

---

# Expected production change set

The Phase 1 implementation is intentionally smaller than the audit surface. Expected production/code files that may change are:

```text
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml.cs
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml.cs
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentTemplateSelector.cs
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml.cs
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml.cs
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionContentCardPresentation.cs
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InitialInspectionProgressPresentationFactory.cs
```

Expected documentation files that may change:

```text
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Contracts/README.md
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/README.md
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/README.md
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/README.md
```

Expected test files created/moved/changed:

```text
tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentTemplateSelectorTests.cs
tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionVisualStateGuardTests.cs
tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Models/InspectionContentCardPresentationTests.cs
tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/InitialInspectionProgressPresentationTests.cs
tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs
tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingModelInspectionNavigationTests.cs
```

The old selector-test path under `Features/Onboarding/Controls/` is removed by the move.

No application contract file is expected to change in production code after audit. That is intentional: those contracts are already cohesive, validated and strongly protected, and changing them for stylistic reasons would violate the cleanup's behavior-preserving goal.

---

# Explicit non-goals for Phase 1

Phase 1 does not:

- connect LLamaSharp to the production worker;
- add a classifier;
- add an application inspection service;
- add a Model Inspection execution ViewModel;
- add OpenVINO inspection;
- change protocol JSON;
- change any diagnostic code;
- change worker process containment;
- change cancellation or timeout behavior;
- change the original model file;
- change current five-stage wording/order;
- change visible page layout;
- change the visible `runtime is support` typo without separate UX approval;
- replace fixed status/check colors with theme-aware resources because that would alter Dark/HighContrast behavior;
- perform the full Phase 6 test-cleanup campaign;
- perform the full Phase 7 workflow/documentation-cleanup campaign.

---

# Phase 1 done criteria

Phase 1 is complete only when all of the following are true:

1. every file in the Phase 1 audit above has a recorded disposition;
2. every changed production file is tied to a concrete audit finding;
3. the shared mutable hidden presentation hazard is covered by a red test then removed;
4. all selector entry routes are characterized and its test lives under Model Inspection ownership;
5. initial progress construction has fewer correlated arguments with exact behavior unchanged;
6. `ModelInspectionPage.Request` is the sole navigation source of truth and the page no longer re-parses validated request facts;
7. the 46 distinct Model Inspection `WMC1506` warning locations are reduced to zero while genuine two-way disclosure bindings remain two-way;
8. ActionCard, ModelCard and OutcomeCard all fail clearly if required XAML visual states drift from code;
9. no application contract/protocol/security/process behavior is changed;
10. all relevant focused tests pass;
11. the complete packaged WinUI/application suite passes;
12. every permanent Gate 2 test layer still passes;
13. orphan-process and privacy checks still pass;
14. the source list and review ledger contain the final exact set of cleanup files in ordinal order;
15. Phase 1 evidence is tied to the exact tested commit and downloaded artifacts;
16. draft PR #54 contains the complete Phase 1 context and verification evidence;
17. no unexplained Phase 1 row remains `pending review`.

---

# Approval gate

This document records the Phase 1 audit and implementation plan only. **Do not modify the production WinUI/application-contract boundary until the user reviews and approves this plan.**
