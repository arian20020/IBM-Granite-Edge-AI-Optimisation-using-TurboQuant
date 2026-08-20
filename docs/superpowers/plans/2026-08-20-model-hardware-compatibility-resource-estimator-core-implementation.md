# Decision 4 Core Resource Estimator and Standard GGUF Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the application-owned resource-estimation contracts, provider router, bounded Windows GGUF helper, standard llama.cpp resource provider, runtime/storage profiles, calibration harness, CI discovery, and evidence for the first supported GGUF route.

**Architecture:** `ConfigurationResourceEstimator` receives one immutable trusted model handoff, one immutable resource-topology projection, and one exact llama.cpp configuration. It selects only allowlisted providers, invokes a project-owned local helper through the canonical protected process foundation, normalises component evidence into application-owned contracts, and returns `Established`, `NotEstablished`, `OperationalFailure`, or `Cancelled`. It estimates resource demand only; Decisions 5–8 own formulas, safety, ranking, and candidate generation.

**Tech Stack:** C# 12, .NET 8, WinUI 3, Windows App SDK, MSTest, PowerShell, Go 1.22.x for the helper spike, pinned `gguf-parser-go`, SHA-256, Windows Job Objects through the repository's canonical process-execution package, packaged `.build.appxrecipe` verification.

**Spec:** `docs/superpowers/specs/2026-08-20-model-hardware-compatibility-resource-estimator-design.md`

## Global Constraints

- Reuse the canonical Decision 1 `ModelInspectionHandoff` and guarded artifact-access service.
- Reuse the canonical Decision 2 policy-identity/version types; do not add a second generic policy-version model.
- Reuse Decision 3 `ContextTokenCount`; it means the total runtime context window.
- Consume an immutable Compatibility-owned projection of `HardwareSnapshot`; do not consume LLM Fit, DXGI, WMI, native handles, pages, or ViewModels.
- Do not freeze current available RAM/VRAM in an estimator request.
- Estimate exactly one configuration; never create, rank, or silently substitute alternatives.
- Use exact bytes in checked `ulong` values; zero is a known value and never means unknown.
- Preserve component target, lifecycle phase, source, evidence grade, assumptions, and calibration reference.
- Shared GPU memory is system-backed and must be counted once.
- Missing mandatory evidence returns typed `NotEstablished`; no file-size heuristic or nearby-route fallback is allowed.
- No provider returns `safe`, `compatible`, `will run`, or `maximum context`.
- Reuse the canonical shared external-tool/process foundation for executable verification, bounded pipes, Job Object containment, timeout, cancellation, and process-tree cleanup.
- The helper supports one locked local single-file GGUF in version one; split GGUF is not admitted.
- The model path never appears in command-line arguments, public diagnostics, fingerprints, or retained evidence.
- The helper opens no server or port and performs no runtime download.
- Comments explain trust boundaries and invariants, not obvious syntax.
- Every reviewable commit leaves all accepted request paths internally complete.
- Do not mark `F-M08` Verified from this slice alone; Decisions 5–6 and matched calibration evidence are still required.

---

## Delivery slices

This plan produces three independently reviewable deliveries:

```text
A. Windows helper spike
   → Pass / Partial / Fail disposition

B. Pure application contracts and routing
   → testable without external process execution

C. Standard GGUF vertical slice
   → protected helper integration, profiles, calibration, CI, evidence
```

A failed helper spike does not invalidate the application contracts. It selects the narrow app-owned fallback described in the specification.

---

### Task 0: Freeze the implementation base and prove prerequisites

**Files:**
- Create: `docs/evidence/requirements/F-M08/decision-4-core-entry-gate.md`
- Read: `docs/superpowers/specs/2026-08-20-model-hardware-compatibility-resource-estimator-design.md`
- Read: `.github/workflows/build-and-test.yml`
- Read: canonical Decision 1–3 design/plan files at their accepted paths
- Read: canonical shared `ExternalTools` and `ProcessExecution` packages on the selected base

**Interfaces:**
- Consumes: accepted Decision 1–3 contracts and shared process foundations.
- Produces: an immutable entry-gate record that authorises or blocks Tasks 1–7.

- [ ] **Step 1: Create an isolated worktree**

```powershell
git fetch origin
$BaseSha = git rev-parse origin/main
git worktree add ..\decision4-core -b feature/model-hardware-compatibility-resource-estimator-core $BaseSha
Set-Location ..\decision4-core
git status --short
```

Expected: clean output after `git status --short`.

- [ ] **Step 2: Record exact owners instead of guessing namespaces**

