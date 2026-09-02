# Workbook 05 Two-Route Preflight Scaffolding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the repository-controlled, read-only preflight and evidence scaffolding for the Workbook 05 two-route memory-frontier campaign without compiling OpenVINO, downloading models, or running inference.

**Architecture:** The implementation separates static campaign controls from machine-produced evidence. Versioned JSON schemas and manifests define Route A and Route B, a pinned-document capture component preserves exact source instructions without executing them, a Windows PowerShell collector observes the Intel runner, and a GitHub-hosted validation job treats the uploaded bundle as untrusted data. The self-hosted runner retains read-only repository permission throughout this plan.

**Tech Stack:** Python 3.12.10, Python standard library, `jsonschema==4.25.1`, Windows PowerShell 5.1, GitHub Actions, JSON Schema Draft 2020-12, CSV, Markdown, deterministic DOCX generation with the repository's existing `python-docx` tooling.

## Global Constraints

- Work only on `testing/workbook-05-two-route-memory-frontier` until the pull request is reviewed.
- The campaign ID is exactly `GTQ-WB05-MF-v1`.
- Route A ID is exactly `route-a-merged-openvino`; its existing evidence folder name remains `official-openvino`.
- Route B ID is exactly `route-b-experimental-qjl-polar`; its existing evidence folder name remains `custom-openvino-turboquant`.
- Route A Runtime provenance starts at OpenVINO PR `#35853`, merged commit `b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- Route B source candidate is OpenVINO PR `#35092`, fork commit `1827f6458d049de11c1a8203c793af67c99935dc`, base commit `7de5a4fbb178a1de43f6bc3cccc95ff656abed05`.
- Route B remains `Candidate` until all required source-admission proofs pass; the open test-source collection defect in `src/plugins/intel_cpu/tests/functional/cmake/target_per_test.cmake` is recorded as blocker `RB-SRC-001`.
- The current controlled WB-05 revision is `1.3`; this change creates revision `1.4`. Do not describe the next revision as `1.2`.
- Existing `OV-*` and `OVT-*` test IDs retain their meanings. Execution order is additional metadata, not a replacement ID system.
- The first implementation boundary does not build OpenVINO, build OpenVINO GenAI, download Granite models, run inference, calculate benchmark results, implement QJL/PolarQuant, or write to a results branch.
- The self-hosted workflow uses only `contents: read` and `actions: read`; it never receives `contents: write`, `pull-requests: write`, or a personal access token.
- Pull requests from forks cannot target the self-hosted runner.
- All third-party Actions are pinned to immutable commit SHAs.
- Raw evidence is immutable after capture. Corrections use a new attempt or a new processed file.
- Large models, third-party source trees, build outputs, executables, DLLs, authentication tokens, and private data are never committed.
- Preflight gates require runner `lenovo-pf4hmd0t-wb05`, computer `LENOVO-PF4HMD0T`, CPU name containing `i5-12450H`, Windows `X64`, service account `nt authority\network service`, at least `15.0 GiB` physical RAM, at least `6.0 GiB` available RAM, and at least `80.0 GiB` free on the system drive.
- Machine-wide Python is exactly `C:\Program Files\Python312\python.exe`, version `3.12.10`.
- Formal quality controls remain `GTQ-PROMPTS-v1` and `GTQ-QUALITY-RUBRIC-v1`; this plan validates their references but does not run them.
- Allowed campaign result states are exactly `Passed`, `Failed`, `Blocked`, `Skipped by frontier`, and `Not applicable`.
- Each production code block must include beginner-readable comments explaining purpose and non-obvious decisions.
- Each task ends in a focused commit and must pass its own tests before the next task begins.

---

## Locked File Structure

The implementation creates or changes the following units. Each file has one primary responsibility.

```text
.github/workflows/
└── workbook-05-preflight.yml

docs/superpowers/specs/
└── 2026-08-03-workbook-05-two-route-memory-frontier-design.md

docs/testing/
├── Decision-Log.md
├── Workbook-Revision-Register.csv
├── Workbook-05-Memory-Frontier-Execution-Index-v1.csv
└── workbooks/
    ├── Controlled-Workbook-Manifest.csv
    └── text-templates/
        └── 05_Custom_OpenVINO_TurboQuant_Controlled_Retest_Workbook_v1.md

experiments/granite_turboquant_intel/
├── configurations/workbook05/
│   ├── preflight-settings.json
│   └── pinned-document-sources.json
├── manifests/campaigns/GTQ-WB05-MF-v1/
│   ├── campaign-manifest.json
│   ├── checkpoint.json
│   ├── route-a-source-admission.json
│   └── route-b-source-admission.json
├── manifests/templates/workbook05/
│   ├── campaign-manifest-template.json
│   ├── checkpoint-template.json
│   ├── documented-command-manifest-template.json
│   ├── preflight-report-template.json
│   └── source-admission-template.json
└── schemas/workbook05/
    ├── campaign-manifest.schema.json
    ├── checkpoint.schema.json
    ├── documented-command-manifest.schema.json
    ├── preflight-report.schema.json
    └── source-admission.schema.json

scripts/testing/workbook05/
├── __init__.py
├── bundle_validation.py
├── capture_documented_commands.py
├── checkpoint.py
├── generate_execution_index.py
├── hash_manifest.py
├── requirements.txt
├── schema_validation.py
├── source_admission.py
├── Invoke-Workbook05Preflight.ps1
└── Workbook05.Preflight.psm1

scripts/testing/
└── Validate-Workbook05-MemoryFrontier.ps1

tests/testing/workbook05/
├── fixtures/
│   ├── build-guide.md
│   ├── invalid-source-admission.json
│   ├── powercfg-ac-never.txt
│   └── powercfg-ac-timeout.txt
├── Invoke-PreflightModuleTests.ps1
├── test_bundle_validation.py
├── test_checkpoint.py
├── test_command_capture.py
├── test_control_documents.py
├── test_execution_index.py
├── test_schema_validation.py
├── test_source_admission.py
└── test_workflow_contract.py
```

## Stable Interfaces Between Tasks

The following names are contracts. Do not rename them in individual tasks.

```python
# scripts/testing/workbook05/schema_validation.py
@dataclass(frozen=True)
class ValidationIssue:
    json_path: str
    message: str


def validate_json_file(instance_path: Path, schema_path: Path) -> list[ValidationIssue]: ...

def assert_valid_json_file(instance_path: Path, schema_path: Path) -> None: ...


# scripts/testing/workbook05/source_admission.py
@dataclass(frozen=True)
class AdmissionDecision:
    permitted: bool
    calculated_status: str
    reasons: tuple[str, ...]


def evaluate_source_admission(record: Mapping[str, Any]) -> AdmissionDecision: ...


# scripts/testing/workbook05/capture_documented_commands.py
@dataclass(frozen=True)
class CommandBlock:
    command_id: str
    document_id: str
    heading: str
    language: str
    start_line: int
    end_line: int
    verbatim_text: str
    sha256: str


def extract_fenced_commands(document_id: str, markdown_text: str) -> list[CommandBlock]: ...

def capture_documents(config_path: Path, output_directory: Path) -> list[Path]: ...


# scripts/testing/workbook05/checkpoint.py
class CheckpointConflictError(RuntimeError): ...

def load_checkpoint(path: Path) -> dict[str, Any]: ...

def record_step(
    path: Path,
    *,
    expected_generation: int,
    step_id: str,
    status: str,
    evidence_sha256: str,
) -> dict[str, Any]: ...

def first_incomplete_step(checkpoint: Mapping[str, Any]) -> str | None: ...


# scripts/testing/workbook05/hash_manifest.py
def write_hash_manifest(root: Path, destination: Path) -> list[tuple[str, str]]: ...

def verify_hash_manifest(root: Path, manifest_path: Path) -> list[str]: ...


# scripts/testing/workbook05/bundle_validation.py
@dataclass(frozen=True)
class BundleIssue:
    code: str
    path: str
    message: str


def validate_preflight_bundle(bundle_root: Path, repository_root: Path) -> list[BundleIssue]: ...
```

PowerShell contracts:

```powershell
# scripts/testing/workbook05/Workbook05.Preflight.psm1
Get-Workbook05PreflightObservation -RepositoryRoot <path>
Test-Workbook05PreflightObservation -Observation <object> -Settings <object>
Export-Workbook05PreflightEvidence -Observation <object> -Evaluation <object> -OutputDirectory <path>
Get-Workbook05AcSleepTimeoutSeconds -PowerCfgOutput <string>
```

---

### Task 1: Reconcile the Approved Specification with the Current Repository Baseline

**Files:**
- Create: `tests/testing/workbook05/test_control_documents.py`
- Modify: `docs/superpowers/specs/2026-08-03-workbook-05-two-route-memory-frontier-design.md`
- Modify: `docs/testing/Decision-Log.md`

**Interfaces:**
- Consumes: current WB-05 revision `1.3` from `docs/testing/workbooks/Controlled-Workbook-Manifest.csv`.
- Produces: an approved design that consistently names revision `1.4` and decision `TD-013`.

- [ ] **Step 1: Write the failing control-document test**

