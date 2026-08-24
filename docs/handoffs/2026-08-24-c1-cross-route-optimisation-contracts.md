# C1 Handoff — Cross-Route Optimisation Contracts

**Worker:** C1 (shared planning and immutable contracts)
**Date:** 2026-08-24
**Status:** Contract frozen. G1, O1 and UO1 may start.

## Branch and base

| Field | Value |
|---|---|
| Branch | `feature/cross-route-optimisation-contracts-v1` |
| Worktree | `C:\c1-xroute` |
| Base (resolved, 40 characters) | `699c4826af6ee1e8ffe18f2b1eca1b0e6ab9f3e3` |
| Base branch | `feature/model-hardware-compatibility` |
| Contract code frozen at | `48f578e09fb312c874ef2c75ea6c79b2c448c23c` |
| Branch tip | see `git rev-parse feature/cross-route-optimisation-contracts-v1` |
| Worktree status | clean |
| Whitespace/encoding (`git diff --check`) | no errors |

The audited tip matched exactly; the branch had not advanced, so no newer commits
needed preserving. Nothing was reset, overwritten or discarded.

**G1, O1 and UO1 branch from the branch ref**, not from a SHA copied out of
this table. A handoff document cannot name its own commit, and correcting one
that tried produced a further commit - so the ref is the reliable pointer and
the table would always lag it.

Each worker resolves and records the SHA itself:

```powershell
git rev-parse feature/cross-route-optimisation-contracts-v1
git merge-base --is-ancestor feature/cross-route-optimisation-contracts-v1 HEAD
```

Every commit after `48f578e0` is documentation only. The contract assembly
G1, O1 and UO1 compile against is identical at `48f578e0` and at the tip, so a
worker who has the branch ref has the frozen contract whichever they resolve.

## Commits

| SHA | Subject |
|---|---|
| `5c8a3e94` | docs(compatibility): add the approved cross-route optimisation design and plans |
| `594cc9c7` | feat(compatibility): define optimisation preferences |
| `afb0e3a6` | feat(compatibility): add OpenVINO capability model |
| `6310126e` | feat(compatibility): generate cross-route candidates |
| `c6622168` | feat(compatibility): select safe optimisation frontier |
| `48f578e0` | feat(compatibility): issue bound optimisation plans |
| `f7837584` | docs(compatibility): hand off optimisation contracts |

## Canonical contract document

`docs/superpowers/specs/2026-08-24-cross-route-optimisation-contract-design.md`

SHA-256: `FD2B3C7B2B3857667BBA4F30ABC24330AB03069F6012C59DEB21E68C72FE7568`

Transcribed from the approved design supplied with the assignment. Mojibake from
the supplied transport encoding (en dashes rendered as `â`) was normalised to
ASCII equivalents; no clause, table, name or number was altered. The hash above
is of the transcribed file as committed.

## Changed paths

All 41 paths are inside C1's exclusive ownership: the compatibility core, its
standalone test project, and documentation. **No XAML, no executor, no
navigation, no shared project or resource file was touched.**

### Production — `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/`

