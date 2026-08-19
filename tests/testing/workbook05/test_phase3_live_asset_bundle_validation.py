from __future__ import annotations

import copy
import hashlib
import json
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

from scripts.testing.workbook05.hash_manifest import write_hash_manifest
from scripts.testing.workbook05.phase3.live_asset_bundle_validation import (
    validate_live_asset_bundle,
)
from tests.testing.workbook05.test_phase3_asset_bundle_validation import (
    _create_valid_bundle,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]

LIVE_REQUIRED_PATHS = (
    "dependency-acceptance-proof.json",
    "dependency/decision.json",
    "resolved-model.json",
    "stage-order.json",
)

CLAIM_FLAGS = (
    "model_download_authorised",
    "granite_model_test_authorised",
    "activation_claim_authorised",
    "packed_storage_claim_authorised",
    "performance_claim_authorised",
    "quality_claim_authorised",
)


def _write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _refresh_manifest(bundle: Path) -> None:
    write_hash_manifest(bundle, bundle / "manifest.sha256")


def _create_live_bundle(bundle: Path) -> None:
    """Promote the existing model-free fixture to the live C1 evidence shape."""

    _create_valid_bundle(bundle)

    decision = {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "dependency-preflight-decision",
        "route_id": "route-a-merged-openvino",
        "status": "Passed",
    }
    decision.update({flag: False for flag in CLAIM_FLAGS})
    decision_path = bundle / "dependency" / "decision.json"
    _write_json(decision_path, decision)
    decision_sha256 = _sha256(decision_path)

    proof = {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "dependency-acceptance-proof",
        "route_id": "route-a-merged-openvino",
        "status": "Passed",
        "workflow_run_id": "32211117536",
        "run_attempt": 1,
        "artifact_id": "9350956534",
        "artifact_sha256": (
            "b68a4f8af8c57a9f5d71347d2485855796f4d0dce0291c50b596512c309c0c21"
        ),
        "decision_sha256": decision_sha256,
        "workspace_root": r"C:\w5c\dependency-preflight-32211117536-1",
        "evidence_root": (
            r"C:\w5c\dependency-preflight-32211117536-1\evidence"
        ),
        "python_executable_path": (
            r"C:\w5c\dependency-preflight-32211117536-1"
            r"\workspace\environment\Scripts\python.exe"
        ),
        "python_executable_sha256": "a" * 64,
        "optimum_cli_path": (
            r"C:\w5c\dependency-preflight-32211117536-1"
            r"\workspace\environment\Scripts\optimum-cli.exe"
        ),
        "optimum_cli_sha256": "b" * 64,
        "optimum_commit": "982e495540364f95da1e4b6f62d2d4e5907d08fd",
        "optimum_intel_commit": "a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0",
        "decision_status": "Passed",
    }
    proof.update({flag: False for flag in CLAIM_FLAGS})
    _write_json(bundle / "dependency-acceptance-proof.json", proof)

    asset_path = bundle / "asset-lock.json"
    asset = json.loads(asset_path.read_text(encoding="utf-8"))
    resolved = {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "resolved-model",
        "route_id": "route-a-merged-openvino",
        "repository": asset["source"]["repository"],
        "requested_revision": asset["source"]["requested_revision"],
        "resolved_revision": asset["source"]["resolved_revision"],
        "license": asset["source"]["license"],
        "source_directory": asset["source_directory"],
        "declared_model_metadata": copy.deepcopy(asset["declared_model_metadata"]),
        "model_files": copy.deepcopy(asset["source_files"]),
        "tokenizer_files": copy.deepcopy(asset["tokenizer_files"]),
        "aggregate_model_sha256": asset["aggregate_model_sha256"],
        "aggregate_tokenizer_sha256": asset["aggregate_tokenizer_sha256"],
    }
    resolved.update({flag: False for flag in CLAIM_FLAGS})
    _write_json(bundle / "resolved-model.json", resolved)

    conversion_path = bundle / "conversion-record.json"
    conversion = json.loads(conversion_path.read_text(encoding="utf-8"))
    conversion["dependency_preflight"] = {
        "status": "Passed",
        "record_path": "dependency/decision.json",
        "record_sha256": decision_sha256,
    }
    _write_json(conversion_path, conversion)

    _write_json(
        bundle / "stage-order.json",
        {
            "schema_version": "1.0",
            "campaign_id": "GTQ-WB05-MF-v1",
            "route_id": "route-a-merged-openvino",
            "run_id": "32299999999",
            "run_attempt": 1,
            "stages": [
                "prerequisite-verification",
                "path-root-verification",
                "disk-preflight",
                "immutable-revision-resolution",
                "source-snapshot-download",
                "source-file-hash-inventory",
                "conversion-new-output-directory",
                "converted-file-hash-inventory",
                "schema-validation",
                "manifest-generation",
            ],
            "status": "Passed",
        },
    )
    _refresh_manifest(bundle)


class Phase3LiveAssetBundleValidationTests(unittest.TestCase):
    def _issues_after(self, mutate) -> list:
        with TemporaryDirectory() as directory:
            bundle = Path(directory) / "bundle"
            bundle.mkdir()
            _create_live_bundle(bundle)
            mutate(bundle)
            return validate_live_asset_bundle(bundle, REPOSITORY_ROOT)

    def test_complete_live_bundle_passes(self) -> None:
        self.assertEqual([], self._issues_after(lambda bundle: None))

    def test_live_bundle_requires_every_binding_and_stage_record(self) -> None:
        for relative in LIVE_REQUIRED_PATHS:
            with self.subTest(relative=relative):
                def remove(bundle: Path, relative: str = relative) -> None:
                    (bundle / relative).unlink()
                    _refresh_manifest(bundle)

                issues = self._issues_after(remove)
                self.assertTrue(
                    any(
                        issue.code == "REQUIRED_PATH_MISSING"
                        and issue.path == relative
                        for issue in issues
                    ),
                    msg=f"Missing live C1 evidence was not rejected: {relative}",
                )

    def test_live_bundle_rehashes_the_bound_dependency_decision(self) -> None:
        def mutate(bundle: Path) -> None:
            decision_path = bundle / "dependency" / "decision.json"
            decision = json.loads(decision_path.read_text(encoding="utf-8"))
            decision["status"] = "Changed"
            _write_json(decision_path, decision)
            _refresh_manifest(bundle)

        issues = self._issues_after(mutate)
        self.assertTrue(
            any(issue.code == "DEPENDENCY_DECISION_MISMATCH" for issue in issues),
            msg="Changed dependency decision bytes were not rejected.",
        )

    def test_live_bundle_binds_resolved_model_to_the_asset_record(self) -> None:
        def mutate(bundle: Path) -> None:
            path = bundle / "resolved-model.json"
            resolved = json.loads(path.read_text(encoding="utf-8"))
            resolved["resolved_revision"] = "f" * 40
            _write_json(path, resolved)
            _refresh_manifest(bundle)

        issues = self._issues_after(mutate)
        self.assertTrue(
            any(issue.code == "RESOLVED_MODEL_RELATIONSHIP" for issue in issues),
            msg="Resolved-model drift was not rejected.",
        )


if __name__ == "__main__":
    unittest.main()
