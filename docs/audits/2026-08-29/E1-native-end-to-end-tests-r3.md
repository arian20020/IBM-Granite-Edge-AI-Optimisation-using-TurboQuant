# E1 R3 independent release-veto report

## Decision

`CHANGES REQUIRED`

E1 found no exact pushed C0 R3 integration candidate and no candidate-committed R3 issue ledger. Consequently none of R3-001 through R3-022 has executable GREEN evidence bound to the required candidate. This is a release veto, not `BLOCKED BY EXTERNAL ENVIRONMENT`: known product issues have not been independently cleared and locally required candidate/evidence/package inputs are missing.

## Identity and scope

- Worker: E1
- Direct ownership: R3-020
- Supporting boundary: R3-019, R3-021, R3-022
- Provisional base commit/tree: `d36c529acf754016fdfa23a6d36c7fd913940701` / `31f232bce2b0d63e6a8c17e099d848a812beb2c9`
- Deterministic implementation tip/tree: `168aaef7db91affa87cb7342a4d4546e8ba63de7` / `78c13dffa9ec7926a8800528ae4017b275e0de41`
- Branch: `test/ucl-e1-native-acceptance-r3`
- Exact C0 R3 candidate: absent
- Evidence manifest: `null`

E1 changed only E1 test infrastructure, its R3 spec/plan, and this report. No application, service, model, package, signing, policy, trust, firewall, certificate, or production composition behavior changed.

## Independent issue revalidation

The result column is based on the only admissible subject: an exact pushed C0 candidate. Individual R2 worker branches are not a substitute for integrated candidate evidence.

| Issue | Required executable evidence | Exact-candidate result |
|---|---|---|
| R3-001 | corrected M1 handoff/evidence arithmetic | no candidate-bound GREEN evidence |
| R3-002 | M1 test rejects unresolved package expressions | no candidate-bound GREEN evidence |
| R3-003 | every production `DebugFixtures` root covered | no candidate-bound GREEN evidence |
| R3-004 | schema-v2 projection reached by a live production route | no candidate-bound GREEN evidence |
| R3-005 | approved recommended-model download backend composed | no candidate-bound GREEN evidence |
| R3-006 | retired-page download completion cannot mutate UI | no candidate-bound GREEN evidence |
| R3-007 | navigation observes/cancels export and download operations | no candidate-bound GREEN evidence |
| R3-008 | F1/Q1 service contracts connected in production | no candidate-bound GREEN evidence |
| R3-009 | genuine user-selected OpenVINO export destination accepted | no candidate-bound GREEN evidence |
| R3-010 | no child traversal before custody validation | no candidate-bound GREEN evidence |
| R3-011 | every existing path ancestor checked | no candidate-bound GREEN evidence |
| R3-012 | temporary-output cleanup failure observable | no candidate-bound GREEN evidence |
| R3-013 | large-file identity work cancellation-sensitive | no candidate-bound GREEN evidence |
| R3-014 | live OpenVINO Chat uses exact result, not last directory | no candidate-bound GREEN evidence |
| R3-015 | intermediate quantizer package reparse points rejected | no candidate-bound GREEN evidence |
| R3-016 | GGUF package closure rejects unresolved expression | no candidate-bound GREEN evidence |
| R3-017 | A1 production composition has one real registration | no candidate-bound GREEN evidence |
| R3-018 | download and exact OpenVINO Chat requirements GREEN | no candidate-bound GREEN evidence |
| R3-019 | H1 to M1 to Q1 dependency/native order closed | no handoffs/native receipts or candidate ledger |
| R3-020 | current integrated-candidate E1 receipt/native acceptance | absent; provisional deterministic branch only |
| R3-021 | evidence reproducible solely from committed Git blobs | no candidate closure manifest; new E1 gate rejects working-tree-only evidence |
| R3-022 | package, App Control, native, cleanup, and E2E acceptance | no exact candidate/package/native campaign |

Any one row is sufficient for `CHANGES REQUIRED`; all 22 currently lack admissible candidate evidence.

## E1 R3 executable infrastructure

The new deterministic gates:

- require exactly R3-001 through R3-022 once each;
- require positive executable behavioral/package/native evidence with zero failures and zero skips;
- reject mock-only and build-only evidence as issue closure;
- require definition, production caller, composition point, behavioral regression test, and exactly one observed registration for production issues R3-004 through R3-018;
- verify the closure manifest from the candidate Git blob;
- verify each producer evidence blob at its separate subject commit/tree with exact SHA-256 and bytes;
- verify the exact pushed candidate remote ref;
- reject working-tree-only mutations and stale/unpushed refs;
- encode `CHANGES REQUIRED` precedence over the narrowly defined environment disposition;
- run candidate/evidence preflight before package construction and native lock acquisition.

