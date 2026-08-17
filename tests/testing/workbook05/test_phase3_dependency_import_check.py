from __future__ import annotations

import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

from scripts.testing.workbook05.phase3.dependency_import_check import (
    collect_import_observations,
)


class Phase3DependencyImportCheckTests(unittest.TestCase):
    """Bind fresh-process imports to the supplied final environment."""

    def setUp(self) -> None:
        self.root = Path(r"C:\w5c\dependency-preflight-fixture\workspace\environment")

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
        with (
            patch(
                "scripts.testing.workbook05.phase3.dependency_import_check.importlib.import_module",
                side_effect=self._module,
            ),
            patch(
                "scripts.testing.workbook05.phase3.dependency_import_check.importlib.metadata.version",
                side_effect=lambda name: versions[name],
            ),
        ):
            observations = collect_import_observations(self.root)

        self.assertEqual(
            {"optimum", "optimum.intel", "transformers", "nncf", "openvino"},
            set(observations),
        )
        self.assertTrue(
            all(
                row["module_file"].casefold().startswith(str(self.root).casefold())
                for row in observations.values()
            )
        )

    def test_import_escaping_final_environment_is_rejected(self) -> None:
        escaped = SimpleNamespace(__file__=r"C:\outside\module.py")
        with (
            patch(
                "scripts.testing.workbook05.phase3.dependency_import_check.importlib.import_module",
                return_value=escaped,
            ),
            patch(
                "scripts.testing.workbook05.phase3.dependency_import_check.importlib.metadata.version",
                return_value="1.0",
            ),
        ):
            with self.assertRaisesRegex(ValueError, "escaped"):
                collect_import_observations(self.root)

    def test_import_without_file_identity_is_rejected(self) -> None:
        missing = SimpleNamespace(__file__="")
        with patch(
            "scripts.testing.workbook05.phase3.dependency_import_check.importlib.import_module",
            return_value=missing,
        ):
            with self.assertRaisesRegex(ValueError, "no file identity"):
                collect_import_observations(self.root)


if __name__ == "__main__":
    unittest.main()
