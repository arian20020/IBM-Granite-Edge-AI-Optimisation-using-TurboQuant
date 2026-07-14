# MoSCoW Requirements Baseline

**Document ID:** REQ-MOSCOW-001  
**Version:** 1.2  
**Status:** Developer-approved working baseline — supervisor review pending  
**Prepared and approved by:** Arian B  
**Effective date:** 14 July 2026  
**Supersedes:** MoSCoW Requirements Baseline v1.1  
**Related change:** CHG-013 / CR-013  

> This baseline consists of this priority catalogue and the linked [Requirements Traceability Matrix v1.3](Requirements-Traceability-Matrix-v1.3.md), which contains each requirement’s rationale, source, objective/RQ mapping, work package, planned component, measurable acceptance criteria, verification method, evidence ID and evidence path.

> IDs are stable traceability keys. A legacy ID prefix may not match the current priority after a controlled priority change. The current Priority, Release Role and Lifecycle fields control.

## Baseline counts

- Lifecycle records: **111**
- Active requirements: **72**
- Must: **56**
- Should: **14**
- Could: **2**
- Deferred: **11**
- Superseded: **10**
- Excluded/Won't Have: **18**
- Duplicate IDs: **0**
- Active Must Haves missing required RTM fields: **0**

## Completion rule

Written scope is not implementation evidence. An item becomes Verified only when its deliverable exists, evidence exists, acceptance criteria are checked and the controlled RTM marks Validation as `Validated`.

## CHG-013 decisions

- F-M16, F-M17, F-M20, F-M23 and F-M24 are active Should Haves.
- F-M18 and the narrowed F-M19 remain Must Haves.
- F-M21, F-M22 and N-M11 remain bounded Must Haves for one pinned TurboQuant route, truthful activation reporting and upstream fallback.
- R-M02 remains Must as the TurboVec identification and release-decision gate.
- F-M25, F-M26, F-M27 and R-M13 are Deferred from the first release.

## Must Haves

