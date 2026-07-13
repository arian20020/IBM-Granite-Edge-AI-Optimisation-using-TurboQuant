<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# MoSCoW Requirements Baseline v1.2 â€” Generated Draft Snapshot

> The Excel RTM is the authoritative status/statistics source. This Markdown file is a review snapshot and must be regenerated or updated after approved changes.

## Must (65)

### F-M01 â€” Functional

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must open on the target Windows 11 x64 Intel computer.

**Acceptance criteria:** Clean checkout builds; release build launches twice on the target laptop without fatal error.

**Verification:** Clean-build test and release demo  
**Evidence ID/path:** AC-F-M01 â€” `docs/evidence/requirements/F-M01/`

### F-M02 â€” Functional

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must let the user select a supported local model.

**Acceptance criteria:** A user selects a valid GGUF file with the Windows picker; cancel returns safely; path is retained for inspection.

**Verification:** Picker integration test and UI test  
**Evidence ID/path:** AC-F-M02 â€” `docs/evidence/requirements/F-M02/`

### F-M03 â€” Functional

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must validate a selected input before using it.

**Acceptance criteria:** Missing, empty, wrong-type, truncated and corrupt fixtures are rejected with a classified reason; valid fixture proceeds.

**Verification:** Automated validation matrix  
**Evidence ID/path:** AC-F-M03 â€” `docs/evidence/requirements/F-M03/`

### F-M04 â€” Functional

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must show the correct inspection result state.

**Acceptance criteria:** Fixtures produce Ready, Ready with warnings, Conversion required, Unsupported, and Invalid or incomplete.

**Verification:** State-decision table test and UI state test  
**Evidence ID/path:** AC-F-M04 â€” `docs/evidence/requirements/F-M04/`

### F-M05 â€” Functional

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must display the model details needed for compatibility and memory checks.

**Acceptance criteria:** For a verified Granite GGUF, the app displays format, architecture/name where available, file size, weight type, context information, tokenizer/chat-template information or explicit missing-data warnings.

**Verification:** Real-model inspection test and fixture tests  
**Evidence ID/path:** AC-F-M05 â€” `docs/evidence/requirements/F-M05/`

### F-M06 â€” Usability

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must show plain-English reasons for warnings or failures.

**Acceptance criteria:** Every tested failure code maps to a non-technical explanation and a valid recovery action; no raw exception is the only user message.

**Verification:** Failure-message tests and task-based usability test  
**Evidence ID/path:** AC-F-M06 â€” `docs/evidence/requirements/F-M06/`

### F-M07 â€” Functional

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must read the hardware information needed for fit analysis.

**Acceptance criteria:** The app records CPU details, installed and available RAM, available GPU/device information, disk space and runtime availability with clear units; values are cross-checked against trusted Windows tools.

**Verification:** Hardware service unit/integration test  
**Evidence ID/path:** AC-F-M07 â€” `docs/evidence/requirements/F-M07/`

### F-M08 â€” Functional

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must estimate peak memory for a supported configuration.

**Acceptance criteria:** The estimate shows weights, KV cache, runtime/app overhead, OS allowance and safety reserve; matched measured error is recorded.

**Verification:** Formula/unit tests and predicted-versus-measured calibration  
**Evidence ID/path:** AC-F-M08 â€” `docs/evidence/requirements/F-M08/`

### F-M09 â€” Functional

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must show whether a configuration is likely to fit, needs optimisation, or has no verified safe option.

**Acceptance criteria:** Boundary tests change the result at documented thresholds and show the limiting reason and uncertainty.

**Verification:** Boundary and UI decision tests  
**Evidence ID/path:** AC-F-M09 â€” `docs/evidence/requirements/F-M09/`

### F-M10 â€” Functional

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must generate only complete configurations valid for the current model, runtime and device.

**Acceptance criteria:** All approved combinations are generated; all known invalid model/weight/cache/runtime/backend/device combinations are rejected.

**Verification:** Decision-table and registry tests  
**Evidence ID/path:** AC-F-M10 â€” `docs/evidence/requirements/F-M10/`

### F-M11 â€” Functional

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must provide Automatic, Quality, Balanced and Efficiency modes when valid alternatives exist.

**Acceptance criteria:** Each visible mode resolves to a complete valid configuration with a documented ranking rationale; unavailable modes are hidden or disabled; slider label updates correctly.

**Verification:** Mode algorithm tests and UI interaction tests  
**Evidence ID/path:** AC-F-M11 â€” `docs/evidence/requirements/F-M11/`

### F-M12 â€” Functional

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must show the chosen complete configuration before starting an operation.

**Acceptance criteria:** Before launch the UI shows model, runtime, backend, actual/requested device target, weight format, KV cache, context, estimated memory, status and selection reason.

**Verification:** UI acceptance test  
**Evidence ID/path:** AC-F-M12 â€” `docs/evidence/requirements/F-M12/`

