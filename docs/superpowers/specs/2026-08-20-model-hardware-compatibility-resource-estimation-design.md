# Model–Hardware Compatibility Decision 4 — Resource-Estimation Architecture

- **Decision:** 4 of 8
- **Status:** Approved and closed at planning level
- **Decision date:** 2026-08-20
- **Scope:** Resource-estimator sources, provider boundaries, component coverage, evidence grades, calibration and secure local-helper integration
- **Implementation status:** Planned, not yet implemented
- **Decision identity:** `ResourceEstimation / resource-estimation-v1`
- **Predecessors:** Decisions 1–3
- **Downstream owners:** Decisions 5–8 and Runtime Verification

---

## 1. Final decision

The application will own one stable resource-estimation boundary:

```text
IConfigurationResourceEstimator
```

That boundary estimates the resource consumption of **one exact configuration**. It
does not generate candidate configurations and it does not decide whether the estimate
is safe for the current computer.

Version one uses an application-owned provider architecture:

```text
standard GGUF + llama.cpp
→ pinned project-owned local helper
→ helper imports a reviewed gguf-parser-go commit

TurboQuant KV cache
→ project-owned KV replacement provider

OpenVINO GenAI
→ project-owned structural/profile provider

application/runtime overhead
→ project-owned measured profile provider

persistent/temporary storage
→ project-owned storage provider
```

The complete GPUStack serving platform is rejected. The useful standalone
`gguf-parser-go` library is admitted only through a small project-owned, local-only
helper after a bounded Windows spike proves its contract.

The application owns:

```text
the request and result contracts
configuration identity
component names and units
memory-domain normalisation
provider selection
component-coverage rules
evidence grading
calibration records
failure mapping
security controls
all final compatibility semantics
```

Third-party code may supply structural estimates. It never owns the final fit result.

---

## 2. The question Decision 4 answers

Decision 4 answers:

> How does Model–Hardware Compatibility obtain a complete, traceable resource
> estimate for one exact model/runtime/device configuration?

It does not answer:

```text
What exact formula or rounding rule should each project-owned calculator use?
What RAM/VRAM/OS reserve makes a configuration safe?
Which alternative configurations should be generated?
Which safe candidate should be ranked first?
Will the configuration actually initialise and run?
```

Those remain separate:

| Concern | Owner |
|---|---|
| Exact arithmetic, unit conversion, overflow and peak-composition rules | Decision 5 |
| OS allowance, safety reserve and fit thresholds | Decision 6 |
| Ranking and selection policy | Decision 7 |
| Supported candidate matrix and candidate generation | Decision 8 |
| Actual loading and inference proof | Runtime Verification |

This separation prevents one estimator class from becoming an untestable combination
of parsing, arithmetic, safety policy, candidate generation and runtime execution.

---

## 3. Requirement traceability

Decision 4 provides the architecture required by:

```text
F-M08
→ estimate peak memory for a supported configuration

HE-03 / WP20
→ transparent component-level estimator

HE-04 / WP22
→ matched predicted-versus-measured calibration

F-M10 / HE-05
→ later candidate generation may retain only complete supported configurations
```

F-M08 eventually requires the user-facing explanation to show:

```text
weights
KV cache
runtime/application overhead
OS allowance
safety reserve
matched measured error
```

Decision 4 owns the estimate sources and component evidence. Decision 6 later adds
OS allowance and safety reserve; therefore those two values must not be hidden inside
a Decision 4 provider.

An empty evidence folder, a formula-only unit test or an upstream accuracy statement
does not verify F-M08. Verification requires matched measurements tied to exact
configuration identities.

---

## 4. Architectural context

The complete flow is:

```text
ModelInspectionHandoff
        +
HardwareInspectionHandoff
        +
one exact ResourceEstimationConfiguration
        ↓
ConfigurationResourceEstimationRequest
        ↓
ConfigurationResourceEstimator
        ↓
ResourceEstimatorProviderPlanner
        ↓
allowlisted providers
        ↓
complete component set
        ↓
ConfigurationResourceEstimationResult
```

Decision 1 remains authoritative for the trusted model and hardware handoffs. The
resource estimator must use `IInspectedModelArtifactAccessService` for exact-file
access; it must not add a second raw-path contract or reopen an arbitrary path from a
page or ViewModel.

The hardware handoff is used only to:

```text
prove the requested device/runtime route exists
identify dedicated, shared or unified memory topology
normalise the target memory domain
retain hardware evidence correlation
```

It is not used to freeze current available RAM. Decision 1 already assigns fresh
available-memory capture to the later safety/classification boundary.

---

## 5. Consumption, availability and safety are different facts

Three concepts must remain separate:

```text
Resource consumption
→ what the exact configuration is expected to allocate or store

Resource availability
→ what the inspected computer and fresh memory snapshot report

Safety policy
→ how much resource must remain unused
```

