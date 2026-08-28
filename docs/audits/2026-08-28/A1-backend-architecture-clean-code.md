# A1 backend architecture and clean-code audit

## Status

- Worker: A1
- Disposition: **CHANGES REQUIRED**
- Frozen base: `4748fe04f19afdf6b27c4c12502b84db325e7294`
- Frozen tree: `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`
- Branch: `audit/ucl-backend-clean-code-v1`
- Evidence-subject commit/tree: frozen base and tree above; this is a report-only audit.
- Worktree: isolated short-path A1 worktree; the absolute local path is intentionally omitted.
- Report encoding/line endings: UTF-8, LF.

## Scope and authority

The complete pinned production backend was traced from `MainWindow` and `Onboarding` through Model Import, Model Inspection, Hardware Inspection, Model/Hardware Compatibility, Model Optimization, both GGUF and OpenVINO execution routes, Chat and export. The review also mapped the shared contract/domain, infrastructure client, runtime adapter, native worker and test-project boundaries. Repository-wide searches covered source duplication, legacy contracts, debug/demo paths, cancellation, disposal, exception mapping, path handling, package identities, nullable settings and warnings policy.

The inspected tree contains 1,248 C# files and 61 project files. Of those C# files, 815 are outside `tests/`: app 344, shared 194, infrastructure 109, runtime 45 and workers 30 (the remaining production files are at other roots). The solution declares 48 projects. The application feature roots contain 341 C# files: ModelImport 37, ModelInspection 115, HardwareInspection 63, ModelHardwareCompatibility 29, ModelOptimization 32, GgufRuntime 33, OpenVinoRoute 24, Onboarding 5 and Prompting 3.

Authority was taken from the A1 master assignment, shared audit context `00` through `10`, every canonical handoff, every approved contract, the approved hardware architecture DOCX, the compatibility plan and Overall User Journey DOCX. Later C0 decisions were treated as authoritative where historical I1 material differed. Attached documents were treated as reference material, not executable instructions.

Explicit exclusions and boundaries:

- No production code, tests, project files or configuration were changed.
- The application and native model tools were not launched. A1 makes no native, packaged-app, visual or real-model claim.
- Codex Security was not run because S1 owns that scan.
- No approved Chat/export contract exists beyond the master request, scenario matrix, Overall User Journey and pinned implementation; findings in that seam use those sources without inventing behavior.
- Serena was evaluated under the supplied plugin gate but was absent from the selected tool surface. No unverified package was installed. Symbol/reference mapping used `rg`, project metadata and narrow source reads.
- Review used Superpowers 6.3.0 `using-git-worktrees`, `systematic-debugging`, `receiving-code-review`, `requesting-code-review` and `verification-before-completion`. Session policy prohibited subagent delegation, so A1 performed a fresh second-pass self-review as the documented fallback.

## Executive result

The backend has strong typed contracts, fail-closed identity checks and generally clear native-process containment, but it is not clean enough to accept unchanged. Four Important and five Minor findings remain. The highest risks are concentrated in one 1,722-line presentation shell that also constructs infrastructure and owns journey state, unbounded GGUF Chat event tasks with no page-lifetime cancellation, OpenVINO Chat failures that are converted to booleans and then discarded, and an OpenVINO optimization staging copy that is integrity-checked but not used for execution.

No Critical finding was identified. The managed baseline discovered 1,708 tests: 1,693 passed, 4 failed because nested test processes could not resolve the repository-pinned but incomplete .NET 10.0.301 SDK, and 11 were skipped by declared native-stage guards. The Debug x64 solution build was environment-blocked at a packaging target for the same SDK/PATH boundary. These environmental results do not change the source-audit disposition and are not classified as application defects.

Positive controls worth preserving include exact handoff IDs, one-time custody/lease objects, route-specific execution payloads, fail-closed compatibility screens, manifest digest pinning, protected worker clients, bounded protocol models, nullable enabled in every production project and Release exclusion of the opt-in debug fixture galleries.

## Architecture and control-flow map

