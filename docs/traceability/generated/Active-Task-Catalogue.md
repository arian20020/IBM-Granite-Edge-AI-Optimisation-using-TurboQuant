<!-- GENERATED FILE: edit the RTM workbook or generator, then regenerate. -->


# Active Task Catalogue

**Catalogue version:** 1.3
**Generated on:** 2026-08-21
**Source workbook SHA-256:** `47186617deb1e1b6c95a26f0c10b29b84ecab07db1592c453c84659ff95b8fac`
**Active task count:** 165

> This is a generated repository snapshot. Update status and validation in the controlled RTM workbook, then regenerate these files.

## Quick navigation

- [Requirements](#requirements)
- [Work packages](#work-packages)
- [Engineering practices](#engineering-practices)

## Requirements

<a id="req-g-m01"></a>
### REQ:G-M01 — The project problem, aim, research questions, objectives, first-release scope, exclusions and satisfactory outcome must be version-controlled.

| Field | Value |
|---|---|
| Task number | 1 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-07-11 |
| Working status | Implemented |
| Validation | Validated |
| Effective status | **Verified** |
| Planned evidence | [docs/planning/Project-Definition-v1.md](../../planning/Project-Definition-v1.md) |

#### Task statement

The project problem, aim, research questions, objectives, first-release scope, exclusions and satisfactory outcome must be version-controlled.

#### Definition of Done / next action

Dated document contains all agreed sections, source links and status; linked from repository README.

#### Traceability

- Requirements: `G-M01`
- Work packages: `PD-01`
- Engineering practices: `EP-001`, `EP-032`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="req-g-m07"></a>
### REQ:G-M07 — Irreplaceable raw evidence from completed feasibility campaigns must be recovered, hashed, indexed and backed up.

| Field | Value |
|---|---|
| Task number | 2 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-07-11 |
| Working status | Partially Verified |
| Validation | Not Validated |
| Effective status | **Partially Verified** |
| Planned evidence | [docs/evidence/Evidence-Recovery-Index.md](../../evidence/Evidence-Recovery-Index.md) |

#### Task statement

Irreplaceable raw evidence from completed feasibility campaigns must be recovered, hashed, indexed and backed up.

#### Definition of Done / next action

Upstream, AtomicBot, animehacker and OpenVINO campaigns each have model/runtime/hash manifest, raw-output location, checksum and independent backup or explicit gap.

#### Traceability

- Requirements: `G-M07`
- Work packages: `PD-03`
- Engineering practices: `EP-020`, `EP-032`
- Objectives: `O11`
- Research questions: `RQ1`, `RQ2`, `RQ3`

<a id="req-g-m02"></a>
### REQ:G-M02 — The project must maintain one versioned MoSCoW requirements baseline with stable IDs and measurable acceptance criteria.

| Field | Value |
|---|---|
| Task number | 9 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-07-12 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/requirements/MoSCoW-Requirements-v1.2.md](../../requirements/MoSCoW-Requirements-v1.2.md) |

#### Task statement

The project must maintain one versioned MoSCoW requirements baseline with stable IDs and measurable acceptance criteria.

#### Definition of Done / next action

Every active Must has a stable ID, source, rationale, acceptance and verification; changes are dated and approved.

#### Traceability

- Requirements: `G-M02`
- Work packages: `PD-04`
- Engineering practices: `EP-004`, `EP-005`, `EP-006`, `EP-032`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="req-g-m04"></a>
### REQ:G-M04 — The project must record proportionate architecture views, key ADRs, stable contracts, states and diagnostic codes.

| Field | Value |
|---|---|
| Task number | 16 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-07-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/architecture/](../../architecture) |

#### Task statement

The project must record proportionate architecture views, key ADRs, stable contracts, states and diagnostic codes.

#### Definition of Done / next action

Context/component/process/deployment views and ADRs for WinUI, adapters/no port, upstream/TQ/OpenVINO/TurboVec/evidence agree with code and requirements.

#### Traceability

- Requirements: `G-M04`
- Work packages: `PD-06`, `PD-07`
- Engineering practices: `EP-008`, `EP-009`, `EP-010`, `EP-011`, `EP-012`, `EP-013`, `EP-014`, `EP-015`, `EP-016`, `EP-017`, `EP-032`
- Objectives: `O1`, `O5`, `O8`, `O9`, `O11`
- Research questions: `RQ1`, `RQ4`

<a id="req-r-m02"></a>
### REQ:R-M02 — The project must identify, pin and document the exact TurboVec implementation and its role in the release.

| Field | Value |
|---|---|
| Task number | 29 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Research |
| Deadline | 2026-07-14 |
| Working status | Implemented |
| Validation | Validated |
| Effective status | **Verified** |
| Planned evidence | [docs/architecture/decisions/ADR-TurboVec.md](../../architecture/decisions/ADR-TurboVec.md) |

#### Task statement

The project must identify, pin and document the exact TurboVec implementation and its role in the release.

#### Definition of Done / next action

This is a critical assumption and gate.

#### Traceability

- Requirements: `R-M02`
- Work packages: `PD-10`, `TV-01`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O9`
- Research questions: `RQ-TV`

<a id="req-f-m02"></a>
### REQ:F-M02 — The application must let the user select a supported local model.

| Field | Value |
|---|---|
| Task number | 37 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-07-16 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/evidence/requirements/F-M02/](../../evidence/requirements/F-M02) |

#### Task statement

The application must let the user select a supported local model.

#### Definition of Done / next action

A user selects a valid GGUF file with the Windows picker; cancel returns safely; path is retained for inspection.

#### Traceability

- Requirements: `F-M02`
- Work packages: `IM-02`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O2`
- Research questions: `RQ4`

<a id="req-f-s01"></a>
### REQ:F-S01 — The application should support drag-and-drop model import.

| Field | Value |
|---|---|
| Task number | 41 |
| Source tab | Requirements |
| Priority | Should |
| Release role / phase | Should |
| Deadline | 2026-07-17 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-S01/](../../evidence/requirements/F-S01) |

#### Task statement

The application should support drag-and-drop model import.

#### Definition of Done / next action

Supported file drop follows the same validation path; invalid/multiple drops are handled.

#### Traceability

- Requirements: `F-S01`
- Work packages: `IM-03`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O2`
- Research questions: `RQ4`

<a id="req-f-m03"></a>
### REQ:F-M03 — The application must validate a selected input before using it.

| Field | Value |
|---|---|
| Task number | 42 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-07-18 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M03/](../../evidence/requirements/F-M03) |

#### Task statement

The application must validate a selected input before using it.

#### Definition of Done / next action

Missing, empty, wrong-type, truncated and corrupt fixtures are rejected with a classified reason; valid fixture proceeds.

#### Traceability

- Requirements: `F-M03`
- Work packages: `IM-03`, `IM-04`
- Engineering practices: `EP-021`, `EP-023`, `EP-032`
- Objectives: `O2`, `O8`
- Research questions: `RQ4`

<a id="req-f-m05"></a>
### REQ:F-M05 — The application must display the model details needed for compatibility and memory checks.

| Field | Value |
|---|---|
| Task number | 44 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-07-19 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M05/](../../evidence/requirements/F-M05) |

#### Task statement

The application must display the model details needed for compatibility and memory checks.

#### Definition of Done / next action

For a verified Granite GGUF, the app displays format, architecture/name where available, file size, weight type, context information, tokenizer/chat-template information or explicit missing-data warnings.

#### Traceability

- Requirements: `F-M05`
- Work packages: `IM-05`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O2`, `O3`, `O4`
- Research questions: `RQ3`, `RQ4`

<a id="req-f-m16"></a>
### REQ:F-M16 — The application must download at least one approved recommended Granite model from a fixed trusted source.

| Field | Value |
|---|---|
| Task number | 47 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-07-20 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M16/](../../evidence/requirements/F-M16) |

#### Task statement

The application must download at least one approved recommended Granite model from a fixed trusted source.

#### Definition of Done / next action

New scope item; timetable must be re-baselined.

#### Traceability

- Requirements: `F-M16`
- Work packages: `DL-01`, `DL-02`, `DL-03`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O2`
- Research questions: `RQ4`

<a id="req-f-m17"></a>
### REQ:F-M17 — The application must prevent partial, corrupt or unverified downloads from being used as valid models.

| Field | Value |
|---|---|
| Task number | 48 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-07-20 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M17/](../../evidence/requirements/F-M17) |

#### Task statement

The application must prevent partial, corrupt or unverified downloads from being used as valid models.

#### Definition of Done / next action

Disk space is checked; cancellation/failure leaves no valid-state artefact; final size and SHA-256 match the approved manifest.

#### Traceability

- Requirements: `F-M17`
- Work packages: `DL-02`, `DL-03`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O2`, `O11`
- Research questions: `RQ4`

<a id="req-f-s03"></a>
### REQ:F-S03 — The application should recognise selected Hugging Face/Safetensors model folders.

| Field | Value |
|---|---|
| Task number | 51 |
| Source tab | Requirements |
| Priority | Should |
| Release role / phase | Should |
| Deadline | 2026-07-20 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-S03/](../../evidence/requirements/F-S03) |

#### Task statement

The application should recognise selected Hugging Face/Safetensors model folders.

#### Definition of Done / next action

Selected known folder structures are classified without being falsely treated as directly runnable GGUF.

#### Traceability

- Requirements: `F-S03`
- Work packages: `IM-06`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O2`
- Research questions: `RQ4`

<a id="req-f-m04"></a>
### REQ:F-M04 — The application must show the correct inspection result state.

| Field | Value |
|---|---|
| Task number | 52 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-07-21 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M04/](../../evidence/requirements/F-M04) |

#### Task statement

The application must show the correct inspection result state.

#### Definition of Done / next action

Fixtures produce Ready, Ready with warnings, Conversion required, Unsupported, and Invalid or incomplete.

#### Traceability

- Requirements: `F-M04`
- Work packages: `IM-06`, `IM-07`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O2`, `O8`
- Research questions: `RQ4`

<a id="req-f-s08"></a>
### REQ:F-S08 — The user should be able to copy or save the technical inspection report.

| Field | Value |
|---|---|
| Task number | 54 |
| Source tab | Requirements |
| Priority | Should |
| Release role / phase | Should |
| Deadline | 2026-07-21 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-S08/](../../evidence/requirements/F-S08) |

#### Task statement

The user should be able to copy or save the technical inspection report.

#### Definition of Done / next action

Report includes model/hardware/result/assumptions without secrets or sensitive prompts.

#### Traceability

- Requirements: `F-S08`
- Work packages: `IM-07`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O2`, `O8`
- Research questions: `RQ4`

<a id="req-f-m07"></a>
### REQ:F-M07 — The application must read the hardware information needed for fit analysis.

| Field | Value |
|---|---|
| Task number | 56 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-07-23 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M07/](../../evidence/requirements/F-M07) |

#### Task statement

The application must read the hardware information needed for fit analysis.

#### Definition of Done / next action

The app records CPU details, installed and available RAM, available GPU/device information, disk space and runtime availability with clear units; values are cross-checked against trusted Windows tools.

#### Traceability

- Requirements: `F-M07`
- Work packages: `HE-01`, `HE-02`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O3`, `O4`
- Research questions: `RQ1`, `RQ3`, `RQ4`

<a id="req-f-m08"></a>
### REQ:F-M08 — The application must estimate peak memory for a supported configuration.

| Field | Value |
|---|---|
| Task number | 59 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-07-25 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M08/](../../evidence/requirements/F-M08) |

#### Task statement

The application must estimate peak memory for a supported configuration.

#### Definition of Done / next action

The estimate shows weights, KV cache, runtime/app overhead, OS allowance and safety reserve; matched measured error is recorded.

#### Traceability

- Requirements: `F-M08`
- Work packages: `HE-03`, `HE-04`
- Engineering practices: `EP-021`, `EP-023`, `EP-030`, `EP-032`
- Objectives: `O3`, `O10`
- Research questions: `RQ3`, `RQ4`

<a id="req-f-m10"></a>
### REQ:F-M10 — The application must generate only complete configurations valid for the current model, runtime and device.

| Field | Value |
|---|---|
| Task number | 61 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-07-26 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M10/](../../evidence/requirements/F-M10) |

#### Task statement

The application must generate only complete configurations valid for the current model, runtime and device.

#### Definition of Done / next action

All approved combinations are generated; all known invalid model/weight/cache/runtime/backend/device combinations are rejected.

#### Traceability

- Requirements: `F-M10`
- Work packages: `HE-02`, `HE-05`
- Engineering practices: `EP-021`, `EP-023`, `EP-032`
- Objectives: `O4`, `O8`
- Research questions: `RQ1`, `RQ3`, `RQ4`

<a id="req-f-m11"></a>
### REQ:F-M11 — The application must provide Automatic, Quality, Balanced and Efficiency modes when valid alternatives exist.

| Field | Value |
|---|---|
| Task number | 63 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-07-27 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/evidence/requirements/F-M11/](../../evidence/requirements/F-M11) |

#### Task statement

The application must provide Automatic, Quality, Balanced and Efficiency modes when valid alternatives exist.

#### Definition of Done / next action

Each visible mode resolves to a complete valid configuration with a documented ranking rationale; unavailable modes are hidden or disabled; slider label updates correctly.

#### Traceability

- Requirements: `F-M11`
- Work packages: `HE-06`
- Engineering practices: `EP-021`, `EP-023`, `EP-032`
- Objectives: `O4`, `O8`
- Research questions: `RQ3`, `RQ4`

<a id="req-f-m09"></a>
### REQ:F-M09 — The application must show whether a configuration is likely to fit, needs optimisation, or has no verified safe option.

| Field | Value |
|---|---|
| Task number | 66 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-07-28 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M09/](../../evidence/requirements/F-M09) |

#### Task statement

The application must show whether a configuration is likely to fit, needs optimisation, or has no verified safe option.

#### Definition of Done / next action

Boundary tests change the result at documented thresholds and show the limiting reason and uncertainty.

#### Traceability

