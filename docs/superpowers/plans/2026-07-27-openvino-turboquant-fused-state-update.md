# OpenVINO TurboQuant Fused State Update Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the numerically non-conforming standard-op codec subgraph with one exact registered `TurboQuantStateUpdateDecode` operation, prove compressed-only CPU state for 100 steps, and release the patched runtime to the complete WB-04 execution and quality workflow.

**Architecture:** The operation is a pure four-input/four-output OpenVINO `ov::op::Op` whose `evaluate()` reuses the frozen scalar codec for exact encoding and decoding. Existing `ReadValue`/`Assign` nodes retain persistent-state ownership; the operation appends payload/norm/metadata and emits only the current decoded SDPA slice as transient scratch. Registration occurs in the GenAI singleton core before compilation, and activation remains false until runtime state, allocation, device, output, and cleanup evidence reconcile.

**Tech Stack:** C++17, OpenVINO Runtime 2026.2.1, OpenVINO GenAI 2026.2.1.0, CPU Reference custom-operation execution, GoogleTest, CMake/MSBuild, Python 3.11, PowerShell.

## Global Constraints

- Preserve upstream OpenVINO GenAI base `7dea0459b2ac7d8dfd877fd9df6737674fd8371d` and the ordered controlled patch boundary.
- The operation type is exactly `TurboQuantStateUpdateDecode` in domain `openvino_genai_turboquant`; one type is instantiated independently for every selected key or value state.
- Supported runtime is CPU stateful `ov::op::v13::ScaledDotProductAttention`; reject GPU, NPU, PagedAttention, non-SDPA, ambiguous, dynamic-head, unsupported-rank, and unsupported-layout paths.
- Key and value algorithms remain independently selectable as exactly `STANDARD`, `TBQ3`, or `TBQ4`.
- Existing `encode()` and `decode()` outputs are frozen; do not change them to fit reduced graph precision.
- Persistent selected state is only `u8` payload, `f32` norm, and `i64` metadata; no equivalent persistent full-precision shadow is permitted.
- Encode only the new sequence-length-one vector and decode only the current `S + 1` sequence slice needed by the matched SDPA port.
- Requested TurboQuant never silently falls back to STANDARD.
- Do not add GPU/NPU/PagedAttention TurboQuant, QJL, PolarQuant, hidden operation state, whole-cache host decompression, or software-emulated general FP64.
- Activation requires successful transform, registration, CPU compilation, deterministic inference, exact state/allocation reconciliation, valid output, and clean process teardown.
- Every runnable WB-04 row requires a pilot, one excluded warm-up, exactly three accepted serial samples, every required metric, and complete P1–P6 quality evidence.
- Never invent a number, infer a zero, use a bare placeholder, or describe an unexecuted row as passed.

---

## Current Recovery Boundary

- Parent recovery branch: `testing/openvino-turboquant-recovery`.
- Controlled derived checkout branch: `project/turboquant-wb04`.
- Accepted strict matcher head: `80f63c17c6517c236c6cda119a21e17b78266102`.
- Current derived head: `345a4b1dd49405859716cde6f491c6c8fcaab8c4`.
- Two retained RED parity tests are intentionally uncommitted in `tests/cpp/turboquant_stateful_graph.cpp`; they fail only because the standard-op implementation cannot match frozen double-precision codec semantics.
- Existing graph behavior excluding those RED tests passes 28/28; codec/config tests pass 18/18; `openvino_genai_obj` builds.

## File Map

