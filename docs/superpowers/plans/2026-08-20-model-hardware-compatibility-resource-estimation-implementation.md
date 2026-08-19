# Decision 4 — Resource-Estimation Architecture Implementation Plan

> **For agentic workers:** Use `superpowers:subagent-driven-development` (recommended)
> or `superpowers:executing-plans`. Use TDD for every production behaviour and run the
> repository's live packaged WinUI verification before claiming completion.

- **Decision:** 4 of 8
- **Status:** Approved implementation plan
- **Plan date:** 2026-08-20
- **Scope:** Resource-estimation contracts, provider coverage, local GGUF helper, standard GGUF provider, project-owned extensions, calibration, CI and evidence
- **Spec:** `docs/superpowers/specs/2026-08-20-model-hardware-compatibility-resource-estimation-design.md`
- **Aggregate identity:** `ResourceEstimation / resource-estimation-v1`
- **Expected delivery:** Three short vertical slices plus controlled calibration runs

---

## 1. Goal

Implement one application-owned boundary that estimates the resource consumption of
one exact model/runtime/device configuration:

```text
IConfigurationResourceEstimator
        ↓
static provider planner
        ↓
complete component-level estimate
        ↓
Established / NotEstablished / Failed / Cancelled
```

The implementation must support a proven standard GGUF/llama.cpp route through a
small local-only helper, then add project-owned TurboQuant, OpenVINO, application /
runtime and storage evidence where their prerequisites are available.

This plan does not implement:

```text
Decision 5 arithmetic policy beyond already accepted calculators
Decision 6 OS allowance, safety reserve or fit thresholds
Decision 7 candidate ranking
Decision 8 candidate generation or support-matrix policy
Runtime Verification
WinUI fit-result cards
```

---

## 2. Non-negotiable constraints

- Reuse canonical Decisions 1–3 contracts; never recreate temporary handoffs, policy
  identities or `ContextTokenCount`.
- Estimate one exact configuration; never create or rank alternatives here.
- Keep all internal values as checked unsigned 64-bit bytes.
- Preserve component lifecycle phase and memory domain.
- Count shared/unified device allocations against physical system memory once.
- Require exactly one provider owner for every mandatory component slot.
- TurboQuant K/V replaces standard K/V; never add both.
- Missing evidence returns typed `NotEstablished`; unknown never means zero.
- No provider returns “safe,” “compatible,” “will run” or “maximum context.”
- No provider silently changes backend, device, cache type, offload or model format.
- Use a static provider allowlist; no assembly scanning or user-supplied plug-ins.
- The helper supports local GGUF only and opens no network path, server or port.
- Do not put the model path on the command line or in retained evidence.
- Bound stdin, stdout, stderr, execution time and process lifetime.
- Comments explain trust boundaries, invariants and non-obvious calculations rather
  than restating syntax.
- Every remote commit leaves all public request modes internally complete.

---

## 3. Entry gate

Create the implementation branch only after one accepted base contains the production
contracts required by the slice being implemented.

Record:

```text
repository and branch
base SHA
Decision 1 accepted SHA
Decision 2 accepted SHA
Decision 3 accepted SHA
ModelInspectionHandoff path and namespace
HardwareInspectionHandoff path and namespace
IInspectedModelArtifactAccessService path and namespace
live workflow SHA
reviewer and date
```

Before Task 1:

- [ ] Confirm one canonical definition of every reused type.
- [ ] Confirm the guarded artifact service supports `CompatibilityAnalysis`.
- [ ] Confirm the hardware handoff exposes stable topology/device evidence.
- [ ] Confirm the current test project and packaged runner pass on the exact base.
- [ ] Search for existing Decision 4 type names and reject duplicate ownership.
- [ ] Record a `Pass` or `Blocked` disposition.

A missing predecessor blocks only the affected implementation slice. It does not
invalidate this completed planning decision. Do not make a slice compile using
`object`, `dynamic`, dictionaries, raw JSON or duplicate DTOs.

