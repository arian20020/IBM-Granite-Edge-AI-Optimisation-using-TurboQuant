# Model Inspection Test Completeness Matrix

| Metadata | Value |
|---|---|
| Document ID | `TEST-COV-MODEL-INSPECTION-001` |
| Status | Read-only audit complete; gap-closing implementation not started |
| Audit date | 2026-08-08 |
| Audited commit | `8f00a64a40e916872abfd6b72721ef86f223bab9` |
| Audit branch | `test/model-inspection-completeness-gate` |
| Stacked base | `refactor/model-inspection-cleanup` |
| Related roadmap | [Model Inspection completion roadmap](../superpowers/specs/2026-08-08-model-inspection-completion-roadmap-design.md) |
| Specialist runtime matrix | [LLamaSharp runtime coverage matrix](./LLamaSharp-Runtime-Test-Coverage-Matrix.md) |

## Purpose

This matrix traces every behavior cluster identified in the current Model
Inspection implementation to its strongest useful verification evidence. It
separates missing tests from missing implementation and prevents a test suite,
design document or historical green workflow from being treated as proof of a
behavior it does not execute.

The audit is read-only. No build, test or coverage command was run to produce
these findings because those commands write build output. Existing execution
results are labelled as historical evidence until the gap-closing branch has
fresh exact-head verification.

The matrix applies the Project Testing Standard rather than creating a second
generic testing method.

## Classification rules

| Classification | Meaning |
|---|---|
| `IMPLEMENTED - TEST NOW` | Production behavior exists and belongs in the immediate completeness gate |
| `PARTIALLY IMPLEMENTED` | Only the existing boundary is tested now; unfinished behavior remains explicit |
| `NOT YET IMPLEMENTED` | Future product requirement; do not build it during the completeness gate |
| `DEFERRED / OUT OF CURRENT GATE` | A current or environmental concern with a recorded later evidence route |
| `SUPERSEDED` | An older design decision replaced by a named later authority |

Coverage meanings:

| Coverage | Meaning |
|---|---|
| `Adequate` | Existing direct evidence is proportionate to the current behavior and realistic failures |
| `Partial` | Some direct evidence exists, but a material boundary, branch or assertion is absent |
| `None` | No test directly executes the behavior cluster |
| `Deferred` | Evidence is intentionally assigned to a later controlled gate |

Matrix IDs are audit identifiers. They do not create product requirement IDs.

## Audit summary

### Implementation classification

| Classification | Rows |
|---|---:|
| `IMPLEMENTED - TEST NOW` | 50 |
| `PARTIALLY IMPLEMENTED` | 6 |
| `NOT YET IMPLEMENTED` | 4 |
| `DEFERRED / OUT OF CURRENT GATE` | 2 |
| `SUPERSEDED` | 1 |
| **Total** | **63** |

### Evidence for implemented and partially implemented rows

| Coverage | Rows |
|---|---:|
| `Adequate` | 18 |
| `Partial` | 29 |
| `None` | 9 |
| **Current behavior total** | **56** |

### Protocol field audit

| Audited surface | Direct | Partial | None | Total |
|---|---:|---:|---:|---:|
| Serialized public properties | 24 | 31 | 41 | 96 |
| Public enum members | 11 | 4 | 4 | 19 |
| JSON behavior rules | 8 | 9 | 5 | 22 |

Nineteen test/assertion patterns warrant strengthening because they can remain
green after a meaningful regression. They are listed separately from coverage
gaps below.

## Source key

| Source | Meaning |
|---|---|
| `WF-IMP-001` | controlled import/select workflow |
| `WF-INS-001` | controlled inspection-begins workflow |
| `WF-OVR-*` | controlled Ready, ConversionRequired, Unsupported and Invalid/incomplete overview workflows |
| `WF-DIAG-SCR-001` | controlled technical-details workflow |
| `TS` | Project Testing Standard |
| `ADR-003/G2` | protected-worker ADR and protected-worker Gate 2 design/evidence |
| `LLAMA-MATRIX` | specialist LLamaSharp matrix and retained Tier 1/Tier 2 evidence |
| `CI/EVIDENCE` | repository workflow, evidence and traceability requirements |

## Matrix A - import, quick scan and request creation

| ID | Source | Behavior and owner | Inputs, boundaries and realistic failures | Correct layer and existing evidence | Coverage | Implementation class | Required action |
|---|---|---|---|---|---|---|---|
| MI-TC-001 | `WF-IMP-001` | File picker/import routing and GGUF allowlist; Model Import | supported/unsupported extension, picker cancel, route mismatch | packaged WinUI; `ModelQuickScannerTests`, import composition tests | Adequate | IMPLEMENTED - TEST NOW | Preserve existing regression coverage |
| MI-TC-002 | `WF-IMP-001`, `TS` | Asynchronous quick-scan state, cancellation, retry and stale completion; import state machine | repeat selection, old completion after new run, cancellation race, failure recovery | packaged/domain tests; `ModelImportPageStateMachineTests`, `ImportModelCardTests` | Adequate | IMPLEMENTED - TEST NOW | Preserve run-identity and cancellation assertions |
| MI-TC-003 | `WF-IMP-001`, `TS` | GGUF header recognition; `GgufQuickScanner` | empty, short magic, bad magic, unsupported version, truncated header | packaged scanner/fixture tests; `GgufQuickScannerTests`, `GgufFixtureIntegrityTests` | Adequate | IMPLEMENTED - TEST NOW | Preserve deterministic malformed-fixture matrix |
| MI-TC-004 | `WF-IMP-001`, `TS` | GGUF metadata decoding and bounded parsing; `GgufQuickScanner` | every official type, order, duplicates, invalid UTF-8, excessive count/string/array/depth/key bytes, cancellation | packaged scanner tests and generated fixtures | Adequate | IMPLEMENTED - TEST NOW | Preserve explicit parser bounds and fixture manifest |
| MI-TC-005 | `WF-IMP-001` | Scanner routing, result invariants and fallback display mapping | recognised GGUF, non-GGUF, unavailable optional metadata, invalid result combinations | packaged tests; `ModelQuickScannerTests`, `ModelQuickScanResultTests` | Adequate | IMPLEMENTED - TEST NOW | Preserve exact success/failure result mapping |
| MI-TC-006 | `WF-IMP-001`, `TS` | Scanner filesystem boundary | directory, locked/inaccessible, read-only, spaces, Unicode, unusual name, long path | request-factory coverage handles several failures; scanner boundary lacks direct path-class tests | Partial | IMPLEMENTED - TEST NOW | Add directory, lock, read-only, spaces/Unicode and feasible long-path scanner tests |
| MI-TC-007 | `WF-IMP-001`, `TS` | Immutable request creation and handoff revalidation; `ModelInspectionRequestFactory` | missing/directory/non-GGUF/locked/changed file, length/time identity mismatch, stale scan | packaged tests; `ModelInspectionRequestFactoryTests` | Adequate | IMPLEMENTED - TEST NOW | Preserve exact identity and fail-closed revalidation |
| MI-TC-008 | `WF-IMP-001`, `TS` | Source no-write guarantee and path-minimised diagnostics | read-only source, before/after identity, exception/path leakage | no-write behavior is indirect; diagnostics do not exercise all filesystem failures | Partial | IMPLEMENTED - TEST NOW | Add read-only/integrity assertions and full-path-negative diagnostics |

