<!-- GENERATED FILE: edit the RTM workbook or generator, then regenerate. -->


# Full Requirement Baseline

**Generated on:** 2026-07-14  
**Requirement count:** 111

> This baseline deliberately retains Active, Deferred, Superseded and Excluded records so that scope history is not lost.

<a id="req-f-m01"></a>
## REQ:F-M01 — The application must open on the target Windows 11 x64 Intel computer.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Partially Verified |
| Target date | 2026-08-14 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M01/](../../evidence/requirements/F-M01) |

### Rationale

A usable prototype must launch on the target platform.

### Acceptance and verification

- **Acceptance criteria:** Clean checkout builds; release build launches twice on the target laptop without fatal error.
- **Verification method:** Clean-build test and release demo
- **Test/evidence ID:** `AC-F-M01`

### Allocation

- Objectives: `O1`, `O11`
- Research questions: `RQ4`
- Work packages: `FR-04`, `IM-01`
- Planned components: `WinUI shell`, `packaging`
- Dependencies: `Windows App SDK`, `x64 build`

### Source and history

- Source: MoSCoW v1.1 F-M01; Project Definition §5
- Previous/replacement ID: —
- Notes: ModelImportPage and release packaging still need completion.

<a id="req-f-m02"></a>
## REQ:F-M02 — The application must let the user select a supported local model.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | In Progress |
| Target date | 2026-07-16 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M02/](../../evidence/requirements/F-M02) |

### Rationale

Model import is the first user task.

### Acceptance and verification

- **Acceptance criteria:** A user selects a valid GGUF file with the Windows picker; cancel returns safely; path is retained for inspection.
- **Verification method:** Picker integration test and UI test
- **Test/evidence ID:** `AC-F-M02`

### Allocation

- Objectives: `O2`
- Research questions: `RQ4`
- Work packages: `IM-02`
- Planned components: `ModelImportPage`, `ModelImportViewModel`, `picker service`
- Dependencies: `Windows file picker`

### Source and history

- Source: MoSCoW v1.1 F-M02; Project Definition §5.2.2
- Previous/replacement ID: —
- Notes: —

<a id="req-f-m03"></a>
## REQ:F-M03 — The application must validate a selected input before using it.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-07-18 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M03/](../../evidence/requirements/F-M03) |

### Rationale

Untrusted or corrupt files must not reach a runtime.

### Acceptance and verification

- **Acceptance criteria:** Missing, empty, wrong-type, truncated and corrupt fixtures are rejected with a classified reason; valid fixture proceeds.
- **Verification method:** Automated validation matrix
- **Test/evidence ID:** `AC-F-M03`

### Allocation

- Objectives: `O2`, `O8`
- Research questions: `RQ4`
- Work packages: `IM-03`, `IM-04`
- Planned components: `InputValidationService`, `GGUF header validator`
- Dependencies: `F-M02`

### Source and history

- Source: MoSCoW v1.1 F-M03; Project Definition §5.2.2–5.2.4
- Previous/replacement ID: —
- Notes: —

<a id="req-f-m04"></a>
## REQ:F-M04 — The application must show the correct inspection result state.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-07-21 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M04/](../../evidence/requirements/F-M04) |

### Rationale

Users need a consistent decision before continuing.

### Acceptance and verification

- **Acceptance criteria:** Fixtures produce Ready, Ready with warnings, Conversion required, Unsupported, and Invalid or incomplete.
- **Verification method:** State-decision table test and UI state test
- **Test/evidence ID:** `AC-F-M04`

### Allocation

- Objectives: `O2`, `O8`
- Research questions: `RQ4`
- Work packages: `IM-06`, `IM-07`
- Planned components: `InspectionResult`, `result pages/state mapper`
- Dependencies: `F-M03`, `F-M05`

### Source and history

- Source: MoSCoW v1.1 F-M04; acceptance rule p.6
- Previous/replacement ID: —
- Notes: —

<a id="req-f-m05"></a>
## REQ:F-M05 — The application must display the model details needed for compatibility and memory checks.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-07-19 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M05/](../../evidence/requirements/F-M05) |

### Rationale

Later decisions depend on trustworthy metadata.

### Acceptance and verification

- **Acceptance criteria:** For a verified Granite GGUF, the app displays format, architecture/name where available, file size, weight type, context information, tokenizer/chat-template information or explicit missing-data warnings.
- **Verification method:** Real-model inspection test and fixture tests
- **Test/evidence ID:** `AC-F-M05`

### Allocation

- Objectives: `O2`, `O3`, `O4`
- Research questions: `RQ3`, `RQ4`
- Work packages: `IM-05`
- Planned components: `GGUFMetadataInspector`, `ModelDescriptor`, `ModelOverviewPage`
- Dependencies: `F-M03`

### Source and history

- Source: MoSCoW v1.1 F-M05; Project Definition §5.2.4
- Previous/replacement ID: —
- Notes: —

<a id="req-f-m06"></a>
## REQ:F-M06 — The application must show plain-English reasons for warnings or failures.

| Field | Value |
|---|---|
| Category | Usability |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-12 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M06/](../../evidence/requirements/F-M06) |

### Rationale

Non-specialist users must understand what happened.

### Acceptance and verification

- **Acceptance criteria:** Every tested failure code maps to a non-technical explanation and a valid recovery action; no raw exception is the only user message.
- **Verification method:** Failure-message tests and task-based usability test
- **Test/evidence ID:** `AC-F-M06`

### Allocation

- Objectives: `O1`, `O2`, `O8`
- Research questions: `RQ4`
- Work packages: `FR-02`, `IM-07`, `RT-05`
- Planned components: `Diagnostic catalogue`, `error mapper`, `UI messages`
- Dependencies: `PD-07`

### Source and history

- Source: MoSCoW v1.1 F-M06; UX requirements
- Previous/replacement ID: —
- Notes: —

<a id="req-f-m07"></a>
## REQ:F-M07 — The application must read the hardware information needed for fit analysis.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-07-23 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M07/](../../evidence/requirements/F-M07) |

### Rationale

Memory and backend recommendations require current hardware data.

### Acceptance and verification

- **Acceptance criteria:** The app records CPU details, installed and available RAM, available GPU/device information, disk space and runtime availability with clear units; values are cross-checked against trusted Windows tools.
- **Verification method:** Hardware service unit/integration test
- **Test/evidence ID:** `AC-F-M07`

### Allocation

- Objectives: `O3`, `O4`
- Research questions: `RQ1`, `RQ3`, `RQ4`
- Work packages: `HE-01`, `HE-02`
- Planned components: `HardwareSnapshotService`, `RuntimeCapabilityRegistry`
- Dependencies: `Windows system APIs`

### Source and history

- Source: MoSCoW v1.1 F-M07; Project Definition §5.2.5
- Previous/replacement ID: —
- Notes: —

<a id="req-f-m08"></a>
## REQ:F-M08 — The application must estimate peak memory for a supported configuration.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-07-25 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M08/](../../evidence/requirements/F-M08) |

### Rationale

Users need a transparent fit prediction before loading a model.

### Acceptance and verification

- **Acceptance criteria:** The estimate shows weights, KV cache, runtime/app overhead, OS allowance and safety reserve; matched measured error is recorded.
- **Verification method:** Formula/unit tests and predicted-versus-measured calibration
- **Test/evidence ID:** `AC-F-M08`

### Allocation

- Objectives: `O3`, `O10`
- Research questions: `RQ3`, `RQ4`
- Work packages: `HE-03`, `HE-04`
- Planned components: `MemoryEstimatorService`, `EstimationResult`
- Dependencies: `F-M05`, `F-M07`

### Source and history

- Source: MoSCoW v1.1 F-M08; Project Definition §5.2.6
- Previous/replacement ID: —
- Notes: —

<a id="req-f-m09"></a>
## REQ:F-M09 — The application must show whether a configuration is likely to fit, needs optimisation, or has no verified safe option.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-07-28 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M09/](../../evidence/requirements/F-M09) |

### Rationale

The raw estimate must become an understandable decision.

### Acceptance and verification

- **Acceptance criteria:** Boundary tests change the result at documented thresholds and show the limiting reason and uncertainty.
- **Verification method:** Boundary and UI decision tests
- **Test/evidence ID:** `AC-F-M09`

### Allocation

- Objectives: `O3`, `O4`, `O8`
- Research questions: `RQ3`, `RQ4`
- Work packages: `HE-07`
- Planned components: `FitDecisionService`, `compatibility UI`
- Dependencies: `F-M08`

### Source and history

- Source: MoSCoW v1.1 F-M09; Project Definition §5.2.6
- Previous/replacement ID: —
- Notes: —

<a id="req-f-m10"></a>
## REQ:F-M10 — The application must generate only complete configurations valid for the current model, runtime and device.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-07-26 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M10/](../../evidence/requirements/F-M10) |

### Rationale

Individually valid settings can still form an invalid combination.

### Acceptance and verification

- **Acceptance criteria:** All approved combinations are generated; all known invalid model/weight/cache/runtime/backend/device combinations are rejected.
- **Verification method:** Decision-table and registry tests
- **Test/evidence ID:** `AC-F-M10`

### Allocation

- Objectives: `O4`, `O8`
- Research questions: `RQ1`, `RQ3`, `RQ4`
- Work packages: `HE-02`, `HE-05`
- Planned components: `CapabilityRegistry`, `CandidateConfigurationGenerator`
- Dependencies: `F-M05`, `F-M07`

### Source and history

- Source: MoSCoW v1.1 F-M10; Project Definition §5.2.7
- Previous/replacement ID: —
- Notes: —

<a id="req-f-m11"></a>
## REQ:F-M11 — The application must provide Automatic, Quality, Balanced and Efficiency modes when valid alternatives exist.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | In Progress |
| Target date | 2026-07-27 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M11/](../../evidence/requirements/F-M11) |

### Rationale

Users need goal-based choices rather than low-level flags.

### Acceptance and verification

- **Acceptance criteria:** Each visible mode resolves to a complete valid configuration with a documented ranking rationale; unavailable modes are hidden or disabled; slider label updates correctly.
- **Verification method:** Mode algorithm tests and UI interaction tests
- **Test/evidence ID:** `AC-F-M11`

### Allocation

- Objectives: `O4`, `O8`
- Research questions: `RQ3`, `RQ4`
- Work packages: `HE-06`
- Planned components: `ModeSelectionService`, `ModelPreferences UI`
- Dependencies: `F-M10`, `F-M08`

### Source and history

- Source: MoSCoW v1.1 F-M11; Project Definition §5.2.8
- Previous/replacement ID: —
- Notes: —

<a id="req-f-m12"></a>
## REQ:F-M12 — The application must show the chosen complete configuration before starting an operation.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-07-29 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M12/](../../evidence/requirements/F-M12) |