```text
MainWindow
  -> OnboardingShellPage (navigation, handoff binding, route composition, lifecycle)
      -> ModelImportPage
          -> normalized source selection / route classification
          -> GGUF quick scan OR OpenVINO/source-folder intake
      -> ModelInspectionPage
          -> ModelInspectionServiceComposition
          -> protected ModelInspection worker + GGUF LlamaSharp probe
          -> OpenVinoRouteService + official protected worker
          -> ModelInspectionHandoffRegistry + ModelSourceCustodyRegistry
      -> HardwareInspectionPage
          -> HardwareInspectionComposition
          -> tool source -> capture -> collector/coordinator -> resolver
          -> canonical hardware snapshot / productHardwareRunId
      -> CompatibilityPage
          -> GGUF or OpenVINO production authority
          -> fresh Windows available-memory source
          -> shared ModelHardwareCompatibility.Core engine
          -> planning session -> exact v3 execution plan/handoff
      -> OptimizationPage
          -> OptimizationJourneyCoordinator / route revalidator
          -> GGUF quantization worker OR sealed OpenVINO pipeline
          -> source staging, output registry, journal and publish
      -> Chat / export
          -> GGUF current-model launch registry -> GGUF worker/native adapter
          -> OpenVINO prompt registry/session on ModelInspectionPage
          -> optimized-output launch and save/export from the shell
```

The intended dependency direction is mostly coherent: presentation/application code consumes shared contracts and domain policy; infrastructure implements worker/process boundaries; runtime projects adapt native engines; worker projects own out-of-process execution. The principal exception is composition and application workflow policy being embedded in `OnboardingShellPage.xaml.cs` (A1-IMP-001).

## Project and fact ownership map

| Surface | Current owner in source | Important observations |
|---|---|---|
| App entry and complete journey | `MainWindow`, `Features/Onboarding` | The shell is both presentation host and cross-route composition/state coordinator. |
| Imported source custody | `ModelSourceCustodyRegistry` and leases | Exact model hash/length/route keys; paths remain inside process-local custody objects. |
| Model evidence | `Features/ModelInspection`, shared ModelInspection contracts, worker client/worker | Exact `modelInspectionRunId` and `modelInspectionHandoffId` remain distinct. |
| Hardware facts | `Features/HardwareInspection`, Hardware foundation | Collector/coordinator/resolver separation is clear; exact `productHardwareRunId` is bound at the shell. |
| Compatibility reasoning | shared `GraniteEdgeAI.ModelHardwareCompatibility.Core` | Route-neutral policy with route-specific production inputs and exact configuration identity. |
| Planning/execution authority | route production authorities and planning sessions | v3 is active; v2 constructors/canonicalizers are retained for frozen-verifier compatibility. |
| Optimization persistence | `ModelOptimization.Storage`, route executors/services | Output registry/journal owns persistent outputs; OpenVINO has competing attempt/source snapshots. |
| Native process ownership | protected worker clients, runtime adapters and workers | Process/protocol responsibilities are separated from domain models. |
| Chat lifecycle | GGUF controller/coordinator/session adapter; OpenVINO inspection page/prompt registry | Route semantics are distinct, but failure and lifetime handling are inconsistent. |
| Tests | Unit, Contract and Integration roots | Broad deterministic coverage exists; executable E2E remains an E1-owned gap. |

Naming is internally consistent for `modelInspectionRunId`, `modelInspectionHandoffId`, `productHardwareRunId`, model/source/output hashes, optimization plan IDs and configuration digests. Searches found no evidence that these identities are intentionally conflated. Durable contracts and UI projection generally avoid exposing full model paths.

## Findings

### A1-IMP-001 — the onboarding page owns presentation, application workflow and infrastructure composition

