from __future__ import annotations

import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from scripts.testing.workbook05.phase3 import dependency_acceptance as acceptance


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
ACCEPTANCE_RELATIVE_PATH = Path(
    "experiments/granite_turboquant_intel/manifests/campaigns/"
    "GTQ-WB05-MF-v1/phase3/accepted-dependency-preflight.json"
)
ACCEPTANCE_PATH = REPOSITORY_ROOT / ACCEPTANCE_RELATIVE_PATH

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
    def setUp(self) -> None:
        self.record = json.loads(ACCEPTANCE_PATH.read_text(encoding="utf-8"))

    def test_committed_acceptance_binds_the_independently_verified_run(self) -> None:
        self.assertEqual([], acceptance.validate_dependency_acceptance_record(self.record))
        self.assertEqual("32211117536", self.record["workflow_run_id"])
        self.assertEqual(1, self.record["run_attempt"])
        self.assertEqual(
            "workbook-05-phase3-dependency-preflight-32211117536-1",
            self.record["artifact_name"],
        )
        self.assertEqual(
            "b68a4f8af8c57a9f5d71347d2485855796f4d0dce0291c50b596512c309c0c21",
            self.record["github_artifact_sha256"],
        )
        self.assertEqual(
            self.record["github_artifact_sha256"],
            self.record["independent_artifact_sha256"],
        )
        self.assertEqual(
            "429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49",
            self.record["decision_sha256"],
        )
        self.assertEqual(
            r"C:\w5c\dependency-preflight-32211117536-1",
            self.record["workspace_root"],
        )
        self.assertTrue(self.record["owner_acceptance"])
        self.assertEqual("arian20020", self.record["accepted_by"])
        for flag in CLAIM_FLAGS:
            self.assertIs(self.record[flag], False)

    def test_expected_decision_digest_must_match_before_workspace_access(self) -> None:
        with self.assertRaisesRegex(
            ValueError,
            "supplied dependency decision SHA-256 does not match",
        ):
            acceptance.verify_retained_dependency_acceptance(
                REPOSITORY_ROOT,
                "0" * 64,
            )

    def test_github_and_independent_artifact_digests_must_agree(self) -> None:
        changed = copy.deepcopy(self.record)
        changed["independent_artifact_sha256"] = "0" * 64

        issues = acceptance.validate_dependency_acceptance_record(changed)

        self.assertTrue(
            any(issue.code == "ARTIFACT_SHA256_MISMATCH" for issue in issues)
        )

    def test_workspace_identity_must_match_the_accepted_attempt(self) -> None:
        changed = copy.deepcopy(self.record)
        changed["workspace_root"] = (
            r"C:\w5c\dependency-preflight-32211117536-2"
        )

        issues = acceptance.validate_dependency_acceptance_record(changed)

        self.assertTrue(
            any(issue.code == "WORKSPACE_IDENTITY_MISMATCH" for issue in issues)
        )

    def _create_retained_fixture(
        self,
        root: Path,
    ) -> tuple[Path, Path, Path, Path, str]:
        repository = root / "repository"
        workspace = root / "dependency-preflight-32211117536-1"
        evidence = workspace / "evidence"
        scripts = workspace / "workspace" / "environment" / "Scripts"
        acceptance_path = repository / ACCEPTANCE_RELATIVE_PATH
        evidence.mkdir(parents=True)
        scripts.mkdir(parents=True)
        acceptance_path.parent.mkdir(parents=True)

        # Canonicalize after directory creation. Windows can expand an 8.3 temp
        # path during resolve(); the committed and observed fixture identities
        # must use the same canonical spelling as the production verifier.
        workspace = workspace.resolve(strict=True)
        evidence = evidence.resolve(strict=True)
        scripts = scripts.resolve(strict=True)

        python_path = scripts / "python.exe"
        optimum_path = scripts / "optimum-cli.exe"
        python_path.write_bytes(b"accepted-python")
        optimum_path.write_bytes(b"accepted-optimum-cli")
        python_path = python_path.resolve(strict=True)
        optimum_path = optimum_path.resolve(strict=True)

        decision = {
            "schema_version": "1.0",
            "campaign_id": "GTQ-WB05-MF-v1",
            "record_type": "dependency-preflight-decision",
            "route_id": "route-a-merged-openvino",
            "status": "Passed",
        }
        decision.update({flag: False for flag in CLAIM_FLAGS})
        decision_bytes = (
            json.dumps(decision, indent=2, sort_keys=True) + "\n"
        ).encode("utf-8")
        (evidence / "decision.json").write_bytes(decision_bytes)
        decision_sha256 = hashlib.sha256(decision_bytes).hexdigest()

        observation = {
            "generated_at_utc": "2026-08-19T03:18:00Z",
            "workspace_root": str(workspace),
            "python_version": "3.12.10",
            "python_executable_path": str(python_path),
            "python_executable_sha256": hashlib.sha256(
                python_path.read_bytes()
            ).hexdigest(),
        }
        (evidence / "observation.json").write_text(
            json.dumps(observation, indent=2) + "\n",
            encoding="utf-8",
        )

        record = copy.deepcopy(self.record)
        record["decision_sha256"] = decision_sha256
        record["workspace_root"] = str(workspace)
        record["evidence_root"] = str(evidence)
        acceptance_path.write_text(
            json.dumps(record, indent=2) + "\n",
            encoding="utf-8",
        )
        return repository, workspace, evidence, optimum_path, decision_sha256

    def test_retained_workspace_is_revalidated_before_model_access(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (
                repository,
                workspace,
                evidence,
                optimum_path,
                decision_sha256,
            ) = self._create_retained_fixture(root)

            with (
                patch.object(
                    acceptance,
                    "ACCEPTED_DEPENDENCY_DECISION_SHA256",
                    decision_sha256,
                ),
                patch.object(
                    acceptance,
                    "ACCEPTED_DEPENDENCY_WORKSPACE",
                    str(workspace),
                ),
                patch.object(
                    acceptance,
                    "ACCEPTED_DEPENDENCY_EVIDENCE",
                    str(evidence),
                ),
                patch.object(
                    acceptance,
                    "validate_dependency_bundle",
                    return_value=[],
                ),
            ):
                proof = acceptance.verify_retained_dependency_acceptance(
                    repository,
                    decision_sha256,
                )

            self.assertEqual("Passed", proof["status"])
            self.assertEqual(
                hashlib.sha256(optimum_path.read_bytes()).hexdigest(),
                proof["optimum_cli_sha256"],
            )
            self.assertEqual(decision_sha256, proof["decision_sha256"])
            self.assertEqual(str(workspace), proof["workspace_root"])
            for flag in CLAIM_FLAGS:
                self.assertIs(proof[flag], False)

    def test_changed_retained_decision_blocks_binding(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            repository, workspace, evidence, _, decision_sha256 = (
                self._create_retained_fixture(root)
            )
            (evidence / "decision.json").write_text(
                '{"status":"Changed"}\n',
                encoding="utf-8",
            )

            with (
                patch.object(
                    acceptance,
                    "ACCEPTED_DEPENDENCY_DECISION_SHA256",
                    decision_sha256,
                ),
                patch.object(
                    acceptance,
                    "ACCEPTED_DEPENDENCY_WORKSPACE",
                    str(workspace),
                ),
                patch.object(
                    acceptance,
                    "ACCEPTED_DEPENDENCY_EVIDENCE",
                    str(evidence),
                ),
                patch.object(
                    acceptance,
                    "validate_dependency_bundle",
                    return_value=[],
                ),
            ):
                with self.assertRaisesRegex(
                    ValueError,
                    "decision SHA-256 mismatch",
                ):
                    acceptance.verify_retained_dependency_acceptance(
                        repository,
                        decision_sha256,
                    )

    def test_retained_bundle_validator_issue_blocks_binding(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            repository, workspace, evidence, _, decision_sha256 = (
                self._create_retained_fixture(root)
            )

            with (
                patch.object(
                    acceptance,
                    "ACCEPTED_DEPENDENCY_DECISION_SHA256",
                    decision_sha256,
                ),
                patch.object(
                    acceptance,
                    "ACCEPTED_DEPENDENCY_WORKSPACE",
                    str(workspace),
                ),
                patch.object(
                    acceptance,
                    "ACCEPTED_DEPENDENCY_EVIDENCE",
                    str(evidence),
                ),
                patch.object(
                    acceptance,
                    "validate_dependency_bundle",
                    return_value=[
                        _BundleIssue("TEST", "decision.json", "changed")
                    ],
                ),
            ):
                with self.assertRaisesRegex(
                    ValueError,
                    "Retained dependency bundle is no longer valid",
                ):
                    acceptance.verify_retained_dependency_acceptance(
                        repository,
                        decision_sha256,
                    )


if __name__ == "__main__":
    unittest.main()
