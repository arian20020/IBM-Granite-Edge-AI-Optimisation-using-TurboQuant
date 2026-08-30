# A1 R4 backend composition report

## Outcome

Complete with external native/application-host block. All A1-controlled managed implementation, review, structural scanning, package-independent builds, and focused production-composition verification are complete. The production package gate failed closed because the externally supplied `GgufQuantizerStageDirectory` is absent. The exact Release executable then terminated before creating a window because this machine does not have the required Windows App SDK activation class registered (`REGDB_E_CLASSNOTREG`). No screenshot, Intel-native, native-runtime, package, or performance acceptance is claimed.

The schema-v2 evidence disposition for A1 is `evidenceManifest: null`. The receipt schema permits an evidence-manifest object only for H1, M1, Q1, C0, and E1; A1's reproducible managed test files remain in the Git-ignored locator `TestResults/R4-A1/final/` and are summarized below.

## Immutable source and subject

| Identity | Commit | Tree |
|---|---|---|
| Frozen historical source | `4748fe04f19afdf6b27c4c12502b84db325e7294` | `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91` |
| A1 R3 base | `3c2cccbf77e5acf1ebf673c78f57e35a08950450` | `47ed8e9b751e3ad5e3d24943a447a2c4b0b32c0b` |
| A1 R4 implementation subject | `4db647ac038c3cd691b219cf29e0ec02965dfdb0` | `9a68aa4d0cc9b4dd690310dce8943dd04312cd75` |

Branch: `audit/ucl-a1-remediation-r4`. The base contains required C0 ancestor `a5ef3558334e50587889140dafba194853938765`.

The approved source identities were independently rechecked: decision ref `5e7a74300bdd0c2fff9ffe1bcf51eebed2bf4cc2`; handoff contract SHA-256 `a91672a4bc8af08df3ddfe2bdfbb56c708b0729fb6efcf3e163215044db3d836`; Stage C SHA-256 `92426f25a049b025313b9afe16e10916bb6ba118ff9e4245a4025ea39b240bc4`; decision register SHA-256 `205e22bea3e2a09576e6a737f017e1907b74804ce89c27b94c6ab971c0312b9a`. Visual refs resolved to the required commit/tree pairs `bb500936...`/`8890ca57...` and `ba4fd7ba...`/`be5a10de...`.

## Requirement-to-evidence closure

| Requirement | Production seam and caller | R4 result | Evidence |
|---|---|---|---|
| 1. One production authority | Shell compatibility construction; shell optimization factory; Model Inspection official worker composition; two intentional Chat launch callers into one controller | Full production-tree test asserts one orchestrator construction, one route factory, one official worker authority, one coordinator/controller definition, and one coordinator construction | `A1BackendProductionReachabilityTests`, 6/6 within the 28-test packaged gate |
| 2. No new shell behavior | Existing shell callers were inspected but not edited | Construction and lifetime logic remains in A1-owned Task-returning backend types; exact C0 migration is below | Forbidden-file scan: no shell, XAML, project, manifest, or navigation file changed |
| 3. Cancellation and fault taxonomy | `CompatibilityEvaluationOrchestrator`, core `CompatibilityRunCoordinator`, `ChatDemoController` | Cancellation propagates; expected fresh-fact capture failure is fail-closed only at the typed capture boundary; evaluator/adapter/event/stop programming faults are no longer converted or discarded | Compatibility full suite 1,052/1,052; packaged cancellation/fault tests; focused TDD RED/GREEN below |
| 4. Cohesive extraction only | Existing production factory, transaction orchestrator, and Chat lifetime owner | No service locator or broad rewrite added | Diff/architecture review, 12 changed paths |
| 5. One Chat retirement task | `ChatDemoController.DisposeAsync` owns publish-before-start retirement; `GgufChatCoordinator` owns session state | Concurrent, re-entrant, stop-fault, and event-fault teardown callers converge on one Task; cleanup phases run before a fault is returned | Lifetime class 4/4 in three consecutive final runs, without sleeps |
| 6. Narrow C0 seams | Fresh-facts orchestrator; route-exact factory; Model Inspection composition; Chat production controller | All four remain production reachable and unique | Packaged composition 28/28 plus real packaged Model Inspection N-001 journey 1/1 |
| 7. Structural plus behavioral regressions | Real app assembly and production source tree | Structural scan is not the sole oracle; real orchestrator, coordinator, lifetime, and Model Inspection composition behavior also executes | Packaged tests and complete managed suites below |
| 8. Warning inventory | All A1-changed paths | Zero warnings in A1 production/test changes | Release app 0 warnings; test-host inherited warning inventory below |

## Implementation and review findings