- Requirements: `F-M09`
- Work packages: `HE-07`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O3`, `O4`, `O8`
- Research questions: `RQ3`, `RQ4`

<a id="req-f-s04"></a>
### REQ:F-S04 — The user should be able to change the requested context length before configuration selection.

| Field | Value |
|---|---|
| Task number | 68 |
| Source tab | Requirements |
| Priority | Should |
| Release role / phase | Should |
| Deadline | 2026-07-28 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-S04/](../../evidence/requirements/F-S04) |

#### Task statement

The user should be able to change the requested context length before configuration selection.

#### Definition of Done / next action

Only supported range/steps are allowed and estimator/configuration update immediately.

#### Traceability

- Requirements: `F-S04`
- Work packages: `HE-06`, `HE-07`
- Engineering practices: `EP-021`, `EP-023`, `EP-032`
- Objectives: `O3`, `O4`
- Research questions: `RQ2`, `RQ3`, `RQ4`

<a id="req-f-m12"></a>
### REQ:F-M12 — The application must show the chosen complete configuration before starting an operation.

| Field | Value |
|---|---|
| Task number | 69 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-07-29 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M12/](../../evidence/requirements/F-M12) |

#### Task statement

The application must show the chosen complete configuration before starting an operation.

#### Definition of Done / next action

Before launch the UI shows model, runtime, backend, actual/requested device target, weight format, KV cache, context, estimated memory, status and selection reason.

#### Traceability

- Requirements: `F-M12`
- Work packages: `HE-08`
- Engineering practices: `EP-021`, `EP-025`, `EP-032`
- Objectives: `O4`, `O8`
- Research questions: `RQ4`

<a id="req-n-m12"></a>
### REQ:N-M12 — The main local inference route must not require a local web server or open network port.

| Field | Value |
|---|---|
| Task number | 72 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-07-31 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/N-M12/](../../evidence/requirements/N-M12) |

#### Task statement

The main local inference route must not require a local web server or open network port.

#### Definition of Done / next action

Main upstream route completes with no listening port created by the application/runtime; evidence is recorded.

#### Traceability

- Requirements: `N-M12`
- Work packages: `PD-06`, `RT-01`, `RT-02`
- Engineering practices: `EP-008`, `EP-011`, `EP-012`, `EP-013`, `EP-014`, `EP-015`, `EP-016`, `EP-021`, `EP-024`, `EP-031`, `EP-032`
- Objectives: `O5`, `O11`
- Research questions: `RQ4`

<a id="req-f-m28"></a>
### REQ:F-M28 — The user must be able to copy generated chat output.

| Field | Value |
|---|---|
| Task number | 74 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-01 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M28/](../../evidence/requirements/F-M28) |

#### Task statement

The user must be able to copy generated chat output.

#### Definition of Done / next action

Copy action places the exact selected/full generated response on the Windows clipboard and handles empty output safely.

#### Traceability

- Requirements: `F-M28`
- Work packages: `RT-03`
- Engineering practices: `EP-021`, `EP-031`, `EP-032`
- Objectives: `O6`, `O8`
- Research questions: `RQ4`

<a id="req-f-m13"></a>
### REQ:F-M13 — The application must run local chat with at least one supported Granite GGUF model.

| Field | Value |
|---|---|
| Task number | 76 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-02 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M13/](../../evidence/requirements/F-M13) |

#### Task statement

The application must run local chat with at least one supported Granite GGUF model.

#### Definition of Done / next action

One real Granite GGUF completes import, inspection, fit, selection, loading and valid local generation through WinUI without manual terminal commands.

#### Traceability

- Requirements: `F-M13`
- Work packages: `RT-01`, `RT-02`, `RT-03`, `RT-04`
- Engineering practices: `EP-021`, `EP-024`, `EP-031`, `EP-032`
- Objectives: `O5`, `O6`, `O8`
- Research questions: `RQ1`, `RQ4`

<a id="req-f-m20"></a>
### REQ:F-M20 — The local chat must support at least two user turns in the same session.

| Field | Value |
|---|---|
| Task number | 77 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-02 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M20/](../../evidence/requirements/F-M20) |

#### Task statement

The local chat must support at least two user turns in the same session.

#### Definition of Done / next action

Two sequential prompts complete; second prompt uses the intended session context; reset starts a new session.

#### Traceability

- Requirements: `F-M20`
- Work packages: `RT-04`
- Engineering practices: `EP-021`, `EP-031`, `EP-032`
- Objectives: `O6`
- Research questions: `RQ4`

<a id="req-f-m18"></a>
### REQ:F-M18 — The WinUI application must launch and control supported local command-line runtimes without requiring the user to enter terminal commands.

| Field | Value |
|---|---|
| Task number | 79 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-03 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M18/](../../evidence/requirements/F-M18) |

#### Task statement

The WinUI application must launch and control supported local command-line runtimes without requiring the user to enter terminal commands.

#### Definition of Done / next action

Structured arguments, redirected stdout/stderr, hidden console, startup/error result, timeout and process-tree cleanup all pass tests.

#### Traceability

- Requirements: `F-M18`
- Work packages: `RT-01`, `RT-02`, `RT-05`
- Engineering practices: `EP-021`, `EP-024`, `EP-025`, `EP-031`, `EP-032`
- Objectives: `O5`, `O6`, `O8`
- Research questions: `RQ1`, `RQ4`

<a id="req-f-m19"></a>
### REQ:F-M19 — Chat and long-running operations must stream progress/output and support safe cancellation.

| Field | Value |
|---|---|
| Task number | 81 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-04 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M19/](../../evidence/requirements/F-M19) |

#### Task statement

Chat and long-running operations must stream progress/output and support safe cancellation.

#### Definition of Done / next action

Output appears during generation; cancellation during loading/generation/download/processing returns a controlled state and leaves no child process or partial valid artefact.

#### Traceability

- Requirements: `F-M19`
- Work packages: `DL-02`, `IM-03`, `QX-01`, `RT-03`
- Engineering practices: `EP-021`, `EP-031`, `EP-032`
- Objectives: `O5`, `O6`, `O8`
- Research questions: `RQ4`

<a id="req-f-m14"></a>
### REQ:F-M14 — The application must keep the original model file unchanged.

| Field | Value |
|---|---|
| Task number | 83 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-05 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M14/](../../evidence/requirements/F-M14) |

#### Task statement

The application must keep the original model file unchanged.

#### Definition of Done / next action

SHA-256 before and after inspection/processing is identical; generated artefacts use a different path.

#### Traceability

- Requirements: `F-M14`
- Work packages: `IM-04`, `QX-01`, `QX-02`
- Engineering practices: `EP-021`, `EP-023`, `EP-031`, `EP-032`
- Objectives: `O2`, `O7`
- Research questions: `RQ4`

<a id="req-f-m23"></a>
### REQ:F-M23 — The application must create one new validated GGUF model artefact through a supported weight-quantisation workflow.

| Field | Value |
|---|---|
| Task number | 84 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-05 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M23/](../../evidence/requirements/F-M23) |

#### Task statement

The application must create one new validated GGUF model artefact through a supported weight-quantisation workflow.

#### Definition of Done / next action

A suitable source model is processed into a new GGUF, original hash is unchanged, output loads/inspects and can be selected for chat.

#### Traceability

- Requirements: `F-M23`
- Work packages: `QX-01`, `QX-02`
- Engineering practices: `EP-021`, `EP-031`, `EP-032`
- Objectives: `O7`
- Research questions: `RQ3`, `RQ4`

<a id="req-f-m24"></a>
### REQ:F-M24 — Each generated model artefact must have a processing manifest containing source/output hashes, tool/version, settings and result.

| Field | Value |
|---|---|
| Task number | 85 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-05 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M24/](../../evidence/requirements/F-M24) |

#### Task statement

Each generated model artefact must have a processing manifest containing source/output hashes, tool/version, settings and result.

#### Definition of Done / next action

Manifest is saved for success and classified failure and validates against the agreed schema.

#### Traceability

- Requirements: `F-M24`
- Work packages: `QX-02`
- Engineering practices: `EP-021`, `EP-031`, `EP-032`
- Objectives: `O7`, `O11`
- Research questions: `RQ3`, `RQ4`

<a id="req-f-m21"></a>
### REQ:F-M21 — The application must run at least one verified TurboQuant-enabled Granite configuration end to end.

| Field | Value |
|---|---|
| Task number | 88 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | App-integrated Experimental |
| Deadline | 2026-08-07 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M21/](../../evidence/requirements/F-M21) |

#### Task statement

The application must run at least one verified TurboQuant-enabled Granite configuration end to end.

#### Definition of Done / next action

A pinned supported model/runtime/cache/device combination is selected in WinUI, loads, generates through normal chat, proves TQ activation, records actual state, and supports cancellation.

#### Traceability

- Requirements: `F-M21`
- Work packages: `QX-03`, `QX-04`
- Engineering practices: `EP-021`, `EP-024`, `EP-031`, `EP-032`
- Objectives: `O5`, `O6`, `O8`
- Research questions: `RQ1`, `RQ2`, `RQ4`

<a id="req-f-m22"></a>
### REQ:F-M22 — A dependable upstream llama.cpp configuration must remain available when the TurboQuant route is unavailable or fails.

| Field | Value |
|---|---|
| Task number | 89 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-07 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M22/](../../evidence/requirements/F-M22) |

#### Task statement

A dependable upstream llama.cpp configuration must remain available when the TurboQuant route is unavailable or fails.

#### Definition of Done / next action

A forced TQ unavailability/failure returns the user to a verified upstream configuration without claiming TQ was active.

#### Traceability

- Requirements: `F-M22`
- Work packages: `QX-04`, `RT-05`
- Engineering practices: `EP-021`, `EP-025`, `EP-031`, `EP-032`
- Objectives: `O5`, `O6`, `O8`
- Research questions: `RQ1`, `RQ4`

<a id="req-f-m25"></a>
### REQ:F-M25 — The application must import and preserve at least one supported text-based knowledge file for the bounded retrieval workflow.

| Field | Value |
|---|---|
| Task number | 91 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | App-integrated Experimental |
| Deadline | 2026-08-08 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M25/](../../evidence/requirements/F-M25) |

#### Task statement

The application must import and preserve at least one supported text-based knowledge file for the bounded retrieval workflow.

#### Definition of Done / next action

Exact supported file type and size limit must be frozen.

#### Traceability

- Requirements: `F-M25`
- Work packages: `TV-01`, `TV-02`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O9`
- Research questions: `RQ-TV`, `RQ4`

<a id="req-n-m05"></a>
### REQ:N-M05 — The application must handle model/document paths and process arguments safely.

| Field | Value |
|---|---|
| Task number | 92 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-08 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/N-M05/](../../evidence/requirements/N-M05) |

#### Task statement

The application must handle model/document paths and process arguments safely.

#### Definition of Done / next action

Tests cover spaces, Unicode, quotes and supported special characters; no shell string concatenation/injection path is used.

#### Traceability

- Requirements: `N-M05`
- Work packages: `DL-02`, `QX-01`, `RT-01`, `RT-02`, `TV-02`
- Engineering practices: `EP-021`, `EP-024`, `EP-031`, `EP-032`
- Objectives: `O5`, `O11`
- Research questions: `RQ4`

<a id="req-f-m26"></a>
### REQ:F-M26 — The application must create an uncompressed embedding baseline and use the selected TurboVec implementation to compress or optimise the vectors.

| Field | Value |
|---|---|
| Task number | 95 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | App-integrated Experimental |
| Deadline | 2026-08-09 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M26/](../../evidence/requirements/F-M26) |

#### Task statement

The application must create an uncompressed embedding baseline and use the selected TurboVec implementation to compress or optimise the vectors.

#### Definition of Done / next action

Pinned embedding/TurboVec versions produce both baseline and compressed vector artefacts with recorded dimensions, sizes, timings and hashes.

#### Traceability

- Requirements: `F-M26`
- Work packages: `TV-01`, `TV-02`, `TV-03`
- Engineering practices: `EP-021`, `EP-024`, `EP-032`
- Objectives: `O9`, `O10`
- Research questions: `RQ-TV`

<a id="req-n-m03"></a>
### REQ:N-M03 — The application window must remain responsive during long tasks.

| Field | Value |
|---|---|
| Task number | 96 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-09 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/N-M03/](../../evidence/requirements/N-M03) |

#### Task statement

The application window must remain responsive during long tasks.

#### Definition of Done / next action

UI input/paint remains responsive during each long task; no synchronous runtime call blocks the UI thread.

#### Traceability

- Requirements: `N-M03`
- Work packages: `DL-02`, `IM-03`, `QX-01`, `RT-03`, `TV-03`
- Engineering practices: `EP-021`, `EP-024`, `EP-031`, `EP-032`
- Objectives: `O1`, `O8`
- Research questions: `RQ4`

<a id="req-n-m04"></a>
### REQ:N-M04 — The application must show the current state of a long task.

| Field | Value |
|---|---|
| Task number | 97 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-09 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/N-M04/](../../evidence/requirements/N-M04) |

#### Task statement

The application must show the current state of a long task.

#### Definition of Done / next action

Each long operation reports at least Starting, Running/Loading/Generating/Processing, Completed, Cancelled or Failed; progress is determinate where available.

#### Traceability

- Requirements: `N-M04`
- Work packages: `DL-02`, `IM-03`, `QX-01`, `RT-03`, `TV-03`
- Engineering practices: `EP-021`, `EP-024`, `EP-031`, `EP-032`
- Objectives: `O1`, `O8`
- Research questions: `RQ4`

<a id="req-n-m06"></a>
### REQ:N-M06 — The application must clean temporary files and stopped child processes.

| Field | Value |
|---|---|
| Task number | 98 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-09 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/N-M06/](../../evidence/requirements/N-M06) |

#### Task statement

The application must clean temporary files and stopped child processes.

#### Definition of Done / next action

Success, cancellation, timeout and crash tests leave no unintended child process and no partial artefact marked complete.

#### Traceability

- Requirements: `N-M06`
- Work packages: `DL-02`, `QX-01`, `RT-03`, `RT-05`, `TV-03`
- Engineering practices: `EP-021`, `EP-024`, `EP-025`, `EP-031`, `EP-032`
- Objectives: `O5`, `O7`, `O11`
- Research questions: `RQ4`

<a id="req-f-s02"></a>
### REQ:F-S02 — The application should recognise complete OpenVINO IR model folders.

| Field | Value |
|---|---|
| Task number | 101 |
| Source tab | Requirements |
| Priority | Should |
| Release role / phase | Should |
| Deadline | 2026-08-09 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-S02/](../../evidence/requirements/F-S02) |

#### Task statement

The application should recognise complete OpenVINO IR model folders.

#### Definition of Done / next action

Valid XML/BIN/tokenizer/config folder is recognised and incomplete folder receives a classified result.

#### Traceability

- Requirements: `F-S02`
- Work packages: `IM-06`, `OV-02`
- Engineering practices: `EP-021`, `EP-024`, `EP-031`, `EP-032`
- Objectives: `O2`, `O8`
- Research questions: `RQ1`, `RQ4`

<a id="req-f-s11"></a>
### REQ:F-S11 — The application should provide one official OpenVINO GenAI inference route after its integration gate passes.

| Field | Value |
|---|---|
| Task number | 102 |
| Source tab | Requirements |
| Priority | Should |
| Release role / phase | Should |
| Deadline | 2026-08-09 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-S11/](../../evidence/requirements/F-S11) |

#### Task statement

The application should provide one official OpenVINO GenAI inference route after its integration gate passes.

#### Definition of Done / next action

Pinned supported official-source configuration launches from WinUI, records actual device and generates or is explicitly deferred after gate review.

#### Traceability

- Requirements: `F-S11`
- Work packages: `OV-01`, `OV-02`
- Engineering practices: `EP-021`, `EP-024`, `EP-031`, `EP-032`
- Objectives: `O8`
- Research questions: `RQ1`, `RQ4`

<a id="req-f-m27"></a>
### REQ:F-M27 — The application must retrieve relevant sections from the local index and provide them to the Granite chat workflow.

| Field | Value |
|---|---|
| Task number | 104 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | App-integrated Experimental |
| Deadline | 2026-08-10 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M27/](../../evidence/requirements/F-M27) |

#### Task statement

The application must retrieve relevant sections from the local index and provide them to the Granite chat workflow.

#### Definition of Done / next action

For a fixed question set, retrieved chunks are shown/recorded and passed into Granite; answer and retrieval evidence are retained.

#### Traceability

- Requirements: `F-M27`
- Work packages: `TV-03`, `TV-04`
- Engineering practices: `EP-021`, `EP-024`, `EP-032`
- Objectives: `O9`, `O6`
- Research questions: `RQ-TV`, `RQ4`

<a id="req-n-m10"></a>
### REQ:N-M10 — Experimental options must be clearly labelled in the interface.

| Field | Value |
|---|---|
| Task number | 105 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-10 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/N-M10/](../../evidence/requirements/N-M10) |

#### Task statement

Experimental options must be clearly labelled in the interface.

#### Definition of Done / next action

Every Experimental registry entry displays a persistent label and limitations before selection and launch.

#### Traceability

- Requirements: `N-M10`
- Work packages: `HE-02`, `OV-02`, `QX-03`, `TV-04`
- Engineering practices: `EP-021`, `EP-024`, `EP-031`, `EP-032`
- Objectives: `O8`
- Research questions: `RQ4`

<a id="req-n-m11"></a>
### REQ:N-M11 — An experimental option must not be reported as active unless activation is proved.

| Field | Value |
|---|---|
| Task number | 106 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-10 |
| Working status | Partially Verified |
| Validation | Not Validated |
| Effective status | **Partially Verified** |
| Planned evidence | [docs/evidence/requirements/N-M11/](../../evidence/requirements/N-M11) |

#### Task statement

