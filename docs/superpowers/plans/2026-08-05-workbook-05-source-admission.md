# Workbook 05 Staged Source-Admission Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and execute the read-only `phase-1-source-admission` gate for Workbook 05, while hardening the downstream measured-run contract so no successful future inference row can omit performance, resource, activation, fallback, raw-output, or quality evidence.

**Architecture:** Phase 1 uses a self-hosted Intel collection job and a separate GitHub-hosted validator. The Intel job verifies safe workspace boundaries, clones exact allowlisted OpenVINO commits and recursive submodules into `C:\wb05`, captures authoritative documents and source classifications, audits Route B test discovery, and performs only a Route A CMake configure probe. A versioned measurement-control report binds later model runs to the frozen metric definitions, prompt set, rubric, matched-baseline rules, and a strict measured-run schema.

**Tech Stack:** Python 3.12.10, Python standard library, `jsonschema==4.25.1`, Windows PowerShell 5.1, Git 2.53+, CMake 4.3.1-msvc1, Visual Studio 2022 generator through installed Visual Studio Build Tools, JSON Schema Draft 2020-12, GitHub Actions, SHA-256, Markdown, JSON, and text evidence.

## Global Constraints

- Work only on `testing/workbook-05-source-admission` until its pull request is reviewed.
- Campaign ID is exactly `GTQ-WB05-MF-v1`.
- Phase ID is exactly `phase-1-source-admission`.
- Route A ID is exactly `route-a-merged-openvino`.
- Route B ID is exactly `route-b-experimental-qjl-polar`.
- Route A Runtime is `openvinotoolkit/openvino` commit `b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- Route A GenAI candidate is `openvinotoolkit/openvino.genai` commit `05e5c7670b597746f858946974d11f38e3baf42f`.
- Route B is `EgorDuplensky/openvino` commit `1827f6458d049de11c1a8203c793af67c99935dc`, base `7de5a4fbb178a1de43f6bc3cccc95ff656abed05`.
- Source-admission status values remain exactly `Candidate`, `Admitted`, and `Blocked`.
- Route B remains experimental even when admitted.
- `RB-SRC-001` remains open until captured evidence proves required test instances are not omitted or a later reviewed correction resolves it.
- External workspaces use only `C:\wb05` and remain outside the project repository.
- Route A and Route B never share source, build, install, temporary, or evidence directories.
- The self-hosted runner retains only `contents: read` and `actions: read`.
- Pull requests from forks cannot execute the self-hosted job.
- All third-party Actions are pinned to immutable commit SHAs.
- Checkout uses `persist-credentials: false`.
- Captured README commands remain inert evidence.
- Phase 1 may run CMake generation for Route A but may not run `cmake --build`, compile, install, package, download a model, run inference, or produce benchmark values.
- Phase 1 may not run a Route B configure probe while `RB-SRC-001` remains open.
- Python is exactly `C:\Program Files\Python312\python.exe`, version `3.12.10` on the Intel runner.
- Route A configure options include generator `Visual Studio 17 2022`, architecture `x64`, and `ENABLE_INTEL_GPU=OFF`.
- Raw evidence is immutable after upload. Corrections use a new workflow attempt.
- OpenVINO trees, submodules, DLLs, executables, wheels, archives, packages, and models are never uploaded as evidence or committed.
- Every source-presence claim is labelled `Present in source`; it is never promoted to `Executable`, `Activated`, or `Supported` during Phase 1.
- Quality controls remain `GTQ-PROMPTS-v1` and `GTQ-QUALITY-RUBRIC-v1`.
- Later successful measured runs must preserve raw output and all applicable metrics from `docs/testing/Metric-Definitions.md`.
- Weight quantisation and KV-cache compression are separate comparison axes.
- The project-specific material-quality-degradation rule is controlled by `docs/superpowers/specs/2026-08-05-workbook-05-quality-metrics-amendment.md`.
- Every production code block includes beginner-readable comments explaining purpose and non-obvious decisions.
- Each task follows red/green/refactor, ends in a focused commit, and receives a fresh review before the next task.

---

## Locked File Structure

```text
.github/workflows/
└── workbook-05-source-admission.yml

docs/superpowers/plans/
└── 2026-08-05-workbook-05-source-admission.md

docs/superpowers/specs/
├── 2026-08-05-workbook-05-source-admission-design.md
└── 2026-08-05-workbook-05-quality-metrics-amendment.md

docs/testing/
└── Decision-Log.md

experiments/granite_turboquant_intel/
├── configurations/workbook05/
│   ├── measurement-controls.json
│   └── source-admission-settings.json
├── manifests/templates/workbook05/
│   ├── cmake-test-discovery-report-template.json
│   ├── configure-probe-report-template.json
│   ├── measured-run-manifest-template.json
│   ├── measurement-controls-report-template.json
│   ├── source-capability-report-template.json
│   ├── source-tree-report-template.json
│   └── source-admission-summary-template.json
└── schemas/workbook05/
    ├── cmake-test-discovery-report.schema.json
    ├── configure-probe-report.schema.json
    ├── measured-run-manifest.schema.json
    ├── measurement-controls-report.schema.json
    ├── source-capability-report.schema.json
    ├── source-tree-report.schema.json
    └── source-admission-summary.schema.json

scripts/testing/workbook05/
├── cmake_test_discovery.py
├── configure_probe.py
├── measurement_controls.py
├── source_admission_bundle_validation.py
├── source_admission_phase.py
├── source_capability.py
├── source_verification.py
├── workspace_policy.py
├── Invoke-Workbook05SourceAdmission.ps1
└── Workbook05.SourceAdmission.psm1

scripts/testing/
└── Validate-Workbook05-SourceAdmission.ps1

tests/testing/workbook05/
├── fixtures/source-admission/
│   ├── route-a-internal-properties.hpp
│   ├── route-a-config.cpp
│   ├── route-b-internal-properties.hpp
│   ├── route-b-attn-quant-turboq.cpp
│   ├── route-b-polar-codecs.hpp
│   ├── route-b-target-per-test.cmake
│   ├── generated-target-complete.txt
│   ├── generated-target-omitted.txt
│   ├── cmake-cache-route-a.txt
│   ├── cmake-cache-wrong-source.txt
│   └── fake-cmake.py
├── Invoke-SourceAdmissionModuleTests.ps1
├── test_cmake_test_discovery.py
├── test_configure_probe.py
├── test_measurement_controls.py
├── test_source_admission_bundle.py
├── test_source_admission_phase.py
├── test_source_admission_settings.py
├── test_source_capability.py
├── test_source_verification.py
├── test_source_workflow_contract.py
└── test_workspace_policy.py
```

## Stable Interfaces Between Tasks

```python
# scripts/testing/workbook05/measurement_controls.py
@dataclass(frozen=True)
class MeasurementControlIssue:
    code: str
    path: str
    message: str