Suggested branch:

```text
feature/model-hardware-compatibility-resource-estimation
```

---

## 4. Expected file map

Use actual namespaces established at the entry gate.

### Application contracts

```text
IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/
└── Application/ResourceEstimation/
    ├── IConfigurationResourceEstimator.cs
    ├── ConfigurationResourceEstimationRequest.cs
    ├── ConfigurationResourceEstimationResult.cs
    ├── ConfigurationResourceEstimate.cs
    ├── ResourceEstimationConfiguration.cs
    ├── LlamaCppResourceEstimationConfiguration.cs
    ├── OpenVinoGenAiResourceEstimationConfiguration.cs
    ├── ResourceRuntimeIdentity.cs
    ├── ResourceConfigurationFingerprint.cs
    ├── ResourceEstimateComponent.cs
    ├── ResourceEstimatorProviderIdentity.cs
    └── ResourceEstimationEnums.cs
```

### Provider planning

```text
Application/ResourceEstimation/Providers/
├── IResourceEstimatorProvider.cs
├── ResourceEstimatorProviderPlanner.cs
├── ResourceEstimatorCoveragePlan.cs
├── ResourceEstimatorProviderSupport.cs
└── ResourceEstimatorProviderResult.cs
```

### Standard GGUF helper and adapter

```text
tools/Granite.ResourceEstimator/
├── go.mod
├── go.sum
├── main.go
├── contract.go
├── estimate.go
├── contract_test.go
├── estimate_test.go
├── LICENSES/gguf-parser-go-MIT.txt
└── README.md

Infrastructure/ResourceEstimation/GgufParser/
├── GgufParserProcessAdapter.cs
├── GgufParserResourceEstimator.cs
├── GgufParserRequestDto.cs
├── GgufParserResponseDto.cs
└── GgufParserFailureMapper.cs
```

### Project-owned providers

```text
Application/ResourceEstimation/Providers/
├── TurboQuantKvResourceEstimator.cs
├── OpenVinoGenAiResourceEstimator.cs
├── ApplicationRuntimeFootprintProvider.cs
└── ConfigurationStorageEstimator.cs
```

### Tests and evidence

```text
tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/
├── ResourceEstimationContractTests.cs
├── ResourceConfigurationFingerprintTests.cs
├── ResourceEstimatorProviderPlannerTests.cs
├── ConfigurationResourceEstimatorTests.cs
├── GgufParserProcessAdapterTests.cs
├── GgufParserResourceEstimatorTests.cs
├── TurboQuantKvResourceEstimatorTests.cs
├── OpenVinoGenAiResourceEstimatorTests.cs
├── ResourceCalibrationTests.cs
└── ResourceEstimationPrivacyTests.cs

docs/architecture/decisions/ADR-Decision-4-Resource-Estimation.md
docs/evidence/requirements/F-M08/
IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/README.md
.github/workflows/build-and-test.yml
```

---

## 5. Task 1 — Bounded Windows helper spike

**Purpose:** Prove or reject the local helper before coupling production code to it.

### 5.1 Freeze the spike candidate

Start from the reviewed candidate:

```text
repository: gpustack/gguf-parser-go
commit: a5d9227ae50725494c827c7810c662e102143293
licence: MIT
upstream Go declaration: go 1.22.0 / toolchain go1.22.9
```

The spike may select a newer reviewed commit only through an explicit evidence record.
Record source archive SHA-256, dependency list, toolchain, licence notice and produced
binary SHA-256.

### 5.2 Write helper contract tests first

The project helper accepts only:

```text
granite-resource-estimator.exe --stdio
```

Request arrives as one bounded UTF-8 JSON document on stdin. Response is one bounded
UTF-8 JSON document on stdout.

Test before implementation:

```text
missing --stdio rejected
unknown command-line option rejected
valid local request accepted
URL/token/RPC fields rejected by schema
schema version mismatch rejected
path is never echoed
invalid UTF-8 rejected
oversized request rejected
malformed/truncated GGUF rejected
context/batch/cache/offload changes alter expected output fields
same request and file produce value-equivalent JSON
```

### 5.3 Minimum helper implementation

Use the pinned library directly rather than invoking the full upstream CLI.

Conceptual flow:

```go
// Read one bounded request from stdin.
request := readAndValidateRequest(os.Stdin)

// Open only the supplied local GGUF; remote library APIs are not called.
model, err := ggufparser.ParseGGUFFile(request.ModelPath, readOptions...)

// Map allowlisted configuration values to reviewed estimate options.
estimate := model.EstimateLLaMACppRun(options...)

// Emit only the project-owned schema.
writeResponse(os.Stdout, mapEstimate(estimate))
```

Do not import HTTP/RPC behaviour into the wrapper. `UseMMap()` is used only when the
request explicitly selects mmap.

### 5.4 Run the Windows spike matrix

Test on Windows x64 with:

```text
small bounded GGUF fixture
exact Granite fixture through guarded read-only access
CPU route
partial GPU offload
full/all-layer GPU offload
F16 and Q8_0 standard K/V where supported
mmap on/off
Flash Attention on/off
KV offload on/off
context and batch boundaries
malformed, truncated and adversarial count/overflow fixtures
timeout and cancellation
```

Match helper components against pinned llama.cpp allocation evidence for at least one
standard CPU and one admitted GPU route.

### 5.5 Spike disposition

Create:

```text
docs/evidence/requirements/F-M08/decision-4-gguf-helper-spike.md
```

Record exactly one:

```text
Pass
→ standard GGUF provider may use the helper

ConditionalPass
→ only the named component subset is admitted

Fail
→ retain the application contract and implement a narrow project-owned provider
```

No production provider is added before this disposition.

Suggested commit:

```text
test(spike): qualify local GGUF resource helper
```

---

## 6. Task 2 — Immutable contracts and configuration fingerprint

### 6.1 Write failing contract tests

Cover:

```text
all enums reserve zero for Unspecified
ResourceByteCount accepts 0..ulong.MaxValue and never represents unknown
llama.cpp counts must be positive
physical batch <= logical batch
CPU route rejects GPU offload
all-layer offload uses an explicit enum, not 999
TurboQuant mode and K/V intent are consistent
OpenVINO route rejects contradictory scheduler settings
request requires valid model/hardware handoffs and exactly one route configuration
result-envelope status/payload combinations are exhaustive
component requires kind, domain, phase, provider and evidence level
helper provider identity requires binary SHA-256
pure provider identity rejects an unnecessary binary digest
```

### 6.2 Implement the minimum immutable types

Use constructors or named factories that validate cross-property invariants before
assigning properties. Do not expose setters, mutable collections or parameterless
construction for trust-bearing values.

### 6.3 Fingerprint tests

Prove the fingerprint:

```text
is deterministic across repeated calls
is culture independent
changes when any memory-relevant setting changes
does not change with timestamp, free RAM, path, username or machine name
rejects non-canonical enum/contract versions
uses model content/package manifest identity rather than file location
```

Use a fixed canonical writer with explicit field order and UTF-8/binary encoding; never
hash `ToString()` or default JSON serializer output.

Suggested commit:

```text
test/feat: add resource-estimation contracts and fingerprint
```

---

## 7. Task 3 — Static provider planner and coverage enforcement

### 7.1 Write failing planning tests

Build route-specific required slots and prove:

```text
standard llama.cpp route assigns weights, K, V, compute, runtime, app and storage
TurboQuant route assigns standard weights/compute but replacement K/V
duplicate slot ownership is rejected
missing mandatory slot is rejected
inapplicable slot is absent rather than zero-filled
OpenVINO route never selects GGUF provider
GGUF route never selects OpenVINO provider
unsupported backend/device/cache route returns NotEstablished
provider order does not change the final plan
UI/provider-name input cannot select a provider
```

