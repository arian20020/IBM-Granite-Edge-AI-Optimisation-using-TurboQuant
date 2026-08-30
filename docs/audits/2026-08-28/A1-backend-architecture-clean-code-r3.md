# A1 backend architecture and clean-code remediation R3

## Status

- Worker: A1
- Disposition: **A1 R3 implementation complete; inherited cross-owner contract and package-stage blockers remain**
- Authoritative R3 base: `66e2a4a5a4509962cb3faee410c69c5d764e0854`
- R3 base tree: `dd4c9eef755e126ee6fab3a901fec56ddc267ab5`
- C0 base/ancestor: `a5ef3558334e50587889140dafba194853938765`
- Frozen ancestor/tree: `4748fe04f19afdf6b27c4c12502b84db325e7294` / `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`
- Branch: `audit/ucl-a1-remediation-r3`
- Evidence-subject commit/tree: `5cb1a1c988c9bee3587d55a2d299fa819683b434` / `90b0d2c79613443d6b0b80ff35686a86497ba076`
- Worktree: isolated short-path A1 R3 worktree; absolute local path intentionally omitted
- Report encoding/line endings: UTF-8, LF

The final report-containing commit/tree, report SHA-256, byte count, remote equality, and clean state are derived externally after this report is committed. A1 creates no evidence manifest.

## Scope and authority

The explicit R3 assignment authorized three narrow production-composition edits that had previously remained C0-owned: the live compatibility path and optimization navigation in `OnboardingShellPage`, and official OpenVINO worker creation in `ModelInspectionServiceComposition`. The assignment did not authorize `MainWindow`, navigation registration, solution/project/package registration, frontend presentation, Q1 OpenVINO result semantics, or M1 inspection contracts.

Reviewed references were the C0 final reconciliation, the original A1 audit, the A1 R2 report, the committed R3 design/plan, and the shared authority, safety, testing, report, and receipt contracts. Reference-document instructions were treated as context; the user's R3 request supplied execution authority. Superpowers 6.3.0 planning, execution, TDD, systematic-debugging, and verification workflows informed the work. After the local skill cache was removed during machine cleanup, the same evidence-first verification steps were applied directly.

## Executive result

R3-017 is closed. The three R2 backend authorities now have exactly one live production registration each:

| Backend authority | Definition | Live production caller | Registration count |
|---|---|---|---:|
| Compatibility evaluation and fallback | `Features/ModelHardwareCompatibility/Application/CompatibilityEvaluationOrchestrator.cs:13` | `Features/Onboarding/OnboardingShellPage.xaml.cs:683` | 1 |
| Route-exact optimization composition | `Features/ModelOptimization/Application/OptimizationBackendCompositionFactory.cs:35` | `Features/Onboarding/OnboardingShellPage.xaml.cs:837` | 1 |
| Official OpenVINO worker installation | `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/OpenVinoOfficialWorkerAuthority.cs:10` | `Features/ModelInspection/Services/ModelInspectionServiceComposition.cs:75` | 1 |

The shell's three duplicate compatibility bodies now call `EvaluateAuthorityAsync` twice and `EvaluateBoundAsync` once. Dependency cancellation propagates as `OperationCanceledException`; ordinary dependency/evaluation faults still use the existing fail-closed fallback policy. `NavigateToOptimization` no longer directly constructs runners, executors, revalidators, context factories, output registries, routers, or coordinators. Model Inspection retains its exact packaged-manifest byte hash and assembly-approved digest comparison before calling the official worker authority; duplicated build evidence, binary-machine inventory, and direct installation construction are removed.

Every R3-owned test and affected backend suite is green with non-zero discovery and no skips. The final verification invocations total 1,509 discovered/executed, 1,487 passed, 22 failed, and 0 skipped. The 22 failures are the exact pre-existing C0 Model Inspection contract baseline (335/357 passed), owned by M1/H1/C0 and unrelated to the three R3 production files; there are zero unexpected R3 failures or skips. A Release x64 app/unit build succeeds with zero errors and no warning in an A1-changed path. It emits seven unique inherited nullable warnings from a frontend Model Import test file. The real package gate remains correctly fail-closed because the authorized GGUF quantizer stage is absent.

