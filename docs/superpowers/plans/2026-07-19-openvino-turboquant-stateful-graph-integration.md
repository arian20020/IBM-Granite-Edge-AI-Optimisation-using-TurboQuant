# OpenVINO TurboQuant Stateful Graph Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add genuine independently selectable TBQ3/TBQ4 persistent K/V state to the supported OpenVINO GenAI CPU stateful-SDPA path, with real activation, allocation, inference, and no-full-precision-shadow evidence.

**Architecture:** A strict model pass discovers only unambiguous `ReadValue`/`Assign` state feeding SDPA, rejects unsupported graphs, and replaces selected K/V variables with packed payload, norm, and metadata variables. Standard OpenVINO operations are attempted first; a measured capability gate decides whether a narrowly scoped CPU decode-slice extension is necessary. The transformation runs after TurboQuant properties are removed and before CPU compilation, and returns an immutable manifest used for activation telemetry and evidence.

**Tech Stack:** C++17, OpenVINO Runtime opset 13 and pass manager, OpenVINO GenAI, GoogleTest, CMake/Ninja, Python 3 repository validators, PowerShell on Windows.

## Global Constraints

- Upstream base remains OpenVINO GenAI commit `7dea0459b2ac7d8dfd877fd9df6737674fd8371d`; all changes are ordered controlled patches.
- Supported execution path is CPU stateful `ov::op::v13::ScaledDotProductAttention`; reject NPU, GPU, PagedAttention, ambiguous state ownership, unsupported rank/layout/head dimension, and non-SDPA consumers.
- Key and value algorithms are independent and each is exactly `STANDARD`, `TBQ3`, or `TBQ4`.
- Encode only newly appended vectors and decode only the sequence slice required by the active SDPA invocation.
- Do not retain an equivalent persistent full-precision shadow cache; account persistent payload, norm, metadata, and bounded scratch separately.
- Do not claim QJL, PolarQuant, GPU TurboQuant, PagedAttention TurboQuant, or upstream-shipped TurboQuant.
- A TurboQuant run may report `activated` only after graph matching, transformation, CPU compilation, deterministic inference, and allocation-formula checks succeed.
- Every failure remains explicit; no silent fallback is permitted when TBQ3 or TBQ4 is requested.

---

## File Map

- `src/cpp/src/llm/turboquant_stateful_graph.hpp`: transformation request, manifest, rejection, and public pass interface.
- `src/cpp/src/llm/turboquant_stateful_graph.cpp`: graph discovery, validation, standard-op encode/decode construction, state replacement, and byte formulas.
- `src/cpp/src/llm/pipeline_stateful.cpp`: invokes the pass before CPU compilation and transfers the manifest to runtime telemetry.
- `src/cpp/src/llm/pipeline_base.hpp`: stores the immutable manifest beside validated TurboQuant configuration.
- `src/cpp/src/continuous_batching/cache/turboquant_config.cpp`: serializes activated telemetry from the manifest rather than configuration acceptance.
- `tests/cpp/turboquant_stateful_graph.cpp`: frozen synthetic two-layer SDPA graph, rejection tests, numerical inference, state-shape, and repeat tests.
- `tests/cpp/turboquant_pipeline_activation.cpp`: pipeline-boundary and telemetry tests.
- `tests/cpp/CMakeLists.txt`: focused test targets.
- `experiments/patches/openvino-turboquant/0003-stateful-kv-graph.patch`: exact controlled production patch.
- `experiments/raw-results/openvino-turboquant/2026-07-19/conformance/`: capability, build, inference, state-allocation, and repeat evidence.

### Task 1: Freeze the Supported Graph Contract and Matcher

