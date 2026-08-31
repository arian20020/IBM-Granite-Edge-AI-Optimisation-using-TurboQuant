# C0 R4 preliminary specialist reconciliation

## Disposition

C0 produced a clean, managed-verified preliminary candidate and published its immutable H1 issued base. This is a preliminary integration checkpoint, not final release acceptance. H1 R4 is the next authorized candidate-bound campaign; Q1 and E1 remain sequenced behind H1.

- Frozen source commit: `4748fe04f19afdf6b27c4c12502b84db325e7294`
- Frozen source tree: `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`
- Candidate branch: `integration/ucl-r4-specialist-reconciliation-v1`
- C0 implementation subject: `3be52f0b0763cd0ba164b13914d8b0b4f37433e8`
- C0 implementation tree: `5f50b480d39cbfbeb1ff74fab866b6a995418868`
- Immutable H1 issued-base ref: `refs/remotes/origin/integration/ucl-r4-h1-issued-base-v1`
- H1 issued-base commit/tree: `3be52f0b0763cd0ba164b13914d8b0b4f37433e8` / `5f50b480d39cbfbeb1ff74fab866b6a995418868`
- H1 prompt: `01-H1-R4-UCL-INTEL-MASTER-PROMPT.md`, 8098 bytes, SHA-256 `f09cd973e2da5321ef01dd3392206c49bcbe21da8d61d7d09219640bf29798cc`

The immutable H1 issued-base ref deliberately remains at the implementation subject. The coordinator branch advances by one documentation-only dispatch-record commit so H1 can read the committed `DISPATCHED` register without creating an impossible self-referential Git commit.

## Integrated provenance

| Order | C0 integration commit | Producer input |
| --- | --- | --- |
| 1 | `8d5fff6a` | S1 R4.1 subject `4e76c9f55a787ea8f51ba81d4c870743d41f9d24` |
| 2 | `97437c03` | H1 R3 subject `32b0cab7e4c6c30b4b3d3c3588f7fc394e9d26eb` |
| 3 | `6b409407` | M1 R4.1 subject `ca913c68eb62fdd70f1c408fd74e7b9afb66a546` |
| 4 | `1fd4d69e` | Q1 R3 subject `31302b4f46cf562271767e0bbcfd241bb4db28f5` |
| 5 | `c0a1adeb` | A1 R4.1 subject `7dbf42bd3a72ba8b11c8342ad0abb150e0268c3d` |
| 6 | `6b744162` | F1 R4.1 subject `69057dc7f5bf5849e12bb48e5924ae9fad1fa6db` |
| 7 | `97f17f8b` | T1 R4.2 subject `9b3bffefdd5f337803a52b938b141d8a56063f99` |
| 8 | `5e4d5aeb` | T1 canonical download-authority proposal applied |
| 9 | `3be52f0b` | C0 shared seams, conflict reconciliation, regressions, and portable test support |

## Semantic reconciliation

- Preserved S1 trusted process-environment and containment authority while adapting T1 integration tests to the production environment seam.
- Preserved H1 typed current-memory evidence and proportional reserve policy while adapting cross-feature and compatibility fixtures away from raw integer memory values.
- Preserved M1 Model Inspection source custody and project namespace while linking only exact internal sources needed by the managed cross-feature host.
- Preserved Q1 exact optimization execution identity, leases, registry lookup, persistent export, and runtime-only custody.
- Preserved A1 lifecycle ownership and added one idempotent shell lifetime cancellation source shared by exact Chat/export operations.
- Preserved F1 download and import ownership; the automatic readiness event remains path-free, C0 claims the exact in-memory request once and retires before replacement, then invokes the existing validated `ModelInspectionRequest` navigation boundary.
- OpenVINO Chat now consumes `CreateChatTargetAsync` from the exact result and lifecycle token; persistent export consumes `ExportPersistentAsync` from the exact result, selected destination, bounded byte policy, and lifecycle token. Live `LastPublishedDirectory` use was removed.
- Source conversion is tied to the exact selection cancellation token. Stale conversion completion or rejection cannot clear a replacement selection.
- Verified-download navigation faults and false installation results are observed and recover to a fresh import page rather than escaping an `async void` boundary.
- The optimization presentation fixtures are test-owned; production Release/package closure does not acquire a DebugFixtures dependency.
- Portable harnesses invoke fresh test assemblies through the approved signed `dotnet.exe` host when Windows Application Control rejects generated unsigned apphosts.
- The implementation subject froze with cleanup inventory and ledger reconciled at 711/711. This dispatch publication registers its six durable artifacts and reconciles the current scope at 717/717; the cross-feature source-link statement matches the enforced count of 62.

## Verification

The non-overlapping test total is 2105 discovered, 2105 executed, 2098 passed, 0 failed, and 7 skipped.

| Gate | Result |
| --- | --- |
| Cross-feature integration | 114/114 passed |
| Canonical Model Download Authority | 30/30 passed |
| Model Inspection contracts | 428/428 passed |
| Model/Hardware Compatibility | 1062/1062 passed |
| Hardware Inspection Foundation | 234/234 passed |
| Model Inspection Transport | 27/27 passed |
| Model Inspection Worker | 78/78 passed |
| Model Inspection WorkerClient | 121/121 passed |
| OpenVINO packaging focus | 11 discovered: 4 passed, 7 skipped because verified native stage inputs were unavailable |
| WinUI UnitTests managed Release x64 build | succeeded with 0 warnings and 0 errors |
| Application managed Release x64 build | succeeded with 0 warnings and 0 errors |
| Application managed Debug x64 build | succeeded with 0 warnings and 0 errors |
| Cleanup source/inventory contract | implementation subject 711/711; current dispatch publication 717 paths, 717 unique, exact ordinal order, 717 ledger rows |
| `git diff --check` | passed; line-ending conversion notices only |

Direct execution of the WinUI UnitTests DLL is blocked by absent package context with `REGDB_E_CLASSNOTREG`; it is not counted as a pass. Raw local test output is not committed because it contains machine paths and must be regenerated by C0/E1.

## T1 intentional RED disposition

All ten deterministic product obligations are closed on the C0 implementation subject. Exact per-case disposition is committed in `docs/audits/2026-08-30/C0-r4-issue-ledger.md`. No test was skipped, weakened, inverted, renamed away, or excluded to obtain the 114/114 cross-feature result.

## Honest blockers and non-claims

- The required pinned GGUF quantizer stage is absent. An ordinary Release/package build fails closed because `GgufQuantizerStageDirectory` is required.
- Seven controlled OpenVINO packaging tests remain skipped because their exact verified native-stage inputs are unavailable.
- Windows Application Control rejects freshly generated unsigned executables; managed DLL execution through the pinned signed SDK host remains available.
- No signed package installation, full native GGUF/OpenVINO/TurboQuant journey, Intel performance acceptance, or complete screenshot/accessibility gate is claimed.
- H1 must validate hardware facts, degraded optional-probe behavior, compatibility input projection, package/native availability, and visuals from the exact issued base.
- Q1 must not begin until C0 accepts and republishes H1. E1 must not begin until the accepted post-Q1 candidate is frozen.
- `main` was not merged or pushed.

## Independent review

The first read-only review found three Important issues: stale conversion ownership, unbounded verified-download navigation failure, and incomplete cleanup/coverage documentation. C0 corrected all three and added behavioral regressions. Re-review found no remaining Critical or Important publication blocker. A Minor host-resolution wording issue was also corrected before the implementation subject was frozen.
