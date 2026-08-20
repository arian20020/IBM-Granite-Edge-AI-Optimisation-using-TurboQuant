# Model/Hardware Compatibility (Block 3) Design

| Metadata | Value |
|---|---|
| Status | Approved by the user on 2026-08-20 |
| Date | 2026-08-20 |
| Owner | C1 — Model/Hardware Compatibility |
| Delivery branch | `feature/model-hardware-compatibility` |
| Worktree | `C:/c1-compat` |
| Base commit | `960bb4d047b976d4bad68d05c4481e1937a2bf27` — stable Model Inspection reference |
| Visual authority | `hardware-layout-direction-b-refinement-v3.html`, SHA-256 `6C677D5E9BF9F2F58F1F404ECA3798CD6D17C68911F966CA21DEC01CA902FB4B`, pinned by V0; plus the user-supplied Compatibility screen set (10 states) approved 2026-08-20 |
| Owner answers consumed | `HI-C1-Owner-Contract-Answers-2026-08-20` |

## 1. Outcome

After Model Inspection and Hardware Inspection both complete, the user asks:

> Can this model run on my computer?

Compatibility answers that question by combining two already-approved handoffs, estimating
the memory a supported configuration would need, comparing it against a conservative safe
budget, and presenting either a recommended configuration or an honest statement that no
safe configuration was established.

Compatibility **selects a plan**. It does not collect hardware, parse models, run converters,
run inference, or optimise artifacts. G1 and O1 execute later.

## 2. Governing constraint

Every upstream dependency is currently unavailable. Verified at the base commit and against
the owner answers:

- `HardwareInspectionHandoff` and `HardwareSnapshot` — **not implemented, not frozen.**
  `feature/hardware-inspection-contract-v1` at `d2ebde6a` carries contract *tests* only; no
  production type exists under `Features/HardwareInspection`.
- `ModelInspectionHandoff` — **not implemented.** Only the six-field wire contract is approved.
- Identity-bound model-fact resolver — **does not exist.** Owned by I1.
- `IInspectedModelArtifactAccessService` — **does not exist.**
- Fresh-memory provider — **does not exist.**
- OpenVINO inspection record — **does not exist.** `ModelInspectionConfigurationEvidence`
  rejects any format other than `GGUF`; `ModelQuickScanner` returns `openvino-scan-not-implemented`.

Therefore this design **must not** produce a fit result today, and must not pretend to.

## 3. Central architectural decision

**C1 ports accept and return C1-owned calculation-domain records, never owner types.**

This is not shadowing. C1 does not declare a `HardwareSnapshot` or a `HardwareInspectionHandoff`.
It declares `HardwareFacts` — a record shaped by what the estimator consumes, with a different
owner and a different lifetime. Thin adapters map owner types into these records at M6, once
owner contract snapshots are published.

Consequence: **the compatibility core compiles today with zero reference to H1 or I1.** No
guessed fully-qualified name, no invented constructor, nothing to unpick later.

## 4. Project layout

The route-neutral core is a **plain `net8.0` class library**, not WinUI code. This makes
"the core cannot reference WinUI" a compiler guarantee rather than a fitness test, and gives
a `dotnet test` loop measured in seconds.

```
shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/     net8.0, no WinUI
├── Domain/              value objects, checked byte math, identities
├── Application/
│   ├── Contracts/       request, run result, progress, policy identity
│   ├── Ports/           the five owner seams
│   ├── PlanningContext/ Decision 3
│   ├── Capabilities/    versioned support matrix
│   ├── Estimation/      per-component resource estimation
│   ├── FitAssessment/   safe budget, thresholds, classification
│   ├── Candidates/      complete candidate generation
│   ├── ModeSelection/   Automatic / Quality / Balanced / Efficiency
│   ├── Routes/          ICompatibilityRouteEvaluator + static registry
│   └── CompatibilityRunCoordinator.cs
└── Routes/
    ├── Gguf/            GGUF evaluator + closed GGUF-typed records
    └── OpenVino/        registration point only — typed Unavailable

IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/
├── Presentation/        ViewModel, presentation factory, theme
├── Controls/            compatibility cards
├── DebugFixtures/       Debug-only, Release-excluded
└── Infrastructure/Adapters/   empty until M6

tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/   net8.0, MSTest + MTP
```

