# Model–Hardware Compatibility Decision 4 — Resource-Estimator Architecture

- **Decision:** 4 of 8
- **Status:** Final design candidate; written review pending
- **Decision date:** 2026-08-20
- **Selected approach:** Application-owned estimator boundary, a pinned local `gguf-parser-go` helper for proven standard-GGUF calculations, and project-owned TurboQuant/OpenVINO extensions
- **Primary requirement:** `F-M08`
- **Implementation status:** Not implemented
- **Upstream:** Decisions 1–3, Model Inspection, Hardware Inspection
- **Downstream:** Decisions 5–8 and Runtime Verification

---

## 1. Final decision

The application owns one stable boundary:

```text
IConfigurationResourceEstimator
```

It estimates the resources required by **one exact model/runtime configuration** and
returns component-level evidence. It never decides whether the computer is safe, never
chooses another configuration, and never claims that the model has run.

```text
Trusted model handoff
        +
resource-topology projection
        +
exact configuration
        ↓
ConfigurationResourceEstimator
        ├── standard GGUF + llama.cpp provider
        ├── optional TurboQuant KV overlay
        ├── OpenVINO GenAI provider
        ├── application/runtime-footprint provider
        └── storage-footprint provider
        ↓
immutable ConfigurationResourceProfileResolution
```

The full GPUStack platform is rejected. The only GPUStack-originated component under
consideration is the separately maintained `gguf-parser-go` library, placed behind a
small project-owned Windows helper and project-owned DTOs.

### Decision boundaries

```text
Decision 4
→ provider architecture, component evidence, provenance and calibration boundary

Decision 5
→ exact formulas, units, checked arithmetic, phase composition and rounding

Decision 6
→ OS allowance, safety reserve and fit thresholds

Decision 7
→ ranking and mode objectives

Decision 8
→ supported configuration matrix and candidate generation

Runtime Verification
→ proof that a selected configuration loads and generates
```

Decision 4 advances `F-M08`, but it cannot verify the full requirement by itself.
Complete `F-M08` evidence also requires Decisions 5 and 6 plus matched
predicted-versus-measured results.

---

## 2. Architectural rules

1. **One owner per fact.** Model facts come from Model Inspection; machine facts come
   from Hardware Inspection; Decision 4 estimates configuration requirements.
2. **Exact configuration only.** Every memory-relevant setting is explicit.
3. **Application-owned contracts.** Third-party types and display output never cross the
   application boundary.
4. **No silent approximation.** An unsupported route returns a typed result, not the
   nearest format, offload count or model-family guess.
5. **Evidence quality is explicit.** A successful helper exit is not calibration.
6. **Missing mandatory evidence fails closed.** Unknown never means zero.
7. **No double counting.** Shared GPU memory remains system-backed memory.
8. **Replaceable infrastructure.** The GGUF helper can be replaced without changing
   Compatibility contracts.
9. **Offline native process model.** No local HTTP server, dashboard, daemon, container
   platform or cloud request is introduced.
10. **Time-boxed scope.** Only routes admitted by Decision 8 receive production claims.

---

## 3. Input contract

```csharp
Task<ConfigurationResourceProfileResolution> EstimateAsync(
    ConfigurationResourceEstimationRequest request,
    CancellationToken cancellationToken);
```

Progress remains an orchestration concern. The low-level estimator has no progress
callback and no UI dependency.

```text
ConfigurationResourceEstimationRequest
├── Model : ModelInspectionHandoff
├── ResourceTopology : ResourceTopologyProjection
└── Configuration : ResourceEstimationConfiguration
```

### 3.1 Model boundary

`ModelInspectionHandoff` supplies trusted inspected facts and content identity. Temporary
read-only access comes through Decision 1's guarded artifact service.

Before parsing:

```text
artifact exists
full SHA-256 still matches
size/identity checks remain valid
stable read-only access is held
```

