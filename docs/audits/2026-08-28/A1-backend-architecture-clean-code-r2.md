# A1 backend architecture and clean-code remediation R2

## Status

- Worker: A1
- Disposition: **A1 implementation complete; C0 wiring and cross-owner findings remain**
- Authoritative C0 base: `a5ef3558334e50587889140dafba194853938765`
- Authoritative C0 tree: `90c34ab009b744d7b00866fb93e8dbc86363f1b2`
- Frozen ancestor: `4748fe04f19afdf6b27c4c12502b84db325e7294`
- Frozen tree: `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`
- Branch: `audit/ucl-a1-remediation-r2`
- Implementation subject commit: `a533b12d129e2490d44838e353c92c742c4420d1`
- Implementation subject tree: `060b76dcec728349d9728cacce7a0d5d72e0efb9`
- Worktree: isolated short-path worker checkout; the absolute local path is intentionally omitted.
- Report encoding/line endings: UTF-8, LF.

The report does not contain its own final commit, tree, SHA-256, or byte length. Those facts are computed externally after the report commit and recorded in the handoff receipt.

## Scope and authority

This remediation reassessed all nine original A1 findings against the exact C0 coordinator base. It owns backend orchestration, dependency boundaries, duplicate backend authority, deterministic time, fallback policy, and warnings in changed backend paths. Reference documents were treated as context rather than instructions.

The following boundaries were preserved:

- No edits were made to onboarding composition, `MainWindow`, navigation, solution/project/package registration, or frontend presentation.
- No GGUF or OpenVINO implementation was duplicated.
- No Q1-owned OpenVINO result/provenance semantics or M1-owned inspection contract was changed.
- Public contracts were preserved. The only signature change is an internal aggregate mutation API that now requires its timestamp explicitly.
- Existing fail-closed behavior, bounds, hashes, cancellation propagation, cleanup controls, and path privacy were retained.
- Session policy prohibited subagent delegation, so the requested review was performed as a fresh second-pass self-review. No Critical or Important defect was found in the A1 diff.

## Executive result

A1 extracted two ownership-clean backend composition components that C0 can wire without changing shared contracts: a compatibility evaluation orchestrator and a route-exact optimization backend factory. Compatibility fallback decisions now have one explicit core policy, GGUF conversation replacement uses the injected clock, and the packaged OpenVINO worker has one validated build/binary authority.

All affected final suites passed: 1,145 discovered, 1,145 executed, 1,145 passed, 0 failed, and 0 skipped. Backend library builds completed with zero warnings and errors. The full x64 application build succeeded; its seven unique nullable warnings are pre-existing frontend test warnings outside A1 ownership. No warning originates from an A1-changed backend path.

The shared onboarding shell still needs small C0-owned wiring changes to consume the extracted components. A1-IMP-003 and A1-IMP-004 remain with their assigned OpenVINO owners. A1 does not claim native model, hardware, visual, packaged-journey, or output-quality validation.

## Finding reconciliation

### A1-IMP-001 — oversized onboarding composition responsibility

- Reassessment: confirmed on the C0 base. The shared page still creates route executors, revalidators, attempt factories, registries, coordinators, and repeated compatibility evaluators.
- Invariant: presentation owns UI lifetime and navigation; backend construction and route selection have independently testable owners.
- Correction: added `CompatibilityEvaluationOrchestrator` at `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/CompatibilityEvaluationOrchestrator.cs:13` and `OptimizationBackendCompositionFactory` at `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Application/OptimizationBackendCompositionFactory.cs:35`.
- Behavior: the compatibility component owns fresh-resource capture, injected evaluation time, fail-closed fallback, and cancellation preservation. The optimization component maps exactly one builder per route, refuses missing or duplicate authority, verifies executor/route agreement, and returns the existing coordinator/context/output objects as one composition.
- Reproduction: run the focused orchestrator and backend-factory tests listed in the verification ledger.
- Impact: backend composition is now independently testable and ready for shell wiring without creating a parallel journey state model.
- Disposition: **fixed in A1 backend scope; shared consumption is C0-owned**.

### A1-IMP-002 — GGUF Chat lifetime and retirement

