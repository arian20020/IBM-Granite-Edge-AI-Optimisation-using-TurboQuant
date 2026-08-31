# E1 R4 v2 Authoritative Evidence Runner Round 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Evidence mode execute only the reviewed implementation with trusted Visual Studio VSTest, fresh exact-identity TRX proof, and fail-closed bound-artifact reads.

**Architecture:** A focused PowerShell support module will own Visual Studio/VSTest discovery, implementation-boundary enforcement, build-output validation, exact VSTest argument construction, and strict TRX parsing. The runner will remove caller-controlled VSTest/assembly inputs and use those functions on a fresh E2E build. C# invocation tests will exercise the real module and trusted VSTest integration, while verifier tests cover file custody failures.

**Tech Stack:** Windows PowerShell 5.1, Git, .NET 8/MSTest, Visual Studio VSTest/TRX, C#.

**Spec:** User-provided “E1 R4 v2 — C0 INTAKE CHANGES REQUIRED, ROUND 2”.

## Global Constraints

- Continue append-only on `test/ucl-e1-native-acceptance-r4-v2`.
- Tested base remains `b5d2cd34c57368efb9b122cddf16c2ffa2d3895e` / `a3e4d82095caa9688f30d2463fa5971c788fd33f`.
- Do not modify product files, the tested base, or `main`.
- Do not weaken Windows Application Control or count blocked/unexecuted gates as passes.
- Authoritative Evidence mode executes exactly the schema-v4 preflight and E1 post-acceptance tests once each.

---

### Task 1: Trusted execution and TRX contracts

**Files:**
- Create: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/scripts/E1EvidenceSupport.psm1`
- Modify: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Tests/E1EndToEndRunnerInvocationTests.cs`

**Interfaces:**
- Produces PowerShell functions `Get-E1TrustedVSTest`, `Assert-E1ImplementationBoundary`, `Assert-E1EvidenceAssembly`, `New-E1AuthoritativeVSTestArguments`, and `Assert-E1AuthoritativeTrx`.
- TRX validation consumes a path, expected fully qualified class, expected method, and invocation start time; it requires one executed/passed test, zero failure/skip, successful run outcome, a fresh regular file, and exact definition/result identity.

- [ ] Add failing tests for fake VSTest, fake assembly, stale/missing/wrong/failed/skipped TRX, reparse inputs, and a code-bearing commit after the claimed subject.
- [ ] Run the focused invocation tests and confirm failures identify missing trusted-path, subject-boundary, and TRX contracts.
- [ ] Implement the minimal support functions with strict path containment, `.exe`/`.dll` type checks, regular/non-reparse custody, size/time bounds, exact Git allowlist, and namespace-aware TRX parsing.
- [ ] Run the focused tests and confirm the negative matrix passes.

### Task 2: Remove Evidence-mode injection and use exact authoritative identities

**Files:**
- Modify: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/scripts/Invoke-E1EndToEnd.ps1`
- Modify: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Tests/E1EndToEndRunnerInvocationTests.cs`

**Interfaces:**
- Evidence mode has no `VSTestPath` or `EvidenceTestAssembly` input.
- It validates `ImplementationCommit..HEAD` and worktree changes against the six explicit E1 audit paths, builds the E2E project with `SourceRevisionId=ImplementationCommit`, discovers trusted VSTest through `vswhere`, and validates the fresh expected assembly.
- It runs the exact fully qualified schema-v4 preflight and post-acceptance identities to unique TRX paths and validates both TRX files.

- [ ] Add a failing source-boundary test proving fake caller paths are unavailable and authoritative filters are exact identities.
- [ ] Remove caller-supplied execution inputs and replace category filters with the two literal fully qualified identities.
- [ ] Build a fresh subject-bound assembly and require no protected code/config/test differences after the implementation subject.
- [ ] Add a real integration test that invokes trusted VSTest for both exact identities and validates fresh TRX evidence through the production support module.
- [ ] Run the focused invocation suite and confirm the real integration and argument-construction tests pass without a command recorder.

### Task 3: Harden bound evidence file custody

**Files:**
- Modify: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Infrastructure/R3IssueEvidenceVerifier.cs`
- Modify: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Tests/R4TwoPhaseIssueEvidenceVerifierTests.cs`

**Interfaces:**
- `VerifyBoundFile` rejects absent, empty, over-1-MiB, inaccessible, file/ancestor-reparse, and non-regular evidence before bounded streaming hash calculation.

- [ ] Add failing post-evidence tests for empty, oversized, locked/inaccessible, and reparse-bound report/review files.
- [ ] Run the verifier focus and confirm each new case fails because current reads are permissive or leak raw I/O exceptions.
- [ ] Implement closed `FileInfo`/attribute/ancestor checks and bounded streaming SHA-256 with I/O exceptions normalized to `InvalidDataException`.
- [ ] Run the verifier focus and confirm all custody cases pass.

### Task 4: Create and review the implementation subject

**Files:**
- Modify only the plan and E1 test-infrastructure files listed above.

- [ ] Run `git diff --check`, fresh E2E build, focused tests, and deterministic tests.
- [ ] Confirm no product/configuration files changed and commit the implementation subject append-only.
- [ ] Obtain independent Critical/Important review; correct findings test-first before accepting the final subject.

### Task 5: Full exact-candidate rerun and evidence return

**Files:**
- Modify: `docs/audits/2026-08-30/E1-r4-independent-acceptance-v2.md`
- Modify: `docs/audits/2026-08-30/evidence/E1-r4-independent-acceptance-v2.json`
- Modify: `docs/audits/2026-08-30/evidence/E1-r4-post-acceptance-v2.json`
- Modify: `docs/audits/2026-08-30/handoffs/R4-E1.json`
- Modify: `docs/audits/2026-08-30/evidence/E1-r4-external-block-observation-v2.json`
- Create: `docs/audits/2026-08-30/evidence/E1-r4-independent-final-review-v4.json`

- [ ] Rerun discovery, focused/deterministic, schema-v4/legacy disclosure, every managed suite, App Control, supplementary, prerequisite/lock/cleanup checks, and the production Evidence stage.
- [ ] Record exact real authoritative TRX identities and 1/1/1/0/0 arithmetic for each successful authoritative command; otherwise record the actual block without promotion.
- [ ] Regenerate all six artifacts with exact subject/hash/byte bindings and `R3-020=true`, `R3-022=false` unless actual native/package/E2E evidence changes that result.
- [ ] Run the post evaluator and both repository validators against final bytes; require zero arithmetic violations.
- [ ] Obtain final independent artifact review with zero Critical/Important findings.
- [ ] Commit and push append-only, then verify clean local/tracking/advertised equality and final lock/process cleanup.