def capture_measurement_controls(
    repository_root: Path,
    configuration_path: Path,
    destination: Path,
) -> dict[str, Any]: ...


def validate_measurement_controls(
    report: Mapping[str, Any],
) -> list[MeasurementControlIssue]: ...


# scripts/testing/workbook05/workspace_policy.py
@dataclass(frozen=True)
class WorkspaceDecision:
    permitted: bool
    canonical_path: str
    reasons: tuple[str, ...]


def evaluate_workspace_path(
    requested: PureWindowsPath,
    allowed_root: PureWindowsPath = PureWindowsPath("C:/wb05"),
) -> WorkspaceDecision: ...


# scripts/testing/workbook05/source_verification.py
@dataclass(frozen=True)
class NativeCommandRecord:
    argv: tuple[str, ...]
    working_directory: str
    started_utc: str
    ended_utc: str
    exit_code: int
    stdout_path: str
    stderr_path: str


@dataclass(frozen=True)
class SourceTreeResult:
    report: dict[str, Any]
    command_records: tuple[NativeCommandRecord, ...]


def verify_source_tree(
    route: Mapping[str, Any],
    source_directory: Path,
    evidence_directory: Path,
    *,
    timeout_seconds: int,
) -> SourceTreeResult: ...


# scripts/testing/workbook05/source_capability.py
@dataclass(frozen=True)
class CapabilityFinding:
    capability_id: str
    classification: str
    source_path: str
    matched_tokens: tuple[str, ...]
    missing_tokens: tuple[str, ...]
    contradictory_tokens: tuple[str, ...]
    sha256: str


def inspect_route_capabilities(
    source_root: Path,
    requirements: Sequence[Mapping[str, Any]],
) -> list[CapabilityFinding]: ...


# scripts/testing/workbook05/cmake_test_discovery.py
@dataclass(frozen=True)
class CMakeDiscoveryDecision:
    blocker_confirmed: bool
    intended_sources: tuple[str, ...]
    final_sources: tuple[str, ...]
    omitted_sources: tuple[str, ...]
    reasons: tuple[str, ...]


def audit_target_per_test(
    cmake_text: str,
    test_root: Path,
    class_file_name: str,
    generated_metadata_text: str,
) -> CMakeDiscoveryDecision: ...


# scripts/testing/workbook05/configure_probe.py
@dataclass(frozen=True)
class ConfigureProbeDecision:
    passed: bool
    command: tuple[str, ...]
    cache_values: Mapping[str, str]
    reasons: tuple[str, ...]


def build_route_a_configure_command(
    cmake_path: Path,
    source_root: Path,
    build_root: Path,
) -> tuple[str, ...]: ...


def evaluate_route_a_configure_probe(
    command: Sequence[str],
    exit_code: int,
    cmake_cache_text: str,
    expected_source_root: Path,
) -> ConfigureProbeDecision: ...


# scripts/testing/workbook05/source_admission_phase.py
@dataclass(frozen=True)
class PhaseDecision:
    route_a_status: str
    route_b_status: str
    phase_status: str
    reasons: tuple[str, ...]


def calculate_phase_decision(
    route_a_record: Mapping[str, Any],
    route_b_record: Mapping[str, Any],
) -> PhaseDecision: ...


def assemble_source_admission_bundle(
    repository_root: Path,
    evidence_root: Path,
    route_a_report: Mapping[str, Any],
    route_b_report: Mapping[str, Any],
    measurement_report: Mapping[str, Any],
) -> PhaseDecision: ...


# scripts/testing/workbook05/source_admission_bundle_validation.py
@dataclass(frozen=True)
class SourceAdmissionBundleIssue:
    code: str
    path: str
    message: str


