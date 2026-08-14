"""Revalidate the exact accepted Workbook 05 Runtime and GenAI prerequisites."""

from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Mapping, Sequence

from scripts.testing.workbook05.phase3.contracts import assert_phase3_record
from scripts.testing.workbook05.phase3.hashing import sha256_file
from scripts.testing.workbook05.phase3.paths import (
    assert_normal_local_directory,
    relative_evidence_path,
)


EXPECTED_RUNTIME_DECISION_SHA256 = (
    "5dae00b38edb9a20f2d82a99dcf4cd1e2d4e7aeaaf6984873ccc55abccf3ae38"
)
EXPECTED_GENAI_DECISION_SHA256 = (
    "0f273e1f345e1512e8e159af9b83f9ad521a26aa5e0ae813f2599161a5cb4a79"
)
EXPECTED_RUNTIME_SOURCE = "b9a1f201c109e0bed74763934f79483cf6c4cbf4"
EXPECTED_GENAI_SOURCE = "bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0"
EXPECTED_ROUTE = "route-a-merged-openvino"
EXPECTED_CAMPAIGN = "GTQ-WB05-MF-v1"

RUNTIME_REQUIRED_FILES: tuple[str, ...] = (
    "runtime/cmake/OpenVINOConfig.cmake",
    "runtime/include/openvino/frontend/onnx/extension/conversion.hpp",
    "runtime/include/openvino/frontend/tensorflow/extension/conversion.hpp",
)
GENAI_REQUIRED_FILES: tuple[str, ...] = (
    "runtime/bin/intel64/Release/openvino_genai.dll",
    "runtime/bin/intel64/Release/openvino_genai_c.dll",
    "runtime/bin/intel64/Release/openvino_tokenizers.dll",
    "python/openvino_genai/py_openvino_genai.cp312-win_amd64.pyd",
)
CLAIM_FLAGS: tuple[str, ...] = (
    "granite_model_test_authorised",
    "activation_claim_authorised",
    "packed_storage_claim_authorised",
    "performance_claim_authorised",
    "quality_claim_authorised",
)


def _utc_now() -> str:
    """Return an RFC 3339 UTC timestamp with a stable trailing Z."""

    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def _load_json_object(path: Path, label: str) -> dict[str, Any]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"{label} is not valid UTF-8 JSON: {path}: {error}") from error
    if not isinstance(payload, dict):
        raise ValueError(f"{label} must contain one JSON object: {path}")
    return payload


def _verify_decision(
    *,
    decision: Mapping[str, Any],
    label: str,
    component: str,
    source_commit: str,
) -> None:
    expected = {
        "schema_version": "1.0",
        "campaign_id": EXPECTED_CAMPAIGN,
        "record_type": "build-decision",
        "route_id": EXPECTED_ROUTE,
        "component": component,
        "source_commit": source_commit,
        "status": "Passed",
    }
    for field, expected_value in expected.items():
        actual = decision.get(field)
        if actual != expected_value:
            raise ValueError(
                f"{label} {field} mismatch. Expected {expected_value!r}, found {actual!r}."
            )

    required_components = decision.get("required_components")
    if required_components != []:
        raise ValueError(f"{label} required_components must remain an empty array.")

    for flag in CLAIM_FLAGS:
        if decision.get(flag) is not False:
            raise ValueError(f"{label} unexpectedly authorises scientific claim: {flag}")


def _verify_decision_file(
    path: Path,
    *,
    label: str,
    expected_sha256: str,
    component: str,
    source_commit: str,
) -> dict[str, Any]:
    # This checks the decision parent and the decision itself without following a
    # symbolic-link or reparse-point boundary.
    relative_evidence_path(path.parent, path)
    actual_sha256 = sha256_file(path)
    if actual_sha256 != expected_sha256:
        raise ValueError(
            f"{label} SHA-256 mismatch. Expected {expected_sha256}, found {actual_sha256}."
        )

    decision = _load_json_object(path, label)
    _verify_decision(
        decision=decision,
        label=label,
        component=component,
        source_commit=source_commit,
    )
    return decision


