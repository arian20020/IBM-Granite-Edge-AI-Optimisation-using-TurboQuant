# E1 R4 independent exact-candidate acceptance — harness-corrected v2 rerun

## Disposition

The exact candidate remains blocked by external prerequisites. R3-020 is satisfied only by the schema-v2 post-acceptance receipt and its structured, candidate-bound missing-prerequisite observation. R3-022 remains blocked and unresolved because package activation, native execution, and candidate E2E execution did not occur.

No native, package activation, UI, visual, accessibility, performance, or release approval is claimed.

## Exact identity and scope

- Issued base ref: `refs/remotes/origin/integration/ucl-r4-e1-issued-base-v2`
- Tested base commit/tree: `b5d2cd34c57368efb9b122cddf16c2ffa2d3895e` / `a3e4d82095caa9688f30d2463fa5971c788fd33f`
- Product subject commit/tree: `429298d3328a62d4fcf2f5f3f12821342be4a23c` / `2e92e172679d1558972846694306d28ad56e2cf4`
- E1 implementation subject commit/tree: `7cf892d17f81b87b64b4d634b2e018b9005b7656` / `777b9af69c14fc60a14adbe7968f9d5ebd980921`
- Return branch: `test/ucl-e1-native-acceptance-r4-v2`

Relative to intake return `b6d7ccb5bd618fea148b25e2482217b2f787eba7`, implementation changes are confined to the E1 plan, runner, verifier, and their two test files: `docs/superpowers/plans/2026-08-31-e1-r4-v2-harness-correction.md`, `Invoke-E1EndToEnd.ps1`, `R3IssueEvidenceVerifier.cs`, `E1EndToEndRunnerInvocationTests.cs`, and `R4TwoPhaseIssueEvidenceVerifierTests.cs`. Product files, the immutable base, the product subject, and `main` are unchanged.

## Harness corrections

The runner uses strict mode; declares and validates every candidate, closure, implementation, asset, and producer input; clears stale optional evidence variables; exports all authoritative environment variables; and binds the candidate to the immutable issued origin ref rather than return-branch `HEAD`. Candidate, executable, asset, H1/M1/Q1, Git, path, tree, blob, ancestry, and advertised-ref relationships are checked before build or lock. The executable-path check rejects drive-root-relative and drive-relative forms before mutable execution while retaining fully qualified drive and UNC paths.

Five invocation tests cover missing arguments, wrong ref/subject identity, absent native prerequisites, malformed candidate/asset/producer evidence including a drive-root-relative executable, and exact preflight/post invocation. Twelve verifier tests cover the schema-v4 two-phase contract. The single final focused command therefore passed 17/17; it supersedes historical 9/9, 10/10, 14/14, and 16/16 snapshots and is a subset of deterministic, not an additional canonical total.

External blocking evidence is structured, hash/byte bound, restricted to enumerated missing prerequisites or observed App Control `0x800711C7`, and valid only with `BLOCKED BY EXTERNAL ENVIRONMENT`. R3-022 requires distinct manifest-bound package, App Control, native, cleanup, and E2E evidence, exact arithmetic, semantic manifest validation, and a subject-matched durable independent review.

## Final-subject command arithmetic

The repository-pinned SDK `10.0.301` was unavailable. Installed SDK `10.0.400` was selected transiently for build and `global.json` was restored before commit. VSTest 17.14 x64 supplied discovery and execution. Assembly discovery found 68 tests; discovery is narrative evidence and is not a test-command record.

| Command/gate | Discovered | Executed | Passed | Failed | Skipped | Exit/result |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Focused corrected harness | 17 | 17 | 17 | 0 | 0 | 0; passed, deterministic subset |
| E1 deterministic selection | 48 | 48 | 48 | 0 | 0 | 0; passed |
| Schema-v4 candidate preflight | 1 | 1 | 1 | 0 | 0 | 0; passed |
| Combined legacy/v4 preflight | 2 | 2 | 1 | 1 | 0 | 1; legacy schema-v3 route rejected schema v4 |
| OpenVINO optimization | 55 | 55 | 55 | 0 | 0 | 0; passed |
| OpenVINO worker client | 17 | 17 | 17 | 0 | 0 | 0; passed |
| GGUF quantization contracts | 7 | 7 | 7 | 0 | 0 | 0; passed |
| GGUF quantization worker | 19 | 19 | 19 | 0 | 0 | 0; passed |
| Q1 identity/lifecycle | 51 | 51 | 51 | 0 | 0 | 0; passed |
| Cross-feature integration | 115 | 115 | 115 | 0 | 0 | 0; passed |
| Security/App Control | 12 | 12 | 11 | 1 | 0 | 1; `0x800711C7`, blocked |
| OpenVINO broad supplementary | 496 | 496 | 489 | 0 | 7 | 0; mixed/excluded |
| External prerequisite preflight | 1 | 1 | 0 | 1 | 0 | 1; candidate manifest absent |
| Package activation | 0 | 0 | 0 | 0 | 0 | blocked, not passed |
| Native execution | 0 | 0 | 0 | 0 | 0 | blocked, not passed |
| Candidate E2E execution | 0 | 0 | 0 | 0 | 0 | blocked, not passed |
| Final candidate-bound evaluator | 1 | 1 | 1 | 0 | 0 | 0; R3-020 true, R3-022 false |

The canonical non-overlapping passing total is 314/314: `48 + 1 + 55 + 17 + 7 + 19 + 51 + 115 + 1`. Focused tests are contained in deterministic. The combined legacy failure, security failure, broad supplementary command, seven skips, blocked/unexecuted gates, and external-prerequisite failure are excluded from the passing total. Every persisted command row satisfies `discovered == executed` and `executed == passed + failed + skipped`.

## Native lock, package, cleanup, and privacy

- Candidate, asset, H1/M1/Q1 native manifests and predecessor receipt root were absent at the final prerequisite check.
- The approved shared lock was therefore never acquired. Acquisitions: 0. Final lock state: absent.
- Package activation, native, candidate E2E, UI, visual, accessibility, and performance stages remained ineligible and unexecuted.
- Security/App Control executed 12 tests: 11 passed and the unsigned hostile fixture was policy-blocked with `0x800711C7`. Machine security was not weakened.
- Candidate process count after execution: 0. Cleanup verification passed.
- No model data, secrets, environment dump, or private rooted path is committed.

## Two-phase issue result and review

Schema-v4 preflight validated all 22 R3 identifiers and all eight R4-C0 findings. R3-001 through R3-019 and R3-021 are closed preflight; R3-020 and R3-022 were evaluated only after attempted execution. Final result: `R3-020=true`; `R3-022=false`. Blocked and unexecuted gates are not passes.

The durable structured independent review is `docs/audits/2026-08-30/evidence/E1-r4-independent-final-review-v3.json`. It is bound by exact hash and bytes in the evidence manifest and records zero Critical and zero Important findings for the final implementation subject.