### 7.2 Implement the planner

Register providers explicitly in the composition root. `AssessSupport` is pure and must
not start a process or open a file.

Planner output:

```text
ResourceEstimatorCoveragePlan
├── RequiredSlots
└── Assignments
```

Every mandatory `(kind, domain, phase)` slot has exactly one owner before estimation
starts.

### 7.3 Shared/unified memory tests

Prove that:

```text
host + dedicated GPU remain separate pools
shared GPU allocation maps to system-memory pressure once
unified allocation maps to the unified physical pool once
unknown topology prevents complete coverage
```

Suggested commit:

```text
test/feat: enforce resource-provider coverage
```

---

## 8. Task 4 — Secure process adapter and standard GGUF provider

Task 4 begins only after Task 1 is `Pass` or after a `ConditionalPass` whose admitted
fields cover the intended provider slots.

### 8.1 Adapter tests first

Use a fake process launcher for deterministic tests. Cover:

```text
only fixed --stdio enters ArgumentList
model path appears in stdin JSON but not command line
spaces and Unicode paths work
stdin/stdout/stderr limits are enforced
schema/version mismatch fails
negative or overflowing numeric values fail checked parsing
unknown enum and duplicate component fail
non-zero exit is sanitised
timeout kills process tree
cancellation kills process tree
raw stdout/stderr never enters public diagnostic
```

### 8.2 Guarded artifact integration

Call the process inside one
`IInspectedModelArtifactAccessService.ExecuteWithValidatedAccessAsync` callback.

Flow:

```text
validate immutable artifact
→ protected open and full SHA-256 revalidation
→ invoke local helper while parent guard prevents replacement/deletion
→ parse bounded response
→ validate configuration echo/fingerprint/provider identity
→ post-operation continuity validation
→ retain result only after continuity passes
```

An artifact change maps to `NotEstablished / ModelArtifactChanged`; no component is
retained.

### 8.3 Map standard GGUF components

Map only reviewed helper fields into:

```text
ModelWeights
KvKeyCache
KvValueCache
ComputeAndScratch
separately identified RuntimeFootprint where proven
```

Do not include OS allowance or safety reserve. Do not estimate unsupported adapters,
projectors, draft models, RPC or multi-GPU splits.

### 8.4 Integration fixtures

Run the real helper against bounded fixtures and at least one controlled Granite GGUF.
Verify helper and model hashes before and after. Scan retained evidence for model/path
leakage.

Suggested commit:

```text
feat(resource-estimation): add secure standard GGUF provider
```

---

## 9. Task 5 — TurboQuant K/V replacement

Task 5 waits for accepted Decision 5 calculators for the admitted TurboQuant modes.
Decision 4 must not invent temporary bit-width arithmetic.

### 9.1 Write failing tests

For each admitted mode (`Turbo4`, `Turbo3`, `Turbo2`) prove:

```text
standard weights and compute remain unchanged
standard K/V publication is suppressed
TurboQuant provider owns exactly one K and one V slot
payload, metadata, padding and auxiliary categories are represented
context increase is monotonic where the accepted formula requires it
checked arithmetic rejects overflow
unsupported runtime/device/mode returns NotEstablished
unvalidated QJL or PolarQuant name is rejected
no silent fallback to Q8_0/F16/another TurboQuant mode
```

### 9.2 Implement only accepted formulas

Consume Decision 5 value/calculator objects. The provider translates exact
configuration/model facts and publishes replacement K/V components; it does not copy
the formula into a second location.

### 9.3 Calibration linkage

Attach matching controlled-workbook evidence by exact model hash, runtime identity,
context, device and cache mode. A measured ratio is never used as a universal formula.

Suggested commit:

```text
test/feat: add TurboQuant KV resource replacement
```

---

## 10. Task 6 — OpenVINO, application/runtime and storage providers

### 10.1 OpenVINO tests

