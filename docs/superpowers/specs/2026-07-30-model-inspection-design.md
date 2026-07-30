# Model Inspection Feature — MVP Architecture Design

**Status:** Proposed design baseline for user review  
**Date:** 2026-07-30  
**Scope:** GGUF model inspection between the existing Model Import feature and the later LLM Fit hardware-analysis feature

## 1. Purpose

The Model Inspection feature determines whether a successfully quick-scanned GGUF model is understood and supported by the application's exact pinned `llama.cpp` runtime.

The feature must:

- receive the validated model path and quick-scan result from Model Import;
- let the user explicitly start the inspection;
- show a responsive before, during, and after inspection experience;
- use LLamaSharp as the C# wrapper around the pinned `llama.cpp` backend;
- gather runtime evidence without exposing LLamaSharp or native objects to the UI;
- translate technical evidence into one controlled application outcome;
- show a simple summary first and optional expanded details on demand;
- allow only safe outcomes to continue to LLM Fit hardware analysis;
- never modify the selected model file.

This design deliberately keeps the first implementation small. It avoids five separate result controls, a separate technical-details page, a feature-specific style folder, process isolation, and full runtime smoke testing in the first slice.

## 2. Confirmed architecture decision

Use a small MVVM-based feature with one application service and one runtime adapter:

```text
ModelInspectionPage
        ↓ data binding and commands
ModelInspectionViewModel
        ↓ complete inspection request
IModelInspectionService
        ↓ implemented by
ModelInspectionService
        ├── ILlamaModelProbe
        │       ↓ implemented by
        │   LlamaSharpModelProbe
        │       ↓
        │   LLamaSharp
        │       ↓
        │   exact pinned llama.cpp backend
        │
        └── ModelInspectionClassifier
```

Commands travel down this chain. Progress, evidence, and the final result return as data. No lower layer calls the page directly.

### Why this option was selected

Calling LLamaSharp directly from the page would mix native-runtime integration, progress, cancellation, classification, and XAML state in one class. A separate worker process would provide stronger crash isolation but adds too much packaging and communication work for the available time.

The selected approach keeps the first version understandable and testable while preserving an `ILlamaModelProbe` boundary that can later be implemented by a worker process without changing the page, ViewModel, service, or classifier.

## 3. Reduced first-version folder structure

```text
Features/
└── ModelInspection/
    ├── ModelInspectionPage.xaml
    ├── ModelInspectionPage.xaml.cs
    ├── ModelInspectionViewModel.cs
    │
    ├── Controls/
    │   ├── InspectionModelCard.xaml
    │   ├── InspectionModelCard.xaml.cs
    │   ├── InspectionChecksCard.xaml
    │   ├── InspectionChecksCard.xaml.cs
    │   ├── InspectionResultCard.xaml
    │   └── InspectionResultCard.xaml.cs
    │
    ├── Dialogs/
    │   ├── InspectionDetailsDialog.xaml
    │   └── InspectionDetailsDialog.xaml.cs
    │
    ├── Models/
    │   └── ModelInspectionModels.cs
    │
    ├── Services/
    │   ├── IModelInspectionService.cs
    │   └── ModelInspectionService.cs
    │
    ├── Runtime/
    │   ├── ILlamaModelProbe.cs
    │   └── LlamaSharpModelProbe.cs
    │
    └── ModelInspectionClassifier.cs
```

This is the initial target, not a permanent rule. Closely related small records and enums start together in `ModelInspectionModels.cs`. They are split only when that file becomes difficult to understand or individual types gain meaningful behaviour.

## 4. Component responsibilities

### 4.1 `ModelInspectionPage.xaml`

Owns the shared visual shell:

- page title;
- selected-model card;
- inspection checks area;
- completed result area;
- fixed bottom action region;
- visual transitions between awaiting, inspecting, completed, cancelled, and operational-failure states.

It does not call LLamaSharp, classify evidence, interpret native exceptions, or retain native handles.

### 4.2 `ModelInspectionViewModel.cs`

Holds what the page should show and which page actions are available:

- selected model information;
- current page state;
- current inspection stage;
- completed and waiting checks;
- optional genuine stage progress;
- whether Start, Cancel, Retry, Details, or Continue is available;
- the final `ModelInspectionResult`;
- the currently selected finding for the details dialog.

It provides the page-facing actions:

- start inspection;
- cancel inspection;
- retry inspection;
- open warning or failure details;
- choose another model;
- continue to hardware analysis.

It does not contain LLamaSharp calls or classification rules.

### 4.3 `InspectionModelCard.xaml`

Displays the selected model throughout the complete feature. It reuses the already validated quick-scan result instead of rereading the file.

Expected fields:

- model display name;
- filename;
- GGUF format;
- architecture;
- parameter-size label;
- quantisation;
- file size;
- declared context length;
- GGUF version;
- "Quick scan passed" status.

The card remains in the same position before, during, and after inspection.

### 4.4 `InspectionChecksCard.xaml`

Displays one symmetrical list of inspection stages. Every row uses fixed columns:

```text
| Indicator | Check description | Status |
```

Indicator states:

- waiting: neutral outlined circle;
- current: animated WinUI `ProgressRing`;
- passed: green checkmark;
- warning: amber warning icon;
- failed: red error icon;
- cancelled: neutral stopped state.

Only one stage normally shows the animated spinner. Where LLamaSharp provides a genuine numeric fraction, the card may also show a determinate progress bar. Otherwise it shows truthful stage progress such as "3 of 6 stages complete".

### 4.5 `InspectionResultCard.xaml`

One generic result control displays all five model outcomes. It receives data and changes icon, heading, summary, findings, and actions without duplicating the entire layout in five XAML files.

Supported outcomes:

1. `Ready`
2. `ReadyWithWarnings`
3. `ConversionRequired`
4. `Unsupported`
5. `InvalidOrIncomplete`

`Ready` and `ReadyWithWarnings` share almost identical geometry. `ReadyWithWarnings` adds warning rows and an information action.

### 4.6 `InspectionDetailsDialog.xaml`

One reusable dialog displays expanded information for warnings and blocking findings.

It shows:

- a short plain-English summary;
- what was found;
- what it means;
- the recommended action;
- the stable diagnostic code;
- an optional `Expander` containing fuller technical detail.

The result card must still show the essential explanation. The dialog adds depth; it does not hide all useful information.

### 4.7 `ModelInspectionModels.cs`

Contains small project-owned data contracts and enums for the first version:

- `ModelInspectionRequest`;
- `ModelInspectionProgress`;
- `ModelInspectionEvidence`;
- `ModelInspectionFinding`;
- `ModelInspectionResult`;
- `ModelInspectionOutcome`;
- `ModelInspectionPageState`;
- `ModelInspectionStage`;
- `InspectionFindingSeverity`.

These types hold information. They do not run the inspection or update XAML by themselves.

No LLamaSharp object, native pointer, `LLamaWeights`, XAML control, or page object may appear in these contracts.

### 4.8 `IModelInspectionService.cs`

Defines the application-level contract for the complete use case:

```text
Receive one validated inspection request
Report structured progress
Support cancellation
Return one final ModelInspectionResult
```

The ViewModel depends on this interface so tests can provide a controlled fake service without loading a real model.

### 4.9 `ModelInspectionService.cs`

Coordinates the complete inspection:

1. validate the request;
2. confirm the selected file still exists;
3. start stage reporting;
4. call `ILlamaModelProbe`;
5. combine runtime evidence with the validated quick-scan result;
6. call `ModelInspectionClassifier`;
7. create and return an immutable `ModelInspectionResult`.

It does not reference XAML controls or navigate pages.

### 4.10 `ILlamaModelProbe.cs`

Defines the lower-level contract for requesting technical evidence from the exact configured runtime.

It answers:

> What evidence did the pinned `llama.cpp` runtime produce for this model?

It does not decide which application result card should be displayed.

### 4.11 `LlamaSharpModelProbe.cs`

Implements `ILlamaModelProbe` and is the only project class that directly knows about LLamaSharp and native runtime details.

It is responsible for:

- providing the model path to LLamaSharp;
- configuring the selected inspection mode;
- forwarding genuine native progress;
- supporting cancellation;
- collecting model metadata, size, parameter count, context, vocabulary/tokenizer evidence, and chat-template evidence where available;
- translating known native failures into controlled project-owned evidence;
- recording LLamaSharp and native-runtime identity;
- disposing all native resources on success, cancellation, and failure.

### 4.12 `ModelInspectionClassifier.cs`

Converts `ModelInspectionEvidence` into `ModelInspectionResult`.

It is deterministic and contains no XAML, file I/O, or LLamaSharp types.

Classification rules:

| Evidence | Outcome |
|---|---|
| Required checks pass and no warnings exist | Ready |
| Runtime supports the model but non-blocking findings exist | Ready with warnings |
| A recognised model has a real, verified preparation route | Conversion required |
| The GGUF is readable but the pinned runtime does not support it | Unsupported |
| The model is corrupt, malformed, truncated, or incomplete | Invalid or incomplete |

A missing DLL, access-denied error, cancellation, unexpected exception, or runtime-initialisation failure is an operational failure, not proof that the model is invalid.

## 5. User-visible state flow