### F-M13 â€” Functional

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must run local chat with at least one supported Granite GGUF model.

**Acceptance criteria:** One real Granite GGUF completes import, inspection, fit, selection, loading and valid local generation through WinUI without manual terminal commands.

**Verification:** End-to-end application test  
**Evidence ID/path:** AC-F-M13 â€” `docs/evidence/requirements/F-M13/`

### F-M14 â€” Data Integrity

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must keep the original model file unchanged.

**Acceptance criteria:** SHA-256 before and after inspection/processing is identical; generated artefacts use a different path.

**Verification:** Hash integrity test  
**Evidence ID/path:** AC-F-M14 â€” `docs/evidence/requirements/F-M14/`

### F-M15 â€” Usability

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must give the user a clear next step after a failure.

**Acceptance criteria:** Each formal failure case offers a safe action such as Retry, Choose another model/configuration, Open details, or Return to standard route.

**Verification:** Failure/recovery test and usability task  
**Evidence ID/path:** AC-F-M15 â€” `docs/evidence/requirements/F-M15/`

### F-M16 â€” Functional

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must download at least one approved recommended Granite model from a fixed trusted source.

**Acceptance criteria:** A listed model shows source/licence/size/location, downloads with progress, and enters inspection after completion.

**Verification:** Download integration and end-to-end test  
**Evidence ID/path:** AC-F-M16 â€” `docs/evidence/requirements/F-M16/`

### F-M17 â€” Security / Integrity

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must prevent partial, corrupt or unverified downloads from being used as valid models.

**Acceptance criteria:** Disk space is checked; cancellation/failure leaves no valid-state artefact; final size and SHA-256 match the approved manifest.

**Verification:** Failure-injection, cancellation and hash tests  
**Evidence ID/path:** AC-F-M17 â€” `docs/evidence/requirements/F-M17/`

### F-M18 â€” Functional / Security

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The WinUI application must launch and control supported local command-line runtimes without requiring the user to enter terminal commands.

**Acceptance criteria:** Structured arguments, redirected stdout/stderr, hidden console, startup/error result, timeout and process-tree cleanup all pass tests.

**Verification:** Contract, path-safety and integration tests  
**Evidence ID/path:** AC-F-M18 â€” `docs/evidence/requirements/F-M18/`

### F-M19 â€” Functional / Reliability

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** Chat and long-running operations must stream progress/output and support safe cancellation.

**Acceptance criteria:** Output appears during generation; cancellation during loading/generation/download/processing returns a controlled state and leaves no child process or partial valid artefact.

**Verification:** Streaming, responsiveness, cancellation and cleanup tests  
**Evidence ID/path:** AC-F-M19 â€” `docs/evidence/requirements/F-M19/`

### F-M20 â€” Functional

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The local chat must support at least two user turns in the same session.

**Acceptance criteria:** Two sequential prompts complete; second prompt uses the intended session context; reset starts a new session.

**Verification:** Multi-turn end-to-end test  
**Evidence ID/path:** AC-F-M20 â€” `docs/evidence/requirements/F-M20/`

### F-M21 â€” Functional / Experimental

**Lifecycle:** Active  
**Release role:** App-integrated Experimental  
**Requirement:** The application must run at least one verified TurboQuant-enabled Granite configuration end to end.

**Acceptance criteria:** A pinned supported model/runtime/cache/device combination is selected in WinUI, loads, generates through normal chat, proves TQ activation, records actual state, and supports cancellation.

**Verification:** App-integrated TurboQuant E2E and activation audit  
**Evidence ID/path:** AC-F-M21 â€” `docs/evidence/requirements/F-M21/`

### F-M22 â€” Reliability

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** A dependable upstream llama.cpp configuration must remain available when the TurboQuant route is unavailable or fails.

**Acceptance criteria:** A forced TQ unavailability/failure returns the user to a verified upstream configuration without claiming TQ was active.

**Verification:** Forced-failure and recovery test  
**Evidence ID/path:** AC-F-M22 â€” `docs/evidence/requirements/F-M22/`

### F-M23 â€” Functional

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must create one new validated GGUF model artefact through a supported weight-quantisation workflow.

**Acceptance criteria:** A suitable source model is processed into a new GGUF, original hash is unchanged, output loads/inspects and can be selected for chat.

**Verification:** Processing integration, hash, reinspection and generation tests  
**Evidence ID/path:** AC-F-M23 â€” `docs/evidence/requirements/F-M23/`

### F-M24 â€” Traceability / Data

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** Each generated model artefact must have a processing manifest containing source/output hashes, tool/version, settings and result.

**Acceptance criteria:** Manifest is saved for success and classified failure and validates against the agreed schema.

**Verification:** Manifest schema/contract test and evidence audit  
**Evidence ID/path:** AC-F-M24 â€” `docs/evidence/requirements/F-M24/`

### F-M25 â€” Functional / Data