```python
from __future__ import annotations

import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]


class ControlDocumentTests(unittest.TestCase):
    def test_spec_names_revision_1_4_as_the_next_wb05_revision(self) -> None:
        spec = (
            REPOSITORY_ROOT
            / "docs/superpowers/specs/2026-08-03-workbook-05-two-route-memory-frontier-design.md"
        ).read_text(encoding="utf-8")

        self.assertIn("Workbook 05 v1.3 remains unchanged", spec)
        self.assertIn("A new controlled v1.4 revision will", spec)
        self.assertNotIn("A new controlled v1.2 revision will", spec)

    def test_decision_log_records_the_two_route_memory_frontier_decision(self) -> None:
        decision_log = (
            REPOSITORY_ROOT / "docs/testing/Decision-Log.md"
        ).read_text(encoding="utf-8")

        self.assertIn("| TD-013 | 2026-08-03 |", decision_log)
        self.assertIn("two separately labelled OpenVINO routes", decision_log)
        self.assertIn("lowest verified KV storage first", decision_log)


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run the test and confirm the baseline contradiction is detected**

Run:

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest tests.testing.workbook05.test_control_documents -v
```

Expected: the revision assertions fail because the approved specification still says v1.2, and the decision-log assertion fails because `TD-013` does not exist.

- [ ] **Step 3: Correct only the revision wording in the approved specification**

Change the control section to exactly:

```markdown
## 14. Workbook and traceability revision

Workbook 05 v1.3 remains unchanged.

A new controlled v1.4 revision will:

- retain all existing test IDs;
- add route, phase and memory rank;
- add expected/verified bytes;
- add frontier and skip status;
- add run/evidence references;
- correct QJL/Polar sizing only after conformance evidence;
- update the append-only revision register;
- add a decision-log entry for the two-route, memory-first order;
- regenerate controlled outputs and hashes.
```

Also change the first implementation boundary from `v1.2 scaffolding` to `v1.4 scaffolding`. Do not alter the already approved route, safety, metric, quality, or security rules.

- [ ] **Step 4: Append decision `TD-013`**

Append one Markdown table row exactly:

```markdown
| TD-013 | 2026-08-03 | Execute Workbook 05 as two separately labelled OpenVINO routes and discover the target-laptop frontier from lowest verified KV storage first. | Merged OpenVINO exposes a different support boundary from the experimental QJL/Polar source, while historical row order is not the safest order for a 16 GB laptop. | `docs/superpowers/specs/2026-08-03-workbook-05-two-route-memory-frontier-design.md`; OpenVINO PRs #35853 and #35092 | Route A and Route B receive separate source-admission gates; existing IDs remain stable; context increases only after the preceding point is stable. | A pinned source changes the exposed codecs, measured storage invalidates the ordering, or the target hardware changes. | Accepted |
```

- [ ] **Step 5: Run the control-document test again**