Version one supports a **single-file GGUF**. Split GGUF is `NotEstablished` until Model
Inspection owns an immutable package identity containing every authorised shard hash.
The estimator must not trust filename-based discovery of adjacent files.

OpenVINO estimation starts only after Model Inspection owns an immutable OpenVINO
package identity for every required XML, BIN and auxiliary artifact. Decision 4 does not
invent a competing package model.

### 3.2 Resource topology

`ResourceTopologyProjection` is an immutable Compatibility-owned projection of the
canonical `HardwareSnapshot`. It contains only allocation-mapping facts:

```text
target device identities
memory-pool kinds
dedicated-versus-shared topology
unified/shared-memory relationships
supported route identifiers
```

It excludes current available RAM/VRAM. Providers cannot read live system state, LLM
Fit DTOs, DXGI structures or Hardware Inspection presentation models.

### 3.3 Exact configuration

```text
ResourceEstimationConfiguration
├── RuntimeRoute
├── ContextTokens
├── BatchSizing
├── KvCacheSelection
├── DevicePlacement
└── RuntimeMemoryOptions
```

Material fields include:

```text
runtime family and exact version/commit
backend and target device route
context window
logical batch, micro-batch and parallel sequences
K/V formats and cache placement
TurboQuant format/version where applicable
OpenVINO KV precision where applicable
CPU-only, partial or full offload
exact GPU-layer/tensor placement
mmap, Flash Attention and KV offload
OpenVINO cache/scheduler settings
```

Runtime options use closed typed variants, not a string dictionary, reflection bag or
caller-supplied command fragment.

The request never carries:

```text
absolute model path
current available memory
reserve or fit threshold
ranking weights
provider selection
UI strings
native handles/process objects
raw third-party output
```

---

## 4. Result contract

```text
ConfigurationResourceProfileResolution
├── Status
├── Profile?
├── FailureReason?
└── SanitisedDiagnostic?
```

```text
Unspecified = 0
Established = 1
NotEstablished = 2
OperationalFailure = 3
Cancelled = 4
```

Invariants:

```text
Established
→ complete profile; no failure reason

NotEstablished
→ no profile; stable evidence reason

OperationalFailure
→ no profile; stable operational reason; optional sanitised diagnostic

Cancelled
→ no profile; no fabricated failure
```

Programming-contract defects throw synchronously before work begins.

### 4.1 Profile

```text
ConfigurationResourceProfile
├── ConfigurationFingerprint
├── ModelArtifactIdentity
├── RuntimeRouteIdentity
├── ContextTokens
├── Components
├── OverallEvidenceGrade
├── CalculationPolicyIdentity
├── ProviderImplementationIdentities
├── HelperToolIdentity?
├── CalibrationProfileIdentity?
├── AssumptionCodes
├── WarningCodes
└── OptionalComponentGaps
```

All collections are immutable. Assumptions and warnings are stable typed codes; the
Presentation layer owns beginner-readable/localised wording.

### 4.2 Component

```text
ResourceComponentEstimate
├── ComponentKind
├── ResourceTarget
├── LifecyclePhase
├── StructuralBytes?
├── PredictedBytes
├── CalibrationAdjustmentBytes?
├── EvidenceGrade
├── SourceIdentity
├── CalibrationReference?
└── AssumptionCodes
```

When calibration adjusts a structural value, the original value and adjustment remain
visible. The system never collapses them into an unexplained total.

Mandatory component kinds:

```text
ModelWeights
KvCache
ComputeAndScratch
RuntimeBootstrap
ApplicationFootprint
LoadOrCompileTransient
StorageExistingArtifact
StorageAdditionalArtifact
StorageTemporary
```

Resource targets:

```text
SystemMemory
DedicatedVideoMemory
SharedSystemMemoryForGpu
DeviceLocalMemory
Storage
```

Lifecycle phases:

```text
Startup
LoadOrCompile
Prefill
Decode
SteadyState
Conversion
```