- `src/cpp/src/continuous_batching/cache/turboquant_codec.hpp/.cpp`: frozen encode/decode plus additive natural-inverse decode helper.
- `src/cpp/src/llm/turboquant_state_update_decode.hpp/.cpp`: operation contract, validation, attributes, shape inference, and exact evaluation.
- `src/cpp/src/llm/turboquant_stateful_graph.cpp`: replaces standard arithmetic codec subgraphs with the fused operation.
- `src/cpp/src/utils.cpp`: registers the operation with the singleton core before CPU compilation.
- `src/cpp/src/llm/pipeline_stateful.hpp/.cpp` and `src/cpp/src/llm/pipeline.cpp`: pass validated configuration into the transform/compile boundary.
- `src/cpp/src/llm/pipeline_base.hpp` and `src/cpp/src/continuous_batching/cache/turboquant_config.cpp`: store and serialize proven activation telemetry.
- `tests/cpp/turboquant_state_update_decode.cpp`: focused operation tests.
- `tests/cpp/turboquant_stateful_graph.cpp`: graph, parity, state, and all-nine K/V tests.
- `tests/cpp/turboquant_pipeline_activation.cpp`: pipeline integration and telemetry tests.
- `experiments/patches/openvino-turboquant/0003-stateful-kv-graph.patch`: controlled implementation patch.
- `experiments/raw-results/openvino-turboquant/2026-07-27/conformance/`: capability/build/activation evidence.

### Task 1: Freeze Natural-Inverse Decode Without Changing the Codec

**Files:**
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/continuous_batching/cache/turboquant_codec.hpp`
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/continuous_batching/cache/turboquant_codec.cpp`
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/tests/cpp/turboquant_codec.cpp`

**Interfaces:**
- Consumes: `EncodedVector`, `TurboQuantBits`, existing `encode()` and `decode()`.
- Produces: `std::vector<float> decode_natural_inverse(const EncodedVector& encoded)`; existing functions remain bit-identical.

- [ ] **Step 1: Write the failing additive-helper test**

```cpp
TEST(TurboQuantCodec, NaturalInverseUsesStoredNormWithoutQuantizedNormCorrection) {
    EncodedVector encoded{TurboQuantBits::B4, 8, 2.0f, {0x88, 0x88, 0x88, 0x88}};
    const auto output = decode_natural_inverse(encoded);
    const float expected =
        static_cast<float>(0.0784124 * (static_cast<double>(encoded.norm) / std::sqrt(8.0)));
    ASSERT_EQ(output.size(), 8u);
    for (const float value : output)
        EXPECT_EQ(value, expected);
    EXPECT_NE(output, decode(encoded));
}

TEST(TurboQuantCodec, ExistingCodecGoldenVectorsRemainBitIdentical) {
    const std::vector<float> input{-1.0f, 0.0f, 1.0f};
    EXPECT_EQ(encode(input, TurboQuantBits::B3).payload,
              (std::vector<uint8_t>{0xd8, 0x01}));
    EXPECT_EQ(encode(input, TurboQuantBits::B4).payload,
              (std::vector<uint8_t>{0x71, 0x0e}));
}
```

- [ ] **Step 2: Build and verify RED**

Run:

```powershell
cmake --build R:/external/official-openvino/2026-07-19/build-genai-turboquant --config Release --target turboquant_codec_tests -j 1
```

Expected: compilation fails because `decode_natural_inverse` is undeclared; the existing golden-vector test compiles unchanged once the declaration is added.

- [ ] **Step 3: Implement one shared validation/unpack path**

Extract the current record validation and centroid unpacking into a private helper returning centroid values. Keep `decode()` behavior unchanged. Implement `decode_natural_inverse()` by multiplying each centroid with:

```cpp
const double scale = static_cast<double>(encoded.norm) /
                     std::sqrt(static_cast<double>(encoded.element_count));
value = static_cast<float>(static_cast<double>(value) * scale);
```

For a zero norm, require zero payload and return zeros. Preserve all malformed payload, padding-bit, metadata, and non-finite norm rejections from `decode()`.

- [ ] **Step 4: Run codec GREEN twice**

Run:

```powershell
R:/external/official-openvino/2026-07-19/build-genai-turboquant/tests/cpp/Release/turboquant_codec_tests.exe
R:/external/official-openvino/2026-07-19/build-genai-turboquant/tests/cpp/Release/turboquant_codec_tests.exe
```

Expected: all existing and new codec tests pass twice with identical golden bytes.

- [ ] **Step 5: Commit**

```powershell
git add src/cpp/src/continuous_batching/cache/turboquant_codec.hpp src/cpp/src/continuous_batching/cache/turboquant_codec.cpp tests/cpp/turboquant_codec.cpp
git commit -m "feat(openvino): add exact natural-inverse TurboQuant decode"
```

### Task 2: Implement the Exact Fused OpenVINO Operation

**Files:**
- Create: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/llm/turboquant_state_update_decode.hpp`
- Create: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/llm/turboquant_state_update_decode.cpp`
- Create: `external/official-openvino/2026-07-19/openvino.genai-turboquant/tests/cpp/turboquant_state_update_decode.cpp`
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/tests/cpp/CMakeLists.txt`

