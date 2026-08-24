# C1 Handoff — Cross-Route Execution Contract V2

**Branch:** `feature/cross-route-optimisation-contracts-v2`
**V1 base:** `f999443279ae3505f7df1c686f98c5163c190acd` (untouched, not amended)
**Purpose:** contract correction. No executor implemented.

G1 and O1 **branch or merge from the remote branch ref**, not from copied files:

```powershell
git fetch origin
git rev-parse origin/feature/cross-route-optimisation-contracts-v2
git merge-base --is-ancestor origin/feature/cross-route-optimisation-contracts-v2 HEAD
```

## What changed and why

V1 represented the *choice* correctly but not the *work*. The plan carried a
coarse `GgufRouteConfiguration` and one `RuntimeVersion`, so an executor had to
supply a thread count, a batch size, an exact GPU layer count and separate key
and value cache types from somewhere — after the user had confirmed. Its
`ConfigurationSha256` therefore did not bind the settings that decide what runs.

V2 adds a closed route execution payload and binds every execution-affecting
field into the digest.

## Contract version

| | |
|---|---|
| `OptimizationExecutionPlan.CurrentContractVersion` | `2` |
| `OptimizationExecutionPlan.MinimumExecutableContractVersion` | `2` |
| `plan.IsExecutableBy(int executorContractVersion)` | false for 1, true for 2 |

A V1 plan carries no payload at all. `OptimizationExecutionPlan` has no public
constructor and `OptimizationPlanIssuer.Issue` requires a payload, so this
assembly can no longer produce one. An executor must call `IsExecutableBy` and
refuse rather than partially interpret.

## New public types

Namespace
`GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution`.

| Type | Members |
|---|---|
| `OptimizationExecutionPayload` | `Route`, `Gguf`, `OpenVino`, `RequiresPersistentConversion`; factories `ForGguf`, `ForOpenVino` |
| `GgufRuntimeBackend` | `Cpu`, `Vulkan`, `Sycl` |
| `GgufCacheType` | `F16`, `Q8Zero`, `Q4Zero`, `Turbo3`, `Turbo4` |
| `GgufQuantiserIdentity` | `PackageId`, `ToolVersion`, `ExecutableSha256`; `Create` |
| `GgufExecutionPayload` | see mapping table below; `RequiresPersistentConversion`; `Create` |
| `OpenVinoWeightPrecision` | `Fp16`, `EightBit`, `FourBit` |
| `OpenVinoKvCachePrecision` | `ReleasedDefault`, `U8` |
| `OpenVinoBuildIdentity` | `RuntimeBuild`, `GenAiBuild`, `TokenizersBuild`, `WorkerManifestDigest`; `Create` |
| `TurboQuantBuildIdentity` | `SourceCommit`, `ImplementationCommit`, `PatchSeriesDigest`, `RuntimeManifestDigest`; `Create` |
| `OpenVinoExecutionPayload` | see mapping table below; `RequiresPersistentConversion`; `Create` |
| `GgufOffloadPolicy` | `PartialOffloadShare`, `ExactLayerCount(level, modelLayerCount)`, `Agrees(...)` |
| `TrustedSourceContext` | `OptimizationPlanId`, `ModelInspectionRunId`, `ModelInspectionHandoffId`, `ExpectedModelSha256`, `ExpectedModelLengthBytes`; `ForPlan`, `Verify`, `RevealVerifiedPath` |
| `TrustedToolContext` | `OptimizationPlanId`, `PackageId`, `ExpectedExecutableSha256`; `ForGgufQuantiser`, `Verify`, `RevealVerifiedPath` |
| `TrustedResolution` | `Outcome`, `IsVerified` |
| `TrustedResolutionOutcome` | `Unspecified`, `Verified`, `NotARegularFile`, `LengthMismatch`, `DigestMismatch`, `PlanMismatch` |

Changed on `OptimizationExecutionPlan`: new `ExecutionPayload`, new
`IsExecutableBy(int)`, `ContractVersion` now 2.
Changed on `OptimizationPlanIssuer.Issue`: now takes `executionPayload` (2nd)
and `modelLayerCount` (6th).

