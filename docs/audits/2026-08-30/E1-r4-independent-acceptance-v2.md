# E1 R4 independent exact-candidate acceptance — authoritative-runner v2 rerun

## Disposition

The exact candidate remains blocked by external prerequisites. `R3-020=true` is supported by the schema-v2 candidate-bound receipt and the structured missing-candidate-manifest observation. `R3-022=false` remains blocked and unresolved because package activation, native execution, and candidate E2E execution did not occur.

No native, package activation, UI, visual, accessibility, performance, or release approval is claimed.

## Exact identity and scope

- Issued base ref: `refs/remotes/origin/integration/ucl-r4-e1-issued-base-v2`
- Tested base commit/tree: `b5d2cd34c57368efb9b122cddf16c2ffa2d3895e` / `a3e4d82095caa9688f30d2463fa5971c788fd33f`
- Product subject commit/tree: `429298d3328a62d4fcf2f5f3f12821342be4a23c` / `2e92e172679d1558972846694306d28ad56e2cf4`
- E1 implementation subject commit/tree: `d36bd6807cb95a26098fdbea98b287f56a37007e` / `859708fe513059d34f1ab2cb00f080518b698955`
- Return branch: `test/ucl-e1-native-acceptance-r4-v2`

Relative to rejected intake return `a18321a8f0121c786d1b1290a7ed9e29b7e73226`, implementation changes are exactly seven E1-owned plan/test-infrastructure files: the Round 2 plan, `R3IssueEvidenceVerifier.cs`, `E1EndToEndRunnerInvocationTests.cs`, `E1EvidenceSupportTests.cs`, `R4TwoPhaseIssueEvidenceVerifierTests.cs`, `E1EvidenceSupport.psm1`, and `Invoke-E1EndToEnd.ps1`. Product files, the immutable base, the product subject, and `main` are unchanged.

## Authoritative runner correction

Evidence mode now discovers VSTest through canonical `vswhere`, requires canonical Visual Studio and dotnet roots, rejects caller executable/assembly substitution, creates a fresh implementation-versioned assembly under the expected repository output, and parses a fresh TRX for each exact fully qualified evaluator. Each TRX must contain exactly one executed passing test, no failure or skip, the exact class/method/test ID, and a successful outcome. Repository-root ancestry checks reject a reparse file, results root, or parent above the results root.

The implementation boundary permits only the six named audit return artifacts after the implementation subject. Test, source, script, project, configuration, and other code-bearing changes after the subject fail closed. Bound report, review, observation, and manifest inputs reject missing, empty, oversized, inaccessible, or reparse-point files and ancestors before reading.

The intended focused command is unambiguous: 27 tests, comprising nine evidence-support tests, five non-authoritative runner invocation tests, and thirteen schema-v4 verifier tests. On the final implementation subject, Windows Application Control blocked the freshly rebuilt E1 test assembly itself before discovery (`0x800711C7`), including on a separate unchanged-binary retry. Consequently none of those 27 tests, the deterministic selection, the exact evaluators, or the production Evidence-mode integration is claimed executed or passed for the final subject.

## Final-subject command arithmetic

Repository SDK `10.0.301` was unavailable. Canonical SDK `10.0.400` was selected through its validated Program Files `MSBuild.dll` without changing `global.json`. The E1 assembly discovery found 79 tests; discovery is narrative evidence, not a command row. A full solution `--no-restore` build attempt failed because 32 unrelated project asset files were absent; all required runnable projects were then restored/built independently. Windows Application Control was not changed or bypassed.

| Command/gate | Discovered | Executed | Passed | Failed | Skipped | Exit/result |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| E1 assembly rebuild | 0 | 0 | 0 | 0 | 0 | 0; succeeded, non-test operation |
| Focused authoritative-harness attempt | 0 | 0 | 0 | 0 | 0 | 1; E1 assembly blocked before discovery |
| Focused unchanged-binary retry | 0 | 0 | 0 | 0 | 0 | 1; E1 assembly blocked before discovery |
| E1 deterministic selection | 0 | 0 | 0 | 0 | 0 | blocked, not executed |
| Schema-v4 candidate preflight | 0 | 0 | 0 | 0 | 0 | blocked, not executed |
| Combined legacy/v4 preflight | 0 | 0 | 0 | 0 | 0 | blocked, not executed |
| OpenVINO optimization | 0 | 0 | 0 | 0 | 0 | blocked, not executed |
| OpenVINO worker client | 0 | 0 | 0 | 0 | 0 | blocked, not executed |
| GGUF quantization contracts | 0 | 0 | 0 | 0 | 0 | blocked, not executed |
| GGUF quantization worker | 0 | 0 | 0 | 0 | 0 | blocked, not executed |
| Q1 identity/lifecycle | 0 | 0 | 0 | 0 | 0 | blocked, not executed |
| Cross-feature integration | 0 | 0 | 0 | 0 | 0 | blocked, not executed |
| Security/App Control | 0 | 0 | 0 | 0 | 0 | blocked, not executed |
| OpenVINO broad supplementary | 0 | 0 | 0 | 0 | 0 | blocked, not executed |
| External prerequisite preflight | 1 | 1 | 0 | 1 | 0 | 1; candidate manifest absent |
| Package activation | 0 | 0 | 0 | 0 | 0 | blocked, not passed |
| Native execution | 0 | 0 | 0 | 0 | 0 | blocked, not passed |
| Candidate E2E execution | 0 | 0 | 0 | 0 | 0 | blocked, not passed |
| Final candidate-bound evaluator | 0 | 0 | 0 | 0 | 0 | blocked, not executed |
| Production Evidence-mode integration | 0 | 0 | 0 | 0 | 0 | blocked, not executed |

The final-subject canonical passing total is 0/0. Results executed before the new implementation subject are historical diagnostics and are not relabelled as final-subject evidence. Blocked and unexecuted gates are not passes. Every persisted test-command row satisfies `discovered == executed` and `executed == passed + failed + skipped`; the successful rebuild is expressly a non-test operation and is retained only in this narrative.

## Native lock, package, cleanup, and privacy

- Candidate, asset, H1/M1/Q1 native manifests and predecessor receipt root were absent at the final prerequisite check.
- The approved shared lock `C:\UCL-AUDIT-NATIVE.lock` was never acquired. Acquisitions: 0. Final lock state: absent.
- Package activation, native, candidate E2E, UI, visual, accessibility, and performance stages remained ineligible and unexecuted.
- Windows Application Control blocked the unsigned final-subject E1 test assembly itself with `0x800711C7`; machine security was not weakened. This stopped the final-subject campaign before test discovery.
- Candidate process count after execution: 0. Cleanup verification passed.
- No model data, secrets, environment dump, or private rooted evidence path is committed.

## Two-phase issue result and review

Committed schema-v4 evidence covers all 22 R3 identifiers and all eight R4-C0 findings, but the final-subject evaluator could not execute after Application Control blocked its assembly. The bound return remains fail-closed: `R3-020=true` from the exact-candidate receipt plus structured missing-prerequisite evidence; `R3-022=false` because package/native/E2E evidence was not executed.

The durable independent review is `docs/audits/2026-08-30/evidence/E1-r4-independent-final-review-v4.json`. It reviews the exact implementation subject and records zero Critical and zero Important findings.
