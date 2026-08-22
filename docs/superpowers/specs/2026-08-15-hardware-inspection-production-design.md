# Hardware Inspection Production Design

**Status:** Approved architecture; written specification awaiting user review

**Date:** 2026-08-15

**Branch:** `feature/hardware-inspection`
**Authoritative source:** `Granite_Edge_AI_Approved_Hardware_Inspection_Architecture_Block_2.docx`

## 1. Purpose and authority

This specification converts the approved Block 2 architecture into an executable production design for the current Granite Edge AI repository. The DOCX remains authoritative. This specification fixes repository placement, delivery sequencing, integration boundaries, verification evidence, and the UI decisions approved during the Hardware Inspection prototype review.

If an implementation discovery would change any of the following, work must stop and the change must be recorded in an ADR before implementation continues:

- LLM Fit's role as the primary hardware collector.
- Application ownership of `HardwareSnapshot` and provenance.
- The seven user-visible progress stages.
- Evidence authority, tolerance, freshness, or resolution semantics.
- The offline, no-dashboard, no-listening-port route.
- Cancellation, ordering, or stale-run guarantees.
- The `HardwareInspectionHandoff` eligibility rules.

## 2. Product boundary

Hardware Inspection answers:

- What processor, memory, graphics, storage, operating-system, NPU, and local runtime capabilities are available?
- Which source established each fact?
- How trustworthy, current, and internally consistent is each fact?
- Was inspection completed, completed with warnings, cancelled, or prevented by an operational/integrity failure?

Hardware Inspection does not calculate model memory, KV cache, runtime overhead, safety reserves, context size, quantisation, GPU offload, performance, quality, or model suitability. Those decisions belong to Model-Hardware Compatibility in Block 3. UI language must therefore describe hardware facts and inspection trust, not claim that a model will run.

## 3. Entry and exit contracts

### 3.1 Entry

Only Model Inspection outcomes `Ready` and `ReadyWithWarnings` may enable **Check hardware fit**. Selecting it navigates to Hardware Inspection and starts exactly one run without an artificial start delay.

The Model Inspection page passes an immutable request containing only the model-inspection identity and data needed to correlate the later journey. Hardware providers never receive model content or use model characteristics to alter hardware collection.

### 3.2 Exit

Terminal statuses are:

- `Completed`
- `CompletedWithWarnings`
- `Failed`
- `Cancelled`

Only `Completed` and `CompletedWithWarnings`, with a usable canonical snapshot, create `HardwareInspectionHandoff` and enable **Continue to compatibility**. `Failed` and `Cancelled` retain privacy-safe diagnostics but never create an actionable handoff.

## 4. Architectural boundaries

The dependency direction is:

```text
Presentation -> Application -> Domain
Infrastructure --------^---------^
```

- Domain contains immutable canonical models and pure policies. It references no WinUI, JSON DTO, native API, process, DXGI, LLM Fit, or llama.cpp implementation.
- Application owns provider interfaces, orchestration, validation coordination, progress contracts, outcomes, and handoffs.
- Infrastructure implements Application contracts and contains every third-party DTO, native structure, executable command, and parser.
- Presentation calls only `IHardwareInspectionService`. It never starts processes, parses provider output, invokes native APIs, or resolves evidence.
- Composition remains explicit/manual until the cross-cutting DI decision in Block 5. This feature must not introduce a competing application-wide container.

Production code is grouped under dedicated Hardware Inspection namespaces and projects. Existing Model Inspection protocols and fixture schemas are not extended for Hardware Inspection.

## 5. Canonical data model

`HardwareSnapshot` is immutable and contains:

- snapshot identity, capture time, schema version, and policy version;
- processor information and topology;
- the canonical shared memory snapshot;
- graphics subsystems and adapters;
- neural-processor evidence;
- storage facts;
- operating-system facts;
- pinned llama.cpp build, backend, and visible-device capabilities;
- a `HardwareEvidenceManifest` linking every resolved field to its candidate evidence, resolution state, source, timestamp, confidence, and diagnostics.

Required semantic distinctions are structural, not display-only:

- Installed, OS-usable, and currently available RAM are separate values.
- Available RAM carries a capture timestamp and can be refreshed later.
- Dedicated video memory and shared system memory are separate values and are never summed or relabelled.
- `NotPresent` and `DetectionUnavailable` are separate NPU states.
- Runtime build support, visible devices, and model-execution proof are separate concepts.

No third-party DTO, raw native structure, command-line string, raw path, or raw process output reaches Domain or Presentation.

## 6. Collection and evidence flow

The collection pipeline is fixed:

```text
Trusted tool verification
        |
Parallel bounded collection
  - LLM Fit primary hardware evidence
  - Windows processor/memory/OS corroboration
  - DXGI graphics corroboration/enrichment
  - NPU presence/unavailability evidence
  - Storage evidence
  - llama.cpp build/backend/device capabilities
        |
CollectedHardwareEvidence
        |
Validation -> consistency comparison -> authority/tolerance/freshness policies
        |
HardwareEvidenceResolver
        |
HardwareSnapshotNormalizer
        |
Outcome policy -> HardwareInspectionResult -> optional handoff
```

LLM Fit owns the primary end-to-end observation. Windows and DXGI may corroborate, enrich missing Intel-specific data, or provide an explicitly approved field-level fallback; they do not become a second compatibility engine.

Consistency checkers report agreement or disagreement but do not select winners. Only `HardwareEvidenceResolver` may convert competing observations into resolved evidence. Resolution states include primary accepted, primary accepted with conflict, approved secondary fallback, unavailable, and unresolved.

## 7. Source authority and freshness

- Overall collection route: LLM Fit.
- CPU identity/topology and installed RAM: valid LLM Fit evidence preferred; Windows corroborates and may provide an approved fallback.
- Currently available RAM: fresh Windows system-memory snapshot.
- Graphics identity and memory: valid LLM Fit evidence preferred; DXGI is the Windows Intel authority for missing or inaccurate fields.
- NPU: the approved Windows NPU probe only. It is never inferred from unrelated behavior.
- OS and storage: Windows providers.
- llama.cpp capabilities: the pinned runtime's own version/device reports.
- Tool identity: the trusted application manifest plus validated reported build/version identity.

Each comparison uses normalized units and meanings. A value outside its approved tolerance is a conflict, not an average. Stale dynamic evidence cannot be silently treated as current.

## 8. Trusted external-tool boundary

Every external tool follows this sequence before evidence is accepted:

1. Read the trusted pinned manifest.
2. Resolve the canonical approved executable path.
3. prove that the file exists and remains inside the approved package root.
4. Reject traversal, reparse-point, symlink, or canonical-path escape.
5. Verify SHA-256.
6. Verify PE architecture.
7. Verify required package dependencies.
8. Launch only through `IExternalProcessRunner` with fixed or allowlisted arguments.
9. Bound stdout and stderr, enforce timeout/cancellation, and terminate the complete process tree.
10. Validate reported build/version identity before accepting evidence.

The implementation must not use a shell, accept arbitrary executable paths or command fragments, start an LLM Fit dashboard/server, require a listening port, or require network access. A missing, moved, modified, wrong-architecture, timed-out, or unverifiable executable is an operational/integrity result; it is never reported as absent hardware.

Existing hardened WorkerClient process-containment code is a security reference, not a reason to couple Hardware Inspection to the Model Inspection protocol. Reuse requires a deliberately extracted shared boundary with preserved tests; copying a weaker `Process.Start` route is prohibited.

## 9. Orchestration, progress, and cancellation

One run has one `InspectionId`, one linked cancellation source, and monotonically increasing progress sequence numbers. Independent safe probes may overlap, while external-process concurrency remains bounded. Failure of an optional probe does not cancel unrelated work.

The seven user-visible stages are exactly:

1. Starting hardware inspection.
2. Reading processor information.
3. Reading system memory.
4. Detecting graphics hardware.
5. Checking local inference runtimes.
6. Normalising hardware information.
7. Creating the hardware report.

The underlying inspection begins immediately. Presentation serializes visible stage activation and keeps each newly active stage visible for at least 500 ms so state changes remain perceptible. Fast provider events are queued rather than rendered simultaneously; the completed terminal view appears after that short visual queue drains. This pacing never delays collection or process cancellation, and a user cancellation replaces the queue immediately. When no genuine fraction exists, progress is indeterminate and copy uses truthful counts such as “3 of 7 checks complete”; fabricated percentages are prohibited.

Cancellation is terminal and distinct from failure. It propagates through active providers and process execution, kills child process trees, and prevents later progress or completion from changing the result. A retry creates a new run identity. The ViewModel ignores mismatched run IDs, non-increasing sequence numbers, messages after a terminal state, and messages received after the page/run becomes inactive. Navigating away requests cancellation.

## 10. Outcome policy and recovery

- `Completed`: required processor/RAM facts and provenance are trustworthy and all important checks succeeded.
- `CompletedWithWarnings`: the required foundation is trustworthy, but an approved fallback, non-critical conflict, or optional unavailable probe exists.
- `Failed`: required CPU/RAM evidence remains unresolved, or security/orchestration failure prevents a trustworthy snapshot.
- `Cancelled`: the user cancelled the run.