**Files:**
- Create: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/llm/turboquant_stateful_graph.hpp`
- Create: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/llm/turboquant_stateful_graph.cpp`
- Create: `external/official-openvino/2026-07-19/openvino.genai-turboquant/tests/cpp/turboquant_stateful_graph.cpp`
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/tests/cpp/CMakeLists.txt`

**Interfaces:**
- Consumes: `ov::genai::turboquant::TurboQuantConfig` and `CacheAlgorithm` from the controlled configuration patch.
- Produces: `GraphTransformResult transform_stateful_kv_graph(std::shared_ptr<ov::Model>, const TurboQuantConfig&, const std::string& device)` where `GraphTransformResult` contains `GraphTransformManifest manifest`; failures throw `ov::Exception` before compilation.

- [ ] **Step 1: Declare exact manifest types and pass interface**

```cpp
enum class KVKind { KEY, VALUE };
struct StateRecord {
    std::string variable_id;
    size_t layer_index;
    KVKind kind;
    CacheAlgorithm algorithm;
    ov::element::Type original_type;
    ov::PartialShape original_shape;
    ov::PartialShape payload_shape;
    size_t head_dimension;
    size_t packed_bytes_per_vector;
    size_t norm_bytes_per_vector;
    size_t metadata_bytes_per_vector;
    size_t scratch_bytes_upper_bound;
};
struct GraphTransformManifest {
    bool activated = false;
    std::string device;
    std::vector<StateRecord> states;
    std::string transformed_model_hash;
};
struct GraphTransformResult {
    std::shared_ptr<ov::Model> model;
    GraphTransformManifest manifest;
};
GraphTransformResult transform_stateful_kv_graph(std::shared_ptr<ov::Model> model,
                                                 const TurboQuantConfig& config,
                                                 const std::string& device);
