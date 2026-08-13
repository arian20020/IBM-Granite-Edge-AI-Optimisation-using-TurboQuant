# Workbook 05 C3 Activation and Storage Conformance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove, reject, or explicitly leave unproven each Route A K/V cache configuration using independent request, property, dispatch, fallback, and storage evidence before any compressed Granite benchmark is permitted.

**Architecture:** A project-controlled C++20 probe consumes a validated JSON request and passes independent K/V properties to the exact accepted OpenVINO GenAI/Runtime pair. A separate trace-only exact-source probe supplies direct dispatch and packed-size evidence when the accepted performance build exposes no sufficient diagnostic. Python decision modules reconcile request, runtime, trace, and storage records; PowerShell uses the accepted C2 supervisor to run each case safely and retain text-only evidence.

**Tech Stack:** C++20, CMake/MSVC, OpenVINO GenAI C++ API, OpenVINO Runtime internal K/V property types at exact commit `b9a1f201c109e0bed74763934f79483cf6c4cbf4`, Python 3.12.10, Windows PowerShell 5.1, JSON Schema Draft 2020-12, GitHub Actions.

## Global Constraints

- C3 consumes accepted C1 asset identities and the accepted C2 process harness; it does not alter either package's evidence.
- The accepted Runtime and GenAI installations remain read-only.
- The formal device is CPU. Requested and actual device must both be retained.
- K and V algorithm and precision are always represented independently, even for symmetric cases.
- Exact algorithm properties are `KEY_CACHE_QUANT_ALG` and `VALUE_CACHE_QUANT_ALG` with values `SCALAR` or `TURBO`.
- Exact precision properties are `KEY_CACHE_PRECISION` and `VALUE_CACHE_PRECISION` with reviewed values drawn from `f32`, `f16`, `bf16`, `u8`, `u4`, and `u3` only where the exact source and executable evidence support them.
- Precision alone never proves TurboQuant. `u3` or `u4` without verified `TURBO` dispatch is not labelled TurboQuant.
- A compressed candidate passes only when request proof, property proof, direct dispatch proof, no-fallback proof, K storage reconciliation, V storage reconciliation, normal process completion, output integrity, and hosted validation all pass.
- If direct dispatch proof is unavailable, classification is `ActivationUnproven`; successful text generation or lower memory does not upgrade it.
- Scalar U3 is blocked unless executable evidence proves a valid scalar-U3 implementation.
- Route A negative labels `QJL3`, `QJL4`, `PolarQuant3`, and `PolarQuant4` must be rejected or classified unsupported. They are never translated to `SCALAR`, `TURBO`, `u3`, or `u4`.
- Route B remains blocked. C3 does not build or execute QJL/PolarQuant.
- The trace-only build is exact-source conformance instrumentation. It is never used for formal performance, memory, or quality comparisons.
- Live workspaces are unique normal directories under `C:\w5c` and run evidence is under `C:\w5r`.
- C2 safety thresholds and retry policy remain unchanged.

## File Structure

```text
experiments/granite_turboquant_intel/schemas/workbook05/
  activation-proof.schema.json
  storage-proof.schema.json
  conformance-result.schema.json
  probe-run-request.schema.json

experiments/granite_turboquant_intel/manifests/templates/workbook05/
  activation-proof-template.json
  storage-proof-template.json
  conformance-result-template.json
  probe-run-request-template.json

experiments/granite_turboquant_intel/configurations/workbook05/
  route-a-cache-capability-catalogue.json
  route-a-phase3-conformance-matrix.json

experiments/granite_turboquant_intel/phase3/probe/
  CMakeLists.txt
  src/main.cpp
  src/run_request.hpp
  src/run_request.cpp
  src/cache_properties.hpp
  src/cache_properties.cpp
  src/event_writer.hpp
  src/event_writer.cpp
  src/driver.hpp
  src/driver.cpp
  tests/CMakeLists.txt
  tests/run_request_tests.cpp
  tests/cache_properties_tests.cpp

experiments/granite_turboquant_intel/phase3/trace/
  README.md
  turboq_trace.patch
  expected-source-hashes.json

scripts/testing/workbook05/phase3/
  source_capabilities.py
  activation.py
  storage.py
  conformance.py
  conformance_bundle_validation.py

scripts/testing/workbook05/
  Build-Workbook05Phase3Probe.ps1
  Build-Workbook05Phase3TraceRuntime.ps1
  Invoke-Workbook05Phase3Conformance.ps1
  Validate-Workbook05-Phase3.ps1

tests/testing/workbook05/
  test_phase3_source_capabilities.py
  test_phase3_activation_contracts.py
  test_phase3_storage.py
  test_phase3_activation.py
  test_phase3_conformance.py
  test_phase3_conformance_bundle_validation.py
  test_phase3_conformance_workflow_contract.py
  Invoke-Phase3ProbeBuildTests.Tests.ps1
  Invoke-Phase3ConformanceTests.Tests.ps1
  fixtures/phase3/conformance/

.github/workflows/
  workbook-05-phase3-conformance.yml

docs/testing/workbook05/
  phase3-conformance-runbook.md
```

