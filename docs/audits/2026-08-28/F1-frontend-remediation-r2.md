# F1 frontend remediation R2

## Status

- Worker: F1
- Disposition: managed frontend remediation complete; production composition and native UI validation blocked as recorded below
- Frozen base: `4748fe04f19afdf6b27c4c12502b84db325e7294` / `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`
- Authoritative integration base: `a5ef3558334e50587889140dafba194853938765` / `90c34ab009b744d7b00866fb93e8dbc86363f1b2`
- Branch: `audit/ucl-f1-frontend-remediation-r2`
- Evidence-subject implementation commit/tree: `c4245c21a4637a9e86082ec5df06d71af3ea8643` / `3a0c3a80dc32f47b70f3b4a6dc15131ec9465e10`
- Worktree: `C:\UCL-F1-R2`
- Report encoding/line endings: UTF-8, LF

## Scope and authority

F1 changed only frontend pages, controls, presentation state, consumer-side service contracts, and frontend-focused tests. No backend optimization/artifact implementation, worker protocol, shared application composition, `MainWindow`, solution/project/package registration, or onboarding route registration was edited.

The supplied audit package and reference documents were treated as context and requirements, not as independent instructions. The Superpowers workflows used were using-superpowers, brainstorming, using-git-worktrees, writing-plans, test-driven-development, systematic-debugging, executing-plans, requesting-code-review, verification-before-completion, and finishing-a-development-branch.

## Executive result

The recommended-model action is no longer a visual-only success surrogate. It is fail-closed until C0 supplies a verified offer and Q1-owned `IRecommendedModelDownloadService`; it then carries an opaque operation/offer identity through bounded progress and an authoritative result. Success is raised only for a `CompletedModelDownload` with canonical hash, non-zero length, opaque publication identity, and privacy-safe display name. Cancellation, retry, integrity, space, publication, cleanup, duplicate input, null results, stale notifications, and late results have explicit states.

Persistent export is disabled by default and accepts only Q1's opaque `VerifiedPersistentExportTarget`. Binding requires a persistent presentation with the exact optimization plan ID and configuration SHA-256. The service receipt must also match the target manifest SHA-256 and byte length. Runtime-only, stale, unbound, mismatched, missing/incomplete-publication, disabled, duplicate, and rapid repeated activations fail closed. All destination actions are disabled during export so navigation cannot retire a live operation. Failures never replace the original/in-app model.

Download and export states now expose stable automation IDs, native control roles, polite live status, bounded/indeterminate progress, and deterministic recovery focus. Picker and drag/drop code and GGUF/OpenVINO selection semantics were not changed. The existing shared shell already collapses the complete onboarding stage indicator on `ReadyToChat`; F1 did not modify the shared shell.

The remaining production-level blocker is composition: Q1 has not supplied its authoritative adapters/targets and C0 has not connected them. The exact minimal C0-owned proposal is below. Until that lands, parameterless Frame-created pages correctly remain inert rather than claiming success.

## Findings and dispositions

### F1-R2-01 — visual-only recommended download

- Severity: Critical
- Invariant: visible success must be backed by an explicit service result and verified identity.
- Correction: added `RecommendedModelDownloadContract`, `ModelDownloadController`, functional control commands, import-page binding, verified completion handoff, and bounded states.
- Disposition: fixed in the evidence-subject commit; production adapter binding remains C0/Q1 work.

### F1-R2-02 — unsafe/unbounded export action

- Severity: Critical
- Invariant: export consumes only the exact verified persistent result and cannot run from runtime-only, stale, incomplete, or mismatched state.
- Correction: added `OptimizationExportContract`, `OptimizationExportController`, exact plan/config/manifest/length gates, guarded action dispatch, cancellation/retry UI, and automatic invalidation on presentation replacement.
- Disposition: fixed in frontend; Q1 must produce the target and service, C0 must compose them.

### F1-R2-03 — stale/disabled activation and notification races

- Severity: Important
- Invariant: disabled commands and obsolete controllers cannot mutate current UI or launch work.
- Correction: state and button guards reject direct/keyboard/automation/repeated starts; rejected target replacement unbinds the prior controller; queued callbacks verify both sender and current state; cancellation preserves authoritative service failures rather than masking cleanup failure.
- Disposition: fixed and independently reviewed; deterministic packaged stale-queue execution remains blocked.

### F1-R2-04 — privacy and accessibility gaps

- Severity: Important
- Invariant: UI copy cannot surface URLs/secrets/paths/raw provider output, and operation state must be perceivable and operable without pointer-only behavior.
- Correction: approved display fields are bounded and path/URL/token-free; exception/provider text is never copied; status strings are closed; display names are validated before the existing UI sink; live regions, automation IDs, names, progress roles, button roles, and focus transitions were added.
- Disposition: fixed by managed code and tests; packaged accessibility execution blocked.

