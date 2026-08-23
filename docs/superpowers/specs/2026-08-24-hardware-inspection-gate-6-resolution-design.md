# Hardware Inspection Gate 6 Evidence Resolution Design

**Status:** Approved by the user on 24 August 2026.

## Purpose

Gate 6 converts the separate, immutable Gate 3–5 provider evidence into one privacy-safe canonical `HardwareSnapshot`. It owns field authority, unit normalization, consistency comparison, tolerance, freshness, resolution, and provenance. It must fail closed when a critical fact is absent, stale, contradictory, or cannot be represented truthfully.

This gate does not collect hardware, start a process, register a provider, activate production composition, decide a terminal run outcome, create a handoff, calculate model compatibility, or change the WinUI journey. Gate 7 remains the only owner of orchestration and production activation.

## Architectural decision

Use one internal, versioned resolution subsystem with namespace `GraniteEdgeAI.Features.HardwareInspection.Resolution`. Its files live under the existing x64-only `Features/HardwareInspection/Infrastructure/Resolution` subtree because they reference Foundation evidence. It consumes the existing Foundation evidence types and produces only application-owned Domain types. Provider implementations remain unaware of canonical policy and cannot select themselves as authoritative.

The alternatives are rejected:

1. Provider-specific translators that populate a shared builder would distribute precedence decisions across components and make order affect the result.
2. A generic rules engine would add reflection, configuration parsing, and a second policy language without a current product need.

The selected resolver is deterministic, side-effect free except for an injected `TimeProvider`, has no service locator, and accepts no raw paths, commands, payloads, exception text, model data, or free-form diagnostics.

## Placement and dependency direction

- Provider DTOs remain in `GraniteEdgeAI.HardwareInspection.Foundation`.
- Resolution types live in the application project under `Features/HardwareInspection/Infrastructure/Resolution` because that x64-only subtree already references Foundation while the application project owns `HardwareSnapshot`.
- The resolver and its input/result types are internal. Gate 7 may consume them inside the same assembly; the existing unit-test friend assembly verifies them directly.
- Public Domain types remain provider-neutral. Gate 6 does not add a Foundation dependency to Domain classes.
- Production onboarding continues to compose `UnavailableHardwareInspectionService` throughout Gate 6.

## Fixed v1 policy

The policy identifier is exactly `hardware-policy-v1`; the snapshot schema remains `1`.

### Clock and freshness

The resolver receives UTC time from an injected `TimeProvider` and rejects a non-UTC resolution time. Timestamp arithmetic is checked and follows these closed limits:

| Rule | Limit |
|---|---:|
| Future clock skew | 5 seconds maximum |
| Windows dynamic memory age | 30 seconds maximum |
| Other available evidence age | 5 minutes maximum |
| Span between oldest and newest accepted available observations | 60 seconds maximum |

A timestamp more than five seconds in the future is invalid, not fresh. A stale or future critical observation prevents a snapshot. A stale optional observation is represented as unavailable provenance and cannot populate canonical facts.

### Unit conversion and numeric comparison

- Bytes are canonical.
- LLM Fit GiB values are multiplied by exactly `1,073,741,824` using checked `decimal` arithmetic and rounded to the nearest byte with `MidpointRounding.ToEven`.
- Conversion overflow, non-finite input, negative input, or a post-conversion relationship violation is a closed normalization failure.
- Logical processor counts must agree exactly when both LLM Fit and Windows observations are available.
- CPU names corroborate only under `StringComparison.OrdinalIgnoreCase`. No punctuation, trademark, whitespace, token, or vendor-name stripping is allowed because it could equate different processors. The accepted canonical spelling is the primary source spelling.
- LLM Fit total RAM is compared with Windows OS-usable RAM using a fixed 1 GiB tolerance.
- LLM Fit available RAM is compared with fresh Windows available RAM using `max(2 GiB, 10% of Windows OS-usable RAM)`.
- Values outside tolerance are conflicts. Values are never averaged, clamped, or silently replaced.

The earlier architecture described LLM Fit total RAM as preferred installed-memory evidence. Gate 1 established only a comparison with Windows total physical memory and did not prove that the candidate field means physically installed DIMM capacity. Gate 6 therefore keeps LLM Fit as the primary overall collection route but does not relabel its `total_ram_gb` as physically installed bytes. Windows `GetPhysicallyInstalledSystemMemory` supplies `PhysicallyInstalledBytes`; LLM Fit total RAM corroborates Windows OS-usable bytes. This is a stricter semantic interpretation, not a competing compatibility engine.

## Collected input boundary

`CollectedHardwareEvidence` is an immutable internal carrier for exactly:

- `LlmFitHardwareEvidence`;
- `WindowsProcessorEvidence`;
- an explicit available/unavailable observation of `WindowsSystemSnapshot`;
- `WindowsStorageEvidence`;
- `DxgiGraphicsEvidence`;
- `NeuralProcessorEvidence`;
- `LlamaCppCapabilityEvidence`.

The Windows-system observation wrapper exists because that provider currently reports capture failure through a closed exception instead of an unavailable evidence object. Its unavailable form contains only attempt time plus a closed Gate 6 diagnostic enum. It cannot contain exception text.

Every collection is copied at construction. Null evidence, undefined enums, invalid state/value combinations, duplicate values, and unbounded diagnostic collections are rejected before resolution. Cancellation is not converted into unavailable evidence; Gate 7 owns cancellation and must not call the resolver for a cancelled run.

## Field authority and canonical mapping

### Processor

| Canonical field | Primary | Corroboration/fallback | Failure policy |
|---|---|---|---|
| `processor.name` | Available LLM Fit name | Windows name fallback | Mismatch is a critical conflict; both absent is unavailable |
| `processor.logicalProcessors` | Available LLM Fit count | Windows count fallback | Exact mismatch is a critical conflict |
| `processor.physicalCores` | Windows | None | Missing is critical |
| `processor.architecture` | Windows native architecture | None | Missing is critical |
| `processor.instructionSets` | No approved provider in Gates 3–5 | None | Empty collection plus unavailable provenance; no absence claim |

Windows physical cores must not exceed the resolved logical count. Any cross-source topology contradiction prevents a snapshot.

### Memory and operating system

Windows is canonical for physically installed, OS-usable, and currently available bytes because those meanings are structurally distinct and directly exposed by the approved Windows APIs. LLM Fit total and available GiB values are corroborating observations under the fixed tolerances above. A discrepancy outside tolerance on any of the four v1 critical CPU/RAM keys prevents a usable snapshot.

The Windows system observation is also the sole source for OS name, version, and architecture. All three memory relationships must remain `installed >= OS-usable >= available`. Dynamic available-memory provenance retains the Windows capture timestamp rather than the resolver time.

### Graphics

DXGI is canonical for adapter identity and the three separate memory categories. Adapters retain DXGI ordinal order. Dedicated video, dedicated system, and shared system bytes are never summed or relabelled.

LLM Fit GPU evidence may corroborate only whether graphics were reported. Gate 1 did not establish its memory semantics, and Gate 3 intentionally retains no GPU memory value. Gate 6 performs no name-based fuzzy join, vendor inference, Intel classification, primary-adapter selection, usable-memory calculation, or count equality assertion between LLM Fit and DXGI.

If DXGI is unavailable and valid LLM Fit evidence reports GPUs, the resolver may create fallback adapters with the bounded LLM Fit names and all three memory fields `null`. If both sources lack usable graphics evidence, the snapshot uses an empty adapter collection with unavailable provenance; this means unresolved graphics evidence, not proven hardware absence. Gate 8 presentation must consult provenance before describing an empty collection.

### Neural processor

Only `NeuralProcessorEvidence` may populate NPU facts. `Present`, `NotPresent`, and `DetectionUnavailable` map one-to-one. CPU, GPU, LLM Fit, and llama.cpp behavior can never infer NPU presence or absence.

### Storage

The Windows system-volume provider is the sole authority. Capacity and caller-available bytes remain distinct and path-free. Because the v1 `StorageFacts` contract cannot represent unavailable capacity without fabrication, unavailable or stale storage prevents snapshot construction.

### Local runtime

Only available Gate 5 llama.cpp capability evidence with the exact pinned identity may populate `LocalRuntimeCapabilities`. The canonical backend is exactly `Cpu`; visible devices preserve contiguous probe order and expose only bounded buffer-type labels. Runtime unavailability prevents snapshot construction. Capability is not model execution proof.

## Resolution and provenance

The resolver processes data in a fixed order:

1. validate input states and bounds;
2. evaluate every timestamp against the v1 clock policy;
3. normalize units with checked arithmetic;
4. calculate consistency observations without selecting a winner;
5. apply field authority and fallback rules;
6. build exactly one evidence-manifest entry per canonical field;
7. construct the canonical snapshot only if every construction-critical and v1 critical decision is safe.

Manifest field keys are fixed and ordinal:

- `processor.name`, `processor.architecture`, `processor.physicalCores`, `processor.logicalProcessors`, `processor.instructionSets`;
- `memory.installedBytes`, `memory.osUsableBytes`, `memory.availableBytes`;
- `graphics.adapters`, `graphics.memory`;
- `npu.state`;
- `storage.systemVolumeCapacityBytes`, `storage.systemVolumeAvailableBytes`;
- `os.name`, `os.version`, `os.architecture`;
- `runtime.buildIdentity`, `runtime.backends`, `runtime.visibleDevices`.

