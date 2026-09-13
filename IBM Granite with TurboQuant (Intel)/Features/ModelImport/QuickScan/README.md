# Model quick-scan architecture

**Status:** Living current-state documentation
**Last reviewed:** 2026-08-03
**Reviewed implementation baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`

[← Model Import architecture](../README.md)

## Purpose

This folder contains the lightweight validation route used immediately after a local model is selected.

A quick scan answers:

> Can the application read a bounded, recognised model container and extract the small metadata set needed by Model Import?

It does **not** answer:

> Can the complete model be loaded and executed by llama.cpp or OpenVINO on this machine?

That stronger question belongs to Model Inspection and Hardware Fit.

## Responsibility boundary

### This folder owns

- routing a requested model format to a scanner;
- bounded, asynchronous, read-only GGUF header and metadata parsing;
- stable Success, Failure, and Cancelled result construction;
- the metadata contract consumed by Model Import;
- stable diagnostic codes and separation of user versus technical messages;
- conversion of cancellation exceptions into a controlled cancellation result.

### This folder does not own

- native file or folder selection;
- visible card formatting;
- page state and active-operation identity;
- onboarding navigation;
- model tensor allocation;
- runtime loading or inference;
- hardware-fit analysis.

## Local architecture

```text
ModelImportPage
    → calls ModelQuickScanner.ScanAsync(format, path, token)

ModelQuickScanner
    ├── GGUF → GgufQuickScanner.ScanAsync(path, token)
    ├── cancellation → ModelQuickScanResult.Cancelled
    └── unsupported route → controlled Failure

GgufQuickScanner
    → reads fixed header
    → validates GGUF identity and supported version
    → makes one bounded sequential metadata pass
    → extracts approved metadata
    → returns Success or controlled Failure

ModelQuickScanResult
    → consumed by ModelImportPage
    → mapped to imported-card data or diagnostic state
```

## File inventory

### `ModelQuickScanner.cs`

Acts as the format router and operation boundary.

It receives:

```text
ModelFormatSelection
selected path
CancellationToken
```

It returns one `ModelQuickScanResult` regardless of whether the operation succeeds, fails validation, or is cancelled.

The current connected application route is GGUF. OpenVINO quick scanning is not implemented.

[Open file](./ModelQuickScanner.cs)

### `GgufQuickScanner.cs`

Implements bounded parsing for little-endian GGUF containers.

Current compatibility:

```text
GGUF version 1       → controlled obsolete-version failure
GGUF versions 2–3   → accepted for bounded metadata scanning
future/unknown       → controlled unsupported-version failure
```

The scanner:

- reads the fixed header before metadata;
- validates magic, version, counts, lengths, and supported metadata types;
- checks cancellation during asynchronous work;
- avoids loading tensor data;
- avoids executing or rewriting the model;
- extracts only the metadata needed by the current import route;
- converts malformed and adversarial input into stable failures rather than exposing parser exceptions to the page.

The scanner must continue treating every selected local file as untrusted input.

[Open file](./GgufQuickScanner.cs)

### `ModelQuickScanResult.cs`

Defines the completed quick-scan contract.

Successful metadata:

```text
ModelName
Architecture
ParameterSizeLabel
Quantization
FileSizeBytes
ContextLength
GgufVersion
```

Failure data:

```text
FailureCode
UserMessage
TechnicalMessage
```

Factory methods enforce different invariants:

```csharp
CreateSuccess(...)
CreateFailure(...)
CreateCancelled()
```

A successful result requires a usable model name, architecture, non-empty file size, and non-zero supported version. A failure always receives a stable code plus usable user and technical text.

[Open file](./ModelQuickScanResult.cs)

### `ModelQuickScanOutcome.cs`

Defines the three terminal quick-scan outcomes:

```text
Success
Failure
Cancelled
```

This enum is distinct from Model Import card state and from future full Model Inspection outcomes.

[Open file](./ModelQuickScanOutcome.cs)

### `ModelQuickScanFailureDiagnostic.cs`

Defines the minimized diagnostic information passed by the page to its diagnostic sink after a controlled quick-scan failure.

The diagnostic boundary preserves:

- selected filename rather than full local path;
- stable failure code;
- technical message.

It does not decide what the user sees and does not perform logging itself.

[Open file](./ModelQuickScanFailureDiagnostic.cs)

## Success contract

A Success result means:

```text
The selected file had a recognised supported GGUF container
AND the bounded metadata pass completed
AND required success fields were available
```

It does not mean:

- every tensor is valid;
- the tokenizer is compatible;
- the architecture is supported by the chosen runtime;
- the model fits available RAM or VRAM;
- inference will succeed.

## Failure contract

Controlled failures keep two audiences separate:

```text
UserMessage
→ readable explanation suitable for the UI