Run:

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest tests.testing.workbook05.test_control_documents -v
```

Expected: `2 tests` pass.

- [ ] **Step 6: Commit the reconciled control baseline**

```powershell
git add docs/superpowers/specs/2026-08-03-workbook-05-two-route-memory-frontier-design.md docs/testing/Decision-Log.md tests/testing/workbook05/test_control_documents.py
git commit -m "docs(workbook-05): reconcile the v1.4 campaign baseline"
```

---

### Task 2: Add Versioned Workbook 05 JSON Schemas and a Shared Validator

**Files:**
- Create: `scripts/testing/workbook05/__init__.py`
- Create: `scripts/testing/workbook05/requirements.txt`
- Create: `scripts/testing/workbook05/schema_validation.py`
- Create: `experiments/granite_turboquant_intel/schemas/workbook05/campaign-manifest.schema.json`
- Create: `experiments/granite_turboquant_intel/schemas/workbook05/source-admission.schema.json`
- Create: `experiments/granite_turboquant_intel/schemas/workbook05/documented-command-manifest.schema.json`
- Create: `experiments/granite_turboquant_intel/schemas/workbook05/checkpoint.schema.json`
- Create: `experiments/granite_turboquant_intel/schemas/workbook05/preflight-report.schema.json`
- Create: `experiments/granite_turboquant_intel/manifests/templates/workbook05/campaign-manifest-template.json`
- Create: `experiments/granite_turboquant_intel/manifests/templates/workbook05/source-admission-template.json`
- Create: `experiments/granite_turboquant_intel/manifests/templates/workbook05/documented-command-manifest-template.json`
- Create: `experiments/granite_turboquant_intel/manifests/templates/workbook05/checkpoint-template.json`
- Create: `experiments/granite_turboquant_intel/manifests/templates/workbook05/preflight-report-template.json`
- Create: `tests/testing/workbook05/test_schema_validation.py`

**Interfaces:**
- Consumes: JSON files under `experiments/granite_turboquant_intel/`.
- Produces: `validate_json_file()` and `assert_valid_json_file()` for all subsequent tasks.

- [ ] **Step 1: Pin the only new Python dependency**

Create `scripts/testing/workbook05/requirements.txt` containing exactly:

```text
jsonschema==4.25.1
```

Keep this separate from `scripts/testing/requirements.txt`, which controls deterministic workbook generation.

- [ ] **Step 2: Write the failing validator tests**

```python
from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.schema_validation import (
    assert_valid_json_file,
    validate_json_file,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
SCHEMA_ROOT = (
    REPOSITORY_ROOT
    / "experiments/granite_turboquant_intel/schemas/workbook05"
)


class SchemaValidationTests(unittest.TestCase):
    def test_valid_source_admission_template_passes(self) -> None:
        instance = (
            REPOSITORY_ROOT
            / "experiments/granite_turboquant_intel/manifests/templates/workbook05/source-admission-template.json"
        )
        issues = validate_json_file(
            instance,
            SCHEMA_ROOT / "source-admission.schema.json",
        )
        self.assertEqual([], issues)

    def test_unknown_route_is_reported_with_a_json_path(self) -> None:
        template_path = (
            REPOSITORY_ROOT
            / "experiments/granite_turboquant_intel/manifests/templates/workbook05/source-admission-template.json"
        )
        record = json.loads(template_path.read_text(encoding="utf-8"))
        record["route_id"] = "route-c-unknown"

        with tempfile.TemporaryDirectory() as temporary_directory:
            instance = Path(temporary_directory) / "invalid.json"
            instance.write_text(json.dumps(record), encoding="utf-8")
            issues = validate_json_file(
                instance,
                SCHEMA_ROOT / "source-admission.schema.json",
            )

        self.assertEqual(1, len(issues))
        self.assertEqual("$.route_id", issues[0].json_path)
        self.assertIn("is not one of", issues[0].message)

    def test_assert_valid_raises_one_readable_exception(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            instance = Path(temporary_directory) / "invalid.json"
            instance.write_text("{}", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "campaign_id"):
                assert_valid_json_file(
                    instance,
                    SCHEMA_ROOT / "campaign-manifest.schema.json",
                )


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 3: Run the tests and verify they fail because the package and schemas do not exist**

```powershell
& 'C:\Program Files\Python312\python.exe' -m pip install -r scripts/testing/workbook05/requirements.txt
& 'C:\Program Files\Python312\python.exe' -m unittest tests.testing.workbook05.test_schema_validation -v
```

Expected: import or file-not-found failure.

- [ ] **Step 4: Implement the shared validator**

```python
"""Validate Workbook 05 JSON controls against versioned JSON Schemas."""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from jsonschema import Draft202012Validator


@dataclass(frozen=True)
class ValidationIssue:
    """One deterministic, display-ready schema validation problem."""

    json_path: str
    message: str


def _json_path(parts: list[object]) -> str:
    """Convert jsonschema's path deque into a beginner-readable JSON path."""

    value = "$"
    for part in parts:
        value += f"[{part}]" if isinstance(part, int) else f".{part}"
    return value


def validate_json_file(
    instance_path: Path,
    schema_path: Path,
) -> list[ValidationIssue]:
    """Return every schema issue in stable path/message order."""

    instance = json.loads(instance_path.read_text(encoding="utf-8-sig"))
    schema = json.loads(schema_path.read_text(encoding="utf-8-sig"))
    validator = Draft202012Validator(schema)
    errors = sorted(
        validator.iter_errors(instance),
        key=lambda error: (list(error.absolute_path), error.message),
    )
    return [
        ValidationIssue(
            json_path=_json_path(list(error.absolute_path)),
            message=error.message,
        )
        for error in errors
    ]


def assert_valid_json_file(instance_path: Path, schema_path: Path) -> None:
    """Raise one exception containing all problems when validation fails."""

    issues = validate_json_file(instance_path, schema_path)
    if not issues:
        return
    details = "\n".join(
        f"- {issue.json_path}: {issue.message}" for issue in issues
    )
    raise ValueError(
        f"JSON validation failed for {instance_path}:\n{details}"
    )
```

- [ ] **Step 5: Create the five Draft 2020-12 schemas**

Every schema must include:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false
}
```

Use these exact required contracts:

- `campaign-manifest.schema.json`: `schema_version`, `campaign_id`, `workbook_id`, `workbook_revision`, `status`, `target`, `routes`, `controls`, and `created_at_utc`. `campaign_id` is `GTQ-WB05-MF-v1`; `workbook_id` is `WB-05`; `workbook_revision` is `1.4`.
- `source-admission.schema.json`: `schema_version`, `campaign_id`, `route_id`, `support_boundary`, `source`, `genai_candidate`, `admission_status`, `proofs`, `known_blockers`, and `decision_reason`. Route IDs and statuses use the exact enums from Global Constraints.
- `documented-command-manifest.schema.json`: `schema_version`, `campaign_id`, `route_id`, `execution_allowed`, `documents`, and `commands`; `execution_allowed` is constrained with `"const": false`.
- `checkpoint.schema.json`: `schema_version`, `campaign_id`, `generation`, `phase_order`, `steps`, `updated_at_utc`; each step status is one of `Not started`, `In progress`, `Passed`, `Failed`, or `Blocked`.
- `preflight-report.schema.json`: `schema_version`, `campaign_id`, `generated_at_utc`, `observation`, `checks`, and `overall_status`; `overall_status` is `Passed` or `Failed`.

A source object requires a GitHub HTTPS URL, a 40-character lowercase hexadecimal commit, a repository full name, and a source-role string. A proof requires `proof_id`, `required`, `status`, and `evidence_path`. A blocker requires `blocker_id`, `severity`, `status`, `path`, `description`, and `evidence_url`.

- [ ] **Step 6: Create valid neutral templates**

The templates are structurally valid examples, not campaign evidence. Use these neutral values:

```json
{
  "schema_version": "1.0",
  "campaign_id": "GTQ-WB05-MF-v1",
  "route_id": "route-a-merged-openvino",
  "support_boundary": "merged",
  "source": {
    "repository_full_name": "openvinotoolkit/openvino",
    "repository_url": "https://github.com/openvinotoolkit/openvino.git",
    "source_role": "runtime",
    "commit": "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
    "base_commit": "816d60598f9b37135cf7d9ee7ca11b75082896b2",
    "pull_request_url": "https://github.com/openvinotoolkit/openvino/pull/35853",
    "pull_request_state": "merged"
  },
  "genai_candidate": null,
  "admission_status": "Candidate",
  "proofs": [],
  "known_blockers": [],
  "decision_reason": "Template example; no source-admission decision is implied."
}
```

Create equivalent valid neutral objects for the other four templates. Use an empty list only where the schema permits it; use explicit `Not started` or `Candidate` states instead of ambiguous blank strings.

- [ ] **Step 7: Run the schema tests**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest tests.testing.workbook05.test_schema_validation -v
```

Expected: `3 tests` pass.

- [ ] **Step 8: Commit the schema boundary**

```powershell
git add scripts/testing/workbook05 experiments/granite_turboquant_intel/schemas/workbook05 experiments/granite_turboquant_intel/manifests/templates/workbook05 tests/testing/workbook05/test_schema_validation.py
git commit -m "test(workbook-05): add versioned preflight schemas"
```

---

### Task 3: Record and Enforce Route A and Route B Source Admission

**Files:**
- Create: `scripts/testing/workbook05/source_admission.py`
- Create: `experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1/campaign-manifest.json`
- Create: `experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1/route-a-source-admission.json`
- Create: `experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1/route-b-source-admission.json`
- Create: `tests/testing/workbook05/test_source_admission.py`

**Interfaces:**
- Consumes: source-admission schema and route manifests.
- Produces: `evaluate_source_admission()` and two honest `Candidate` records.

- [ ] **Step 1: Write the failing source-admission tests**

```python
from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.source_admission import (
    evaluate_source_admission,
)


ROOT = Path(__file__).resolve().parents[3]
CAMPAIGN_ROOT = (
    ROOT
    / "experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1"
)


class SourceAdmissionTests(unittest.TestCase):
    def test_route_b_is_candidate_while_the_open_blocker_exists(self) -> None:
        record = json.loads(
            (CAMPAIGN_ROOT / "route-b-source-admission.json").read_text(
                encoding="utf-8"
            )
        )
        decision = evaluate_source_admission(record)

        self.assertFalse(decision.permitted)
        self.assertEqual("Candidate", decision.calculated_status)
        self.assertTrue(any("RB-SRC-001" in reason for reason in decision.reasons))

    def test_admitted_status_requires_every_required_proof(self) -> None:
        record = json.loads(
            (CAMPAIGN_ROOT / "route-a-source-admission.json").read_text(
                encoding="utf-8"
            )
        )
        record["admission_status"] = "Admitted"
        decision = evaluate_source_admission(record)

        self.assertFalse(decision.permitted)
        self.assertTrue(any("required proof" in reason for reason in decision.reasons))

    def test_record_can_be_admitted_after_proofs_and_blockers_pass(self) -> None:
        record = json.loads(
            (CAMPAIGN_ROOT / "route-b-source-admission.json").read_text(
                encoding="utf-8"
            )
        )
        for proof in record["proofs"]:
            if proof["required"]:
                proof["status"] = "Passed"
                proof["evidence_path"] = "evidence/proof.json"
        for blocker in record["known_blockers"]:
            blocker["status"] = "Resolved"
        record["admission_status"] = "Admitted"

        decision = evaluate_source_admission(record)
        self.assertTrue(decision.permitted)
        self.assertEqual("Admitted", decision.calculated_status)
        self.assertEqual((), decision.reasons)


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run the tests and verify the admission component is missing**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest tests.testing.workbook05.test_source_admission -v
```

Expected: import or file-not-found failure.

- [ ] **Step 3: Implement deterministic admission evaluation**

```python
"""Apply the non-negotiable source-admission gate for Workbook 05."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Mapping


@dataclass(frozen=True)
class AdmissionDecision:
    permitted: bool
    calculated_status: str
    reasons: tuple[str, ...]


def evaluate_source_admission(
    record: Mapping[str, Any],
) -> AdmissionDecision:
    """Permit admission only when required proofs pass and blockers are closed."""

    reasons: list[str] = []
    for proof in record["proofs"]:
        if proof["required"] and proof["status"] != "Passed":
            reasons.append(
                f"required proof {proof['proof_id']} is {proof['status']}"
            )
        if proof["status"] == "Passed" and not proof["evidence_path"]:
            reasons.append(
                f"passed proof {proof['proof_id']} has no evidence_path"
            )

    for blocker in record["known_blockers"]:
        if blocker["status"] != "Resolved":
            reasons.append(
                f"open blocker {blocker['blocker_id']}: {blocker['description']}"
            )

    requested_status = record["admission_status"]
    if not reasons and requested_status == "Admitted":
        return AdmissionDecision(True, "Admitted", ())
    if requested_status == "Blocked":
        return AdmissionDecision(False, "Blocked", tuple(reasons))
    return AdmissionDecision(False, "Candidate", tuple(reasons))
```

- [ ] **Step 4: Create the Route A source-admission record**

Record Runtime provenance as merged OpenVINO PR #35853 and commit `b9a1f201c109e0bed74763934f79483cf6c4cbf4`. Record OpenVINO GenAI commit `05e5c7670b597746f858946974d11f38e3baf42f` only as a compatibility candidate. Required proof IDs are:

```text
RA-P01-document-capture
RA-P02-runtime-commit-reachable
RA-P03-genai-commit-reachable
RA-P04-documented-build
RA-P05-standard-cache-baseline
RA-P06-property-visibility
RA-P07-turbo-u3-activation
RA-P08-turbo-u4-activation
RA-P09-packed-allocation
RA-P10-no-silent-fallback
```

All proof statuses begin as `Pending`, evidence paths begin as empty strings, and admission status is `Candidate`. The decision reason states that merged Runtime provenance is known but the Runtime/GenAI pair has not been built and activated on the target laptop.

- [ ] **Step 5: Create the Route B source-admission record**

Record:

```json
{
  "repository_full_name": "EgorDuplensky/openvino",
  "repository_url": "https://github.com/EgorDuplensky/openvino.git",
  "source_role": "experimental-runtime",
  "commit": "1827f6458d049de11c1a8203c793af67c99935dc",
  "base_commit": "7de5a4fbb178a1de43f6bc3cccc95ff656abed05",
  "pull_request_url": "https://github.com/openvinotoolkit/openvino/pull/35092",
  "pull_request_state": "open"
}
```

Required proof IDs are:

```text
RB-P01-document-capture
RB-P02-source-commit-reachable
RB-P03-documented-build
RB-P04-qjl-selectable
RB-P05-polar-selectable
RB-P06-encode-decode-present
RB-P07-packed-size-conformance
RB-P08-repository-tests
RB-P09-independent-kv-dispatch
RB-P10-no-silent-fallback
```

Record blocker `RB-SRC-001` exactly:

```json
{
  "blocker_id": "RB-SRC-001",
  "severity": "Blocker",
  "status": "Open",
  "path": "src/plugins/intel_cpu/tests/functional/cmake/target_per_test.cmake",
  "description": "Repeated GLOB_RECURSE assignments overwrite earlier test-source lists, so the current functional-test target may omit required instances.",
  "evidence_url": "https://github.com/openvinotoolkit/openvino/pull/35092#discussion_r3042885748"
}
```

Keep admission status `Candidate`; do not mark any QJL or Polar capability as passed.

- [ ] **Step 6: Create the campaign manifest**

The campaign manifest references both route records, WB-05 revision `1.4`, target machine identity, frozen prompt/rubric IDs, allowed result states, and these phase IDs in order:

```text
phase-0-preflight
phase-1-source-admission
phase-2-documented-build
phase-3-conformance
phase-4-capability-sweep
phase-5-granite-3b-frontier
phase-6-granite-3b-formal
phase-7-asymmetric-cross-family
phase-8-granite-8b-frontier
phase-9-ablations-repeatability
phase-10-conclusion
```

Set campaign status to `Not started`.

- [ ] **Step 7: Validate schemas and admission rules**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest tests.testing.workbook05.test_schema_validation tests.testing.workbook05.test_source_admission -v
```

Expected: `6 tests` pass.

- [ ] **Step 8: Commit source admission**

```powershell
git add scripts/testing/workbook05/source_admission.py experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1 tests/testing/workbook05/test_source_admission.py
git commit -m "docs(workbook-05): lock two-route source candidates"
```

---

### Task 4: Capture Exact Pinned Build Documents and Command Blocks Without Executing Them

**Files:**
- Create: `experiments/granite_turboquant_intel/configurations/workbook05/pinned-document-sources.json`
- Create: `scripts/testing/workbook05/capture_documented_commands.py`
- Create: `tests/testing/workbook05/fixtures/build-guide.md`
- Create: `tests/testing/workbook05/test_command_capture.py`

**Interfaces:**
- Consumes: allowlisted GitHub repository, 40-character commit, and path records.
- Produces: immutable document snapshots plus one validated documented-command manifest per route.

- [ ] **Step 1: Create the command-extraction fixture**

```markdown
# Build guide

Introductory text that is not a command.

## Windows build

```powershell
git clone --recursive https://example.invalid/repository.git
cmake -S . -B build
```

## Verification

```text
cmake --build build --config Release
```
```

- [ ] **Step 2: Write failing extraction and security tests**

```python
from __future__ import annotations

import hashlib
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.capture_documented_commands import (
    build_raw_github_url,
    extract_fenced_commands,
    validate_document_spec,
)


ROOT = Path(__file__).resolve().parents[3]


class CommandCaptureTests(unittest.TestCase):
    def test_fenced_commands_preserve_text_heading_and_line_numbers(self) -> None:
        fixture = (
            ROOT / "tests/testing/workbook05/fixtures/build-guide.md"
        ).read_text(encoding="utf-8")
        commands = extract_fenced_commands("DOC-1", fixture)

        self.assertEqual(2, len(commands))
        self.assertEqual("Windows build", commands[0].heading)
        self.assertEqual("powershell", commands[0].language)
        self.assertEqual(
            "git clone --recursive https://example.invalid/repository.git\n"
            "cmake -S . -B build",
            commands[0].verbatim_text,
        )
        self.assertEqual(
            hashlib.sha256(commands[0].verbatim_text.encode("utf-8")).hexdigest(),
            commands[0].sha256,
        )
        self.assertLess(commands[0].start_line, commands[0].end_line)

    def test_raw_url_contains_the_exact_commit(self) -> None:
        url = build_raw_github_url(
            "openvinotoolkit/openvino",
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            "docs/dev/build_windows.md",
        )
        self.assertEqual(
            "https://raw.githubusercontent.com/openvinotoolkit/openvino/"
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4/"
            "docs/dev/build_windows.md",
            url,
        )

    def test_path_traversal_and_unpinned_refs_are_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "40-character"):
            validate_document_spec(
                {
                    "repository_full_name": "openvinotoolkit/openvino",
                    "commit": "master",
                    "path": "README.md",
                }
            )
        with self.assertRaisesRegex(ValueError, "repository-relative"):
            validate_document_spec(
                {
                    "repository_full_name": "openvinotoolkit/openvino",
                    "commit": "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
                    "path": "../secret.txt",
                }
            )


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 3: Run the tests and verify the capture component is absent**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest tests.testing.workbook05.test_command_capture -v
```

Expected: import failure.

- [ ] **Step 4: Implement safe pinned-document capture**

The implementation must:

1. accept only repository names listed in `pinned-document-sources.json`;
2. require lowercase 40-character commit SHAs;
3. reject absolute paths, `..`, backslashes, and URL fragments in repository paths;
4. construct only `https://raw.githubusercontent.com/<owner>/<repo>/<commit>/<path>` URLs;
5. use a 30-second timeout and a 2 MiB maximum document size;
6. decode UTF-8 strictly;
7. save the untouched document bytes before parsing;
8. calculate SHA-256 for each document and each fenced block;
9. preserve code-fence language, nearest preceding Markdown heading, and 1-based line range;
10. write `execution_allowed: false` into each command manifest;
11. never invoke a captured command.