---

### Task 1: Define probe request, activation, storage, and conformance contracts

**Files:**
- Create four schemas and four templates listed above
- Modify: `scripts/testing/workbook05/phase3/contracts.py`
- Create: `tests/testing/workbook05/test_phase3_activation_contracts.py`

**Interfaces:**
- Adds record types: `probe-run-request`, `activation-proof`, `storage-proof`, `conformance-result`
- K/V selection shape: `{"algorithm": "SCALAR|TURBO|FLOATING", "precision": "f32|f16|bf16|u8|u4|u3"}`
- Activation confidence: `Proven`, `Rejected`, `Unproven`
- Storage status: `Reconciled`, `Mismatch`, `Unavailable`

- [ ] **Step 1: Write failing schema tests**

```python
class Phase3ActivationContractTests(unittest.TestCase):
    def test_templates_validate(self) -> None:
        for record_type, filename in {
            "probe-run-request": "probe-run-request-template.json",
            "activation-proof": "activation-proof-template.json",
            "storage-proof": "storage-proof-template.json",
            "conformance-result": "conformance-result-template.json",
        }.items():
            self.assertEqual([], validate_phase3_record(record_type, load_template(filename), REPOSITORY_ROOT))

    def test_proven_activation_requires_dispatch_and_no_fallback(self) -> None:
        payload = load_template("activation-proof-template.json")
        payload["confidence"] = "Proven"
        payload["dispatch"]["evidence_path"] = ""
        payload["fallback"]["observed"] = True
        self.assertNotEqual([], validate_phase3_record("activation-proof", payload, REPOSITORY_ROOT))
```

Also reject one combined `kv_precision` field, missing K or V side, `TURBO/f16`, `FLOATING/u4`, scalar U3 marked admitted, and a passed compressed conformance result with unavailable storage.

- [ ] **Step 2: Verify RED**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_activation_contracts`

Expected: unknown record types or missing schema files.

- [ ] **Step 3: Implement closed conditional schemas**

A proven side requires:

```json
{
  "requested": {"algorithm": "TURBO", "precision": "u3"},
  "verified": {"algorithm": "TURBO", "precision": "u3"},
  "property": {
    "algorithm_name": "KEY_CACHE_QUANT_ALG",
    "algorithm_serialized": "TURBO",
    "precision_name": "KEY_CACHE_PRECISION",
    "precision_serialized": "u3",
    "accepted": true
  },
  "dispatch": {
    "evidence_type": "trace-only-exact-source",
    "evidence_path": "proof/k-dispatch.json",
    "runtime_source_commit": "b9a1f201c109e0bed74763934f79483cf6c4cbf4"
  },
  "fallback": {"observed": false, "evidence_path": "proof/k-fallback.json"},
  "confidence": "Proven"
}
```

The conformance schema must make `Passed` impossible unless both sides are `Proven`, both storage records are `Reconciled`, fallback is false, output integrity passed, process classification is `Passed`, and hosted validation is `Passed`.

- [ ] **Step 4: Extend the schema registry and verify GREEN**

Run:

```powershell
python -m unittest -v tests.testing.workbook05.test_phase3_activation_contracts tests.testing.workbook05.test_phase3_contracts
```

Expected: pass.

- [ ] **Step 5: Commit**

```powershell
git add experiments/granite_turboquant_intel/schemas/workbook05 experiments/granite_turboquant_intel/manifests/templates/workbook05 scripts/testing/workbook05/phase3/contracts.py tests/testing/workbook05/test_phase3_activation_contracts.py
git commit -m "test: define Phase 3 conformance contracts"
```

---

### Task 2: Freeze the exact Route A source capability catalogue

**Files:**
- Create: `scripts/testing/workbook05/phase3/source_capabilities.py`
- Create: `tests/testing/workbook05/test_phase3_source_capabilities.py`
- Create: `experiments/granite_turboquant_intel/configurations/workbook05/route-a-cache-capability-catalogue.json`

**Interfaces:**
- Produces: `inspect_route_a_source(source_root: Path) -> dict[str, Any]`
- Produces source findings with path, complete-file SHA-256, bounded excerpt hash, and semantic role

- [ ] **Step 1: Write failing exact-source tests**

Use fixture source files copied as minimal text fixtures. Require findings for:

```text
src/inference/dev_api/openvino/runtime/internal_properties.hpp
src/inference/include/openvino/runtime/properties.hpp
src/plugins/intel_cpu/src/config.cpp
src/plugins/intel_cpu/src/nodes/scaled_attn.cpp
src/plugins/intel_cpu/src/nodes/kernels/scaled_attn/mha_kv_cache_codec.cpp
src/plugins/intel_cpu/src/nodes/kernels/scaled_attn/codecs/turboq_quantize.hpp
```

Assert that the catalogue records independent key/value algorithm properties, independent key/value precision properties, Turbo dispatch, supported parser values, and packed-index formula. A missing token is `SourceCapabilityMissing`, not inferred support.

- [ ] **Step 2: Verify RED**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_source_capabilities`