Run:

```powershell
git grep -n -E "class ModelInspectionHandoff|record ModelInspectionHandoff"
git grep -n -E "IInspectedModelArtifactAccessService|ExecuteWithValidatedAccessAsync"
git grep -n -E "class ContextTokenCount|record ContextTokenCount|struct ContextTokenCount"
git grep -n -E "class CompatibilityPolicyIdentity|record CompatibilityPolicyIdentity"
git grep -n -E "HardwareSnapshot|HardwareInspectionHandoff"
git grep -n -E "ExternalTools|ProcessExecution|JobObject|ProcessRunner"
```

Expected: one canonical owner per reused contract. Multiple or missing owners are a `Blocked` disposition for the affected slice.

- [ ] **Step 3: Confirm the artifact access purpose**

Verify that the guarded artifact service has an accepted purpose equivalent to:

```text
CompatibilityResourceEstimation
```

Do not reuse a purpose intended only for diagnostics or runtime execution. If no accepted purpose exists, amend Decision 1 first rather than bypassing the guard.

- [ ] **Step 4: Confirm the topology projection source**

Record the exact canonical fields that allow the application to distinguish:

```text
system RAM
dedicated GPU memory
shared/UMA GPU memory
target runtime/device route
```

Expected: stable immutable facts, not current free-memory readings.

- [ ] **Step 5: Run the exact baseline route**

Follow the live workflow rather than copying an old command. At minimum retain evidence for:

```text
fixture reproducibility
WinUI Release x64 restore/build
test-project restore/build
packaged app-container test execution
non-zero TRX discovery
all executed tests passed
```

- [ ] **Step 6: Write the entry-gate disposition**

Use exactly one:

```text
Pass
→ Tasks 1–7 may proceed

Blocked
→ name each missing canonical owner or failed verification;
  do not create temporary DTOs or process infrastructure
```

- [ ] **Step 7: Commit the gate evidence**

```powershell
git add docs/evidence/requirements/F-M08/decision-4-core-entry-gate.md
git commit -m "docs(compatibility): record Decision 4 core entry gate"
```

---

### Task 1: Qualify the bounded Windows GGUF helper

**Files:**
- Create: `tools/Granite.ResourceEstimator/go.mod`
- Create: `tools/Granite.ResourceEstimator/go.sum`
- Create: `tools/Granite.ResourceEstimator/main.go`
- Create: `tools/Granite.ResourceEstimator/protocol.go`
- Create: `tools/Granite.ResourceEstimator/estimate.go`
- Create: `tools/Granite.ResourceEstimator/protocol_test.go`
- Create: `tools/Granite.ResourceEstimator/estimate_test.go`
- Create: `tools/Granite.ResourceEstimator/README.md`
- Create: `tools/Granite.ResourceEstimator/THIRD-PARTY-NOTICES.txt`
- Create: `docs/evidence/requirements/F-M08/decision-4-gguf-helper-spike.md`
- Create: `tests/TestFixtures/ResourceEstimator/Generate-ResourceEstimatorFixtures.ps1`
- Create: `tests/TestFixtures/ResourceEstimator/resource-estimator-fixture-manifest.json`

**Interfaces:**
- Consumes: pinned spike candidate `gpustack/gguf-parser-go@a5d9227ae50725494c827c7810c662e102143293`.
- Produces: `granite-resource-estimator.exe --stdio`, protocol `granite-resource-estimator/1`, and a `Pass`, `Partial`, or `Fail` disposition.
- Does not produce an application provider yet.

#### Protocol values

```go
const (
    ProtocolVersion  = "granite-resource-estimator/1"
    MaxRequestBytes  = 64 * 1024
    MaxResponseBytes = 1024 * 1024
    MaxStderrBytes   = 64 * 1024
)
```

Request shape:

```go
type EstimateRequest struct {
    ProtocolVersion          string `json:"protocolVersion"`
    ConfigurationFingerprint string `json:"configurationFingerprint"`
    ModelPath                string `json:"modelPath"`
    ExpectedFileSizeBytes    uint64 `json:"expectedFileSizeBytes"`
    ContextTokens            int32  `json:"contextTokens"`
    LogicalBatchSize         int32  `json:"logicalBatchSize"`
    PhysicalBatchSize        int32  `json:"physicalBatchSize"`
    ParallelSequences        int32  `json:"parallelSequences"`
    KeyCacheType             string `json:"keyCacheType"`
    ValueCacheType           string `json:"valueCacheType"`
    OffloadMode              string `json:"offloadMode"`
    OffloadedLayerCount      uint64 `json:"offloadedLayerCount,omitempty"`
    MemoryMapEnabled         bool   `json:"memoryMapEnabled"`
    FlashAttentionEnabled    bool   `json:"flashAttentionEnabled"`
    KvCacheOffloadEnabled    bool   `json:"kvCacheOffloadEnabled"`
}
```

