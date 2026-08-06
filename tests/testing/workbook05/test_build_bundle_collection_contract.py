from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.build_bundle_validation import (
    ExpectedBuildBundle,
    validate_build_bundle,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
ROUTE_A_COMMIT = "b9a1f201c109e0bed74763934f79483cf6c4cbf4"


def _write_json(path: Path, value: object) -> None:
    # Keep fixtures deterministic and ordinary UTF-8 text, matching real bundles.
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def _write_manifest(root: Path) -> None:
    # Recreate the production artifact contract: every file except the manifest
    # itself is covered by one lowercase SHA-256 entry.
    lines: list[str] = []
    for candidate in sorted(path for path in root.rglob("*") if path.is_file()):
        relative = candidate.relative_to(root).as_posix()
        if relative == "manifest.sha256":
            continue
        digest = hashlib.sha256(candidate.read_bytes()).hexdigest()
        lines.append(f"{digest}  {relative}")
    (root / "manifest.sha256").write_text(
        "\n".join(lines) + "\n",
        encoding="utf-8",
    )


def _decision() -> dict[str, object]:
    # This is the smallest valid Route A Runtime build-only decision.
    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "build-decision",
        "route_id": "route-a-merged-openvino",
        "component": "runtime",
        "source_commit": ROUTE_A_COMMIT,
        "status": "Passed",
        "reasons": ["Fixture retains only build evidence."],
        "required_components": [],
        "granite_model_test_authorised": False,
        "activation_claim_authorised": False,
        "packed_storage_claim_authorised": False,
        "performance_claim_authorised": False,
        "quality_claim_authorised": False,
    }


def _binary_record(*, copied_to_artifact: bool) -> dict[str, object]:
    # Real orchestrators write binaries.json as an array of records. The invalid
    # copied flag gives the schema validator one deterministic defect to detect.
    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "build-binary",
        "route_id": "route-a-merged-openvino",
        "component": "runtime",
        "relative_path": "bin/intel64/Release/openvino.dll",
        "size_bytes": 4096,
        "sha256": "b" * 64,
        "producer_command_id": "route-a-runtime-install",
        "configuration": "Release",
        "copied_to_artifact": copied_to_artifact,
    }


def _expected() -> ExpectedBuildBundle:
    return ExpectedBuildBundle(
        route_id="route-a-merged-openvino",
        component="runtime",
        source_commit=ROUTE_A_COMMIT,
        run_id="31100000000",
        run_attempt=1,
    )


def _create_minimal_bundle(root: Path) -> None:
    # Only bundle.json, decision.json, and manifest.sha256 are globally required.
    _write_json(
        root / "bundle.json",
        {
            "schema_version": "1.0",
            "campaign_id": "GTQ-WB05-MF-v1",
            "route_id": "route-a-merged-openvino",
            "component": "runtime",
            "source_commit": ROUTE_A_COMMIT,
            "run_id": "31100000000",
            "run_attempt": 1,
        },
    )
    _write_json(root / "decision.json", _decision())
    _write_manifest(root)


def _codes(issues: list[object]) -> set[str]:
    return {getattr(issue, "code") for issue in issues}


class BuildBundleCollectionContractTests(unittest.TestCase):
    """Exercise the real array and machine-path shapes emitted by Phase 2."""

    def test_binary_record_array_is_schema_validated(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _create_minimal_bundle(root)

            # The current defect is that a top-level JSON list is skipped before
            # record_type/schema validation. This invalid element must be caught.
            _write_json(
                root / "binaries.json",
                [_binary_record(copied_to_artifact=True)],
            )
            _write_manifest(root)

            issues = validate_build_bundle(root, _expected(), REPOSITORY_ROOT)
            self.assertIn("RECORD_INVALID", _codes(issues))

    def test_machine_tool_paths_are_not_artifact_evidence_paths(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _create_minimal_bundle(root)

            # Reproducibility metadata legitimately records absolute machine tool
            # locations. They are data, not paths that should resolve inside the
            # uploaded evidence artifact.
            _write_json(
                root / "environment.json",
                {
                    "schema_version": "1.0",
                    "campaign_id": "GTQ-WB05-MF-v1",
                    "route_id": "route-a-merged-openvino",
                    "component": "runtime",
                    "python_path": "C:/Program Files/Python312/python.exe",
                    "git_path": "C:/Program Files/Git/cmd/git.exe",
                    "cmake_path": "C:/Program Files/Microsoft Visual Studio/CMake/bin/cmake.exe",
                },
            )
            _write_manifest(root)

            issues = validate_build_bundle(root, _expected(), REPOSITORY_ROOT)
            self.assertNotIn("UNSAFE_EVIDENCE_PATH", _codes(issues))


if __name__ == "__main__":
    unittest.main()