Decision 4 produces resource-consumption evidence.

Hardware Inspection owns factual hardware capacity and topology.

Decision 6 combines Decision 4 estimates with a fresh memory snapshot and approved
reserves. No Decision 4 provider may return “compatible,” “safe,” “will run,” or
“maximum supported context.”

---

## 6. Application contract

### 6.1 Public application boundary

```csharp
internal interface IConfigurationResourceEstimator
{
    CompatibilityPolicyIdentity Identity { get; }

    Task<ConfigurationResourceEstimationResult> EstimateAsync(
        ConfigurationResourceEstimationRequest request,
        CancellationToken cancellationToken);
}
```

The service is asynchronous because a provider may require full model-artifact
revalidation and a bounded helper process.

There is no provider-level progress interface in version one. The Compatibility
orchestrator maps this operation to its existing stable progress stage. Adding granular
progress that a provider cannot report truthfully is rejected.

### 6.2 Request

```text
ConfigurationResourceEstimationRequest
├── ModelInspection : ModelInspectionHandoff
├── HardwareInspection : HardwareInspectionHandoff
├── Configuration : ResourceEstimationConfiguration
└── Fingerprint : ResourceConfigurationFingerprint
```

The request does not contain:

```text
raw model path
current available RAM
OS allowance
safety reserve
fit threshold
ranking policy
UI text
page or ViewModel state
process objects
native handles
```

### 6.3 Exact configuration, not candidate generation

`ResourceEstimationConfiguration` is the canonical memory-relevant configuration
shape. Decision 8 later embeds or references this value when it creates candidates;
it must not create a second set of context/cache/offload fields.

```text
ResourceEstimationConfiguration
├── LlamaCppResourceEstimationConfiguration
└── OpenVinoGenAiResourceEstimationConfiguration
```

A discriminated shape is selected instead of one large DTO with unrelated nullable
fields.

---

## 7. llama.cpp configuration contract

```text
LlamaCppResourceEstimationConfiguration
├── Runtime : ResourceRuntimeIdentity
├── Backend : LlamaCppBackendKind
├── TargetDeviceId : string
├── ContextTokens : ContextTokenCount
├── LogicalBatchSize : int
├── PhysicalBatchSize : int
├── ParallelSequenceCount : int
├── Offload : LlamaCppOffloadPlan
├── KeyCacheType : LlamaCppKvCacheType
├── ValueCacheType : LlamaCppKvCacheType
├── TurboQuantMode : TurboQuantCacheMode
├── MemoryMapEnabled : bool
├── FlashAttentionEnabled : bool
└── KvCacheOffloadEnabled : bool
```

Version-one invariants:

```text
all counts are positive
physical batch <= logical batch
CPU route has no GPU offload
layer-count offload has a positive explicit count
all-layer offload carries no numeric sentinel such as 999
TurboQuantMode=None permits only standard cache types
TurboQuantMode=Turbo2/Turbo3/Turbo4 requires matching K and V intent
multi-device tensor split is not represented in version one
```

Enumerations reserve zero for `Unspecified`.

Supported standard cache names are not inferred from arbitrary strings. The adapter
uses an allowlist that maps only reviewed application enum values to reviewed
`gguf-parser-go`/llama.cpp types.

---

## 8. OpenVINO GenAI configuration contract

```text
OpenVinoGenAiResourceEstimationConfiguration
├── Runtime : ResourceRuntimeIdentity
├── Device : OpenVinoDeviceKind
├── TargetDeviceId : string
├── ContextTokens : ContextTokenCount
├── WeightPrecision : OpenVinoWeightPrecision
├── KvCachePrecision : OpenVinoKvCachePrecision
├── ParallelSequenceCount : int
└── Scheduler : OpenVinoSchedulerResourceSettings
```

Scheduler settings carry only memory-relevant values:

```text
OpenVinoSchedulerResourceSettings
├── CacheSizeBytes : ResourceByteCount?
├── NumKvBlocks : int?
├── MaxNumBatchedTokens : int?
└── PrefixCachingEnabled : bool
```

Cross-property validation rejects contradictory cache-size and block-count
combinations where the selected OpenVINO contract says they are mutually exclusive.

OpenVINO estimation requires a trusted multi-file artifact/package identity. Until the
Model Inspection boundary supplies that identity, the OpenVINO provider returns
`NotEstablished / RequiredModelEvidenceUnavailable`. It must not accept an arbitrary
XML path plus a guessed BIN path.

---

## 9. Configuration identity

Every estimate is tied to:

```text
ResourceConfigurationFingerprint
```

The fingerprint is SHA-256 over a canonical binary/UTF-8 representation containing:

```text
model content SHA-256 or trusted package-manifest SHA-256
resource-estimation configuration contract version
runtime family
runtime version/commit/build identity
backend and target device route
context
batch and physical batch
parallel sequences
K and V cache types
TurboQuant mode/version
offload plan
mmap
Flash Attention
KV offload
OpenVINO scheduler settings where applicable
```

It excludes:

```text
absolute local path
username
machine name
account identity
UI label
timestamp
current free RAM
```

Property ordering and enum numeric values are fixed by the contract. Culture-sensitive
string formatting is forbidden. Equivalent inputs always produce the same fingerprint.

---

## 10. Component-level result

A single unexplained total is rejected.

```text
ConfigurationResourceEstimate
├── ConfigurationFingerprint
├── Components : IReadOnlyList<ResourceEstimateComponent>
├── Coverage
├── EstimatorIdentity
├── ProviderIdentities
└── AssumptionCodes
```

### 10.1 Component kinds

```text
ResourceEstimateComponentKind
├── Unspecified = 0
├── ModelWeights
├── KvKeyCache
├── KvValueCache
├── RecurrentState
├── ComputeAndScratch
├── RuntimeFootprint
├── ApplicationFootprint
├── PersistentStorage
└── TemporaryStorage
```

`RecurrentState` is explicit because Granite hybrid/recurrent architectures may have
state that is not reduced by a KV-cache codec.

### 10.2 Memory domains

```text
ResourceMemoryDomain
├── Unspecified = 0
├── HostSystemMemory
├── DedicatedDeviceMemory
├── SharedDeviceMemory
├── UnifiedMemory
└── Disk
```

### 10.3 Lifecycle phases

```text
ResourceLifecyclePhase
├── Unspecified = 0
├── ArtifactOpen
├── Conversion
├── ModelLoad
├── Compile
├── Prefill
├── Decode
└── SteadyState
```

A component may appear in more than one phase only where the provider has distinct
evidence for those allocations. Decision 5 later defines which phase combination forms
the estimated peak. Decision 4 does not blindly add allocations that do not coexist.

### 10.4 Byte values

All internal resource values use exact bytes:

```text
ResourceByteCount
└── unsigned 64-bit value
```

MiB/GiB conversion is presentation-only.

Zero is a known value, not “unknown.” Missing evidence is represented through a
typed `NotEstablished` result or an incomplete coverage plan. Checked numeric parsing
and arithmetic are mandatory.

### 10.5 Component shape

```text
ResourceEstimateComponent
├── Kind
├── Bytes
├── MemoryDomain
├── Phase
├── ProviderIdentity
├── EvidenceLevel
└── AssumptionCodes
```

Components contain stable codes, not user-facing prose.

---

## 11. Shared and unified memory correctness

On an integrated GPU, device allocations can be backed by system RAM. The estimator
must not calculate:

```text
host RAM
+
shared GPU memory
```

as though they were independent physical pools.

The normalisation rule is:

```text
DedicatedDeviceMemory
→ counted against dedicated device capacity

SharedDeviceMemory
→ counted against physical system memory exactly once

UnifiedMemory
→ counted against the unified physical pool exactly once

HostSystemMemory
→ counted against physical system memory

Disk
→ counted separately
```

A later safety policy may show both the logical allocation location and the physical
capacity pressure, but the same bytes must never be deducted twice.

A provider that cannot classify the target memory domain for a required component
does not return a complete estimate.

---

## 12. Provider architecture

### 12.1 One public estimator, internal providers

The application exposes one estimator boundary. Internal providers implement:

```csharp
internal interface IResourceEstimatorProvider
{
    ResourceEstimatorProviderIdentity Identity { get; }

    ResourceEstimatorProviderSupport AssessSupport(
        ConfigurationResourceEstimationRequest request);

    Task<ResourceEstimatorProviderResult> EstimateAsync(
        ConfigurationResourceEstimationRequest request,
        CancellationToken cancellationToken);
}
```

The interface is internal because pages, ViewModels and other features must not select
providers.

### 12.2 Static allowlist

Providers are registered through a static composition-root allowlist.

Rejected:

```text
assembly scanning
arbitrary plugin folder
user-supplied estimator DLL
network provider discovery
provider name supplied by UI
```

The estimator planner selects providers from exact route facts.

### 12.3 Complete coverage plan

Before invoking I/O, `ResourceEstimatorProviderPlanner` builds:

```text
ResourceEstimatorCoveragePlan
├── RequiredSlots
└── Assignments
```

A slot is:

```text
component kind
+
memory domain
+
lifecycle phase
```

Rules:

```text
every mandatory slot has exactly one owner
optional inapplicable slots are absent, not zero-filled
two providers may not own the same slot
a provider may explicitly replace another provider's slot only through a
declared replacement rule
```

Missing or duplicate ownership returns:

```text
NotEstablished / ProviderCoverageIncomplete
```

No later total is calculated from an incomplete component set.

---