Decision 5 owns phase overlap and peak composition. Providers preserve phase evidence
rather than blindly summing all allocations.

### 4.3 Shared-memory rule

`SharedSystemMemoryForGpu` is backed by system memory. It is not an extra independent
pool. A helper's generic `VRAM` value is mapped through trusted topology:

```text
discrete dedicated allocation
→ DedicatedVideoMemory

UMA/integrated-GPU allocation
→ SharedSystemMemoryForGpu

ambiguous mapping
→ NotEstablished
```

### 4.4 Evidence grade

Availability and quality are separate concepts. Established components use:

```text
Unspecified = 0
StructuralEstimate = 1
CalibratedMatched = 2
MeasuredExact = 3
```

A missing mandatory component causes a `NotEstablished` resolution; it is not hidden as
a weak grade inside a successful profile. Overall grade is the weakest mandatory
component grade and is never averaged.

`MeasuredExact` requires the same material configuration. `CalibratedMatched` requires a
versioned applicable profile. A complete uncalibrated calculation remains
`StructuralEstimate`; it must not be presented as measured.

### 4.5 Identity ownership

Keep these identities distinct:

```text
calculation/policy identity
→ approved estimator algorithm version; reuse Decision 2's policy identity model

provider implementation identity
→ application adapter/provider version

helper tool identity
→ upstream source commit, protocol, build and binary digest

calibration profile identity
→ applicability and measured-error evidence
```

Decision 4 does not create a duplicate generic policy-version type. The request/page
cannot choose any estimator identity.

---

## 5. Provider architecture

The application service:

1. validates the request;
2. selects one base provider from a closed allowlist;
3. obtains base components;
4. applies one approved overlay when required;
5. adds application/runtime and storage components;
6. validates completeness, topology mapping and provenance;
7. returns one normalised profile.

Internal interfaces remain small:

```text
IBaseResourceEstimatorProvider
IResourceEstimateOverlay
IRuntimeFootprintProvider
IStorageFootprintProvider
```

There is no dynamic plugin discovery.

### 5.1 Standard GGUF + llama.cpp

```text
GgufLlamaCppResourceEstimatorProvider
```

A project-owned Windows x64 helper embeds a reviewed `gguf-parser-go` commit. The
current spike candidate is:

```text
a5d9227ae50725494c827c7810c662e102143293
```

It is not the production pin until the spike, licence review and supply-chain review pass.

The adapter maps explicit values for:

```text
context
batch/micro-batch
parallel sequences
GPU layers/main device/tensor split
K/V cache types
mmap
Flash Attention
KV offload
```

It may contribute:

```text
CPU/GPU model-weight allocations
standard llama.cpp KV cache
input/compute/output buffers
runtime bootstrap/tool footprint
placement evidence
```

The upstream platform-footprint default is explicitly zeroed where proven. Otherwise it
remains a separate identified component so Decision 6 does not count it again as an OS
allowance.

The provider never falls back to file-size multipliers, nearest KV format, nearest offload
count or implicit model context. Experimental TPS prediction is out of scope.

### 5.2 TurboQuant KV overlay

```text
TurboQuantKvResourceEstimateOverlay
```

The base GGUF provider owns weights, placement and ordinary buffers. The overlay
replaces the complete standard KV component set only:

```text
standard KV components removed
→ complete TurboQuant KV component set inserted
→ no standard/TurboQuant KV coexistence
→ non-KV values and provenance retained
```

Initial candidates are only the genuinely implemented/calibrated routes:

```text
turbo4
turbo3
turbo2
```

QJL remains `NotEstablished` until an exact implementation and calibration route is
verified. It is never relabelled as `turbo3` or another format.

Decision 5 must represent more than nominal bits:

```text
quantised K/V payload
scale/codebook metadata
rotation/preconditioning metadata
alignment and block padding
per-layer/per-head metadata
runtime auxiliary buffers
```

Changing TurboQuant format must not change non-KV model-weight evidence.