### Rationale

Users must understand what will run and whether it is experimental.

### Acceptance and verification

- **Acceptance criteria:** Before launch the UI shows model, runtime, backend, actual/requested device target, weight format, KV cache, context, estimated memory, status and selection reason.
- **Verification method:** UI acceptance test
- **Test/evidence ID:** `AC-F-M12`

### Allocation

- Objectives: `O4`, `O8`
- Research questions: `RQ4`
- Work packages: `HE-08`
- Planned components: `ConfigurationSummaryPage`
- Dependencies: `F-M10`, `F-M11`

### Source and history

- Source: MoSCoW v1.1 F-M12; Project Definition §5.2.8
- Previous/replacement ID: —
- Notes: —

<a id="req-f-m13"></a>
## REQ:F-M13 — The application must run local chat with at least one supported Granite GGUF model.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-02 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M13/](../../evidence/requirements/F-M13) |

### Rationale

A complete inference result proves the selected configuration is usable.

### Acceptance and verification

- **Acceptance criteria:** One real Granite GGUF completes import, inspection, fit, selection, loading and valid local generation through WinUI without manual terminal commands.
- **Verification method:** End-to-end application test
- **Test/evidence ID:** `AC-F-M13`

### Allocation

- Objectives: `O5`, `O6`, `O8`
- Research questions: `RQ1`, `RQ4`
- Work packages: `RT-01`, `RT-02`, `RT-03`, `RT-04`
- Planned components: `LlamaCppRuntimeAdapter`, `ChatService`, `ChatPage`
- Dependencies: `F-M02–F-M12`

### Source and history

- Source: MoSCoW v1.1 F-M13; Project Definition §5.2.10–5.2.12
- Previous/replacement ID: —
- Notes: —

<a id="req-f-m14"></a>
## REQ:F-M14 — The application must keep the original model file unchanged.

| Field | Value |
|---|---|
| Category | Data Integrity |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-05 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M14/](../../evidence/requirements/F-M14) |

### Rationale

Inspection and optimisation must not damage the user's source asset.

### Acceptance and verification

- **Acceptance criteria:** SHA-256 before and after inspection/processing is identical; generated artefacts use a different path.
- **Verification method:** Hash integrity test
- **Test/evidence ID:** `AC-F-M14`

### Allocation

- Objectives: `O2`, `O7`
- Research questions: `RQ4`
- Work packages: `IM-04`, `QX-01`, `QX-02`
- Planned components: `File processing services`
- Dependencies: `F-M02`

### Source and history

- Source: MoSCoW v1.1 F-M14
- Previous/replacement ID: —
- Notes: —

<a id="req-f-m15"></a>
## REQ:F-M15 — The application must give the user a clear next step after a failure.

| Field | Value |
|---|---|
| Category | Usability |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-12 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M15/](../../evidence/requirements/F-M15) |

### Rationale

Failure handling must support recovery, not only reporting.

### Acceptance and verification

- **Acceptance criteria:** Each formal failure case offers a safe action such as Retry, Choose another model/configuration, Open details, or Return to standard route.
- **Verification method:** Failure/recovery test and usability task
- **Test/evidence ID:** `AC-F-M15`

### Allocation

- Objectives: `O1`, `O8`
- Research questions: `RQ4`
- Work packages: `FR-02`, `IM-07`, `RT-05`
- Planned components: `RecoveryActionMapper`, `result/error pages`
- Dependencies: `F-M06`

### Source and history

- Source: MoSCoW v1.1 F-M15
- Previous/replacement ID: —
- Notes: —

<a id="req-f-m16"></a>
## REQ:F-M16 — The application must download at least one approved recommended Granite model from a fixed trusted source.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-07-20 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M16/](../../evidence/requirements/F-M16) |

### Rationale

The agreed product aim includes controlled model download for non-specialists.

### Acceptance and verification

- **Acceptance criteria:** A listed model shows source/licence/size/location, downloads with progress, and enters inspection after completion.
- **Verification method:** Download integration and end-to-end test
- **Test/evidence ID:** `AC-F-M16`

### Allocation

- Objectives: `O2`
- Research questions: `RQ4`
- Work packages: `DL-01`, `DL-02`, `DL-03`
- Planned components: `ApprovedModelCatalog`, `ModelDownloadService`, `download UI`
- Dependencies: `Licence/source/hash manifest`

### Source and history

- Source: Agreed Project Definition §5.2.3; promoted from C-01
- Previous/replacement ID: C-01
- Notes: New scope item; timetable must be re-baselined.

<a id="req-f-m17"></a>
## REQ:F-M17 — The application must prevent partial, corrupt or unverified downloads from being used as valid models.

| Field | Value |
|---|---|
| Category | Security / Integrity |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-07-20 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M17/](../../evidence/requirements/F-M17) |

### Rationale

Large external assets require integrity and failure controls.

### Acceptance and verification

- **Acceptance criteria:** Disk space is checked; cancellation/failure leaves no valid-state artefact; final size and SHA-256 match the approved manifest.
- **Verification method:** Failure-injection, cancellation and hash tests
- **Test/evidence ID:** `AC-F-M17`

### Allocation

- Objectives: `O2`, `O11`
- Research questions: `RQ4`
- Work packages: `DL-02`, `DL-03`
- Planned components: `ModelDownloadService`, `HashVerificationService`
- Dependencies: `F-M16`

### Source and history

- Source: Agreed Project Definition §5.2.3
- Previous/replacement ID: —
- Notes: —

<a id="req-f-m18"></a>
## REQ:F-M18 — The WinUI application must launch and control supported local command-line runtimes without requiring the user to enter terminal commands.

| Field | Value |
|---|---|
| Category | Functional / Security |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-03 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M18/](../../evidence/requirements/F-M18) |

### Rationale

The app's main contribution is to hide specialist CLI operation behind a safe adapter.

### Acceptance and verification

- **Acceptance criteria:** Structured arguments, redirected stdout/stderr, hidden console, startup/error result, timeout and process-tree cleanup all pass tests.
- **Verification method:** Contract, path-safety and integration tests
- **Test/evidence ID:** `AC-F-M18`

### Allocation

- Objectives: `O5`, `O6`, `O8`
- Research questions: `RQ1`, `RQ4`
- Work packages: `RT-01`, `RT-02`, `RT-05`
- Planned components: `ProcessRuntimeAdapter`, `runtime contracts`
- Dependencies: `PD-07`, `N-M05`

### Source and history

- Source: Agreed Objective O5; promoted from F-S13
- Previous/replacement ID: F-S13
- Notes: —

<a id="req-f-m19"></a>
## REQ:F-M19 — Chat and long-running operations must stream progress/output and support safe cancellation.

| Field | Value |
|---|---|
| Category | Functional / Reliability |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-04 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M19/](../../evidence/requirements/F-M19) |

### Rationale

Users need responsiveness and control during model operations.

### Acceptance and verification

- **Acceptance criteria:** Output appears during generation; cancellation during loading/generation/download/processing returns a controlled state and leaves no child process or partial valid artefact.
- **Verification method:** Streaming, responsiveness, cancellation and cleanup tests
- **Test/evidence ID:** `AC-F-M19`

### Allocation

- Objectives: `O5`, `O6`, `O8`
- Research questions: `RQ4`
- Work packages: `DL-02`, `IM-03`, `QX-01`, `RT-03`
- Planned components: `Progress model`, `CancellationToken flow`, `streaming parser`
- Dependencies: `F-M18`, `N-M03–N-M06`

### Source and history

- Source: Agreed Project Definition §5.2.9–5.2.12; promoted from F-S05/F-S06
- Previous/replacement ID: F-S05; F-S06
- Notes: —

<a id="req-f-m20"></a>
## REQ:F-M20 — The local chat must support at least two user turns in the same session.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-02 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M20/](../../evidence/requirements/F-M20) |

### Rationale

A real chat workflow needs maintained conversation context.

### Acceptance and verification

- **Acceptance criteria:** Two sequential prompts complete; second prompt uses the intended session context; reset starts a new session.
- **Verification method:** Multi-turn end-to-end test
- **Test/evidence ID:** `AC-F-M20`

### Allocation

- Objectives: `O6`
- Research questions: `RQ4`
- Work packages: `RT-04`
- Planned components: `ChatSessionService`, `conversation state`
- Dependencies: `F-M13`, `F-M19`

### Source and history

- Source: Agreed Project Definition §5.2.12; promoted from F-S07
- Previous/replacement ID: F-S07
- Notes: —

<a id="req-f-m21"></a>
## REQ:F-M21 — The application must run at least one verified TurboQuant-enabled Granite configuration end to end.

| Field | Value |
|---|---|
| Category | Functional / Experimental |
| Priority | Must |
| Release role | App-integrated Experimental |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-07 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M21/](../../evidence/requirements/F-M21) |

### Rationale

TurboQuant is a central contribution and must be more than an external benchmark.

### Acceptance and verification

- **Acceptance criteria:** A pinned supported model/runtime/cache/device combination is selected in WinUI, loads, generates through normal chat, proves TQ activation, records actual state, and supports cancellation.
- **Verification method:** App-integrated TurboQuant E2E and activation audit
- **Test/evidence ID:** `AC-F-M21`

### Allocation

- Objectives: `O5`, `O6`, `O8`
- Research questions: `RQ1`, `RQ2`, `RQ4`
- Work packages: `QX-03`, `QX-04`
- Planned components: `TurboQuantRuntimeAdapter`, `experimental capability registry`
- Dependencies: `F-M18`, `N-M10–N-M11`, `N-M07`

### Source and history

- Source: Agreed Project Definition §5.2.11
- Previous/replacement ID: F-S14
- Notes: —

<a id="req-f-m22"></a>
## REQ:F-M22 — A dependable upstream llama.cpp configuration must remain available when the TurboQuant route is unavailable or fails.

| Field | Value |
|---|---|
| Category | Reliability |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-07 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M22/](../../evidence/requirements/F-M22) |

### Rationale

An experimental backend must not make the core product unusable.

### Acceptance and verification

- **Acceptance criteria:** A forced TQ unavailability/failure returns the user to a verified upstream configuration without claiming TQ was active.
- **Verification method:** Forced-failure and recovery test
- **Test/evidence ID:** `AC-F-M22`

### Allocation

- Objectives: `O5`, `O6`, `O8`
- Research questions: `RQ1`, `RQ4`
- Work packages: `QX-04`, `RT-05`
- Planned components: `Fallback orchestration`, `capability registry`
- Dependencies: `F-M13`, `F-M21`

### Source and history

- Source: Agreed Project Definition §5.2.11
- Previous/replacement ID: —
- Notes: —

<a id="req-f-m23"></a>
## REQ:F-M23 — The application must create one new validated GGUF model artefact through a supported weight-quantisation workflow.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-05 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M23/](../../evidence/requirements/F-M23) |