Use this heading-aware extraction core:

```python
def extract_fenced_commands(
    document_id: str,
    markdown_text: str,
) -> list[CommandBlock]:
    """Extract fenced blocks verbatim without interpreting shell syntax."""

    lines = markdown_text.splitlines()
    current_heading = "Document root"
    commands: list[CommandBlock] = []
    index = 0
    while index < len(lines):
        line = lines[index]
        if line.startswith("#"):
            current_heading = line.lstrip("#").strip() or "Document root"
            index += 1
            continue
        if not line.startswith("```"):
            index += 1
            continue

        language = line[3:].strip().lower()
        opening_line = index + 1
        index += 1
        content: list[str] = []
        while index < len(lines) and not lines[index].startswith("```"):
            content.append(lines[index])
            index += 1
        if index >= len(lines):
            raise ValueError(
                f"Unclosed code fence in {document_id} at line {opening_line}"
            )

        verbatim = "\n".join(content)
        command_number = len(commands) + 1
        commands.append(
            CommandBlock(
                command_id=f"{document_id}-C{command_number:03d}",
                document_id=document_id,
                heading=current_heading,
                language=language,
                start_line=opening_line + 1,
                end_line=index,
                verbatim_text=verbatim,
                sha256=hashlib.sha256(verbatim.encode("utf-8")).hexdigest(),
            )
        )
        index += 1
    return commands
```

- [ ] **Step 5: Add exact document-source records**

`pinned-document-sources.json` contains these records:

```text
RA-OV-README       openvinotoolkit/openvino        b9a1f201... README.md
RA-OV-WINDOWS      openvinotoolkit/openvino        b9a1f201... docs/dev/build_windows.md
RA-GENAI-README    openvinotoolkit/openvino.genai  05e5c767... README.md
RA-GENAI-BUILD     openvinotoolkit/openvino.genai  05e5c767... src/docs/BUILD.md
RB-OV-README       EgorDuplensky/openvino          1827f645... README.md
RB-OV-WINDOWS      EgorDuplensky/openvino          1827f645... docs/dev/build_windows.md
RB-CODECS          EgorDuplensky/openvino          1827f645... KV_CACHE_CODECS.md
RB-POLAR-DESIGN    EgorDuplensky/openvino          1827f645... polarquant_implementation.md
```

Every record includes `document_id`, `route_id`, `repository_full_name`, `commit`, `path`, and `maximum_bytes: 2097152`.

- [ ] **Step 6: Run the command-capture tests**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest tests.testing.workbook05.test_command_capture -v
```

Expected: `3 tests` pass.

- [ ] **Step 7: Commit exact-document capture**

```powershell
git add experiments/granite_turboquant_intel/configurations/workbook05/pinned-document-sources.json scripts/testing/workbook05/capture_documented_commands.py tests/testing/workbook05
git commit -m "feat(workbook-05): capture pinned source instructions"
```

---

### Task 5: Add Atomic Checkpoint and Resume Controls

**Files:**
- Create: `experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1/checkpoint.json`
- Create: `scripts/testing/workbook05/checkpoint.py`
- Create: `tests/testing/workbook05/test_checkpoint.py`

**Interfaces:**
- Consumes: checkpoint schema and fixed campaign phase order.
- Produces: atomic `record_step()` updates and deterministic `first_incomplete_step()` results.

- [ ] **Step 1: Write failing checkpoint tests**

```python
from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.checkpoint import (
    CheckpointConflictError,
    first_incomplete_step,
    load_checkpoint,
    record_step,
)


class CheckpointTests(unittest.TestCase):
    def _create_checkpoint(self, directory: Path) -> Path:
        path = directory / "checkpoint.json"
        path.write_text(
            json.dumps(
                {
                    "schema_version": "1.0",
                    "campaign_id": "GTQ-WB05-MF-v1",
                    "generation": 0,
                    "phase_order": [
                        "phase-0-preflight",
                        "phase-1-source-admission",
                    ],
                    "steps": [
                        {
                            "step_id": "phase-0-preflight",
                            "status": "Not started",
                            "evidence_sha256": "",
                        },
                        {
                            "step_id": "phase-1-source-admission",
                            "status": "Not started",
                            "evidence_sha256": "",
                        },
                    ],
                    "updated_at_utc": "2026-08-03T00:00:00Z",
                }
            ),
            encoding="utf-8",
        )
        return path

    def test_record_step_is_atomic_and_increments_generation(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            path = self._create_checkpoint(Path(temporary_directory))
            updated = record_step(
                path,
                expected_generation=0,
                step_id="phase-0-preflight",
                status="Passed",
                evidence_sha256="a" * 64,
            )
            persisted = load_checkpoint(path)

        self.assertEqual(1, updated["generation"])
        self.assertEqual(updated, persisted)
        self.assertFalse(path.with_suffix(".json.tmp").exists())

    def test_stale_generation_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            path = self._create_checkpoint(Path(temporary_directory))
            record_step(
                path,
                expected_generation=0,
                step_id="phase-0-preflight",
                status="Passed",
                evidence_sha256="a" * 64,
            )
            with self.assertRaises(CheckpointConflictError):
                record_step(
                    path,
                    expected_generation=0,
                    step_id="phase-1-source-admission",
                    status="Passed",
                    evidence_sha256="b" * 64,
                )

    def test_resume_returns_the_first_nonpassed_phase(self) -> None:
        checkpoint = {
            "phase_order": ["phase-0-preflight", "phase-1-source-admission"],
            "steps": [
                {"step_id": "phase-0-preflight", "status": "Passed"},
                {"step_id": "phase-1-source-admission", "status": "Blocked"},
            ],
        }
        self.assertEqual(
            "phase-1-source-admission",
            first_incomplete_step(checkpoint),
        )


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run the tests and verify the checkpoint module is absent**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest tests.testing.workbook05.test_checkpoint -v
```

