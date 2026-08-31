# C0 R4 E1 replacement-candidate rerun contract

## Authority and scope

This candidate-controlled contract is additional controlling context for the E1 R4 master prompt. It corrects only the circular and internally inconsistent issue-closure entry gate exposed by E1 return `dba63fcfcacc7d8ac541e863ff609fa785ee4802`. Every other ownership, security, candidate-identity, native-lock, evidence, cleanup, non-claim, and final-disposition rule in the master prompt remains in force.

E1 continues to own test infrastructure, independent execution, evidence, and the release verdict. C0 has made no product correction. The product subject remains `429298d3328a62d4fcf2f5f3f12821342be4a23c` with tree `2e92e172679d1558972846694306d28ad56e2cf4`.

## Corrected two-phase issue contract

The candidate contains `docs/audits/2026-08-30/evidence/C0-r4-issue-closure.json` using `C0-R4-ISSUE-CLOSURE-V4` and its committed evidence catalog `C0-r4-issue-evidence-index-v2.json`.

E1 preflight must require:

1. `R3-001` through `R3-022` occur exactly once.
2. `R3-001` through `R3-019` and `R3-021` are `preflight`/`CLOSED`, have `productDefectRemains: false`, and resolve at least one positive executable command through the evidence catalog.
3. `R3-020` and `R3-022` are the only `postAcceptance`/`PENDING_E1_ACCEPTANCE` rows. They must not carry fabricated passing commands and must not block deterministic, managed, package, App Control, native, visual, accessibility, performance, or cleanup attempts.
4. `R4-C0-001` through `R4-C0-008` occur exactly once, are `preflight`/`CLOSED`, have `productDefectRemains: false`, and resolve positive executable commands.
5. The evidence catalog blob exists at its declared subject commit/tree/path with exact SHA-256 and byte count. Every referenced command has non-zero discovery, execution, and passes; zero failures and skips; and valid arithmetic. Every declared repository path exists without absolute, parent-traversal, or backslash syntax. Production-reachability rows retain exactly one observed registration.
6. The closure blob itself exists unchanged in the exact fetched replacement candidate and that candidate equals the pushed immutable issued-base ref.

After preflight, E1 must execute the full assignment from the beginning. At final evaluation:

- `R3-020` is satisfied only by an exact-candidate, schema-valid E1 receipt and an honest native result or precisely evidenced external native block.
- `R3-022` is satisfied only by exact-candidate package/App Control/native/cleanup/E2E evidence actually executed. Blocked and unrun gates remain excluded from passing totals.
- E1 records the result in its own report, manifest, and receipt. It must not edit C0's closure to manufacture a prior pass.

## Required E1 verifier correction

E1 is explicitly authorized to correct its test-only `R3IssueEvidenceVerifier` and release-veto preflight for this two-phase contract. Use test-first development and retain fail-closed behavior. At minimum, the E1 tests must prove that the verifier:

- accepts the exact schema-v4 shape and only the two permitted pending acceptance IDs;
- rejects a missing, duplicate, unknown, or extra R3/R4 identifier;
- rejects pending state on any other R3/R4 finding;
- rejects a falsely pre-closed `R3-020` or `R3-022` without candidate-bound E1 acceptance evidence;
- rejects non-GREEN, zero-discovery, failed, skipped, missing-command, stale-blob, hash, byte, tree, path, or remote-ref evidence;
- permits the full E1 execution matrix to start after the preflight-resolvable findings pass;
- evaluates `R3-020` and `R3-022` only after the relevant attempts and preserves honest external-block semantics.

E1 may replay or adapt its prior E1-owned infrastructure commits, but it must branch anew from the replacement issued base, resolve no production conflict, edit no production file, and rerun all deterministic, managed, package, native, cleanup, privacy, schema, and independent-review gates. The prior return is historical evidence only and cannot be relabelled as the replacement-candidate result.

## Required return

Push the replacement-candidate rerun to `test/ucl-e1-native-acceptance-r4-v2` unless that ref already exists with unrelated content, in which case stop and return the collision to C0. The return must state one unambiguous master-prompt disposition and provide remote/local-equal tip/tree, exact tested base ref/commit/tree, complete arithmetic, cleanup state, and schema-valid report/manifest/receipt hashes and byte counts.
