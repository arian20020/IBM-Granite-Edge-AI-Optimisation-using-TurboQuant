from __future__ import annotations

import json
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch

from scripts.testing.workbook05.hash_manifest import write_hash_manifest
from scripts.testing.workbook05.phase3.c1_resume import ValidationResult
from scripts.testing.workbook05.phase3.c1_resume_bundle_validation import (
    RESUME_REQUIRED_PATHS,
    RESUME_STAGE_ORDER,
    validate_c1_resume_bundle,
)
from tests.testing.workbook05.test_phase3_live_asset_bundle_validation import (
    _create_live_bundle,
    _sha256,
    _write_json,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
PRIOR_RUN_ID = "32410714130"
PRIOR_ARTIFACT_NAME = "workbook-05-phase3-assets-32410714130-1"
PRIOR_ARTIFACT_DIGEST = (
    "sha256:33d3ca32236d6da973c21c5d95ba36d088c5ed7384d93eb3f353dbd081671257"
)
PRIOR_HEAD_SHA = "80946fc2e06767a8aeff878deab7d31f10fd6676"
REVISION = "c" * 40
SOURCE_DIRECTORY = r"C:\w5m\sources\granite41-3b-synthetic"
CONVERTED_DIRECTORY = (
    r"C:\w5m\converted\granite41-3b-int4a-g128-r100-synthetic-resume-1-1"
)
MODEL_SHA256 = "a" * 64
TOKENIZER_SHA256 = "b" * 64
CONVERSION_MINIMUM_FREE_BYTES = 20 * 1024 * 1024 * 1024


def _refresh_manifest(bundle: Path) -> None:
    """Rebind the complete synthetic text-only artifact after each mutation."""

    write_hash_manifest(bundle, bundle / "manifest.sha256")


def _create_resume_bundle(bundle: Path) -> str:
    """Promote the existing valid live fixture to one valid resume-shaped bundle."""

    _create_live_bundle(bundle)
    decision_digest = _sha256(bundle / "dependency" / "decision.json")

    # Rebind the standalone source record to the synthetic resume identity.
    resolved_path = bundle / "resolved-model.json"
    resolved = json.loads(resolved_path.read_text(encoding="utf-8"))
    resolved.update(
        {
            "repository": "ibm-granite/granite-4.1-3b",
            "requested_revision": "main",
            "resolved_revision": REVISION,
            "source_directory": SOURCE_DIRECTORY,
            "aggregate_model_sha256": MODEL_SHA256,
            "aggregate_tokenizer_sha256": TOKENIZER_SHA256,
        }
    )
    _write_json(resolved_path, resolved)

    # Keep asset and conversion records mutually consistent and resume-scoped.
    asset_path = bundle / "asset-lock.json"
    asset = json.loads(asset_path.read_text(encoding="utf-8"))
    asset["source"].update(
        {
            "repository": "ibm-granite/granite-4.1-3b",
            "requested_revision": "main",
            "resolved_revision": REVISION,
        }
    )
    asset.update(
        {
            "source_directory": SOURCE_DIRECTORY,
            "converted_directory": CONVERTED_DIRECTORY,
            "aggregate_model_sha256": MODEL_SHA256,
            "aggregate_tokenizer_sha256": TOKENIZER_SHA256,
        }
    )
    _write_json(asset_path, asset)

    conversion_path = bundle / "conversion-record.json"
    conversion = json.loads(conversion_path.read_text(encoding="utf-8"))
    conversion["source"].update(
        {
            "repository": "ibm-granite/granite-4.1-3b",
            "resolved_revision": REVISION,
            "source_directory": SOURCE_DIRECTORY,
            "aggregate_model_sha256": MODEL_SHA256,
            "aggregate_tokenizer_sha256": TOKENIZER_SHA256,
        }
    )
    conversion["dependency_preflight"] = {
        "status": "Passed",
        "record_path": "dependency/decision.json",
        "record_sha256": decision_digest,
    }
    conversion["conversion"]["arguments"] = [
        "export",
        "openvino",
        "--model",
        SOURCE_DIRECTORY,
        "--task",
        "text-generation-with-past",
        "--weight-format",
        "int4",
        "--group-size",
        "128",
        "--ratio",
        "1.0",
        CONVERTED_DIRECTORY,
    ]
    conversion["output"]["directory"] = CONVERTED_DIRECTORY
    _write_json(conversion_path, conversion)

    command_path = bundle / "commands" / "conversion.json"
    command = json.loads(command_path.read_text(encoding="utf-8"))
    command.update(
        {
            "arguments": conversion["conversion"]["arguments"],
            "exit_code": 0,
            "safety_stop_triggered": False,
        }
    )
    _write_json(command_path, command)

    # The accepted dependency proof must bind the copied decision bytes.
    proof_path = bundle / "dependency-acceptance-proof.json"
    proof = json.loads(proof_path.read_text(encoding="utf-8"))
    proof["decision_sha256"] = decision_digest
    _write_json(proof_path, proof)

    _write_json(
        bundle / "prior-artifact-identity.json",
        {
            "status": "Passed",
            "prior_run_id": PRIOR_RUN_ID,
            "prior_run_attempt": 1,
            "prior_artifact_name": PRIOR_ARTIFACT_NAME,
            "prior_artifact_digest": PRIOR_ARTIFACT_DIGEST,
            "prior_head_sha": PRIOR_HEAD_SHA,
            "model_execution_authorised": False,
            "activation_claim_authorised": False,
            "packed_storage_claim_authorised": False,
            "performance_claim_authorised": False,
            "quality_claim_authorised": False,
        },
    )
    _write_json(
        bundle / "prior-attempt-validation.json",
        {
            "status": "Passed",
            "prior_run_id": PRIOR_RUN_ID,
            "prior_run_attempt": 1,
            "prior_artifact_name": PRIOR_ARTIFACT_NAME,
            "prior_artifact_digest": PRIOR_ARTIFACT_DIGEST,
            "prior_head_sha": PRIOR_HEAD_SHA,
            "repository": "ibm-granite/granite-4.1-3b",
            "resolved_revision": REVISION,
            "source_directory": SOURCE_DIRECTORY,
            "aggregate_model_sha256": MODEL_SHA256,
            "aggregate_tokenizer_sha256": TOKENIZER_SHA256,
            "model_execution_authorised": False,
            "activation_claim_authorised": False,
            "packed_storage_claim_authorised": False,
            "performance_claim_authorised": False,
            "quality_claim_authorised": False,
        },
    )
    _write_json(
        bundle / "retained-source-proof.json",
        {
            "status": "Passed",
            "prior_run_id": PRIOR_RUN_ID,
            "repository": "ibm-granite/granite-4.1-3b",
            "resolved_revision": REVISION,
            "source_directory": SOURCE_DIRECTORY,
            "aggregate_model_sha256": MODEL_SHA256,
            "aggregate_tokenizer_sha256": TOKENIZER_SHA256,
            "source_reused_read_only": True,
            "prior_partial_conversion_reused": False,
            "model_execution_authorised": False,
            "activation_claim_authorised": False,
            "packed_storage_claim_authorised": False,
            "performance_claim_authorised": False,
            "quality_claim_authorised": False,
        },
    )
    _write_json(
        bundle / "resource-preflight.json",
        {
            "status": "Passed",
            "available_memory_bytes": 4_294_967_296,
            "minimum_available_memory_bytes": 4_294_967_296,
            "commit_percent": 60,
            "maximum_commit_percent": 70,
            "conflicting_processes": [],
            "model_execution_authorised": False,
            "activation_claim_authorised": False,
            "packed_storage_claim_authorised": False,
            "performance_claim_authorised": False,
            "quality_claim_authorised": False,
        },
    )

    disk_path = bundle / "disk-preflight.json"
    disk = json.loads(disk_path.read_text(encoding="utf-8"))
    disk.update(
        {
            "status": "Passed",
            "free_bytes": CONVERSION_MINIMUM_FREE_BYTES,
            "minimum_free_bytes_for_conversion_resume": (
                CONVERSION_MINIMUM_FREE_BYTES
            ),
            "source_download_required": False,
            "source_download_authorised": False,
            "deletion_authorised": False,
            "deletion_performed": False,
            "prior_failed_workspace_preserved": True,
            "prior_partial_conversion_preserved": True,
        }
    )
    _write_json(disk_path, disk)
    _write_json(
        bundle / "stage-order.json",
        {
            "status": "Passed",
            "operation": "controlled-source-resume",
            "stages": list(RESUME_STAGE_ORDER),
            "model_execution_authorised": False,
            "activation_claim_authorised": False,
            "packed_storage_claim_authorised": False,
            "performance_claim_authorised": False,
            "quality_claim_authorised": False,
        },
    )

    # The hosted validator only reads these prior members as data; the detailed
    # prior validator is patched below so this unit test stays small and focused.
    prior = bundle / "prior-attempt"
    _write_json(prior / "failure.json", {"status": "Failed"})
    _write_json(prior / "resolved-model.json", {"record_type": "resolved-model"})
    (prior / "source-files.csv").write_text(
        "relative_path,size_bytes,sha256\n",
        encoding="utf-8",
        newline="\n",
    )
    _refresh_manifest(bundle)
    return decision_digest


class Phase3C1ResumeHostedValidationTests(unittest.TestCase):
    def _issues(self, mutate=None) -> list:
        with TemporaryDirectory() as directory:
            bundle = Path(directory) / "bundle"
            bundle.mkdir()
            decision_digest = _create_resume_bundle(bundle)
            if mutate is not None:
                mutate(bundle)
                _refresh_manifest(bundle)
            with (
                patch(
                    "scripts.testing.workbook05.phase3.c1_resume_bundle_validation."
                    "EXPECTED_REVISION",
                    REVISION,
                ),
                patch(
                    "scripts.testing.workbook05.phase3.c1_resume_bundle_validation."
                    "EXPECTED_SOURCE_DIRECTORY",
                    SOURCE_DIRECTORY,
                ),
                patch(
                    "scripts.testing.workbook05.phase3.c1_resume_bundle_validation."
                    "EXPECTED_MODEL_SHA256",
                    MODEL_SHA256,
                ),
                patch(
                    "scripts.testing.workbook05.phase3.c1_resume_bundle_validation."
                    "EXPECTED_TOKENIZER_SHA256",
                    TOKENIZER_SHA256,
                ),
                patch(
                    "scripts.testing.workbook05.phase3.c1_resume_bundle_validation."
                    "EXPECTED_DEPENDENCY_DECISION_SHA256",
                    decision_digest,
                ),
                patch(
                    "scripts.testing.workbook05.phase3.c1_resume_bundle_validation."
                    "validate_prior_failure_bundle",
                    return_value=ValidationResult(valid=True, issues=()),
                ),
            ):
                return validate_c1_resume_bundle(bundle, REPOSITORY_ROOT)

    def test_complete_resume_shaped_bundle_passes(self) -> None:
        self.assertEqual([], self._issues())

    def test_resume_validator_requires_every_resume_specific_record(self) -> None:
        for relative in RESUME_REQUIRED_PATHS:
            with self.subTest(relative=relative):
                def remove(bundle: Path, relative: str = relative) -> None:
                    (bundle / relative).unlink()

                issues = self._issues(remove)
                self.assertTrue(
                    any(
                        issue.code == "REQUIRED_PATH_MISSING"
                        and issue.path == relative
                        for issue in issues
                    )
                )

    def test_partial_output_reuse_and_true_claim_are_rejected(self) -> None:
        def partial_output(bundle: Path) -> None:
            path = bundle / "commands" / "conversion.json"
            command = json.loads(path.read_text(encoding="utf-8"))
            command["arguments"][-1] = (
                r"C:\w5m\converted\granite41-3b-int4a-g128-r100-c0650403"
            )
            _write_json(path, command)

        def true_claim(bundle: Path) -> None:
            path = bundle / "retained-source-proof.json"
            proof = json.loads(path.read_text(encoding="utf-8"))
            proof["performance_claim_authorised"] = True
            _write_json(path, proof)

        for mutate, code in (
            (partial_output, "COMMAND_IDENTITY"),
            (true_claim, "SCIENTIFIC_CLAIM"),
        ):
            with self.subTest(code=code):
                self.assertIn(code, {issue.code for issue in self._issues(mutate)})


if __name__ == "__main__":
    unittest.main()