Expected: import failure.

- [ ] **Step 3: Implement bounded source inspection**

Required source truths include:

```text
KEY_CACHE_QUANT_ALG and VALUE_CACHE_QUANT_ALG
CacheQuantAlgorithm::SCALAR and CacheQuantAlgorithm::TURBO
KEY_CACHE_PRECISION and VALUE_CACHE_PRECISION
u3, u4, u8 and floating parser branches
compress_cache dispatches turboq_quantize only for TURBO
turboq_head_bytes = (head_dim * bits + 7) / 8
Turbo norm metadata is stored separately as one fp32 value per head/token record
```

Do not treat a property parser token as proof that every algorithm/precision combination executes.

- [ ] **Step 4: Write the controlling catalogue**

The catalogue includes candidate families:

```json
[
  {"configuration_id": "RA-SCALAR-U8-SYM", "k": ["SCALAR", "u8"], "v": ["SCALAR", "u8"], "initial_state": "Candidate"},
  {"configuration_id": "RA-SCALAR-U4-SYM", "k": ["SCALAR", "u4"], "v": ["SCALAR", "u4"], "initial_state": "Candidate"},
  {"configuration_id": "RA-TURBO-U4-SYM", "k": ["TURBO", "u4"], "v": ["TURBO", "u4"], "initial_state": "Candidate"},
  {"configuration_id": "RA-TURBO-U3-SYM", "k": ["TURBO", "u3"], "v": ["TURBO", "u3"], "initial_state": "Candidate"},
  {"configuration_id": "RA-FLOATING-CONTROL", "k": ["FLOATING", "bf16"], "v": ["FLOATING", "bf16"], "initial_state": "CompatibilityCheck"},
  {"configuration_id": "RA-SCALAR-U3-SYM", "k": ["SCALAR", "u3"], "v": ["SCALAR", "u3"], "initial_state": "BlockedPendingExecutableProof"}
]
```

- [ ] **Step 5: Verify GREEN and commit**

```powershell
python -m unittest -v tests.testing.workbook05.test_phase3_source_capabilities
git add scripts/testing/workbook05/phase3/source_capabilities.py tests/testing/workbook05/test_phase3_source_capabilities.py experiments/granite_turboquant_intel/configurations/workbook05/route-a-cache-capability-catalogue.json
git commit -m "docs: freeze Route A cache capabilities"
```

---

### Task 3: Create the C++ request parser and structured event writer

**Files:**
- Create: `experiments/granite_turboquant_intel/phase3/probe/CMakeLists.txt`
- Create: `src/run_request.hpp`, `src/run_request.cpp`, `src/event_writer.hpp`, `src/event_writer.cpp`, `src/main.cpp`
- Create: `tests/CMakeLists.txt`, `tests/run_request_tests.cpp`
- Create: `scripts/testing/workbook05/Build-Workbook05Phase3Probe.ps1`
- Create: `tests/testing/workbook05/Invoke-Phase3ProbeBuildTests.Tests.ps1`

**Interfaces:**
- Produces executable: `wb05_phase3_probe.exe`
- CLI: `wb05_phase3_probe.exe --request <absolute-json> --events <absolute-jsonl> --raw-output <absolute-text>`
- C++ types: `CacheSelection`, `RunRequest`, `EventWriter`

- [ ] **Step 1: Write C++ tests first**

```cpp
TEST_CASE("request keeps key and value selections independent") {
    const auto request = wb05::RunRequest::from_json(R"json({
      "schema_version":"1.0",
      "model_path":"C:\\w5m\\diagnostic",
      "device":"CPU",
      "key":{"algorithm":"TURBO","precision":"u3"},
      "value":{"algorithm":"SCALAR","precision":"u8"},
      "prompt":"probe",
      "seed":42,
      "max_new_tokens":8
    })json");
    REQUIRE(request.key.algorithm == wb05::CacheAlgorithm::Turbo);
    REQUIRE(request.value.algorithm == wb05::CacheAlgorithm::Scalar);
}
```

