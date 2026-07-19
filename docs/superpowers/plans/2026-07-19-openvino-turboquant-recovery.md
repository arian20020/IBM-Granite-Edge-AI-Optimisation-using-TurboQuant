# OpenVINO TurboQuant WB-04 Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a reproducible OpenVINO `2026.2.1` project patch with independently selectable TBQ3/TBQ4 CPU SDPA KV caches, then replace WB-04 placeholders with complete measured Granite runtime and P1-P6 quality evidence.

**Architecture:** Keep the pinned upstream checkouts immutable and develop in a clean derived checkout. Commit the integration as a portable patch plus build/evidence tooling in this repository. Gate model execution on deterministic codec, packed-allocation, CPU SDPA activation, repository-test, model-conversion, safety, measurement, and quality checks.

**Tech Stack:** C++17, OpenVINO `2026.2.1`, OpenVINO GenAI `2026.2.1.0`, CMake/Ninja/MSVC, Python 3.11, PowerShell 5.1, `unittest`, psutil/PDH, python-docx.

## Global Constraints

- Preserve upstream commits `ede283a88e35465f0d680dabbf1f44080f8fc387` and `7dea0459b2ac7d8dfd877fd9df6737674fd8371d` unchanged.
- Identify every patched result as `OpenVINO 2026.2.1 + project TurboQuant patch` followed by the exact recorded 40-character derived-checkout commit.
- Implement only norm-preserving TBQ3/TBQ4 MSE codecs initially; do not claim QJL, PolarQuant, GPU TurboQuant, or upstream support.
- Key and value algorithms must be independently selectable and runtime-proven.
- Use one pilot, one excluded warm-up, and exactly three accepted measured repetitions per runnable model configuration.
- Capture all timing, memory, KV, CPU/GPU mean-median-peak-count, activation, fallback, output, and cleanup fields.
- Run P1-P6 for every configuration that performs Granite generation.
- Retain a 2,048 MiB available-physical-RAM emergency floor and one owned process tree at a time.
- Never count a blocked, crashed, unsupported, incomplete, or fallback attempt as a pass.
- Do not describe WB-04 as fully executed until required 8B rows run on suitable hardware.

---

### Task 1: Correct WB-04's Current Execution Status

**Files:**
- Modify: `scripts/testing/finalize_official_openvino_workbook.py`
- Modify: `docs/testing/workbooks/text-templates/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md`
- Modify: `docs/testing/Workbook-Revision-Register.csv`
- Test: `scripts/testing/tests/test_official_openvino_reconcile.py`

**Interfaces:**
- Consumes: current v1.4 evidence under `experiments/raw-results/official-openvino/2026-07-19/`.
- Produces: an honest recovery-pending workbook state that later tasks supersede row by row.

- [ ] **Step 1: Add a failing status test**

```python
def test_workbook_does_not_call_terminal_placeholders_execution_complete(self):
    text = WORKBOOK.read_text(encoding="utf-8")
    self.assertNotIn("Overall status | Terminal-complete", text)
    self.assertIn("Recovery pending: zero Granite benchmark rows executed", text)
```

- [ ] **Step 2: Run the focused test and confirm RED**

Run: `python -m unittest scripts.testing.tests.test_official_openvino_reconcile.OfficialOpenVINOReconcileTests.test_workbook_does_not_call_terminal_placeholders_execution_complete -v`

Expected: FAIL because v1.4 currently says `Terminal-complete`.

- [ ] **Step 3: Change the finalizer's campaign summary**

Replace the current overall/final wording with:

```python
("Overall status", "Recovery pending: zero Granite benchmark rows executed; seven setup/API probes passed"),
("Final bounded reasoning", "The 2026-07-19 campaign proved environment and source boundaries only. Runtime and quality placeholders are not execution passes and must be superseded by measured evidence."),
```

Append a v1.5 revision row that supersedes v1.4 and records the correction without deleting prior evidence.

- [ ] **Step 4: Regenerate and validate**

Run:

```powershell
python scripts/testing/finalize_official_openvino_workbook.py
python scripts/testing/Generate-Controlled-Workbooks.py
python scripts/testing/Apply-Workbook-Revision-History.py
python scripts/testing/Validate-Workbook-Revision-Control.py
```

Expected: focused test PASS and revision-control PASS after updating controlled hashes.