### Rationale

The project includes a real model-file optimisation/export capability separate from KV-cache optimisation.

### Acceptance and verification

- **Acceptance criteria:** A suitable source model is processed into a new GGUF, original hash is unchanged, output loads/inspects and can be selected for chat.
- **Verification method:** Processing integration, hash, reinspection and generation tests
- **Test/evidence ID:** `AC-F-M23`

### Allocation

- Objectives: `O7`
- Research questions: `RQ3`, `RQ4`
- Work packages: `QX-01`, `QX-02`
- Planned components: `GgufQuantizationService`, `processing UI`
- Dependencies: `F-M14`, `F-M18`

### Source and history

- Source: Agreed Objective O7; promoted from F-S09
- Previous/replacement ID: F-S09
- Notes: —

<a id="req-f-m24"></a>
## REQ:F-M24 — Each generated model artefact must have a processing manifest containing source/output hashes, tool/version, settings and result.

| Field | Value |
|---|---|
| Category | Traceability / Data |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-05 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M24/](../../evidence/requirements/F-M24) |

### Rationale

Optimised artefacts need provenance and reproducibility.

### Acceptance and verification

- **Acceptance criteria:** Manifest is saved for success and classified failure and validates against the agreed schema.
- **Verification method:** Manifest schema/contract test and evidence audit
- **Test/evidence ID:** `AC-F-M24`

### Allocation

- Objectives: `O7`, `O11`
- Research questions: `RQ3`, `RQ4`
- Work packages: `QX-02`
- Planned components: `ProcessingManifestService`
- Dependencies: `F-M23`

### Source and history

- Source: Agreed Objective O7; promoted from F-S10
- Previous/replacement ID: F-S10
- Notes: —

<a id="req-f-m25"></a>
## REQ:F-M25 — The application must import and preserve at least one supported text-based knowledge file for the bounded retrieval workflow.

| Field | Value |
|---|---|
| Category | Functional / Data |
| Priority | Must |
| Release role | App-integrated Experimental |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-08 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M25/](../../evidence/requirements/F-M25) |

### Rationale

TurboVec requires a document-to-vector workflow distinct from model import.

### Acceptance and verification

- **Acceptance criteria:** A supported local file is validated, text is extracted/chunked, and the original file hash remains unchanged.
- **Verification method:** Fixture, extraction, chunking and hash tests
- **Test/evidence ID:** `AC-F-M25`

### Allocation

- Objectives: `O9`
- Research questions: `RQ-TV`, `RQ4`
- Work packages: `TV-01`, `TV-02`
- Planned components: `KnowledgeFileImportService`, `text extractor`, `chunker`
- Dependencies: `TurboVec implementation contract`

### Source and history

- Source: Agreed Objective O9; Project Definition §5.2.17
- Previous/replacement ID: R-S01
- Notes: Exact supported file type and size limit must be frozen.

<a id="req-f-m26"></a>
## REQ:F-M26 — The application must create an uncompressed embedding baseline and use the selected TurboVec implementation to compress or optimise the vectors.

| Field | Value |
|---|---|
| Category | Functional / AI |
| Priority | Must |
| Release role | App-integrated Experimental |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-09 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M26/](../../evidence/requirements/F-M26) |

### Rationale

The agreed TurboVec contribution concerns vector storage, not original document compression.

### Acceptance and verification

- **Acceptance criteria:** Pinned embedding/TurboVec versions produce both baseline and compressed vector artefacts with recorded dimensions, sizes, timings and hashes.
- **Verification method:** Adapter integration and artefact-manifest tests
- **Test/evidence ID:** `AC-F-M26`

### Allocation

- Objectives: `O9`, `O10`
- Research questions: `RQ-TV`
- Work packages: `TV-01`, `TV-02`, `TV-03`
- Planned components: `EmbeddingService`, `TurboVecAdapter`, `VectorIndex`
- Dependencies: `F-M25`, `exact TurboVec contract`

### Source and history

- Source: Agreed Objective O9; Project Definition §5.2.17
- Previous/replacement ID: R-S01
- Notes: —

<a id="req-f-m27"></a>
## REQ:F-M27 — The application must retrieve relevant sections from the local index and provide them to the Granite chat workflow.

| Field | Value |
|---|---|
| Category | Functional / AI |
| Priority | Must |
| Release role | App-integrated Experimental |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-10 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M27/](../../evidence/requirements/F-M27) |

### Rationale

The TurboVec result must be usable by the end application.

### Acceptance and verification

- **Acceptance criteria:** For a fixed question set, retrieved chunks are shown/recorded and passed into Granite; answer and retrieval evidence are retained.
- **Verification method:** Retrieval and end-to-end RAG-style test
- **Test/evidence ID:** `AC-F-M27`

### Allocation

- Objectives: `O9`, `O6`
- Research questions: `RQ-TV`, `RQ4`
- Work packages: `TV-03`, `TV-04`
- Planned components: `RetrievalService`, `prompt-context builder`, `ChatPage`
- Dependencies: `F-M13`, `F-M26`

### Source and history

- Source: Agreed Objective O9; Project Definition §5.2.17
- Previous/replacement ID: R-S01
- Notes: —

<a id="req-f-m28"></a>
## REQ:F-M28 — The user must be able to copy generated chat output.

| Field | Value |
|---|---|
| Category | Functional / Usability |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-01 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-M28/](../../evidence/requirements/F-M28) |

### Rationale

Users need to reuse useful local output.

### Acceptance and verification

- **Acceptance criteria:** Copy action places the exact selected/full generated response on the Windows clipboard and handles empty output safely.
- **Verification method:** UI/clipboard test
- **Test/evidence ID:** `AC-F-M28`

### Allocation

- Objectives: `O6`, `O8`
- Research questions: `RQ4`
- Work packages: `RT-03`
- Planned components: `ChatPage clipboard action`
- Dependencies: `F-M13`

### Source and history

- Source: Agreed Project Definition §5.2.12
- Previous/replacement ID: —
- Notes: —

<a id="req-r-m01"></a>
## REQ:R-M01 — The project must compare at least one verified TurboQuant run with a matched standard KV-cache baseline.

| Field | Value |
|---|---|
| Category | Research |
| Priority | Must |
| Release role | Research |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Partially Verified |
| Target date | 2026-08-13 |
| Owner | Arian B |
| Planned evidence | [experiments/processed-results/EXP-TQ-COMP-001/](../../../experiments/processed-results/EXP-TQ-COMP-001) |

### Rationale

The core research contribution is a controlled memory/quality/performance trade-off.

### Acceptance and verification

- **Acceptance criteria:** Same model, weights, prompt/template, context, settings, hardware and measurement method; TQ activation proved; differences reported.
- **Verification method:** Controlled matched experiment
- **Test/evidence ID:** `EXP-TQ-COMP-001`

### Allocation

- Objectives: `O8`, `O10`
- Research questions: `RQ2`
- Work packages: `FR-03`, `QX-04`
- Planned components: `Experiment harness`, `analysis workbook`
- Dependencies: `F-M21`, `R-M04–R-M07`

### Source and history

- Source: MoSCoW v1.1 R-M01; RQ2
- Previous/replacement ID: —
- Notes: Final app-integrated matched comparison still required.

<a id="req-r-m02"></a>
## REQ:R-M02 — The project must identify, pin and document the exact TurboVec implementation and its role in the release.

| Field | Value |
|---|---|
| Category | Research / Scope |
| Priority | Must |
| Release role | Research |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | In Progress |
| Target date | 2026-07-14 |
| Owner | Arian B |
| Planned evidence | [docs/architecture/decisions/ADR-TurboVec.md](../../architecture/decisions/ADR-TurboVec.md) |

### Rationale

The implementation name alone is insufficient for safe integration.

### Acceptance and verification

- **Acceptance criteria:** Repository, commit/version, licence, platform, input/output, vector representation and retrieval contract are recorded before integration.
- **Verification method:** Source inspection, licence review and technical spike
- **Test/evidence ID:** `DEC-TV-001`

### Allocation

- Objectives: `O9`
- Research questions: `RQ-TV`
- Work packages: `PD-10`, `TV-01`
- Planned components: `TurboVec ADR/contract`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 R-M02 revised by agreed scope
- Previous/replacement ID: —
- Notes: This is a critical assumption and gate.

<a id="req-r-m03"></a>
## REQ:R-M03 — The project must complete and preserve an official OpenVINO GenAI Granite baseline or a reproducible blocker.

| Field | Value |
|---|---|
| Category | Research |
| Priority | Must |
| Release role | Research |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Partially Verified |
| Target date | 2026-08-10 |
| Owner | Arian B |
| Planned evidence | [experiments/raw-results/EXP-OV-OFFICIAL-001/](../../../experiments/raw-results/EXP-OV-OFFICIAL-001) |

### Rationale

OpenVINO is the second Intel route and needs evidence distinct from community/custom routes.

### Acceptance and verification

- **Acceptance criteria:** Exact official-source model/revision, conversion/runtime versions, requested/actual device and outcome are recorded; pass or blocker is reproducible.
- **Verification method:** Official-source conversion/runtime experiment
- **Test/evidence ID:** `EXP-OV-OFFICIAL-001`

### Allocation

- Objectives: `O8`
- Research questions: `RQ1`, `RQ2`
- Work packages: `OV-01`, `OV-03`
- Planned components: `OpenVINO experiment environment`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 R-M03; updated evidence
- Previous/replacement ID: —
- Notes: Do not merge community-model evidence with official-source retest.

<a id="req-r-m04"></a>
## REQ:R-M04 — The project must measure memory use for every final test configuration.

| Field | Value |
|---|---|
| Category | Research |
| Priority | Must |
| Release role | Research |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Partially Verified |
| Target date | 2026-08-13 |
| Owner | Arian B |
| Planned evidence | [experiments/processed-results/final-metrics/](../../../experiments/processed-results/final-metrics) |

### Rationale

Memory is central to the project aim.

### Acceptance and verification

- **Acceptance criteria:** Each final row records available RAM before, peak process-tree working set/private where used, relevant GPU shared memory and measurement definition.
- **Verification method:** Metrics audit
- **Test/evidence ID:** `MET-MEM`

### Allocation

- Objectives: `O6`, `O10`
- Research questions: `RQ2`, `RQ3`
- Work packages: `FR-03`
- Planned components: `Metrics collector`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 R-M04
- Previous/replacement ID: —
- Notes: —

<a id="req-r-m05"></a>
## REQ:R-M05 — The project must evaluate runtime performance for every final test configuration.

| Field | Value |
|---|---|
| Category | Research |
| Priority | Must |
| Release role | Research |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Partially Verified |
| Target date | 2026-08-13 |
| Owner | Arian B |
| Planned evidence | [experiments/processed-results/final-metrics/](../../../experiments/processed-results/final-metrics) |