```
Application/Candidates/RouteConfiguration.cs                 (visibility)
Application/ModeSelection/CompatibilityMode.cs               (doc only)
Application/ModeSelection/SafeCandidateFrontier.cs           (new)
Application/Optimization/CrossRouteCandidateGenerator.cs     (new)
Application/Optimization/OptimizationCandidate.cs            (new)
Application/Optimization/OptimizationCanonicalizer.cs        (new)
Application/Optimization/OptimizationCapabilitySnapshot.cs   (new)
Application/Optimization/OptimizationDigest.cs               (new)
Application/Optimization/OptimizationExecutionPlan.cs        (new)
Application/Optimization/OptimizationExecutionResult.cs      (new)
Application/Optimization/OptimizationIdentifier.cs           (new)
Application/Optimization/OptimizationPlanIssuer.cs           (new)
Application/Optimization/OptimizationPreferenceLabelPolicy.cs(new)
Application/Optimization/OptimizationPreferenceResolver.cs   (new)
Application/Optimization/OptimizationPreferenceSelection.cs  (new)
Application/Optimization/OptimizationWorkload.cs             (new)
Domain/ContextTokenCount.cs                                  (visibility)
Domain/EvidenceGrade.cs                                      (visibility)
Domain/SupportLevel.cs                                       (visibility)
Routes/Gguf/GgufKvCacheFormat.cs                             (visibility)
Routes/Gguf/GgufRouteConfiguration.cs                        (visibility)
Routes/Gguf/GgufWeightFormat.cs                              (visibility)
Routes/Gguf/GpuOffloadLevel.cs                               (visibility)
Routes/OpenVino/OpenVinoCompiledCachePolicy.cs               (new)
Routes/OpenVino/OpenVinoFormatMap.cs                         (new)
Routes/OpenVino/OpenVinoKvCacheFormat.cs                     (new)
Routes/OpenVino/OpenVinoPerformanceHint.cs                   (new)
Routes/OpenVino/OpenVinoResourceEstimator.cs                 (new)
Routes/OpenVino/OpenVinoRouteConfiguration.cs                (new)
Routes/OpenVino/OpenVinoWeightFormat.cs                      (new)
```

### Tests — `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/`

```
Application/Candidates/CrossRouteCandidateGeneratorTests.cs      (new)
Application/Optimization/OptimizationCapabilitySnapshotTests.cs  (new)
Application/Optimization/OptimizationPreferenceSelectionTests.cs (new)
Invariants/OptimizationPlanBindingTests.cs                       (new)
Invariants/OptimizationPreferenceInvariantTests.cs               (new)
Invariants/PrivacyCanaryTests.cs                                 (allowlist only)
Routes/OpenVino/OpenVinoResourceEstimatorTests.cs                (new)
Routes/OpenVino/OpenVinoRouteConfigurationTests.cs               (new)
```

### Documentation

```
docs/superpowers/specs/2026-08-24-cross-route-optimisation-contract-design.md
docs/superpowers/plans/2026-08-24-c1-cross-route-planner.md
docs/superpowers/plans/2026-08-24-cross-route-optimisation-implementation.md
docs/handoffs/2026-08-24-c1-cross-route-optimisation-contracts.md
```

## Tests

```powershell
dotnet test --project tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj --configuration Release
```

| Metric | Value |
|---|---|
| total | 659 |
| failed | 0 |
| succeeded | 659 |
| skipped | 0 |
| baseline at `699c4826` | 526 |
| added by C1 | 133 |

Solution build also verified:

```powershell
dotnet build "IBM Granite with TurboQuant (Intel).slnx" -p:Platform=x64
```

Zero errors. The existing compatibility feature and its WinUI page still compile
against the widened core.

### A note on filtering

The plan's per-task commands use `--filter "FullyQualifiedName~..."`. **That form
selects zero tests with this project's runner**, and `--list-tests` produces no
listing either. Every filtered form attempted returned `total: 0`, which is
indistinguishable from a passing run and must not be reported as one.

The whole-suite command above is therefore the verification of record. It does
execute the invariant and privacy suites — `PrivacyCanaryTests` failed and passed
repeatedly during this work as new string members were added and reviewed, which
is direct evidence it runs.

## Public contract

Namespace `GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization`
unless noted.

### Preference

| Type | Members |
|---|---|
| `OptimizationPreferenceKind` | `Automatic = 1`, `Manual = 2` |
| `OptimizationPreferenceBand` | `MaximumEfficiency = 1`, `Efficient = 2`, `Balanced = 3`, `HighCapability = 4`, `MaximumCapability = 5` |
| `OptimizationPreferenceSelection` | `Kind`, `PreferenceValue` (`int?`), `Band` (`OptimizationPreferenceBand?`); factories `Automatic()`, `Manual(int)` |
| `OptimizationPreferenceLabelPolicy` | `AutomaticLabel`, `GetLabel(selection)`, `GetLabel(band)` |

