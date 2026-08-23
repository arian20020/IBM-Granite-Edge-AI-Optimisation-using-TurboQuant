# Workbook 05 C1 Asset Locking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce immutable, independently validated diagnostic and IBM Granite 4.1 3B model/tokenizer/conversion identities without executing the model or authorising any inference claim.

**Architecture:** C1 separates four concerns: accepted-build verification, safe local storage, immutable Hugging Face acquisition, and OpenVINO conversion provenance. Python owns deterministic record generation and validation; PowerShell owns Windows path, process, and workflow orchestration; a GitHub-hosted validator treats the self-hosted evidence bundle only as data.

**Tech Stack:** Python 3.12.10, Windows PowerShell 5.1, JSON Schema Draft 2020-12, `huggingface_hub==1.24.0`, `optimum-intel==2.0.0`, `nncf==3.2.0`, `transformers==5.14.1`, GitHub Actions, OpenVINO IR, IBM Granite 4.1 3B.

## Global Constraints

- Campaign is exactly `GTQ-WB05-MF-v1`; route is exactly `route-a-merged-openvino`.
- Runtime install `C:\w5a\phase2-31391119557-4\i-ov` and GenAI install `C:\w5a\phase2-31661571860-1\i-genai` are read-only.
- Runtime decision SHA-256 is `5dae00b38edb9a20f2d82a99dcf4cd1e2d4e7aeaaf6984873ccc55abccf3ae38`; GenAI decision SHA-256 is `0f273e1f345e1512e8e159af9b83f9ad521a26aa5e0ae813f2599161a5cb4a79`.
- Runtime source commit is `b9a1f201c109e0bed74763934f79483cf6c4cbf4`; GenAI source commit is `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0`.
- Model assets live under `C:\w5m`; C1 workspaces live under `C:\w5c`; neither root nor a child run directory may be a reparse point.
- Existing directories are never deleted or reused automatically.
- At least `53687091200` free bytes are required before Granite 4.1 3B download or conversion.
- The formal model repository is exactly `ibm-granite/granite-4.1-3b`; the 8B repository is recorded as deferred metadata only and is not downloaded.
- Mutable revision names such as `main` may be resolved, but downloaded files must use the returned immutable full revision SHA.
- The preferred first conversion candidate is INT4 asymmetric, group size `128`, ratio `1.0`, data-free. Failure creates a separate conversion-attempt identity; it never overwrites source assets or a prior output.
- C1 uploads no model files, tokenizer files, OpenVINO IR, executables, DLLs, libraries, or archives containing them.
- C1 authorises no model execution, TurboQuant activation, packed-storage, performance, or quality claim.

## File Structure

```text
scripts/testing/workbook05/phase3/
  __init__.py
  contracts.py
  hashing.py
  paths.py
  prerequisites.py
  disk_preflight.py
  model_assets.py
  conversion.py
  asset_bundle_validation.py

scripts/testing/workbook05/
  Assert-Workbook05Phase3Prerequisites.ps1
  Invoke-Workbook05Phase3AssetLock.ps1
  Validate-Workbook05-Phase3.ps1
  requirements.phase3-assets.in
  requirements.phase3-assets.txt

experiments/granite_turboquant_intel/schemas/workbook05/
  phase3-prerequisite-proof.schema.json
  model-asset-lock.schema.json
  model-conversion-record.schema.json

experiments/granite_turboquant_intel/manifests/templates/workbook05/
  phase3-prerequisite-proof-template.json
  model-asset-lock-template.json
  model-conversion-record-template.json

experiments/granite_turboquant_intel/configurations/workbook05/
  phase3-asset-lock-settings.json

tests/testing/workbook05/
  test_phase3_contracts.py
  test_phase3_paths.py
  test_phase3_prerequisites.py
  test_phase3_disk_preflight.py
  test_phase3_model_assets.py
  test_phase3_conversion.py
  test_phase3_asset_bundle_validation.py
  test_phase3_asset_workflow_contract.py
  fixtures/phase3/assets/

.github/workflows/
  workbook-05-phase3-assets.yml

docs/testing/workbook05/
  phase3-asset-lock-runbook.md
```

---

### Task 1: Add the C1 JSON contracts and shared schema registry

**Files:**
- Create: `scripts/testing/workbook05/phase3/__init__.py`
- Create: `scripts/testing/workbook05/phase3/contracts.py`
- Create: `experiments/granite_turboquant_intel/schemas/workbook05/phase3-prerequisite-proof.schema.json`
- Create: `experiments/granite_turboquant_intel/schemas/workbook05/model-asset-lock.schema.json`
- Create: `experiments/granite_turboquant_intel/schemas/workbook05/model-conversion-record.schema.json`
- Create: `experiments/granite_turboquant_intel/manifests/templates/workbook05/phase3-prerequisite-proof-template.json`
- Create: `experiments/granite_turboquant_intel/manifests/templates/workbook05/model-asset-lock-template.json`
- Create: `experiments/granite_turboquant_intel/manifests/templates/workbook05/model-conversion-record-template.json`
- Create: `tests/testing/workbook05/test_phase3_contracts.py`