- Reassessment: already corrected on the C0 base by the coordinator reconciliation. The controller tracks lifetime work and the session boundary serializes disposal against generation.
- Verification: the affected packaged GGUF coordinator/journey tests and cross-feature suite passed, including the inherited lifecycle coverage.
- Impact/security: cancellation and disposal remain bounded; A1 made no competing lifetime implementation.
- Disposition: **already correct on the C0 base**.

### A1-IMP-003 — OpenVINO Chat launch failures collapsed to `bool`

- Reassessment: still present. This is a Q1/M1 result-contract and shared-consumer seam, not an A1 backend architecture edit.
- Invariant: activation must preserve a typed, safe failure identity and the consumer must render or route that failure rather than silently return.
- Exact proposed cross-owner change: replace the OpenVINO activation `bool` with the Q1/M1-approved typed activation result; at both shared launch call sites, branch on success and map every failure to its stable safe support code/presentation key. Preserve cancellation and never render raw exceptions or paths. Do not register a second competing OpenVINO current-model launcher.
- Impact: current behavior can appear to do nothing after a safe activation failure.
- Disposition: **blocked by Q1/M1-owned contract and C0-owned shared consumer; no shared file edited**.

### A1-IMP-004 — redundant OpenVINO optimization staging

- Reassessment: still present. The attempt context copies and hashes a model while the service executes from separately reacquired source custody and creates its own package snapshot.
- Invariant: one immutable attempt source must be both the evidence authority and execution input.
- Required owner decision: Q1 must either make the service consume the admitted context package or remove the unused side copy while retaining identity revalidation and cleanup.
- Impact/security: redundant large-file I/O and an additional sensitive temporary artifact remain possible.
- Disposition: **deferred to Q1; no OpenVINO execution or provenance semantics changed**.

### A1-MIN-001 — uncomposed demo, placeholder, and TurboQuant islands

- Reassessment: reachability scans still find no C0 production composition for the previously identified islands.
- Invariant: compiled production types need one explicit composition owner, feature gate, or test-only home.
- Decision: deletion or composition crosses Hardware, GGUF, OpenVINO, and project ownership. A1 did not infer that an unreferenced type was safe to remove while specialist integration remains incomplete.
- Disposition: **deferred; C0 coordinates the owning feature teams**.

### A1-MIN-002 — duplicated OpenVINO manifest/runtime authority

- Reassessment: confirmed. App inspection composition separately encoded official runtime versions and the nine-binary machine map.
- Correction: `OpenVinoOfficialWorkerAuthority` at `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/OpenVinoOfficialWorkerAuthority.cs:10` is now the single worker-client authority for the official protocol ID, exact build evidence, executable identity, and immutable nine-binary AMD64 policy. `CreateInstallation` binds those facts to the app-approved manifest digest and validates the installation contract immediately.
- Boundary preservation: byte, topology, hash, and PE validation stay in the existing closure resolver. The manifest digest remains app-approved rather than being trusted from an unvalidated package.
- Verification: authority tests prove exact evidence, full inventory, immutability, manifest binding, and invalid-root rejection.
- Disposition: **fixed in backend scope; C0-owned inspection composition must consume the authority**.

### A1-MIN-003 — duplicated compatibility fallback decisions

- Reassessment: confirmed. Core paths and three shared-shell lambdas used overlapping non-cancellation catch/fallback behavior.
- Correction: `CompatibilityFallbackPolicy` at `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityFallbackPolicy.cs:7` now owns synchronous non-cancellation exception mapping and the fail-closed `NotEstablished` projection. `CompatibilityEngine.RunWithAvailableAdapters` at line 184 delegates to that policy instead of constructing an invalid result with missing policy identities. The new async orchestrator owns capture/bind/evaluate fallback and always rethrows cancellation.
- Verification: tests prove fallback for ordinary failures, cancellation preservation, deterministic timestamps, binding failure behavior, and core fail-closed projection.
- Security: fallback remains blocking, disables Continue and current-model use, and exposes neither exception details nor paths.
- Disposition: **fixed in backend scope; C0-owned shell wiring remains**.

