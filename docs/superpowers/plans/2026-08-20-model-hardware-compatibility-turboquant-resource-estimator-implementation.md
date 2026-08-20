# Decision 4 TurboQuant Resource-Estimation Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a project-owned TurboQuant KV-cache overlay that replaces the complete standard llama.cpp KV component set for explicitly supported `turbo4`, `turbo3`, and `turbo2` configurations, with exact Decision 5 calculators and matched calibration evidence.

**Architecture:** The standard GGUF provider continues to own model weights, placement, compute buffers, runtime footprint, and storage. `TurboQuantKvResourceEstimateOverlay` consumes the same exact model/configuration facts, suppresses standard K/V evidence, inserts the complete TurboQuant K/V allocation model, preserves all non-KV evidence, and returns typed `NotEstablished` rather than guessing unsupported QJL or nearby formats.

**Tech Stack:** C# 12, .NET 8, WinUI 3, MSTest, accepted Decision 5 byte calculators, pinned TurboQuant-enabled llama.cpp runtime, PowerShell measurement harness, packaged Windows test route.

**Spec:** `docs/superpowers/specs/2026-08-20-model-hardware-compatibility-resource-estimator-design.md`

## Global Constraints

- Execute only after the Decision 4 core contracts/router and the applicable Decision 5 TurboQuant calculators are accepted.
- Reuse the exact `ResourceEstimationConfiguration`, fingerprint, byte, component, evidence, and provider-identity types from the core plan.
- TurboQuant changes KV-cache evidence only; model weights and unrelated compute/storage evidence remain unchanged.
- Remove the complete standard KV component set before adding TurboQuant KV evidence.
- Never publish both standard and TurboQuant KV components for one configuration.
- Treat `turbo4`, `turbo3`, and `turbo2` as separate formats with separate identities and evidence.
- Do not call any route QJL, PolarQuant, or QJL+TurboQuant unless the pinned runtime and measurement evidence prove that exact algorithmic path.
- Nominal bits per value are not a complete allocation formula.
- Include payload, scales/codebooks, preconditioning/rotation data, alignment, padding, per-layer/head metadata, and auxiliary allocations required by the accepted implementation.
- All calculations use checked exact bytes; unknown never means zero.
- No format compression ratio from one case becomes a universal formula.
- Unsupported runtime/model/device/format returns typed `NotEstablished`; no fallback to F16, Q8_0, another TurboQuant format, or CPU.
- Preserve requested versus actual runtime route and reject silent fallback.
- Decision 6 owns safety margins; Decision 7 ranking; Decision 8 route admission; Runtime Verification actual execution proof.
- Do not mark F-M08 Verified from this overlay alone.

---

### Task 0: Freeze the admitted TurboQuant route and calculator identities

**Files:**
- Create: `docs/evidence/requirements/F-M08/decision-4-turboquant-entry-gate.md`
- Read: accepted Decision 5 TurboQuant formula specification/plan
- Read: pinned TurboQuant runtime manifest and controlled workbook evidence
- Read: Decision 4 core implementation/evidence

**Interfaces:**
- Consumes: accepted core estimator and exact Decision 5 calculators.
- Produces: an allowlisted route/format matrix and `Pass`/`Blocked` disposition.

- [ ] **Step 1: Record exact runtime identity**

Capture:

```text
repository/fork
commit SHA
build configuration
backend/device route
cache-format names exposed by the binary
proof that each requested format actually activated
```

Accepted display names are only those produced by the pinned runtime evidence.

- [ ] **Step 2: Record accepted calculator identities**

For each admitted format, record the exact Decision 5 calculator type and formula policy identity:

```text
Turbo4 → calculator/version
Turbo3 → calculator/version
Turbo2 → calculator/version
```

If a calculator is missing, that format is `Blocked`; do not add provisional arithmetic.

- [ ] **Step 3: Prove model facts are available**

Required facts include every value consumed by the accepted calculators, such as:

```text
layer/block count
K/V head count
head dimensions
recurrent/hybrid state where applicable
context
format block size
metadata/alignment parameters
```

- [ ] **Step 4: Write route matrix**

Use exact rows:

```text
model identity/class
runtime commit
backend/device route
format
context range
batch/offload constraints
calculator identity
calibration status
```

- [ ] **Step 5: Record `Pass` or `Blocked`**

A missing calculator or unverifiable activation blocks only the affected format.

- [ ] **Step 6: Commit gate evidence**

```powershell
git add docs/evidence/requirements/F-M08/decision-4-turboquant-entry-gate.md
git commit -m "docs(compatibility): record TurboQuant estimator entry gate"
```

---

### Task 1: Add typed TurboQuant configuration and provider identity

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/LlamaCppResourceEstimationConfiguration.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/ResourceEstimationEnums.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/TurboQuantCacheSelection.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers/TurboQuantKvResourceEstimateOverlay.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/TurboQuantConfigurationContractTests.cs`

**Interfaces:**
- Consumes: core llama.cpp configuration/fingerprint contracts.
- Produces: one closed TurboQuant selection and overlay identity.

- [ ] **Step 1: Write failing configuration tests**

Required enum:

```csharp
internal enum TurboQuantCacheFormat
{
    Unspecified = 0,
    None = 1,
    Turbo4 = 2,
    Turbo3 = 3,
    Turbo2 = 4,
}
```

Tests prove:

```text
standard route uses None
TurboQuant route requires one non-None format
format requires pinned TurboQuant runtime identity
K and V use the same accepted TurboQuant intent in version one
QJL/PolarQuant cannot be represented by arbitrary strings
format change changes configuration fingerprint
format does not change model-artifact identity
```

- [ ] **Step 2: Run tests red**

Expected: compile/test failure because the typed selection is absent.

- [ ] **Step 3: Implement immutable selection**

```csharp
internal sealed record TurboQuantCacheSelection
{
    public TurboQuantCacheSelection(
        TurboQuantCacheFormat format,
        CompatibilityPolicyIdentity calculationPolicyIdentity)
    {
        if (format is TurboQuantCacheFormat.Unspecified or TurboQuantCacheFormat.None)
        {
            throw new ArgumentOutOfRangeException(nameof(format));
        }

        Format = format;
        CalculationPolicyIdentity = calculationPolicyIdentity
            ?? throw new ArgumentNullException(nameof(calculationPolicyIdentity));
    }

    public TurboQuantCacheFormat Format { get; }
    public CompatibilityPolicyIdentity CalculationPolicyIdentity { get; }
}
```

Integrate as an optional, typed field in the llama.cpp configuration and canonical fingerprint writer.

- [ ] **Step 4: Run tests green and commit**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility" tests/UnitTests/GraniteEdgeAI.UnitTests
git commit -m "test/feat: add typed TurboQuant resource configuration"
```

---

### Task 2: Implement all-or-nothing KV component replacement

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers/TurboQuantKvResourceEstimateOverlay.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers/TurboQuantKvComponentSet.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/ResourceProfileValidator.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/TurboQuantKvOverlayTests.cs`

**Interfaces:**
- Consumes: one established standard GGUF profile and one typed TurboQuant selection.
- Produces: one complete profile with standard KV removed and TurboQuant KV inserted.

- [ ] **Step 1: Write failing replacement tests**

Cover:

```text
standard profile contains complete standard KV evidence
TurboQuant overlay removes every standard KV subcomponent
overlay adds exactly one complete logical KV component set
weights unchanged byte-for-byte
compute/runtime/app/storage unchanged byte-for-byte
component target and phase remain explicit
duplicate or incomplete KV input rejected
second overlay application rejected
unsupported format returns NotEstablished
```

Representative assertion:

```csharp
CollectionAssert.AreEqual(
    baseProfile.Components.Where(c => c.Kind != ResourceComponentKind.KvCache).ToArray(),
    overlaidProfile.Components.Where(c => c.Kind != ResourceComponentKind.KvCache).ToArray());