def validate_source_admission_bundle(
    bundle_root: Path,
    repository_root: Path,
) -> list[SourceAdmissionBundleIssue]: ...
```

PowerShell contracts:

```powershell
# scripts/testing/workbook05/Workbook05.SourceAdmission.psm1
Test-Workbook05ExternalWorkspace -WorkspaceRoot <string>
Invoke-Workbook05RecordedCommand -FilePath <string> -ArgumentList <string[]> -WorkingDirectory <string> -EvidenceDirectory <string> -CommandId <string> -TimeoutSeconds <int>
```

---

### Task 1: Harden the Future Performance and Quality Evidence Contract

**Files:**
- Create: `tests/testing/workbook05/test_measurement_controls.py`
- Create: `experiments/granite_turboquant_intel/configurations/workbook05/measurement-controls.json`
- Create: `experiments/granite_turboquant_intel/manifests/templates/workbook05/measured-run-manifest-template.json`
- Create: `experiments/granite_turboquant_intel/manifests/templates/workbook05/measurement-controls-report-template.json`
- Create: `experiments/granite_turboquant_intel/schemas/workbook05/measured-run-manifest.schema.json`
- Create: `experiments/granite_turboquant_intel/schemas/workbook05/measurement-controls-report.schema.json`
- Create: `scripts/testing/workbook05/measurement_controls.py`
- Modify: `docs/testing/Decision-Log.md`

**Interfaces:**
- Consumes: `docs/testing/Metric-Definitions.md`, `GTQ-PROMPTS-v1`, `GTQ-QUALITY-RUBRIC-v1`, and the quality amendment.
- Produces: `capture_measurement_controls(...)`, `validate_measurement_controls(...)`, a strict measured-run schema, and decision `TD-014`.

- [ ] **Step 1: Write the failing metric-control tests**

```python
from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.measurement_controls import (
    capture_measurement_controls,
    validate_measurement_controls,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
CONFIGURATION = (
    REPOSITORY_ROOT
    / "experiments/granite_turboquant_intel/configurations/workbook05/measurement-controls.json"
)


class MeasurementControlTests(unittest.TestCase):
    def test_measured_run_schema_requires_quality_and_separate_kv_metrics(self) -> None:
        schema_path = (
            REPOSITORY_ROOT
            / "experiments/granite_turboquant_intel/schemas/workbook05/measured-run-manifest.schema.json"
        )
        schema = json.loads(schema_path.read_text(encoding="utf-8"))
        required_resources = schema["properties"]["resources"]["required"]
        required_quality = schema["properties"]["quality"]["required"]

        self.assertIn("k_cache_allocated_bytes", required_resources)
        self.assertIn("v_cache_allocated_bytes", required_resources)
        self.assertIn("raw_output_sha256", required_quality)
        self.assertIn("deterministic_failures", required_quality)
        self.assertIn("dimension_scores", required_quality)
        self.assertIn("critical_caps", required_quality)
        self.assertIn("matched_baseline_run_id", required_quality)
        self.assertIn("paired_score_delta", required_quality)
        self.assertIn("material_degradation", required_quality)

    def test_controlling_measurement_assets_are_hashed_and_valid(self) -> None:
        report = capture_measurement_controls(
            REPOSITORY_ROOT,
            CONFIGURATION,
            REPOSITORY_ROOT / "measurement-controls-test.json",
        )
        issues = validate_measurement_controls(report)

        self.assertEqual([], issues)
        self.assertEqual("GTQ-PROMPTS-v1", report["prompt_set_id"])
        self.assertEqual("GTQ-QUALITY-RUBRIC-v1", report["rubric_id"])
        self.assertAlmostEqual(1.0, report["rubric_weight_total"])
        self.assertEqual(6, report["prompt_count"])
        self.assertTrue(report["raw_output_required"])
        self.assertTrue(report["activation_proof_required"])
        self.assertTrue(report["fallback_result_required"])


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run the focused tests and verify the contract is absent**

Run:

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest `
    tests.testing.workbook05.test_measurement_controls -v
```

Expected: FAIL because the new module, configuration, schema, and template do not exist.

- [ ] **Step 3: Add the exact measurement-control configuration**

Create `measurement-controls.json` with these controlled paths and identifiers:

```json
{
  "schema_version": "1.0",
  "campaign_id": "GTQ-WB05-MF-v1",
  "metric_definitions_path": "docs/testing/Metric-Definitions.md",
  "prompt_set_path": "experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json",
  "prompt_set_id": "GTQ-PROMPTS-v1",
  "rubric_path": "experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json",
  "rubric_id": "GTQ-QUALITY-RUBRIC-v1",
  "quality_amendment_path": "docs/superpowers/specs/2026-08-05-workbook-05-quality-metrics-amendment.md",
  "measured_run_schema_path": "experiments/granite_turboquant_intel/schemas/workbook05/measured-run-manifest.schema.json",
  "measured_run_template_path": "experiments/granite_turboquant_intel/manifests/templates/workbook05/measured-run-manifest-template.json",
  "required_prompt_ids": ["P1", "P2", "P3", "P4", "P5", "P6"],
  "required_metric_names": [
    "Model load time",
    "TTFT",
    "Prompt-processing throughput",
    "TPOT",
    "Decode throughput",
    "Total generation time",
    "Peak working set",
    "Peak private bytes",
    "Minimum available RAM",
    "KV-cache allocation",
    "Quality score",
    "Stability"
  ]
}
```

- [ ] **Step 4: Implement the measured-run schema and template**

The schema must require:

```json
{
  "references": [
    "model_id",
    "model_hash",
    "tokenizer_hash",
    "weight_quantization",
    "prompt_set_id",
    "prompt_id",
    "rubric_id",
    "matched_baseline_run_id"
  ],
  "execution": [
    "requested_backend",
    "actual_backend",
    "requested_device",
    "actual_device",
    "requested_k_codec",
    "verified_k_codec",
    "requested_v_codec",
    "verified_v_codec",
    "activation_proof_path",
    "fallback_check_path",
    "fallback_observed"
  ],
  "performance": [
    "model_load_time_ms",
    "ttft_ms",
    "prompt_processing_tokens_per_second",
    "tpot_ms",
    "decode_tokens_per_second",
    "total_generation_time_ms",
    "perplexity"
  ],
  "resources": [
    "peak_working_set_bytes",
    "peak_private_bytes",
    "available_ram_before_bytes",
    "minimum_available_ram_during_bytes",
    "available_ram_after_bytes",
    "k_cache_allocated_bytes",
    "v_cache_allocated_bytes",
    "cpu_mean_percent",
    "cpu_peak_percent"
  ],
  "quality": [
    "raw_output_path",
    "raw_output_sha256",
    "deterministic_checks_path",
    "deterministic_failures",
    "dimension_scores",
    "critical_caps",
    "score_0_to_10",
    "matched_baseline_run_id",
    "paired_score_delta",
    "judge_label_hidden",
    "pairwise_order",
    "adjudication_path",
    "material_degradation"
  ]
}
```

Use nullable numeric values for genuinely missing observations, but require `classification.missing_data_codes` to explain every missing applicable field. Do not permit absent keys on a row classified `Passed`.

- [ ] **Step 5: Implement `measurement_controls.py`**

```python
"""Capture and validate the frozen Workbook 05 measurement controls."""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping


@dataclass(frozen=True)
class MeasurementControlIssue:
    """One deterministic problem in the future measurement contract."""

    code: str
    path: str
    message: str


def _sha256(path: Path) -> str:
    """Hash one controlling file without normalising its bytes."""

    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def capture_measurement_controls(
    repository_root: Path,
    configuration_path: Path,
    destination: Path,
) -> dict[str, Any]:
    """Capture IDs, hashes, rubric weights, and strict completion requirements."""

    configuration = json.loads(configuration_path.read_text(encoding="utf-8"))
    prompt_path = repository_root / configuration["prompt_set_path"]
    rubric_path = repository_root / configuration["rubric_path"]
    prompt_set = json.loads(prompt_path.read_text(encoding="utf-8"))
    rubric = json.loads(rubric_path.read_text(encoding="utf-8"))

    report = {
        "schema_version": "1.0",
        "campaign_id": configuration["campaign_id"],
        "prompt_set_id": prompt_set["prompt_set_id"],
        "rubric_id": rubric["rubric_id"],
        "prompt_count": len(prompt_set["prompts"]),
        "prompt_ids": [prompt["prompt_id"] for prompt in prompt_set["prompts"]],
        "rubric_weight_total": sum(item["weight"] for item in rubric["dimensions"]),
        "controls": [
            {
                "path": relative,
                "sha256": _sha256(repository_root / relative),
            }
            for relative in (
                configuration["metric_definitions_path"],
                configuration["prompt_set_path"],
                configuration["rubric_path"],
                configuration["quality_amendment_path"],
                configuration["measured_run_schema_path"],
                configuration["measured_run_template_path"],
            )
        ],
        "raw_output_required": True,
        "activation_proof_required": True,
        "fallback_result_required": True,
        "separate_k_v_allocation_required": True,
        "weight_and_kv_axes_separate": True,
    }
    destination.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return report
```

Implement `validate_measurement_controls` to reject wrong IDs, missing P1-P6, rubric weights not equal to `1.0` within `1e-9`, missing hashes, absent raw-output/activation/fallback requirements, or an invalid measured-run template against its schema.

- [ ] **Step 6: Record decision `TD-014`**

Append a decision stating that performance and quality are independent first-class outcomes; matched baselines must keep weight quantisation fixed; successful measured inference is incomplete without raw output, per-prompt deterministic and rubric evidence, activation/fallback proof, separate K/V allocation, and applicable performance/resource metrics.

- [ ] **Step 7: Run focused and full tests**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest `
    tests.testing.workbook05.test_measurement_controls -v

& 'C:\Program Files\Python312\python.exe' -m unittest discover `
    -s 'tests/testing/workbook05' `
    -p 'test_*.py' `
    -v
```

Expected: PASS, with no temporary `measurement-controls-test.json` left in the repository after test cleanup.

- [ ] **Step 8: Commit**

```powershell
git add `
  docs/testing/Decision-Log.md `
  experiments/granite_turboquant_intel/configurations/workbook05/measurement-controls.json `
  experiments/granite_turboquant_intel/manifests/templates/workbook05/measured-run-manifest-template.json `
  experiments/granite_turboquant_intel/manifests/templates/workbook05/measurement-controls-report-template.json `
  experiments/granite_turboquant_intel/schemas/workbook05/measured-run-manifest.schema.json `
  experiments/granite_turboquant_intel/schemas/workbook05/measurement-controls-report.schema.json `
  scripts/testing/workbook05/measurement_controls.py `
  tests/testing/workbook05/test_measurement_controls.py

git commit -m "test(workbook-05): require complete performance and quality evidence"
```

---

### Task 2: Define the Source-Admission Configuration and Evidence Schemas

**Files:**
- Create: `tests/testing/workbook05/test_source_admission_settings.py`
- Create: `experiments/granite_turboquant_intel/configurations/workbook05/source-admission-settings.json`
- Create: seven source-admission templates and six source-admission schemas listed in the locked structure.

**Interfaces:**
- Consumes: pinned route commits, `C:\wb05`, Route A expected source paths, Route B changed-file paths, and existing source-admission records.
- Produces: validated versioned settings and evidence contracts used by Tasks 3-11.

- [ ] **Step 1: Write the failing settings test**

```python
from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.schema_validation import assert_valid_json_file


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]