- [ ] **Step 5: Commit**

```powershell
git add scripts/testing/finalize_official_openvino_workbook.py scripts/testing/tests/test_official_openvino_reconcile.py docs/testing/Workbook-Revision-Register.csv docs/testing/workbooks
git commit -m "docs(testing): correct WB-04 execution status"
```

### Task 2: Create a Reproducible Patched-Source Workspace

**Files:**
- Create: `scripts/testing/prepare_openvino_turboquant_patch.ps1`
- Create: `scripts/testing/official_openvino/patch_identity.py`
- Create: `scripts/testing/tests/test_official_openvino_patch_identity.py`
- Create: `experiments/patches/openvino-turboquant/README.md`

**Interfaces:**
- Produces: `prepare_patch_workspace(upstream: Path, destination: Path, expected_commit: str)`, plus an evidence JSON containing upstream/patch commits and clean state.

- [ ] **Step 1: Write failing identity tests**

```python
def test_patch_identity_rejects_wrong_or_dirty_base(self):
    with self.assertRaisesRegex(ValueError, "base commit"):
        validate_patch_identity({"base_commit": "wrong", "dirty": False}, EXPECTED)
    with self.assertRaisesRegex(ValueError, "dirty"):
        validate_patch_identity({"base_commit": EXPECTED, "dirty": True}, EXPECTED)
```

- [ ] **Step 2: Run RED**

Run: `python -m unittest scripts.testing.tests.test_official_openvino_patch_identity -v`

Expected: FAIL because `patch_identity.py` is absent.

- [ ] **Step 3: Implement strict identity validation**

```python
def validate_patch_identity(record: dict, expected_base: str) -> None:
    if record.get("base_commit") != expected_base:
        raise ValueError("base commit mismatch")
    if record.get("dirty") is not False:
        raise ValueError("dirty patch workspace")
    if not record.get("patch_commit"):
        raise ValueError("patch commit missing")
```

The PowerShell controller must clone locally from the pinned checkout, detach at the expected commit, create branch `project/turboquant-wb04`, apply only patches under `experiments/patches/openvino-turboquant/`, and refuse an existing non-clean destination.

- [ ] **Step 4: Run GREEN and prepare the workspace**

Run:

```powershell
python -m unittest scripts.testing.tests.test_official_openvino_patch_identity -v
powershell -ExecutionPolicy Bypass -File scripts/testing/prepare_openvino_turboquant_patch.ps1
```

Expected: tests PASS and `external/official-openvino/2026-07-19/openvino.genai-turboquant/` is clean at the pinned base before development.

- [ ] **Step 5: Commit**

```powershell
git add scripts/testing/prepare_openvino_turboquant_patch.ps1 scripts/testing/official_openvino/patch_identity.py scripts/testing/tests/test_official_openvino_patch_identity.py experiments/patches/openvino-turboquant/README.md
git commit -m "build(openvino): prepare reproducible TurboQuant patch workspace"
```

### Task 3: Implement and Prove the Standalone TBQ3/TBQ4 Codec

**Files:**
- Create in derived checkout: `src/cpp/src/continuous_batching/cache/turboquant_codec.hpp`
- Create in derived checkout: `src/cpp/src/continuous_batching/cache/turboquant_codec.cpp`
- Create in derived checkout: `tests/cpp/unit/continuous_batching/turboquant_codec.cpp`
- Modify in derived checkout: `src/cpp/src/CMakeLists.txt`
- Modify in derived checkout: `tests/cpp/unit/CMakeLists.txt`

**Interfaces:**
- Produces: `enum class TurboQuantBits { B3 = 3, B4 = 4 }`, `EncodedVector encode(const std::vector<float>&, TurboQuantBits)`, `std::vector<float> decode(const EncodedVector&)`, and `size_t packed_bytes(size_t, TurboQuantBits)`.

- [ ] **Step 1: Add deterministic failing C++ tests**

```cpp
TEST(TurboQuantCodec, RoundTripPreservesNormAndPackedSize) {
    const std::vector<float> input{0.25f, -0.5f, 0.75f, -1.0f, 0.125f, 0.5f, -0.25f, 1.0f};
    for (auto bits : {TurboQuantBits::B3, TurboQuantBits::B4}) {
        auto encoded = encode(input, bits);
        auto output = decode(encoded);
        EXPECT_EQ(encoded.payload.size(), packed_bytes(input.size(), bits));
        EXPECT_NEAR(l2_norm(input), l2_norm(output), 1e-5f);
        EXPECT_LT(normalized_rmse(input, output), bits == TurboQuantBits::B4 ? 0.20f : 0.35f);
    }
}
```