## Matrix B - navigation, presentation and accessibility

| ID | Source | Behavior and owner | Inputs, boundaries and realistic failures | Correct layer and existing evidence | Coverage | Implementation class | Required action |
|---|---|---|---|---|---|---|---|
| MI-TC-009 | `WF-IMP-001`, `WF-INS-001` | Import -> onboarding -> inspection preserves the exact request instance | event forwarding, reconstruction with equal values, replacement request | packaged navigation tests assert values; a direct helper test asserts identity but bypasses the event chain | Partial | IMPLEMENTED - TEST NOW | Assert reference identity through the real event/navigation pipeline |
| MI-TC-010 | `WF-INS-001`, `TS` | Shell subscription replacement/detach and failed navigation handling | page replacement, unload, repeated attach, `Frame.Navigate` returns false | no direct test | None | IMPLEMENTED - TEST NOW | Add lifecycle and controlled navigation-failure tests |
| MI-TC-011 | `WF-INS-001` | `ModelInspectionPage` navigation parameter contract | valid exact request, null, wrong type | valid request covered; wrong/missing parameter absent | Partial | IMPLEMENTED - TEST NOW | Add actual navigation tests for null and wrong parameter |
| MI-TC-012 | `WF-INS-001` | Actual page `Loaded` composition | filename without path, hidden outcome, compact model, five stages, inspecting action, visible disabled Cancel | helper/factory tests do not execute `ModelInspectionPage_Loaded` | None | IMPLEMENTED - TEST NOW | Add packaged UI-thread Loaded composition test |
| MI-TC-013 | `WF-INS-001`, `TS` | Repeated Loaded, re-entry and navigate-away/back lifecycle | duplicate Loaded, new request after navigation, stale visual state | no direct test | None | IMPLEMENTED - TEST NOW | Prove one application per navigation and correct reset for a new request |
| MI-TC-014 | `WF-INS-001` | Initial five-stage presentation factory | exact order/names, active first stage, pending remainder, stage count | packaged factory tests | Adequate | IMPLEMENTED - TEST NOW | Preserve semantic stage assertions |
| MI-TC-015 | `WF-INS-001`, `WF-OVR-*` | Content template selection | every mode/status, item vs wrapper, `ContentControl`, `ContentPresenter`, null, missing template, invalid enum | warning route and selected wrapper paths covered | Partial | IMPLEMENTED - TEST NOW | Add table-driven selector coverage and controlled invalid configuration failure |
| MI-TC-016 | `WF-INS-001`, `WF-OVR-*` | Hidden outcome independence and required visual-state guards | repeated hidden use, shared mutable state, missing visual state | direct packaged tests | Adequate | IMPLEMENTED - TEST NOW | Preserve immutable hidden state and guard tests |
| MI-TC-017 | `WF-INS-001`, `WF-OVR-*` | Model/content card modes, badges, icons, status maps and bindings | every implemented enum value, binding refresh, disclosure state | production mappings exist; only selected presentation paths execute | Partial | PARTIALLY IMPLEMENTED | Test every currently implemented mapping; retain runtime-fed state as future |
| MI-TC-018 | `WF-OVR-*` | Outcome/action tones, disclosure, buttons, enabled and visible state | every model/execution presentation, hidden/disabled independence, invalid combination | selected models and guards covered | Partial | PARTIALLY IMPLEMENTED | Add pure presentation and packaged control matrix for existing modes |
| MI-TC-019 | `WF-OVR-READY-001` | Hardware Fit eligibility contract | Ready, ReadyWithWarnings, ConversionRequired, Unsupported, IncompletePackage, Invalid, Cancelled, OperationalFailure | eligibility is represented in current contracts/presentation but no live classifier exists | Partial | PARTIALLY IMPLEMENTED | Lock current Ready/ReadyWithWarnings rule; defer navigation execution |
| MI-TC-020 | `WF-INS-001`, `WF-OVR-*`, `TS` | Accessible names, semantic status text and non-color communication | every card/action/outcome, error/recovery state, icon/brush changes | static AutomationName properties and text exist; complete rendered-state evidence absent | Partial | PARTIALLY IMPLEMENTED | Add packaged semantic assertions and manual high-contrast/non-color review |
| MI-TC-021 | `WF-INS-001`, `TS` | Observable stage live-region announcement | stage change, repeated value, peer creation, actual `LiveRegionChanged` event | existing test inspects peer/name but not the event | Partial | IMPLEMENTED - TEST NOW | Observe and assert the automation event on Windows |
| MI-TC-022 | `WF-INS-001`, `WF-OVR-*`, `TS` | Keyboard, focus, tab order, 200% text scaling, resize and reduced-motion-safe behavior | keyboard-only use, visible focus, long names, expanded details, small window, high contrast | no retained automated/manual execution evidence | None | PARTIALLY IMPLEMENTED | Add feasible packaged checks and a retained manual Windows acceptance record |
| MI-TC-023 | `WF-INS-001` | Application request/progress/result/evidence invariants and enums | null/default/invalid combinations, immutability, execution/model outcome separation | packaged `ModelInspectionContractTests` and execution-result tests | Adequate | IMPLEMENTED - TEST NOW | Preserve constructor/validation and exact outcome meaning |
| MI-TC-024 | `WF-OVR-*`, `WF-DIAG-SCR-001` | Application result-to-presentation and privacy-safe operational failure mapping | every outcome, finding severity, safe details, absent runtime data | several presentation types exist; production mapper/classifier/service do not | Partial | PARTIALLY IMPLEMENTED | Test existing maps only; leave runtime mapping to production Gate 5 |

