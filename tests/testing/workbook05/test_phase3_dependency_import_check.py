from __future__ import annotations

import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

from scripts.testing.workbook05.phase3 import dependency_import_check


class Phase3DependencyImportCheckTests(unittest.TestCase):
    """Bind fresh-process imports to the supplied final environment."""

    def setUp(self) -> None:
        self.root = Path(
            r"C:\w5c\dependency-preflight-fixture\workspace\environment"
        )

    def _module(self, name: str) -> SimpleNamespace:
        relative = name.replace(".", "\\") + r"\__init__.py"
        return SimpleNamespace(
            __file__=str(self.root / "Lib" / "site-packages" / relative)
        )

    def test_imports_inside_final_environment_are_accepted(self) -> None:
        versions = {
            "optimum": "2.3.0",
            "transformers": "5.5.0",
            "nncf": "3.2.0",
            "openvino": "2026.2.1",
        }
        # Patch the already imported module objects directly. Patching by a long
        # dotted string after replacing importlib.import_module would make the
        # mock interfere with unittest.mock's own target resolution.
        with (
            patch.object(
                dependency_import_check.importlib,
                "import_module",
                side_effect=self._module,
            ),
            patch.object(
                dependency_import_check.importlib.metadata,
                "version",
                side_effect=lambda name: versions[name],
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

    def test_import_without_file_identity_is_rejected(self) -> None:
        missing = SimpleNamespace(__file__="")
        with patch.object(
            dependency_import_check.importlib,
            "import_module",
            return_value=missing,
        ):
            with self.assertRaisesRegex(ValueError, "no file identity"):
                dependency_import_check.collect_import_observations(self.root)


if __name__ == "__main__":
    unittest.main()
