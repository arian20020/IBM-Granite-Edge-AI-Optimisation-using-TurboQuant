# R4 T1 cross-feature integration report (r3)

## Disposition

**T1 assignment complete; integrated product changes are required.** The T1-owned test implementation and managed verification are complete on the reviewed implementation subject. The result is intentionally not an all-green product acceptance: five T1 cross-feature contracts remain RED against current product behavior, and 22 Model Inspection failures match the inherited authoritative baseline.

This run was performed on a non-authoritative development machine. It makes no Intel-native, accelerator, packaging, visual, or performance acceptance claim.

## Git identity and custody

| Item | Value |
| --- | --- |
| Frozen source commit | `4748fe04f19afdf6b27c4c12502b84db325e7294` |
| Frozen source tree | `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91` |
| T1 base commit | `c2db11dedcd2bb6e7345e4c8f4b93da928d0af1f` |
| T1 base tree | `839bc873180839f3d3cb7fdd922f59bb204a3886` |
| Implementation subject commit | `3b5f6eecaf6af4fea985be7549c43e8bff0fb5e7` |
| Implementation subject tree | `62db4f2b0f6bbd1e3a8a4e6b5946003bf43f144b` |
| Branch | `test/ucl-t1-cross-feature-remediation-r3` |
| Transport | `refs/remotes/origin/test/ucl-t1-cross-feature-remediation-r3` |

The implementation subject is frozen before this report and receipt. The final handoff commit is a descendant containing only the durable handoff documents in addition to that subject.

## Scope delivered

The implementation subject changes only `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/**`: 13 files, 1,934 insertions, and 75 deletions. It adds or strengthens:

- 8/16/32 GiB proportional memory planning boundaries and schema-hostile inputs.
- Real reducer identity, stale/retry, terminal substitution, and orphan staging behavior.
- Route privacy and quantizer child-environment isolation, including private `TEMP`/`TMP` handling.
- Reparse-point custody behavior using real filesystem operations.
- GGUF chat lifecycle, persistent-output recovery, Model Inspection handoff, and production compatibility bindings.
- Exact canonical-source compile-link integrity plus active call-path and lifecycle-token checks.
- A parser that masks comments and literals, evaluates active preprocessor branches, rejects declarations/local functions/default tokens, and validates active API ranges.

The detailed requirement-to-test mapping, individual method inventory, and C0 follow-up ownership are in `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/COVERAGE-MAP.md`. Direct canonical source links are guarded by compile-link integrity tests; migration away from those links remains an explicit C0 action.

## Required RED contracts

The T1 suite discovers and executes 99 tests: 94 pass, 5 fail intentionally, and 0 skip. The five failures are the required product-gap signals:

1. `TerminalSuccessWithSubstitutedJourneyIdentityIsSuppressed`
2. `RetiringUnsealedLeaseLeavesNoStagedOutputOrPublication`
3. `QuantizerChildEnvironmentIsAllowlistedAndDiagnosticsDisabled`
4. `RecommendedModelDownloadActionIsFunctionallyWired`
5. `OpenVinoChatAndExportSourceCompositionRequiresExactResultAndLifecycleToken`

These failures require integrated product changes by the mapped C0/Q1/S1/F1 or download-integration owners. T1 did not change production code.

## Verification ledger

All paths below are repository-relative under `TestResults/`. Hashes are SHA-256 of the final TRX files.

