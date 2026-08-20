# Decision 4 Resource-Estimator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development`
> (recommended) or `superpowers:executing-plans` task-by-task. Apply
> `superpowers:test-driven-development` to every production behaviour and
> `superpowers:verification-before-completion` before each success claim.

**Goal:** Implement one application-owned estimator that produces a complete,
component-level resource profile for one exact model/runtime/device configuration.
The first production route is qualified local GGUF/llama.cpp; TurboQuant and OpenVINO
are added only when their exact formula, package-identity, support-matrix and calibration
prerequisites exist.

**Architecture:** `ConfigurationResourceEstimator` consumes canonical Model Inspection
facts, a privacy-safe projection of canonical Hardware Inspection topology and one exact
configuration. A static router selects one base provider, applies at most one approved
overlay, adds versioned application/runtime and storage evidence, validates component
ownership and provenance, normalises shared-memory pressure and returns an immutable
resolution. A local Go helper may supply proven GGUF structural facts, but all public
contracts, failure semantics, topology mapping, evidence grades and final interpretation
remain application-owned.

**Tech Stack:** C# 12, .NET 8, WinUI 3, Windows App SDK, packaged MSTest,
Windows x64, Go 1.22.9, pinned `gguf-parser-go`, SHA-256, GitHub Actions and the
repository's canonical protected external-process foundation.

**Spec:**
`docs/superpowers/specs/2026-08-20-model-hardware-compatibility-resource-estimator-design.md`

**Binding amendment:**
`docs/superpowers/specs/2026-08-20-model-hardware-compatibility-resource-estimator-approval-amendment.md`

## Global constraints

- Reuse the canonical `ModelInspectionHandoff`, `HardwareInspectionHandoff`,
  `ContextTokenCount`, `CompatibilityContextRequest`, `CompatibilityPolicyIdentity`
  and guarded artifact-access contracts. Never recreate predecessor DTOs.
- Production work starts only on an accepted integration base containing one production
  owner for every predecessor contract and an approved reusable process-security package.
- Reuse the canonical executable verifier, shell-free launcher, creation-time Job Object,
  exact handle allowlist, bounded streams, timeout/cancellation, whole-tree termination,
  orphan check and privacy-safe diagnostics. Decision 4 owns only helper-specific framing,
  schema mapping, failure codes and component conversion.
- Estimate one exact configuration. Do not generate alternatives, rank candidates, change
  context, substitute cache/device/backend, or silently alter offload.
- Decision 4 owns provider/evidence architecture. Decision 5 owns formulas, checked
  arithmetic, rounding and lifecycle peak composition. Decision 6 owns OS allowance,
  reserve and fit thresholds. Decision 7 owns ranking. Decision 8 owns admitted routes and
  candidate generation. Runtime Verification owns actual load/generation proof.
- Use checked unsigned 64-bit bytes internally. Zero is a known value; unknown evidence is
  a typed unavailable result. Signed differences use direction plus unsigned magnitude.
- Every mandatory `(kind, role, target, phase)` slot has exactly one owner. TurboQuant
  replaces the complete standard KV set; it never coexists with it.
- `SharedSystemMemoryForGpu` is system-backed pressure and is counted once.
- `ResourceDeviceRouteId` is a deterministic allowlisted key such as `cpu:0`, `vulkan:0`,
  `openvino:CPU` or `openvino:GPU.0`. Raw PnP, serial, machine, account, telemetry and
  registry identifiers cannot cross into Compatibility.
- No assembly scanning, provider plug-in folder, UI-selected provider or network discovery.
- The helper is short-lived, local-only, schema-versioned, bounded, shell-free and receives
  the private model locator only through bounded stdin while guarded access is held.
- Version one admits one locked single-file GGUF. Split GGUF remains unavailable until
  Model Inspection owns an immutable complete shard-package identity.
- Structural estimates are not measurements. `CalibratedMatched` and `MeasuredExact`
  require the approved applicability rules and matched evidence.
- No completion claim is made before fresh exact-head CI and retained-artifact inspection.

---

## File map