class SourceAdmissionSettingsTests(unittest.TestCase):
    def test_settings_pin_both_routes_and_safe_workspace(self) -> None:
        path = (
            REPOSITORY_ROOT
            / "experiments/granite_turboquant_intel/configurations/workbook05/source-admission-settings.json"
        )
        settings = json.loads(path.read_text(encoding="utf-8"))

        self.assertEqual("C:\\wb05", settings["workspace_root"])
        self.assertEqual(
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            settings["routes"]["route-a-merged-openvino"]["runtime"]["commit"],
        )
        self.assertEqual(
            "1827f6458d049de11c1a8203c793af67c99935dc",
            settings["routes"]["route-b-experimental-qjl-polar"]["runtime"]["commit"],
        )
        self.assertEqual(
            ["-G", "Visual Studio 17 2022", "-A", "x64", "-DENABLE_INTEL_GPU=OFF"],
            settings["route_a_configure_options"],
        )
        self.assertFalse(settings["allow_route_b_configure_while_blocked"])

    def test_every_template_passes_its_schema(self) -> None:
        template_root = (
            REPOSITORY_ROOT
            / "experiments/granite_turboquant_intel/manifests/templates/workbook05"
        )
        schema_root = (
            REPOSITORY_ROOT
            / "experiments/granite_turboquant_intel/schemas/workbook05"
        )
        bindings = {
            "source-tree-report-template.json": "source-tree-report.schema.json",
            "source-capability-report-template.json": "source-capability-report.schema.json",
            "cmake-test-discovery-report-template.json": "cmake-test-discovery-report.schema.json",
            "configure-probe-report-template.json": "configure-probe-report.schema.json",
            "source-admission-summary-template.json": "source-admission-summary.schema.json",
        }
        for template_name, schema_name in bindings.items():
            with self.subTest(template=template_name):
                assert_valid_json_file(
                    template_root / template_name,
                    schema_root / schema_name,
                )
```

- [ ] **Step 2: Run and confirm missing settings/schemas**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest `
    tests.testing.workbook05.test_source_admission_settings -v