Response shape:

```go
type EstimateResponse struct {
    ProtocolVersion          string            `json:"protocolVersion"`
    ConfigurationFingerprint string            `json:"configurationFingerprint"`
    UpstreamCommit           string            `json:"upstreamCommit"`
    ObservedFileSizeBytes    uint64            `json:"observedFileSizeBytes"`
    Architecture             string            `json:"architecture"`
    ContextTokens            uint64            `json:"contextTokens"`
    LogicalBatchSize         int32             `json:"logicalBatchSize"`
    PhysicalBatchSize        int32             `json:"physicalBatchSize"`
    OffloadedLayers          uint64            `json:"offloadedLayers"`
    FullOffload              bool              `json:"fullOffload"`
    Devices                  []DeviceEstimate  `json:"devices"`
}
```

`DeviceEstimate` contains only position, placement class, footprint, weight, K/V, and computation byte fields. It contains no path, endpoint, username, hostname, prompt, token, or arbitrary upstream object.

- [ ] **Step 1: Pin the candidate dependency**

Create `go.mod` with:

```go
module granite.local/resource-estimator

go 1.22.0

require github.com/gpustack/gguf-parser-go v0.0.0-20260819053922-a5d9227ae507
```

Then run:

```powershell
Set-Location tools/Granite.ResourceEstimator
go mod tidy
go mod verify
go list -m -json all | Out-File -Encoding utf8 dependency-graph.json
```

Expected: module verification succeeds and the resolved upstream commit is `a5d9227...`.

- [ ] **Step 2: Write failing protocol tests**

Add tests for:

```text
exactly one --stdio argument is accepted
unknown or missing argument is rejected
one bounded UTF-8 JSON request is accepted
unknown JSON properties are rejected
URL/token/RPC fields are rejected
schema mismatch is rejected
zero/negative counts are rejected
physical batch > logical batch is rejected
partial offload requires a positive explicit count
non-partial offload rejects a count
path is never emitted in response or stderr
oversized input is rejected before allocation
```

Representative test:

```go
func TestDecodeRequestRejectsUnknownField(t *testing.T) {
    input := []byte(`{"protocolVersion":"granite-resource-estimator/1","modelPath":"x.gguf","url":"https://example.invalid"}`)
    _, err := decodeRequest(bytes.NewReader(input))
    require.ErrorContains(t, err, "unknown field")
}
```

- [ ] **Step 3: Run the protocol tests red**

```powershell
go test ./... -run "TestDecodeRequest|TestValidateArguments" -count=1
```

Expected: FAIL because decoder/validator functions do not exist.

- [ ] **Step 4: Implement strict bounded protocol parsing**

Use `io.LimitReader`, `json.Decoder.DisallowUnknownFields`, one JSON value plus EOF, explicit enum allowlists, and checked range validation. Do not log the request.

- [ ] **Step 5: Run the protocol tests green**

```powershell
go test ./... -run "TestDecodeRequest|TestValidateArguments" -count=1
```

Expected: PASS with non-zero tests.

- [ ] **Step 6: Write failing estimate-mapping tests**

Cover:

```text
F16 and Q8_0 K/V map to reviewed upstream enum values
unsupported K/V type is rejected
CPU-only, partial and all-layer placement map explicitly
context/batch/FA/mmap/KV-offload enter upstream options
platform footprint is zeroed or emitted separately
upstream remote/RPC/draft/adapter/projector fields never enter project response
same input and fixture returns value-equivalent response
```

- [ ] **Step 7: Implement minimal local-only estimation**

Use only:

```go
model, err := ggufparser.ParseGGUFFile(request.ModelPath, options...)
estimate := model.EstimateLLaMACppRun(mappedOptions...)
```

Do not call any remote, cache, URL, token, Hugging Face, ModelScope, Ollama, RPC, draft, adapter, or projector API. Recover an upstream panic at the process boundary and emit a stable generic failure without raw path or parser internals.

- [ ] **Step 8: Run all helper unit tests**

```powershell
go test ./... -count=1
```

Expected: PASS.

- [ ] **Step 9: Generate adversarial fixtures**

The fixture generator creates deterministic minimal files for:

```text
valid tiny GGUF
bad magic
unsupported version
truncated metadata
oversized metadata count
oversized tensor count
invalid tensor dimensions
integer-overflow dimensions
split-shard-style filename beside an unauthorised shard
```

Record SHA-256 for every generated fixture in the manifest.

- [ ] **Step 10: Build reproducibly for Windows x64**

```powershell
$env:CGO_ENABLED = '0'
$env:GOOS = 'windows'
$env:GOARCH = 'amd64'
$env:SOURCE_DATE_EPOCH = '0'
go build -trimpath -buildvcs=false -ldflags="-s -w" -o artifacts/granite-resource-estimator.exe .
Get-FileHash artifacts/granite-resource-estimator.exe -Algorithm SHA256
```

Repeat in a clean checkout with the same pinned toolchain. Expected: identical digest or a documented `Fail`/`Partial` disposition.

- [ ] **Step 11: Run the Windows behavioural matrix**

Run the helper through the canonical protected process runner for:

```text
tiny fixture and exact Granite fixture
CPU-only
partial GPU offload
full GPU offload
F16 and Q8_0 K/V
two contexts
two batch combinations
mmap on/off
Flash Attention on/off
KV offload on/off
timeout/cancellation
oversized output simulation
child-process attempt
adjacent-shard mutation attempt
```

The model path must be in private stdin only.

- [ ] **Step 12: Compare with pinned llama.cpp evidence**

For at least one CPU case and one admitted GPU/offload case, record:

```text
helper weights/K/V/compute/footprint
llama.cpp runtime-reported allocations
matched configuration fingerprint
signed and absolute differences
whether any component is missing or semantically incomparable
```

Do not compare working set to a component allocation as though they are the same metric.

- [ ] **Step 13: Record the spike disposition**

Use exactly one:

```text
Pass
→ helper may own every proven standard-GGUF component

Partial
→ list each admitted component and every excluded component

Fail
→ no production helper provider; retain application contracts and implement the
  narrow app-owned fallback in a separately reviewed plan
```

- [ ] **Step 14: Commit the spike**

```powershell
git add tools/Granite.ResourceEstimator tests/TestFixtures/ResourceEstimator docs/evidence/requirements/F-M08/decision-4-gguf-helper-spike.md
git commit -m "test(spike): qualify bounded GGUF resource helper"
```

---

### Task 2: Add immutable application contracts and deterministic fingerprinting

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/IConfigurationResourceEstimator.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/ConfigurationResourceEstimationRequest.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/ResourceEstimationConfiguration.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/LlamaCppResourceEstimationConfiguration.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/ResourceTopologyProjection.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/ResourceConfigurationFingerprint.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/ResourceByteCount.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/ConfigurationResourceProfileResolution.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/ConfigurationResourceProfile.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/ResourceComponentEstimate.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/ResourceEstimationEnums.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/ResourceEstimationContractTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/ResourceConfigurationFingerprintTests.cs`

**Interfaces:**
- Consumes: canonical `ModelInspectionHandoff`, `ContextTokenCount`, `CompatibilityPolicyIdentity`, and accepted topology facts.
- Produces: all application-owned types used by Tasks 3–7 and the extension plans.

- [ ] **Step 1: Write failing enum/value tests**

Required enum values:

```csharp
internal enum ResourceProfileResolutionStatus
{
    Unspecified = 0,
    Established = 1,
    NotEstablished = 2,
    OperationalFailure = 3,
    Cancelled = 4,
}

internal enum ResourceEvidenceGrade
{
    Unspecified = 0,
    StructuralEstimate = 1,
    CalibratedMatched = 2,
    MeasuredExact = 3,
}

internal enum ResourceComponentKind
{
    Unspecified = 0,
    ModelWeights = 1,
    KvCache = 2,
    ComputeAndScratch = 3,
    RuntimeBootstrap = 4,
    ApplicationFootprint = 5,
    LoadOrCompileTransient = 6,
    StorageExistingArtifact = 7,
    StorageAdditionalArtifact = 8,
    StorageTemporary = 9,
}
```

Also define explicit target, lifecycle, route, K/V, placement, failure, assumption, and warning enums with zero reserved for `Unspecified`.

- [ ] **Step 2: Run the contract tests red**

Run the exact focused command proven by the entry gate. Expected: non-zero discovery and compile/test failure because the types are absent.

- [ ] **Step 3: Implement `ResourceByteCount`**

```csharp
internal readonly record struct ResourceByteCount
{
    public ResourceByteCount(ulong value) => Value = value;

    public ulong Value { get; }
}
```

Unknown is never encoded in this type.

- [ ] **Step 4: Implement discriminated configuration values**

Use a sealed/abstract hierarchy rather than nullable unrelated fields:

```csharp
internal abstract record ResourceEstimationConfiguration(
    ResourceRuntimeRouteIdentity Runtime,
    ContextTokenCount ContextTokens);

