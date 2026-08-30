# F1 R4.1 frontend/download correction and evidence

## Disposition

**COMPLETE WITH EXTERNAL NATIVE BLOCK.** F1-owned source, managed build, packaged lifecycle tests, review remediation, and the narrow path-private handoff seam are complete. Intel-native, performance, Release packaging, the full screenshot matrix, and C0-owned automatic navigation are not accepted on this non-authoritative machine.

Branch `audit/ucl-f1-frontend-download-remediation-r4-1`; worktree `C:\R41-F1`. Base commit `414ad9f97e5cc27e6b710f82d70b985aa14f3507`, tree `8209912d1ace72665b596c13ba4c461444e99665`. Immutable implementation subject is `69057dc7f5bf5849e12bb48e5924ae9fad1fa6db`, tree `9a8b27ca01ff26137cde1892a3211cb67ea0a2f1`.

## Provenance

- `origin/audit/ucl-f1-frontend-download-remediation-r4` equalled required tip `414ad9f97e5cc27e6b710f82d70b985aa14f3507` and tree `8209912d1ace72665b596c13ba4c461444e99665` before work.
- `git merge-base --is-ancestor ac5965a2179fb90b04c80ee8a0d49eaf87ada407 414ad9f9...` and the equivalent check for verified-download base `4154ed6194864c6852728bd81e7373d7e7bc3ff2` succeeded.
- Frozen source remains `4748fe04f19afdf6b27c4c12502b84db325e7294`, tree `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`.
- Predecessor report: SHA-256 `08679190c42a0574d127cdeca7bd08d40b472a5608d5c67f363a48d943f5ff4c`, 9068 bytes. Predecessor receipt: `f570aa7cafdf6bfcaec4cbe2838dfdaf92071666d302a749893034e5670be08d`, 1078 bytes.
- The complete `4154ed61..414ad9f9` changed-path set comprised the inherited Model Import/download, Optimization presentation/export, R4 audit/receipt, and corresponding UnitTests paths; `git diff --name-only 4154ed61..414ad9f9` was captured in preflight. No files were imported from another checkout.

## Findings and corrections

| Finding | Reproduced | Correction and proof |
|---|---|---|
| F1-R41-001 unexpected export faults | Yes. `red-fault.trx`: 1 executed, 0 passed, 1 failed under broad conversion. | Controller returns the exact operation task, settles state/resources, preserves typed cancellation/failure, and rethrows unexpected faults. The card observes faults without `async void`, records only exception `Type`, and traces only its name. Retry, rebind, stale progress/success, null result, foreign cancellation, and retirement are tested. Temporary broad-conversion mutation produced the required 0/1 RED in `mutation-broad-conversion-red.trx`; restored GREEN is included in 197/197. |
| F1-R41-002 cancellation boundaries | Yes; initial tests observed several pre-operation seams only. | Connection and streamed-read cancellation use caller tokens. Injected bounded storage primitives now block *inside* partial write, checkpoint write, checkpoint replacement, hash, and publish calls. Blocking cancellation callbacks are dispatched asynchronously and cancel/retirement are test-bounded; CTS disposal waits for callback quiescence. Storage preflight has its own phase so local `IOException` becomes `download-storage-failed`, not a network interruption. Tests assert no unverified final, valid resumability policy, lease reacquisition, no false success, and distinct timeout/integrity/storage outcomes. |
| F1-R41-003 opaque automatic handoff | Yes; the prior branch lacked complete wrong/replaced/drop/retired claim coverage. | Authority is installed before provider work, rechecked atomically, one-time claimed, revoked by download/picker/drop replacement and retirement, and retired within a bounded classifier timeout. Events remain operation-ID plus safe display name only; exact verified artifact identity remains inside the live page. |
| F1-R41-004 post-fix visual evidence | Partially reproduced. | Two privacy-safe exact-app idle captures were inspected. The final subject has no F1 lifecycle-state gallery or C0/Q1 wiring, so dynamic download/export states, 200%, High Contrast, and automatic inspection cannot be deterministically reached without fabricated native stages or foreign-owner changes. Those scenarios are recorded below as unavailable, not passed. Behavioral/automation tests cover their typed state and accessibility contracts. |