Also test zero vector, non-byte-aligned lengths, deterministic output, invalid metadata, and exact golden payload bytes.

- [ ] **Step 2: Configure/build the unit target and confirm RED**

Run: `cmake --build external/official-openvino/2026-07-19/build-genai-turboquant --config Release --target genai_unit_tests` followed by `external\official-openvino\2026-07-19\build-genai-turboquant\bin\Release\genai_unit_tests.exe --gtest_filter=TurboQuantCodec.*`.

Expected: compilation failure because codec types/functions are absent.

- [ ] **Step 3: Implement the minimal norm-preserving scalar codec**

Implement stable norm calculation, deterministic fixed codebooks documented from the project research, nearest-centroid encoding, little-endian 3/4-bit packing, strict metadata validation, decoding, and norm restoration. Do not add residual/QJL data.

- [ ] **Step 4: Run codec tests under sanitizable bounds**

Run: `external\official-openvino\2026-07-19\build-genai-turboquant\bin\Release\genai_unit_tests.exe --gtest_filter=TurboQuantCodec.*`

Expected: all codec tests PASS with exact allocation and error bounds.

- [ ] **Step 5: Export and commit the patch checkpoint**

Run `git -C external/official-openvino/2026-07-19/openvino.genai-turboquant diff --binary 7dea0459b2ac7d8dfd877fd9df6737674fd8371d | Out-File -Encoding ascii experiments/patches/openvino-turboquant/0001-tbq-codec.patch`, then:

```powershell
git add experiments/patches/openvino-turboquant/0001-tbq-codec.patch
git commit -m "feat(openvino): add tested TBQ3 and TBQ4 codec"
```

### Task 4: Add Independent K/V Configuration and Activation Telemetry

**Files:**
- Create in derived checkout: `src/cpp/include/openvino/genai/turboquant_config.hpp`
- Create in derived checkout: `src/cpp/src/continuous_batching/cache/turboquant_config.cpp`
- Modify in derived checkout: `src/cpp/src/llm/pipeline_base.hpp`
- Modify in derived checkout: `src/cpp/src/utils.cpp`
- Test in derived checkout: `tests/cpp/unit/continuous_batching/turboquant_config.cpp`

**Interfaces:**
- Produces: `TurboQuantConfig { key_algorithm, value_algorithm, norm_correction }`, accepted properties `TURBOQUANT_KEY_ALGORITHM`, `TURBOQUANT_VALUE_ALGORITHM`, `TURBOQUANT_NORM_CORRECTION`, and a JSON activation record.

- [ ] **Step 1: Write failing parser/dispatch tests**

```cpp
TEST(TurboQuantConfig, PreservesAsymmetricKeyValueSelection) {
    AnyMap properties{{"TURBOQUANT_KEY_ALGORITHM", "TBQ3"},
                      {"TURBOQUANT_VALUE_ALGORITHM", "TBQ4"},
                      {"TURBOQUANT_NORM_CORRECTION", true}};
    auto config = parse_turboquant_config(properties);
    EXPECT_EQ(config.key_algorithm, CacheAlgorithm::TBQ3);
    EXPECT_EQ(config.value_algorithm, CacheAlgorithm::TBQ4);
    EXPECT_TRUE(config.norm_correction);
}
```

Test all four standard/TBQ3/TBQ4 asymmetric directions, unknown values, case sensitivity, and property removal before plugin compilation.

- [ ] **Step 2: Run RED**

Expected: compilation failure for missing configuration types.

- [ ] **Step 3: Implement parsing and telemetry**

Emit one activation object per run containing requested/activated K/V algorithms, norm flag, expected/actual bytes, attention path, device, fallback, build commit, and model hash. Reject any unconsumed or unsupported value.

- [ ] **Step 4: Run GREEN**

Run the focused C++ tests and a Python schema validator fixture. Expected: all PASS.

- [ ] **Step 5: Export patch and commit**

Commit message: `feat(openvino): expose independent TurboQuant KV controls`.

