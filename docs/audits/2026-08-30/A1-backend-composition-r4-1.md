# A1 R4.1 backend composition remediation report

## Outcome

Complete with external native, production-package, and application-host blocks. All A1-controlled Critical and Important defects in the R4.1 assignment are corrected, production reachable, covered by managed tests, and approved by the independent read-only reviewer. This non-authoritative development machine does not establish Intel-native, native-runtime, TurboQuant, OpenVINO, GGUF-runtime, package-install, screenshot, hardware, or performance acceptance.

The production package gate failed closed because the externally supplied `GgufQuantizerStageDirectory` is absent. The exact built Debug executable then failed before creating a window because the Windows App SDK activation class is not registered (`0x80040154`). No screenshot surface existed. The complete Model Inspection contracts suite still exposes 22 inherited cross-owner failures; none were suppressed.

## Immutable identities

| Identity | Commit | Tree |
|---|---|---|
| Frozen historical source | `4748fe04f19afdf6b27c4c12502b84db325e7294` | `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91` |
| A1 R3 source | `3c2cccbf77e5acf1ebf673c78f57e35a08950450` | `47ed8e9b751e3ad5e3d24943a447a2c4b0b32c0b` |
| A1 R4 implementation subject | `4db647ac038c3cd691b219cf29e0ec02965dfdb0` | `9a68aa4d0cc9b4dd690310dce8943dd04312cd75` |
| R4.1 exact base | `3a1f2df54e21e0881b5f5f1e8d9b52ef33c279a6` | `94a637078e1e481329dac3d81da168f5e8111cce` |
| R4.1 final implementation subject | `7dbf42bd3a72ba8b11c8342ad0abb150e0268c3d` | `bf3c1da94688e50b673d660ff0b79a459ac26755` |

Branch: `audit/ucl-a1-remediation-r4-1`. Worktree: the assigned isolated `C:\R4-A1-41`. The base descends from the R4 subject and contains C0 ancestor `a5ef3558334e50587889140dafba194853938765`. Main was not merged, rebased, reset, or modified.

## Commit sequence

1. `0abc071c1537f61f264c26b999b63a5226521c02` — initial typed fault boundaries, transactional Chat initialization, retirement, demo removal, and tests.
2. `9e5f1cf70238bc41f3d94acd01c6782f5ca4e73a` — queued-render containment, provenance taxonomy, and startup cleanup fixes from independent review.
3. `edba9ff15853b97c6eb5908d24735412ed194b03` — history-record, save-cleanup, and runtime teardown corrections.
4. `7abfc690b0f04e8386851c838cde63a5107cece6` — process-wide authority root, private constructors, non-forgeable token, and behavioral bypass tests.
5. `7dbf42bd3a72ba8b11c8342ad0abb150e0268c3d` — nested private registry compile correction; final code/test subject.

## Requirement-to-evidence closure

| Requirement | Production result | Managed evidence |
|---|---|---|
| CRIT-001 delayed Chat faults | Expected history/runtime failures become bounded support codes; unexpected programming faults cross one stable-code/classification reporter; queued callbacks are contained; raw operational exceptions do not reach later retirement | Chat affected 56/56; lifetime filter 18/18 three times; privacy and closure scans |
| Idempotent retirement and complete cleanup | One Task is published before cancellation/cleanup; concurrent/re-entrant callers share it; every phase runs; first failure is preserved and safely classified | Controller lifetime, shell lifetime, scheduler, adapter, worker-client 12/12 |
| Import and shutdown | `async void` handlers immediately await Task-owned business logic; Import navigates once in `finally`; shutdown awaits Chat retirement and continues cleanup | `OnboardingShellChatLifetimeTests`; final lifetime repetitions |
| IMP-001 compatibility taxonomy | Sealed typed fresh-resource unavailability is fail-closed; exact cancellation token and unrelated programming faults propagate | Compatibility complete 1,052/1,052; affected packaged 27/27 |
| IMP-002 page reporting | Expected unavailability renders safely with no programming-fault report; unexpected evaluator faults render safely and report once without raw detail | View-model/page/fault-reporter focus within 27/27 |
| IMP-003 failed initialization | One transactional initialized factory owns creation; failure/cancellation retires once and preserves the primary failure plus bounded cleanup fact | Initialization and lifetime tests within 56/56 and 18/18 repeats |
| IMP-004 demo authority | Release production demo constructor and `DemoGgufChatSession` removed | Release closure test and zero production demo-type scan |
| IMP-005 semantic uniqueness | One process-wide `A1BackendProductionAuthorities.Shared`; private root/registry/constructors; private identity token; duplicate root and forged token reject behaviorally | Semantic composition 25/25; supplementary full-tree scan |
| IMP-006 independent review | Exact immutable subjects reviewed read-only; every Critical/Important finding corrected and re-reviewed | Durable review document; final reviewer disposition 0 Critical / 0 Important |
| IMP-007 repeat evidence | Three fresh, consecutive, no-sleep full lifetime-filter executions against the final subject | 18/18, 18/18, 18/18 with distinct timestamps, bytes, and hashes |