## Requirement-to-evidence matrix

| Requirement | Evidence | Result |
|---|---|---|
| Typed export outcomes; unexpected fault observable and private | `fault-final-green.trx` 2/2; post-review owned run; mutation RED; UI diagnostic-type assertion | Pass |
| Connection/header and response-read cancellation | deterministic transport/header and cancellation-only stream tests | Pass |
| In-flight partial/checkpoint/hash/publish cancellation | injected storage-operation gates, 4 DataRows plus partial-write test; focused 11/11 | Pass |
| Automatic classification/replacement/retirement | integration tests for never-settling classifier, exact artifact, wrong/second/concurrent/download/picker/drop/retired claims | Pass |
| Repeated lifecycle stability | `R41-review11-lifecycle-1/2/3.trx`, 103/103 each | Pass |
| Managed/package-independent build | Debug x64 packaged UnitTests build, 0 errors, 13 inherited nullable/analyzer warnings | Pass |
| Native/Release package acceptance | official OpenVINO/GGUF stage directories unavailable | Blocked externally |
| Full production automatic navigation | F1 seam complete; C0 subscription/claim/navigation absent by ownership | Pending C0 |
| Full visual matrix | no controllable final-subject F1 lifecycle gallery; only two auxiliary idle captures | Blocked/unavailable, no acceptance claim |

## Changed paths and commit sequence

Implementation commits:

1. `42f7c9e7c3a7fd169760777bdbf6a0f44426c1c5` / tree `409cdc55b0d0b965576800b16796b4bdd3059217` — initial R4.1 lifecycle correction.
2. `24e5cd0bd24da72e92377821abc91c1ae051c560` / tree `b00404ec53b37760378620eea3bb68473216adf0` — first review remediation.
3. `f64b9895d83409e5eee4e5ffe6323f037fea977e` / tree `73e87f3d977e6e4b0f697bb0026b18cb372cecb9` — cancellation-source and atomic-commit review remediation.
4. `f6af3666e656415d3b8fc51b7a628e237f9758e6` / tree `161d7b3d9aea4cc80eef24968bff988d3e245337` — deferred disposal and cleanup-gating review remediation.
5. `7ed317e69c65340a526facb1cc6912138250f9c9` / tree `a823a5c66b2299dd6b526ab95628fc4340b9b59d` — deferred cancellation ownership review remediation.
6. `38980face511f383b432d554d6cd87835e78d309` / tree `1b2b68859ccfcf70a77fef5dc2fe25a7513ff28f` — download/export reconciliation and replacement-race review remediation.
7. `2ac62be9aed9bd1b01812c7b84832e6cf0175b17` / tree `b47d6028d3d265a6e8bb07c4613c9162963694c5` — terminal cancellation ownership and detached-cleanup review remediation.
8. `5451e212ff5f58c25fd7540b5b6affcc675f86a7` / tree `74d9651d1a9d1c17d3b31d787a6c459d072f74fb` — inactive discard and direct-retirement cleanup authority remediation.

9. `4ef42360e184cee04a8f5abd1d9ce978f00da1d5` / tree `7f52d93896eba747ab40e1ad5111197954e1c120` — per-operation cancellation and post-render observer remediation.
10. `4242be5e8d9894906ec95a2ec1d4d268ec7a3356` / tree `c92caa42dc0cc0e77ff909091b6293ffbe7b5b7d` — cleanup authority/caller observation separation and preserve-to-discard upgrade remediation.
11. `231cbf8bd150f376d6ace460dd98b7d6e76a3abd` / tree `adada649a65591b802f0ba00c6b0b3bfc2b07fef` — bounded discard reconciliation and atomic final-settlement remediation.
12. `69057dc7f5bf5849e12bb48e5924ae9fad1fa6db` / tree `9a8b27ca01ff26137cde1892a3211cb67ea0a2f1` — immutable implementation subject settling queued cleanup authority directly from retirement.

