# OpenVINO CPU KV Physical-Allocation Observability Design

## Approval and Scope

The user approved this design boundary on 2026-07-28 so WB-04 can record
actual CPU KV-cache allocation and precision rather than values inferred from
configuration labels, public tensor shapes, or process memory.

This design covers one bounded sub-project:

1. add a private, default-off physical-state observer to a derived OpenVINO
   `2026.2.1` CPU plugin;
2. bind that observer to the derived OpenVINO GenAI TurboQuant runtime;
3. produce exact post-inference KV allocation evidence for native
   STANDARD/scalar and project TBQ3/TBQ4 state paths.

The typed 60-row matrix, full Granite execution, quality scoring, suitable-host
transfer, and workbook rendering remain later sub-projects. They may consume
this observer only after its implementation and provenance are independently
approved.

## Problem

OpenVINO's public `InferRequest::query_state()` returns model-facing
`VariableState` tensors. On the CPU plugin's optimized stateful-SDPA path,
`VariableStateKVcache::get_state()` can allocate an external tensor and
dequantize or convert an internal U8/U4 cache into the model precision.
Consequently:

- the returned element type is not proof of the internal cache precision;
- `Tensor::get_byte_size()` is not proof of the native backing allocation;
- a public state query can itself allocate a large external copy and perturb
  memory measurements;
- generic project TurboQuant state uses double buffers, so one returned tensor
  omits the second retained backing block;
- native quantized state owns scale/zero-point and beam-table storage that is
  absent from the public tensor.

WB-04 must not call these logical exported bytes physical KV allocation.

## Decision

Create a derived OpenVINO core checkout at exact upstream commit
`ede283a88e35465f0d680dabbf1f44080f8fc387`. Add a private observer compiled
only with `CPU_DEBUG_CAPS` and enabled only by an explicit JSONL output path.
The official checkout and official wheel remain immutable.

The measured runtime uses:

- the derived CPU plugin with the observer compiled in;
- the observer enabled only for controlled measurement processes;
- the accepted derived OpenVINO GenAI TurboQuant checkout;
- one post-inference physical snapshot taken before any public
  `VariableState::get_state()` materialization.

Every result visibly identifies the upstream commits, observer patch commit,
GenAI patch commit, plugin and runtime hashes, build options, and evidence
hashes. Project-added measurement capability is never described as
upstream-shipped OpenVINO functionality.

## Provenance Routes

Three immutable identities remain separate:

1. **Clean official reference**
   - OpenVINO `2026.2.1` at `ede283a...`;
   - OpenVINO GenAI `2026.2.1.0` at `7dea045...`;
   - official wheel/plugin hashes;
   - no project instrumentation.
2. **Observer measurement runtime**
   - the same OpenVINO upstream commit plus the exact CPU observer patch;
   - `CPU_DEBUG_CAPS=ON`;
   - exact derived OpenVINO commit, tree, patch SHA-256, plugin SHA-256, and
     build transcript hashes.
3. **Observer plus TurboQuant runtime**
   - the observer measurement runtime;
   - OpenVINO GenAI upstream commit plus the exact accepted TurboQuant patch
     series and derived commit;
   - exact GenAI binary/library hashes and build transcript hashes.

The clean official route remains a reproducibility and behavior-equivalence
reference. Runtime rows that require physical allocation use one internally
coherent observer-build identity; values from different binaries are not
silently combined into one sample.

Before Granite execution, a synthetic equivalence gate must prove that the
default-off observer build preserves the clean route's output, runtime graph,
selected device, cache precision configuration, and no-fallback behavior for
matched fixtures.

## CPU Observer Architecture

### Trigger

The observer runs at the leaf of CPU
`SyncInferRequest::query_state()`, before public wrappers call `get_state()`.
It is compiled only under `CPU_DEBUG_CAPS` and remains inert unless
`OV_CPU_STATE_ALLOCATION_DUMP_PATH` names a valid `.jsonl` file.

The controlled process also supplies
`OV_CPU_STATE_ALLOCATION_PHASE=post_infer`. Accepted WB-04 evidence permits
only the exact `post_infer` phase for steady-state allocation claims.

### Internal Data Sources

The observer reads existing private CPU state interfaces:

- `IVariableState::input_mem()`;
- `IVariableState::output_mem()`;
- `IVariableState::internal_desc()`;
- `VariableStateKVcache::internal_state_mem()`;
- `VariableStateKVcache::hidden_state_mem()`;
- `VariableStateKVcache::get_scale_zp()`.

