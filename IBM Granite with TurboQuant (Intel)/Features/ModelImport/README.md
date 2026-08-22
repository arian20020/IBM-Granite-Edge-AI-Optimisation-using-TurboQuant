# Model Import architecture

**Status:** Local GGUF quick scan and immutable handoff implemented; the downstream protected x64 inspection journey is connected
**Last reviewed:** 2026-08-09

[← Application feature architecture](../README.md)

## Purpose

Model Import is onboarding stage one. It lets the user choose a local model package, performs a bounded quick scan, presents a controlled result, and hands only a currently validated selection to Model Inspection.

The connected route is GGUF. Quick scan remains deliberately lighter than full inspection: it reads enough untrusted container metadata to validate and describe the selection, but it does not load tensors, create a context, execute inference, or prove hardware compatibility.

## Responsibility boundary

### This feature owns

- model-format selection and native file picking;
- Model Import page state;
- active quick-scan identity and cancellation;
- bounded GGUF header and metadata scanning;
- stale-result suppression;
- quick-scan result and diagnostic handling;
- imported-model card mapping;
- the final file-continuity check immediately before navigation;
- creation of the immutable `ModelInspectionRequest`.

### This feature does not own

- onboarding `Frame` navigation;
- full runtime inspection;
- worker-process launch or protocol transport;
- application outcome classification;
- hardware-fit analysis;
- conversion, configuration, inference, or chat.

## Current source structure

```text
ModelImport/
├── README.md
├── ModelImportPage.xaml
├── ModelImportPage.xaml.cs
├── ModelInspectionRequestFactory.cs
├── ModelInspectionRequestedEventArgs.cs
├── Controls/
├── FileImport/
├── ModelDownload/
└── QuickScan/
```

Nested documentation:

- [Imported-model controls](./Controls/README.md)
- [File-import boundary](./FileImport/README.md)
- [Native picker routes](./FileImport/PickerRoute/README.md)
- [Recommended-model download prototype](./ModelDownload/README.md)
- [Quick-scan architecture](./QuickScan/README.md)

## Successful import flow

```text
Choose GGUF
    → select a .gguf file
    → invalidate any previous selection
    → show Scanning
    → run ModelQuickScanner
    → receive Success
    → retain SelectedModelPath and ValidatedScanResult
    → show ScanSucceeded
    → enable Continue to model inspection
```

A quick-scan success is not permanently trusted. The scanner captures file
length and UTC last-write time while its read handle excludes writers and
replacement. The file may still be deleted, replaced, resized, locked for
writing, or otherwise changed before the user continues.

## Immutable handoff

```text
User selects Continue to model inspection
    ↓
ModelImportPage.TryRequestModelInspection()
    ↓
re-check validated page state
    ↓
ModelInspectionRequestFactory.TryCreate(...)
    ↓
canonicalise path
open read-only while excluding concurrent writers
require ordinary .gguf file
require current size = quick-scan size
require current UTC last-write time = scan-time UTC last-write time
capture the matched current length and timestamp
copy bounded quick-scan facts
    ↓
raise ModelInspectionRequested(Request)
```

`ModelInspectionRequest` carries:

```text
ModelPath
FileName
ExpectedFileIdentity
    LengthBytes
    LastWriteTimeUtc
QuickScan
    Format
    ModelName
    Architecture
    ParameterSizeLabel
    Quantisation
    FileSizeBytes
    DeclaredContextLength
    GgufVersion
```

The request is immutable and is created only after the current file still agrees with the successful scan. The factory does not hash or load the complete model in the WinUI process.

## Changed-selection failure

When the file no longer satisfies the handoff boundary:

```text
no navigation event is raised
HasValidatedModel becomes false
ValidatedScanResult is cleared
SelectedModelPath is cleared
Continue is disabled
ImportModelCard shows ScanFailed
```

Stable diagnostic code:

```text
model-selection-changed
```

User message:

```text
The selected model changed after validation. Choose the model again.
```

The visible and default diagnostic data uses the final filename rather than exposing the complete local path.

## Navigation ownership

`ModelImportPage` raises intent and data only. It does not search for or manipulate a parent frame.

```text
ModelImportPage
    → creates request
    → raises ModelInspectionRequested

OnboardingShellPage
    → owns StageFrame navigation
    → forwards the same request object
```

## Cancellation and replacement safety

The page owns one active scan identity. A replacement scan is published before the older scan is cancelled, so any late result from the older operation is recognised as stale and cannot overwrite the current page.

A current `Cancelled` result resets the page to `AwaitingSelection`, clears selection and validation state, and keeps Continue disabled.

## Automated coverage

The current tests cover:

- GGUF quick-scan success, failure, cancellation, malformed input, and fixture integrity;
- picker and format routing;
- imported-card states and culture-controlled mapping;
- replacement and cancellation races;
- diagnostic containment and path minimisation;
- immutable request construction;
- missing files, directories, non-GGUF files, size changes, and concurrent writers;
- deleted or modified files between scan and Continue, including a same-length
  replacement with a changed timestamp;
- successful request event creation;
- no navigation request without a validated model.

Relevant tests:

- [`ModelInspectionRequestFactoryTests.cs`](../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelInspectionRequestFactoryTests.cs)
- [`ModelImportNavigationRequestTests.cs`](../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelImportNavigationRequestTests.cs)

## Implemented now

- local GGUF selection;
- bounded GGUF v2/v3 quick scan;
- controlled Success, Failure, and Cancelled outcomes;
- active-scan cancellation identity and stale-result suppression;
- culture-aware presentation mapping;
- diagnostic minimisation and containment;
- immutable, file-backed Model Inspection request creation;
- guarded event-driven handoff.

## Non-claims and deferred work

- OpenVINO quick scanning and validated import;
- actual drag-and-drop handling;
- recommended-model catalog and download verification;
- worker launch, inspection evidence, classification, or result presentation
  (owned by Model Inspection, not Model Import);
- full model allocation, context, inference, or chat.

## Related documentation

- [Onboarding architecture](../Onboarding/README.md)
- [Model Inspection architecture](../ModelInspection/README.md)
- [Model Inspection application contracts](../ModelInspection/Contracts/README.md)
