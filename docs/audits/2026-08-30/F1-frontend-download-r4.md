# F1 R4 frontend lifecycle and verified-download audit

## Disposition

F1-owned implementation is complete on `audit/ucl-f1-frontend-download-remediation-r4`. Managed Debug x64 build and packaged tests pass. Exact RID/native staging and post-fix exact-app screenshot acceptance are **blocked** on this non-authoritative machine; this report makes no Intel-native or performance acceptance claim.

Canonical base: commit `4154ed6194864c6852728bd81e7373d7e7bc3ff2`, tree `64fe1e175139f981b5636adb8815806f86c12498`. Immutable implementation subject: commit `ac5965a2179fb90b04c80ee8a0d49eaf87ada407`, tree `df3d7816554dbcb94ed2b9aa7ec1ea5cc75118aa`.

## Delivered behavior

- Preserved the canonical five-entry pinned Granite catalog and the single production chain `IModelDownloadService` -> `ResumableVerifiedModelDownloadService` -> `HttpModelDownloadTransport` -> `AppModelLibrary`. No obsolete R3 `IRecommendedModelDownloadService` or prototype backend was ported.
- Ported the useful F1 R3 lifecycle behavior: task authority before provider work, immediate cancellation UI, late/stale publication rejection, isolated observer/cancellation failures, bounded idempotent retirement, picker/download replacement suppression, and stable page retirement seams.
- Corrected changed-ETag resume handling, settled-partial discard, non-cooperative success after cancellation, startup-recovery retirement, borrowed coordinator ownership, stale dispatcher callbacks, actionable failure copy, percentage/byte progress, and determinate/indeterminate rendering.
- Automatic verified-download handoff reuses `SubmitInputAsync`, then retains the path-bearing inspection request inside `ModelImportPage`. `VerifiedDownloadInspectionReadyEventArgs` contains only operation identity and safe display name; C0 must claim the capability directly rather than put it in an event or `Frame` parameter.
- Added a typed optimization export controller and UI with exact canonical-result target construction, full authority-bound receipt validation, cancellation/retry states, observer isolation, and bounded retirement. It does not implement Q1 storage or read an ambient directory.

## Production reachability and integration ownership

| Surface | F1 state | Required integration |
|---|---|---|
| Download card/backend | Production composed on Model Import | Existing composition retained |
| Verified download inspection | Path-free opaque event plus one-time page claim | C0 subscribes, claims directly, installs into Model Inspection without a navigation parameter |
| Model Import retirement | `ModelImportPage.RetireForNavigationAsync()` | C0 awaits before replacing/detaching content and at shutdown |
| Optimization retirement | `OptimizationPage.RetireForNavigationAsync()` | C0 awaits before replacing/detaching content and at shutdown |
| Persistent export | Disabled until exact result/service binding | Q1 calls its `ExportPersistentAsync(state.Result, ...)` implementation; C0 constructs the target with `VerifiedPersistentExportTarget.FromExecutionResult(state.Result)` and binds it before enabling Save |

Exact C0 shell patch: before every `StageFrame.Content` replacement, detachment, Back, Return, or shutdown, detect the attached import/optimization page and await its `RetireForNavigationAsync()` task. For automatic download completion, handle `VerifiedDownloadInspectionReady`, call `TryClaimVerifiedDownloadInspection(e.OperationId, out request)` on that same live page, create/bind the Model Inspection destination directly, and do not place `request` in an event or `NavigationEventArgs.Parameter`. For a persistent optimization success, pass the real plan/configuration identities into `OptimizationPresentationFactory.Success`, derive the export target only from `state.Result`, and bind Q1's service before Save becomes reachable. F1 intentionally did not edit `OnboardingShellPage.xaml.cs`, `MainWindow`, or Q1-owned storage/registry code.

## Verification

Behavioral RED was recorded before implementation: lifecycle `3` executed (`1` pass, `2` fail); cancellation `2/2` failed; picker `1/1` failed; optimization `1/1` failed. Failures covered late completion, observer escape, delayed cancellation state, cancellation callback escape, stale picker completion, and unverified Save enablement.

Final managed evidence:

