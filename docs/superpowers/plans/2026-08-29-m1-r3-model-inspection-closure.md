# M1 R3 Model Inspection Closure Implementation Plan

> Execute this plan in the isolated `C:\UCL-M1-R3` worktree on
> `audit/ucl-m1-model-inspection-remediation-r3`. The approved design is
> `docs/superpowers/specs/2026-08-29-m1-r3-model-inspection-closure-design.md`.

**Goal:** Close M1-owned R3-001 through R3-004 with executable contracts,
route-bound schema-v2 projections, and independently reproducible evidence.

**Constraints:** Preserve route-specific validation and existing production
composition. Do not edit Q1 optimization behavior, shared navigation/package
composition, Application Control, or native trust policy. Native execution may
start only after fresh H1 handoff and phase receipts validate.

## Task 1: Make evidence arithmetic and identity executable (R3-001)

**Files:**

- Create `scripts/model-inspection/Test-M1R3AuditEvidence.ps1`.
- Create `tests/Contracts/M1R3AuditEvidenceContractTests.cs`.

1. Add a contract fixture reproducing the R2 arithmetic defect: discovered
   409, executed 402, passed 402, failed 0, skipped 7. Assert that the validator
   exits non-zero and names the violated equation.
2. Run the focused test and retain the failing result as RED evidence.
3. Implement a bounded JSON validator enforcing `executed = passed + failed +
   skipped`, `discovered >= executed`, exact byte counts/SHA-256, report and
   manifest Git blobs, implementation-subject versus final Git identity,
   frozen ancestry, pushed remote ref, clean worktree, R3 artifact paths, and
   receipt-to-manifest joins. If native evidence exists, require its hash and
   identity to bind to the exact H1/M1 phase receipts.
4. Add valid fixtures plus one mutation per invariant. Run the focused class
   until all cases pass.
5. Commit the validator and contracts with the implementation commit; do not
   generate final evidence yet.

## Task 2: Evaluate package closures without expression allowlists (R3-002)

**Files:**

- Create `tests/Contracts/Support/EvaluatedMsBuildItems.cs`.
- Create `tests/Contracts/M1R3PackageClosureContractTests.cs`.
- Modify the existing Model Inspection package-boundary contract files found
  by the RED test.

1. Write a controlled MSBuild fixture whose imported target leaves a property,
   item, or metadata expression in a final package item. Assert failure without
   using a source-path exception.
2. Run the focused class and retain RED evidence showing the current allowlist
   accepts the unresolved closure.
3. Add an MSBuild evaluation helper that imports the production project/targets,
   supplies controlled stage roots and required properties, executes the item
   construction targets, and returns normalized final item identities and
   target paths. Bound process time/output and fail on non-zero exit, missing
   output, duplicate target, root escape, or surviving `$(...)`, `@(...)`, or
   `%(...)` syntax.
4. Remove `IsControlledImportedPackageExpression` and equivalent unresolved
   expression allowlists/booleans. Validate resolved controlled closures and
   the real production closure. Record an unresolved GGUF production target as
   an exact R3-016 owner blocker; do not weaken the gate.
5. Run the focused class and the existing package/privacy boundary classes.

## Task 3: Discover and verify every production DebugFixtures root (R3-003)

**Files:**

- Create `tests/Contracts/Support/ProductionDebugFixtureInventory.cs`.
- Create `tests/Contracts/M1R3DebugFixtureRootContractTests.cs`.
- Modify existing fixture-boundary contracts only where the RED test proves a
  static or incomplete root list.

1. Add a mutation fixture introducing `Features/NewFeature/DebugFixtures` to a
   temporary production project without complete exclusions. Assert the root is
   discovered and the closure fails.
2. Run the focused class and retain RED evidence showing the baseline verifier
   misses the added root.
3. Discover all `Features/*/DebugFixtures` roots represented by the filesystem
   and application project. Evaluate Compile, Page, Content, None,
   EmbeddedResource, and PRIResource for default, Release, x86, ARM64, and Debug
   x64 configurations.
4. Require zero fixture items outside approved Debug x64 conditions; require
   Debug x64 to contain only explicitly authorized items. Evaluate Model
   Hardware Compatibility gallery opt-in separately from ordinary Debug x64.
5. Run the mutation and real-project matrices and existing fixture/package
   boundary suites.

## Task 4: Wire exact GGUF handoffs into schema-v2 projection (R3-004)

