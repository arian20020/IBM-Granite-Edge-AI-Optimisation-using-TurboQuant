# O1 OpenVINO C1 V2 Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing O1 OpenVINO adapter execute only authoritative C1 V2 OpenVINO payloads while preserving its proven transaction and result lifecycle.

**Architecture:** Keep capability projection and the sealed OpenVINO pipeline intact. Replace candidate-derived execution interpretation with one strict V2 adapter that validates and maps `plan.ExecutionPayload.OpenVino`; use public C1 issuance to recompute the configuration digest and `TrustedSourceContext` to release the planned model file immediately before reads/conversion. Extend schema-v2 provenance/profile output to bind the authoritative payload rather than define another plan schema.

**Tech Stack:** C# 12, .NET 8/10 SDK tooling, MSTest/Microsoft.Testing.Platform, WinUI Debug x64 build, Git/PowerShell.

**Spec:** `docs/superpowers/specs/2026-08-24-o1-openvino-c1-v2-migration-design.md`

## Global Constraints

- C1 V2 commit is exactly `892bc689627142e5ffbd0ef0c12d2c5e952bd5a2` and its owned files are immutable.
- Accept only `ContractVersion == 2`, `IsExecutableBy(2)`, route OpenVINO, and a single non-null OpenVINO payload.
- Never infer or substitute execution-affecting values; fail closed with bounded C1 results.
- Do not weaken Windows Application Control, stage, manifest, executable-digest, or packaging requirements.
- Preserve the honest TurboQuant optimization nonclaim and all existing rollback/cleanup behavior.
- No XAML, navigation, MainWindow, GGUF executor, shared protocol, I0, or main-branch changes.

---

### Task 1: Migrate fixtures and strict adaptation to C1 V2

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationPlanAdapter.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Optimization/OpenVinoOptimizationPlanAdapterTests.cs`
- Modify narrowly as needed: OpenVINO test-data helpers in existing optimization test files

**Interfaces:**
- Consumes: `OptimizationExecutionPlan.ExecutionPayload.OpenVino`
- Produces: `OpenVinoOptimizationPlanAdapter.Adapt(...)` returning one exact route-native candidate or typed `ReplanRequired`

- [ ] Add failing tests for V1/non-executable plans, missing/GGUF/mixed payloads, candidate/payload disagreement, and configuration-digest disagreement. Use reflection only to construct impossible malformed immutable C1 plans; ordinary fixtures must use the public V2 issuer.
- [ ] Run the focused adapter tests and record the expected compile/behavior failures caused by the V1 issuer signature and V1 interpretation.
- [ ] Require `plan.IsExecutableBy(2)`, `plan.ContractVersion == 2`, and exact OpenVINO union shape before reading route fields.
- [ ] Map all `OpenVinoExecutionPayload` fields directly: identifiers, source/target precision, KV cache, all compiled-cache flags, complete-package fact, build identity, optimizer versions, and TurboQuant absence.
- [ ] Remove configuration-ID lookup, Original-as-a-precision mapping, and other candidate-derived execution defaults. Derive runtime-only solely from equal source/target payload precision.
- [ ] Recompute `ConfigurationSha256` by reissuing the exact candidate and payload through public `OptimizationPlanIssuer.Issue`; pass the OpenVINO-irrelevant model-layer parameter without using it to interpret the payload.
- [ ] Verify current capability admission and build/tool identities agree exactly with the payload; reject any missing or extra experimental identity.
- [ ] Run focused adapter/projector tests, then commit `feat(openvino): consume C1 V2 execution payloads`.

### Task 2: Bind trusted source and V2 payload to execution/provenance

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationService.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationProvenance.cs`
- Modify narrowly if necessary: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/SealedOpenVinoOptimizationPipeline.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Optimization/OpenVinoOptimizationTests.cs`

**Interfaces:**
- Consumes: strict V2 adaptation, live current-state provider, `TrustedSourceContext`
- Produces: persistent schema-v2 provenance or runtime-only schema-v2 profile bound to the authoritative V2 plan/payload

- [ ] Add failing tests proving trusted-source failure happens before any pipeline/native call, verification occurs before initial model inspection and immediately before conversion/publication, and every payload field survives into the intended candidate/provenance boundary.
- [ ] Add failing tests proving equal source/target precision writes only a runtime profile and unequal precision cannot fall back to runtime-only storage.
- [ ] Run focused service/provenance tests and record RED.
- [ ] Create `TrustedSourceContext.ForPlan(plan, Path.Combine(sourceDirectory, "openvino_model.bin"))`; require `Verify(plan).IsVerified` and reveal only after verification before package inspection, conversion, and publication.
- [ ] Continue independent live plan/run/model/hardware/capability validation at `InitialPreflight`, `BeforeStaging`, and `BeforePublish`; recompute the V2 configuration digest at each adapter invocation.
- [ ] Extend O1 schema-v2 result evidence to bind contract version 2, plan ID, C1 `ConfigurationSha256`, and an exact serialization/digest of the authoritative OpenVINO payload. Remove durable fields that reinterpret the plan when they can instead be copied from the payload.
- [ ] Require actual completion optimizer versions, runtime device/cache, and persistence result to match payload values exactly; return typed replan/failure without paths or native output.
- [ ] Preserve atomic transaction, Original/runtime-profile isolation, rollback, cancellation, `ReinspectionFailed`, and cleanup behavior.
- [ ] Run focused and full OpenVINO component tests, then commit `feat(openvino): bind V2 plan to trusted execution`.

### Task 3: Migrate native E2E and complete source handoff

**Files:**
- Modify: `tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/OptimizationEndToEndTests.cs`
- Modify: `docs/handoffs/2026-08-24-o1-openvino-optimisation-adapter.md`
- Create: `docs/handoffs/2026-08-24-o1-openvino-c1-v2-source-handoff.md`

**Interfaces:**
- Consumes: V2 projector/issuer/adapter/service/provenance path
- Produces: final source-ready branch and exact native-environment nonclaims

- [ ] Add/update a converter-gated native E2E that issues a V2 OpenVINO payload and reaches `ExecuteAsync`, the sealed converter/official worker, and schema-v2 provenance; missing verified stages remain skips/failures, never passes.
- [ ] Add a static architecture test proving O1 production contains no duplicate execution payload/canonicalizer and the accepting path does not use legacy objective/configuration lookup.
- [ ] Run complete OpenVINO component, C1 V2 (at least 728), OpenVINO contract, worker-client, and integration suites; record exact totals, failures, and skips.
- [ ] Run the established two-phase Debug x64 build. Use packaging-disabled flags only because verified official-worker inputs are absent, and state that this is not Release packaging verification.
- [ ] Run `git diff --check`, conflict-marker/unmerged-path checks, C1 ancestry/tip checks, and a path-scope audit from V2 merge commit.
- [ ] Update both handoff documents with the V2 merge commit/parents, final seams, exact test evidence, trusted-source behavior, native blockers, and no-I0/no-main integration status.
- [ ] Commit `docs(openvino): hand off C1 V2 source integration`.
- [ ] After a clean final review and fresh verification, push `feature/openvino-optimisation-adapter-v1` to origin and verify the remote tip equals local HEAD.