**Interfaces:**
- Consumes: compressed payload/norm/metadata, one fresh vector, `TurboQuantBits`, `packed_bytes`, `encode`, `decode`, and `decode_natural_inverse`.
- Produces: `TurboQuantStateUpdateDecode` with four outputs and exact `evaluate()` behavior.

Test-only helper contracts in `turboquant_state_update_decode.cpp`:

```cpp
struct EvaluatedState {
    std::vector<uint8_t> payload;
    std::vector<uint8_t> norm_bytes;
    std::vector<int64_t> metadata;
    std::vector<float> decoded;
    size_t sequence_length;
};
EvaluatedState evaluate_steps(TurboQuantBits bits,
                              size_t head_dimension,
                              bool norm_correction,
                              const std::vector<std::vector<float>>& fresh_vectors);
std::vector<std::function<void()>> malformed_operation_evaluations();
std::vector<std::vector<float>> reviewer_adversarial_vectors();
std::vector<uint8_t> scalar_payload_for(const std::vector<std::vector<float>>& sources,
                                        TurboQuantBits bits);
std::vector<uint8_t> scalar_norm_bytes_for(const std::vector<std::vector<float>>& sources,
                                           TurboQuantBits bits);
std::vector<float> scalar_corrected_decode_for(const std::vector<std::vector<float>>& sources,
                                               TurboQuantBits bits);
```

`evaluate_steps` starts with tensors shaped `[1,1,0,P]`, `[1,1,0,1]`,
`[1,1,0,1]`, feeds one `[1,1,1,H]` vector per step, calls the real operation
`evaluate()`, and feeds outputs 0–2 into the next step. The malformed
functions each call the real validation/evaluation path and are expected to
throw. The scalar oracle helpers call the production `encode()`/`decode()`
functions row by row and flatten their outputs; no codec formula is
reimplemented in the tests.

- [ ] **Step 1: Declare the operation contract**

```cpp
class TurboQuantStateUpdateDecode : public ov::op::Op {
public:
    OPENVINO_OP("TurboQuantStateUpdateDecode", "openvino_genai_turboquant");
    TurboQuantStateUpdateDecode() = default;
    TurboQuantStateUpdateDecode(const ov::Output<ov::Node>& payload,
                                const ov::Output<ov::Node>& norm,
                                const ov::Output<ov::Node>& metadata,
                                const ov::Output<ov::Node>& new_vector,
                                turboquant::TurboQuantBits bits,
                                size_t head_dimension,
                                bool norm_correction);
    void validate_and_infer_types() override;
    std::shared_ptr<ov::Node> clone_with_new_inputs(const ov::OutputVector& inputs) const override;
    bool visit_attributes(ov::AttributeVisitor& visitor) override;
    bool has_evaluate() const override;
    bool evaluate(ov::TensorVector& outputs, const ov::TensorVector& inputs) const override;
};
```

- [ ] **Step 2: Write validation and exact-evaluation RED tests**

```cpp
TEST(TurboQuantStateUpdateDecode, AppendsExactScalarRecordsAndDecodesCurrentSlice) {
    const auto sources = reviewer_adversarial_vectors();
    const auto outputs = evaluate_steps(TurboQuantBits::B4, 8, true, sources);
    EXPECT_EQ(outputs.payload, scalar_payload_for(sources, TurboQuantBits::B4));
    EXPECT_EQ(outputs.norm_bytes, scalar_norm_bytes_for(sources, TurboQuantBits::B4));
    EXPECT_EQ(outputs.metadata, (std::vector<int64_t>{8, 8}));
    EXPECT_EQ(outputs.decoded, scalar_corrected_decode_for(sources, TurboQuantBits::B4));
}

TEST(TurboQuantStateUpdateDecode, RejectsMalformedInputsBeforeWritingOutputs) {
    for (const auto& evaluate_malformed : malformed_operation_evaluations())
        EXPECT_THROW(evaluate_malformed(), ov::Exception);
}
```