Expected: import failure.

- [ ] **Step 3: Implement lock, generation check, and atomic replacement**

Use an exclusive lock file created with `os.O_CREAT | os.O_EXCL`. Store the current process ID in the lock file, always remove it in `finally`, write the new JSON to `<name>.tmp`, flush and `os.fsync()`, and replace the original with `os.replace()`.

```python
class CheckpointConflictError(RuntimeError):
    """Raised when another writer or stale caller tries to update state."""


def first_incomplete_step(checkpoint: Mapping[str, Any]) -> str | None:
    status_by_id = {
        step["step_id"]: step["status"] for step in checkpoint["steps"]
    }
    for step_id in checkpoint["phase_order"]:
        if status_by_id[step_id] != "Passed":
            return step_id
    return None
```

`record_step()` must reject an unknown step ID, a status outside the checkpoint schema, an invalid SHA-256 when status is `Passed`, and an `expected_generation` that differs from the persisted generation.

- [ ] **Step 4: Create the committed initial checkpoint**

Use all eleven phase IDs from Task 3, generation `0`, each status `Not started`, empty evidence hashes, and timestamp `2026-08-03T00:00:00Z`. The runtime workflow copies this file into its temporary evidence directory before any update; it never modifies the committed copy.

- [ ] **Step 5: Run checkpoint and schema tests**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest tests.testing.workbook05.test_checkpoint tests.testing.workbook05.test_schema_validation -v
```

Expected: all tests pass.

- [ ] **Step 6: Commit checkpoint controls**

```powershell
git add experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1/checkpoint.json scripts/testing/workbook05/checkpoint.py tests/testing/workbook05/test_checkpoint.py
git commit -m "feat(workbook-05): add atomic campaign checkpoints"
```

---

### Task 6: Implement the Read-Only Windows Preflight Collector

**Files:**
- Create: `experiments/granite_turboquant_intel/configurations/workbook05/preflight-settings.json`
- Create: `scripts/testing/workbook05/Workbook05.Preflight.psm1`
- Create: `scripts/testing/workbook05/Invoke-Workbook05Preflight.ps1`
- Create: `tests/testing/workbook05/fixtures/powercfg-ac-never.txt`
- Create: `tests/testing/workbook05/fixtures/powercfg-ac-timeout.txt`
- Create: `tests/testing/workbook05/Invoke-PreflightModuleTests.ps1`

**Interfaces:**
- Consumes: fixed settings, repository path, runner environment, Windows observations.
- Produces: `preflight-report.json`, `preflight-summary.md`, `environment-snapshot.json`, route command manifests, and a runtime checkpoint copy.

- [ ] **Step 1: Create exact preflight settings**

```json
{
  "schema_version": "1.0",
  "campaign_id": "GTQ-WB05-MF-v1",
  "expected_runner_name": "lenovo-pf4hmd0t-wb05",
  "expected_computer_name": "LENOVO-PF4HMD0T",
  "expected_cpu_substring": "i5-12450H",
  "expected_runner_os": "Windows",
  "expected_runner_arch": "X64",
  "expected_service_account": "nt authority\\network service",
  "minimum_physical_memory_gib": 15.0,
  "minimum_available_memory_gib": 6.0,
  "minimum_system_drive_free_gib": 80.0,
  "require_ac_power": true,
  "required_ac_sleep_timeout_seconds": 0,
  "python_path": "C:\\Program Files\\Python312\\python.exe",
  "python_version": "3.12.10",
  "minimum_cmake_version": "3.26.0",
  "minimum_msbuild_major": 16,
  "required_repository": "arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant"
}
```

- [ ] **Step 2: Create representative `powercfg` fixtures**

The `never` fixture includes:

```text
Current AC Power Setting Index: 0x00000000
```

The timeout fixture includes:

```text
Current AC Power Setting Index: 0x00000384
```

`0x384` is 900 seconds and must fail the no-sleep gate.

- [ ] **Step 3: Write failing PowerShell module tests**

```powershell
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Import-Module (
    Join-Path $RepositoryRoot 'scripts/testing/workbook05/Workbook05.Preflight.psm1'
) -Force

function Assert-Equal {
    param($Expected, $Actual, [string]$Message)
    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected' but got '$Actual'."
    }
}

$neverText = Get-Content -Raw -LiteralPath (
    Join-Path $PSScriptRoot 'fixtures/powercfg-ac-never.txt'
)
$timeoutText = Get-Content -Raw -LiteralPath (
    Join-Path $PSScriptRoot 'fixtures/powercfg-ac-timeout.txt'
)

Assert-Equal 0 (Get-Workbook05AcSleepTimeoutSeconds -PowerCfgOutput $neverText) 'Never-sleep parsing failed.'
Assert-Equal 900 (Get-Workbook05AcSleepTimeoutSeconds -PowerCfgOutput $timeoutText) 'Timeout parsing failed.'

$settings = Get-Content -Raw -LiteralPath (
    Join-Path $RepositoryRoot 'experiments/granite_turboquant_intel/configurations/workbook05/preflight-settings.json'
) | ConvertFrom-Json

$validObservation = [pscustomobject]@{
    RunnerName = 'lenovo-pf4hmd0t-wb05'
    RunnerOs = 'Windows'
    RunnerArch = 'X64'
    ComputerName = 'LENOVO-PF4HMD0T'
    ProcessorName = '12th Gen Intel(R) Core(TM) i5-12450H'
    ServiceAccount = 'nt authority\network service'
    Is64BitOperatingSystem = $true
    PhysicalMemoryGiB = 15.7
    AvailableMemoryGiB = 8.0
    SystemDriveFreeGiB = 100.0
    PowerLineStatus = 'Online'
    AcSleepTimeoutSeconds = 0
    PythonVersion = '3.12.10'
    PythonPath = 'C:\Program Files\Python312\python.exe'
    GitVersion = 'git version 2.53.0.windows.3'
    CMakeVersion = '3.31.6'
    MSBuildVersion = '17.14.51.32402'
    CompilerVersion = '19.44.35228.0'
    WindowsSdkVersion = '10.0.28000.0'
    Repository = 'arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant'
    RepositoryCommit = ('a' * 40)
    RepositoryClean = $true
    EvidenceWriteProbePassed = $true
}

$passed = Test-Workbook05PreflightObservation -Observation $validObservation -Settings $settings
Assert-Equal 'Passed' $passed.OverallStatus 'Valid observation should pass.'

$lowMemoryObservation = $validObservation.PSObject.Copy()
$lowMemoryObservation.AvailableMemoryGiB = 5.5
$failed = Test-Workbook05PreflightObservation -Observation $lowMemoryObservation -Settings $settings
Assert-Equal 'Failed' $failed.OverallStatus 'Low-memory observation should fail.'
if (-not ($failed.Checks | Where-Object { $_.Name -eq 'Available physical memory' -and -not $_.Passed })) {
    throw 'Low-memory failure did not identify the available-memory gate.'
}

