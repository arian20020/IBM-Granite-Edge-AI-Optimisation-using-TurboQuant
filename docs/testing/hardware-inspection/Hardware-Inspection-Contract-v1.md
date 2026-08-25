# Hardware Inspection Contract v1

**Owner implementation commit:** `0b0b98cb5436735a14939dae556be73e341d7685`

**Namespace roots:**

- `GraniteEdgeAI.Features.HardwareInspection.Domain`
- `GraniteEdgeAI.Features.HardwareInspection.Application`

## Actionable handoff

`HardwareInspectionHandoff` has exactly two public instance properties:

| Property | Type | Meaning |
|---|---|---|
| `InspectionId` | `Guid` | Non-empty identity of the product Hardware Inspection run. |
| `Snapshot` | `HardwareSnapshot` | Immutable, usable canonical hardware facts and evidence. |

Creation is permitted only for `Completed` or `CompletedWithWarnings` with a `Usable` snapshot. `Failed`, `Cancelled`, an empty inspection identity, or a `NotUsable` snapshot fails closed.

## Terminal outcomes

`HardwareInspectionOutcome` contains exactly `Completed`, `CompletedWithWarnings`, `Failed`, and `Cancelled`.

## Canonical snapshot

| Block | Public members | Units/nullability |
|---|---|---|
| Identity | `SnapshotId`, `CapturedAtUtc`, `SchemaVersion`, `PolicyVersion` | Non-empty UUID; UTC; positive version; non-blank policy token. |
| Processor | `Name`, `Architecture`, `PhysicalCoreCount`, `LogicalProcessorCount`, `InstructionSets` | Counts are positive; logical is not below physical. |
| Memory | `PhysicallyInstalledBytes`, `OsUsablePhysicalBytes`, `AvailablePhysicalBytes`, `AvailableCapturedAtUtc` | Unsigned bytes; installed >= usable >= available; timestamp is UTC. |
| Graphics adapter | `Name`, `DedicatedVideoMemoryBytes`, `DedicatedSystemMemoryBytes`, `SharedSystemMemoryBytes` | Each memory category is an independent nullable unsigned-byte value and must never be summed or relabelled. |
| Neural processor | `State`, `Name` | `Name` required only for `Present`; states are `Present`, `NotPresent`, `DetectionUnavailable`. |
| Storage | `SystemVolumeCapacityBytes`, `SystemVolumeAvailableBytes` | Unsigned bytes; no volume path; available <= capacity. |
| Operating system | `Name`, `Version`, `Architecture` | Non-blank safe values; no host/user identity. |
| Local runtime | `BuildIdentity`, `SupportedBackends`, `VisibleDevices` | Backends are `Cpu`, `Sycl`, `Vulkan`; capability is not model-execution proof. |
| Evidence | `Entries` | Canonical field, source, resolution, UTC capture, confidence, optional bounded safe diagnostic code. |
| Usability | `Usability` | Exactly `Usable` or `NotUsable`. |

Collections are copied at construction and exposed read-only.

## Evidence contract

Sources are `LlmFit`, `Windows`, `Dxgi`, `NeuralProcessorProbe`, and `LlamaCpp`.

Resolution states are `ResolvedPrimary`, `ResolvedCorroborated`, `ResolvedFallback`, `Conflict`, and `Unavailable`. Only the first three count as resolved.

A v1 `Usable` snapshot requires resolved entries for exactly these minimum critical keys:

- `processor.name`
- `memory.installedBytes`
- `memory.osUsableBytes`
- `memory.availableBytes`

Later Gate 6 normalization may require more field-level evidence; it may not weaken these minimum keys.

## Fresh available-memory seam

`IAvailableMemoryProvider.CaptureAsync(CancellationToken)` returns `ValueTask<AvailableMemorySnapshot>`. The value contains `AvailablePhysicalBytes` and `CapturedAtUtc`. Zero available bytes is valid; the timestamp must be UTC. This volatile sample can be refreshed without mutating a prior snapshot.

## Privacy and cross-feature boundary

These contracts contain no raw path, filename, hostname, username, device serial, command, stdout/stderr, provider payload, credential, free-form native diagnostic, model metadata, or compatibility result.

The contract is neutral to GGUF and OpenVINO. Model formats are inspected by Model Inspection and interpreted for fit only by Block 3. Hardware providers never receive either model format, model metadata, or the Model Inspection handoff.

`HardwareInspectionHandoff` can be carried beside the separately approved six-field `ModelInspectionHandoff`; Hardware Inspection does not join or interpret them.

## Non-claims

This contract does not implement a provider, collect evidence, execute hardware tools, prove Intel behavior, calculate compatibility, enable Continue, close Gate 1, or authorize Gate 2.