```

Expected: FAIL because the files do not exist.

- [ ] **Step 3: Create exact settings**

The JSON must include:

```json
{
  "schema_version": "1.0",
  "campaign_id": "GTQ-WB05-MF-v1",
  "phase_id": "phase-1-source-admission",
  "workspace_root": "C:\\wb05",
  "minimum_free_bytes": 85899345920,
  "clone_timeout_seconds": 3600,
  "submodule_timeout_seconds": 7200,
  "configure_timeout_seconds": 3600,
  "route_a_configure_options": [
    "-G",
    "Visual Studio 17 2022",
    "-A",
    "x64",
    "-DENABLE_INTEL_GPU=OFF"
  ],
  "allow_route_b_configure_while_blocked": false,
  "routes": {
    "route-a-merged-openvino": {
      "runtime": {
        "repository_full_name": "openvinotoolkit/openvino",
        "origin_url": "https://github.com/openvinotoolkit/openvino.git",
        "commit": "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
        "source_directory": "C:\\wb05\\source\\route-a\\openvino"
      },
      "genai": {
        "repository_full_name": "openvinotoolkit/openvino.genai",
        "origin_url": "https://github.com/openvinotoolkit/openvino.genai.git",
        "commit": "05e5c7670b597746f858946974d11f38e3baf42f",
        "source_directory": "C:\\wb05\\source\\route-a\\openvino.genai"
      },
      "build_directory": "C:\\wb05\\build\\route-a-runtime"
    },
    "route-b-experimental-qjl-polar": {
      "runtime": {
        "repository_full_name": "EgorDuplensky/openvino",
        "origin_url": "https://github.com/EgorDuplensky/openvino.git",
        "commit": "1827f6458d049de11c1a8203c793af67c99935dc",
        "base_commit": "7de5a4fbb178a1de43f6bc3cccc95ff656abed05",
        "source_directory": "C:\\wb05\\source\\route-b\\openvino"
      },
      "build_directory": "C:\\wb05\\build\\route-b-runtime",
      "blocker_id": "RB-SRC-001"
    }
  }
}
```

Add exact source requirements for Route A files from PR #35853 and Route B files from PR #35092. Route B requirements must include the contradictory `not yet supported` QJL comment as a negative/contradictory token, so it is captured rather than hidden.

- [ ] **Step 4: Create strict schemas and valid templates**

Schemas use `additionalProperties: false`, exact campaign and route enums, lowercase 40-character SHAs, safe relative evidence paths, and explicit status enums. Configure-probe reports must contain `build_invoked: false` and `install_invoked: false` constants.

- [ ] **Step 5: Run focused and full schema tests**

Expected: every template validates and every wrong route/commit/status fixture fails with a readable JSON path.

- [ ] **Step 6: Commit**

```powershell
git add `
  experiments/granite_turboquant_intel/configurations/workbook05/source-admission-settings.json `
  experiments/granite_turboquant_intel/manifests/templates/workbook05 `
  experiments/granite_turboquant_intel/schemas/workbook05 `
  tests/testing/workbook05/test_source_admission_settings.py

git commit -m "test(workbook-05): define source-admission evidence contracts"
```

---

### Task 3: Enforce the `C:\wb05` Workspace Safety Boundary

**Files:**
- Create: `scripts/testing/workbook05/workspace_policy.py`
- Create: `scripts/testing/workbook05/Workbook05.SourceAdmission.psm1`
- Create: `tests/testing/workbook05/test_workspace_policy.py`
- Create: `tests/testing/workbook05/Invoke-SourceAdmissionModuleTests.ps1`

**Interfaces:**
- Produces: `evaluate_workspace_path`, `Test-Workbook05ExternalWorkspace`, and `Invoke-Workbook05RecordedCommand`.

- [ ] **Step 1: Write Python path-policy tests**

Cover exact `C:\wb05`, child paths, a different drive, `..`, UNC paths, device paths, repository paths, and case normalisation.

```python
def test_parent_traversal_is_rejected(self) -> None:
    decision = evaluate_workspace_path(PureWindowsPath(r"C:\wb05\source\..\..\escape"))
    self.assertFalse(decision.permitted)
    self.assertIn("parent traversal", " ".join(decision.reasons).lower())
```

- [ ] **Step 2: Write PowerShell tests**

The PowerShell test creates a temporary normal directory, file, and symbolic link or junction where permitted. It proves:

- an ordinary `C:\wb05` directory is accepted;
- a file at the workspace root is rejected;
- a reparse point is rejected;
- an existing unexpected child is not deleted;
- command records preserve stdout, stderr, exit code, timestamps, argv, and working directory.

- [ ] **Step 3: Implement the pure Python evaluator**

Reject any requested path whose normalised parts do not begin exactly with `C:\wb05`, including traversal before resolution.

- [ ] **Step 4: Implement the PowerShell module**

`Test-Workbook05ExternalWorkspace` must inspect `Get-Item -Force` and reject `[IO.FileAttributes]::ReparsePoint`. It may create only known campaign subdirectories after the root passes.

`Invoke-Workbook05RecordedCommand` must use `System.Diagnostics.Process`, redirect stdout/stderr to separate files, enforce timeout, kill the process tree on timeout, and emit a JSON command record. It must never invoke a shell string through `Invoke-Expression`.

- [ ] **Step 5: Run tests**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest `
    tests.testing.workbook05.test_workspace_policy -v

& '.\tests\testing\workbook05\Invoke-SourceAdmissionModuleTests.ps1'
```

- [ ] **Step 6: Commit**

```powershell
git add `
  scripts/testing/workbook05/workspace_policy.py `
  scripts/testing/workbook05/Workbook05.SourceAdmission.psm1 `
  tests/testing/workbook05/test_workspace_policy.py `
  tests/testing/workbook05/Invoke-SourceAdmissionModuleTests.ps1

git commit -m "feat(workbook-05): enforce the external source workspace boundary"
```

---

### Task 4: Verify or Clone Exact Git Sources and Recursive Submodules

**Files:**
- Create: `scripts/testing/workbook05/source_verification.py`
- Create: `tests/testing/workbook05/test_source_verification.py`

**Interfaces:**
- Consumes: settings and safe source directories.
- Produces: `SourceTreeResult` and `source-tree-report.json` per repository.

- [ ] **Step 1: Write failing tests using temporary local Git repositories**

Tests create tiny repositories with Git commands and cover:

- exact commit and origin accepted;
- dirty worktree rejected;
- mismatched origin rejected;
- existing non-repository directory rejected without deletion;
- unexpected `HEAD` rejected;
- recursive submodule status parsed into path, URL, and SHA;
- non-allowlisted repository rejected before network access;
- uppercase or short SHA rejected.

- [ ] **Step 2: Implement immutable source specification validation**

```python
def _validate_commit(value: str) -> None:
    """Reject moving refs and malformed commit identifiers."""

    if not re.fullmatch(r"[0-9a-f]{40}", value):
        raise ValueError(f"Commit must be a lowercase 40-character SHA: {value}")
```

- [ ] **Step 3: Implement safe clone/reuse behaviour**

For an absent directory:

```text
git clone --no-checkout <allowlisted-origin> <source-directory>
git -C <source-directory> fetch --no-tags origin <exact-sha>
git -C <source-directory> checkout --detach <exact-sha>
git -C <source-directory> submodule sync --recursive
git -C <source-directory> submodule update --init --recursive
```

For an existing directory, do not fetch/reset until origin, cleanliness, and current `HEAD` already match the controlled values. A mismatch produces a blocker report.

- [ ] **Step 4: Capture exact Git evidence**

Record:

```text
git version
git remote get-url origin
git rev-parse HEAD
git status --porcelain=v1 --untracked-files=all
git submodule status --recursive
git config --file .gitmodules --get-regexp ^submodule\..*\.(path|url)$
```

The report stores only metadata and hashes, not repository files.

- [ ] **Step 5: Run tests and commit**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest `
    tests.testing.workbook05.test_source_verification -v