```text
IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/
├── Application/ResourceEstimation/
│   ├── IConfigurationResourceEstimator.cs
│   ├── ConfigurationResourceEstimator.cs
│   ├── ConfigurationResourceEstimationRequest.cs
│   ├── ConfigurationResourceProfileResolution.cs
│   ├── ConfigurationResourceProfile.cs
│   ├── ResourceComponentEstimate.cs
│   ├── ResourceComponentRequirement.cs
│   ├── ResourceByteCount.cs
│   ├── ResourceByteDelta.cs
│   ├── ResourceDeviceRouteId.cs
│   ├── ResourceRuntimeIdentity.cs
│   ├── ResourceConfigurationFingerprint.cs
│   ├── ResourceEstimationConfiguration.cs
│   ├── LlamaCppResourceEstimationConfiguration.cs
│   ├── OpenVinoGenAiResourceEstimationConfiguration.cs
│   ├── ResourceTopologyProjection.cs
│   ├── ResourceEstimationIdentity.cs
│   ├── ResourceEstimationCodes.cs
│   ├── ResourceEstimationEnums.cs
│   └── Providers/
│       ├── IBaseResourceEstimatorProvider.cs
│       ├── IResourceEstimateOverlay.cs
│       ├── IRuntimeFootprintProvider.cs
│       ├── IStorageFootprintProvider.cs
│       ├── ResourceEstimatorProviderRouter.cs
│       ├── ResourceProfileCompletenessValidator.cs
│       ├── ResourceTopologyProjector.cs
│       ├── ResourceTopologyNormalizer.cs
│       ├── ResourceEstimationComposition.cs
│       ├── ApplicationRuntimeFootprintProvider.cs
│       ├── ConfigurationStorageFootprintProvider.cs
│       ├── TurboQuantKvResourceEstimateOverlay.cs
│       ├── OpenVinoGenAiResourceEstimatorProvider.cs
│       └── ProviderContracts.cs
└── Infrastructure/ResourceEstimation/Gguf/
    ├── GgufResourceEstimatorToolProfile.cs
    ├── GgufResourceEstimatorProcessAdapter.cs
    ├── GgufLlamaCppResourceEstimatorProvider.cs
    ├── GgufResourceEstimatorRequestDto.cs
    ├── GgufResourceEstimatorResponseDto.cs
    └── GgufResourceEstimatorFailureMapper.cs

tools/Granite.ResourceEstimator/
├── go.mod
├── go.sum
├── main.go
├── contract.go
├── estimate.go
├── contract_test.go
├── estimate_test.go
├── spike/Run-ResourceEstimatorAuditHarness.ps1
├── README.md
└── LICENSES/gguf-parser-go-MIT.txt

IBM Granite with TurboQuant (Intel)/Assets/ResourceEstimation/
├── application-runtime-footprints-v1.json
├── storage-footprints-v1.json
├── calibration-profiles-v1.json
└── resource-estimator-tool-manifest-v1.json

tests/UnitTests/GraniteEdgeAI.UnitTests/Features/
└── ModelHardwareCompatibility/ResourceEstimation/
    ├── ResourceEstimationContractTests.cs
    ├── ResourceConfigurationFingerprintTests.cs
    ├── ResourceDeviceRouteIdTests.cs
    ├── ResourceEstimatorProviderRouterTests.cs
    ├── ResourceProfileCompletenessValidatorTests.cs
    ├── ResourceTopologyNormalizerTests.cs
    ├── ConfigurationResourceEstimatorTests.cs
    ├── GgufResourceEstimatorProcessAdapterTests.cs
    ├── GgufLlamaCppResourceEstimatorProviderTests.cs
    ├── ApplicationRuntimeFootprintProviderTests.cs
    ├── ConfigurationStorageFootprintProviderTests.cs
    ├── TurboQuantKvResourceEstimateOverlayTests.cs
    ├── OpenVinoGenAiResourceEstimatorProviderTests.cs
    ├── ResourceCalibrationTests.cs
    ├── ResourceEstimationPrivacyTests.cs
    └── ResourceEstimationTestData.cs

scripts/testing/resource_estimation/
├── build_calibration_profile.py
├── validate_calibration_profile.py
└── README.md

tests/testing/resource_estimation/
├── test_build_calibration_profile.py
└── test_validate_calibration_profile.py

docs/evidence/requirements/F-M08/
├── decision-4-entry-gate.md
├── decision-4-gguf-helper-spike.md
└── decision-4-resource-estimator-calibration.md

docs/architecture/decisions/ADR-Decision-4-Resource-Estimator.md
```

The four JSON catalogues are `EmbeddedResource`. The qualified helper becomes fixed
application `Content` only after its manifest, architecture and SHA-256 are verified.

## Locked contract shape

```csharp
internal interface IConfigurationResourceEstimator
{
    CompatibilityPolicyIdentity Identity { get; }

    Task<ConfigurationResourceProfileResolution> EstimateAsync(
        ConfigurationResourceEstimationRequest request,
        CancellationToken cancellationToken);
}
```

`ConfigurationResourceEstimationRequest.Create(...)` accepts exactly:

```text
ModelInspectionHandoff
ResourceTopologyProjection
ResourceEstimationConfiguration
ResourceConfigurationFingerprint
```

Validated request/configuration/component types are sealed classes with private
constructors and named factories. Positional records and public `init` setters are not used
for cross-property invariants because a non-destructive mutation could bypass validation.

Configuration variants:

```text
LlamaCppResourceEstimationConfiguration
→ runtime, logical route, context, backend, logical/physical batch, parallel sequences,
  offload plan, K/V types, TurboQuant mode, mmap, Flash Attention, KV offload

OpenVinoGenAiResourceEstimationConfiguration
→ runtime, logical route, context, device, weight precision, KV precision,
  parallel sequences and scheduler settings
```

`LlamaCppOffloadPlan` is `None`, `ExactLayers` or `AllLayers`; application contracts do
not use `-1`, `999` or another magic sentinel.

Result states:

```text
Established
→ complete profile; no failure/diagnostic

NotEstablished
→ no profile; stable evidence/support reason

OperationalFailure
→ no profile; stable failure plus sanitised diagnostic code

Cancelled
→ no profile and no fabricated failure
```

Component catalogues:

```text
ResourceComponentKind
→ ModelWeights, KvCache, ComputeAndScratch, RuntimeBootstrap,
  ApplicationFootprint, LoadOrCompileTransient, StorageExistingArtifact,
  StorageAdditionalArtifact, StorageTemporary

ResourceComponentRole
→ None, Key, Value, Auxiliary

ResourceTarget
→ SystemMemory, DedicatedVideoMemory, SharedSystemMemoryForGpu,
  DeviceLocalMemory, Storage

ResourceLifecyclePhase
→ Startup, ModelLoad, LoadOrCompile, Prefill, Decode, SteadyState, Conversion

ResourceEvidenceGrade
→ StructuralEstimate, CalibratedMatched, MeasuredExact
```

Every enum reserves zero for `Unspecified`. `ResourceByteCount` wraps `ulong`.
`ResourceByteDelta` represents zero or increase/decrease plus unsigned magnitude.
The fingerprint is SHA-256 over a fixed domain separator, canonical model content
identity, canonical topology projection and every memory-relevant configuration field. It
excludes paths, timestamps, free memory, usernames, machine names and UI labels.

Provider contracts expose pure `AssessSupport(...)` methods and asynchronous estimate or
overlay methods. `AssessSupport` starts no process, opens no file, reads no clock and queries
no hardware/network.

---

## Task 0: Establish the accepted integration base

**Files:**
- Create: `docs/evidence/requirements/F-M08/decision-4-entry-gate.md`
- Do not modify production source in this task

**Produces:** exact accepted base SHA; canonical predecessor paths/namespaces; reusable
process package identity; guarded-access purpose; workflow/TRX baseline; `Pass` or
`Blocked`.

- [ ] **Step 1: Create an isolated branch from one reviewed integrated commit**

```powershell
$ErrorActionPreference = 'Stop'
$base = (git rev-parse --verify "$env:DECISION4_ACCEPTED_BASE^{commit}").Trim()
if ($base -notmatch '^[0-9a-f]{40}$') { throw 'Accepted base is not one full commit.' }
if ((git status --porcelain).Count -ne 0) { throw 'Repository must be clean.' }
git switch --detach $base
git switch -c feature/model-hardware-compatibility-resource-estimator
```

- [ ] **Step 2: Prove one production owner for every predecessor type**

Search production roots for exactly one definition of:

```text
ModelInspectionHandoff
HardwareInspectionHandoff
ContextTokenCount
CompatibilityContextRequest
CompatibilityPolicyIdentity
IInspectedModelArtifactAccessService
```

A zero or duplicate count records `Blocked`; do not create a substitute DTO.

- [ ] **Step 3: Prove the reusable process foundation**

Record the exact project/interface owning executable trust, `CreateProcessW`/
`STARTUPINFOEX`, creation-time Job Object assignment, handle allowlist, bounded pipes,
timeout/cancellation, whole-tree termination and orphan proof. A feature-private
Model Inspection client without an approved shared extraction is `Blocked`.

- [ ] **Step 4: Prove guarded Compatibility artifact access**

Require explicit `CompatibilityAnalysis` purpose and a callback policy that locks the
artifact and performs full pre/post SHA-256 continuity validation. A different purpose
requires a reviewed Decision 1 amendment.

- [ ] **Step 5: Run the exact WinUI Release x64 and packaged MSTest route**

Use `.github/workflows/build-and-test.yml`, including `msbuild` restore/build and
`vstest.console.exe` against the `.build.appxrecipe`; `dotnet test` is not a substitute.
Require non-zero discovery, all tests passed and a retained TRX bound to the base SHA.

- [ ] **Step 6: Write and commit the gate**

Record exact SHAs, type paths, namespaces, shared process interface, workflow blob,
run/TRX hashes, reviewer/date and `Pass` or `Blocked`.

```powershell
git add 'docs/evidence/requirements/F-M08/decision-4-entry-gate.md'
git commit -m 'docs(compatibility): record Decision 4 implementation entry gate'
```

---

## Task 1: Qualify the bounded local GGUF helper

**Files:** every file under `tools/Granite.ResourceEstimator/` in the file map,
`decision-4-gguf-helper-spike.md`, and `.github/workflows/build-and-test.yml` only after a
`Pass` or accepted `Partial` disposition.

**Produces:** bounded project-owned JSON with raw model-size, per-device weight, ordinary
KV, compute, footprint, placement and applied-configuration evidence. It never returns a
fit verdict, reserve, maximum context, TurboQuant result, OpenVINO result, path or UI text.

- [ ] **Step 1: Pin the Go module without changing global Go settings**

```powershell
Set-Location 'tools/Granite.ResourceEstimator'
$env:GOTOOLCHAIN = 'go1.22.9'
go mod init github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant/tools/Granite.ResourceEstimator
go get github.com/gpustack/gguf-parser-go@a5d9227ae50725494c827c7810c662e102143293
go mod tidy
go list -m all
```

Commit the resolver-produced pseudo-version and `go.sum`; do not use `go env -w`.

- [ ] **Step 2: Write RED contract tests**

Test closed JSON, strict UTF-8, one 64 KiB request, one 256 KiB response, schema 1,
absolute local non-UNC path, lowercase 64-character fingerprint, positive counts,
physical batch not above logical batch, allowlisted cache/offload values, split filename and
adjacent-shard rejection, stable parse failures and path-free output. Unknown `url`, token,
RPC or repository fields must fail decoding.