def _verify_install(
    install: Path,
    *,
    label: str,
    required_files: Sequence[str],
) -> tuple[Path, list[dict[str, Any]]]:
    install_root = assert_normal_local_directory(install, install.parent)
    records: list[dict[str, Any]] = []
    for relative in required_files:
        path = install_root.joinpath(*relative.split("/"))
        if not path.exists():
            raise ValueError(f"Required {label} file is missing: {path}")
        canonical_relative = relative_evidence_path(install_root, path)
        if not path.is_file():
            raise ValueError(f"Required {label} path is not a regular file: {path}")
        records.append(
            {
                "relative_path": canonical_relative,
                "size_bytes": path.stat().st_size,
                "sha256": sha256_file(path),
            }
        )
    return install_root, records


def verify_prerequisites(
    runtime_install: Path,
    runtime_decision: Path,
    genai_install: Path,
    genai_decision: Path,
) -> dict[str, Any]:
    """Return a proof only when the accepted Phase 2 identities remain exact."""

    runtime_root, runtime_files = _verify_install(
        runtime_install,
        label="Runtime",
        required_files=RUNTIME_REQUIRED_FILES,
    )
    genai_root, genai_files = _verify_install(
        genai_install,
        label="GenAI",
        required_files=GENAI_REQUIRED_FILES,
    )

    _verify_decision_file(
        runtime_decision,
        label="Runtime decision",
        expected_sha256=EXPECTED_RUNTIME_DECISION_SHA256,
        component="runtime",
        source_commit=EXPECTED_RUNTIME_SOURCE,
    )
    _verify_decision_file(
        genai_decision,
        label="GenAI decision",
        expected_sha256=EXPECTED_GENAI_DECISION_SHA256,
        component="genai",
        source_commit=EXPECTED_GENAI_SOURCE,
    )

    return {
        "schema_version": "1.0",
        "campaign_id": EXPECTED_CAMPAIGN,
        "record_type": "prerequisite-proof",
        "route_id": EXPECTED_ROUTE,
        "generated_at_utc": _utc_now(),
        "status": "Passed",
        "reasons": [
            "The exact accepted Route A Runtime and GenAI decisions, source commits, claim boundaries, and required installed files were independently revalidated."
        ],
        "runtime": {
            "component": "runtime",
            "install_path": str(runtime_root),
            "decision_path": str(runtime_decision.resolve(strict=True)),
            "decision_sha256": EXPECTED_RUNTIME_DECISION_SHA256,
            "source_commit": EXPECTED_RUNTIME_SOURCE,
            "decision_status": "Passed",
            "required_files": runtime_files,
        },
        "genai": {
            "component": "genai",
            "install_path": str(genai_root),
            "decision_path": str(genai_decision.resolve(strict=True)),
            "decision_sha256": EXPECTED_GENAI_DECISION_SHA256,
            "source_commit": EXPECTED_GENAI_SOURCE,
            "decision_status": "Passed",
            "required_files": genai_files,
        },
        "granite_model_test_authorised": False,
        "activation_claim_authorised": False,
        "packed_storage_claim_authorised": False,
        "performance_claim_authorised": False,
        "quality_claim_authorised": False,
    }


def _parse_args(argv: Sequence[str] | None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Revalidate accepted Workbook 05 Phase 2 prerequisites."
    )
    parser.add_argument("--runtime-install", type=Path, required=True)
    parser.add_argument("--runtime-decision", type=Path, required=True)
    parser.add_argument("--genai-install", type=Path, required=True)
    parser.add_argument("--genai-decision", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv)
    try:
        if args.output.exists():
            raise ValueError(f"Refusing to overwrite prerequisite proof output: {args.output}")
        if not args.output.is_absolute():
            raise ValueError(f"Prerequisite proof output must be absolute: {args.output}")
        output_parent = assert_normal_local_directory(
            args.output.parent,
            args.output.parent,
            allow_root=True,
        )
        repository_root = args.repository_root.resolve(strict=True)
        proof = verify_prerequisites(
            args.runtime_install,
            args.runtime_decision,
            args.genai_install,
            args.genai_decision,
        )
        assert_phase3_record("prerequisite-proof", proof, repository_root)
        args.output.write_text(
            json.dumps(proof, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
        )
        # Ensure no caller-controlled path replacement occurred during writing.
        if args.output.parent.resolve(strict=True) != output_parent:
            raise ValueError,"Prequisite proof output parent changed during writing.")
    except (OSError, ValueError) as error:
        print(f"Error: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