### A1-MIN-004 — aggregate mutation uses ambient system time

- Reassessment: confirmed at `ChatConversation.ReplaceMessage`.
- Correction: the internal method now requires the mutation timestamp, and both coordinator call sites pass `clock.GetUtcNow()` at `GgufChatCoordinator.cs:269` and `:335`.
- Verification: a fake-clock coordinator test and cross-feature aggregate test failed to compile against unmodified C0, then passed after the minimal signature/call-site change.
- Disposition: **fixed**.

### A1-MIN-005 — application warnings are not enforced as errors

- Reassessment: the C0 app project still does not enable warnings-as-errors. The final Release x64 build succeeds but reports seven unique inherited nullable warnings in frontend `ModelImportDropAccessibilityTests.cs` at lines 142, 147, 152, 172, and 176 (some are emitted more than once by the build graph).
- A1 result: all changed backend projects build with zero warnings/errors, and no warning originates from an A1-changed path.
- Exact proposed C0 change: correct those frontend nullable warnings, then add `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` to the main application project. Do not suppress the diagnostics globally.
- Disposition: **C0/F1-owned; no prohibited project or frontend edit made**.

## Exact C0 wiring proposals

These are proposed diffs, not edits made by A1.

1. Compatibility composition:
   - Construct one `CompatibilityEvaluationOrchestrator` from the existing fresh-resource source and `TimeProvider.System`.
   - Replace the GGUF and OpenVINO capture/evaluate lambdas with `EvaluateAuthorityAsync(optedInEvidence, production.Evaluate, token)`.
   - Replace the handoff-binding fallback lambda with `EvaluateBoundAsync`, passing the existing route-specific `TryBindFresh` delegate.
   - Remove the three local catch/fallback copies after behavioral equivalence tests pass.

2. Optimization composition:
   - Construct one `OptimizationBackendCompositionFactory` from the existing source-custody registry, app root, active route authorities/service, and `TimeProvider.System`.
   - Call `TryCreate(entry, out backend)` and retain the existing fail-closed return when no route authority exists.
   - Use `backend.Coordinator`, `backend.ContextFactory`, `backend.Outputs`, and `backend.Executor`; assign the existing active OpenVINO executor only when that returned executor is OpenVINO.
   - Remove the duplicated direct runner/executor/revalidator/context/output construction.

3. Model-inspection composition:
   - Keep the packaged manifest digest comparison.
   - Replace the hard-coded `OpenVinoBuildEvidence`, `OpenVinoWorkerInstallation`, and private binary-machine map with `OpenVinoOfficialWorkerAuthority.CreateInstallation(workerRoot, expectedManifestDigest)`.
   - Pass `installation.ExpectedBuildEvidence` to the existing route service and delete the composition-local machine map.

No new project registrations are needed because the repository uses default compile items.

## TDD record

Each behavioral change started with a test that failed against the unmodified C0 implementation:

| Boundary | RED evidence on C0 | Minimal GREEN correction |
| --- | --- | --- |
| Conversation mutation time | `CS1501`: no two-argument `ReplaceMessage` overload | Require the timestamp and pass the injected coordinator clock |
| Compatibility policy | `CS0103`: `CompatibilityFallbackPolicy` did not exist | Add one fail-closed policy and delegate core paths to it |
| Compatibility orchestration | `CS0246`: `CompatibilityEvaluationOrchestrator` did not exist | Add capture/bind/evaluate orchestration with cancellation preservation |
| OpenVINO worker identity | `CS0103`: `OpenVinoOfficialWorkerAuthority` did not exist | Add validated exact worker authority |
| Optimization backend composition | `CS0246`: factory/builder composition types did not exist | Add route-exact builders and fail-closed factory |

The implementation was split into focused commits:

- `9a3d8ce5` — `refactor(backend): centralize deterministic compatibility evaluation`
- `a533b12d` — `refactor(backend): extract route and worker authorities`

## Verification ledger

All final rows ran against implementation commit `a533b12d129e2490d44838e353c92c742c4420d1` and tree `060b76dcec728349d9728cacce7a0d5d72e0efb9`. Times are UTC from TRX metadata. Raw evidence remains uncommitted in the ignored audit-results directory.