```go
func TestDecodeRequestRejectsUnknownFields(t *testing.T) {
    input := `{"schemaVersion":1,"modelPath":"C:\\m.gguf",` +
        `"requestFingerprint":"` + strings.Repeat("a", 64) + `",` +
        `"contextTokens":4096,"logicalBatchSize":512,` +
        `"physicalBatchSize":512,"parallelSequenceCount":1,` +
        `"offload":{"kind":"none"},"keyCacheType":"f16",` +
        `"valueCacheType":"f16","memoryMapEnabled":true,` +
        `"flashAttentionEnabled":false,"kvCacheOffloadEnabled":false,` +
        `"url":"https://example.invalid"}`

    _, err := decodeRequest(strings.NewReader(input))
    if !errors.Is(err, errUnknownField) { t.Fatalf("unexpected error: %v", err) }
}
```

- [ ] **Step 3: Prove RED**

```powershell
go test ./... -run 'TestDecodeRequest|TestResponse|TestMain' -count=1
```

Expected: compile/test failure because the closed contract is absent.

- [ ] **Step 4: Implement the minimum helper**

Validate UTF-8 before `json.Decoder.DisallowUnknownFields()`. Require one JSON value.
Reject split-style names before calling `CompleteShardGGUFFilename`; reject any discovered
set. Call `ParseGGUFFile` with `SkipLargeMetadata()` and optional `UseMMap()`. Map every
context/batch/parallel/cache/offload/mmap/Flash-Attention/KV-offload option explicitly into
`EstimateLLaMACppRun`. Support only F16 and Q8_0 KV, CPU plus at most one local GPU,
and `None`/`ExactLayers`/`AllLayers`. Emit separate raw categories and an exact
`appliedConfiguration` echo. Never calculate peak, reserve, shared-memory composition or
calibration grade.

- [ ] **Step 5: Prove GREEN and reproducible build**

```powershell
go test ./... -count=1
go vet ./...
$env:CGO_ENABLED='0'; $env:GOOS='windows'; $env:GOARCH='amd64'
go build -trimpath -buildvcs=true -ldflags '-s -w' -o artifacts/granite-resource-estimator.exe .
go version -m artifacts/granite-resource-estimator.exe
Get-FileHash artifacts/granite-resource-estimator.exe -Algorithm SHA256
```

Build twice from clean caches on the same clean commit; hashes must match.

- [ ] **Step 6: Audit supply chain and prohibited network surface**

Record `go list -m -json all`, `go list -deps -json .`, `go tool nm`, PE imports, MIT
notice, toolchain and binary hash. Search source/symbols/imports for URL/token/RPC/server
modes. In a disposable elevated VM, back up audit policy, enable successful
`Filtering Platform Connection` auditing, run the fixed helper harness, query Security
Events 5154/5156/5158 by helper PID, and restore audit policy in `finally`. Any observed
connection/listen/bind is `Fail`. Linked unreachable network code is at most `Partial` and
requires architecture/security acceptance or a reviewed network-free fork/subpackage.

- [ ] **Step 7: Run the spike matrix and record disposition**

Run generated valid/malformed/truncated/overflow fixtures, one locked Granite GGUF, CPU,
partial/full one-GPU offload, F16 and Q8_0 KV, mmap/Flash Attention/KV offload toggles,
context/batch boundaries, timeout/cancellation and three repeated identical requests.
Compare at least one CPU and one admitted GPU mapping with pinned llama.cpp allocation
evidence. Record exactly `Pass`, named-subset `Partial`, or `Fail` with all source/module/
licence/toolchain/binary/fixture hashes and privacy results.

- [ ] **Step 8: Add CI gates and commit**

Use pinned `actions/setup-go@924ae3a1cded613372ab5595356fb5720e22ba16`, Go
1.22.9, module/licence tests, `go test`, `go vet`, reproducible build and forbidden-mode
checks. Do not package the helper into WinUI yet.

```powershell
git add tools/Granite.ResourceEstimator `
  docs/evidence/requirements/F-M08/decision-4-gguf-helper-spike.md `
  .github/workflows/build-and-test.yml
git commit -m 'test(spike): qualify local GGUF resource estimator helper'
```

---

## Task 2: Add immutable contracts and deterministic fingerprinting

**Files:** all application contract files plus
`ResourceEstimationContractTests.cs`, `ResourceConfigurationFingerprintTests.cs`,
`ResourceDeviceRouteIdTests.cs` and `ResourceEstimationTestData.cs` from the file map.

- [ ] **Step 1: Write RED contract tests**

Prove zero-reserved enums, known zero and `ulong.MaxValue`, directional byte deltas,
positive counts, physical batch at most logical batch, CPU/no-GPU-offload, explicit
offload variants, TurboQuant/KV consistency, OpenVINO scheduler consistency, exhaustive
resolution invariants and immutable copied collections.

- [ ] **Step 2: Write route-privacy tests**

