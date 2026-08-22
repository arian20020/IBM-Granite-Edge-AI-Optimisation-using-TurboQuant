# Hardware Inspection Contract Foundation v1 Design

**Status:** Approved for implementation by the user on 20 August 2026.

## Purpose

Create the smallest production contract that lets Hardware Inspection, its WinUI presentation, and the later model/hardware compatibility feature exchange truthful hardware facts without exposing provider DTOs, raw paths, commands, process output, or compatibility conclusions.

This slice does not collect hardware, execute LLM Fit or llama.cpp, contact the Intel laptop, close Gate 1, or enable compatibility navigation. It establishes types only.

## Authority and precedence

1. The approved Hardware Inspection architecture controls domain semantics, evidence ownership, four terminal outcomes, and provider isolation.
2. The approved C0 I1/S1 decision controls execution-plane separation and keeps model data out of Hardware providers.
3. The approved V0 visual contract consumes these types but does not change their semantics.
4. Gate 1 remains Blocked and Gate 2 remains prohibited until their separate evidence gates pass.

## Placement

The contract lives under `GraniteEdgeAI.Features.HardwareInspection` in the application project. It has no WinUI, JSON, native API, process, LLM Fit, llama.cpp, OpenVINO, or model-inspection dependency. The existing unit-test assembly already references the application assembly.

This placement avoids a premature project-graph change. A later architecture gate may extract the pure types into a dedicated assembly only through an explicit ADR without changing their public semantics.

## Public contract

### Terminal outcome

`HardwareInspectionOutcome` contains exactly:

- `Completed`
- `CompletedWithWarnings`
- `Failed`
- `Cancelled`

### Snapshot

`HardwareSnapshot` is immutable after construction and contains:

- `SnapshotId`, `CapturedAtUtc`, `SchemaVersion`, and `PolicyVersion`;
- processor identity, architecture, physical cores, logical processors, and instruction sets;
- physically installed, OS-usable, and currently available memory as three separate byte values, with a capture time for available memory;
- zero or more graphics adapters, keeping dedicated video, dedicated system, and shared system memory separate;
- explicit NPU state: `Present`, `NotPresent`, or `DetectionUnavailable`;
- system-volume capacity and available bytes without a path;
- operating-system name, version, and architecture;
- pinned local-runtime build identity, supported backends, and visible devices, without model-execution claims;
- an evidence manifest whose entries identify a canonical field, source kind, resolution state, capture time, confidence, and optional privacy-safe diagnostic code;
- explicit `Usable` or `NotUsable` normalization status.

The constructor rejects empty identities, non-UTC timestamps, impossible core/memory/storage relationships, duplicate canonical evidence keys, blank collection values, and a `Usable` snapshot whose required CPU/RAM evidence is not resolved.

Required evidence keys for v1 usability are `processor.name`, `memory.installedBytes`, `memory.osUsableBytes`, and `memory.availableBytes`. `Conflict` and `Unavailable` are not resolved states.

### Handoff

`HardwareInspectionHandoff` contains exactly:

- `InspectionId`
- `Snapshot`

Its factory rejects an empty run identity, `Failed`, `Cancelled`, or a non-usable snapshot. It accepts only `Completed` and `CompletedWithWarnings` with a usable snapshot. It contains no model information and no compatibility result.

### Fresh-memory seam

`IAvailableMemoryProvider.CaptureAsync(CancellationToken)` returns `AvailableMemorySnapshot`, containing only `AvailablePhysicalBytes` and an exact UTC `CapturedAtUtc`. This makes volatile available memory refreshable without rebuilding or mutating the canonical snapshot.

## Privacy and compatibility boundaries

- No path, filename, hostname, username, device serial, raw provider payload, command, stderr/stdout, credential, or free-form native diagnostic is represented.
- `SafeDiagnosticCode` is a bounded token, not arbitrary diagnostic text.
- Graphics memory categories are never added together by the contract.
- Runtime capability is not proof that a model ran.
- NPU absence is distinct from inability to detect an NPU.
- Hardware providers never receive model metadata or either model handoff.
- The future Block 3 compatibility feature may read the completed Hardware handoff and the separately approved six-field Model handoff; Hardware Inspection does not interpret the model handoff.

## Verification

Unit tests must prove:

- exact outcome and enum members;
- valid immutable snapshot construction;
- invalid timestamps, values, duplicate evidence, and false-usable snapshots fail closed;
- graphics memory categories remain distinct;
- NPU states remain distinct;
- only actionable outcomes create a handoff;
- handoff public shape is exactly two properties;
- available-memory refresh records bytes and UTC capture time;
- public production types expose no banned privacy or model/compatibility members.

The contract snapshot document records names, fields, units, nullability, enum values, privacy exclusions, and an owner commit for downstream workers.

## Deferred work

Providers, evidence collection, validators, resolver policy, normalization, orchestration, UI, navigation, compatibility calculation, OpenVINO/GGUF interpretation, and Intel-machine execution remain separate gated work. This foundation must not fabricate evidence or relax those gates.
