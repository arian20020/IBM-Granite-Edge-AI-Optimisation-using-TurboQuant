from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.route_a_runtime_resume_bundle_validation import (
    validate_runtime_resume_bundle,
)


RUN_ID = "31391119557"
RUN_ATTEMPT = 4
WORKSPACE = r"C:\w5a\phase2-31391119557-4"
CACHE_SHA256 = "b5c0606efa261a9525f5562eb973919d508f5242a567f82a46d0b0b9df725c77"
SOURCE_COMMIT = "b9a1f201c109e0bed74763934f79483cf6c4cbf4"
BUILD_DOCUMENT_SHA256 = (
    "1eb445141e72a0f0d5a1186de28bfe2144db500357b860b6a239843c85e5aa65"
)


def _write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(payload, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )


def _write_manifest(root: Path) -> None:
    entries: list[str] = []
    for candidate in sorted(path for path in root.rglob("*") if path.is_file()):
        if candidate.name == "manifest.sha256":
            continue
        relative = candidate.relative_to(root).as_posix()
        digest = hashlib.sha256(candidate.read_bytes()).hexdigest()
        entries.append(f"{digest}  {relative}")
    (root / "manifest.sha256").write_text(
        "\n".join(entries) + "\n",
        encoding="utf-8",
        newline="\n",
    )


def _create_timeout_bundle(root: Path) -> None:
    commands = root / "commands"
    commands.mkdir(parents=True, exist_ok=True)

    _write_json(
        root / "bundle.json",
        {
            "schema_version": "1.0",
            "campaign_id": "GTQ-WB05-MF-v1",
            "route_id": "route-a-merged-openvino",
            "component": "runtime",
            "source_commit": SOURCE_COMMIT,
            "run_id": RUN_ID,
            "run_attempt": RUN_ATTEMPT,
        },
    )
    _write_json(
        root / "environment.json",
        {
            "schema_version": "1.0",
            "campaign_id": "GTQ-WB05-MF-v1",
            "route_id": "route-a-merged-openvino",
            "component": "runtime",
            "work_directory": WORKSPACE,
            "source_directory": WORKSPACE + r"\ov",
            "build_directory": WORKSPACE + r"\b-ov",
            "install_directory": WORKSPACE + r"\i-ov",
            "generator": "Visual Studio 17 2022",
            "platform": "x64",
            "configuration": "Release",
            "parallelism": 1,
            "python": "Python 3.12.10",
        },
    )
    _write_json(
        root / "source-provenance.json",
        {
            "schema_version": "1.0",
            "campaign_id": "GTQ-WB05-MF-v1",
            "route_id": "route-a-merged-openvino",
            "component": "runtime",
            "requested_repository": "https://github.com/openvinotoolkit/openvino.git",
            "actual_repository": "https://github.com/openvinotoolkit/openvino.git",
            "requested_commit": SOURCE_COMMIT,
            "actual_commit": SOURCE_COMMIT,
            "clean_before_configure": True,
            "recursive_submodule_count": 36,
            "recursive_submodules_complete": True,
            "build_document": "docs/dev/build_windows.md",
            "build_document_sha256": BUILD_DOCUMENT_SHA256,
            "source_root": WORKSPACE + r"\ov",
        },
    )
    _write_json(
        root / "cmake-cache-summary.json",
        {
            "schema_version": "1.0",
            "sha256": CACHE_SHA256,
            "values": {
                "CMAKE_GENERATOR": "Visual Studio 17 2022",
                "CMAKE_GENERATOR_PLATFORM": "x64",
                "ENABLE_INTEL_CPU": "ON",
                "ENABLE_INTEL_GPU": "OFF",
                "ENABLE_INTEL_NPU": "OFF",
                "ENABLE_TESTS": "OFF",
                "ENABLE_FUNCTIONAL_TESTS": "OFF",
                "ENABLE_SAMPLES": "OFF",
                "ENABLE_PYTHON": "ON",
                "ENABLE_WHEEL": "OFF",
                "ENABLE_JS": "OFF",
                "ENABLE_OV_IR_FRONTEND": "ON",
                "ENABLE_OV_ONNX_FRONTEND": "ON",
                "ENABLE_OV_PADDLE_FRONTEND": "OFF",
                "ENABLE_OV_TF_FRONTEND": "ON",
                "ENABLE_OV_TF_LITE_FRONTEND": "OFF",
                "ENABLE_OV_PYTORCH_FRONTEND": "OFF",
                "ENABLE_OV_JAX_FRONTEND": "OFF",
                "ENABLE_SYSTEM_PROTOBUF": "OFF",
                "Python3_EXECUTABLE": r"C:\Program Files\Python312\python.exe",
            },
        },
    )
    _write_json(root / "dependencies.json", [])
    _write_json(
        commands / "route-a-runtime-configure.command.json",
        {
            "schema_version": "1.0",
            "campaign_id": "GTQ-WB05-MF-v1",
            "record_type": "build-command",
            "command_id": "route-a-runtime-configure",
            "route_id": "route-a-merged-openvino",
            "component": "runtime",
            "executable": (
                r"C:\Program Files\Microsoft Visual Studio\18\Community"
                r"\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
            ),
            "arguments": [
                "-S",
                WORKSPACE + r"\ov",
                "-B",
                WORKSPACE + r"\b-ov",
            ],
            "working_directory": WORKSPACE,
            "environment_allowlist": {},
            "started_utc": "2026-08-11T12:08:00.0000000Z",
            "ended_utc": "2026-08-11T12:09:00.0000000Z",
            "elapsed_seconds": 60.0,
            "exit_code": 0,
            "stdout_path": "commands/route-a-runtime-configure.stdout.log",
            "stderr_path": "commands/route-a-runtime-configure.stderr.log",
        },
    )
    (commands / "route-a-runtime-configure.stdout.log").write_text(
        "Configure completed.\n", encoding="utf-8", newline="\n"
    )
    (commands / "route-a-runtime-configure.stderr.log").write_text(
        "", encoding="utf-8", newline="\n"
    )
    _write_json(
        commands / "route-a-runtime-configure.resources.json",
        {
            "schema_version": "1.0",
            "campaign_id": "GTQ-WB05-MF-v1",
            "record_type": "build-resource-summary",
            "route_id": "route-a-merged-openvino",
            "component": "runtime",
            "command_id": "route-a-runtime-configure",
            "sample_interval_seconds": 2,
            "sample_count": 5,
            "peak_working_set_bytes": 1024,
            "peak_private_bytes": 1024,
            "minimum_available_memory_bytes": 8 * 1024**3,
            "maximum_commit_percent": 40.0,
            "heartbeat_timeout_seconds": 900,
            "safety_stop_triggered": False,
            "safety_stop_reason": None,
        },
    )
    (commands / "route-a-runtime-build.resources.csv").write_text(
        (
            '"timestamp_utc","root_process_id","process_tree_ids",'
            '"working_set_bytes","private_bytes","available_memory_bytes",'
            '"commit_percent","heartbeat_age_seconds"\n'
            '"2026-08-11T12:10:00Z","1","1;2","1024","1024",'
            '"8589934592","40","0"\n'
        ),
        encoding="utf-8",
        newline="\n",
    )
    _write_manifest(root)


