# A1 R3 Production Reachability Design

## Status and authority

This design closes A1-owned issue R3-017 on the validated A1 R2 base:

- Base commit: `66e2a4a5a4509962cb3faee410c69c5d764e0854`
- Base tree: `dd4c9eef755e126ee6fab3a901fec56ddc267ab5`
- Branch: `audit/ucl-a1-remediation-r3`
- C0 ancestor: `a5ef3558334e50587889140dafba194853938765`

The user authorized three narrowly scoped edits at previously C0-owned composition points. A1 supports R3-018 by providing a production-reachability test seam, but does not own or claim closure of T1 download/Chat requirements. R3-008 and R3-014 remain F1/Q1-owned.

## Problem

A1 R2 introduced three tested production components that have no live production callers:

1. `CompatibilityEvaluationOrchestrator` owns fresh-resource capture, injected evaluation time, cancellation preservation, and fail-closed fallback, but `OnboardingShellPage` still contains three inline copies of that behavior.
2. `OptimizationBackendCompositionFactory` owns exact route-to-executor/revalidator/context composition, but `OnboardingShellPage.NavigateToOptimization` still constructs those objects directly.
3. `OpenVinoOfficialWorkerAuthority` owns the validated official worker protocol, build evidence, executable, and machine inventory, but `ModelInspectionServiceComposition` still duplicates those values.

Definitions plus tests are not production reachability. Each component must have exactly one real production composition call, and the displaced inline authority must be removed.

## Considered approaches

### Selected: direct composition at the existing call sites

Instantiate and call each existing A1 component where the current inline implementation lives. This is the smallest change, preserves current dependency direction, and removes duplicated authority. It creates no new facade, service locator, registration framework, or public contract.

### Rejected: add a new onboarding backend facade

A facade would merely introduce a fourth uncalled production type until the shell consumed it. It would also combine compatibility, optimization, and worker-package concerns that have separate natural owners.

### Rejected: delete the unused A1 components

Deletion would make the reachability scan green without correcting the oversized inline composition or duplicate package authority. That would evade rather than close R3-017.

## Production composition

### Compatibility evaluation

Definition:

- `Features/ModelHardwareCompatibility/Application/CompatibilityEvaluationOrchestrator.cs`

Real caller and registration point:

- `Features/Onboarding/OnboardingShellPage.xaml.cs`, inside `NavigateToCompatibilityAsync` after route inputs are prepared.

The shell constructs one orchestrator per compatibility navigation from the existing `_compatibilityFreshResourcesSource` and `TimeProvider.System`. The GGUF and OpenVINO production branches call `EvaluateAuthorityAsync`. The non-production-authority branch calls `EvaluateBoundAsync` with the existing route-specific `TryBindFresh` operation.

The three inline capture/`Task.Run`/catch/fallback bodies are removed. Cancellation is never converted to a fallback result. All other failures still produce the existing fail-closed `NotEstablished` projection through `CompatibilityFallbackPolicy`.

Behavioral regression:

- An onboarding compatibility test supplies a fresh-resource source that throws `OperationCanceledException` while the caller token is not already cancelled. On R2, the broad inline catch converts it to a fallback screen. On R3, the production orchestrator preserves cancellation and the test observes the exception.

### Optimization backend composition

Definition:

- `Features/ModelOptimization/Application/OptimizationBackendCompositionFactory.cs`

Real caller and registration point:

- `Features/Onboarding/OnboardingShellPage.xaml.cs`, inside `NavigateToOptimization`.

The shell retains the existing app-root calculation, constructs the factory from the current source-custody registry and active GGUF/OpenVINO authorities/service, and calls `TryCreate`. A false result retains the existing fail-closed return. A successful result supplies the coordinator, context factory, output registry, and executor already expected by the shell. `_activeOpenVinoExecutor` is assigned only when the returned executor is an `OpenVinoOptimizationExecutor`.

The direct GGUF/OpenVINO runner, executor, revalidator, context-factory, output-registry, router, and coordinator construction is removed from the shell. Route implementations, storage paths, timeout, source custody, hashes, revalidation, cancellation, and cleanup behavior are unchanged.