Genuine absence of a GPU or NPU is a hardware fact, not a collection failure. Inability to determine whether an NPU exists is `DetectionUnavailable`, not `NotPresent`.

Recovery copy is derived from stable diagnostic categories:

- Tool integrity or installation cannot be verified: ask the organisation's IT support to repair/reinstall the approved package, then provide the safe support code and affected component from Technical information.
- A retryable provider/runtime problem: offer **Run inspection again** and retain safe diagnostics.
- A non-critical unavailable check with a trustworthy foundation: explain the warning and allow continuation.
- Cancellation: explain that no complete report or compatibility decision was produced and allow retry.

Normal UI never exposes raw paths, full command lines, raw stdout/stderr, prompt/model data, or stack traces. Expanded Technical information may show stable codes, component names, versions, timestamps, field-level provenance, safe diagnostics, and conflict descriptions intended for support staff.

## 11. Presentation design

The approved Hardware Inspection prototype defines the visual and interaction baseline. Production WinUI uses the same typography, spacing, cards, state language, centered vector status symbols, and responsive hierarchy as the selected design, while sharing high-level product tokens with Model Inspection so the journey feels coherent.

Required interaction and layout behavior:

- Auto-start progress view with one active-stage spinner and completed/warning/failure symbols rendered as single-coordinate-system vector icons.
- A minimum 44 px target for every action and disclosure control.
- **Run inspection again** and **Continue to compatibility** centered in their action area.
- Status labels vertically centered with their associated content.
- Outcome symbols vertically centered in their cards.
- The provenance card keeps comfortable internal padding and uses progressive disclosure.
- **Inspection details** expands into a polished, plain-language stage report with seven stage records.
- Each stage record includes status, what was checked, result, evidence/source, timestamp/version where relevant, and a safe diagnostic when applicable.
- **Technical information for IT** is a nested disclosure containing provider/build facts and support codes without exposing sensitive paths or raw output.
- Disclosures use native WinUI semantics, preserve focus, expose accessible expanded/collapsed states, and completely remove collapsed content from layout and accessibility traversal.
- Buttons are real controls. Preview-only behavior is not carried into production; production actions invoke typed ViewModel commands.
- Keyboard access, visible focus, accessible names, screen-reader-understandable progress, non-colour state cues, high contrast, 200% text scaling, and reduced motion are acceptance requirements.

The presentation uses plain language suitable for healthcare and education workers. Technical terminology is allowed when paired with an understandable explanation and placed primarily inside the expanded technical details.

## 12. Repository and parallel-work strategy

Hardware Inspection is developed on `feature/hardware-inspection` in `C:\hardware-inspection`. The active Model Inspection worktree remains untouched.

New projects and folders use Hardware-specific names and tests. Until the parallel Model Inspection work is integrated, Hardware work avoids modifying these high-collision files:

- `Features/ModelInspection/**`
- Model Inspection fixture schemas/catalogues
- `OnboardingShellPage.xaml.cs`
- `OnboardingStageIndicator.xaml(.cs)`
- the main app project file
- the packaged WinUI unit-test project file

Gates 1–7 can be built and tested behind independent contracts without those integration edits. Gate 8 adds the Hardware WinUI feature and its own fixtures. The final onboarding/app-project seam is integrated from the then-current Model Inspection baseline, preserving both histories and resolving conflicts deliberately.

## 13. Test architecture

All deterministic production behavior is developed test-first.

- Pure unit tests cover authority, tolerance, freshness, validators, consistency, resolution, normalization, outcomes, stages, progress ordering, cancellation, stale events, and handoff eligibility.
- Contract tests cover pinned LLM Fit JSON, additive/missing/malformed fields, command construction, exit codes, timeouts, cancellation, output caps, identity mismatch, dashboard-disabled operation, and offline behavior.
- Security tests cover path containment, traversal, reparse/symlink escape, checksum, PE architecture, dependencies, version mismatch, output truncation, timeout, and process-tree cleanup.
- Provider tests cover memory semantics, refreshed available RAM, DXGI adapter varieties, dedicated/shared memory separation, NPU absent/unavailable distinctions, storage facts, and llama.cpp build/backend/device states.
- Orchestrator tests prove primary collection, bounded concurrency, optional isolation, cancellation propagation, fallback, unresolved critical evidence, manifest conflicts, normalization ordering, and single outcome-policy application.
- ViewModel/UI tests prove one auto-start, duplicate-start rejection, cancellation, new-identity retry, stale-event rejection, four terminal views, continuation eligibility, seven stages, disclosures, actions, accessibility, high contrast, text scaling, and reduced motion.
- Windows integration acceptance records side-by-side LLM Fit/Windows/DXGI/llama.cpp evidence on the approved Intel target.
- Offline acceptance proves no network dependency, dashboard, or listening port and proves complete child-process cleanup.