A narrow debug-only accessor on `DnnlMemoryBlock` exposes the retained
`MemoryBlockWithReuse::size()` capacity. Unsupported backing blocks produce a
typed `allocation_unknown` record and fail acceptance; descriptor size is
never substituted as a guess.

### Physical Byte Definition

The authoritative physical KV state-data total is:

1. the sum of unique retained backing-block capacities for all state-owned
   input/output/internal memories;
2. plus hidden beam-table retained capacity;
3. plus owned scale/zero-point `PlainTensor` capacity;
4. with aliasing deduplicated by stable per-snapshot block identity.

It excludes C++ object headers, allocator metadata, guard pages, page
residency, model weights, scratchpads, and process-wide memory. Those remain
separate process-memory metrics.

Each memory record preserves both:

- active descriptor bytes; and
- reserved backing bytes.

The workbook's physical KV MiB field derives only from the observed unique
reserved-byte total.

### JSONL Schema

Each snapshot contains:

- `schema_version`;
- snapshot sequence and process-local request identifier;
- trigger and phase;
- the executing CPU-plugin component and an opaque correlation identifier;
- state name and concrete CPU state class;
- memory role: `input`, `output`, `kv`, `beam`, or `scale_zp`;
- stable block ordinal and alias relationship;
- internal element type;
- concrete shape, order, and strides;
- active descriptor bytes;
- reserved backing bytes;
- owned/external backing flag;
- allocation-observed status;
- per-state totals;
- request totals after unique-block deduplication.

The raw observer does not classify state names as key, value, TBQ3, or TBQ4.
That mapping belongs to the GenAI manifest and activation evidence.

### Device-Truth Feasibility Amendment

The CPU child plugin cannot truthfully recover the caller's original virtual
device request or the outer compiled model's full execution-device list. For
example, an `AUTO` request delegated to CPU sees a CPU child compiled model;
recording that child fact as `requested_device = CPU` would be false.

Device truth therefore uses two correlated records:

- the raw CPU allocation snapshot records `observer_plugin_device = CPU`, the
  process ID, a process-wide monotonic observer request ID, and an opaque
  correlation ID copied from private model runtime information;
- the GenAI context record records the exact constructor `requested_device`
  and the outer compiled model's uncollapsed
  `actual_execution_devices` array, joined by process ID and correlation ID.

The correlation marker is attached only after canonical model hashing so it
cannot perturb source/transformed SHA-256 identity. It is a join key, not
execution proof. The reconciled `kv_physical_allocation` event contains the
requested and actual device fields required by the workbook and is accepted
only when they are exactly `CPU` and `["CPU"]`. An `AUTO` request that resolves
to CPU remains `AUTO` and is rejected for a formal direct-CPU row.

## GenAI Observation Architecture

### Immutable Logical Manifest

Before compilation, GenAI discovers K/V roles from the exact supported
stateful-SDPA graph topology and records:

- original variable ID;
- K or V role derived from SDPA input port;
- source partial shape and element type;
- requested algorithm;
- exact runtime state bindings;
- component role: `STANDARD_STATE`, `PAYLOAD`, `NORM`, or `METADATA`.

The manifest contains no measured byte totals. Transformed component roles are
recorded when variables are created, never reconstructed from suffixes.

### Identity

Source and transformed models use SHA-256, not the current 64-bit FNV label.
The exact domains are:

- source model: SHA-256 of canonical OpenVINO serialization consisting of the
  UTF-8 XML bytes, a zero-byte domain separator, and BIN bytes;
- transformed model: the same domain over a deterministic temporary
  serialization;
- STANDARD transform identity: the literal discriminator
  `standard-untransformed` plus the source-model SHA-256.

Every hash field is lowercase 64-hex and schema-validated.

### Event Separation

Three evidence types remain distinct:

1. `turboquant_activation`
   - proves requested TBQ3/TBQ4 algorithms, exact fused runtime operations,
     device, route, and no fallback;
   - its existing byte fields are explicitly renamed or versioned as logical
     current-state component bytes, not total physical allocation.
2. `kv_logical_state_observation`
   - reports exported/logical state identity, types, shapes, and current bytes;
   - never proves native scalar U8/U4 physical storage.
3. `kv_physical_allocation`
   - consumes the CPU observer snapshot;
   - reports internal classes, types, active bytes, retained capacities,
     auxiliaries, K/V/component subtotals, and total physical KV state bytes.

For mixed STANDARD/TBQ routes:

- all physical K and V states contribute to total physical KV bytes;
- only the TBQ payload/norm/metadata subtotal cross-checks TurboQuant logical
  activation bytes;
- total physical KV bytes are not required to equal the activation subtotal.

