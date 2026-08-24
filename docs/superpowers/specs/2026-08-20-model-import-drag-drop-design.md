# Model Import and Drag-and-Drop Design

**Status:** Approved design sections; written-spec review pending

**Date:** 2026-08-20

**Owner:** D1 — Model Import and Drag-and-Drop

## 1. Purpose and scope

This design completes the safe entry journey for one local model selection in the WinUI 3 application. A user can drop one local item or choose it through an accessible file or folder picker. Both inputs enter one operation pipeline, which performs only enough bounded inspection to choose a route:

| Accepted route | Logical selection | D1 action | Downstream owner |
| --- | --- | --- | --- |
| GGUF | one ordinary `.gguf` file | existing bounded GGUF quick scan and continuity check | G1 / current GGUF Model Inspection |
| Existing OpenVINO | one directory containing exactly one candidate IR pair | bounded pair preflight and immutable dispatch selection | O1 |
| Source model | one supported Hugging Face/Safetensors directory | bounded source-shape preflight and immutable conversion-required selection | O1 |

D1 does not parse tensors or OpenVINO graphs, load a runtime, execute repository/model code, convert, optimise, quantify, run inference, calculate hardware suitability, collect hardware, or upload/move/rename/write/delete a chosen item. Import means reference/select only.

The first end-to-end MVP is a GGUF file or complete existing OpenVINO directory through either drop or picker, safely classified and dispatched to a test-double inspector seam. Source-model folders and Find downloaded models are planned increments, not omitted work.

## 2. Current-state map and retained behaviour

`Features/ModelImport/ModelImportPage` currently has a GGUF-only native picker, `ModelQuickScanner`, `GgufQuickScanner`, cancellation, active-scan stale-result suppression, a controlled imported-model card, and `ModelInspectionRequestFactory` continuity checking. `OnboardingShellPage` owns frame navigation; the import page raises an immutable request event only.

The existing GGUF implementation remains the authoritative route behaviour. A dropped GGUF must behave identically to a picked GGUF after normalization: replacement invalidation, progress presentation, cancellation, diagnostic mapping, accepted-card state, current-file continuity recheck, navigation intent, and stale-result rejection.

Current Model Import hard-coded light styling is not a new visual authority. The implementation must adopt equivalent semantic resources compatible with the established Model Inspection Light, Dark, and High Contrast grammar without copying Hardware-specific states, evidence, or wording.

## 3. Recommended architecture

### 3.1 One input pipeline

```text
Drop StorageItems ─┐
File picker ───────┼─> SelectionInputNormalizer
Folder picker ─────┘          |
                                v
                    SelectionOperation (operation ID + CTS)
                                |
                                v
                 BoundedSelectionClassifier / route preflight
                  /              |                \
                 GGUF         OpenVINO           source folder
                  |              |                |
          existing scanner   O1 dispatch      O1 dispatch
                  \              |                /
                   immutable accepted local selection
                                |
                  current-operation and continuity checks
                                |
                     page raises route-specific intent
                                |
                     I0-owned navigation integration
```

`SelectionInputNormalizer` accepts exactly one top-level candidate. It is the only place where drag/drop and picker representations become a shared internal input. `SelectionOperation` owns a monotonically new opaque operation identity, cancellation token source, and page-lifetime retirement semantics. `BoundedSelectionClassifier` may inspect names, attributes, and bounded structural members; it never asserts model validity from an extension alone.

Accepted selections are immutable application objects. They contain only an operation identity, route, safe display name, selection kind, route-local continuity identity, and bounded preflight summary. An absolute path remains private to the Model Import/application route until the immediate local inspector invocation. It is absent from telemetry, normal summaries, diagnostics, remote evidence, Hardware APIs, and the approved path-minimized ModelInspectionHandoff.

### 3.2 Existing and new route contracts

GGUF preserves the current `ModelInspectionRequestFactory` and its final open/read-only/current-size/current-last-write validation. D1 must not widen that request across the later ModelInspection-to-Hardware seam. The path-minimized ModelInspectionHandoff remains C0/I0-owned work.

OpenVINO and source folders use D1-owned internal, route-local selection records and narrow O1 dispatch ports. Their final public handoff schema is an I0/O1 integration request. D1 may define the required invariants but cannot edit O1 or shared-contract files.

### 3.3 Operation, cancellation, and stale-result rules