Also test rejection of QJL/Polar labels, relative model paths, non-CPU device, missing output paths, invalid bit/algorithm combinations, and unknown keys.

- [ ] **Step 2: Configure/build and verify RED**

The build script configures a new `C:\w5c\phase3-probe-contract-<guid>` workspace against:

```text
OpenVINO_DIR=C:\w5a\phase2-31391119557-4\i-ov\runtime\cmake
OpenVINOGenAI_DIR=C:\w5a\phase2-31661571860-1\i-genai\runtime\cmake
```

Run the test target; expect missing source failure.

- [ ] **Step 3: Implement CMake from the accepted GenAI sample pattern**

```cmake
cmake_minimum_required(VERSION 3.26)
project(wb05_phase3_probe LANGUAGES CXX)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
find_package(OpenVINOGenAI REQUIRED PATHS ${OpenVINOGenAI_DIR} ${OpenVINO_DIR} NO_CMAKE_FIND_ROOT_PATH)
add_executable(wb05_phase3_probe src/main.cpp src/run_request.cpp src/event_writer.cpp src/cache_properties.cpp src/driver.cpp)
target_link_libraries(wb05_phase3_probe PRIVATE openvino::genai)
```

Use repository-vendored or standard-library JSON parsing already approved by the project; do not fetch an unpinned dependency during CMake configuration. If no approved JSON library exists, parse the narrow closed request through a project-owned parser tested against the schema.

- [ ] **Step 4: Implement deterministic JSONL events**

Every line has `schema_version`, `event`, `monotonic_ns`, and `utc`. Initial events are `request_validated`, `pipeline_constructing`, `pipeline_ready`, `generation_started`, `first_token`, `token`, `generation_completed`, or `failed`. Flush after every event and heartbeat update.

- [ ] **Step 5: Verify GREEN**

Run:

```powershell
& '.\scripts\testing\workbook05\Build-Workbook05Phase3Probe.ps1' -Mode ContractTests
```

Expected: CMake configure/build/CTest pass against the accepted installs without model execution.

- [ ] **Step 6: Commit**

```powershell
git add experiments/granite_turboquant_intel/phase3/probe scripts/testing/workbook05/Build-Workbook05Phase3Probe.ps1 tests/testing/workbook05/Invoke-Phase3ProbeBuildTests.Tests.ps1
git commit -m "feat: add Phase 3 probe request boundary"
```

---

### Task 4: Implement the exact K/V property adapter

**Files:**
- Create: `probe/src/cache_properties.hpp`
- Create: `probe/src/cache_properties.cpp`
- Create: `probe/tests/cache_properties_tests.cpp`

**Interfaces:**
- Produces: `ov::AnyMap make_cache_properties(const CacheSelection& key, const CacheSelection& value)`
- Produces: `SerializedPropertyEvidence serialize_cache_properties(...)`

- [ ] **Step 1: Write compile-time and behavior tests**

```cpp
TEST_CASE("turbo u3 maps to exact key properties") {
    const auto properties = wb05::make_cache_properties(
        {wb05::CacheAlgorithm::Turbo, wb05::CachePrecision::U3},
        {wb05::CacheAlgorithm::Scalar, wb05::CachePrecision::U8});
    REQUIRE(properties.at(ov::internal::key_cache_quant_alg.name()).as<ov::internal::CacheQuantAlgorithm>() ==
            ov::internal::CacheQuantAlgorithm::TURBO);
    REQUIRE(properties.at(ov::key_cache_precision.name()).as<ov::element::Type>() == ov::element::u3);
    REQUIRE(properties.at(ov::internal::value_cache_quant_alg.name()).as<ov::internal::CacheQuantAlgorithm>() ==
            ov::internal::CacheQuantAlgorithm::SCALAR);
}
```

- [ ] **Step 2: Verify RED**

Run the CTest target; expect missing adapter.

- [ ] **Step 3: Implement exact property mapping**

```cpp
ov::AnyMap properties{
    ov::internal::key_cache_quant_alg(key_algorithm),
    ov::internal::value_cache_quant_alg(value_algorithm),
    ov::key_cache_precision(key_precision),
    ov::value_cache_precision(value_precision),
};
```

Floating selections set precision only and omit quant-alg properties because the exact source states algorithm selection has no effect for floating precision. Reject `TURBO` with floating precision and `FLOATING` with integer precision before pipeline construction.

- [ ] **Step 4: Preserve negative Route A labels**

The JSON parser returns `UnsupportedConfiguration` for QJL/Polar labels. It must not call `make_cache_properties` and must emit a rejection record containing the original label.

- [ ] **Step 5: Verify GREEN and commit**

