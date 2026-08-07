from __future__ import annotations

import unittest
from copy import deepcopy
from pathlib import Path

from scripts.testing.workbook05.build_contracts import (
    SCHEMA_FILES,
    load_build_schemas,
    validate_build_record,
    validate_build_stage_template,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]


def valid_command_record() -> dict[str, object]:
    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "build-command",
        "command_id": "route-a-runtime-configure",
        "route_id": "route-a-merged-openvino",
        "component": "runtime",
        "executable": "C:/Program Files/CMake/bin/cmake.exe",
        "arguments": ["-S", "C:/w5a/run/ov", "-B", "C:/w5a/run/b-ov"],
        "working_directory": "C:/w5a/run",
        "environment_allowlist": {"RUNNER_NAME": "lenovo-pf4hmd0t-wb05"},
        "started_utc": "2026-08-06T12:00:00Z",
        "ended_utc": "2026-08-06T12:01:00Z",
        "elapsed_seconds": 60.0,
        "exit_code": 0,
        "stdout_path": "commands/route-a-runtime-configure.stdout.log",
        "stderr_path": "commands/route-a-runtime-configure.stderr.log",
    }


def valid_deviation_record() -> dict[str, object]:
    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "build-deviation",
        "deviation_id": "DEV-RA-001",
        "route_id": "route-a-merged-openvino",
        "component": "runtime",
        "document": "docs/dev/build_windows.md",
        "document_commit": "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
        "original_step": "Clone the repository default branch.",
        "proposed_change": "Fetch and detach at the exact approved commit.",
        "reason": "Freeze reproducible source provenance.",
        "risk": "A wrong commit would invalidate comparisons.",
        "approval_status": "Approved",
        "approved_by": "Checkpoint B1",
        "approved_utc": "2026-08-06T15:00:00Z",
        "evidence_paths": ["deviations/DEV-RA-001.json"],
        "executed": True,
    }


def valid_dependency_record() -> dict[str, object]:
    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "build-dependency",
        "route_id": "route-a-merged-openvino",
        "component": "runtime",
        "name": "oneTBB",
        "path": "C:/w5a/run/ov/temp/Windows_AMD64/tbb/bin/tbb12.dll",
        "source": "OpenVINO CMake dependency download",
        "version": "2021.13.1",
        "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "purpose": "CPU runtime threading dependency.",
        "producer_command_id": "route-a-runtime-configure",
    }


def valid_resource_record() -> dict[str, object]:
    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "build-resource-summary",
        "route_id": "route-a-merged-openvino",
        "component": "runtime",
        "command_id": "route-a-runtime-build",
        "sample_interval_seconds": 2,
        "sample_count": 30,
        "peak_working_set_bytes": 1024,
        "peak_private_bytes": 2048,
        "minimum_available_memory_bytes": 4294967296,
        "maximum_commit_percent": 50.0,
        "heartbeat_timeout_seconds": 900,
        "safety_stop_triggered": False,
        "safety_stop_reason": None,
    }


def valid_binary_record() -> dict[str, object]:
    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "build-binary",
        "route_id": "route-a-merged-openvino",
        "component": "runtime",
        "relative_path": "bin/intel64/Release/openvino.dll",
        "size_bytes": 4096,
        "sha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
        "producer_command_id": "route-a-runtime-build",
        "configuration": "Release",
        "copied_to_artifact": False,
    }


def valid_compatibility_record() -> dict[str, object]:
    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "build-compatibility-attempt",
        "route_id": "route-a-merged-openvino",
        "component": "genai",
        "runtime_route_id": "route-a-merged-openvino",
        "runtime_source_commit": "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
        "genai_source_commit": "05e5c7670b597746f858946974d11f38e3baf42f",
        "openvino_config_directory": "C:/w5a/run/i-ov/runtime/cmake",
        "configure_command_id": "route-a-genai-configure",
        "build_command_id": "route-a-genai-build",
        "install_command_id": "route-a-genai-install",
        "status": "Passed",
        "retained": True,
        "reason": "GenAI configured and built against the exact Route A Runtime install.",
        "evidence_paths": ["compatibility/route-a-genai.json"],
    }