TechnicalMessage
→ diagnostic evidence suitable for logs and debugging

FailureCode
→ stable machine-readable classification
```

The page displays only the user-facing information approved for the card and sends technical detail to the diagnostic seam.

## Cancellation contract

Cancellation is a normal terminal outcome:

```text
CancellationToken requested
    ↓
scanner or router observes cancellation
    ↓
ModelQuickScanResult.CreateCancelled()
    ↓
ModelImportPage checks active scan identity
    ↓
current cancelled scan resets to AwaitingSelection
```

The scanner does not own stale-result suppression. `ModelImportPage` decides whether a completed result still belongs to the active operation.

## Security and resource boundary

The scanner is intentionally narrower than a general GGUF reader.

It should continue to enforce:

- bounded counts and lengths;
- checked arithmetic before seeking or allocating;
- supported metadata types only;
- cancellation checks;
- no tensor loading;
- no executable model behavior;
- controlled failure rather than raw exception leakage;
- no mutation of the selected file.

Any future parser extension must be reviewed as an untrusted-input change, not merely as a UI enhancement.

## Tests and evidence

Key tests:

- [`GgufQuickScannerTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufQuickScannerTests.cs)
- [`ModelQuickScannerTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/ModelQuickScannerTests.cs)
- [`ModelQuickScanResultTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/ModelQuickScanResultTests.cs)

The wider fixture and integration suite covers:

- deterministic fixture hashes;
- supported versions;
- obsolete and future versions;
- malformed strings, arrays, counts, and metadata values;
- cancellation;
- router behavior;
- one real deterministic fixture through the router and import card.

Detailed documents:

- [Model Import and GGUF quick scan current state](../../../../docs/development/Model-Import-Quick-Scan-Current-State.md)
- [GGUF quick-scanner beginner guide](../../../../docs/development/GGUF-Quick-Scanner-Beginner-Guide.md)
- [GGUF quick-scanner test report](../../../../docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md)

## Implemented now

- GGUF quick-scan route;
- bounded GGUF v2/v3 parsing;
- obsolete and unsupported version classification;
- controlled result factories;
- metadata extraction for the import card;
- cancellation conversion;
- diagnostic contract;
- deterministic malformed and valid fixtures;
- scanner, router, result, and integration tests.

## Not implemented and non-claims

- OpenVINO quick scanning;
- complete tensor validation;
- tokenizer and chat-template validation;
- runtime architecture support checks;
- model allocation or context creation;
- smoke inference;
- hardware-fit analysis;
- model conversion.

## Known limitations and change hazards

- the parser supports only the metadata types and versions explicitly implemented and tested;
- adding a field to `ModelQuickScanResult` requires scanner, mapper, page, test, and documentation review;
- adding a model format requires a route-specific scanner rather than expanding the GGUF scanner into a format-agnostic parser;
- parser limits must remain proportionate and evidence-backed;
- quick-scan success must never be presented as full Model Inspection success.

## Related documentation

- [Model Import architecture](../README.md)
- [Picker routes](../FileImport/PickerRoute/README.md)
- [Model Import controls](../Controls/README.md)
- [Model Inspection architecture](../../ModelInspection/README.md)