```

- [ ] **Step 2: Run tests red**

Expected: overlay behaviour absent.

- [ ] **Step 3: Implement atomic replacement**

The overlay first validates the base profile and computes a complete `TurboQuantKvComponentSet`. Only after successful computation does it return a new immutable profile. It never mutates the base profile or return a partial result.

- [ ] **Step 4: Add validator rules**

`ResourceProfileValidator` rejects:

```text
standard and TurboQuant KV coexistence
more than one TurboQuant KV owner
missing K or V storage category required by the calculator
format/provider identity mismatch
```

- [ ] **Step 5: Run tests green and commit**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility" tests/UnitTests/GraniteEdgeAI.UnitTests
git commit -m "test/feat: enforce atomic TurboQuant KV replacement"
```

---

### Task 3: Connect accepted Decision 5 calculators

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/TurboQuant/ITurboQuantKvByteCalculator.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/TurboQuant/TurboQuantKvCalculationRequest.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/TurboQuant/TurboQuantKvCalculationResult.cs`
- Create or reference accepted calculator implementations at the exact Decision 5 paths
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers/TurboQuantKvResourceEstimateOverlay.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/TurboQuantKvCalculationIntegrationTests.cs`

**Interfaces:**
- Consumes: accepted Decision 5 calculators.
- Produces: complete, explainable TurboQuant K/V bytes and assumption codes.

- [ ] **Step 1: Define the integration seam**

```csharp
internal interface ITurboQuantKvByteCalculator
{
    CompatibilityPolicyIdentity Identity { get; }

    TurboQuantKvCalculationResolution Calculate(
        TurboQuantKvCalculationRequest request);
}
```

The provider does not copy formula arithmetic.

- [ ] **Step 2: Write failing integration tests**

For each admitted format prove:

```text
calculator selected by exact format and policy identity
payload bytes represented
metadata bytes represented
alignment/padding bytes represented
auxiliary bytes represented where active
K/V totals equal checked sum of categories
larger context cannot reduce payload
integer overflow returns typed NumericRangeUnsupported
missing model fact returns NotEstablished
format cannot silently select another calculator
```

- [ ] **Step 3: Implement calculator selection**

Use an explicit switch/static map over `TurboQuantCacheFormat`; no reflection or string lookup.

- [ ] **Step 4: Map category evidence**

Preserve calculation categories as typed assumptions/sub-evidence rather than one unexplained scalar. `PredictedBytes` is the checked total.

- [ ] **Step 5: Run tests green and commit**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility" tests/UnitTests/GraniteEdgeAI.UnitTests
git commit -m "feat(compatibility): connect accepted TurboQuant KV calculators"
```

---

### Task 4: Prove route activation and reject runtime fallback

**Files:**
- Modify: `tools/ResourceMeasurementHarness/Measure-ResourceConfiguration.ps1`
- Create: `tools/ResourceMeasurementHarness/Parse-TurboQuantActivationEvidence.ps1`
- Create: `tools/ResourceMeasurementHarness/turboquant-activation.schema.json`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/TurboQuantActivationEvidenceTests.cs`

**Interfaces:**
- Consumes: pinned runtime output and requested configuration fingerprint.
- Produces: typed requested/actual activation evidence.

- [ ] **Step 1: Write failing activation-evidence tests**

Cover:

```text
requested format equals actual format
requested backend equals actual backend
requested device/offload equals actual route
missing activation evidence invalidates calibration
fallback to F16/Q8/another Turbo format invalidates case
legacy QJL flag without activated implementation is rejected
raw log/path is not retained in product profile
```

- [ ] **Step 2: Implement strict evidence parser**

Parse only stable, pinned-runtime evidence fields into a project-owned DTO. Unknown runtime version or changed evidence schema returns `NotEstablished`.

- [ ] **Step 3: Extend measurement harness**

Capture activation evidence before accepting memory measurements. Failed activation remains a failed case, never calibration.

- [ ] **Step 4: Run tests and commit**

```powershell
git add tools/ResourceMeasurementHarness tests/UnitTests/GraniteEdgeAI.UnitTests
git commit -m "test(compatibility): verify TurboQuant cache activation"
```

---

### Task 5: Calibrate every admitted format independently