An experimental option must not be reported as active unless activation is proved.

#### Definition of Done / next action

When proof is absent the run is labelled Unverified/Fallback rather than active; supported routes have direct activation evidence.

#### Traceability

- Requirements: `N-M11`
- Work packages: `OV-03`, `QX-03`, `QX-04`, `TV-03`
- Engineering practices: `EP-021`, `EP-024`, `EP-031`, `EP-032`
- Objectives: `O8`, `O11`
- Research questions: `RQ1`, `RQ2`

<a id="req-r-m03"></a>
### REQ:R-M03 — The project must complete and preserve an official OpenVINO GenAI Granite baseline or a reproducible blocker.

| Field | Value |
|---|---|
| Task number | 107 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Research |
| Deadline | 2026-08-10 |
| Working status | Partially Verified |
| Validation | Not Validated |
| Effective status | **Partially Verified** |
| Planned evidence | [experiments/raw-results/EXP-OV-OFFICIAL-001/](../../../experiments/raw-results/EXP-OV-OFFICIAL-001) |

#### Task statement

The project must complete and preserve an official OpenVINO GenAI Granite baseline or a reproducible blocker.

#### Definition of Done / next action

Do not merge community-model evidence with official-source retest.

#### Traceability

- Requirements: `R-M03`
- Work packages: `OV-01`, `OV-03`
- Engineering practices: `EP-021`, `EP-031`, `EP-032`
- Objectives: `O8`
- Research questions: `RQ1`, `RQ2`

<a id="req-f-s15"></a>
### REQ:F-S15 — The project should provide one verified source-to-OpenVINO model-preparation route.

| Field | Value |
|---|---|
| Task number | 109 |
| Source tab | Requirements |
| Priority | Should |
| Release role / phase | Should |
| Deadline | 2026-08-10 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [experiments/raw-results/openvino-conversion/](../../../experiments/raw-results/openvino-conversion) |

#### Task statement

The project should provide one verified source-to-OpenVINO model-preparation route.

#### Definition of Done / next action

Pinned IBM source revision converts in a clean recorded environment and the output passes structure/runtime checks, or a reproducible blocker is preserved.

#### Traceability

- Requirements: `F-S15`
- Work packages: `OV-01`, `OV-03`
- Engineering practices: `EP-021`, `EP-031`, `EP-032`
- Objectives: `O8`
- Research questions: `RQ1`

<a id="req-f-m06"></a>
### REQ:F-M06 — The application must show plain-English reasons for warnings or failures.

| Field | Value |
|---|---|
| Task number | 115 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-12 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M06/](../../evidence/requirements/F-M06) |

#### Task statement

The application must show plain-English reasons for warnings or failures.

#### Definition of Done / next action

Every tested failure code maps to a non-technical explanation and a valid recovery action; no raw exception is the only user message.

#### Traceability

- Requirements: `F-M06`
- Work packages: `FR-02`, `IM-07`, `RT-05`
- Engineering practices: `EP-002`, `EP-021`, `EP-025`, `EP-026`, `EP-027`, `EP-031`, `EP-032`
- Objectives: `O1`, `O2`, `O8`
- Research questions: `RQ4`

<a id="req-f-m15"></a>
### REQ:F-M15 — The application must give the user a clear next step after a failure.

| Field | Value |
|---|---|
| Task number | 116 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-12 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-M15/](../../evidence/requirements/F-M15) |

#### Task statement

The application must give the user a clear next step after a failure.

#### Definition of Done / next action

Each formal failure case offers a safe action such as Retry, Choose another model/configuration, Open details, or Return to standard route.

#### Traceability

- Requirements: `F-M15`
- Work packages: `FR-02`, `IM-07`, `RT-05`
- Engineering practices: `EP-002`, `EP-021`, `EP-025`, `EP-026`, `EP-027`, `EP-031`, `EP-032`
- Objectives: `O1`, `O8`
- Research questions: `RQ4`

<a id="req-n-m01"></a>
### REQ:N-M01 — The core workflow must work locally after required models and tools are installed.

| Field | Value |
|---|---|
| Task number | 117 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-12 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/N-M01/](../../evidence/requirements/N-M01) |

#### Task statement

The core workflow must work locally after required models and tools are installed.

#### Definition of Done / next action

With network disconnected, model inspection, configuration selection and upstream local chat complete using installed assets.

#### Traceability

- Requirements: `N-M01`
- Work packages: `FR-02`, `RT-05`
- Engineering practices: `EP-002`, `EP-021`, `EP-025`, `EP-026`, `EP-027`, `EP-031`, `EP-032`
- Objectives: `O1`, `O5`, `O6`
- Research questions: `RQ4`

<a id="req-n-m02"></a>
### REQ:N-M02 — The application must not upload models, prompts, knowledge files or answers to a cloud AI service in the core workflow.

| Field | Value |
|---|---|
| Task number | 118 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-12 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/N-M02/](../../evidence/requirements/N-M02) |

#### Task statement

The application must not upload models, prompts, knowledge files or answers to a cloud AI service in the core workflow.

#### Definition of Done / next action

Network inspection shows no cloud AI upload during core model/document/chat workflow; any initial approved download is separately documented.

#### Traceability

- Requirements: `N-M02`
- Work packages: `FR-02`
- Engineering practices: `EP-002`, `EP-021`, `EP-026`, `EP-027`, `EP-032`
- Objectives: `O1`, `O8`, `O9`
- Research questions: `RQ-TV`, `RQ4`

<a id="req-n-m13"></a>
### REQ:N-M13 — The main workflow must support keyboard operation and Windows text scaling.

| Field | Value |
|---|---|
| Task number | 119 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-12 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/ux/accessibility/](../../ux/accessibility) |

#### Task statement

The main workflow must support keyboard operation and Windows text scaling.

#### Definition of Done / next action

Core tasks complete keyboard-only; focus is visible; content remains usable at 200% Windows text scaling with no critical clipping.

#### Traceability

- Requirements: `N-M13`
- Work packages: `FR-02`, `IM-01`
- Engineering practices: `EP-002`, `EP-021`, `EP-026`, `EP-027`, `EP-032`
- Objectives: `O1`, `O8`, `O11`
- Research questions: `RQ4`

<a id="req-n-m07"></a>
### REQ:N-M07 — Each final run must record requested and actual backend, device and optimisation state.

| Field | Value |
|---|---|
| Task number | 124 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-13 |
| Working status | Partially Verified |
| Validation | Not Validated |
| Effective status | **Partially Verified** |
| Planned evidence | [experiments/manifests/](../../../experiments/manifests) |

#### Task statement

Each final run must record requested and actual backend, device and optimisation state.

#### Definition of Done / next action

Manifest separates requested from actual runtime/backend/device/cache and cites the evidence used to determine actual state.

#### Traceability

- Requirements: `N-M07`
- Work packages: `FR-03`, `PD-09`
- Engineering practices: `EP-019`, `EP-021`, `EP-028`, `EP-029`, `EP-030`, `EP-032`, `EP-035`, `EP-036`
- Objectives: `O8`, `O10`, `O11`
- Research questions: `RQ1`, `RQ2`

<a id="req-r-m01"></a>
### REQ:R-M01 — The project must compare at least one verified TurboQuant run with a matched standard KV-cache baseline.

| Field | Value |
|---|---|
| Task number | 125 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Research |
| Deadline | 2026-08-13 |
| Working status | Partially Verified |
| Validation | Not Validated |
| Effective status | **Partially Verified** |
| Planned evidence | [experiments/processed-results/EXP-TQ-COMP-001/](../../../experiments/processed-results/EXP-TQ-COMP-001) |

#### Task statement

The project must compare at least one verified TurboQuant run with a matched standard KV-cache baseline.

#### Definition of Done / next action

Final app-integrated matched comparison still required.

#### Traceability

- Requirements: `R-M01`
- Work packages: `FR-03`, `QX-04`
- Engineering practices: `EP-021`, `EP-028`, `EP-029`, `EP-030`, `EP-031`, `EP-032`, `EP-035`, `EP-036`
- Objectives: `O8`, `O10`
- Research questions: `RQ2`

<a id="req-r-m04"></a>
### REQ:R-M04 — The project must measure memory use for every final test configuration.

| Field | Value |
|---|---|
| Task number | 126 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Research |
| Deadline | 2026-08-13 |
| Working status | Partially Verified |
| Validation | Not Validated |
| Effective status | **Partially Verified** |
| Planned evidence | [experiments/processed-results/final-metrics/](../../../experiments/processed-results/final-metrics) |

#### Task statement

The project must measure memory use for every final test configuration.

#### Definition of Done / next action

Each final row records available RAM before, peak process-tree working set/private where used, relevant GPU shared memory and measurement definition.

#### Traceability

- Requirements: `R-M04`
- Work packages: `FR-03`
- Engineering practices: `EP-021`, `EP-028`, `EP-029`, `EP-030`, `EP-032`, `EP-035`, `EP-036`
- Objectives: `O6`, `O10`
- Research questions: `RQ2`, `RQ3`

<a id="req-r-m05"></a>
### REQ:R-M05 — The project must evaluate runtime performance for every final test configuration.

| Field | Value |
|---|---|
| Task number | 127 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Research |
| Deadline | 2026-08-13 |
| Working status | Partially Verified |
| Validation | Not Validated |
| Effective status | **Partially Verified** |
| Planned evidence | [experiments/processed-results/final-metrics/](../../../experiments/processed-results/final-metrics) |

#### Task statement

The project must evaluate runtime performance for every final test configuration.

#### Definition of Done / next action

Cold/warm load, TTFT, prompt speed, generation speed and total response duration are reported where available using fixed definitions and repetitions.

#### Traceability

- Requirements: `R-M05`
- Work packages: `FR-03`
- Engineering practices: `EP-021`, `EP-028`, `EP-029`, `EP-030`, `EP-032`, `EP-035`, `EP-036`
- Objectives: `O6`, `O10`
- Research questions: `RQ1`, `RQ2`, `RQ3`

<a id="req-r-m06"></a>
### REQ:R-M06 — The project must compare output quality with a fixed prompt set and scoring guide.

| Field | Value |
|---|---|
| Task number | 128 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Research |
| Deadline | 2026-08-13 |
| Working status | Partially Verified |
| Validation | Not Validated |
| Effective status | **Partially Verified** |
| Planned evidence | [experiments/raw-results/quality/](../../../experiments/raw-results/quality) |

#### Task statement

The project must compare output quality with a fixed prompt set and scoring guide.

#### Definition of Done / next action

Prompt/rubric versions are frozen before final comparison; raw answers, instruction/format/fact scores and limitations are retained.

#### Traceability

- Requirements: `R-M06`
- Work packages: `FR-03`, `PD-09`
- Engineering practices: `EP-019`, `EP-021`, `EP-028`, `EP-029`, `EP-030`, `EP-032`, `EP-035`, `EP-036`
- Objectives: `O6`, `O10`
- Research questions: `RQ2`, `RQ3`

<a id="req-r-m07"></a>
### REQ:R-M07 — The project must identify the largest stable tested context for selected final configurations.

| Field | Value |
|---|---|
| Task number | 129 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Research |
| Deadline | 2026-08-13 |
| Working status | Partially Verified |
| Validation | Not Validated |
| Effective status | **Partially Verified** |
| Planned evidence | [experiments/processed-results/context/](../../../experiments/processed-results/context) |

#### Task statement

The project must identify the largest stable tested context for selected final configurations.

#### Definition of Done / next action

A versioned step test records maximum attempted and maximum stable context, success/retrieval result, memory and failure reason; it is not called the model maximum.

#### Traceability

- Requirements: `R-M07`
- Work packages: `FR-03`
- Engineering practices: `EP-021`, `EP-028`, `EP-029`, `EP-030`, `EP-032`, `EP-035`, `EP-036`
- Objectives: `O6`, `O10`
- Research questions: `RQ2`, `RQ3`

<a id="req-r-m11"></a>
### REQ:R-M11 — The project must determine which selected Granite/runtime/backend combinations run reliably on the tested Intel CPU and integrated GPU.

| Field | Value |
|---|---|
| Task number | 130 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Research |
| Deadline | 2026-08-13 |
| Working status | Partially Verified |
| Validation | Not Validated |
| Effective status | **Partially Verified** |
| Planned evidence | [experiments/processed-results/cross-route/](../../../experiments/processed-results/cross-route) |

#### Task statement

The project must determine which selected Granite/runtime/backend combinations run reliably on the tested Intel CPU and integrated GPU.

#### Definition of Done / next action

Matrix includes upstream llama.cpp, selected TQ forks, official OpenVINO and OpenVINO TQ with model, commit/version, build, requested/actual device, success/failure and role.

#### Traceability

- Requirements: `R-M11`
- Work packages: `FR-03`, `PD-09`
- Engineering practices: `EP-019`, `EP-021`, `EP-028`, `EP-029`, `EP-030`, `EP-032`, `EP-035`, `EP-036`
- Objectives: `O8`, `O10`
- Research questions: `RQ1`

<a id="req-r-m12"></a>
### REQ:R-M12 — The project must assess selected complete configurations against 4 GB, 8 GB and 16 GB total system-memory budgets.

| Field | Value |
|---|---|
| Task number | 131 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Research |
| Deadline | 2026-08-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [experiments/processed-results/memory-budgets/](../../../experiments/processed-results/memory-budgets) |

#### Task statement

The project must assess selected complete configurations against 4 GB, 8 GB and 16 GB total system-memory budgets.

#### Definition of Done / next action

Each result is labelled physical, controlled-limit, calculated or predicted and includes OS/app/model/KV/runtime/shared-memory allowance and uncertainty.

#### Traceability

- Requirements: `R-M12`
- Work packages: `FR-03`, `HE-04`
- Engineering practices: `EP-021`, `EP-028`, `EP-029`, `EP-030`, `EP-032`, `EP-035`, `EP-036`
- Objectives: `O10`
- Research questions: `RQ3`

<a id="req-r-m13"></a>
### REQ:R-M13 — The project must compare TurboVec-compressed or optimised vectors with an uncompressed-vector baseline.

| Field | Value |
|---|---|
| Task number | 132 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Research |
| Deadline | 2026-08-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [experiments/processed-results/EXP-TV-COMP-001/](../../../experiments/processed-results/EXP-TV-COMP-001) |

#### Task statement

The project must compare TurboVec-compressed or optimised vectors with an uncompressed-vector baseline.

#### Definition of Done / next action

Same documents, chunking, embeddings and queries; compare storage, memory, index/query time, relevance, answer usefulness, failures and stability.

#### Traceability

- Requirements: `R-M13`
- Work packages: `FR-03`, `TV-03`, `TV-04`
- Engineering practices: `EP-021`, `EP-024`, `EP-028`, `EP-029`, `EP-030`, `EP-032`, `EP-035`, `EP-036`
- Objectives: `O9`, `O10`
- Research questions: `RQ-TV`

<a id="req-r-m14"></a>
### REQ:R-M14 — The project must quantify memory-estimator error and false-safe/false-unsafe recommendations.

| Field | Value |
|---|---|
| Task number | 133 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Research |
| Deadline | 2026-08-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [experiments/processed-results/EST-VALID-001/](../../../experiments/processed-results/EST-VALID-001) |

#### Task statement

The project must quantify memory-estimator error and false-safe/false-unsafe recommendations.

#### Definition of Done / next action

Matched rows report predicted, measured, absolute error, percentage error, fit decision, false-safe/false-unsafe and confidence.

#### Traceability

- Requirements: `R-M14`
- Work packages: `FR-03`, `HE-04`
- Engineering practices: `EP-021`, `EP-028`, `EP-029`, `EP-030`, `EP-032`, `EP-035`, `EP-036`
- Objectives: `O3`, `O10`
- Research questions: `RQ3`, `RQ4`

<a id="req-f-s12"></a>
### REQ:F-S12 — The application should export benchmark results as CSV or JSON.

