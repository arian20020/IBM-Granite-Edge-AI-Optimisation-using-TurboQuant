# E1 R4 v2 Final C0 Correction Round 3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce an append-only, fail-closed E1 return that honestly accepts the exact structured `0x800711C7` pre-discovery block for R3-020 while leaving R3-022 open and protecting the authoritative results directory before VSTest.

**Architecture:** Keep approval and blocked paths distinct in `R3IssueEvidenceVerifier`: parse nonnegative/reconciled gate arithmetic first, validate kind-specific structured evidence second, and permit zero execution only for the exact blocked App Control shape. Reuse the existing safe-directory-chain primitive to validate/create the fixed repository-contained results root before any VSTest invocation. Tests use real JSON/file fixtures and PowerShell directory behavior; the final return remains six artifact files after the implementation subject.

**Tech Stack:** C# 12 / MSTest, `System.Text.Json`, PowerShell 5.1, Git, repository R4 Draft 2020-12 validator.

**Spec:** local C0 correction prompt (private attachment path intentionally omitted)

**Observed-outcome note:** The original plan anticipated that the successor might be
blocked by App Control. The committed successor instead executed its focused harness,
and the final observed block was the absent exact `candidateManifest`. The sealed
return therefore uses the `missingPrerequisite` contract and does not promote the
historical App Control event to successor evidence.

## Global Constraints

- Continue append-only on `test/ucl-e1-native-acceptance-r4-v2` from `fc4c3b96ef71145b36f450cd3c587d42e0e2bba8`.
- Keep tested base `b5d2cd34c57368efb9b122cddf16c2ffa2d3895e` / tree `a3e4d82095caa9688f30d2463fa5971c788fd33f` immutable.
- Modify only the seven permitted E1 implementation paths before the new subject, and only the six named audit artifacts afterward.
- Do not change product files, `main`, Windows Application Control, native inputs, or historical evidence.
- Blocked/unexecuted work remains zero and never counts as passing.

---

### Task 1: Exact zero-execution App Control contract

**Files:**
- Modify: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Tests/R4TwoPhaseIssueEvidenceVerifierTests.cs`
- Modify: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Infrastructure/R3IssueEvidenceVerifier.cs`

**Interfaces:**
- Consumes: schema-v2 post receipt, schema-v2 evidence manifest, bound observation and review.
- Produces: `VerifyPostAcceptance(...)` returning `ClosesR3_020=true`, `ClosesR3_022=false` only for exact structured pre-discovery App Control evidence.

- [ ] Add a fixture mode whose receipt/manifest/observation exactly models four zero gates plus `E1-FOCUSED-HARNESS` with exit 1 and `0/0/0/0/0`, including attempted flag and assembly hash/bytes.
- [ ] Add positive and negative tests for every Round 3 item 1–21, including null/arbitrary blocks, cross-shape misuse, identity/digest/size mismatch, file custody failures, reconciliation failures, and approval non-regression.
- [ ] Run the focused verifier selection and confirm RED because zero managed execution is rejected and the expanded observation shape is unsupported.
- [ ] Refactor counter parsing to accept nonnegative arithmetic, reconcile all four gate rows, then allow all-zero counters only after exact blocked external evidence validates.
- [ ] Split missing-prerequisite and App-Control command predicates; require exact command ID, zero counters, blocked disposition, nonzero exit, exact observation schema, identities, `0x800711C7`, and assembly binding for App Control.
- [ ] Run the focused verifier selection and confirm GREEN, or record Application Control `0x800711C7` with `0/0` if discovery is blocked.

### Task 2: Authoritative result-root prevalidation

**Files:**
- Modify: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Tests/E1EvidenceSupportTests.cs`
- Modify only if needed: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Tests/E1EndToEndRunnerInvocationTests.cs`
- Modify: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/scripts/Invoke-E1EndToEnd.ps1`
- Modify only if needed: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/scripts/E1EvidenceSupport.psm1`