### Task 5: Integrate Compressed Storage into CPU SDPA

**Files:**
- Modify in derived checkout: `src/cpp/src/llm/pipeline_static.cpp`
- Modify in derived checkout: `src/cpp/src/llm/pipeline_stateful.cpp`
- Modify in derived checkout: `src/cpp/src/continuous_batching/cache/kv_cache_manager.hpp`
- Create in derived checkout: `src/cpp/src/continuous_batching/cache/turboquant_kv_cache.hpp`
- Create in derived checkout: `src/cpp/src/continuous_batching/cache/turboquant_kv_cache.cpp`
- Test in derived checkout: `tests/cpp/unit/continuous_batching/turboquant_kv_cache.cpp`

**Interfaces:**
- Produces: per-layer compressed key/value storage whose allocation and decode path are observable by the activation record.

- [ ] **Step 1: Write failing integration tests**

Create a two-layer deterministic attention fixture. Assert TBQ3/TBQ4 and asymmetric configurations allocate the formula-proven byte counts, execute CPU SDPA, return finite output within the documented tolerance, and do not allocate an equivalent full-precision shadow cache after activation.

- [ ] **Step 2: Run RED**

Run: `external\official-openvino\2026-07-19\build-genai-turboquant\bin\Release\genai_unit_tests.exe --gtest_filter=TurboQuantKVCache.*`

Expected: tests fail because KVCacheManager only owns standard OpenVINO tensors.

- [ ] **Step 3: Implement the integration**

Add an explicit compressed-cache owner per selected K/V side, encode new cache vectors, decode only the attention slice required by CPU SDPA, preserve standard cache behavior when `STANDARD` is selected, and count payload/metadata/scratch allocations independently. Reject PagedAttention, GPU, unsupported layouts, and prefill compression rather than falling back.

- [ ] **Step 4: Run GREEN plus leak/cleanup repetition**

Run the focused test 100 times and assert stable owned allocation and zero survivors. Expected: PASS.

- [ ] **Step 5: Export patch and commit**

Commit message: `feat(openvino): integrate TurboQuant CPU SDPA cache`.

### Task 6: Build, Test, and Freeze the Patched Runtime

**Files:**
- Create: `scripts/testing/build_openvino_turboquant.ps1`
- Create: `scripts/testing/verify_openvino_turboquant_build.py`
- Create: `scripts/testing/tests/test_official_openvino_patched_build.py`
- Create: `experiments/raw-results/official-openvino/2026-07-19-recovery/patched-build/`

**Interfaces:**
- Produces: hashed build manifest, repository-test summaries, codec-test logs, and reusable patched binaries.

- [ ] **Step 1: Write failing build-manifest tests**

Require exact upstream/patch commits, compiler/CMake/options, zero failed tests, required binary hashes, and clean source status.

- [ ] **Step 2: Run RED**

Expected: FAIL because no patched build manifest exists.

- [ ] **Step 3: Implement isolated serial build controller**

Configure only required CPU/runtime/GenAI/test targets, disable unrelated UI/examples, limit parallelism from available-memory preflight, capture stdout/stderr, and kill owned descendants on timeout.

- [ ] **Step 4: Build and run all relevant tests**

Expected: configuration/build exit code 0, all codec/integration tests pass, upstream affected tests pass, and required files hash successfully.

- [ ] **Step 5: Commit**

Commit message: `build(openvino): verify patched TurboQuant runtime`.

### Task 7: Acquire or Produce Safe Granite OpenVINO Models

**Files:**
- Modify: `scripts/testing/convert_official_openvino_models.py`
- Modify: `scripts/testing/official_openvino/conversion.py`
- Create: `scripts/testing/resolve_openvino_model_artifact.py`
- Test: `scripts/testing/tests/test_official_openvino_conversion.py`

**Interfaces:**
- Produces: load-proven immutable FP16/INT8/INT4 model manifests or a host-transfer package for rows that cannot run locally.

- [ ] **Step 1: Add failing pre-converted-artifact validation tests**

Require source revision, conversion command/tool versions, XML/BIN/tokenizer/config inventory, hashes, precision, deterministic CPU generation, and licence evidence.

- [ ] **Step 2: Run RED**

Expected: FAIL because current conversion records contain only memory gates.