Changed production paths are `AppModelLibrary.cs`, `ModelDownloadCoordinator.cs`, `ResumableVerifiedModelDownloadService.cs`, `ModelImportPage.Selection.cs`, `ModelImportPage.xaml.cs`, `ModelSelectionOperation.cs`, `OptimizationDestinationCard.xaml.cs`, and `OptimizationExportController.cs`. Changed tests are `AppModelLibraryTests.cs`, `ModelDownloadCoordinatorTests.cs`, `ModelImportDownloadedModelIntegrationTests.cs`, `ResumableVerifiedModelDownloadServiceTests.cs`, `OptimizationDestinationCardTests.cs`, and `OptimizationExportControllerTests.cs` under the existing UnitTests tree.

## Commands and results

Build command (with the installed VS2022 x64 C++ tools, Windows Kits `LIB`, and Visual Studio Installer path prepended):

```text
dotnet build tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj -c Debug -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:IlcUseEnvironmentalTools=true -m:1 --no-restore
```

Result: succeeded, 13 inherited Model Inspection fixture nullable/analyzer warnings, 0 errors. The installed VS2022 x64 C++ tool/library paths and `vswhere.exe` location were supplied for native test-fixture packaging; no product gate was weakened.

Packaged command form used Visual Studio 18.7 `vstest.console.exe`, the generated `GraniteEdgeAI.UnitTests.build.appxrecipe`, `tests\runsettings\OneWorker.runsettings`, non-zero `TestCaseFilter`, and TRX logger. Final owned filter was:

```text
/TestCaseFilter:"FullyQualifiedName~ModelImport|FullyQualifiedName~ModelDownload|FullyQualifiedName~OptimizationExport|FullyQualifiedName~OptimizationDestination"
```

Exact additional filters used with that same executable, recipe, `/Settings:C:\R41-F1\tests\runsettings\OneWorker.runsettings`, and the named `/Logger:trx;LogFileName=<file>.trx` were:

```text
focused: /TestCaseFilter:"Name~QueuedDiscardSettlesWithoutSpin|Name~DiscardUpgradeAtFinal|Name~NeverSettlingDiscard"
lifecycle (three separate invocations): /TestCaseFilter:"FullyQualifiedName~ModelDownloadCoordinatorTests|FullyQualifiedName~ResumableVerifiedModelDownloadServiceTests|FullyQualifiedName~AppModelLibraryTests|FullyQualifiedName~ModelImportDownloadedModelIntegrationTests|FullyQualifiedName~OptimizationExportControllerTests"
inclusive: /TestCaseFilter:"FullyQualifiedName~ModelImport|FullyQualifiedName~ModelDownload|FullyQualifiedName~OptimizationExport|FullyQualifiedName~OptimizationDestination|FullyQualifiedName~OnboardingModelInspectionNavigationTests"
mutation: /Tests:"UnexpectedProviderFaultPropagatesAndSettlesForRetry"
```