**Files:**
- Create: `docs/evidence/requirements/F-M08/decision-4-turboquant-calibration.md`
- Create: `IBM Granite with TurboQuant (Intel)/Assets/ResourceEstimation/turboquant-calibration-profiles.v1.json`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Calibration/ResourceCalibrationMatcher.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/TurboQuantCalibrationTests.cs`

**Interfaces:**
- Consumes: successful matched standard/TurboQuant measurements.
- Produces: format-specific calibration profiles and error records.

- [ ] **Step 1: Freeze matched pairs**

Each pair uses the same:

```text
model SHA-256
runtime commit
backend/device
context
batch/micro-batch
parallel count
offload
mmap
Flash Attention
KV placement
prompt/workload
```

Only the intended KV format differs.

- [ ] **Step 2: Run minimum matrix**

For each format admitted by Decision 8:

```text
Granite 3B baseline context
Granite 3B one higher runnable context
Granite 8B only when the format/route is visible in version one
one cold + at least three repeats where practical
```

Do not infer missing format evidence from another format.

- [ ] **Step 3: Record error**

Store predicted/measured bytes, signed/absolute/percentage error, underprediction, activation evidence, sample count, maximum peak, median, and applicability.

- [ ] **Step 4: Write failing profile-matching tests**

Prove that changing format, context, runtime, model hash, backend, device, or offload prevents an exact match.

- [ ] **Step 5: Implement profile loading/matching**

Profiles are source-controlled, schema-validated, and privacy-safe. Invalid or overlapping applicability rules fail closed.

- [ ] **Step 6: Run tests and commit**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Assets/ResourceEstimation" "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility" tests/UnitTests/GraniteEdgeAI.UnitTests docs/evidence/requirements/F-M08
git commit -m "test(compatibility): calibrate TurboQuant resource estimates"
```

---

### Task 6: Document and verify the TurboQuant slice

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/README.md`
- Modify: `docs/architecture/decisions/ADR-Decision-4-Resource-Estimator.md`
- Modify: `.github/workflows/build-and-test.yml`
- Create: `docs/evidence/requirements/F-M08/decision-4-turboquant-closure.md`

**Interfaces:**
- Consumes: Tasks 0–5.
- Produces: exact-head, reviewable TurboQuant estimator evidence.

- [ ] **Step 1: Document supported and unavailable formats**

State exact runtime/format/model/device/context applicability and why QJL remains unavailable if not proven.

- [ ] **Step 2: Add required CI class discovery**

Append all TurboQuant Decision 4 tests without removing existing required classes.

- [ ] **Step 3: Run static checks**

Search production for:

```text
QJL string aliases
hard-coded universal compression ratios
standard and TurboQuant KV coexistence
silent F16/Q8 fallback
unchecked arithmetic
```

- [ ] **Step 4: Run focused and packaged verification**

Retain non-zero class execution, all-pass TRX, privacy scan, activation evidence, and exact head/helper/runtime hashes.

- [ ] **Step 5: Independently inspect artifacts**

Verify no model, path, prompt, raw log, credential, username, or machine identity is retained.

- [ ] **Step 6: Record closure and request review**

The closure states per-format calibration coverage, worst underprediction, unsupported routes, and Decision 6 dependencies. Do not claim safety or runtime proof.

```powershell
git add .
git commit -m "docs(test): close TurboQuant estimator evidence"
```

Use `superpowers:requesting-code-review` and `superpowers:verification-before-completion`.

---

## TurboQuant acceptance checklist

- [ ] Exact pinned runtime and activated format evidence exist.
- [ ] Only `turbo4`, `turbo3`, and `turbo2` can be represented in version one.
- [ ] QJL/PolarQuant are not inferred from names or flags.
- [ ] Standard KV is removed before TurboQuant KV is inserted.
- [ ] Non-KV components remain unchanged.
- [ ] Accepted Decision 5 calculators are the single arithmetic owner.
- [ ] Metadata, padding, alignment, and auxiliary storage are represented.
- [ ] Overflow and missing facts fail closed.
- [ ] Runtime fallback invalidates calibration.
- [ ] Every admitted format has independent matched evidence.
- [ ] Profiles are exact-route matched and privacy-safe.
- [ ] Focused and packaged tests pass with non-zero discovery.
- [ ] Safety, ranking, candidate generation, and runtime proof remain outside this slice.