| ID | Requirement | Acceptance / verification control |
|---|---|---|
| F-M01 | The application must open on the target Windows 11 x64 Intel computer. | `AC-F-M01` — Clean-build test and release demo |
| F-M02 | The application must let the user select a supported local model. | `AC-F-M02` — Picker integration test and UI test |
| F-M03 | The application must validate a selected input before using it. | `AC-F-M03` — Automated validation matrix |
| F-M04 | The application must show the correct inspection result state. | `AC-F-M04` — State-decision table test and UI state test |
| F-M05 | The application must display the model details needed for compatibility and memory checks. | `AC-F-M05` — Real-model inspection test and fixture tests |
| F-M06 | The application must show plain-English reasons for warnings or failures. | `AC-F-M06` — Failure-message tests and task-based usability test |
| F-M07 | The application must read the hardware information needed for fit analysis. | `AC-F-M07` — Hardware service unit/integration test |
| F-M08 | The application must estimate peak memory for a supported configuration. | `AC-F-M08` — Formula/unit tests and predicted-versus-measured calibration |
| F-M09 | The application must show whether a configuration is likely to fit, needs optimisation, or has no verified safe option. | `AC-F-M09` — Boundary and UI decision tests |
| F-M10 | The application must generate only complete configurations valid for the current model, runtime and device. | `AC-F-M10` — Decision-table and registry tests |
| F-M11 | The application must provide Automatic, Quality, Balanced and Efficiency modes when valid alternatives exist. | `AC-F-M11` — Mode algorithm tests and UI interaction tests |
| F-M12 | The application must show the chosen complete configuration before starting an operation. | `AC-F-M12` — UI acceptance test |
| F-M13 | The application must run local chat with at least one supported Granite GGUF model. | `AC-F-M13` — End-to-end application test |
| F-M14 | The application must keep the original model file unchanged. | `AC-F-M14` — Hash integrity test |
| F-M15 | The application must give the user a clear next step after a failure. | `AC-F-M15` — Failure/recovery test and usability task |
| F-M18 | The WinUI application must launch and control supported local command-line runtimes without requiring the user to enter terminal commands. | `AC-F-M18` — Contract, path-safety and integration tests |
| F-M19 | Long-running operations included in the active first-release workflow must stream progress or output and support safe cancellation. | `AC-F-M19` — Streaming, responsiveness, cancellation and cleanup tests |
| F-M21 | The application must run at least one verified TurboQuant-enabled Granite configuration end to end. | `AC-F-M21` — App-integrated TurboQuant E2E and activation audit |
| F-M22 | A dependable upstream llama.cpp configuration must remain available when the TurboQuant route is unavailable or fails. | `AC-F-M22` — Forced-failure and recovery test |
| F-M28 | The user must be able to copy generated chat output. | `AC-F-M28` — UI/clipboard test |
| R-M01 | The project must compare at least one verified TurboQuant run with a matched standard KV-cache baseline. | `EXP-TQ-COMP-001` — Controlled matched experiment |
| R-M02 | The project must identify, pin and document the exact TurboVec implementation and decide its release role. | `DEC-TV-001` — Source inspection, licence review, bounded technical spike and ADR review |
| R-M03 | The project must complete and preserve an official OpenVINO GenAI Granite baseline or a reproducible blocker. | `EXP-OV-OFFICIAL-001` — Official-source conversion/runtime experiment |
| R-M04 | The project must measure memory use for every final test configuration. | `MET-MEM` — Metrics audit |
| R-M05 | The project must evaluate runtime performance for every final test configuration. | `MET-PERF` — Performance experiment audit |
| R-M06 | The project must compare output quality with a fixed prompt set and scoring guide. | `QUAL-FINAL` — Quality evaluation |
| R-M07 | The project must identify the largest stable tested context for selected final configurations. | `CTX-FINAL` — Context stability experiment |
| R-M08 | The project must preserve a complete record of every final experiment. | `EVID-AUDIT` — Evidence-manifest audit |
| R-M09 | The project must record failed attempts and known limitations. | `FAIL-AUDIT` — Failure-register and report audit |
| R-M10 | Another developer must be able to reproduce the clean build and core result from written instructions. | `REPRO-001` — Independent reproduction test |
| R-M11 | The project must determine which selected Granite/runtime/backend combinations run reliably on the tested Intel CPU and integrated GPU. | `COMPAT-FINAL` — Cross-route evidence audit |
| R-M12 | The project must assess selected complete configurations against 4 GB, 8 GB and 16 GB total system-memory budgets. | `MEM-BUDGET-001` — Budget-analysis audit |
| R-M14 | The project must quantify memory-estimator error and false-safe/false-unsafe recommendations. | `EST-VALID-001` — Estimator validation experiment |
| N-M01 | The core workflow must work locally after required models and tools are installed. | `AC-N-M01` — Offline end-to-end test |
| N-M02 | The application must not upload models, prompts, knowledge files or answers to a cloud AI service in the core workflow. | `AC-N-M02` — Network/offline check and data-flow review |
| N-M03 | The application window must remain responsive during long tasks. | `AC-N-M03` — UI responsiveness test |
| N-M04 | The application must show the current state of a long task. | `AC-N-M04` — State-transition and UI tests |
| N-M05 | The application must handle model/document paths and process arguments safely. | `AC-N-M05` — Path and argument security tests |
| N-M06 | The application must clean temporary files and stopped child processes. | `AC-N-M06` — Cleanup/failure-injection tests |
| N-M07 | Each final run must record requested and actual backend, device and optimisation state. | `AC-N-M07` — Manifest audit |
| N-M08 | A clean copy of the repository must build and run its automated tests. | `AC-N-M08` — Clean-build and CI test |
| N-M09 | The repository must not contain secrets, personal test data or large proprietary model files. | `AC-N-M09` — Repository scan |
| N-M10 | Experimental options must be clearly labelled in the interface. | `AC-N-M10` — Registry/UI inspection test and UX task |
| N-M11 | An experimental option must not be reported as active unless activation is proved. | `AC-N-M11` — Log/manifest audit and forced-unverified test |
| N-M12 | The main local inference route must not require a local web server or open network port. | `AC-N-M12` — Port/network inspection test |
| N-M13 | The main workflow must support keyboard operation and Windows text scaling. | `AC-N-M13` — Accessibility checklist and task test |
| N-M14 | The project must produce a basic distributable Windows x64 release build with documented dependencies. | `AC-N-M14` — Release smoke and checksum test |
| G-M01 | The project problem, aim, research questions, objectives, first-release scope, exclusions and satisfactory outcome must be version-controlled. | `AC-G-M01` — Document review and Git history |
| G-M02 | The project must maintain one versioned MoSCoW requirements baseline with stable IDs and measurable acceptance criteria. | `AC-G-M02` — Requirements-quality review and Must-Have coverage audit |
| G-M03 | The project must maintain bidirectional traceability from requirements to objectives/RQs, work packages, implementation, tests and evidence. | `AC-G-M03` — RTM completeness formula and manual audit |
| G-M04 | The project must record proportionate architecture views, key ADRs, stable contracts, states and diagnostic codes. | `AC-G-M04` — Architecture consistency review |
| G-M05 | The project must maintain a consolidated risk, assumption, constraint and licence register. | `AC-G-M05` — Register audit |
| G-M06 | Requirement and scope changes must be linked to dated decisions, GitHub issues, pull requests/commits and affected tests. | `AC-G-M06` — Change-log/PR audit |
| G-M07 | Irreplaceable raw evidence from completed feasibility campaigns must be recovered, hashed, indexed and backed up. | `AC-G-M07` — Evidence recovery audit |
| G-M08 | The release must include a README, user manual, developer/build guide, known limitations and final feature-status table. | `AC-G-M08` — Documentation and walkthrough review |
| G-M09 | The final release must be tied to a Git tag, checksums, evidence pack, independent backup and evidence-based answers to every RQ. | `AC-G-M09` — Final release gate audit |