```csharp
[DataTestMethod]
[DataRow(@"PCI\\VEN_8086&DEV_7D55")]
[DataRow("SERIAL-001")]
[DataRow("MACHINE-GUID")]
[DataRow(@"HKEY_LOCAL_MACHINE\\SYSTEM")]
public void TryParseCanonical_RejectsNativeIdentifiers(string value)
{
    Assert.IsFalse(ResourceDeviceRouteId.TryParseCanonical(value, out _));
}
```

Also prove the exact logical keys and ambiguous-route rejection.

- [ ] **Step 3: Prove RED**

Build the packaged test project. Expected: compile failure because contracts are absent.

- [ ] **Step 4: Implement sealed validated factories**

Use private constructors, named factories, get-only properties and copied arrays. Do not
use positional records/public `init` on trust-bearing cross-property contracts. Result
factories enforce the four status/payload combinations. Test builders use production
factories and no reflection/uninitialised objects.

- [ ] **Step 5: Write RED fingerprint tests**

Prove repeatability and culture independence; then change every model/topology/runtime/
context/batch/cache/offload/mmap/Flash-Attention/KV-offload/OpenVINO scheduler field one
at a time and require a different hash. Dedicated-versus-shared topology changes the
hash. Paths, timestamps, free memory, username, machine name and UI label cannot be
supplied.

- [ ] **Step 6: Implement canonical SHA-256 writer and prove GREEN**

Use `IncrementalHash`, fixed field order, UTF-8 strings prefixed by checked length,
little-endian integers, fixed enum values and one-byte booleans. Prefix:

```text
GraniteEdgeAI.ResourceConfigurationFingerprint\0v1\0
```

Do not hash `ToString()`, reflection order or default JSON. Run the exact packaged VSTest
route and require non-zero passes from all new classes, then full regression.

- [ ] **Step 7: Commit**

```powershell
git add 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/ResourceEstimation'
git commit -m 'feat(compatibility): add resource-estimation contracts and fingerprint'
```

---

## Task 3: Add topology, static routing and completeness

**Files:** `ConfigurationResourceEstimator.cs`; every provider interface/router/topology/
composition contract in the file map; router/completeness/topology/orchestrator tests.

- [ ] **Step 1: Write RED topology tests**

Canonical CPU maps to `cpu:0`; discrete GPU maps to a dedicated pool; integrated/UMA GPU
maps to `SharedSystemMemoryForGpu`; raw native identity is never copied; ambiguous or
conflicting topology returns `ResourceTopologyAmbiguous`; unknown route returns
`UnsupportedDeviceRoute`; identical facts produce value-equivalent projection.

- [ ] **Step 2: Write RED routing tests**

```text
standard llama.cpp → one GGUF base, no overlay
TurboQuant llama.cpp → GGUF base plus one TurboQuant overlay
OpenVINO → OpenVINO base only
unsupported route → NotEstablished before I/O
provider registration order does not change selection
caller/UI cannot name a provider
```

- [ ] **Step 3: Write RED ownership/normalisation tests**

Require one owner per exact slot; missing mandatory slot is unavailable; duplicate slot is
provider-contract failure; inapplicable slot is absent; standard/TurboQuant KV coexistence
is rejected; host and dedicated GPU remain separate; shared GPU pressure maps to system
memory once; ambiguous helper target cannot establish a profile.

- [ ] **Step 4: Implement explicit composition and pure router**

`ResourceEstimationComposition` constructs exact providers with typed constructors.
`ResourceEstimatorProviderRouter` switches only on typed configuration variants; no
assembly scanning or service lookup by string. `AssessSupport` has no I/O.

- [ ] **Step 5: Implement orchestration**

```text
validate request/fingerprint
→ pure plan
→ one base provider
→ zero/one planned overlay
→ runtime and storage providers
→ component ownership/provenance validation
→ topology normalisation
→ weakest mandatory evidence grade
→ immutable resolution
```

Cancellation remains distinct. Expected support gaps are `NotEstablished`; raw provider
exceptions are not retained.

- [ ] **Step 6: Prove GREEN and commit**

Run new classes and full packaged tests, including a test that `AssessSupport` starts no
process/clock/network/file operation.

```powershell
git add 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/ResourceEstimation'
git commit -m 'feat(compatibility): enforce resource-provider coverage and topology'
```

---

## Task 4: Add protected standard-GGUF provider

**Entry:** Task 1 is `Pass`, or accepted `Partial` covers every component published;
Task 0 names the canonical process and artifact-access interfaces.

**Files:** all six GGUF infrastructure files; adapter/provider/privacy tests; app and test
project references; `ResourceEstimationComposition.cs`.

- [ ] **Step 1: Reference the canonical process project**

Use exact Task 0 paths. Build immediately. Copying Model Inspection runner source is
forbidden.

- [ ] **Step 2: Write RED adapter tests against the shared fake seam**

Prove fixed helper identity and `--stdio`; path only in bounded stdin; Unicode path
framing; no path in argv/result/diagnostic/Trace/TRX; request/output/error limits;
closed JSON/schema/helper identity; checked numbers; sanitised non-zero exit; timeout/
cancellation whole-tree request; digest/architecture mismatch prevents launch. Do not
mock `System.Diagnostics.Process`.

- [ ] **Step 3: Implement helper-specific adapter only**