- Production app design-time Debug x64 build: succeeded, `0` warnings, `0` errors.
- Packaged UnitTests Debug x64 build: succeeded, `13` inherited warnings, `0` errors (nullable Model Inspection fixture warnings and obsolete MSTest attributes; no F1-owned nullable warnings).
- F1 owned packaged filter: `86/86` passed (`owned-handoff.trx`).
- Race-heavy lifecycle/download/export filter: `59/59` passed in three serialized post-review runs.
- Post-custody privacy/handoff filter: `24/24` passed (`privacy-final.trx`).
- ModelImport/Onboarding non-shell owner filter: `35/35` passed.
- Inclusive affected run: `104` executed, `103` passed, `1` failed when the host later aborted on inherited `OnboardingModelInspectionNavigationTests.ChooseAnotherOwnerAwaitsRetirementBeforeChangingFrameContent`. Baseline serial evidence had `145` total, `141` passed, `4` failures; the other three are the same C0-owned shared-shell retirement family (`NavigationAndShutdownShareOneRetirement`, `ReentrantConcurrentReturnAndShutdown`, and `ThrowingRetirement`). F1 did not conceal or modify these forbidden-owner defects.
- Three lifecycle stress runs and the final owned run use Visual Studio 18.7 packaged AppContainer VSTest with one worker; discovery was non-zero.
- `git diff --check` passed. Production duplicate scan found exactly one each of the download service/implementation/transport/library and export controller; `IRecommendedModelDownloadService` count is zero. Sensitive/path scans found no new export URL, credential, user path, or ambient-directory authority. Production AppX layout contains `0` `.gguf` and `0` partial/checkpoint files (test AppX intentionally contains GGUF fixtures).

The candidate CrossFeature project is absent from the canonical F1 base. A separate dirty `C:\R4-T1` worktree belongs to the active T1 worker and was not touched, so CrossFeature is external/unavailable here, not reported as passed.

## Native/package blockers

The managed packaged build succeeds. Exact Debug x64 RID app build is blocked by the required external property `OpenVinoOfficialWorkerStageDirectory`. Release x64 packaging is blocked by `GgufQuantizerStageDirectory` when GGUF quantizer packaging is enabled. These official artifact stages were not fabricated or bypassed. Native disposition is `blocked`; Intel-native and performance acceptance remain for the authoritative integration/acceptance machines.

## Visual and accessibility evidence

The exact packaged app was launched and inspected at normal and reduced widths. Visible states retained the centered light baseline without clipping. Runtime inspection found two Important defects—primary Cancel discarded resumable data, and determinate download showed an indeterminate ring/stale automation name—which were fixed and covered by packaged regressions. Because rebuilding the exact RID app after those corrections is blocked by the official OpenVINO stage, the following ignored files are **pre-fix defect evidence**, not post-fix visual acceptance:

| Capture | Bytes | SHA-256 |
|---|---:|---|
| `model-import-idle-normal-100.png` | 37871 | `538baf59dac681639557aedc4e8dcd17700976f7db7495f43c5e94e6bcb435af` |
| `model-import-idle-download-card-100.png` | 42939 | `4f8c47bd2ce7aadf518b3bd08f93a963089f34fed51168c141dd29dd2f703306` |
| `model-import-download-card-reduced-100.png` | 41836 | `9f79385e7570dcbb48ce6d40277fe59d441de85aaf15b302142491ebfa898bdf` |
| `model-download-active-reduced-100.png` | 43354 | `d63a59c601d31329fb54601b8e623790c3bd2bf28066327c1d8e80225bcc849f` |
| `model-download-cancelling-reduced-100.png` | 43855 | `8cc8d5b8bcb307add3d2b03507ecd2a6fc840198c95eef7856c93fce7f61c795` |

Packaged tests validate stable automation IDs/names, live-region semantics, keyboard action routing, reduced-motion determinate rendering, disabled/running/cancel/retry export states, and responsive layouts. The complete file-13 post-fix screenshot matrix, optimization states, 200% scaling, High Contrast, and exact automatic inspection screen remain blocked/uncomposed until the C0/Q1 seams and official RID artifacts are present.

## Independent review

Requirements and adversarial reviewers reported no remaining Critical issue in F1-owned code after remediation. Their Important findings drove the settled discard, cancellation ordering, bounded retirement, ETag continuity, borrowed ownership, dispatcher identity, bounded UI handlers, full export receipt authority, canonical-result constructor, failure-copy, percentage-progress, recovery lifetime, and path-custody corrections. Production export enablement and shell awaiting are intentionally documented above as C0/Q1 integration work because the master prompt forbids F1 from editing those owners.