Behavioral regression coverage:

- Existing `OptimizationBackendCompositionFactoryTests` execute exact GGUF/OpenVINO selection, missing-authority refusal, duplicate authority rejection, and route mismatch rejection.
- Existing onboarding journey tests are run after wiring to prove the production shell remains behaviorally compatible.

### Official OpenVINO worker authority

Definition:

- `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/OpenVinoOfficialWorkerAuthority.cs`

Real caller and registration point:

- `Features/ModelInspection/Services/ModelInspectionServiceComposition.cs`, inside `CreateOpenVinoRouteService`.

The composition root keeps its existing packaged-manifest byte hashing and exact comparison with assembly-approved metadata. After that comparison, it calls `OpenVinoOfficialWorkerAuthority.CreateInstallation(workerRoot, expectedManifestDigest)` and passes `installation.ExpectedBuildEvidence` to `OpenVinoRouteService`.

The local build literals, direct `OpenVinoWorkerInstallation` construction, and `OfficialBinaryMachines` map are removed. Closure resolution remains responsible for byte count, hashes, topology, and PE-machine checks. No trust root, manifest pin, protocol, package path, or validation order is weakened.

Behavioral regression coverage:

- Existing `OpenVinoOfficialWorkerAuthorityTests` execute exact build/protocol/inventory validation, immutability, manifest binding, and invalid-input rejection.
- Affected Model Inspection and worker-client suites are run after composition.

## Reachability fitness test

R3-017 is an architectural defect: the intended runtime behavior is deliberately equivalent, while the missing fact is the production caller itself. A focused `A1BackendProductionReachabilityTests` test therefore reads only the three approved composition files and asserts:

- exactly one `CompatibilityEvaluationOrchestrator` construction exists in the onboarding composition;
- exactly one `OptimizationBackendCompositionFactory` construction exists in the onboarding composition;
- exactly one `OpenVinoOfficialWorkerAuthority.CreateInstallation` call exists in inspection composition;
- the displaced `OfficialBinaryMachines` member and direct official installation construction no longer exist in inspection composition.

This structural fitness test supplements rather than replaces behavioral tests. It must fail on the unmodified R2 base and pass after the production edits. Repository-wide final scans independently confirm exactly one production registration for each component.

## Error, security, and privacy invariants

- Exact manifest digest comparison occurs before official installation construction.
- Existing worker closure byte-count, hash, topology, PE-machine, protocol, timeout, cancellation, and cleanup checks remain in their current owners.
- Compatibility continues to fail closed and never exposes exception details or paths.
- Optimization continues to refuse absent or mismatched route authority without cross-route fallback.
- No recursive traversal, export destination, output publication, original-model mutation, or native execution behavior changes in this branch.
- Tests and committed evidence contain no username, hostname, absolute local path, filename from user input, provider output, credential, token, prompt, or model data.

## Verification strategy

1. Verify base commit/tree, C0 ancestry, clean isolated worktree, available disk, SDK/runtime, buildability, package-stage availability, and native-lock status.
2. Add the reachability and cancellation tests and run them on unchanged production code to retain RED evidence.
3. Make the three minimal production edits.
4. Run the identical focused tests GREEN.
5. Run complete affected WorkerClient, Model/Hardware Compatibility, app unit/packaged, Model Inspection contract, and cross-feature suites with non-zero discovery.
6. Build all changed production projects and the x64 app package configuration; report warnings without suppressing them.
7. Run `git diff --check`, duplicate-definition/registration scans, changed-path checks, private-path/secret scans, and ambient-time scans.
8. Record exact tested commit/tree, reproducible committed evidence, report hash/bytes, pushed remote ref, clean state, and schema-valid R3 receipt. Native disposition remains non-applicable unless a separately authorized native stage is actually executed.

## Completion boundary

R3-017 is closed only when all three production callers are present exactly once, RED/GREEN evidence is fresh, affected suites pass, the branch is clean and pushed, and the receipt binds the tested committed bytes. A1 makes no closure claim for R3-008, R3-014, R3-018, R3-019, R3-020, or R3-022.