`GgufResourceEstimatorToolProfile` owns manifest key, protocol/schema, fixed argument,
64 KiB stdin, 256 KiB stdout, 64 KiB stderr and 120-second timeout. The canonical runner
owns process creation and containment. Parse with case-sensitive `System.Text.Json`,
unmapped members disallowed, no comments/trailing commas, depth 16 and checked numeric
conversion. Validate schema, request fingerprint, source commit, binary SHA-256, applied
configuration, unique components and status invariants.

- [ ] **Step 4: Write RED guarded-artifact tests**

Prove no raw path in provider API; full hash before launch; write/delete-denying access
held for helper lifetime; post-operation continuity; changed/busy model and split GGUF map
to typed unavailable reasons; helper failure retains no component.

- [ ] **Step 5: Implement guarded provider mapping**

Inside one guarded callback, build exact DTO, invoke canonical runner, validate echo,
complete post-check, then publish only spike-admitted categories:

```text
weights by device → ModelWeights / ModelLoad
KV key/value → KvCache / Key or Value / Decode
compute input/graph/output → ComputeAndScratch / proven Prefill or Decode phase
separately proven bootstrap → RuntimeBootstrap / Startup
```

An unproven category is omitted and completeness returns unavailable; no heuristic fills it.

- [ ] **Step 6: Run real helper, privacy and duplication checks**

Use generated fixtures plus one controlled uncommitted Granite GGUF. Verify model/helper
hash continuity, timeout/cancel, no orphan/child and no retained path. `git grep` the
Decision 4 feature for `CreateProcessW`, `CreateJobObject`, handle-list primitives and
`new Process(`; no generic process implementation may exist there.

- [ ] **Step 7: Prove GREEN and commit**

```powershell
git add 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/ResourceEstimation/Gguf' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers/ResourceEstimationComposition.cs' `
  'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests'
git commit -m 'feat(compatibility): add protected standard-GGUF resource provider'
```

---

## Task 5: Add versioned runtime/application and storage profiles

**Files:** both profile providers/tests; four JSON assets; application project resource
entries.

- [ ] **Step 1: Write RED runtime-profile tests**

Matching includes application build, exact runtime version/commit, backend/logical route,
Windows architecture, required model class and lifecycle phase. No applicable profile is
`RuntimeFootprintProfileUnavailable`; global catch-all is rejected; helper analysis memory
is excluded; schema/version mismatch is profile-contract failure.

- [ ] **Step 2: Write RED storage tests**

Existing model bytes use inspected exact size; installed and additionally required bytes
remain separate; helper/runtime bytes come from immutable digest manifest; conversion/
cache/temp bytes require applicable evidence; unknown temp is unavailable; disk never
combines with RAM/VRAM; helper digest drift invalidates manifest.

- [ ] **Step 3: Implement strict embedded catalogues**

Compile all four JSON files as fixed-name `EmbeddedResource`. Parse once into immutable
records with closed schema, unique keys, exact bytes and catalogue SHA-256 in provider
identity. Selection is deterministic and most-specific; equal-specificity duplicate matches
are contract failure. Unit tests use test-only catalogues. Production files contain only
accepted evidence; an empty catalogue honestly returns unavailable.

- [ ] **Step 4: Prove GREEN and commit**

```powershell
git add 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers' `
  'IBM Granite with TurboQuant (Intel)/Assets/ResourceEstimation' `
  'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/ResourceEstimation'
git commit -m 'feat(compatibility): add versioned runtime and storage profiles'
```

---

## Task 6: Add Decision-5-backed TurboQuant KV replacement

**Entry:** accepted Decision 5 calculator and tests for every Decision 8-admitted mode;
entry-gate evidence records exact SHA/type/namespace/formula identity.

**Files:** `TurboQuantKvResourceEstimateOverlay.cs`, its test, explicit composition and
matched calibration catalogue.

- [ ] **Step 1: Verify one calculator owner and write RED overlay tests**

For each actually admitted `turbo4`, `turbo3`, `turbo2`, prove weights/compute unchanged;
all standard Key/Value/Auxiliary KV removed; one complete replacement set inserted;
payload, scales/codebooks, rotation/preconditioning, alignment/padding, per-layer/head
metadata and active auxiliary categories represented; checked overflow unavailable;
unsupported route unavailable; QJL/PolarQuant rejected without exact accepted
implementation; no fallback to standard or another Turbo mode; formula identity retained.

- [ ] **Step 2: Implement translation/replacement only**

Translate exact model/configuration facts into the canonical Decision 5 request, invoke it,
map categories, remove standard KV and atomically insert the complete set. Do not copy a
byte formula into Decision 4. Re-run completeness/provenance validation.

- [ ] **Step 3: Apply only matched calibration**

Require model hash, runtime, topology/route, context, batch, offload and exact Turbo mode.
A measured compression ratio from one case cannot calculate another.

- [ ] **Step 4: Prove GREEN and commit**

Run overlay, canonical calculator, metamorphic, router, completeness, privacy and full
packaged tests.

```powershell
git add 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers/TurboQuantKvResourceEstimateOverlay.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/ResourceEstimation/TurboQuantKvResourceEstimateOverlayTests.cs' `
  'IBM Granite with TurboQuant (Intel)/Assets/ResourceEstimation/calibration-profiles-v1.json'
