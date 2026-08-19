from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.hash_manifest import write_hash_manifest
from scripts.testing.workbook05.phase3.dependency_bundle_validation import (
    validate_dependency_bundle,
)
from scripts.testing.workbook05.phase3.dependency_decision import (
    build_live_dependency_preflight_record,
)
from scripts.testing.workbook05.phase3.dependency_preflight_fixture import (
    generate_fixture,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]


class Phase3DependencyBundleExactByteTests(unittest.TestCase):
    """Keep retained report identity byte-exact across newline conventions."""

    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary_directory.cleanup)
        self.root = Path(self.temporary_directory.name)
        attempt = generate_fixture(
            self.root,
            "exact-bytes",
            1,
            REPOSITORY_ROOT,
        )
        self.bundle = attempt / "evidence"

    @staticmethod
    def _write_json(path: Path, value: object) -> None:
        path.write_text(
            json.dumps(value, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
            newline="\n",
        )

    def _refresh_manifest(self) -> None:
        write_hash_manifest(self.bundle, self.bundle / "manifest.sha256")

    def test_crlf_bootstrap_report_matches_observation_exact_bytes(self) -> None:
        report_path = self.bundle / "reports" / "bootstrap-install-report.json"
        observation_path = self.bundle / "observation.json"
        decision_path = self.bundle / "decision.json"

        # Reproduce pip's Windows report bytes. Text-mode reads normalize these
        # CRLF pairs, but the retained artifact and observation identity must not.
        original_text = report_path.read_bytes().decode("utf-8")
        crlf_text = original_text.replace("\r\n", "\n").replace("\n", "\r\n")
        crlf_bytes = crlf_text.encode("utf-8")
        self.assertIn(b"\r\n", crlf_bytes)
        report_path.write_bytes(crlf_bytes)

        observation = json.loads(observation_path.read_text(encoding="utf-8"))
        observation["bootstrap_install_report_text"] = crlf_text
        observation["bootstrap_install_report_sha256"] = hashlib.sha256(
            crlf_bytes
        ).hexdigest()
        self._write_json(observation_path, observation)
        self._write_json(
            decision_path,
            build_live_dependency_preflight_record(observation),
        )
        self._refresh_manifest()

        self.assertEqual(
            [],
            validate_dependency_bundle(self.bundle, REPOSITORY_ROOT),
        )


if __name__ == "__main__":
    unittest.main()