**Interfaces:**
- Produces: `validate_phase3_record(record_type: str, payload: Mapping[str, Any], repository_root: Path) -> list[ValidationIssue]`
- Produces: `assert_phase3_record(record_type: str, payload: Mapping[str, Any], repository_root: Path) -> None`
- Produces record types: `prerequisite-proof`, `model-asset-lock`, `model-conversion-record`

- [ ] **Step 1: Write the failing schema-registry tests**

```python
from pathlib import Path
import json
import unittest

from scripts.testing.workbook05.phase3.contracts import validate_phase3_record

REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
TEMPLATE_ROOT = REPOSITORY_ROOT / "experiments/granite_turboquant_intel/manifests/templates/workbook05"


class Phase3ContractTests(unittest.TestCase):
    def test_c1_templates_validate(self) -> None:
        bindings = {
            "prerequisite-proof": "phase3-prerequisite-proof-template.json",
            "model-asset-lock": "model-asset-lock-template.json",
            "model-conversion-record": "model-conversion-record-template.json",
        }
        for record_type, filename in bindings.items():
            with self.subTest(record_type=record_type):
                payload = json.loads((TEMPLATE_ROOT / filename).read_text(encoding="utf-8"))
                self.assertEqual([], validate_phase3_record(record_type, payload, REPOSITORY_ROOT))

    def test_asset_lock_rejects_moving_revision(self) -> None:
        payload = json.loads((TEMPLATE_ROOT / "model-asset-lock-template.json").read_text(encoding="utf-8"))
        payload["source"]["resolved_revision"] = "main"
        issues = validate_phase3_record("model-asset-lock", payload, REPOSITORY_ROOT)
        self.assertTrue(any(issue.json_path == "$.source.resolved_revision" for issue in issues))
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
python -m unittest -v tests.testing.workbook05.test_phase3_contracts
```

Expected: import failure for `scripts.testing.workbook05.phase3.contracts`.

- [ ] **Step 3: Create closed schemas with exact identities and fail-closed conditions**

Every schema must use `additionalProperties: false`, require `schema_version: "1.0"`, `campaign_id: "GTQ-WB05-MF-v1"`, and lowercase 64-character SHA-256 values. The asset schema must require:

```json
{
  "asset_id": "MODEL-WB05-GRANITE41-3B-INT4A-G128-R100",
  "asset_role": "granite-3b",
  "source": {
    "repository": "ibm-granite/granite-4.1-3b",
    "requested_revision": "main",
    "resolved_revision": "0123456789abcdef0123456789abcdef01234567",
    "license": "apache-2.0"
  },
  "declared_model_metadata": {
    "parameter_family": "3B",
    "layers": 40,
    "kv_heads": 8,
    "declared_sequence_length": 131072,
    "observed_runtime_capability": false
  },
  "source_files": [],
  "tokenizer_files": [],
  "conversion_record_path": "records/model-conversion-record.json",
  "converted_files": [],
  "aggregate_model_sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "aggregate_tokenizer_sha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
  "status": "Candidate",
  "reasons": ["Template record; not live evidence."]
}
```

Use a revision pattern of `^[0-9a-f]{40,64}$`; do not permit branch names in `resolved_revision`.

- [ ] **Step 4: Implement the schema registry**

```python
SCHEMA_NAMES = {
    "prerequisite-proof": "phase3-prerequisite-proof.schema.json",
    "model-asset-lock": "model-asset-lock.schema.json",
    "model-conversion-record": "model-conversion-record.schema.json",
}


def validate_phase3_record(record_type, payload, repository_root):
    schema_name = SCHEMA_NAMES.get(record_type)
    if schema_name is None:
        raise ValueError(f"Unsupported Phase 3 record type: {record_type}")
    schema_path = repository_root / "experiments/granite_turboquant_intel/schemas/workbook05" / schema_name
    schema = json.loads(schema_path.read_text(encoding="utf-8-sig"))
    errors = sorted(Draft202012Validator(schema).iter_errors(payload), key=_stable_error_key)
    return [ValidationIssue(_json_path(list(error.absolute_path)), error.message) for error in errors]
```

Reuse `ValidationIssue` from `scripts.testing.workbook05.schema_validation`; do not create a second issue type.

- [ ] **Step 5: Run the focused tests and verify GREEN**

Run:

```powershell
python -m unittest -v tests.testing.workbook05.test_phase3_contracts
```

Expected: all C1 contract tests pass.

- [ ] **Step 6: Commit the contracts**

```powershell
git add scripts/testing/workbook05/phase3 experiments/granite_turboquant_intel/schemas/workbook05 experiments/granite_turboquant_intel/manifests/templates/workbook05 tests/testing/workbook05/test_phase3_contracts.py
git commit -m "test: define Phase 3 asset evidence contracts"
```

---

### Task 2: Add deterministic hashing and controlled-path primitives

**Files:**
- Create: `scripts/testing/workbook05/phase3/hashing.py`
- Create: `scripts/testing/workbook05/phase3/paths.py`
- Create: `tests/testing/workbook05/test_phase3_paths.py`