## Implementation summary and live callers

- `ApplicationFaultReporter` retains only a stable safe code and exception classification with bounded first-report behavior.
- Compatibility capture catches only the sealed operational taxonomy; cancellation and programming defects remain observable to Task callers.
- `CompatibilityPage.ActivateAsync` owns expected/unexpected rendering and reporting; `Page_Loaded` only awaits it.
- `ChatDemoController` owns the single retirement Task, bounded operation/support state, transactional initialized production creation, and complete cleanup.
- `ChatRenderScheduler` reports faults thrown inside the queued dispatcher callback.
- History load marks empty, oversized, null, and identity-mismatched records unavailable; save cleanup cannot replace a primary failure.
- `GgufRuntimeSession.DisposeAsync` attempts close, channel, and process phases and rethrows the first failure; the app adapter converts expected/non-cancellable teardown failures to typed support state.
- `A1BackendProductionAuthorities.Shared` is the live process root for compatibility, route-exact optimization, official OpenVINO worker installation, and initialized GGUF Chat.

The five live root uses are the shell compatibility path, shell optimization path, current-model Chat launcher, optimized-model Chat launcher, and Model Inspection official-worker composition. There are zero named direct compatibility/optimization constructions, zero production demo Chat types, and no unguarded production Chat initializer outside the root.

## TDD and mutation record

The pre-edit matrix is committed at `docs/superpowers/plans/2026-08-30-a1-r4-1-remediation.md`. Behavior-first tests covered typed/untyped fresh capture, exact cancellation, page reporting, history I/O, queued faults, bounded reporter state, import navigation, shutdown continuation, failed initialization before/after preparation, cleanup failure, concurrent/re-entrant retirement, demo removal, and authority uniqueness.

Observed RED included the focused packaged run with 14 discovered, 12 passed, and 2 failed: a queued-render programming fault escaped its actual callback boundary, and unscoped I/O was incorrectly classified as history unavailability. Reviewer-driven mutation checks then exposed startup cleanup masking, history-record gaps, silent teardown failures, and fresh-registry uniqueness bypass. Each correction established a new subject and invalidated affected evidence. The final mutation probes reject swallowed unrelated `InvalidOperationException`, omitted failed-init retirement, expected history I/O escaping through retirement, a second retirement Task, restored demo authority, a second process root, and a forged authority token.

## Managed verification arithmetic

Principal non-overlapping test rows exclude the semantic duplicate of focused classes and the three repeat runs:

| Suite | Discovered | Executed | Passed | Failed | Skipped | Disposition |
|---|---:|---:|---:|---:|---:|---|
| Model/Hardware Compatibility complete | 1,052 | 1,052 | 1,052 | 0 | 0 | Pass |
| CrossFeature Integration complete | 53 | 53 | 53 | 0 | 0 | Pass |
| OpenVINO WorkerClient complete | 16 | 16 | 16 | 0 | 0 | Pass |
| GGUF Runtime WorkerClient complete | 12 | 12 | 12 | 0 | 0 | Pass |
| Packaged Chat affected | 56 | 56 | 56 | 0 | 0 | Pass |
| Packaged compatibility/fault boundary affected | 27 | 27 | 27 | 0 | 0 | Pass |
| Packaged Model Inspection service/composition affected | 17 | 17 | 17 | 0 | 0 | Pass |
| Model Inspection contracts complete | 357 | 357 | 335 | 22 | 0 | Inherited failures retained |
| **Principal total** | **1,590** | **1,590** | **1,568** | **22** | **0** | A1-managed gates pass; inherited failures explicit |

Supplemental semantic composition was 25/25. Consecutive lifetime runs were 18/18 each. Debug and Release x64 application builds passed with 0 warnings. Debug and Release x64 packaged-test builds passed; their warnings occur only in unchanged Model Import/Model Inspection test-fixture paths. Changed-path warning count is zero. `git diff --check` is zero.

The 22 known Model Inspection contract failures concern missing workflow/controlled-evidence files, unapplied fixture/cleanup inventories, Debug fixture boundary expectations, and package-item assertions. They predate and do not intersect the A1 changed implementation; A1 did not edit those cross-owner contracts to hide them.

## Application, package, privacy, and process gates