```powershell
& '.\scripts\testing\workbook05\Build-Workbook05Phase3Probe.ps1' -Mode ContractTests
git add experiments/granite_turboquant_intel/phase3/probe/src/cache_properties.* experiments/granite_turboquant_intel/phase3/probe/tests/cache_properties_tests.cpp
git commit -m "feat: map independent Route A cache properties"
```

---

### Task 5: Implement the accepted-build inference driver and timing events

**Files:**
- Create: `probe/src/driver.hpp`
- Create: `probe/src/driver.cpp`
- Modify: `probe/src/main.cpp`
- Create fixture integration tests under: `tests/testing/workbook05/fixtures/phase3/conformance/driver/`

**Interfaces:**
- Produces: `DriverResult run_request(const RunRequest&, EventWriter&)`
- Uses: `ov::genai::LLMPipeline(models_path, "CPU", properties)`
- Streams first token through `ov::genai::StreamerVariant`

- [ ] **Step 1: Write a fake-driver integration seam test**

Inject a `PipelineFactory` interface so request/event/output behavior is testable without loading a model. Assert `pipeline_ready` precedes `generation_started`, first token is emitted once, all text reaches raw output, and failure returns a stable class.

- [ ] **Step 2: Verify RED**

Run CTest; expect missing driver.

- [ ] **Step 3: Implement the real factory**

```cpp
ov::genai::LLMPipeline pipeline(request.model_path, "CPU", make_cache_properties(request.key, request.value));
ov::genai::GenerationConfig config;
config.max_new_tokens = request.max_new_tokens;
config.do_sample = false;
config.rng_seed = request.seed;
```

Use a streamer callback to append complete token text, emit one `first_token` event at first callback, update the heartbeat, and never truncate the raw output.

- [ ] **Step 4: Record GenAI performance metrics separately**

After generation, serialize available model-load, TTFT, prompt-processing, TPOT, decode-throughput, and total-generation observations separately. Missing metrics are `null` with a missing-data code; they are never replaced by a single wall-clock value.

- [ ] **Step 5: Verify GREEN and commit**

Run contract tests and the fake-pipeline integration test, then commit the driver source.

---

### Task 6: Build a separately labelled exact-source trace probe for direct dispatch evidence

**Files:**
- Create: `experiments/granite_turboquant_intel/phase3/trace/README.md`
- Create: `experiments/granite_turboquant_intel/phase3/trace/turboq_trace.patch`
- Create: `experiments/granite_turboquant_intel/phase3/trace/expected-source-hashes.json`
- Create: `scripts/testing/workbook05/Build-Workbook05Phase3TraceRuntime.ps1`
- Create: `tests/testing/workbook05/Invoke-Phase3ConformanceTests.Tests.ps1`

**Interfaces:**
- Produces separate trace install under: `C:\w5c\trace-runtime-<run>-<attempt>\install`
- Trace record fields: side, requested algorithm/precision, selected algorithm/precision, dispatch path, data bytes, metadata bytes, source commit, original/patched source hashes

- [ ] **Step 1: Write failing patch-integrity tests**

Tests require exact pre-patch hashes for each touched source file, reject unexpected source shape, reject a dirty source tree, and assert the patch changes only reviewed trace points. The patch must not alter quantization arithmetic, codebooks, rotation, packing, attention, or allocation decisions.

- [ ] **Step 2: Verify RED**

Run the PowerShell test suite; expect missing patch/build script.

- [ ] **Step 3: Add minimal trace points**

The patch emits structured trace records at:

```text
property acceptance for K and V
compress_cache branch selection for K and V
turboq_quantize dispatch with selected bit width
scalar quantization branch selection
raw floating copy branch selection
packed data allocation and metadata allocation
```

Trace emission is enabled only by a dedicated conformance environment variable and writes to an explicit path under the run workspace.

- [ ] **Step 4: Build from exact source in a fresh workspace**

The build script checks out only `b9a1f201c109e0bed74763934f79483cf6c4cbf4`, verifies recursive source identity, applies the exact patch, configures CPU-only Release with one build job and one compiler process, installs to the trace workspace, records hashes, and never modifies `C:\w5a`.

- [ ] **Step 5: Label scientific limitations**

Every trace record and decision contains:

```text
performance_eligible: false
quality_eligible: false
purpose: direct codec dispatch and storage conformance only
```

- [ ] **Step 6: Verify GREEN and commit**

Run patch integrity and dry-run build-contract tests. A full trace build occurs only after the C3 PR is merged and manually dispatched.

---

### Task 7: Implement exact storage formulas and reconciliation

**Files:**
- Create: `scripts/testing/workbook05/phase3/storage.py`
- Create: `tests/testing/workbook05/test_phase3_storage.py`

