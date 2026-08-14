from __future__ import annotations

import hashlib
import json
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

from scripts.testing.workbook05.hash_manifest import write_hash_manifest
from scripts.testing.workbook05.phase3.conversion import (
    OPTIMUM_COMMIT,
    OPTIMUM_INTEL_COMMIT,
    REVIEWED_DIRECT_REQUIREMENTS,
)
from scripts.testing.workbook05.phase3.dependency_bundle_validation import (
    validate_dependency_bundle,
)
from scripts.testing.workbook05.phase3.dependency_lock import (
    build_dependency_preflight_record,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]


def _digest(character: str) -> str:
    return character * 64


def _lock_text() -> str:
    packages = (
        ("transformers", "5.5.0", "a"),
        ("huggingface-hub", "1.21.0", "b"),
        ("nncf", "3.2.0", "c"),
        ("openvino", "2026.2.1", "d"),
        ("openvino-tokenizers", "2026.2.1.0", "e"),
        ("requests", "2.33.0", "f"),
    )
    return "".join(
        f"{name}=={version} --hash=sha256:{_digest(character)}\n"
        for name, version, character in packages
    )


def _install_report() -> dict[str, object]:
    packages = (
        ("transformers", "5.5.0", "a"),
        ("huggingface-hub", "1.21.0", "b"),
        ("nncf", "3.2.0", "c"),
        ("openvino", "2026.2.1", "d"),
        ("openvino-tokenizers", "2026.2.1.0", "e"),
        ("requests", "2.33.0", "f"),
    )
    return {
        "version": "1",
        "pip_version": "25.2",
        "install": [
            {
                "download_info": {
                    "url": f"https://files.pythonhosted.org/{name}.whl",
                    "archive_info": {
                        "hashes": {"sha256": _digest(character)}
                    },
                },
                "metadata": {"name": name, "version": version},
            }
            for name, version, character in packages
        ],
    }


def _observation() -> dict[str, object]:
    lock = _lock_text()
    return {
        "generated_at_utc": "2026-08-14T12:00:00Z",
        "workspace_root": r"C:\w5c\dependency-preflight-31820000000-1",
        "workspace_is_normal_local_directory": True,
        "workspace_is_fresh": True,
        "python_version": "3.12.10",
        "python_executable_path": (
            r"C:\w5c\dependency-preflight-31820000000-1\venv\Scripts\python.exe"
        ),
        "python_executable_sha256": _digest("1"),
        "pip_version": "25.2",
        "pip_executable_path": (
            r"C:\w5c\dependency-preflight-31820000000-1\venv\Scripts\pip.exe"
        ),
        "pip_executable_sha256": _digest("2"),
        "source_trees": [
            {
                "name": "optimum-intel",
                "repository": "huggingface/optimum-intel",
                "origin": "https://github.com/huggingface/optimum-intel.git",
                "commit": OPTIMUM_INTEL_COMMIT,
                "clean": True,
                "aggregate_sha256": _digest("3"),
            },
            {
                "name": "optimum",
                "repository": "huggingface/optimum",
                "origin": "https://github.com/huggingface/optimum.git",
                "commit": OPTIMUM_COMMIT,
                "clean": True,
                "aggregate_sha256": _digest("4"),
            },
        ],
        "direct_requirements": list(REVIEWED_DIRECT_REQUIREMENTS),
        "lock_path": "locks/requirements.phase3-assets.txt",
        "lock_text": lock,
        "lock_sha256": hashlib.sha256(lock.encode()).hexdigest(),
        "lock_generator": "pip-tools==7.5.0",
        "normal_install_report": _install_report(),
        "vcs_packages": [
            {
                "name": "optimum-intel",
                "version": "2.3.0.dev0",
                "commit": OPTIMUM_INTEL_COMMIT,
            },
            {
                "name": "optimum",
                "version": "2.3.0",
                "commit": OPTIMUM_COMMIT,
            },
        ],
        "checks": [
            {"name": "resolver", "status": "Passed", "exit_code": 0},
            {"name": "install", "status": "Passed", "exit_code": 0},
            {"name": "imports", "status": "Passed", "exit_code": 0},
            {"name": "cli_help", "status": "Passed", "exit_code": 0},
            {
                "name": "no_model_compatibility",
                "status": "Passed",
                "exit_code": 0,
            },
            {
                "name": "remote_code_disabled",
                "status": "Passed",
                "exit_code": None,
            },
        ],
        "import_modules": [
            "optimum",
            "optimum.intel",
            "transformers",
            "nncf",
            "openvino",
        ],
        "cli_help_exit_code": 0,
        "no_model_compatibility_exit_code": 0,
    }