## Matrix C - shared contracts and protocol

| ID | Source | Behavior and owner | Inputs, boundaries and realistic failures | Correct layer and existing evidence | Coverage | Implementation class | Required action |
|---|---|---|---|---|---|---|---|
| MI-TC-025 | `ADR-003/G2`, `TS` | Start, cancel, hello, started, progress and completed validation | invalid IDs/version/kinds/PID/timestamps/profile/architecture/stage/count/fraction/status/terminal branch | `WorkerProtocolTests.CompletedStatus_RequiresEvidence`, `CompletedStatus_ForbidsOperationalFailure`, `CancelledStatus_ForbidsEvidence`, `CancelledStatus_ForbidsOperationalFailure`, `OperationalFailureStatus_RequiresFailure`, and `OperationalFailureStatus_ForbidsEvidence` isolate all six terminal predicates; three status-positive tests also pass; fresh task-worktree runs based on `8016231bdda87c3440eb2bd19a104ae6c55ebde7`: focused 64/64 and full Contracts 129/129, zero skipped | Partial | IMPLEMENTED - TEST NOW | Add the remaining field-specific hello/start/progress validator matrix; preserve the now-independent terminal predicates |
| MI-TC-026 | `ADR-003/G2`, `TS` | Nested evidence validation and cross-field invariants | null section/list/item/dictionary, invalid runtime/file/observation/failure, contradictory integrity | `WorkerEvidenceValidationTests` directly mutates every runtime/model-file/observation/failure guard, every composed null boundary, all three preservation snapshots, and the null tokenizer dictionary at composed and dispatcher layers; fresh task-worktree runs based on `8016231bdda87c3440eb2bd19a104ae6c55ebde7`: focused 64/64 and full Contracts 129/129, zero skipped | Adequate | IMPLEMENTED - TEST NOW | Preserve the direct mutation suite and do not infer count-range, special-token, or SHA-format policy |
| MI-TC-027 | `ADR-003/G2`, `TS` | Strict JSON document rules | invalid UTF-8, comments, trailing commas, root shape, recursive duplicates, max depth, exact/over size | 8 of 22 audited rules direct; depth and exact-size boundary absent | Partial | IMPLEMENTED - TEST NOW | Add missing root/depth/duplicate/boundary cases |
| MI-TC-028 | `ADR-003/G2`, `TS` | Exact wire fields, casing, enum representation, round trips and additive compatibility | all 96 properties, 19 enum members, ordinary property case, missing/null/type invalid values, nested additive fields | only 24 properties direct; cancel is the only strong full-record round trip | Partial | IMPLEMENTED - TEST NOW | Add full supported-record round trips and field/enum behavior matrix without silent requiredness change |
| MI-TC-029 | `ADR-003/G2` | Command sequence state machine | cancel-before-start, second start, mismatched/repeated cancel, post-terminal command | all nine core command-sequence methods direct | Adequate | IMPLEMENTED - TEST NOW | Add only null/invalid input and state-after-rejection characterization if needed |
| MI-TC-030 | `ADR-003/G2` | Message sequence state machine | hello/started/progress/terminal order, wrong request ID, decreasing stage/count, duplicate terminal, invalid status | core ordering direct; wrong IDs for progress/completed and several status branches absent | Partial | IMPLEMENTED - TEST NOW | Add missing request/status/state-preservation cases and correct misleading test name |
| MI-TC-031 | `ADR-003/G2`, `CI/EVIDENCE` | Dependency, source, package and build-policy fitness including Contracts | forbidden third-party/native/WinUI/runtime dependency, Contracts -> Transport edge, package asset leak | blacklist and approved-subset tests omit material Contracts boundaries | Partial | IMPLEMENTED - TEST NOW | Replace finite blacklist confidence with complete allowlist/graph checks including Contracts |
| MI-TC-032 | `CI/EVIDENCE` | Cleanup inventory uniqueness, roots and allowed review dispositions | missing current file, LLama sibling roots, duplicate row, bare invalid `reviewed` status | path/source equality tests pass historically; status vocabulary is not checked | Partial | IMPLEMENTED - TEST NOW | Discover every current root and enforce the approved final disposition set |

## Matrix D - bounded transport