Each gate records its exact commands, counts, failures, environment, tool versions, and privacy-safe evidence. A gate is not complete merely because code compiles.

## 14. Nine implementation gates

### Gate 1 — Windows Intel LLM Fit spike

Prove a candidate-pinned local JSON CLI route on the target machine: command, version/build identity, CPU/RAM values, Windows Intel GPU/NPU gaps, offline operation, bounded output, and no dashboard/server. The spike selects the final exact LLM Fit version only if the evidence passes. It does not create production hardware facts from unverified output.

### Gate 2 — Shared resources and process-security foundations

Implement canonical memory/OS snapshots and the trusted manifest, path, hash, PE, dependency, process-runner, timeout, cancellation, output-bound, and process-tree-cleanup contracts. Exit with deterministic security and memory-semantics tests.

### Gate 3 — LLM Fit provider and parser

Implement the command builder, infrastructure-only DTO, tolerant parser, validation mapping, and `LlmFitHardwareEvidence`. Exit with fixtures for every required success and failure mode plus mapping of the accepted Gate 1 capture.

### Gate 4 — Windows and DXGI enrichment

Implement Windows processor/memory/OS, DXGI graphics, storage, and the provisional `INeuralProcessorProbe` boundary. Exit with Intel graphics/memory corroboration and explicit NPU absent/unavailable behavior. The exact NPU enumeration mechanism remains a separately approved spike.

### Gate 5 — llama.cpp capability subsystem

Integrate the pinned version/device capability route without loading native runtime code into the app or test host. Exit with build, backend, visible-device, unavailability, timeout, and identity evidence.

### Gate 6 — Evidence policies, resolver, and normalizer

Implement component validators, consistency checks, field-authority/tolerance/freshness policies, resolution, canonical `HardwareSnapshot`, and provenance manifest. Exit with decision-table tests proving that conflicts and missing critical facts cannot produce false-safe snapshots.

### Gate 7 — Orchestrator, outcomes, cancellation, and progress

Implement the complete collection lifecycle, bounded overlap, seven stages, unique run identity, ordering, cancellation, stale-run filtering, partial diagnostics, outcome policy, and handoff. Exit with all four terminal outcomes and lifecycle tests.

### Gate 8 — WinUI presentation and journey integration

Implement the ViewModel, immutable presentation state, page, reusable cards, fixtures, auto-start, retry/cancel/continue, expanded details, responsive behavior, and accessibility. Integrate the Model Inspection handoff and onboarding route only against the current merged Model Inspection baseline.

### Gate 9 — Windows integration and evidence package

Run end-to-end acceptance on the supported Windows Intel target. Complete offline/no-port/security evidence, requirements traceability, ADRs, READMEs, and the Block 2 Definition of Done. Unresolved approved spikes remain explicitly documented; they are never represented as supported capability.

## 15. Deliberately deferred decisions

These are not implementation gaps to fill by assumption:

- Exact Windows NPU enumeration: resolve through `HardwareInspection.NpuSpike`; retain `INeuralProcessorProbe` meanwhile.
- Exact LLM Fit version/commit: select only after Gate 1 evidence.
- Exact final llama.cpp package/backend build: select against the verified Intel execution route.
- Application-wide DI, logging library, and persisted diagnostic format: Block 5.
- Model memory formulas, reserves, ranking, GGUF resource profiling, and GPUStack role: Block 3.
- Final visual polish beyond the approved functional state system: after functional architecture is stable.

## 16. Completion criteria

Block 2 is complete only when all nine gates pass and the DOCX Definition of Done is traceable to executable tests or reviewed evidence. In particular:

- successful Model Inspection is the only entry route;
- navigation auto-starts exactly one run;
- LLM Fit remains primary and runs without dashboard/server;
- Windows Intel enrichment does not become a competing compatibility engine;
- all external tools are verified before execution;
- all facts are application-owned, resolved deterministically, and traceable;
- memory, graphics, NPU, and runtime semantics remain distinct and truthful;
- progress, cancellation, retry, stale-event behavior, and four outcomes pass;
- only actionable outcomes create a handoff;
- no model-fit recommendation is emitted;
- offline, no-port, privacy, integrity, accessibility, and documentation acceptance pass.
