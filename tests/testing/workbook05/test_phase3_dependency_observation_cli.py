from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.dependency_observation_cli import main


class Phase3DependencyObservationCliTests(unittest.TestCase):
    """Reproduce the live observation with a full-scale pip metadata string."""

    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary_directory.cleanup)
        self.root = Path(self.temporary_directory.name)
        self.evidence = self.root / "evidence"
        self.locks = self.evidence / "locks"
        self.reports = self.evidence / "reports"
        self.sources = self.evidence / "sources"
        for directory in (self.locks, self.reports, self.sources):
            directory.mkdir(parents=True, exist_ok=False)

        # The live failure included large package descriptions from pip's report.
        # Keep a representative non-BMP character and a string larger than the
        # descriptions that crossed the Windows PowerShell 5.1 JSON boundary.
        self.large_description = "Granite dependency metadata 🚀\r\n" + ("x" * 60_000)

        self.bootstrap_lock = self.locks / "requirements.phase3-bootstrap.txt"
        self.bootstrap_lock.write_bytes(
            b"pip==26.1.2 --hash=sha256:" + (b"1" * 64) + b"\r\n"
        )
        self.bootstrap_report = self.reports / "bootstrap-install-report.json"
        self._write_json(
            self.bootstrap_report,
            {
                "version": "1",
                "pip_version": "26.1.2",
                "install": [],
            },
        )

        self.normal_lock = self.locks / "requirements.phase3-assets.txt"
        self.normal_lock.write_bytes(
            b"example==1.0 --hash=sha256:" + (b"2" * 64) + b"\n"
        )
        self.normal_report = self.reports / "normal-install-report.json"
        self._write_json(
            self.normal_report,
            {
                "version": "1",
                "pip_version": "26.1.2",
                "install": [
                    {
                        "download_info": {
                            "url": "https://files.pythonhosted.org/example.whl",
                            "archive_info": {"hashes": {"sha256": "2" * 64}},
                        },
                        "requested": True,
                        "is_direct": True,
                        "metadata": {
                            "name": "example",
                            "version": "1.0",
                            "description": self.large_description,
                        },
                    }
                ],
            },
        )

        self.source_paths: list[Path] = []
        for name, commit, aggregate in (
            ("optimum", "3" * 40, "4" * 64),
            ("optimum-intel", "5" * 40, "6" * 64),
        ):
            path = self.sources / f"{name}.json"
            self._write_json(
                path,
                {
                    "name": name,
                    "repository": f"huggingface/{name}",
                    "origin": f"https://github.com/huggingface/{name}.git",
                    "commit": commit,
                    "clean": True,
                    "file_count": 1,
                    "aggregate_sha256": aggregate,
                    "manifest_path": f"sources/{name}.csv",
                },
            )
            self.source_paths.append(path)

        self.vcs_packages = self.reports / "vcs-packages.json"
        self._write_json(
            self.vcs_packages,
            {
                "packages": [
                    {"name": "optimum", "version": "2.3.0", "commit": "3" * 40},
                    {
                        "name": "optimum-intel",
                        "version": "2.2.0.dev0+fixture",
                        "commit": "5" * 40,
                    },
                ]
            },
        )
        self.checks = self.evidence / "checks.json"
        self._write_json(
            self.checks,
            {
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
                ]
            },
        )
        self.final_packages = self.reports / "final-environment-packages.json"
        self._write_json(
            self.final_packages,
            {"packages": [{"name": "example", "version": "1.0"}]},
        )
        self.output = self.evidence / "observation.json"

    @staticmethod
    def _write_json(path: Path, value: object) -> None:
        path.write_text(
            json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + "\n",
            encoding="utf-8",
            newline="\n",
        )

    @staticmethod
    def _sha256(path: Path) -> str:
        return hashlib.sha256(path.read_bytes()).hexdigest()

    def _arguments(self) -> list[str]:
        arguments = [
            "--output",
            str(self.output),
            "--generated-at-utc",
            "2026-08-18T16:45:00Z",
            "--workspace-root",
            r"C:\w5c\dependency-preflight-fixture-1",
            "--python-version",
            "3.12.10",
            "--python-executable-path",
            r"C:\w5c\dependency-preflight-fixture-1\workspace\environment\Scripts\python.exe",
            "--python-executable-sha256",
            "7" * 64,
            "--pip-version",
            "26.1.2",
            "--pip-executable-path",
            r"C:\w5c\dependency-preflight-fixture-1\workspace\environment\Scripts\pip.exe",
            "--pip-executable-sha256",
            "8" * 64,
            "--bootstrap-lock",
            str(self.bootstrap_lock),
            "--bootstrap-install-report",
            str(self.bootstrap_report),
            "--normal-lock",
            str(self.normal_lock),
            "--normal-install-report",
            str(self.normal_report),
            "--vcs-packages",
            str(self.vcs_packages),
            "--checks",
            str(self.checks),
            "--final-environment-packages",
            str(self.final_packages),
        ]
        for source_path in self.source_paths:
            arguments.extend(("--source-tree", str(source_path)))
        return arguments

    def test_large_live_observation_is_written_atomically_without_data_loss(self) -> None:
        self.assertEqual(0, main(self._arguments()))

        self.assertTrue(self.output.is_file())
        self.assertFalse(self.output.with_name("observation.json.tmp").exists())
        observation = json.loads(self.output.read_text(encoding="utf-8"))
        self.assertEqual(
            self.large_description,
            observation["normal_install_report"]["install"][0]["metadata"][
                "description"
            ],
        )
        self.assertEqual(
            self.bootstrap_lock.read_bytes().decode("utf-8"),
            observation["bootstrap_lock_text"],
        )
        self.assertEqual(
            self._sha256(self.bootstrap_lock),
            observation["bootstrap_lock_sha256"],
        )
        self.assertEqual(
            self._sha256(self.bootstrap_report),
            observation["bootstrap_install_report_sha256"],
        )
        self.assertEqual(
            self._sha256(self.normal_lock),
            observation["lock_sha256"],
        )
        self.assertEqual(2, len(observation["source_trees"]))
        self.assertEqual(6, len(observation["checks"]))

    def test_existing_output_is_rejected_without_overwrite(self) -> None:
        self.output.write_text("sentinel\n", encoding="utf-8")
        with self.assertRaisesRegex(FileExistsError, "already exists"):
            main(self._arguments())
        self.assertEqual("sentinel\n", self.output.read_text(encoding="utf-8"))

    def test_non_object_evidence_input_is_rejected(self) -> None:
        self.checks.write_text("[]\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "checks.*JSON object"):
            main(self._arguments())
        self.assertFalse(self.output.exists())


if __name__ == "__main__":
    unittest.main()