- Source/component app build: Debug and Release x64 pass with external packaging disabled explicitly; this is not package acceptance.
- Packaged-test project build: Debug and Release x64 pass with the documented source/component packaging controls.
- Strongest production gate: exit 1, fail closed at missing `GgufQuantizerStageDirectory`; no production package was asserted.
- Exact built-app launch: process exit `-532462766` (`0xe0434352`); Windows events 1026/1000/1001 safely classify the cause as Windows App SDK activation class not registered (`0x80040154`). No window, smoke path, or screenshot was available.
- Privacy: zero sensitive-pattern matches in added production lines and zero private-machine tokens in durable external records. Seven deliberate fake path/provider sentinels exist only in tests to prove non-leakage.
- Closure: five production `Shared` callers; zero direct named compatibility/optimization constructors; zero demo session types; clean code subject; zero orphan Granite app/worker processes.

## Independent review

The independent task `/root/r4_1_independent_review` inspected the prompt, historical report, full base-to-subject diff, live callers, fault/lifetime paths, tests, and exact commit/tree identities. Its review trail found and closed:

- `0abc071c`: 1 Critical and 4 Important findings.
- `9e5f1cf`: 0 Critical and 3 Important findings.
- `edba9ff`: 0 Critical and 1 Important finding (fresh-registry bypass).
- `7abfc690`: semantic finding closed, but owner verification found compile error CS9051, so the approval and evidence were superseded.
- `7dbf42bd`: compile-capable Release x64 re-review passed with 0 warnings/errors; final disposition approved with 0 Critical and 0 Important findings.

## Method-level C0 merge rules

- `CompatibilityEvaluationOrchestrator.CaptureFreshAsync` and `CreateForAuthority`: preserve H1 acquisition logic; retain the sealed unavailability catch, exact cancellation, private constructor, and authority-token check.
- `WindowsCompatibilityFreshResourcesSource.CaptureAsync`: preserve later H1 facts; translate only named inaccessible/unreadable/stale/inconsistent outcomes.
- `CompatibilityPage.Page_Loaded` / `ActivateAsync`: preserve F1 rendering/navigation; keep the immediate await and one privacy-safe unexpected-fault report after safe VM presentation.
- `OnboardingShellPage.CreateCompatibilityPage`, optimization entry, and optimized Chat launch: preserve all other shell sequencing; retain only the `Shared` authority calls and transactional initialized Chat ownership.
- `OnboardingShellPage.ChatPage_ImportModelRequested` and its Task method: preserve navigation; retain awaited retirement and exactly-once navigation in `finally`.
- `OnboardingShellPage.ModelSourceCustody.ShutdownAsync`: preserve all other cleanup phases; retain awaited Chat retirement and continuation after bounded teardown support state.
- `GgufCurrentModelChatRouteLauncher.LaunchAsync`: preserve route validation/show callback; retain `Shared.CreateInitializedChatAsync` as the only ownership transfer.
- `ModelInspectionServiceComposition.CreateProductionService`: preserve M1 service/schema logic; retain official worker installation through `Shared`.
- `OptimizationBackendCompositionFactory` constructors/factories: preserve route builders; retain private constructors and authority-token validation. Tests use `CreateOptimizationForValidation`, not a second production root.
- `A1BackendProductionReachabilityTests`: retain behavioral duplicate-root/forged-token assertions; source scans remain supplemental only.

## Changed paths and cross-owner disposition

The final base-to-subject diff contains 39 paths: 19 app production paths, three GGUF worker-client paths, 16 tests, and the implementation plan. It adds the authority root and fault reporter, deletes `DemoGgufChatSession.cs`, and does not change XAML, `MainWindow`, project/solution registration, package manifests, navigation registration, Model Inspection schemas/projections, Q1 export/publication semantics, F1 styling/download behavior, or S1 security policy.

Unresolved external/cross-owner items are the 22 inherited Model Inspection contract failures, missing production native stage inputs, and the local Windows App SDK activation registration. None is claimed as A1 acceptance.

## Durable evidence locations

- Verification ledger: `docs/audits/2026-08-30/A1-backend-composition-r4-1-verification.md`
- Independent review: `docs/audits/2026-08-30/reviews/A1-backend-composition-r4-1-independent-review.md`
- Receipt: `docs/audits/2026-08-30/handoffs/R4-A1-R4-1.json`
- External manifest: `C:\UCL-AUDIT-HANDOFFS\R4-A1-R4-1\7dbf42bd3a72ba8b11c8342ad0abb150e0268c3d\EVIDENCE-MANIFEST.json`
- External manifest SHA-256/bytes: `c04f43b177e6eadfa88f84702070b0d5c73cd66354e6e770f1fe0a276b9749e1` / 21,372
- Post-push closure: `C:\UCL-AUDIT-HANDOFFS\R4-A1-R4-1\REMOTE-CLOSURE.json`

The supplied R4 evidence-manifest v2 schema excludes A1, so the schema-v2 receipt correctly uses `evidenceManifest: null`. The external manifest is a supplemental subject-bound evidence index and is not represented as a schema-v2 A1 manifest.

The new receipt was validated successfully with AJV against the supplied R4 worker handoff receipt v2 schema.