**Lifecycle:** Active  
**Release role:** App-integrated Experimental  
**Requirement:** The application must import and preserve at least one supported text-based knowledge file for the bounded retrieval workflow.

**Acceptance criteria:** A supported local file is validated, text is extracted/chunked, and the original file hash remains unchanged.

**Verification:** Fixture, extraction, chunking and hash tests  
**Evidence ID/path:** AC-F-M25 â€” `docs/evidence/requirements/F-M25/`

### F-M26 â€” Functional / AI

**Lifecycle:** Active  
**Release role:** App-integrated Experimental  
**Requirement:** The application must create an uncompressed embedding baseline and use the selected TurboVec implementation to compress or optimise the vectors.

**Acceptance criteria:** Pinned embedding/TurboVec versions produce both baseline and compressed vector artefacts with recorded dimensions, sizes, timings and hashes.

**Verification:** Adapter integration and artefact-manifest tests  
**Evidence ID/path:** AC-F-M26 â€” `docs/evidence/requirements/F-M26/`

### F-M27 â€” Functional / AI

**Lifecycle:** Active  
**Release role:** App-integrated Experimental  
**Requirement:** The application must retrieve relevant sections from the local index and provide them to the Granite chat workflow.

**Acceptance criteria:** For a fixed question set, retrieved chunks are shown/recorded and passed into Granite; answer and retrieval evidence are retained.

**Verification:** Retrieval and end-to-end RAG-style test  
**Evidence ID/path:** AC-F-M27 â€” `docs/evidence/requirements/F-M27/`

### F-M28 â€” Functional / Usability

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The user must be able to copy generated chat output.

**Acceptance criteria:** Copy action places the exact selected/full generated response on the Windows clipboard and handles empty output safely.

**Verification:** UI/clipboard test  
**Evidence ID/path:** AC-F-M28 â€” `docs/evidence/requirements/F-M28/`

### R-M01 â€” Research

**Lifecycle:** Active  
**Release role:** Research  
**Requirement:** The project must compare at least one verified TurboQuant run with a matched standard KV-cache baseline.

**Acceptance criteria:** Same model, weights, prompt/template, context, settings, hardware and measurement method; TQ activation proved; differences reported.

**Verification:** Controlled matched experiment  
**Evidence ID/path:** EXP-TQ-COMP-001 â€” `experiments/processed-results/EXP-TQ-COMP-001/`

### R-M02 â€” Research / Scope

**Lifecycle:** Active  
**Release role:** Research  
**Requirement:** The project must identify, pin and document the exact TurboVec implementation and its role in the release.

**Acceptance criteria:** Repository, commit/version, licence, platform, input/output, vector representation and retrieval contract are recorded before integration.

**Verification:** Source inspection, licence review and technical spike  
**Evidence ID/path:** DEC-TV-001 â€” `docs/architecture/decisions/ADR-TurboVec.md`

### R-M03 â€” Research

**Lifecycle:** Active  
**Release role:** Research  
**Requirement:** The project must complete and preserve an official OpenVINO GenAI Granite baseline or a reproducible blocker.

**Acceptance criteria:** Exact official-source model/revision, conversion/runtime versions, requested/actual device and outcome are recorded; pass or blocker is reproducible.

**Verification:** Official-source conversion/runtime experiment  
**Evidence ID/path:** EXP-OV-OFFICIAL-001 â€” `experiments/raw-results/EXP-OV-OFFICIAL-001/`

### R-M04 â€” Research

**Lifecycle:** Active  
**Release role:** Research  
**Requirement:** The project must measure memory use for every final test configuration.

**Acceptance criteria:** Each final row records available RAM before, peak process-tree working set/private where used, relevant GPU shared memory and measurement definition.

**Verification:** Metrics audit  
**Evidence ID/path:** MET-MEM â€” `experiments/processed-results/final-metrics/`

### R-M05 â€” Research

**Lifecycle:** Active  
**Release role:** Research  
**Requirement:** The project must evaluate runtime performance for every final test configuration.

**Acceptance criteria:** Cold/warm load, TTFT, prompt speed, generation speed and total response duration are reported where available using fixed definitions and repetitions.

**Verification:** Performance experiment audit  
**Evidence ID/path:** MET-PERF â€” `experiments/processed-results/final-metrics/`

### R-M06 â€” Research / AI Quality

**Lifecycle:** Active  
**Release role:** Research  
**Requirement:** The project must compare output quality with a fixed prompt set and scoring guide.

**Acceptance criteria:** Prompt/rubric versions are frozen before final comparison; raw answers, instruction/format/fact scores and limitations are retained.

**Verification:** Quality evaluation  
**Evidence ID/path:** QUAL-FINAL â€” `experiments/raw-results/quality/`

### R-M07 â€” Research

**Lifecycle:** Active  
**Release role:** Research  
**Requirement:** The project must identify the largest stable tested context for selected final configurations.

**Acceptance criteria:** A versioned step test records maximum attempted and maximum stable context, success/retrieval result, memory and failure reason; it is not called the model maximum.