```text
AwaitingStart
      │ Start inspection
      ▼
Inspecting
      ├── Cancelled
      ├── OperationalFailure
      └── Completed
            ├── Ready
            ├── ReadyWithWarnings
            ├── ConversionRequired
            ├── Unsupported
            └── InvalidOrIncomplete
```

### 5.1 Before inspection

The page shows:

- heading;
- detailed selected-model card;
- all inspection checks in waiting state;
- primary button labelled **Start inspection**.

The Model Import page retains the wording **Continue to model inspection** because that button navigates to this feature.

### 5.2 During inspection

The page keeps the same model card and overall geometry. The active check shows the loading spinner. Completed checks become checkmarks. Waiting checks remain neutral.

The fixed bottom action region changes from Start to Cancel without changing its height.

### 5.3 After inspection

The checks area and result area update in place. The page displays the generic result card configured for the final outcome.

Only `Ready` and `ReadyWithWarnings` enable the action:

**Can this model run on my computer?**

That action creates a clean handoff to the later LLM Fit hardware-analysis feature.

## 6. Proposed first-version inspection stages

The visual stages must reflect genuine operations rather than invented independent runtime callbacks:

1. Preparing inspection
2. Initialising local runtime
3. Reading model through `llama.cpp`
4. Collecting model and tokenizer information
5. Evaluating runtime findings
6. Finalising result

The exact wording may be refined after the LLamaSharp feasibility spike confirms which operations and callbacks are genuinely observable.

## 7. Data flow

### 7.1 Import handoff

The existing Model Import feature passes a `ModelInspectionRequest` containing:

- selected model path;
- selected filename and format;
- successful validated quick-scan result;
- file length and last-modified identity where available.

The inspection page does not receive or retain the Model Import page object.

### 7.2 Inspection command flow

```text
User selects Start inspection
        ↓
ModelInspectionViewModel
        ↓
IModelInspectionService.InspectAsync(...)
        ↓
ModelInspectionService
        ↓
ILlamaModelProbe.ProbeAsync(...)
        ↓
LlamaSharpModelProbe
        ↓
LLamaSharp
        ↓
pinned llama.cpp backend
```

### 7.3 Return flow

```text
llama.cpp evidence and progress
        ↓
LlamaSharpModelProbe normalises evidence
        ↓
ModelInspectionService coordinates classification
        ↓
ModelInspectionClassifier returns application outcome
        ↓
ModelInspectionResult returns to ViewModel
        ↓
ViewModel updates observable state
        ↓
XAML displays the matching result
```

### 7.4 Stale-result protection

Every run owns its own cancellation token and run identity. A cancelled or replaced run may not update the current page after a newer run has started. This preserves the stale-result protection already used by Model Import quick scanning.

## 8. Lightweight inspection boundary

Model inspection occurs before LLM Fit hardware analysis. The first inspection phase must therefore avoid blindly performing an unsafe full model allocation on a machine that may not have enough memory.

The intended split is:

### Model inspection now

- runtime format parsing;
- architecture recognition;
- metadata collection;
- vocabulary/tokenizer inspection;
- chat-template inspection;
- lightweight runtime compatibility evidence.

### Final runtime verification later

After hardware fit is confirmed:

- full tensor-data checking;
- full model allocation;
- context creation;
- small generation smoke test.

The first implementation activity is a focused LLamaSharp feasibility spike to prove the lightest safe inspection path available through the selected LLamaSharp version and exact native backend. The spike may use the high-level API, lower-level LLamaSharp native API, or a small internal extension hidden behind `ILlamaModelProbe`.

## 9. Error handling and safety

The selected GGUF remains untrusted input even though processing is local.

Required behaviour:

- open the model read-only;
- never alter the original file;
- preserve the existing quick-scan safety gate;
- confirm the file still exists and was not silently replaced;
- support cancellation;
- map known failures to stable diagnostic codes;
- keep raw native exception text out of the primary user message;
- release native resources on every exit path;
- do not keep the model loaded merely to show inspection metadata;
- do not upload the model or require an HTTP service;
- do not label the model invalid when the inspection runtime itself failed.

Suggested diagnostic groups:

```text
MI-WARN-*       non-blocking warning
MI-CONV-*       verified preparation requirement
MI-UNSUP-*      unsupported model
MI-INVALID-*    invalid or incomplete model
MI-OP-*         operational inspection failure
```

## 10. Version policy

Inspection and later GGUF inference must use the same pinned native `llama.cpp` build.

The completed result records:

- LLamaSharp managed version;
- native `llama.cpp` version or commit;
- native backend/build type;
- application version;
- inspection mode.

This prevents a model from being approved by one runtime revision and later rejected by a different inference revision.

## 11. Testing strategy

### 11.1 Classifier unit tests

Use project-owned evidence objects to verify every model outcome and operational-failure separation.