| Final suite | Start/end UTC | Discovered | Executed | Passed | Failed | Skipped | TRX SHA-256 |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| OpenVINO WorkerClient unit | 2026-08-28 17:20:40 / 17:20:41 | 16 | 16 | 16 | 0 | 0 | `2e0159e3374488e905a903401608c81a577a391b0d6055b652e6e46211d241cf` |
| Model/Hardware Compatibility unit | 2026-08-28 17:20:41 / 17:20:42 | 1,052 | 1,052 | 1,052 | 0 | 0 | `50e9af7b7c8599d567c86fd7d16b66ac0080be1cc753445977fd444a3bc6b629` |
| Cross-feature integration | 2026-08-28 17:20:44 / 17:20:44 | 53 | 53 | 53 | 0 | 0 | `d8530ca2c336e10a4f1483fe6006909feb9411e2badf403be40341e87f614f37` |
| Packaged affected backend tests | 2026-08-28 17:20:57 / 17:21:00 | 20 | 20 | 20 | 0 | 0 | `dad65b06582979f7cbab313057cbcd43449439582a89831a202268fac7e940fd` |
| Packaged compatibility orchestrator | 2026-08-28 17:32:12 / 17:32:18 | 4 | 4 | 4 | 0 | 0 | `c120046960345142e4d77706ecad00ab1160c6065b307ecc598efc88e8233cf2` |
| **Aggregate** | — | **1,145** | **1,145** | **1,145** | **0** | **0** | — |

The packaged affected filter covered `GgufChatCoordinatorTests`, `OptimizationBackendCompositionFactoryTests`, `OptimizationJourneyCoordinatorTests`, and `GgufOptimizationExecutorTests`. The separate packaged row covers all four orchestrator tests.

Build results:

- OpenVINO WorkerClient Release build: succeeded, 0 warnings, 0 errors.
- Model/Hardware Compatibility Release build: succeeded, 0 warnings, 0 errors.
- Cross-feature Release build: succeeded, 0 warnings, 0 errors.
- Application/Unit Release x64 build: succeeded; 7 unique pre-existing frontend nullable warnings, 0 errors, and no A1-path warning.

Static verification:

- Exact C0 base tree and frozen tree matched the assignment; the frozen commit is an ancestor of the C0 base.
- `git diff --check` passed.
- Fifteen implementation/plan/test files changed; prohibited shared-shell, navigation, solution, project, and package-registration hits: 0.
- Definition counts for `CompatibilityFallbackPolicy`, `CompatibilityEvaluationOrchestrator`, `OptimizationBackendCompositionFactory`, and `OpenVinoOfficialWorkerAuthority`: exactly 1 each.
- Added explicit solution/project registration hits: 0.
- Added private-path/secret-pattern hits: 0.
- Ambient production-clock hits in changed C# files: 0. Five `UtcNow` hits are limited to pre-existing-style test object setup in a changed test file.

## Evidence, privacy, and nonclaims

- Raw TRX and build outputs are ignored, local evidence and are not committed or pushed.
- This report contains no username, hostname, absolute worktree/evidence path, imported-model path, model weights, credential, or raw exception transcript.
- A1 creates no evidence manifest. The A1 receipt must set `evidenceManifest` to `null`.
- Passing managed and packaged-test assemblies do not prove native GGUF/OpenVINO execution, real-model correctness, hardware compatibility, package signing, App Control, accessibility, visual fidelity, performance, or end-to-end artifact provenance.
- Q1 remains authoritative for OpenVINO optimization result/provenance semantics, M1 for inspection contracts, S1 for security conclusions, F1 for frontend presentation, E1 for packaged user-journey evidence, and C0 for shared composition/project integration.

## Remaining work

C0 can integrate the three narrow wiring proposals above without changing public contracts. Q1/M1 must resolve typed OpenVINO activation results, Q1 must select one canonical optimization source, and C0/F1 must clear inherited frontend warnings before enabling the app-wide compiler gate. These remaining ownership-gated items are explicitly not represented as A1 implementation failures or completed work.