## R3 production corrections

### Compatibility evaluation

`OnboardingShellPage.NavigateToCompatibilityAsync` constructs one `CompatibilityEvaluationOrchestrator` from the existing fresh-resource source and `TimeProvider.System`. GGUF and OpenVINO authority branches delegate to `EvaluateAuthorityAsync`; the bound fallback branch delegates to `EvaluateBoundAsync` using the existing route-exact `TryBindFresh` operations.

The displaced broad catches no longer convert an `OperationCanceledException` raised by a dependency into a fallback result merely because the caller token is not already cancelled. The orchestrator remains the single owner for deterministic evaluation time, ordinary-fault mapping, binding failure, and fail-closed projection. No public contract changed.

### Optimization composition

`NavigateToOptimization` keeps the existing per-user app-root calculation, then creates one `OptimizationBackendCompositionFactory` from the existing custody registry, active route authorities/service, and `TimeProvider.System`. `TryCreate` refuses a missing, duplicate, or mismatched route authority. On success, the shell retains only the returned coordinator, context factory, outputs, and route executor reference needed for UI lifetime.

The factory remains the sole constructor of route runners, executors, revalidators, attempt-context factories, output storage/registry, router, and coordinator. Source custody, exact route selection, timeouts, identity revalidation, cancellation, cleanup, output admission, and original-model preservation stay in their existing backend owners.

### Official worker authority

`ModelInspectionServiceComposition` still reads and hashes the exact packaged `worker-manifest.json` bytes and compares the lowercase SHA-256 with assembly-approved metadata using ordinal equality. Only after that comparison does it call `OpenVinoOfficialWorkerAuthority.CreateInstallation`. The route service receives `installation.ExpectedBuildEvidence` from the same validated authority.

The local `OpenVinoBuildEvidence` literals, `OfficialBinaryMachines`, direct `OpenVinoWorkerInstallation` construction, and now-unused collection import are removed. Existing closure validation continues to own binary topology, byte counts, hashes, PE machines, protocol identity, cancellation, timeout, and cleanup. No GGUF or OpenVINO implementation was duplicated.

## Finding reconciliation

### A1-IMP-001 - oversized onboarding composition responsibility - fixed

- Reassessment: confirmed on the R3 base; the live shell still bypassed both R2 composition components.
- Correction: the authorized composition points now delegate compatibility and optimization backend ownership to the existing orchestrator/factory. The shell retains UI navigation and lifetime state only.
- Reproduction/verification: the reachability fitness tests fail with zero callers on unchanged R2 production and pass with exactly one caller each; the 22-test onboarding/optimization package filter passes.
- User/security impact: route behavior is no longer split between an unused authority and an inline duplicate; missing/mismatched route authority still refuses composition without fallback.
- Owner/disposition: A1, fixed in R3.

### A1-IMP-002 - GGUF Chat lifetime and retirement - already correct

- Reassessment: C0 had already serialized disposal against active generation; R2 preserved that behavior.
- Verification: the complete cross-feature suite passes 53/53.
- Owner/disposition: already corrected by C0; A1 made no competing implementation.

### A1-IMP-003 - OpenVINO Chat launch failures collapsed to `bool` - blocked by Q1/M1/C0

- Reassessment: unchanged and outside this assignment's result-contract ownership.
- Exact proposed C0 diff: replace the OpenVINO activation `bool` with the Q1/M1-approved typed activation result; at each shared launch consumer, branch on success and map every failure to its stable safe support code/presentation key. Preserve cancellation, redact raw exception/path data, and register no second launcher.
- Owner/disposition: Q1/M1 contract plus C0 shared consumer; blocked/deferred, not edited by A1.

### A1-IMP-004 - redundant OpenVINO optimization staging - deferred to Q1

- Reassessment: the attempt context and service still have separate staging/custody responsibilities. R3 composition intentionally preserves this behavior.
- Required owner decision: Q1 must make the execution service consume the admitted immutable attempt source or remove the redundant copy while preserving identity revalidation, exact result binding, bounds, cancellation, and cleanup.
- Owner/disposition: Q1; deferred, not edited by A1.

