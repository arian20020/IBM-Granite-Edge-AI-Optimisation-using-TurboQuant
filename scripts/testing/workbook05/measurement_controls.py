"""Capture and validate the frozen Workbook 05 measurement controls."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping

from scripts.testing.workbook05.schema_validation import validate_json_file


EXPECTED_CAMPAIGN_ID = "GTQ-WB05-MF-v1"
EXPECTED_PROMPT_SET_ID = "GTQ-PROMPTS-v1"
EXPECTED_RUBRIC_ID = "GTQ-QUALITY-RUBRIC-v1"
EXPECTED_PROMPT_IDS = ("P1", "P2", "P3", "P4", "P5", "P6")


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


def _load_json(path: Path) -> dict[str, Any]:
    """Read a UTF-8 JSON object and reject non-object documents."""

    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object: {path}")
    return value


def capture_measurement_controls(
    repository_root: Path,
    configuration_path: Path,
    destination: Path,
) -> dict[str, Any]:
    """Capture IDs, hashes, rubric weights, and completion requirements."""

    repository_root = repository_root.resolve()
    configuration = _load_json(configuration_path)
    prompt_path = repository_root / configuration["prompt_set_path"]
    rubric_path = repository_root / configuration["rubric_path"]
    prompt_set = _load_json(prompt_path)
    rubric = _load_json(rubric_path)

    control_paths = (
        configuration["metric_definitions_path"],
        configuration["prompt_set_path"],
        configuration["rubric_path"],
        configuration["quality_amendment_path"],
        configuration["measured_run_schema_path"],
        configuration["measured_run_template_path"],
    )
    prompt_ids = [prompt["prompt_id"] for prompt in prompt_set["prompts"]]
    rubric_weight_total = sum(
        float(dimension["weight"]) for dimension in rubric["dimensions"]
    )

    report = {
        "schema_version": "1.0",
        "campaign_id": configuration["campaign_id"],
        "prompt_set_id": prompt_set["prompt_set_id"],
        "rubric_id": rubric["rubric_id"],
        "prompt_count": len(prompt_ids),
        "prompt_ids": prompt_ids,
        "rubric_weight_total": rubric_weight_total,
        "required_metric_names": list(configuration["required_metric_names"]),
        "controls": [
            {
                "path": relative_path,
                "sha256": _sha256(repository_root / relative_path),
            }
            for relative_path in control_paths
        ],
        "raw_output_required": True,
        "activation_proof_required": True,
        "fallback_result_required": True,
        "separate_k_v_allocation_required": True,
        "weight_and_kv_axes_separate": True,
    }
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(
        json.dumps(report, indent=2) + "\n",
        encoding="utf-8",
    )
    return report


def validate_measurement_controls(
    report: Mapping[str, Any],
    *,
    repository_root: Path | None = None,
    configuration_path: Path | None = None,
) -> list[MeasurementControlIssue]:
    """Return every identity, completeness, hash, and schema problem."""

    issues: list[MeasurementControlIssue] = []

    for field, expected in {
        "campaign_id": EXPECTED_CAMPAIGN_ID,
        "prompt_set_id": EXPECTED_PROMPT_SET_ID,
        "rubric_id": EXPECTED_RUBRIC_ID,
    }.items():
        if report.get(field) != expected:
            issues.append(
                MeasurementControlIssue(
                    "CONTROL_ID_MISMATCH",
                    field,
                    f"Expected {expected!r}; found {report.get(field)!r}.",
                )
            )

    prompt_ids = tuple(report.get("prompt_ids", ()))
    if prompt_ids != EXPECTED_PROMPT_IDS or report.get("prompt_count") != 6:
        issues.append(
            MeasurementControlIssue(
                "PROMPT_SET_INCOMPLETE",
                "prompt_ids",
                f"Expected P1-P6 in order; found {prompt_ids!r}.",
            )
        )

    weight_total = report.get("rubric_weight_total")
    if not isinstance(weight_total, (int, float)) or not math.isclose(
        float(weight_total), 1.0, rel_tol=0.0, abs_tol=1e-9
    ):
        issues.append(
            MeasurementControlIssue(
                "RUBRIC_WEIGHT_INVALID",
                "rubric_weight_total",
                f"Rubric weights must total 1.0; found {weight_total!r}.",
            )
        )

    for flag in (
        "raw_output_required",
        "activation_proof_required",
        "fallback_result_required",
        "separate_k_v_allocation_required",
        "weight_and_kv_axes_separate",
    ):
        if report.get(flag) is not True:
            issues.append(
                MeasurementControlIssue(
                    "CONTROL_FLAG_DISABLED",
                    flag,
                    "The control must be true.",
                )
            )

    controls = report.get("controls")
    if not isinstance(controls, list) or len(controls) < 6:
        issues.append(
            MeasurementControlIssue(
                "CONTROL_HASHES_INCOMPLETE",
                "controls",
                "At least six controlling files and hashes are required.",
            )
        )
    else:
        for index, control in enumerate(controls):
            path = control.get("path") if isinstance(control, dict) else None
            digest = control.get("sha256") if isinstance(control, dict) else None
            if not isinstance(path, str) or not path:
                issues.append(
                    MeasurementControlIssue(
                        "CONTROL_PATH_INVALID",
                        f"controls[{index}].path",
                        "Control path must be non-empty.",
                    )
                )
            if (
                not isinstance(digest, str)
                or len(digest) != 64
                or any(character not in "0123456789abcdef" for character in digest)
            ):
                issues.append(
                    MeasurementControlIssue(
                        "CONTROL_HASH_INVALID",
                        f"controls[{index}].sha256",
                        "SHA-256 must contain 64 lowercase hexadecimal characters.",
                    )
                )
            if repository_root is not None and isinstance(path, str) and isinstance(digest, str):
                control_path = repository_root.resolve() / path
                if not control_path.is_file():
                    issues.append(
                        MeasurementControlIssue(
                            "CONTROL_FILE_MISSING",
                            path,
                            "Controlling file is missing.",
                        )
                    )
                elif _sha256(control_path) != digest:
                    issues.append(
                        MeasurementControlIssue(
                            "CONTROL_HASH_MISMATCH",
                            path,
                            "Captured SHA-256 does not match the controlling file.",
                        )
                    )

    if repository_root is not None and configuration_path is not None:
        try:
            configuration = _load_json(configuration_path)
            schema_path = repository_root / configuration["measured_run_schema_path"]
            template_path = repository_root / configuration["measured_run_template_path"]
            for schema_issue in validate_json_file(template_path, schema_path):
                issues.append(
                    MeasurementControlIssue(
                        "MEASURED_RUN_TEMPLATE_INVALID",
                        schema_issue.json_path,
                        schema_issue.message,
                    )
                )
        except (OSError, KeyError, ValueError, json.JSONDecodeError) as error:
            issues.append(
                MeasurementControlIssue(
                    "MEASURED_RUN_TEMPLATE_INVALID",
                    "measured_run_template",
                    str(error),
                )
            )

    return issues


def _main() -> int:
    """Capture and validate controls for local or workflow use."""

    parser = argparse.ArgumentParser()
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--configuration", type=Path, required=True)
    parser.add_argument("--destination", type=Path, required=True)
    arguments = parser.parse_args()

    report = capture_measurement_controls(
        arguments.repository_root,
        arguments.configuration,
        arguments.destination,
    )
    issues = validate_measurement_controls(
        report,
        repository_root=arguments.repository_root,
        configuration_path=arguments.configuration,
    )
    for issue in issues:
        print(f"{issue.code}: {issue.path}: {issue.message}")
    return 1 if issues else 0


if __name__ == "__main__":
    raise SystemExit(_main())
