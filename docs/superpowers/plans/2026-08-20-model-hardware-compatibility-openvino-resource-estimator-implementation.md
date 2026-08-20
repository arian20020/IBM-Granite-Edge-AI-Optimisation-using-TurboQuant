# Decision 4 OpenVINO GenAI Resource-Estimator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a truthful OpenVINO GenAI resource-estimation provider for only the immutable model packages, runtime versions, devices, precisions, scheduler settings, and calibration routes explicitly admitted by Decision 8.

**Architecture:** `OpenVinoGenAiResourceEstimatorProvider` is a separate base provider; it never consumes GGUF estimates. It combines trusted package identity, inspected architecture/state facts, accepted Decision 5 structural calculators, exact package/storage measurements, and versioned route calibration. When any mandatory package, component, device-route, or profile evidence is missing, it returns typed `NotEstablished`.

**Tech Stack:** C# 12, .NET 8, WinUI 3, Windows App SDK, OpenVINO GenAI pinned runtime, MSTest, PowerShell measurement harness, immutable JSON profiles, packaged Windows test route.

**Spec:** `docs/superpowers/specs/2026-08-20-model-hardware-compatibility-resource-estimator-design.md`

## Global Constraints

- Execute as the second Decision 4 slice after the core contracts/router are accepted.
- Reuse the canonical Model Inspection OpenVINO package identity; never accept arbitrary XML/BIN paths.
- Reuse the canonical hardware topology and privacy-safe device-route identities.
- Reuse Decision 3 `ContextTokenCount` and Decision 4 fingerprint/component contracts.
- Use accepted Decision 5 calculators only; do not hide temporary formulas in the provider.
- Never use GGUF/llama.cpp estimates for OpenVINO IR.
- XML/BIN/package size alone is not peak-memory evidence.
- Every memory-relevant scheduler/cache setting is explicit and fingerprinted.
- A missing mandatory route profile/calculator/component returns typed `NotEstablished`; unknown never means zero.
- No silent CPU/GPU/NPU, precision, scheduler, batch, context, or model fallback.
- Preserve requested versus actual device/precision/runtime evidence.
- Separate package/storage bytes, structural allocations, calibration adjustments, and measured peaks.
- Decision 6 owns reserves and fit; Decision 7 ranking; Decision 8 support admission/candidates; Runtime Verification actual load/generation.
- OpenVINO may remain unavailable without weakening the complete GGUF/TurboQuant core.
- Do not mark F-M08 Verified from this provider alone.

---

### Task 0: Establish immutable OpenVINO package identity and admitted routes

**Files:**
- Create: `docs/evidence/requirements/F-M08/decision-4-openvino-entry-gate.md`
- Read: canonical Model Inspection OpenVINO package contract
- Read: accepted Decision 5 OpenVINO calculators/profile policy
- Read: pinned OpenVINO runtime manifest and controlled workbook evidence
- Read: Decision 8 supported-route matrix

**Interfaces:**
- Consumes: trusted package identity, runtime/device evidence, and accepted calculators.
- Produces: a closed supported/structural/unavailable route matrix.

- [ ] **Step 1: Record package-identity owner**

Confirm that one canonical package object identifies and hashes every required artifact:

```text
model XML
model BIN
generation/config JSON where required
tokenizer assets where runtime loading requires them
approved auxiliary files
package manifest/schema version
```

The provider must not discover sibling files by filename convention.

- [ ] **Step 2: Record exact runtime identities**

For each potential route capture:

```text
OpenVINO version
OpenVINO GenAI version
build/package identity
device string
driver version
weight precision
KV precision
scheduler contract version
```

- [ ] **Step 3: Record structural calculator/profile availability**

Required component families:

```text
weights/compiled model
KV cache
compute/work buffers
runtime/device bootstrap
application footprint
load/compile transient
storage existing/additional/temporary
```

- [ ] **Step 4: Freeze route matrix**

Use explicit rows such as:

```text
Granite 3B INT4 / CPU / F16 KV / 4K context
Granite 3B INT4 / CPU / U8 KV / 4K context
Granite 3B INT4 / GPU / admitted KV precision / 4K context
```

Only rows backed by current evidence are admitted. Do not add 8B or NPU unless package, runtime, measurement, and support evidence exist.

- [ ] **Step 5: Record `Pass`, `Partial`, or `Blocked`**