internal sealed record LlamaCppResourceEstimationConfiguration(
    ResourceRuntimeRouteIdentity Runtime,
    ContextTokenCount ContextTokens,
    int LogicalBatchSize,
    int PhysicalBatchSize,
    int ParallelSequenceCount,
    LlamaCppKvCacheType KeyCacheType,
    LlamaCppKvCacheType ValueCacheType,
    LlamaCppOffloadPlan Offload,
    bool MemoryMapEnabled,
    bool FlashAttentionEnabled,
    bool KvCacheOffloadEnabled)
    : ResourceEstimationConfiguration(Runtime, ContextTokens);
```

Constructors reject non-positive counts, `PhysicalBatchSize > LogicalBatchSize`, CPU routes with GPU offload, and invalid offload-mode/count combinations.

- [ ] **Step 5: Implement topology projection**

`ResourceTopologyProjection` contains immutable logical route/pool relationships only. It rejects duplicate route IDs, duplicate pool IDs, raw PnP/serial/machine identifiers, and ambiguous shared/dedicated mapping.

- [ ] **Step 6: Implement result/profile invariants**

Factories:

```csharp
ConfigurationResourceProfileResolution.Established(profile)
ConfigurationResourceProfileResolution.NotEstablished(reason)
ConfigurationResourceProfileResolution.OperationalFailure(reason, diagnostic)
ConfigurationResourceProfileResolution.Cancelled()
```

`Established` requires all mandatory components and no duplicate `(kind,target,phase,source-slot)` entries.

- [ ] **Step 7: Write failing fingerprint tests**

Prove:

```text
same material input → same SHA-256
culture change → no change
path/timestamp/current-free-memory change → no change
context/batch/KV/offload/mmap/FA/runtime/helper/provider change → different hash
field ordering is fixed
model content hash, not path, is included
```

- [ ] **Step 8: Implement canonical fingerprint writer**

Use `IncrementalHash` with explicit field tags, fixed integer endianness, UTF-8 length prefixes, and enum numeric values. Never hash `ToString()`, reflection order, or default JSON serialisation.

- [ ] **Step 9: Run focused tests green**

Expected: all contract and fingerprint tests pass with non-zero discovery.

- [ ] **Step 10: Commit contracts**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation" tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility
git commit -m "test/feat: add resource-estimation contracts and fingerprint"
```

---

### Task 3: Add deterministic provider routing, overlays, and profile validation

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers/IBaseResourceEstimatorProvider.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers/IResourceEstimateOverlay.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers/IRuntimeFootprintProvider.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers/IStorageFootprintProvider.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers/ResourceEstimatorProviderRouter.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/ResourceProfileValidator.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/ConfigurationResourceEstimator.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/ResourceEstimatorProviderRouterTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/ResourceProfileValidatorTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/ConfigurationResourceEstimatorTests.cs`

**Interfaces:**
- Consumes: Task 2 contracts.
- Produces: one deterministic application service with explicit base/overlay/runtime/storage composition.

- [ ] **Step 1: Write failing routing tests**

Cover:

```text
standard GGUF + pinned llama.cpp → standard GGUF base provider only
TurboQuant route → same base + exactly one approved KV overlay
OpenVINO route → no GGUF provider
unsupported route → NotEstablished / UnsupportedEstimatorRoute
provider registration order → no behavioural change
caller/UI cannot choose provider identity
two base providers claiming one route → contract failure
two overlays claiming one slot → NotEstablished
```

- [ ] **Step 2: Define small internal interfaces**

```csharp
internal interface IBaseResourceEstimatorProvider
{
    ResourceProviderImplementationIdentity Identity { get; }
    bool Supports(ResourceEstimationConfiguration configuration);
    Task<ResourceProviderResolution> EstimateAsync(
        ConfigurationResourceEstimationRequest request,
        CancellationToken cancellationToken);
}