**Verification:** Context stability experiment  
**Evidence ID/path:** CTX-FINAL â€” `experiments/processed-results/context/`

### R-M08 â€” Research / Evidence

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The project must preserve a complete record of every final experiment.

**Acceptance criteria:** Each formal experiment has an ID, hardware/model/runtime/config hashes, requested/actual state, command, raw stdout/stderr, measurements, outputs and failure status.

**Verification:** Evidence-manifest audit  
**Evidence ID/path:** EVID-AUDIT â€” `experiments/manifests/`

### R-M09 â€” Research / Evidence

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The project must record failed attempts and known limitations.

**Acceptance criteria:** Every formal failure has ID, stage, code, evidence, likely cause, next action and resolution status; limitations appear in final documentation.

**Verification:** Failure-register and report audit  
**Evidence ID/path:** FAIL-AUDIT â€” `docs/testing/Failure-Register.md`

### R-M10 â€” Research / Reproducibility

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** Another developer must be able to reproduce the clean build and core result from written instructions.

**Acceptance criteria:** Independent clean-checkout reproduction completes the documented x64 build, tests and main upstream route; deviations are recorded.

**Verification:** Independent reproduction test  
**Evidence ID/path:** REPRO-001 â€” `release-evidence/reproduction/`

### R-M11 â€” Research

**Lifecycle:** Active  
**Release role:** Research  
**Requirement:** The project must determine which selected Granite/runtime/backend combinations run reliably on the tested Intel CPU and integrated GPU.

**Acceptance criteria:** Matrix includes upstream llama.cpp, selected TQ forks, official OpenVINO and OpenVINO TQ with model, commit/version, build, requested/actual device, success/failure and role.

**Verification:** Cross-route evidence audit  
**Evidence ID/path:** COMPAT-FINAL â€” `experiments/processed-results/cross-route/`

### R-M12 â€” Research

**Lifecycle:** Active  
**Release role:** Research  
**Requirement:** The project must assess selected complete configurations against 4 GB, 8 GB and 16 GB total system-memory budgets.

**Acceptance criteria:** Each result is labelled physical, controlled-limit, calculated or predicted and includes OS/app/model/KV/runtime/shared-memory allowance and uncertainty.

**Verification:** Budget-analysis audit  
**Evidence ID/path:** MEM-BUDGET-001 â€” `experiments/processed-results/memory-budgets/`

### R-M13 â€” Research / AI Retrieval

**Lifecycle:** Active  
**Release role:** Research  
**Requirement:** The project must compare TurboVec-compressed or optimised vectors with an uncompressed-vector baseline.

**Acceptance criteria:** Same documents, chunking, embeddings and queries; compare storage, memory, index/query time, relevance, answer usefulness, failures and stability.

**Verification:** Matched retrieval experiment  
**Evidence ID/path:** EXP-TV-COMP-001 â€” `experiments/processed-results/EXP-TV-COMP-001/`

### R-M14 â€” Research / Estimation

**Lifecycle:** Active  
**Release role:** Research  
**Requirement:** The project must quantify memory-estimator error and false-safe/false-unsafe recommendations.

**Acceptance criteria:** Matched rows report predicted, measured, absolute error, percentage error, fit decision, false-safe/false-unsafe and confidence.

**Verification:** Estimator validation experiment  
**Evidence ID/path:** EST-VALID-001 â€” `experiments/processed-results/EST-VALID-001/`

### N-M01 â€” Quality / Offline

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The core workflow must work locally after required models and tools are installed.

**Acceptance criteria:** With network disconnected, model inspection, configuration selection and upstream local chat complete using installed assets.

**Verification:** Offline end-to-end test  
**Evidence ID/path:** AC-N-M01 â€” `docs/evidence/requirements/N-M01/`

### N-M02 â€” Privacy

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must not upload models, prompts, knowledge files or answers to a cloud AI service in the core workflow.

**Acceptance criteria:** Network inspection shows no cloud AI upload during core model/document/chat workflow; any initial approved download is separately documented.

**Verification:** Network/offline check and data-flow review  
**Evidence ID/path:** AC-N-M02 â€” `docs/evidence/requirements/N-M02/`

### N-M03 â€” Performance / UX

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application window must remain responsive during long tasks.

**Acceptance criteria:** UI input/paint remains responsive during each long task; no synchronous runtime call blocks the UI thread.

**Verification:** UI responsiveness test  
**Evidence ID/path:** AC-N-M03 â€” `docs/evidence/requirements/N-M03/`

### N-M04 â€” Usability

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must show the current state of a long task.

**Acceptance criteria:** Each long operation reports at least Starting, Running/Loading/Generating/Processing, Completed, Cancelled or Failed; progress is determinate where available.

**Verification:** State-transition and UI tests  
**Evidence ID/path:** AC-N-M04 â€” `docs/evidence/requirements/N-M04/`

### N-M05 â€” Security

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must handle model/document paths and process arguments safely.