**Interfaces:**
- Produces: `expected_turbo_storage(head_dim: int, kv_heads: int, tokens: int, bits: int) -> StorageExpectation`
- Produces: `reconcile_storage(expectation: StorageExpectation, observation: StorageObservation, tolerance: StorageTolerance) -> StorageDecision`
- Produces: `memory_rank(decisions: Sequence[StorageDecision]) -> list[str]`

- [ ] **Step 1: Write failing formula tests**

```python
def test_turbo_u3_separates_data_and_norm_metadata(self) -> None:
    result = expected_turbo_storage(head_dim=64, kv_heads=8, tokens=10, bits=3)
    self.assertEqual(24, result.data_bytes_per_head_record)
    self.assertEqual(4, result.metadata_bytes_per_head_record)
    self.assertEqual(8 * 10 * 24, result.total_data_bytes)
    self.assertEqual(8 * 10 * 4, result.total_metadata_bytes)
```

The packed formula is `(head_dim * bits + 7) // 8`. Record alignment/padding separately when the trace reports it. Do not absorb unexplained padding into metadata.

- [ ] **Step 2: Verify RED**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_storage`

Expected: import failure.

- [ ] **Step 3: Implement Turbo and observed scalar reconciliation**

Turbo expectation is source-derived. Scalar U8/U4 formulas include scale/zero-point metadata according to the exact selected source path; when source metadata layout is not statically sufficient, require trace-observed data/metadata sizes and classify expectation as bounded rather than inventing a formula.

- [ ] **Step 4: Implement low-memory rank**

Rank only `Reconciled` configurations by:

```text
K data + K metadata + V data + V metadata
```

Tie-break symmetric configuration, lower K total, lower V total, then configuration ID. `Mismatch` and `Unavailable` receive no rank.

- [ ] **Step 5: Verify GREEN and commit**

```powershell
python -m unittest -v tests.testing.workbook05.test_phase3_storage
git add scripts/testing/workbook05/phase3/storage.py tests/testing/workbook05/test_phase3_storage.py
git commit -m "feat: reconcile Phase 3 cache storage"
```

---

### Task 8: Implement activation and conformance decision engines

**Files:**
- Create: `scripts/testing/workbook05/phase3/activation.py`
- Create: `scripts/testing/workbook05/phase3/conformance.py`
- Create: `tests/testing/workbook05/test_phase3_activation.py`
- Create: `tests/testing/workbook05/test_phase3_conformance.py`

**Interfaces:**
- Produces: `decide_activation(request, property_evidence, dispatch_evidence, fallback_evidence, runtime_identity) -> ActivationDecision`
- Produces: `decide_conformance(process, k_activation, v_activation, k_storage, v_storage, output_integrity, hosted_validation) -> ConformanceDecision`

- [ ] **Step 1: Write failing decision-table tests**

Cover:

```text
request accepted + no dispatch proof                  -> ActivationUnproven
request TURBO/u3 + dispatch SCALAR/u3                 -> ActivationRejected
request TURBO/u4 + verified TURBO/u4 + no fallback    -> Proven
request TURBO/u4 + verified TURBO/u4 + fallback true  -> ActivationRejected
both proven + one storage mismatch                    -> StorageMismatch
process success + activation unproven                 -> ActivationUnproven
all required evidence passed                          -> Passed
```

- [ ] **Step 2: Verify RED**

Run both focused suites; expect import failures.

- [ ] **Step 3: Implement side-local decisions**

Never copy K evidence into V or infer V from a symmetric request. Require explicit side labels in every trace and storage record.

- [ ] **Step 4: Implement fail-closed conformance aggregation**

The decision reason names the first blocking class and retains all failure IDs. A later valid run may create a new decision but never rewrite the evidence of an earlier rejected/unproven attempt.

- [ ] **Step 5: Verify GREEN and commit**

Run focused suites and commit both modules.

---

### Task 9: Define and execute the controlled conformance matrix

**Files:**
- Create: `experiments/granite_turboquant_intel/configurations/workbook05/route-a-phase3-conformance-matrix.json`
- Create: `scripts/testing/workbook05/Invoke-Workbook05Phase3Conformance.ps1`
- Modify: `tests/testing/workbook05/Invoke-Phase3ConformanceTests.Tests.ps1`

**Interfaces:**
- Matrix cases: one positive/negative property case per admitted family plus explicit Route A rejection cases
- Orchestrator consumes C1 asset decision, C2 harness, accepted/trace probe hashes, and matrix

- [ ] **Step 1: Write failing matrix tests**

Require exactly these initial cases:

```text
RA-FLOATING-CONTROL
RA-SCALAR-U8-SYM
RA-SCALAR-U4-SYM
RA-TURBO-U4-SYM
RA-TURBO-U3-SYM
RA-SCALAR-U3-SYM-BLOCKED
RA-REJECT-QJL3
RA-REJECT-QJL4
RA-REJECT-POLARQUANT3
RA-REJECT-POLARQUANT4
```

Require each positive case to request K and V separately and each negative label to retain the original unsupported name.

- [ ] **Step 2: Verify RED**

Run the Python/PowerShell matrix contracts; expect missing files.

- [ ] **Step 3: Implement low-risk execution order**

Repository-only negative label checks execute first. Live path-equivalent diagnostic cases then execute in provisional low-memory order `TURBO/u3`, `TURBO/u4`, `SCALAR/u4`, `SCALAR/u8`, floating, but final promotion order is replaced by reconciled measured bytes. A failed safety check stops subsequent live cases and records them as `Skipped by frontier` with the blocking attempt ID.

- [ ] **Step 4: Use the C2 harness unchanged**

Every case gets a new workspace, request hash, executable hash, process attempt, trace evidence, storage proof, conformance result, and manifest. One infrastructure retry follows C2 policy; no other failure retries automatically.

- [ ] **Step 5: Verify GREEN in fixture mode**

Use canned trace and process fixture records to exercise every decision without model execution. Live diagnostic execution occurs only after merge.

- [ ] **Step 6: Commit**

Commit the matrix, orchestrator, and tests with a message explaining Route A negative QJL/Polar cases.

---

### Task 10: Validate the complete C3 bundle as untrusted data

**Files:**
- Create: `scripts/testing/workbook05/phase3/conformance_bundle_validation.py`
- Create: `tests/testing/workbook05/test_phase3_conformance_bundle_validation.py`
- Create fixtures under: `tests/testing/workbook05/fixtures/phase3/conformance-bundles/`

**Interfaces:**
- Produces: `validate_conformance_bundle(bundle_root: Path, repository_root: Path) -> list[BundleIssue]`

- [ ] **Step 1: Write adversarial tests**

Reject a bundle where:

- requested and verified side differ but confidence is `Proven`;
- K dispatch proof is copied into V;
- trace runtime commit/hash differs;
- trace-only binary is marked performance-eligible;
- fallback is true but conformance passed;
- Turbo data bytes do not match packed formula;
- metadata is hidden inside data bytes;
- scalar U3 is admitted without executable proof;
- QJL/Polar label is translated to Turbo;
- a compressed result passed without hosted validation;
- executable/model/archive payload is uploaded.

- [ ] **Step 2: Verify RED**

Run focused suite; expect import failure.

- [ ] **Step 3: Implement schema, manifest, path, payload, and cross-record validation**

Validate both accepted-build and trace-only identities. Hosted validation never loads the model or runs the probe.

- [ ] **Step 4: Verify GREEN and commit**

Valid fixture passes; each adversarial mutation yields the intended stable issue code.

---

### Task 11: Add the three-boundary C3 workflow

**Files:**
- Create: `.github/workflows/workbook-05-phase3-conformance.yml`
- Create: `tests/testing/workbook05/test_phase3_conformance_workflow_contract.py`
- Modify: `scripts/testing/Validate-Workbook05-Phase3.ps1`

**Interfaces:**
- Manual stages: `trace-build`, `diagnostic-conformance`, `repository-only`
- Artifacts:
  - `workbook-05-phase3-trace-${{ github.run_id }}-${{ github.run_attempt }}`
  - `workbook-05-phase3-conformance-${{ github.run_id }}-${{ github.run_attempt }}`

- [ ] **Step 1: Write failing workflow contracts**

Require manual `main` only for live stages, exact self-hosted labels, same-repository guard, read-only permissions, pinned actions, exact-head checkout, repository gate first, no PR live jobs, no binary/model uploads, and an exact same-attempt hosted validator.

- [ ] **Step 2: Verify RED**

Run workflow contract; expect missing workflow.

- [ ] **Step 3: Implement repository-only PR behavior**

Pull requests build and run C++ parser/property tests only against reviewed repository fixtures where possible. They do not access accepted local installs or the Intel runner. Live trace/conformance jobs are `workflow_dispatch` from `main` only.

- [ ] **Step 4: Implement separate trace and conformance collections**

The trace build produces text metadata/hashes only; binaries remain in `C:\w5c`. Diagnostic conformance consumes the locally accepted trace install by exact decision/hash and produces text evidence only.

- [ ] **Step 5: Extend the Phase 3 gate and verify GREEN**

Run C3 Python, PowerShell, schema, workflow, and source-capability tests before the final gate marker.

- [ ] **Step 6: Commit**

Commit workflow, gate, and tests.

---

### Task 12: Document operation, result interpretation, and Route B boundary

**Files:**
- Create: `docs/testing/workbook05/phase3-conformance-runbook.md`

- [ ] **Step 1: Explain the four proof layers**

Use one beginner-friendly example showing why a request for `TURBO/u3` plus generated text is insufficient without property, dispatch, fallback, and storage proof.

- [ ] **Step 2: Explain each result class**

Distinguish `UnsupportedConfiguration`, `ActivationRejected`, `ActivationUnproven`, `StorageMismatch`, `ResourceSafetyStop`, `InfrastructureInterrupted`, and `Passed` with the next permitted action.

- [ ] **Step 3: Explain trace-only limitations**

State that trace instrumentation can prove branch selection and allocation facts but cannot supply formal latency, memory, or quality comparisons.

- [ ] **Step 4: Explain QJL/Polar handling**

Route A rejection tests confirm that unsupported labels do not silently fall back. They are not evidence that QJL or PolarQuant cannot work, and they are not Route B execution tests.

- [ ] **Step 5: Commit**

Commit the runbook.

---

### Task 13: Run complete C3 verification and prepare the implementation PR

- [ ] **Step 1: Run Python suites**

```powershell
python -m unittest -v `
  tests.testing.workbook05.test_phase3_source_capabilities `
  tests.testing.workbook05.test_phase3_activation_contracts `
  tests.testing.workbook05.test_phase3_storage `
  tests.testing.workbook05.test_phase3_activation `
  tests.testing.workbook05.test_phase3_conformance `
  tests.testing.workbook05.test_phase3_conformance_bundle_validation `
  tests.testing.workbook05.test_phase3_conformance_workflow_contract
```

