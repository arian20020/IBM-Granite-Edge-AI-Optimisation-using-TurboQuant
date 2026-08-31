# C0 R4 final candidate issue ledger

Final product candidate: `429298d3328a62d4fcf2f5f3f12821342be4a23c` / `2e92e172679d1558972846694306d28ad56e2cf4`

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

## Environment and campaign blockers

| ID | State | Owner/next action |
| --- | --- | --- |
| H1-R4 | ACCEPTED | C0 preserved returned evidence and remediation record |
| Q1-R4 | ACCEPTED | C0 integrated the return and closed coordinator-owned lifecycle findings |
| E1-R4 | DISPATCHED | E1 independently validates the exact immutable final candidate |
| NATIVE-AUTH-LOCK | BLOCKED_EXTERNAL | E1 may acquire only the approved shared native lock and exact authorization |
| NATIVE-GGUF-STAGE | BLOCKED_EXTERNAL | exact pinned stage input is absent; no binary/hash may be invented |
| NATIVE-OPENVINO-STAGES | BLOCKED_EXTERNAL | exact verified native-stage inputs remain absent |
| APP-CONTROL | BLOCKED_EXTERNAL | unsigned fresh apphosts and packaged runner are policy-blocked |
| SIGNED-PACKAGE | PENDING_E1 | E1 records only evidence actually obtainable on its candidate-bound host |
| VISUAL-PERFORMANCE | PENDING_E1 | E1 claims these gates only if the exact candidate can execute |