`Partial` lists exact routes/components admitted. An unverified route remains `NotEstablished`.

- [ ] **Step 6: Commit entry gate**

```powershell
git add docs/evidence/requirements/F-M08/decision-4-openvino-entry-gate.md
git commit -m "docs(compatibility): record OpenVINO estimator entry gate"
```

---

### Task 1: Add typed OpenVINO resource configuration

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/OpenVinoGenAiResourceEstimationConfiguration.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/OpenVinoSchedulerResourceSettings.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/ResourceEstimationEnums.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/ResourceConfigurationFingerprint.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/OpenVinoResourceConfigurationTests.cs`

**Interfaces:**
- Consumes: Decision 4 core configuration base and canonical package identity.
- Produces: one immutable OpenVINO route configuration and fingerprint fields.

- [ ] **Step 1: Write failing enum/configuration tests**

Required closed enums include:

```csharp
internal enum OpenVinoDeviceRoute
{
    Unspecified = 0,
    Cpu = 1,
    Gpu = 2,
    Npu = 3,
}

internal enum OpenVinoWeightPrecision
{
    Unspecified = 0,
    Fp16 = 1,
    Int8 = 2,
    Int4 = 3,
}

internal enum OpenVinoKvCachePrecision
{
    Unspecified = 0,
    Fp16 = 1,
    Int8 = 2,
    U8 = 3,
}
```

Use only values verified by the selected runtime contract. Adjust names at implementation time only through a reviewed spec amendment if the canonical runtime terminology differs.

- [ ] **Step 2: Define scheduler settings**

```csharp
internal sealed record OpenVinoSchedulerResourceSettings
{
    public OpenVinoSchedulerResourceSettings(
        ResourceByteCount? cacheSize,
        int? numKvBlocks,
        int? maxNumBatchedTokens,
        int maxNumSequences,
        bool prefixCachingEnabled,
        ContextTokenCount? maxPromptTokens)
    {
        // Validate the accepted runtime contract before assigning fields.
    }

    public ResourceByteCount? CacheSize { get; }
    public int? NumKvBlocks { get; }
    public int? MaxNumBatchedTokens { get; }
    public int MaxNumSequences { get; }
    public bool PrefixCachingEnabled { get; }
    public ContextTokenCount? MaxPromptTokens { get; }
}
```

Reject mutually exclusive cache-size/block settings according to the pinned runtime API, non-positive counts, and prompt limits above the selected context.

- [ ] **Step 3: Write fingerprint tests**

Changing any package manifest hash, runtime version, device, precision, context, sequence count, cache size/block count, prompt limit, batch token limit, or prefix-cache setting changes the fingerprint.

- [ ] **Step 4: Implement configuration and fingerprint mapping**

No device string or scheduler dictionary enters the fingerprint without typed normalisation.

- [ ] **Step 5: Run tests green and commit**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility" tests/UnitTests/GraniteEdgeAI.UnitTests
git commit -m "test/feat: add OpenVINO resource configuration contracts"
```

---

### Task 2: Add trusted package access and structural evidence contracts

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/OpenVino/OpenVinoStructuralResourceRequest.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/OpenVino/OpenVinoStructuralResourceResolution.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/OpenVino/IOpenVinoStructuralResourceCalculator.cs`
- Create or reference accepted Decision 5 calculator implementations
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/ResourceEstimation/OpenVino/OpenVinoPackageAccessAdapter.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/OpenVinoPackageAccessTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/OpenVinoStructuralCalculatorIntegrationTests.cs`

**Interfaces:**
- Consumes: canonical immutable package identity and accepted Decision 5 calculators.
- Produces: trusted exact package bytes and structural components.

- [ ] **Step 1: Write failing package-access tests**

Cover:

```text
all manifest files exist and hashes match
unexpected/missing required file rejected
arbitrary XML/BIN path rejected
symlink/reparse-point escape rejected according to canonical package policy
package mutation during calculation rejected
absolute path absent from returned evidence
existing package bytes measured exactly
```

- [ ] **Step 2: Integrate canonical package guard**

Use the Model Inspection package-access service. Do not create a second file-discovery or hashing layer.

- [ ] **Step 3: Define calculator seam**