git commit -m 'feat(compatibility): add Decision-5-backed TurboQuant KV overlay'
```

---

## Task 7: Add the explicitly admitted OpenVINO second slice

**Entry:** immutable canonical OpenVINO package identity; accepted Decision 5 structural/
phase rules; Decision 8 exact model/device/precision/scheduler routes; complete component
evidence. Otherwise keep typed unavailable routing.

**Files:** OpenVINO provider/test; router, completeness and composition; accepted profile
assets.

- [ ] **Step 1: Write RED package, route and evidence tests**

Require trusted package manifest; reject arbitrary XML plus guessed BIN; member hash/size
drift is changed artifact; GGUF provider never selected; unadmitted 8B/NPU/device/
precision/scheduler route unavailable; folder size cannot establish peak; missing KV or
transient evidence prevents completeness; structural route stays structural and only
matching evidence raises grade.

- [ ] **Step 2: Implement the narrow provider**

Use only accepted package/calculator/profile facts; preserve every scheduler setting in the
fingerprint. Do not convert or compile a model inside the estimator.

- [ ] **Step 3: Prove GREEN and commit separately**

```powershell
git add 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ResourceEstimation/Providers/OpenVinoGenAiResourceEstimatorProvider.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/ResourceEstimation/OpenVinoGenAiResourceEstimatorProviderTests.cs' `
  'IBM Granite with TurboQuant (Intel)/Assets/ResourceEstimation'
git commit -m 'feat(compatibility): add admitted OpenVINO resource provider'
```

The core implementation PR may omit this separately gated commit.

---

## Task 8: Materialise calibration and underprediction evidence

**Files:** both Python scripts/tests, `ResourceCalibrationTests.cs`, calibrated JSON and
`decision-4-resource-estimator-calibration.md`.

- [ ] **Step 1: Write RED Python tests**

Prove exact key accepted; one material mismatch rejected; failed/OOM/cancelled/fallback
run rejected; signed error is measured minus predicted; absolute error non-negative;
predicted-zero percentage explicit; underprediction retained; unobserved component cannot
be marked measured; sample/validation sets disjoint where required; path/machine/account/
token fields rejected. Use Python integers and `decimal.Decimal`.

- [ ] **Step 2: Prove RED**

```powershell
python -m unittest discover -s tests/testing/resource_estimation -p 'test_*.py' -v
```

- [ ] **Step 3: Implement deterministic materialiser and independent validator**

Read schema-validated JSON only, verify all identities, calculate error facts and write
UTF-8 deterministic JSON through exclusive sibling temp plus atomic rename. Refuse
overwrite. The validator recomputes and rejects missing/extra/duplicate/case-colliding or
privacy-unsafe fields.

- [ ] **Step 4: Write C# applicability tests and curate cases**

Freeze model/package hash, runtime/provider/helper/formula identity, logical route,
canonical topology fingerprint, context, batches, parallel sequences, K/V or Turbo mode,
offload, mmap, Flash Attention, KV offload, scheduler and requested/actual runtime route.
Retain predicted/measured peaks, signed/absolute/percentage error, underprediction,
observable component facts, run/artifact/tool hashes. Failed/fallback cases remain negative
evidence.

- [ ] **Step 5: Validate, document and commit**

Report route coverage, sample/validation cases, maximum observed underprediction per
physical pool, limitations and non-claims. Do not choose Decision 6's margin or mark F-M08
fully verified.

```powershell
git add scripts/testing/resource_estimation tests/testing/resource_estimation `
  'IBM Granite with TurboQuant (Intel)/Assets/ResourceEstimation/calibration-profiles-v1.json' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/ResourceEstimation/ResourceCalibrationTests.cs' `
  docs/evidence/requirements/F-M08/decision-4-resource-estimator-calibration.md
git commit -m 'test(compatibility): add matched resource-estimator calibration evidence'
```

---

## Task 9: Close packaging, CI, documentation and exact-head evidence

**Files:** ADR, feature README, app/test project files, workflow and final PR body.

- [ ] **Step 1: Write ADR and beginner-facing README**

Record selected architecture, rejected full GPUStack/CLI, component ownership,
single-file boundary, shared-memory rule, process/security boundary, route/grade matrix,
calibration, rollback/supersession and Decision 5–8/Runtime Verification boundaries.

- [ ] **Step 2: Package helper and embed catalogues**

Add the qualified executable as fixed `Content` under `Tools/ResourceEstimation/`; embed
four JSON resources. Tool manifest records relative path, source commit, protocol, PE
architecture and lowercase SHA-256. Packaged test resolves through canonical verifier and
rejects hash/architecture mismatch.

- [ ] **Step 3: Extend CI without replacing the packaged WinUI route**

Sparse checkout includes helper, shared process projects, assets and calibration tests.
Before WinUI restore run module/licence checks, Go test/vet, reproducible helper build,
forbidden-mode checks, Python tests, catalogue validation and privacy scan. Retain existing
Release x64 build and app-container VSTest command.

- [ ] **Step 4: Require non-zero Decision 4 class discovery**

Append at minimum:

```text
ResourceEstimationContractTests
ResourceConfigurationFingerprintTests
ResourceEstimatorProviderRouterTests
GgufResourceEstimatorProcessAdapterTests
GgufLlamaCppResourceEstimatorProviderTests
ResourceEstimationPrivacyTests
```

Append TurboQuant/OpenVINO classes when present. Existing required classes remain.

- [ ] **Step 5: Run static, focused and full verification**

Run `git diff --check`; scan production/helper/assets/evidence for temporary compatibility
DTOs, reflection discovery, shell/process duplication, network modes, model paths and raw
hardware/account/machine identifiers. Run each Go/Python/C# class with non-zero discovery,
then all suites.

- [ ] **Step 6: Commit final docs/CI**

```powershell
git add docs/architecture/decisions/ADR-Decision-4-Resource-Estimator.md `
  'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/README.md' `
  '.github/workflows/build-and-test.yml' `
  'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj'
git commit -m 'docs(test): close Decision 4 resource-estimator evidence'
```

