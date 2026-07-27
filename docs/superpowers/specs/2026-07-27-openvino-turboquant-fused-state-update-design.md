# OpenVINO TurboQuant Fused State Update Design

## Decision

Implement one project-owned OpenVINO operation type,
`TurboQuantStateUpdateDecode`, for the supported CPU stateful-SDPA path. The
operation is instantiated independently for every selected key or value state.
It performs exact new-vector encoding, appends compressed persistent state, and
decodes the current sequence slice required by SDPA.

This supersedes only the standard-operation codec subgraph in the approved
stateful KV transformation. The strict graph matcher, independent K/V
selection, compressed `ReadValue`/`Assign` ownership, provenance boundary,
workbook measurement contract, quality contract, and safety gates remain
unchanged.

## Reason for the Expansion

The standard-operation implementation cannot reproduce the frozen scalar
codec on the pinned OpenVINO CPU runtime:

- model-level `f64` arithmetic compiles but the CPU plugin executes the
  arithmetic at `f32` runtime precision;
- `f32 ReduceL2` overflows for large finite vectors and underflows for small
  finite vectors;
- power-of-two-scaled and sequential scaled-`f32` alternatives still differ
  from the scalar codec by payload decisions or stored-norm ULPs;
- the frozen codec uses sequential double `std::hypot`, double normalization,
  and then an `f32` cast.

The retained adversarial tests demonstrate a TBQ4 payload-byte mismatch, an
infinite graph norm where the scalar norm is finite, and a zero graph norm
where the scalar norm is nonzero. Weakening exact parity, silently restricting
finite inputs, or changing the frozen scalar codec is not acceptable.

The pinned CPU plugin has been locally proven to execute a registered
`ov::op::Op::evaluate()` implementation through its `Reference` node. A
project operation can therefore reuse the authoritative scalar codec without
modifying OpenVINO Runtime or the immutable upstream checkout.

## Approved Metadata Compatibility Amendment

OpenVINO CPU 2026.2.1 cannot carry strict `i64` state through
`ReadValue ->` a custom extension operation `-> Assign`. Its
`ConvertPrecision` path lowers an `i64` `ReadValue` output to `i32`, while
`InsertConvertAfterExtension` independently lowers every `i64` output of an
unknown extension operation to `i32` for non-Result consumers. The standard
`keep_const_precision` runtime attribute exempts Parameters and Constants,
not state variables or extension outputs. A built-in `i64` state also fails
at CPU memory execution. There is no public graph property or extension hook
that disables these conversions.

The approved compatible contract therefore stores metadata as strict `i32`.
Metadata contains only the validated static head dimension `H`; it never
contains a sequence offset, byte count, or value derived from model data.
`H` must be positive, divisible by eight, and no greater than `INT32_MAX`.
Converting this field from `i64` to `i32` is exact and does not alter payload
bytes, stored `f32` norms, decoded values, quantization quality, or sequence
ordering. Metadata accounting is exactly four bytes per record.

Eliminating metadata would make the state less self-describing and would
change the four-input/four-output contract. Retaining strict `i64` would
require a separately built OpenVINO CPU-plugin fork, including memory and
precision-pass changes. Both alternatives are outside this recovery.

## Operation Contract

The production type is:

```cpp
class TurboQuantStateUpdateDecode : public ov::op::Op {
public:
    OPENVINO_OP("TurboQuantStateUpdateDecode", "openvino_genai_turboquant");

    TurboQuantStateUpdateDecode() = default;
    TurboQuantStateUpdateDecode(const ov::Output<ov::Node>& payload,
                                const ov::Output<ov::Node>& norm,
                                const ov::Output<ov::Node>& metadata,
                                const ov::Output<ov::Node>& new_vector,
                                TurboQuantBits bits,
                                size_t head_dimension,
                                bool norm_correction);
};
```

Inputs:

1. payload: `u8 [B, heads, S, packed_bytes(H, bits)]`;
2. norm: `f32 [B, heads, S, 1]`;
3. metadata: `i32 [B, heads, S, 1]`;
4. new vector: `f32 [B, heads, 1, H]`.

Attributes:

- `bits`: exactly `3` or `4`;
- `head_dimension`: static `H`, positive and divisible by eight;
- `norm_correction`: the validated configuration value.