| Field | Value |
|---|---|
| Task number | 139 |
| Source tab | Requirements |
| Priority | Should |
| Release role / phase | Should |
| Deadline | 2026-08-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/F-S12/](../../evidence/requirements/F-S12) |

#### Task statement

The application should export benchmark results as CSV or JSON.

#### Definition of Done / next action

Export validates against schema and contains units, evidence classification and requested/actual state.

#### Traceability

- Requirements: `F-S12`
- Work packages: `FR-03`
- Engineering practices: `EP-021`, `EP-028`, `EP-029`, `EP-030`, `EP-032`, `EP-035`, `EP-036`
- Objectives: `O10`, `O11`
- Research questions: `RQ1`, `RQ2`, `RQ3`

<a id="req-c-03"></a>
### REQ:C-03 — The application could show charts for memory and speed trade-offs.

| Field | Value |
|---|---|
| Task number | 140 |
| Source tab | Requirements |
| Priority | Could |
| Release role / phase | Could |
| Deadline | 2026-08-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/C-03/](../../evidence/requirements/C-03) |

#### Task statement

The application could show charts for memory and speed trade-offs.

#### Definition of Done / next action

Dashboard visualisation is optional.

#### Traceability

- Requirements: `C-03`
- Work packages: `FR-03`
- Engineering practices: `EP-021`, `EP-028`, `EP-029`, `EP-030`, `EP-032`, `EP-035`, `EP-036`
- Objectives: `O8`, `O10`
- Research questions: —

<a id="req-f-m01"></a>
### REQ:F-M01 — The application must open on the target Windows 11 x64 Intel computer.

| Field | Value |
|---|---|
| Task number | 141 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-14 |
| Working status | Partially Verified |
| Validation | Not Validated |
| Effective status | **Partially Verified** |
| Planned evidence | [docs/evidence/requirements/F-M01/](../../evidence/requirements/F-M01) |

#### Task statement

The application must open on the target Windows 11 x64 Intel computer.

#### Definition of Done / next action

ModelImportPage and release packaging still need completion.

#### Traceability

- Requirements: `F-M01`
- Work packages: `FR-04`, `IM-01`
- Engineering practices: `EP-021`, `EP-032`, `EP-034`, `EP-037`
- Objectives: `O1`, `O11`
- Research questions: `RQ4`

<a id="req-g-m08"></a>
### REQ:G-M08 — The release must include a README, user manual, developer/build guide, known limitations and final feature-status table.

| Field | Value |
|---|---|
| Task number | 142 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-14 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/manuals/](../../manuals) |

#### Task statement

The release must include a README, user manual, developer/build guide, known limitations and final feature-status table.

#### Definition of Done / next action

Documents cover install/run/use/build/test/evidence/limitations; feature statuses match the RTM.

#### Traceability

- Requirements: `G-M08`
- Work packages: `FR-04`
- Engineering practices: `EP-021`, `EP-032`, `EP-034`, `EP-037`
- Objectives: `O11`
- Research questions: `RQ4`

<a id="req-n-m08"></a>
### REQ:N-M08 — A clean copy of the repository must build and run its automated tests.

| Field | Value |
|---|---|
| Task number | 143 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-14 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [release-evidence/clean-build/](../../../release-evidence/clean-build) |

#### Task statement

A clean copy of the repository must build and run its automated tests.

#### Definition of Done / next action

Documented x64 restore/build/test commands pass from a clean checkout in the pinned environment.

#### Traceability

- Requirements: `N-M08`
- Work packages: `FR-04`, `PD-08`
- Engineering practices: `EP-018`, `EP-021`, `EP-022`, `EP-032`, `EP-034`, `EP-037`
- Objectives: `O11`
- Research questions: `RQ4`

<a id="req-n-s02"></a>
### REQ:N-S02 — The project should provide a simple packaged installation route such as MSIX.

| Field | Value |
|---|---|
| Task number | 147 |
| Source tab | Requirements |
| Priority | Should |
| Release role / phase | Should |
| Deadline | 2026-08-14 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [release-evidence/installer/](../../../release-evidence/installer) |

#### Task statement

The project should provide a simple packaged installation route such as MSIX.

#### Definition of Done / next action

Fresh install/uninstall works on the target Windows machine and dependencies/limitations are documented.

#### Traceability

- Requirements: `N-S02`
- Work packages: `FR-04`
- Engineering practices: `EP-021`, `EP-032`, `EP-034`, `EP-037`
- Objectives: `O11`
- Research questions: `RQ4`

<a id="req-g-m03"></a>
### REQ:G-M03 — The project must maintain bidirectional traceability from requirements to objectives/RQs, work packages, implementation, tests and evidence.

| Field | Value |
|---|---|
| Task number | 148 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-15 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/requirements/Requirements-Traceability-Matrix.md](../../requirements/Requirements-Traceability-Matrix.md) |

#### Task statement

The project must maintain bidirectional traceability from requirements to objectives/RQs, work packages, implementation, tests and evidence.

#### Definition of Done / next action

No active Must row lacks source, rationale, acceptance, WP, component, verification, evidence path, owner and status; release audit confirms reverse links.

#### Traceability

- Requirements: `G-M03`
- Work packages: `FR-01`, `FR-05`, `PD-04`
- Engineering practices: `EP-004`, `EP-005`, `EP-006`, `EP-021`, `EP-022`, `EP-025`, `EP-032`, `EP-033`, `EP-036`, `EP-038`, `EP-039`, `EP-040`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="req-g-m05"></a>
### REQ:G-M05 — The project must maintain a consolidated risk, assumption, constraint and licence register.

| Field | Value |
|---|---|
| Task number | 149 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-15 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/risks/](../../risks) |

#### Task statement

The project must maintain a consolidated risk, assumption, constraint and licence register.

#### Definition of Done / next action

Each high risk has probability, impact, owner, trigger, validation, mitigation, contingency and status; assumptions are confirmed/rejected with evidence; licences are reviewed.

#### Traceability

- Requirements: `G-M05`
- Work packages: `FR-05`, `PD-05`
- Engineering practices: `EP-007`, `EP-021`, `EP-032`, `EP-036`, `EP-038`, `EP-039`, `EP-040`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="req-g-m06"></a>
### REQ:G-M06 — Requirement and scope changes must be linked to dated decisions, GitHub issues, pull requests/commits and affected tests.

| Field | Value |
|---|---|
| Task number | 150 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-15 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/planning/Change-Log.md](../../planning/Change-Log.md) |

#### Task statement

Requirement and scope changes must be linked to dated decisions, GitHub issues, pull requests/commits and affected tests.

#### Definition of Done / next action

Every approved change records reason, date, affected IDs/WPs/tests/report sections and superseded/replacement links.

#### Traceability

- Requirements: `G-M06`
- Work packages: `FR-05`, `PD-04`, `PD-08`
- Engineering practices: `EP-004`, `EP-005`, `EP-006`, `EP-018`, `EP-021`, `EP-022`, `EP-032`, `EP-036`, `EP-038`, `EP-039`, `EP-040`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="req-g-m09"></a>
### REQ:G-M09 — The final release must be tied to a Git tag, checksums, evidence pack, independent backup and evidence-based answers to every RQ.

| Field | Value |
|---|---|
| Task number | 151 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-15 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [release-evidence/](../../../release-evidence) |

#### Task statement

The final release must be tied to a Git tag, checksums, evidence pack, independent backup and evidence-based answers to every RQ.

#### Definition of Done / next action

Tag/commit/checksums/backups exist; all Must rows have final status/evidence; report answers each RQ and states limitations/negative results.

#### Traceability

- Requirements: `G-M09`
- Work packages: `FR-05`
- Engineering practices: `EP-021`, `EP-032`, `EP-036`, `EP-038`, `EP-039`, `EP-040`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="req-n-m09"></a>
### REQ:N-M09 — The repository must not contain secrets, personal test data or large proprietary model files.

| Field | Value |
|---|---|
| Task number | 152 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-15 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [release-evidence/repository-scan/](../../../release-evidence/repository-scan) |

#### Task statement

The repository must not contain secrets, personal test data or large proprietary model files.

#### Definition of Done / next action

Release scan finds no credentials, real patient/pupil data, unapproved model weights or prohibited third-party files.

#### Traceability

- Requirements: `N-M09`
- Work packages: `FR-05`, `PD-08`
- Engineering practices: `EP-018`, `EP-021`, `EP-022`, `EP-032`, `EP-036`, `EP-038`, `EP-039`, `EP-040`
- Objectives: `O11`
- Research questions: `RQ4`

<a id="req-n-m14"></a>
### REQ:N-M14 — The project must produce a basic distributable Windows x64 release build with documented dependencies.

| Field | Value |
|---|---|
| Task number | 153 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-15 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [release-evidence/](../../../release-evidence) |

#### Task statement

The project must produce a basic distributable Windows x64 release build with documented dependencies.

#### Definition of Done / next action

Release artefact launches on the target machine; checksum, commit/tag, runtime dependencies and install/run steps are recorded.

#### Traceability

- Requirements: `N-M14`
- Work packages: `FR-04`, `FR-05`
- Engineering practices: `EP-021`, `EP-032`, `EP-034`, `EP-036`, `EP-037`, `EP-038`, `EP-039`, `EP-040`
- Objectives: `O11`
- Research questions: `RQ4`

<a id="req-r-m08"></a>
### REQ:R-M08 — The project must preserve a complete record of every final experiment.

| Field | Value |
|---|---|
| Task number | 154 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-15 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [experiments/manifests/](../../../experiments/manifests) |

#### Task statement

The project must preserve a complete record of every final experiment.

#### Definition of Done / next action

Each formal experiment has an ID, hardware/model/runtime/config hashes, requested/actual state, command, raw stdout/stderr, measurements, outputs and failure status.

#### Traceability

- Requirements: `R-M08`
- Work packages: `FR-05`, `PD-03`, `PD-08`
- Engineering practices: `EP-018`, `EP-020`, `EP-021`, `EP-022`, `EP-032`, `EP-036`, `EP-038`, `EP-039`, `EP-040`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="req-r-m09"></a>
### REQ:R-M09 — The project must record failed attempts and known limitations.

| Field | Value |
|---|---|
| Task number | 155 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-15 |
| Working status | Partially Verified |
| Validation | Not Validated |
| Effective status | **Partially Verified** |
| Planned evidence | [docs/testing/Failure-Register.md](../../testing/Failure-Register.md) |

#### Task statement

The project must record failed attempts and known limitations.

#### Definition of Done / next action

Every formal failure has ID, stage, code, evidence, likely cause, next action and resolution status; limitations appear in final documentation.

#### Traceability

- Requirements: `R-M09`
- Work packages: `FR-03`, `FR-05`, `PD-03`
- Engineering practices: `EP-020`, `EP-021`, `EP-028`, `EP-029`, `EP-030`, `EP-032`, `EP-035`, `EP-036`, `EP-038`, `EP-039`, `EP-040`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="req-r-m10"></a>
### REQ:R-M10 — Another developer must be able to reproduce the clean build and core result from written instructions.

| Field | Value |
|---|---|
| Task number | 156 |
| Source tab | Requirements |
| Priority | Must |
| Release role / phase | Core |
| Deadline | 2026-08-15 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [release-evidence/reproduction/](../../../release-evidence/reproduction) |

#### Task statement

Another developer must be able to reproduce the clean build and core result from written instructions.

#### Definition of Done / next action

Independent clean-checkout reproduction completes the documented x64 build, tests and main upstream route; deviations are recorded.

#### Traceability

- Requirements: `R-M10`
- Work packages: `FR-04`, `FR-05`
- Engineering practices: `EP-021`, `EP-032`, `EP-034`, `EP-036`, `EP-037`, `EP-038`, `EP-039`, `EP-040`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="req-c-02"></a>
### REQ:C-02 — The application could keep a local benchmark history.

| Field | Value |
|---|---|
| Task number | 165 |
| Source tab | Requirements |
| Priority | Could |
| Release role / phase | Could |
| Deadline | 2026-08-15 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/requirements/C-02/](../../evidence/requirements/C-02) |

#### Task statement

The application could keep a local benchmark history.

#### Definition of Done / next action

Only after core evidence/export is stable.

#### Traceability

- Requirements: `C-02`
- Work packages: `FR-03`, `FR-05`
- Engineering practices: `EP-021`, `EP-028`, `EP-029`, `EP-030`, `EP-032`, `EP-035`, `EP-036`, `EP-038`, `EP-039`, `EP-040`
- Objectives: `O10`, `O11`
- Research questions: —

## Work packages

<a id="wp-pd-01"></a>
### WP:PD-01 — Freeze first-release definition/RQs

| Field | Value |
|---|---|
| Task number | 3 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P0 |
| Deadline | 2026-07-11 |
| Working status | Implemented |
| Validation | Validated |
| Effective status | **Verified** |
| Planned evidence | [docs/evidence/work-packages/PD-01/](../../evidence/work-packages/PD-01) |

#### Task statement

Freeze first-release definition/RQs

#### Definition of Done / next action

One aim, measurable RQs, contribution and scope tiers are versioned.

#### Traceability

- Requirements: `G-M01`
- Work packages: `PD-01`
- Engineering practices: `EP-001`, `EP-032`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="wp-pd-02"></a>
### WP:PD-02 — Correct/version controlling workflows

| Field | Value |
|---|---|
| Task number | 4 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P0 |
| Deadline | 2026-07-11 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/PD-02/](../../evidence/work-packages/PD-02) |

#### Task statement

Correct/version controlling workflows

#### Definition of Done / next action

Workflow distinguishes runtime KV-cache modes from exported weight files and separates runtime routes.

#### Traceability

- Requirements: —
- Work packages: `PD-02`
- Engineering practices: `EP-003`, `EP-032`
- Objectives: —
- Research questions: —

<a id="wp-pd-03"></a>
### WP:PD-03 — Recover/archive raw evidence and current UI code

| Field | Value |
|---|---|
| Task number | 5 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P0 |
| Deadline | 2026-07-11 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/evidence/work-packages/PD-03/](../../evidence/work-packages/PD-03) |

#### Task statement

Recover/archive raw evidence and current UI code

#### Definition of Done / next action

MoSCoW, ModelImportPage and raw campaign evidence are backed up and referenced.

#### Traceability

- Requirements: `G-M07`, `R-M08`, `R-M09`
- Work packages: `PD-03`
- Engineering practices: `EP-020`, `EP-032`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="wp-pd-04"></a>
### WP:PD-04 — Must-Have RTM and acceptance criteria

| Field | Value |
|---|---|
| Task number | 10 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P0 |
| Deadline | 2026-07-12 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/evidence/work-packages/PD-04/](../../evidence/work-packages/PD-04) |

#### Task statement

Must-Have RTM and acceptance criteria

#### Definition of Done / next action

All Must Haves map to workflows, components, tests and evidence.

#### Traceability

- Requirements: `G-M02`, `G-M03`, `G-M06`
- Work packages: `PD-04`
- Engineering practices: `EP-004`, `EP-005`, `EP-006`, `EP-032`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="wp-pd-05"></a>
### WP:PD-05 — Risk/assumption/constraint/licence register

| Field | Value |
|---|---|
| Task number | 11 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P0 |
| Deadline | 2026-07-12 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/evidence/work-packages/PD-05/](../../evidence/work-packages/PD-05) |

#### Task statement

Risk/assumption/constraint/licence register

#### Definition of Done / next action

High risks have validation, mitigation, contingency, owner and status.

#### Traceability

- Requirements: `G-M05`
- Work packages: `PD-05`
- Engineering practices: `EP-007`, `EP-032`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="wp-pd-06"></a>
### WP:PD-06 — Architecture views and ADRs

| Field | Value |
|---|---|
| Task number | 17 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P0 |
| Deadline | 2026-07-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/PD-06/](../../evidence/work-packages/PD-06) |

#### Task statement

Architecture views and ADRs

#### Definition of Done / next action

Context/component/process/deployment views and key decisions agree.