```

- [ ] **Step 2: Write the frozen two-layer graph and matcher RED tests**

```cpp
TEST(TurboQuantStatefulGraph, DiscoversTwoLayersAndIndependentKeyValueState) {
    auto model = make_two_layer_stateful_sdpa_model(ov::element::f32, 8);
    TurboQuantConfig cfg{CacheAlgorithm::TBQ3, CacheAlgorithm::TBQ4, true};
    const auto result = transform_stateful_kv_graph(model, cfg, "CPU");
    ASSERT_EQ(result.manifest.states.size(), 4u);
    EXPECT_EQ(result.manifest.states[0].algorithm, CacheAlgorithm::TBQ3);
    EXPECT_EQ(result.manifest.states[1].algorithm, CacheAlgorithm::TBQ4);
}
TEST(TurboQuantStatefulGraph, RejectsGpuPagedAttentionAmbiguousAndNonSdpaGraphs) {
    TurboQuantConfig cfg{CacheAlgorithm::TBQ3, CacheAlgorithm::TBQ3, true};
    EXPECT_THROW(transform_stateful_kv_graph(make_two_layer_stateful_sdpa_model(), cfg, "GPU"), ov::Exception);
    EXPECT_THROW(transform_stateful_kv_graph(make_paged_attention_model(), cfg, "CPU"), ov::Exception);
    EXPECT_THROW(transform_stateful_kv_graph(make_ambiguous_state_model(), cfg, "CPU"), ov::Exception);
    EXPECT_THROW(transform_stateful_kv_graph(make_non_sdpa_state_model(), cfg, "CPU"), ov::Exception);
}
```

- [ ] **Step 3: Add `turboquant_stateful_graph_tests` and verify RED**

Run: `cmake --build external/official-openvino/2026-07-19/build-genai-turboquant --config Release --target turboquant_stateful_graph_tests -j 1`

Expected: compilation fails because `transform_stateful_kv_graph` is not defined.

- [ ] **Step 4: Implement strict discovery without mutation**

Iterate `model->get_sinks()` for `ov::op::v6::Assign`, pair by `Variable::get_info().variable_id` with one `ov::op::v6::ReadValue`, trace only `Concat`/`Slice`/`Convert` edges, and require exactly one key input (SDPA port 1) or value input (port 2). Sort records by SDPA friendly name, then key before value; reject duplicate IDs, mixed element types, dynamic head dimension, rank other than four, head dimension not divisible by eight, or any unclassified state.

- [ ] **Step 5: Run focused matcher tests**

Run: `external/official-openvino/2026-07-19/build-genai-turboquant/bin/Release/turboquant_stateful_graph_tests.exe --gtest_filter=TurboQuantStatefulGraph.Discovers*:TurboQuantStatefulGraph.Rejects*`

Expected: all matcher and rejection tests pass; no model mutation occurs on a rejection.

- [ ] **Step 6: Commit the contract**

```powershell
git add external/official-openvino/2026-07-19/openvino.genai-turboquant
git commit -m "test(openvino): freeze stateful TurboQuant graph contract"
```

### Task 2: Build Standard-Op Packed State and Numerical Decode

**Files:**
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/llm/turboquant_stateful_graph.cpp`
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/tests/cpp/turboquant_stateful_graph.cpp`

**Interfaces:**
- Consumes: Task 1 `StateRecord`, standalone `packed_bytes(element_count, bits)`, and matched SDPA K/V edges.
- Produces: persistent `u8` payload state, `f32` norm state, `i64` metadata state, and decoded `f32` SDPA inputs for every selected state.

- [ ] **Step 1: Add RED tests for exact state shapes and byte formulas**

```cpp
TEST(TurboQuantStatefulGraph, ReplacesOnlySelectedStateWithExactPackedAllocation) {
    auto result = transform_stateful_kv_graph(make_two_layer_stateful_sdpa_model(ov::element::f32, 8),
        {CacheAlgorithm::TBQ3, CacheAlgorithm::STANDARD, true}, "CPU");
    EXPECT_EQ(query_variable_type(result.model, "layer.0.key.payload"), ov::element::u8);
    EXPECT_EQ(query_last_dim(result.model, "layer.0.key.payload"), 3u);
    EXPECT_EQ(query_variable_type(result.model, "layer.0.value"), ov::element::f32);
    EXPECT_EQ(find_record(result.manifest, 0, KVKind::KEY).packed_bytes_per_vector, 3u);
}
TEST(TurboQuantStatefulGraph, RestoresNormAndMeetsFrozenMseTolerance) {
    auto outputs = run_one_step_and_read_sdpa_inputs(CacheAlgorithm::TBQ4);
    EXPECT_NEAR(l2_norm(outputs.decoded_key), l2_norm(outputs.source_key), 1e-5f);
    EXPECT_LE(mean_squared_error(outputs.decoded_key, outputs.source_key), 0.08f);
}
```

- [ ] **Step 2: Run RED tests**

Run: `external/official-openvino/2026-07-19/build-genai-turboquant/bin/Release/turboquant_stateful_graph_tests.exe --gtest_filter=*ExactPackedAllocation:*FrozenMseTolerance`

Expected: failures show original `f32` state or missing transformed variables.

- [ ] **Step 3: Construct the standard-op codec subgraphs**

For each new vector compute its `ReduceL2` norm, normalize with guarded `Maximum(norm, 1e-12f)`, map to the same frozen scalar codebook as `turboquant_codec.cpp`, produce integer indices, and pack with `Convert`, `Multiply`, `Add`, `Reshape`, and `ReduceSum` into `u8`. Decode with `Convert`, quotient/remainder arithmetic (`Divide`, `Floor`, `Subtract`, `Multiply`) rather than relying on unsupported shift nodes, `Gather` the codebook, multiply by norm, and slice only `[0:current_sequence_length]` immediately before SDPA.

- [ ] **Step 4: Replace variables transactionally**

Clone the model first. Create unique variables `<original>.payload`, `<original>.norm`, and `<original>.meta`; replace `ReadValue` and `Assign` only after every state subgraph validates. Preserve STANDARD edges unchanged, add transformed sinks, remove old selected sinks, call `validate_nodes_and_infer_types()`, and return only the clone.

- [ ] **Step 5: Run codec, graph, and asymmetric matrix tests**

Run: `external/official-openvino/2026-07-19/build-genai-turboquant/bin/Release/turboquant_stateful_graph_tests.exe`

Expected: TBQ3/TBQ4 golden vectors, zero vectors, three-token append, all nine K/V combinations, formulas, and rejection tests pass.

- [ ] **Step 6: Commit standard-op transformation**

```powershell
git add external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/llm external/official-openvino/2026-07-19/openvino.genai-turboquant/tests/cpp
git commit -m "feat(openvino): transform stateful KV into packed TurboQuant state"
```

### Task 3: Prove CPU Capability, State Lifetime, and Bounded Scratch

**Files:**
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/tests/cpp/turboquant_stateful_graph.cpp`
- Create: `experiments/raw-results/openvino-turboquant/2026-07-19/conformance/standard-op-capability.json`

**Interfaces:**
- Consumes: Task 2 transformed two-layer model.
- Produces: a machine-readable decision with `standard_ops_supported`, compile/infer status, state byte counts, scratch bound, repeat hash, and reason.

