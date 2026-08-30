# M1 R4 Model Inspection Closure Implementation Plan

> **For agentic workers:** Execute inline with the required Superpowers workflows. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the route-exact Model Inspection schema, typed-result, custody, workflow, and reproducible-evidence gaps on the assigned M1 R4 branch.

**Architecture:** Keep route-specific inspection and parsing behind the existing public services. Make the shared Model Inspection handoff record the canonical six-field codec, use narrow GGUF/OpenVINO adapters, and map fixed OpenVINO support codes into typed terminal outcomes without changing shell, XAML, project composition, or optimization publication.

**Tech Stack:** C# 12, .NET 8 targets with .NET SDK 10.0.301/Microsoft.Testing.Platform, MSTest, WinUI 3, PowerShell/GitHub Actions.

---

## Requirement-to-evidence matrix

| R4 requirement | Production owner/caller/composition | Baseline or RED | Planned GREEN/evidence | Cross-owner boundary |
| --- | --- | --- | --- | --- |
| 1. Live GGUF/OpenVINO routes and lifecycle | `ModelInspectionHandoffRegistry`, `ModelInspectionProjectionFactory`, `OpenVinoRouteService`, `ModelInspectionServiceComposition` | GGUF 2/2 and OpenVINO 404/411 baseline | focused route, registry issue/claim/rollback/reissue/invalidation/no-reuse suites | shell registration is read-only |
| 2. One canonical six-field schema | `shared/.../ModelInspectionProjectionV2.cs`; GGUF/OpenVINO adapters | three boundary representations can drift | exact canonical byte snapshots, UUID-role/digest/length/order/512/privacy mutations | no Q1 fields; no project-reference edit |
| 3. Actual package inspection and cleanup | GGUF worker boundary; `OpenVinoStaticPackageInspector` and `OpenVinoRouteService` | current suites pass; operational categories collapse | package/source/worker result tests and cleanup assertions | no conversion/optimization publication edits |
| 4. Typed OpenVINO result | `OpenVinoRouteInspectionOutcome`, `OpenVinoRouteService.InspectAsync` | support codes map to `Invalid` | RED/GREEN for dependency, cancelled, timeout, invalid, stale, unsupported, conversion and success | page consumes existing result contract |
| 5. Contract/workflow/inventory drift | Model Inspection workflows, evaluated-MSBuild test support, cleanup inventory/source list | 404/410 with six hard-coded-host errors; old MTP syntax | portable exact SDK host resolution, `--project` workflow contracts, inventory scans | fixture composition remains C0 proposal |
| 6. Validate before lease consumption/privacy | `OpenVinoRouteHandoffLease`, `StartSessionAsync`, registry handles | adverse projection test passes | retain regression plus path/filename/free-text/model-byte canaries | no navigation payload edits |
| 7. Reduce high-risk complexity only | route mapping/codec helpers | mapping and schema logic duplicated | small cohesive codec/mapping helpers behind existing contracts | no Q1/frontend refactor |
| 8. Preserve compatibility names | canonical handoff/projection properties and compatibility consumers | names currently exact | source/duplicate scan and consumer suites | `productHardwareRunId` remains at downstream seam |
| App/screenshot gate | exact Debug x64 app output and Model Inspection page | not yet run on R4 worktree | controlled launch, both-route smoke where fixtures/activation permit, privacy-safe screenshots and defect ledger | route XAML defects go to F1/C0 |
| Durable handoff | audit report, evidence manifest, R4 receipt | absent | schema validation, subject commit/tree binding, hashes/bytes, remote equality/ancestry | S1 owns committed schema blobs |

### Task 1: Canonical six-field schema authority

