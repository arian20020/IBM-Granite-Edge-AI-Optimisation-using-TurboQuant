# E1 R4 independent exact-candidate acceptance — corrected v2 rerun

## Disposition

`BLOCKED BY EXTERNAL ENVIRONMENT`

The new implementation subject passed every freshly executed candidate-controlled deterministic, managed, source, security, privacy, schema, and cleanup gate. Exact package/native prerequisites remain absent, so package activation, native journeys, visual, accessibility, and performance gates were not executed and are not passes. R3-020 is satisfied by the exact-candidate receipt and precise external block. R3-022 remains blocked and unresolved.

## Exact binding

- Issued base ref: `refs/remotes/origin/integration/ucl-r4-e1-issued-base-v2`
- Tested base commit/tree: `b5d2cd34c57368efb9b122cddf16c2ffa2d3895e` / `a3e4d82095caa9688f30d2463fa5971c788fd33f`
- Coordinator commit/tree: `fa4174fa45b66f0e6c71b9127bc603fcdea1607a` / `a2fe0500954e05e3985ea2f6be5af4a22acf7463`
- Unchanged product subject commit/tree: `429298d3328a62d4fcf2f5f3f12821342be4a23c` / `2e92e172679d1558972846694306d28ad56e2cf4`
- E1 implementation subject commit/tree: `6e8eaabea5e8b0fbf37fe1b9131335247bb0a64e` / `a6c5958ec917cd110ad24361a4a9f8dec0ebb21d`
- Return branch: `test/ucl-e1-native-acceptance-r4-v2`

Only E1 test infrastructure changed in the implementation subject. No product file, tested base, or main branch changed.

## Two-phase issue result

Schema-v4 preflight verified R3-001 through R3-019 and R3-021 closed exactly once, R3-020 and R3-022 pending at entry, and all eight R4-C0 findings closed exactly once. It also verified the pushed candidate, closure Git blob, evidence catalog subject/blob, command arithmetic, declared paths, and production reachability.

The post-acceptance verifier was corrected test-first. The first red run failed because the old contract did not understand the required package/native evidence fields. Independent review then found that an external block with otherwise passing evidence could still close R3-022; the new edge-case test failed on `true` before the predicate was corrected. The final green focused run passed 9/9 and proves:

- an external block closes R3-020 but not R3-022;
- zero package attempts cannot close R3-022;
- zero native-lock acquisitions cannot close R3-022;
- approval closes R3-022 only after package, App Control, native, cleanup, and E2E evidence executed and passed;
- `CHANGES REQUIRED` closes neither finding without its precise independent evidence.

Current post result: `R3-020=true`; `R3-022=false`.

## Fresh command arithmetic

The repository-pinned SDK `10.0.301` was unavailable. Installed SDK `10.0.400` was selected transiently and `global.json` was restored before commit. Visual Studio VSTest 18.9 x64 supplied discovery and execution.

| Command/gate | Discovered | Executed | Passed | Failed | Skipped | Result |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| E1 assembly list | 60 | 0 | 0 | 0 | 0 | narrative discovery only |
| E1 deterministic selection | 40 | 40 | 40 | 0 | 0 | passed |
| Schema-v4 candidate preflight | 1 | 1 | 1 | 0 | 0 | passed |
| Combined legacy/v4 preflight attempt | 2 | 2 | 1 | 1 | 0 | legacy schema-v3 route rejected schema v4; excluded |
| OpenVINO optimization | 55 | 55 | 55 | 0 | 0 | passed |
| OpenVINO worker client | 17 | 17 | 17 | 0 | 0 | passed |
| GGUF quantization contracts | 7 | 7 | 7 | 0 | 0 | passed |
| GGUF quantization worker client | 19 | 19 | 19 | 0 | 0 | passed |
| Q1 identity/lifecycle | 51 | 51 | 51 | 0 | 0 | passed |
| Cross-feature integration | 115 | 115 | 115 | 0 | 0 | passed |
| Security/package/App Control | 12 | 12 | 11 | 1 | 0 | App Control `0x800711C7` blocked hostile fixture |
| OpenVINO broad supplementary | 496 | 496 | 489 | 0 | 7 | mixed; guarded skips excluded |
| Final candidate-bound evaluator | 1 | 1 | 1 | 0 | 0 | passed; `R3-020=true`, `R3-022=false` |
| Native/package/UI/performance | 0 | 0 | 0 | 0 | 0 | blocked before lock; not passed |

The non-overlapping fresh passing total is 306/306: 40 + 1 + 55 + 17 + 7 + 19 + 51 + 115 + 1. The security suite's 11 passes are not split from its failed command. Supplementary, overlapping, failed, skipped, blocked, and unexecuted rows are excluded.

The post record's `managedExecuted=317`, `managedPassed=316`, `managedFailed=1`, and `managedSkipped=0` describe the candidate-controlled managed commands executed before the separate final evaluator: the 305 passing total plus the 12-test security/App Control command. That command was checked but did not pass as a whole, so `appControlPassed=false`.

## Package, App Control, native lock, and cleanup

- Package membership/capability/closure and App Control security checks executed 12 tests: 11 passed and the hostile unsigned fixture was blocked by App Control `0x800711C7`. The command is failed/external, not passed.
- No candidate manifest, asset manifest, H1/M1/Q1 native manifest input, verified OpenVINO/GGUF stage input, or predecessor native receipt store was available.
- The controlling sequence prohibits acquiring the shared native lock before every exact stage exists. Lock acquisitions: 0. Lock final state: absent.
- Package construction/activation, packaged discovery, native GGUF/OpenVINO, real-model Chat/export, screenshots, keyboard/UIA, text scaling, High Contrast, reduced motion, visual comparison, accessibility, and performance were not executed.
- Candidate process count after execution: 0. No model data, secret, environment dump, or private path is committed. Machine security and App Control were not weakened.

## Independent review

The required durable independent review is created after all substantive corrections and is bound by exact path, SHA-256, and byte count in the final evidence manifest.

## C0 intake

R3-020 is satisfied. R3-022 is not closed: its package/native/E2E evidence was not executed. C0 must not promote the external block to a pass or release approval. Another complete acceptance execution is required after the exact external prerequisites become available.