Exact labels and ranges, matching Model Download:

| Slider value | Label |
|---:|---|
| 0–19 | `Maximum efficiency` |
| 20–39 | `Efficient` |
| 40–59 | `Balanced` |
| 60–79 | `High capability` |
| 80–100 | `Maximum capability` |

`Automatic` is a separate choice with label `Automatic`; it carries no slider
value and no band. `Manual` throws `ArgumentOutOfRangeException` outside 0–100
rather than clamping.

### Route and capability

| Type | Members |
|---|---|
| `OptimizationRoute` | `Gguf = 1`, `OpenVino = 2` |
| `OptimizationCapabilitySnapshot` | `SnapshotId`, `CapabilitySnapshotSha256`, `Route`, `Gguf` (`GgufCapabilityPayload?`), `OpenVino` (`OpenVinoCapabilityPayload?`); factories `ForGguf(...)`, `ForOpenVino(...)` |
| `GgufCapabilityPayload` | `RuntimeVersion`, `Admitted`; `Create(...)` |
| `OpenVinoCapabilityPayload` | `RuntimeVersion`, `Admitted`; `Create(...)` |
| `GgufAdmittedConfiguration` | `EvidenceId`, `Backend`, `Device`, `Weights`, `KvCache`, `Offload`, `MinimumContextTokens`, `MaximumContextTokens`, `Level`, `RequiresEvidence` |
| `OpenVinoAdmittedConfiguration` | `EvidenceId`, `Device`, `Weights`, `KvCache`, `PerformanceHint`, `CompiledCache`, `Streams`, `MinimumContextTokens`, `MaximumContextTokens`, `Level`, `RequiresEvidence` |

Exactly one payload per snapshot, enforced by construction — there is no factory
that can produce both.

### Sealed route configurations

`RouteConfiguration` (abstract, in `...Application.Candidates`) exposes
`RouteId` and `CanonicalDescriptor`. Two sealed concrete types:

- `GgufRouteConfiguration` — `Weights`, `KvCache`, `Backend`, `Device`, `Offload`
- `OpenVinoRouteConfiguration` — `Weights`, `KvCache`, `Device`, `PerformanceHint`, `CompiledCache`, `Streams`

Executors pattern-match the concrete type and refuse anything else. Descriptors
are route-prefixed (`gguf|…`, `openvino|…`) so two routes cannot collide.

New OpenVINO vocabularies (`...Routes.OpenVino`):

- `OpenVinoWeightFormat`: `Unspecified`, `Original`, `Fp16`, `Int8`, `Int4`, `TurboQuantTbq4`, `TurboQuantTbq3`
- `OpenVinoKvCacheFormat`: `Unspecified`, `RouteDefault`, `F16`, `Bf16`, `U8`, `U4`
- `OpenVinoPerformanceHint`: `Unspecified`, `Latency`, `Throughput`
- `OpenVinoCompiledCachePolicy`: `Unspecified`, `Disabled`, `Enabled`

### Candidate

| Type | Members |
|---|---|
| `OptimizationAssessment` | `Unknown = 0`, `Poor`, `Acceptable`, `Good`, `Excellent` |
| `OptimizationExclusionReason` | `None`, `EstimateNotEstablished`, `ExceedsSafeMemoryBudget`, `InsufficientDiskSpace`, `ContextBelowWorkloadMinimum`, `EvidenceBelowAdmissionLevel`, `ExperimentalNotAdmitted`, `QualityBelowFloor`, `Dominated` |
| `OptimizationCandidateMetrics` | `Evidence`, `Quality`, `Performance`, `Stability`, `ContextTokens`, `PredictedPeakBytes`, `SafeBudgetBytes`, `HeadroomBytes`, `WorkingDiskBytes`, `OutputDiskBytes`, `RequiresPersistentChange`, `FitsSafely` |
| `OptimizationCandidate` | `Route`, `Configuration`, `Metrics`, `EvidenceId`, `IsExperimental`, `CanonicalDescriptor` |
| `OptimizationWorkload` | `WorkloadId`, `MinimumContextTokens`, `MinimumQuality`, `CandidateContexts` |
| `OptimizationExclusion` | `EvidenceId`, `CanonicalDescriptor`, `Reason` |
| `CrossRouteGenerationResult` | `Candidates`, `Exclusions` |
| `OptimizationSelection` | `Candidate`, `Preference`, `SharedWithAdjacentBand` |