| ID | Source | Behavior and owner | Inputs, boundaries and realistic failures | Correct layer and existing evidence | Coverage | Implementation class | Required action |
|---|---|---|---|---|---|---|---|
| MI-TC-033 | `ADR-003/G2`, `TS` | Reader LF framing, UTF-8, fragmentation, EOF and byte limits | clean EOF, empty, CR/CRLF, BOM, invalid/split multibyte UTF-8, one-byte reads, exact max, one over, unterminated EOF | `BoundedUtf8LineTests` now includes `ReadLineAsyncReassemblesOneByteReadsIncludingSplitMultibyteUtf8`, complete exact-limit byte comparison, `ReadLineAsyncRejectsExactLimitPayloadAtEofAsUnexpectedEndOfStream`, and `ReadLineAsyncUsesFixedSizeReadAheadBuffer`; fresh task-worktree runs based on `192c16d27ba0f0d10ff0055d5bbd3d613cedff13`: bounded slice and full Transport both 27/27, zero skipped | Adequate | IMPLEMENTED - TEST NOW | Preserve strict framing, complete-byte, one-byte fragmentation, and fixed 4096-byte read-ahead assertions |
| MI-TC-034 | `ADR-003/G2`, `TS` | Writer framing, exact limit, stream ownership and disposal | empty/BOM/CR/CRLF/invalid UTF-8/oversize, exact max, disposed use, caller stream after disposal | `WriteLineAsyncAcceptsPayloadAtExactLimit`, `WriteLineAsyncObservesPreCancellationWithoutWriting`, and `DisposeLeavesCallerOwnedStreamUsable` directly cover the missing boundaries; full Transport 27/27 on the task working tree based on `192c16d27ba0f0d10ff0055d5bbd3d613cedff13`, zero skipped | Adequate | IMPLEMENTED - TEST NOW | Preserve inclusive byte limit, zero-output pre-cancellation, disposed-writer rejection, and successful caller-stream use |
| MI-TC-035 | `ADR-003/G2`, `TS` | Buffered-reader and queued-writer cancellation | pre-cancel with read-ahead frame, blocked refill, cancellation while queued, following frame continues | `ReadLineAsyncObservesPreCanceledTokenAtBufferedFrameBoundary` reproduced RED while both writer cases passed; after the one-line reader fix the focused defect set is 3/3 and `WriteLineAsyncCancellationWhileQueuedDoesNotWriteAndAllowsFollowingFrame` proves zero cancelled bytes plus an intact following frame; full Transport 27/27, zero skipped | Adequate | IMPLEMENTED - TEST NOW | Preserve cancellation before buffered consumption and cancellation-aware semaphore waiting |
| MI-TC-036 | `ADR-003/G2`, `TS` | Concurrent frame plus flush transaction and bounded memory | mutable queued payload, concurrent frames, flush before gate release, many queued maximum frames | `WriteLineAsyncSerializesFlushWithinEachFrameTransaction` records payload/LF/flush order and `WriteLineAsyncPreservesValidatedQueuedPayload` preserves the pre-wait snapshot; full Transport 27/27, zero skipped; aggregate queued allocation remains unbounded and unmeasured | Partial | IMPLEMENTED - TEST NOW | Preserve frame/flush serialization and the pre-wait snapshot; require separate approval before adding pending-byte reservation or aggregate resource policy |

## Matrix E - worker host, WorkerClient and real processes

| ID | Source | Behavior and owner | Inputs, boundaries and realistic failures | Correct layer and existing evidence | Coverage | Implementation class | Required action |
|---|---|---|---|---|---|---|---|
| MI-TC-037 | `ADR-003/G2` | Worker host controlled unavailable path, malformed start, matching cancel and one-terminal race | valid start/unavailable engine, malformed first command, matching cancel, competing terminals | `WorkerHostTests` retains direct unavailable/malformed/matching-cancel cases and now races completion, cooperative cancellation, and parent loss through `TerminalCoordinatorAllowsOnlyOneWinner` for 100 barrier-synchronized rounds; focused and full Worker task-worktree runs based on `f20131aec0f9728e4aec62f1e70cbdfd0e31d5f0`: 10/10, zero skipped | Adequate | IMPLEMENTED - TEST NOW | Preserve fixed failure paths, cooperative cancellation, and the atomic one-terminal gate |
| MI-TC-038 | `ADR-003/G2`, `TS` | Host success/progress, engine exception, EOF, mismatched cancel, external cancellation and parent loss | completed engine result, progress ordering, thrown exception, stdin closure, wrong request, monitor signal | `CompletedEngineWritesProgressBeforeOneCompletedTerminal`, `EngineExceptionMapsToFixedControlledFailureWithoutDetailLeak`, `MismatchedCancelWritesProtocolFailureAndNoTerminal`, `EndOfCommandStreamCancelsEngineAndWritesCancelledTerminal`, `ParentLossCancelsEngineAndWritesCancelledTerminal`, and `ExternalCancellationWritesFixedOperationalError` execute every listed branch with controllable engine/input/monitor seams; focused and full Worker task-worktree runs: 10/10, zero skipped | Adequate | IMPLEMENTED - TEST NOW | Preserve progress-before-terminal ordering, fixed privacy-safe failures, cancellation draining, and one terminal or no terminal as specified |
| MI-TC-039 | `ADR-003/G2`, `TS` | Production worker `Program` and apphost real-process smoke | publish exact worker, hello/start, controlled unavailable terminal, exit, cleanup | `ProductionWorkerReturnsTruthfulUnavailableEngineFailure` launches the published production apphost through `InspectionWorkerClient`, validates the fixed unavailable-engine terminal and exit `1`, requires empty valid untruncated stderr with no model-path/file/request sentinel, and proves zero surviving production processes; focused 1/1 and full process 30/30 task-worktree runs, zero skipped | Adequate | IMPLEMENTED - TEST NOW | Preserve the production apphost launch, trusted terminal/exit/privacy cross-check, immutable publish-tree checks, and zero-process assertion |
| MI-TC-040 | `ADR-003/G2` | Executable resolver, publish layout, PE x64 and reparse protection | missing/wrong name, outside root, reparse, wrong architecture, local/published layout | strong WorkerClient unit tests | Adequate | IMPLEMENTED - TEST NOW | Preserve fail-closed resolver boundary |
| MI-TC-041 | `ADR-003/G2`, `TS` | Environment, stdio, stderr, privacy and handle inheritance | secret/path parent variables, unexpected handle, stderr flood/truncation, pipe ownership | strong WorkerClient and process tests | Adequate | IMPLEMENTED - TEST NOW | Preserve allowlist, bounded drain and exact inherited handles |
| MI-TC-042 | `ADR-003/G2` | Handshake, sequence, exit consistency and failure accumulation | hello identity mismatches, progress/terminal ordering, exit mismatch, multiple failures | strong unit/process coverage for executed modes | Adequate | IMPLEMENTED - TEST NOW | Preserve first-failure and exit-consistency behavior |
| MI-TC-043 | `ADR-003/G2`, `TS` | Cooperative/forced cancellation, timeout, Job containment and child cleanup | cancellation before/after start, ignored cancel, root with live child, waiting child, forced kill | `OverallTimeoutStartsAfterStartNotDuringDelayedHandshake` uses a controlled 700 ms hello delay, 250 ms active budget, and fixture-owned 100 ms post-Start gate; the exact old handshake-arbitrated mutation failed with exit `1`, and an early-created timer ignored until active execution failed with exit `2`; corrected code passed with cooperative exit `3`, and the full process run passed 30/30 | Adequate | IMPLEMENTED - TEST NOW | Preserve independent startup/inspection/grace clocks, both deterministic clock-origin mutation guards, and current process-tree assertions |
| MI-TC-044 | `ADR-003/G2`, `TS` | Malformed identity, JSON, UTF-8 and line bounds through a real process | wrong protocol/PID/profile/architecture, text before hello, invalid UTF-8, BOM, oversized/malformed/duplicate JSON | fixture modes exist; most are tested only by parser-name coverage or lower layers | Partial | IMPLEMENTED - TEST NOW | Execute every mode through WorkerClient and assert failure plus cleanup |
| MI-TC-045 | `ADR-003/G2`, `TS` | Crash, hang and output-flood behavior at lifecycle phases | crash/hang before hello, after hello, after start; flood stdout | selected no-hello/flood-stderr/timeout paths execute; six lifecycle modes and stdout flood do not | Partial | IMPLEMENTED - TEST NOW | Add table-driven real-process crash/hang/flood campaign |
| MI-TC-046 | `ADR-003/G2`, `CI/EVIDENCE` | Concurrent sessions, repeat stability and final orphan verification | cross-session interference, repeated timing races, production/fixture/descendant survivors | 30 discovered test cases with 10 timing-sensitive executions retained in the five-run workflow campaign; the focused workflow has an unconditional final check for both production worker and fixture names, while the process tests retain descendant/Job cleanup assertions; local full process run 30/30, zero skipped | Adequate | IMPLEMENTED - TEST NOW | Preserve repeat stability, hidden-file-inclusive immutable publish inputs, descendant containment, and the unconditional two-process-name final check |