Closure commands were `git diff --check`; `rg` production declarations for each service/controller and forbidden interface; source-only PowerShell grouping of `x:Class`; recursive AppX `.gguf`/partial/checkpoint counts; project-file `DebugFixtures` condition inspection; and literal/regex scans of changed paths for `C:\Users\`, credentials, bearer strings, URLs with tokens, and private output. Their numeric results follow below.

Results:

| Evidence | Discovered | Executed | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|
| `R41-review11-owned.trx` | 197 | 197 | 197 | 0 | 0 |
| `R41-review11-lifecycle-1.trx` | 103 | 103 | 103 | 0 | 0 |
| `R41-review11-lifecycle-2.trx` | 103 | 103 | 103 | 0 | 0 |
| `R41-review11-lifecycle-3.trx` | 103 | 103 | 103 | 0 | 0 |
| `R41-review11-focused.trx` | 4 | 4 | 4 | 0 | 0 |
| `R41-review10-focused-green.trx` | 7 | 7 | 7 | 0 | 0 |
| `mutation-broad-conversion-red.trx` (intentional mutation) | 1 | 1 | 0 | 1 | 0 |
| `R41-review11-inclusive.trx` | 168 | 168 | 167 | 1 | 0 |

Arithmetic holds for every row. The inclusive failure is inherited C0-owned `GraniteEdgeAI.UnitTests.OnboardingModelInspectionNavigationTests.ChooseAnotherOwnerAwaitsRetirementBeforeChangingFrameContent`, timing out after about five seconds; F1 did not edit the forbidden shell owner. The CrossFeature project is absent from this canonical base and is unavailable, not passed.

Static closure: `git diff --check` passed; production source XAML count 48 with zero duplicate `x:Class`; production counts are exactly one `IModelDownloadService`, resumable service, HTTP transport, model library, and export controller; forbidden `IRecommendedModelDownloadService` count zero. The packaged UnitTests AppX contains 53 deliberate synthetic `.gguf` files (4,096,964 bytes), all under `TestFixtures`, zero outside that fixture root, and zero partial/checkpoint artifacts; the managed build did not emit a main-app AppX directory. Release fixture items are removed generally and re-included only under explicit Debug x64/fixture conditions. Sensitive scan found only deliberate synthetic private strings in tests used to prove they do not escape; no real credential, user path, host, prompt, provider output, or private destination was added.

## Build, package, native, and visual disposition

The strongest managed Debug x64 app and packaged tests built. Release x64 was blocked by required `GgufQuantizerStageDirectory`; exact x64 app staging also requires the official `OpenVinoOfficialWorkerStageDirectory`. Neither gate was weakened and no artifact was copied or fabricated. Unit/packaged tests do not prove Intel-native or performance acceptance.

Ignored privacy-safe locator: `TestResults/R41/visual/`.

| Scenario | Candidate | Size | Scale/theme | SHA-256 / bytes | Result and typed pairing |
|---|---|---:|---|---|---|
| `R41-F1-MI-idle-normal-100` | strongest Debug x64 exact app at capture time | 1440x753 | 100%, light | `83e72143406f8967af2a47adc1d4f1e62fda9caceb051bd08f605302456ea006` / 45767 | Auxiliary pass: centered idle import surface; paired with composition/accessibility tests. Not final-subject/full-matrix acceptance. |
| `R41-F1-MI-idle-reduced-100` | same | 800x700 | 100%, light | `635c2c6e7e7c2ed675a313a7b16b41ba9dadf0721c1a19e869297ad89046203a` / 30918 | Auxiliary pass: no clipping at reduced width; paired with responsive tests. Not final-subject/full-matrix acceptance. |

The third ready/keyboard capture was rejected because it did not visibly prove ready state or focus. The unexecuted scenario ledger is explicit:

| Scenario ID | Disposition | Exact dependency / behavioral pairing |
|---|---|---|
| MI-drag-valid | Unavailable visually | No drag injection in candidate; drop accessibility test covers overlay contract |
| MI-drop-invalid | Unavailable visually | No drag injection; rejection/live-region test covers behavior |
| MD-preparing | Unavailable visually | No download-state fixture control; coordinator test |
| MD-determinate | Unavailable visually | No bounded fake-network UI host; progress/card tests |
| MD-indeterminate | Not applicable to pinned artifacts | All five pinned entries have known lengths |
| MD-cancelling | Unavailable visually | No blocking provider in app host; immediate-state test |
| MD-cancelled-resumable | Unavailable visually | No controlled partial fixture; coordinator/library tests |
| MD-recovered-restart | Unavailable visually | No persisted fixture control; recovery tests |
| MD-stalled | Unavailable visually | No controlled transport in app; inactivity test |
| MD-retry | Unavailable visually | No controlled failure; generation/retry tests |
| MD-verifying | Unavailable visually | No bounded payload fixture; hash-boundary tests |
| MD-integrity-failure | Unavailable visually | No corrupt payload fixture; digest test |
| MD-storage-failure | Unavailable visually | No storage-fault fixture; IO/access-denied tests |
| MD-network-policy | Unavailable visually | No offline/metered control; policy tests |
| MD-handoff-ready | Unavailable visually | C0 navigation absent; opaque-event/claim tests |
| MI-retired-stale | Unavailable visually | Retirement is nonvisual; stale-event tests |
| OE-unbound | Unavailable on captured screen | Optimization surface not reachable in candidate; card tests |
| OE-bound | Externally blocked | Q1 service/verified target not production-bound; card tests |
| OE-exporting | Externally blocked | Q1 binding absent; controller tests |
| OE-cancelling | Externally blocked | Q1 binding absent; bounded-cancel tests |
| OE-cancelled | Externally blocked | Q1 binding absent; typed cancellation tests |
| OE-success | Externally blocked | Q1 publication absent; typed receipt tests |
| OE-failure-retry | Externally blocked | Q1 provider absent; typed failure/retry tests |
| A11Y-200 | Host control unavailable | No controlled DPI/text-scale harness |
| A11Y-high-contrast | Host control unavailable | No controlled HC capture harness |
| A11Y-reduced-motion | Visual capture unavailable | packaged reduced-motion behavior tests |

Creating these screens would require new fixture composition or foreign-owner wiring beyond this remediation. Packaged tests verify keyboard invocation, stable names/automation IDs, live-region semantics, disabled/running/cancel/retry states, reduced-motion rendering behavior, responsive widths, and stale-update suppression. Screenshots are not used as functional proof.

## Independent review

The first requirements review found four Important gaps: UI diagnostic observability, pre-operation rather than in-operation storage proof, absent drop-replacement proof, and incomplete visual evidence. Adversarial passes additionally found synchronous/unbounded cancellation callbacks, false in-flight evidence, local-preflight classification, cancellation-source retention, on-page cancel settlement, atomic-publish commit races, unavailable Retry actions, premature cleanup/replacement authority, result-losing export reconciliation, terminal commit-wins races, inactive-discard overlap, detached-cleanup rebind bypasses, duplicate discard authority, pre-render observer notification, mixed preserve/discard coalescing, shared caller cancellation tokens, late escalation finalization, unbounded discard settlement, and retirement of queued cleanup authority. All source/test findings were corrected across the review commits and immutable subject `69057dc7`; focused, full owned, repeated lifecycle, inclusive affected, and build gates were rerun. Both exact-subject independent rereviews returned CLEAN for Critical/Important findings. Visual and native limitations remain explicitly blocked/nonclaimed rather than represented as acceptance.

## Apply-checkable C0 integration proposal

On the currently attached live `ModelImportPage`, C0 should subscribe to `VerifiedDownloadInspectionReady`. In the handler, first verify the sender is still the live page, then call `TryClaimVerifiedDownloadInspection(e.OperationId, out ModelInspectionRequest? request)` exactly once. If claim fails, the event is stale and must be ignored. After successful ownership transfer, await the old page's `RetireForNavigationAsync()` in the same ordering used before any `StageFrame.Content` replacement/detachment, then construct/bind the existing Model Inspection destination from `request` through the direct in-memory contract and navigate. Never put `request`, its path, a storage token, or provider object in event args, frame/navigation parameters, logs, UI, or diagnostics. All Back/Return/replacement/shutdown paths must likewise await the live import/optimization page retirement task.

## Remaining blockers and mandatory nonclaims

- F1 did not implement or validate C0 shell/navigation integration. The full automatic verified-download-to-Model-Inspection production journey remains pending C0's event subscription, one-time claim, retirement ordering, and navigation wiring.
- F1 did not modify the Model Inspection backend or implement Q1 export storage/publication.
- F1 did not implement hardware inspection, compatibility calculation, optimization execution, or Chat.
- Unit/packaged tests do not prove Intel-native acceptance. Auxiliary/fixture screenshots do not prove Release/native packaging.
- Missing official stage directories were not fabricated or bypassed. Intel-native, Release package activation, performance, 200%/High Contrast, full visual-state acceptance, and C0 navigation remain external.
- No user model path, hostname, username, prompt, provider output, real secret, or private destination was committed.
- This branch was not merged to `main`.
