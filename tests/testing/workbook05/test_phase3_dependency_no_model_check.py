from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

from scripts.testing.workbook05.phase3 import conversion
from scripts.testing.workbook05.phase3.dependency_no_model_check import (
    NoModelCheckError,
    run_no_model_check,
    write_no_model_check,
)


class Phase3DependencyNoModelCheckTests(unittest.TestCase):
    """Prove the compatibility check stays inert and fail closed."""

    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary_directory.cleanup)
        self.root = Path(self.temporary_directory.name)

        # The real conversion request admits only a final environment beneath
        # C:\w5c. This is a lexical test value; the check never probes it.
        self.environment_root = Path(
            r"C:\w5c\dependency-preflight-fixture\environment"
        )
        self.optimum_root = self.root / "sources" / "optimum"
        self.optimum_intel_root = self.root / "sources" / "optimum-intel"

    @staticmethod
    def _source_report() -> dict[str, object]:
        return {
            "schema_version": "1.0",
            "record_type": "dependency-source-contract",
            "status": "Passed",
            "source_metadata_execution": False,
            "sources": {
                "optimum": {
                    "name": "optimum",
                    "base_version": "2.3.0",
                    "reviewed_commit": conversion.OPTIMUM_COMMIT,
                },
                "optimum-intel": {
                    "name": "optimum-intel",
                    "base_version": "2.2.0.dev0",
                    "reviewed_commit": conversion.OPTIMUM_INTEL_COMMIT,
                },
            },
        }

    @staticmethod
    def _versions() -> dict[str, str]:
        # Derive exact ordinary pins from the same production catalogue used by
        # the checker instead of maintaining a second copied package table.
        values = {
            requirement.split("==", 1)[0]: requirement.split("==", 1)[1]
            for requirement in conversion.REVIEWED_NORMAL_REQUIREMENT_INPUT
            if "==" in requirement
        }
        values.update(
            {
                "optimum": "2.3.0",
                "optimum-intel": "2.2.0.dev0+a3b6012",
            }
        )
        return values

    def _module(self, name: str) -> SimpleNamespace:
        """Return a conventional package with one concrete module file."""

        relative = name.replace(".", "\\") + r"\__init__.py"
        return SimpleNamespace(
            __file__=str(
                self.environment_root / "Lib" / "site-packages" / relative
            )
        )

    @staticmethod
    def _namespace_module(*locations: str) -> SimpleNamespace:
        """Return the PEP 420 shape used by the installed Optimum namespace."""

        return SimpleNamespace(
            __file__=None,
            __spec__=SimpleNamespace(
                submodule_search_locations=list(locations),
            ),
        )

    def _run_with_importer(self, importer: object) -> dict[str, object]:
        """Run the inert check with only import identity replaced for the test."""

        with (
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.validate_reviewed_source_contracts",
                return_value=self._source_report(),
            ) as source_validator,
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.import_module",
                side_effect=importer,
            ),
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.distribution_version",
                side_effect=lambda name: self._versions()[name],
            ),
        ):
            result = run_no_model_check(
                self.environment_root,
                self.optimum_root,
                self.optimum_intel_root,
            )

        source_validator.assert_called_once_with(
            self.optimum_root,
            self.optimum_intel_root,
        )
        return result

    def test_check_validates_sources_versions_and_exact_conversion_arguments(self) -> None:
        result = self._run_with_importer(self._module)

        self.assertEqual("Passed", result["status"])
        self.assertFalse(result["source_contracts"]["source_metadata_execution"])
        self.assertEqual(
            str(self.environment_root / "Scripts" / "optimum-cli.exe"),
            result["conversion_executable"],
        )
        arguments = result["conversion_arguments"]
        self.assertEqual("export", arguments[0])
        self.assertEqual("openvino", arguments[1])
        self.assertIn("--model", arguments)
        self.assertIn(r"C:\w5m\sources\not-opened-source", arguments)
        self.assertEqual(r"C:\w5m\converted\not-created-output", arguments[-1])
        self.assertNotIn("--trust-remote-code", arguments)
        self.assertFalse(result["trust_remote_code"])
        self.assertFalse(result["model_opened"])
        self.assertFalse(result["network_contacted"])
        self.assertFalse(result["process_executed"])
        self.assertFalse(result["output_directory_created"])
        self.assertTrue(
            all(
                value is False
                for value in result["scientific_authorisations"].values()
            )
        )
        self.assertTrue(
            all(row["kind"] == "file" for row in result["imported_modules"])
        )

    def test_namespace_package_inside_final_environment_is_accepted(self) -> None:
        namespace_location = str(
            self.environment_root / "Lib" / "site-packages" / "optimum"
        )

        def import_candidate(name: str) -> SimpleNamespace:
            if name == "optimum":
                return self._namespace_module(namespace_location)
            return self._module(name)

        result = self._run_with_importer(import_candidate)

        optimum = next(
            row for row in result["imported_modules"] if row["module"] == "optimum"
        )
        self.assertEqual("namespace", optimum["kind"])
        self.assertEqual(namespace_location, optimum["file"])
        self.assertEqual([namespace_location], optimum["locations"])

    def test_check_never_touches_model_paths_network_or_subprocess(self) -> None:
        with (
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.validate_reviewed_source_contracts",
                return_value=self._source_report(),
            ),
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.import_module",
                side_effect=self._module,
            ),
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.distribution_version",
                side_effect=lambda name: self._versions()[name],
            ),
            patch.object(
                Path,
                "exists",
                side_effect=AssertionError("filesystem probe forbidden"),
            ),
            patch.object(
                Path,
                "resolve",
                side_effect=AssertionError("path resolution forbidden"),
            ),
            patch.object(
                Path,
                "open",
                side_effect=AssertionError("model open forbidden"),
            ),
            patch.object(
                Path,
                "mkdir",
                side_effect=AssertionError("output creation forbidden"),
            ),
            patch("subprocess.run", side_effect=AssertionError("subprocess forbidden")),
            patch("socket.socket", side_effect=AssertionError("network forbidden")),
        ):
            result = run_no_model_check(
                self.environment_root,
                self.optimum_root,
                self.optimum_intel_root,
            )

        self.assertEqual("Passed", result["status"])

    def test_import_outside_supplied_environment_is_rejected(self) -> None:
        escaped_module = SimpleNamespace(__file__=r"C:\outside\module.py")
        with (
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.validate_reviewed_source_contracts",
                return_value=self._source_report(),
            ),
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.import_module",
                return_value=escaped_module,
            ),
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.distribution_version",
                side_effect=lambda name: self._versions()[name],
            ),
        ):
            with self.assertRaisesRegex(
                NoModelCheckError,
                "outside the supplied environment",
            ):
                run_no_model_check(
                    self.environment_root,
                    self.optimum_root,
                    self.optimum_intel_root,
                )

    def test_namespace_import_outside_supplied_environment_is_rejected(self) -> None:
        escaped_namespace = self._namespace_module(r"C:\outside\optimum")
        with (
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.validate_reviewed_source_contracts",
                return_value=self._source_report(),
            ),
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.import_module",
                return_value=escaped_namespace,
            ),
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.distribution_version",
                side_effect=lambda name: self._versions()[name],
            ),
        ):
            with self.assertRaisesRegex(
                NoModelCheckError,
                "outside the supplied environment",
            ):
                run_no_model_check(
                    self.environment_root,
                    self.optimum_root,
                    self.optimum_intel_root,
                )

    def test_direct_version_drift_is_rejected(self) -> None:
        versions = self._versions()
        versions["openvino"] = "2026.2.2"
        with (
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.validate_reviewed_source_contracts",
                return_value=self._source_report(),
            ),
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.import_module",
                side_effect=self._module,
            ),
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.distribution_version",
                side_effect=lambda name: versions[name],
            ),
        ):
            with self.assertRaisesRegex(NoModelCheckError, "openvino version"):
                run_no_model_check(
                    self.environment_root,
                    self.optimum_root,
                    self.optimum_intel_root,
                )

    def test_unrelated_optimum_intel_git_suffix_is_rejected(self) -> None:
        versions = self._versions()
        versions["optimum-intel"] = "2.2.0.dev0+deadbee"
        with (
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.validate_reviewed_source_contracts",
                return_value=self._source_report(),
            ),
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.import_module",
                side_effect=self._module,
            ),
            patch(
                "scripts.testing.workbook05.phase3.dependency_no_model_check.distribution_version",
                side_effect=lambda name: versions[name],
            ),
        ):
            with self.assertRaisesRegex(NoModelCheckError, "optimum-intel version"):
                run_no_model_check(
                    self.environment_root,
                    self.optimum_root,
                    self.optimum_intel_root,
                )

    def test_atomic_writer_rejects_existing_final_or_temporary_output(self) -> None:
        output = self.root / "no-model-check.json"
        payload = {
            "status": "Passed",
            "scientific_authorisations": {"model": False},
        }

        write_no_model_check(output, payload)
        self.assertEqual(payload, json.loads(output.read_text(encoding="utf-8")))
        self.assertFalse(Path(str(output) + ".tmp").exists())

        with self.assertRaisesRegex(FileExistsError, "already exists"):
            write_no_model_check(output, payload)

        output.unlink()
        temporary = Path(str(output) + ".tmp")
        temporary.write_text("occupied", encoding="utf-8")
        with self.assertRaisesRegex(FileExistsError, "Temporary"):
            write_no_model_check(output, payload)


if __name__ == "__main__":
    unittest.main()
