# S1 Security and Packaging R4 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close S1-owned native process environment, path/custody, package verification, and R4 schema trust-boundary gaps without weakening native or packaging policy.

**Architecture:** Harden the existing S1-owned security primitives and keep launchers behind their existing public seams. Introduce operation-owned temporary-directory custody where process launch currently inherits parent TEMP/TMP, reuse strict path/custody checks across environment and package verification, and validate package/schema closure with deterministic machine-readable outputs.

**Tech Stack:** C#/.NET 8, Win32 process/job/ACL APIs, MSTest/VSTest, PowerShell package gates, JSON Schema draft 2020-12, WinUI/MSIX build tooling.

---

## Requirement-to-evidence matrix

| Requirement | Production owner/caller | Baseline defect or evidence | Planned verification | Dependency/disposition |
|---|---|---|---|---|
| R1 closed environment and private TEMP/TMP | `TrustedToolEnvironmentPolicy`, `WindowsSuspendedProcess`; parallel worker policies/launchers | Hardware, GGUF, quantizer, Model Inspection, and OpenVINO policies accept inherited TEMP/TMP; root validation only checks absolute/existing | hostile-parent and poisoned TEMP/TMP RED/GREEN; canonical Windows-root, path-form, reparse, ACL, cleanup tests | S1 primitives; cross-assembly consumers updated only at owned security seams |
| R2 bounded native launch | existing suspended launchers, job objects, bounded stream readers | audit explicit paths, argument arrays, timeout/cancel, output bounds, tree cleanup | focused process suites including blocked streams, timeout, cancellation, zero job members | preserve existing compositions; fix only demonstrated S1 gaps |
| R3 equivalent path/file/hash custody | trusted-tool, GGUF, quantizer, Model Inspection, OpenVINO verifiers | existing validators differ in path-form/ancestor/race checks | negative path/reparse/race/hash/length suites and production reachability scan | expose narrow shared rule or apply-checkable C0 proposal where assembly ownership prevents reuse |
| R4 GGUF build/runtime parity | `GgufQuantizerPackageVerifier`, `Test-GgufQuantizerPackage.ps1` | R3 bounded non-recursive inventory is green | deep/wide/duplicate/case/reparse reason-class parity tests | preserve R3 behavior |
| R5 package closure allowlist | main csproj/manifest and packaging scripts | no R4 machine-readable closure diff; historical DebugFixtures hazard | Release x64 evaluated/output closure scan with exact allowlist/diff JSON | shared composition defects become C0 proposals unless safely owned |
| R6 network capability/download-only | `Package.appxmanifest`, download authority and endpoint catalogue | audit required; no upload path is authorized | manifest capability and pinned HTTPS/length/hash source scans/tests | no UX/backend redesign |
| R7 path-private diagnostics | S1 exceptions and process/client mappings | audit for echoed path/host/provider values | sentinel privacy tests and repository scan | fix owned messages; report presentation issues |
| R8 no machine-policy mutation | repository diff and executed commands | invariant | diff/command audit | external policy blocks recorded, never bypassed |
| R9 R4 schemas and mutation tests | two committed schemas plus schema-validation tests | schemas absent on R3 | byte identity, draft-2020-12 validation, required mutations and transport rules | S1 campaign infrastructure; `evidenceManifest` is null for S1 |
| Application/screenshot gate | exact Release x64 app/packageable output | native activation may be externally blocked | bounded launch/smoke, privacy-safe captures when activation works, process cleanup | no Intel-native/performance claim |
| Durable handoff | R4 report and receipt | absent | schema validation, hashes/bytes, immutable subject, push and remote equality | receipt finalized after implementation subject/report |

### Task 1: Establish supported baseline and behavioral RED

**Files:**
- Test: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Processes/TrustedToolEnvironmentPolicyTests.cs`
- Test: worker-client environment and process test projects under `tests/UnitTests` and `tests/IntegrationTests`

- [ ] Discover tests with the repository-supported VS Test runner and require non-zero discovery.
- [ ] Run current focused security/environment/package suites and record exact arithmetic.
- [ ] Add hostile environment/path/temp behavioral tests that compile and fail for the defective behavior, not for host setup.
- [ ] Commit the RED tests separately.

### Task 2: Harden operation environment and temporary custody

**Files:**
- Modify: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Processes/TrustedToolEnvironmentPolicy.cs`
- Modify/create: focused types in `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Processes/`
- Modify: native launcher consumers only where required to own/dispose operation temp custody
- Test: corresponding environment/process suites