def valid_decision_record() -> dict[str, object]:
    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "build-decision",
        "route_id": "route-a-merged-openvino",
        "component": "route",
        "source_commit": "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
        "status": "BuildCandidate",
        "reasons": ["Runtime and GenAI component builds passed independent validation."],
        "required_components": [
            {
                "component": "runtime",
                "status": "Passed",
                "evidence_path": "decisions/route-a-runtime.json",
            },
            {
                "component": "genai",
                "status": "Passed",
                "evidence_path": "decisions/route-a-genai.json",
            },
        ],
        "granite_model_test_authorised": False,
        "activation_claim_authorised": False,
        "packed_storage_claim_authorised": False,
        "performance_claim_authorised": False,
        "quality_claim_authorised": False,
    }


def valid_template() -> dict[str, object]:
    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "workbook_id": "WB-05",
        "workbook_revision": "1.4",
        "phase_id": "phase-2-documented-build",
        "routes": [
            {
                "route_id": "route-a-merged-openvino",
                "enabled": True,
                "prerequisite_status": "Accepted",
                "prerequisite_evidence_path": "phase-1/route-a-source-admission.json",
            },
            {
                "route_id": "route-b-experimental-qjl-polar",
                "enabled": False,
                "prerequisite_status": "Pending",
                "prerequisite_evidence_path": "br8/decision.json",
            },
        ],
        "granite_model_test_authorised": False,
        "activation_claim_authorised": False,
        "packed_storage_claim_authorised": False,
        "performance_claim_authorised": False,
        "quality_claim_authorised": False,
    }


class BuildContractTests(unittest.TestCase):
    def test_load_build_schemas_returns_every_record_type(self) -> None:
        schemas = load_build_schemas(REPOSITORY_ROOT)
        self.assertEqual(set(SCHEMA_FILES), set(schemas))

    def test_valid_records_pass(self) -> None:
        fixtures = {
            "command": valid_command_record(),
            "deviation": valid_deviation_record(),
            "dependency": valid_dependency_record(),
            "resource": valid_resource_record(),
            "binary": valid_binary_record(),
            "compatibility": valid_compatibility_record(),
            "decision": valid_decision_record(),
        }
        for record_type, payload in fixtures.items():
            with self.subTest(record_type=record_type):
                self.assertEqual(
                    [],
                    validate_build_record(record_type, payload, REPOSITORY_ROOT),
                )

    def test_valid_build_template_passes(self) -> None:
        self.assertEqual(
            [],
            validate_build_stage_template(valid_template(), REPOSITORY_ROOT),
        )

    def test_command_record_rejects_shell_string(self) -> None:
        record = valid_command_record()
        record["arguments"] = "--build C:/w5a/run/b-ov"
        errors = validate_build_record("command", record, REPOSITORY_ROOT)
        self.assertIn("arguments must be an array", "\n".join(errors))

    def test_decision_cannot_authorise_later_claims(self) -> None:
        record = valid_decision_record()
        record["granite_model_test_authorised"] = True
        errors = validate_build_record("decision", record, REPOSITORY_ROOT)
        self.assertIn("granite_model_test_authorised must be false", "\n".join(errors))

    def test_executed_deviation_must_be_approved(self) -> None:
        record = valid_deviation_record()
        record["approval_status"] = "Proposed"
        errors = validate_build_record("deviation", record, REPOSITORY_ROOT)
        self.assertIn("unapproved deviation cannot be executed", "\n".join(errors))

    def test_evidence_paths_reject_parent_traversal(self) -> None:
        record = valid_command_record()
        record["stdout_path"] = "../outside.log"
        errors = validate_build_record("command", record, REPOSITORY_ROOT)
        self.assertIn("unsafe evidence path", "\n".join(errors))

    def test_binary_record_rejects_artifact_copy(self) -> None:
        record = valid_binary_record()
        record["copied_to_artifact"] = True
        errors = validate_build_record("binary", record, REPOSITORY_ROOT)
        self.assertIn("copied_to_artifact must be false", "\n".join(errors))

    def test_build_candidate_requires_all_required_components_to_pass(self) -> None:
        record = valid_decision_record()
        required_components = deepcopy(record["required_components"])
        assert isinstance(required_components, list)
        required_components[1]["status"] = "Failed"
        record["required_components"] = required_components
        errors = validate_build_record("decision", record, REPOSITORY_ROOT)
        self.assertIn(
            "BuildCandidate requires every required component to be Passed",
            "\n".join(errors),
        )


if __name__ == "__main__":
    unittest.main()
