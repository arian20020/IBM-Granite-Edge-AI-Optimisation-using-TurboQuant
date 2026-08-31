# E1 R4 v2 Harness Correction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the E1 runner and post-acceptance verifier fail closed on immutable identities and structured candidate-bound evidence, then rerun and return exact blocked evidence.

**Architecture:** The PowerShell runner establishes every Git/path/input relationship before any build or native mutation and exposes an evidence-only stage that invokes authoritative preflight and post-acceptance tests. The C# verifier parses a structured external block, discrete gate evidence, the bound evidence manifest, and a structured independent-review artifact before deciding R3-020/R3-022.

**Tech Stack:** PowerShell 5.1, .NET 8, MSTest/VSTest, System.Text.Json, Git, repository R4 validators.

**Spec:** `docs/audits/2026-08-30/C0-r4-E1-rerun-contract.md` plus the C0 intake dated 2026-08-31.

## Global Constraints

- Append commits only on `test/ucl-e1-native-acceptance-r4-v2`.
- Base commit/tree stay `b5d2cd34c57368efb9b122cddf16c2ffa2d3895e` / `a3e4d82095caa9688f30d2463fa5971c788fd33f`.
- Modify no product file or `main`; do not weaken App Control.
- Blocked/unexecuted native, package, UI, visual, accessibility, and performance gates are never passes.

---

### Task 1: Fail-closed runner boundary

**Files:**
- Modify: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/scripts/Invoke-E1EndToEnd.ps1`
- Create: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Tests/E1EndToEndRunnerInvocationTests.cs`

**Interfaces:**
- Consumes: exact candidate remote/ref/commit/tree, closure path/relative path, implementation commit/tree.
- Produces: validated `GRANITE_E2E_*` environment and authoritative evidence-stage execution.

- [ ] Write process-level tests proving missing inputs and wrong identities exit before a build/lock sentinel.
- [ ] Run the focused tests and observe contract failures against the current script.
- [ ] Add strict mode, mandatory parameters, Git/path validation, immutable candidate binding, and complete environment propagation.
- [ ] Add an evidence stage that invokes both authoritative categories after validation.
- [ ] Run focused tests green.

### Task 2: Structured post-acceptance semantics

**Files:**
- Modify: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Infrastructure/R3IssueEvidenceVerifier.cs`
- Modify: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Tests/R4TwoPhaseIssueEvidenceVerifierTests.cs`

**Interfaces:**
- Consumes: structured external-block and gate evidence plus bound manifest/review JSON.
- Produces: fail-closed `R4PostAcceptanceEvidence` with independent R3-020/R3-022 results.

- [ ] Add negative tests for arbitrary/malformed/disposition-mismatched external blocks.
- [ ] Add approval tests for aggregate-only, missing E2E, fabricated gates, manifest mismatch, and review mismatch.
- [ ] Run the focused tests and observe the expected failures.
- [ ] Parse and validate structured blocks, discrete gates, manifest semantics, and independent-review semantics.
- [ ] Run one unambiguous focused suite green and record its exact count.

### Task 3: New subject and complete rerun

**Files:** test harness files and this plan only.

- [ ] Verify changed-file scope contains no product file.
- [ ] Commit the runner/verifier/tests as the new implementation subject.
- [ ] Run build, discovery, deterministic/preflight, managed suites, security/App Control, and exact external-prerequisite checks from the beginning.
- [ ] Run no native/package/UI gate unless every prerequisite validates before lock acquisition.

### Task 4: Final evidence and transport

**Files:**
- Regenerate: `docs/audits/2026-08-30/E1-r4-independent-acceptance-v2.md`
- Regenerate: `docs/audits/2026-08-30/evidence/E1-r4-independent-acceptance-v2.json`
- Regenerate: `docs/audits/2026-08-30/evidence/E1-r4-post-acceptance-v2.json`
- Regenerate: `docs/audits/2026-08-30/handoffs/R4-E1.json`
- Regenerate: durable independent-review JSON under `docs/audits/2026-08-30/evidence/`.

- [ ] Record one focused-suite count and complete command arithmetic.
- [ ] Bind final report, manifest, structured post evidence, and review by exact SHA-256/bytes.
- [ ] Obtain independent Critical/Important review and resolve findings.
- [ ] Run both R4 validators and the exact final evaluator.
- [ ] Commit/push append-only and verify local/tracking/advertised equality with a clean worktree.