## GGUF field mapping

Read from `GraniteEdgeAI.GgufRuntime.Contracts.Configuration.GgufRuntimeConfiguration`
at `origin/feature/gguf-cli-chat-production`. Names and types are 1:1 so G1
constructs one directly — this project does not reference that assembly, because
it lives on another branch and depending on it would invert the layering.

| `GgufExecutionPayload` | `GgufRuntimeConfiguration` | Notes |
|---|---|---|
| `RuntimeBuildId` | `runtimeBuildId` | |
| `RuntimeSourceCommit` | `runtimeSourceCommit` | lowercase 40-char Git id, validated |
| `Backend` | `backend` | same member names |
| `DeviceId` | `deviceId` | `CPU`, `GPU.0`, `GPU.1`, `NPU` |
| `ContextSize` | `contextSize` | |
| `KeyCacheType` | `keyCacheType` | same member names incl. `Q8Zero` |
| `ValueCacheType` | `valueCacheType` | |
| `GpuLayerCount` | `gpuLayerCount` | exact, see offload below |
| `FlashAttention` | `flashAttention` | |
| `ThreadCount` | `threadCount` | |
| `BatchSize` | `batchSize` | |
| `EvidenceGrade` | `evidenceGrade` | string, as the runtime contract has it |
| `ProfileId` | `profileId` | |
| `MaximumGeneratedTokens` | `maximumGeneratedTokens` | |
| `PersistentTargetWeightFormat` | — | C1 addition; `Imported` means runtime-only |
| `Quantiser` | — | C1 addition; required iff converting |

`modelId` and `modelSha256` are **not** duplicated in the payload: they are
already on `OptimizationJourneyBinding`. G1 fills them from there.

## OpenVINO field mapping

Read from the published OpenVINO route contracts at
`c7c7ae34210caa3d0af03643ea1fa4966bb8b296`. Terminology preserved exactly; no
OpenVINO concept is expressed in GGUF terms.

| `OpenVinoExecutionPayload` | Published source |
|---|---|
| `ConfigurationId` | `OpenVinoOptimizationCandidate.ConfigurationId` |
| `Device` | `OpenVinoOptimizationCandidate.Device` (string, e.g. `CPU`) |
| `Maturity` | `OpenVinoOptimizationCandidate.Maturity` |
| `EvidenceId` | `OpenVinoOptimizationCandidate.EvidenceId` |
| `SourceWeightPrecision` | `OpenVinoOptimizationProvenance.SourceWeightPrecision` |
| `TargetWeightPrecision` | `OpenVinoOptimizationProvenance.TargetWeightPrecision` |
| `KvCachePrecision` | `OpenVinoRuntimeOptimization.KvCachePrecision` |
| `CompiledCacheEnabled` | `OpenVinoCompiledCachePolicy.Enabled` |
| `CompiledCacheIsDisposable` | `OpenVinoCompiledCachePolicy.IsDisposable` |
| `CompiledCacheIsModelArtifact` | `OpenVinoCompiledCachePolicy.IsModelArtifact` |
| `CreatesCompletePackage` | `OpenVinoPersistentArtifact.CreatesCompletePackage` |
| `BuildIdentity.*` | `OpenVinoBuildEvidence` |
| `OptimizerVersions` | `OpenVinoOptimizationProvenance.OptimizerVersions` |
| `TurboQuantBuild.*` | `TurboQuantBuildEvidence` |

**One deliberate shape difference.** The compiled-cache policy is carried as its
three published fields rather than as a type named
`OpenVinoCompiledCachePolicy`. That name already exists twice in this codebase
with different shapes, and this assembly holds an invariant forbidding two types
from sharing a short name (it exists so a reviewed privacy member cannot
silently exempt an unreviewed one). Field names are exact; only the wrapper is
not reintroduced.