Outputs:

1. appended payload: `u8 [B, heads, S + 1, packed_bytes(H, bits)]`;
2. appended norm: `f32 [B, heads, S + 1, 1]`;
3. appended metadata: `i32 [B, heads, S + 1, 1]`;
4. decoded current slice: `f32 [B, heads, S + 1, H]`.

The operation is pure. It owns no hidden cache, mutable singleton, or
cross-request state. Existing compressed `ReadValue` outputs feed inputs 1–3;
outputs 1–3 feed their corresponding `Assign` sinks; output 4 feeds only the
matched SDPA K or V port. OpenVINO remains the owner of persistent lifecycle
state.

## Evaluation Semantics

`validate_and_infer_types()` rejects:

- a device or graph path not already accepted by the strict CPU matcher;
- incorrect input count, types, ranks, or incompatible batch/head/sequence
  dimensions;
- a new-vector sequence dimension other than one;
- metadata values or shapes inconsistent with `H`;
- unsupported bits, dynamic `H`, non-divisible-by-eight `H`, `H` greater than
  `INT32_MAX`, and size calculations that overflow `size_t`;
- payload dimensions inconsistent with `packed_bytes(H, bits)`.

`evaluate()` performs these steps for each `[batch, head]` row:

1. validate all input tensor sizes before allocating or copying output;
2. preserve prior payload, norm, and metadata bytes exactly;
3. call the frozen scalar `encode()` for the one new `f32[H]` vector;
4. append the returned payload, exact stored `f32` norm, and checked
   `int32_t` metadata `H`;
5. decode every compressed row in the current `S + 1` slice using the
   selected, frozen norm-restoration semantics;
6. write decoded values into output 4 and return success only after all output
   tensors are complete.

For `norm_correction=true`, decoding uses the existing scalar `decode()`
function, including quantized-vector norm correction. For
`norm_correction=false`, decoding uses an explicitly tested
`decode_natural_inverse()` helper that validates and unpacks the same encoded
record, then multiplies each centroid by
`stored_norm / sqrt(head_dimension)` using double intermediate arithmetic
before the final `f32` cast. Adding that helper must not alter the existing
`encode()` or `decode()` outputs. Both modes use the same payload and are not
different quantizers. Zero vectors remain finite and deterministic. Malformed
existing state throws before producing a successful inference result.

The initial correctness implementation may use row-local `std::vector`
storage from the existing codec. Any later allocation optimization must retain
bit-identical outputs and receive separate tests and review.

## Graph Transformation

For STANDARD state, the graph remains byte-for-byte and edge-for-edge
unchanged.

For TBQ3 or TBQ4 state, the transformation:

1. creates payload, norm, and metadata variables with empty initial sequence;
2. reads those variables and the validated one-vector append input;
3. creates one `TurboQuantStateUpdateDecode` node;
4. replaces the old full-precision selected `Assign` with three compressed
   `Assign` sinks;
5. rewires only the matching SDPA port to decoded output 4;
6. removes the selected full-precision variable and sink;
7. validates the cloned graph before returning it.

All selected replacements are constructed before the cloned graph is mutated.
The caller's model is never mutated. A transformation or validation failure
throws; requested TurboQuant never falls back to STANDARD.

## Registration and Packaging

The operation implementation lives with the stateful graph transformation:

- `src/cpp/src/llm/turboquant_state_update_decode.hpp`;
- `src/cpp/src/llm/turboquant_state_update_decode.cpp`.

The operation is linked into OpenVINO GenAI and registered once with
`utils::singleton_core()` through
`ov::OpExtension<TurboQuantStateUpdateDecode>` before the transformed model is
compiled. Registration is process-safe and idempotent. The first recovery
implementation does not introduce a separately shipped extension DLL.

The operation implements:

- default and full constructors;
- `validate_and_infer_types()`;
- `clone_with_new_inputs()`;
- `visit_attributes()` for bits, head dimension, and norm correction;
- `has_evaluate()` returning true;
- `evaluate()` with exact scalar-codec behavior.

Model import, compiled-model caching, or deserialization may be used only when
the same operation extension is registered first and the operation/build
identity matches the recorded provenance. Otherwise those paths fail
explicitly.

## Allocation and Scratch Accounting