Expected: pass.

- [ ] **Step 2: Run C++ and PowerShell contract suites**

```powershell
& '.\scripts\testing\workbook05\Build-Workbook05Phase3Probe.ps1' -Mode ContractTests
& '.\tests\testing\workbook05\Invoke-Phase3ConformanceTests.Tests.ps1'
```

Expected: pass; no model execution in PR verification.

- [ ] **Step 3: Run complete repository gate**

```powershell
& '.\scripts\testing\Validate-Workbook05-Phase3.ps1' -PythonPath 'python'
```

Expected final line: `WORKBOOK05_PHASE3_GATE_PASS`.

- [ ] **Step 4: Verify payload and workspace boundaries**

Run `git diff --check`, inspect new tracked suffixes, and confirm no generated CMake build, trace install, executable, model, or artifact is tracked.

- [ ] **Step 5: Open a detailed C3 PR**

Include source paths/hashes, exact properties, direct-dispatch strategy, storage formulas, negative Route A cases, trace-only non-claims, RED/GREEN evidence, and exact final head.

- [ ] **Step 6: After merge, run and accept live C3 stages in order**

```text
1. trace-build
2. project-owner trace artifact/digest acceptance
3. diagnostic-conformance
4. hosted validation
5. project-owner conformance artifact/digest acceptance
6. publish admitted/blocked configuration catalogue
```