- [ ] **Step 1: Add a real CPU InferRequest RED test**

```cpp
TEST(TurboQuantStatefulGraph, CpuInferRequestPersistsPackedStateForOneHundredSteps) {
    auto transformed = transform_stateful_kv_graph(make_two_layer_stateful_sdpa_model(),
        {CacheAlgorithm::TBQ3, CacheAlgorithm::TBQ4, true}, "CPU");
    ov::Core core;
    auto request = core.compile_model(transformed.model, "CPU").create_infer_request();
    for (size_t step = 0; step < 100; ++step) {
        set_deterministic_inputs(request, step);
        request.infer();
        assert_only_packed_selected_states(request.query_state(), transformed.manifest, step + 1);
    }
    const auto first_run_hash = hash_outputs(request);
    auto repeated = run_one_hundred_steps_again(transformed.model);
    EXPECT_EQ(hash_outputs(repeated), first_run_hash);
    EXPECT_FALSE(first_run_hash.empty());
}
```

- [ ] **Step 2: Build and run the test with an absolute timeout**

Run: `$p=Start-Process external/official-openvino/2026-07-19/build-genai-turboquant/bin/Release/turboquant_stateful_graph_tests.exe -ArgumentList '--gtest_filter=*CpuInferRequest*' -PassThru -NoNewWindow; if(-not $p.WaitForExit(300000)){Stop-Process -Id $p.Id -Force; throw 'CPU capability timeout'}; exit $p.ExitCode`

Expected: PASS within 300 seconds, or a retained exact compile/infer failure that selects the custom-op branch below.

- [ ] **Step 3: Prove no persistent full-precision shadow**

Enumerate `request.query_state()` after steps 1, 2, and 100. For each selected K/V state require only `u8` payload, `f32` norms, and `i64` metadata; fail if a persistent state has the original selected variable ID, original full shape/type, or total bytes greater than `payload + norm + metadata` formula.

- [ ] **Step 4: Measure bounded scratch**

Use the test process working-set sampler at 100 ms and the manifest formula. Require `scratch_bytes_upper_bound <= active_layers * batch * heads * active_sequence * head_dimension * sizeof(float)` and require working-set growth between steps 50 and 100 to remain below one additional full-precision persistent-cache allocation.

- [ ] **Step 5: Write the capability decision atomically**

Write JSON with schema keys `derived_commit`, `executable_sha256`, `device`, `standard_ops_supported`, `compile_ms`, `steps`, `repeat_output_sha256`, `persistent_state_bytes`, `full_precision_equivalent_bytes`, `peak_working_set_bytes`, `scratch_upper_bound_bytes`, and `failure`. Zero is valid only when directly observed; a failed capability gate records the exact exception and sets `standard_ops_supported=false`.

- [ ] **Step 6: Apply the decision rule**

If the standard-op test passes all assertions, do not create a custom operation. If CPU cannot compile/execute the arithmetic graph or persistent/scratch assertions fail, stop this plan after committing the evidence and write a separate design and plan for exactly one registered `TurboQuantDecodeSlice` CPU extension; do not weaken these assertions or add host-side full-cache decode.

- [ ] **Step 7: Commit capability evidence**

```powershell
git add external/official-openvino/2026-07-19/openvino.genai-turboquant/tests/cpp experiments/raw-results/openvino-turboquant/2026-07-19/conformance/standard-op-capability.json
git commit -m "test(openvino): prove compressed state lifetime on CPU"
```

### Task 4: Integrate Before Compilation and Emit Activated Telemetry

**Files:**
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/llm/pipeline_stateful.cpp`
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/llm/pipeline_base.hpp`
- Modify: `external/official-openvino/2026-07-19/openvino.genai-turboquant/src/cpp/src/continuous_batching/cache/turboquant_config.cpp`
- Create: `external/official-openvino/2026-07-19/openvino.genai-turboquant/tests/cpp/turboquant_pipeline_activation.cpp`

**Interfaces:**
- Consumes: validated config extracted in `pipeline.cpp` and Task 3 proven transformation.
- Produces: `set_turboquant_manifest(GraphTransformManifest)` and activated JSON containing exact state/allocation evidence.

- [ ] **Step 1: Write pipeline activation RED tests**