**Interfaces:**
- Produces: `sha256_file(path: Path) -> str`
- Produces: `sha256_tree(root: Path, files: Sequence[Path]) -> str`
- Produces: `assert_normal_local_directory(path: Path, approved_root: Path, allow_root: bool = False) -> Path`
- Produces: `relative_evidence_path(root: Path, path: Path) -> str`

- [ ] **Step 1: Write failing path and aggregate-hash tests**

```python
class Phase3PathTests(unittest.TestCase):
    def test_sibling_prefix_is_not_a_child(self) -> None:
        with self.assertRaises(ValueError):
            assert_normal_local_directory(Path(r"C:\w5m-other\run"), Path(r"C:\w5m"))

    def test_tree_hash_binds_relative_name_size_and_file_digest(self) -> None:
        with TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "a.txt").write_text("A", encoding="utf-8")
            (root / "b.txt").write_text("B", encoding="utf-8")
            first = sha256_tree(root, [root / "a.txt", root / "b.txt"])
            second = sha256_tree(root, [root / "b.txt", root / "a.txt"])
            self.assertEqual(first, second)
```

- [ ] **Step 2: Verify RED**

Run:

```powershell
python -m unittest -v tests.testing.workbook05.test_phase3_paths
```

Expected: import failure for `phase3.hashing` or `phase3.paths`.

- [ ] **Step 3: Implement canonical tree hashing**

Hash a canonical UTF-8 line per sorted file:

```python
line = f"{relative_path}\0{size}\0{file_sha256}\n".encode("utf-8")
aggregate.update(line)
```

Reject files outside `root`, duplicate canonical relative paths, and symlink/reparse-point inputs.

- [ ] **Step 4: Implement Windows-safe containment**

Use `Path.resolve(strict=True)` for existing paths, reject UNC strings beginning with `\\`, reject device prefixes beginning with `\\?\` or `\\.\`, use `os.path.commonpath`, and on Windows inspect `st_file_attributes & stat.FILE_ATTRIBUTE_REPARSE_POINT`.

- [ ] **Step 5: Verify GREEN and the complete existing path suite**

Run:

```powershell
python -m unittest -v tests.testing.workbook05.test_phase3_paths tests.testing.workbook05.test_workspace_policy
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```powershell
git add scripts/testing/workbook05/phase3/hashing.py scripts/testing/workbook05/phase3/paths.py tests/testing/workbook05/test_phase3_paths.py
git commit -m "feat: add Phase 3 path and hashing primitives"
```

---

### Task 3: Revalidate the accepted Runtime and GenAI before every C1 live action

**Files:**
- Create: `scripts/testing/workbook05/phase3/prerequisites.py`
- Create: `scripts/testing/workbook05/Assert-Workbook05Phase3Prerequisites.ps1`
- Create: `tests/testing/workbook05/test_phase3_prerequisites.py`
- Create: `tests/testing/workbook05/Invoke-Phase3PrerequisiteTests.Tests.ps1`

**Interfaces:**
- Produces: `verify_prerequisites(runtime_install: Path, runtime_decision: Path, genai_install: Path, genai_decision: Path) -> dict[str, Any]`
- PowerShell output: one schema-valid `phase3-prerequisite-proof.json`

- [ ] **Step 1: Write the failing Python identity tests**

Create temporary accepted directories and decisions. Assert that the validator rejects one changed hash, wrong source commit, reparse-point marker, missing `OpenVINOConfig.cmake`, missing `openvino_genai.dll`, or a decision that enables a later scientific claim.

```python
def test_changed_runtime_decision_is_integrity_failure(self) -> None:
    fixture = make_valid_prerequisite_fixture(self.temporary_directory)
    fixture.runtime_decision.write_text("{}", encoding="utf-8")
    with self.assertRaisesRegex(ValueError, "Runtime decision SHA-256 mismatch"):
        verify_prerequisites(**fixture.as_kwargs())
```

- [ ] **Step 2: Verify RED**

Run:

```powershell
python -m unittest -v tests.testing.workbook05.test_phase3_prerequisites
```

Expected: import failure for `phase3.prerequisites`.

- [ ] **Step 3: Implement exact prerequisite verification**

The function must verify:

```python
EXPECTED_RUNTIME_DECISION_SHA256 = "5dae00b38edb9a20f2d82a99dcf4cd1e2d4e7aeaaf6984873ccc55abccf3ae38"
EXPECTED_GENAI_DECISION_SHA256 = "0f273e1f345e1512e8e159af9b83f9ad521a26aa5e0ae813f2599161a5cb4a79"
EXPECTED_RUNTIME_SOURCE = "b9a1f201c109e0bed74763934f79483cf6c4cbf4"
EXPECTED_GENAI_SOURCE = "bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0"
```

Required Runtime files:

```text
runtime/cmake/OpenVINOConfig.cmake
runtime/include/openvino/frontend/onnx/extension/conversion.hpp
runtime/include/openvino/frontend/tensorflow/extension/conversion.hpp
```

Required GenAI files:

```text
runtime/bin/intel64/Release/openvino_genai.dll
runtime/bin/intel64/Release/openvino_genai_c.dll
runtime/bin/intel64/Release/openvino_tokenizers.dll
python/openvino_genai/py_openvino_genai.cp312-win_amd64.pyd
```