```csharp
internal interface IOpenVinoStructuralResourceCalculator
{
    CompatibilityPolicyIdentity Identity { get; }

    OpenVinoStructuralResourceResolution Calculate(
        OpenVinoStructuralResourceRequest request);
}
```

- [ ] **Step 4: Write failing calculator-integration tests**

Cover:

```text
weight/package component produced
KV component produced by exact precision/context
compute/work component produced
load/compile transient represented or route NotEstablished
runtime/device footprint remains separate
checked overflow
missing architecture fact → NotEstablished
unsupported precision/device/runtime → NotEstablished
```

- [ ] **Step 5: Connect only accepted calculators**

Provider integration never duplicates formula arithmetic. Structural output includes typed source and assumptions.

- [ ] **Step 6: Run tests green and commit**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility" tests/UnitTests/GraniteEdgeAI.UnitTests
git commit -m "test/feat: add trusted OpenVINO structural resource evidence"
```

---

### Task 3: Implement the OpenVINO base provider and route gating

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers/OpenVinoGenAiResourceEstimatorProvider.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers/ResourceEstimatorProviderRouter.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/ResourceProfileValidator.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/OpenVinoResourceEstimatorProviderTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/OpenVinoProviderRoutingTests.cs`

**Interfaces:**
- Consumes: Tasks 1–2 and core runtime/storage provider families.
- Produces: complete OpenVINO structural profile or typed unavailable outcome.

- [ ] **Step 1: Write failing routing tests**

Prove:

```text
OpenVINO configuration selects OpenVINO provider only
GGUF configuration never selects OpenVINO provider
CPU/GPU/NPU route matching is exact
unsupported device/precision/runtime returns NotEstablished
caller cannot supply a provider name
no automatic CPU fallback from GPU/NPU
```

- [ ] **Step 2: Write failing profile tests**

Cover exact mandatory components, resource targets, lifecycle phases, provider identities, and overall weakest evidence grade.

- [ ] **Step 3: Implement provider**

Flow:

```text
validate exact package/runtime/configuration
→ guarded package access
→ accepted structural calculator
→ route-matched runtime/application profile
→ storage profile
→ calibration matcher
→ profile validator
→ Established or typed outcome
```

- [ ] **Step 4: Validate requested versus actual route evidence**

If controlled measurement/runtime evidence reports a different device or precision, it cannot be used as calibration for the requested route.

- [ ] **Step 5: Run tests green and commit**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility" tests/UnitTests/GraniteEdgeAI.UnitTests
git commit -m "feat(compatibility): add gated OpenVINO resource provider"
```

---

### Task 4: Create versioned OpenVINO runtime and calibration profiles

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Assets/ResourceEstimation/openvino-runtime-profiles.v1.json`
- Create: `IBM Granite with TurboQuant (Intel)/Assets/ResourceEstimation/openvino-calibration-profiles.v1.json`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/ResourceEstimation/Profiles/EmbeddedResourceProfileCatalogue.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Calibration/ResourceCalibrationMatcher.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/OpenVinoRuntimeProfileTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/OpenVinoCalibrationProfileTests.cs`

**Interfaces:**
- Consumes: route-specific measured evidence.
- Produces: exact or route-matched OpenVINO calibration and runtime footprint components.

- [ ] **Step 1: Write strict schema/profile tests**

Profiles require package/model applicability, runtime versions, device route, driver/environment constraints, precision, scheduler settings, component bytes/phases, evidence references, sample counts, and underprediction statistics.

- [ ] **Step 2: Reject broad or overlapping profiles**

No “all OpenVINO GPU” profile without explicit applicability. Duplicate/overlapping rules fail catalogue load.

- [ ] **Step 3: Implement deterministic selection**

Selection is exact-first, route-matched second, structural third where allowed; otherwise `NotEstablished`.

- [ ] **Step 4: Run tests green and commit**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Assets/ResourceEstimation" "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility" tests/UnitTests/GraniteEdgeAI.UnitTests
git commit -m "test/feat: add versioned OpenVINO resource profiles"
```

---

### Task 5: Run matched OpenVINO calibration

