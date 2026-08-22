# Model import and GGUF quick scan: current state

**Status:** Detailed current-state implementation record  
**Last reconciled:** 2026-08-03  
**Current consuming branch:** `feature/model-inspection`

The Model Import and quick-scan foundation was completed on
`feature/winui-shell-model-import` and is now consumed by the connected
Model Inspection route on `feature/model-inspection`.

The older `GGUF-Quick-Scanner-Beginner-Guide.md` remains useful as historical
parser evidence, but some of its workflow-boundary statements describe an
earlier commit before the page, router, cards, validated-state flow, and
Model Inspection navigation were connected.

For the shorter architecture map beside the source, see:

- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/README.md`
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Controls/README.md`
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/FileImport/README.md`
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/FileImport/PickerRoute/README.md`
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/README.md`

## Current responsibility flow

```text
ImportModelCard
    -> raises browse/remove intent

ModelImportPage
    -> selects the format and file
    -> owns the active scan identity and page state
    -> calls ModelQuickScanner

ModelQuickScanner
    -> routes GGUF to GgufQuickScanner

GgufQuickScanner
    -> validates the untrusted GGUF container
    -> extracts bounded metadata

ModelQuickScanResult
    -> returns Success, Failure, or Cancelled

ModelImportPage
    -> maps the result to the matching ImportModelCard presentation
    -> stores successful validated state
    -> enables Continue only after success
    -> raises ModelInspectionRequested after a guarded user action

OnboardingShellPage
    -> receives the request
    -> navigates StageFrame to ModelInspectionPage
    -> advances CurrentStage and the persistent stage indicator

ModelInspectionPage
    -> receives the selected model path
    -> displays the initial inspection presentation after Loaded
```

The page coordinates the Model Import workflow but does not parse GGUF bytes.
The card renders state but does not pick files or scan models. Success-card
formatting is isolated in `ImportedModelCardDataMapper` rather than being mixed
into page orchestration. The onboarding shell performs navigation because it
owns `StageFrame`, `CurrentStage`, and the persistent stage indicator.

## Implemented UI states

`ImportModelCard` has four explicit presentations:

1. `AwaitingSelection` — browse and future drag-and-drop surface.
2. `Scanning` — selected filename, metadata skeletons, progress ring, and
   cancellation/removal action.
3. `ScanFailed` — selected filename, stable failure code, user-facing message,
   recovery guidance, and removal action.
4. `ScanSucceeded` — model name, filename, GGUF and quantisation badges,
   parameters, architecture, file size, declared context, completion status,
   and removal action.

Typed methods establish the required data for each state:

```csharp
ShowAwaitingSelection();
ShowScanning(selectedFileName);
ShowFailure(selectedFileName, failureCode, userMessage);
ShowSuccess(importedModelCardData);
```

The success state cannot be entered through the generic state seam without a
complete `ImportedModelCardData` object.

## Successful import flow

```text
Choose GGUF
    -> select .gguf file
    -> clear any previous validated result
    -> show Scanning
    -> run ModelQuickScanner
    -> receive Success
    -> format visible values with the configured culture
    -> store ValidatedScanResult
    -> set HasValidatedModel = true
    -> show ScanSucceeded
    -> enable Continue to model inspection
```

The card displays only the values approved in the imported-card design:

- model name;
- selected filename;
- GGUF badge;
- quantisation;
- parameter-size label;
- architecture;
- formatted file size; and
- formatted declared context.

`GgufVersion` remains in `ValidatedScanResult` for diagnostics and later
workflow decisions; it is not displayed merely because the scanner returns it.

## Implemented Model Inspection handoff

The Continue button is a user-interface guard, but the page validates the state
again before publishing a request.

```text
Continue to model inspection
    -> TryRequestModelInspection()
    -> require HasValidatedModel == true
    -> require ValidatedScanResult != null
    -> require non-empty SelectedModelPath
    -> raise ModelInspectionRequested
    -> ModelInspectionRequestedEventArgs carries ModelPath
    -> OnboardingShellPage navigates StageFrame
    -> onboarding stage 2 becomes active
    -> ModelInspectionPage receives the path