## 13. Standard GGUF and llama.cpp provider

### 13.1 Selected provider

```text
GgufLlamaCppResourceEstimator
```

uses a project-owned helper:

```text
granite-resource-estimator.exe
```

The helper imports a pinned `gguf-parser-go` library commit. The initial reviewed
spike candidate is:

```text
repository: gpustack/gguf-parser-go
commit: a5d9227ae50725494c827c7810c662e102143293
licence: MIT
```

The spike may select a newer reviewed commit, but the final commit, dependency graph,
licence notice, source archive identity and binary SHA-256 must be frozen before
production admission.

### 13.2 Why the full upstream CLI is not used

The upstream CLI exposes remote URLs, authentication tokens, repository downloads,
RPC endpoints and many unrelated model types.

The project helper instead:

```text
imports the library directly
accepts one bounded JSON request on stdin
supports local GGUF only
returns one bounded project-owned JSON response on stdout
contains no URL/token/Hugging Face/ModelScope/RPC option
contains no dashboard or server
opens no port
```

This reduces attack surface and prevents the application from parsing human display
tables.

### 13.3 Library boundary

The reviewed library exposes:

```text
GGUFFile.EstimateLLaMACppRun(...)
```

and structured per-device fields for:

```text
footprint
weight
KV key
KV value
computation
offloaded layers
context
batch sizes
```

The helper maps only the reviewed fields needed by the project schema.

### 13.4 Component ownership

For a standard llama.cpp route, the provider owns:

```text
ModelWeights
KvKeyCache
KvValueCache
ComputeAndScratch
```

It may expose a tool/runtime bootstrap footprint only as a separately identified
`RuntimeFootprint` component. That footprint must never be silently combined with
the project's application footprint, OS allowance or safety reserve.

The helper is invoked with zero/neutral platform-footprint assumptions when the
library contract supports that safely. Otherwise the returned footprint remains a
distinct component with an assumption code and is reviewed during calibration.

### 13.5 Provider limits

Version one does not expose:

```text
remote RPC
multi-GPU tensor splitting
LoRA/adapters
multimodal projectors
draft/speculative models
remote GGUF
context extension beyond the trusted model limit
```

A request containing an unsupported feature receives
`NotEstablished / UnsupportedConfiguration`.

---

## 14. TurboQuant KV replacement provider

### 14.1 Selected provider

```text
TurboQuantKvResourceEstimator
```

is project-owned.

It replaces only:

```text
KvKeyCache
KvValueCache
```

for an explicitly supported TurboQuant route.

The standard GGUF provider continues to own weights and ordinary compute/scratch
components.

### 14.2 No double counting

For a TurboQuant configuration:

```text
standard GGUF provider
→ suppresses standard K/V component publication

TurboQuant provider
→ publishes replacement K/V components

coverage planner
→ proves one owner for each K/V slot
```

Adding standard KV plus TurboQuant KV is a contract violation.

### 14.3 Initial modes

The current project evidence supports planning for:

```text
Turbo4
Turbo3
Turbo2
```

They remain eligible only where the exact fork/runtime/device route is validated.

The current `turbo2`, `turbo3` and `turbo4` names must not be described as QJL or
PolarQuant unless runtime evidence proves that exact implementation path. An
unvalidated QJL or PolarQuant request returns `NotEstablished`; it never falls back
silently to another cache type.

### 14.4 Required storage categories

Decision 5 later freezes the exact arithmetic, but the Decision 4 provider contract must
represent:

```text
quantised K/V payload
scales or codebook data
rotation/preconditioning data
residual/sketch data where genuinely active
block alignment and padding
per-layer/head metadata
runtime auxiliary buffers
```

A nominal `tokens × dimensions × bits` calculation is not complete evidence by
itself.

Measured compression ratios from one Granite/context/device combination are
calibration observations. They are not universal constants.

---

## 15. OpenVINO GenAI provider

### 15.1 Selected provider

```text
OpenVinoGenAiResourceEstimator
```

is project-owned. `gguf-parser-go` is not used for OpenVINO IR.

### 15.2 Evidence sources

The provider combines:

```text
trusted package/artifact sizes
trusted Model Inspection architecture and state metadata
exact OpenVINO Runtime/GenAI identity
memory-relevant scheduler settings
curated measured calibration profiles
```

### 15.3 Component coverage

Where evidence exists, the provider represents:

```text
ModelWeights
KvKeyCache
KvValueCache
RecurrentState
ComputeAndScratch
RuntimeFootprint
TemporaryStorage during compile/conversion where applicable
```

### 15.4 Honest evidence grades

The existing project campaign establishes a selected Granite 4.1 3B INT4 OpenVINO
CPU/GPU research route, but it does not establish isolated KV memory for every route
and does not establish 8B/NPU support.

Therefore:

```text
matching tested route + matching profile
→ calibrated estimate

complete structural facts without matched profile
→ structural estimate

missing package identity, model state, scheduler fact or route support
→ NotEstablished
```

XML/BIN file size alone is not a complete peak-memory estimate.

---

## 16. Application/runtime footprint provider

```text
ApplicationRuntimeFootprintProvider
```

owns measured profile components for:

```text
WinUI application baseline
Compatibility worker/helper baseline
runtime initialisation
IPC buffers
tokenisation/result buffers
model-load transient overhead
device/backend bootstrap not already owned by another provider
```

Profiles are keyed by at least:

```text
application build identity
runtime family and exact version/commit
backend/device route
Windows architecture
relevant model scale/architecture class
```

A small versioned profile table is selected over a machine-learning predictor.

If no sufficiently matching profile exists for a mandatory component, the provider
returns `NotEstablished / CalibrationEvidenceUnavailable`. It must not use one global
constant for every runtime and device.

---

## 17. Storage provider

```text
ConfigurationStorageEstimator
```

owns:

```text
PersistentStorage
TemporaryStorage
```

Rules:

```text
existing imported artifact
→ exact measured logical bytes

bundled runtime/helper
→ exact manifest bytes

generated converted artifact
→ exact known manifest/profile or NotEstablished

cache files
→ exact known configuration/profile or NotEstablished

temporary conversion space
→ exact proven bound/profile or NotEstablished
```

Storage remains separate from RAM/VRAM.

An unexplained file-size multiplier is rejected.

---

## 18. Provider and policy identity

### 18.1 Aggregate identity

```text
ResourceEstimation / resource-estimation-v1
```

is recorded through Decision 2's `CompatibilityPolicyIdentity`.

### 18.2 Provider identity

```text
ResourceEstimatorProviderIdentity
├── Kind
├── Version
├── ContractVersion
└── BinarySha256?
```

Provider kinds:

```text
GgufParser
TurboQuantKv
OpenVinoGenAi
ApplicationRuntimeProfile
Storage
```

`BinarySha256` is mandatory for an out-of-process helper and absent for a pure
in-process provider. A present digest is exactly 64 lowercase hexadecimal characters.

The final Compatibility result later records every provider identity used.

---

## 19. Evidence levels and calibration

### 19.1 Estimate evidence levels

```text
ResourceEstimateEvidenceLevel
├── Unspecified = 0
├── Structural
├── CalibratedRoute
└── CalibratedExact
```

Definitions:

```text
Structural
→ computed from trusted structure/tool output but not calibrated for the exact route

CalibratedRoute
→ calibrated on the same runtime/backend/device/configuration class

CalibratedExact
→ exact model hash and all memory-relevant configuration fields match a
  successful calibration observation
```

Actual measurements are stored separately. An estimate is never labelled
`MeasuredExact`.

### 19.2 Calibration key

```text
ResourceCalibrationKey
├── ModelContentSha256
├── ModelFormat
├── RuntimeIdentity
├── Backend
├── DeviceRoute
├── ContextTokens
├── LogicalBatchSize
├── PhysicalBatchSize
├── ParallelSequenceCount
├── KeyCacheType
├── ValueCacheType
├── TurboQuantMode
├── OffloadPlan
├── MemoryMapEnabled
├── FlashAttentionEnabled
├── KvCacheOffloadEnabled
└── SchedulerSettings
```

Fields that do not apply to a route use route-specific canonical absence; they are not
invented as zeros.

### 19.3 Calibration observation

```text
ResourceCalibrationObservation
├── Key
├── PredictedPeakBytes
├── MeasuredPeakBytes
├── SignedErrorBytes
├── AbsoluteErrorBytes
├── AbsolutePercentageError
├── Underpredicted
├── ComponentObservations
├── Outcome
├── MeasurementToolIdentities
└── EvidenceReference
```

Only successful, matched runs become calibration observations. Failed/OOM/cancelled
runs remain useful failure evidence but do not become successful calibration points.

### 19.4 Accuracy reporting

At minimum record:

```text
signed error
absolute error
percentage error
underprediction count and magnitude
overprediction count and magnitude
worst matched underprediction
false-safe cases after the later Decision 6 margin
```

Mean absolute error alone is insufficient because it can hide dangerous
underprediction.

No universal numeric accuracy threshold is frozen in Decision 4. Decision 6 selects
the conservative admission margin after the matched data exists. Upstream accuracy
claims are background evidence only.

### 19.5 Admission rule

A provider may produce a `Structural` estimate before calibration. It may influence a
final “safe/compatible” result only when the later Decision 6 policy explicitly admits
its evidence level.

No configuration can be classified safe when a mandatory component is missing.

---

## 20. Measurement authority

A controlled calibration harness records:

```text
peak process private/committed bytes
peak working set
runtime-reported model allocation
runtime-reported K/V allocation
runtime-reported compute allocation
dedicated GPU allocation where observable
shared GPU allocation where observable
actual backend/device
requested versus actual offload
fallback evidence
run outcome
runtime/tool/build identities
model hash
configuration fingerprint
```

Generation success alone is not proof of requested GPU execution or TurboQuant
activation.

Measurements must preserve:

```text
successful
failed
out of memory
cancelled
unsupported
measurement unavailable
```

as distinct outcomes.

The application consumes curated profiles, not raw logs containing local paths,
machine/account identity or private data.

---

## 21. Result and failure model

### 21.1 Result statuses

```text
ConfigurationResourceEstimationStatus
├── Unspecified = 0
├── Established
├── NotEstablished
├── Failed
└── Cancelled
```

Invariants:

```text
Established
→ complete estimate exists
→ no not-established reason
→ no failure diagnostic

NotEstablished
→ no estimate
→ one non-Unspecified reason
→ no failure diagnostic

Failed
→ no estimate
→ failure diagnostic exists

Cancelled
→ no estimate
→ no reason or failure diagnostic
```

### 21.2 Not-established reasons

```text
ResourceEstimationNotEstablishedReason
├── Unspecified = 0
├── UnsupportedConfiguration
├── RequiredModelEvidenceUnavailable
├── RequiredProviderUnavailable
├── ProviderCoverageIncomplete
├── CalibrationEvidenceUnavailable
├── NumericLimitExceeded
└── ModelArtifactChanged
```

Expected evidence limitations are not operational failures.

### 21.3 Stable failure codes

Operational failures use stable codes:

```text
resource-helper-start-failed
resource-helper-timeout
resource-helper-output-too-large
resource-helper-schema-mismatch
resource-helper-output-invalid
resource-provider-contract-violation
resource-estimation-unexpected-failure
```

Diagnostics contain:

```text
stable code
provider kind
sanitised technical summary
```

They do not contain:

```text
absolute path
full command line
raw stdout/stderr
username or machine name
credential/token
native stack dump
```

Cancellation remains separate from failure.

### 21.4 No silent fallback

A failed or unsupported requested provider route is never replaced with another
configuration.

Examples:

```text
Turbo3 unsupported
✕ silently estimate Q8_0

OpenVINO GPU profile absent
✕ silently estimate CPU

helper schema mismatch
✕ use model file size × constant
```

The caller may ask Decision 8 to generate another explicit configuration later.

---

## 22. Secure local-helper boundary

The helper is an untrusted local process operating on an untrusted model file.

Required controls:

```text
pinned source commit and dependency graph
reviewed MIT licence and retained notice
reproducible Windows x64 build
binary SHA-256 and SBOM
version/schema handshake
no runtime download or “latest” lookup
the wrapper exposes and invokes no network or remote-model request path
no local server or open port
no shell command construction
ProcessStartInfo.ArgumentList for fixed flags only
sensitive model path sent through bounded stdin JSON, not command line
parent holds protected read-only model handle without delete sharing
full content SHA-256 revalidated before helper invocation
bounded stdin/stdout/stderr
strict UTF-8 and JSON schema
checked numeric conversion
timeout
cancellation
process-tree termination
sanitised diagnostics
post-operation artifact continuity validation
```

The helper must not echo the model path.

Malformed, truncated, oversized and adversarial GGUF fixtures are mandatory tests.

---

## 23. Risk-first Windows spike

The `gguf-parser-go` provider is not production-approved merely because its source
builds.

The spike tests:

```text
1. Windows x64 reproducible build from the pinned commit
2. local Granite GGUF opened read-only under guarded access
3. deterministic project JSON schema
4. context, batch, standard K/V type, mmap, Flash Attention,
   KV offload and GPU-layer settings affect the expected fields
5. CPU, partial-offload and full-offload outputs map without ambiguity
6. platform footprint can be neutralised or isolated
7. the project wrapper exposes and invokes no network, URL, token, RPC or server route
8. cancellation and timeout terminate the process tree
9. malformed/truncated/oversized input fails safely
10. model path does not enter stdout, stderr, retained evidence or command line
11. predicted components can be matched to pinned llama.cpp measurements
12. licence, source, build and binary identities are retained
```

Disposition:

```text
Pass
→ provider may enter implementation and calibration

ConditionalPass
→ use only the proven component subset; missing components remain unowned

Fail
→ keep the application contract and implement the narrow project-owned
  standard-GGUF provider instead
```

A conditional result cannot be widened by assumption.

---

## 24. Testing strategy

### 24.1 Pure contract tests

Prove:

```text
zero enum values are Unspecified
request rejects null/contradictory handoffs/configuration
route-specific invariants
fingerprint determinism and sensitivity
result-envelope invariants
provider identity validation
component slot validation
checked byte conversion
```