### 5.3 OpenVINO GenAI

```text
OpenVinoGenAiResourceEstimatorProvider
```

The GGUF provider is never used for OpenVINO IR. The provider combines exact package
sizes, inspected architecture, runtime/device structural rules and versioned calibration.
It represents:

```text
weight/package footprint
compiled-model/device footprint
KV cache by precision
prefill/decode work buffers
CPU/GPU/NPU runtime overhead
prefix-cache/scheduler allocations
load/compile transient
```

Memory-relevant scheduler settings are fingerprinted, including cache size/block count,
maximum batched tokens, maximum sequences, prefix caching, prompt limit and KV
precision.

```text
supported route + matching calibration
→ CalibratedMatched

complete structural route without matching calibration
→ StructuralEstimate

missing package identity, model facts or supported device route
→ NotEstablished
```

XML/BIN file size alone is never treated as peak memory. OpenVINO is the second slice
after the standard GGUF/TurboQuant core.

### 5.4 Application/runtime footprint

```text
ApplicationRuntimeFootprintProvider
```

It supplies measured versioned profiles for resources that coexist with inference:

```text
WinUI baseline
production inference-worker baseline
runtime and device initialisation
model-load transient
tokenisation/IPC/result buffers
```

The short-lived estimator helper is analysis-time machinery. Its memory is constrained
for safety but excluded from the predicted inference peak.

Profile keys include app version, runtime commit, backend/device route, Windows
architecture and model scale/architecture where evidence shows it matters. A missing
mandatory core profile is `NotEstablished`, not zero.

### 5.5 Storage

```text
ConfigurationStorageFootprintProvider
```

It distinguishes:

```text
already-present imported bytes
already-installed versus additionally-required runtime/helper bytes
additional converted/generated artifacts
known cache files
known temporary conversion/compilation space
```

Existing files use exact sizes. Unknown temporary space is `NotEstablished`; no
unexplained multiplier is allowed.

---

## 6. Provider routing and failures

Closed routing table:

```text
GGUF + pinned llama.cpp + supported standard KV
→ GGUF provider

GGUF + pinned TurboQuant fork + approved TurboQuant KV
→ GGUF provider + TurboQuant overlay

OpenVINO package + pinned OpenVINO GenAI + supported device route
→ OpenVINO provider

anything else
→ NotEstablished / UnsupportedEstimatorRoute
```

There is no nearest-route matching and the UI cannot override provider identity.

Stable evidence reasons include:

```text
UnsupportedEstimatorRoute
UnsupportedModelArchitecture
UnsupportedRuntimeVersion
UnsupportedKvCacheFormat
UnsupportedDevicePlacement
ModelEvidenceIncomplete
ModelArtifactChanged
ModelArtifactBusyOrMutable
RequiredCalibrationProfileUnavailable
RuntimeFootprintProfileUnavailable
MandatoryComponentUnavailable
NumericRangeUnsupported
```

Stable operational reasons include:

```text
EstimatorHelperUnavailable
EstimatorHelperIntegrityFailure
EstimatorHelperLaunchFailure
EstimatorHelperHandshakeFailure
EstimatorHelperTimedOut
EstimatorHelperProtocolViolation
EstimatorHelperOutputTooLarge
EstimatorHelperMalformedOutput
EstimatorHelperExitedUnexpectedly
GuardedArtifactAccessFailure
```

Diagnostics exclude absolute paths, command lines, raw stdout/stderr, usernames,
machine names, tokens and credentials.

---

## 7. Secure GGUF helper boundary

The helper is:

```text
short-lived
Windows x64
offline
no UI or shell
no server/port/dashboard
one request per process in version one
```

### 7.1 Protocol

Use a fixed framed-JSON or JSON-lines protocol over inherited pipes:

```text
Hello
→ helper identity, schema version, upstream commit

EstimateRequest
→ private locator + typed options over stdin

EstimateResponse
→ project-owned schema

Terminal
→ one final state
```