**Acceptance criteria:** Tests cover spaces, Unicode, quotes and supported special characters; no shell string concatenation/injection path is used.

**Verification:** Path and argument security tests  
**Evidence ID/path:** AC-N-M05 â€” `docs/evidence/requirements/N-M05/`

### N-M06 â€” Reliability

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The application must clean temporary files and stopped child processes.

**Acceptance criteria:** Success, cancellation, timeout and crash tests leave no unintended child process and no partial artefact marked complete.

**Verification:** Cleanup/failure-injection tests  
**Evidence ID/path:** AC-N-M06 â€” `docs/evidence/requirements/N-M06/`

### N-M07 â€” Evidence / Reliability

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** Each final run must record requested and actual backend, device and optimisation state.

**Acceptance criteria:** Manifest separates requested from actual runtime/backend/device/cache and cites the evidence used to determine actual state.

**Verification:** Manifest audit  
**Evidence ID/path:** AC-N-M07 â€” `experiments/manifests/`

### N-M08 â€” Maintainability / Reproducibility

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** A clean copy of the repository must build and run its automated tests.

**Acceptance criteria:** Documented x64 restore/build/test commands pass from a clean checkout in the pinned environment.

**Verification:** Clean-build and CI test  
**Evidence ID/path:** AC-N-M08 â€” `release-evidence/clean-build/`

### N-M09 â€” Security / Data

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The repository must not contain secrets, personal test data or large proprietary model files.

**Acceptance criteria:** Release scan finds no credentials, real patient/pupil data, unapproved model weights or prohibited third-party files.

**Verification:** Repository scan  
**Evidence ID/path:** AC-N-M09 â€” `release-evidence/repository-scan/`

### N-M10 â€” Usability / Transparency

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** Experimental options must be clearly labelled in the interface.

**Acceptance criteria:** Every Experimental registry entry displays a persistent label and limitations before selection and launch.

**Verification:** Registry/UI inspection test and UX task  
**Evidence ID/path:** AC-N-M10 â€” `docs/evidence/requirements/N-M10/`

### N-M11 â€” Evidence / Reliability

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** An experimental option must not be reported as active unless activation is proved.

**Acceptance criteria:** When proof is absent the run is labelled Unverified/Fallback rather than active; supported routes have direct activation evidence.

**Verification:** Log/manifest audit and forced-unverified test  
**Evidence ID/path:** AC-N-M11 â€” `docs/evidence/requirements/N-M11/`

### N-M12 â€” Security / Architecture

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The main local inference route must not require a local web server or open network port.

**Acceptance criteria:** Main upstream route completes with no listening port created by the application/runtime; evidence is recorded.

**Verification:** Port/network inspection test  
**Evidence ID/path:** AC-N-M12 â€” `docs/evidence/requirements/N-M12/`

### N-M13 â€” Accessibility / Usability

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The main workflow must support keyboard operation and Windows text scaling.

**Acceptance criteria:** Core tasks complete keyboard-only; focus is visible; content remains usable at 200% Windows text scaling with no critical clipping.

**Verification:** Accessibility checklist and task test  
**Evidence ID/path:** AC-N-M13 â€” `docs/ux/accessibility/`

### N-M14 â€” Release / Maintainability

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The project must produce a basic distributable Windows x64 release build with documented dependencies.

**Acceptance criteria:** Release artefact launches on the target machine; checksum, commit/tag, runtime dependencies and install/run steps are recorded.

**Verification:** Release smoke and checksum test  
**Evidence ID/path:** AC-N-M14 â€” `release-evidence/`

### G-M01 â€” Governance

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The project problem, aim, research questions, objectives, first-release scope, exclusions and satisfactory outcome must be version-controlled.

**Acceptance criteria:** Dated document contains all agreed sections, source links and status; linked from repository README.

**Verification:** Document review and Git history  
**Evidence ID/path:** AC-G-M01 â€” `docs/planning/Project-Definition-v1.md`

### G-M02 â€” Governance / Requirements

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The project must maintain one versioned MoSCoW requirements baseline with stable IDs and measurable acceptance criteria.

**Acceptance criteria:** Every active Must has a stable ID, source, rationale, acceptance and verification; changes are dated and approved.

**Verification:** Requirements quality review  
**Evidence ID/path:** AC-G-M02 â€” `docs/requirements/MoSCoW-Requirements-v1.2.md`

### G-M03 â€” Governance / Traceability

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The project must maintain bidirectional traceability from requirements to objectives/RQs, work packages, implementation, tests and evidence.

**Acceptance criteria:** No active Must row lacks source, rationale, acceptance, WP, component, verification, evidence path, owner and status; release audit confirms reverse links.

**Verification:** RTM completeness formula and manual audit  
**Evidence ID/path:** AC-G-M03 â€” `docs/requirements/Requirements-Traceability-Matrix.md`