## Matrix F - LLamaSharp feasibility

| ID | Source | Behavior and owner | Inputs, boundaries and realistic failures | Correct layer and existing evidence | Coverage | Implementation class | Required action |
|---|---|---|---|---|---|---|---|
| MI-TC-047 | `LLAMA-MATRIX` | Dependency identity, CLI/path/runtime and package policy | spaces/Unicode quoting, pinned package/native identity, CPU-only, forbidden runtime leak | deterministic Tier 1 coverage | Adequate | IMPLEMENTED - TEST NOW | Preserve specialist rows and raise deterministic CI floor |
| MI-TC-048 | `LLAMA-MATRIX` | File integrity, metadata, tokenizer/template, progress, failure, redaction and atomic JSON | missing/directory/read-only/changed file, malformed metadata, cancellation, serialization failure, temp cleanup | extensive deterministic Tier 1 coverage | Adequate | IMPLEMENTED - TEST NOW | Preserve coverage during later extraction; avoid lexical-only reliance where behavior can execute |
| MI-TC-049 | `LLAMA-MATRIX` | Contained native smoke, missing backend and invalid runtime | valid CPU closure, missing/incorrect native library, contained process exit | four hosted native tests | Adequate | IMPLEMENTED - TEST NOW | Preserve contained native gate and exact binary identity |
| MI-TC-050 | `LLAMA-MATRIX`, `TS` | Controlled Granite success, repetition, cancellation, hostile input, privacy and offline behavior | exact hash/length/model, three runs, both cancellation scopes, hostile file/path/evidence, socket observation | 20 trusted tests and retained local evidence; no fresh evidence at audit head | Adequate | IMPLEMENTED - TEST NOW | Re-run trusted suite at exact closure head and retain model-integrity/no-port evidence |
| MI-TC-051 | `LLAMA-MATRIX`, `TS` | Link aliases and destructive/environmental campaigns | symlink/junction/hardlink alias, disconnected machine, x86 fixture, disk full, power loss, hard memory limit | explicitly recorded specialist deferrals | Deferred | DEFERRED / OUT OF CURRENT GATE | Follow the per-campaign owner/destination/evidence routes in roadmap section 3.1 |

## Matrix G - CI, privacy and retained evidence

