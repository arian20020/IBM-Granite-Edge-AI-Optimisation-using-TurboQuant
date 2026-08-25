# Hardware Inspection Gate 4 Windows and DXGI Enrichment Design

**Status:** Approved
**Date:** 2026-08-23
**Parent design:** `docs/superpowers/specs/2026-08-15-hardware-inspection-production-design.md`
**Foundation:** Gates 2 and 3 through `19f7f5f`

## 1. Goal

Implement provider-specific Windows processor, memory/OS, DXGI graphics, system-volume storage, and provisional neural-processor evidence in `GraniteEdgeAI.HardwareInspection.Foundation`. Gate 4 must corroborate the accepted Intel CPU/RAM/GPU shape without resolving competing sources, fabricating NPU presence, activating product collection, or exposing native implementation details.

## 2. Chosen approach

Three approaches were considered:

1. **Separate provider-specific evidence boundaries — selected.** Windows processor, Windows memory/OS, DXGI graphics, system-volume storage, and NPU evidence remain distinct. Gate 6 later applies authority, tolerance, freshness, consistency, and canonical resolution.
2. **One combined Windows enrichment object.** This reduces surface area initially but couples independent failure and freshness semantics and makes optional-probe isolation harder in Gate 7.
3. **Populate app-domain `HardwareSnapshot` facts directly.** This is rejected because it bypasses Gate 6 provenance and could silently turn corroborating Windows data into canonical truth.

The selected approach follows the parent architecture: infrastructure owns native APIs and provider DTOs, while only the future resolver may choose canonical facts.

## 3. Scope and non-scope

Gate 4 includes:

- Windows processor name, native architecture, physical-core count, and logical-processor count;
- the existing distinct installed, OS-usable, and currently available memory values and OS facts, with bounded safe text invariants;
- system-volume total capacity and bytes available to the caller, without retaining its path;
- DXGI 1.1 adapter enumeration with bounded adapter count, safe names, hardware/software/remote classification, vendor/device IDs, and separate dedicated-video, dedicated-system, and shared-system memory;
- an `INeuralProcessorProbe` boundary and immutable `Present`, `NotPresent`, and `DetectionUnavailable` evidence;
- a production default NPU probe that returns `DetectionUnavailable` until the separately approved NPU spike selects an enumeration mechanism;
- deterministic fake-native tests and privacy-safe Windows integration smoke tests.

Gate 4 does not include:

- canonical `HardwareSnapshot` construction, source selection, tolerance, conflict resolution, normalization, or provenance manifests;
- LLM Fit/Windows/DXGI comparison policy;
- NPU inference from CPU, GPU, registry naming, DirectML, OpenVINO, or runtime behavior;
- the exact Windows NPU enumeration mechanism;
- llama.cpp capability collection;
- orchestration, concurrency policy, progress, outcomes, handoffs, ViewModel/UI, onboarding, or app composition;
- paths, raw HRESULTs, registry paths, native structs, exception text, telemetry, persistence, or logging in evidence.

`UnavailableHardwareInspectionService` remains the sole production composition.

## 4. Project placement and contracts

All Gate 4 production types remain in `GraniteEdgeAI.HardwareInspection.Foundation`:

- `Windows/WindowsProcessorEvidence.cs` and `WindowsProcessorEvidenceProvider.cs`;
- the existing `WindowsSystemSnapshot` and `WindowsSystemSnapshotProvider` for memory/OS;
- `Windows/WindowsStorageEvidence.cs` and `WindowsStorageEvidenceProvider.cs`;
- `Dxgi/DxgiGraphicsEvidence.cs`, `DxgiGraphicsEvidenceProvider.cs`, and the internal DXGI COM adapter;
- `NeuralProcessors/INeuralProcessorProbe.cs`, `NeuralProcessorEvidence.cs`, and `UnavailableNeuralProcessorProbe.cs`;
- one internal bounded-hardware-text validator shared by new evidence and used to align existing Windows/LLM Fit text invariants where safe.

No Gate 4 type references WinUI, app-domain `HardwareSnapshot`, Model Inspection, LLM Fit resolution policy, or presentation types.

## 5. Windows processor evidence

`WindowsProcessorEvidence` is immutable and has `Available` or `Unavailable` state.

Available evidence contains:

- bounded processor name;
- native architecture (`X64`, `Arm64`, or another closed enum value explicitly supported by the API mapping);
- physical-core count from `GetLogicalProcessorInformationEx(RelationProcessorCore)`;
- active logical-processor count from `GetActiveProcessorCount(ALL_PROCESSOR_GROUPS)`;
- UTC capture time;
- no diagnostics.

Unavailable evidence contains no processor facts and exactly one closed diagnostic such as name unavailable, topology unavailable, topology inconsistent, unsupported architecture, or native API unavailable.

The production adapter reads the processor name from the Windows hardware-description registry and native topology from Kernel32. Registry/native paths and errors never enter evidence. Physical cores and logical processors are bounded to `1..4096`, and physical cores cannot exceed logical processors. Variable-length topology records are parsed only after validating returned byte counts, record sizes, bounds, and forward progress. No unsafe code is enabled.

## 6. Memory and operating-system evidence

The existing `WindowsSystemSnapshotProvider` remains the authority for:

- physically installed bytes;
- OS-usable physical bytes;
- currently available physical bytes;
- UTC capture time;
- OS name, version, and architecture.

Gate 4 preserves `installed >= usable >= available`, checked KiB-to-byte conversion, cancellation, and safe diagnostic exceptions. OS strings gain the same bounded printable-text validation used by other provider evidence. Dynamic available memory remains timestamped and is not compared with LLM Fit until Gate 6.

## 7. System-volume storage evidence

`WindowsStorageEvidenceProvider` uses an internal injectable API that obtains the Windows system directory, derives its volume root internally, and calls `GetDiskFreeSpaceExW`.

Available evidence contains only:

- total volume capacity bytes;
- bytes available to the current caller;
- UTC capture time.

The volume path is never retained. Capacity must be positive and available bytes cannot exceed capacity. API failure, invalid root, overflow, or inconsistent values map to one closed unavailable diagnostic. Cancellation is checked before native work.

## 8. DXGI graphics evidence

`DxgiGraphicsEvidenceProvider` uses a minimal internal DXGI 1.1 COM seam implemented with `CreateDXGIFactory1`, `IDXGIFactory1.EnumAdapters1`, and `IDXGIAdapter1.GetDesc1`. No graphics device or rendering context is created, and no third-party package is added.

Enumeration rules:

- `DXGI_ERROR_NOT_FOUND` ends successful enumeration;
- any other failed HRESULT maps internally to a closed outcome and never crosses into evidence;
- at most 64 adapters may be retained; attempting to enumerate a 65th fails closed;
- every COM object is released in `finally`;
- adapter descriptions are normalized only by removing the fixed native buffer terminator and boundary whitespace, then validated as 1..256 printable Unicode scalar values;
- vendor ID and device ID are retained as unsigned values;
- adapter kind is a closed `Hardware`, `Software`, or `Remote` enum derived only from DXGI flags;
- dedicated video, dedicated system, and shared system memory remain separate `ulong` fields and are never summed or relabelled;
- adapter names must be unique by a stable provider identity comprising kind, vendor ID, device ID, and ordinal occurrence rather than name alone.

Successful enumeration with zero adapters is `Available` with an empty list and means graphics hardware was not reported by DXGI. Factory/enumeration/description/limit failures are `Unavailable`, never false absence. The provider does not decide which adapter is Intel or canonical; Gate 6 owns that policy.

## 9. Neural-processor boundary

`INeuralProcessorProbe` exposes one cancellation-aware capture method returning `NeuralProcessorEvidence`.

Evidence states are structurally distinct:

- `Present` requires one bounded device name and no diagnostic;
- `NotPresent` requires no name and no diagnostic;
- `DetectionUnavailable` requires no name and exactly one closed diagnostic.

`UnavailableNeuralProcessorProbe` is the production-safe default and always returns `DetectionUnavailable(EnumerationMechanismNotApproved)` after honoring cancellation. It does not query or infer hardware. Deterministic tests use fake probes to prove all three states. A later approved NPU spike may add an implementation without changing this contract.

## 10. Security, privacy, and resource properties

- Native APIs are reached only through small internal adapters with deterministic seams.
- Native buffer sizes, record sizes, indexes, counts, and collection growth are bounded before allocation or retention.
- COM ownership is explicit and released on success and every failure path.
- Evidence contains only bounded hardware facts, UTC timestamps, state, and closed enum diagnostics.
- No paths, registry locations, HRESULT values, native handles, exception messages, stack traces, usernames, environment values, or raw structures cross the provider boundary.
- No new shell, process, network, dashboard, listener, package acquisition, or elevated operation is introduced.
- Genuine zero DXGI adapters and an approved NPU `NotPresent` result are facts. Any inability to enumerate is `Unavailable`/`DetectionUnavailable`.
- Provider evidence is corroborating input only; it cannot enable compatibility or continuation.

## 11. Testing and verification

Test-first slices cover:

1. bounded shared text and Windows memory/OS invariants;
2. processor DTO/provider contracts and adverse topology buffers;
3. storage DTO/provider success, cancellation, API failure, and inconsistent values;
4. DXGI DTO/provider success with integrated/discrete/software/remote/zero adapters, memory separation, 65-adapter rejection, duplicate display names, and every closed failure;
5. NPU present/not-present/unavailable invariants and safe default behavior;
6. privacy-safe Windows integration smoke tests and full Gate 2/3/app regressions.

Gate 4 closure requires:

- all foundation tests pass with zero skipped;
- real Windows processor, memory/OS, storage, and DXGI smoke tests return bounded evidence without recording host-identifying values;
- NPU default is explicitly `DetectionUnavailable`, not `NotPresent`;
- Gate 3 foundation, 174 deterministic Gate tests, 114 packaged tests, Stage A and Stage 0/acquisition/public/theme contracts remain green;
- Debug/Release x64 app builds have zero errors;
- no candidate, native capture, path, HRESULT, TRX, or host hardware value is tracked or packaged;
- production composition remains unavailable;
- independent review has no unresolved Critical or Important finding.

## 12. Exit and next gate

Gate 4 exits with independent Windows, DXGI, storage, and provisional NPU evidence only. It does not construct canonical domain facts or compare them with LLM Fit.

Gate 5 is next and adds the pinned llama.cpp version/backend/visible-device capability subsystem without loading native runtime code into the app or test host.