Cover:

```text
trusted package identity required
arbitrary XML/BIN paths rejected
matching Granite 3B CPU profile admitted
matching tested GPU profile admitted with its evidence level
unmeasured K/V component prevents a complete estimate unless a structural calculator exists
8B/NPU/unvalidated route returns NotEstablished
file/folder size alone cannot establish peak memory
OpenVINO route cannot select GGUF provider
```

Implement only routes backed by accepted package identity and structural/profile
evidence. Keep unsupported routes explicit.

### 10.2 Application/runtime profile tests

Prove profile selection includes:

```text
application build identity
runtime version/commit
backend/device route
Windows architecture
relevant model scale/architecture class
```

No matching mandatory profile returns
`NotEstablished / CalibrationEvidenceUnavailable`; do not use a global constant.

### 10.3 Storage tests

Prove:

```text
existing imported bytes are exact
bundled helper/runtime bytes come from an immutable manifest
known converted/cache/temp bytes use matching profiles
unknown conversion/temp footprint returns NotEstablished
RAM/VRAM and disk are never summed as one memory pool
```

Suggested commit:

```text
feat(resource-estimation): add project-owned profile providers
```

---

## 11. Task 7 — Calibration, false-safe analysis, CI and evidence

### 11.1 Calibration contract tests

Prove:

```text
exact key match → CalibratedExact
route-class match → CalibratedRoute
no match → Structural or typed NotEstablished according to provider contract
signed error = measured - predicted
absolute error is non-negative
percentage error handles zero predicted value explicitly
underprediction is retained
failed/OOM/cancelled run cannot become a successful calibration point
component observations cannot claim values the measurement did not expose
```

Use `decimal` for percentage reporting and checked byte differences; do not truncate
large byte values through `int` or `double`.

### 11.2 Curate matched measurements

For every admitted route, freeze all memory-relevant settings:

```text
model SHA-256
runtime/build identity
backend/device
context
logical/physical batch
parallel sequences
K/V cache types
TurboQuant mode
offload
mmap
Flash Attention
KV offload
OpenVINO scheduler settings
```

Record predicted/measured peak, signed/absolute/percentage error, underprediction and
component evidence where observable.

Decision 6 later applies its margin and computes false-safe cases. Decision 4 retains
the raw facts and must not invent that margin.

### 11.3 Documentation

Update the feature README and add an ADR explaining:

```text
why option C was selected
why full GPUStack and the full upstream CLI were rejected
component/provider ownership
shared-memory normalisation
evidence grades
known route limitations
security boundary
supersession/rollback route
```

Create concise F-M08 evidence with exact SHAs, provider identities, test classes,
calibration artifacts and explicit non-claims. Do not mark F-M08 Verified before
Decisions 5–6 complete the full acceptance criteria.

### 11.4 CI discovery

Append all required Decision 4 test classes to the existing workflow's required-class
list without deleting prior gates. Add helper build/test/SBOM/licence/privacy checks
through the repository's established Windows workflow.

Suggested commit:

```text
docs(test): record Decision 4 resource-estimation evidence
```

---

## 12. Final verification

Run fresh verification on the exact head intended for review.

### Static checks

```powershell
git diff --check

git grep -n -E `
  "HttpClient|TcpListener|HttpListener|--url|--token|--rpc" -- `
  "tools/Granite.ResourceEstimator" `
  "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/ResourceEstimation"

git grep -n -E `
  "DateTimeOffset\.UtcNow|DateTime\.Now|Random\(" -- `
  "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation"