No new production type or method was added, so the production-reachability gate has no E1-owned production registration. The executable E1 routes are:

| Definition | Executable caller/composition | Behavioral regression |
|---|---|---|
| `R3IssueEvidenceVerifier.Verify` | `R3ReleaseVetoPreflight.Verify`, invoked by the runner preflight | `R3IssueEvidenceVerifierTests`, `R3ReleaseVetoPreflightTests` |
| `GitEvidenceVerifier.VerifyBlob/VerifyPushedRef` | `R3ReleaseVetoPreflight.Verify`, invoked by the runner preflight | real temporary repository/bare-remote tests |
| `R3ReleaseVetoPreflight.Verify` | `Exact_R3_candidate_and_issue_evidence_are_committed_and_pushed`, selected by `Invoke-E1EndToEnd.ps1` | committed-blob and working-tree-mutation tests |
| `R3ReleaseVeto.Evaluate` | deterministic E1 veto contract | precedence table tests; not claimed as a production journey |

## RED and GREEN evidence

RED evidence was observed before implementation:

- `R3IssueEvidenceVerifierTests`: compile failure because the issue verifier/types did not exist.
- `GitEvidenceVerifierTests`: compile failure because the Git verifier did not exist.
- `TestDirectoryTests`: `UnauthorizedAccessException` deleting a nested read-only Git object; the test utility now clears only read-only attributes below its owned temporary root before deletion.
- `R3ReleaseVetoTests`: compile failure because the evaluator/types did not exist.
- `R3ReleaseVetoPreflightTests`: compile failure because the executable preflight did not exist.

Fresh GREEN evidence from the real E1 runner:

| Layer | Discovered | Executed | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|
| Visual Studio VSTest 17.14 x64 List | 81 | 0 | 0 | 0 | 0 |
| Deterministic, one worker | 42 | 42 | 42 | 0 | 0 |
| Packaged List gate | 0 | 0 | 0 | 0 | 0 |
| Smoke | 0 | 0 | 0 | 0 | 0 |
| Failure | 0 | 0 | 0 | 0 | 0 |
| Acceptance | 0 | 0 | 0 | 0 | 0 |
| Restart | 0 | 0 | 0 | 0 | 0 |
| RealModel | 0 | 0 | 0 | 0 | 0 |

The packaged List invocation exited 1 before build, package access, or lock acquisition because exact candidate, pushed ref, and committed closure inputs were absent. Zero packaged discovery is not a passing result.

The ignored deterministic TRX is 58,791 bytes with SHA-256 `2928220a33e5154c6a3d358676f07054634ebbfe8d285b4344c462e47bf31f6c`. Arithmetic is `42 = 42 + 0 + 0`, with discovery 42 for the selected deterministic stage. The full assembly List discovered 81 tests.

## Build, package, native, and cleanup

- E1 diagnostic build using installed SDK 10.0.400: exit 0, zero warnings, zero errors.
- Repository-pinned SDK 10.0.301: unavailable.
- Exact candidate package construction: not run; no exact C0 candidate or closure manifest.
- Authoritative packaged discovery: zero; rejected before package work.
- Native journeys: not run.
- Native lock: never acquired and absent after verification.
- Candidate/app/worker/tool descendants owned by E1 after verification: zero.
- Handoff/native-receipt stores: absent.
- Final E1 handoff/native receipt: not published because their identity and native joins cannot be valid.

PowerShell execution used process-scoped policy bypass for the committed local runner because script execution was disabled; no machine/user policy, App Control, signing, trust, certificate, or firewall configuration was changed.

## Required upstream action

C0 must publish one exact pushed candidate containing accepted producer corrections and a candidate-committed R3 closure manifest. Producers must publish reproducible committed evidence and valid final handoff/native joins. The pinned build/native/package prerequisites must then be available. E1 must rebuild its final branch from that candidate, replay only E1 test infrastructure, re-run all source/managed/integration/package/security/privacy/cleanup checks, obtain non-zero packaged discovery, and run every authorized native campaign before reconsidering the veto.

Main was not merged or pushed. Only the E1 branch is eligible to be pushed from this worktree.