### Ownership

- **C1 owns:** the two directories above and its test project. Nothing else.
- **I0 integration requests:** app `csproj` project reference and Debug-only fixture includes;
  `App.xaml` theme dictionary registration; `OnboardingShellPage` route registration and
  stage-3 wiring; solution file entry; central fixture/test-count registries.
- **Never touched by C1:** Model Inspection internals, Hardware providers, G1/O1 execution.

## 5. Ports

Five seams. Each ships exactly one implementation today, returning a typed unavailable result
with a stable reason code.

| Port | Responsibility |
|---|---|
| `ICompatibilityInputGateway` | Validate and atomically claim the paired handoffs; rollback; commit one transfer |
| `IInspectedModelFactsSource` | Identity → `InspectedModelFacts` |
| `IHardwareFactsSource` | Identity → `HardwareFacts` |
| `IFreshSystemMemoryProbe` | Re-read available bytes plus capture time at the safety gate |
| `IRuntimeVerificationRunner` | Screens 07–09; implemented by G1/O1, never by C1 |

`IFreshSystemMemoryProbe` is deliberately separate from `IHardwareFactsSource`. The handoff's
availability figure is historical; the safety gate needs a reading taken now. Merging them
would make it easy to satisfy the gate with a stale number — the exact false-safe failure mode
the owner answers warn about.

**C1 must not implement a Windows memory collector.** That fact is owned by H1/I0.

## 6. Run lifecycle

```
create CompatibilityRunId
  → gateway validates and atomically claims the paired handoffs
  → resolve InspectedModelFacts + HardwareFacts
  → resolve planning context
  → load support matrix, project installed capability
  → generate complete candidates, baseline first
  → estimate each candidate component-by-component
  → probe fresh available memory        (once, immediately before the gate)
  → assess fit for every candidate against that single observation
  → classify baseline and alternatives
  → resolve the four modes
  → build immutable result
  → (user confirms) gateway commits exactly one transfer
```

One fresh-memory observation is shared across all candidates in a run so comparisons are fair.
A later runtime launch re-checks independently.

## 7. Request, context, result

### Request

```csharp
internal sealed record CompatibilityRunRequest(CompatibilityContextRequest Context);
```

**Recorded deviation from Decision 1.** Decision 1 specifies the request carries both handoff
objects directly. Those types cannot be referenced today and guessing their shape is prohibited
by the owner answers. The handoffs are therefore reached through `ICompatibilityInputGateway`,
which owns which handoffs are current and performs the claim. This honours the master prompt's
correction — *the approved six-field public handoff plus identity-bound internal registry/claim
transaction* — and confines any future change to the request record and one port signature.

Reflection tests assert the request contains no run id, RAM figure, policy version, progress,
`CancellationToken`, UI string, or duplicated model/hardware field.

### Planning context — Decision 3

```
ApplicationDefault  → min(4096, declared model context limit)
UserRequested       → preserved exactly, never silently clamped
missing / zero limit → NotEstablished
overflow             → NotEstablished
```

`ContextTokenCount` is a strongly-typed positive `int`. `ApplicationDefault` forbids an explicit
count; `UserRequested` requires one. Pure policy: no hardware, no I/O, no clock.

### Result

```
CompatibilityRunResult
├── RunId
├── Outcome      : Unspecified=0 | Completed | NotEstablished | Failed | Cancelled
├── Assessment?  (only when Completed)
├── Failure?     (only when Failed)
├── Findings     (defensively copied)
├── PolicyIdentities
└── StartedAtUtc, CompletedAtUtc
```

Success requires a value. Cancelled and failed must not retain one.

## 8. Memory model

### Arithmetic

