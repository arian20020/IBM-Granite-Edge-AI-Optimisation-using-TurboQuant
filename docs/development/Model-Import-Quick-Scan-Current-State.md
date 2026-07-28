# Model import and GGUF quick scan: current state

This document records the current model-import workflow on
`feature/winui-shell-model-import`. The older
`GGUF-Quick-Scanner-Beginner-Guide.md` remains useful as historical parser
evidence, but some of its workflow-boundary statements describe an earlier
commit before the page, router, cards, and validated-state flow were connected.

## Current responsibility flow

```text
ImportModelCard
    -> raises browse/remove events
ModelImportPage
    -> selects the format and file
    -> owns the active scan identity and page state
    -> calls ModelQuickScanner
ModelQuickScanner
    -> routes GGUF to GgufQuickScanner
GgufQuickScanner
    -> validates the untrusted GGUF container and extracts bounded metadata
ModelQuickScanResult
    -> returns Success, Failure, or Cancelled
ModelImportPage
    -> maps the result to the matching ImportModelCard presentation
```

The page coordinates the workflow but does not parse GGUF bytes. The card
renders state but does not pick files or scan models. Success-card formatting is
isolated in `ImportedModelCardDataMapper` rather than being mixed into page
orchestration.

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

## GGUF compatibility and parser boundary

The current little-endian scanner accepts GGUF container versions 2 and 3.
Version 1 returns `obsolete-version`; versions outside the supported 2–3 range
return `unsupported-version`.

The scanner reads the 24-byte fixed header and makes one bounded sequential
pass through metadata. It does not load or execute the model, read tensor data,
rewrite the file, or create a general-purpose in-memory GGUF representation.
The existing parser safety limits, controlled failure codes, deterministic
fixtures, and integrity checks remain unchanged by the page-state refactor.

## Automated verification

The packaged Windows CI run for the completed state-machine and code-quality
slice executed 134 tests and reported:

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

## Deliberately deferred

The following are not part of this slice:

- `ModelInspectionPage` and Continue-button navigation;
- a stable navigation parameter for the validated selection;
- OpenVINO quick scanning and validation;
- actual drag-and-drop event handling;
- recommended-model downloads entering the same validation contract;
- runtime inference or model loading; and
- formal manual acceptance for responsive layout, high text scaling, keyboard
  navigation, and screen-reader behaviour.

The next implementation slice is the model-inspection workflow. It should
consume the selected path, selected format, and validated scan result without
re-scanning or parsing the model in the page.