- [ ] **Step 3: Implement resolver and guarded conversion retry**

Accept no downloaded artifact without the complete provenance contract. For local conversion, require available RAM above the recorded threshold before launch and retain the 2,048 MiB floor during execution.

- [ ] **Step 4: Validate 3B locally and package 8B for suitable hardware**

Expected: 3B models load/generate or remain honestly blocked; 8B is never forced on the 16 GiB host.

- [ ] **Step 5: Commit**

Commit message: `test(openvino): prepare verified Granite artifacts`.

### Task 8: Implement Complete Runtime Measurement and Activation Reconciliation

**Files:**
- Extend: `scripts/testing/official_openvino/metrics.py`
- Replace terminal-only logic: `scripts/testing/official_openvino/runner.py`
- Modify: `scripts/testing/run_official_openvino_retest.py`
- Create: `scripts/testing/measure_official_openvino.py`
- Test: `scripts/testing/tests/test_official_openvino_metrics.py`
- Test: `scripts/testing/tests/test_official_openvino_runner.py`

**Interfaces:**
- Produces: pilot/warm-up/three-sample evidence and complete aggregates for every runnable configuration.

- [ ] **Step 1: Write failing end-to-end fake-runtime tests**

Assert exact three-sample acceptance, all scalar and utilization fields, request-to-first-token TTFT, activation/packed-byte/device proof, invalid-attempt retention, timeout/memory stop, and zero surviving processes.

- [ ] **Step 2: Run RED**

Expected: FAIL because the current runner only writes terminal records.

- [ ] **Step 3: Implement measurement controller**

Use monotonic timing, request-window CPU/GPU sampling, process-tree memory, OpenVINO profiling, activation JSON, atomic attempt directories, and strict reconciliation. Never infer missing GPU counters or KV bytes.

- [ ] **Step 4: Run fake-runtime GREEN and diagnostic smoke run**

Expected: tests PASS and a short real CPU SDPA diagnostic produces all metrics plus activation proof.

- [ ] **Step 5: Commit**

Commit message: `test(openvino): measure patched TurboQuant execution`.

### Task 9: Execute the Complete 3B Runtime Matrix

**Files:**
- Modify: `experiments/manifests/official-openvino/retest-matrix.json` to add required `source_identity` and `build_identity` fields without changing controlled IDs.
- Create: `experiments/raw-results/official-openvino/2026-07-19-recovery/runtime/`

**Interfaces:**
- Consumes: validated patched build, validated 3B artifacts, measurement controller.
- Produces: accepted standard, TBQ3, TBQ4, asymmetric, norm, context, repeatability, and genuine device/fallback rows.

- [ ] **Step 1: Dry-run exact commands and preflight every row**

Expected: no launch; every command records model/build/config IDs and safety limits.

- [ ] **Step 2: Run standard baselines serially**

Accept only rows with pilot, warm-up, three measured samples, complete metrics, valid output, and cleanup.

- [ ] **Step 3: Run TBQ4/TBQ3 and asymmetric configurations serially**

Require exact requested/activated algorithms and packed bytes for every accepted sample.

- [ ] **Step 4: Run norm, context, repeatability, and device-boundary rows**

Reject silent fallback; preserve all attempts and update WB-04 after each accepted row.

- [ ] **Step 5: Commit evidence checkpoint**

Commit message: `test(openvino): execute measured 3B TurboQuant matrix`.

### Task 10: Score P1-P6 for Every Generating Configuration

**Files:**
- Extend: `scripts/testing/official_openvino/quality.py`
- Create: `scripts/testing/run_official_openvino_quality.py`
- Create: `scripts/testing/adjudicate_official_openvino_quality.py`
- Test: `scripts/testing/tests/test_official_openvino_quality.py`
- Create: `experiments/raw-results/official-openvino/2026-07-19-recovery/quality/`

**Interfaces:**
- Produces: six hashed response/adjudication records and mean/median/min/max for every Granite-generating configuration.

- [ ] **Step 1: Write failing completeness and bias tests**

Require all P1-P6 records, raw response hashes, deterministic gates, criterion scores, caps, notes, arithmetic aggregates, and equality of scoring for identical text under different precision/codec labels.

- [ ] **Step 2: Run RED**

Expected: FAIL because current quality output contains only `not-scored` terminal records.