### Rationale

Memory savings alone do not prove usefulness.

### Acceptance and verification

- **Acceptance criteria:** Cold/warm load, TTFT, prompt speed, generation speed and total response duration are reported where available using fixed definitions and repetitions.
- **Verification method:** Performance experiment audit
- **Test/evidence ID:** `MET-PERF`

### Allocation

- Objectives: `O6`, `O10`
- Research questions: `RQ1`, `RQ2`, `RQ3`
- Work packages: `FR-03`
- Planned components: `Metrics collector`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 R-M05
- Previous/replacement ID: —
- Notes: —

<a id="req-r-m06"></a>
## REQ:R-M06 — The project must compare output quality with a fixed prompt set and scoring guide.

| Field | Value |
|---|---|
| Category | Research / AI Quality |
| Priority | Must |
| Release role | Research |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Partially Verified |
| Target date | 2026-08-13 |
| Owner | Arian B |
| Planned evidence | [experiments/raw-results/quality/](../../../experiments/raw-results/quality) |

### Rationale

Compression must be evaluated against model behaviour.

### Acceptance and verification

- **Acceptance criteria:** Prompt/rubric versions are frozen before final comparison; raw answers, instruction/format/fact scores and limitations are retained.
- **Verification method:** Quality evaluation
- **Test/evidence ID:** `QUAL-FINAL`

### Allocation

- Objectives: `O6`, `O10`
- Research questions: `RQ2`, `RQ3`
- Work packages: `FR-03`, `PD-09`
- Planned components: `Quality dataset and scorer`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 R-M06
- Previous/replacement ID: —
- Notes: —

<a id="req-r-m07"></a>
## REQ:R-M07 — The project must identify the largest stable tested context for selected final configurations.

| Field | Value |
|---|---|
| Category | Research |
| Priority | Must |
| Release role | Research |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Partially Verified |
| Target date | 2026-08-13 |
| Owner | Arian B |
| Planned evidence | [experiments/processed-results/context/](../../../experiments/processed-results/context) |

### Rationale

KV-cache optimisation matters most as context grows.

### Acceptance and verification

- **Acceptance criteria:** A versioned step test records maximum attempted and maximum stable context, success/retrieval result, memory and failure reason; it is not called the model maximum.
- **Verification method:** Context stability experiment
- **Test/evidence ID:** `CTX-FINAL`

### Allocation

- Objectives: `O6`, `O10`
- Research questions: `RQ2`, `RQ3`
- Work packages: `FR-03`
- Planned components: `Context step-test harness`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 R-M07
- Previous/replacement ID: —
- Notes: —

<a id="req-r-m08"></a>
## REQ:R-M08 — The project must preserve a complete record of every final experiment.

| Field | Value |
|---|---|
| Category | Research / Evidence |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | In Progress |
| Target date | 2026-08-15 |
| Owner | Arian B |
| Planned evidence | [experiments/manifests/](../../../experiments/manifests) |

### Rationale

Results must be auditable and reproducible.

### Acceptance and verification

- **Acceptance criteria:** Each formal experiment has an ID, hardware/model/runtime/config hashes, requested/actual state, command, raw stdout/stderr, measurements, outputs and failure status.
- **Verification method:** Evidence-manifest audit
- **Test/evidence ID:** `EVID-AUDIT`

### Allocation

- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`
- Work packages: `FR-05`, `PD-03`, `PD-08`
- Planned components: `Experiment manifests and evidence index`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 R-M08
- Previous/replacement ID: —
- Notes: —

<a id="req-r-m09"></a>
## REQ:R-M09 — The project must record failed attempts and known limitations.

| Field | Value |
|---|---|
| Category | Research / Evidence |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Partially Verified |
| Target date | 2026-08-15 |
| Owner | Arian B |
| Planned evidence | [docs/testing/Failure-Register.md](../../testing/Failure-Register.md) |

### Rationale

Negative results prevent biased conclusions and support troubleshooting.

### Acceptance and verification

- **Acceptance criteria:** Every formal failure has ID, stage, code, evidence, likely cause, next action and resolution status; limitations appear in final documentation.
- **Verification method:** Failure-register and report audit
- **Test/evidence ID:** `FAIL-AUDIT`

### Allocation

- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`
- Work packages: `FR-03`, `FR-05`, `PD-03`
- Planned components: `Failure register`, `known limitations`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 R-M09
- Previous/replacement ID: —
- Notes: —

<a id="req-r-m10"></a>
## REQ:R-M10 — Another developer must be able to reproduce the clean build and core result from written instructions.

| Field | Value |
|---|---|
| Category | Research / Reproducibility |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-15 |
| Owner | Arian B |
| Planned evidence | [release-evidence/reproduction/](../../../release-evidence/reproduction) |

### Rationale

Academic engineering evidence must be reproducible.

### Acceptance and verification

- **Acceptance criteria:** Independent clean-checkout reproduction completes the documented x64 build, tests and main upstream route; deviations are recorded.
- **Verification method:** Independent reproduction test
- **Test/evidence ID:** `REPRO-001`

### Allocation

- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`
- Work packages: `FR-04`, `FR-05`
- Planned components: `Build/install docs`, `reproducibility scripts`
- Dependencies: `N-M08`

### Source and history

- Source: MoSCoW v1.1 R-M10
- Previous/replacement ID: —
- Notes: —

<a id="req-r-m11"></a>
## REQ:R-M11 — The project must determine which selected Granite/runtime/backend combinations run reliably on the tested Intel CPU and integrated GPU.

| Field | Value |
|---|---|
| Category | Research |
| Priority | Must |
| Release role | Research |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Partially Verified |
| Target date | 2026-08-13 |
| Owner | Arian B |
| Planned evidence | [experiments/processed-results/cross-route/](../../../experiments/processed-results/cross-route) |

### Rationale

RQ1 requires exact compatibility evidence, not a universal Intel claim.

### Acceptance and verification

- **Acceptance criteria:** Matrix includes upstream llama.cpp, selected TQ forks, official OpenVINO and OpenVINO TQ with model, commit/version, build, requested/actual device, success/failure and role.
- **Verification method:** Cross-route evidence audit
- **Test/evidence ID:** `COMPAT-FINAL`

### Allocation

- Objectives: `O8`, `O10`
- Research questions: `RQ1`
- Work packages: `FR-03`, `PD-09`
- Planned components: `Cross-route comparison matrix`
- Dependencies: `R-M03`, `F-M13`, `F-M21`

### Source and history

- Source: Agreed RQ1 and Project Definition
- Previous/replacement ID: —
- Notes: —

<a id="req-r-m12"></a>
## REQ:R-M12 — The project must assess selected complete configurations against 4 GB, 8 GB and 16 GB total system-memory budgets.

| Field | Value |
|---|---|
| Category | Research |
| Priority | Must |
| Release role | Research |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-13 |
| Owner | Arian B |
| Planned evidence | [experiments/processed-results/memory-budgets/](../../../experiments/processed-results/memory-budgets) |

### Rationale

RQ3 explicitly concerns lower-memory feasibility.

### Acceptance and verification

- **Acceptance criteria:** Each result is labelled physical, controlled-limit, calculated or predicted and includes OS/app/model/KV/runtime/shared-memory allowance and uncertainty.
- **Verification method:** Budget-analysis audit
- **Test/evidence ID:** `MEM-BUDGET-001`

### Allocation

- Objectives: `O10`
- Research questions: `RQ3`
- Work packages: `FR-03`, `HE-04`
- Planned components: `Memory-budget analysis workbook`
- Dependencies: `F-M08`, `R-M04`

### Source and history

- Source: Agreed RQ3 and Project Definition
- Previous/replacement ID: —
- Notes: —

<a id="req-r-m13"></a>
## REQ:R-M13 — The project must compare TurboVec-compressed or optimised vectors with an uncompressed-vector baseline.

| Field | Value |
|---|---|
| Category | Research / AI Retrieval |
| Priority | Must |
| Release role | Research |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-13 |
| Owner | Arian B |
| Planned evidence | [experiments/processed-results/EXP-TV-COMP-001/](../../../experiments/processed-results/EXP-TV-COMP-001) |

### Rationale

A vector-compression feature needs a matched effectiveness test.

### Acceptance and verification

- **Acceptance criteria:** Same documents, chunking, embeddings and queries; compare storage, memory, index/query time, relevance, answer usefulness, failures and stability.
- **Verification method:** Matched retrieval experiment
- **Test/evidence ID:** `EXP-TV-COMP-001`

### Allocation

- Objectives: `O9`, `O10`
- Research questions: `RQ-TV`
- Work packages: `FR-03`, `TV-03`, `TV-04`
- Planned components: `Retrieval evaluation harness`
- Dependencies: `F-M26`, `F-M27`

### Source and history

- Source: Agreed RQ-TV and Objective O9
- Previous/replacement ID: —
- Notes: —

<a id="req-r-m14"></a>
## REQ:R-M14 — The project must quantify memory-estimator error and false-safe/false-unsafe recommendations.

| Field | Value |
|---|---|
| Category | Research / Estimation |
| Priority | Must |
| Release role | Research |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-13 |
| Owner | Arian B |
| Planned evidence | [experiments/processed-results/EST-VALID-001/](../../../experiments/processed-results/EST-VALID-001) |

### Rationale

An estimator must be calibrated rather than merely demonstrated.

### Acceptance and verification

- **Acceptance criteria:** Matched rows report predicted, measured, absolute error, percentage error, fit decision, false-safe/false-unsafe and confidence.
- **Verification method:** Estimator validation experiment
- **Test/evidence ID:** `EST-VALID-001`

### Allocation

- Objectives: `O3`, `O10`
- Research questions: `RQ3`, `RQ4`
- Work packages: `FR-03`, `HE-04`
- Planned components: `Estimator validation dataset`
- Dependencies: `F-M08`, `F-M09`, `R-M04`

### Source and history

- Source: Agreed RQ3/RQ4 and satisfactory outcome
- Previous/replacement ID: —
- Notes: —

<a id="req-n-m01"></a>
## REQ:N-M01 — The core workflow must work locally after required models and tools are installed.

| Field | Value |
|---|---|
| Category | Quality / Offline |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-12 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/N-M01/](../../evidence/requirements/N-M01) |

### Rationale

Local inference is the product's defining operational property.

### Acceptance and verification

- **Acceptance criteria:** With network disconnected, model inspection, configuration selection and upstream local chat complete using installed assets.
- **Verification method:** Offline end-to-end test
- **Test/evidence ID:** `AC-N-M01`

### Allocation

- Objectives: `O1`, `O5`, `O6`
- Research questions: `RQ4`
- Work packages: `FR-02`, `RT-05`
- Planned components: `All core components`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 N-M01
- Previous/replacement ID: —
- Notes: —

<a id="req-n-m02"></a>
## REQ:N-M02 — The application must not upload models, prompts, knowledge files or answers to a cloud AI service in the core workflow.

| Field | Value |
|---|---|
| Category | Privacy |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-12 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/N-M02/](../../evidence/requirements/N-M02) |

### Rationale

Target sectors require careful data handling.

### Acceptance and verification

- **Acceptance criteria:** Network inspection shows no cloud AI upload during core model/document/chat workflow; any initial approved download is separately documented.
- **Verification method:** Network/offline check and data-flow review
- **Test/evidence ID:** `AC-N-M02`

### Allocation

- Objectives: `O1`, `O8`, `O9`
- Research questions: `RQ-TV`, `RQ4`
- Work packages: `FR-02`
- Planned components: `Network/data-flow controls`, `logging policy`
- Dependencies: `N-M01`

### Source and history

- Source: MoSCoW v1.1 N-M02; expanded scope
- Previous/replacement ID: —
- Notes: —

<a id="req-n-m03"></a>
## REQ:N-M03 — The application window must remain responsive during long tasks.

| Field | Value |
|---|---|
| Category | Performance / UX |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-09 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/N-M03/](../../evidence/requirements/N-M03) |

### Rationale

Blocking the UI makes download, loading, quantisation and generation unusable.

### Acceptance and verification

- **Acceptance criteria:** UI input/paint remains responsive during each long task; no synchronous runtime call blocks the UI thread.
- **Verification method:** UI responsiveness test
- **Test/evidence ID:** `AC-N-M03`

### Allocation

- Objectives: `O1`, `O8`
- Research questions: `RQ4`
- Work packages: `DL-02`, `IM-03`, `QX-01`, `RT-03`, `TV-03`
- Planned components: `Async orchestration`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 N-M03
- Previous/replacement ID: —
- Notes: —

<a id="req-n-m04"></a>
## REQ:N-M04 — The application must show the current state of a long task.

| Field | Value |
|---|---|
| Category | Usability |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-09 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/N-M04/](../../evidence/requirements/N-M04) |

### Rationale

Users need progress and confidence that work is continuing.

### Acceptance and verification

- **Acceptance criteria:** Each long operation reports at least Starting, Running/Loading/Generating/Processing, Completed, Cancelled or Failed; progress is determinate where available.
- **Verification method:** State-transition and UI tests
- **Test/evidence ID:** `AC-N-M04`

### Allocation

- Objectives: `O1`, `O8`
- Research questions: `RQ4`
- Work packages: `DL-02`, `IM-03`, `QX-01`, `RT-03`, `TV-03`
- Planned components: `OperationState/Progress model`, `UI indicators`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 N-M04
- Previous/replacement ID: —
- Notes: —

<a id="req-n-m05"></a>
## REQ:N-M05 — The application must handle model/document paths and process arguments safely.

| Field | Value |
|---|---|
| Category | Security |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-08 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/N-M05/](../../evidence/requirements/N-M05) |

### Rationale

Imported paths and CLI arguments are untrusted boundaries.

### Acceptance and verification

- **Acceptance criteria:** Tests cover spaces, Unicode, quotes and supported special characters; no shell string concatenation/injection path is used.
- **Verification method:** Path and argument security tests
- **Test/evidence ID:** `AC-N-M05`

### Allocation

- Objectives: `O5`, `O11`
- Research questions: `RQ4`
- Work packages: `DL-02`, `QX-01`, `RT-01`, `RT-02`, `TV-02`
- Planned components: `Path validation`, `ProcessStartInfo argument list`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 N-M05
- Previous/replacement ID: —
- Notes: —

<a id="req-n-m06"></a>
## REQ:N-M06 — The application must clean temporary files and stopped child processes.

| Field | Value |
|---|---|
| Category | Reliability |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-09 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/N-M06/](../../evidence/requirements/N-M06) |

### Rationale

Cancellation and failures must not leave unsafe resource leaks.

### Acceptance and verification

- **Acceptance criteria:** Success, cancellation, timeout and crash tests leave no unintended child process and no partial artefact marked complete.
- **Verification method:** Cleanup/failure-injection tests
- **Test/evidence ID:** `AC-N-M06`

### Allocation

- Objectives: `O5`, `O7`, `O11`
- Research questions: `RQ4`
- Work packages: `DL-02`, `QX-01`, `RT-03`, `RT-05`, `TV-03`
- Planned components: `Process-tree cleanup`, `temp-file manager`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 N-M06
- Previous/replacement ID: —
- Notes: —

<a id="req-n-m07"></a>
## REQ:N-M07 — Each final run must record requested and actual backend, device and optimisation state.

| Field | Value |
|---|---|
| Category | Evidence / Reliability |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Partially Verified |
| Target date | 2026-08-13 |
| Owner | Arian B |
| Planned evidence | [experiments/manifests/](../../../experiments/manifests) |

### Rationale

Silent fallback would invalidate compatibility and performance claims.

### Acceptance and verification

- **Acceptance criteria:** Manifest separates requested from actual runtime/backend/device/cache and cites the evidence used to determine actual state.
- **Verification method:** Manifest audit
- **Test/evidence ID:** `AC-N-M07`

### Allocation

- Objectives: `O8`, `O10`, `O11`
- Research questions: `RQ1`, `RQ2`
- Work packages: `FR-03`, `PD-09`
- Planned components: `BackendRunResult`, `experiment manifest`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 N-M07
- Previous/replacement ID: —
- Notes: —

<a id="req-n-m08"></a>
## REQ:N-M08 — A clean copy of the repository must build and run its automated tests.

| Field | Value |
|---|---|
| Category | Maintainability / Reproducibility |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | In Progress |
| Target date | 2026-08-14 |
| Owner | Arian B |
| Planned evidence | [release-evidence/clean-build/](../../../release-evidence/clean-build) |

### Rationale

The prototype must not depend on an unrecorded local machine state.

### Acceptance and verification

- **Acceptance criteria:** Documented x64 restore/build/test commands pass from a clean checkout in the pinned environment.
- **Verification method:** Clean-build and CI test
- **Test/evidence ID:** `AC-N-M08`

### Allocation

- Objectives: `O11`
- Research questions: `RQ4`
- Work packages: `FR-04`, `PD-08`
- Planned components: `Build scripts`, `test projects`, `CI`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 N-M08
- Previous/replacement ID: —
- Notes: —

<a id="req-n-m09"></a>
## REQ:N-M09 — The repository must not contain secrets, personal test data or large proprietary model files.

| Field | Value |
|---|---|
| Category | Security / Data |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | In Progress |
| Target date | 2026-08-15 |
| Owner | Arian B |
| Planned evidence | [release-evidence/repository-scan/](../../../release-evidence/repository-scan) |

### Rationale

The public/private repo must remain safe and legally manageable.

### Acceptance and verification

- **Acceptance criteria:** Release scan finds no credentials, real patient/pupil data, unapproved model weights or prohibited third-party files.
- **Verification method:** Repository scan
- **Test/evidence ID:** `AC-N-M09`

### Allocation

- Objectives: `O11`
- Research questions: `RQ4`
- Work packages: `FR-05`, `PD-08`
- Planned components: `.gitignore`, `secret/large-file scans`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 N-M09
- Previous/replacement ID: —
- Notes: —

<a id="req-n-m10"></a>
## REQ:N-M10 — Experimental options must be clearly labelled in the interface.

| Field | Value |
|---|---|
| Category | Usability / Transparency |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-10 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/N-M10/](../../evidence/requirements/N-M10) |

### Rationale

Users must not confuse limited-evidence routes with dependable ones.

### Acceptance and verification

- **Acceptance criteria:** Every Experimental registry entry displays a persistent label and limitations before selection and launch.
- **Verification method:** Registry/UI inspection test and UX task
- **Test/evidence ID:** `AC-N-M10`

### Allocation

- Objectives: `O8`
- Research questions: `RQ4`
- Work packages: `HE-02`, `OV-02`, `QX-03`, `TV-04`
- Planned components: `Capability status badges and warnings`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 N-M10
- Previous/replacement ID: —
- Notes: —

<a id="req-n-m11"></a>
## REQ:N-M11 — An experimental option must not be reported as active unless activation is proved.

| Field | Value |
|---|---|
| Category | Evidence / Reliability |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Partially Verified |
| Target date | 2026-08-10 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/N-M11/](../../evidence/requirements/N-M11) |

### Rationale

Accepted flags and successful output are insufficient proof.

### Acceptance and verification

- **Acceptance criteria:** When proof is absent the run is labelled Unverified/Fallback rather than active; supported routes have direct activation evidence.
- **Verification method:** Log/manifest audit and forced-unverified test
- **Test/evidence ID:** `AC-N-M11`

### Allocation

- Objectives: `O8`, `O11`
- Research questions: `RQ1`, `RQ2`
- Work packages: `OV-03`, `QX-03`, `QX-04`, `TV-03`
- Planned components: `Activation verifier`, `BackendRunResult`
- Dependencies: `N-M07`

### Source and history

- Source: MoSCoW v1.1 N-M11
- Previous/replacement ID: —
- Notes: —

<a id="req-n-m12"></a>
## REQ:N-M12 — The main local inference route must not require a local web server or open network port.

| Field | Value |
|---|---|
| Category | Security / Architecture |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-07-31 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/N-M12/](../../evidence/requirements/N-M12) |

### Rationale

Direct child-process communication reduces attack surface and deployment complexity.

### Acceptance and verification

- **Acceptance criteria:** Main upstream route completes with no listening port created by the application/runtime; evidence is recorded.
- **Verification method:** Port/network inspection test
- **Test/evidence ID:** `AC-N-M12`

### Allocation

- Objectives: `O5`, `O11`
- Research questions: `RQ4`
- Work packages: `PD-06`, `RT-01`, `RT-02`
- Planned components: `Redirected standard streams or other non-network IPC`
- Dependencies: `F-M18`

### Source and history

- Source: Promoted/strengthened from F-S13; architecture decision
- Previous/replacement ID: F-S13
- Notes: —

<a id="req-n-m13"></a>
## REQ:N-M13 — The main workflow must support keyboard operation and Windows text scaling.

| Field | Value |
|---|---|
| Category | Accessibility / Usability |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-12 |
| Owner | Arian B |
| Planned evidence | [docs/ux/accessibility/](../../ux/accessibility) |

### Rationale

The intended users require a basic accessible desktop experience.

### Acceptance and verification

- **Acceptance criteria:** Core tasks complete keyboard-only; focus is visible; content remains usable at 200% Windows text scaling with no critical clipping.
- **Verification method:** Accessibility checklist and task test
- **Test/evidence ID:** `AC-N-M13`

### Allocation

- Objectives: `O1`, `O8`, `O11`
- Research questions: `RQ4`
- Work packages: `FR-02`, `IM-01`
- Planned components: `WinUI pages and controls`
- Dependencies: —

### Source and history

- Source: Promoted from N-S01; satisfactory outcome
- Previous/replacement ID: N-S01
- Notes: —

<a id="req-n-m14"></a>
## REQ:N-M14 — The project must produce a basic distributable Windows x64 release build with documented dependencies.

| Field | Value |
|---|---|
| Category | Release / Maintainability |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-15 |
| Owner | Arian B |
| Planned evidence | [release-evidence/](../../../release-evidence) |

### Rationale

A working project needs a repeatable handover artefact, even if full MSIX is deferred.

### Acceptance and verification

- **Acceptance criteria:** Release artefact launches on the target machine; checksum, commit/tag, runtime dependencies and install/run steps are recorded.
- **Verification method:** Release smoke and checksum test
- **Test/evidence ID:** `AC-N-M14`

### Allocation

- Objectives: `O11`
- Research questions: `RQ4`
- Work packages: `FR-04`, `FR-05`
- Planned components: `Release build and packaging instructions`
- Dependencies: `N-M08`

### Source and history

- Source: Satisfactory outcome; Project Definition §5.2.20
- Previous/replacement ID: —
- Notes: —

<a id="req-g-m01"></a>
## REQ:G-M01 — The project problem, aim, research questions, objectives, first-release scope, exclusions and satisfactory outcome must be version-controlled.

| Field | Value |
|---|---|
| Category | Governance |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Verified |
| Target date | 2026-07-11 |
| Owner | Arian B |
| Planned evidence | [docs/planning/Project-Definition-v1.md](../../planning/Project-Definition-v1.md) |

### Rationale

All requirements and evaluation must derive from a stable project definition.

### Acceptance and verification

- **Acceptance criteria:** Dated document contains all agreed sections, source links and status; linked from repository README.
- **Verification method:** Document review and Git history
- **Test/evidence ID:** `AC-G-M01`

### Allocation

- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`
- Work packages: `PD-01`
- Planned components: `Project-Definition-v1.md`
- Dependencies: —

