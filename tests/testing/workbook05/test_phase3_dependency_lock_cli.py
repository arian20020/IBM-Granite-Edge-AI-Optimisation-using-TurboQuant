from __future__ import annotations

import json
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

from scripts.testing.workbook05.phase3 import dependency_lock


def _digest(character: str) -> str:
    return character * 64


def _bootstrap_lock_text() -> str:
    return """\
build==1.3.0 \\
    --hash=sha256:{build}
click==8.2.1 \\
    --hash=sha256:{click}
pip-tools==7.5.0 \\
    --hash=sha256:{pip_tools}
pyproject-hooks==1.2.0 \\
    --hash=sha256:{hooks}
""".format(
        build=_digest("1"),
        click=_digest("2"),
        pip_tools=_digest("3"),
        hooks=_digest("4"),
    )


def _bootstrap_report() -> dict[str, object]:
    packages = (
        ("build", "1.3.0", "1", False),
        ("click", "8.2.1", "2", False),
        ("pip-tools", "7.5.0", "3", True),
        ("pyproject-hooks", "1.2.0", "4", False),
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
                "is_direct": direct,
                "requested": direct,
                "metadata": {"name": name, "version": version},
            }
            for name, version, character, direct in packages
        ],
    }


class GenericDependencyLockTests(unittest.TestCase):
    """Define one reusable policy API for bootstrap and ordinary locks."""

    def test_generic_lock_accepts_explicit_required_versions(self) -> None:
        packages = dependency_lock.parse_hash_locked_requirements(
            _bootstrap_lock_text(),
            required_direct_versions={"pip-tools": "7.5.0"},
            forbidden_names=frozenset({"optimum", "optimum-intel"}),
        )

        self.assertEqual(
            ["build", "click", "pip-tools", "pyproject-hooks"],
            [package.name for package in packages],
        )

    def test_generic_lock_rejects_a_forbidden_canonical_name(self) -> None:
        lock = _bootstrap_lock_text() + (
            "Optimum_Intel==2.2.0.dev0 \\\n"
            f"    --hash=sha256:{_digest('5')}\n"
        )

        with self.assertRaisesRegex(ValueError, "forbidden"):
            dependency_lock.parse_hash_locked_requirements(
                lock,
                required_direct_versions={"pip-tools": "7.5.0"},
                forbidden_names=frozenset({"optimum-intel"}),
            )

    def test_generic_report_binds_actual_hashes_and_requested_flags(self) -> None:
        lock = dependency_lock.parse_hash_locked_requirements(
            _bootstrap_lock_text(),
            required_direct_versions={"pip-tools": "7.5.0"},
        )

        packages = dependency_lock.parse_install_report_against_lock(
            _bootstrap_report(),
            lock,
        )

        self.assertEqual(4, len(packages))
        self.assertEqual(
            ["pip-tools"],
            [package.name for package in packages if package.direct],
        )
        self.assertTrue(
            all(package.source_identity.startswith("sha256:") for package in packages)
        )

    def test_generic_report_rejects_package_set_drift(self) -> None:
        lock = dependency_lock.parse_hash_locked_requirements(
            _bootstrap_lock_text(),
            required_direct_versions={"pip-tools": "7.5.0"},
        )
        report = _bootstrap_report()
        report["install"].pop()

        with self.assertRaisesRegex(ValueError, "package sets differ"):
            dependency_lock.parse_install_report_against_lock(report, lock)


class DependencyLockCliTests(unittest.TestCase):
    """Define deterministic, atomic lock and report validation commands."""

    def setUp(self) -> None:
        self.temporary_directory = TemporaryDirectory()
        self.addCleanup(self.temporary_directory.cleanup)
        self.root = Path(self.temporary_directory.name)
        self.lock_path = self.root / "bootstrap.txt"
        self.report_path = self.root / "report.json"
        self.output_path = self.root / "result.json"
        self.lock_path.write_text(
            _bootstrap_lock_text(),
            encoding="utf-8",
            newline="\n",
        )
        self.report_path.write_text(
            json.dumps(_bootstrap_report(), indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
            newline="\n",
        )

    @staticmethod
    def _cli_main(arguments: list[str]) -> int:
        from scripts.testing.workbook05.phase3.dependency_lock_cli import main

        return main(arguments)

    def test_validate_lock_writes_atomic_computed_summary(self) -> None:
        exit_code = self._cli_main(
            [
                "validate-lock",
                "--lock",
                str(self.lock_path),
                "--output",
                str(self.output_path),
                "--required-direct",
                "pip-tools==7.5.0",
                "--forbidden-name",
                "optimum",
                "--forbidden-name",
                "optimum-intel",
            ]
        )

        self.assertEqual(0, exit_code)
        result = json.loads(self.output_path.read_text(encoding="utf-8"))
        self.assertEqual("dependency-lock-validation", result["record_type"])
        self.assertEqual(4, result["package_count"])
        self.assertRegex(result["lock_sha256"], r"^[0-9a-f]{64}$")
        self.assertFalse(self.output_path.with_name(self.output_path.name + ".tmp").exists())

    def test_validate_report_records_actual_package_identities(self) -> None:
        exit_code = self._cli_main(
            [
                "validate-report",
                "--lock",
                str(self.lock_path),
                "--report",
                str(self.report_path),
                "--output",
                str(self.output_path),
                "--required-direct",
                "pip-tools==7.5.0",
            ]
        )

        self.assertEqual(0, exit_code)
        result = json.loads(self.output_path.read_text(encoding="utf-8"))
        self.assertEqual(
            "dependency-install-report-validation",
            result["record_type"],
        )
        self.assertEqual(4, result["package_count"])
        self.assertEqual(
            ["pip-tools"],
            [item["name"] for item in result["packages"] if item["direct"]],
        )

    def test_existing_output_or_temporary_file_is_rejected(self) -> None:
        argument_prefix = [
            "validate-lock",
            "--lock",
            str(self.lock_path),
            "--output",
            str(self.output_path),
            "--required-direct",
            "pip-tools==7.5.0",
        ]
        for path in (
            self.output_path,
            self.output_path.with_name(self.output_path.name + ".tmp"),
        ):
            with self.subTest(path=path):
                path.write_text("occupied\n", encoding="utf-8")
                with self.assertRaises(FileExistsError):
                    self._cli_main(argument_prefix)
                path.unlink()


if __name__ == "__main__":
    unittest.main()