Persistent state bytes are calculated independently for payload, norms, and
metadata from actual output tensor shapes. Query-state inspection must show no
selected full-precision KV variable.

Metadata bytes equal the actual metadata element count multiplied by
`sizeof(int32_t)`. Expected and actual accounting must both use four bytes per
record; an eight-byte assumption or an implicit CPU conversion is a
reconciliation failure.

Decoded output 4 is transient scratch. Telemetry records:

- expected and actual persistent payload bytes;
- expected and actual norm bytes;
- expected and actual metadata bytes;
- decoded scratch tensor bytes;
- CPU `Reference` operation count and matched state count;
- full-precision equivalent bytes for comparison only.

No hidden persistent full-precision copy is permitted. Working-set growth is
measured over 100 steps and reconciled against persistent plus bounded scratch
formulas. A memory mismatch prevents activation and workbook-row acceptance.

## Activation and Telemetry

Configuration acceptance is not activation. `activated=true` is emitted only
after:

- strict graph discovery and transformation succeed;
- the custom operation is registered;
- the transformed graph compiles on CPU;
- deterministic pilot inference succeeds;
- requested and actual K/V algorithms match;
- compressed state types, shapes, and bytes reconcile;
- no selected full-precision persistent state exists;
- output and process-cleanup checks pass.

Telemetry identifies the implementation path as
`stateful_sdpa_reference_codec`, never as upstream-shipped OpenVINO
TurboQuant. A compile, evaluation, allocation, or reconciliation failure
throws and retains a failed attempt with `activated=false` and no fallback.

## Test and Release Gates

Implementation proceeds test-first and must pass these gates in order:

1. custom-operation validation and clone/attribute tests;
2. the three retained adversarial norm/payload vectors;
3. seeded exact payload and stored-norm parity for TBQ3/TBQ4 at H=8 and H=16
   across subnormal, small, ordinary, large, and mixed finite magnitudes;
4. malformed state, overflow, shape, type, metadata, and bits rejection;
5. zero vector and `norm_correction` true/false behavior;
6. two-step then three-step real CPU `InferRequest` state persistence;
7. all nine STANDARD/TBQ3/TBQ4 K/V selections using distinct key/value data;
8. 100-step deterministic CPU inference with selected state types restricted
   to `u8` payload, `f32` norm, and `i32` metadata, plus no-shadow checks;
9. exact persistent and transient allocation reconciliation;
10. existing codec, configuration, graph, production-object, and relevant
    OpenVINO GenAI tests;
11. strict clean-checkout reproduction as controlled patch 0003.

If the CPU Reference path cannot support dynamic sequence outputs, exact
state, or safe lifecycle behavior, the implementation is blocked. Tests or
telemetry must not be weakened. Poor Reference performance is measured
honestly; it does not change quality scores or permit missing workbook fields.

## Workbook Completion Contract

After conformance gates pass, resume the existing WB-04 execution plan:

- run a pilot, one excluded warm-up, and exactly three accepted serial
  repetitions for every runnable configuration;
- capture every required latency, throughput, RAM, KV, CPU, GPU, device,
  fallback, validity, timeout, and cleanup field;
- preserve mean, median, minimum, maximum, and sample count for scalar
  aggregates;
- run and harshly score the complete frozen P1–P6 quality set for every
  Granite-generating configuration;
- update raw evidence, operational registers, and the workbook immediately
  after each accepted result;
- independently recompute workbook aggregates and hashes before release.

No required cell receives an invented value, inferred zero, `N/A`, or a
configuration-acceptance placeholder. A row passes only with complete runtime,
activation, allocation, cleanup, and quality evidence. Granite 8B remains
hardware-gated and cannot be described as executed until it runs on a host
meeting the measured RAM floor.

## Non-Goals

- Modifying the frozen scalar codec to match reduced graph precision.
- Software-emulating general FP64 with a large static f32 graph.
- Separate encode and decode custom operation types.
- Hidden mutable state inside the operation.
- Whole-cache host decompression between inference requests.
- A downstream OpenVINO CPU-plugin fork to preserve `i64` state.
- GPU, NPU, PagedAttention, QJL, PolarQuant, or upstream-support claims.
- Weakening timeouts or RAM floors to force workbook completion.