The current upstream API is path-based and auto-completes split shards. Version one
therefore uses this controlled single-file route:

1. host opens the authorised GGUF read-only while denying write/delete sharing;
2. host retains the handle for the whole helper run;
3. host hashes the locked artifact;
4. private locator is sent through stdin, never command line;
5. helper returns observed identity/size;
6. host performs final identity verification before accepting output;
7. adjacent split shards are rejected.

Failure to establish the stable boundary returns `ModelArtifactBusyOrMutable`. A future
reader/handle-based upstream patch is allowed only if the spike proves it is lower risk
and easy to maintain.

### 7.2 Process containment

Reuse the protected worker foundation:

```text
creation-time Job Object assignment
exact standard/artifact handle allowlist
no unrelated handle inheritance
JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
active-process limit
bounded process memory
bounded stdout/stderr
timeout and cancellation
process-tree termination
no shell window
```

### 7.3 Supply chain

Record and verify:

```text
upstream repository and exact commit
diff/review disposition
MIT licence and notices
Go toolchain and go.sum
reproducible build workflow
helper protocol version
binary SHA-256
```

No runtime “latest” download is allowed. The packaged helper digest is checked before
launch.

### 7.4 Untrusted input

The helper enforces bounded counts, strings, arrays and output; checked size arithmetic;
read-only access; no URL/token/RPC mode; panic-to-controlled-failure handling; and path
redaction. A malformed GGUF may fail estimation but must not crash the WinUI process.

Version one does not claim an OS-level network sandbox. It instead ships a minimal helper
with no production network code, no network modes and a reviewed process boundary.

---

## 8. Configuration fingerprint

Every estimate/calibration record uses a deterministic, culture-independent fingerprint
over:

```text
model content identity and format
runtime/helper/provider identities
backend/device route
context
batch/micro-batch/parallel sequences
K/V format and placement
offload/tensor split
mmap, Flash Attention and KV offload
TurboQuant format/version
OpenVINO scheduler/cache settings
```

It excludes paths, timestamps, current available memory, UI state and display strings.
Changing any material field creates a different configuration.

---

## 9. Accuracy and calibration

A development `ResourceMeasurementHarness` produces calibration evidence. The
production UI consumes curated profiles, not raw experiment logs.

For each exact case it captures separately:

```text
process-tree commit/private memory
process-tree working set
runtime-reported weights, KV and compute allocations
dedicated GPU-memory peak
shared GPU-memory peak
requested and actual backend/offload
load, prefill, decode and steady-state phases
terminal outcome
```

Working set, commit/private bytes and GPU counters are never summed without an explicit
Decision 5 composition rule.

### 9.1 Controlled procedure

Record:

```text
app/runtime/helper/provider versions
Windows build and driver version
model SHA-256
configuration fingerprint
baseline resource state
cold load
prefill
decode
steady state
clean shutdown
```

Core cases use one cold run and at least three repeated measured runs where practical.
Retain the maximum peak for safety analysis and median for stability analysis. Failed,
cancelled, fallback or OOM runs are failure evidence, not successful calibration points.

### 9.2 Error evidence

```text
CalibrationErrorRecord
├── PredictedBytes
├── MeasuredPeakBytes
├── SignedErrorBytes
├── AbsoluteErrorBytes
├── PercentageError
├── Underprediction
├── ComponentErrors
└── CaseIdentity
```

Underprediction is explicit because it can create a false-safe result.

```text
ResourceCalibrationProfile
├── ProfileIdentity
├── RouteKey
├── FormulaVersion
├── SampleCount
├── CalibrationCaseIds
├── ValidationCaseIds
├── MaximumObservedUnderpredictionBytes
├── MaximumObservedUnderpredictionPercent
├── ErrorSummary
└── ApplicabilityRules
```

Use separate validation cases where the matrix is large enough. A held-out miss outside
the documented envelope downgrades or replaces the profile.

