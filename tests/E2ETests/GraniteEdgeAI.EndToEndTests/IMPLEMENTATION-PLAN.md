# Native Packaged End-to-End Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:test-driven-development to implement each task test-first. E1 is the sole editor; no subagent may edit or run native UI work.

**Goal:** Add a discoverable out-of-process MSTest project that launches the exact packaged Granite Edge AI candidate by AUMID, drives its public Windows UI Automation surface, captures bounded diagnostics, and guards identity-bound native journeys with explicit local manifests.

**Architecture:** A plain x64 Windows test executable uses the inbox `UIAutomationClient` and `UIAutomationTypes` assemblies instead of Playwright, WinAppDriver, coordinates, OCR, or a separately installed automation server. Small infrastructure units validate candidate/asset/producer identities, allocate isolated test roots, activate the package, wait on observable UIA state, capture sanitized failure evidence, and expose route-neutral page objects. Deterministic infrastructure tests always run; packaged smoke and real-model acceptance tests become MSTest inconclusive with the exact missing guard rather than passing on fixtures.

**Tech Stack:** .NET 8 Windows, MSTest 4.3.2, Microsoft Testing Platform/Test SDK 18.8.1, Windows UI Automation COM/framework assemblies, Win32 package activation and screenshot APIs.

**Spec:** `C:\UCL-E1-Package\E1-Native-End-to-End-Tests\MASTER-PROMPT.md` and shared context `00`-`10` (external verified assignment package).

## Global Constraints

- Frozen source commit `4748fe04f19afdf6b27c4c12502b84db325e7294`, tree `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`.
- Edit only `tests/E2ETests/GraniteEdgeAI.EndToEndTests/**`, `tests/E2ETests/README.md`, safe E1 scripts/manifests, and `docs/audits/2026-08-28/E1-native-end-to-end-tests.md`.
- Do not edit production code or shared solution/project composition; give C0 the exact solution-entry proposal.
- No coordinates, arbitrary sleeps, OCR, visual-pixel functional assertions, production bypasses, committed model paths/weights, or weakening of identity/privacy checks.
- Missing packages, authorized assets, predecessor evidence, signing, policy permission, or native lock prerequisites are explicit blocked/inconclusive guards, never passing substitutes.

---

### Task 1: Project and deterministic identity boundaries

**Files:**
- Create: `GraniteEdgeAI.EndToEndTests.csproj`, `Infrastructure/CandidateManifest.cs`, `Infrastructure/AssetManifest.cs`, `Infrastructure/ProducerEvidenceSet.cs`, `Infrastructure/TestWorkspace.cs`
- Test: `Tests/CandidateManifestTests.cs`, `Tests/AssetManifestTests.cs`, `Tests/ProducerEvidenceSetTests.cs`, `Tests/TestWorkspaceTests.cs`

**Interfaces:**
- `CandidateManifest.Load(path)` validates frozen commit/tree, package family/application ID, executable path, SHA-256 and length against the actual file.
- `AssetManifest.Load(path)` accepts only stable asset IDs, routes, lowercase SHA-256 and byte lengths; local paths arrive separately and are verified before use.
- `ProducerEvidenceSet.Load(h1, m1, q1)` validates worker IDs, frozen/evidence-subject identities, required evidence kinds, arithmetic, and cross-route source/hardware/plan/output joins.
- `TestWorkspace.Create(testName)` creates a unique child below the configured E1 result root and disposes only that exact child.

- [ ] Write tests for valid records and each fail-closed mismatch.
- [ ] Run the focused project and confirm RED because the infrastructure types do not exist.
- [ ] Implement the minimal parsers, hashing, canonical checks and safe workspace ownership.
- [ ] Run focused tests and confirm GREEN.

### Task 2: Package activation, bounded UIA and diagnostics

**Files:**
- Create: `Automation/PackageActivator.cs`, `Automation/ConditionWait.cs`, `Automation/AutomationSession.cs`, `Automation/FailureDiagnostics.cs`, `Automation/OwnedProcessSet.cs`
- Test: `Tests/ConditionWaitTests.cs`, `Tests/PrivacyRedactorTests.cs`, `Tests/OwnedProcessSetTests.cs`