git add scripts/testing/workbook05/source_verification.py `
        tests/testing/workbook05/test_source_verification.py
git commit -m "feat(workbook-05): verify pinned source trees and submodules"
```

---

### Task 5: Classify Route A and Route B Source Capabilities Without Overclaiming

**Files:**
- Create: `scripts/testing/workbook05/source_capability.py`
- Create: `tests/testing/workbook05/test_source_capability.py`
- Create: source fixtures listed in the locked structure.

**Interfaces:**
- Produces: `CapabilityFinding` rows and route capability reports.

- [ ] **Step 1: Write Route A tests**

Assert that fixtures classify:

- `CacheQuantAlgorithm`;
- `SCALAR` and `TURBO`;
- `key_cache_quant_alg` and `value_cache_quant_alg`;
- `ov::element::u3` and `ov::element::u4`;
- TurboQuant quantize/codec file presence.

The classification must be exactly `Present in source`.

- [ ] **Step 2: Write Route B tests**

Assert that fixtures capture:

- `TURBO_QUANT_3_QJL` and `TURBO_QUANT_4_QJL` declarations;
- the contradictory phrase `not yet supported`;
- `POLAR_QUANT_3` and `POLAR_QUANT_4`;
- `key_cache_codec` and `value_cache_codec`;
- QJL quantize/read-path tokens when present;
- Polar encode/decode paths;
- missing tokens as explicit findings.

A QJL enum plus `not yet supported` must never become an executable-support claim.

- [ ] **Step 3: Implement exact-file, exact-token inspection**

The inspector reads only paths declared in settings, hashes complete file bytes, records matched/missing/contradictory tokens, and stores bounded excerpts around matches. It rejects paths outside the verified source root.

- [ ] **Step 4: Run tests and commit**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest `
    tests.testing.workbook05.test_source_capability -v

git add `
  scripts/testing/workbook05/source_capability.py `
  tests/testing/workbook05/test_source_capability.py `
  tests/testing/workbook05/fixtures/source-admission

git commit -m "feat(workbook-05): classify codec source capabilities"
```

---

### Task 6: Reproduce Route B Blocker `RB-SRC-001`

**Files:**
- Create: `scripts/testing/workbook05/cmake_test_discovery.py`
- Create: `tests/testing/workbook05/test_cmake_test_discovery.py`
- Create: CMake and generated-metadata fixtures.

**Interfaces:**
- Produces: `CMakeDiscoveryDecision` and a strict CMake-discovery report.

- [ ] **Step 1: Write a failing omission test**

Create a fixture tree containing both:

```text
instances/x64/concat_sdp_turboq.cpp
x64/concat_sdp_turboq.cpp
instances/common/concat_sdp_turboq.cpp
common/concat_sdp_turboq.cpp
```

The test must prove the second `GLOB_RECURSE` assignment replaces the first result for both architecture and common variables.

```python
def test_repeated_assignments_omit_first_glob_results(self) -> None:
    decision = audit_target_per_test(
        CMAKE_FIXTURE.read_text(encoding="utf-8"),
        self.test_root,
        "concat_sdp_turboq.cpp",
        GENERATED_OMITTED.read_text(encoding="utf-8"),
    )
    self.assertTrue(decision.blocker_confirmed)
    self.assertIn(
        "instances/x64/concat_sdp_turboq.cpp",
        decision.omitted_sources,
    )
    self.assertIn(
        "instances/common/concat_sdp_turboq.cpp",
        decision.omitted_sources,
    )
```

- [ ] **Step 2: Write a disproval test**

A generated target that explicitly includes all intended sources must return `blocker_confirmed=False`, while still noting the repeated assignments as a source risk.

- [ ] **Step 3: Implement the focused auditor**

Do not attempt to interpret arbitrary CMake. Parse the exact `LIST_OF_TEST_ARCH_INSTANCES` and `LIST_OF_TEST_COMMON_INSTANCES` assignments, glob the declared directories, calculate assignment replacement in source order, then compare with captured generated target metadata.

- [ ] **Step 4: Run tests and commit**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest `
    tests.testing.workbook05.test_cmake_test_discovery -v

git add `
  scripts/testing/workbook05/cmake_test_discovery.py `
  tests/testing/workbook05/test_cmake_test_discovery.py `
  tests/testing/workbook05/fixtures/source-admission

git commit -m "test(workbook-05): reproduce the Route B test-discovery blocker"
```

---

### Task 7: Record and Evaluate the Route A Configure-Only Probe

**Files:**
- Create: `scripts/testing/workbook05/configure_probe.py`
- Create: `tests/testing/workbook05/test_configure_probe.py`
- Create: configure fixtures.

**Interfaces:**
- Produces: the exact Route A CMake command and `ConfigureProbeDecision`.

- [ ] **Step 1: Write command-construction tests**

The exact command must be:

```text
<absolute-cmake>
-S C:\wb05\source\route-a\openvino
-B C:\wb05\build\route-a-runtime
-G Visual Studio 17 2022
-A x64
-DENABLE_INTEL_GPU=OFF
```

The test rejects `--build`, `--install`, `--target`, Route B paths, GPU enabled, a moving source ref, or a non-fresh build directory.

- [ ] **Step 2: Write cache-evaluation tests**

A passing cache must prove:

```text
CMAKE_HOME_DIRECTORY=C:/wb05/source/route-a/openvino
CMAKE_GENERATOR=Visual Studio 17 2022
CMAKE_GENERATOR_PLATFORM=x64
ENABLE_INTEL_GPU=OFF
```

Wrong source, generator, platform, or GPU value fails even when CMake exits zero.

- [ ] **Step 3: Implement command and cache evaluation**

Use a simple CMake-cache parser that splits the first `=` after `NAME:TYPE`. Preserve the full command and cache hash in the report.

- [ ] **Step 4: Add a fake-CMake integration fixture**

`fake-cmake.py` writes a deterministic `CMakeCache.txt`, stdout, and exit code so orchestration tests can exercise the complete probe without compiling OpenVINO.

- [ ] **Step 5: Run tests and commit**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest `
    tests.testing.workbook05.test_configure_probe -v

git add `
  scripts/testing/workbook05/configure_probe.py `
  tests/testing/workbook05/test_configure_probe.py `
  tests/testing/workbook05/fixtures/source-admission