### 24.2 Provider-planning tests

Prove:

```text
one owner per mandatory slot
missing owner → NotEstablished
duplicate owner → NotEstablished
TurboQuant replaces standard K/V exactly once
unsupported route has no fallback
OpenVINO route never selects GGUF provider
GGUF route never selects OpenVINO provider
shared/unified memory maps to system pressure once
```

### 24.3 Helper adapter tests

Prove:

```text
stdin carries path; command line does not
spaces and Unicode path work
schema/version mismatch fails
malformed JSON fails
negative/overflowing numeric value fails
stdout/stderr limits fail closed
timeout kills process tree
cancellation kills process tree
non-zero exit maps to a sanitised failure
artifact change blocks result retention
```

### 24.4 Calibration tests

Prove:

```text
exact key match → CalibratedExact
route-class match → CalibratedRoute
no profile → Structural or typed NotEstablished according to provider contract
signed/absolute/percentage error is correct
underprediction is preserved
failed/OOM run cannot become a successful calibration record
false-safe analysis uses the later Decision 6 margin, not an ad-hoc value
```

### 24.5 Integration and packaged regression

The final implementation must preserve the repository's authoritative WinUI 3
packaged test route and prove non-zero discovery/execution of every new required test
class.

Unit tests isolate pure logic. Integration tests use the real helper and bounded
fixtures. Target-machine calibration remains a separate controlled evidence run.

---

## 25. Pragmatic version-one boundary

Decision 4 does not promise universal estimator support.

The first implementation sequence is:

```text
1. standard local GGUF + pinned llama.cpp routes
2. project-validated Turbo2/Turbo3/Turbo4 KV replacement routes
3. selected, trusted OpenVINO Granite 3B routes with sufficient package identity
4. app/runtime and storage profiles needed by those routes
```

The following remain unavailable until separately proven:

```text
unvalidated QJL
unvalidated PolarQuant
arbitrary model families
arbitrary OpenVINO IR folders
OpenVINO 8B where evidence is incomplete
NPU routes without matched evidence
multi-GPU/RPC
multimodal projectors
adapters/LoRA
draft/speculative decoding
remote model URLs
full GPUStack platform
```

Decision 8 owns the final supported candidate matrix. It may admit only routes for
which Decision 4 can create a complete estimate.

---

## 26. Delivery size and time-box

The design is intentionally limited to:

```text
one public estimator interface
one immutable request/result family
one static provider planner
one small local-only Go helper
four provider families
one calibration record family
focused tests and evidence
```

Recommended implementation slices:

```text
Slice A — helper spike and contract admission
→ approximately half to one focused day

Slice B — core contracts, planner and standard GGUF provider
→ approximately one focused day

Slice C — project-owned extensions, calibration mapping and evidence
→ approximately one to two focused days, excluding long hardware runs
```

If time is tighter, complete GGUF + validated TurboQuant first. OpenVINO remains
typed `NotEstablished`; it must not be represented by an inaccurate shortcut.

---

## 27. Definition of planning done

Decision 4 is planning-complete when:

1. option C is fixed;
2. full GPUStack is rejected;
3. every required resource component has an owner or typed unavailable state;
4. request/result/provider identities and invariants are explicit;
5. shared/unified memory cannot be double counted;
6. phase-aware evidence is preserved;
7. the helper spike has exact pass/conditional/fail criteria;
8. TurboQuant replaces rather than adds standard KV;
9. OpenVINO limitations are explicit;
10. calibration and false-safe evidence are required;
11. failures and cancellation are typed;
12. Decision 5–8 responsibilities remain outside this design;
13. no placeholder, hidden formula or universal-support claim remains.

---

## 28. Definition of implementation done

Decision 4 implementation is complete only when:

1. the helper spike has a retained disposition;
2. exact source, licence, dependency, build and binary identities are recorded;
3. the application-owned contracts compile against the canonical Decisions 1–3 types;
4. one exact configuration produces either a complete estimate or a typed non-success;
5. provider coverage rejects missing and duplicate owners;
6. standard GGUF output is mapped through the stable project schema;
7. TurboQuant K/V replacement is proven without double counting;
8. admitted OpenVINO routes use trusted package identity and matching evidence;
9. application/runtime and storage components have versioned profiles;
10. all byte parsing/arithmetic is checked;
11. shared/unified memory is normalised exactly once;
12. no raw path or third-party output reaches public contracts/evidence;
13. matched calibration error and underprediction are recorded;
14. no final fit claim is made by Decision 4;
15. focused, integration and packaged tests pass on the exact review SHA;
16. F-M08 evidence states what is proven and what remains for Decisions 5–6.

---

## 29. Rejected alternatives

### Full GPUStack platform

Rejected because it adds server, cluster, container, network and operational concepts
not needed by the local WinUI application.