Bytes in checked `ulong`. Bits→bytes by ceiling division. Alignment rounds upward only at a
documented allocation boundary. Signed differences are direction plus unsigned magnitude.
**Unknown is a typed unavailable value, never zero.** MiB/GiB are display conversions only.

### Resource targets

| Target | Rule |
|---|---|
| System memory | RAM |
| Dedicated device memory | Discrete VRAM only |
| Shared device memory | Integrated GPU — counted against system-memory pressure **exactly once**, never as extra capacity |
| Storage | Free space on the evaluated volume |

Never summed into a single capacity figure.

### Peak composition

Components carry a target and a lifecycle phase — `Load`, `Compile`, `SteadyStateGeneration`.

```
phase requirement (target) = Σ components coexisting in that phase mapping to that target
peak (target)              = max over phases
```

Every mandatory component has exactly one owner. TurboQuant **replaces** the standard KV
component set; the two never coexist.

### KV cache

```
KV bytes ≈ contextTokens × layers × kvHeads
           × (headDimK × bytesPerK + headDimV × bytesPerV)
           × parallelSequences
```

`headDim` derives from `EmbeddingSize / AttentionHeadCount` when both are known. If any input
is unknown the result is `NotEstablished`. Block-quantised caches use the real encoded block
size, not the nominal bit width — the examined SYCL `TQ3_0` stores a 32-value block in 14 bytes,
roughly 3.5 bits per value, not 3.

### Weight memory — recorded limitation

C1 receives model byte length and quantisation identifiers, not per-tensor sizes. Version one
bases weight memory on file length plus an explicit alignment and overhead term, and records
that limitation on the result. It is derived from a measured quantity, but is less precise than
a tensor-level read. A future GGUF tensor reader slots in behind the same component provider.

### Canonical quantisation

`ModelInspectionConfigurationEvidence` exposes `FileType : int?` and `QuantisationVersion : int?`,
not a single quantisation member. C1 defines its own canonical `WeightQuantisation` derived from
those two values. A display string is never used as a calculation input.

## 9. Safe budget and fit policy

```
SafeBudget(target) = FreshAvailable(target) − OsOrDriverAllowance − OperationalReserve
Required(target)   = PredictedPeak + CalibrationUnderpredictionMargin
```

Installed capacity may come from the handoff. **Current availability may not.**

### Policy provenance

Every reserve, allowance, threshold and margin lives in a versioned JSON asset with an explicit
provenance:

| Provenance | Behaviour |
|---|---|
| `Absent` | Evaluation returns `NotEstablished`. No constant is invented. |
| `Provisional` | Evaluation proceeds. A mandatory `UncalibratedEstimate` finding is attached, evidence grade is pinned to `Estimated`, and the UI shows the `ESTIMATED` badge and the calculation disclosure. |
| `Calibrated` | Backed by a recorded predicted-versus-measured dataset. The finding disappears and higher evidence grades become reachable. |

This is the difference between a fabricated constant and a documented default. Nothing is
presented as measured that was not measured, and provisional → calibrated is a data change,
not a code change.

Version one ships `Provisional` thresholds taken from the supplied workflow documents:

```
≤ 75% of safe budget  → Safe   (comfortable)
75–90%                → Safe   (moderate headroom)
90–100%               → Narrow (optimisation recommended)
> 100%                → DoesNotFit
```

### Fit states and bias

Per candidate: `Safe | Narrow | DoesNotFit | Unsupported | NotEstablished`, each with a limiting
reason — insufficient system memory, insufficient dedicated device memory, insufficient storage,
context exceeds model limit, evidence below admission level.

The bias is one-directional and deliberate. A false-safe result crashes the user's machine; a
false-unsafe result is an inconvenience. So the calibration margin is added to the requirement
and never subtracted, equality counts as fitting only after all mandatory margins are included,
and any unknown input collapses the candidate to `NotEstablished`. The calibrated release target
is zero known false-safe results.

## 10. Candidates

