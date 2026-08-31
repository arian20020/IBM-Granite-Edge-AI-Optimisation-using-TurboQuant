# C0 R4 final-candidate reconciliation and E1 rerun dispatch

## Disposition

C0 accepted the H1 and Q1 R4 returns, completed the required coordinator-owned production wiring, remediated every Critical or Important lifecycle finding, and froze the exact post-Q1 product subject. E1's first return was `CHANGES REQUIRED` because the candidate-controlled closure omitted all 22 R3 identifiers. C0 corrected that evidence defect without changing production, froze replacement evidence candidate `b5d2cd34c57368efb9b122cddf16c2ffa2d3895e` / `a3e4d82095caa9688f30d2463fa5971c788fd33f`, and formally redispatched E1 under a non-circular two-phase acceptance contract. Release acceptance remains pending the complete E1 rerun.

- Frozen historical source: `4748fe04f19afdf6b27c4c12502b84db325e7294` / `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`
- Coordinator branch: `integration/ucl-r4-specialist-reconciliation-v1`
- Immutable replacement E1 issued-base ref: `refs/remotes/origin/integration/ucl-r4-e1-issued-base-v2`
- Replacement evidence candidate: `b5d2cd34c57368efb9b122cddf16c2ffa2d3895e` / `a3e4d82095caa9688f30d2463fa5971c788fd33f`
- Unchanged product subject: `429298d3328a62d4fcf2f5f3f12821342be4a23c` / `2e92e172679d1558972846694306d28ad56e2cf4`
- E1 rerun branch: `test/ucl-e1-native-acceptance-r4-v2`
- E1 prompt: `03-E1-R4-EXACT-CANDIDATE-MASTER-PROMPT.md`, 7803 bytes, SHA-256 `3d48774a58d109f72d77d70bec0c01d3f9baf312d81fa22f54113a622dfa9e07`

The replacement immutable E1 issued-base contains the committed closure and its prior-commit evidence catalog. The coordinator branch advances afterward only with return-intake and dispatch records. E1 must branch from the immutable v2 issued-base ref, not from the later coordinator documentation tip.

## E1 first-return intake and correction

E1 returned `CHANGES REQUIRED` at `dba63fcfcacc7d8ac541e863ff609fa785ee4802` / `273d64f48275eb43079ae4168ee5794546728c01`. C0 independently verified its remote equality, ancestry, owned-path scope, schema validity, privacy, arithmetic, cleanup, and exact artifact hashes. Its release-blocking finding was reproduced: `R3-001` through `R3-022` each occurred zero times in the original closure.

The original entry gate also required `R3-020` and `R3-022`—the current E1 receipt and package/native acceptance—to be closed before E1 could attempt those same gates, while its verifier rejected the R4 findings required by the master prompt. C0 therefore published `C0-R4-ISSUE-CLOSURE-V4`: 20 preflight-resolvable R3 findings and all eight R4 findings are closed with executable evidence; only `R3-020` and `R3-022` remain pending for E1 post-attempt evaluation. No native or release pass was invented.

## Accepted specialist provenance

| Worker | Issued base | Returned tip/tree | Implementation subject/tree | C0 decision |
| --- | --- | --- | --- | --- |
| H1 | `3be52f0b` / `5f50b480` | `75689afc` / `7b5b4001` | `99294b54` / `e96ce082` | accepted after C0 trust-boundary remediation |
| Q1 | `218ad08f` / `78c96061` | `8627de71` / `d6022a7e` | `8769ec80` / `b068ca7f` | accepted after C0 lifecycle remediation |

For Q1, C0 independently verified remote equality of the required validation ref and schema-compatible audit alias, ancestry from the issued base, returned hashes and byte counts, schema validity, privacy, owned paths, arithmetic, and blockers. Q1's four returned commits were integrated as `2cf766d0`, `5df9859a`, `16dca7ed`, and `6cb2addb`.

## C0 post-Q1 reconciliation

`b63e3913eddffaf7eb6d25b0c0058424bae6ef06` connected the exact optimization destinations to the coordinator-owned shell. Independent review then exposed lifecycle races that were outside Q1's authorized production-wiring scope. C0 closed them in `429298d3328a62d4fcf2f5f3f12821342be4a23c`:

- journey-scoped lifecycle ownership closes destination admission before cancellation and join;
- duplicate Chat target admission is rejected without replacement or premature disposal;
- a target-use lease retains custody through activation and controller initialization;
- Chat, Back, Done, and shutdown share one handoff gate with exact coordinator/page revalidation;
- synchronous and asynchronous shutdown converge on one published ordered task;
- cancellation callback and `async void` faults are bounded and reported;
- Cancel remains able to interrupt Confirm rather than being suppressed by a global intent lock;
- destination names are collision-resistant and checked absent; coordinator cleanup is unconditional.

The final independent re-review found no remaining Critical or Important issue. Machine-readable closure is in `docs/audits/2026-08-30/evidence/C0-r4-issue-closure.json`.

## Verification

The final non-overlapping affected managed matrix is 466 discovered, 466 executed, 466 passed, 0 failed, 0 skipped.

| Gate | Result |
| --- | --- |
| OpenVINO optimization | 55/55 passed |
| OpenVINO contracts | 202/202 passed |
| OpenVINO worker client | 17/17 passed |
| GGUF quantization contracts | 7/7 passed |
| GGUF quantization worker client | 19/19 passed |
| Q1 identity and lifecycle | 51/51 passed |
| Cross-feature integration | 115/115 passed |
| Security audit | 12/12 passed, supplementary to the non-overlapping total |
| Cleanup source/inventory verifier | 3/3 passed; 729/729 at the implementation subject and 731/731 after the two E1 dispatch artifacts are registered |
| Managed application Debug x64 build | succeeded with 0 warnings and 0 errors using explicit external-stage opt-outs because exact native inputs are absent |
| WinUI UnitTests project build | succeeded with 13 inherited warnings and 0 errors |
| Independent C0 wiring re-review | PASS; no remaining Critical or Important finding |

`global.json` remained byte-for-byte at its pinned `10.0.301` value. Local managed verification temporarily selected installed SDK `10.0.400`, then restored the file before the implementation commit.

## Honest blockers and non-claims

- Q1 did not receive the native authorization/lock; no real model/tool, screenshot, package, or performance result is claimed from Q1.
- Exact native GGUF/OpenVINO stage inputs remain absent. No binary, hash, or stage identity was invented.
- Windows Application Control blocks freshly generated unsigned apphosts and the packaged WinUI test runner (`0x800711C7`). Policy was not weakened.
- The managed application build used explicit external-stage opt-outs and is not signed-package or native acceptance.
- No signed package, complete native journey, Intel performance gate, screenshot/accessibility gate, or final release approval is claimed.
- E1 owns independent tests, evidence, and its verbatim verdict only. E1 may not make product fixes; any product correction invalidates its evidence and requires a new frozen base and complete rerun.
- `main` was not modified or pushed.

## Formal E1 rerun dispatch

E1 may begin only from `refs/remotes/origin/integration/ucl-r4-e1-issued-base-v2` after verifying it equals commit `b5d2cd34c57368efb9b122cddf16c2ffa2d3895e` and tree `a3e4d82095caa9688f30d2463fa5971c788fd33f`. The candidate-controlled `C0-r4-E1-rerun-contract.md`, coordinator register, and `C0-r4-E1-rerun-launch-packet.json` are the formal rerun dispatch record. E1 must restart the full matrix, own the test-only verifier correction, and return exactly one allowed master-prompt disposition.