### Direct use of the full `gguf-parser` CLI

Rejected because its public surface includes remote models, tokens, RPC and unrelated
features, and because display output is not an application contract.

### Entire estimator written from scratch immediately

Rejected as the only route because reproducing tensor-aware llama.cpp placement and
buffer behaviour would consume time and increase underestimation risk. It remains the
fallback if the bounded helper spike fails.

### File-size multiplier

Rejected because file size does not establish KV, compute, runtime, phase or device
placement.

### One giant nullable configuration DTO

Rejected because invalid combinations become easy to construct and hard to test.

### Dynamic plugin architecture

Rejected because the project needs a small allowlisted estimator set, not an extensible
third-party marketplace.

### Upstream accuracy claim as acceptance

Rejected. Accuracy is established using matched project measurements.

---

## 30. Risk register

| Risk | Consequence | Control |
|---|---|---|
| Parser estimate differs from pinned runtime | false-safe or false-unsafe result | exact-version calibration and evidence grade |
| Shared GPU memory counted twice | false rejection or misleading total | explicit memory domains and one physical-pool mapping |
| Missing runtime/app overhead | underprediction | mandatory profile owner |
| TurboQuant metadata omitted | underprediction | explicit storage categories and calibration |
| Standard and TurboQuant KV both included | overprediction | replacement rule and slot uniqueness |
| OpenVINO inferred from file size | false confidence | trusted package + structural/profile evidence |
| Tool output schema changes | corrupt mapping | pinned schema/version handshake |
| Malicious GGUF/helper output | denial, overflow or disclosure | bounded process, strict schema, checked conversion |
| Raw path enters logs | privacy leakage | stdin-only path, sanitised diagnostics and privacy tests |
| Failed provider silently falls back | result no longer describes request | typed non-success; no fallback |
| Average error hides underprediction | unsafe recommendation | signed/worst underprediction and false-safe analysis |
| Later decisions leak into estimator | coupling and policy confusion | explicit Decision 5–8 boundaries |

---

## 31. Technical source basis

Primary external sources reviewed for this decision:

- `gpustack/gguf-parser-go`, commit
  `a5d9227ae50725494c827c7810c662e102143293`
  - `file_estimate__llamacpp.go`
  - `file_estimate_option.go`
  - `cmd/gguf-parser/README.md`
  - MIT `LICENSE`
- `ggml-org/llama.cpp` runtime allocation and configuration evidence
- OpenVINO GenAI scheduler/configuration documentation
- the project's controlled AtomicBot/TurboQuant and OpenVINO workbooks

Project-source constraints retained:

```text
TurboQuant changes runtime KV cache, not GGUF weights
generation is not proof of requested device/offload/cache activation
OpenVINO CPU/GPU evidence is route-specific
unmeasured KV or unsupported 8B/NPU routes are not silently generalised
all comparisons freeze memory-relevant settings
```

---

## 32. Textbook alignment

- **Systems Engineering: Principles and Practice**, Chapters 6–8, 11–13 and 17:
  requirements traceability, functional allocation, alternatives analysis, risk-first
  prototyping and test evidence.
- **Fundamentals of Software Architecture**, Chapters 2–3, 6, 8 and 21–22:
  trade-offs, cohesive component boundaries, architecture fitness checks, ADRs and
  explicit risk.
- **Engineering Software Products**, Chapters 4 and 7–10:
  product architecture, security/privacy, validation, failure management, testing and
  controlled dependency delivery.
- **AI Engineering**, Chapters 3–4 and 9:
  component-level evaluation, explicit metrics, matched comparisons and inference
  optimisation.
- **Designing Secure Software**, Chapters 2–4, 6–7 and 10–13:
  threat boundaries, least information, allowlists, fail-secure behaviour, untrusted
  input and security testing.
- **The Art of Unit Testing**, Chapters 7–10:
  trustworthy, maintainable test layers and test recipes.
- **Code Complete**, Chapters 3, 5, 8, 22, 25 and 28:
  upstream preparation, information hiding, defensive programming, measurement and
  configuration control.
- **Why Programs Fail**, Chapters 3–6, 8 and 10:
  reproducible failures, simplified fixtures, scientific debugging, observability and
  assertions.
- **Build desktop apps for Windows**:
  retain the native WinUI 3/Windows App SDK packaging and deployment model.

---

## 33. Final summary

```text
one exact configuration
        ↓
application-owned estimator boundary
        ↓
allowlisted, evidence-backed providers
        ↓
complete phase- and domain-aware components
        ↓
typed Established / NotEstablished / Failed / Cancelled result
        ↓
later formula, safety, ranking and candidate decisions
        ↓
Runtime Verification for actual proof
```

Decision 4 is closed at the planning level. Decision 5 may now define the exact
formula, unit, overflow, rounding and peak-composition rules without reopening the
provider architecture.
