"""Validate Workbook 05 documented-build records and the stage template."""

from __future__ import annotations

import json
import re
from pathlib import Path, PurePosixPath
from typing import Final

from jsonschema import Draft202012Validator


SCHEMA_FILES: Final[dict[str, str]] = {
    "command": "build-command-record.schema.json",
    "deviation": "build-deviation-record.schema.json",
    "dependency": "build-dependency-record.schema.json",
    "resource": "build-resource-summary.schema.json",
    "binary": "build-binary-record.schema.json",
    "compatibility": "build-compatibility-attempt.schema.json",
    "decision": "build-decision.schema.json",
}

_SCHEMA_DIRECTORY = Path(
    "experiments/granite_turboquant_intel/schemas/workbook05"
)
_CLAIM_FLAGS: Final[tuple[str, ...]] = (
    "granite_model_test_authorised",
    "activation_claim_authorised",
    "packed_storage_claim_authorised",
    "performance_claim_authorised",
    "quality_claim_authorised",
)
_ROUTE_IDS: Final[tuple[str, str]] = (
    "route-a-merged-openvino",
    "route-b-experimental-qjl-polar",
)


def _json_path(parts: list[object]) -> str:
    """Convert jsonschema's path into a stable beginner-readable JSON path."""

    value = "$"
    for part in parts:
        value += f"[{part}]" if isinstance(part, int) else f".{part}"
    return value


def _schema_errors(payload: object, schema: dict[str, object]) -> list[str]:
    """Return deterministic schema issues for one in-memory payload."""

    validator = Draft202012Validator(schema)
    errors = sorted(
        validator.iter_errors(payload),
        key=lambda error: (
            tuple(str(part) for part in error.absolute_path),
            error.message,
        ),
    )
    return [
        f"{_json_path(list(error.absolute_path))}: {error.message}"
        for error in errors
    ]


def _is_safe_evidence_path(value: object) -> bool:
    """Return True only for a non-empty normalised relative artifact path."""

    if not isinstance(value, str) or not value or "\x00" in value:
        return False
    if "\\" in value or value.startswith("/") or value.startswith("//"):
        return False
    if re.match(r"^[A-Za-z]:", value):
        return False

    path = PurePosixPath(value)
    return all(part not in {"", ".", ".."} for part in path.parts)


def _append_path_issue(errors: list[str], json_path: str, value: object) -> None:
    """Append one stable message when an evidence path is unsafe."""

    if not _is_safe_evidence_path(value):
        errors.append(f"{json_path}: unsafe evidence path")


def load_build_schemas(repository_root: Path) -> dict[str, dict]:
    """Load every versioned documented-build schema from the repository."""

    schema_root = repository_root / _SCHEMA_DIRECTORY
    loaded: dict[str, dict] = {}
    for record_type, file_name in SCHEMA_FILES.items():
        schema_path = schema_root / file_name
        if not schema_path.is_file():
            raise FileNotFoundError(
                f"Required Workbook 05 build schema is missing: {schema_path}"
            )
        schema = json.loads(schema_path.read_text(encoding="utf-8-sig"))
        Draft202012Validator.check_schema(schema)
        loaded[record_type] = schema
    return loaded