**Executor-owned, and only because it cannot change the confirmed result:**
`OperationId`, `ValidationRunId`, `OutputFiles`, `OutputManifestSha256`, and the
three disposition strings on `OpenVinoOptimizationProvenance`. These are
outcomes of a run, produced by it, and no value of theirs alters the output
format, quality, resource use, persistent artifact or device the user confirmed.
Everything that could is in the plan.

## GPU offload

`GgufOffloadPolicy` is the one deterministic relationship between the coarse
category and the exact count:

| `GpuOffloadLevel` | Exact count |
|---|---|
| `None` | `0` |
| `Partial` | `floor(modelLayerCount * 0.5)` |
| `Full` | `modelLayerCount` |
| `Unspecified` | throws |

`Unspecified` fails closed rather than defaulting to zero, because zero is a
real placement — everything on the CPU — and must not double as "unknown".
Issuance calls `Agrees(...)` so a payload cannot state a count the category does
not imply. The share is versioned through the contract version: changing it
changes every digest and forces a replan.

## Candidate/payload agreement

Rejected at issuance, both routes: persistence (checked first, same promise
either way), then per route.

GGUF: backend, device, both cache halves, weight format, context, exact GPU
layer count.
OpenVINO: weight precision, KV cache precision, device, compiled cache,
evidence id.

Two consequences worth knowing before you build against this:

1. **Overlapping fields cannot be varied on one side.** Device, cache and
   context appear on both, so a payload differing from the candidate is refused
   — that is the check working, not a bug in your fixture.
2. **Key and value cache types are forced equal.** The support matrix admits one
   format per entry while the runtime configures the halves separately, so an
   admitted entry can only authorise the same format for both. An asymmetric
   pair would be a configuration no evidence covered. Admitting one requires the
   matrix to admit a pair — a contract change, not an executor decision.

## Runtime-only OpenVINO

The published route defines `Fp16`, `EightBit`, `FourBit` and no `Original`. A
runtime-only OpenVINO plan is therefore expressed as **target precision equal to
source precision** — converting nothing — not as an "Original" precision.
`RequiresPersistentConversion` is derived from that comparison.

## Canonicalisation rules

`ConfigurationSha256` covers: contract version (first), route, candidate route
descriptor, context, persistence flag, evidence id, experimental flag, then
every field of the present payload.

- deterministic and stable across runs and processes;
- culture invariant (`CultureInfo.InvariantCulture` on every number);
- explicitly ordered — a fixed emission order, never reflection or dictionary
  enumeration order; `OptimizerVersions` is sorted ordinally by key;
- versioned — the version is the first field, so a layout change cannot collide
  with another layout;
- unambiguous — every field is length-prefixed (`name=len:value`), so a value
  containing the separator cannot read as two fields and two layouts cannot
  concatenate to the same bytes;
- absence is encoded, not omitted — a missing quantiser emits
  `gguf.quantiser=4:none`, so a runtime-only plan cannot hash equal to a
  converting one;
- excludes paths, timestamps and free text.

Output is lowercase hex, because every consumer compares ordinally.

## Source-resolution seam

`TrustedSourceContext` travels **beside** the plan, never inside it. Binds
`optimizationPlanId`, `modelInspectionRunId`, `modelInspectionHandoffId`,
expected `modelSha256`, expected `modelLengthBytes`, and the local path.

`Verify(plan)` immediately before execution, in this order:

1. plan id matches;
2. `Path.GetFullPath` canonicalises;
3. must exist and be a regular file — directories and reparse points refused
   before anything is read;
4. length compared;
5. SHA-256 recomputed and compared.

Both length and digest, because a length alone collides trivially and a digest
alone would accept a file truncated and re-padded to match. `RevealVerifiedPath`
throws unless `Verify` passes, so nothing gets the path without checking.

## Tool-resolution seam

`TrustedToolContext.ForGgufQuantiser(plan, path)` refuses when the plan pins no
quantiser — a tool context on a runtime-only plan would let a conversion run
under a plan that never proposed one. `Verify` recomputes the executable digest
and compares it with the pinned `ExecutableSha256`.

## Privacy