```

Review every match. Documentation may describe rejected choices; production code may
not expose them.

### Focused tests

Run each new class using a proven command and retain output showing non-zero discovery
and execution. A zero-test result is a failure.

### Authoritative packaged route

Use the live workflow for:

```text
fixture reproducibility
helper tests and reproducible build
licence/SBOM/binary hash checks
WinUI Release x64 restore/build
test-project restore/build
packaged app-container tests
TRX totals and required-class validation
privacy scan
artifact upload
```

### Independent artifact inspection

Download retained artifacts and verify:

```text
artifact and TRX SHA-256
all executed tests passed
all required Decision 4 classes present
helper source/binary/licence identities
no GGUF/model file
no absolute model path
no username/machine name
no credential/token
exact head SHA
```

### Manual diff review

Confirm:

```text
one public estimator
one static planner
one owner per slot
one deterministic configuration fingerprint
local-only helper
no full GPUStack
no hidden Decision 5 formula
no Decision 6 reserve or fit verdict
no Decision 7 ranking
no Decision 8 candidate ladder
no Runtime Verification claim
```

---

## 13. Pull-request requirements

Open one detailed implementation PR containing:

```text
summary and selected option C rationale
exact base/head SHAs
provider/component ownership table
helper source, licence, dependency and binary identities
security and privacy boundary
spike disposition
supported/structural/not-established matrix
test matrix
calibration/error summary
known limitations
explicit non-claims
rollback/supersession route
review checklist
```

State clearly:

```text
An estimate is not proof that the model will run.
Structural evidence is not measured evidence.
Decision 6 owns reserves and fit thresholds.
Runtime Verification owns actual load/inference proof.
```

Keep the PR draft until exact-head verification and independent artifact inspection
pass.

---

## 14. Acceptance checklist

- [ ] Canonical Decisions 1–3 contracts are reused.
- [ ] One exact configuration has one deterministic fingerprint.
- [ ] One application-owned estimator boundary exists.
- [ ] Provider selection is static and caller-independent.
- [ ] Every mandatory component slot has exactly one owner.
- [ ] Shared/unified memory is counted once.
- [ ] Components carry exact bytes, domain, phase, provider and evidence level.
- [ ] Helper is local-only, bounded, pinned and licence/SBOM identified.
- [ ] Model path never enters command line or retained evidence.
- [ ] Timeout/cancellation kills the helper process tree.
- [ ] Standard GGUF mapping is schema-validated.
- [ ] TurboQuant replaces standard K/V and uses accepted Decision 5 calculators.
- [ ] OpenVINO requires trusted package identity and honest route evidence.
- [ ] App/runtime and storage profiles are versioned.
- [ ] Missing evidence is typed `NotEstablished`.
- [ ] No silent configuration fallback exists.
- [ ] Matched signed/absolute/percentage error and underprediction are retained.
- [ ] Focused and packaged tests pass with non-zero class discovery.
- [ ] Retained evidence is independently verified.
- [ ] F-M08 is not prematurely marked Verified.

---

## 15. Stop conditions

Stop and report the exact issue when:

```text
canonical predecessor contracts are ambiguous or duplicated
the helper cannot produce stable bounded structured output
the helper exposes or invokes a remote/server path
the model path appears in command line or retained output
provider ownership is missing or duplicated
shared-memory topology is ambiguous
numeric conversion exceeds ulong
Decision 5 calculator is absent for a project-owned formula
OpenVINO package identity is insufficient
focused tests execute zero intended tests
packaged CI fails
a proposed change introduces safety, ranking or candidate policy
```

Do not repair a stop condition with:

```text
file-size multiplier
zero for unknown
global overhead constant
silent cache/device/backend fallback
duplicate model/hardware DTO
object/dynamic/dictionary placeholder
raw third-party JSON as an application result
```

---

## 16. Execution handoff

Recommended order:

```text
Task 1
→ review helper spike disposition

Tasks 2–4
→ deliver standard GGUF vertical slice

Decision 5 accepted calculators
→ enable Tasks 5–6 project-owned formula routes

Task 7
→ calibration, CI and evidence closure
```

Use `superpowers:subagent-driven-development` for fresh implementation and review
workers between tasks. Use `superpowers:verification-before-completion` before every
success claim and `superpowers:requesting-code-review` before marking the PR ready.