The malformed matrix contains wrong input count, wrong types, rank other than four, incompatible B/head/S dimensions, fresh sequence other than one, bits other than 3/4, H not divisible by eight, payload-width mismatch, metadata not equal to H, non-finite norm, non-zero padding bits, and byte-size overflow.

- [ ] **Step 3: Build and verify RED**

Run:

```powershell
cmake --build R:/external/official-openvino/2026-07-19/build-genai-turboquant --config Release --target turboquant_state_update_decode_tests -j 1
```

Expected: compile/link failure because the operation methods are not implemented.

- [ ] **Step 4: Implement validation, shape inference, attributes, and cloning**

Require input types `u8/f32/i64/f32`, rank four, compatible static B/head dimensions, payload last dimension `packed_bytes(H,bits)`, norm/metadata last dimension one, fresh sequence dimension one, and matching prior sequence dimensions. Set outputs to `[B,heads,S+1,P]`, `[B,heads,S+1,1]`, `[B,heads,S+1,1]`, and `[B,heads,S+1,H]`; use a dynamic sequence dimension when input S is dynamic.

Serialize attributes as:

```cpp
int64_t bits_value = static_cast<int64_t>(m_bits);
visitor.on_attribute("bits", bits_value);
visitor.on_attribute("head_dimension", m_head_dimension);
visitor.on_attribute("norm_correction", m_norm_correction);
m_bits = checked_bits(bits_value);
```

- [ ] **Step 5: Implement pure exact evaluation**

Validate all runtime tensor shapes and record bytes before setting output shapes. Allocate output shapes, copy old compressed bytes, then iterate `[batch, head]`: copy the fresh `f32[H]` row into a vector, call `encode`, append its exact record, and decode every row in the current sequence using `decode` or `decode_natural_inverse`. Catch `std::exception` and rethrow with state index context using `OPENVINO_THROW`. Do not retain pointers or mutable state after return.

- [ ] **Step 6: Run focused GREEN and CPU Reference proof**

Run:

```powershell
R:/external/official-openvino/2026-07-19/build-genai-turboquant/bin/Release/turboquant_state_update_decode_tests.exe
```

Expected: validation, clone/attribute, adversarial, seeded magnitude, zero, both norm modes, two-step append, and malformed tests pass.

Compile a model containing the registered operation on CPU and require:

```cpp
std::string runtime_layer_type(const ov::CompiledModel& compiled,
                               const std::string& original_op_name);
EvaluatedState run_compiled_two_steps(const ov::CompiledModel& compiled);

EXPECT_EQ(runtime_layer_type(compiled, "TurboQuantStateUpdateDecode"), "Reference");
EXPECT_EQ(run_compiled_two_steps(compiled).sequence_length, 2u);
```

`runtime_layer_type` reads the named runtime node's `layerType` entry, and
`run_compiled_two_steps` feeds outputs 0–2 back through the model state for two
distinct inputs and returns an `EvaluatedState`.

- [ ] **Step 7: Commit**

```powershell
git add src/cpp/src/llm/turboquant_state_update_decode.hpp src/cpp/src/llm/turboquant_state_update_decode.cpp tests/cpp/turboquant_state_update_decode.cpp tests/cpp/CMakeLists.txt
git commit -m "feat(openvino): add exact fused TurboQuant state operation"
```

### Task 3: Replace the Standard Codec Subgraph Transactionally