The path never enters plan serialization, `ConfigurationSha256`, results, logs,
telemetry or `ToString`. Controls, each tested:

- private field, no property returning it;
- gated reveal only after verification;
- `ToString` overridden on all three trusted types;
- none is a `record`, so nothing synthesizes a member-printing `ToString`;
- `TrustedContextsAreNotReachableFromAPlan` walks the entire plan and result
  property graph and asserts neither context is reachable;
- every adapter-supplied identifier goes through `OptimizationIdentifier`
  (bounded length, no separators, no traversal), so a path passed as a device
  id, profile id, package id or configuration id throws at the door.

Failures are reported through the closed `OptimizationSupportCode` enum and
`TrustedResolutionOutcome`. No free-form text, no tool output, no native error.

## Preserved unchanged

Seam names: `modelInspectionRunId`, `modelInspectionHandoffId`, `modelSha256`,
`modelLengthBytes`, `productHardwareRunId`, `hardwareSnapshotSha256`.

Bands and labels: `Maximum efficiency` 0–19, `Efficient` 20–39, `Balanced`
40–59, `High capability` 60–79, `Maximum capability` 80–100, plus separate
`Automatic`. Monotonicity invariants unchanged and still green.

No UI, XAML, slider behaviour, navigation, compatibility calculation, hardware
inspection, GGUF execution, OpenVINO execution, Chat, Save or export was
touched.

## Migration for G1 and O1

1. Check `plan.IsExecutableBy(2)`; refuse otherwise.
2. Read `plan.ExecutionPayload`, match on `Route`, use the payload for that route
   and nothing else.
3. GGUF: build a `GgufRuntimeConfiguration` from the mapping table, taking
   `modelId`/`modelSha256` from `plan.Binding`.
4. Recompute `ConfigurationSha256` and compare; recompute source identity via
   `TrustedSourceContext`; verify the tool via `TrustedToolContext`.
5. On any planning-input mismatch return `ReplanRequired` (`CapabilityDrift`,
   `SourceIdentityMismatch`, `ModelBindingMismatch`, `HardwareBindingMismatch`,
   `ToolNotAdmitted`). Environmental problems that left the plan valid are
   `Failed`.
6. Never substitute. Never amend.

`OptimizationPlanIssuer.Issue` is a breaking signature change; every call site
must supply a payload and `modelLayerCount`.

## Tests

```powershell
dotnet test --project tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj --configuration Release
```

| | |
|---|---|
| total | **728** |
| failed | **0** |
| succeeded | 728 |
| skipped | 0 |
| V1 baseline | 659 |
| added by V2 | 69 |

Whole project, no filter. A filtered invocation is not used as evidence: the
`--filter` and `--list-tests` forms both return `total: 0` with this runner,
which is indistinguishable from a passing run.

Solution build:

```powershell
dotnet build "IBM Granite with TurboQuant (Intel).slnx" -c Debug -p:Platform=x64
```

Build succeeded, zero errors.

## Files changed from V1

Production (`shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/`):

```
Application/Optimization/Execution/GgufExecutionPayload.cs          (new)
Application/Optimization/Execution/OpenVinoExecutionPayload.cs      (new)
Application/Optimization/Execution/OptimizationExecutionPayload.cs  (new)
Application/Optimization/Execution/TrustedExecutionContext.cs       (new)
Application/Optimization/OptimizationExecutionPlan.cs               (modified)
Application/Optimization/OptimizationCanonicalizer.cs               (modified)
Application/Optimization/OptimizationPlanIssuer.cs                  (modified)
```

Tests:

```
Invariants/OptimizationExecutionContractV2Tests.cs  (new)
Invariants/OptimizationPlanBindingTests.cs          (migrated to V2)
Invariants/PrivacyCanaryTests.cs                    (allowlist)
```

`GgufOffloadPolicy` and `ExecutionVocabularyMap` live in
`OptimizationExecutionPayload.cs`.

## Not done

No executor. Nothing in this branch runs a model, writes an artifact, launches a
tool or touches a device. Not merged into G1, O1, I0 or main.