git commit -m "feat(workbook-05): add the Route A configure-only gate"
```

---

### Task 8: Assemble Truthful Route Decisions and the Phase Checkpoint

**Files:**
- Create: `scripts/testing/workbook05/source_admission_phase.py`
- Create: `tests/testing/workbook05/test_source_admission_phase.py`
- Modify: `scripts/testing/workbook05/source_admission.py`

**Interfaces:**
- Consumes: verified source, capability, CMake, configure, and measurement-control reports.
- Produces: route admission records, phase summary, checkpoint candidate, and hash manifest.

- [ ] **Step 1: Write three decision tests**

1. Route A all proofs pass, Route B blocker confirmed: Route A `Admitted`, Route B `Blocked`, phase `Passed with blocked experimental route` in human summary, checkpoint phase status `Passed` because the approved design permits Route A continuation.
2. Route A configure fails: Route A `Blocked`, phase `Blocked`.
3. Measurement controls invalid: neither route can be admitted and phase `Blocked`.

- [ ] **Step 2: Strengthen `evaluate_source_admission`**

Require every passed proof to have a safe relative `evidence_path`. Reject `Admitted` when `decision_reason` is empty or when a known blocker is `Open` or `Accepted risk`.

- [ ] **Step 3: Implement phase assembly**

The assembler copies controlled manifests into the evidence bundle, replaces proof statuses from actual reports, calculates decisions, writes summary JSON/Markdown, writes a checkpoint candidate using `record_step`, and creates the final hash manifest only after every file is closed.

Do not modify the repository’s controlled checkpoint from the self-hosted runner.

- [ ] **Step 4: Run tests and commit**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest `
    tests.testing.workbook05.test_source_admission_phase -v

git add `
  scripts/testing/workbook05/source_admission.py `
  scripts/testing/workbook05/source_admission_phase.py `
  tests/testing/workbook05/test_source_admission_phase.py

git commit -m "feat(workbook-05): calculate source-admission outcomes"
```

---

### Task 9: Build the Read-Only Windows Orchestrator

**Files:**
- Create: `scripts/testing/workbook05/Invoke-Workbook05SourceAdmission.ps1`
- Modify: `tests/testing/workbook05/Invoke-SourceAdmissionModuleTests.ps1`
- Create: `tests/testing/workbook05/test_source_workflow_contract.py`

**Interfaces:**
- Produces one immutable source-admission evidence directory under runner temp and uses `C:\wb05` only for external source/build workspace.

- [ ] **Step 1: Write static script-contract tests**

Tests assert that the orchestrator:

- imports `Workbook05.SourceAdmission.psm1`;
- validates preflight settings before source operations;
- uses exact settings paths;
- invokes Route A and Route B verifiers separately;
- captures measurement controls;
- runs Route A configure only after source gates;
- never contains `cmake --build`, `cmake --install`, model URLs, `git reset --hard`, `git clean`, `Remove-Item C:\wb05 -Recurse`, or `Invoke-Expression`;
- never runs a Route B configure command while blocker status is open.

- [ ] **Step 2: Implement orchestration order**

```text
1. Validate repository root and output directory.
2. Re-run the existing read-only machine preflight evaluation.
3. Validate `C:\wb05` workspace safety.
4. Create route-specific directories only.
5. Capture measurement controls.
6. Verify/clone Route A Runtime and GenAI.
7. Verify/clone Route B Runtime.
8. Capture pinned documents.
9. Inspect Route A and Route B source capabilities.
10. Audit Route B CMake test discovery.
11. Run Route A configure-only probe when prerequisites pass.
12. Assemble decisions, summary, checkpoint candidate, and hashes.
13. Exit non-zero only for infrastructure/integrity failure; a truthful source blocker remains valid evidence.
```

- [ ] **Step 3: Add beginner-readable comments to every block**

Explain why source operations are outside the repository, why Route B can block independently, why captured commands are not executed, and why the script does not compile.

- [ ] **Step 4: Run PowerShell and Python contract tests**

```powershell
& '.\tests\testing\workbook05\Invoke-SourceAdmissionModuleTests.ps1'
& 'C:\Program Files\Python312\python.exe' -m unittest `
    tests.testing.workbook05.test_source_workflow_contract -v
```

- [ ] **Step 5: Commit**

```powershell
git add `
  scripts/testing/workbook05/Invoke-Workbook05SourceAdmission.ps1 `
  tests/testing/workbook05/Invoke-SourceAdmissionModuleTests.ps1 `
  tests/testing/workbook05/test_source_workflow_contract.py

git commit -m "feat(workbook-05): orchestrate staged source admission"
```

---

### Task 10: Validate the Source-Admission Artifact as Untrusted Data

**Files:**
- Create: `scripts/testing/workbook05/source_admission_bundle_validation.py`
- Create: `tests/testing/workbook05/test_source_admission_bundle.py`

**Interfaces:**
- Produces: `validate_source_admission_bundle` and a CLI summary.

- [ ] **Step 1: Write valid and adversarial bundle tests**

Reject:

- changed file after hash creation;
- false `Admitted` status;
- wrong commit/origin;
- missing submodule evidence;
- Route B `Admitted` while `RB-SRC-001` is confirmed;
- Route A `Admitted` without a passing configure cache;
- invalid measurement-control hashes or IDs;
- a measured-run schema that permits passed rows without quality/raw-output fields;
- absolute or parent-traversing evidence paths;
- secrets;
- `.exe`, `.dll`, `.lib`, `.pdb`, `.zip`, `.7z`, `.tar`, `.gz`, `.whl`, `.onnx`, `.gguf`, and `.safetensors` payloads.

- [ ] **Step 2: Implement separate source-admission validation**

Reuse shared hash/schema/admission helpers without adding source-admission paths to the preflight validator’s required set. This keeps preflight and source-admission bundles independently understandable.

- [ ] **Step 3: Add CLI**

```powershell
python -m scripts.testing.workbook05.source_admission_bundle_validation `
    --bundle-root <downloaded-artifact> `
    --repository-root <checkout> `
    --summary <summary-path>
```

The CLI returns `0` only when no issues exist and always writes a readable Markdown summary.

- [ ] **Step 4: Run tests and commit**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest `
    tests.testing.workbook05.test_source_admission_bundle -v