| ID | Source | Behavior and owner | Inputs, boundaries and realistic failures | Correct layer and existing evidence | Coverage | Implementation class | Required action |
|---|---|---|---|---|---|---|---|
| MI-TC-052 | `CI/EVIDENCE`, `TS` | Every executable test project is discovered, reachable and protected by a meaningful floor/trigger | project omitted from solution/workflow, filter loses class, dependency change does not trigger, zero tests | permanent workflow covers nine executable projects through separate commands but several floors/triggers/solution entries are absent | Partial | IMPLEMENTED - TEST NOW | Add complete project register, exact workflow assertions and current floors including LLama 170 |
| MI-TC-053 | `CI/EVIDENCE`, `TS` | Privacy scan gates every retained artifact | scan failure followed by `always()` upload, path text in TRX/evidence, non-GGUF model copy, test failure still uploads | permanent and LLama scans exist but upload conditions and scan scope are incomplete | Partial | IMPLEMENTED - TEST NOW | Gate upload on scan and intended test outcomes; scan text paths and file identity/size/hash patterns |
| MI-TC-054 | `CI/EVIDENCE`, `ADR-003/G2` | Always-run orphan checks cover production worker, fixture and descendants | prior step failure, fixture-only scan, native helper/child survives | permanent and focused process workflows now always check both production worker and fixture; process tests retain descendant Job-empty assertions, while dedicated LLama native-helper workflow checks remain incomplete | Partial | IMPLEMENTED - TEST NOW | Add exact always-run orphan verification to relevant native workflows and preserve descendant Job evidence |
| MI-TC-055 | `CI/EVIDENCE`, `TS` | Line/branch coverage and selective mutation evidence | unexecuted branch hidden by green test count, weak assertion survives mutation | no coverage collector/config/report exists | None | IMPLEMENTED - TEST NOW | Add coverage collection and matrix mapping; mutation-test highest-risk invariants without arbitrary percentage target |
| MI-TC-056 | `CI/EVIDENCE`, `TS` | Exact-head raw artifacts, test totals, skips, digests and reproducibility | historical source SHA confused with final head, badge without TRX parsing, real-model evidence stale | exact-head permanent run exists; evidence documents split `401259...` source and `8f00a64...` closure; trusted run historical | Partial | IMPLEMENTED - TEST NOW | Produce one exact final-head evidence record and independently parse retained artifacts |
| MI-TC-057 | `CI/EVIDENCE` | Documentation, ledger, source and commit traceability agree | stale count/status/runner/gate claim, invalid ledger disposition, missing root, historical design read as current | cleanup inventory and evidence exist but contain identified contradictions | Partial | IMPLEMENTED - TEST NOW | Correct current docs, add supersession notes and preserve historical records |

## Matrix H - future, downstream and superseded work

| ID | Source | Behavior and owner | Inputs, boundaries and realistic failures | Correct layer and existing evidence | Coverage | Implementation class | Required action |
|---|---|---|---|---|---|---|---|
| MI-TC-058 | `WF-INS-001`, roadmap production Gates 5–6 | Async application service/ViewModel runtime, real progress and functional Cancel | run identity, auto-start, progress, cooperative/forced cancel, stale callback | interfaces/design only; current Cancel deliberately disabled | Deferred | NOT YET IMPLEMENTED | Preserve as production Gates 5 and 6 work; do not implement in the test-completeness gate |
| MI-TC-059 | `WF-OVR-*`, `WF-DIAG-SCR-001` | Live classifier, runtime-driven outcome routes, technical details and handoff actions | six model outcomes, two execution outcomes, precedence, recovery, route eligibility | presentation shells/contracts exist; producer and live workflow absent | Deferred | NOT YET IMPLEMENTED | Implement through production Gates 5-6 after protected runtime/package gates |
| MI-TC-060 | `ADR-003/G2`, roadmap production Gate 3 | Production LLamaSharp worker engine and application mappers | exact CPU runtime, native lifetime, factual evidence, no duplicate implementation | feasibility implementation only; production worker uses unavailable engine | Deferred | NOT YET IMPLEMENTED | Extract once and connect through production Gate 3; mapper follows in production Gate 5 |
| MI-TC-061 | downstream programme boundary | OpenVINO/Hugging Face conversion/optimisation/TurboQuant execution | backend/package/model routes and performance evidence | not part of current Model Inspection production boundary | Deferred | NOT YET IMPLEMENTED | Keep downstream; Model Inspection owns only extension/handoff contracts |
| MI-TC-062 | `ADR-003/G2` | In-process LLamaSharp production probe | native abort would terminate WinUI process | older July design only | Deferred | SUPERSEDED | Protected worker architecture in ADR-003 and protected-worker Gate 2 is authoritative |
| MI-TC-063 | `LLAMA-MATRIX`, downstream evaluation | Full CPU inference, Vulkan/GPU, TurboQuant performance and destructive environment campaigns | generation, quality, memory/performance, device/power/disconnection | outside lightweight inspection and current evidence boundary | Deferred | DEFERRED / OUT OF CURRENT GATE | Follow the named CPU, GPU/Vulkan, TurboQuant and destructive-reliability routes in roadmap section 3.1 |

## Ranked audit findings

### Critical

No critical defect was proven during the static audit.

### Important

1. Two shared evidence validators accept states that contradict their public
   contracts.
2. Resolved in Task 6: the process project now publishes and launches the
   production worker, validates its truthful protected-worker Gate 2 terminal,
   and proves process cleanup.
3. Eighteen implemented fixture scenarios are never exercised through the real
   process boundary.
4. Actual `ModelInspectionPage.Loaded` composition has no direct packaged test.
5. Shared evidence validators and most serialized fields have weak or no direct
   coverage.
6. Buffered transport pre-cancellation may be ignored when a complete frame is
   already read ahead.
7. Current accessibility evidence does not prove live announcements, keyboard,
   focus, text scaling, high contrast or screen-reader use.
8. CI project discovery, floors and dependency triggers are incomplete.
9. Artifact upload conditions can retain evidence after a privacy scan or test
   failure.
10. Resolved in Task 6: focused process orphan verification now runs
    unconditionally and covers both the production worker and protocol fixture.
11. No line/branch coverage or mutation evidence exists.
12. Trusted controlled-model evidence must be refreshed at the exact closure
    head.

### Minor

1. The end-to-end navigation test name implies exact request identity while its
   event-chain assertions compare values.
2. Subscription/detach and failed navigation behavior are untested.
3. Several implemented selector/card mapping branches are untested.
4. Quick-scan filesystem name/path classes are incomplete.
5. The Model Inspection explanatory copy says `runtime is support` rather than
   `runtime is supported`.
6. Several current READMEs and evidence summaries describe older project/test
   boundaries.

## Test-quality findings

These 19 patterns are not automatically deleted. Each must be strengthened or
explicitly retained with its limited evidence meaning.