### 9.3 Admission rules

`CalibratedMatched` requires:

1. exact model/runtime/configuration identities;
2. component-level predictions;
3. matched measured peaks;
4. retained raw evidence;
5. documented sample coverage;
6. underprediction statistics;
7. a validation case where practical;
8. no unresolved mandatory component;
9. no silent runtime fallback.

No upstream “accurate within X MiB” statement is copied into the product as proof.

---

## 10. Bounded first-release evidence matrix

Decision 8 may expose only routes for which Decision 4 has an estimator path and the
required evidence grade.

### Standard GGUF core

```text
Granite 3B
→ Decision 3 baseline + one higher approved context
→ CPU-only + one verified GPU/offload route
→ F16 KV + Q8_0 KV

Granite 8B
→ Decision 3 baseline
→ every standard route/format visible in version one
```

Add full/partial offload cases only when those placements are user-visible candidates.

### TurboQuant core

For every format admitted by Decision 8:

```text
Granite 3B baseline + one higher context where runnable
Granite 8B only for routes/formats visible in version one
same weight file and matched standard-KV comparison
same batch/offload settings
```

Every format needs its own evidence. Do not infer `turbo4` or `turbo2` from one
`turbo3` compression ratio.

### OpenVINO second slice

Calibrate only the explicit model/device/precision routes admitted by Decision 8. Until
implemented, other OpenVINO routes are `NotEstablished` rather than confidently guessed.

---

## 11. Risk-first Windows spike

Before production integration, test the candidate `gguf-parser-go` commit on Windows x64.

The spike must prove:

```text
source/licence/toolchain pin recorded
reproducible helper build and binary digest
protocol handshake and strict schema
locked exact-file route
concurrent mutation and adjacent-shard rejection
context/batch/KV/mmap/FA/KV-offload/GPU-layer sensitivity
platform-footprint isolation
Job Object containment
timeout/cancellation/output bounds
malformed-fixture safe failure
matched llama.cpp prediction-error table
no silent fallback or network/server mode
```

Disposition:

```text
Pass
→ use helper for proven standard-GGUF components

Partial
→ use only proven components; project owns the missing components

Fail
→ keep the application contract; implement a narrow app-owned provider
```

The spike never approves full GPUStack integration.

---

## 12. Testing strategy

### Pure contract tests

```text
immutable requests/profiles
explicit enum zero values
resolution invariants
checked byte values
fingerprint determinism/culture independence
shared-memory topology
mandatory-component completeness
evidence-grade aggregation
typed assumption/warning codes
TurboQuant component-set replacement
```

### Router tests

```text
standard GGUF → GGUF provider
TurboQuant → GGUF + exactly one overlay
OpenVINO → OpenVINO only
unsupported → NotEstablished
no nearest-route or UI-selected provider
```

### Helper protocol/security tests

```text
valid handshake/result
wrong identity/schema
malformed/duplicate terminal output
oversized stdout/stderr
timeout/cancellation/unexpected exit
child-process attempt
binary digest mismatch
path redaction
artifact mutation
adjacent-shard rejection
```

### Integration and metamorphic tests

Use generated minimal GGUF fixtures plus controlled Granite fixtures. Valid invariants
include:

```text
larger context cannot reduce logical KV payload
more offloaded layers cannot increase CPU-resident tensor payload
logical tensor payload stays constant when only placement changes
TurboQuant format change cannot alter non-KV tensor payload
KV placement preserves logical cache capacity; any allocation difference must be
explicit alignment/implementation overhead
same request produces a value-equivalent profile
```

### Calibration tests

```text
exact key matching
profile rejection after material field changes
underprediction calculation
calibration/validation separation
downgrade outside applicability envelope
failed runtime cannot create successful calibration
```

The final implementation preserves the existing packaged WinUI 3/Windows App SDK test
route and proves non-zero execution of Decision 4 test classes.

---

## 13. Privacy, versioning and change control