- [ ] Validate `SystemRoot` and `WINDIR` against the canonical local Windows directory and require equality.
- [ ] Reject UNC, device, alternate-stream, traversal, whitespace/control-confused, missing, and any-ancestor reparse paths.
- [ ] Create a unique application-owned local operation temp with restrictive current-user ACL, exact TEMP/TMP binding, and bounded non-recursive cleanup.
- [ ] Include validated `DOTNET_ROOT*` only for managed verified tools that require it; keep diagnostics/profiling disabled and all other parent variables absent.
- [ ] Run focused GREEN three times and mutation-check the central rejection.
- [ ] Commit the implementation and GREEN tests.

### Task 3: Audit and close launcher/custody parity gaps

**Files:**
- Modify: S1-owned verifier/launcher primitives proven defective by Task 1/audit
- Test: affected GGUF, quantizer, Model Inspection, OpenVINO, hardware suites

- [ ] Inventory every production native launcher and verifier with its real caller/composition path.
- [ ] Correct demonstrated gaps in explicit executable identity, argument transport, bounded drain, timeout/cancel, tree termination, post-cleanup emptiness, ancestor/reparse/regular-file/size/hash enforcement.
- [ ] Preserve non-recursive GGUF package inventory and align C#/PowerShell failure classes.
- [ ] Run negative, boundary, race, cleanup, and legitimate verified-tool tests; commit focused changes.

### Task 4: Commit and test campaign schemas

**Files:**
- Create: `docs/audits/2026-08-30/schemas/R4-HANDOFF-RECEIPT-SCHEMA.json`
- Create: `docs/audits/2026-08-30/schemas/R4-EVIDENCE-MANIFEST-SCHEMA.json`
- Create/modify: focused schema validation tests under `tests/SecurityAudit/GraniteEdgeAI.SecurityAudit.Tests/`

- [ ] Add byte-identical schema files from the supplied package and verify SHA-256/byte equality.
- [ ] Validate both as draft 2020-12 schemas.
- [ ] Add valid-instance tests and mutations for worker/branch/ref/subject/report/test arithmetic/native/evidence/additional-fields/transport rules.
- [ ] Run schema tests and commit.

### Task 5: Package/network/privacy closure

**Files:**
- Create: machine-readable R4 package allowlist/diff under `docs/audits/2026-08-30/`
- Modify: S1-owned packaging/security sources only for demonstrated defects

- [ ] Build Release x64 with the strongest available package gate.
- [ ] Inspect evaluated and output closure for unresolved expressions, fixtures, duplicate workers, private evidence, raw dumps/TRX, model assets, paths/secrets, and permissive capabilities.
- [ ] Verify `internetClient` is limited to the pinned verified-download route and source contains no upload proxy/telemetry path.
- [ ] Run sensitive/path/binary/duplicate scans and `git diff --check`.
- [ ] Commit allowlist/diff and any S1 corrections; record cross-owner proposals precisely.

### Task 6: Managed application smoke and independent review

**Files:**
- Evidence remains in a git-ignored operation directory; report records only privacy-safe hashes/metadata.

- [ ] Launch the exact built/packageable app once when host activation permits; exercise trusted-tool truth/error/cancel/timeout/shutdown states.
- [ ] Capture and inspect privacy-safe screenshots for leaked paths/provider text, blank/duplicate errors, and false-success security states.
- [ ] Verify zero owned descendants/temp remnants and record exact native block if activation is unavailable.
- [ ] Perform requirements/architecture review and adversarial security/code/test review; fix all Critical/Important S1 findings and rerun affected gates.

### Task 7: Finalize immutable handoff and push

**Files:**
- Create: `docs/audits/2026-08-30/S1-security-packaging-r4.md`
- Create: `docs/audits/2026-08-30/handoffs/R4-S1.json`

- [ ] Commit the immutable implementation/evidence subject and record its commit/tree.
- [ ] Finalize the report with exact changed paths, evidence arithmetic, blockers/non-claims, defect ledger, and C0 consumption list.
- [ ] Hash the finalized report and create the schema-valid receipt with `evidenceManifest: null`.
- [ ] Commit report/receipt, verify clean status, push `audit/ucl-s1-security-remediation-r4`.
- [ ] Resolve the remote tip/tree independently and prove it contains exact report/receipt/schema blobs and descends from the implementation subject.