**Files:**
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/llm/turboquant_stateful_graph.cpp`
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/tests/cpp/turboquant_stateful_graph.cpp`
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/tests/cpp/CMakeLists.txt`

**Interfaces:**
- Consumes: accepted Task 1 matcher and Task 2 operation.
- Produces: cloned stateful graph using one fused node per selected K/V state; STANDARD paths remain unchanged.

Test-only inspection helpers are defined in
`tests/cpp/turboquant_stateful_graph.cpp`:

```cpp
size_t fused_operation_count(const std::shared_ptr<ov::Model>& model);
size_t standard_codec_arithmetic_region_count(const std::shared_ptr<ov::Model>& model);
size_t selected_full_precision_variable_count(const std::shared_ptr<ov::Model>& model);
```

They inspect `model->get_ops()` and state variables only; they do not mutate
the graph.

- [ ] **Step 1: Preserve and run the existing parity RED**

Run:

```powershell
R:/external/official-openvino/2026-07-19/build-genai-turboquant/bin/Release/turboquant_stateful_graph_tests.exe --gtest_filter=TurboQuantStatefulGraph.MatchesFrozenScalarCodecForReviewerAdversarialVectors:TurboQuantStatefulGraph.MatchesFrozenScalarCodecAcrossSeededFiniteMagnitudes
```

Expected at derived head `345a4b1`: 0/2 pass, with the documented payload boundary, overflow, underflow, and one-ULP failures.

- [ ] **Step 2: Add graph-identity and fused-node tests before production changes**

```cpp
TEST(TurboQuantStatefulGraph, UsesOneFusedOperationPerSelectedState) {
    auto result = transform_stateful_kv_graph(make_two_layer_stateful_sdpa_model(),
        {CacheAlgorithm::TBQ3, CacheAlgorithm::TBQ4, true}, "CPU");
    EXPECT_EQ(fused_operation_count(result.model), 4u);
    EXPECT_EQ(standard_codec_arithmetic_region_count(result.model), 0u);
    EXPECT_EQ(selected_full_precision_variable_count(result.model), 0u);
}
```

- [ ] **Step 3: Replace the arithmetic region**

In `build_replacement`, retain compressed variables and no-initializer `ReadValue` nodes. Replace `encode_vectors`, compressed `Concat`, and `decode_vectors` with:

```cpp
auto update = std::make_shared<TurboQuantStateUpdateDecode>(
    payload_read, norm_read, metadata_read, candidate.new_vector,
    bits_for(algorithm), candidate.head_dimension, config.norm_correction);
auto payload_assign = std::make_shared<Assign>(update->output(0), payload_variable);
auto norm_assign = std::make_shared<Assign>(update->output(1), norm_variable);
auto metadata_assign = std::make_shared<Assign>(update->output(2), metadata_variable);
return PendingReplacement{candidate, payload_assign, norm_assign, metadata_assign, update->output(3)};
```

Remove unused standard-op codec builders. Build every pending replacement before rewiring the cloned graph. Validate after replacing selected SDPA ports and sinks.

- [ ] **Step 4: Run full graph GREEN**

Run:

```powershell
R:/external/official-openvino/2026-07-19/build-genai-turboquant/bin/Release/turboquant_stateful_graph_tests.exe
```

Expected: all prior 28 tests plus the two retained parity tests and fused-node topology tests pass. Coverage includes all nine K/V selections with distinct inputs, H=8/H=16, TBQ3/TBQ4, zero, both norm modes, three-step persistence, STANDARD preservation, metadata, and transactional rejection.

- [ ] **Step 5: Build production objects and regressions**

Run:

```powershell
cmake --build R:/external/official-openvino/2026-07-19/build-genai-turboquant --config Release --target openvino_genai_obj turboquant_codec_tests turboquant_config_tests turboquant_state_update_decode_tests turboquant_stateful_graph_tests -j 1
```

Expected: exit 0; no owned CMake/MSBuild/compiler process remains.

- [ ] **Step 6: Commit**

```powershell
git add src/cpp/src/llm/turboquant_stateful_graph.cpp tests/cpp/turboquant_stateful_graph.cpp tests/cpp/CMakeLists.txt
git commit -m "fix(openvino): use exact fused compressed KV update"
```

### Task 4: Register the Operation and Prove 100-Step CPU State

**Files:**
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/utils.cpp`
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/tests/cpp/turboquant_stateful_graph.cpp`
- Create: `experiments/raw-results/openvino-turboquant/2026-07-27/conformance/reference-capability.json`

**Interfaces:**
- Consumes: transformed model with fused operations.
- Produces: automatically registered production core and measured 100-step CPU capability evidence.

The lifetime test uses:

```cpp
struct LifetimeEvidence {
    size_t steps;
    bool repeat_hash_matches;
    bool no_full_precision_selected_state;
    size_t surviving_owned_processes;
};
LifetimeEvidence run_one_hundred_steps(CacheAlgorithm key, CacheAlgorithm value);
std::shared_ptr<ov::Model> transformed_two_layer_model(CacheAlgorithm key,
                                                       CacheAlgorithm value);