### G-M04 â€” Architecture / Governance

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The project must record proportionate architecture views, key ADRs, stable contracts, states and diagnostic codes.

**Acceptance criteria:** Context/component/process/deployment views and ADRs for WinUI, adapters/no port, upstream/TQ/OpenVINO/TurboVec/evidence agree with code and requirements.

**Verification:** Architecture consistency review  
**Evidence ID/path:** AC-G-M04 â€” `docs/architecture/`

### G-M05 â€” Risk / Governance

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The project must maintain a consolidated risk, assumption, constraint and licence register.

**Acceptance criteria:** Each high risk has probability, impact, owner, trigger, validation, mitigation, contingency and status; assumptions are confirmed/rejected with evidence; licences are reviewed.

**Verification:** Register audit  
**Evidence ID/path:** AC-G-M05 â€” `docs/risks/`

### G-M06 â€” Governance / Change Control

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** Requirement and scope changes must be linked to dated decisions, GitHub issues, pull requests/commits and affected tests.

**Acceptance criteria:** Every approved change records reason, date, affected IDs/WPs/tests/report sections and superseded/replacement links.

**Verification:** Change-log/PR audit  
**Evidence ID/path:** AC-G-M06 â€” `docs/planning/Change-Log.md`

### G-M07 â€” Evidence / Governance

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** Irreplaceable raw evidence from completed feasibility campaigns must be recovered, hashed, indexed and backed up.

**Acceptance criteria:** Upstream, AtomicBot, animehacker and OpenVINO campaigns each have model/runtime/hash manifest, raw-output location, checksum and independent backup or explicit gap.

**Verification:** Evidence recovery audit  
**Evidence ID/path:** AC-G-M07 â€” `docs/evidence/Evidence-Recovery-Index.md`

### G-M08 â€” Documentation

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The release must include a README, user manual, developer/build guide, known limitations and final feature-status table.

**Acceptance criteria:** Documents cover install/run/use/build/test/evidence/limitations; feature statuses match the RTM.

**Verification:** Documentation and walkthrough review  
**Evidence ID/path:** AC-G-M08 â€” `docs/manuals/`

### G-M09 â€” Release / Reporting

**Lifecycle:** Active  
**Release role:** Core  
**Requirement:** The final release must be tied to a Git tag, checksums, evidence pack, independent backup and evidence-based answers to every RQ.

**Acceptance criteria:** Tag/commit/checksums/backups exist; all Must rows have final status/evidence; report answers each RQ and states limitations/negative results.

**Verification:** Final release gate audit  
**Evidence ID/path:** AC-G-M09 â€” `release-evidence/`

## Should (18)

### F-S01 â€” Functional

**Lifecycle:** Active  
**Release role:** Should  
**Requirement:** The application should support drag-and-drop model import.

**Acceptance criteria:** Supported file drop follows the same validation path; invalid/multiple drops are handled.

**Verification:** UI drag/drop tests  
**Evidence ID/path:** AC-F-S01 â€” `docs/evidence/requirements/F-S01/`

### F-S02 â€” Functional

**Lifecycle:** Active  
**Release role:** Should  
**Requirement:** The application should recognise complete OpenVINO IR model folders.

**Acceptance criteria:** Valid XML/BIN/tokenizer/config folder is recognised and incomplete folder receives a classified result.

**Verification:** Folder fixture matrix  
**Evidence ID/path:** AC-F-S02 â€” `docs/evidence/requirements/F-S02/`

### F-S03 â€” Functional

**Lifecycle:** Active  
**Release role:** Should  
**Requirement:** The application should recognise selected Hugging Face/Safetensors model folders.

**Acceptance criteria:** Selected known folder structures are classified without being falsely treated as directly runnable GGUF.

**Verification:** Folder fixture tests  
**Evidence ID/path:** AC-F-S03 â€” `docs/evidence/requirements/F-S03/`

### F-S04 â€” Functional

**Lifecycle:** Active  
**Release role:** Should  
**Requirement:** The user should be able to change the requested context length before configuration selection.

**Acceptance criteria:** Only supported range/steps are allowed and estimator/configuration update immediately.

**Verification:** Boundary and UI tests  
**Evidence ID/path:** AC-F-S04 â€” `docs/evidence/requirements/F-S04/`

### F-S08 â€” Functional

**Lifecycle:** Active  
**Release role:** Should  
**Requirement:** The user should be able to copy or save the technical inspection report.

**Acceptance criteria:** Report includes model/hardware/result/assumptions without secrets or sensitive prompts.

**Verification:** Export content test  
**Evidence ID/path:** AC-F-S08 â€” `docs/evidence/requirements/F-S08/`

### F-S11 â€” Functional

**Lifecycle:** Active  
**Release role:** Should  
**Requirement:** The application should provide one official OpenVINO GenAI inference route after its integration gate passes.

**Acceptance criteria:** Pinned supported official-source configuration launches from WinUI, records actual device and generates or is explicitly deferred after gate review.