Production evidence may retain model hash, configuration fingerprint, provider versions,
component bytes, grades and stable failure codes. It must not retain paths, account or
machine names, command lines, raw logs, prompts, credentials or tokens.

Version independently:

```text
profile contract and assumption-code catalogue
fingerprint schema
helper protocol and binary
GGUF provider
TurboQuant overlay
OpenVINO provider
runtime-footprint profiles
calibration profiles
Decision 5 formula policy
Decision 6 safety policy
```

A provider/runtime update invalidates calibration unless compatibility is demonstrated.
Dependency updates require licence and source review, reproducible build, fixture
regression, matched calibration rerun, digest update and evidence update.

---

## 14. Time-boxed delivery

### Core slice

```text
application contracts and router
protected GGUF helper spike
standard GGUF provider
TurboQuant overlay for validated formats
application/runtime and storage profiles
calibration harness/evidence
```

### Second slice

```text
OpenVINO provider for explicitly admitted CPU/GPU/NPU routes
```

### Deferred

```text
full GPUStack platform
network/remote inspection
dynamic plugins
arbitrary model families and multimodal projectors
multi-host/RPC estimation
unvalidated QJL
TPS prediction
automatic formula learning
```

This keeps the implementation achievable without weakening honesty or safety.

---

## 15. Definition of done

Decision 4 is planning-complete when:

1. this written design is approved;
2. Decision 5–8 ownership boundaries are accepted;
3. every intended route has a provider or typed unsupported outcome;
4. evidence quality and availability are separate;
5. shared-memory double counting is prevented;
6. the helper trust boundary and spike disposition are executable;
7. calibration and underprediction evidence are defined;
8. first-release scope is bounded;
9. Decision 4's partial contribution to `F-M08` is explicit.

Implementation is complete when:

1. contracts are immutable and application-owned;
2. provider routing is allowlisted and deterministic;
3. the pinned helper passes the Windows spike;
4. source, licence, toolchain, protocol and digest are recorded;
5. the locked artifact hash is revalidated before accepting output;
6. standard GGUF profiles exist for admitted routes;
7. TurboQuant replaces only complete KV evidence;
8. unsupported QJL is `NotEstablished`;
9. OpenVINO is implemented only for admitted package/device routes or is honestly unavailable;
10. mandatory runtime/application/storage components exist;
11. every component records structural/predicted bytes, target, phase, source, grade and typed assumptions;
12. all arithmetic uses checked byte values;
13. timeout, cancellation, containment, integrity and output-limit tests pass;
14. matched calibration records include one-sided underprediction;
15. no diagnostic leaks sensitive paths or identity;
16. packaged CI discovers and passes Decision 4 tests;
17. evidence references the exact implementation SHA;
18. no Decision 5–8 policy is hidden in provider code.

---

## 16. Rejected alternatives and principal risks

Rejected:

```text
app-owned estimator from scratch for every route
full GPUStack server/platform
human-readable CLI table parsing
LLM Fit catalogue values as exact imported-GGUF evidence
file-size multiplier as peak memory
silent nearby-format/configuration fallback
one universal accuracy claim
```

| Risk | Control |
|---|---|
| Upstream estimator drift | Pin source/binary and version calibration |
| Corrupt/malicious GGUF | Locked read boundary, bounded parser, checked arithmetic, timeout |
| False-safe underprediction | Record one-sided error; Decision 6 applies conservative policy |
| Shared-memory double counting | Explicit system-backed target |
| Hidden third-party totals | Consume component output only |
| TurboQuant metadata omitted | Separate overlay and matched calibration |
| OpenVINO uncertainty | Structural grade or NotEstablished |
| Calibration overfitting | Applicability rules and validation cases |
| Scope overrun | GGUF core first; OpenVINO second |
| Path/race exposure | Private stdin, write-denying handle, hash checks, redaction |
| Binary substitution | Packaged digest verification |
| Runtime fallback | Requested/actual route comparison invalidates mismatches |
| Overlapping counters | Preserve semantics; Decision 5 controls composition |