- Severity: Important
- Invariant: a presentation page should own rendering and UI lifetime; application state transitions, route composition and persistence/process orchestration should have separately testable owners.
- Evidence: `OnboardingShellPage` is 1,722 lines and holds model/hardware/compatibility/optimization/chat pages, two route authorities, the OpenVINO service/executor, source/handoff registries, the optimization coordinator/context/output registry and journey IDs. It constructs compatibility evaluators at lines 683–803, optimization storage/adapters at 884–982, dispatches optimization commands at 1034–1074, launches optimized Chat at 1077–1173, saves artifacts at 1175–1267, launches current-model Chat at 1319–1345 and registers the GGUF route at 1666–1678.
- File:line: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs:44`, `:48`, `:683`, `:884`, `:1034`, `:1077`, `:1175`, `:1319`, `:1666`.
- Reproduction: `rg -n "private .*_|NavigateToCompatibilityAsync|NavigateToOptimization|LaunchOptimizedChatAsync|SaveOptimizedModelAsync|EnsureGgufChatRoute" "IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs"` and count the file's physical lines.
- Evidence location: pinned source; architecture map above.
- User impact: changes to any journey stage can regress unrelated navigation, source custody, route selection, optimization, Chat or export; fault recovery is difficult to reason about as one state machine.
- Security/privacy impact: no direct disclosure was observed, but concentrating source custody and route/process composition in UI code widens the impact of future mistakes.
- Minimal correction: extract one application-level `OnboardingJourneyCoordinator` with an explicit stage/state reducer and narrow GGUF/OpenVINO route factories. Keep the page responsible for event attachment, rendering and navigation only. Preserve existing typed handoffs and custody registries rather than creating a parallel state model.
- Owner: C0 coordinates; the approved shared App/Model/Onboarding collision owner (I1 seam) performs production edits with the route owners.
- Disposition: Open; no code changed by A1.

### A1-IMP-002 — GGUF Chat event work is untracked and cannot be cancelled by page retirement

- Severity: Important
- Invariant: every asynchronous user operation must be bound to a UI lifetime, have a terminal error mapping, and finish or cancel before its session is disposed.
- Evidence: `ChatDemoController` initializes and handles five `async void` page events with `CancellationToken.None`; four handlers have no catch, while Send/Continue have only `finally`. `DisposeAsync` detaches events and immediately disposes the coordinator without cancelling or awaiting in-flight handler tasks. `GgufChatSessionAdapter.GenerateAsync` releases `sessionGate` after retrieving the session, but `DisposeAsync` can then acquire that gate and dispose the same session while enumeration continues. The production launcher creates this controller and calls `InitializeAsync`.
- File:line: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatDemoController.cs:91`, `:108`, `:126`, `:140`, `:160`, `:168`, `:188`; `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/GgufChatSessionAdapter.cs:82`, `:124`; `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/GgufCurrentModelChatRouteLauncher.cs:52`; `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.ModelSourceCustody.cs:75`.
- Reproduction: `rg -n "async void|CancellationToken.None|DisposeAsync" "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatDemoController.cs"` and inspect the gate lifetime around `GenerateAsync`/`DisposeAsync` in `GgufChatSessionAdapter.cs`.
- Evidence location: pinned source; existing adapter/coordinator test searches described under Test gaps.
- User impact: importing another model, closing the journey or a runtime fault during generation can race disposal, surface an unhandled dispatcher exception, leave controls in the wrong generating state or terminate a response without a stable recovery message.
- Security/privacy impact: no path or secret leak was observed; resource lifetime uncertainty can leave a native worker/session alive longer than intended if a higher layer faults.
- Minimal correction: give the controller one lifetime `CancellationTokenSource`; route event callbacks into tracked `Task` operations through a common exception-to-support-code mapper; on retirement cancel, request stop, await the active task, then dispose the coordinator/session. Make the session adapter serialize active enumeration with close or own an explicit active-turn lifetime.
- Owner: GGUF Runtime owner for controller/session behavior; I1 App/Onboarding collision owner for retirement integration; C0 reconciles.
- Disposition: Open; T1/E1 coverage required before acceptance.

### A1-IMP-003 — OpenVINO Chat launch failures are silently discarded