```

The helper compiles through `utils::singleton_core()`, runs fixed distinct K/V
inputs, snapshots `query_state()` at 1/2/50/100, repeats in a fresh request,
and returns false for any state/type/byte/hash mismatch.

- [ ] **Step 1: Write the registration and lifetime RED tests**

```cpp
TEST(TurboQuantStatefulGraph, SingletonCoreCompilesRegisteredFusedOperation) {
    auto transformed = transformed_two_layer_model(CacheAlgorithm::TBQ3, CacheAlgorithm::TBQ4);
    EXPECT_NO_THROW(utils::singleton_core().compile_model(transformed, "CPU"));
}

TEST(TurboQuantStatefulGraph, PersistsOnlyCompressedStateForOneHundredSteps) {
    const auto evidence = run_one_hundred_steps(CacheAlgorithm::TBQ3, CacheAlgorithm::TBQ4);
    EXPECT_EQ(evidence.steps, 100u);
    EXPECT_TRUE(evidence.repeat_hash_matches);
    EXPECT_TRUE(evidence.no_full_precision_selected_state);
    EXPECT_EQ(evidence.surviving_owned_processes, 0u);
}
```

- [ ] **Step 2: Verify RED**

Run the two tests. Expected: singleton-core compilation fails with an unsupported custom operation before registration; the lifetime helper cannot produce accepted evidence.

- [ ] **Step 3: Register exactly once in the singleton core**

Implement:

```cpp
ov::Core& singleton_core() {
    static ov::Core core;
    static const bool registered = [] {
        core.add_extension(std::make_shared<ov::OpExtension<TurboQuantStateUpdateDecode>>());
        return true;
    }();
    (void)registered;
    return core;
}
```

Unit tests using independent `ov::Core` objects add the same `OpExtension` explicitly.

- [ ] **Step 4: Run the guarded 100-step process**

Launch a fresh test process with a 300-second absolute timeout, 100 ms CPU/GPU/RAM sampling, owned-process-tree cleanup, and state snapshots after steps 1, 2, 50, and 100. Run it twice. Require identical output hashes and only selected `u8/f32/i64` states.

- [ ] **Step 5: Write capability evidence atomically**

`reference-capability.json` contains exact derived commit, executable SHA-256, device, runtime layer type, operation count, steps, output hashes, payload/norm/metadata bytes, full-precision equivalent bytes, decoded scratch bytes, peak working set/private bytes, available RAM before/min/after, CPU and GPU mean/median/peak/sample count, cleanup duration, exit/timeout status, and surviving process count. Zero is recorded only from a real counter sample.

- [ ] **Step 6: Commit**

```powershell
git add src/cpp/src/utils.cpp tests/cpp/turboquant_stateful_graph.cpp
git commit -m "test(openvino): prove fused compressed state lifetime"
```

Commit the capability JSON later in the controlling repository with the derived identity that produced it.

### Task 5: Integrate Configuration, Compilation, and Activated Telemetry

**Files:**
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/llm/pipeline_stateful.hpp`
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/llm/pipeline_stateful.cpp`
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/llm/pipeline.cpp`
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/llm/pipeline_base.hpp`
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/continuous_batching/cache/turboquant_config.cpp`
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/include/openvino/genai/turboquant_config.hpp`
- Create: `external/official-openvino/2026-07-19/openvino.genai-turboquant/tests/cpp/turboquant_pipeline_activation.cpp`

**Interfaces:**
- Consumes: validated `TurboQuantConfig`, transformed graph, manifest, and compiled model.
- Produces: activated pipeline and exact JSON telemetry; unsupported requested paths throw.

Pipeline tests define:

```cpp
nlohmann::json compile_and_generate_fixture_pipeline(const std::string& device,
                                                     CacheAlgorithm key,
                                                     CacheAlgorithm value);
