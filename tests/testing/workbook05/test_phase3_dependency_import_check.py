from __future__ import annotations

import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

from scripts.testing.workbook05.phase3 import dependency_import_check


class Phase3DependencyImportCheckTests(unittest.TestCase):
    """Bind fresh-process imports to the supplied final environment."""

    def setUp(self) -> None:
        # Use the same lexical Windows layout as the live dependency preflight.
        self.root = Path(
            r"C:\w5c\dependency-preflight-fixture\workspace\environment"
        )

    def _module(self, name: str) -> SimpleNamespace:
        """Return a conventional package with one concrete ``__file__``."""

        relative = name.replace(".", "\\") + r"\__init__.py"
        return SimpleNamespace(
            __file__=str(self.root / "Lib" / "site-packages" / relative)
        )

    def _namespace_module(self, *locations: str) -> SimpleNamespace:
        """Return the PEP 420 shape used by the installed ``optimum`` package."""

        return SimpleNamespace(
            __file__=None,
            __spec__=SimpleNamespace(
                submodule_search_locations=list(locations),
            ),
        )

    @staticmethod
    def _versions() -> dict[str, str]:
        """Return the package versions observed by the import-only boundary."""

        return {
            "optimum": "2.3.0",
            "transformers": "5.5.0",
            "nncf": "3.2.0",
            "openvino": "2026.2.1",
        }

    def test_imports_inside_final_environment_are_accepted(self) -> None:
        with (
            patch.object(
                dependency_import_check.importlib,
                "import_module",
                side_effect=self._module,
            ),
            patch.object(
                dependency_import_check.importlib.metadata,
                "version",
                side_effect=lambda name: self._versions()[name],
            ),
        ):
            observations = dependency_import_check.collect_import_observations(
                self.root
            )

        self.assertEqual(
            {"optimum", "optimum.intel", "transformers", "nncf", "openvino"},
            set(observations),
        )
        self.assertTrue(
            all(
                row["module_file"].casefold().startswith(
                    str(self.root).casefold()
                )
                for row in observations.values()
            )
        )
        self.assertTrue(
            all(row["module_kind"] == "file" for row in observations.values())
        )
        self.assertTrue(
            all(
                row["module_locations"] == [row["module_file"]]
                for row in observations.values()
            )
        )

    def test_namespace_package_inside_final_environment_is_accepted(self) -> None:
        # Optimum and Optimum Intel intentionally contribute files to the same
        # top-level PEP 420 namespace, so ``import optimum`` has no ``__file__``.
        namespace_location = str(
            self.root / "Lib" / "site-packages" / "optimum"
        )

        def import_candidate(name: str) -> SimpleNamespace:
            if name == "optimum":
                return self._namespace_module(namespace_location)
            return self._module(name)

        with (
            patch.object(
                dependency_import_check.importlib,
                "import_module",
                side_effect=import_candidate,
            ),
            patch.object(
                dependency_import_check.importlib.metadata,
                "version",
                side_effect=lambda name: self._versions()[name],
            ),
        ):
            observations = dependency_import_check.collect_import_observations(
                self.root
            )

        optimum = observations["optimum"]
        self.assertEqual("namespace", optimum["module_kind"])
        self.assertEqual(namespace_location, optimum["module_file"])
        self.assertEqual([namespace_location], optimum["module_locations"])

    def test_namespace_package_location_escaping_final_environment_is_rejected(self) -> None:
        escaped = self._namespace_module(r"C:\outside\optimum")
        with (
            patch.object(
                dependency_import_check.importlib,
                "import_module",
                return_value=escaped,
            ),
            patch.object(
                dependency_import_check.importlib.metadata,
                "version",
                return_value="1.0",
            ),
        ):
            with self.assertRaisesRegex(ValueError, "escaped"):
                dependency_import_check.collect_import_observations(self.root)

    def test_namespace_package_without_locations_is_rejected(self) -> None:
        empty_namespace = self._namespace_module()
        with patch.object(
            dependency_import_check.importlib,
            "import_module",
            return_value=empty_namespace,
        ):
            with self.assertRaisesRegex(ValueError, "no import location"):
                dependency_import_check.collect_import_observations(self.root)

    def test_import_escaping_final_environment_is_rejected(self) -> None:
        escaped = SimpleNamespace(__file__=r"C:\outside\module.py")
        with (
            patch.object(
                dependency_import_check.importlib,
                "import_module",
                return_value=escaped,
            ),
            patch.object(
                dependency_import_check.importlib.metadata,
                "version",
                return_value="1.0",
            ),
        ):
            with self.assertRaisesRegex(ValueError, "escaped"):
                dependency_import_check.collect_import_observations(self.root)

    def test_import_without_file_or_namespace_identity_is_rejected(self) -> None:
        missing = SimpleNamespace(__file__=None, __spec__=None)
        with patch.object(
            dependency_import_check.importlib,
            "import_module",
            return_value=missing,
        ):
            with self.assertRaisesRegex(ValueError, "no file or namespace identity"):
                dependency_import_check.collect_import_observations(self.root)


if __name__ == "__main__":
    unittest.main()