#### Traceability

- Requirements: `G-M04`, `N-M12`
- Work packages: `PD-06`
- Engineering practices: `EP-008`, `EP-011`, `EP-012`, `EP-013`, `EP-014`, `EP-015`, `EP-016`, `EP-032`
- Objectives: `O1`, `O11`, `O5`, `O8`, `O9`
- Research questions: `RQ1`, `RQ4`

<a id="wp-pd-07"></a>
### WP:PD-07 — Application contracts/states/diagnostics

| Field | Value |
|---|---|
| Task number | 18 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P0 |
| Deadline | 2026-07-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/PD-07/](../../evidence/work-packages/PD-07) |

#### Task statement

Application contracts/states/diagnostics

#### Definition of Done / next action

Stable data/state/error contracts support independent UI and service work.

#### Traceability

- Requirements: `G-M04`
- Work packages: `PD-07`
- Engineering practices: `EP-009`, `EP-010`, `EP-015`, `EP-017`, `EP-032`
- Objectives: `O1`, `O11`, `O5`, `O8`, `O9`
- Research questions: `RQ1`, `RQ4`

<a id="wp-pd-08"></a>
### WP:PD-08 — Repository/test/evidence preparation

| Field | Value |
|---|---|
| Task number | 30 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P0 |
| Deadline | 2026-07-14 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/evidence/work-packages/PD-08/](../../evidence/work-packages/PD-08) |

#### Task statement

Repository/test/evidence preparation

#### Definition of Done / next action

Clean build/test and evidence structure exist.

#### Traceability

- Requirements: `G-M06`, `N-M08`, `N-M09`, `R-M08`
- Work packages: `PD-08`
- Engineering practices: `EP-018`, `EP-022`, `EP-032`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="wp-tv-01"></a>
### WP:TV-01 — Identify and pin exact TurboVec implementation and contract

| Field | Value |
|---|---|
| Task number | 31 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P0 |
| Deadline | 2026-07-14 |
| Working status | Implemented |
| Validation | Validated |
| Effective status | **Verified** |
| Planned evidence | [docs/evidence/work-packages/TV-01/](../../evidence/work-packages/TV-01) |

#### Task statement

Identify and pin exact TurboVec implementation and contract

#### Definition of Done / next action

Repository/version/licence/platform/input/output/vector/retrieval contract and go/no-go are recorded.

#### Traceability

- Requirements: `F-M25`, `F-M26`, `R-M02`
- Work packages: `TV-01`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O10`, `O9`
- Research questions: `RQ-TV`, `RQ4`

<a id="wp-pd-09"></a>
### WP:PD-09 — App-specific evaluation addendum

| Field | Value |
|---|---|
| Task number | 34 |
| Source tab | Work Packages |
| Priority | P1 |
| Release role / phase | P0 |
| Deadline | 2026-07-14 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/evidence/work-packages/PD-09/](../../evidence/work-packages/PD-09) |

#### Task statement

App-specific evaluation addendum

#### Definition of Done / next action

Remaining experiments map to RQs and frozen evidence schemas.

#### Traceability

- Requirements: `N-M07`, `R-M06`, `R-M11`
- Work packages: `PD-09`
- Engineering practices: `EP-019`, `EP-028`, `EP-029`, `EP-032`
- Objectives: `O10`, `O11`, `O6`, `O8`
- Research questions: `RQ1`, `RQ2`, `RQ3`

<a id="wp-pd-10"></a>
### WP:PD-10 — TurboVec and LLM Fit decision

| Field | Value |
|---|---|
| Task number | 35 |
| Source tab | Work Packages |
| Priority | P1 |
| Release role / phase | P0 |
| Deadline | 2026-07-14 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/evidence/work-packages/PD-10/](../../evidence/work-packages/PD-10) |

#### Task statement

TurboVec and LLM Fit decision

#### Definition of Done / next action

Both items have explicit Implement/Defer decisions.

#### Traceability

- Requirements: `R-M02`
- Work packages: `PD-10`
- Engineering practices: `EP-032`
- Objectives: `O9`
- Research questions: `RQ-TV`

<a id="wp-im-01"></a>
### WP:IM-01 — Finish existing ModelImportPage layout/navigation

| Field | Value |
|---|---|
| Task number | 36 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P1 |
| Deadline | 2026-07-15 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/evidence/work-packages/IM-01/](../../evidence/work-packages/IM-01) |

#### Task statement

Finish existing ModelImportPage layout/navigation

#### Definition of Done / next action

Page launches and matches the core import workflow.

#### Traceability

- Requirements: `F-M01`, `N-M13`
- Work packages: `IM-01`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O1`, `O11`, `O8`
- Research questions: `RQ4`

<a id="wp-im-02"></a>
### WP:IM-02 — ViewModel + file/folder picker

| Field | Value |
|---|---|
| Task number | 38 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P1 |
| Deadline | 2026-07-16 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/IM-02/](../../evidence/work-packages/IM-02) |

#### Task statement

ViewModel + file/folder picker

#### Definition of Done / next action

Real file/folder selection produces a testable state object.

#### Traceability

- Requirements: `F-M02`
- Work packages: `IM-02`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O2`
- Research questions: `RQ4`

<a id="wp-dl-01"></a>
### WP:DL-01 — Freeze approved model catalogue/source/licence/hash manifest

| Field | Value |
|---|---|
| Task number | 39 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P1 |
| Deadline | 2026-07-17 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/DL-01/](../../evidence/work-packages/DL-01) |

#### Task statement

Freeze approved model catalogue/source/licence/hash manifest

#### Definition of Done / next action

At least one model has approved source, licence, revision, expected size, SHA-256 and destination policy.

#### Traceability

- Requirements: `F-M16`
- Work packages: `DL-01`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O2`
- Research questions: `RQ4`

<a id="wp-im-03"></a>
### WP:IM-03 — Drag/drop, validation, progress/cancel

| Field | Value |
|---|---|
| Task number | 40 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P1 |
| Deadline | 2026-07-17 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/IM-03/](../../evidence/work-packages/IM-03) |

#### Task statement

Drag/drop, validation, progress/cancel

#### Definition of Done / next action

Input edge cases and cancellation recover safely.

#### Traceability

- Requirements: `F-M03`, `F-M19`, `F-S01`, `N-M03`, `N-M04`
- Work packages: `IM-03`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O1`, `O2`, `O5`, `O6`, `O8`
- Research questions: `RQ4`

<a id="wp-im-04"></a>
### WP:IM-04 — Format detector and GGUF header validation

| Field | Value |
|---|---|
| Task number | 43 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P1 |
| Deadline | 2026-07-18 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/IM-04/](../../evidence/work-packages/IM-04) |

#### Task statement

Format detector and GGUF header validation

#### Definition of Done / next action

Valid/corrupt GGUF recognition is deterministic.

#### Traceability

- Requirements: `F-M03`, `F-M14`
- Work packages: `IM-04`
- Engineering practices: `EP-021`, `EP-023`, `EP-032`
- Objectives: `O2`, `O7`, `O8`
- Research questions: `RQ4`

<a id="wp-dl-02"></a>
### WP:DL-02 — Implement model download/progress/cancel/disk/hash service

| Field | Value |
|---|---|
| Task number | 45 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P1 |
| Deadline | 2026-07-19 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/DL-02/](../../evidence/work-packages/DL-02) |

#### Task statement

Implement model download/progress/cancel/disk/hash service

#### Definition of Done / next action

Approved model downloads safely; partials cannot pass; progress, cancellation, disk and SHA-256 checks work.

#### Traceability

- Requirements: `F-M16`, `F-M17`, `F-M19`, `N-M03`, `N-M04`, `N-M05`, `N-M06`
- Work packages: `DL-02`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O1`, `O11`, `O2`, `O5`, `O6`, `O7`, `O8`
- Research questions: `RQ4`

<a id="wp-im-05"></a>
### WP:IM-05 — Real GGUF metadata inspector

| Field | Value |
|---|---|
| Task number | 46 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P1 |
| Deadline | 2026-07-19 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/IM-05/](../../evidence/work-packages/IM-05) |

#### Task statement

Real GGUF metadata inspector

#### Definition of Done / next action

Real Granite metadata populates ModelDescriptor.

#### Traceability

- Requirements: `F-M05`
- Work packages: `IM-05`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O2`, `O3`, `O4`
- Research questions: `RQ3`, `RQ4`

<a id="wp-dl-03"></a>
### WP:DL-03 — Download-to-inspection end-to-end and failure tests

| Field | Value |
|---|---|
| Task number | 49 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P1 |
| Deadline | 2026-07-20 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/DL-03/](../../evidence/work-packages/DL-03) |

#### Task statement

Download-to-inspection end-to-end and failure tests

#### Definition of Done / next action

Verified download enters inspection; interrupted/corrupt/low-disk cases are handled and evidenced.

#### Traceability

- Requirements: `F-M16`, `F-M17`
- Work packages: `DL-03`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O11`, `O2`
- Research questions: `RQ4`

<a id="wp-im-06"></a>
### WP:IM-06 — Other-format recognition and classification

| Field | Value |
|---|---|
| Task number | 50 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P1 |
| Deadline | 2026-07-20 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/IM-06/](../../evidence/work-packages/IM-06) |

#### Task statement

Other-format recognition and classification

#### Definition of Done / next action

OpenVINO/HF and canonical result states are safe and explicit.

#### Traceability

- Requirements: `F-M04`, `F-S02`, `F-S03`
- Work packages: `IM-06`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O2`, `O8`
- Research questions: `RQ1`, `RQ4`

<a id="wp-im-07"></a>
### WP:IM-07 — Result screens, diagnostics and inspection demo

| Field | Value |
|---|---|
| Task number | 53 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P1 |
| Deadline | 2026-07-21 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/IM-07/](../../evidence/work-packages/IM-07) |

#### Task statement

Result screens, diagnostics and inspection demo

#### Definition of Done / next action

Import/inspection slice passes tests and demo.

#### Traceability

- Requirements: `F-M04`, `F-M06`, `F-M15`, `F-S08`
- Work packages: `IM-07`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O1`, `O2`, `O8`
- Research questions: `RQ4`

<a id="wp-he-01"></a>
### WP:HE-01 — Hardware snapshot service

| Field | Value |
|---|---|
| Task number | 55 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P2 |
| Deadline | 2026-07-22 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/HE-01/](../../evidence/work-packages/HE-01) |

#### Task statement

Hardware snapshot service

#### Definition of Done / next action

RAM/CPU/OS/disk snapshot is serialisable and testable.

#### Traceability

- Requirements: `F-M07`
- Work packages: `HE-01`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O3`, `O4`
- Research questions: `RQ1`, `RQ3`, `RQ4`

<a id="wp-he-02"></a>
### WP:HE-02 — Backend/device capability registry

| Field | Value |
|---|---|
| Task number | 57 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P2 |
| Deadline | 2026-07-23 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/HE-02/](../../evidence/work-packages/HE-02) |

#### Task statement

Backend/device capability registry

#### Definition of Done / next action

Installed/supported/experimental routes are distinct.

#### Traceability

- Requirements: `F-M07`, `F-M10`, `N-M10`
- Work packages: `HE-02`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O3`, `O4`, `O8`
- Research questions: `RQ1`, `RQ3`, `RQ4`

<a id="wp-he-03"></a>
### WP:HE-03 — Memory estimator core

| Field | Value |
|---|---|
| Task number | 58 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P2 |
| Deadline | 2026-07-24 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/HE-03/](../../evidence/work-packages/HE-03) |

#### Task statement

Memory estimator core

#### Definition of Done / next action

Transparent component estimate with uncertainty and safety reserve.

#### Traceability

- Requirements: `F-M08`
- Work packages: `HE-03`
- Engineering practices: `EP-021`, `EP-023`, `EP-032`
- Objectives: `O10`, `O3`
- Research questions: `RQ3`, `RQ4`

<a id="wp-he-04"></a>
### WP:HE-04 — Estimator calibration from existing evidence

| Field | Value |
|---|---|
| Task number | 60 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P2 |
| Deadline | 2026-07-25 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/HE-04/](../../evidence/work-packages/HE-04) |

#### Task statement

Estimator calibration from existing evidence

#### Definition of Done / next action

Prediction error/margins are quantified for matched tested cases.

#### Traceability

- Requirements: `F-M08`, `R-M12`, `R-M14`
- Work packages: `HE-04`
- Engineering practices: `EP-021`, `EP-030`, `EP-032`
- Objectives: `O10`, `O3`
- Research questions: `RQ3`, `RQ4`

<a id="wp-he-05"></a>
### WP:HE-05 — Candidate generator

| Field | Value |
|---|---|
| Task number | 62 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P2 |
| Deadline | 2026-07-26 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/HE-05/](../../evidence/work-packages/HE-05) |

#### Task statement

Candidate generator

#### Definition of Done / next action

Only complete supported candidates survive.

#### Traceability

- Requirements: `F-M10`
- Work packages: `HE-05`
- Engineering practices: `EP-021`, `EP-023`, `EP-032`
- Objectives: `O4`, `O8`
- Research questions: `RQ1`, `RQ3`, `RQ4`

<a id="wp-he-06"></a>
### WP:HE-06 — Mode selector algorithms

| Field | Value |
|---|---|
| Task number | 64 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P2 |
| Deadline | 2026-07-27 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/HE-06/](../../evidence/work-packages/HE-06) |

#### Task statement

Mode selector algorithms

#### Definition of Done / next action

Four deterministic objectives pass boundary tests.

#### Traceability

- Requirements: `F-M11`, `F-S04`
- Work packages: `HE-06`
- Engineering practices: `EP-021`, `EP-023`, `EP-032`
- Objectives: `O3`, `O4`, `O8`
- Research questions: `RQ2`, `RQ3`, `RQ4`

<a id="wp-he-07"></a>
### WP:HE-07 — Fit/not-fit compatibility UI

| Field | Value |
|---|---|
| Task number | 67 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P2 |
| Deadline | 2026-07-28 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/HE-07/](../../evidence/work-packages/HE-07) |

#### Task statement

Fit/not-fit compatibility UI

#### Definition of Done / next action

UI shows only verified configurations and transparent memory reasoning.

#### Traceability

