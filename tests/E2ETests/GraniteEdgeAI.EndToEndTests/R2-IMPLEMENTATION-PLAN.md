# E1 R2 Native Acceptance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a fail-closed executable E1 R2 lane that binds an exact integrated C0 package candidate, verifies predecessor provenance before native locking, catalogues every required journey, and records authoritative stage counts without converting guards into passes.

**Architecture:** Keep deterministic identity/provenance validation independent from package activation. The PowerShell orchestrator receives the immutable C0 candidate commit/tree explicitly, proves frozen/previous-C0/specialist ancestry, validates sanitized predecessor inputs through the test assembly, and only then acquires the shared native lock for packaged stages. Native journey classes map one test to each approved scenario and use accessible UIA actions where available; missing safe automation seams end in precise inconclusive dispositions.

**Tech Stack:** .NET 8 Windows, MSTest, Windows UI Automation, PowerShell, Git object verification, Visual Studio VSTest x64, JSON/SHA-256 receipts.

**Spec:** User-approved E1 R2 request dated 2026-08-28 in this session.

## Global Constraints

- Frozen commit/tree: `4748fe04f19afdf6b27c4c12502b84db325e7294` / `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`.
- Previous C0 tip: `a5ef3558334e50587889140dafba194853938765`.
- Never execute native/package stages from the previous C0 tip alone.
- Modify only E1 test/infrastructure/documentation/report paths; never production behavior.
- Never fabricate receipts, evidence, tools, assets, package trust, results, or cleanup.
- Never acquire `C:\UCL-AUDIT-NATIVE.lock` before all candidate and predecessor gates pass.

---

### Task 1: Immutable integration-candidate identity

**Files:**
- Create: `Infrastructure/IntegrationCandidate.cs`
- Test: `Tests/IntegrationCandidateTests.cs`
- Modify: `scripts/Invoke-E1EndToEnd.ps1`, `Infrastructure/CandidateManifest.cs`

**Interfaces:**
- `IntegrationCandidate.Create(commit, tree, previousC0Tip)` validates lowercase Git identities and rejects the previous tip as a final native candidate.
- The runner requires `-IntegrationCandidateCommit` and `-IntegrationCandidateTree`, verifies exact Git tree plus frozen/previous-C0 ancestry, proves E1 `HEAD` descends from the candidate, and binds the local package manifest to that immutable pair.

- [ ] Write tests that reject the previous C0 tip and malformed/mismatched identities.
- [ ] Run focused tests and confirm RED because `IntegrationCandidate` does not exist.
- [ ] Implement the minimum immutable identity contract and update the runner.
- [ ] Run focused tests and confirm GREEN.

### Task 2: Predecessor handoff/evidence/native-receipt closure

**Files:**
- Create: `Infrastructure/PredecessorEvidenceVerifier.cs`
- Test: `Tests/PredecessorEvidenceVerifierTests.cs`
- Modify: `scripts/Invoke-E1EndToEnd.ps1`, `Infrastructure/ProducerEvidenceSet.cs`

**Interfaces:**
- `PredecessorEvidenceVerifier.Verify(workerId, handoffPath, nativeReceiptPath, evidenceManifestPath?)` verifies frozen identities, final tip/tree agreement, report and handoff hashes/bytes, closed cleanup, evidence-subject agreement, evidence-manifest hash/bytes, non-negative command arithmetic, and required stable evidence kinds.
- The runner verifies H1/M1/Q1/F1 records before lock acquisition and uses Git to prove their final tips are ancestors of the C0 candidate. It verifies accepted A1/S1/T1 final tips are also ancestors.

- [ ] Write valid closure fixtures and one failing test per mismatch family.
- [ ] Run focused tests and confirm RED because the verifier does not exist.
- [ ] Implement strict parsing/digest/arithmetic/identity joins without accepting private paths.
- [ ] Run focused tests and confirm GREEN.

### Task 3: Complete journey catalogue and stage taxonomy

**Files:**
- Create: `Journeys/DownloadAndIntegrityJourneys.cs`, `Journeys/ChatLifecycleJourneys.cs`, `Journeys/AccessibilityAndCleanupJourneys.cs`
- Modify: `Journeys/PackagedSmokeJourneys.cs`, `Journeys/FailureAndRecoveryJourneys.cs`, `Journeys/IdentityBoundAcceptanceJourneys.cs`, `Pages/*.cs`
- Test: `Tests/JourneyCatalogueTests.cs`

**Interfaces:**
- Every approved journey has a unique MSTest method and one category among `NativeSmoke`, `NativeFailure`, `NativeAcceptance`, `NativeRestart`, or `NativeRealModel`.
- `JourneyCatalogue.RequiredNames` is the deterministic source of the 20 required scenario identities; reflection verifies every name is discovered and categorized.
- Unsupported native seams call `Assert.Inconclusive` with a product/environment-specific reason after prerequisite validation; they never pass on a surface-only assertion.

- [ ] Write reflection tests for exact journey names/categories and confirm RED for missing journeys.
- [ ] Add the missing guarded journeys and only the UIA actions supported by public accessibility contracts.
- [ ] Run catalogue tests and confirm GREEN.

### Task 4: Campaign accounting and cleanup proof

**Files:**
- Create: `Infrastructure/TrxStageSummary.cs`
- Test: `Tests/TrxStageSummaryTests.cs`
- Modify: `scripts/Invoke-E1EndToEnd.ps1`, `Automation/OwnedProcessSet.cs`

**Interfaces:**
- `TrxStageSummary.Load(path)` returns exact discovered/executed/passed/failed/skipped counts and rejects inconsistent totals or zero discovery.
- Runner stages are `Deterministic`, `List`, `Smoke`, `Failure`, `Acceptance`, `Restart`, `RealModel`, and `All`; each execution writes a unique TRX and JSON summary below ignored results.
- Native cleanup records candidate-root processes created after lock acquisition; it releases the lock only after zero descendants remain.

- [ ] Write TRX arithmetic tests and confirm RED.
- [ ] Implement parsing and stage filters; keep deterministic execution outside the native lock.
- [ ] Run focused and complete deterministic tests and confirm GREEN.

### Task 5: Candidate rerun, report, transport, and receipts

**Files:**
- Create: `docs/audits/2026-08-28/E1-native-end-to-end-tests-r2.md`
- Replace atomically after final verification: `C:\UCL-AUDIT-HANDOFFS\E1.json`
- Publish after cleanup verification: `C:\UCL-AUDIT-NATIVE-RECEIPTS\E1.json`

**Interfaces:**
- Report separates passed, failed, blocked, skipped-by-guard, and not-run for every stage; `evidenceManifest` remains `null`.
- Receipt binds the clean pushed E1 tip/tree, report hash/bytes, exact totals, transport, and native disposition.

- [ ] Recheck for a new C0 candidate and all predecessor records.
- [ ] Run deterministic first, then authoritative List/Smoke/Failure/Acceptance/Restart/RealModel only when all gates pass.
- [ ] Commit E1 implementation before the report, then commit the report separately.
- [ ] Push only `test/ucl-e1-native-acceptance-r2`, validate the final handoff schema, and atomically replace E1 receipts only after final verification.
