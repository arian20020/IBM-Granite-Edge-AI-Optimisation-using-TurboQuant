# F1 frontend remediation R3

## Disposition

- Worker: F1
- Owned issues: R3-006 and R3-007
- Supported issues: R3-005 and R3-008
- Branch: `audit/ucl-f1-frontend-remediation-r3`
- Authoritative R3 base commit/tree: `cb63932e71eb3ed148993382ca4d2cccee646df6` / `1d504b538a6993501981408a49bb9fcc5011ea44`
- Frozen ancestor commit/tree: `4748fe04f19afdf6b27c4c12502b84db325e7294` / `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`
- Evidence-subject implementation commit/tree: `51844bc292def936575688aa3ae406105615eb0d` / `bc7805ac2e62d313e46e005223768cf11f85e455`
- `evidenceManifest`: null

The production correction is committed and independently reviewed. R3-006 and
R3-007 are not declared closed because the required packaged regression RED and
GREEN executions are phase-gated. R3-005 and R3-008 remain C0/Q1 composition
work; F1 does not claim the download/export journey is production-composed.

## Production behavior

Download and export controllers now create the observable operation task before
provider work starts. Retirement is idempotent, requests cancellation, and awaits
that exact task even when a provider ignores cancellation. A retired controller,
control, or page rejects start, retry, cancel, picker completion, verified-download
completion, stale events, and replacement bindings. Late progress and terminal
results cannot mutate a retired page.

Cancellation callback failures cannot escape retirement or be overwritten by a
provider result. Terminalization crosses a context-preserving asynchronous
boundary; therefore a callback that completes the provider task inline and then
throws is classified as the existing privacy-safe cleanup failure before result
publication. Event subscribers are isolated individually, and diagnostics contain
only the exception type.

The export Save handler awaits the control operation. Download, picker, and export
entry points check retirement both before and after asynchronous boundaries.
Original-model preservation and the R2 identity, integrity, bounded-progress,
retry, insufficient-space, publication, cleanup, stale-target, runtime-only,
accessibility, privacy, and command-state gates remain unchanged.

## Production reachability

No new production type or registration was introduced. The R3 methods extend the
already registered `ModelImportPage`, `ModelDownloadCard`, `OptimizationPage`, and
`OptimizationDestinationCard` XAML types. Each affected page/control `x:Class`
occurs exactly once.

| Definition/seam | Real production caller | Composition point | Behavioral regression |
|---|---|---|---|
| `ModelDownloadController.RetireAsync` | `ModelDownloadCard.RetireAsync` | Existing download card controller binding | retirement waits, rejects late success, throwing cancellation callback |
| `ModelDownloadCard.RetireAsync` | `ModelImportPage.RetireForNavigationAsync` | Existing card in `ModelImportPage` | command disablement and non-cooperative provider observation |
| `ModelImportPage.RetireForNavigationAsync` | `ModelImportPage.OnNavigatedFrom` and page disposal | Existing Frame-created import page | late verified completion and late picker completion after navigation |
| `OptimizationExportController.RetireAsync` | `OptimizationDestinationCard.RetireAsync` | Existing verified-target control binding | retirement waits, target invalidation, throwing cancellation callback |
| `OptimizationDestinationCard.RetireAsync` | `OptimizationPage.RetireForNavigationAsync` | Existing destination card in `OptimizationPage` | action disablement and non-cooperative provider observation |
| `OptimizationPage.RetireForNavigationAsync` | `OptimizationPage.OnNavigatedFrom` | Existing shell-created optimization page | page retirement observes export |
| `OptimizationDestinationCard.TryStartExportAsync` | awaited Save button handler | Existing destination control | duplicate activation and retired activation rejection |
| `ModelDownloadCard.TryStartDownloadAsync` | awaited Download button handler | Existing download control | duplicate activation and retired activation rejection |

`OnNavigatedFrom` is the F1-owned production fallback and invokes each page seam
exactly once. It does not establish shell-wide ordering. C0 must explicitly await
the same idempotent seams before shared navigation, detachment, or shutdown.

## Exact C0-owned seam proposal

F1 did not edit the shared shell. C0 should consume only these stable calls:

```diff
 private async Task RetireOptimizationAsync()
 {
     if (_attachedOptimizationPage is { } page)
     {
         page.IntentRequested -= OptimizationPage_IntentRequested;
+        await page.RetireForNavigationAsync();
     }
     if (_optimizationCoordinator is { } coordinator)
     {
         coordinator.StateChanged -= OptimizationCoordinator_StateChanged;
         await coordinator.DisposeAsync();
     }
```