def validate_build_record(
    record_type: str,
    payload: object,
    repository_root: Path,
) -> list[str]:
    """Validate one build record using schema and cross-field safety rules."""

    schemas = load_build_schemas(repository_root)
    if record_type not in schemas:
        return [f"$: unknown build record type: {record_type}"]

    errors = _schema_errors(payload, schemas[record_type])
    if not isinstance(payload, dict):
        return errors

    # Schema errors are useful, but these explicit messages make the most
    # important security and scientific boundaries stable for tests and reports.
    if record_type == "command":
        if not isinstance(payload.get("arguments"), list):
            errors.append("$.arguments: arguments must be an array")
        _append_path_issue(errors, "$.stdout_path", payload.get("stdout_path"))
        _append_path_issue(errors, "$.stderr_path", payload.get("stderr_path"))

    if record_type == "deviation":
        evidence_paths = payload.get("evidence_paths")
        if isinstance(evidence_paths, list):
            for index, value in enumerate(evidence_paths):
                _append_path_issue(
                    errors,
                    f"$.evidence_paths[{index}]",
                    value,
                )
        if (
            payload.get("executed") is True
            and payload.get("approval_status") != "Approved"
        ):
            errors.append(
                "$.approval_status: unapproved deviation cannot be executed"
            )

    if record_type == "binary":
        _append_path_issue(
            errors,
            "$.relative_path",
            payload.get("relative_path"),
        )
        if payload.get("copied_to_artifact") is not False:
            errors.append(
                "$.copied_to_artifact: copied_to_artifact must be false"
            )

    if record_type == "compatibility":
        evidence_paths = payload.get("evidence_paths")
        if isinstance(evidence_paths, list):
            for index, value in enumerate(evidence_paths):
                _append_path_issue(
                    errors,
                    f"$.evidence_paths[{index}]",
                    value,
                )

    if record_type == "decision":
        for flag in _CLAIM_FLAGS:
            if payload.get(flag) is not False:
                errors.append(f"$.{flag}: {flag} must be false")

        required_components = payload.get("required_components")
        if isinstance(required_components, list):
            for index, component in enumerate(required_components):
                if isinstance(component, dict):
                    _append_path_issue(
                        errors,
                        f"$.required_components[{index}].evidence_path",
                        component.get("evidence_path"),
                    )

        if payload.get("status") == "BuildCandidate":
            statuses = []
            if isinstance(required_components, list):
                statuses = [
                    item.get("status")
                    for item in required_components
                    if isinstance(item, dict)
                ]
            if (
                payload.get("component") != "route"
                or not statuses
                or any(status != "Passed" for status in statuses)
            ):
                errors.append(
                    "$.status: BuildCandidate requires every required "
                    "component to be Passed"
                )

    return sorted(set(errors))


def validate_build_stage_template(
    payload: object,
    repository_root: Path,
) -> list[str]:
    """Validate the route and claim boundary in the versioned stage template."""

    # Loading the schemas here also proves the template references a complete
    # contract set before any build-stage implementation can proceed.
    load_build_schemas(repository_root)

    errors: list[str] = []
    if not isinstance(payload, dict):
        return ["$: build-stage template must be an object"]

    expected_scalars = {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "workbook_id": "WB-05",
        "workbook_revision": "1.4",
        "phase_id": "phase-2-documented-build",
    }
    for key, expected in expected_scalars.items():
        if payload.get(key) != expected:
            errors.append(f"$.{key}: must equal {expected!r}")

    allowed_keys = set(expected_scalars) | {"routes", *_CLAIM_FLAGS}
    unexpected = sorted(set(payload) - allowed_keys)
    for key in unexpected:
        errors.append(f"$.{key}: unexpected property")

    for flag in _CLAIM_FLAGS:
        if payload.get(flag) is not False:
            errors.append(f"$.{flag}: {flag} must be false")

    routes = payload.get("routes")
    if not isinstance(routes, list):
        errors.append("$.routes: routes must be an array")
        return sorted(set(errors))

    if len(routes) != 2:
        errors.append("$.routes: exactly two route records are required")

    seen_route_ids: list[object] = []
    for index, route in enumerate(routes):
        path = f"$.routes[{index}]"
        if not isinstance(route, dict):
            errors.append(f"{path}: route must be an object")
            continue

        route_allowed = {
            "route_id",
            "enabled",
            "prerequisite_status",
            "prerequisite_evidence_path",
        }
        for key in sorted(set(route) - route_allowed):
            errors.append(f"{path}.{key}: unexpected property")

        route_id = route.get("route_id")
        seen_route_ids.append(route_id)
        if route_id not in _ROUTE_IDS:
            errors.append(f"{path}.route_id: unknown route")

        if not isinstance(route.get("enabled"), bool):
            errors.append(f"{path}.enabled: must be a boolean")

        if route.get("prerequisite_status") not in {
            "Accepted",
            "Pending",
            "Rejected",
            "Blocked",
        }:
            errors.append(
                f"{path}.prerequisite_status: unsupported prerequisite status"
            )

        _append_path_issue(
            errors,
            f"{path}.prerequisite_evidence_path",
            route.get("prerequisite_evidence_path"),
        )

    if set(seen_route_ids) != set(_ROUTE_IDS):
        errors.append("$.routes: Route A and Route B must each appear once")

    return sorted(set(errors))