### Source and history

- Source: Engineering audit; corrected timetable PD-01
- Previous/replacement ID: —
- Notes: —

<a id="req-g-m02"></a>
## REQ:G-M02 — The project must maintain one versioned MoSCoW requirements baseline with stable IDs and measurable acceptance criteria.

| Field | Value |
|---|---|
| Category | Governance / Requirements |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | In Progress |
| Target date | 2026-07-12 |
| Owner | Arian B |
| Planned evidence | [docs/requirements/MoSCoW-Requirements-v1.2.md](../../requirements/MoSCoW-Requirements-v1.2.md) |

### Rationale

A single controlled catalogue prevents contradictory requirements.

### Acceptance and verification

- **Acceptance criteria:** Every active Must has a stable ID, source, rationale, acceptance and verification; changes are dated and approved.
- **Verification method:** Requirements quality review
- **Test/evidence ID:** `AC-G-M02`

### Allocation

- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`
- Work packages: `PD-04`
- Planned components: `MoSCoW Requirements v1.2`
- Dependencies: `G-M01`

### Source and history

- Source: Engineering audit; corrected timetable PD-04
- Previous/replacement ID: —
- Notes: —

<a id="req-g-m03"></a>
## REQ:G-M03 — The project must maintain bidirectional traceability from requirements to objectives/RQs, work packages, implementation, tests and evidence.

| Field | Value |
|---|---|
| Category | Governance / Traceability |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | In Progress |
| Target date | 2026-08-15 |
| Owner | Arian B |
| Planned evidence | [docs/requirements/Requirements-Traceability-Matrix.md](../../requirements/Requirements-Traceability-Matrix.md) |

### Rationale

Nothing important should be forgotten or counted complete without proof.

### Acceptance and verification

- **Acceptance criteria:** No active Must row lacks source, rationale, acceptance, WP, component, verification, evidence path, owner and status; release audit confirms reverse links.
- **Verification method:** RTM completeness formula and manual audit
- **Test/evidence ID:** `AC-G-M03`

### Allocation

- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`
- Work packages: `FR-01`, `FR-05`, `PD-04`
- Planned components: `Excel RTM`, `GitHub Markdown snapshot`
- Dependencies: `G-M02`

### Source and history

- Source: MoSCoW §9; engineering audit; corrected timetable PD-04
- Previous/replacement ID: —
- Notes: —

<a id="req-g-m04"></a>
## REQ:G-M04 — The project must record proportionate architecture views, key ADRs, stable contracts, states and diagnostic codes.

| Field | Value |
|---|---|
| Category | Architecture / Governance |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-07-13 |
| Owner | Arian B |
| Planned evidence | [docs/architecture/](../../architecture) |

### Rationale

UI and backend work need clear boundaries and justified decisions.

### Acceptance and verification

- **Acceptance criteria:** Context/component/process/deployment views and ADRs for WinUI, adapters/no port, upstream/TQ/OpenVINO/TurboVec/evidence agree with code and requirements.
- **Verification method:** Architecture consistency review
- **Test/evidence ID:** `AC-G-M04`

### Allocation

- Objectives: `O1`, `O5`, `O8`, `O9`, `O11`
- Research questions: `RQ1`, `RQ4`
- Work packages: `PD-06`, `PD-07`
- Planned components: `Architecture diagrams`, `ADRs`, `contracts`
- Dependencies: `G-M01`

### Source and history

- Source: Engineering audit; corrected timetable PD-06/PD-07
- Previous/replacement ID: —
- Notes: —

<a id="req-g-m05"></a>
## REQ:G-M05 — The project must maintain a consolidated risk, assumption, constraint and licence register.

| Field | Value |
|---|---|
| Category | Risk / Governance |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | In Progress |
| Target date | 2026-08-15 |
| Owner | Arian B |
| Planned evidence | [docs/risks/](../../risks) |

### Rationale

High-risk experimental dependencies and sector claims require active control.

### Acceptance and verification

- **Acceptance criteria:** Each high risk has probability, impact, owner, trigger, validation, mitigation, contingency and status; assumptions are confirmed/rejected with evidence; licences are reviewed.
- **Verification method:** Register audit
- **Test/evidence ID:** `AC-G-M05`

### Allocation

- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`
- Work packages: `FR-05`, `PD-05`
- Planned components: `Risk/assumption/licence registers`
- Dependencies: `G-M01`

### Source and history

- Source: Engineering audit; corrected timetable PD-05
- Previous/replacement ID: —
- Notes: —

<a id="req-g-m06"></a>
## REQ:G-M06 — Requirement and scope changes must be linked to dated decisions, GitHub issues, pull requests/commits and affected tests.

| Field | Value |
|---|---|
| Category | Governance / Change Control |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-15 |
| Owner | Arian B |
| Planned evidence | [docs/planning/Change-Log.md](../../planning/Change-Log.md) |

### Rationale

The project must not silently rewrite history.

### Acceptance and verification

- **Acceptance criteria:** Every approved change records reason, date, affected IDs/WPs/tests/report sections and superseded/replacement links.
- **Verification method:** Change-log/PR audit
- **Test/evidence ID:** `AC-G-M06`

### Allocation

- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`
- Work packages: `FR-05`, `PD-04`, `PD-08`
- Planned components: `Change log`, `GitHub workflow`
- Dependencies: `G-M02`, `G-M03`

### Source and history

- Source: MoSCoW §9; engineering audit change-control gap
- Previous/replacement ID: —
- Notes: —

<a id="req-g-m07"></a>
## REQ:G-M07 — Irreplaceable raw evidence from completed feasibility campaigns must be recovered, hashed, indexed and backed up.

| Field | Value |
|---|---|
| Category | Evidence / Governance |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Partially Verified |
| Target date | 2026-07-11 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/Evidence-Recovery-Index.md](../../evidence/Evidence-Recovery-Index.md) |

### Rationale

Workbook summaries alone may not support final claims.

### Acceptance and verification

- **Acceptance criteria:** Upstream, AtomicBot, animehacker and OpenVINO campaigns each have model/runtime/hash manifest, raw-output location, checksum and independent backup or explicit gap.
- **Verification method:** Evidence recovery audit
- **Test/evidence ID:** `AC-G-M07`

### Allocation

- Objectives: `O11`
- Research questions: `RQ1`, `RQ2`, `RQ3`
- Work packages: `PD-03`
- Planned components: `Evidence recovery manifest`
- Dependencies: —

### Source and history

- Source: Corrected timetable PD-03; current-state audit
- Previous/replacement ID: —
- Notes: —

<a id="req-g-m08"></a>
## REQ:G-M08 — The release must include a README, user manual, developer/build guide, known limitations and final feature-status table.

| Field | Value |
|---|---|
| Category | Documentation |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-14 |
| Owner | Arian B |
| Planned evidence | [docs/manuals/](../../manuals) |

### Rationale

The system must be understandable and handover-ready.

### Acceptance and verification

- **Acceptance criteria:** Documents cover install/run/use/build/test/evidence/limitations; feature statuses match the RTM.
- **Verification method:** Documentation and walkthrough review
- **Test/evidence ID:** `AC-G-M08`

### Allocation

- Objectives: `O11`
- Research questions: `RQ4`
- Work packages: `FR-04`
- Planned components: `Manuals and README`
- Dependencies: —

### Source and history

- Source: Project Definition §5.2.20; corrected timetable FR-04
- Previous/replacement ID: —
- Notes: —

<a id="req-g-m09"></a>
## REQ:G-M09 — The final release must be tied to a Git tag, checksums, evidence pack, independent backup and evidence-based answers to every RQ.

| Field | Value |
|---|---|
| Category | Release / Reporting |
| Priority | Must |
| Release role | Core |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-15 |
| Owner | Arian B |
| Planned evidence | [release-evidence/](../../../release-evidence) |

### Rationale

The final project must have a controlled baseline and defensible conclusions.

### Acceptance and verification

- **Acceptance criteria:** Tag/commit/checksums/backups exist; all Must rows have final status/evidence; report answers each RQ and states limitations/negative results.
- **Verification method:** Final release gate audit
- **Test/evidence ID:** `AC-G-M09`

### Allocation

- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`
- Work packages: `FR-05`
- Planned components: `Release evidence pack`, `report`
- Dependencies: `G-M03`, `G-M07`, `G-M08`

### Source and history

- Source: Satisfactory outcome; corrected timetable FR-05
- Previous/replacement ID: —
- Notes: —

<a id="req-f-s01"></a>
## REQ:F-S01 — The application should support drag-and-drop model import.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Should |
| Release role | Should |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-07-17 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-S01/](../../evidence/requirements/F-S01) |

### Rationale

Drag-and-drop improves convenience after the picker route is stable.

### Acceptance and verification

- **Acceptance criteria:** Supported file drop follows the same validation path; invalid/multiple drops are handled.
- **Verification method:** UI drag/drop tests
- **Test/evidence ID:** `AC-F-S01`

### Allocation

- Objectives: `O2`
- Research questions: `RQ4`
- Work packages: `IM-03`
- Planned components: `ModelImportPage drop target`
- Dependencies: `F-M02`, `F-M03`

### Source and history

- Source: MoSCoW v1.1 F-S01
- Previous/replacement ID: —
- Notes: —

<a id="req-f-s02"></a>
## REQ:F-S02 — The application should recognise complete OpenVINO IR model folders.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Should |
| Release role | Should |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-09 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-S02/](../../evidence/requirements/F-S02) |

### Rationale

The second runtime route needs correct multi-file input recognition.

### Acceptance and verification

- **Acceptance criteria:** Valid XML/BIN/tokenizer/config folder is recognised and incomplete folder receives a classified result.
- **Verification method:** Folder fixture matrix
- **Test/evidence ID:** `AC-F-S02`

### Allocation

- Objectives: `O2`, `O8`
- Research questions: `RQ1`, `RQ4`
- Work packages: `IM-06`, `OV-02`
- Planned components: `OpenVINO folder detector`
- Dependencies: `R-M03`

### Source and history

- Source: MoSCoW v1.1 F-S02
- Previous/replacement ID: —
- Notes: —

<a id="req-f-s03"></a>
## REQ:F-S03 — The application should recognise selected Hugging Face/Safetensors model folders.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Should |
| Release role | Should |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-07-20 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-S03/](../../evidence/requirements/F-S03) |

### Rationale

Recognition enables honest Conversion required/Unsupported decisions.

### Acceptance and verification

- **Acceptance criteria:** Selected known folder structures are classified without being falsely treated as directly runnable GGUF.
- **Verification method:** Folder fixture tests
- **Test/evidence ID:** `AC-F-S03`

### Allocation

- Objectives: `O2`
- Research questions: `RQ4`
- Work packages: `IM-06`
- Planned components: `HuggingFace folder detector`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 F-S03
- Previous/replacement ID: —
- Notes: —

<a id="req-f-s04"></a>
## REQ:F-S04 — The user should be able to change the requested context length before configuration selection.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Should |
| Release role | Should |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-07-28 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-S04/](../../evidence/requirements/F-S04) |

### Rationale

Context is a major memory/quality/performance trade-off.

### Acceptance and verification

- **Acceptance criteria:** Only supported range/steps are allowed and estimator/configuration update immediately.
- **Verification method:** Boundary and UI tests
- **Test/evidence ID:** `AC-F-S04`

### Allocation

- Objectives: `O3`, `O4`
- Research questions: `RQ2`, `RQ3`, `RQ4`
- Work packages: `HE-06`, `HE-07`
- Planned components: `Context selector`
- Dependencies: `F-M08`, `F-M10`

### Source and history

- Source: MoSCoW v1.1 F-S04
- Previous/replacement ID: —
- Notes: —

<a id="req-f-s08"></a>
## REQ:F-S08 — The user should be able to copy or save the technical inspection report.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Should |
| Release role | Should |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-07-21 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-S08/](../../evidence/requirements/F-S08) |

### Rationale

Users and developers need a shareable explanation of compatibility.

### Acceptance and verification

- **Acceptance criteria:** Report includes model/hardware/result/assumptions without secrets or sensitive prompts.
- **Verification method:** Export content test
- **Test/evidence ID:** `AC-F-S08`

### Allocation

- Objectives: `O2`, `O8`
- Research questions: `RQ4`
- Work packages: `IM-07`
- Planned components: `TechnicalReportExporter`
- Dependencies: `F-M05`, `F-M06`

### Source and history

- Source: MoSCoW v1.1 F-S08
- Previous/replacement ID: —
- Notes: —

<a id="req-f-s11"></a>
## REQ:F-S11 — The application should provide one official OpenVINO GenAI inference route after its integration gate passes.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Should |
| Release role | Should |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-09 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-S11/](../../evidence/requirements/F-S11) |

### Rationale

A second Intel-optimised route strengthens the product but must not block the dependable core.

### Acceptance and verification

- **Acceptance criteria:** Pinned supported official-source configuration launches from WinUI, records actual device and generates or is explicitly deferred after gate review.
- **Verification method:** App integration E2E test
- **Test/evidence ID:** `AC-F-S11`

### Allocation

- Objectives: `O8`
- Research questions: `RQ1`, `RQ4`
- Work packages: `OV-01`, `OV-02`
- Planned components: `OpenVINORuntimeAdapter`
- Dependencies: `R-M03`, `F-M18`

### Source and history

- Source: MoSCoW v1.1 F-S11
- Previous/replacement ID: —
- Notes: —

<a id="req-f-s12"></a>
## REQ:F-S12 — The application should export benchmark results as CSV or JSON.

| Field | Value |
|---|---|
| Category | Functional |
| Priority | Should |
| Release role | Should |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-13 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/F-S12/](../../evidence/requirements/F-S12) |

### Rationale

Structured results support analysis and reproducibility.

### Acceptance and verification

- **Acceptance criteria:** Export validates against schema and contains units, evidence classification and requested/actual state.
- **Verification method:** Schema/export test
- **Test/evidence ID:** `AC-F-S12`

### Allocation

- Objectives: `O10`, `O11`
- Research questions: `RQ1`, `RQ2`, `RQ3`
- Work packages: `FR-03`
- Planned components: `BenchmarkResultExporter`
- Dependencies: `R-M04–R-M08`

### Source and history

- Source: MoSCoW v1.1 F-S12
- Previous/replacement ID: —
- Notes: —

<a id="req-f-s15"></a>
## REQ:F-S15 — The project should provide one verified source-to-OpenVINO model-preparation route.

| Field | Value |
|---|---|
| Category | Functional / Research |
| Priority | Should |
| Release role | Should |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | In Progress |
| Target date | 2026-08-10 |
| Owner | Arian B |
| Planned evidence | [experiments/raw-results/openvino-conversion/](../../../experiments/raw-results/openvino-conversion) |

### Rationale

Official provenance and reproducibility improve the OpenVINO claim.

### Acceptance and verification

- **Acceptance criteria:** Pinned IBM source revision converts in a clean recorded environment and the output passes structure/runtime checks, or a reproducible blocker is preserved.
- **Verification method:** Conversion and runtime test
- **Test/evidence ID:** `AC-F-S15`

### Allocation

- Objectives: `O8`
- Research questions: `RQ1`
- Work packages: `OV-01`, `OV-03`
- Planned components: `Conversion script/environment`
- Dependencies: `R-M03`

### Source and history

- Source: MoSCoW v1.1 F-S15
- Previous/replacement ID: —
- Notes: —

<a id="req-n-s02"></a>
## REQ:N-S02 — The project should provide a simple packaged installation route such as MSIX.

| Field | Value |
|---|---|
| Category | Release |
| Priority | Should |
| Release role | Should |
| Lifecycle | **Active** |
| Baseline version | v1.2 Draft |
| Status | Not Started |
| Target date | 2026-08-14 |
| Owner | Arian B |
| Planned evidence | [release-evidence/installer/](../../../release-evidence/installer) |

### Rationale

A packaged installer improves non-specialist deployment beyond the basic release build.

### Acceptance and verification

- **Acceptance criteria:** Fresh install/uninstall works on the target Windows machine and dependencies/limitations are documented.
- **Verification method:** Installation test
- **Test/evidence ID:** `AC-N-S02`

### Allocation

- Objectives: `O11`
- Research questions: `RQ4`
- Work packages: `FR-04`
- Planned components: `MSIX packaging`
- Dependencies: `N-M14`

### Source and history

- Source: MoSCoW v1.1 N-S02
- Previous/replacement ID: —
- Notes: —

<a id="req-f-s05"></a>
## REQ:F-S05 — The user should be able to cancel a long task.

| Field | Value |
|---|---|
| Category | Historical |
| Priority | Should |
| Release role | Superseded |
| Lifecycle | **Superseded** |
| Baseline version | v1.1 |
| Status | Superseded |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Retained only to show the controlled change into the v1.2 draft.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Superseded by a stronger active requirement.
- **Test/evidence ID:** `Change-control audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: High
- Notes: F-M19

<a id="req-f-s06"></a>
## REQ:F-S06 — Chat answers should appear while they are being generated.

| Field | Value |
|---|---|
| Category | Historical |
| Priority | Should |
| Release role | Superseded |
| Lifecycle | **Superseded** |
| Baseline version | v1.1 |
| Status | Superseded |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Retained only to show the controlled change into the v1.2 draft.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Superseded by a stronger active requirement.
- **Test/evidence ID:** `Change-control audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: High
- Notes: F-M19

<a id="req-f-s07"></a>
## REQ:F-S07 — The chat should support more than one turn in the same session.

| Field | Value |
|---|---|
| Category | Historical |
| Priority | Should |
| Release role | Superseded |
| Lifecycle | **Superseded** |
| Baseline version | v1.1 |
| Status | Superseded |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Retained only to show the controlled change into the v1.2 draft.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Superseded by a stronger active requirement.
- **Test/evidence ID:** `Change-control audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: High
- Notes: F-M20

<a id="req-f-s09"></a>
## REQ:F-S09 — The application should create a new GGUF file through a supported weight-quantisation step.

| Field | Value |
|---|---|
| Category | Historical |
| Priority | Should |
| Release role | Superseded |
| Lifecycle | **Superseded** |
| Baseline version | v1.1 |
| Status | Superseded |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Retained only to show the controlled change into the v1.2 draft.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Superseded by a stronger active requirement.
- **Test/evidence ID:** `Change-control audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: High
- Notes: F-M23

<a id="req-f-s10"></a>
## REQ:F-S10 — The application should save a processing record for each new model file.

| Field | Value |
|---|---|
| Category | Historical |
| Priority | Should |
| Release role | Superseded |
| Lifecycle | **Superseded** |
| Baseline version | v1.1 |
| Status | Superseded |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Retained only to show the controlled change into the v1.2 draft.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Superseded by a stronger active requirement.
- **Test/evidence ID:** `Change-control audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: High
- Notes: F-M24

<a id="req-f-s13"></a>
## REQ:F-S13 — The main local inference route should not need a local web server or network port.