internal interface IResourceEstimateOverlay
{
    ResourceProviderImplementationIdentity Identity { get; }
    bool AppliesTo(ResourceEstimationConfiguration configuration);
    ResourceProviderResolution Apply(
        ResourceProviderResolution baseResolution,
        ConfigurationResourceEstimationRequest request);
}
```

Runtime/storage interfaces return only their owned component families.

- [ ] **Step 3: Implement closed router**

The router receives explicit concrete providers in the composition root. It performs exact route matching and returns typed unsupported/ambiguous outcomes. No assembly scanning or plugin folder is allowed.

- [ ] **Step 4: Write failing profile-validation tests**

Cover:

```text
all mandatory component families present
missing family → NotEstablished
duplicate component owner → NotEstablished
shared GPU component mapped to system-backed target
ambiguous target → NotEstablished
weakest mandatory grade becomes overall grade
optional inapplicable component absent, not zero-filled
structural/predicted/calibration values remain explainable
```

- [ ] **Step 5: Implement validator and service**

`ConfigurationResourceEstimator` validates request, obtains one base resolution, applies zero/one overlays, appends runtime/storage profiles, validates the final profile, and preserves cancellation.

- [ ] **Step 6: Run focused tests green**

Expected: router, validator, and service tests all pass.

- [ ] **Step 7: Commit routing**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation" tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility
git commit -m "test/feat: add deterministic resource-provider routing"
```

---

### Task 4: Integrate the protected helper and standard GGUF provider

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/ResourceEstimation/Gguf/IGgufResourceEstimatorClient.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/ResourceEstimation/Gguf/GgufResourceEstimatorProcessClient.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/ResourceEstimation/Gguf/GgufResourceEstimatorRequestDto.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/ResourceEstimation/Gguf/GgufResourceEstimatorResponseDto.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/ResourceEstimation/Gguf/GgufResourceEstimatorFailureMapper.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers/GgufLlamaCppResourceEstimatorProvider.cs`
- Modify: application composition root at the exact accepted path
- Modify: packaged helper manifest at the canonical external-tool manifest path
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/GgufResourceEstimatorProcessClientTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/GgufLlamaCppResourceEstimatorProviderTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/ResourceEstimationPrivacyTests.cs`

**Interfaces:**
- Consumes: Task 1 `Pass`/`Partial` fields, Task 2 contracts, Task 3 router, canonical process/artifact foundations.
- Produces: structural standard-GGUF components for admitted routes.

- [ ] **Step 1: Stop if the spike is not admissible**

A `Fail` disposition stops this task. A `Partial` disposition restricts mapping to its named component subset.

- [ ] **Step 2: Write failing process-client tests**

Using the canonical fake process launcher, cover:

```text
fixed --stdio is the only command argument
path appears only in private stdin
spaces and Unicode path survive JSON framing
helper binary digest/version/protocol mismatch rejected
request/stdout/stderr/time/memory bounds enforced
timeout and cancellation terminate complete process tree
wrong fingerprint or upstream commit rejected
unknown JSON property/enum/negative/overflow rejected
raw output/path never enters public diagnostic
```

- [ ] **Step 3: Implement DTOs with strict deserialisation**

Use a source-generated `System.Text.Json` context, fixed schema, maximum depth, bounded byte input, unknown-property rejection, checked `ulong` parsing, and explicit enum allowlists.

- [ ] **Step 4: Integrate guarded artifact access**

Conceptual flow:

```csharp
return await artifactAccess.ExecuteWithValidatedAccessAsync(
    request.Model.Artifact,
    ModelArtifactAccessPurpose.CompatibilityResourceEstimation,
    async (validatedPath, token) =>
    {
        // The canonical guard retains protected read access and verifies full SHA-256.
        var helperResponse = await client.EstimateAsync(
            BuildPrivateRequest(validatedPath, request),
            token);

        return MapValidatedResponse(helperResponse, request);
    },
    cancellationToken);
```

The path is not returned or retained after the callback.

- [ ] **Step 5: Write failing mapping tests**

Prove:

```text
CPU/GPU weights mapped to trusted targets
standard K and V combined into one complete KvCache component without losing sub-evidence
compute buffers remain separate
platform footprint remains RuntimeBootstrap or excluded by explicit zero policy
shared/UMA mapping uses SharedSystemMemoryForGpu
ambiguous topology → NotEstablished
actual offload/context/batch echo mismatch → NotEstablished
```

- [ ] **Step 6: Implement provider mapping**

Map only admitted helper fields. Do not map TPS prediction, remote endpoint, projector, adapter, draft model, RPC, or split-shard data.

- [ ] **Step 7: Run real-helper integration tests**

Use generated fixtures and one controlled Granite GGUF. Verify helper digest, model digest, no mutation, no adjacent-shard read, and deterministic result.

- [ ] **Step 8: Run privacy tests**

