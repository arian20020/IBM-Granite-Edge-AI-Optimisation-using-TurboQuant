from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.dependency_decision_cli import main
from scripts.testing.workbook05.phase3.dependency_preflight_fixture import (
    generate_fixture,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]


class Phase3DependencyDecisionCliTests(unittest.TestCase):
    """Keep decision materialisation and package inventory one atomic boundary."""

    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary_directory.cleanup)
        self.root = Path(self.temporary_directory.name)
        attempt = generate_fixture(
            self.root,
            "decision-cli",
            1,
            REPOSITORY_ROOT,
        )
        self.evidence = attempt / "evidence"
        self.observation = self.evidence / "observation.json"
        self.output = self.evidence / "replacement-decision.json"
        self.normal_packages = self.evidence / "reports" / "normal-packages.json"
        self.normal_packages.unlink()

    def test_cli_writes_decision_and_exact_normal_package_inventory(self) -> None:
        exit_code = main(
            [
                "--observation",
                str(self.observation),
                "--output",
                str(self.output),
            ]
        )

        self.assertEqual(0, exit_code)
        self.assertTrue(self.output.is_file())
        self.assertTrue(self.normal_packages.is_file())
        inventory = json.loads(self.normal_packages.read_text(encoding="utf-8"))
        self.assertEqual(
            [
                {"name": "huggingface-hub", "version": "1.21.0"},
                {"name": "nncf", "version": "3.2.0"},
                {"name": "numpy", "version": "2.3.0"},
                {"name": "openvino", "version": "2026.2.1"},
                {"name": "openvino-tokenizers", "version": "2026.2.1.0"},
                {"name": "transformers", "version": "5.5.0"},
            ],
            inventory["packages"],
        )
        self.assertFalse(Path(str(self.output) + ".tmp").exists())
        self.assertFalse(Path(str(self.normal_packages) + ".tmp").exists())

    def test_existing_inventory_blocks_decision_publication(self) -> None:
        self.normal_packages.write_text("{}\n", encoding="utf-8")
        with self.assertRaisesRegex(FileExistsError, "already exists"):
            main(
                [
                    "--observation",
                    str(self.observation),
                    "--output",
                    str(self.output),
                ]
            )
        self.assertFalse(self.output.exists())


if __name__ == "__main__":
    unittest.main()