| Field | Value |
|---|---|
| Category | Historical |
| Priority | Should |
| Release role | Superseded |
| Lifecycle | **Superseded** |
| Baseline version | v1.1 |
| Status | Superseded |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Retained only to show the controlled change into the v1.2 draft.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Superseded by a stronger active requirement.
- **Test/evidence ID:** `Change-control audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: High
- Notes: F-M18; N-M12

<a id="req-f-s14"></a>
## REQ:F-S14 — The application should expose an AtomicBot TurboQuant route only after its gate passes.

| Field | Value |
|---|---|
| Category | Historical |
| Priority | Should |
| Release role | Superseded |
| Lifecycle | **Superseded** |
| Baseline version | v1.1 |
| Status | Superseded |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Retained only to show the controlled change into the v1.2 draft.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Superseded by a stronger active requirement.
- **Test/evidence ID:** `Change-control audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: High
- Notes: F-M21

<a id="req-n-s01"></a>
## REQ:N-S01 — The main workflow should support keyboard use and Windows text scaling.

| Field | Value |
|---|---|
| Category | Historical |
| Priority | Should |
| Release role | Superseded |
| Lifecycle | **Superseded** |
| Baseline version | v1.1 |
| Status | Superseded |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Retained only to show the controlled change into the v1.2 draft.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Superseded by a stronger active requirement.
- **Test/evidence ID:** `Change-control audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: High
- Notes: N-M13

<a id="req-r-s01"></a>
## REQ:R-S01 — The project should run a small TurboVec feasibility test if the scope decision supports it.

| Field | Value |
|---|---|
| Category | Historical |
| Priority | Should |
| Release role | Superseded |
| Lifecycle | **Superseded** |
| Baseline version | v1.1 |
| Status | Superseded |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Retained only to show the controlled change into the v1.2 draft.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Superseded by a stronger active requirement.
- **Test/evidence ID:** `Change-control audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: High
- Notes: R-M02; F-M25–F-M27; R-M13

<a id="req-c-01"></a>
## REQ:C-01 — The application could help the user find or download suitable models.

| Field | Value |
|---|---|
| Category | Historical |
| Priority | Could |
| Release role | Superseded |
| Lifecycle | **Superseded** |
| Baseline version | v1.1 |
| Status | Superseded |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Retained only to show the controlled change into the v1.2 draft.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Superseded by a stronger active requirement.
- **Test/evidence ID:** `Change-control audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: High
- Notes: F-M16; F-M17

<a id="req-c-02"></a>
## REQ:C-02 — The application could keep a local benchmark history.

| Field | Value |
|---|---|
| Category | Functional / Future |
| Priority | Could |
| Release role | Could |
| Lifecycle | **Active** |
| Baseline version | v1.1 |
| Status | Not Started |
| Target date | 2026-08-15 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/C-02/](../../evidence/requirements/C-02) |

### Rationale

Lower priority or future work.

### Acceptance and verification

- **Acceptance criteria:** When approved, completed benchmark summaries can be stored, listed and removed locally without storing sensitive prompt or document content.
- **Verification method:** Repository and privacy tests
- **Test/evidence ID:** `Scope review`

### Allocation

- Objectives: `O10`, `O11`
- Research questions: —
- Work packages: `FR-03`, `FR-05`
- Planned components: `LocalBenchmarkHistoryRepository`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: Medium
- Notes: Only after core evidence/export is stable.

<a id="req-c-03"></a>
## REQ:C-03 — The application could show charts for memory and speed trade-offs.

| Field | Value |
|---|---|
| Category | Functional / Future |
| Priority | Could |
| Release role | Could |
| Lifecycle | **Active** |
| Baseline version | v1.1 |
| Status | Not Started |
| Target date | 2026-08-13 |
| Owner | Arian B |
| Planned evidence | [docs/evidence/requirements/C-03/](../../evidence/requirements/C-03) |

### Rationale

Lower priority or future work.

### Acceptance and verification

- **Acceptance criteria:** When approved, charts label units and distinguish measured, estimated and externally reported values.
- **Verification method:** Chart and data-classification test
- **Test/evidence ID:** `Scope review`

### Allocation

- Objectives: `O8`, `O10`
- Research questions: —
- Work packages: `FR-03`
- Planned components: `TradeOffChartViewModel`
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: Medium
- Notes: Dashboard visualisation is optional.

<a id="req-c-04"></a>
## REQ:C-04 — The application could support selected non-Granite GGUF models.

| Field | Value |
|---|---|
| Category | Functional / Future |
| Priority | Could |
| Release role | Deferred |
| Lifecycle | **Deferred** |
| Baseline version | v1.1 |
| Status | Deferred |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Lower priority or future work.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Only implemented through approved scope change after Must/Should stability.
- **Test/evidence ID:** `Scope review`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: Medium
- Notes: Out of first-release scope.

<a id="req-c-05"></a>
## REQ:C-05 — The project could explore DirectML as another Windows route.

| Field | Value |
|---|---|
| Category | Functional / Future |
| Priority | Could |
| Release role | Deferred |
| Lifecycle | **Deferred** |
| Baseline version | v1.1 |
| Status | Deferred |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Lower priority or future work.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Only implemented through approved scope change after Must/Should stability.
- **Test/evidence ID:** `Scope review`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: Medium
- Notes: Out of first-release scope.

<a id="req-c-06"></a>
## REQ:C-06 — The project could test an Intel NPU when suitable hardware is available.

| Field | Value |
|---|---|
| Category | Functional / Future |
| Priority | Could |
| Release role | Deferred |
| Lifecycle | **Deferred** |
| Baseline version | v1.1 |
| Status | Deferred |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Lower priority or future work.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Only implemented through approved scope change after Must/Should stability.
- **Test/evidence ID:** `Scope review`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: Medium
- Notes: No suitable NPU hardware.

<a id="req-c-07"></a>
## REQ:C-07 — The project could explore macOS and Metal after the Windows release.

| Field | Value |
|---|---|
| Category | Functional / Future |
| Priority | Could |
| Release role | Deferred |
| Lifecycle | **Deferred** |
| Baseline version | v1.1 |
| Status | Deferred |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Lower priority or future work.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Only implemented through approved scope change after Must/Should stability.
- **Test/evidence ID:** `Scope review`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: Medium
- Notes: Windows-first release.

<a id="req-c-08"></a>
## REQ:C-08 — The project could explore a cross-platform CMake backend.

| Field | Value |
|---|---|
| Category | Functional / Future |
| Priority | Could |
| Release role | Deferred |
| Lifecycle | **Deferred** |
| Baseline version | v1.1 |
| Status | Deferred |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Lower priority or future work.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Only implemented through approved scope change after Must/Should stability.
- **Test/evidence ID:** `Scope review`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: Medium
- Notes: Out of first-release scope.

<a id="req-c-09"></a>
## REQ:C-09 — The project could try a model near 32 billion parameters on stronger hardware.

| Field | Value |
|---|---|
| Category | Functional / Future |
| Priority | Could |
| Release role | Deferred |
| Lifecycle | **Deferred** |
| Baseline version | v1.1 |
| Status | Deferred |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Lower priority or future work.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Only implemented through approved scope change after Must/Should stability.
- **Test/evidence ID:** `Scope review`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: Medium
- Notes: No guaranteed 32B target hardware.

<a id="req-c-10"></a>
## REQ:C-10 — The application could support more than one model session at a time.

| Field | Value |
|---|---|
| Category | Functional / Future |
| Priority | Could |
| Release role | Deferred |
| Lifecycle | **Deferred** |
| Baseline version | v1.1 |
| Status | Deferred |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Lower priority or future work.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Only implemented through approved scope change after Must/Should stability.
- **Test/evidence ID:** `Scope review`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1
- Previous/replacement ID: Medium
- Notes: Single-session prototype.

<a id="req-w-01"></a>
## REQ:W-01 — The project will not train or fine-tune a Granite model.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.1 |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.

<a id="req-w-02"></a>
## REQ:W-02 — The first release will not support every GGUF, OpenVINO or Hugging Face model.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.1 |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.

<a id="req-w-03"></a>
## REQ:W-03 — The project will not guarantee that a 32B model runs on the target laptop.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.1 |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.

<a id="req-w-04"></a>
## REQ:W-04 — The project will not guarantee that every model runs on a 4 GB, 8 GB or 16 GB computer.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.1 |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.

<a id="req-w-05"></a>
## REQ:W-05 — The project will not claim six-times compression, near-zero quality loss or sub-second latency without its own evidence.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.1 |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.

<a id="req-w-06"></a>
## REQ:W-06 — The core workflow will not depend on cloud inference.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.1 |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.

<a id="req-w-07"></a>
## REQ:W-07 — The project will not use real patient data or provide medical advice.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.1 |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.

<a id="req-w-08"></a>
## REQ:W-08 — The interface will not show an unverified experimental option as a normal working option.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.1 |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.

<a id="req-w-09"></a>
## REQ:W-09 — The project will not build a full replacement for llama.cpp, OpenVINO, LM Studio or Ollama.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.1 |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.

<a id="req-w-10"></a>
## REQ:W-10 — Full cross-platform delivery is not required for this release.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.1 |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.

<a id="req-w-11"></a>
## REQ:W-11 — The application will not automatically re-quantise an already quantised GGUF through an unsafe route.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.1 |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.

<a id="req-w-12"></a>
## REQ:W-12 — A runtime-only TurboQuant setting will not be presented as a new exportable model file.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.1 |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.

<a id="req-w-13"></a>
## REQ:W-13 — TurboVec will not be described as directly compressing the original knowledge file.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.2 Draft |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.

<a id="req-w-14"></a>
## REQ:W-14 — The application will not provide unrestricted model downloads from arbitrary or untrusted sources.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.2 Draft |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.

<a id="req-w-15"></a>
## REQ:W-15 — The first release will not be a full enterprise RAG, document-management or vector-database platform.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.2 Draft |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.

<a id="req-w-16"></a>
## REQ:W-16 — The prototype will not claim operational clinical, NHS, school or classroom approval.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.2 Draft |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.

<a id="req-w-17"></a>
## REQ:W-17 — The first release will not provide multi-user server operation or multiple simultaneous model sessions.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.2 Draft |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.

<a id="req-w-18"></a>
## REQ:W-18 — The first release will not automatically update runtimes/models or collect prompts/documents for analytics.

| Field | Value |
|---|---|
| Category | Exclusion |
| Priority | Won't |
| Release role | Out of Scope |
| Lifecycle | **Excluded** |
| Baseline version | v1.2 Draft |
| Status | Removed |
| Target date | — |
| Owner | Arian B |
| Planned evidence | — |

### Rationale

Explicit boundary protects feasibility and claim accuracy.

### Acceptance and verification

- **Acceptance criteria:** —
- **Verification method:** Release review confirms no implementation or claim contradicts this boundary.
- **Test/evidence ID:** `Scope and claims audit`

### Allocation

- Objectives: —
- Research questions: —
- Work packages: —
- Planned components: —
- Dependencies: —

### Source and history

- Source: MoSCoW v1.1 / Project Definition §6
- Previous/replacement ID: High
- Notes: Excluded from progress denominator.