Scan command specification, public result, diagnostics, TRX attachments, and retained evidence for absolute model path, username, machine name, token/credential markers, and raw helper output.

- [ ] **Step 9: Commit provider**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility" tests/UnitTests/GraniteEdgeAI.UnitTests tools/Granite.ResourceEstimator
git commit -m "feat(compatibility): add protected standard GGUF resource provider"
```

---

### Task 5: Add application/runtime and storage profiles

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers/ApplicationRuntimeFootprintProvider.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers/ConfigurationStorageFootprintProvider.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Profiles/RuntimeFootprintProfile.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Profiles/StorageFootprintProfile.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/ResourceEstimation/Profiles/EmbeddedResourceProfileCatalogue.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Assets/ResourceEstimation/runtime-footprint-profiles.v1.json`
- Create: `IBM Granite with TurboQuant (Intel)/Assets/ResourceEstimation/storage-footprint-profiles.v1.json`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/RuntimeFootprintProviderTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/StorageFootprintProviderTests.cs`

**Interfaces:**
- Consumes: exact runtime/configuration identities and immutable packaging manifests.
- Produces: mandatory application/runtime and storage component families.

- [ ] **Step 1: Write failing profile-validation tests**

Profiles must include exact schema version, app build identity, runtime commit, backend/device route, Windows architecture, applicability rules, component bytes/phases, evidence grade, and evidence reference.

- [ ] **Step 2: Implement strict embedded catalogue loader**

Reject duplicate profile IDs, unknown properties, invalid hashes, unsupported schema versions, overlapping applicability rules, and unchecked byte values.

- [ ] **Step 3: Write provider-selection tests**

Prove:

```text
exact match selected deterministically
no mandatory runtime profile → NotEstablished
no broad global constant fallback
existing model bytes use exact inspected size
installed helper/runtime not double-counted as additional storage
unknown conversion/temp footprint → NotEstablished
RAM and disk never combined
```

- [ ] **Step 4: Implement providers**

Application/runtime profile includes only allocations that coexist with inference. Analysis-helper memory is not charged to inference. Storage distinguishes present, additional, and temporary bytes.

- [ ] **Step 5: Run tests and commit**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility" "IBM Granite with TurboQuant (Intel)/Assets/ResourceEstimation" tests/UnitTests/GraniteEdgeAI.UnitTests
git commit -m "test/feat: add runtime and storage resource profiles"
```

---

### Task 6: Build the measurement and calibration evidence boundary

**Files:**
- Create: `tools/ResourceMeasurementHarness/README.md`
- Create: `tools/ResourceMeasurementHarness/Measure-ResourceConfiguration.ps1`
- Create: `tools/ResourceMeasurementHarness/Validate-ResourceMeasurement.ps1`
- Create: `tools/ResourceMeasurementHarness/measurement.schema.json`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Calibration/CalibrationErrorRecord.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Calibration/ResourceCalibrationProfile.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Calibration/ResourceCalibrationMatcher.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/ResourceCalibrationTests.cs`
- Create: `docs/evidence/requirements/F-M08/decision-4-standard-gguf-calibration.md`

**Interfaces:**
- Consumes: exact configuration fingerprints and controlled runtime measurements.
- Produces: curated calibration profiles and signed/absolute/percentage error records.

- [ ] **Step 1: Write failing calibration tests**

Prove:

```text
same complete key → exact match
material field change → no exact match
route-class profile applies only inside explicit rules
signed error = measured - predicted
underprediction when measured > predicted
absolute error never negative
percentage uses decimal and handles predicted zero explicitly
failed/OOM/cancelled/fallback run cannot become successful calibration
overall grade cannot exceed weakest mandatory component
```

- [ ] **Step 2: Implement immutable calibration contracts**

Use checked integer differences and `decimal` for percentages. Preserve predicted and measured values separately.

- [ ] **Step 3: Implement measurement schema and validation**

The harness records but never conflates:

```text
process-tree commit/private bytes
working set
runtime component allocations
dedicated GPU peak
shared GPU peak
phase
actual route/offload
terminal outcome
```

- [ ] **Step 4: Run the core calibration matrix**

At minimum:

```text
Granite 3B
→ baseline context + one higher admitted context
→ CPU + one admitted GPU/offload route
→ F16 + Q8_0 KV