class RouteARuntimeResumeBundleValidationTests(unittest.TestCase):
    def _validate(self, root: Path):
        return validate_runtime_resume_bundle(
            root,
            expected_run_id=RUN_ID,
            expected_run_attempt=RUN_ATTEMPT,
            expected_workspace=WORKSPACE,
            expected_cache_sha256=CACHE_SHA256,
        )

    def _assert_invalid(self, root: Path, expected_reason: str) -> None:
        result = self._validate(root)
        self.assertFalse(result.valid)
        self.assertTrue(
            any(expected_reason.lower() in reason.lower() for reason in result.reasons),
            msg=f"Expected reason containing {expected_reason!r}, found {result.reasons!r}",
        )

    def test_exact_timeout_bundle_is_accepted_for_resume_only(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _create_timeout_bundle(root)

            result = self._validate(root)

            self.assertTrue(result.valid, result.reasons)
            self.assertEqual((), result.reasons)
            self.assertGreater(result.file_count, 0)
            self.assertEqual(result.file_count - 1, result.manifest_entry_count)

    def test_manifest_hash_drift_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _create_timeout_bundle(root)
            (root / "environment.json").write_text(
                "{}\n", encoding="utf-8", newline="\n"
            )

            self._assert_invalid(root, "sha-256")

    def test_wrong_run_attempt_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _create_timeout_bundle(root)
            bundle = json.loads((root / "bundle.json").read_text(encoding="utf-8"))
            bundle["run_attempt"] = 3
            _write_json(root / "bundle.json", bundle)
            _write_manifest(root)

            self._assert_invalid(root, "run attempt")

    def test_wrong_workspace_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _create_timeout_bundle(root)
            environment = json.loads(
                (root / "environment.json").read_text(encoding="utf-8")
            )
            environment["work_directory"] = r"C:\w5a\other-attempt"
            _write_json(root / "environment.json", environment)
            _write_manifest(root)

            self._assert_invalid(root, "workspace")

    def test_wrong_cache_digest_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _create_timeout_bundle(root)
            cache = json.loads(
                (root / "cmake-cache-summary.json").read_text(encoding="utf-8")
            )
            cache["sha256"] = "0" * 64
            _write_json(root / "cmake-cache-summary.json", cache)
            _write_manifest(root)

            self._assert_invalid(root, "cache")

    def test_unexpected_decision_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _create_timeout_bundle(root)
            _write_json(root / "decision.json", {"status": "Passed"})
            _write_manifest(root)

            self._assert_invalid(root, "decision.json")

    def test_completed_build_command_record_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _create_timeout_bundle(root)
            _write_json(
                root / "commands" / "route-a-runtime-build.command.json",
                {"record_type": "build-command"},
            )
            _write_manifest(root)

            self._assert_invalid(root, "build command")

    def test_forbidden_binary_payload_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _create_timeout_bundle(root)
            (root / "payload.dll").write_bytes(b"MZ")
            _write_manifest(root)

            self._assert_invalid(root, "forbidden")

    def test_secret_pattern_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _create_timeout_bundle(root)
            (root / "notes.txt").write_text(
                "ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZ012345\n",
                encoding="utf-8",
                newline="\n",
            )
            _write_manifest(root)

            self._assert_invalid(root, "secret")

    def test_empty_build_resource_trace_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _create_timeout_bundle(root)
            trace = root / "commands" / "route-a-runtime-build.resources.csv"
            trace.write_text("", encoding="utf-8", newline="\n")
            _write_manifest(root)

            self._assert_invalid(root, "resource trace")


if __name__ == "__main__":
    unittest.main()