1. Every accepted picker/drop input first creates and publishes a new operation ID.
2. Publishing the new ID clears the previous accepted selection and disables Continue before cancellation is requested for the previous operation.
3. The classifier, UI completion, exception mapper, cancellation handler, continuity check, and navigation intent all compare their operation ID to the current one.
4. A result for an older, cancelled, retired, or unknown ID is discarded without changing UI, navigation, live region, or diagnostics.
5. Cancel clears the selection and returns to AwaitingSelection. A late success cannot resurrect it.
6. Navigation away retires the operation before requesting cancellation, detaches observers, and permits no late presentation or route dispatch.
7. Accepted results are immutable. Replacement always makes a new identity; results are never edited in place.

## 4. Classification and bounded validation contract

### 4.1 Input matrix

| Input | Classification result | Recovery |
| --- | --- | --- |
| one ordinary `.gguf` file | GGUF candidate; existing bounded quick scan decides valid/invalid | replace or cancel |
| one ordinary non-GGUF file | unsupported file | choose model file/folder |
| one folder with one `.xml` and exact stem-matched `.bin` | existing OpenVINO candidate | continue to O1 inspector seam |
| one folder with no/multiple/mismatched IR pairs | incomplete or ambiguous folder | choose another folder |
| one source folder with readable root `config.json`, supported declared task/architecture policy, and valid root Safetensors shape | source-model candidate | continue to O1 conversion-required seam |
| source folder with index referencing missing shard, missing config, custom-code requirement, unsupported task/architecture, or malformed bounded metadata | invalid/incomplete/unsupported | choose another folder |
| zero, multiple, or mixed dropped storage items | rejected before classification | choose exactly one item |
| picker cancellation | cancelled | select again |
| inaccessible, reparse, network, cloud-only, disappearing, or changed candidate | access/selection-changed failure | make locally available or choose another |

The initial source-folder policy is root-only: `config.json`, weights/index, tokenizer and generation resources are discovered only at the selected root. D1 does not recursively search arbitrary trees to make a folder fit. O1 owns deep structural validation and conversion eligibility.

### 4.2 Explicit resource limits

The feature has these release-1 limits, versioned beside the classifier policy:

- exactly one selected top-level item;
- no recursive enumeration; maximum depth one from the selected folder;
- at most 512 direct children inspected, otherwise `selection-enumeration-limit`;
- at most 32 candidate IR, config, index, tokenizer, or safetensors names retained in a preflight summary;
- at most 256 KiB total metadata bytes opened/read by D1; no model-weight body read;
- at most 5 seconds wall-clock classification time; cancellation checked before and after every asynchronous storage operation;
- at most one open metadata stream at a time; all are read-only and disposed before dispatch.

These are safety limits, not model-size limits. The UI says the selected folder is too large to check safely and offers Choose another; it never silently broadens the scan.

### 4.3 Unsafe location policy

D1 fails closed for reparse points (including symlinks and junctions), UNC/network paths, device paths, cloud placeholders that are not locally available, inaccessible items, items whose identity changes while checked, and files/folders removed during validation. It does not follow, hydrate, resolve, retry, preview, or execute such items. The implementation must use storage/file APIs and handles that make the check observable and testable; it must not infer safety from a string alone.

## 5. UI, interaction, and accessibility

### 5.1 State machine

| State | Primary actions | Required behaviour |
| --- | --- | --- |
| AwaitingSelection | drop one item; Choose model file; Choose model folder | supported alternatives are visible and keyboard reachable |
| DragOverValid | drop | copy operation only; positive visual feedback |
| DragOverInvalid | leave/drop rejected | invalid visual feedback; no classification starts |
| Validating | Cancel; replace through picker/drop | route-neutral active status; no Continue |
| ValidSelection | Continue; remove/replace | immutable current selection only; recheck before dispatch |
| InvalidSelection | choose another | plain-language reason and stable support code |
| UnsupportedFormat | choose another | no validity claim from filename alone |
| AmbiguousFolder | choose another folder | explain single-logical-model rule |
| AccessDenied | make available/choose another | no raw path or exception text |
| Cancelled | choose/drop | selection cleared; no failure wording |
| Replaced | current operation controls | old results suppressed |

The non-null transparent drop-surface background, `AllowDrop`, `DragOver`, `DragLeave`, and `Drop` handlers are part of the same target. `DragOver` accepts `DataPackageOperation.Copy` only when the data contains StorageItems and the candidate-count/type precheck permits the input. Drop uses the event deferral for the complete asynchronous StorageItems acquisition and always completes it; the handler never moves a user item.