| ID | Current pattern | False-confidence risk | Required strengthening |
|---|---|---|---|
| MI-QT-001 | Event-chain navigation test compares request values | reconstruction can pass | assert `AreSame` through the real event pipeline |
| MI-QT-002 | Direct shell navigation identity test bypasses event subscription | broken wiring can pass | execute import event -> shell handler -> Frame navigation |
| MI-QT-003 | Stage indicator test inspects automation peer/name | no live event need fire | observe `LiveRegionChanged` |
| MI-QT-004 | Backend-leak presentation test scans only selected title text | another rendered field can leak | inspect all relevant rendered text/properties |
| MI-QT-005 | Application graph/runtime-identity tests inspect selected types | a new unsafe edge/type can escape | traverse complete approved graph/surface |
| MI-QT-006 | Packaged CI requires only three quick-scanner classes | Model Inspection UI tests can disappear | require all priority classes or a verified complete project floor |
| MI-QT-007 | Resolved: completion exclusivity tests previously failed on their first guard | `WorkerProtocolTests` now isolates all six required/forbidden branches with unrelated predicates valid | preserve the six focused negative tests plus `CompletedStatus_WithEvidenceOnly_PassesValidation`, `CancelledStatus_WithoutTerminalData_PassesValidation`, and `OperationalFailureStatus_WithFailureOnly_PassesValidation`; fresh task-worktree runs based on `8016231bdda87c3440eb2bd19a104ae6c55ebde7`: focused 64/64 and full Contracts 129/129, zero skipped |
| MI-QT-008 | Start-command JSON test asserts only `ModelPath` | other fields can be dropped/corrupted | full-record equality and exact JSON members |
| MI-QT-009 | JSON casing test inspects only version and discriminator | ordinary field casing can drift | assert every supported property name through round trips |
| MI-QT-010 | Additive compatibility test asserts root property and CLR type only | nested skip/value preservation can fail | add nested/array additive fields and full equality |
| MI-QT-011 | Contract dependency isolation uses four forbidden substrings | unlisted dependency passes | use explicit framework/approved reference allowlist |
| MI-QT-012 | Protected-worker Gate 2 static suites omit the Contracts project | target project can violate policy | include Contracts project/source/package surface |
| MI-QT-013 | Workflow contract tests use token substrings | comments/unreachable YAML can satisfy them | parse named step command, project and failure/floor semantics |
| MI-QT-014 | `Progress_DecreasingStageCountInvariant...` tests only upper bound | decrease/lower bound can break | add real decreasing transition and rename upper-bound case |
| MI-QT-015 | Resolved: exact-limit reader previously checked only length/endpoints | `ReadLineAsyncReturnsPayloadAtExactOneMiBLimit` now compares all 1 MiB of returned bytes | preserve complete `CollectionAssert.AreEqual` evidence |
| MI-QT-016 | Resolved: concurrent writer flush probe was a no-op | `WriteLineAsyncSerializesFlushWithinEachFrameTransaction` blocks the first flush and records exact payload/LF/flush ordering before frame B | preserve exact transaction events and pre-release queue assertion |
| MI-QT-017 | Resolved: stream ownership previously checked only `CanWrite` | `DisposeLeavesCallerOwnedStreamUsable` writes and flushes caller bytes after writer disposal | preserve direct caller operation plus disposed-writer rejection |
| MI-QT-018 | Resolved: one-byte helper previously omitted requested-buffer size | `ReadLineAsyncUsesFixedSizeReadAheadBuffer` records the largest async read request and asserts the declared 4096-byte bound | preserve an actual requested-size assertion rather than inferring allocation from bytes served |
| MI-QT-019 | LLama source-contract checks rely on lexical forbidden strings | semantically unsafe refactor can evade tokens | retain lexical fitness only as a supplement to behavioral/type graph tests |

## Potential defects

Coverage gaps are not code defects. The following candidates are tracked
separately and require focused RED tests before production edits.

| ID | Confidence | Candidate | Failure mode | Intended first test |
|---|---|---|---|---|
| MI-DEF-001 | fixed and full-contract-green | `WorkerModelFileEvidence.Validate()` now rejects `IntegrityPreserved=true` when length, UTC timestamp, or case-insensitive SHA differs | prevents a consumer from trusting contradictory preservation evidence | `WorkerEvidenceValidationTests.ModelFileIntegrityTrueRejectsContradictoryEvidence`: RED 0/3, then GREEN in focused 64/64 and full Contracts 129/129 task-worktree runs based on `8016231bdda87c3440eb2bd19a104ae6c55ebde7`, zero skipped |
| MI-DEF-002 | fixed and full-contract-green | `WorkerTokenizerEvidence.Validate()` rejects null `KnownSpecialTokenIds`, and composed evidence invokes it | prevents a downstream non-null assumption from receiving invalid completed evidence | `InspectionEvidenceRejectsNullKnownSpecialTokenIds` and `CompletedDispatcherRejectsNullKnownSpecialTokenIds`: RED 0/2, then GREEN in focused 64/64 and full Contracts 129/129 task-worktree runs based on `8016231bdda87c3440eb2bd19a104ae6c55ebde7`, zero skipped |
| MI-DEF-003 | fixed and full-transport-green | buffered `ReadLineAsync` now checks cancellation before renting or consuming buffered bytes while retaining refill cancellation | prevents a cancelled operation from consuming or returning the next buffered frame | `ReadLineAsyncObservesPreCanceledTokenAtBufferedFrameBoundary`: focused RED 0/1 for this case, then focused defect set 3/3 and full Transport 27/27 GREEN on the task working tree based on `192c16d27ba0f0d10ff0055d5bbd3d613cedff13`, zero skipped |
| MI-DEF-004 | design/resource risk — characterized, not closed | each queued writer intentionally takes its bounded mutable-payload snapshot before waiting on the semaphore | aggregate memory can still grow with the number of queued maximum-size callers | `WriteLineAsyncPreservesValidatedQueuedPayload` and queued-cancellation evidence preserve per-call correctness only; pending-byte reservation remains deferred to a separately approved transport resource-policy decision |
| MI-DEF-005 | confirmed copy defect | page explanation uses `runtime is support` | visible ungrammatical user text | packaged/source copy assertion followed by minimal XAML correction |