Write-Host 'Workbook 05 preflight module tests passed.'
```

- [ ] **Step 4: Run the PowerShell tests and verify the module is missing**

```powershell
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File tests/testing/workbook05/Invoke-PreflightModuleTests.ps1
```

Expected: import failure.

- [ ] **Step 5: Implement pure evaluation separately from machine collection**

`Test-Workbook05PreflightObservation` must not call CIM, `powercfg`, Git, Python, CMake, or MSBuild. It receives an observation object and returns:

```powershell
[pscustomobject]@{
    OverallStatus = 'Passed' # or 'Failed'
    Checks = @(
        [pscustomobject]@{
            Name = 'Available physical memory'
            Required = $true
            Passed = $true
            Expected = 'At least 6 GiB'
            Actual = '8 GiB'
        }
    )
}
```

This separation makes the safety rules deterministic and testable.

- [ ] **Step 6: Implement machine observation**

`Get-Workbook05PreflightObservation` collects, but does not alter:

- GitHub runner environment values;
- computer, CPU, operating-system and memory CIM values;
- `whoami`;
- system-drive free bytes;
- `.NET` power-line status;
- `powercfg /QUERY SCHEME_CURRENT SUB_SLEEP STANDBYIDLE`;
- exact Python path/version;
- Git version;
- `vswhere.exe` installation, MSBuild, Visual Studio CMake, latest x64 `cl.exe`, and Windows SDK;
- `git rev-parse HEAD`, `git status --porcelain`, and `GITHUB_REPOSITORY`;
- one create/read/delete probe under the requested output directory.

The function must not change the power plan, sleep settings, PATH, page file, service account, tool installation, or repository files.

- [ ] **Step 7: Implement evidence export and orchestration**

`Invoke-Workbook05Preflight.ps1` performs this exact sequence:

1. create a clean output directory under `RUNNER_TEMP`;
2. copy the committed campaign manifest, source-admission records and checkpoint into the output directory;
3. collect and evaluate the machine observation;
4. write JSON and Markdown reports before deciding the exit code;
5. run `capture_documented_commands.py` into `documents/` and `commands/`;
6. update only the runtime checkpoint copy for `phase-0-preflight`;
7. leave hash-manifest creation to Task 7;
8. exit nonzero only after all obtainable evidence has been written.

Use parameters:

```powershell
param(
    [Parameter(Mandatory)][string]$RepositoryRoot,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$CampaignId = 'GTQ-WB05-MF-v1'
)
```

- [ ] **Step 8: Run the PowerShell tests**

```powershell
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File tests/testing/workbook05/Invoke-PreflightModuleTests.ps1
```

Expected: `Workbook 05 preflight module tests passed.`

- [ ] **Step 9: Commit the preflight collector**

```powershell
git add experiments/granite_turboquant_intel/configurations/workbook05/preflight-settings.json scripts/testing/workbook05/Workbook05.Preflight.psm1 scripts/testing/workbook05/Invoke-Workbook05Preflight.ps1 tests/testing/workbook05
git commit -m "feat(workbook-05): add read-only Intel preflight"
```

---

### Task 7: Add Evidence Hashing and Untrusted-Bundle Validation

**Files:**
- Create: `scripts/testing/workbook05/hash_manifest.py`
- Create: `scripts/testing/workbook05/bundle_validation.py`
- Create: `tests/testing/workbook05/test_bundle_validation.py`
- Modify: `scripts/testing/workbook05/Invoke-Workbook05Preflight.ps1`

**Interfaces:**
- Consumes: self-hosted preflight bundle.
- Produces: `hash-manifest.sha256` and a list of deterministic `BundleIssue` objects.

- [ ] **Step 1: Write failing bundle-validation tests**

```python
from __future__ import annotations

import json
import shutil
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.bundle_validation import (
    validate_preflight_bundle,
)
from scripts.testing.workbook05.hash_manifest import write_hash_manifest


ROOT = Path(__file__).resolve().parents[3]