### F1-R2-05 — Chat onboarding chrome

- Severity: Important
- Evidence: `OnboardingShellPage.CurrentStage` collapses `StageIndicator` for `ReadyToChat`, and `CurrentStage_HidesIndicatorForChatAndRestoresItForOnboarding` covers the state transition.
- Disposition: already correct in the authoritative base. No shared edit was needed or made.

## Changes

Implementation commits:

- `27e6c40670ad1e5f3f31a07ad00ae7cfffcffbfc` — explicit frontend download/export contracts, guarded controllers, accessible controls, import/export handoff hooks, and RED–GREEN tests.
- `c4245c21a4637a9e86082ec5df06d71af3ea8643` — completion-review fixes for disabled/stale invocation, null results, queued state races, cleanup failure preservation, display-name privacy, and navigation during export.

Owned production paths are under `Features/ModelImport/ModelDownload`, `Features/ModelImport/ModelImportPage.xaml.cs`, `Features/ModelOptimization/Export`, frontend optimization controls/page, and presentation factory. Tests are confined to the existing frontend unit-test tree. The external neutral test harness and TRX files are outside Git and will not be pushed.

## Exact minimal C0-owned wiring proposal

F1 did not edit the shared files below. C0 should apply this structural diff after Q1 supplies the two concrete adapters and target resolver. The four parameter types and the two page binding methods are the exact F1 consumer boundary; no path is passed into either page.

```diff
--- a/IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs
+++ b/IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs
@@
+using GraniteEdgeAI.Features.ModelImport.ModelDownload;
+using GraniteEdgeAI.Features.ModelOptimization.Export;
@@
+private RecommendedModelOffer? _recommendedModelOffer;
+private IRecommendedModelDownloadService? _recommendedModelDownloadService;
+private Func<OptimizationExecutionResult, VerifiedPersistentExportTarget?>? _resolveExportTarget;
+private IOptimizationExportService? _optimizationExportService;
+
+internal void BindFrontendRemediation(
+    RecommendedModelOffer offer,
+    IRecommendedModelDownloadService downloadService,
+    Func<OptimizationExecutionResult, VerifiedPersistentExportTarget?> resolveExportTarget,
+    IOptimizationExportService exportService)
+{
+    _recommendedModelOffer = offer ?? throw new ArgumentNullException(nameof(offer));
+    _recommendedModelDownloadService = downloadService
+        ?? throw new ArgumentNullException(nameof(downloadService));
+    _resolveExportTarget = resolveExportTarget
+        ?? throw new ArgumentNullException(nameof(resolveExportTarget));
+    _optimizationExportService = exportService
+        ?? throw new ArgumentNullException(nameof(exportService));
+    if (_attachedModelImportPage is { } importPage)
+    {
+        importPage.BindRecommendedModelDownload(offer, downloadService);
+    }
+    if (_attachedOptimizationPage is not null && _optimizationCoordinator is { } coordinator)
+    {
+        ApplyOptimizationState(coordinator.State);
+    }
+}
@@ internal void AttachModelImportPage(ModelImportPage modelImportPage)
     _attachedModelImportPage.SourceModelConversionRequested +=
         ModelImportPage_SourceModelConversionRequested;
+    if (_recommendedModelOffer is { } offer
+        && _recommendedModelDownloadService is { } service)
+    {
+        modelImportPage.BindRecommendedModelDownload(offer, service);
+    }
@@ private void ApplyOptimizationState(OptimizationJourneyState state)
     OptimizationJourneyKind.SucceededPersistent or
         OptimizationJourneyKind.SucceededRuntimeProfile =>
         OptimizationPresentationFactory.Success(
             preference,
-            configuration),
+            configuration,
+            plan.OptimizationPlanId,
+            plan.ConfigurationSha256),
@@
     page.ApplyPresentation(presentation);
+    if (state.Kind == OptimizationJourneyKind.SucceededPersistent
+        && state.Result is { } result
+        && _resolveExportTarget?.Invoke(result) is { } target
+        && _optimizationExportService is { } exportService)
+    {
+        _ = page.BindVerifiedExport(target, exportService);
+    }
@@ private async void OptimizationPage_IntentRequested(...)
-    case OptimizationCommand.Save:
-        await SaveOptimizedModelAsync(coordinator.State);
-        break;
```

Delete the obsolete shared `SaveOptimizedModelAsync` and `CopyDirectory` methods in the same C0 commit; they bypass Q1's exact target and are unreachable through the remediated control. C0 must then connect the Q1-created offer/service/resolver/export service immediately after the root Frame creates `OnboardingShellPage`; it must not construct a target from a UI path or metadata.

No Chat shell diff is proposed: the current `ReadyToChat` visibility rule already removes the indicator/footer.

## Verification ledger