A candidate is immutable and complete: weights, KV format, context, backend, device, offload,
batch, runtime options, preparation requirement, support-entry id, experimental flag. No
partially-filled candidate exists anywhere in the system.

Generation is not a Cartesian product. Each candidate starts from exactly one admitted
support-matrix entry. Duplicates are removed by a deterministic fingerprint excluding path,
timestamp, username, machine name, free memory and UI state.

The **baseline** is the as-imported configuration, always evaluated first when structurally
supported; otherwise the exact reason is preserved.

### Support matrix resolution

```
DeclaredSupported + InstalledAndVerified → Available
DeclaredSupported + NotInstalled         → Unavailable
Experimental      + VerifiedAndOptedIn   → ExperimentalAvailable
Unknown / ambiguous                      → Unsupported
```

### Context ladder

```
1,024 · 2,048 · 4,096 · 8,192 · 16,384 · 32,768
```

Preservation target first, then baseline if different, then unique lower rungs. Never above the
trusted model limit, never above explicit user intent, never below the entry's minimum.
Automatic extension beyond the trained limit is excluded.

### Preparation

```
None                     — current artifact runs directly
RuntimeProfileOnly       — runtime settings change, no new model file
WeightConversionRequired — only with a trusted source and approved route
```

Converting a lower-precision model upward never restores quality and is never generated as an
upgrade. An already-quantised GGUF is never requantised by default without a suitable
higher-precision source (`W-11`, `DR-WF-009`).

## 11. Optimisation modes

Hard gates before a candidate reaches mode selection: complete support entry; fit state `Safe`
or `Narrow`; evidence at or above the entry's admission level; no requested-versus-actual backend
mismatch. Where an entry declares a quality, performance or stability threshold but no evidence
exists to test it, the candidate becomes `Unsupported` with reason `EvidenceBelowAdmissionLevel`.

Each mode is a **lexicographic comparison key**, not a weighted score.

| Mode | Ordering |
|---|---|
| Quality | preserve requested context → highest quality tier → highest evidence grade → non-experimental → least destructive preparation → performance → headroom → fingerprint |
| Efficiency | minimise worst pool pressure ratio → minimise added storage → maximise context → quality → performance → non-experimental → fingerprint |
| Balanced | preserve context where any safe candidate can → establish best safe quality tier → admit within one tier → maximise minimum headroom → performance → quality in band → least destructive preparation → fingerprint |
| Automatic | preserve context → non-experimental → avoid weight conversion → minimise distance from imported config → quality → evidence grade → headroom → performance → fingerprint |

Pressure ratio is `required / safeBudget` per pool; the worst pool controls. RAM and dedicated
VRAM are never added together.

Availability per mode: `Available` with a candidate, `Unavailable` with a stable reason, or
`NotEstablished`. Unavailable modes are disabled with an accessible explanation, never hidden.

`Use current model` is separate from the four modes and remains available whenever the baseline
is safe. It creates a runtime profile, not an artifact.

### Future mode-selection screen

`CompatibilityAssessment` already carries four `CompatibilityModeSelection` records, each with
the selected candidate id, availability, reason code and the ordered `SelectionFactor` list that
decided it. A later "Choose optimisation mode" page is therefore a view over data the engine
already produced — no estimator, generator or orchestrator change required. A
`SelectedModelConfigurationHandoff` and an explicit
`AnalysisResult → ModeSelection → ConfigurationSummary → Runtime` stage seam are defined now so
stage 4 slots into a route the shell already anticipates.

## 12. Presentation

Layout **A** — the approved Direction B grammar applied literally: model row, outcome card,
`.b-columns` at `1.35fr / .85fr` with a dominant facts card and stacked mini-cards, disclosure,
centred actions, 5-step stepper on *Check hardware fit*.

Approved geometry rules: every grid and flex child may shrink (`min-width:0`) so no pill or label
escapes its card; cards clip their content; columns stretch to equal height and the side stack
uses equal rows; fact tiles share one row height with the detail line pinned to the bottom; one
gap token drives all vertical rhythm.