**Files:**
- Modify: `tools/ResourceMeasurementHarness/Measure-ResourceConfiguration.ps1`
- Create: `tools/ResourceMeasurementHarness/Parse-OpenVinoRouteEvidence.ps1`
- Create: `tools/ResourceMeasurementHarness/openvino-route.schema.json`
- Create: `docs/evidence/requirements/F-M08/decision-4-openvino-calibration.md`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/OpenVinoRouteEvidenceTests.cs`

**Interfaces:**
- Consumes: admitted route matrix and exact package/runtime identities.
- Produces: matched prediction/error evidence for every exposed OpenVINO route.

- [ ] **Step 1: Capture requested and actual route**

Evidence includes exact device, runtime/build, weight precision, KV precision, context, scheduler values, and fallback status.

- [ ] **Step 2: Freeze matched cases**

Use only Decision 8 admitted rows. Current evidence should begin with bounded Granite 3B CPU/GPU rows; 8B/NPU remains unavailable unless independently admitted.

- [ ] **Step 3: Run controlled measurements**

For each route:

```text
cold load/compile
prefill
decode
steady state
one cold + at least three repeats where practical
maximum peak retained
median stability retained
```

Keep process commit/private, working set, device memory, and runtime components separate.

- [ ] **Step 4: Record errors and component observability**

A component not measured separately remains structural/calibrated, never `MeasuredExact`.

- [ ] **Step 5: Validate no silent fallback**

Any device/precision/runtime mismatch invalidates the case.

- [ ] **Step 6: Run tests and commit evidence**

```powershell
git add tools/ResourceMeasurementHarness tests/UnitTests/GraniteEdgeAI.UnitTests docs/evidence/requirements/F-M08 "IBM Granite with TurboQuant (Intel)/Assets/ResourceEstimation"
git commit -m "test(compatibility): calibrate admitted OpenVINO resource routes"
```

---

### Task 6: Document, verify, and close the OpenVINO slice

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/README.md`
- Modify: `docs/architecture/decisions/ADR-Decision-4-Resource-Estimator.md`
- Modify: `.github/workflows/build-and-test.yml`
- Create: `docs/evidence/requirements/F-M08/decision-4-openvino-closure.md`

**Interfaces:**
- Consumes: Tasks 0–5.
- Produces: exact supported/unavailable matrix and review evidence.

- [ ] **Step 1: Document exact routes**

State model/package, runtime, device, precision, context, scheduler applicability, evidence grade, and known unavailable routes.

- [ ] **Step 2: Add CI required-class discovery**

Append every OpenVINO Decision 4 test class without removing prior gates.

- [ ] **Step 3: Run static checks**

Search for arbitrary XML/BIN paths, file-size multipliers, GGUF provider reuse, silent CPU fallback, untyped device strings, and unchecked arithmetic.

- [ ] **Step 4: Run focused and packaged verification**

Retain exact-head, non-zero class execution and all-pass TRX.

- [ ] **Step 5: Independently inspect artifacts**

No package/model files, absolute paths, usernames, machine names, prompts, credentials, raw runtime logs, or private native IDs may be retained.

- [ ] **Step 6: Record honest closure**

The closure names every established route and every `NotEstablished` route. It states that estimation is not Runtime Verification and that Decision 6 still owns fit safety.

- [ ] **Step 7: Commit and request review**

```powershell
git add .
git commit -m "docs(test): close OpenVINO estimator evidence"
```

Use `superpowers:requesting-code-review` and `superpowers:verification-before-completion`.

---

## OpenVINO acceptance checklist

- [ ] Canonical immutable package identity is reused.
- [ ] No arbitrary XML/BIN path or sibling discovery exists.
- [ ] Every scheduler/cache setting that affects memory is typed and fingerprinted.
- [ ] GGUF estimation is never reused.
- [ ] Exact route gating prevents CPU/GPU/NPU or precision fallback.
- [ ] Structural calculations come from accepted Decision 5 calculators.
- [ ] File size alone is not peak memory.
- [ ] Runtime/application/storage components are complete or typed unavailable.
- [ ] Profiles are versioned, non-overlapping, and exact-route matched.
- [ ] Requested/actual route evidence is retained.
- [ ] Every exposed route has matched calibration or the permitted structural grade.
- [ ] Unmeasured component values are never labelled `MeasuredExact`.
- [ ] 8B/NPU remain unavailable unless separately admitted.
- [ ] Focused and packaged tests pass with non-zero discovery.
- [ ] Privacy and evidence boundaries pass.
- [ ] Safety, ranking, candidate generation, and runtime proof remain outside this slice.