### 11.2 Service unit tests

Replace `ILlamaModelProbe` with a fake and verify:

- stage mapping;
- cancellation;
- classifier invocation;
- operational-failure conversion;
- final result construction.

### 11.3 ViewModel unit tests

Replace `IModelInspectionService` with a fake and verify:

- awaiting-to-inspecting transition;
- active spinner stage;
- cancellation;
- Ready progression;
- Ready-with-warnings details action;
- blocking outcomes;
- retry behaviour;
- stale-result rejection.

### 11.4 Runtime integration tests

Use controlled fixtures for:

- supported Granite GGUF;
- corrupt or truncated GGUF;
- unsupported architecture;
- missing chat template;
- cancellation during model reading;
- missing native backend;
- file-access failure.

### 11.5 UI and accessibility tests

Verify:

- Start and Cancel actions;
- current-stage `ProgressRing`;
- generic result card in all five states;
- details dialog and Expander;
- keyboard navigation and focus;
- large text scaling;
- stable geometry during transitions;
- reduced-motion behaviour.

### 11.6 Integrity tests

Hash the original model before and after inspection and confirm that it is unchanged. Confirm that the core inspection route works without internet access and does not open an HTTP port.

## 12. First delivery scope

The first usable delivery includes:

1. navigation from Model Import;
2. detailed selected-model card;
3. Start inspection action;
4. symmetrical inspection checks;
5. active loading spinner;
6. cancellation;
7. LLamaSharp runtime probe;
8. evidence classification;
9. one generic result card supporting five outcomes;
10. one reusable details dialog with technical Expander;
11. progression to LLM Fit only for Ready and Ready with warnings.

## 13. Explicitly deferred work

The first delivery does not include:

- five separate result controls;
- a separate technical-details page;
- a feature-specific shared style dictionary;
- exportable inspection reports;
- worker-process isolation;
- full tensor validation before hardware fit;
- full model load and generation smoke test;
- advanced ETA prediction;
- elaborate animation.

These may be added only when the core route works and a concrete requirement justifies them.

## 14. Implementation order after approval

1. LLamaSharp feasibility spike
2. compact data contracts and outcome rules
3. classifier unit tests and classifier
4. `ILlamaModelProbe` and `LlamaSharpModelProbe`
5. `IModelInspectionService` and `ModelInspectionService`
6. ViewModel state machine and tests
7. Model Import to Model Inspection navigation handoff
8. selected-model card and checks card
9. generic result card
10. reusable details dialog
11. end-to-end supported, warning, unsupported, invalid, cancelled, and operational-failure tests
12. Ready/Ready-with-warnings handoff to the later LLM Fit feature

## 15. Definition of Done

The feature is complete when:

- a successfully quick-scanned GGUF reaches Model Inspection with its existing evidence;
- the initial button says **Start inspection**;
- the UI remains responsive;
- the current real stage displays one animated spinner;
- cancellation is controlled and stale results cannot overwrite newer state;
- LLamaSharp uses the exact pinned native backend;
- LLamaSharp/native types remain inside `LlamaSharpModelProbe`;
- the classifier deterministically produces the correct model outcome;
- all five model outcomes can be displayed through one result card;
- warning and failure details open through the reusable dialog;
- operational failures are not misclassified as model failures;
- only Ready and Ready with warnings continue to LLM Fit;
- the original GGUF is unchanged;
- unit, integration, UI-state, accessibility, and offline tests pass;
- runtime identity and diagnostic evidence are recorded.

## 16. Source and textbook basis

This design uses the following project and textbook guidance:

- the current Model Import quick-scan workflow and its validated handoff state;
- the project user-journey and backend-workflow drafts for Model Inspection;
- `windows-apps.pdf` for WinUI 3, XAML, native Windows interaction, progress, motion, and accessibility guidance;
- *Code Complete*, especially design in construction, information hiding, loose coupling, defensive programming, developer testing, and incremental integration;
- *Fundamentals of Software Architecture*, especially modularity, cohesion, coupling, component responsibility, architectural decisions, and trade-off analysis;
- *The Art of Unit Testing*, especially dependency injection, common interfaces, asynchronous testing, and Extract Adapter;
- *Designing Secure Software*, especially trust boundaries, untrusted input, narrow interfaces, secure programming, and security testing;
- *The UX Book*, especially interaction design, visible outcomes, affordances, progress feedback, assessment, and progressive disclosure;
- *Systems Engineering: Principles and Practice*, especially functional allocation, interface definition, risk reduction through prototyping, integration, and traceable test planning;
- *AI Engineering*, especially starting with the simplest workable design, evaluating components independently, and treating runtime/model evidence as an engineering evaluation problem.
