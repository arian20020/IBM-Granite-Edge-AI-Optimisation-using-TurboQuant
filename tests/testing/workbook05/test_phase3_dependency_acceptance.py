from __future__ import annotations

import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.dependency_acceptance import (
    load_dependency_acceptance,
    verify_dependency_acceptance_identity,
    verify_retained_dependency_acceptance,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
ACCEPTANCE_PATH = (
    REPOSITORY_ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "configurations"
    / "workbook05"
    / "accepted-dependency-preflight.json"
)

CLAIM_FLAGS = (
    "model_download_authorised",
    "granite_model_test_authorised",
    "activation_claim_authorised",
    "packed_storage_claim_authorised",
    "performance_claim_authorised",
    "quality_claim_authorised",
)


class _BundleIssue:
    def __init__(self, code: str, path: str, message: str) -> None:
        self.code = code
        self.path = path
        self.message = message


class Phase3DependencyAcceptanceTests(unittest.TestCase):
    def test_committed_acceptance_binds_the_independently_verified_run(self) -> None:
        record = load_dependency_acceptance(ACCEPTANCE_PATH, REPOSITORY_ROOT)

        self.assertEqual(
            "c417efd936a7fa2e871b689065b2f3b88636c1a1",
            record["repository_commit"],
        )
        self.assertEqual(32211117536, record["workflow"]["run_id"])
        self.assertEqual(1, record["workflow"]["run_attempt"])
        self.assertEqual(
            "workbook-05-phase3-dependency-preflight-32211117536-1",
            record["artifact"]["name"],
        )
        self.assertEqual(9350956534, record["artifact"]["github_artifact_id"])
        self.assertEqual(
            "b68a4f8af8c57a9f5d71347d2485855796f4d0dce0291c50b596512c309c0c21",
            record["artifact"]["github_sha256"],
        )
        self.assertEqual(
            record["artifact"]["github_sha256"],
            record["artifact"]["independent_sha256"],
        )
        self.assertEqual(
            "429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49",
            record["decision"]["sha256"],
        )
        self.assertEqual("Passed", record["decision"]["status"])
        self.assertEqual(
            r"C:\w5c\dependency-preflight-32211117536-1",
            record["retained_workspace"]["path"],
        )
        self.assertTrue(record["owner_acceptance"]["accepted"])
        self.assertEqual("arian20020", record["owner_acceptance"]["accepted_by"])
        for flag in CLAIM_FLAGS:
            self.assertIs(record[flag], False)

        verify_dependency_acceptance_identity(
            record,
            record["decision"]["sha256"],
        )

    def test_expected_decision_digest_must_match_the_committed_acceptance(self) -> None:
        record = load_dependency_acceptance(ACCEPTANCE_PATH, REPOSITORY_ROOT)

        with self.assertRaisesRegex(
            ValueError,
            "expected dependency decision SHA-256",
        ):
            verify_dependency_acceptance_identity(record, "0" * 64)

    def test_github_and_independent_artifact_digests_must_agree(self) -> None:
        record = load_dependency_acceptance(ACCEPTANCE_PATH, REPOSITORY_ROOT)
        changed = copy.deepcopy(record)
        changed["artifact"]["independent_sha256"] = "0" * 64

        with self.assertRaisesRegex(ValueError, "artifact digests differ"):
            verify_dependency_acceptance_identity(
                changed,
                changed["decision"]["sha256"],
            )

    def test_workspace_identity_must_match_the_run_and_attempt(self) -> None:
        record = load_dependency_acceptance(ACCEPTANCE_PATH, REPOSITORY_ROOT)
        changed = copy.deepcopy(record)
        changed["retained_workspace"]["path"] = (
            r"C:\w5c\dependency-preflight-32211117536-2"
        )

        with self.assertRaisesRegex(ValueError, "workspace identity"):
            verify_dependency_acceptance_identity(
                changed,
                changed["decision"]["sha256"],
            )

    def test_retained_workspace_is_revalidated_before_model_access(self) -> None:
        record = load_dependency_acceptance(ACCEPTANCE_PATH, REPOSITORY_ROOT)

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            workspace = root / "dependency-preflight-32211117536-1"
            evidence = workspace / "evidence"
            environment = workspace / "workspace" / "environment"
            scripts = environment / "Scripts"
            evidence.mkdir(parents=True)
            scripts.mkdir(parents=True)

            python_path = scripts / "python.exe"
            pip_path = scripts / "pip.exe"
            optimum_path = scripts / "optimum-cli.exe"
            python_path.write_bytes(b"accepted-python")
            pip_path.write_bytes(b"accepted-pip")
            optimum_path.write_bytes(b"accepted-optimum-cli")

            decision = {
                "schema_version": "1.0",
                "campaign_id": "GTQ-WB05-MF-v1",
                "record_type": "dependency-preflight-decision",
                "route_id": "route-a-merged-openvino",
                "status": "Passed",
                "workspace": {
                    "root": record["retained_workspace"]["path"],
                    "fresh": True,
                    "normal_local_directory": True,
                },
                "python": {
                    "version": "3.12.10",
                    "executable_path": (
                        record["retained_workspace"]["path"]
                        + r"\workspace\environment\Scripts\python.exe"
                    ),
                    "executable_sha256": hashlib.sha256(
                        python_path.read_bytes()
                    ).hexdigest(),
                },
                "pip": {
                    "version": "25.0.1",
                    "executable_path": (
                        record["retained_workspace"]["path"]
                        + r"\workspace\environment\Scripts\pip.exe"
                    ),
                    "executable_sha256": hashlib.sha256(
                        pip_path.read_bytes()
                    ).hexdigest(),
                },
                "direct_requirements": [
                    "optimum-intel @ git+https://github.com/huggingface/"
                    "optimum-intel.git@a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0",
                    "optimum @ git+https://github.com/huggingface/"
                    "optimum.git@982e495540364f95da1e4b6f62d2d4e5907d08fd",
                    "transformers==5.5.0",
                    "huggingface-hub==1.21.0",
                    "nncf==3.2.0",
                    "openvino==2026.2.1",
                    "openvino-tokenizers==2026.2.1.0",
                ],
                "checks": [
                    {"name": name, "status": "Passed"}
                    for name in (
                        "resolver",
                        "install",
                        "imports",
                        "cli_help",
                        "no_model_compatibility",
                        "remote_code_disabled",
                    )
                ],
            }
            decision.update({flag: False for flag in CLAIM_FLAGS})
            decision_bytes = (
                json.dumps(decision, indent=2, sort_keys=True) + "\n"
            ).encode("utf-8")
            (evidence / "decision.json").write_bytes(decision_bytes)

            test_record = copy.deepcopy(record)
            test_record["decision"]["sha256"] = hashlib.sha256(
                decision_bytes
            ).hexdigest()

            proof = verify_retained_dependency_acceptance(
                test_record,
                REPOSITORY_ROOT,
                test_record["decision"]["sha256"],
                workspace_override=workspace,
                bundle_validator=lambda _bundle, _repository: [],
            )

            self.assertEqual("Passed", proof["status"])
            self.assertEqual(
                hashlib.sha256(optimum_path.read_bytes()).hexdigest(),
                proof["conversion_environment"]["optimum_cli_sha256"],
            )
            self.assertEqual(
                test_record["decision"]["sha256"],
                proof["decision_sha256"],
            )
            for flag in CLAIM_FLAGS:
                self.assertIs(proof[flag], False)

    def test_retained_bundle_validator_issue_blocks_binding(self) -> None:
        record = load_dependency_acceptance(ACCEPTANCE_PATH, REPOSITORY_ROOT)

        with tempfile.TemporaryDirectory() as directory:
            workspace = Path(directory) / "dependency-preflight-32211117536-1"
            (workspace / "evidence").mkdir(parents=True)
            (workspace / "workspace" / "environment" / "Scripts").mkdir(
                parents=True
            )

            with self.assertRaisesRegex(
                ValueError,
                "retained dependency evidence failed validation",
            ):
                verify_retained_dependency_acceptance(
                    record,
                    REPOSITORY_ROOT,
                    record["decision"]["sha256"],
                    workspace_override=workspace,
                    bundle_validator=lambda _bundle, _repository: [
                        _BundleIssue("TEST", "decision.json", "changed")
                    ],
                )


if __name__ == "__main__":
    unittest.main()