- Requirements: `F-M09`, `F-S04`
- Work packages: `HE-07`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O3`, `O4`, `O8`
- Research questions: `RQ2`, `RQ3`, `RQ4`

<a id="wp-he-08"></a>
### WP:HE-08 — Import-to-mode E2E stabilisation

| Field | Value |
|---|---|
| Task number | 70 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P2 |
| Deadline | 2026-07-29 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/HE-08/](../../evidence/work-packages/HE-08) |

#### Task statement

Import-to-mode E2E stabilisation

#### Definition of Done / next action

Vertical slice passes twice and gate is approved.

#### Traceability

- Requirements: `F-M12`
- Work packages: `HE-08`
- Engineering practices: `EP-021`, `EP-025`, `EP-032`
- Objectives: `O4`, `O8`
- Research questions: `RQ4`

<a id="wp-rt-01"></a>
### WP:RT-01 — Upstream llama.cpp adapter contract

| Field | Value |
|---|---|
| Task number | 71 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P3 |
| Deadline | 2026-07-30 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/RT-01/](../../evidence/work-packages/RT-01) |

#### Task statement

Upstream llama.cpp adapter contract

#### Definition of Done / next action

Pinned executable/model command contract works independently.

#### Traceability

- Requirements: `F-M13`, `F-M18`, `N-M05`, `N-M12`
- Work packages: `RT-01`
- Engineering practices: `EP-021`, `EP-024`, `EP-031`, `EP-032`
- Objectives: `O11`, `O5`, `O6`, `O8`
- Research questions: `RQ1`, `RQ4`

<a id="wp-rt-02"></a>
### WP:RT-02 — Secure single-turn inference

| Field | Value |
|---|---|
| Task number | 73 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P3 |
| Deadline | 2026-07-31 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/RT-02/](../../evidence/work-packages/RT-02) |

#### Task statement

Secure single-turn inference

#### Definition of Done / next action

App returns one valid local response without a network port.

#### Traceability

- Requirements: `F-M13`, `F-M18`, `N-M05`, `N-M12`
- Work packages: `RT-02`
- Engineering practices: `EP-021`, `EP-031`, `EP-032`
- Objectives: `O11`, `O5`, `O6`, `O8`
- Research questions: `RQ1`, `RQ4`

<a id="wp-rt-03"></a>
### WP:RT-03 — Streaming chat and cancellation

| Field | Value |
|---|---|
| Task number | 75 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P3 |
| Deadline | 2026-08-01 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/RT-03/](../../evidence/work-packages/RT-03) |

#### Task statement

Streaming chat and cancellation

#### Definition of Done / next action

Streaming/stop/retry work and clean up resources.

#### Traceability

- Requirements: `F-M13`, `F-M19`, `F-M28`, `N-M03`, `N-M04`, `N-M06`
- Work packages: `RT-03`
- Engineering practices: `EP-021`, `EP-031`, `EP-032`
- Objectives: `O1`, `O11`, `O5`, `O6`, `O7`, `O8`
- Research questions: `RQ1`, `RQ4`

<a id="wp-rt-04"></a>
### WP:RT-04 — Session/context/metrics

| Field | Value |
|---|---|
| Task number | 78 |
| Source tab | Work Packages |
| Priority | P1 |
| Release role / phase | P3 |
| Deadline | 2026-08-02 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/RT-04/](../../evidence/work-packages/RT-04) |

#### Task statement

Session/context/metrics

#### Definition of Done / next action

Second turn, bounded history and metrics are correct.

#### Traceability

- Requirements: `F-M13`, `F-M20`
- Work packages: `RT-04`
- Engineering practices: `EP-021`, `EP-031`, `EP-032`
- Objectives: `O5`, `O6`, `O8`
- Research questions: `RQ1`, `RQ4`

<a id="wp-rt-05"></a>
### WP:RT-05 — Primary runtime hardening

| Field | Value |
|---|---|
| Task number | 80 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P3 |
| Deadline | 2026-08-03 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/RT-05/](../../evidence/work-packages/RT-05) |

#### Task statement

Primary runtime hardening

#### Definition of Done / next action

Primary GGUF route passes failure/offline E2E gate.

#### Traceability

- Requirements: `F-M06`, `F-M15`, `F-M18`, `F-M22`, `N-M01`, `N-M06`
- Work packages: `RT-05`
- Engineering practices: `EP-021`, `EP-025`, `EP-031`, `EP-032`
- Objectives: `O1`, `O11`, `O2`, `O5`, `O6`, `O7`, `O8`
- Research questions: `RQ1`, `RQ4`

<a id="wp-qx-01"></a>
### WP:QX-01 — Standard GGUF weight quantisation

| Field | Value |
|---|---|
| Task number | 82 |
| Source tab | Work Packages |
| Priority | P1 |
| Release role / phase | P4 |
| Deadline | 2026-08-04 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/QX-01/](../../evidence/work-packages/QX-01) |

#### Task statement

Standard GGUF weight quantisation

#### Definition of Done / next action

One suitable source produces a smaller validated GGUF safely.

#### Traceability

- Requirements: `F-M14`, `F-M19`, `F-M23`, `N-M03`, `N-M04`, `N-M05`, `N-M06`
- Work packages: `QX-01`
- Engineering practices: `EP-021`, `EP-031`, `EP-032`
- Objectives: `O1`, `O11`, `O2`, `O5`, `O6`, `O7`, `O8`
- Research questions: `RQ3`, `RQ4`

<a id="wp-qx-02"></a>
### WP:QX-02 — Reinspection, manifest and export

| Field | Value |
|---|---|
| Task number | 86 |
| Source tab | Work Packages |
| Priority | P1 |
| Release role / phase | P4 |
| Deadline | 2026-08-05 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/QX-02/](../../evidence/work-packages/QX-02) |

#### Task statement

Reinspection, manifest and export

#### Definition of Done / next action

Output is registered/exported and original preserved.

#### Traceability

- Requirements: `F-M14`, `F-M23`, `F-M24`
- Work packages: `QX-02`
- Engineering practices: `EP-021`, `EP-031`, `EP-032`
- Objectives: `O11`, `O2`, `O7`
- Research questions: `RQ3`, `RQ4`

<a id="wp-qx-03"></a>
### WP:QX-03 — AtomicBot Experimental adapter

| Field | Value |
|---|---|
| Task number | 87 |
| Source tab | Work Packages |
| Priority | P1 |
| Release role / phase | P4 |
| Deadline | 2026-08-06 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/QX-03/](../../evidence/work-packages/QX-03) |

#### Task statement

AtomicBot Experimental adapter

#### Definition of Done / next action

App proves real turbo3 activation and actual backend for a bounded configuration.

#### Traceability

- Requirements: `F-M21`, `N-M10`, `N-M11`
- Work packages: `QX-03`
- Engineering practices: `EP-021`, `EP-024`, `EP-031`, `EP-032`
- Objectives: `O11`, `O5`, `O6`, `O8`
- Research questions: `RQ1`, `RQ2`, `RQ4`

<a id="wp-qx-04"></a>
### WP:QX-04 — TurboQuant product gate/fallback

| Field | Value |
|---|---|
| Task number | 90 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P4 |
| Deadline | 2026-08-07 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/QX-04/](../../evidence/work-packages/QX-04) |

#### Task statement

TurboQuant product gate/fallback

#### Definition of Done / next action

Experimental option is included only if evidence passes; upstream fallback works.

#### Traceability

- Requirements: `F-M21`, `F-M22`, `N-M11`, `R-M01`
- Work packages: `QX-04`
- Engineering practices: `EP-021`, `EP-031`, `EP-032`
- Objectives: `O10`, `O11`, `O5`, `O6`, `O8`
- Research questions: `RQ1`, `RQ2`, `RQ4`

<a id="wp-tv-02"></a>
### WP:TV-02 — Knowledge-file import, text extraction, chunking and embedding baseline

| Field | Value |
|---|---|
| Task number | 93 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P4 |
| Deadline | 2026-08-08 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/TV-02/](../../evidence/work-packages/TV-02) |

#### Task statement

Knowledge-file import, text extraction, chunking and embedding baseline

#### Definition of Done / next action

One supported file is preserved, extracted, chunked and embedded into a versioned uncompressed baseline.

#### Traceability

- Requirements: `F-M25`, `F-M26`, `N-M05`
- Work packages: `TV-02`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O10`, `O11`, `O5`, `O9`
- Research questions: `RQ-TV`, `RQ4`

<a id="wp-ov-01"></a>
### WP:OV-01 — Official OpenVINO baseline

| Field | Value |
|---|---|
| Task number | 94 |
| Source tab | Work Packages |
| Priority | P1 |
| Release role / phase | P5 |
| Deadline | 2026-08-08 |
| Working status | Partially Verified |
| Validation | Not Validated |
| Effective status | **Partially Verified** |
| Planned evidence | [docs/evidence/work-packages/OV-01/](../../evidence/work-packages/OV-01) |

#### Task statement

Official OpenVINO baseline

#### Definition of Done / next action

One official Granite IR CPU run passes or blocker is reproducible.

#### Traceability

- Requirements: `F-S11`, `F-S15`, `R-M03`
- Work packages: `OV-01`
- Engineering practices: `EP-021`, `EP-031`, `EP-032`
- Objectives: `O8`
- Research questions: `RQ1`, `RQ2`, `RQ4`

<a id="wp-tv-03"></a>
### WP:TV-03 — TurboVec vector compression/index/retrieval integration

| Field | Value |
|---|---|
| Task number | 99 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P4 |
| Deadline | 2026-08-09 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/TV-03/](../../evidence/work-packages/TV-03) |

#### Task statement

TurboVec vector compression/index/retrieval integration

#### Definition of Done / next action

Compressed/optimised vectors are produced, indexed and queried with exact versions and evidence.

#### Traceability

- Requirements: `F-M26`, `F-M27`, `N-M03`, `N-M04`, `N-M06`, `N-M11`, `R-M13`
- Work packages: `TV-03`
- Engineering practices: `EP-021`, `EP-024`, `EP-032`
- Objectives: `O1`, `O10`, `O11`, `O5`, `O6`, `O7`, `O8`, `O9`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ4`

<a id="wp-ov-02"></a>
### WP:OV-02 — Official OpenVINO app adapter

| Field | Value |
|---|---|
| Task number | 103 |
| Source tab | Work Packages |
| Priority | P1 |
| Release role / phase | P5 |
| Deadline | 2026-08-09 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/OV-02/](../../evidence/work-packages/OV-02) |

#### Task statement

Official OpenVINO app adapter

#### Definition of Done / next action

One IR follows common app states with actual device evidence.

#### Traceability

- Requirements: `F-S02`, `F-S11`, `N-M10`
- Work packages: `OV-02`
- Engineering practices: `EP-021`, `EP-024`, `EP-031`, `EP-032`
- Objectives: `O2`, `O8`
- Research questions: `RQ1`, `RQ4`

<a id="wp-tv-04"></a>
### WP:TV-04 — Granite retrieval-chat integration and matched TurboVec evaluation

| Field | Value |
|---|---|
| Task number | 108 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P4 |
| Deadline | 2026-08-10 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/TV-04/](../../evidence/work-packages/TV-04) |

#### Task statement

Granite retrieval-chat integration and matched TurboVec evaluation

#### Definition of Done / next action

Retrieved sections feed Granite chat and compressed/uncompressed routes are compared on fixed queries.

#### Traceability

- Requirements: `F-M27`, `N-M10`, `R-M13`
- Work packages: `TV-04`
- Engineering practices: `EP-021`, `EP-032`
- Objectives: `O10`, `O6`, `O8`, `O9`
- Research questions: `RQ-TV`, `RQ4`

<a id="wp-ov-03"></a>
### WP:OV-03 — OpenVINO/custom-TQ decision gate

| Field | Value |
|---|---|
| Task number | 110 |
| Source tab | Work Packages |
| Priority | P1 |
| Release role / phase | P5 |
| Deadline | 2026-08-10 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/OV-03/](../../evidence/work-packages/OV-03) |

#### Task statement

OpenVINO/custom-TQ decision gate

#### Definition of Done / next action

Final UI scope matches verified OpenVINO capability; custom TQ is honestly kept/deferred.

#### Traceability

- Requirements: `F-S15`, `N-M11`, `R-M03`
- Work packages: `OV-03`
- Engineering practices: `EP-021`, `EP-031`, `EP-032`
- Objectives: `O11`, `O8`
- Research questions: `RQ1`, `RQ2`

<a id="wp-fr-01"></a>
### WP:FR-01 — Full regression and requirements acceptance

| Field | Value |
|---|---|
| Task number | 111 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P6 |
| Deadline | 2026-08-11 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/FR-01/](../../evidence/work-packages/FR-01) |

#### Task statement

Full regression and requirements acceptance

#### Definition of Done / next action

Must Haves have pass evidence or explicit incomplete status.

#### Traceability

- Requirements: `G-M03`
- Work packages: `FR-01`
- Engineering practices: `EP-021`, `EP-022`, `EP-025`, `EP-032`, `EP-033`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="wp-fr-02"></a>
### WP:FR-02 — Security/offline/UX/accessibility

| Field | Value |
|---|---|
| Task number | 120 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P6 |
| Deadline | 2026-08-12 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/FR-02/](../../evidence/work-packages/FR-02) |

#### Task statement

Security/offline/UX/accessibility

#### Definition of Done / next action

No critical unresolved issue; residual issues are recorded.

#### Traceability

- Requirements: `F-M06`, `F-M15`, `N-M01`, `N-M02`, `N-M13`
- Work packages: `FR-02`
- Engineering practices: `EP-002`, `EP-021`, `EP-026`, `EP-027`, `EP-032`
- Objectives: `O1`, `O11`, `O2`, `O5`, `O6`, `O8`, `O9`
- Research questions: `RQ-TV`, `RQ4`

<a id="wp-fr-03"></a>
### WP:FR-03 — Final app-integrated evaluation

| Field | Value |
|---|---|
| Task number | 134 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P6 |
| Deadline | 2026-08-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/FR-03/](../../evidence/work-packages/FR-03) |

#### Task statement

Final app-integrated evaluation

#### Definition of Done / next action

Frozen raw dataset and cross-route comparison are complete.

#### Traceability

- Requirements: `C-02`, `C-03`, `F-S12`, `N-M07`, `R-M01`, `R-M04`, `R-M05`, `R-M06`, `R-M07`, `R-M09`, `R-M11`, `R-M12`, `R-M13`, `R-M14`
- Work packages: `FR-03`
- Engineering practices: `EP-021`, `EP-028`, `EP-029`, `EP-030`, `EP-032`, `EP-035`, `EP-036`
- Objectives: `O10`, `O11`, `O3`, `O6`, `O8`, `O9`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="wp-fr-04"></a>
### WP:FR-04 — Clean build/package/manuals/demo

| Field | Value |
|---|---|
| Task number | 144 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P6 |
| Deadline | 2026-08-14 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/FR-04/](../../evidence/work-packages/FR-04) |

#### Task statement

Clean build/package/manuals/demo

#### Definition of Done / next action

Clean build/install and demo pass twice.

#### Traceability

- Requirements: `F-M01`, `G-M08`, `N-M08`, `N-M14`, `N-S02`, `R-M10`
- Work packages: `FR-04`
- Engineering practices: `EP-021`, `EP-032`, `EP-034`, `EP-037`
- Objectives: `O1`, `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="wp-fr-05"></a>
### WP:FR-05 — Release freeze/evidence audit

| Field | Value |
|---|---|
| Task number | 157 |
| Source tab | Work Packages |
| Priority | P0 |
| Release role / phase | P6 |
| Deadline | 2026-08-15 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/work-packages/FR-05/](../../evidence/work-packages/FR-05) |

#### Task statement

Release freeze/evidence audit

#### Definition of Done / next action

Tag, backups, final status and evidence pack complete.

#### Traceability