**Files:**
- Modify: `shared/GraniteEdgeAI.ModelInspection.Contracts/Evidence/ModelInspectionProjectionV2.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Application/ModelInspectionHandoffCodec.cs`
- Test: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/Protocol/ModelInspectionProjectionV2Tests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Contracts/ModelInspectionHandoffTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Inspection/OpenVinoInspectionHandoffFactoryTests.cs`

- [ ] Add failing tests for shared canonical handoff bytes, same-role UUID rejection, strict order/unknown fields, and byte equality with both route adapters.
- [ ] Run only the new rows and confirm behavioral failures for missing authority/validation.
- [ ] Add validation/codec methods to the shared handoff record and delegate projection handoff validation to it.
- [ ] Adapt the GGUF codec through the shared canonical value without changing its application-facing type.
- [ ] Run the focused rows, then the shared contracts, GGUF handoff, and OpenVINO suites.
- [ ] Commit the schema change as one focused implementation commit.

### Task 2: Typed OpenVINO inspection outcomes and custody

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/OpenVinoRouteStateMachine.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/OpenVinoRouteService.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.OpenVino.cs` only if exhaustive result consumption requires it
- Test: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/OpenVinoRouteServiceTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/OpenVinoRouteStateMachineTests.cs`

- [ ] Add failing data-driven inspection tests for dependency unavailable, cancelled, timed out, invalid evidence, stale evidence, unsupported, conversion-required, and success.
- [ ] Assert every non-success result has no handoff/configuration and no path-bearing offer except conversion-required.
- [ ] Run the new rows and confirm current `Invalid` collapsing/cancellation behavior is the cause.
- [ ] Add the minimal exhaustive support-code-to-outcome mapping and expected cancellation handling.
- [ ] Update only the existing Model Inspection result consumer needed for exhaustive typed presentation.
- [ ] Rerun focused tests three times without sleeps, then the complete OpenVINO suite.
- [ ] Temporarily reverse one key mapping, prove its regression fails, restore, and rerun GREEN.
- [ ] Commit the typed-result correction separately.

### Task 3: Portable evaluated closure and MTP workflows

**Files:**
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/Support/EvaluatedMsBuildItems.cs`
- Modify: M1-owned `.github/workflows/model-inspection-*.yml`, `.github/workflows/llamasharp-*.yml`, and affected Model Inspection commands in `.github/workflows/build-and-test.yml`
- Test: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/Gate5ApplicationBoundaryContractTests.cs`
- Test: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/M1R3PackageClosureContractTests.cs`
- Test: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/M1R3DebugFixtureRootContractTests.cs`

- [ ] Add a contract that requires MTP `--project` syntax and forbids a repository claim that one absolute SDK path is the sole fallback.
- [ ] Run the new contract and confirm the existing workflows/helper fail it.
- [ ] Resolve a concrete installed dotnet host through explicit/current/SDK-root/standard-install candidates with bounded validation.
- [ ] Update only M1-owned test commands to MTP syntax and preserve all filters/test floors/TRX gates.
- [ ] Rerun the six formerly blocked evaluated-closure rows with no environment override.
- [ ] Rerun the full Model Inspection contract suite and update cleanup inventory/source list if their exact closure changed.
- [ ] Keep the C0 fixture-boundary proposal apply-checkable; do not edit shared project composition.
- [ ] Commit workflow/test-infrastructure closure separately.

### Task 4: Full managed and app verification

**Files:**
- Evidence only under ignored `artifacts/r4-m1/` until final sanitized records are written.

- [ ] Discover and run Model Inspection contracts, transport, worker, worker client, structural worker-process, GGUF handoff, OpenVINO, compatibility, and Hardware Foundation suites with exact arithmetic.
- [ ] Run Debug x64 app/test builds and the strongest package/worker gate permitted by current policy.
- [ ] Run evaluated package/fixture matrix, cleanup/inventory/workflow, privacy/path, duplicate type/schema/registration, and `git diff --check` gates.
- [ ] Launch the exact built app once under controlled ownership, exercise both affected route states available on this host, capture privacy-safe screenshots, inspect full resolution, and record defects/blockers honestly.
- [ ] Confirm no owned app/worker/tool processes or temporary custody remain.

### Task 5: Review, subject freeze, and durable R4 handoff

**Files:**
- Create: `docs/audits/2026-08-30/M1-model-inspection-r4.md`
- Create: `docs/audits/2026-08-30/evidence/M1-model-inspection-r4.json`
- Create: `docs/audits/2026-08-30/handoffs/R4-M1.json`
- Modify: `docs/audits/2026-08-28/proposals/M1-R3-shared-project-fixture-boundary.diff` only if the exact C0 proposal needs an apply-checkable update

- [ ] Perform requirements/architecture and adversarial code/security/test reviews; fix every in-scope Critical/Important finding and rerun affected gates.
- [ ] Re-audit every requirement-to-evidence row and freeze the implementation/evidence subject commit and tree.
- [ ] Finalize the report without self-hashes and include the exact C0 integration note.
- [ ] Create the evidence manifest against the supplied R4 v2 schema, hashing the finalized report and binding the subject.
- [ ] Create the receipt against the supplied R4 v2 schema, hashing the report/manifest and binding the same subject.
- [ ] Commit durable artifacts, verify clean status, push `audit/ucl-m1-model-inspection-remediation-r4`, and independently prove remote equality, ancestry, and exact report/manifest/receipt blobs.
