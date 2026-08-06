from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.build_bundle_validation import (
    ExpectedBuildBundle,
    main,
    validate_build_bundle,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
ROUTE_A_COMMIT = "b9a1f201c109e0bed74763934f79483cf6c4cbf4"
ROUTE_B_COMMIT = "1827f6458d049de11c1a8203c793af67c99935dc"


def _write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def _command_record(arguments: object | None = None) -> dict[str, object]:
    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "build-command",
        "command_id": "route-a-runtime-configure",
        "route_id": "route-a-merged-openvino",
        "component": "runtime",
        "executable": "C:/Program Files/CMake/bin/cmake.exe",
        "arguments": arguments
        if arguments is not None
        else ["-S", "C:/w5a/run/ov", "-B", "C:/w5a/run/b-ov"],
        "working_directory": "C:/w5a/run",
        "environment_allowlist": {"RUNNER_NAME": "lenovo-pf4hmd0t-wb05"},
        "started_utc": "2026-08-06T12:00:00Z",
        "ended_utc": "2026-08-06T12:01:00Z",
        "elapsed_seconds": 60.0,
        "exit_code": 0,
        "stdout_path": "commands/configure.stdout.log",
        "stderr_path": "commands/configure.stderr.log",
    }


def _binary_record(copied: bool = False) -> dict[str, object]:
    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "build-binary",
        "route_id": "route-a-merged-openvino",
        "component": "runtime",
        "relative_path": "bin/intel64/Release/openvino.dll",
        "size_bytes": 4096,
        "sha256": "b" * 64,
        "producer_command_id": "route-a-runtime-build",
        "configuration": "Release",
        "copied_to_artifact": copied,
    }


def _decision(
    *,
    route_id: str = "route-a-merged-openvino",
    source_commit: str = ROUTE_A_COMMIT,
    status: str = "Passed",
    component: str = "runtime",
    authorise_model: bool = False,
    required_status: str = "Passed",
) -> dict[str, object]:
    required_components: list[dict[str, object]] = []
    if component == "route":
        required_components = [
            {
                "component": "runtime",
                "status": required_status,
                "evidence_path": "decisions/runtime.json",
            }
        ]
    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "build-decision",
        "route_id": route_id,
        "component": component,
        "source_commit": source_commit,
        "status": status,
        "reasons": ["Evidence retained for independent review."],
        "required_components": required_components,
        "granite_model_test_authorised": authorise_model,
        "activation_claim_authorised": False,
        "packed_storage_claim_authorised": False,
        "performance_claim_authorised": False,
        "quality_claim_authorised": False,
    }


def _deviation(approved: bool) -> dict[str, object]:
    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "build-deviation",
        "deviation_id": "DEV-RA-001",
        "route_id": "route-a-merged-openvino",
        "component": "runtime",
        "document": "docs/dev/build_windows.md",
        "document_commit": ROUTE_A_COMMIT,
        "original_step": "Clone the default branch.",
        "proposed_change": "Detach at the exact commit.",
        "reason": "Reproducible source provenance.",
        "risk": "A wrong commit invalidates comparisons.",
        "approval_status": "Approved" if approved else "Proposed",
        "approved_by": "Checkpoint B1" if approved else None,
        "approved_utc": "2026-08-06T15:00:00Z" if approved else None,
        "evidence_paths": ["deviations/DEV-RA-001.json"],
        "executed": True,
    }


def _write_manifest(root: Path) -> None:
    lines: list[str] = []
    for candidate in sorted(path for path in root.rglob("*") if path.is_file()):
        relative = candidate.relative_to(root).as_posix()
        if relative == "manifest.sha256":
            continue
        digest = hashlib.sha256(candidate.read_bytes()).hexdigest()
        lines.append(f"{digest}  {relative}")
    (root / "manifest.sha256").write_text("\n".join(lines) + "\n", encoding="utf-8")


def _create_valid_bundle(
    root: Path,
    *,
    route_id: str = "route-a-merged-openvino",
    source_commit: str = ROUTE_A_COMMIT,
    br8: object = None,
) -> None:
    metadata = {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "route_id": route_id,
        "component": "runtime",
        "source_commit": source_commit,
        "run_id": "31099999999",
        "run_attempt": 1,
        "br8_prerequisite": br8,
    }
    _write_json(root / "bundle.json", metadata)
    command = _command_record()
    command["route_id"] = route_id
    _write_json(root / "commands/configure.command.json", command)
    (root / "commands/configure.stdout.log").write_text("configured\n", encoding="utf-8")
    (root / "commands/configure.stderr.log").write_text("", encoding="utf-8")
    binary = _binary_record()
    binary["route_id"] = route_id
    _write_json(root / "binaries/runtime.json", binary)
    _write_json(
        root / "decision.json",
        _decision(route_id=route_id, source_commit=source_commit),
    )
    _write_manifest(root)


def _expected(
    *,
    route_id: str = "route-a-merged-openvino",
    source_commit: str = ROUTE_A_COMMIT,
) -> ExpectedBuildBundle:
    return ExpectedBuildBundle(
        route_id=route_id,
        component="runtime",
        source_commit=source_commit,
        run_id="31099999999",
        run_attempt=1,
    )