Granite 8B
→ baseline context
→ every standard route visible in version one
```

Use one cold run and at least three repeats where practical. Retain maximum peak and median stability value.

- [ ] **Step 5: Create profiles without data leakage**

Curated profiles contain hashes, fingerprints, component bytes, error summaries, applicability rules, and evidence IDs. Raw paths, usernames, machine names, prompts, and logs remain outside product assets.

- [ ] **Step 6: Run calibration tests and commit**

```powershell
git add tools/ResourceMeasurementHarness "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Calibration" tests/UnitTests/GraniteEdgeAI.UnitTests docs/evidence/requirements/F-M08
git commit -m "test(compatibility): calibrate standard GGUF resource estimates"
```

---

### Task 7: Document, wire CI discovery, and verify exact-head evidence

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/README.md`
- Create: `docs/architecture/decisions/ADR-Decision-4-Resource-Estimator.md`
- Modify: `.github/workflows/build-and-test.yml`
- Modify: `docs/evidence/requirements/F-M08/README.md`
- Create: `docs/evidence/requirements/F-M08/decision-4-core-closure.md`

**Interfaces:**
- Consumes: Tasks 0–6.
- Produces: reviewable, exact-head evidence without overclaiming F-M08 completion.

- [ ] **Step 1: Update beginner-readable feature documentation**

Explain:

```text
what is estimated
why one total is insufficient
what Structural/Calibrated/Measured mean
why shared GPU memory is not extra RAM
why TurboQuant replaces KV only
why an estimate is not runtime proof
which routes are supported or NotEstablished
```

- [ ] **Step 2: Write the ADR**

Record option C, rejected alternatives, helper pin/spike result, provider ownership, trust boundary, calibration policy, rollback route, and Decision 5–8 ownership.

- [ ] **Step 3: Add CI gates**

Preserve every existing workflow gate and add:

```text
Go helper unit tests
reproducible helper build/hash check
licence and dependency manifest check
generated adversarial fixture reproducibility
Decision 4 required test-class discovery
privacy scan
orphan-process check
packaged WinUI regression
artifact upload
```

- [ ] **Step 4: Run static verification**

```powershell
git diff --check
git grep -n -E "TODO|TBD|FIXME" -- docs/superpowers docs/architecture
git grep -n -E "HttpListener|TcpListener|UseShellExecute = true|--url|--token|--rpc" -- tools/Granite.ResourceEstimator "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility"
```

Review every match; documentation may describe rejected options, production code may not expose them.

- [ ] **Step 5: Run focused tests**

Retain output proving every new class is discovered and executes at least one test.

- [ ] **Step 6: Run authoritative packaged verification**

Follow the live workflow. Expected:

```text
all restores/builds succeed
non-zero tests execute
all executed tests pass
all required Decision 4 classes appear in TRX
helper gates pass
privacy/orphan checks pass
artifacts upload
```

- [ ] **Step 7: Independently inspect retained artifacts**

Verify:

```text
artifact/TRX/helper SHA-256
exact head SHA
all test outcomes
helper source/licence/toolchain identity
no model file
no absolute path/username/machine name
no token/credential
```

- [ ] **Step 8: Record honest closure**

`decision-4-core-closure.md` states:

```text
Decision 4 core implementation status
helper disposition
supported standard routes
calibration coverage and underprediction
known NotEstablished routes
Decision 5/6 dependencies
F-M08 remains partial until full acceptance criteria pass
```

- [ ] **Step 9: Request review and commit**

```powershell
git add .
git commit -m "docs(test): close Decision 4 core estimator evidence"
```

Use `superpowers:requesting-code-review`, fix every Critical/Important finding, then use `superpowers:verification-before-completion` before marking the PR ready.

---

## Core acceptance checklist

- [ ] One application estimator boundary exists.
- [ ] One exact configuration has one deterministic fingerprint.
- [ ] Decision 1–3 contracts are reused.
- [ ] Current free memory and safety policy remain outside Decision 4.
- [ ] Every mandatory component has one source or a typed unavailable outcome.
- [ ] Shared GPU memory is system-backed and counted once.
- [ ] The helper spike records `Pass`, `Partial`, or `Fail`.
- [ ] The helper is pinned, offline, bounded, contained, digest-verified, and local-only.
- [ ] The model path is absent from command line, diagnostics, fingerprint, and evidence.
- [ ] Split GGUF is rejected in version one.
- [ ] Standard-GGUF components map through project-owned DTOs.
- [ ] App/runtime and storage profiles are versioned and exact-route matched.
- [ ] Calibration records include one-sided underprediction.
- [ ] No silent format, device, backend, offload, or context fallback exists.
- [ ] Focused and packaged tests execute and pass.
- [ ] Retained artifacts are independently inspected.
- [ ] `F-M08` is not marked Verified prematurely.