### 5.2 Approved and proposed copy

Existing approved/implemented copy retained where applicable:

- `Import a model to begin`
- `Drag model file here`
- `Quick scan in progress · Reading model metadata`
- `Continue to model inspection`
- `The selected model changed after validation. Choose the model again.`

Approved additions:

- buttons: `Choose model file`, `Choose model folder`;
- help: `Choose or drop one GGUF model file, OpenVINO model folder, or supported source-model folder.`;
- valid drag: `Drop to check this model`;
- invalid drag/multiple item: `Choose one model file or folder at a time.`;
- access: `This model could not be opened. Check that it is available on this computer, then try again.`;
- ambiguity: `This folder contains more than one possible model. Choose the folder for one model only.`;
- unsupported: `This model format is not supported here. Choose a GGUF file or a supported model folder.`

### 5.3 Accessibility and responsive behaviour

The target automation name identifies it as an optional drop area and names supported inputs. Help text tells users that picker actions provide an equivalent route. A polite live region announces only meaningful transitions: validation started, accepted route, failure category, cancellation, or replacement. It never announces every enumerated item. The initiating picker action retains focus while work begins; terminal state moves focus to its outcome heading. Cancel returns focus to the initiating picker action or the target's accessible Choose action.

Pointer, keyboard, screen-reader, picker and drag/drop routes have parity. Controls meet the existing 44 px minimum target size. At 200% text, layout stacks, cards grow naturally, labels wrap, and there is no horizontal clipping/scrolling. Reduced motion replaces active animation with a static activity indication. Semantic Light/Dark/High Contrast resources provide contrast, system borders, and text/icon status meaning independent of color.

## 6. Privacy and error policy

User-visible diagnostics contain a stable code, safe display name, plain-language explanation, and explicit recovery action. They contain no absolute/relative full path, UNC host, account name, credential, raw exception, raw tool output, model bytes, prompt/template content, or unbounded metadata. Existing trace diagnostics remain local and must be audited so they cannot reach normal summaries or evidence.

| Code family | Meaning | Recovery |
| --- | --- | --- |
| `selection-multiple-items` | more than one top-level item | choose one item |
| `selection-unsupported-file` / `selection-unsupported-folder` | no supported shallow route | choose another |
| `selection-ambiguous-folder` | more than one possible logical model | choose a single model folder |
| `selection-incomplete-*` | named required shallow structural member absent | restore a complete folder, then choose again |
| `selection-access-denied` / `selection-not-local` | not safely readable/local | make it available locally or choose another |
| `selection-reparse-point` / `selection-network-location` | unsafe location | choose an ordinary local item |
| `selection-enumeration-limit` / `selection-timeout` | bounded check could not safely complete | choose a more specific folder |
| `model-selection-changed` | continuity identity changed | choose again |
| `selection-cancelled` | user cancelled | select again |

## 7. File ownership and I0 requests

D1 may plan only ModelImport-owned feature and ModelImport test paths. Proposed D1 path groups are `Features/ModelImport/DragDropRoute/`, `Features/ModelImport/Selection/`, existing ModelImport page/control paths, `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/`, and ModelImport-specific fixtures/documentation.

D1 must not edit `App.xaml`, either project file, Onboarding shell/stage indicator, central routes, shared fixture registries/catalogues, ModelInspection internals, HardwareInspection, Compatibility, GGUF runtime, or OpenVINO runtime files.

I0 requests are separately named and serially integrated:

1. register approved route-specific inspector navigation after each owner exposes its narrow invocation contract;
2. add any shared resource registration and project include required by D1-owned assets/pages;
3. register shared fixtures and final cross-feature test inventory;
4. integrate the approved path-minimized ModelInspectionHandoff only after C0 freezes its schema/lifetime;
5. own failed-navigation recovery and end-to-end route validation.

## 8. Tests and acceptance criteria

Tests are required at five levels:

- **Unit:** normalizer, operation identity, route policy, bounded enumerator, continuities, safe diagnostic mapper, cancellation and stale-result suppression.
- **Contract and mutation:** immutable selection records; no forbidden path-bearing leakage; invalid extension/shape combinations; corrupt/missing/mismatched IR; source index/shard/config failures; reparse/UNC/cloud/change cases.
- **Integration:** picker and drop each dispatch GGUF/OpenVINO test doubles exactly once; replacement/cancel/navigation never dispatch stale work.
- **Native UI:** `AllowDrop`, non-null target, Copy semantics, asynchronous deferral completion, valid/invalid visuals, multiple/mixed input, focus/automation/live regions, keyboard parity, themes, reduced motion, compact layout, and 200% text.
- **Manual packaged checks:** drag from Explorer, denied/local-only placeholder/network/reparse situations available on the controlled test machine; no model mutation, listener, upload, or execution.

Acceptance requires all of the following:

1. A drop and equivalent picker selection produce the same normalized input, operation lifecycle, UI state, route, and error/recovery outcome.
2. Exactly one logical selection is enforced for all entry points.
3. GGUF and complete OpenVINO MVP selections are safely classified and test-double dispatched.
4. Extension-only trust, unbounded scanning, reparse following, cloud hydration, model execution, modification, upload, and raw-path exposure are absent.
5. Cancel, replacement, item change, navigation, duplicate completion, and late results cannot replace the current selection or navigate.
6. Keyboard, screen reader, contrast, reduced motion, compact widths, and 200% text all have verified parity.
7. D1 changes stay inside owned paths and all shared edits are delivered as I0 requests.

## 9. Sequencing, deferred work, risks, and stop conditions

**MVP:** normalize inputs; add Drop; preserve picker behaviour; enforce one item; classify GGUF/OpenVINO directory; dispatch test doubles; complete lifecycle/privacy/accessibility tests.

**Increment 2:** source-model folder shallow classification and O1 conversion-required dispatch only.

**Increment 3:** Find downloaded models behind a consent screen, a fixed declared location set, explicit item/time limits, cancellation, no background scan, and no persisted absolute discovery paths.

Stop and return to C0/I0 rather than inventing behaviour if the path-minimized ModelInspectionHandoff schema/lifetime, an O1 route contract, shared route registration, shared resource/project-file modification, or final unsupported/invalid inspector copy cannot be established from an approved owner contract. Do not activate a destination route merely because a D1 preflight passes.

## 10. Traceability and sources

The D1 plan maps every direct requirement from the adopted master prompt and D1 worker architecture to a task. P1 is primarily a Hardware Inspection requirements index; D1's relevant cross-feature entries are `MI-SEAM-003`–`MI-SEAM-009`, `MI-SEAM-025`, and `MI-SEAM-028`. The plan records all other `HI-*` IDs as non-D1 scope rather than claiming false coverage. P2/P3 findings are addressed through path minimization, safe diagnostics, ownership isolation, approval-gated shared integration, and proof-oriented tests.

| Primary source | Retrieved | Claim used | Implementation consequence |
| --- | --- | --- | --- |
| [Microsoft Learn: Drag and drop](https://learn.microsoft.com/windows/apps/develop/data/drag-and-drop) | 2026-08-20 | drop targets need `AllowDrop`, a non-null background, accepted operation, and StorageItems handling; multiple/mixed items need explicit handling | transparent target, Copy-only `DragOver`, deferral-backed Drop, single-item policy |
| [Microsoft Learn: UIElement.AllowDrop](https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.allowdrop?view=windows-app-sdk-1.8) | 2026-08-20 | drag events require `AllowDrop` | target contract and native UI tests |
| [Microsoft Learn: Windows App SDK file management](https://learn.microsoft.com/windows/apps/develop/files/) | 2026-08-20 | desktop picker APIs use a window identity and return path-oriented results | retain current Windows App SDK picker family; normalize path/storage inputs |
| [Microsoft Learn: display WinRT UI objects](https://learn.microsoft.com/windows/apps/develop/ui/display-ui-objects) | 2026-08-20 | legacy WinRT pickers require a window owner; desktop picker ownership is explicit | preserve current owner-window picker usage |
| [Microsoft Learn: AutomationProperties](https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.automation.automationproperties?view=windows-app-sdk-2.0) | 2026-08-20 | automation names/help and live settings are available attached properties | accessible target/action and bounded live region |

Controlled references reviewed include the package source manifest; approved whole-feature worker architecture; P1, P2, P3 and C0 records; approved handoff/Stage C/visual contracts; all supplied workflow DOCX files including the nested 21-workflow archive; repository-control records; current Model Import/Model Inspection code, tests, and recent history. Where initial workflow text conflicts with current controlled implementation or later authority, this specification gives later approved/current controlled sources precedence.