`OptimizationPreferenceResolver.Resolve(admitted, preference)` returns
`OptimizationSelection?` — **null when nothing was admitted.** It never
fabricates a candidate to fill a slider position.

### Plan and result

| Type | Members |
|---|---|
| `OptimizationJourneyBinding` | `ModelInspectionRunId`, `ModelInspectionHandoffId`, `ModelSha256`, `ModelLengthBytes`, `ProductHardwareRunId`, `HardwareSnapshotSha256` |
| `OptimizationExecutionPlan` | `ContractVersion` (1), `OptimizationPlanId`, `Binding`, `CapabilitySnapshot`, `Workload`, `Candidate`, `Preference`, `SharedWithAdjacentBand`, `ConfigurationSha256`, `CreatedAtUtc`, `Route`, `ProducesPersistentArtifact`, `MatchesCapability(...)`, `MatchesSource(...)` |
| `OptimizationPlanIssuer` | `Issue(selection, capabilitySnapshot, workload, binding, createdAtUtc)` |
| `OptimizationExecutionResult` | `Status`, `OptimizationPlanId`, `Route`, `ConfigurationSha256`, `SourceSha256`, `SourceUnchanged`, `OutputIdentity`, `OutputManifestSha256`, `OutputSizeBytes`, `SupportCode`, `CompletedAtUtc`, `IsSuccessful`, `ProducedPersistentArtifact` |

`OptimizationExecutionStatus`: `Unspecified = 0`, `SucceededPersistent = 1`,
`SucceededRuntimeProfile = 2`, `Cancelled = 3`, `Failed = 4`, `ReplanRequired = 5`.

`OptimizationSupportCode` is a closed enum: `None`, `SourceIdentityMismatch`,
`CapabilityDrift`, `ModelBindingMismatch`, `HardwareBindingMismatch`,
`ToolNotAdmitted`, `InsufficientDiskSpace`, `StagingUnavailable`,
`ConversionFailed`, `ValidationFailed`, `SmokeTestFailed`, `ReinspectionFailed`,
`PublicationFailed`, `CancelledByUser`, `UnexpectedFailure`.

### Preserved seam names

`modelInspectionRunId`, `modelInspectionHandoffId`, `modelSha256`,
`modelLengthBytes`, `productHardwareRunId`. C# properties are the PascalCase
equivalents; the serialized names are unchanged and were not widened,
reconstructed or renamed.

## Executor obligations

1. Recompute `ConfigurationSha256` with `OptimizationCanonicalizer` and match it.
2. Call `plan.MatchesSource(digest, length)` — **both**, not either.
3. Call `plan.MatchesCapability(currentSnapshot)`.
4. On any planning-input mismatch return `ReplanRequired` with one of
   `SourceIdentityMismatch`, `CapabilityDrift`, `ModelBindingMismatch`,
   `HardwareBindingMismatch`, `ToolNotAdmitted`. The factory refuses any other
   code — an environmental problem that left the plan valid is `Failed`.
5. Never substitute a configuration. Never amend a plan.
6. `Failed`, `Cancelled` and `ReplanRequired` publish nothing: output identity,
   manifest and size are forced null/zero by the type.
7. `Succeeded` cannot be constructed with `sourceUnchanged: false`.

## Deliberate deviations from the plan text

Each was a choice, not an oversight.

1. **`CompatibilityMode` kept, not deleted.** Task 1 says retire `Quality` and
   `Efficiency`. Those members still drive the shipped compatibility screens and
   a large share of the 526 baseline tests. Design §13 mandates additive-first
   migration through adapters, and §6.1 permits legacy values read through a
   versioned adapter. The enum is marked migration-only and forbidden in durable
   serialization (`OptimizationPlanBindingTests.PlanDoesNotSerializeTheLegacyModeVocabulary`).