The decisions must remain `Passed`, use the exact route/component/source, and keep all five later claim flags false.

- [ ] **Step 4: Add the PowerShell wrapper**

The wrapper must call Python with an argument array, write to a caller-supplied output path, validate the result through `assert_phase3_record`, and return nonzero on every integrity failure. It must not copy, modify, or repair accepted prerequisites.

- [ ] **Step 5: Add PowerShell behavior tests**

Use temporary fixture paths and verify the wrapper preserves an existing output file on failure by writing to `output.tmp`, validating, and then calling `[IO.File]::Replace` or `Move-Item` only after success.

- [ ] **Step 6: Verify GREEN**

Run:

```powershell
python -m unittest -v tests.testing.workbook05.test_phase3_prerequisites
& '.\tests\testing\workbook05\Invoke-Phase3PrerequisiteTests.Tests.ps1'
```

Expected: both suites pass.

- [ ] **Step 7: Commit**

```powershell
git add scripts/testing/workbook05/phase3/prerequisites.py scripts/testing/workbook05/Assert-Workbook05Phase3Prerequisites.ps1 tests/testing/workbook05/test_phase3_prerequisites.py tests/testing/workbook05/Invoke-Phase3PrerequisiteTests.Tests.ps1
git commit -m "feat: verify accepted Phase 2 prerequisites"
```

---

### Task 4: Add disk and workspace preflight

**Files:**
- Create: `scripts/testing/workbook05/phase3/disk_preflight.py`
- Create: `tests/testing/workbook05/test_phase3_disk_preflight.py`
- Modify: `experiments/granite_turboquant_intel/configurations/workbook05/phase3-asset-lock-settings.json`

**Interfaces:**
- Produces: `collect_disk_preflight(model_root: Path, probe_root: Path, run_root: Path, free_bytes: int) -> dict[str, Any]`
- Produces status: `Passed` or `Blocked`

- [ ] **Step 1: Write failing threshold and inventory tests**

```python
def test_granite_download_is_blocked_below_fifty_gib(self) -> None:
    result = collect_disk_preflight(
        model_root=Path(r"C:\w5m"),
        probe_root=Path(r"C:\w5c"),
        run_root=Path(r"C:\w5r"),
        free_bytes=53687091199,
    )
    self.assertEqual("Blocked", result["status"])
    self.assertIn("DISK_BELOW_50_GIB", result["failure_ids"])
```

- [ ] **Step 2: Verify RED**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_disk_preflight`

Expected: import failure.

- [ ] **Step 3: Implement inventory without deletion**

Record total bytes and file counts for `C:\w5a`, `C:\w5m`, `C:\w5c`, and `C:\w5r`; record free bytes for drive C; list candidate stale workspaces by run identity but never delete them. A deletion proposal is evidence only and requires a separate project-owner command outside C1.

- [ ] **Step 4: Add exact settings**

```json
{
  "schema_version": "1.0",
  "campaign_id": "GTQ-WB05-MF-v1",
  "model_root": "C:\\w5m",
  "probe_root": "C:\\w5c",
  "run_root": "C:\\w5r",
  "minimum_free_bytes_before_granite_3b": 53687091200,
  "granite_3b_repository": "ibm-granite/granite-4.1-3b",
  "granite_8b_repository": "ibm-granite/granite-4.1-8b",
  "granite_8b_download_authorised": false
}
```

- [ ] **Step 5: Verify GREEN**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_disk_preflight`

Expected: pass.

- [ ] **Step 6: Commit**

```powershell
git add scripts/testing/workbook05/phase3/disk_preflight.py tests/testing/workbook05/test_phase3_disk_preflight.py experiments/granite_turboquant_intel/configurations/workbook05/phase3-asset-lock-settings.json
git commit -m "feat: add Phase 3 disk preflight"
```

---

### Task 5: Resolve and download a Hugging Face model by immutable revision

**Files:**
- Create: `scripts/testing/workbook05/phase3/model_assets.py`
- Create: `tests/testing/workbook05/test_phase3_model_assets.py`
- Create: `tests/testing/workbook05/fixtures/phase3/assets/fake_hub_manifest.json`
- Create: `scripts/testing/workbook05/requirements.phase3-assets.in`

**Interfaces:**
- Produces: `ResolvedModel(repository: str, requested_revision: str, resolved_revision: str, siblings: tuple[HubFile, ...])`
- Produces: `resolve_model(api: HubApi, repository: str, requested_revision: str) -> ResolvedModel`
- Produces: `download_snapshot(api: HubApi, model: ResolvedModel, destination: Path) -> tuple[Path, ...]`

- [ ] **Step 1: Write failing tests using an injected fake Hub API**

```python
class FakeHubApi:
    def model_info(self, repo_id: str, revision: str):
        return SimpleNamespace(sha="bef400f943f2fcf440cf1d4c38c6f844e2d4a387", siblings=[SimpleNamespace(rfilename="config.json")])


def test_resolution_records_full_immutable_revision(self) -> None:
    result = resolve_model(FakeHubApi(), "ibm-granite/granite-4.1-3b", "main")
    self.assertRegex(result.resolved_revision, r"^[0-9a-f]{40}$")
```