Memory budget diagram: **brackets treatment on the main surface, legend treatment inside the
calculation disclosure.** Bar carries a dashed safe-limit rule with a named flag; reserved memory
is hatched, not coloured; segment labels reduce to numbers with words placed where they always
fit. When the model does not fit, the bar rescales to estimated peak so the overflow is visible
rather than clipped, and the footer names the limiting component.

### Ten states

`Analysing` (4 stages) · `EstimatedCompatible` · `…Details` · `OptimisationRequired` ·
`NoEstimatedSafeConfiguration` · `NotEstablished` · `Verifying` (4 stages) · `VerifiedCompatible` ·
`VerificationFailed` · `Cancelled`.

Analysis stages: analyse model requirements → check available execution paths → check memory and
safety limits → select the safest configuration.
Verification stages: validate selected backend and device → load the imported model → generate a
short response → record measured outcome.

**Production behaviour until M6:** `NotEstablished` (screen 06), listing the exact missing
evidence with recovery actions, and Continue visible-disabled with its accessible reason per V0 F9.

ViewModel mirrors `ModelInspectionViewModel`: navigation-owned instance, one automatic attempt per
navigation, attempt-generation stale rejection, `DelegateCommand` actions, immutable snapshot
applied as deltas to a stable control tree. No business logic in code-behind.

## 13. Continue predicate

Continue is enabled only when the Hardware outcome is `Completed` or `CompletedWithWarnings`
**and** all six conditions hold: current valid Model handoff; current usable Hardware handoff;
registered and available Block 3 route; both handoffs bound to the current `productHardwareRunId`
and expected `modelInspectionRunId`; Model handoff not stale, superseded, expired, consumed,
ambiguously reissued or associated with a failed rollback; navigation transaction completed and
not pending, failed, rolled back, duplicated or ambiguous. Any false or unknown condition leaves
it visible-disabled.

## 14. Privacy

No absolute or relative path, UNC path, filename, model name, canonical-path hash, raw worker or
tool output, native error, command, device identifier, hostname, credential or free-form provider
payload enters any C1 result, finding, log, snapshot, fixture or UI surface. Enforced by canary
tests across results, findings and fixtures.

Hardware providers receive no model data. Block 3 is the sole interpreter of the paired handoffs.

## 15. Testing

Pure unit tests: context policy, checked byte math, phase composition, budget, exact thresholds
one byte either side of every boundary, candidate completeness and ladder, mode ordering,
determinism under reordering and culture change.

Metamorphic properties that hold regardless of tuning:

- a larger context can never reduce logical KV payload
- adding a live component can never reduce its phase requirement
- changing the KV format can never alter non-KV weight payload
- shared GPU memory can never increase total system capacity

Architecture: the core library has no WinUI reference (compiler-enforced); ports default to
unavailable; the request carries no prohibited member.

`CMP-nnn` fixture catalogue mirrors `MI-001…MI-050`, Debug-only and Release-excluded, so all ten
screens are deterministically renderable and testable before adapters exist.

## 16. Milestones

| Milestone | Contents | Executable now |
|---|---|---|
| M1 | Core contracts, ports, policy identities, run identity | yes |
| M2 | Planning context, safe budget, fresh-availability port, fit classification | yes |
| M3 | Support matrix, GGUF candidate generation, four deterministic modes | yes |
| M4 | Orchestrator, cancellation, stale-run rejection, Continue predicate | yes |
| M5 | Ten screens, ViewModel, `CMP-nnn` fixture catalogue, accessibility | yes |
| M6 | Thin H1/I1 adapters plus contract-snapshot tests | **blocked on owners** |

## 17. Non-claims

This design does not implement or prove: Hardware Inspection; the Model Inspection handoff or its
registry; OpenVINO inspection or evaluation; runtime verification execution; optimisation
execution; calibrated estimator constants; any Gate 1 or Gate 2 evidence. Until M6 completes, no
compatibility conclusion about a real model on real hardware is produced by this feature, and the
production page states so.