- [ ] **Step 7: Push exact head and independently inspect artifacts**

Require all applicable workflows on exact head. Download artifacts; verify GitHub digest,
safe ZIP members, parseable TRX, total=executed=passed, non-zero required classes,
helper/module/licence/toolchain/binary identities, catalogue hashes, exact head, and absence
of model/OpenVINO payloads, paths, native device IDs, usernames, machine names, tokens or
credentials.

- [ ] **Step 8: Manual boundary review and PR update**

Confirm one estimator, one static router, one shared process foundation, one owner per
component, one fingerprint schema, one helper protocol, no Decision 5 formula duplicate,
no Decision 6 verdict, no Decision 7 ranking, no Decision 8 ladder and truthful runtime
non-claim. Update PR with exact SHAs, gate/spike dispositions, ownership, supply chain,
security/privacy, route matrix, tests, calibration, limitations, rollback and review focus.
Keep draft until exact-head verification and independent review pass.

---

## Stop conditions

Stop the affected task for missing/duplicate predecessor contracts; feature-private-only
process runner; absent guarded Compatibility access; unstable/unbounded helper output;
network/listen/token/URL/RPC/download route; path leakage; split-shard discovery; missing or
duplicate component ownership; ambiguous shared topology; non-deterministic fingerprint;
unchecked range overflow; missing Decision 5 calculator; standard/Turbo KV coexistence;
incomplete OpenVINO package; ambiguous profile applicability; runtime fallback; zero test
discovery; failed packaged CI; or failed artifact privacy/integrity.

Never bypass a stop with a file-size multiplier, zero for unknown, global overhead constant,
nearest route/format, duplicate DTO, dynamic dictionary contract, raw third-party JSON,
unreviewed moving dependency or deletion of failure evidence.

## Self-review

- [x] Every approved design section maps to a task.
- [x] Process reuse, privacy-safe route identity and integration-base gate are binding.
- [x] Decision 4 does not own formulas, reserve, ranking, candidate generation or runtime proof.
- [x] Risk-first helper qualification precedes provider coupling.
- [x] Core GGUF delivery is independent; OpenVINO is a second slice.
- [x] Missing evidence is typed and shared memory is counted once.
- [x] TurboQuant atomically replaces the complete standard KV set.
- [x] Calibration preserves underprediction and rejects failed/fallback runs.
- [x] RED precedes GREEN; non-zero discovery and exact-head evidence are mandatory.
- [x] No unresolved placeholder or vague deferred implementation instruction remains.

## Engineering basis

- **Systems Engineering: Principles and Practice**, Chapters 6–8, 11–13, 16–17 and 22:
  allocation, alternatives, risk-first prototyping, integration and traceable evaluation.
- **Engineering Software Products**, Chapters 4 and 7–10: modular architecture,
  security/privacy, reliable input, automated testing and DevOps evidence.
- **Fundamentals of Software Architecture**, Chapters 2–8, 21–22 and 26–27:
  trade-offs, cohesion/coupling, fitness functions, ADRs and architecture risk.
- **AI Engineering**, Chapters 3–4 and 9–10: component evaluation, inference resource
  metrics, optimisation and observability.
- **Designing Secure Software**, Chapters 1–4, 6–7 and 10–13: trust boundaries,
  attack-surface reduction, allowlists, untrusted input and secure development.
- **The Art of Unit Testing**, Chapters 1 and 7–10: trustworthy tests, TDD and test recipes.
- **Refactoring**, Chapters 1–4: small behaviour-preserving changes protected by tests.
- **Code Complete**, Chapters 3, 5, 8, 20–25, 28–29 and 32–34: prerequisites,
  information hiding, defensive code, measurement and configuration control.
- **Why Programs Fail**, Chapters 3–6, 8–10 and 15–16: reproduce, isolate, observe and
  verify corrections.
- **Build desktop apps for Windows**: preserve native WinUI 3, Windows App SDK,
  packaging and app-container testing.

## Execution handoff

```text
Task 0 → stop unless integration gate passes
Task 1 → independent architecture/security review of Pass/Partial/Fail
Tasks 2–5 → standard-GGUF core
Task 6 → only after Decisions 5 and 8
Task 7 → separately gated OpenVINO slice
Tasks 8–9 → calibration, CI, evidence and review closure
```

Use a fresh implementer per task, followed by specification review and code-quality review.