Also test that a non-IBM repository, a short SHA, an empty sibling list, duplicate filenames, path traversal, and an unexpected local file are rejected.

- [ ] **Step 2: Verify RED**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_model_assets`

Expected: import failure.

- [ ] **Step 3: Implement the adapter boundary**

Define a protocol instead of importing the live client throughout the module:

```python
class HubApi(Protocol):
    def model_info(self, repo_id: str, revision: str) -> Any: ...
    def snapshot_download(self, *, repo_id: str, revision: str, local_dir: str, allow_patterns: list[str]) -> str: ...
```

Permit only `ibm-granite/granite-4.1-3b` for a formal asset and a separately configured small diagnostic repository for the diagnostic spike.

- [ ] **Step 4: Pin the direct acquisition dependency**

`scripts/testing/workbook05/requirements.phase3-assets.in` contains:

```text
huggingface_hub==1.24.0
```

Generate a hash-locked transitive file in Task 6 rather than installing from an unreviewed moving dependency set.

- [ ] **Step 5: Verify GREEN**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_model_assets`

Expected: pass with no network access.

- [ ] **Step 6: Commit**

```powershell
git add scripts/testing/workbook05/phase3/model_assets.py scripts/testing/workbook05/requirements.phase3-assets.in tests/testing/workbook05/test_phase3_model_assets.py tests/testing/workbook05/fixtures/phase3/assets/fake_hub_manifest.json
git commit -m "feat: resolve immutable model snapshots"
```

---

### Task 6: Freeze the model-conversion environment and compatibility decision

**Files:**
- Modify: `scripts/testing/workbook05/requirements.phase3-assets.in`
- Create: `scripts/testing/workbook05/requirements.phase3-assets.txt`
- Create: `scripts/testing/workbook05/phase3/conversion.py`
- Create: `tests/testing/workbook05/test_phase3_conversion.py`

**Interfaces:**
- Produces: `ConversionRequest`
- Produces: `build_optimum_argument_list(request: ConversionRequest) -> list[str]`
- Produces: `collect_conversion_record(...) -> dict[str, Any]`

- [ ] **Step 1: Write failing command-construction tests**

```python
def test_int4_asymmetric_command_is_an_argument_array(self) -> None:
    request = ConversionRequest(
        optimum_cli=Path(r"C:\w5c\tools\Scripts\optimum-cli.exe"),
        source_directory=Path(r"C:\w5m\sources\granite41-3b-bef400f9"),
        output_directory=Path(r"C:\w5m\converted\granite41-3b-int4a-g128-r100-bef400f9"),
        weight_format="int4",
        symmetric=False,
        group_size=128,
        ratio=1.0,
    )
    arguments = build_optimum_argument_list(request)
    self.assertEqual("export", arguments[0])
    self.assertNotIn("--sym", arguments)
    self.assertIn("128", arguments)
    self.assertNotIn(" ".join(arguments), arguments)
```

