# E1 R4 independent exact-candidate acceptance — v2 rerun

## Disposition

`BLOCKED BY EXTERNAL ENVIRONMENT`

All candidate-controlled preflight, deterministic, managed, source, security, privacy, schema, and cleanup gates executed by E1 passed. Native/package activation could not lawfully start because this host supplied none of the exact candidate manifest, asset manifest, verified native-stage manifests, or predecessor cleanup receipts required before the shared native lock may be acquired. No blocked or unexecuted gate is counted as a pass.

## Exact candidate and implementation binding

- Issued base ref: `refs/remotes/origin/integration/ucl-r4-e1-issued-base-v2`
- Tested base commit/tree: `b5d2cd34c57368efb9b122cddf16c2ffa2d3895e` / `a3e4d82095caa9688f30d2463fa5971c788fd33f`
- Coordinator ref: `refs/remotes/origin/integration/ucl-r4-specialist-reconciliation-v1`
- Coordinator commit/tree: `fa4174fa45b66f0e6c71b9127bc603fcdea1607a` / `a2fe0500954e05e3985ea2f6be5af4a22acf7463`
- Unchanged production subject commit/tree: `429298d3328a62d4fcf2f5f3f12821342be4a23c` / `2e92e172679d1558972846694306d28ad56e2cf4`
- E1 implementation subject commit/tree: `e3dee59e8fe57ae9b7176f851d3ca6acd9536dec` / `0fa34d9b169b598dca79e5c074813944e4c0e6e3`
- Return branch: `test/ucl-e1-native-acceptance-r4-v2`

The coordinator register marked H1 and Q1 accepted and E1 dispatched. Their already accepted lineage was verified from committed evidence; no new candidate-bound receipt was required at entry. The fresh isolated v2 worktree preserved the historical first worktree and return. Five historical E1-only test-infrastructure commits were replayed, then the verifier was corrected test-first. No production file changed.

## Two-phase issue-evidence result

The fail-closed verifier resolved the schema-v4 closure from the pushed candidate, verified its Git blob and remote identity, verified the evidence catalog at its declared subject, checked every declared file hash and byte count, and enforced exact identifier sets and command reachability.

- Closure: `docs/audits/2026-08-30/evidence/C0-r4-issue-closure.json`, SHA-256 `f047c491a3dd5004ccb85bc1860daeb1d0a3339a92025442cd0c010bca48469d`, 6490 bytes.
- Catalog: `docs/audits/2026-08-30/evidence/C0-r4-issue-evidence-index-v2.json`, SHA-256 `13dd8e4e103d82dd8449e4b37e46b538725aea67fdeef18d92dd02760f8d488c`, 18924 bytes, evidence subject `615d9e08b0b58a6c775a290e37a2f45b01f2efb3` / `f7be57a9d912fe4a1f6285ba9c606189bca2117b`.
- Preflight: R3-001 through R3-019 and R3-021 closed exactly once; all R4-C0-001 through R4-C0-008 closed exactly once.
- Post acceptance: R3-020 and R3-022 were correctly pending at entry. E1 now supplies a candidate-bound return receipt and an honest external-prerequisite disposition. Their native/package/visual/performance requirements remain unexecuted, not passed.

The preflight verifier regression was observed red before implementation, then three focused schema-v4 tests passed. Independent review identified the missing second phase; a new post-acceptance test was observed red, then the focused post-capable set passed 4/4. The post verifier binds exact base and implementation identities, hashes the report and manifest, reconciles managed arithmetic, rejects blocked gates counted as passes, and fail-closes cleanup, App Control, lock, package-attempt, external-block, and disposition fields. The real candidate preflight passed 1/1. A subsequent fresh run against the finalized candidate-bound post record was blocked at assembly load by App Control `0x800711C7`; it is not counted as a pass.

## Complete authoritative command arithmetic