- Requirements: `C-02`, `G-M03`, `G-M05`, `G-M06`, `G-M09`, `N-M09`, `N-M14`, `R-M08`, `R-M09`, `R-M10`
- Work packages: `FR-05`
- Engineering practices: `EP-021`, `EP-032`, `EP-036`, `EP-038`, `EP-039`, `EP-040`
- Objectives: `O10`, `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

## Engineering practices

<a id="ep-ep-001"></a>
### EP:EP-001 — Freeze problem/aim/RQs/contribution/scope

| Field | Value |
|---|---|
| Task number | 6 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Pre-development |
| Deadline | 2026-07-11 |
| Working status | Implemented |
| Validation | Validated |
| Effective status | **Verified** |
| Planned evidence | [docs/evidence/engineering-practices/EP-001/](../../evidence/engineering-practices/EP-001) |

#### Task statement

Freeze problem/aim/RQs/contribution/scope

#### Definition of Done / next action

Project definition text drafted; version-controlled freeze and contribution statement remain.

#### Traceability

- Requirements: `G-M01`
- Work packages: `PD-01`
- Engineering practices: `EP-001`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-003"></a>
### EP:EP-003 — Version and correct controlling workflows

| Field | Value |
|---|---|
| Task number | 7 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Pre-development |
| Deadline | 2026-07-11 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-003/](../../evidence/engineering-practices/EP-003) |

#### Task statement

Version and correct controlling workflows

#### Definition of Done / next action

Workflows are detailed but contain duplicates/naming/technical contradictions.

#### Traceability

- Requirements: —
- Work packages: `PD-02`
- Engineering practices: `EP-003`
- Objectives: —
- Research questions: —

<a id="ep-ep-020"></a>
### EP:EP-020 — Recover and back up raw feasibility evidence

| Field | Value |
|---|---|
| Task number | 8 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Pre-development |
| Deadline | 2026-07-11 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/evidence/engineering-practices/EP-020/](../../evidence/engineering-practices/EP-020) |

#### Task statement

Recover and back up raw feasibility evidence

#### Definition of Done / next action

Workbooks exist; raw external-path evidence needs manifest/checksum/backup.

#### Traceability

- Requirements: `G-M07`, `R-M08`, `R-M09`
- Work packages: `PD-03`
- Engineering practices: `EP-020`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-004"></a>
### EP:EP-004 — Maintain stable MoSCoW catalogue

| Field | Value |
|---|---|
| Task number | 12 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Pre-development |
| Deadline | 2026-07-12 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/evidence/engineering-practices/EP-004/](../../evidence/engineering-practices/EP-004) |

#### Task statement

Maintain stable MoSCoW catalogue

#### Definition of Done / next action

v1.1 exists; v1.2 must incorporate agreed scope changes.

#### Traceability

- Requirements: `G-M02`, `G-M03`, `G-M06`
- Work packages: `PD-04`
- Engineering practices: `EP-004`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-005"></a>
### EP:EP-005 — Build bidirectional RTM

| Field | Value |
|---|---|
| Task number | 13 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Pre-development |
| Deadline | 2026-07-12 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/evidence/engineering-practices/EP-005/](../../evidence/engineering-practices/EP-005) |

#### Task statement

Build bidirectional RTM

#### Definition of Done / next action

This workbook/Markdown deliverable.

#### Traceability

- Requirements: `G-M02`, `G-M03`, `G-M06`
- Work packages: `PD-04`
- Engineering practices: `EP-005`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-006"></a>
### EP:EP-006 — Maintain derived-requirement and change log

| Field | Value |
|---|---|
| Task number | 14 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Pre-development |
| Deadline | 2026-07-12 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-006/](../../evidence/engineering-practices/EP-006) |

#### Task statement

Maintain derived-requirement and change log

#### Definition of Done / next action

Changes currently exist in conversation/summary but not a controlled log.

#### Traceability

- Requirements: `G-M02`, `G-M03`, `G-M06`
- Work packages: `PD-04`
- Engineering practices: `EP-006`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-007"></a>
### EP:EP-007 — Consolidate risk/assumption/constraint/licence register

| Field | Value |
|---|---|
| Task number | 15 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Pre-development |
| Deadline | 2026-07-12 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/evidence/engineering-practices/EP-007/](../../evidence/engineering-practices/EP-007) |

#### Task statement

Consolidate risk/assumption/constraint/licence register

#### Definition of Done / next action

Content drafted; live owner/status/evidence register remains.

#### Traceability

- Requirements: `G-M05`
- Work packages: `PD-05`
- Engineering practices: `EP-007`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-008"></a>
### EP:EP-008 — Create overall activity/user-journey diagram

| Field | Value |
|---|---|
| Task number | 19 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Pre-development |
| Deadline | 2026-07-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-008/](../../evidence/engineering-practices/EP-008) |

#### Task statement

Create overall activity/user-journey diagram

#### Definition of Done / next action

Textual journey exists; formal visual needed.

#### Traceability

- Requirements: `G-M04`, `N-M12`
- Work packages: `PD-06`
- Engineering practices: `EP-008`
- Objectives: `O1`, `O11`, `O5`, `O8`, `O9`
- Research questions: `RQ1`, `RQ4`

<a id="ep-ep-009"></a>
### EP:EP-009 — Create decision tables for inspection/configuration

| Field | Value |
|---|---|
| Task number | 20 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Pre-development |
| Deadline | 2026-07-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-009/](../../evidence/engineering-practices/EP-009) |

#### Task statement

Create decision tables for inspection/configuration

#### Definition of Done / next action

Rules are spread across workflows.

#### Traceability

- Requirements: `G-M04`
- Work packages: `PD-07`
- Engineering practices: `EP-009`
- Objectives: `O1`, `O11`, `O5`, `O8`, `O9`
- Research questions: `RQ1`, `RQ4`

<a id="ep-ep-011"></a>
### EP:EP-011 — Create import/chat/processing sequence diagrams

| Field | Value |
|---|---|
| Task number | 21 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Pre-development |
| Deadline | 2026-07-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-011/](../../evidence/engineering-practices/EP-011) |

#### Task statement

Create import/chat/processing sequence diagrams

#### Definition of Done / next action

Textual sequences exist; formal diagrams needed.

#### Traceability

- Requirements: `G-M04`, `N-M12`
- Work packages: `PD-06`
- Engineering practices: `EP-011`
- Objectives: `O1`, `O11`, `O5`, `O8`, `O9`
- Research questions: `RQ1`, `RQ4`

<a id="ep-ep-013"></a>
### EP:EP-013 — Rank architecture characteristics/drivers

| Field | Value |
|---|---|
| Task number | 22 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Pre-development |
| Deadline | 2026-07-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-013/](../../evidence/engineering-practices/EP-013) |

#### Task statement

Rank architecture characteristics/drivers

#### Definition of Done / next action

Memory, privacy, reliability, performance and usability are listed but not ranked.

#### Traceability

- Requirements: `G-M04`, `N-M12`
- Work packages: `PD-06`
- Engineering practices: `EP-013`
- Objectives: `O1`, `O11`, `O5`, `O8`, `O9`
- Research questions: `RQ1`, `RQ4`

<a id="ep-ep-014"></a>
### EP:EP-014 — Compare architecture alternatives

| Field | Value |
|---|---|
| Task number | 23 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Pre-development |
| Deadline | 2026-07-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-014/](../../evidence/engineering-practices/EP-014) |

#### Task statement

Compare architecture alternatives

#### Definition of Done / next action

CLI child process, native API, pipes and local HTTP need criteria-based ADR.

#### Traceability

- Requirements: `G-M04`, `N-M12`
- Work packages: `PD-06`
- Engineering practices: `EP-014`
- Objectives: `O1`, `O11`, `O5`, `O8`, `O9`
- Research questions: `RQ1`, `RQ4`

<a id="ep-ep-015"></a>
### EP:EP-015 — Define modular components/responsibilities/interfaces

| Field | Value |
|---|---|
| Task number | 24 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Pre-development |
| Deadline | 2026-07-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-015/](../../evidence/engineering-practices/EP-015) |

#### Task statement

Define modular components/responsibilities/interfaces

#### Definition of Done / next action

Importer, inspector, estimator, selector and adapters need formal boundaries.

#### Traceability

- Requirements: `G-M04`, `N-M12`
- Work packages: `PD-06`, `PD-07`
- Engineering practices: `EP-015`
- Objectives: `O1`, `O11`, `O5`, `O8`, `O9`
- Research questions: `RQ1`, `RQ4`

<a id="ep-ep-016"></a>
### EP:EP-016 — Create context/component/process/deployment views and ADRs

| Field | Value |
|---|---|
| Task number | 25 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Pre-development |
| Deadline | 2026-07-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-016/](../../evidence/engineering-practices/EP-016) |

#### Task statement

Create context/component/process/deployment views and ADRs

#### Definition of Done / next action

Narrative architecture exists; controlled views/decisions do not.

#### Traceability

- Requirements: `G-M04`, `N-M12`
- Work packages: `PD-06`
- Engineering practices: `EP-016`
- Objectives: `O1`, `O11`, `O5`, `O8`, `O9`
- Research questions: `RQ1`, `RQ4`

<a id="ep-ep-017"></a>
### EP:EP-017 — Define diagnostic/error codes and backend contracts

| Field | Value |
|---|---|
| Task number | 26 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Pre-development |
| Deadline | 2026-07-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-017/](../../evidence/engineering-practices/EP-017) |

#### Task statement

Define diagnostic/error codes and backend contracts

#### Definition of Done / next action

Needed for stable UI/service development and failure tests.

#### Traceability

- Requirements: `G-M04`
- Work packages: `PD-07`
- Engineering practices: `EP-017`
- Objectives: `O1`, `O11`, `O5`, `O8`, `O9`
- Research questions: `RQ1`, `RQ4`

<a id="ep-ep-010"></a>
### EP:EP-010 — Define canonical application state machine

| Field | Value |
|---|---|
| Task number | 27 |
| Source tab | Engineering Practices |
| Priority | Recommended |
| Release role / phase | Pre-development |
| Deadline | 2026-07-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-010/](../../evidence/engineering-practices/EP-010) |

#### Task statement

Define canonical application state machine

#### Definition of Done / next action

States are implied but not formally controlled.

#### Traceability

- Requirements: `G-M04`
- Work packages: `PD-07`
- Engineering practices: `EP-010`
- Objectives: `O1`, `O11`, `O5`, `O8`, `O9`
- Research questions: `RQ1`, `RQ4`

<a id="ep-ep-012"></a>
### EP:EP-012 — Create data-flow/privacy boundary diagram

| Field | Value |
|---|---|
| Task number | 28 |
| Source tab | Engineering Practices |
| Priority | Recommended |
| Release role / phase | Pre-development |
| Deadline | 2026-07-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-012/](../../evidence/engineering-practices/EP-012) |

#### Task statement

Create data-flow/privacy boundary diagram

#### Definition of Done / next action

Needed for models, documents, metrics and offline/privacy claims.

#### Traceability

- Requirements: `G-M04`, `N-M12`
- Work packages: `PD-06`
- Engineering practices: `EP-012`
- Objectives: `O1`, `O11`, `O5`, `O8`, `O9`
- Research questions: `RQ1`, `RQ4`

<a id="ep-ep-018"></a>
### EP:EP-018 — Prepare test projects, fixtures, manifests and CI

| Field | Value |
|---|---|
| Task number | 32 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Pre-development |
| Deadline | 2026-07-14 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/evidence/engineering-practices/EP-018/](../../evidence/engineering-practices/EP-018) |

#### Task statement

Prepare test projects, fixtures, manifests and CI

#### Definition of Done / next action

Repository structure exists; real test projects/CI remain.

#### Traceability

- Requirements: `G-M06`, `N-M08`, `N-M09`, `R-M08`
- Work packages: `PD-08`
- Engineering practices: `EP-018`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-019"></a>
### EP:EP-019 — Freeze app-specific evaluation addendum

| Field | Value |
|---|---|
| Task number | 33 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Pre-development |
| Deadline | 2026-07-14 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/evidence/engineering-practices/EP-019/](../../evidence/engineering-practices/EP-019) |

#### Task statement

Freeze app-specific evaluation addendum

#### Definition of Done / next action

RQs/metrics drafted; experiment IDs, prompts/rubric and stopping rules need freeze.

#### Traceability

- Requirements: `N-M07`, `R-M06`, `R-M11`
- Work packages: `PD-09`
- Engineering practices: `EP-019`
- Objectives: `O10`, `O11`, `O6`, `O8`
- Research questions: `RQ1`, `RQ2`, `RQ3`

<a id="ep-ep-023"></a>
### EP:EP-023 — Implement unit tests for pure logic

| Field | Value |
|---|---|
| Task number | 65 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Development |
| Deadline | 2026-07-27 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-023/](../../evidence/engineering-practices/EP-023) |

#### Task statement

Implement unit tests for pure logic

#### Definition of Done / next action

Parser, estimator, registry and selectors should be isolated from UI.

#### Traceability

- Requirements: `F-M03`, `F-M08`, `F-M10`, `F-M11`, `F-M14`, `F-S04`
- Work packages: `HE-03`, `HE-05`, `HE-06`, `IM-04`
- Engineering practices: `EP-023`
- Objectives: `O10`, `O2`, `O3`, `O4`, `O7`, `O8`
- Research questions: `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-024"></a>
### EP:EP-024 — Implement contract/integration tests for runtimes

| Field | Value |
|---|---|
| Task number | 100 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Development |
| Deadline | 2026-08-09 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-024/](../../evidence/engineering-practices/EP-024) |

#### Task statement

Implement contract/integration tests for runtimes

#### Definition of Done / next action

Exact versions, stdout/stderr, exit codes and fallback must be tested.

#### Traceability

- Requirements: `F-M13`, `F-M18`, `F-M21`, `F-M26`, `F-M27`, `F-S02`, `F-S11`, `N-M03`, `N-M04`, `N-M05`, `N-M06`, `N-M10`, `N-M11`, `N-M12`, `R-M13`
- Work packages: `OV-02`, `QX-03`, `RT-01`, `TV-03`
- Engineering practices: `EP-024`
- Objectives: `O1`, `O10`, `O11`, `O2`, `O5`, `O6`, `O7`, `O8`, `O9`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ4`

<a id="ep-ep-022"></a>
### EP:EP-022 — Apply branch/PR/CI controls

| Field | Value |
|---|---|
| Task number | 112 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Development |
| Deadline | 2026-08-11 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-022/](../../evidence/engineering-practices/EP-022) |

#### Task statement

Apply branch/PR/CI controls

#### Definition of Done / next action

No final branch protection/status-check evidence yet.

#### Traceability

- Requirements: `G-M03`, `G-M06`, `N-M08`, `N-M09`, `R-M08`
- Work packages: `FR-01`, `PD-08`
- Engineering practices: `EP-022`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-025"></a>
### EP:EP-025 — Implement E2E happy/failure/cancellation paths

| Field | Value |
|---|---|
| Task number | 113 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Development |
| Deadline | 2026-08-11 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-025/](../../evidence/engineering-practices/EP-025) |

#### Task statement

Implement E2E happy/failure/cancellation paths

#### Definition of Done / next action

Core user journeys must be executable and traceable.

#### Traceability

- Requirements: `F-M06`, `F-M12`, `F-M15`, `F-M18`, `F-M22`, `G-M03`, `N-M01`, `N-M06`
- Work packages: `FR-01`, `HE-08`, `RT-05`
- Engineering practices: `EP-025`
- Objectives: `O1`, `O11`, `O2`, `O4`, `O5`, `O6`, `O7`, `O8`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-033"></a>
### EP:EP-033 — Run full regression and requirements acceptance

| Field | Value |
|---|---|
| Task number | 114 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | After implementation |
| Deadline | 2026-08-11 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-033/](../../evidence/engineering-practices/EP-033) |

#### Task statement

Run full regression and requirements acceptance

#### Definition of Done / next action

Every active Must needs final status and evidence.

#### Traceability

- Requirements: `G-M03`
- Work packages: `FR-01`
- Engineering practices: `EP-033`
- Objectives: `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-026"></a>
### EP:EP-026 — Run security/path/offline/privacy tests

| Field | Value |
|---|---|
| Task number | 121 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Development |
| Deadline | 2026-08-12 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-026/](../../evidence/engineering-practices/EP-026) |

#### Task statement

Run security/path/offline/privacy tests

#### Definition of Done / next action

CLI/file boundaries and network behaviour are safety critical.

#### Traceability

- Requirements: `F-M06`, `F-M15`, `N-M01`, `N-M02`, `N-M13`
- Work packages: `FR-02`
- Engineering practices: `EP-026`
- Objectives: `O1`, `O11`, `O2`, `O5`, `O6`, `O8`, `O9`
- Research questions: `RQ-TV`, `RQ4`

<a id="ep-ep-027"></a>
### EP:EP-027 — Evaluate usability/accessibility with tasks

| Field | Value |
|---|---|
| Task number | 122 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Development |
| Deadline | 2026-08-12 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-027/](../../evidence/engineering-practices/EP-027) |

#### Task statement

Evaluate usability/accessibility with tasks

#### Definition of Done / next action

Keyboard, 200% scaling, experimental-label understanding and recovery tasks.

#### Traceability

- Requirements: `F-M06`, `F-M15`, `N-M01`, `N-M02`, `N-M13`
- Work packages: `FR-02`
- Engineering practices: `EP-027`
- Objectives: `O1`, `O11`, `O2`, `O5`, `O6`, `O8`, `O9`
- Research questions: `RQ-TV`, `RQ4`

<a id="ep-ep-002"></a>
### EP:EP-002 — Validate stakeholder/sector assumptions