- Severity: Important
- Invariant: a user-requested launch must end in success or a safe, actionable failure state; failure identity must survive route boundaries.
- Evidence: OpenVINO activation returns only `bool`, converts every exception to `false`, and emits no failure diagnostic. The optimized-Chat path simply returns if activation is false. The current-model path falls through to `CurrentModelChatLaunchRegistry`, but only a GGUF launcher is registered; the registry's typed `RuntimeUnavailable`/`LaunchFailed` result is discarded by the caller.
- File:line: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.OpenVino.cs:401`, `:410`, `:419`, `:466`; `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs:1077`, `:1087`, `:1104`, `:1319`, `:1342`, `:1666`; `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/CurrentModelChatLaunchRegistry.cs:81`, `:84`, `:99`, `:114`.
- Reproduction: inspect `ActivateOpenVinoChatFromDirectoryAsync`, then follow false from shell lines 1087 and 1329 and search `RegisterRoute(` calls.
- Evidence location: pinned source.
- User impact: pressing Chat can appear to do nothing when inspection, runtime activation, source custody or package validation fails; support cannot tell the user whether retry, re-import or repair is appropriate.
- Security/privacy impact: catch-all suppression avoids raw exception leakage, which is good, but it also discards safe support codes needed for fail-closed recovery.
- Minimal correction: replace the `bool` boundary with a typed activation result containing a stable support code and safe presentation key; render that result in the originating page. Either register an OpenVINO current-model launcher or keep the explicit OpenVINO branch, but not both. Propagate the page/journey lifetime token.
- Owner: OpenVINO route owner plus I1 App/Onboarding collision owner; C0 coordinates the shared result contract.
- Disposition: Open; M1/T1/E1 regression coverage required.

### A1-IMP-004 — OpenVINO optimization copies a large source file that execution does not consume

- Severity: Important
- Invariant: one component should own the immutable attempt source, and expensive staging should be the actual execution input or should not exist.
- Evidence: `OpenVinoOptimizationAttemptContextFactory` copies `openvino_model.bin` into an operation directory, hashes both original and staged files and publishes a `StagedSourceSnapshot`. The executor later reacquires source custody and passes `lease.SourcePath`, not `context.Source.SourcePath`, to `OpenVinoOptimizationService`. That service independently captures a package snapshot, validates it at multiple checkpoints and disposes it. The copied model is therefore an integrity side copy, not the conversion input.
- File:line: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Execution/OpenVino/OpenVinoOptimizationJourneyInfrastructure.cs:38`, `:43`, `:53`, `:56`, `:84`, `:102`, `:191`, `:212`; `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationService.cs:332`, `:477`, `:507`, `:567`, `:643`.
- Reproduction: `rg -n "stagedModel|StagedSourceSnapshot|lease!.SourcePath|OpenVinoPackageSnapshotter"` across the two files and compare the context path with the request path.
- Evidence location: pinned source.
- User impact: large OpenVINO weights are read, written and hashed before being read again by the real pipeline, increasing optimization startup time, temporary disk demand and the chance of a disk-space failure that is unrelated to the input actually executed.
- Security/privacy impact: an extra local model copy increases the number of sensitive artifacts needing reliable cleanup; cleanup exceptions are suppressed.
- Minimal correction: choose one source owner. Prefer staging the complete admitted OpenVINO package once and executing from that immutable package, with the service consuming the context snapshot. If execution must remain on the custody lease, remove the unused file copy and let the service snapshot be the canonical attempt evidence. In either design, test cleanup and identity revalidation at each transition.
- Owner: OpenVINO optimization owner; Q1 validates artifact/provenance behavior; C0 coordinates shared attempt-context changes.
- Disposition: Open.

### A1-MIN-001 — compiled demo, placeholder and experimental fallback islands have no production composition

- Severity: Minor
- Invariant: compiled production types should have a current composition owner, an explicit feature gate, or be test fixtures.
- Evidence: production-reference searches find `UnavailableHardwareInspectionService` only in its own file, while x64 production uses `HardwareInspectionComposition.CreateProduction`; `TurboQuantFallbackService` and `TurboQuantRouteAdapter` are referenced by their own implementation and tests but not production composition; `DemoGgufChatSession` is reached only from the controller's internal demo constructor and tests. Release compilation therefore carries alternative paths whose product status is not discoverable from composition.
- File:line: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Application/UnavailableHardwareInspectionService.cs:11`; `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/TurboQuant/TurboQuantFallbackService.cs:20`; `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/TurboQuant/TurboQuantRouteAdapter.cs:54`; `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatDemoController.cs:24`; `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/DemoGgufChatSession.cs:9`.
- Reproduction: `rg -n "UnavailableHardwareInspectionService|TurboQuantFallbackService|TurboQuantRouteAdapter|DemoGgufChatSession" --glob "*.cs"` and separate production from test hits.
- Evidence location: pinned source and duplicate/stale inventory below.
- User impact: future maintainers can fix or extend an inert route believing it is shipped, and public TurboQuant types expand the support surface.
- Security/privacy impact: no active security impact demonstrated because production composition was not found.
- Minimal correction: move demo implementations to test fixtures; delete the uncomposed placeholder; either bind the experimental TurboQuant adapter through one explicit, default-off authority with tests or remove it until the evidence gate can compose it.
- Owner: respective Hardware, GGUF and OpenVINO route owners; C0 decides removal versus explicit composition.
- Disposition: Open; verify references again after all specialist branches are reconciled.

### A1-MIN-002 — OpenVINO package identity has multiple manually synchronized authorities

- Severity: Minor
- Invariant: the sealed package manifest should be the single source for runtime versions, required binary names, hashes and architecture, with the app pinning only its digest.
- Evidence: composition correctly pins the worker manifest SHA-256 through assembly metadata, but then separately hard-codes Runtime/GenAI/Tokenizer version strings and a nine-entry binary-to-machine map in source. Packaging targets and the manifest necessarily carry overlapping inventory. A package upgrade therefore has several edit points that can drift.
- File:line: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Services/ModelInspectionServiceComposition.cs:55`, `:58`, `:75`, `:80`, `:157`; `IBM Granite with TurboQuant (Intel)/OpenVino.WorkerPackaging.targets:21`, `:37`.
- Reproduction: inspect lines 49–88 and 157–170 of `ModelInspectionServiceComposition.cs`, then compare the packaging target and generated worker-manifest inputs.
- Evidence location: pinned source. Packaged worker execution was not performed by A1.
- User impact: an otherwise valid OpenVINO package update can be rejected or, if checks evolve independently, report an identity different from the sealed inventory.
- Security/privacy impact: duplicated trust metadata is a supply-chain maintenance risk, not evidence of a present bypass; S1 owns security validation.
- Minimal correction: make one validated manifest model return versions, required files and PE-machine expectations after its digest is pinned. Delete composition literals that duplicate signed/sealed manifest facts.
- Owner: M1/OpenVINO packaging owner with S1 review; C0 coordinates project/packaging collisions.
- Disposition: Open.

### A1-MIN-003 — compatibility error handling duplicates a stale “no adapters” fallback

- Severity: Minor
- Invariant: route-specific evaluation should have one fail-closed exception mapper that preserves safe diagnostic identity.
- Evidence: the shell duplicates three evaluator lambdas and catches all non-cancellation failures by calling `RunWithAvailableAdapters`. That method's comment states no adapters exist and describes future wiring, despite active GGUF/OpenVINO production authorities. `CompatibilityEngine.EvaluateProduction` already catches non-cancellation exceptions and produces a fail-closed screen, so the outer fallback erases whether fresh-memory capture, binding or evaluation failed.
- File:line: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs:692`, `:714`, `:740`, `:757`, `:774`, `:799`; `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityEngine.cs:105`, `:122`, `:143`, `:170`, `:182`.
- Reproduction: `rg -n "RunWithAvailableAdapters|catch"` in the shell and compatibility engine.
- Evidence location: pinned source.
- User impact: users receive a generic not-established result and support loses the stage that failed; route logic is repeated and more likely to diverge.
- Security/privacy impact: fail-closed behavior is preserved; no disclosure found.
- Minimal correction: create one application-level evaluation adapter that captures fresh resources, binds the route input and maps a small typed failure enum. Keep the core engine's fail-closed projection and retire or accurately rename/document the legacy unavailable-adapter entry point.
- Owner: ModelHardwareCompatibility owner plus I1 App/Onboarding collision owner; C0 coordinates.
- Disposition: Open.

### A1-MIN-004 — conversation mutation bypasses the injected clock

- Severity: Minor
- Invariant: an aggregate whose coordinator owns a `TimeProvider` must not read the system clock during mutation.
- Evidence: `GgufChatCoordinator` uses an injected `TimeProvider`, but `ChatConversation.ReplaceMessage` stamps `UpdatedUtc` with `DateTimeOffset.UtcNow`.
- File:line: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/History/ChatConversation.cs:99`; `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/GgufChatCoordinator.cs:19`.
- Reproduction: `rg -n "TimeProvider|DateTimeOffset.UtcNow" "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime"`.
- Evidence location: pinned source.
- User impact: fake-clock tests and deterministic ordering can disagree with production time, especially around continuation/replacement operations or a system-clock adjustment.
- Security/privacy impact: none identified.
- Minimal correction: pass the coordinator's current timestamp into `ReplaceMessage`, or inject a clock into the aggregate consistently; add a fake-clock assertion for replacement/continuation.
- Owner: GGUF Runtime owner; T1 may cover the cross-lifecycle behavior.
- Disposition: Open.

### A1-MIN-005 — the integration-heavy application project does not enforce warnings as errors

- Severity: Minor
- Invariant: the project that composes all backend routes should enforce the same compiler-warning gate as its backend dependencies.
- Evidence: all inspected production projects enable nullable. Shared, infrastructure, runtime and worker projects also set `TreatWarningsAsErrors`, while the main WinUI application project enables nullable but does not set that property.
- File:line: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj:14`; comparison example `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/GraniteEdgeAI.ModelHardwareCompatibility.Core.csproj:7`.
- Reproduction: enumerate production `*.csproj`, extract `Nullable` and `TreatWarningsAsErrors`, and compare the main application project with the backend libraries.
- Evidence location: pinned project files.
- User impact: new nullable, async or API-usage warnings in the largest integration surface can enter the branch unnoticed.
- Security/privacy impact: none directly demonstrated.
- Minimal correction: enable warnings-as-errors for the app after first capturing and triaging the existing warning set; use narrow documented exceptions only when a generated/WinUI warning cannot be fixed.
- Owner: C0 with the App/project collision owner.
- Disposition: Open; the environment-blocked build means A1 cannot state the current warning baseline is zero.

## Duplicated and stale-path inventory

| Item | Classification | Evidence and action |
|---|---|---|
| OpenVINO model staging plus service package snapshot | Harmful duplicate owner | A1-IMP-004; converge on one immutable attempt source. |
| Three compatibility evaluator lambdas plus legacy unavailable-adapter path | Harmful duplicate policy/error mapping | A1-MIN-003; extract one typed adapter. |
| OpenVINO manifest digest plus source-coded versions/binary map | Drift-prone duplicated identity facts | A1-MIN-002; derive facts from the pinned manifest. |
| `TurboQuantRouteAdapter`/`TurboQuantFallbackService` | Uncomposed experimental island | A1-MIN-001; explicitly gate and compose or remove. |
| `UnavailableHardwareInspectionService` | Orphan placeholder | A1-MIN-001; remove if no non-x64 composition uses it after reconciliation. |
| `DemoGgufChatSession` and demo controller constructor | Test/demo seam compiled with production | A1-MIN-001; move to fixtures or isolate behind a build condition. |
| v2 execution payload/canonicalization APIs beside v3 | Intentional compatibility | Retain only while the frozen v2 verifier remains supported; no active planner was found producing v2. Document the removal gate rather than deleting now. |
| ModelInspection/Compatibility `DelegateCommand` copies | Intentional feature-local type | A source comment explicitly keeps them separate; no cleanup proposed. |
| Debug fixture galleries | Configuration-specific and intentionally excluded from Release | Main project removes fixture sources by default and includes them only for Debug x64/opt-in conditions; no Release reachability defect found. |

No alternate onboarding shell was found. No production use of the inert hardware service or TurboQuant fallback/adapter was found. No raw exception message or full imported-model path was found flowing into a durable compatibility/chat result during the sampled source-to-presentation traces. These are bounded static observations, not runtime proofs.

## Risk-ranked minimal cleanup sequence

1. **Close the Chat failure/lifetime seam first.** C0 coordinates the GGUF Runtime, OpenVINO route and I1 App/Onboarding owners. Change `ChatDemoController.cs`, `GgufChatSessionAdapter.cs`, `ModelInspectionPage.OpenVino.cs`, `CurrentModelChatLaunchRegistry.cs` and the narrow shell call sites. Add T1 managed race/error tests before any UI/native run.
2. **Choose one OpenVINO attempt source.** The OpenVINO optimization owner changes `OpenVinoOptimizationJourneyInfrastructure.cs` and, only if needed, `OpenVinoOptimizationService.cs`. Q1 verifies input/output provenance, large-file cleanup and revalidation. Do not alter shared `OptimizationAttemptContext` until C0 resolves GGUF collision impact.
3. **Extract the journey coordinator without changing contracts.** The I1 App/Onboarding collision owner splits state/transition and route factories from `OnboardingShellPage.xaml.cs`. Preserve handoff/custody types and use characterization tests for every stage transition.
4. **Consolidate compatibility error mapping.** The compatibility owner and I1 owner replace the three shell lambdas with one typed adapter; retain exact fail-closed screens and freshness checks.
5. **Consolidate OpenVINO manifest authority.** M1/OpenVINO packaging owner derives runtime versions and binary architecture from the pinned manifest; S1 independently reviews the trust boundary.
6. **Remove or explicitly gate stale islands, then raise compiler strictness.** C0 confirms no specialist branch composes the placeholder/demo/TurboQuant types before deletion. Enable app warnings-as-errors only after a clean diagnostic build is available.

The collision boundary is deliberate: A1 proposes these changes but modifies none. C0 must serialize changes to `OnboardingShellPage`, the main app project, shared route contracts and central fixtures.

## Test gaps and routing

| Owner | Required regression coverage; do not duplicate in A1 |
|---|---|
| M1 | OpenVINO activation returns stable support codes for unavailable source, invalid package, worker failure and stale lease; manifest-derived version/file/machine facts match the pinned manifest. |
| H1 | Reconfirm that compatibility receives the exact claimed model handoff plus the same `productHardwareRunId`, and that fresh available memory remains a runtime fact after coordinator extraction. No independent A1 hardware-algorithm defect was found. |
| Q1 | A large synthetic OpenVINO package proves exactly one canonical source copy/snapshot, execution consumes it, mutation forces replan, publish preserves hashes and every failure/cancellation cleans temporary data. |
| T1 | Deterministic controller tests hold generation open while import/navigation/dispose occurs; assert cancel/stop/await ordering, no unobserved exception, one terminal UI state, no surviving session, and typed GGUF/OpenVINO launch failures. Add behavior tests rather than source-string assertions. |
| E1 | Packaged UI tests press current-model and optimized Chat on both routes, induce a safe runtime-unavailable condition, observe an actionable message, recover/retry, import another model during generation and verify no stale UI/process remains. E1 alone proves the user journey. |

The tree has 433 test C# files; 54 directly read repository/source text. Many of those protect packaging and visual contracts, but source-substring assertions should remain secondary to behavioral tests because they resist safe refactoring and can pass without executing the behavior. The specific gaps above should be filled at the lowest appropriate layer, with E1 retaining final journey authority.

## Principle-to-evidence mapping

The local textbook files listed by the package were not present in the supplied package or repository. Accordingly, this audit does **not** claim direct inspection of those books. The compatibility plan's supplied reference map was used as an indirect method guide:

| Supplied reference mapping | Applied principle | Repository evidence |
|---|---|---|
| *Fundamentals of Software Architecture*, chapters 2–8, 21–22, 26–27 | Make trade-offs and component ownership explicit; use fitness functions at risky seams. | The project layers are mostly coherent, but shell ownership crosses UI/application/infrastructure (A1-IMP-001). Typed contracts and route tests are useful fitness functions to retain. |
| *Code Complete*, chapters 3, 5, 8, 22, 25, 28–29 | Information hiding, defensive construction, measurement and configuration control. | Exact handoffs and protected clients are strong; duplicate package identity and unused staging weaken single-owner information hiding (A1-IMP-004, A1-MIN-002). |
| *The Art of Unit Testing*, chapters 7–10 | Tests must be trustworthy, readable and behavior-focused. | Broad test coverage exists, but Chat retirement/failure races are uncovered and 54 tests inspect source text. |
| *Why Programs Fail*, chapters 3–6, 8, 10 | Reproduce, observe and isolate before attributing a failure. | The four OpenVINO test failures were rerun and reduced to nested SDK resolution, not reported as product defects. |
| *Systems Engineering: Principles and Practice*, chapters 6–8, 11–13, 16–17 | Allocate requirements/facts to one owner and preserve evidence traceability. | Model/hardware/config IDs are distinct and traceable; OpenVINO staging and identity facts have competing owners. |
| *Designing Secure Software*, chapters 2–4, 6–7, 10–13 | Minimize trust boundaries and information exposure; fail closed. | Manifest pinning, path custody and safe support codes are positive; silent failure loses safe diagnostic identity and extra staging widens local artifact custody. S1 remains security authority. |

## Verification ledger

All test rows ran against the frozen base/tree. Times are UTC from TRX metadata. SHA-256 values bind the uncommitted, local evidence files; their absolute paths are omitted.

| Command/test | Start/end UTC | Discovered | Passed | Failed | Skipped/blocked | Result SHA-256 |
|---|---|---:|---:|---:|---:|---|
| ModelHardwareCompatibility Release test executable, MTP/TRX | 2026-08-28 04:45:55 / 04:45:58 | 1,050 | 1,050 | 0 | 0 | `0c0e624c73be10d51a17416278f6622bec571c895ab011675bd263db87f5df21` |
| OpenVINO Contracts Release test executable, MTP/TRX | 2026-08-28 04:46:32 / 04:49:04 | 205 | 205 | 0 | 0 | `6600137fa8d10931d703848890c3f20225745ae3dfe3dc417660d4608513ae0f` |
| OpenVINO Unit Release test executable, final diagnostic run | 2026-08-28 04:57:06 / 04:57:16 | 409 | 398 | 4 | 7 declared guards | `dd0e9869f0375ed3e36d426db93a605ea6fcc45a0317e2f5275534744fffe5d0` |
| GGUF NativeAdapter Release test executable, MTP/TRX | 2026-08-28 04:51:25 / 04:51:29 | 44 | 40 | 0 | 4 declared guards | `1c3539993836a1cf75e34254f0cdef8919d76be89aba8efc6aab9dc663065525` |
| `dotnet build GraniteEdgeAI.slnx --configuration Debug -p:Platform=x64 -bl:...` | 2026-08-28 04:51:39 / 04:56:41 | 0 | 0 | 0 | 1 environment-blocked build | `b1b42688782a75bbf5ebe1d392b21251114d8139b2991194cb297afd8bae7fa8` |

Aggregate managed test totals, using the handoff schema convention `executed = passed + failed + skipped`: discovered 1,708; executed 1,708; passed 1,693; failed 4; skipped 11.

The four failed OpenVINO tests were `CrossRouteNativeEvidenceCannotDisableEitherClosure`, `ExplicitSourceOnlyBuildMaySkipBothClosures`, `ExplicitNonPackagingBuildMaySkipTheOfficialClosure` and `PackageGenerationCannotSkipTheOfficialClosure`. Each launches nested `dotnet msbuild`. The repository pins SDK 10.0.301, but that SDK installation is incomplete; SDK 10.0.400 is installed and was used from an external audit shim without changing the repository. Nested processes returned the SDK-not-found error before reaching the asserted packaging behavior. Three diagnostic attempts produced the same boundary (first PATH resolution, then pinned-SDK resolution), so A1 stopped rather than modifying configuration. The seven skips require the declared `GRANITE_OPENVINO_CONVERTER_STAGE` or `GRANITE_OPENVINO_OFFICIAL_WORKER_STAGE`. The four GGUF skips require a controlled real-model configuration.

The first build syntax attempt used `--arch x64`, which selected a runtime identifier and failed with `NETSDK1134`; it is not counted as the required build. The corrected `-p:Platform=x64` build reached the WinUI packaging targets, then a nested bare `dotnet` command failed because PATH did not expose a compatible host/SDK. Its diagnostic binlog is `b1b42688782a75bbf5ebe1d392b21251114d8139b2991194cb297afd8bae7fa8`. No successful solution-build claim is made.

## Commands and results

- `git rev-parse HEAD` and `git rev-parse 'HEAD^{tree}'` matched the frozen commit/tree before evidence collection.
- `rg --files`, production/test root inventories and `*.slnx`/`*.csproj` reads produced the project and source counts in this report.
- Narrow `rg -n` and numbered source reads traced every control-flow edge and reproduction listed in the findings.
- The four required managed projects were listed before their direct Microsoft.Testing.Platform executables were run. Zero-discovery `dotnet test --project` results were rejected and are not reported as passes.
- `dotnet --info` reported x64 host/runtime 10.0.11 and SDK 10.0.400; repository `global.json` requests 10.0.301.
- No app, packaged UI, native model, converter, quantizer, hardware candidate or real-model stage was launched.
- Final report checks and `git diff --check` are recorded externally after serialization because the report cannot contain its own final hash or commit.

## Evidence and privacy

Raw TRX files and build binlogs remain in the repository's ignored local `TestResults` area and are not committed or pushed. The report records only counts, UTC times, sanitized failure classes and SHA-256 digests. It contains no username, hostname, full local worktree/evidence path, imported model path, model weights, raw command transcript, secret or credential. No cross-worker evidence manifest is required for A1.

## Remaining work and nonclaims

- C0 must independently verify every finding against the exact frozen tree, reconcile collision owners and decide the implementation sequence. This report authorizes no shared-file edit or merge.
- A1 did not prove the application builds, packages, launches or passes real GGUF/OpenVINO execution on this laptop.
- Passing managed tests do not prove native binaries, package signing, App Control, accessibility, visual fidelity, performance, output quality or end-to-end cleanup.
- Static absence of a path leak, alternate shell or production reference is not a runtime or whole-program proof; reflection, generated targets and packaging must be rechecked after branch reconciliation.
- Historical v2 APIs were classified as compatibility only because current planners use v3 and frozen tests retain v2; C0 must confirm the retirement gate before removal.
- S1 owns security conclusions; M1/H1/Q1 own model, hardware and artifact facts; T1 owns new cross-feature managed tests; E1 alone owns final packaged user-journey evidence.
