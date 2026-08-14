from __future__ import annotations

import hashlib
import json
import unittest
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker

from scripts.testing.workbook05.phase3.conversion import (
    OPTIMUM_COMMIT,
    OPTIMUM_INTEL_COMMIT,
    REVIEWED_DIRECT_REQUIREMENTS,
)
from scripts.testing.workbook05.phase3.dependency_lock import (
    build_dependency_preflight_record,
    parse_hash_locked_requirements,
    parse_normal_install_report,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
SCHEMA_PATH = (
    REPOSITORY_ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "schemas"
    / "workbook05"
    / "conversion-dependency-preflight.schema.json"
)


def _digest(character: str) -> str:
    return character * 64


def _lock_text() -> str:
    return """\
transformers==5.5.0 \\
    --hash=sha256:{a}
huggingface-hub==1.21.0 \\
    --hash=sha256:{b}
nncf==3.2.0 \\
    --hash=sha256:{c}
openvino==2026.2.1 \\
    --hash=sha256:{d}
openvino-tokenizers==2026.2.1.0 \\
    --hash=sha256:{e}
requests==2.33.0 \\
    --hash=sha256:{f}
""".format(
        a=_digest("a"),
        b=_digest("b"),
        c=_digest("c"),
        d=_digest("d"),
        e=_digest("e"),
        f=_digest("f"),
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
                "is_direct": name != "requests",
                "requested": name != "requests",
                "metadata": {"name": name, "version": version},
            }
            for name, version, character in packages
        ],
    }


def _observation(lock_text: str | None = None) -> dict[str, object]:
    lock = lock_text if lock_text is not None else _lock_text()
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
        "lock_sha256": hashlib.sha256(lock.encode("utf-8")).hexdigest(),
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


class Phase3DependencyLockTests(unittest.TestCase):
    """Prove normal artifacts are hash locked and VCS sources stay separate."""

    def test_valid_normal_lock_is_parsed_deterministically(self) -> None:
        packages = parse_hash_locked_requirements(_lock_text())

        self.assertEqual(
            [
                "huggingface-hub",
                "nncf",
                "openvino",
                "openvino-tokenizers",
                "requests",
                "transformers",
            ],
            [package.name for package in packages],
        )
        self.assertTrue(all(package.hashes for package in packages))

    def test_unhashed_or_moving_requirement_is_rejected(self) -> None:
        invalid_locks = (
            "transformers==5.5.0\n",
            "transformers>=5.5.0 --hash=sha256:" + _digest("a") + "\n",
            (
                "optimum @ git+https://github.com/huggingface/optimum.git@"
                + OPTIMUM_COMMIT
                + "\n"
            ),
            "--extra-index-url https://example.invalid/simple\n",
        )
        for lock in invalid_locks:
            with self.subTest(lock=lock):
                with self.assertRaises(ValueError):
                    parse_hash_locked_requirements(lock)

    def test_install_report_digest_must_match_the_lock(self) -> None:
        lock = parse_hash_locked_requirements(_lock_text())
        report = _install_report()
        report["install"][0]["download_info"]["archive_info"]["hashes"][
            "sha256"
        ] = _digest("0")

        with self.assertRaisesRegex(ValueError, "not present in the lock"):
            parse_normal_install_report(report, lock)

    def test_vcs_package_cannot_appear_as_a_normal_distribution(self) -> None:
        lock = _lock_text() + (
            "optimum==2.3.0 \\\n    --hash=sha256:" + _digest("0") + "\n"
        )
        report = _install_report()
        report["install"].append(
            {
                "download_info": {
                    "url": "https://files.pythonhosted.org/optimum.whl",
                    "archive_info": {"hashes": {"sha256": _digest("0")}},
                },
                "metadata": {"name": "optimum", "version": "2.3.0"},
            }
        )

        with self.assertRaisesRegex(ValueError, "VCS package"):
            parse_normal_install_report(
                report,
                parse_hash_locked_requirements(lock),
            )

    def test_complete_observation_builds_schema_valid_non_authorising_record(
        self,
    ) -> None:
        record = build_dependency_preflight_record(_observation())
        schema = json.loads(SCHEMA_PATH.read_text(encoding="utf-8"))
        errors = list(
            Draft202012Validator(
                schema,
                format_checker=FormatChecker(),
            ).iter_errors(record)
        )

        self.assertEqual([], [error.message for error in errors])
        self.assertEqual("Passed", record["status"])
        self.assertFalse(record["model_download_authorised"])
        self.assertFalse(record["granite_model_test_authorised"])
        self.assertFalse(record["activation_claim_authorised"])
        self.assertFalse(record["packed_storage_claim_authorised"])
        self.assertFalse(record["performance_claim_authorised"])
        self.assertFalse(record["quality_claim_authorised"])

    def test_lock_digest_mismatch_is_integrity_failure(self) -> None:
        observation = _observation()
        observation["lock_sha256"] = _digest("f")

        record = build_dependency_preflight_record(observation)

        self.assertEqual("IntegrityFailure", record["status"])
        self.assertIn("lock", " ".join(record["reasons"]).casefold())


if __name__ == "__main__":
    unittest.main()