Visual Studio VSTest x64 provided non-zero discovery and execution. The repository-pinned SDK `10.0.301` was absent; installed SDK `10.0.400` was selected only during execution and `global.json` was restored before the return commit.

| Command/gate | Discovered | Executed | Passed | Failed | Skipped | Result |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| E1 assembly list | 53 | 0 | 0 | 0 | 0 | non-zero discovery |
| E1 deterministic selection | 34 | 34 | 34 | 0 | 0 | passed |
| Real schema-v4 preflight | 1 | 1 | 1 | 0 | 0 | passed |
| OpenVINO optimization | 55 | 55 | 55 | 0 | 0 | passed |
| OpenVINO worker client | 17 | 17 | 17 | 0 | 0 | passed |
| GGUF quantization contracts | 7 | 7 | 7 | 0 | 0 | passed |
| GGUF worker client, first attempt | 19 | 19 | 18 | 1 | 0 | cleanup-verification failure |
| GGUF worker client, isolated rerun | 19 | 19 | 19 | 0 | 0 | passed |
| Q1 identity/lifecycle | 51 | 51 | 51 | 0 | 0 | passed |
| Cross-feature integration | 115 | 115 | 115 | 0 | 0 | passed |
| Security audit / App Control policy | 12 | 12 | 12 | 0 | 0 | passed |
| OpenVINO broad supplementary suite | 496 | 496 | 489 | 0 | 7 | mixed; seven guarded skips |
| Final candidate-bound post evaluator | 0 | 0 | 0 | 0 | 0 | blocked by App Control `0x800711C7` |
| Native/package/visual/accessibility/performance | 0 | 0 | 0 | 0 | 0 | blocked before lock |

The non-overlapping final passing receipt total is 311 discovered, 311 executed, 311 passed, 0 failed, 0 skipped: deterministic 34 + real preflight 1 + optimization 55 + OpenVINO worker 17 + GGUF contracts 7 + final GGUF worker 19 + Q1 51 + cross-feature 115 + security 12. The broad suite and the superseded first GGUF attempt are disclosed but excluded from that total. Its seven guarded skips are not passes. The first GGUF cleanup failure did not reproduce in the immediate complete isolated rerun; no production change was made.

## Package, App Control, native lock, and cleanup

- Package membership/capability/closure and App Control security tests passed 12/12. A later fresh E1 assembly rebuild was independently blocked at load by App Control `0x800711C7`; policy was not weakened.
- Host prerequisite discovery found no candidate manifest, asset manifest, H1/M1/Q1 native manifest inputs, verified OpenVINO/GGUF stage inputs, or predecessor native receipt store.
- The controlling sequence requires all exact verified stages before acquiring the shared native lock. Therefore lock acquisitions were 0, native attempts were 0, and the lock was absent before and after the audit.
- Signed package construction/activation, non-zero packaged discovery, worker/model/native journeys, real-model Chat/export, screenshots, keyboard/UIA, 200% text, High Contrast, reduced motion, visual comparison, and performance measurements were not executed and are not claimed.
- No candidate-root app, worker, converter, quantizer, llama, or OpenVINO process remained. Ignored raw TRX/build output is local-only. No model bytes, secrets, proxy data, environment dump, or private diagnostic path is committed.
- Machine security, signing, hashes, manifests, execution policy, trust roots, firewall, and App Control were not weakened. Main was neither merged nor pushed.

## Independent review

An independent reviewer found the missing post-acceptance evaluator and two evidence inconsistencies. E1 added the evaluator test-first, changed discovery to a non-pass command disposition, and removed the unsupported receipt-review claim. A final re-review is required after these corrections; the final result is reported at handoff, not encoded in the closed receipt schema.

## C0 intake

This return establishes that candidate-controlled layers passed but does not approve integration. C0 must provide the exact verified native stages, candidate/asset manifests, predecessor cleanup receipts, and an authorized signed package environment, then dispatch another complete acceptance execution if native approval is required. C0 may record but must not rewrite E1's disposition.