- [ ] **Step 2: Verify RED**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_conversion`

Expected: import failure.

- [ ] **Step 3: Freeze the reviewed candidate environment**

Add:

```text
huggingface_hub==1.24.0
optimum-intel==2.0.0
nncf==3.2.0
transformers==5.14.1
```

Generate the lock from a clean Python 3.12.10 virtual environment:

```powershell
python -m pip install pip-tools==7.5.0
pip-compile --generate-hashes --allow-unsafe --output-file scripts/testing/workbook05/requirements.phase3-assets.txt scripts/testing/workbook05/requirements.phase3-assets.in
```

Review every package name and hash. Do not include `openvino-genai`; conversion and accepted inference remain separate environments.

- [ ] **Step 4: Implement exact command and record generation**

The command is:

```text
optimum-cli export openvino
--model <absolute immutable source directory>
--task text-generation-with-past
--weight-format int4
--group-size 128
--ratio 1.0
--trust-remote-code
<new absolute output directory>
```

Omit `--sym` to select the reviewed asymmetric candidate. Record the exact executable hash, package list, argument array, source revision, output file hashes, start/end UTC, exit code, and stderr/stdout paths. A nonzero exit produces `Failed`, never a fallback asset.

- [ ] **Step 5: Add compatibility classification**

The conversion record may become `Candidate` only when the command exits zero, required IR/tokenizer/config files exist, every output is under its new directory, and no unrecorded file appears. It does not become `Accepted` until the later C3/C5 load checks pass.

- [ ] **Step 6: Verify GREEN**

Run:

```powershell
python -m unittest -v tests.testing.workbook05.test_phase3_conversion tests.testing.workbook05.test_phase3_contracts
```

Expected: pass.

- [ ] **Step 7: Commit**

```powershell
git add scripts/testing/workbook05/requirements.phase3-assets.in scripts/testing/workbook05/requirements.phase3-assets.txt scripts/testing/workbook05/phase3/conversion.py tests/testing/workbook05/test_phase3_conversion.py
git commit -m "feat: freeze Phase 3 conversion identity"
```

---

### Task 7: Implement the atomic asset-lock orchestrator

**Files:**
- Create: `scripts/testing/workbook05/Invoke-Workbook05Phase3AssetLock.ps1`
- Create: `tests/testing/workbook05/Invoke-Phase3AssetLockTests.Tests.ps1`

**Interfaces:**
- Consumes: C1 contracts, prerequisite verifier, disk preflight, immutable resolver, conversion command builder
- Produces local directories:
  - `C:\w5m\sources\granite41-3b-<revision-prefix>`
  - `C:\w5m\converted\granite41-3b-int4a-g128-r100-<revision-prefix>`
- Produces text evidence bundle under a new `C:\w5c\phase3-assets-<run>-<attempt>` workspace

- [ ] **Step 1: Write PowerShell tests for stage order and failure preservation**

The tests must prove this order:

```text
prerequisite verification
-> path/root verification
-> disk preflight
-> immutable revision resolution
-> source snapshot download
-> source-file hash inventory
-> conversion in a new output directory
-> converted-file hash inventory
-> schema validation
-> manifest generation
```

They must also prove that a failed stage leaves prior evidence intact, does not create an accepted record, and never deletes an existing source or conversion directory.

- [ ] **Step 2: Verify RED**

Run: `& '.\tests\testing\workbook05\Invoke-Phase3AssetLockTests.Tests.ps1'`

Expected: missing orchestrator failure.

- [ ] **Step 3: Implement the orchestrator with parameter injection**

Use parameters for the Python executable, asset settings, output workspace, and offline fixture mode. Invoke each native program through an executable path and argument array. Do not use `Invoke-Expression`, `cmd /c`, `powershell -Command` with interpolated strings, or an `iex` alias.

- [ ] **Step 4: Make records atomic**

Write each JSON record to `*.tmp`, validate it, flush it, and move it to its final path. Write `manifest.sha256` last. On failure, write `failure.json` with class `IntegrityFailure`, `Blocked`, or `Failed` as appropriate, then return nonzero for integrity/orchestration failure.

- [ ] **Step 5: Verify GREEN in offline fixture mode**

Run:

```powershell
& '.\tests\testing\workbook05\Invoke-Phase3AssetLockTests.Tests.ps1'
```

Expected: all stage-order, path, failure, and atomicity tests pass without network access.

- [ ] **Step 6: Commit**

```powershell
git add scripts/testing/workbook05/Invoke-Workbook05Phase3AssetLock.ps1 tests/testing/workbook05/Invoke-Phase3AssetLockTests.Tests.ps1
git commit -m "feat: orchestrate immutable Phase 3 assets"
```

---

### Task 8: Select and freeze a diagnostic asset by path-equivalence evidence

**Files:**
- Create: `scripts/testing/workbook05/phase3/diagnostic_selection.py`
- Create: `tests/testing/workbook05/test_phase3_diagnostic_selection.py`
- Create: `experiments/granite_turboquant_intel/configurations/workbook05/phase3-diagnostic-candidates.json`

**Interfaces:**
- Produces: `evaluate_diagnostic_candidate(candidate, evidence) -> DiagnosticDecision`
- Candidate statuses: `PathEquivalent`, `HarnessOnly`, `Rejected`

- [ ] **Step 1: Write failing selection tests**

Test that a tiny model cannot be marked `PathEquivalent` merely because it generates text. Require evidence that it reaches the same CPU stateful SDPA/KV-cache implementation and exact accepted Runtime source identity.

- [ ] **Step 2: Verify RED**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_diagnostic_selection`

Expected: import failure.

- [ ] **Step 3: Implement the three-candidate decision order**

```json
{
  "candidate_order": [
    "project-generated-stateful-ir",
    "source-matched-cpu-sdpa-target",
    "small-official-text-generation-model"
  ],
  "required_runtime_source_commit": "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
  "required_backend": "OpenVINO CPU stateful SDPA KV-cache path"
}
```

A candidate with process-harness value but no path proof is labelled `HarnessOnly`; it may be used by C2 but not as codec activation evidence in C3.

- [ ] **Step 4: Verify GREEN**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_diagnostic_selection`

Expected: pass.

- [ ] **Step 5: Commit**

```powershell
git add scripts/testing/workbook05/phase3/diagnostic_selection.py tests/testing/workbook05/test_phase3_diagnostic_selection.py experiments/granite_turboquant_intel/configurations/workbook05/phase3-diagnostic-candidates.json
git commit -m "feat: classify Phase 3 diagnostic assets"
```

---

### Task 9: Validate C1 evidence as untrusted data

**Files:**
- Create: `scripts/testing/workbook05/phase3/asset_bundle_validation.py`
- Create: `tests/testing/workbook05/test_phase3_asset_bundle_validation.py`
- Create fixtures under: `tests/testing/workbook05/fixtures/phase3/asset-bundles/`

**Interfaces:**
- Produces: `validate_asset_bundle(bundle_root: Path, repository_root: Path) -> list[BundleIssue]`
- CLI: `python -m scripts.testing.workbook05.phase3.asset_bundle_validation --bundle-root ... --repository-root ... --report ...`

- [ ] **Step 1: Write adversarial failing tests**

Create fixture mutations for:

- changed hash manifest;
- missing prerequisite proof;
- unsafe parent traversal;
- absolute local evidence path in a portable field;
- secret token pattern;
- `.bin`, `.xml`, `.safetensors`, `.exe`, `.dll`, `.zip`, or unknown binary payload;
- wrong model repository/revision;
- `Accepted` asset with failed conversion;
- scientific authorisation set true.

- [ ] **Step 2: Verify RED**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_asset_bundle_validation`