```

It captures the single JSON line written to `std::clog`, runs one real
generation step, parses the line with `nlohmann::json`, and fails if there is
zero or more than one activation event.

- [ ] **Step 1: Add constructor and activation RED tests**

```cpp
TEST(TurboQuantPipelineActivation, CompilesTransformedCpuModelBeforeReportingActivated) {
    const auto event = compile_and_generate_fixture_pipeline(
        "CPU", CacheAlgorithm::TBQ3, CacheAlgorithm::TBQ4);
    EXPECT_EQ(event.at("status"), "activated");
    EXPECT_EQ(event.at("attention_path"), "stateful_sdpa_reference_codec");
    EXPECT_EQ(event.at("requested_key_algorithm"), event.at("activated_key_algorithm"));
    EXPECT_EQ(event.at("requested_value_algorithm"), event.at("activated_value_algorithm"));
    EXPECT_FALSE(event.at("fallback").get<bool>());
}
```

Add rejection tests for NPU, GPU, explicit/default PagedAttention, model-string construction, transform failure, compile failure, and missing/invalid state reconciliation.

- [ ] **Step 2: Pass configuration into the compile boundary**

Extend the model-taking stateful constructor:

```cpp
StatefulLLMPipeline(const std::shared_ptr<ov::Model>& model,
                    const Tokenizer& tokenizer,
                    const std::string& device,
                    const ov::AnyMap& config,
                    const GenerationConfig& generation_config,
                    const TurboQuantConfig& turboquant_config,
                    const std::string& model_hash);
