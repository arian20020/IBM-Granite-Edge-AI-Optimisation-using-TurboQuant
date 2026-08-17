from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.hash_manifest import write_hash_manifest
from scripts.testing.workbook05.phase3.dependency_bundle_validation import (
    validate_dependency_bundle,
)
from scripts.testing.workbook05.phase3.dependency_decision import (
    build_live_dependency_preflight_record,
)
from scripts.testing.workbook05.phase3.dependency_preflight_fixture import (
    generate_fixture,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]


class Phase3DependencyBundleValidationTests(unittest.TestCase):
    """Validate the complete same-attempt dependency artifact as untrusted data."""

    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary_directory.cleanup)
        self.root = Path(self.temporary_directory.name)
        attempt = generate_fixture(
            self.root,
            "bundle",
            1,
            REPOSITORY_ROOT,
        )
        self.bundle = attempt / "evidence"

    @staticmethod
    def _write_json(path: Path, value: object) -> None:
        path.write_text(
            json.dumps(value, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
            newline="\n",
        )

    def _refresh_manifest(self) -> None:
        write_hash_manifest(self.bundle, self.bundle / "manifest.sha256")

    @staticmethod
    def _codes(issues: object) -> set[str]:
        return {issue.code for issue in issues}

    def _promote_fixture_to_live(self) -> None:
        observation_path = self.bundle / "observation.json"
        decision_path = self.bundle / "decision.json"
        observation = json.loads(observation_path.read_text(encoding="utf-8"))
        observation["simulation_mode"] = False
        self._write_json(observation_path, observation)
        self._write_json(
            decision_path,
            build_live_dependency_preflight_record(observation),
        )
        self._refresh_manifest()

    def test_complete_simulation_bundle_is_valid_but_not_live_acceptable(self) -> None:
        self.assertEqual(
            [],
            validate_dependency_bundle(self.bundle, REPOSITORY_ROOT),
        )
        issues = validate_dependency_bundle(
            self.bundle,
            REPOSITORY_ROOT,
            require_passed=True,
        )
        self.assertIn("SIMULATION_NOT_LIVE", self._codes(issues))

    def test_complete_live_bundle_is_accepted(self) -> None:
        self._promote_fixture_to_live()
        self.assertEqual(
            [],
            validate_dependency_bundle(
                self.bundle,
                REPOSITORY_ROOT,
                require_passed=True,
            ),
        )

    def test_bootstrap_report_drift_is_rejected(self) -> None:
        path = self.bundle / "reports" / "bootstrap-install-report.json"
        report = json.loads(path.read_text(encoding="utf-8"))
        report["install"][0]["metadata"]["version"] = "999"
        self._write_json(path, report)
        self._refresh_manifest()
        self.assertIn(
            "BOOTSTRAP_LOCK_REPORT_MISMATCH",
            self._codes(validate_dependency_bundle(self.bundle, REPOSITORY_ROOT)),
        )

    def test_normal_lock_or_report_drift_is_rejected(self) -> None:
        path = self.bundle / "reports" / "normal-install-report.json"
        report = json.loads(path.read_text(encoding="utf-8"))
        report["install"][0]["download_info"]["archive_info"]["hashes"][
            "sha256"
        ] = "0" * 64
        self._write_json(path, report)
        self._refresh_manifest()
        self.assertIn(
            "NORMAL_LOCK_REPORT_MISMATCH",
            self._codes(validate_dependency_bundle(self.bundle, REPOSITORY_ROOT)),
        )

    def test_unexpected_final_environment_package_is_rejected(self) -> None:
        path = self.bundle / "reports" / "final-environment-packages.json"
        report = json.loads(path.read_text(encoding="utf-8"))
        report["packages"].append({"name": "unexpected", "version": "1"})
        self._write_json(path, report)
        self._refresh_manifest()
        self.assertIn(
            "FINAL_PACKAGE_SET_DRIFT",
            self._codes(validate_dependency_bundle(self.bundle, REPOSITORY_ROOT)),
        )

    def test_vcs_package_in_ordinary_lock_is_rejected(self) -> None:
        path = self.bundle / "locks" / "requirements.phase3-assets.txt"
        path.write_text(
            path.read_text(encoding="utf-8")
            + f"optimum==2.3.0 --hash=sha256:{'9' * 64}\n",
            encoding="utf-8",
            newline="\n",
        )
        self._refresh_manifest()
        self.assertIn(
            "NORMAL_LOCK_REPORT_MISMATCH",
            self._codes(validate_dependency_bundle(self.bundle, REPOSITORY_ROOT)),
        )

    def test_wrong_source_commit_or_source_csv_drift_is_rejected(self) -> None:
        source_path = self.bundle / "sources" / "optimum.json"
        source = json.loads(source_path.read_text(encoding="utf-8"))
        source["commit"] = "f" * 40
        self._write_json(source_path, source)
        csv_path = self.bundle / "sources" / "optimum.csv"
        csv_path.write_text(
            csv_path.read_text(encoding="utf-8")
            + "setup.py,1,"
            + "a" * 64
            + "\n",
            encoding="utf-8",
            newline="\n",
        )
        self._refresh_manifest()
        codes = self._codes(validate_dependency_bundle(self.bundle, REPOSITORY_ROOT))
        self.assertIn("VCS_IDENTITY", codes)
        self.assertIn("SOURCE_MANIFEST_DRIFT", codes)

    def test_source_contract_drift_is_rejected(self) -> None:
        path = self.bundle / "reports" / "source-contracts.json"
        report = json.loads(path.read_text(encoding="utf-8"))
        report["sources"]["optimum-intel"]["base_version"] = "2.3.0.dev0"
        self._write_json(path, report)
        self._refresh_manifest()
        self.assertIn(
            "SOURCE_CONTRACT_DRIFT",
            self._codes(validate_dependency_bundle(self.bundle, REPOSITORY_ROOT)),
        )

    def test_command_index_missing_or_unsafe_evidence_is_rejected(self) -> None:
        path = self.bundle / "command-index.json"
        index = json.loads(path.read_text(encoding="utf-8"))
        index["commands"][0]["stdout_path"] = "../outside.txt"
        index["commands"][1]["record_path"] = "commands/missing.command.json"
        self._write_json(path, index)
        self._refresh_manifest()
        codes = self._codes(validate_dependency_bundle(self.bundle, REPOSITORY_ROOT))
        self.assertIn("COMMAND_INDEX_PATH_UNSAFE", codes)
        self.assertIn("COMMAND_EVIDENCE_MISSING", codes)

    def test_stage_or_check_order_drift_is_rejected(self) -> None:
        stage_path = self.bundle / "stage-order.json"
        stages = json.loads(stage_path.read_text(encoding="utf-8"))
        stages["completed_stages"] = list(reversed(stages["completed_stages"]))
        self._write_json(stage_path, stages)
        checks_path = self.bundle / "checks.json"
        checks = json.loads(checks_path.read_text(encoding="utf-8"))
        checks["checks"] = list(reversed(checks["checks"]))
        self._write_json(checks_path, checks)
        self._refresh_manifest()
        codes = self._codes(validate_dependency_bundle(self.bundle, REPOSITORY_ROOT))
        self.assertIn("STAGE_ORDER_DRIFT", codes)
        self.assertIn("CHECK_ORDER_DRIFT", codes)

    def test_no_model_or_scientific_claim_drift_is_rejected(self) -> None:
        no_model_path = self.bundle / "reports" / "no-model-compatibility.json"
        no_model = json.loads(no_model_path.read_text(encoding="utf-8"))
        no_model["process_executed"] = True
        self._write_json(no_model_path, no_model)
        decision_path = self.bundle / "decision.json"
        decision = json.loads(decision_path.read_text(encoding="utf-8"))
        decision["performance_claim_authorised"] = True
        self._write_json(decision_path, decision)
        self._refresh_manifest()
        codes = self._codes(validate_dependency_bundle(self.bundle, REPOSITORY_ROOT))
        self.assertIn("NO_MODEL_BOUNDARY_DRIFT", codes)
        self.assertIn("SCIENTIFIC_CLAIM", codes)

    def test_decision_must_recompute_exactly(self) -> None:
        path = self.bundle / "decision.json"
        decision = json.loads(path.read_text(encoding="utf-8"))
        decision["reasons"] = ["invented"]
        self._write_json(path, decision)
        self._refresh_manifest()
        self.assertIn(
            "DECISION_RECOMPUTE",
            self._codes(validate_dependency_bundle(self.bundle, REPOSITORY_ROOT)),
        )

    def test_secret_binary_and_manifest_case_collision_are_rejected(self) -> None:
        (self.bundle / "summary.md").write_text(
            "HF_TOKEN=secret\n",
            encoding="utf-8",
        )
        (self.bundle / "payload.WHL").write_bytes(b"forbidden")
        self._refresh_manifest()

        # A Windows directory cannot contain both checks.json and Checks.json.
        # Mutate the untrusted manifest directly so the validator must still
        # reject a case-colliding second spelling before resolving the path.
        manifest = self.bundle / "manifest.sha256"
        checks_digest = next(
            line.split("  ", maxsplit=1)[0]
            for line in manifest.read_text(encoding="utf-8").splitlines()
            if line.endswith("  checks.json")
        )
        manifest.write_text(
            manifest.read_text(encoding="utf-8")
            + f"{checks_digest}  Checks.json\n",
            encoding="utf-8",
            newline="\n",
        )

        codes = self._codes(validate_dependency_bundle(self.bundle, REPOSITORY_ROOT))
        self.assertIn("SECRET_PATTERN", codes)
        self.assertIn("FORBIDDEN_PAYLOAD", codes)
        self.assertIn("HASH_MISMATCH", codes)


if __name__ == "__main__":
    unittest.main()