class Phase3DependencyBundleValidationTests(unittest.TestCase):
    """Validate dependency evidence as text/data on a separate clean runner."""

    def setUp(self) -> None:
        self.temporary_directory = TemporaryDirectory()
        self.addCleanup(self.temporary_directory.cleanup)
        self.bundle = Path(self.temporary_directory.name) / "bundle"
        for relative in ("locks", "reports", "sources", "logs", "steps"):
            (self.bundle / relative).mkdir(parents=True, exist_ok=True)
        self._write_valid_bundle()

    @staticmethod
    def _write_json(path: Path, value: object) -> None:
        path.write_text(
            json.dumps(value, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
            newline="\n",
        )

    def _refresh_manifest(self) -> None:
        write_hash_manifest(self.bundle, self.bundle / "manifest.sha256")

    def _write_valid_bundle(self) -> None:
        observation = _observation()
        decision = build_dependency_preflight_record(observation)
        self._write_json(self.bundle / "decision.json", decision)
        self._write_json(self.bundle / "observation.json", observation)
        (self.bundle / "locks" / "requirements.phase3-assets.txt").write_text(
            observation["lock_text"],
            encoding="utf-8",
            newline="\n",
        )
        self._write_json(
            self.bundle / "reports" / "normal-install-report.json",
            observation["normal_install_report"],
        )
        self._write_json(
            self.bundle / "reports" / "vcs-packages.json",
            {"packages": observation["vcs_packages"]},
        )
        for source in observation["source_trees"]:
            self._write_json(
                self.bundle / "sources" / f"{source['name']}.json",
                source,
            )
        self._write_json(
            self.bundle / "checks.json",
            {"checks": observation["checks"]},
        )
        (self.bundle / "logs" / "resolver.stdout.txt").write_text(
            "resolver passed\n", encoding="utf-8"
        )
        (self.bundle / "logs" / "resolver.stderr.txt").write_text(
            "", encoding="utf-8"
        )
        (self.bundle / "summary.md").write_text(
            "# Dependency preflight\n\nNo model or scientific claim is authorised.\n",
            encoding="utf-8",
        )
        self._write_json(
            self.bundle / "stage-order.json",
            {"schema_version": "1.0", "stages": ["manifest-generation"]},
        )
        self._refresh_manifest()

    @staticmethod
    def _codes(issues: object) -> set[str]:
        return {issue.code for issue in issues}

    def test_valid_passed_bundle_is_accepted(self) -> None:
        self.assertEqual(
            [],
            validate_dependency_bundle(
                self.bundle,
                REPOSITORY_ROOT,
                require_passed=True,
            ),
        )

    def test_changed_lock_is_rejected(self) -> None:
        path = self.bundle / "locks" / "requirements.phase3-assets.txt"
        path.write_text(path.read_text(encoding="utf-8") + "# changed\n")

        issues = validate_dependency_bundle(self.bundle, REPOSITORY_ROOT)

        self.assertIn("HASH_MISMATCH", self._codes(issues))
        self.assertIn("LOCK_IDENTITY", self._codes(issues))

    def test_blocked_fixture_is_not_accepted_as_live(self) -> None:
        path = self.bundle / "decision.json"
        decision = json.loads(path.read_text(encoding="utf-8"))
        decision["status"] = "Blocked"
        decision["reasons"] = ["Offline fixture evidence only."]
        self._write_json(path, decision)
        self._refresh_manifest()

        issues = validate_dependency_bundle(
            self.bundle,
            REPOSITORY_ROOT,
            require_passed=True,
        )

        self.assertIn("DECISION_NOT_PASSED", self._codes(issues))

    def test_wrong_vcs_commit_is_rejected(self) -> None:
        path = self.bundle / "sources" / "optimum.json"
        source = json.loads(path.read_text(encoding="utf-8"))
        source["commit"] = "f" * 40
        self._write_json(path, source)
        self._refresh_manifest()

        issues = validate_dependency_bundle(self.bundle, REPOSITORY_ROOT)

        self.assertIn("VCS_IDENTITY", self._codes(issues))

    def test_secret_and_binary_payload_are_rejected(self) -> None:
        (self.bundle / "summary.md").write_text(
            "HF_TOKEN=secret\n", encoding="utf-8"
        )
        (self.bundle / "package.whl").write_bytes(b"forbidden")
        self._refresh_manifest()

        issues = validate_dependency_bundle(self.bundle, REPOSITORY_ROOT)
        codes = self._codes(issues)

        self.assertIn("SECRET_PATTERN", codes)
        self.assertIn("FORBIDDEN_PAYLOAD", codes)

    def test_true_model_or_performance_claim_is_rejected(self) -> None:
        path = self.bundle / "decision.json"
        decision = json.loads(path.read_text(encoding="utf-8"))
        decision["model_download_authorised"] = True
        decision["performance_claim_authorised"] = True
        self._write_json(path, decision)
        self._refresh_manifest()

        issues = validate_dependency_bundle(self.bundle, REPOSITORY_ROOT)

        self.assertIn("SCIENTIFIC_CLAIM", self._codes(issues))


if __name__ == "__main__":
    unittest.main()