```

In every `LLMPipeline` constructor, reject a non-STANDARD request before NPU/PA selection and pass `turboquant_config` plus the already computed model hash into the CPU stateful constructor. Do not reinsert TurboQuant properties into the plugin map.

- [ ] **Step 3: Transform and compile transactionally**

For requested TBQ3/TBQ4, call `transform_stateful_kv_graph` immediately before `singleton_core().compile_model`. Store the manifest only after compilation succeeds. STANDARD/STANDARD compiles the original model.

- [ ] **Step 4: Replace inactive telemetry with manifest-backed activation**

Extend `ActivationTelemetry` with status, matched state count, persistent payload/norm/metadata bytes, decoded scratch bytes, full-precision equivalent bytes, operation type/count, transformed-model hash, actual device, and runtime layer type.

Remove the current unconditional post-constructor activated emission. For a requested TurboQuant configuration, store the compiled manifest and model hash in `StatefulLLMPipeline`; after the first successful `generate()` call, query the real request states, reconcile their types/shapes/bytes with the manifest, emit one `activated` event, and set `m_turboquant_activation_emitted=true`. A failed generation or failed reconciliation emits no activated event and throws. STANDARD/STANDARD may emit `not_requested` after construction because it makes no activation claim.

- [ ] **Step 5: Run focused and production tests**

Run activation, graph, operation, codec, and config targets twice, then build `openvino_genai_obj`. Expected: all pass; activated JSON parses once per pipeline and STANDARD remains `not_requested`.

- [ ] **Step 6: Commit**

```powershell
git add src/cpp/src/llm src/cpp/src/continuous_batching/cache/turboquant_config.cpp src/cpp/include/openvino/genai/turboquant_config.hpp tests/cpp
git commit -m "feat(openvino): activate fused TurboQuant CPU state"
```

### Task 6: Freeze Patch 0003 and Release to the WB-04 Master Plan

**Files:**
- Create: `experiments/patches/openvino-turboquant/0003-stateful-kv-graph.patch`
- Modify: `scripts/testing/openvino_patch_workspace.py`
- Modify: `tests/test_openvino_patch_workspace.py`
- Create: `experiments/raw-results/openvino-turboquant/2026-07-27/conformance/production-build.json`
- Create: `experiments/raw-results/openvino-turboquant/2026-07-27/conformance/synthetic-activation.json`
- Modify: `.superpowers/sdd/progress.md`
- Modify: `.superpowers/sdd/task-5-report.md`

**Interfaces:**
- Consumes: clean derived implementation and conformance evidence.
- Produces: deterministic ordered patch series, production build, all-nine synthetic release gate, and explicit resumption of the WB-04 master plan at Task 6.

- [ ] **Step 1: Add provenance RED**

Require the exact series:

```python
assert [path.name for path in controlled_patch_series(repo_root)] == [
    "0001-tbq-codec.patch",
    "0002-kv-config-telemetry.patch",
    "0003-stateful-kv-graph.patch",
]
```

Expected: test fails before patch 0003 exists.

- [ ] **Step 2: Export and reproduce patch 0003 twice**

Export only production/test changes after strict derived base `122f50ebf9bcfff8835633b42ef1df0499ae834a`. Apply 0001–0003 into two empty controlled destinations. Require identical derived HEAD, clean status, exact origin/base, and byte-identical tracked content.

- [ ] **Step 3: Build serially and run every relevant test twice**

Build `openvino_genai_obj`, codec, config, fused operation, graph, activation, and affected GenAI test targets with `-j 1`. Run all focused tests twice and record exact counts, elapsed time, hashes, compiler/CMake identity, warnings, and zero failed tests in `production-build.json`.

- [ ] **Step 4: Execute the all-nine synthetic activation matrix**

For every STANDARD/TBQ3/TBQ4 K/V pair, run one warm-up and three accepted fresh-process repetitions with 300-second timeout, 100 ms resource sampling, exact activation/state/allocation checks, distinct K/V data, valid output, and zero survivors. Independently recompute every aggregate into `synthetic-activation.json`.

- [ ] **Step 5: Run controlling-repository validators**

Run:

```powershell
python -m pytest tests/test_openvino_patch_workspace.py tests/test_openvino_workbook.py tests/test_revision_control.py -q
python -m unittest discover -s scripts/testing/tests -p "test_official_openvino*.py" -v
```

Expected: all pass.

- [ ] **Step 6: Supersede the blocker and resume the master plan**

Update the durable ledger and Task 5 report with the exact derived commit, test counts, evidence hashes, supported boundary, and retained non-goals. Mark replacement Task 5 complete only after every prior step passes.

Then continue without another planning pause at:

`docs/superpowers/plans/2026-07-19-openvino-turboquant-recovery.md`, Task 6 through Task 12.

Those tasks build/freeze the patched runtime, acquire or safely convert verified Granite artifacts, implement complete measurement, execute every runnable 3B row, score P1–P6 for every generating configuration, execute/import required 8B rows only on a suitable host, reconcile every workbook field, regenerate/render the DOCX, and run all release validators. WB-04 is not called complete until those acceptance gates pass.

- [ ] **Step 7: Commit controlled patch and conformance evidence**

```powershell
git add experiments/patches/openvino-turboquant experiments/raw-results/openvino-turboquant scripts/testing/openvino_patch_workspace.py tests/test_openvino_patch_workspace.py .superpowers/sdd
git commit -m "test(openvino): release exact fused TurboQuant runtime"
```

## Completion Gate

This subproject is complete only when patch 0003 reproduces from the pinned clean source, every relevant build/test passes twice, all nine K/V combinations execute through real CPU Reference operations, exact codec/state/allocation evidence reconciles for 100 steps, activation cannot be confused with configuration acceptance, and zero owned processes survive.

The overall user request remains incomplete after this subproject. Completion additionally requires master-plan Tasks 6–12: every runnable WB-04 runtime row with complete metrics, every generating row with harsh P1–P6 scoring, suitable-host execution for required 8B rows, zero failed required tests, zero blank or placeholder workbook fields, synchronized manifests/registers/hashes, and visually verified generated workbook pages.