| Suite | Selected | Passed | Failed | Skipped | TRX | Bytes | SHA-256 |
| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |
| T1 cross-feature | 99 | 94 | 5 | 0 | `R4-T1-final-crossfeature/R4-T1-crossfeature.trx` | 155920 | `f49edc784e943950083119c88dc5e253e5043558b51fbeb6d40dab73b0e79746` |
| Compatibility | 1050 | 1050 | 0 | 0 | `R4-T1-final-compat/compat.trx` | 1476103 | `bde55f2958d5d67d5ec47d8a9f80b05e4ebdc3d6e8b4ff2a3f367c40d0fe6643` |
| OpenVINO contracts | 205 | 205 | 0 | 0 | `R4-T1-final-ovcontracts/ovcontracts.trx` | 307434 | `785fae04d3926cad15d1ef9de4483f18524018e0d54075197db67a176bf0be41` |
| OpenVINO WorkerClient | 13 | 13 | 0 | 0 | `R4-T1-final-ovclient/ovclient.trx` | 19731 | `2b0e69a053dc89f0d96a151e8254b7b4c2e9bebca49a80ad8de1773c33c2f336` |
| OpenVINO unit | 409 | 402 | 0 | 7 | `R4-T1-final-ovtests/ovtests.trx` | 639288 | `0c7643ffa76236559ee5359135d39e92714e9b5b54b1e04955472f8bc0b2ee44` |
| Model Inspection contracts | 357 | 335 | 22 | 0 | `R4-T1-final-micontracts/micontracts.trx` | 696351 | `b2fadfa9645070378ff20c50c52d2f2229ea8e1a1be9700b72c25431746a1dcf` |
| GGUF quant contracts | 7 | 7 | 0 | 0 | `R4-T1-final-gqcontracts/gqcontracts.trx` | 10883 | `a5e6c558243f88fd05532efa1c7a5d9d35eaaecabd4dcb26edba860c4be53214` |
| GGUF quant WorkerClient | 8 | 8 | 0 | 0 | `R4-T1-final-gqclient/gqclient.trx` | 12760 | `3920134ad0e6062f1b7c650cb8a776ce384844594edd2ff36285fcfeb0065fe1` |
| GGUF runtime contracts | 12 | 12 | 0 | 0 | `R4-T1-final-grcontracts/grcontracts.trx` | 17523 | `d57ce6e5f11cb1cb26511339c45642af25ab4df18970f946185ebed9b5b6b534` |
| GGUF runtime WorkerClient | 9 | 9 | 0 | 0 | `R4-T1-final-grclient/grclient.trx` | 14151 | `a11a160a7029dcdd9147c398479e305d162822e33483176fbea0b143a5dd11ed` |

Aggregate under the R4 receipt convention: **2,169 discovered; 2,169 executed/selected; 2,135 passed; 27 failed; 7 skipped**. Arithmetic reconciles: 2,135 + 27 + 7 = 2,169. VSTest's raw TRX `executed` counters exclude skipped outcomes and therefore sum to 2,162; the receipt's `executed` field records all selected outcomes, including the seven declared skips. The 27 failures are exactly the five required T1 RED contracts plus the 22 inherited Model Inspection baseline failures. The seven skips are declared native-stage OpenVINO tests.

Fresh Release x64 managed build completed with exit code 0 while package producer requirements and AppX generation were explicitly disabled. Debug and Release x64 application executables were produced. `git diff --check`, ownership, duplicate changed-file hash, sensitive-diff, duplicate-method, and exact-process scans were clean at implementation verification.

## Managed application and package gates

The bounded application smoke attempt exited before creating a window. Windows reported `System.TypeInitializationException` with inner COM error `0x80040154 (REGDB_E_CLASSNOTREG)` because the Windows App Runtime deployment class is not registered on this machine. Consequently:

| Scenario | Severity | Expected | Actual | Owner/disposition | Evidence |
| --- | --- | --- | --- | --- | --- |
| APP-START | Blocking environment/package gate | Main application window | Process exits before UI initialization | Environment/C0 packaging; no T1 production correction | No screenshot can truthfully be captured |

The screenshot directory is empty by design; no screenshot, visual defect, or post-fix evidence was fabricated. A package-enabled build also failed closed with `GgufQuantizerStageDirectory is required when GGUF quantizer packaging is enabled.` This confirms that the required native producer stage was not supplied on this machine.

## Review record

Independent specification and code-quality reviews approved the final implementation subject after all Critical and Important findings were resolved. Review verification included a fresh Release build, focused parser test, three identical full T1 runs (99/94/5/0), and a clean subject diff check.

## Remaining acceptance items and non-claims

- Five intentional T1 RED contracts require integrated product remediation and later green confirmation.
- Twenty-two Model Inspection failures remain the inherited authoritative baseline.
- Seven OpenVINO native-stage tests remain declared skips.
- Windows App Runtime registration/packaging must be corrected before UI screenshot evidence can be captured.
- The GGUF quantizer producer stage must be supplied for package verification.
- Intel-native hardware, acceleration, energy, latency, throughput, package install, and performance acceptance were not run and are not claimed.

T1 is not an evidence-manifest worker under the R4 receipt schema, so the receipt records `evidenceManifest: null`; this report and its receipt are the required durable T1 handoff artifacts.