For import navigation/detachment, C0 must change its shared navigation transaction
to an async transaction and execute the following before replacing Frame content
or clearing the page reference:

```diff
+if (_attachedModelImportPage is { } importPage)
+{
+    await importPage.RetireForNavigationAsync();
+}
 // existing Frame navigation/content replacement and DetachModelImportPage
```

The same two awaits belong in shared shutdown. Because both methods return the
same retirement task on every call, the Frame fallback and explicit C0 ordering
cannot launch duplicate cancellation or cleanup.

C0/Q1 must separately compose the existing R2 contracts
`IRecommendedModelDownloadService`, `RecommendedModelOffer`,
`IOptimizationExportService`, and `VerifiedPersistentExportTarget`. F1 does not
construct backend targets or infer success. The parameterless Frame-created import
page remains fail-closed until that composition exists.

## TDD and verification ledger

The test-only RED commit is
`78cfa241d3053f54623bf7f57b7e32e65d2782c0`. It adds the owned navigation
regressions before the production methods exist. Behavioral RED is **not
established**: predecessor native receipts were absent, so the packaged test host
was not launched. A build-only or source comparison is not reported as RED.

The corrected tree contains ten new deterministic regressions with no sleeps:
late verified completion, late picker completion, non-cooperative download/export
retirement, disabled commands/actions, exact retirement-task identity, cleared
export target, throwing cancellation callbacks, and inline provider completion
followed by cancellation failure. Behavioral GREEN is likewise **not established**
until authorized packaged execution runs the same rows.

| Verification at evidence-subject commit | Discovered | Executed | Passed | Failed | Skipped/blocked | Result |
|---|---:|---:|---:|---:|---:|---|
| T1 cross-feature integration | 52 | 52 | 52 | 0 | 0 | pass |
| App Debug x64 design-time compile | 1 build | 1 | 1 | 0 | 0 | 0 warnings, 0 errors |
| Packaged-test project Debug x64 design-time compile | 1 build | 1 | 1 | 0 | 0 | 27 pre-existing warnings, 0 errors |
| Affected packaged frontend source inventory | 51 | 0 | 0 | 0 | 51 | blocked before discovery/execution |
| New owned packaged regressions | 10 | 0 | 0 | 0 | 10 | blocked before discovery/execution |

Executed test arithmetic: `52 = 52 + 0 + 0`; discovered `52 >= 52`.
Build rows are not included in test arithmetic.

An initial `dotnet test` T1 attempt was blocked by App Control before discovery
and reported zero tests; it is not counted. Running the already-built managed T1
assembly through the installed .NET host passed 52 of 52. App Control was not
changed or bypassed.

## Native/package disposition

H1, M1, and Q1 handoff receipts, their native phase receipts, and the native lock
were absent at the final gate check. No packaged UI discovery, deployment, native
test, accessibility execution, package construction, or F1 native phase receipt
was attempted. The 51 packaged frontend rows above are blocked, not skipped
passes. No screenshot, model, provider, worker, native inference, or end-to-end
claim is made.

The repository pins SDK 10.0.301, but that installation lacks the SDK entry
assembly. Development-only managed compilation used the installed 10.0.400 SDK.
This does not substitute for the pinned official build. Full package construction
also remains blocked by required producer stages and phase receipts.

## Scans and independent review

- `git diff --check`: pass.
- Production-diff privacy scan: zero URL, local-path, username/hostname API,
  authorization, token-query, or retired path-source matches.
- Nine affected automation ID definitions: each occurs once in production XAML.
- Two affected control class definitions and two page class definitions: each
  occurs once.
- Independent final review of `51844bc2`: no production or test findings; the
  synchronous callback-completion regression exercises the re-entrant race.

## Remaining external blockers and receipt disposition

- C0 must await the two stable retirement seams and compose the Q1 services/target.
- Q1 must provide the approved production download service and exact verified
  persistent export service/target; R3-005 and R3-008 remain open until then.
- H1, M1, and Q1 must publish conforming current-phase handoff and native receipts.
- The required handoff schema file is absent from the handoff root, supplied
  attachments, download area, and repository. Therefore F1 cannot validate or
  atomically publish a conforming handoff receipt.
- No F1 receipt is published: the contract forbids a receipt when schema,
  behavioral RED/GREEN, native join, or predecessor validation is unavailable.
- Main was not merged or pushed.