**Interfaces:**
- `PackageActivator.Activate(aumid)` calls `IApplicationActivationManager.ActivateApplication` and returns the owned PID.
- `ConditionWait.Until` polls an observable predicate with monotonic elapsed time, bounded timeout and cancellation.
- `AutomationSession` locates the candidate window by process ID, finds descendants by automation ID or exact accessible name, invokes supported patterns, sets values and sends keyboard gestures without coordinates.
- `FailureDiagnostics` writes a window screenshot and depth-bounded UIA tree after redacting absolute Windows paths; screenshots are diagnostics only.
- `OwnedProcessSet.Dispose` closes/kills only recorded candidate descendants and verifies exit.

- [ ] Write deterministic wait/redaction/ownership tests and confirm RED.
- [ ] Implement the Win32/UIA adapters and minimal diagnostic capture.
- [ ] Run focused tests and confirm GREEN.

### Task 3: Route-neutral page objects and guarded journey catalogue

**Files:**
- Create: `Pages/ImportPage.cs`, `Pages/InspectionPage.cs`, `Pages/HardwarePage.cs`, `Pages/CompatibilityPage.cs`, `Pages/OptimizationPage.cs`, `Pages/ChatPage.cs`, `Journeys/PackagedJourneyFixture.cs`, `Journeys/PackagedSmokeJourneys.cs`, `Journeys/IdentityBoundAcceptanceJourneys.cs`, `Journeys/FailureAndRecoveryJourneys.cs`

**Interfaces:**
- Page objects expose only public accessibility IDs/names and observable terminal states; no page object contains source/output identity assertions.
- `PackagedJourneyFixture` requires the native lock/predecessor receipts, candidate manifest and optional asset/evidence inputs, resets the app between independent tests, and attaches diagnostics on failure.
- Smoke tests cover launch/onboarding/import shell, malformed-source failure, cancellation/retry and path privacy when a signed candidate and small authorized fixtures are supplied.
- Acceptance tests catalogue GGUF direct Chat, GGUF optimization/reinspection/Chat/export, OpenVINO direct Chat, OpenVINO persistent/runtime optimization, Chat gestures/reload, restart/stale rejection and artifact-publication failure. Each requires explicit route assets and verified H1/M1/Q1 manifests.

- [ ] Write journey tests first with explicit MSTest categories and guard messages; confirm discovery and RED/blocked disposition against the unmodified baseline environment.
- [ ] Implement only the page-object actions needed by those tests.
- [ ] Run listing, smoke, failure and acceptance filters; reject zero discovery.

### Task 4: Operator scripts, manifest examples and documentation

**Files:**
- Create: `manifests/candidate-manifest.schema.json`, `manifests/asset-manifest.schema.json`, `scripts/Invoke-E1EndToEnd.ps1`
- Modify: `tests/E2ETests/README.md`

**Interfaces:**
- The script restores/builds Debug x64, resolves the app `.build.appxrecipe`, locates `vstest.console.exe`, lists tests before filters, passes explicit sanitized manifest paths, and records exit/count/hash metadata below ignored `TestResults/Audit-20260828/E1/`.
- README documents guards, AUMID construction, native lock/order, evidence joins, privacy, categories, and the exact C0 solution proposal: `<Project Path="tests/E2ETests/GraniteEdgeAI.EndToEndTests/GraniteEdgeAI.EndToEndTests.csproj"><Platform Solution="*|x64" Project="x64" /></Project>` under `/tests/E2ETests/`.

- [ ] Add script contract tests where behavior can be validated without native launch.
- [ ] Implement schemas/script/docs and run syntax/schema checks.

### Task 5: Verification and two-phase handoff

**Files:**
- Create: `docs/audits/2026-08-28/E1-native-end-to-end-tests.md`
- Create outside Git: `C:\UCL-AUDIT-HANDOFFS\E1.json` and, after native phase, `C:\UCL-AUDIT-NATIVE-RECEIPTS\E1.json`

- [ ] Run non-zero listing, deterministic tests, packaged smoke/failure filters, guarded acceptance, relevant regressions and `git diff --check`; record passed/failed/blocked/skipped/not-run separately.
- [ ] Commit implementation/tests first and bind the report to that implementation commit/tree.
- [ ] Perform a fresh second-pass review (subagents are not authorized by this session), fix Critical/Important issues, and rerun affected verification.
- [ ] Commit the final report separately, push only `test/ucl-native-e2e-v1`, or create and verify a bundle after bounded push failure.
- [ ] Validate the final handoff receipt schema, hashes, byte counts, tip/tree and clean worktree.