class BundleValidationTests(unittest.TestCase):
    def _create_bundle(self, destination: Path) -> None:
        campaign_root = (
            ROOT
            / "experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1"
        )
        shutil.copytree(campaign_root, destination / "controls")
        report = json.loads(
            (
                ROOT
                / "experiments/granite_turboquant_intel/manifests/templates/workbook05/preflight-report-template.json"
            ).read_text(encoding="utf-8")
        )
        report["overall_status"] = "Passed"
        (destination / "preflight-report.json").write_text(
            json.dumps(report), encoding="utf-8"
        )
        (destination / "preflight-summary.md").write_text(
            "# Workbook 05 preflight\n", encoding="utf-8"
        )
        (destination / "environment-snapshot.json").write_text(
            "{}", encoding="utf-8"
        )
        write_hash_manifest(
            destination,
            destination / "hash-manifest.sha256",
        )

    def test_valid_bundle_has_no_issues(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            bundle = Path(temporary_directory)
            self._create_bundle(bundle)
            issues = validate_preflight_bundle(bundle, ROOT)
        self.assertEqual([], issues)

    def test_changed_file_breaks_the_hash_manifest(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            bundle = Path(temporary_directory)
            self._create_bundle(bundle)
            (bundle / "preflight-summary.md").write_text(
                "tampered", encoding="utf-8"
            )
            issues = validate_preflight_bundle(bundle, ROOT)
        self.assertTrue(any(issue.code == "HASH_MISMATCH" for issue in issues))

    def test_secret_and_binary_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            bundle = Path(temporary_directory)
            self._create_bundle(bundle)
            (bundle / "leak.txt").write_text(
                "github_pat_0123456789abcdef", encoding="utf-8"
            )
            (bundle / "runtime.dll").write_bytes(b"MZ")
            write_hash_manifest(bundle, bundle / "hash-manifest.sha256")
            issues = validate_preflight_bundle(bundle, ROOT)
        codes = {issue.code for issue in issues}
        self.assertIn("SECRET_PATTERN", codes)
        self.assertIn("FORBIDDEN_BINARY", codes)


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run the test and verify hashing/validation modules are absent**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest tests.testing.workbook05.test_bundle_validation -v
```

Expected: import failure.

- [ ] **Step 3: Implement stable SHA-256 manifests**

`write_hash_manifest()` recursively hashes regular files in sorted POSIX-relative-path order, excludes the destination manifest itself, and writes:

```text
<64 lowercase hex characters>  <relative/path>
```

`verify_hash_manifest()` rejects missing, added, changed, duplicate or path-traversing entries. It returns readable strings rather than stopping at the first issue.

- [ ] **Step 4: Implement bundle validation**

Required bundle paths are:

```text
preflight-report.json
preflight-summary.md
environment-snapshot.json
controls/campaign-manifest.json
controls/checkpoint.json
controls/route-a-source-admission.json
controls/route-b-source-admission.json
hash-manifest.sha256
```

The validator must:

- verify every required path;
- verify the hash manifest;
- validate campaign, checkpoint, preflight and route JSON with Task 2 schemas;
- run `evaluate_source_admission()` and reject a record labelled `Admitted` when its calculated decision is not admitted;
- reject suffixes `.gguf`, `.safetensors`, `.onnx`, `.pt`, `.pth`, `.ckpt`, `.exe`, and `.dll`;
- reject text patterns `ghp_`, `github_pat_`, `--token `, `HF_TOKEN=`, and `HUGGING_FACE_HUB_TOKEN=`;
- reject absolute or parent-traversing evidence paths;
- retain failed preflight evidence as valid evidence when its report honestly says `Failed`.

- [ ] **Step 5: Add hash generation to the orchestration script**

Call:

```powershell
& 'C:\Program Files\Python312\python.exe' `
    -m scripts.testing.workbook05.hash_manifest `
    --root $OutputDirectory `
    --output (Join-Path $OutputDirectory 'hash-manifest.sha256')
```

Generate the manifest after all evidence files and the runtime checkpoint are complete.

- [ ] **Step 6: Run bundle tests**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest tests.testing.workbook05.test_bundle_validation -v
```

Expected: `3 tests` pass.

- [ ] **Step 7: Commit evidence validation**

```powershell
git add scripts/testing/workbook05 tests/testing/workbook05/test_bundle_validation.py
git commit -m "test(workbook-05): validate preflight evidence bundles"
```

---

### Task 8: Add the Read-Only Two-Job GitHub Actions Preflight Workflow

**Files:**
- Create: `.github/workflows/workbook-05-preflight.yml`
- Create: `tests/testing/workbook05/test_workflow_contract.py`

**Interfaces:**
- Consumes: repository controls and the registered Intel runner.
- Produces: an immutable preflight artifact and a GitHub-hosted validation result; no repository write.

- [ ] **Step 1: Write the failing workflow contract test**

```python
from __future__ import annotations

import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
WORKFLOW = ROOT / ".github/workflows/workbook-05-preflight.yml"


class WorkflowContractTests(unittest.TestCase):
    def test_workflow_is_read_only_and_targets_the_exact_runner(self) -> None:
        text = WORKFLOW.read_text(encoding="utf-8")

        self.assertIn("contents: read", text)
        self.assertIn("actions: read", text)
        self.assertNotIn("contents: write", text)
        self.assertNotIn("pull-requests: write", text)
        for label in (
            "self-hosted",
            "Windows",
            "X64",
            "workbook05",
            "intel-target",
        ):
            self.assertIn(f"- {label}", text)
        self.assertIn("testing/workbook-05-two-route-memory-frontier", text)

    def test_actions_are_pinned_to_immutable_shas(self) -> None:
        text = WORKFLOW.read_text(encoding="utf-8")
        self.assertIn(
            "actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0",
            text,
        )
        self.assertIn(
            "actions/upload-artifact@bbbca2ddaa5d8feaa63e36b76fdaad77386f024f",
            text,
        )
        self.assertIn(
            "actions/download-artifact@974686ed5098c7f9c9289ec946b9058e496a2561",
            text,
        )
        self.assertIn(
            "actions/setup-python@bfe8cc55a7890e3d6672eda6460ef37bfcc70755",
            text,
        )

    def test_workflow_contains_no_repository_write_command(self) -> None:
        lowered = WORKFLOW.read_text(encoding="utf-8").lower()
        for forbidden in (
            "git push",
            "gh pr create",
            "gh pr edit",
            "create-pull-request",
        ):
            self.assertNotIn(forbidden, lowered)


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run the test and verify the workflow is absent**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest tests.testing.workbook05.test_workflow_contract -v
```

Expected: file-not-found failure.

- [ ] **Step 3: Create the workflow with a strict same-repository branch guard**

Use this top-level contract:

```yaml
name: Workbook 05 two-route preflight

on:
  pull_request:
    branches:
      - main
    paths:
      - '.github/workflows/workbook-05-preflight.yml'
      - 'scripts/testing/workbook05/**'
      - 'experiments/granite_turboquant_intel/configurations/workbook05/**'
      - 'experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1/**'
      - 'experiments/granite_turboquant_intel/schemas/workbook05/**'
      - 'tests/testing/workbook05/**'
  workflow_dispatch:

permissions:
  contents: read
  actions: read

concurrency:
  group: workbook-05-preflight
  cancel-in-progress: false
```

The self-hosted job condition is true only for a manual dispatch or a pull request whose source repository equals `github.repository` and whose exact head branch is `testing/workbook-05-two-route-memory-frontier`.

- [ ] **Step 4: Implement the self-hosted collection job**

The job:

- uses all five runner labels;
- has a 20-minute timeout;
- checks out with the pinned checkout SHA;
- verifies `C:\Program Files\Python312\python.exe --version` equals `Python 3.12.10`;
- runs the Python unit tests that need no external dependency;
- runs `Invoke-PreflightModuleTests.ps1`;
- invokes `Invoke-Workbook05Preflight.ps1` with output under `${{ runner.temp }}/workbook-05-preflight`;
- always uploads that directory using the pinned upload-artifact SHA;
- names the artifact `workbook-05-preflight-${{ github.run_id }}-${{ github.run_attempt }}`;
- retains it for 30 days.

Do not use a checkout path under a personal Windows profile.

- [ ] **Step 5: Implement the GitHub-hosted validation job**

The job:

- runs on `windows-latest` and depends on the collection job with `if: ${{ always() }}`;
- checks out the same commit;
- installs Python `3.12` with the pinned setup-python SHA;
- installs `scripts/testing/workbook05/requirements.txt`;
- downloads the exact artifact using the pinned download-artifact SHA;
- runs all Python tests under `tests/testing/workbook05`;
- invokes `bundle_validation.py` against the downloaded directory;
- writes a Markdown validation summary to `GITHUB_STEP_SUMMARY`;
- fails when bundle issues exist;
- never executes any command text captured from an external document.

- [ ] **Step 6: Run the workflow contract test**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest tests.testing.workbook05.test_workflow_contract -v
```

Expected: `3 tests` pass.

- [ ] **Step 7: Commit the workflow**

```powershell
git add .github/workflows/workbook-05-preflight.yml tests/testing/workbook05/test_workflow_contract.py
git commit -m "ci(workbook-05): add read-only two-route preflight"
```

---

### Task 9: Generate the Memory-Frontier Execution Index and WB-05 Revision 1.4 Scaffold

**Files:**
- Create: `scripts/testing/workbook05/generate_execution_index.py`
- Create: `tests/testing/workbook05/test_execution_index.py`
- Create: `docs/testing/Workbook-05-Memory-Frontier-Execution-Index-v1.csv`
- Modify: `docs/testing/workbooks/text-templates/05_Custom_OpenVINO_TurboQuant_Controlled_Retest_Workbook_v1.md`
- Modify: `docs/testing/Workbook-Revision-Register.csv`
- Modify: `docs/testing/workbooks/Controlled-Workbook-Manifest.csv`
- Modify: `scripts/testing/Validate-Controlled-Testing-Workspace.ps1`
- Modify: `scripts/testing/Validate-OpenVINO-Codec-Extension.ps1`

**Interfaces:**
- Consumes: `docs/testing/OpenVINO-Codec-Traceability-Extension-v1.1.csv` and the current WB-05 template.
- Produces: one deterministic index containing every WB-04 Route A and WB-05 Route B test ID exactly once, plus revision 1.4 workbook controls.

- [ ] **Step 1: Write the failing execution-index tests**

```python
from __future__ import annotations

import csv
import subprocess
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
TRACEABILITY = ROOT / "docs/testing/OpenVINO-Codec-Traceability-Extension-v1.1.csv"
INDEX = ROOT / "docs/testing/Workbook-05-Memory-Frontier-Execution-Index-v1.csv"


class ExecutionIndexTests(unittest.TestCase):
    def _rows(self, path: Path) -> list[dict[str, str]]:
        with path.open(newline="", encoding="utf-8-sig") as handle:
            return list(csv.DictReader(handle))

    def test_index_contains_every_openvino_traceability_id_once(self) -> None:
        expected = {
            row["Test_ID"]
            for row in self._rows(TRACEABILITY)
            if row["Workbook_ID"] in {"WB-04", "WB-05"}
        }
        rows = self._rows(INDEX)
        actual = [row["Test_ID"] for row in rows]

        self.assertEqual(expected, set(actual))
        self.assertEqual(len(actual), len(set(actual)))

    def test_route_and_memory_states_are_explicit(self) -> None:
        rows = self._rows(INDEX)
        allowed_routes = {
            "route-a-merged-openvino",
            "route-b-experimental-qjl-polar",
        }
        for row in rows:
            self.assertIn(row["Route_ID"], allowed_routes)
            self.assertIn(
                row["Memory_Rank_Status"],
                {"Not applicable", "Pending conformance", "Verified"},
            )
            self.assertNotEqual("", row["Frontier_Status"])

    def test_generation_is_byte_deterministic(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            first = Path(temporary_directory) / "first.csv"
            second = Path(temporary_directory) / "second.csv"
            command = [
                str(Path(r"C:\Program Files\Python312\python.exe")),
                "-m",
                "scripts.testing.workbook05.generate_execution_index",
                "--traceability",
                str(TRACEABILITY),
            ]
            subprocess.run(command + ["--output", str(first)], check=True)
            subprocess.run(command + ["--output", str(second)], check=True)
            self.assertEqual(first.read_bytes(), second.read_bytes())


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run the test and verify the generator/index are absent**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest tests.testing.workbook05.test_execution_index -v
```

Expected: import or file-not-found failure.

- [ ] **Step 3: Implement deterministic index generation**

Output columns are exactly:

```text
Execution_Record_ID,Test_ID,Workbook_ID,Route_ID,Phase_ID,Category,Test_Title,Expected_K_Record_Bytes,Expected_V_Record_Bytes,Verified_K_Record_Bytes,Verified_V_Record_Bytes,Memory_Rank,Memory_Rank_Status,Frontier_Status,Skip_Reason,Run_ID,Evidence_Path
```

Rules:

- WB-04 rows map to `route-a-merged-openvino`.
- WB-05 rows map to `route-b-experimental-qjl-polar`.
- Build/setup rows use `Memory_Rank_Status=Not applicable` and `Frontier_Status=Not started`.
- Codec/conformance, sweep, inference, context and ablation rows use `Memory_Rank_Status=Pending conformance` and `Frontier_Status=Blocked pending verified storage`.
- Expected and verified byte fields remain empty only because the column's state explains why; no value is invented.
- Execution records are stable `MF-0001`, `MF-0002`, and so forth after sorting by route, phase, category and test ID.
- No final memory rank is assigned before conformance evidence.

- [ ] **Step 4: Generate and commit the execution index**

```powershell
& 'C:\Program Files\Python312\python.exe' -m scripts.testing.workbook05.generate_execution_index `
  --traceability docs/testing/OpenVINO-Codec-Traceability-Extension-v1.1.csv `
  --output docs/testing/Workbook-05-Memory-Frontier-Execution-Index-v1.csv
```

- [ ] **Step 5: Revise the canonical WB-05 template without deleting IDs**

Change the title and opening control text to revision `1.4`. Add a top-level section named `Two-route memory-frontier execution control` containing:

```markdown
| Control | Value |
| --- | --- |
| Campaign ID | GTQ-WB05-MF-v1 |
| Route A | Merged OpenVINO TurboQuant; control IDs remain in WB-04 and are referenced by the execution index |
| Route B | Experimental QJL/PolarQuant candidate from PR #35092; OVT IDs remain in WB-05 |
| Execution order | Derived only after expected and measured K/V storage are reconciled |
| Context discovery | 512, 1024, 2048, 4096, 8192, 16384, then doubling while stable and supported |
| Quality controls | GTQ-PROMPTS-v1 and GTQ-QUALITY-RUBRIC-v1 |
| Execution index | docs/testing/Workbook-05-Memory-Frontier-Execution-Index-v1.csv |
```

Replace language that says the route already exposes all six codecs with language that says those are Route B candidates requiring source admission and activation proof. Retain every `OVT-B*`, `OVT-A*`, `OVT-S*`, and `OVT-*` row.

- [ ] **Step 6: Append WB-05 revision record `WR-032`**

Change `WR-020` status from `Current - pending merge` to `Superseded`, then append:

```csv
WR-032,WB-05,1.4,2026-08-03,Student,Two-route memory-frontier control,"Separated merged OpenVINO and experimental QJL/PolarQuant source admission; added lowest-verified-storage execution metadata, frontier states, exact-document capture and preflight evidence references without changing existing test IDs.",The target 16 GB laptop requires a safe memory-first execution order and the merged and experimental source boundaries must not be conflated.,OV-B08-OV-B12; OV-TQS-01-OV-TQS-12; OV-TQ-01-OV-TQ-20; OVT-B01-OVT-B15; OVT-A01-OVT-A12; OVT-S01-OVT-S36; OVT-01-OVT-36,testing/workbook-05-two-route-memory-frontier; #42,Current - draft PR #42,1.3
```

- [ ] **Step 7: Update the controlled workbook manifest programmatically**

After deterministic generation, calculate the canonical template SHA-256 and generated WB-05 DOCX SHA-256. Update only the WB-05 row:

- `Revision` becomes `1.4`;
- `Canonical_Template_SHA256` is the measured template hash;
- `Last_Validated_DOCX_SHA256` is the measured DOCX hash;
- `Status` describes the two-route preflight scaffold and states that no hardware codec result has been produced;
- `Purpose` describes merged Route A control plus experimental Route B admission, memory-frontier and quality/performance evidence.

Use Python's `csv.DictReader`, `csv.DictWriter`, and `hashlib.sha256`; do not manually paste a guessed hash.

- [ ] **Step 8: Extend existing validators**

`Validate-Controlled-Testing-Workspace.ps1` must require:

```text
docs/testing/Workbook-05-Memory-Frontier-Execution-Index-v1.csv
experiments/granite_turboquant_intel/configurations/workbook05/preflight-settings.json
experiments/granite_turboquant_intel/configurations/workbook05/pinned-document-sources.json
experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1/campaign-manifest.json
experiments/granite_turboquant_intel/schemas/workbook05/source-admission.schema.json
scripts/testing/Validate-Workbook05-MemoryFrontier.ps1
```

`Validate-OpenVINO-Codec-Extension.ps1` must verify that the new execution index contains all Route A and Route B traceability IDs exactly once and that WB-05 revision is `1.4`.

- [ ] **Step 9: Run the index tests and generate workbooks twice**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest tests.testing.workbook05.test_execution_index -v

$first = Join-Path $env:TEMP 'wb05-generation-first'
$second = Join-Path $env:TEMP 'wb05-generation-second'
Remove-Item $first, $second -Recurse -Force -ErrorAction SilentlyContinue

& 'C:\Program Files\Python312\python.exe' scripts/testing/Generate-Controlled-Workbooks.py --output-directory $first
& 'C:\Program Files\Python312\python.exe' scripts/testing/Apply-Workbook-Revision-History.py --input-directory $first --output-directory $first
& 'C:\Program Files\Python312\python.exe' scripts/testing/Generate-Controlled-Workbooks.py --output-directory $second
& 'C:\Program Files\Python312\python.exe' scripts/testing/Apply-Workbook-Revision-History.py --input-directory $second --output-directory $second

$firstHash = (Get-FileHash (Join-Path $first '05_Custom_OpenVINO_TurboQuant_Controlled_Retest_Workbook_v1.docx') -Algorithm SHA256).Hash
$secondHash = (Get-FileHash (Join-Path $second '05_Custom_OpenVINO_TurboQuant_Controlled_Retest_Workbook_v1.docx') -Algorithm SHA256).Hash
if ($firstHash -ne $secondHash) { throw 'WB-05 generation was not byte-deterministic.' }
```

Expected: execution-index tests pass and the two hashes are identical.

- [ ] **Step 10: Commit the controlled revision scaffold**

```powershell
git add docs/testing scripts/testing/Validate-Controlled-Testing-Workspace.ps1 scripts/testing/Validate-OpenVINO-Codec-Extension.ps1 scripts/testing/workbook05/generate_execution_index.py tests/testing/workbook05/test_execution_index.py
git commit -m "docs(workbook-05): scaffold revision 1.4 memory frontier"
```

---

### Task 10: Add One Entry-Point Validator, Run the Complete Gate, and Update PR #42

**Files:**
- Create: `scripts/testing/Validate-Workbook05-MemoryFrontier.ps1`
- Modify: draft pull request `#42` description and checklist after verification.

**Interfaces:**
- Consumes: every control, schema, test, workflow and generated workbook from Tasks 1-9.
- Produces: one local validation command and verified PR context.

- [ ] **Step 1: Create the entry-point validator**

The script runs, in this exact order:

```powershell
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepositoryRoot = (& git rev-parse --show-toplevel).Trim()
Set-Location -LiteralPath $RepositoryRoot
$Python = 'C:\Program Files\Python312\python.exe'

& $Python -m pip install -r scripts/testing/workbook05/requirements.txt
& $Python -m unittest discover -s tests/testing/workbook05 -p 'test_*.py' -v
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File tests/testing/workbook05/Invoke-PreflightModuleTests.ps1
& .\scripts\testing\Validate-Controlled-Testing-Workspace.ps1
& .\scripts\testing\Validate-OpenVINO-Codec-Extension.ps1

if ($LASTEXITCODE -ne 0) {
    throw "Workbook 05 validation failed with exit code $LASTEXITCODE."
}

Write-Host 'WORKBOOK 05 MEMORY-FRONTIER SCAFFOLD: PASS' -ForegroundColor Green
Write-Host 'No OpenVINO build, model download or inference was performed.' -ForegroundColor Yellow
```

Add explicit exit-code checks after every native Python invocation so a failed command cannot be hidden by a later successful command.

- [ ] **Step 2: Run the full local gate**

```powershell
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/testing/Validate-Workbook05-MemoryFrontier.ps1
```

Expected final lines:

```text
WORKBOOK 05 MEMORY-FRONTIER SCAFFOLD: PASS
No OpenVINO build, model download or inference was performed.
```

- [ ] **Step 3: Run repository hygiene checks**

```powershell
git diff --check
git status --short
git grep -n -E 'TBD|TODO|implement later|fill in details' -- `
  .github/workflows/workbook-05-preflight.yml `
  scripts/testing/workbook05 `
  experiments/granite_turboquant_intel/schemas/workbook05 `
  experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1
```

Expected:

- `git diff --check` returns no output;
- only intended files are changed before the final commit;
- the placeholder scan returns no matches.

- [ ] **Step 4: Commit the integrated validation gate**

```powershell
git add scripts/testing/Validate-Workbook05-MemoryFrontier.ps1
git commit -m "test(workbook-05): add the preflight scaffold gate"
```

- [ ] **Step 5: Push and inspect GitHub Actions**

```powershell
git push origin testing/workbook-05-two-route-memory-frontier
```

Expected GitHub checks:

```text
Build and test: success
Workbook 05 two-route preflight / Collect Intel preflight: success or an evidence-backed preflight failure
Workbook 05 two-route preflight / Validate preflight artifact: success
```

A genuine preflight failure is not converted into a pass. Inspect the artifact and correct the first failed gate in a new commit.

- [ ] **Step 6: Update draft PR #42 with complete context**

The PR description must include:

- the exact files and responsibilities added;
- Route A Runtime and GenAI candidate commits;
- Route B PR/commit and blocker `RB-SRC-001`;
- why WB-05 advances from `1.3` to `1.4`;
- proof that no build, model download or inference occurred;
- local test commands and outcomes;
- GitHub Actions run IDs and artifact names;
- security permissions and branch guard;
- remaining gates before an OpenVINO build can start.

Mark the PR ready for review only after local and GitHub-hosted validations are green and the self-hosted result is either `Passed` or an honestly documented environment failure with a preserved artifact.

---

## Plan Self-Review Record

### Specification coverage

- Two route separation: Tasks 1, 3, 9.
- Route B admission gate and source blocker: Task 3.
- Exact pinned document capture: Task 4.
- Machine and repository preflight: Task 6.
- Evidence schemas and validation: Tasks 2 and 7.
- Checkpoint/resume skeleton: Task 5.
- Read-only self-hosted execution and trusted validation: Task 8.
- WB-05 controlled revision, stable IDs and memory-frontier metadata: Task 9.
- Complete verification and detailed PR context: Task 10.
- No OpenVINO build, model inference or algorithm implementation: enforced in Global Constraints and Task 10 output.

### Type and name consistency

- Campaign ID is `GTQ-WB05-MF-v1` everywhere.
- Route IDs and folder names are fixed in Global Constraints.
- `ValidationIssue`, `AdmissionDecision`, `CommandBlock`, `CheckpointConflictError`, `BundleIssue`, and all public function names match the Stable Interfaces section.
- Result-state and checkpoint-state enums are intentionally different and explicitly listed.
- WB-05 advances from current revision `1.3` to `1.4` consistently.

### Engineering references

- *Systems Engineering Principles and Practice*, Chapter 17: test planning, preparation and traceability.
- *The Art of Unit Testing*, Chapter 10: test recipes and separation of delivery and discovery pipelines.
- *Engineering Software Products*, Chapters 9-10: testing, automated DevOps and code management.
- *Designing Secure Software*, Chapters 2-4: trust boundaries, exposure minimisation, least privilege, secure defaults and fail-secure behaviour.
- *Code Complete*, Chapters 22, 28 and 29: automated testing, test records, configuration management, incremental integration and smoke tests.
- *AI Engineering*, Chapters 3, 4 and 9: exact evaluation, system criteria, latency, inference metrics and performance-quality trade-offs.