C4/C5 may consume only accepted C3 decisions.

## C3 Acceptance Gate

C3 is accepted only when:

- the project probe compiles against the exact accepted GenAI/Runtime pair;
- K and V properties are requested and recorded independently;
- direct dispatch evidence is exact-source and side-specific;
- fallback checks are explicit;
- K and V data/metadata storage reconcile separately;
- scalar U3 is not admitted without executable proof;
- QJL/Polar Route A labels are explicitly rejected rather than translated;
- trace-only evidence is barred from performance and quality claims;
- the text-only conformance artifact passes hosted validation;
- the project owner accepts exact artifact and decision hashes;
- only proven/reconciled configurations enter the executable candidate catalogue.

## Textbook and Primary-Source Basis

- *Systems Engineering: Principles and Practice*, Chapters 13, 16, and 17: risk-reduction prototypes, integration hierarchy, and traceable test/evaluation.
- *Why Programs Fail*, Chapters 6–10 and 13–15: scientific hypotheses, executable observations, origin tracking, assertions, and cause-effect isolation.
- *The Art of Unit Testing*, Chapters 3, 6, 7, and 10: dependency seams, asynchronous evidence, trustworthy tests, and layered test recipes.
- *Code Complete*, Chapters 8, 22–25, and 29: defensive boundaries, developer testing, debugging, safe refactoring, measured optimization, and incremental integration.
- *Designing Secure Software*, Chapters 4, 6, 7, 10, and 12: secure defaults, narrow trusted interfaces, design review, untrusted data, and security tests.
- The exact OpenVINO Runtime source defines `CacheQuantAlgorithm`, independent K/V algorithm properties, independent K/V precision properties, Turbo dispatch, and `turboq_head_bytes = (head_dim * bits + 7) / 8`.
- The exact OpenVINO GenAI C++ header defines `LLMPipeline(models_path, device, ov::AnyMap)` and streaming/performance interfaces; the probe follows that installed API rather than a guessed wrapper behavior.