Each entry records one source, source capture time, confidence, resolution state, and at most one closed lowercase diagnostic token. `ResolvedPrimary`, `ResolvedCorroborated`, and `ResolvedFallback` are the only resolved states. Critical `Conflict` or `Unavailable` entries prevent a snapshot. The resolver never labels an unresolved value resolved merely to satisfy the `HardwareSnapshot` constructor.

`HardwareEvidenceResolutionResult` contains an optional `HardwareSnapshot`, the complete `HardwareEvidenceManifest`, and a bounded, sorted, unique list of closed `HardwareResolutionDiagnosticCode` values. Success has a usable schema-v1 snapshot. Failure has no snapshot; it does not construct a partially populated or fabricated `NotUsable` snapshot. Gate 7 later maps this result to the four product outcomes exactly once.

The caller supplies a non-empty snapshot UUID. Snapshot `CapturedAtUtc` is the resolver time; individual source times remain in provenance and available-memory facts. This keeps identity and clocks deterministic in tests and avoids hidden GUID generation.

## Diagnostic and privacy rules

- Diagnostics are closed enums mapped to lowercase ASCII tokens of at most 96 characters.
- Diagnostic ordering is enum order; duplicates are removed under a fixed input bound.
- No diagnostic includes a provider value, device label, path, username, hostname, tool output, hash, exception message, or model fact.
- Raw LLM Fit output hashes stay in infrastructure evidence and are not copied into the Domain snapshot or Gate 6 diagnostics.
- No logging, telemetry, persistence, network access, shell, environment lookup, registry access, or native call occurs in the resolver.

## Failure table

| Condition | Result |
|---|---|
| LLM Fit CPU missing, valid Windows CPU present | Resolved fallback |
| LLM Fit and Windows CPU names or logical counts disagree | Critical conflict; no snapshot |
| Windows physical core count/architecture missing | Unavailable; no snapshot |
| Windows system memory missing, stale, future, or inconsistent | Unavailable/conflict; no snapshot |
| LLM Fit RAM outside approved tolerance | Critical conflict; no snapshot |
| DXGI unavailable, LLM Fit reports bounded GPUs | Graphics fallback with null memory categories |
| Both graphics sources unavailable | Empty graphics plus unavailable provenance; no false absence claim |
| NPU mechanism unavailable | `DetectionUnavailable`; snapshot may remain usable |
| Storage unavailable | No snapshot |
| llama.cpp capability unavailable or wrong identity | No snapshot |
| Optional evidence stale | Excluded and marked unavailable |
| Accepted evidence exceeds the 60-second capture span | No snapshot |
| Any checked conversion overflows | No snapshot |

## Test strategy

Implementation follows strict red-green-refactor slices. Every new production behavior first receives a focused failing MSTest and is observed failing for the intended missing behavior.

Tests use only synthetic evidence and a deterministic `TimeProvider`; they never inspect the development host. Required coverage includes:

- exact policy constants, field-key inventory, and closed diagnostics;
- immutable input and result collections;
- GiB conversion boundaries and midpoint behavior;
- every freshness edge at just below, exactly at, and just above its limit;
- CPU name/count primary, corroborated, fallback, mismatch, and unavailable rows;
- installed/usable/available memory tolerance boundaries and invariant violations;
- graphics DXGI authority, LLM Fit fallback, null memory semantics, zero adapters, and unavailable provenance;
- all three NPU states without inference;
- storage and runtime required/unavailable rows;
- manifest source, timestamp, confidence, resolution, key uniqueness, and deterministic ordering;
- absence of a snapshot for every critical conflict or missing construction fact;
- no provider DTO, raw output, path, model, compatibility, process, network, or WinUI type escaping into Domain;
- production composition remaining `UnavailableHardwareInspectionService`.

Focused tests are followed by the full Foundation suite, probe tests, authoritative packaged Hardware Inspection/model-handoff/onboarding regression, hardware/runner Python contracts, Debug/x64 packaged build, Release/x64 app build, source/dependency/privacy audits, and independent code review. Gate 6 evidence records actual discovered counts rather than planned minima.

## Completion and non-claims

Gate 6 is complete only after the decision tables, full regressions, boundary audits, independent review, and evidence documentation pass at one exact committed source head.

Gate 6 does not prove supported Intel-machine end-to-end behavior, public-trust signing, Smart App Control acceptance, orchestration, cancellation lifecycle, progress, UI integration, handoff creation, compatibility, or model fit. Those remain Gates 7–9. Production service composition remains unavailable after this gate.