**Files:**

- Modify `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Infrastructure/ModelInspectionProjectionFactory.cs`.
- Modify the GGUF `ModelInspectionHandoffRegistry` implementation.
- Create `tests/UnitTests/GraniteEdgeAI.ModelInspection.Handoff.Tests/GraniteEdgeAI.ModelInspection.Handoff.Tests.csproj`.
- Create focused GGUF registry/projection behavior tests in that project.

1. Add headless behavior tests proving a real completed registry issuance does
   not expose an exact schema-v2 projection on the R2 baseline. Add stale,
   changed-source, mismatched-ID/digest/length/route/type/outcome, invalidation,
   and one-use tests.
2. Run the focused test and retain RED evidence.
3. Replace projection-time route reconstruction with
   `CreateGguf(ModelInspectionHandoff handoff)`. Canonically validate that exact
   already-issued handoff and create no new run or handoff ID.
4. Have `ModelInspectionHandoffRegistry.TryIssue` store the handoff and its
   projection together. Add a path-private lookup keyed by the exact handoff ID;
   invalidation retires both values. Preserve the single existing production
   registry registration.
5. Run the headless tests plus existing Model Inspection contract, worker,
   client, transport, process, and hardware-handoff tests.

## Task 5: Wire exact OpenVINO handoffs into schema-v2 projection (R3-004)

**Files:**

- Create `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Inspection/ModelInspectionProjectionFactory.OpenVino.cs`.
- Modify `OpenVinoRouteService`, `OpenVinoRouteHandoffLease`, and their tests.
- Modify the OpenVINO test project only to reference the shared Model Inspection
  contracts required by production sources.

1. Add route tests proving the R2 lease does not retain an exact schema-v2
   projection. Add mutations for run ID, handoff ID, digest, length, route,
   model type, and outcome, asserting rejection before descriptor consumption
   or worker session startup.
2. Run the focused tests and retain RED evidence.
3. Implement the partial route adapter
   `CreateOpenVino(ModelInspectionHandoffV2 handoff)` over the already-issued
   route handoff, with canonical validation and no regenerated identity.
4. Create the projection in `OpenVinoRouteService.InspectAsync`, retain it in
   the lease, and have `StartSessionAsync` validate exact projection/handoff
   equality before consuming the path-bearing descriptor. Preserve the single
   existing route composition.
5. Run focused OpenVINO route tests, cross-route semantic-equivalence tests,
   and the broader Model Inspection managed suites.

## Task 6: Verify, commit, and publish reproducible M1 evidence

**Files:**

- Update `docs/reviews/model-inspection-cleanup-source-files.txt` and
  `docs/reviews/model-inspection-cleanup-inventory.md` for every durable path.
- Create `docs/audits/2026-08-29/M1-model-inspection-remediation-r3.md`.
- Create `docs/audits/2026-08-29/evidence/M1-model-inspection-evidence-r3.json`.
- Atomically publish `C:\UCL-AUDIT-HANDOFFS\M1.json` after all validations.

1. Run focused R3 classes, the complete Model Inspection contract suite,
   transport, worker, worker-client, worker-process integration, deterministic
   runtime, OpenVINO managed route, relevant hardware-handoff/cross-feature,
   Debug x64/non-target fixture evaluation, privacy/package/duplication scans,
   Release builds, and `git diff --check`. Record discovered, executed, passed,
   failed, and skipped separately.
2. Commit implementation/tests first. Capture that commit and tree as the
   evidence subject. Confirm the worktree is clean and the subject descends
   from the approved base.
3. Check for fresh H1 handoff and phase receipts. If absent or invalid, do no
   native work and record `nativeDisposition: not-run` with the exact blocker.
   If valid, follow the lock and H1-to-M1 phase protocol, run bounded native
   checks, and publish the exact M1 native phase receipt.
4. Generate report and evidence manifest from committed results. Validate them
   against the evidence schema and the committed R3 validator, including
   arithmetic, hashes, byte counts, Git identities, ancestry, joins, privacy,
   and native disposition.
5. Commit report/manifest separately. Push only the M1 R3 branch, verify the
   remote ref equals the final commit, rerun validation against final Git blobs,
   and require a clean worktree.
6. Atomically create `C:\UCL-AUDIT-HANDOFFS\M1.json` only if absent; validate its
   schema and exact report/manifest hashes and byte counts. Report any external
   owner blockers without representing them as passing rows.