### A1-MIN-001 - uncomposed demo, placeholder, and TurboQuant islands - C0-owned/deferred

- Reassessment: reachability policy for these cross-feature types still requires C0 and feature-owner decisions. Deletion was not inferred from static non-use.
- Proposed C0 action: assign one production composition owner/feature gate or move each confirmed non-production type to a test-only project after all specialist imports are reconciled.
- Owner/disposition: C0 with feature owners; deferred, not edited by A1.

### A1-MIN-002 - duplicated OpenVINO manifest/runtime authority - fixed

- R2 created the validated worker-client authority. R3 now makes it the live production owner after the unchanged app-approved manifest hash comparison.
- Fitness and WorkerClient tests prove one production call, no local machine inventory/direct installation, immutable exact build/protocol facts, manifest binding, and invalid-input rejection.
- Owner/disposition: A1, fixed in R2 and made production-reachable in R3.

### A1-MIN-003 - duplicated compatibility fallback decisions - fixed

- R2 consolidated the synchronous fallback policy and async orchestration. R3 removes all three inline live-shell bodies and delegates to that owner.
- The behavioral regression proves dependency cancellation is not downgraded; ordinary faults and bind failure remain fail-closed.
- Owner/disposition: A1, fixed in R2 and made production-reachable in R3.

### A1-MIN-004 - aggregate mutation uses ambient system time - already fixed

- R2 changed GGUF conversation replacement to receive the coordinator's injected clock. R3 introduces no new ambient backend timestamp in deterministic behavior.
- The two new production boundaries receive `TimeProvider.System` only at composition; downstream behavior remains injectable and tested.
- Owner/disposition: A1, fixed in R2 and verified unchanged in R3.

### A1-MIN-005 - application warnings are not enforced as errors - C0/F1-owned