**Verification:** App integration E2E test  
**Evidence ID/path:** AC-F-S11 â€” `docs/evidence/requirements/F-S11/`

### F-S12 â€” Functional

**Lifecycle:** Active  
**Release role:** Should  
**Requirement:** The application should export benchmark results as CSV or JSON.

**Acceptance criteria:** Export validates against schema and contains units, evidence classification and requested/actual state.

**Verification:** Schema/export test  
**Evidence ID/path:** AC-F-S12 â€” `docs/evidence/requirements/F-S12/`

### F-S15 â€” Functional / Research

**Lifecycle:** Active  
**Release role:** Should  
**Requirement:** The project should provide one verified source-to-OpenVINO model-preparation route.

**Acceptance criteria:** Pinned IBM source revision converts in a clean recorded environment and the output passes structure/runtime checks, or a reproducible blocker is preserved.

**Verification:** Conversion and runtime test  
**Evidence ID/path:** AC-F-S15 â€” `experiments/raw-results/openvino-conversion/`

### N-S02 â€” Release

**Lifecycle:** Active  
**Release role:** Should  
**Requirement:** The project should provide a simple packaged installation route such as MSIX.

**Acceptance criteria:** Fresh install/uninstall works on the target Windows machine and dependencies/limitations are documented.

**Verification:** Installation test  
**Evidence ID/path:** AC-N-S02 â€” `release-evidence/installer/`

### F-S05 â€” Historical

**Lifecycle:** Superseded  
**Release role:** Superseded  
**Requirement:** The user should be able to cancel a long task.

**Acceptance criteria:** Not recorded.

**Verification:** Superseded by a stronger active requirement.  
**Evidence ID/path:** Change-control audit â€” `No active path`

### F-S06 â€” Historical

**Lifecycle:** Superseded  
**Release role:** Superseded  
**Requirement:** Chat answers should appear while they are being generated.

**Acceptance criteria:** Not recorded.

**Verification:** Superseded by a stronger active requirement.  
**Evidence ID/path:** Change-control audit â€” `No active path`

### F-S07 â€” Historical

**Lifecycle:** Superseded  
**Release role:** Superseded  
**Requirement:** The chat should support more than one turn in the same session.

**Acceptance criteria:** Not recorded.

**Verification:** Superseded by a stronger active requirement.  
**Evidence ID/path:** Change-control audit â€” `No active path`

### F-S09 â€” Historical

**Lifecycle:** Superseded  
**Release role:** Superseded  
**Requirement:** The application should create a new GGUF file through a supported weight-quantisation step.

**Acceptance criteria:** Not recorded.

**Verification:** Superseded by a stronger active requirement.  
**Evidence ID/path:** Change-control audit â€” `No active path`

### F-S10 â€” Historical

**Lifecycle:** Superseded  
**Release role:** Superseded  
**Requirement:** The application should save a processing record for each new model file.

**Acceptance criteria:** Not recorded.

**Verification:** Superseded by a stronger active requirement.  
**Evidence ID/path:** Change-control audit â€” `No active path`

### F-S13 â€” Historical

**Lifecycle:** Superseded  
**Release role:** Superseded  
**Requirement:** The main local inference route should not need a local web server or network port.

**Acceptance criteria:** Not recorded.

**Verification:** Superseded by a stronger active requirement.  
**Evidence ID/path:** Change-control audit â€” `No active path`

### F-S14 â€” Historical

**Lifecycle:** Superseded  
**Release role:** Superseded  
**Requirement:** The application should expose an AtomicBot TurboQuant route only after its gate passes.

**Acceptance criteria:** Not recorded.

**Verification:** Superseded by a stronger active requirement.  
**Evidence ID/path:** Change-control audit â€” `No active path`

### N-S01 â€” Historical

**Lifecycle:** Superseded  
**Release role:** Superseded  
**Requirement:** The main workflow should support keyboard use and Windows text scaling.

**Acceptance criteria:** Not recorded.

**Verification:** Superseded by a stronger active requirement.  
**Evidence ID/path:** Change-control audit â€” `No active path`

### R-S01 â€” Historical

**Lifecycle:** Superseded  
**Release role:** Superseded  
**Requirement:** The project should run a small TurboVec feasibility test if the scope decision supports it.

**Acceptance criteria:** Not recorded.

**Verification:** Superseded by a stronger active requirement.  
**Evidence ID/path:** Change-control audit â€” `No active path`

## Could (10)

### C-01 â€” Historical

**Lifecycle:** Superseded  
**Release role:** Superseded  
**Requirement:** The application could help the user find or download suitable models.

**Acceptance criteria:** Not recorded.

**Verification:** Superseded by a stronger active requirement.  
**Evidence ID/path:** Change-control audit â€” `No active path`

### C-02 â€” Functional / Future

**Lifecycle:** Active  
**Release role:** Could  
**Requirement:** The application could keep a local benchmark history.