| Field | Value |
|---|---|
| Task number | 123 |
| Source tab | Engineering Practices |
| Priority | Recommended |
| Release role / phase | Pre-development |
| Deadline | 2026-08-12 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-002/](../../evidence/engineering-practices/EP-002) |

#### Task statement

Validate stakeholder/sector assumptions

#### Definition of Done / next action

Healthcare/education relevance currently relies mainly on planning assumptions.

#### Traceability

- Requirements: `F-M06`, `F-M15`, `N-M01`, `N-M02`, `N-M13`
- Work packages: `FR-02`
- Engineering practices: `EP-002`
- Objectives: `O1`, `O11`, `O2`, `O5`, `O6`, `O8`, `O9`
- Research questions: `RQ-TV`, `RQ4`

<a id="ep-ep-028"></a>
### EP:EP-028 — Use experiment manifests and preserve failed runs

| Field | Value |
|---|---|
| Task number | 135 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Development |
| Deadline | 2026-08-13 |
| Working status | Partially Verified |
| Validation | Not Validated |
| Effective status | **Partially Verified** |
| Planned evidence | [docs/evidence/engineering-practices/EP-028/](../../evidence/engineering-practices/EP-028) |

#### Task statement

Use experiment manifests and preserve failed runs

#### Definition of Done / next action

Existing campaigns use workbooks/manifests; final app experiment schema must be enforced.

#### Traceability

- Requirements: `C-02`, `C-03`, `F-S12`, `N-M07`, `R-M01`, `R-M04`, `R-M05`, `R-M06`, `R-M07`, `R-M09`, `R-M11`, `R-M12`, `R-M13`, `R-M14`
- Work packages: `FR-03`, `PD-09`
- Engineering practices: `EP-028`
- Objectives: `O10`, `O11`, `O3`, `O6`, `O8`, `O9`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-029"></a>
### EP:EP-029 — Evaluate AI output quality with frozen prompts/rubric

| Field | Value |
|---|---|
| Task number | 136 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Development |
| Deadline | 2026-08-13 |
| Working status | Partially Verified |
| Validation | Not Validated |
| Effective status | **Partially Verified** |
| Planned evidence | [docs/evidence/engineering-practices/EP-029/](../../evidence/engineering-practices/EP-029) |

#### Task statement

Evaluate AI output quality with frozen prompts/rubric

#### Definition of Done / next action

Existing quality evidence exists; final app-integrated set needs freeze.

#### Traceability

- Requirements: `C-02`, `C-03`, `F-S12`, `N-M07`, `R-M01`, `R-M04`, `R-M05`, `R-M06`, `R-M07`, `R-M09`, `R-M11`, `R-M12`, `R-M13`, `R-M14`
- Work packages: `FR-03`, `PD-09`
- Engineering practices: `EP-029`
- Objectives: `O10`, `O11`, `O3`, `O6`, `O8`, `O9`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-030"></a>
### EP:EP-030 — Calibrate estimator and quantify uncertainty

| Field | Value |
|---|---|
| Task number | 137 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Development |
| Deadline | 2026-08-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-030/](../../evidence/engineering-practices/EP-030) |

#### Task statement

Calibrate estimator and quantify uncertainty

#### Definition of Done / next action

Report error, false-safe and false-unsafe results.

#### Traceability

- Requirements: `C-02`, `C-03`, `F-M08`, `F-S12`, `N-M07`, `R-M01`, `R-M04`, `R-M05`, `R-M06`, `R-M07`, `R-M09`, `R-M11`, `R-M12`, `R-M13`, `R-M14`
- Work packages: `FR-03`, `HE-04`
- Engineering practices: `EP-030`
- Objectives: `O10`, `O11`, `O3`, `O6`, `O8`, `O9`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-035"></a>
### EP:EP-035 — Complete cross-route and memory/quality analysis

| Field | Value |
|---|---|
| Task number | 138 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | After implementation |
| Deadline | 2026-08-13 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-035/](../../evidence/engineering-practices/EP-035) |

#### Task statement

Complete cross-route and memory/quality analysis

#### Definition of Done / next action

Populate final comparison from recovered/new evidence.

#### Traceability

- Requirements: `C-02`, `C-03`, `F-S12`, `N-M07`, `R-M01`, `R-M04`, `R-M05`, `R-M06`, `R-M07`, `R-M09`, `R-M11`, `R-M12`, `R-M13`, `R-M14`
- Work packages: `FR-03`
- Engineering practices: `EP-035`
- Objectives: `O10`, `O11`, `O3`, `O6`, `O8`, `O9`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-034"></a>
### EP:EP-034 — Run clean build/install/reproduction

| Field | Value |
|---|---|
| Task number | 145 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | After implementation |
| Deadline | 2026-08-14 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-034/](../../evidence/engineering-practices/EP-034) |

#### Task statement

Run clean build/install/reproduction

#### Definition of Done / next action

Independent clean environment required.

#### Traceability

- Requirements: `F-M01`, `G-M08`, `N-M08`, `N-M14`, `N-S02`, `R-M10`
- Work packages: `FR-04`
- Engineering practices: `EP-034`
- Objectives: `O1`, `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-037"></a>
### EP:EP-037 — Complete manuals/known limitations/feature status

| Field | Value |
|---|---|
| Task number | 146 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | After implementation |
| Deadline | 2026-08-14 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-037/](../../evidence/engineering-practices/EP-037) |

#### Task statement

Complete manuals/known limitations/feature status

#### Definition of Done / next action

User/developer/build/limitations docs.

#### Traceability

- Requirements: `F-M01`, `G-M08`, `N-M08`, `N-M14`, `N-S02`, `R-M10`
- Work packages: `FR-04`
- Engineering practices: `EP-037`
- Objectives: `O1`, `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-021"></a>
### EP:EP-021 — Use small vertical slices linked to requirement IDs

| Field | Value |
|---|---|
| Task number | 158 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Development |
| Deadline | 2026-08-15 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-021/](../../evidence/engineering-practices/EP-021) |

#### Task statement

Use small vertical slices linked to requirement IDs

#### Definition of Done / next action

Issues/PRs should list requirement and evidence IDs.

#### Traceability

- Requirements: `C-02`, `C-03`, `F-M01`, `F-M02`, `F-M03`, `F-M04`, `F-M05`, `F-M06`, `F-M07`, `F-M08`, `F-M09`, `F-M10`, `F-M11`, `F-M12`, `F-M13`, `F-M14`, `F-M15`, `F-M16`, `F-M17`, `F-M18`, `F-M19`, `F-M20`, `F-M21`, `F-M22`, `F-M23`, `F-M24`, `F-M25`, `F-M26`, `F-M27`, `F-M28`, `F-S01`, `F-S02`, `F-S03`, `F-S04`, `F-S08`, `F-S11`, `F-S12`, `F-S15`, `G-M03`, `G-M05`, `G-M06`, `G-M08`, `G-M09`, `N-M01`, `N-M02`, `N-M03`, `N-M04`, `N-M05`, `N-M06`, `N-M07`, `N-M08`, `N-M09`, `N-M10`, `N-M11`, `N-M12`, `N-M13`, `N-M14`, `N-S02`, `R-M01`, `R-M02`, `R-M03`, `R-M04`, `R-M05`, `R-M06`, `R-M07`, `R-M08`, `R-M09`, `R-M10`, `R-M11`, `R-M12`, `R-M13`, `R-M14`
- Work packages: `DL-01`, `DL-02`, `DL-03`, `FR-01`, `FR-02`, `FR-03`, `FR-04`, `FR-05`, `HE-01`, `HE-02`, `HE-03`, `HE-04`, `HE-05`, `HE-06`, `HE-07`, `HE-08`, `IM-01`, `IM-02`, `IM-03`, `IM-04`, `IM-05`, `IM-06`, `IM-07`, `OV-01`, `OV-02`, `OV-03`, `QX-01`, `QX-02`, `QX-03`, `QX-04`, `RT-01`, `RT-02`, `RT-03`, `RT-04`, `RT-05`, `TV-01`, `TV-02`, `TV-03`, `TV-04`
- Engineering practices: `EP-021`
- Objectives: `O1`, `O10`, `O11`, `O2`, `O3`, `O4`, `O5`, `O6`, `O7`, `O8`, `O9`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-031"></a>
### EP:EP-031 — Record actual backend/device/optimisation state

| Field | Value |
|---|---|
| Task number | 159 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | Development |
| Deadline | 2026-08-15 |
| Working status | Partially Verified |
| Validation | Not Validated |
| Effective status | **Partially Verified** |
| Planned evidence | [docs/evidence/engineering-practices/EP-031/](../../evidence/engineering-practices/EP-031) |

#### Task statement

Record actual backend/device/optimisation state

#### Definition of Done / next action

Existing experiments do this; app adapters must carry it through.

#### Traceability

- Requirements: `F-M06`, `F-M13`, `F-M14`, `F-M15`, `F-M18`, `F-M19`, `F-M20`, `F-M21`, `F-M22`, `F-M23`, `F-M24`, `F-M28`, `F-S02`, `F-S11`, `F-S15`, `N-M01`, `N-M03`, `N-M04`, `N-M05`, `N-M06`, `N-M10`, `N-M11`, `N-M12`, `R-M01`, `R-M03`
- Work packages: `OV-01`, `OV-02`, `OV-03`, `QX-01`, `QX-02`, `QX-03`, `QX-04`, `RT-01`, `RT-02`, `RT-03`, `RT-04`, `RT-05`
- Engineering practices: `EP-031`
- Objectives: `O1`, `O10`, `O11`, `O2`, `O5`, `O6`, `O7`, `O8`
- Research questions: `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-036"></a>
### EP:EP-036 — Discuss threats to validity and claim boundaries

| Field | Value |
|---|---|
| Task number | 160 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | After implementation |
| Deadline | 2026-08-15 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-036/](../../evidence/engineering-practices/EP-036) |

#### Task statement

Discuss threats to validity and claim boundaries

#### Definition of Done / next action

One laptop, model provenance, measurement definitions and small UX sample.

#### Traceability

- Requirements: `C-02`, `C-03`, `F-S12`, `G-M03`, `G-M05`, `G-M06`, `G-M09`, `N-M07`, `N-M09`, `N-M14`, `R-M01`, `R-M04`, `R-M05`, `R-M06`, `R-M07`, `R-M08`, `R-M09`, `R-M10`, `R-M11`, `R-M12`, `R-M13`, `R-M14`
- Work packages: `FR-03`, `FR-05`
- Engineering practices: `EP-036`
- Objectives: `O10`, `O11`, `O3`, `O6`, `O8`, `O9`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-038"></a>
### EP:EP-038 — Final traceability and evidence audit

| Field | Value |
|---|---|
| Task number | 161 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | After implementation |
| Deadline | 2026-08-15 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-038/](../../evidence/engineering-practices/EP-038) |

#### Task statement

Final traceability and evidence audit

#### Definition of Done / next action

No Must counted complete without evidence.

#### Traceability

- Requirements: `C-02`, `G-M03`, `G-M05`, `G-M06`, `G-M09`, `N-M09`, `N-M14`, `R-M08`, `R-M09`, `R-M10`
- Work packages: `FR-05`
- Engineering practices: `EP-038`
- Objectives: `O10`, `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-039"></a>
### EP:EP-039 — Tag release, checksum and independent backup

| Field | Value |
|---|---|
| Task number | 162 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | After implementation |
| Deadline | 2026-08-15 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-039/](../../evidence/engineering-practices/EP-039) |

#### Task statement

Tag release, checksum and independent backup

#### Definition of Done / next action

Controlled handover baseline.

#### Traceability

- Requirements: `C-02`, `G-M03`, `G-M05`, `G-M06`, `G-M09`, `N-M09`, `N-M14`, `R-M08`, `R-M09`, `R-M10`
- Work packages: `FR-05`
- Engineering practices: `EP-039`
- Objectives: `O10`, `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-040"></a>
### EP:EP-040 — Answer every RQ from project evidence

| Field | Value |
|---|---|
| Task number | 163 |
| Source tab | Engineering Practices |
| Priority | Essential |
| Release role / phase | After implementation |
| Deadline | 2026-08-15 |
| Working status | Not Started |
| Validation | Not Validated |
| Effective status | **Not Started** |
| Planned evidence | [docs/evidence/engineering-practices/EP-040/](../../evidence/engineering-practices/EP-040) |

#### Task statement

Answer every RQ from project evidence

#### Definition of Done / next action

Positive and negative findings must be explicit.

#### Traceability

- Requirements: `C-02`, `G-M03`, `G-M05`, `G-M06`, `G-M09`, `N-M09`, `N-M14`, `R-M08`, `R-M09`, `R-M10`
- Work packages: `FR-05`
- Engineering practices: `EP-040`
- Objectives: `O10`, `O11`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`

<a id="ep-ep-032"></a>
### EP:EP-032 — Maintain engineering journal and decision evidence

| Field | Value |
|---|---|
| Task number | 164 |
| Source tab | Engineering Practices |
| Priority | Recommended |
| Release role / phase | Development |
| Deadline | 2026-08-15 |
| Working status | In Progress |
| Validation | Not Validated |
| Effective status | **In Progress** |
| Planned evidence | [docs/evidence/engineering-practices/EP-032/](../../evidence/engineering-practices/EP-032) |

#### Task statement

Maintain engineering journal and decision evidence

#### Definition of Done / next action

Journal template exists; disciplined updates required.

#### Traceability

- Requirements: `C-02`, `C-03`, `F-M01`, `F-M02`, `F-M03`, `F-M04`, `F-M05`, `F-M06`, `F-M07`, `F-M08`, `F-M09`, `F-M10`, `F-M11`, `F-M12`, `F-M13`, `F-M14`, `F-M15`, `F-M16`, `F-M17`, `F-M18`, `F-M19`, `F-M20`, `F-M21`, `F-M22`, `F-M23`, `F-M24`, `F-M25`, `F-M26`, `F-M27`, `F-M28`, `F-S01`, `F-S02`, `F-S03`, `F-S04`, `F-S08`, `F-S11`, `F-S12`, `F-S15`, `G-M01`, `G-M02`, `G-M03`, `G-M04`, `G-M05`, `G-M06`, `G-M07`, `G-M08`, `G-M09`, `N-M01`, `N-M02`, `N-M03`, `N-M04`, `N-M05`, `N-M06`, `N-M07`, `N-M08`, `N-M09`, `N-M10`, `N-M11`, `N-M12`, `N-M13`, `N-M14`, `N-S02`, `R-M01`, `R-M02`, `R-M03`, `R-M04`, `R-M05`, `R-M06`, `R-M07`, `R-M08`, `R-M09`, `R-M10`, `R-M11`, `R-M12`, `R-M13`, `R-M14`
- Work packages: `DL-01`, `DL-02`, `DL-03`, `FR-01`, `FR-02`, `FR-03`, `FR-04`, `FR-05`, `HE-01`, `HE-02`, `HE-03`, `HE-04`, `HE-05`, `HE-06`, `HE-07`, `HE-08`, `IM-01`, `IM-02`, `IM-03`, `IM-04`, `IM-05`, `IM-06`, `IM-07`, `OV-01`, `OV-02`, `OV-03`, `PD-01`, `PD-02`, `PD-03`, `PD-04`, `PD-05`, `PD-06`, `PD-07`, `PD-08`, `PD-09`, `PD-10`, `QX-01`, `QX-02`, `QX-03`, `QX-04`, `RT-01`, `RT-02`, `RT-03`, `RT-04`, `RT-05`, `TV-01`, `TV-02`, `TV-03`, `TV-04`
- Engineering practices: `EP-032`
- Objectives: `O1`, `O10`, `O11`, `O2`, `O3`, `O4`, `O5`, `O6`, `O7`, `O8`, `O9`
- Research questions: `RQ-TV`, `RQ1`, `RQ2`, `RQ3`, `RQ4`
