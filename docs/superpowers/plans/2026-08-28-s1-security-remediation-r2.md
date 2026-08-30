# S1 Security Remediation R2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Independently verify C0's quantizer isolation correction, remediate remaining S1-owned process/privacy/package boundary defects, and publish a clean auditable S1 handoff.

**Architecture:** Preserve each existing product contract and apply corrections only at infrastructure launch and validation boundaries. Every correction begins with a consumer-visible failing test, uses explicit executable identity and closed environment data, and leaves native policy/signing decisions fail-closed.

**Tech Stack:** C#/.NET 8 and 10 test projects, MSTest/Microsoft Testing Platform, Windows CreateProcess/job objects, MSBuild package evaluation, PowerShell validation, Git.

**Spec:** Direct user request dated 2026-08-28; report contract `docs/audits/2026-08-28/S1-security-packaging-remediation-r2.md`.

## Global Constraints

- Base commit/tree must remain `a5ef3558334e50587889140dafba194853938765` / `90c34ab009b744d7b00866fb93e8dbc86363f1b2` with frozen ancestor/tree `4748fe04f19afdf6b27c4c12502b84db325e7294` / `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`.
- Do not redesign H1/M1/Q1 semantics, frontend behavior, or C0 shared composition.
- Use RED-GREEN TDD for every production correction.
- Do not weaken App Control, signing, trust roots, firewall, manifest digests, or policy.
- `evidenceManifest` in the final handoff is `null`.

---

### Task 1: Verify C0 Quantizer Isolation

**Files:**
- Review: `infrastructure/GraniteEdgeAI.GgufQuantization.WorkerClient/**`
- Test: `tests/UnitTests/GraniteEdgeAI.GgufQuantization.WorkerClient.Tests/**`

**Interfaces:**
- Consumes: C0 commit `8131e110` closed-environment implementation.
- Produces: independent test totals and a retain/repair disposition.

- [ ] Build the focused worker-client test project with the locally available SDK.
- [ ] Run the real fake-quantizer sentinel regression and policy tests.
- [ ] Confirm executable identity remains absolute and package verified.
- [ ] Retain the implementation unchanged if all checks pass.

### Task 2: Audit Production Launch Boundaries

**Files:**
- Review: `infrastructure/**`, `workers/**`, and application process starters.
- Test: the owning worker-client/foundation process test projects.

**Interfaces:**
- Consumes: existing verified package/tool records and process-launch contracts.
- Produces: file-by-file disposition and focused tests for every confirmed S1 defect.

- [ ] Inventory every production `Process.Start`, `ProcessStartInfo`, and `CreateProcess` call.
- [ ] Trace executable origin, environment block, arguments, handles, timeout, cancellation, descendants, and cleanup.
- [ ] For each confirmed defect, add a real behavior test and verify the expected RED failure.
- [ ] Implement only the minimum S1-owned environment/executable/cleanup correction and verify GREEN.

### Task 3: Audit Manifest, Hash, Path, and Privacy Boundaries

**Files:**
- Review: package verifiers, manifest parsers, storage leases, evidence validators, package project files, workflows, and registration composition.
- Test: owning contract/unit tests plus S1 security scanners.

**Interfaces:**
- Consumes: manifest/package inputs and build item graphs.
- Produces: bounded regular-file/streaming/path/privacy dispositions and any TDD corrections.

- [ ] Check full qualification, regular-file status, size limits, non-reparse custody, exact byte count/hash, and streamed large-file operations.
- [ ] Check traversal, junction/symlink substitution, case collisions, duplicate JSON properties, malformed escapes, oversized JSON, and stale identity rejection.
- [ ] Scan tracked content and evaluated package/registration closures for private evidence, debug/test/audit assets, duplicates, and alternate implementations.
- [ ] Keep external UCL policy or unavailable native artifacts classified as blocked, never passed.

### Task 4: Verify and Publish

**Files:**
- Create: `docs/audits/2026-08-28/S1-security-packaging-remediation-r2.md`
- Publish externally: `C:\UCL-AUDIT-HANDOFFS\S1.json`

**Interfaces:**
- Consumes: final clean Git tree, exact test ledger, report bytes/hash, and handoff schema.
- Produces: pushed S1 branch and schema-valid receipt with `evidenceManifest: null`.

- [ ] Run all affected process/package/privacy/path tests and record discovered/executed/passed/failed/skipped totals.
- [ ] Run package-content and registration scans plus `git diff --check`.
- [ ] Write the report with file-by-file finding ownership and exact native disposition.
- [ ] Commit all S1 changes, rerun fresh verification, and push only `audit/ucl-s1-security-remediation-r2`.
- [ ] Atomically replace `C:\UCL-AUDIT-HANDOFFS\S1.json` and validate it against `10-HANDOFF-RECEIPT-SCHEMA.json`.