def _codes(issues: list[object]) -> set[str]:
    return {getattr(issue, "code") for issue in issues}


class BuildBundleValidationTests(unittest.TestCase):
    def test_valid_bundle_passes(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _create_valid_bundle(root)
            self.assertEqual([], validate_build_bundle(root, _expected(), REPOSITORY_ROOT))

    def test_manifest_hash_mismatch_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _create_valid_bundle(root)
            (root / "decision.json").write_text("{}\n", encoding="utf-8")
            self.assertIn("HASH_MISMATCH", _codes(validate_build_bundle(root, _expected(), REPOSITORY_ROOT)))

    def test_missing_command_log_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _create_valid_bundle(root)
            (root / "commands/configure.stderr.log").unlink()
            _write_manifest(root)
            self.assertIn("COMMAND_LOG_MISSING", _codes(validate_build_bundle(root, _expected(), REPOSITORY_ROOT)))

    def test_uppercase_executable_payload_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _create_valid_bundle(root)
            (root / "payload.EXE").write_bytes(b"MZ")
            _write_manifest(root)
            self.assertIn("FORBIDDEN_PAYLOAD", _codes(validate_build_bundle(root, _expected(), REPOSITORY_ROOT)))

    def test_secret_pattern_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _create_valid_bundle(root)
            (root / "commands/configure.stdout.log").write_text("ghp_example_secret\n", encoding="utf-8")
            _write_manifest(root)
            self.assertIn("SECRET_PATTERN", _codes(validate_build_bundle(root, _expected(), REPOSITORY_ROOT)))

    def test_wrong_route_or_source_identity_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _create_valid_bundle(root)
            issues = validate_build_bundle(root, _expected(route_id="route-b-experimental-qjl-polar", source_commit=ROUTE_B_COMMIT), REPOSITORY_ROOT)
            self.assertIn("IDENTITY_MISMATCH", _codes(issues))

    def test_shell_string_arguments_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _create_valid_bundle(root)
            _write_json(root / "commands/configure.command.json", _command_record("--build C:/w5a/run/b-ov"))
            _write_manifest(root)
            self.assertIn("RECORD_INVALID", _codes(validate_build_bundle(root, _expected(), REPOSITORY_ROOT)))

    def test_unsafe_json_evidence_path_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _create_valid_bundle(root)
            command = _command_record()
            command["stdout_path"] = "../outside.log"
            _write_json(root / "commands/configure.command.json", command)
            _write_manifest(root)
            self.assertIn("UNSAFE_EVIDENCE_PATH", _codes(validate_bundle(root, _expected(), REPOSITORY_ROOT)))

    def test_executed_unapproved_deviation_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _create_valid_bundle(root)
            _write_json(root / "deviations/DEV-RA-001.json", _deviation(False))
            _write_manifest(root)
            self.assertIn("RECORD_INVALID", _codes(validate_build_bundle(root, _expected(), REPOSITORY_ROOT)))

    def test_binary_record_cannot_claim_artifact_copy(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _create_valid_bundle(root)
            _write_json(root / "binaries/runtime.json", _binary_record(True))
            _write_manifest(root)
            self.assertIn("RECORD_INVALID", _codes(validate_build_bundle(root, _expected(), REPOSITORY_ROOT)))

    def test_build_candidate_with_failed_component_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _create_valid_bundle(root)
            _write_json(root / "decision.json", _decision(status="BuildCandidate", component="route", required_status="Failed"))
            _write_manifest(root)
            self.assertIn("RECORD_INVALID", _codes(validate_bundle(root, _expected(), REPOSITORY_ROOT)))

    def test_route_b_requires_accepted_br8_prerequisite(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _create_valid_bundle(root, route_id="route-b-experimental-qjl-polar", source_commit=ROUTE_B_COMMIT)
            issues = validate_build_bundle(root, _expected(route_id="route-b-experimental-qjl-polar", source_commit=ROUTE_B_COMMIT), REPOSITORY_ROOT)
            self.assertIn("BR8_PREREQUISITE_INVALID", _codes(issues))

    def test_true_claim_authorisation_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _create_valid_bundle(root)
            _write_json(root / "decision.json", _decision(authorise_model=True))
            _write_manifest(root)
            self.assertIn("RECORD_INVALID", _codes(validate_build_bundle(root, _expected(), REPOSITORY_ROOT)))

    def test_cli_always_writes_markdown_report(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / "bundle"
            root.mkdir()
            _create_valid_bundle(root)
            report = Path(directory) / "report.md"
            exit_code = main([
                "--bundle", str(root),
                "--repository-root", str(REPOSITORY_ROOT),
                "--route-id", "route-a-merged-openvino",
                "--component", "runtime",
                "--source-commit", ROUTE_A_COMMIT,
                "--run-id", "31099999999",
                "--run-attempt", "1",
                "--report", str(report),
            ])
            self.assertEqual(0, exit_code)
            self.assertTrue(report.is_file())
            self.assertIn("Validation passed", report.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