---

## 17. Primary technical sources

Reviewed 2026-08-20:

1. [`gguf-parser-go` candidate commit](https://github.com/gpustack/gguf-parser-go/tree/a5d9227ae50725494c827c7810c662e102143293), including [`EstimateLLaMACppRun`](https://github.com/gpustack/gguf-parser-go/blob/a5d9227ae50725494c827c7810c662e102143293/file_estimate__llamacpp.go).
2. Its path-based, shard-completing [`ParseGGUFFile`](https://github.com/gpustack/gguf-parser-go/blob/a5d9227ae50725494c827c7810c662e102143293/file.go), which motivates the locked single-file boundary.
3. The pinned [MIT licence](https://github.com/gpustack/gguf-parser-go/blob/a5d9227ae50725494c827c7810c662e102143293/LICENSE).
4. OpenVINO GenAI [`SchedulerConfig`](https://docs.openvino.ai/2026/api/genai_api/_autosummary/openvino_genai.SchedulerConfig.html).
5. Microsoft [Job Objects](https://learn.microsoft.com/windows/win32/procthread/job-objects) and [process security/access rights](https://learn.microsoft.com/windows/win32/procthread/process-security-and-access-rights).

The candidate GGUF commit includes substantial sliding-window KV-estimation corrections.
That is direct evidence that upstream estimator behaviour can change and must be pinned,
reviewed and recalibrated. These sources establish capabilities, not project accuracy.

---

## 18. Engineering basis

- **Systems Engineering: Principles and Practice**, Chapters 6–8, 11–13 and 17:
  requirements allocation, alternatives, risk-first prototyping and traceable evaluation.
- **Fundamentals of Software Architecture**, Chapters 2–8, 21–22 and 26–27:
  isolate volatile technology, minimise coupling, measure characteristics and record risk.
- **Engineering Software Products**, Chapters 4 and 7–10:
  modular product architecture, reliable input handling, testing and controlled delivery.
- **AI Engineering**, Chapters 3–4 and 9:
  component evaluation, explicit metrics and empirical inference optimisation.
- **Designing Secure Software**, Chapters 2–4, 6–7 and 10–13:
  trust boundaries, least information, untrusted input, fail-secure behaviour and pinning.
- **The Art of Unit Testing**, Chapters 7–10:
  trustworthy tests and a balanced unit/component/integration/system recipe.
- **Code Complete**, Chapters 3, 5, 8, 22, 25 and 28:
  prerequisites, information hiding, defensive contracts, measurement and configuration control.
- **Build desktop apps for Windows**:
  preserve the WinUI 3, Windows App SDK and native packaged process model.

---

## 19. Self-review

- No unfinished placeholder or unnamed provider remains.
- Decision 4 estimates requirements, not current capacity or fit.
- Decision 5 owns calculations/peak composition; Decision 6 owns reserve/thresholds;
  Decision 7 owns ranking; Decision 8 owns candidate admission/generation.
- Evidence availability and evidence quality are separate.
- Estimator-helper memory is not charged to inference.
- Shared GPU memory is system-backed.
- Split GGUF and OpenVINO package identity are not invented here.
- TurboQuant changes only KV evidence.
- No silent fallback, runtime guarantee or universal accuracy claim remains.
- The current path-based upstream API has an explicit race/shard mitigation.

---

## 20. Final summary

```text
exact trusted model
        +
trusted resource topology
        +
exact runtime configuration
        ↓
allowlisted, versioned estimator providers
        ↓
component-level immutable resource profile
        ├── structural and predicted bytes
        ├── target pool and phase
        ├── source/provenance
        ├── evidence grade
        └── calibration reference
```

Decision 4 gives the compatibility feature every estimator and evidence boundary it
needs without turning one component into a universal parser, fit classifier, ranking
engine or runtime verifier.