git add `
  scripts/testing/workbook05/source_admission_bundle_validation.py `
  tests/testing/workbook05/test_source_admission_bundle.py

git commit -m "test(workbook-05): validate source-admission evidence bundles"
```

---

### Task 11: Add the Two-Job Source-Admission Workflow

**Files:**
- Create: `.github/workflows/workbook-05-source-admission.yml`
- Modify: `tests/testing/workbook05/test_source_workflow_contract.py`

**Interfaces:**
- Intel job uploads `workbook-05-source-admission-${{ github.run_id }}-${{ github.run_attempt }}`.
- Hosted job downloads and validates the exact artifact from the same attempt.

- [ ] **Step 1: Write failing workflow-contract assertions**

Require:

```yaml
permissions:
  contents: read
  actions: read
```

Require exact branch restrictions for `main` and `testing/workbook-05-source-admission`, same-repository PR only, runner labels `self-hosted`, `Windows`, `X64`, `workbook05`, `intel-target`, `persist-credentials: false`, pinned actions, `cancel-in-progress: false`, and no repository write commands.

- [ ] **Step 2: Create the Intel job**

Use a 180-minute timeout because recursive submodules and CMake generation can be slow. Sparse checkout must include workflows, specs/plans, testing docs, prompts, rubrics, Workbook 05 configurations/manifests/schemas, scripts, and tests.

Install `jsonschema` into runner temp using the pinned Python. Run all Workbook 05 tests and PowerShell tests before source collection. Upload evidence with `if: always()`.

- [ ] **Step 3: Create the hosted validation job**

Use `windows-latest`, Python 3.12, exact artifact name, every Workbook 05 Python test, and `source_admission_bundle_validation`. The hosted job treats the artifact as untrusted and never executes external source or captured commands.

- [ ] **Step 4: Run workflow-contract tests**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest `
    tests.testing.workbook05.test_source_workflow_contract -v
```

- [ ] **Step 5: Commit**

```powershell
git add `
  .github/workflows/workbook-05-source-admission.yml `
  tests/testing/workbook05/test_source_workflow_contract.py

git commit -m "ci(workbook-05): add staged source-admission evidence flow"
```

---

### Task 12: Add the Repository-Level Phase 1 Gate

**Files:**
- Create: `scripts/testing/Validate-Workbook05-SourceAdmission.ps1`
- Modify: `.github/workflows/workbook-05-source-admission.yml`

**Interfaces:**
- Produces one command for local and CI static verification.

- [ ] **Step 1: Write the validation script**

It must run, in order:

```text
All Workbook 05 Python tests
PowerShell source-admission module tests
Controlled testing workspace validator
OpenVINO codec-extension structural validator
Measurement-control capture and validation
Source-admission templates against schemas
git diff --check
```

It prints:

```text
WORKBOOK 05 SOURCE ADMISSION SCAFFOLD: PASS
No OpenVINO build, model download, inference, or benchmark was performed.
```

only after every command returns success.

- [ ] **Step 2: Call the gate in the Intel workflow before the live source job**

Do not replace focused tests; the gate is an additional integration layer.

- [ ] **Step 3: Run the gate locally or in an isolated Windows worker**

```powershell
& '.\scripts\testing\Validate-Workbook05-SourceAdmission.ps1'
```

Expected: PASS.

- [ ] **Step 4: Commit**

```powershell
git add `
  scripts/testing/Validate-Workbook05-SourceAdmission.ps1 `
  .github/workflows/workbook-05-source-admission.yml

git commit -m "test(workbook-05): add the source-admission scaffold gate"
```

---

### Task 13: Execute the Live Phase 1 Gate and Review the Evidence

**Files:**
- Modify only when a reproduced defect requires a test-first correction.
- Update: draft PR #46 body and discussion.

**Interfaces:**
- Produces: final source-admission artifact, independently validated route decisions, and a reviewed Phase 1 PR.

- [ ] **Step 1: Run the full static suite on the final branch head**

Record the exact head SHA and test counts. Stop on any failure.

- [ ] **Step 2: Trigger the source-admission workflow**

Keep the Intel laptop powered, plugged in, connected, awake, and running the service. Do not manually alter `C:\wb05` while the job runs.

- [ ] **Step 3: Inspect Intel logs and artifact**

Verify exact commits, origins, submodules, source classifications, Route B omission evidence, Route A configure cache, measurement-control hashes, route statuses, checkpoint candidate, and artifact digest.

- [ ] **Step 4: Inspect hosted validator logs**

Confirm it downloaded the exact matching artifact and passed hashes, schemas, admission truthfulness, measurement controls, unsafe-path checks, secret checks, and forbidden-payload checks.

- [ ] **Step 5: Re-run the WinUI Build and test workflow on the same final head**

Require successful restore/build, non-zero test discovery, all packaged tests passed, and test-result artifact upload. Record warnings without hiding them.

- [ ] **Step 6: Update PR #46 with complete context**

The PR must state:

- every implemented component;
- exact source commits and workspace boundary;
- the quality/metrics contract;
- Route A admission outcome;
- Route B admission/blocker outcome;
- exact workflow run IDs, artifact ID, and SHA-256;
- test counts;
- defects found and test-first corrections;
- explicit non-claims;
- next phase boundary.

- [ ] **Step 7: Final review**

Check unresolved threads, diff, workflow permissions, no committed external source/build payloads, no secrets, no false support claims, and no invented metric values.

- [ ] **Step 8: Mark ready only when evidence is complete**

Do not merge automatically. Present the verified integration decision to the project owner.

---

## Final Verification Command

```powershell
& '.\scripts\testing\Validate-Workbook05-SourceAdmission.ps1'
```

The final live completion evidence must additionally include a green Intel collection job, green hosted artifact-validation job, and green WinUI build/test workflow on the same branch head.

## Explicit Phase 1 Non-Claims

Even after this plan passes, it does not prove:

- OpenVINO Runtime compiles fully;
- OpenVINO GenAI compiles or links to the pinned Runtime;
- TurboQuant activates at runtime;
- QJL activates or improves quality;
- PolarQuant activates or is usable at acceptable speed;
- measured packed cache sizes;
- Granite model compatibility;
- memory, latency, throughput, perplexity, or quality results.

It proves only that the exact source boundary is controlled, Route A is ready or blocked for documented build, Route B is ready or blocked with evidence, and the future measurement contract cannot discard the project’s central quality question.
