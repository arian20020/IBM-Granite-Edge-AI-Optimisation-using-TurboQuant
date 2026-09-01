# C0 R4 replacement E1 candidate issue ledger

Product subject: `429298d3328a62d4fcf2f5f3f12821342be4a23c` / `2e92e172679d1558972846694306d28ad56e2cf4`

Replacement evidence candidate: `b5d2cd34c57368efb9b122cddf16c2ffa2d3895e` / `a3e4d82095caa9688f30d2463fa5971c788fd33f`

## Deterministic product obligations

| ID | Obligation | Final disposition | Evidence |
| --- | --- | --- | --- |
| T1-01A | Proportional reserve row 1 | CLOSED | bounded typed reserve oracle |
| T1-01B | Proportional reserve row 2 | CLOSED | bounded typed reserve oracle |
| T1-01C | Proportional reserve row 3 | CLOSED | near-boundary oracle without negative budget |
| T1-02 | Zero available memory establishes zero budget | CLOSED | typed zero-availability projection |
| T1-03 | Suppress substituted terminal journey identity | CLOSED | exact active journey publication |
| T1-04 | Reject non-exact published GGUF authority | CLOSED | exact execution identity and hashes |
| T1-05 | Remove staged output/publication for unsealed lease | CLOSED | Q1 custody and recovery contracts |
| T1-06 | Allowlist quantizer environment and disable diagnostics | CLOSED | S1 trusted environment seam |
| T1-07 | Wire recommended-model download action | CLOSED | canonical authority and one-time claim |
| T1-08 | Require exact OpenVINO result and lifecycle token | CLOSED | exact Chat/export destination facade |

## C0 post-Q1 lifecycle findings

| ID | Finding | Final disposition | Evidence |
| --- | --- | --- | --- |
| R4-C0-001 | Duplicate Chat admission could replace a live target | CLOSED | duplicate rejected; behavioral regression in Q1 identity suite |
| R4-C0-002 | Retirement could race new destination admission | CLOSED | admission closes atomically before cancellation/join |
| R4-C0-003 | Synchronous dispose could bypass ordered async retirement | CLOSED | one published shutdown task serves Dispose and ShutdownAsync |
| R4-C0-004 | Optimization `async void` failures could escape | CLOSED | all intent faults bounded and reported |
| R4-C0-005 | Chat could resurrect during shutdown | CLOSED | shared handoff gate closes permanently and joins |
| R4-C0-006 | A global intent gate could suppress Cancel during Confirm | CLOSED | global gate removed; Cancel remains concurrent |
| R4-C0-007 | A throwing cancellation callback could strand shutdown | CLOSED | callback failures cannot prevent shutdown-task publication/completion |
| R4-C0-008 | Back or Done could retire while Chat still used a target | CLOSED | Chat/Back/Done share lease, gate, and exact page/coordinator revalidation |

All rows above are closed on the final product candidate. Independent re-review returned PASS with no remaining Critical or Important finding.

## R3 closure and acceptance phase split

`C0-R4-ISSUE-CLOSURE-V4` records every identifier from `R3-001` through `R3-022` exactly once. `R3-001` through `R3-019` and `R3-021` are closed preflight findings with executable GREEN evidence. E1's final candidate-bound evaluation returns `R3-020=true` from exact structured missing-`candidateManifest` evidence and `R3-022=false` because package, App Control, native, and candidate E2E gates did not execute. The immutable candidate closure remains the preflight input; this post-acceptance result is recorded separately and does not rewrite that candidate.

The closure also records all eight `R4-C0-*` findings exactly once with positive executable evidence. The prior T1 obligation table remains supporting campaign context and is not substituted for the required R3/R4 sets.

## Environment and campaign blockers

| ID | State | Owner/next action |
| --- | --- | --- |
| H1-R4 | ACCEPTED | C0 preserved returned evidence and remediation record |
| Q1-R4 | ACCEPTED | C0 integrated the return and closed coordinator-owned lifecycle findings |
| E1-R4-V1 | CHANGES_REQUIRED | first return `dba63fcf` correctly vetoed the incomplete closure; artifacts preserved verbatim |
| E1-R4-V2 | ACCEPTED_BLOCKED_EXTERNAL | final return `d18c2fb2`; 38/38 canonical managed evidence, R3-020 true, R3-022 false, no release approval |
| NATIVE-AUTH-LOCK | BLOCKED_EXTERNAL | E1 may acquire only the approved shared native lock and exact authorization |
| NATIVE-GGUF-STAGE | BLOCKED_EXTERNAL | exact pinned stage input is absent; no binary/hash may be invented |
| NATIVE-OPENVINO-STAGES | BLOCKED_EXTERNAL | exact verified native-stage inputs remain absent |
| CANDIDATE-MANIFEST | BLOCKED_EXTERNAL | exact candidate manifest was absent before package/native eligibility |
| APP-CONTROL | UNEXECUTED | final subject did not reach this gate; historical policy-block evidence is not relabelled |
| SIGNED-PACKAGE | UNEXECUTED | no package activation or signed-package acceptance is claimed |
| VISUAL-PERFORMANCE | UNEXECUTED | no visual, accessibility, or performance execution is claimed |