## Protocol decisions requiring separate approval

The completeness implementation does not silently decide:

- whether all non-null/defaultable wire properties become explicitly required;
- whether omitted evidence sections differ from present-but-unavailable sections;
- whether SHA values must be exactly 64 hexadecimal characters;
- exact consistency rules for chat-template presence, length and hash;
- new cross-field rules among stage, status, completed count and fraction;
- whether `IntegrityPreserved` remains serialized or becomes derived.

Tests may characterize current behavior. A behavior change needs an approved
protocol design and compatibility assessment.

## Documentation and evidence inconsistencies

| ID | Current inconsistency | Required disposition |
|---|---|---|
| MI-DOC-001 | `tests/README.md` describes one executable project and 108 executions | update current test-project/count description without rewriting history |
| MI-DOC-002 | CI documentation says packaged WinUI uses MTP and omits protected-worker Gate 2 | document app-container VSTest and the full permanent boundary |
| MI-DOC-003 | feature parent README presents protected worker as the next gate | state protected-worker Gate 2 exists and unavailable engine remains deliberate |
| MI-DOC-004 | root solution omits Contracts production/tests and five LLama projects | either include them or clearly stop treating the solution as complete discovery |
| MI-DOC-005 | Protected-worker Gate 2 evidence contains an older exact-head-pending statement | preserve history and link later closure evidence |
| MI-DOC-006 | older ADR/design non-claims read as current when taken alone | add supersession/current-status links without altering decision history |
| MI-DOC-007 | cleanup design allows qualified final dispositions but all ledger rows say bare `reviewed` | enforce and migrate to approved disposition vocabulary |
| MI-DOC-008 | cleanup `CompleteRoots` omits four LLama sibling test/support roots | enumerate those roots dynamically or add them explicitly |
| MI-DOC-009 | `401259...` source evidence and `8f00a64...` cleanup Phase 1 closure can be conflated | retain both identities and explain their different evidence roles |

## Historical evidence baseline

The audit verified the repository/GitHub record for the following historical
evidence. It is not fresh gap-closing evidence.

| Item | Verified record |
|---|---|
| Cleanup Phase 1 closure | `8f00a64a40e916872abfd6b72721ef86f223bab9` |
| Exact-head permanent run | `31235042006` |
| Exact-head job | `93045878125` |
| Historical executable total | 436 passed |
| Contracts | 82 |
| Transport | 20 |
| Worker host | 4 |
| WorkerClient | 86 |
| Worker process methods | 27 |
| Packaged WinUI | 217 |
| Unit-result artifact | ID `9015153450`, 55,650 bytes, recorded digest prefix/suffix `a1ad...ac7c` |
| Protected-worker Gate 2 artifact | ID `9015153234`, 47,064 bytes, recorded digest prefix/suffix `d809...e9b` |
| LLama deterministic retained executions | 170 |
| LLama hosted native retained executions | 4 |
| LLama trusted controlled-model retained executions | 20 |

## Immediate gap-closing order

1. Shared evidence validators and independent completion predicates.
2. Protocol JSON/field/enum and sequence gaps.
3. Transport cancellation, fragmentation, exact bounds, ownership and flush.
4. Worker host missing lifecycle branches.
5. Actual production-worker smoke.
6. Unexecuted real-process fixture scenarios and focused orphan verification.
7. Actual page Loaded/navigation/lifecycle coverage.
8. Current presentation mapping, filesystem and accessibility evidence.
9. CI discovery/floors/triggers, privacy uploads and orphan gates.
10. Coverage/mutation analysis and exact-head evidence closure.

The detailed RED/GREEN steps, commands and commit boundaries belong in
`docs/superpowers/plans/2026-08-08-model-inspection-test-completeness-gate.md`.

## Closure rule

This matrix may move a current row to verified only when:

- the correct layer executes the behavior and realistic failure;
- the test would fail if the behavior broke;
- the exact final commit has fresh evidence;
- no required test is skipped or silently undiscovered;
- privacy, model integrity and orphan-process gates pass where relevant;
- the evidence record links the requirement, implementation, test and result.

It does not authorize production Gate 3, cleanup Phase 2 or any downstream
feature.

## Engineering basis

Repository sources:

- current production, tests, fixtures and workflows at the audited commit;
- [Model Inspection completion roadmap](../superpowers/specs/2026-08-08-model-inspection-completion-roadmap-design.md);
- [worker integration design](../superpowers/specs/2026-08-05-model-inspection-worker-integration-design.md);
- [Protected-worker Gate 2 design](../superpowers/specs/2026-08-05-model-inspection-worker-gate-2-host-process-adapter-design.md);
- [cleanup master plan](../superpowers/plans/2026-08-06-model-inspection-cleanup-master.md);
- protected-worker Gate 1, protected-worker Gate 2, cleanup Phase 0 and cleanup Phase 1 evidence under `evidence/`;
- [LLamaSharp runtime coverage matrix](./LLamaSharp-Runtime-Test-Coverage-Matrix.md).

External read-only project sources:

- Workflow Register v1.2 and the eight controlled workflow documents listed in
  the roadmap;
- Project Testing Standard;
- Full Project Summary;
- Full Implementation Guide.

Current official Microsoft Windows accessibility guidance supports combining
automated checks with manual assistive-technology review, logical keyboard and
focus verification, text scaling, high contrast and observable live-region
events:

- <https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-testing>
- <https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-checklist>
- <https://learn.microsoft.com/en-us/windows/apps/design/input/keyboard-interactions>
- <https://learn.microsoft.com/en-us/windows/apps/develop/input/text-scaling>
- <https://learn.microsoft.com/en-us/accessibility-tools-docs/items/uwpxaml/text_livesetting>

`windows-apps.pdf` was present but not text-extracted, and no unavailable
textbook is claimed as read.