```cpp
TEST(TurboQuantPipelineActivation, EmitsActivatedOnlyForCompiledTransformedCpuModel) {
    const auto event = compile_fixture_through_stateful_pipeline("CPU", "TBQ3", "TBQ4");
    EXPECT_EQ(event.at("status"), "activated");
    EXPECT_EQ(event.at("key_algorithm"), "TBQ3");
    EXPECT_EQ(event.at("value_algorithm"), "TBQ4");
    EXPECT_GT(event.at("actual_persistent_payload_bytes").get<size_t>(), 0u);
    EXPECT_EQ(event.at("fallback"), false);
}
TEST(TurboQuantPipelineActivation, RequestedTurboQuantNeverSilentlyFallsBack) {
    EXPECT_THROW(compile_unsupported_fixture_through_stateful_pipeline(), ov::Exception);
}
```

- [ ] **Step 2: Run RED tests**

Run: `external/official-openvino/2026-07-19/build-genai-turboquant/bin/Release/turboquant_pipeline_activation_tests.exe`

Expected: activation remains `not_activated` or manifest setter is missing.

- [ ] **Step 3: Invoke the pass at the CPU boundary**

In the model-taking `StatefulLLMPipeline` constructor, after slice/adaptor transformations and before `singleton_core().compile_model`, call `transform_stateful_kv_graph(model, m_turboquant_config, device)` only when either algorithm is non-STANDARD. Replace the local model with `result.model`; compile it on CPU; only after successful compilation store `result.manifest` and set `activated=true`. Throw before NPU/GPU/PagedAttention compilation when TurboQuant is requested.

- [ ] **Step 4: Serialize measured activation**

Emit requested/activated K/V algorithms, actual device, selected path `stateful_sdpa`, matched layer/state count, original and transformed types/shapes, expected and actual payload/norm/metadata bytes, scratch upper bound, transformed-model hash, derived build commit, and `fallback=false`. STANDARD/STANDARD remains `not_requested`; rejected requests throw and never emit `activated`.

- [ ] **Step 5: Run focused and existing tests**

Run: `external/official-openvino/2026-07-19/build-genai-turboquant/bin/Release/turboquant_pipeline_activation_tests.exe; external/official-openvino/2026-07-19/build-genai-turboquant/bin/Release/turboquant_config_tests.exe; external/official-openvino/2026-07-19/build-genai-turboquant/bin/Release/turboquant_codec_tests.exe`

Expected: all tests pass and the activation JSON parses exactly once per compiled pipeline.

- [ ] **Step 6: Commit pipeline integration**

```powershell
git add external/official-openvino/2026-07-19/openvino.genai-turboquant
git commit -m "feat(openvino): activate TurboQuant in CPU stateful pipeline"
```

### Task 5: Produce Controlled Patch 0003 and Rebuild Reproducibly

**Files:**
- Create: `experiments/patches/openvino-turboquant/0003-stateful-kv-graph.patch`
- Modify: `scripts/testing/openvino_patch_workspace.py`
- Modify: `tests/test_openvino_patch_workspace.py`
- Create: `experiments/raw-results/openvino-turboquant/2026-07-19/conformance/production-build.json`

**Interfaces:**
- Consumes: Tasks 1-4 production changes and existing ordered patch controller.
- Produces: a clean deterministic derived checkout containing patches 0001-0003 and a hashed production build.

- [ ] **Step 1: Add RED provenance expectations**

```python
def test_patch_series_requires_stateful_graph_patch():
    series = controlled_patch_series(repo_root)
    assert [p.name for p in series] == [
        "0001-tbq-codec.patch",
        "0002-kv-config-telemetry.patch",
        "0003-stateful-kv-graph.patch",
    ]
```

- [ ] **Step 2: Run provenance RED test**

Run: `python -m pytest tests/test_openvino_patch_workspace.py -q`

Expected: FAIL because patch 0003 is absent from the controlled series.

- [ ] **Step 3: Export one exact patch and discard derived edits**

Generate `0003-stateful-kv-graph.patch` as the binary-safe diff from strict derived commit `122f50ebf9bcfff8835633b42ef1df0499ae834a` through Task 4. Do not include build products or conformance JSON. Restore the derived checkout to its controlled clean state only after patch SHA-256 is recorded.

- [ ] **Step 4: Reproduce twice from the clean pinned upstream**