## Should Haves

| ID | Requirement |
|---|---|
| F-M16 | The application should download at least one approved recommended Granite model from a fixed trusted source. |
| F-M17 | If model downloading is implemented, the application should prevent partial, corrupt or unverified downloads from being used as valid models. |
| F-M20 | The local chat should support at least two user turns in the same session. |
| F-M23 | The application should create one new validated GGUF model artefact through a supported weight-quantisation workflow. |
| F-M24 | If the application generates a new model artefact, it should create a processing manifest containing source/output hashes, tool/version, settings and result. |
| F-S01 | The application should support drag-and-drop model import. |
| F-S02 | The application should recognise complete OpenVINO IR model folders. |
| F-S03 | The application should recognise selected Hugging Face/Safetensors model folders. |
| F-S04 | The user should be able to change the requested context length before configuration selection. |
| F-S08 | The user should be able to copy or save the technical inspection report. |
| F-S11 | The application should provide one official OpenVINO GenAI inference route after its integration gate passes. |
| F-S12 | The application should export benchmark results as CSV or JSON. |
| F-S15 | The project should provide one verified source-to-OpenVINO model-preparation route. |
| N-S02 | The project should provide a simple packaged installation route such as MSIX. |

## Could Haves

| ID | Requirement |
|---|---|
| C-02 | The application could keep a local benchmark history. |
| C-03 | The application could show charts for memory and speed trade-offs. |

## Deferred

| ID | Requirement |
|---|---|
| F-M25 | A future release could import and preserve at least one supported text-based knowledge file for a bounded retrieval workflow. |
| F-M26 | A future release could create an uncompressed embedding baseline and use a pinned TurboVec implementation to compress or optimise vectors. |
| F-M27 | A future release could retrieve relevant sections from a local index and provide them to the Granite chat workflow. |
| R-M13 | If TurboVec integration is reactivated, the project should compare compressed or optimised vectors with an uncompressed-vector baseline. |
| C-04 | The application could support selected non-Granite GGUF models. |
| C-05 | The project could explore DirectML as another Windows route. |
| C-06 | The project could test an Intel NPU when suitable hardware is available. |
| C-07 | The project could explore macOS and Metal after the Windows release. |
| C-08 | The project could explore a cross-platform CMake backend. |
| C-09 | The project could try a model near 32 billion parameters on stronger hardware. |
| C-10 | The application could support more than one model session at a time. |

## Won't Have / excluded boundaries

| ID | Exclusion |
|---|---|
| W-01 | The project will not train or fine-tune a Granite model. |
| W-02 | The first release will not support every GGUF, OpenVINO or Hugging Face model. |
| W-03 | The project will not guarantee that a 32B model runs on the target laptop. |
| W-04 | The project will not guarantee that every model runs on a 4 GB, 8 GB or 16 GB computer. |
| W-05 | The project will not claim six-times compression, near-zero quality loss or sub-second latency without its own evidence. |
| W-06 | The core workflow will not depend on cloud inference. |
| W-07 | The project will not use real patient data or provide medical advice. |
| W-08 | The interface will not show an unverified experimental option as a normal working option. |
| W-09 | The project will not build a full replacement for llama.cpp, OpenVINO, LM Studio or Ollama. |
| W-10 | Full cross-platform delivery is not required for this release. |
| W-11 | The application will not automatically re-quantise an already quantised GGUF through an unsafe route. |
| W-12 | A runtime-only TurboQuant setting will not be presented as a new exportable model file. |
| W-13 | TurboVec will not be described as directly compressing the original knowledge file. |
| W-14 | The application will not provide unrestricted model downloads from arbitrary or untrusted sources. |
| W-15 | The first release will not be a full enterprise RAG, document-management or vector-database platform. |
| W-16 | The prototype will not claim operational clinical, NHS, school or classroom approval. |
| W-17 | The first release will not provide multi-user server operation or multiple simultaneous model sessions. |
| W-18 | The first release will not automatically update runtimes/models or collect prompts/documents for analytics. |

## Approval record

| Role | Name / state | Date |
|---|---|---|
| Developer and decision owner | Arian B | 14 July 2026 |
| Supervisor review | Pending — no approval claimed | — |

Any material change requires a new change record and aligned updates to the RTM, tests, evidence and report.