Compatibility no longer has two catch-all fallback layers. `CompatibilityEvaluationOrchestrator` bounds fail-closed handling to fresh-resource capture (`IOException`, `UnauthorizedAccessException`, and the current H1 source's operational `InvalidOperationException`) and leaves evaluator/binder programming defects observable. `CompatibilityRunCoordinator` likewise preserves programming faults while its `finally` still rolls back the handoff claim.

Chat teardown publishes a placeholder retirement Task before starting cancellation or cleanup. This closes the cancellation-callback re-entrancy window. Every caller receives that Task. Stop, pending-operation, renderer, session, and token cleanup failures are collected; one original fault preserves its stack, and multiple faults become an aggregate. Unexpected event-operation faults are recorded behind a lock separate from the active-operation lock and returned through retirement.

Two separate final reviews were performed locally because delegation was not authorized by the workspace instructions:

1. Requirements/architecture review found the cancellation re-entrancy window and the deeper compatibility run-coordinator catch-all.
2. Adversarial concurrency/error review found swallowed Chat stop/event faults, a possible synchronous-operation self-deadlock from sharing the operation lock for fault recording, and a stale structural oracle.

Every Critical/Important finding above was corrected and the affected tests/builds were rerun. No unresolved A1-owned Critical/Important finding remains.

## Behavioral RED/GREEN record

- Core adapter programming-fault tests: genuine RED 0/2 (the old coordinator returned `Failed`), then GREEN 4/4 including rollback.
- Chat cancellation re-entrancy: genuine RED 1 passed / 1 failed, with session disposal count 2 instead of 1; corrected lifetime suite GREEN 4/4.
- Chat stop programming fault: genuine RED 0/1 because no exception was returned; corrected test GREEN.
- Chat event programming fault: genuine RED 0/1 because retirement returned success; corrected test GREEN.
- The final lifetime suite ran three consecutive times at 4/4 with no sleeps. One unrelated transient deployment lock occurred while another worktree was using the shared WinUI package identity; after the other process released it, the bounded sequential reruns passed.

## Managed verification arithmetic

Principal final matrix (repeat/mutation runs excluded from the total so tests are not double-counted):

| Suite/gate | Discovered | Executed | Passed | Failed | Skipped | Disposition |
|---|---:|---:|---:|---:|---:|---|
| Model/Hardware Compatibility complete | 1,052 | 1,052 | 1,052 | 0 | 0 | Pass |
| CrossFeature Integration complete | 53 | 53 | 53 | 0 | 0 | Pass; this frozen A1 base has no expected RED in the suite |
| OpenVINO WorkerClient complete | 16 | 16 | 16 | 0 | 0 | Pass |
| A1 reachability, compatibility cancellation, GGUF coordinator/lifetime packaged focus | 28 | 28 | 28 | 0 | 0 | Pass |
| Real Model Inspection N-001 packaged composition journey | 1 | 1 | 1 | 0 | 0 | Pass |
| Model Inspection contracts complete | 357 | 357 | 335 | 22 | 0 | Inherited M1/C0 workflow and fixture-boundary failures retained |
| **Total** | **1,507** | **1,507** | **1,485** | **22** | **0** | Managed A1 gates pass; inherited failures explicit |

Durable local result records:

| Relative locator | Bytes | SHA-256 |
|---|---:|---|
| `TestResults/R4-A1/final/compatibility/compatibility.trx` | 1,478,824 | `d86598a1e92f24c854f05a65b673735e2851315edf76ed10006f1f55e5a75234` |
| `TestResults/R4-A1/final/crossfeature/crossfeature.trx` | 81,291 | `f420449fcae384a4f9d3329a4486279b27e214712e120fb0ae52cb9d8d18e24e` |
| `TestResults/R4-A1/final/openvino-workerclient/openvino-workerclient.trx` | 23,994 | `56ea774bbdf85a7fd768623d295eece6eff8e8558e7587a29320ff77e82e2627` |
| `TestResults/R4-A1/final/packaged-focused-green/packaged-focused-green.trx` | 156,089 | `ddea9b608efef5a042c45233c311b1b2b7d1d3c468e31dadbb597baee40918e5` |
| `TestResults/R4-A1/final/model-inspection-composition/model-inspection-composition.trx` | 118,352 | `aad3ad53048a4c8ea1608d5932a42f30683548c19e84e77f46e7843422ddb722` |
| `TestResults/R4-A1/final/model-inspection-contracts/model-inspection-contracts.trx` | 696,450 | `785c0ca41293d9ec476a4389b496b2417a4c6491752b94907b4fa4a5bc72b98b` |

The 22 contract failures are unchanged ownership blockers: missing workflow/controlled-evidence files, the unapplied shared fixture-boundary proposal, and package-item assertions. A1 did not edit workflows, project registration, M1 schema/fixture ownership, or C0 package composition to mask them.

## Builds, warnings, package, and application gate

- Release x64 package-independent application: pass, 0 warnings / 0 errors.
- Release x64 packaged test application: pass, 14 warning emissions / 0 errors. These are seven unique inherited nullable warnings in F1-owned `ModelImportDropAccessibilityTests.cs` at lines 142, 147, 152, 172, and 176.
- Debug x64 package-independent application: pass, 0 warnings / 0 errors.
- Debug x64 packaged test application: pass, 27 warning emissions / 0 errors. These represent the seven F1 nullable warnings twice, two M1 fixture nullable warnings twice (`ModelInspectionFixtureGalleryTests.cs` lines 1687 and 2132), and nine M1 fixture `MSTEST0044` warnings. No suppression was added.
- Production package gate: expected fail closed, 0 warnings / 1 error at `GgufQuantization.WorkerPackaging.targets(26,5)`: `GgufQuantizerStageDirectory is required when GGUF quantizer packaging is enabled.`
- Exact Release executable application gate: the owned process started, then exited before presenting a window. Windows Event Log recorded `TypeInitializationException` -> `COMException 0x80040154 (REGDB_E_CLASSNOTREG)` from Windows App SDK deployment initialization. The owned process exited and no app/worker process remained.

Because no exact app window could be activated, the mandatory visual scenarios (normal/reduced size, 100%/200%, state matrix, keyboard/focus, and screenshot comparison) are host-blocked. No screenshot exists and no visual pass is claimed. There were no A1 UI changes; approved visual refs were verified, and no F1/C0 surface was edited.

## Full-candidate scans

- `git diff --check`: pass.
- Worktree status before report creation: clean.
- Forbidden-file diff scan: no `MainWindow`, XAML, project, manifest, shared shell, or navigation change.
- Duplicate authority scan: exactly one production compatibility orchestrator construction, optimization factory construction, official worker installation authority call, `GgufChatCoordinator` definition, `ChatDemoController` definition, and coordinator construction.
- Path/privacy scan: no user path, hostname, model path/name, prompt text, provider output, secret, or raw machine result added by the diff. The word `prompt` occurs only as an existing typed code parameter in the new test fixture.
- No orphan owned application, unit-test host, Model Inspection worker, or GGUF runtime worker remained after the gates.

The only ambient-result authority in the frozen candidate remains outside A1's R4 edit authority: `OnboardingShellPage.xaml.cs` lines 991 and 1094 read Q1's `LastPublishedDirectory`, whose definition/update is in `OpenVinoOptimizationJourneyInfrastructure.cs` lines 177 and 221. A1 composition types contain zero such use.

## Changed paths

- `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatDemoController.cs`
- `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/CompatibilityEvaluationOrchestrator.cs`
- `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/CompatibilityRunCoordinator.cs`
- `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityEngine.cs`
- `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityFallbackPolicy.cs`
- `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/CompatibilityRunCoordinatorTests.cs`
- `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Presentation/CompatibilityFallbackPolicyTests.cs`
- `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Presentation/CompatibilityScreenProjectionTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatDemoControllerLifetimeTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/CompatibilityEvaluationOrchestratorTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/A1BackendProductionReachabilityTests.cs`
- `docs/superpowers/plans/2026-08-30-a1-r4-backend-composition.md`

## Commit list

1. `db7a4d839e38052d2bb526a06589ed64e04d34d2` — plan A1 R4 backend composition
2. `1fd601793719dc8fc5c77fc4a1b93f82bc022ef6` — preserve compatibility programming faults
3. `5cbd05ff6659d23a3b70f1dea6f2dfa92a39dd2f` — unify controller retirement
4. `5ecaf1698c957a36b42b12d35eff1946b815e2ff` — scan complete production authority
5. `ebc0509fd4d90c22ac6a336683b3d9044b6dca8f` — surface lifecycle programming faults
6. `95b2d61843cf9ba1144e9a0fe82db8be59822c2e` — isolate failure-state synchronization
7. `4db647ac038c3cd691b219cf29e0ec02965dfdb0` — require publish-before-start retirement

## C0 apply-checkable handoff

Import the seven commits above in order, or merge the final pushed branch while preserving their ancestry. Keep the existing shell compatibility construction at `OnboardingShellPage.xaml.cs:683` using `CompatibilityEvaluationOrchestrator(_compatibilityFreshResourcesSource, TimeProvider.System)`. Keep the route-exact `OptimizationBackendCompositionFactory` call at line 837, `ModelInspectionServiceComposition.CreateDefault()` from `ModelInspectionPage.xaml.cs:65`, and both intentional `ChatDemoController.CreateProductionAsync(...)` callers (`GgufCurrentModelChatRouteLauncher.cs:52` and shell line 1067).

After integrating Q1's exact-result API, replace both C0-owned shell reads at lines 991 and 1094. Chat must call `CreateChatTargetAsync(state.Result, token)`; export must call `ExportPersistentAsync(state.Result, destination, maximumBytes, token)`. Remove `LastPublishedDirectory` only after those current callers migrate. Do not add another compatibility evaluator, optimization factory, worker inventory, Chat coordinator, or shell.

Method-level collision rule: preserve A1's `ChatDemoController.DisposeAsync`, `CompleteRetirementAsync`, `RetireCoreAsync`, and `RunOperationAsync` semantics if F1/C0 resolves adjacent Chat UI changes. Preserve the `CompatibilityEvaluationOrchestrator.CaptureFreshAsync` boundary and the core run coordinator's `finally` rollback if H1/C0 changes fact projection.

## Remote closure

The receipt/report commit is intentionally later than the immutable implementation subject. Remote tip equality, artifact containment, and ancestry are verified after push and therefore reported outside this non-circular committed report/receipt pair.