Expected: import failure.

- [ ] **Step 3: Implement the validator by reusing common policy**

Reuse `verify_hash_manifest`, `validate_phase3_record`, and the existing stable `BundleIssue` shape. The validator must read files as UTF-8 data and never import, load, unzip nested archives, or execute bundle content.

Required portable files:

```text
prerequisite-proof.json
asset-lock.json
conversion-record.json
disk-preflight.json
source-files.csv
converted-files.csv
commands/conversion.json
logs/conversion.stdout.txt
logs/conversion.stderr.txt
manifest.sha256
summary.md
```

The CSVs contain metadata and hashes only, never file bytes.

- [ ] **Step 4: Verify GREEN**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_asset_bundle_validation`

Expected: valid fixture passes and every adversarial fixture fails with a stable issue code.

- [ ] **Step 5: Commit**

```powershell
git add scripts/testing/workbook05/phase3/asset_bundle_validation.py tests/testing/workbook05/test_phase3_asset_bundle_validation.py tests/testing/workbook05/fixtures/phase3/asset-bundles
git commit -m "test: validate Phase 3 asset bundles"
```

---

### Task 10: Add the repository gate and exact three-boundary workflow

**Files:**
- Create: `scripts/testing/Validate-Workbook05-Phase3.ps1`
- Create: `.github/workflows/workbook-05-phase3-assets.yml`
- Create: `tests/testing/workbook05/test_phase3_asset_workflow_contract.py`
- Modify: `.github/workflows/build-and-test.yml`

**Interfaces:**
- Repository success marker: `WORKBOOK05_PHASE3_GATE_PASS`
- Artifact name: `workbook-05-phase3-assets-${{ github.run_id }}-${{ github.run_attempt }}`

- [ ] **Step 1: Write failing workflow-contract tests**

Assert that:

- PR events run only the hosted repository contract and cannot reach self-hosted collection;
- manual dispatch collection requires `refs/heads/main`;
- the collector uses the exact five labels and read-only permissions;
- all action references are full commit SHAs;
- checkout drops credentials and uses exact `github.sha`;
- the collector calls `Validate-Workbook05-Phase3.ps1` before C1 orchestration;
- the hosted validator downloads exactly one same-attempt artifact;
- the workflow contains no `git push`, model upload, repository write, or shell-string execution;
- `cancel-in-progress` is false.

- [ ] **Step 2: Verify RED**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_asset_workflow_contract`

Expected: missing workflow failure.

- [ ] **Step 3: Implement the Phase 3 repository gate**

The gate must:

1. install only `scripts/testing/workbook05/requirements.txt` into runner temp;
2. run all existing Workbook 05 tests;
3. run every Phase 3 Python test;
4. run every PowerShell `*.Tests.ps1` file;
5. scan for forbidden model/executable payloads under repository-controlled evidence directories;
6. run `git diff --check`;
7. print `WORKBOOK05_PHASE3_GATE_PASS` only after all checks pass.

Asset-only dependencies are installed by the self-hosted collection job into an ephemeral venv from the hash-locked C1 requirements file, not by the repository gate.

- [ ] **Step 4: Implement workflow boundaries**

Jobs:

```text
repository-contract     GitHub-hosted, PR and manual, no model access
collect-assets          self-hosted Intel, manual main only
validate-assets         GitHub-hosted, same-attempt artifact as untrusted data
```

The collector retains assets locally under `C:\w5m` but uploads only the text evidence workspace.

- [ ] **Step 5: Add Phase 3 paths to build-and-test sparse checkout**

Ensure normal CI sees the new schemas, templates, scripts, tests, configuration, and documentation without adding `C:\w5m` or any generated external files.

- [ ] **Step 6: Verify GREEN**

Run:

```powershell
python -m unittest -v tests.testing.workbook05.test_phase3_asset_workflow_contract
& '.\scripts\testing\Validate-Workbook05-Phase3.ps1' -PythonPath 'python'
```

Expected: workflow contract passes and the final gate marker is printed.

- [ ] **Step 7: Commit**

```powershell
git add scripts/testing/Validate-Workbook05-Phase3.ps1 .github/workflows/workbook-05-phase3-assets.yml .github/workflows/build-and-test.yml tests/testing/workbook05/test_phase3_asset_workflow_contract.py
git commit -m "ci: add Phase 3 asset-locking workflow"
```

---

### Task 11: Document operator actions and scientific claim boundary

**Files:**
- Create: `docs/testing/workbook05/phase3-asset-lock-runbook.md`
- Modify: `README.md`

