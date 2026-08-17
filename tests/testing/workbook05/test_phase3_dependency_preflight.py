from __future__ import annotations

import json
import unittest
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker

from scripts.testing.workbook05.phase3.conversion import (
    OPTIMUM_COMMIT,
    OPTIMUM_INTEL_COMMIT,
    REVIEWED_DIRECT_REQUIREMENTS,
)
from scripts.testing.workbook05.phase3.dependency_preflight import (
    IMPORT_MODULES,
    REQUIRED_CHECK_NAMES,
    DependencyCheck,
    DependencyPackage,
    SourceTreeEvidence,
    collect_dependency_preflight_record,
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


def _source_trees() -> tuple[SourceTreeEvidence, ...]:
    return (
        SourceTreeEvidence(
            name="optimum-intel",
            repository="huggingface/optimum-intel",
            origin="https://github.com/huggingface/optimum-intel.git",
            commit=OPTIMUM_INTEL_COMMIT,
            clean=True,
            aggregate_sha256="1" * 64,
        ),
        SourceTreeEvidence(
            name="optimum",
            repository="huggingface/optimum",
            origin="https://github.com/huggingface/optimum.git",
            commit=OPTIMUM_COMMIT,
            clean=True,
            aggregate_sha256="2" * 64,
        ),
    )


def _packages() -> tuple[DependencyPackage, ...]:
    return (
        DependencyPackage(
            name="optimum-intel",
            version="2.2.0.dev0",
            source_identity=OPTIMUM_INTEL_COMMIT,
            direct=True,
        ),
        DependencyPackage(
            name="optimum",
            version="2.3.0",
            source_identity=OPTIMUM_COMMIT,
            direct=True,
        ),
        DependencyPackage(
            name="transformers",
            version="5.5.0",
            source_identity="sha256:" + "3" * 64,
            direct=True,
        ),
        DependencyPackage(
            name="huggingface-hub",
            version="1.21.0",
            source_identity="sha256:" + "4" * 64,
            direct=True,
        ),
        DependencyPackage(
            name="nncf",
            version="3.2.0",
            source_identity="sha256:" + "5" * 64,
            direct=True,
        ),
        DependencyPackage(
            name="openvino",
            version="2026.2.1",
            source_identity="sha256:" + "6" * 64,
            direct=True,
        ),
        DependencyPackage(
            name="openvino-tokenizers",
            version="2026.2.1.0",
            source_identity="sha256:" + "7" * 64,
            direct=True,
        ),
        DependencyPackage(
            name="requests",
            version="2.33.0",
            source_identity="sha256:" + "8" * 64,
            direct=False,
        ),
    )


def _checks() -> tuple[DependencyCheck, ...]:
    return (
        DependencyCheck(name="resolver", status="Passed", exit_code=0),
        DependencyCheck(name="install", status="Passed", exit_code=0),
        DependencyCheck(name="imports", status="Passed", exit_code=0),
        DependencyCheck(name="cli_help", status="Passed", exit_code=0),
        DependencyCheck(
            name="no_model_compatibility",
            status="Passed",
            exit_code=0,
        ),
        DependencyCheck(
            name="remote_code_disabled",
            status="Passed",
            exit_code=None,
        ),
    )


def _record(**overrides: object) -> dict[str, object]:
    values: dict[str, object] = {
        "generated_at_utc": "2026-08-14T05:00:00Z",
        "workspace_root": Path(r"C:\w5c\dependency-preflight-31770000000-1"),
        "workspace_is_normal_local_directory": True,
        "workspace_is_fresh": True,
        "python_version": "3.12.10",
        "python_executable_path": Path(
            r"C:\w5c\dependency-preflight-31770000000-1\environment\Scripts\python.exe"
        ),
        "python_executable_sha256": "9" * 64,
        "pip_version": "25.2",
        "pip_executable_path": Path(
            r"C:\w5c\dependency-preflight-31770000000-1\environment\Scripts\pip.exe"
        ),
        "pip_executable_sha256": "a" * 64,
        "source_trees": _source_trees(),
        "direct_requirements": REVIEWED_DIRECT_REQUIREMENTS,
        "bootstrap_lock_path": "locks/requirements.phase3-bootstrap.txt",
        "bootstrap_lock_sha256": "b" * 64,
        "bootstrap_install_report_path": "reports/bootstrap-install-report.json",
        "bootstrap_install_report_sha256": "c" * 64,
        "bootstrap_normal_distribution_count": 4,
        "bootstrap_all_normal_artifacts_hashed": True,
        "lock_path": "locks/requirements.phase3-assets.txt",
        "lock_sha256": "d" * 64,
        "lock_generator": "pip-tools==7.5.0",
        "normal_distribution_count": 8,
        "all_normal_artifacts_hashed": True,
        "vcs_sources_bound_separately": True,
        "packages": _packages(),
        "checks": _checks(),
        "import_modules": IMPORT_MODULES,
        "cli_help_exit_code": 0,
        "no_model_compatibility_exit_code": 0,
    }
    values.update(overrides)
    return collect_dependency_preflight_record(**values)


class Phase3DependencyPreflightTests(unittest.TestCase):
    """Prove C1 dependency preflight records exact identity without authorising download."""

    def test_valid_preflight_is_schema_valid_but_does_not_authorise_download(
        self,
    ) -> None:
        record = _record()
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
        for claim in (
            "granite_model_test_authorised",
            "activation_claim_authorised",
            "packed_storage_claim_authorised",
            "performance_claim_authorised",
            "quality_claim_authorised",
        ):
            self.assertFalse(record[claim])

    def test_interrupted_check_is_not_misreported_as_package_failure(self) -> None:
        checks = list(_checks())
        checks[1] = DependencyCheck(
            name="install",
            status="InfrastructureInterrupted",
            exit_code=-1,
        )

        record = _record(checks=tuple(checks))

        self.assertEqual("InfrastructureInterrupted", record["status"])
        self.assertIn("install", " ".join(record["reasons"]))

    def test_integrity_failure_outranks_infrastructure_interruption(self) -> None:
        checks = list(_checks())
        checks[1] = DependencyCheck(
            name="install",
            status="InfrastructureInterrupted",
            exit_code=-1,
        )

        record = _record(
            checks=tuple(checks),
            workspace_is_fresh=False,
        )

        self.assertEqual("IntegrityFailure", record["status"])

    def test_bootstrap_lock_and_report_are_bound_into_decision(self) -> None:
        record = _record()

        self.assertEqual(
            {
                "path": "locks/requirements.phase3-bootstrap.txt",
                "sha256": "b" * 64,
                "install_report_path": "reports/bootstrap-install-report.json",
                "install_report_sha256": "c" * 64,
                "normal_distribution_count": 4,
                "all_normal_artifacts_hashed": True,
            },
            record["bootstrap_lock"],
        )

    def test_git_derived_optimum_intel_version_needs_the_full_source_commit(
        self,
    ) -> None:
        packages = list(_packages())
        packages[0] = DependencyPackage(
            name="optimum-intel",
            version=f"2.2.0.dev0+{OPTIMUM_INTEL_COMMIT[:7]}",
            source_identity=OPTIMUM_INTEL_COMMIT,
            direct=True,
        )

        accepted = _record(packages=tuple(packages))

        self.assertEqual("Passed", accepted["status"])

        packages[0] = DependencyPackage(
            name="optimum-intel",
            version=f"2.2.0.dev0+{OPTIMUM_INTEL_COMMIT[:7]}",
            source_identity="f" * 40,
            direct=True,
        )
        rejected = _record(packages=tuple(packages))

        self.assertEqual("IntegrityFailure", rejected["status"])
        self.assertIn("source identity", " ".join(rejected["reasons"]))

    def test_wrong_or_dirty_vcs_source_is_integrity_failure(self) -> None:
        wrong_commit = list(_source_trees())
        wrong_commit[0] = SourceTreeEvidence(
            name=wrong_commit[0].name,
            repository=wrong_commit[0].repository,
            origin=wrong_commit[0].origin,
            commit="f" * 40,
            clean=True,
            aggregate_sha256=wrong_commit[0].aggregate_sha256,
        )
        dirty = list(_source_trees())
        dirty[1] = SourceTreeEvidence(
            name=dirty[1].name,
            repository=dirty[1].repository,
            origin=dirty[1].origin,
            commit=dirty[1].commit,
            clean=False,
            aggregate_sha256=dirty[1].aggregate_sha256,
        )

        for source_trees in (tuple(wrong_commit), tuple(dirty)):
            with self.subTest(source_trees=source_trees):
                record = _record(source_trees=source_trees)
                self.assertEqual("IntegrityFailure", record["status"])

    def test_unhashed_normal_distribution_is_integrity_failure(self) -> None:
        record = _record(all_normal_artifacts_hashed=False)
        self.assertEqual("IntegrityFailure", record["status"])
        self.assertIn("hash", " ".join(record["reasons"]).casefold())

    def test_failed_execution_check_is_blocked(self) -> None:
        checks = list(_checks())
        checks[3] = DependencyCheck(
            name="cli_help",
            status="Failed",
            exit_code=2,
        )

        record = _record(checks=tuple(checks), cli_help_exit_code=2)

        self.assertEqual("Blocked", record["status"])
        self.assertIn("cli_help", " ".join(record["reasons"]))

    def test_workspace_boundary_failure_is_integrity_failure(self) -> None:
        cases = (
            {"workspace_root": Path(r"D:\outside\preflight")},
            {"workspace_is_normal_local_directory": False},
            {"workspace_is_fresh": False},
        )
        for case in cases:
            with self.subTest(case=case):
                record = _record(**case)
                self.assertEqual("IntegrityFailure", record["status"])

    def test_bad_package_identity_is_integrity_failure(self) -> None:
        packages = list(_packages())
        packages[2] = DependencyPackage(
            name="transformers",
            version="5.5.0",
            source_identity="pypi:transformers==5.5.0",
            direct=True,
        )

        record = _record(packages=tuple(packages))

        self.assertEqual("IntegrityFailure", record["status"])
        self.assertIn("source identity", " ".join(record["reasons"]))

    def test_duplicate_package_or_check_is_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "duplicate package"):
            _record(packages=_packages() + (_packages()[0],))
        with self.assertRaisesRegex(ValueError, "duplicate check"):
            _record(checks=_checks() + (_checks()[0],))

    def test_required_check_and_import_catalogues_are_exact(self) -> None:
        self.assertEqual(
            (
                "resolver",
                "install",
                "imports",
                "cli_help",
                "no_model_compatibility",
                "remote_code_disabled",
            ),
            REQUIRED_CHECK_NAMES,
        )
        self.assertEqual(
            (
                "optimum",
                "optimum.intel",
                "transformers",
                "nncf",
                "openvino",
            ),
            IMPORT_MODULES,
        )

        record = _record(
            import_modules=IMPORT_MODULES[:-1],
        )
        self.assertEqual("IntegrityFailure", record["status"])

    def test_lock_paths_must_be_portable(self) -> None:
        for field in ("lock_path", "bootstrap_lock_path", "bootstrap_install_report_path"):
            for value in (
                "../outside.txt",
                r"locks\windows.txt",
                "/absolute.txt",
                "C:/absolute.txt",
            ):
                with self.subTest(field=field, value=value):
                    with self.assertRaises(ValueError):
                        _record(**{field: value})


if __name__ == "__main__":
    unittest.main()