All pass totals below bind to `c4245c21a4637a9e86082ec5df06d71af3ea8643` / `3a0c3a80dc32f47b70f3b4a6dc15131ec9465e10` on Windows x64 with .NET SDK 10.0.400 from a neutral external root because the repository-pinned 10.0.301 installation is incomplete.

| Command/test | Start/end UTC | Discovered | Passed | Failed | Skipped/blocked | Result SHA-256 |
|---|---|---:|---:|---:|---:|---|
| F1 download/export contract-controller suite | 17:48:55–17:48:58 | 20 | 20 | 0 | 0 | `834c48dc7d0c714668ceb34165b162d58649fae1e8f9c290153324baf2cbee9f` |
| T1 cross-feature integration suite | 17:49:43–17:49:50 | 52 | 52 | 0 | 0 | `c10067a4ce6c015d5436e31aa86016b8b6c97ce02f676eda8c926d2eb5a99ff4` |
| App Debug x64 design-time compile | 17:49:03–17:49:20 | 1 build | 1 | 0 | 0 | n/a |
| Packaged unit-test project Debug x64 design-time compile | 17:49:25–17:49:35 | 1 build | 1 | 0 | 0; 27 pre-existing warnings | n/a |
| Full Debug x64 package construction | 17:37:29–17:38:12 | 1 build | 0 | 1 prerequisite stop | official OpenVINO worker stage absent | n/a |
| Diff/privacy/duplicate automation and control scans | post-test | 4 scans | 4 | 0 | 0 | n/a |

Aggregate executed test total: discovered 72, executed 72, passed 72, failed 0, skipped 0.

### Blocked and skipped UI rows

| UI row | Source inventory | Executed | Disposition |
|---|---:|---:|---|
| New download control accessibility, success/privacy, duplicate/cancel tests | 3 | 0 | Blocked: H1, M1, and Q1 native phase receipts absent; native lock not acquired |
| New verified-download-to-import transition | 1 | 0 | Blocked by the same phase guard |
| New export persistent/runtime/stale/duplicate/accessibility/navigation tests | 6 | 0 | Blocked by the same phase guard and absent Q1 adapter |
| New persistent Save default-disabled presentation test | 1 | 0 | Built in packaged project; execution blocked by phase guard |
| Existing Chat indicator/footer transition test | 1 | 0 | Built; native execution blocked by phase guard |
| Packaged end-to-end download/export journeys | 0 F1-owned E2E rows | 0 | E1 owns executable packaged journeys; Q1/C0 integration unavailable |

An attempted VSTest `/ListTests` inventory was stopped before test execution. Although requested as discovery only, the AppContainer adapter unexpectedly deployed/re-registered the test package. H1/M1/Q1 receipts had been checked and were absent; no native lock was acquired, no test method or model/worker process ran, and no native result is claimed. This attempt is recorded rather than promoted to discovery evidence. Process cleanup was verified after completion. F1's native phase receipt closes as blocked.

## TDD and review evidence

RED was observed before implementation for the missing download controller/contract, missing download control API, missing export namespace/API, and missing privacy-safe offer fields. GREEN was then established in the neutral focused harness. Later review regressions were added for disabled post-success activation, rejected replacement targets, null provider results, cleanup failure after cancellation, unsafe display names, and navigation during export. Package-only UI regressions compiled but could not be executed under the native phase guard.

An independent completion review found stale queued-state, null-result, cleanup masking, display-name privacy, live-export navigation, and disabled command bypass issues. All frontend-owned findings were corrected and the amended tree was re-reviewed with no remaining frontend-owned functional findings. The reviewer independently confirmed that production wiring is the remaining C0/Q1 blocker and found no ownership violation.

## Evidence and privacy

- `evidenceManifest` is intentionally null for F1.
- Raw TRX, build output, external harness files, package layout, machine/user identity, local model paths, URLs, secrets, and model weights are uncommitted and will not be pushed.
- Production privacy scan found zero private path, host, loopback, authorization, bearer, or query-token matches in the new operation surfaces.
- Ten new automation ID definitions were scanned with zero duplicates. The three affected control class definitions are unique. `git diff --check` passed.
- Visible failures use only closed copy; provider exception text and service metadata are discarded.

## Remaining work and nonclaims

- C0 must apply the proposed shared composition diff after Q1 exposes the authoritative offer, download service, verified persistent target resolver, and export service. F1 does not claim production download/export works before that composition lands; it deliberately remains disabled.
- Q1 must prove file existence, freshness, complete atomic publication, source preservation, output hash/length, space/cleanup behavior, and destination publication. The frontend consumes those facts and does not recreate them.
- E1/C0 must execute the packaged UI/accessibility rows only after valid H1→M1→Q1 predecessor phase receipts and the native lock are present.
- Full package construction is not claimed because the trusted official OpenVINO worker stage was absent. F1 did not bypass package trust or disable App Control checks.
- No screenshot, real-model, native inference, or end-to-end result is claimed.