2. **Context excluded from the OpenVINO route descriptor.** Task 2 lists it
   there. `CandidateFingerprint` already composes context separately, so
   carrying it in both places gives one candidate two places to state its
   context length, which can disagree. Context is folded into the
   *complete-configuration* digest instead, exactly once
   (`ConfigurationDigestChangesWithContext`).

3. **`ModeSelector`/`ModeComparers` not modified.** Task 4 lists them.
   `SafeCandidateFrontier` and `OptimizationPreferenceResolver` are new types
   alongside; the existing selector is untouched and still green. Same
   additive-first reasoning as (1).

4. **Snippet member names treated as illustrative.** Task 3's snippet calls
   `OpenVinoResourceEstimator.Estimate(candidate)` and reads
   `estimate.SystemCommittedBytes` / `PhysicalPoolCommittedBytes`, none of which
   exist on the current types. The plan already declares its `*TestData` names
   illustrative; the *invariant* is implemented and asserted against the real
   composer (`IntegratedGpuSharedMemoryIsCountedOnce`,
   `NoComponentIsChargedToTwoPools`).

5. **Frontier dominance restricted to quality and memory.** See below — this one
   started as a defect.

## Defects found and fixed during implementation

- **Balanced resolved below Efficient.** My first knee used quality-per-byte,
  which is always maximised by the smallest candidate. Bands are now positions
  on the frontier, which makes both monotonic properties structural.
- **Dominance over six axes broke monotonicity.** A cheaper candidate could also
  be higher quality, kept alive by a third axis, so moving the slider toward
  capability could return something worse. Dominance is now over the two axes
  the slider trades; the richer axes break ties, which is where the design puts
  them.
- **Compiled model cache charged as `PersistentArtifact`.** That would make a
  runtime-only result look like it had created a model. It is charged as
  `ModelState`: a real file, but not a model copy.

## What C1 did NOT do

No UI, XAML, navigation, presentation state, or shared resource dictionary. No
GGUF quantisation. No OpenVINO execution, staging, conversion, validation, smoke
test, provenance, publication or rollback. No Chat, no Save, no destination
page. No integration wiring, no project registration.

**No executor exists.** Nothing in this branch runs a model, writes an artifact
or touches a tool. The contract is frozen; execution is G1's and O1's.

Not pushed, not merged, no PR opened.

## Hardware dependency — the residual, stated separately

C1's work is complete against the approved hardware contracts and did not need
Hardware Inspection production code.

What C1 consumed: `HardwareFacts`, `InspectedModelFacts`, `productHardwareRunId`
and `modelInspectionRunId` as typed contracts, plus deterministic fixture data.
All planning, estimation, frontier construction and plan issuing are exercised
on fixtures and are fully tested.

**What remains blocked on Hardware Inspection, and belongs to nobody on this
wave:**

1. A real `IHardwareFactsSource` adapter. The port ships a typed-unavailable
   implementation, so a live run still reports "no answer yet" rather than
   inventing a machine.
2. Producing a genuine `OptimizationCapabilitySnapshot` from installed backends,
   devices and tool versions. Everything in this branch is admitted-by-fixture.
   **No capability claim here is evidence that any combination is installed.**
3. The real `HardwareSnapshotSha256` value bound into
   `OptimizationJourneyBinding`.

None of these block G1, O1 or UO1: all three consume the frozen contract, and
fixtures are sufficient for their component work.

## Not claimed

- No end-to-end compatibility or optimisation journey is complete.
- No hardware was contacted; no measurement was taken. Every metric this branch
  produces is graded `EvidenceGrade.Estimated`, and a test asserts nothing is
  graded `Measured` before anything has run.
- Planning-document examples were not treated as installed support evidence.
- No claim is made about any other worker's branch.