- The Release x64 build emits seven unique inherited nullable diagnostics, repeated to 14 build emissions, only in `Features/ModelImport/Controls/ModelImportDropAccessibilityTests.cs` at lines 142, 147, 152, 172, and 176.
- No diagnostic originates in an A1-changed production or test path.
- Exact proposed C0/F1 diff: correct these frontend nullable flows, then enable `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in the shared application project. Do not globally suppress the warnings.
- Owner/disposition: C0/F1; deferred, not edited by A1.

### R3 closure ledger

- R3-017: **fixed and closed by A1**. All three definitions have one live caller, all displaced duplicate authority is absent, and behavioral/structural tests are green.
- R3-018: **supported, not closed by A1**. The production-reachability seam is testable, but T1 download/Chat requirements remain externally owned.
- R3-008 and R3-014: F1/Q1-owned; no closure claim.
- R3-019, R3-020, and R3-022: externally owned; no closure claim.

## TDD record

The RED commit `24897c833873c38964a00f2c8235fdb0163b09a9` adds four tests while leaving R2 production unchanged. The authoritative packaged run discovered and executed all four: three failed because expected registration count 1 was actually 0, and the cancellation regression failed because no `OperationCanceledException` escaped. This was genuine behavioral/architectural RED, not compilation failure or zero discovery.

The identical four-test filter then passed 4/4 after the three minimal production corrections. The cancellation test uses the packaged static OpenVINO fixture to enter the real fallback compatibility path and a fresh-resource dependency that raises `OperationCanceledException`; this avoids relying on a native executable blocked by local Application Control while still exercising the live production delegate.

| Boundary | RED | Minimal GREEN correction |
|---|---|---|
| Compatibility registration | expected 1, actual 0 | one live orchestrator registration |
| Optimization registration | expected 1, actual 0 | one live factory registration |
| Official worker registration | expected 1, actual 0 | one live authority call; duplicate inventory removed |
| Dependency cancellation | no exception escaped | orchestrator preserves `OperationCanceledException` |

## Changes

| Commit | Paths | Purpose |
|---|---|---|
| `b8c7d983ffb4210330f1bc92126be33c9bb005b5` | R3 design | Bound the authorized production-reachability solution. |
| `662daa115f48202b1b34908840a13d27b0611110` | R3 implementation plan | Recorded TDD tasks and verification gates. |
| `24897c833873c38964a00f2c8235fdb0163b09a9` | two focused onboarding test files | Added genuine RED reachability and cancellation regressions. |
| `3c4e814884b53281235ccc0c20323797685c5475` | `OnboardingShellPage.xaml.cs` | Composed compatibility orchestration and removed three inline bodies. |
| `15fce5d5356a81d95cf1dc8a65bab48255fc9416` | `OnboardingShellPage.xaml.cs` | Composed the route-exact optimization factory and removed direct backend construction. |
| `5cb1a1c988c9bee3587d55a2d299fa819683b434` | `ModelInspectionServiceComposition.cs` | Composed the official worker authority and removed duplicate runtime facts. |

No `MainWindow`, navigation registration, solution/project/package registration, frontend presentation, Q1 result semantic, or M1 inspection-contract file was edited. The only shared-consumer proposals are the typed OpenVINO activation mapping and warning-gate correction stated above.

## Verification ledger

All final test rows ran against implementation subject `5cb1a1c988c9bee3587d55a2d299fa819683b434` / `90b0d2c79613443d6b0b80ff35686a86497ba076`. Times are UTC from TRX metadata. Raw TRX files remain ignored and local.

| Command/test | Start/end UTC | Discovered | Executed | Passed | Failed | Skipped | TRX SHA-256 |
|---|---|---:|---:|---:|---:|---:|---|
| Authoritative packaged R3 RED filter | 15:11:23 / 15:11:28 | 4 | 4 | 0 | 4 | 0 | `e93bb087b640bdfb72b8a78d1e851ee71e7316104adee0f0b4359ce6495c000d` |
| Identical packaged R3 GREEN filter | 15:25:38 / 15:25:41 | 4 | 4 | 4 | 0 | 0 | `3d5f20bb38b314810d5ff56ea04c795f228c0dfee9a070715b4188a70e944def` |
| Complete Model/Hardware Compatibility executable | 15:19:54 / 15:19:56 | 1,052 | 1,052 | 1,052 | 0 | 0 | `ebf891703829dd1388cf71d2aba6581553f2ec805ca986389b449e76fc3ac81c` |
| Complete cross-feature integration executable | 15:19:58 / 15:20:00 | 53 | 53 | 53 | 0 | 0 | `37cd5ce962aefd149f2f0e6d49386462a36558a7e2ed5a5541d7a04149856566` |
| Complete OpenVINO WorkerClient executable | 15:18:49 / 15:18:51 | 16 | 16 | 16 | 0 | 0 | `978ee0b85e62a3816d47f5f506e03c595f74823227c39758583191f4850e7b5a` |
| Packaged onboarding/optimization affected filter | 15:25:48 / 15:25:51 | 22 | 22 | 22 | 0 | 0 | `56e0f7ca55298cd00b76518bd2efd562d946b7147490d7395322e8c0a3292242` |
| Packaged Model Inspection service/composition filter | 15:26:46 / 15:26:48 | 5 | 5 | 5 | 0 | 0 | `c993514cc32495d2a4bc1530fe9387172ee95a0f0758875e9013d1e063ba7b46` |
| Complete Model Inspection contracts executable | 15:24:44 / 15:25:12 | 357 | 357 | 335 | 22 | 0 | `e0d33c9c6a46fa8981e5b0ca51bfa56b43e56b8ad96136f0607269c95fe8a4fc` |

Final verification totals exclude the intentionally failing RED row and include every final GREEN/gate invocation: discovered 1,509; executed 1,509; passed 1,487; failed 22; skipped 0. Arithmetic is exact: `1,509 = 1,487 + 22 + 0`, and discovered equals executed. The 22 failures match the C0-reported Model Inspection baseline and concern absent M1/H1 workflows, stale cleanup/fixture inventory, fixture build ownership, and an unresolved packaging item expression. A pinned-SDK rerun removed four local environment failures from the first diagnostic attempt and reproduced C0's exact 335/22 result.

An exploratory package filter over the entire Model Inspection frontend namespace executed 388 tests, passing 353 and failing 35 F1-owned visual/layout assertions. It is not an affected-backend acceptance row and is not included in receipt totals. It was not hidden, suppressed, whitelisted, or used to narrow an existing test; the required service/composition filter above is the planned backend boundary.

## Build, package, and scan results

- Release x64 app/unit build with native producer package requirements explicitly disabled: success, 0 errors, 14 warning emissions representing seven unique inherited frontend nullable warnings; zero warning in changed A1 paths. It generated the packaged unit recipe used by VSTest.
- Release x64 production package build with package requirements enabled: fail-closed with one error because `GgufQuantizerStageDirectory` is absent. A1 did not invent or trust an unvalidated stage. No successful native/package acceptance claim is made.
- The first pinned-SDK no-restore attempt failed `NETSDK1112` because restore assets came from a different SDK. A restore-enabled rerun succeeded and is the authoritative build result.
- `git diff --check` against the R3 base: zero findings.
- Production registration counts: compatibility orchestrator 1; optimization factory 1; official worker authority call 1.
- Production definition counts: one for each A1 authority.
- Compatibility delegates: two `EvaluateAuthorityAsync`, one `EvaluateBoundAsync`.
- Displaced direct optimization constructions in `OnboardingShellPage`: 0.
- `OfficialBinaryMachines` and direct official worker installation construction in production: 0.
- Changed-diff private user path, username, hostname, private-key, credential/secret-assignment scans: 0 findings.
- No new facade, service locator, backend implementation, competing production registration, ambient deterministic timestamp, or package trust root was added.

## Reproduction commands

Run the standalone test executables with `--minimum-expected-tests`, `--report-trx`, and a git-ignored results directory. Run the generated x64 Release `.build.appxrecipe` with Visual Studio VSTest and these filters:

```text
FullyQualifiedName~A1BackendProductionReachabilityTests|FullyQualifiedName~CompatibilityEvaluation_DoesNotConvertDependencyCancellationToFallback
FullyQualifiedName~Features.Onboarding|FullyQualifiedName~OptimizationJourney|FullyQualifiedName~OptimizationBackend|FullyQualifiedName~GgufOptimizationExecutor
FullyQualifiedName~GraniteEdgeAI.UnitTests.ModelInspectionServiceTests|FullyQualifiedName~OfficialWorkerAuthority_HasOneRegistrationAndNoDuplicateInventory
```

Complete standalone projects are Model/Hardware Compatibility, OpenVINO WorkerClient, Model Inspection Contracts, and CrossFeature Integration. The Release x64 app/unit build uses `GenerateAppxPackageOnBuild=false`, disables only unavailable external native producer package requirements, and does not disable any test. The separate package-gate command leaves those requirements enabled and must fail closed when the validated stages are absent.

## Evidence and privacy

Raw TRX, build output, and machine-policy diagnostics remain uncommitted beneath the ignored audit-results root. No report or committed test contains a username, hostname, absolute evidence/worktree path, imported-model filename/path, provider output, model weights, prompt, credential, token, or raw exception transcript. The static OpenVINO test fixture is already packaged test content and contains no user model data.

A1's handoff receipt must set `schemaVersion` to 1, `evidenceManifest` to `null`, and `nativeDisposition` to `not-applicable`. No native model, converter, quantizer, optimization worker, or Chat acceptance stage was authorized or executed.

## Remaining work and nonclaims

- M1/H1/C0 still own the 22 known Model Inspection contract failures. R3 neither changes nor claims those contracts.
- A validated GGUF quantizer stage and the other producer package inputs remain prerequisites for a successful production package build. The missing stage is an external blocker, not a reason to weaken the package gate.
- Q1/M1/C0 still own typed OpenVINO activation results and exact result-to-Chat/export binding. Q1 owns the redundant OpenVINO staging decision.
- C0/F1 own the inherited frontend nullable and visual/layout failures and the eventual warnings-as-errors gate.
- R3 managed/packaged unit results do not prove real native binaries, model quality, hardware performance, package signing, complete GUI E2E, Chat/export acceptance, or native cleanup.
- C0 must independently verify the pushed tip/tree, ancestry, committed report bytes, remote equality, receipt schema/arithmetic, and collision-free selective integration. Main was not edited, checked out, merged, or pushed by A1.