- [ ] **Step 1: Write the runbook before live execution**

Document exact prerequisites, expected free space, accepted paths/hashes, workflow input, local output locations, artifact name, validation report, recovery from interruption, and the prohibition on broad cleanup.

- [ ] **Step 2: Add the exact manual sequence**

```text
1. Merge the C1 implementation PR after exact-head CI passes.
2. Confirm the Intel runner service is idle and all four accepted Phase 2 directories exist.
3. Dispatch Workbook 05 Phase 3 assets from main.
4. Wait for repository-contract, collect-assets, and validate-assets.
5. Download the text-only evidence artifact.
6. Recalculate the artifact SHA-256.
7. Verify the decision and local model-asset directories against the retained records.
8. Copy only the accepted text evidence and decision to a stable C:\w5m acceptance directory.
9. Record project-owner acceptance; do not begin C2/C3 live model work from an unaccepted asset.
```

- [ ] **Step 3: State non-claims prominently**

C1 does not prove model loading, token generation, device placement, cache selection, TurboQuant activation, storage reduction, performance, context length, perplexity, or quality.

- [ ] **Step 4: Commit**

```powershell
git add docs/testing/workbook05/phase3-asset-lock-runbook.md README.md
git commit -m "docs: add Phase 3 asset-lock runbook"
```

---

### Task 12: Run the complete C1 verification and prepare the package PR

**Files:**
- Review all C1 files listed above
- Do not add live artifacts or generated model files to Git

- [ ] **Step 1: Run focused tests**

```powershell
python -m unittest -v `
  tests.testing.workbook05.test_phase3_contracts `
  tests.testing.workbook05.test_phase3_paths `
  tests.testing.workbook05.test_phase3_prerequisites `
  tests.testing.workbook05.test_phase3_disk_preflight `
  tests.testing.workbook05.test_phase3_model_assets `
  tests.testing.workbook05.test_phase3_conversion `
  tests.testing.workbook05.test_phase3_diagnostic_selection `
  tests.testing.workbook05.test_phase3_asset_bundle_validation `
  tests.testing.workbook05.test_phase3_asset_workflow_contract
```

Expected: all pass.

- [ ] **Step 2: Run PowerShell suites**

```powershell
& '.\tests\testing\workbook05\Invoke-Phase3PrerequisiteTests.Tests.ps1'
& '.\tests\testing\workbook05\Invoke-Phase3AssetLockTests.Tests.ps1'
```

Expected: pass.

- [ ] **Step 3: Run the complete gate**

```powershell
& '.\scripts\testing\Validate-Workbook05-Phase3.ps1' -PythonPath 'python'
```

Expected final line: `WORKBOOK05_PHASE3_GATE_PASS`.

- [ ] **Step 4: Inspect repository cleanliness**

```powershell
git diff --check
git status --short
git ls-files | Select-String -Pattern '\.(safetensors|bin|xml|onnx|exe|dll|pyd|zip)$'
```

Expected: no whitespace errors, no uncommitted generated files, and no forbidden new payloads.

- [ ] **Step 5: Create the pull request with complete context**

The PR must explain every schema, module, workflow boundary, exact dependency pin, failure classification, local directory, and non-claim. Include RED and GREEN command output and state that live asset acquisition occurs only after merge.

- [ ] **Step 6: Obtain exact-head verification before merge**

Record the final commit SHA and GitHub Actions run IDs. Do not merge on the basis of an earlier head.

## C1 Acceptance Gate

C1 is accepted only when:

- the exact accepted Runtime/GenAI decisions are revalidated;
- a diagnostic decision is recorded without overstating path equivalence;
- Granite 4.1 3B source and tokenizer files are bound to an immutable official revision;
- the INT4 asymmetric group-128 ratio-1.0 conversion attempt has complete provenance;
- local source and converted outputs are under normal `C:\w5m` directories;
- the text-only artifact passes hosted validation;
- the project owner independently accepts the artifact and decision hashes;
- all later scientific authorisations remain false.

## Textbook and Primary-Source Basis

- *Code Complete*, Chapters 3, 8, 22, and 28: verify prerequisites, defend boundaries, retain test records, and freeze tool/machine configuration.
- *Designing Secure Software*, Chapters 4, 7, 10, and 12: secure defaults, design review, untrusted input handling, and security testing.
- *The Art of Unit Testing*, Chapters 1, 7, and 10: tests must fail for the intended missing behavior, remain trustworthy, and cover unit plus component boundaries.
- *Why Programs Fail*, Chapters 4–6: reproduce exact environments, simplify failure causes, and use explicit hypotheses rather than repairing evidence after the fact.
- *Systems Engineering: Principles and Practice*, Chapters 6, 12, and 17: requirements traceability, risk reduction, and independently reviewable test evidence.
- IBM's official Granite 4.1 model cards define the model family, licence, architecture metadata, and instruct repositories; these declarations are recorded as source metadata, not observed runtime capability.
- OpenVINO's official weight-compression documentation defines INT4 symmetric/asymmetric modes, group size, ratio, and the performance/quality trade-off; C1 records the exact choice instead of treating the label `INT4` as a complete method.