Run the controller twice into separate empty destinations. Require identical derived HEAD, clean `git status --porcelain`, correct origin/base identities, and byte-identical tracked files. Record the new 40-character derived commit and all three patch SHA-256 values.

- [ ] **Step 5: Build production objects and focused tests serially**

Run: `cmake --build external/official-openvino/2026-07-19/build-genai-turboquant --config Release --target openvino_genai_obj turboquant_codec_tests turboquant_config_tests turboquant_stateful_graph_tests turboquant_pipeline_activation_tests -j 1`

Expected: exit 0 with no compiler error; record elapsed time, peak working set, executable/library SHA-256, compiler, CMake cache identity, and derived commit in `production-build.json`.

- [ ] **Step 6: Run the complete focused suite twice**

Run: `ctest --test-dir external/official-openvino/2026-07-19/build-genai-turboquant -C Release --output-on-failure -R turboquant`

Expected: 100% pass on both runs.

- [ ] **Step 7: Commit controlled patch and evidence**

```powershell
git add experiments/patches/openvino-turboquant scripts/testing/openvino_patch_workspace.py tests/test_openvino_patch_workspace.py experiments/raw-results/openvino-turboquant/2026-07-19/conformance/production-build.json
git commit -m "build(openvino): add reproducible stateful TurboQuant patch"
```

### Task 6: Synthetic Activation Release Gate and Master-Plan Resume

**Files:**
- Create: `experiments/raw-results/openvino-turboquant/2026-07-19/conformance/synthetic-activation.json`
- Modify: `.superpowers/sdd/progress.md`
- Modify: `.superpowers/sdd/task-5-report.md`

**Interfaces:**
- Consumes: strict 0001-0003 derived build and Task 4 activation telemetry.
- Produces: the release gate that permits Granite pilot execution under the original recovery plan Task 6.

- [ ] **Step 1: Run all nine K/V selections through CPU compilation and inference**

For STANDARD/TBQ3/TBQ4 crossed with STANDARD/TBQ3/TBQ4, run a fresh two-layer process for one excluded warm-up and three accepted deterministic repetitions. Apply a 300-second process timeout, sample resources every 100 ms, and verify zero surviving owned processes after every repetition.

- [ ] **Step 2: Validate each accepted repetition**

Require exit code 0, valid output hash, requested equals activated K/V, actual device CPU, path `stateful_sdpa`, fallback false, exact persistent byte formulas, no original selected full-precision states, complete CPU mean/median/peak/sample count, GPU mean/median/peak/sample count measured as zero on CPU, peak working set, scratch bound, and cleanup duration.

- [ ] **Step 3: Write and double-check the activation artifact**

Write attempts append-only, then independently recompute aggregates and SHA-256 values from raw samples. The final JSON contains mean, median, minimum, maximum, and sample count for every scalar metric; no required key may contain null, `N/A`, or an inferred zero.

- [ ] **Step 4: Run repository validators**

Run: `python -m pytest tests/test_openvino_patch_workspace.py tests/test_openvino_workbook.py tests/test_revision_control.py -q`

Expected: all tests pass; any workbook validator affected by the new derived identity is updated with sourced hashes, never bypassed.

- [ ] **Step 5: Supersede the blocker and mark Task 5 complete**

Update the report with the new derived commit, exact test counts, activation artifact path/hash, supported boundary, and retained non-goals. In `.superpowers/sdd/progress.md`, mark the replacement Task 5 complete only when every gate above passes, then set original recovery-plan Task 6 (Granite model preparation/pilot) to in progress.

- [ ] **Step 6: Commit the release gate**

```powershell
git add experiments/raw-results/openvino-turboquant/2026-07-19/conformance .superpowers/sdd
git commit -m "test(openvino): release stateful TurboQuant for Granite pilots"
```

## Completion Gate

Do not resume Granite WB-04 rows unless Tasks 1-6 pass. Completion requires a clean strict-derived checkout, reproducible 0001-0003 series, production object build, all focused tests twice, all nine asymmetric selections through real CPU `InferRequest`, exact state allocation, no persistent full-precision shadow, complete resource metrics, valid activation telemetry, and zero surviving owned processes. Any failure remains a retained conformance failure and keeps original recovery-plan Task 5 blocked.