- [ ] **Step 3: Implement gated response capture and adjudication**

Quality launch requires an accepted runtime summary for the identical configuration. Preserve exact prompts/settings and never award format, memory, or speed bonuses.

- [ ] **Step 4: Execute P1-P6 for every generating configuration**

Expected: six valid records and complete aggregates per configuration; failures receive rubric caps, not invented passes.

- [ ] **Step 5: Commit**

Commit message: `test(openvino): score every WB-04 model configuration`.

### Task 11: Execute Required 8B Rows on a Suitable Host

**Files:**
- Create: `scripts/testing/export_official_openvino_host_package.ps1`
- Create: `scripts/testing/import_official_openvino_host_results.py`
- Create: `scripts/testing/tests/test_official_openvino_host_transfer.py`
- Create: `experiments/raw-results/official-openvino/2026-07-19-recovery/host-transfer/`

**Interfaces:**
- Produces: signed/hash-verified portable inputs and validated 8B runtime/quality evidence returned from a host meeting preflight requirements.

- [ ] **Step 1: Write failing transfer-integrity tests**

Require package hash manifest, immutable source/build/model/prompt/rubric IDs, host environment capture, minimum installed/available RAM, and rejection of missing/modified evidence.

- [ ] **Step 2: Run RED**

Expected: FAIL because transfer tooling is absent.

- [ ] **Step 3: Implement export/import and host preflight**

The host controller must refuse less than 24 GiB available RAM or any condition that violates the calculated requirement plus 2,048 MiB floor. It uses the same pilot/warm-up/three-sample and P1-P6 contracts.

- [ ] **Step 4: Execute/import the 8B rows**

Expected: all required 8B runtime and quality rows validate, or WB-04 remains explicitly not fully executed.

- [ ] **Step 5: Commit**

Commit message: `test(openvino): validate WB-04 Granite 8B rows`.

### Task 12: Reconcile, Generate, Render, and Release WB-04

**Files:**
- Modify: `scripts/testing/finalize_official_openvino_workbook.py`
- Modify: `scripts/testing/official_openvino/reconcile.py`
- Modify: `scripts/testing/audit_official_openvino_docx.py`
- Modify: `docs/testing/workbooks/text-templates/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md`
- Modify: relevant `docs/testing/*.csv` registers
- Modify: `docs/testing/Workbook-Revision-Register.csv`
- Modify: `docs/testing/workbooks/Controlled-Workbook-Manifest.csv`
- Regenerate: `docs/testing/workbooks/generated/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx`

**Interfaces:**
- Produces: controlled WB-04 release with complete source-separated measurements and quality evidence.

- [ ] **Step 1: Add failing final-acceptance tests**

Require every controlled ID, zero blank/bare-placeholder cells, three accepted samples for each required runtime row, P1-P6 for every generating configuration, correct official/patched provenance, zero failed required tests, and no terminal placeholder represented as an execution pass.

- [ ] **Step 2: Run RED**

Expected: FAIL until all 3B and 8B evidence is present.

- [ ] **Step 3: Reconcile raw evidence into Markdown and registers**

Populate all timing, memory, utilization, KV, activation, fallback, quality, failure, and decision fields from validated evidence. Supersede rather than delete the 2026-07-19 terminal campaign.

- [ ] **Step 4: Generate and verify the controlled artifact**

Run:

```powershell
python scripts/testing/Generate-Controlled-Workbooks.py
python scripts/testing/Apply-Workbook-Revision-History.py
python scripts/testing/audit_official_openvino_docx.py
python -m unittest discover -s scripts/testing/tests -p "test_*.py" -v
python scripts/testing/Validate-Workbook-Revision-Control.py
powershell -ExecutionPolicy Bypass -File scripts/testing/Validate-OpenVINO-Codec-Extension.ps1
powershell -ExecutionPolicy Bypass -File scripts/testing/Validate-Controlled-Testing-Workspace.ps1
```

Render the DOCX using the approved document renderer and inspect every page at full size. Expected: every command PASS, all pages legible, zero blank cells, and controlled hashes synchronized.

- [ ] **Step 5: Commit**

```powershell
git add docs/testing scripts/testing experiments/raw-results/official-openvino experiments/patches/openvino-turboquant
git commit -m "test(openvino): complete measured WB-04 TurboQuant recovery"
```