TBQ3/TBQ4 algorithm identity comes from runtime-proven activation and exact
manifest bindings. U8 payload-container tensors are not relabelled U3/U4
element types.

### Route Boundary

Accepted physical observations require:

- requested device exactly `CPU`;
- actual execution device set exactly `["CPU"]`;
- requested attention route explicitly stateful SDPA;
- no PagedAttention/default-route fallback;
- valid generated output;
- exact source/build/model identities;
- one complete post-inference snapshot;
- zero missing, duplicate, extra, unknown, or ambiguous state bindings.

The precompiled `InferRequest` constructor remains ineligible unless it is
supplied the same original manifest and SHA-256 identities. GPU, NPU,
PagedAttention, AUTO/multiple-device, failed generation, and fallback paths
emit no accepted physical observation.

## Runtime Measurement Order

For every accepted measured repetition:

1. start the owned process, resource samplers, and timeout/memory-floor guard;
2. load and compile the exact model;
3. run one successful controlled generation;
4. stop the performance-window samplers after the output and token timings are
   captured;
5. trigger exactly one post-inference CPU allocation snapshot;
6. reconcile the physical snapshot with the immutable GenAI manifest and
   activation record;
7. fully validate and serialize all events;
8. hash raw output, telemetry, snapshots, configuration, and cleanup evidence;
9. terminate the owned process tree and prove zero survivors.

The physical snapshot occurs after the performance window so observer I/O
does not contaminate TTFT, TPOT, throughput, utilization, or peak process
memory. The allocation itself is still the live retained post-inference state.

## Security and Failure Behavior

The observer:

- is absent from normal release builds;
- is inert without the explicit environment variable;
- accepts only a caller-selected local `.jsonl` path;
- records no tensor contents, prompts, responses, raw addresses, or network
  data;
- serializes complete bounded records under a mutex;
- uses stable ordinals instead of addresses;
- fails safely on invalid paths or serialization errors.

An unknown backing block, malformed record, arithmetic overflow, mismatched
identity, missing state, aliasing inconsistency, write failure, timeout, OOM,
invalid output, fallback, or surviving process makes the attempt invalid. It
never becomes a numeric zero or a workbook pass.

## Build and Laptop Safety

- Use a new derived OpenVINO worktree; never edit the pinned official checkout.
- Use a short mapped path/build directory to avoid Windows `MAX_PATH`.
- Configure a targeted Release CPU-plugin build with debug capabilities;
  disable tests/samples/Python/GPU/NPU/frontends not required by the probe.
- Start with `--parallel 2`; reduce to serial if free physical RAM trends
  toward the 2,048 MiB emergency floor.
- Run only one compiler chain, model process, or benchmark process tree at a
  time.
- Monitor available RAM and owned processes throughout.
- Preserve interrupted attempts and resume from the last validated
  checkpoint.

## TDD and Review Gates

Implementation proceeds in independently reviewed slices:

1. derived OpenVINO worktree and immutable identity controller;
2. debug-only allocation snapshot serializer and exact alias/capacity tests;
3. synthetic native F16/U8/U4 stateful-SDPA probe;
4. GenAI logical manifest, SHA-256 identities, and strict schemas;
5. post-inference physical reconciliation for STANDARD, scalar, mixed, and
   fully compressed routes;
6. clean reproduction, patch export, behavior-equivalence, and evidence
   packaging.

Every slice requires:

- a genuine product RED test before implementation;
- focused GREEN tests twice;
- exact commands and retained logs;
- a production target build;
- `git diff --check`;
- marker and process-survivor audits;
- independent specification and code-quality approval.

## Acceptance Criteria

The observability sub-project is accepted only when:

- clean upstream OpenVINO and GenAI checkouts remain unchanged;
- derived core and GenAI commits, trees, ordered patches, build options, and
  binaries are fully hash-bound;
- ordinary builds contain no active observer behavior;
- invalid or absent observer configuration cannot create misleading evidence;
- native F16/U8/U4 fixtures prove concrete CPU state class, internal
  precision, active bytes, retained capacity, beam bytes, and scale/ZP bytes;
- TBQ fixtures prove concrete double-buffer allocations for payload, norm,
  and metadata, including both retained buffers after inference;
- asymmetric routes report observed concrete classes and separate K/V totals;
- all physical totals are deduplicated from directly observed backing blocks;
- public logical state bytes are never substituted for physical allocation;
- exact post-inference snapshots reconcile with activation, route, model,
  build, output, and cleanup evidence;
- all focused tests and production builds pass with zero surviving owned
  processes;
- independent reviewers approve every implementation slice.

Only then may the 60-row WB-04 matrix treat physical KV precision and bytes as
measurable fields.