**Interfaces:**
- Consumes: `New-E1SafeDirectoryChain -Path <results> -RepositoryRoot <root>`.
- Produces: a validated regular `TestResults/Audit-20260830/E1-Evidence` chain before constructing or invoking either VSTest command.

- [ ] Add behavior tests proving a regular result root is accepted and outside/reparse roots at every named segment are rejected without creating redirected descendants.
- [ ] Add an invocation test that places a junction in the production results chain and proves VSTest is not launched.
- [ ] Run the focused support/runner tests and confirm RED against the current `New-Item -Force` runner behavior.
- [ ] Replace result-root `New-Item` with the safe chain creator before VSTest discovery/invocation; do not add cleanup for an unvalidated root.
- [ ] Run focused support/runner tests and confirm GREEN, or record `0x800711C7` and `0/0` if the test assembly cannot load.

### Task 3: Implementation subject and code review

**Files:**
- Create: `docs/superpowers/plans/2026-09-01-e1-r4-v2-final-c0-correction-round3.md`
- Commit only the permitted implementation files from Tasks 1–2.

**Interfaces:**
- Produces: immutable implementation commit/tree used by the rebuilt assembly and all final evidence.

- [ ] Run `git diff --check`, enumerate changed paths, and reject anything outside the seven-file implementation allowlist.
- [ ] Commit the implementation append-only and record commit/tree.
- [ ] Freshly rebuild the E1 project with `SourceRevisionId=<implementation commit>`; report exit, warnings, and errors.
- [ ] Attempt the exact focused command once and one unchanged-binary retry only if App Control blocks discovery.
- [ ] Dispatch an independent read-only reviewer against `fc4c3b96..implementation` covering every Round 3 code-review topic; correct all Critical/Important findings with a new subject and repeat review.

### Task 4: Six-file blocked return

**Files:**
- Modify: `docs/audits/2026-08-30/E1-r4-independent-acceptance-v2.md`
- Modify: `docs/audits/2026-08-30/evidence/E1-r4-independent-acceptance-v2.json`
- Modify: `docs/audits/2026-08-30/evidence/E1-r4-post-acceptance-v2.json`
- Modify: `docs/audits/2026-08-30/handoffs/R4-E1.json`
- Create: `docs/audits/2026-08-30/evidence/E1-r4-independent-final-review-v5.json`
- Create: `docs/audits/2026-08-30/evidence/E1-r4-external-block-observation-v3.json`

**Interfaces:**
- Consumes: exact implementation commit/tree, exact blocked assembly digest/bytes, actual command outcomes, and independent code-review result.
- Produces: self-consistent blocked manifest/post/receipt with R3-020 true via App Control evidence and R3-022 false.

- [ ] Hash the exact freshly rebuilt blocked assembly and record its positive byte count and canonical observation timestamp.
- [ ] Regenerate the observation with exactly the verifier-approved properties and bind `E1-FOCUSED-HARNESS` `0/0`, exit 1, blocked.
- [ ] Regenerate report, manifest, post receipt, handoff receipt, and review v5; remove all v2/v4 output references and preserve historical/nonclaim separation.
- [ ] Cascade exact final report/review/observation hashes into the manifest, then the manifest hash into post/receipt.

### Task 5: Final validation, adversarial audit, review, and publication

**Files:**
- Commit only the six artifact paths from Task 4.

**Interfaces:**
- Produces: validator-clean, independently reviewed, remotely equal append-only return.

- [ ] Run evidence and receipt validators against exact final bytes; require exit 0 and zero arithmetic violations.
- [ ] Verify all file/Git bindings, implementation ancestry, exact six-file post-subject diff, native lock/process/cleanup/privacy state, and all twelve adversarial self-audit answers.
- [ ] Dispatch an independent read-only final reviewer over the six artifacts and implementation-to-return diff; require exact `PASS_NO_REMAINING_CRITICAL_OR_IMPORTANT` and correct all Critical/Important findings.
- [ ] Recompute every final SHA-256 and byte count, commit only the six artifacts, push the existing branch, fetch, and prove local/tracking/advertised equality plus a clean worktree.