**Acceptance criteria:** When approved, completed benchmark summaries can be stored, listed and removed locally without storing sensitive prompt or document content.

**Verification:** Repository and privacy tests  
**Evidence ID/path:** Scope review â€” `docs/evidence/requirements/C-02/`

### C-03 â€” Functional / Future

**Lifecycle:** Active  
**Release role:** Could  
**Requirement:** The application could show charts for memory and speed trade-offs.

**Acceptance criteria:** When approved, charts label units and distinguish measured, estimated and externally reported values.

**Verification:** Chart and data-classification test  
**Evidence ID/path:** Scope review â€” `docs/evidence/requirements/C-03/`

### C-04 â€” Functional / Future

**Lifecycle:** Deferred  
**Release role:** Deferred  
**Requirement:** The application could support selected non-Granite GGUF models.

**Acceptance criteria:** Not recorded.

**Verification:** Only implemented through approved scope change after Must/Should stability.  
**Evidence ID/path:** Scope review â€” `No active path`

### C-05 â€” Functional / Future

**Lifecycle:** Deferred  
**Release role:** Deferred  
**Requirement:** The project could explore DirectML as another Windows route.

**Acceptance criteria:** Not recorded.

**Verification:** Only implemented through approved scope change after Must/Should stability.  
**Evidence ID/path:** Scope review â€” `No active path`

### C-06 â€” Functional / Future

**Lifecycle:** Deferred  
**Release role:** Deferred  
**Requirement:** The project could test an Intel NPU when suitable hardware is available.

**Acceptance criteria:** Not recorded.

**Verification:** Only implemented through approved scope change after Must/Should stability.  
**Evidence ID/path:** Scope review â€” `No active path`

### C-07 â€” Functional / Future

**Lifecycle:** Deferred  
**Release role:** Deferred  
**Requirement:** The project could explore macOS and Metal after the Windows release.

**Acceptance criteria:** Not recorded.

**Verification:** Only implemented through approved scope change after Must/Should stability.  
**Evidence ID/path:** Scope review â€” `No active path`

### C-08 â€” Functional / Future

**Lifecycle:** Deferred  
**Release role:** Deferred  
**Requirement:** The project could explore a cross-platform CMake backend.

**Acceptance criteria:** Not recorded.

**Verification:** Only implemented through approved scope change after Must/Should stability.  
**Evidence ID/path:** Scope review â€” `No active path`

### C-09 â€” Functional / Future

**Lifecycle:** Deferred  
**Release role:** Deferred  
**Requirement:** The project could try a model near 32 billion parameters on stronger hardware.

**Acceptance criteria:** Not recorded.

**Verification:** Only implemented through approved scope change after Must/Should stability.  
**Evidence ID/path:** Scope review â€” `No active path`

### C-10 â€” Functional / Future

**Lifecycle:** Deferred  
**Release role:** Deferred  
**Requirement:** The application could support more than one model session at a time.

**Acceptance criteria:** Not recorded.

**Verification:** Only implemented through approved scope change after Must/Should stability.  
**Evidence ID/path:** Scope review â€” `No active path`

## Won't (18)

### W-01 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** The project will not train or fine-tune a Granite model.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

### W-02 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** The first release will not support every GGUF, OpenVINO or Hugging Face model.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

### W-03 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** The project will not guarantee that a 32B model runs on the target laptop.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

### W-04 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** The project will not guarantee that every model runs on a 4 GB, 8 GB or 16 GB computer.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

### W-05 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** The project will not claim six-times compression, near-zero quality loss or sub-second latency without its own evidence.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

### W-06 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** The core workflow will not depend on cloud inference.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

### W-07 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** The project will not use real patient data or provide medical advice.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

### W-08 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** The interface will not show an unverified experimental option as a normal working option.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

### W-09 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** The project will not build a full replacement for llama.cpp, OpenVINO, LM Studio or Ollama.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

### W-10 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** Full cross-platform delivery is not required for this release.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

### W-11 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** The application will not automatically re-quantise an already quantised GGUF through an unsafe route.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

### W-12 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** A runtime-only TurboQuant setting will not be presented as a new exportable model file.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

### W-13 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** TurboVec will not be described as directly compressing the original knowledge file.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

### W-14 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** The application will not provide unrestricted model downloads from arbitrary or untrusted sources.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

### W-15 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** The first release will not be a full enterprise RAG, document-management or vector-database platform.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

### W-16 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** The prototype will not claim operational clinical, NHS, school or classroom approval.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

### W-17 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** The first release will not provide multi-user server operation or multiple simultaneous model sessions.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

### W-18 â€” Exclusion

**Lifecycle:** Excluded  
**Release role:** Out of Scope  
**Requirement:** The first release will not automatically update runtimes/models or collect prompts/documents for analytics.

**Acceptance criteria:** Not recorded.

**Verification:** Release review confirms no implementation or claim contradicts this boundary.  
**Evidence ID/path:** Scope and claims audit â€” `No active path`

