# E1 R3 Release-Veto Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make E1 independently reject any R3 candidate that lacks exact, executable GREEN evidence for R3-001 through R3-022 and admit release review only after packaged/native identity and cleanup closure.

**Architecture:** Add focused deterministic verifiers for the 22-issue ledger, Git-object-backed evidence, and release disposition. Wire them into the existing E1 preflight before native lock acquisition, preserving the existing packaged UIA journeys and fail-closed process cleanup.

**Tech Stack:** C# 13, .NET 8 Windows, MSTest 4, PowerShell 5.1, Git object plumbing, Visual Studio VSTest x64.

**Spec:** `docs/superpowers/specs/2026-08-29-e1-r3-release-veto-design.md`

## Global Constraints

- E1 changes only `tests/E2ETests/**`, E1 R3 documentation, and the dated E1 R3 audit report.
- Any missing executable GREEN evidence across R3-001 through R3-022 yields `CHANGES REQUIRED`.
- `BLOCKED BY EXTERNAL ENVIRONMENT` requires all locally runnable layers GREEN and no known product defect.
- R2-or-earlier handoff, native receipt, evidence, and report identities are rejected.
- Zero discovery, skips, mocks without behavioral coverage, and build-only results are not passing evidence.
- Native stages require the exact pushed C0 candidate and occur only after evidence preflight.
- Raw machine evidence remains under ignored `TestResults` storage.

---

### Task 1: Exact R3 issue-ledger verifier

**Files:**
- Create: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Infrastructure/R3IssueEvidenceVerifier.cs`
- Create: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Tests/R3IssueEvidenceVerifierTests.cs`

**Interfaces:**
- Produces: `R3IssueEvidenceVerifier.Verify(string manifestPath)` returning all 22 validated issue records.
- Each record exposes issue ID, remaining-defect flag, executable test totals, evidence subject commit/tree, and reachability fields when required.

- [ ] **Step 1: Write failing behavioral tests** for a literal valid 22-record manifest and mutations that remove R3-020, use a duplicate ID, retain a product defect, use zero/failed/skipped execution, break arithmetic, mark mock/build-only evidence GREEN, or omit production reachability.
- [ ] **Step 2: Run only `R3IssueEvidenceVerifierTests`** and confirm RED because the verifier does not exist.
- [ ] **Step 3: Implement the smallest strict JSON verifier** with exact ID-set comparison, positive executable GREEN totals, zero failures/skips, and reachability requirements.
- [ ] **Step 4: Rerun the focused tests** and confirm every case is GREEN.
- [ ] **Step 5: Commit the task** with the tests and implementation together.

### Task 2: Git-object evidence verifier

**Files:**
- Create: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Infrastructure/GitEvidenceVerifier.cs`
- Create: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Tests/GitEvidenceVerifierTests.cs`

**Interfaces:**
- Produces: `GitEvidenceVerifier.VerifyBlob(repositoryRoot, subjectCommit, subjectTree, relativePath, sha256, bytes)`.
- Produces: `GitEvidenceVerifier.VerifyPushedRef(repositoryRoot, remote, remoteRef, expectedCommit)`.

- [ ] **Step 1: Write failing tests** using a real temporary Git repository and local bare remote; cover valid blob/ref, working-tree-only evidence rejection, wrong subject tree, wrong bytes/hash, escaping path, stale remote ref, and unpushed commit.
- [ ] **Step 2: Run only `GitEvidenceVerifierTests`** and confirm RED because the verifier does not exist.
- [ ] **Step 3: Implement bounded Git process execution** with exact object syntax, captured binary blob bytes, timeout/cancellation, sanitized errors, and no shell interpolation.
- [ ] **Step 4: Rerun the focused tests** and confirm GREEN.
- [ ] **Step 5: Commit the task.**

### Task 3: Release-veto disposition

**Files:**
- Create: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Infrastructure/R3ReleaseVeto.cs`
- Create: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Tests/R3ReleaseVetoTests.cs`

**Interfaces:**
- Produces: `R3ReleaseDisposition` with `ChangesRequired`, `BlockedByExternalEnvironment`, and `ReadyForControlledReleaseReview`.
- Produces: `R3ReleaseVeto.Evaluate(R3ReleaseInputs inputs)`.

- [ ] **Step 1: Write failing table-driven tests** proving product/evidence/local failures always select `ChangesRequired`, external blocking is admitted only with all local layers GREEN and no product defect, and READY requires packaged discovery, every authorized native journey, exact identity, cleanup, and receipt join.
- [ ] **Step 2: Run only `R3ReleaseVetoTests`** and confirm RED because the evaluator does not exist.
- [ ] **Step 3: Implement the minimal precedence-based evaluator.**
- [ ] **Step 4: Rerun the focused tests** and confirm GREEN.
- [ ] **Step 5: Commit the task.**

### Task 4: Wire R3 preflight into the executable runner

**Files:**
- Modify: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/scripts/Invoke-E1EndToEnd.ps1`
- Create: `tests/E2ETests/GraniteEdgeAI.EndToEndTests/Tests/R3ReleaseVetoPreflightTests.cs`
- Modify: `tests/E2ETests/README.md`

**Interfaces:**
- Consumes: `R3IssueEvidenceVerifier`, `GitEvidenceVerifier`, and `R3ReleaseVeto`.
- Adds runner inputs for exact R3 closure manifest, candidate remote ref, and evidence-subject bindings.

- [ ] **Step 1: Write the failing preflight test** that runs the real verifiers from environment-provided exact inputs and rejects the current absent/stale R2 evidence.
- [ ] **Step 2: Run the preflight test** and confirm RED for the expected missing R3 evidence reason.
- [ ] **Step 3: Wire native runner stages** to set exact inputs, verify pushed candidate/blob identities, run the R3 preflight before native lock acquisition, and allow the dated R3 report as the only additional E1-owned file.
- [ ] **Step 4: Parse the PowerShell script and run deterministic tests** to confirm the runner remains executable and all deterministic tests are GREEN.
- [ ] **Step 5: Commit the task.**

### Task 5: Independent R3 campaign and report

**Files:**
- Create: `docs/audits/2026-08-29/E1-native-end-to-end-tests-r3.md`
- Modify only if all final gates pass: external `E1.json` handoff and native receipt through atomic publication.

**Interfaces:**
- Consumes the exact pushed C0 candidate, producer receipts/manifests, package manifest, authorized assets, one-worker settings, and native lock.
- Produces exact stage totals, hashes, identities, process-cleanup result, and one allowed release disposition.

- [ ] **Step 1: Re-query remote refs and prerequisite directories** and bind the exact candidate commit/tree/ref if available.
- [ ] **Step 2: Run deterministic, source, managed, integration, security/privacy, and package-construction checks** that the environment permits, recording non-zero totals.
- [ ] **Step 3: If and only if preflight passes, run authoritative packaged List and all native campaigns under the lock.**
- [ ] **Step 4: Verify zero owned descendants, exact arithmetic, report bytes/hash, Git blobs, evidence subjects, ancestry, remote ref, and clean worktree.**
- [ ] **Step 5: Write the R3 report with `CHANGES REQUIRED` unless every stricter release gate is proven.**
- [ ] **Step 6: Run the full E1 suite, commit, push only the E1 branch, and atomically publish receipts only when READY prerequisites are satisfied.**