```

`ModelImportPage` does not call `Frame.Navigate` directly. It reports intent,
and the onboarding shell changes the journey state.

The current event argument carries only the selected model path. That was
sufficient to establish and test the navigation boundary, but it loses the
validated format and quick-scan metadata already available on the import page.
A future project-owned `ModelInspectionRequest` should carry:

```text
selected path
selected format
validated quick-scan result or equivalent validated metadata
```

The future request must not carry the `ModelImportPage`, its controls, or the
formatted `ImportedModelCardData` display record.

## Failure and diagnostics flow

A controlled failure keeps the selected path available until the user removes
the model. The page:

- clears validated state;
- keeps Continue disabled;
- displays only `FailureCode` and `UserMessage` in the card; and
- sends `FailureCode` and `TechnicalMessage` to the diagnostic seam.

The default diagnostic sink writes a trace warning containing only the selected
filename, stable failure code, and technical message. It does not retain the
full selected path. A diagnostic-sink exception is caught so logging can never
replace the controlled failure card or crash the import workflow.

## Cancellation and replacement safety

The page owns one `CancellationTokenSource` identity for the active scan.
Starting a replacement scan publishes the new identity before cancelling the
previous token. Therefore:

- the previous scan is asked to stop;
- its eventual result is classified as stale;
- only the current scan can update the page; and
- removing a model cannot be undone by a late failure or success result.

A current scan that returns `Cancelled` resets the page to
`AwaitingSelection`, clears selected and validated state, and keeps Continue
disabled.

The Model Inspection screen currently displays a disabled Cancel action only.
That action does not yet control a real inspection `CancellationTokenSource`.
Do not confuse Model Import scan cancellation with future full Model Inspection
cancellation.

## GGUF compatibility and parser boundary

The current little-endian scanner accepts GGUF container versions 2 and 3.
Version 1 returns `obsolete-version`; versions outside the supported 2–3 range
return `unsupported-version`.

The scanner reads the 24-byte fixed header and makes one bounded sequential
pass through metadata. It does not load or execute the model, read tensor data,
rewrite the file, or create a general-purpose in-memory GGUF representation.
The existing parser safety limits, controlled failure codes, deterministic
fixtures, and integrity checks remain unchanged by the page-state and
navigation additions.

Quick-scan Success means the bounded container and required metadata were
read successfully. It does not prove:

- tensor validity;
- tokenizer compatibility;
- chat-template compatibility;
- architecture support in llama.cpp or OpenVINO;
- memory fit;
- successful inference.

## Automated verification

The packaged Windows CI run for the completed Model Import state-machine and
code-quality slice executed 134 tests and reported:

```text
Total:    134
Executed: 134
Passed:   134
Failed:   0
```

Coverage includes:

- scanner and result contracts;
- deterministic fixture integrity;
- GGUF v2/v3 success and obsolete/future-version handling;
- malformed and adversarial metadata cases;
- router outcomes and cancellation conversion;
- awaiting, scanning, failure, and success card rendering;
- successful page-to-router-to-card handoff;
- missing optional metadata display fallbacks;
- culture-controlled presentation formatting;
- cancellation returned by the scanner;
- cancellation through the card action;
- late failure and late success suppression;
- replacement scans cancelling previous work;
- diagnostic capture and diagnostic-sink failure containment; and
- one real deterministic GGUF fixture through the real router into the success
  card.

The 134-test result belongs to the completed Model Import slice at its recorded
head. Newer navigation and Model Inspection selector tests were added later on
`feature/model-inspection`; this document does not merge those later tests into
the historical 134-test claim.

Newer connected-boundary tests include:

- `ModelImportNavigationRequestTests`;
- `OnboardingModelInspectionNavigationTests`;
- `ModelInspectionPageNavigationTests`;
- `InspectionContentTemplateSelectorTests`.

The latest selector fix was committed directly to the feature branch without a
GitHub workflow attached to that commit. A local cold build, Test Explorer run,
and manual Model Import-to-Inspection navigation check remain required before
claiming final runtime verification of the initial Model Inspection screen.

## Implemented now

- bounded local GGUF quick scan;
- deterministic quick-scan result contracts;
- explicit card states;
- cancellation identity and stale-result suppression;
- validated model state;
- diagnostic minimisation and containment;
- guarded Continue action;
- event-driven navigation request;
- onboarding-shell navigation to `ModelInspectionPage`;
- stage-indicator transition from Import Model to Inspect Model;
- selected path receipt by Model Inspection;
- initial Model Inspection presentation architecture.

## Deliberately deferred

The following are not part of the completed Model Import slice or the current
connected UI foundation:

- a richer `ModelInspectionRequest` carrying path, format, and validated
  quick-scan metadata;
- OpenVINO quick scanning and validation;
- actual drag-and-drop event handling;
- recommended-model downloads entering the same validation contract;
- llama.cpp or LLamaSharp model inspection;
- OpenVINO runtime inspection;
- dynamic Model Inspection stage progression;
- full tensor, tokenizer, structure, and runtime validation;
- functional Model Inspection cancellation;
- runtime inference or model loading before Hardware Fit; and
- formal manual acceptance for responsive layout, high text scaling, keyboard
  navigation, High Contrast, and screen-reader behaviour.

## Next implementation boundary

Navigation and the initial Model Inspection presentation now exist. The next
runtime slice should introduce a project-owned request, ViewModel/state engine,
service interface, classifier, and format-specific runtime probes.

Recommended direction:

```text
ModelInspectionPage
    -> ModelInspectionViewModel
    -> IModelInspectionService
    -> GGUF runtime probe / OpenVINO runtime probe
    -> framework-neutral evidence and result
    -> presentation mapping
    -> existing four controls
```

The runtime layer should consume the selected path and already validated
metadata without duplicating quick-scan parsing in the page. Pre-Hardware-Fit
inspection must remain lightweight and proportionate rather than blindly
performing a full model allocation.
