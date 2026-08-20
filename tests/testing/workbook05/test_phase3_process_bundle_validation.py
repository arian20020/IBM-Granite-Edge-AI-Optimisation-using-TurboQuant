"""Adversarial validation contracts for C2 process evidence bundles."""

from __future__ import annotations

import hashlib
import json
import shutil
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.process_bundle_validation import (
    validate_process_bundle,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
FIXTURE_ROOT = (
    REPOSITORY_ROOT
    / "tests/testing/workbook05/fixtures/phase3/process-bundles/valid"
)


class Phase3ProcessBundleValidationTests(unittest.TestCase):
    """Treat every produced process bundle as untrusted input."""

    def _copy(self, root: Path) -> Path:
        bundle = root / "bundle"
        shutil.copytree(FIXTURE_ROOT, bundle)
        return bundle

    def _rewrite_manifest(self, bundle: Path) -> None:
        rows: list[str] = []
        for path in sorted(bundle.rglob("*")):
            if not path.is_file() or path.name == "manifest.sha256":
                continue
            relative = path.relative_to(bundle).as_posix()
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            rows.append(f"{digest}  {relative}")
        (bundle / "manifest.sha256").write_text(
            "\n".join(rows) + "\n",
            encoding="utf-8",
        )

    def _codes(self, bundle: Path) -> set[str]:
        return {
            issue.code
            for issue in validate_process_bundle(bundle, REPOSITORY_ROOT)
        }

    def test_valid_bundle_passes(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            bundle = self._copy(Path(directory))
            self.assertEqual([], validate_process_bundle(bundle, REPOSITORY_ROOT))

    def test_passed_attempt_with_nonzero_exit_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            bundle = self._copy(Path(directory))
            path = bundle / "records/process-attempt.json"
            payload = json.loads(path.read_text(encoding="utf-8"))
            payload["execution"]["exit_code"] = 7
            path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
            self._rewrite_manifest(bundle)
            self.assertIn("PROCESS_EXIT_CONTRADICTION", self._codes(bundle))

    def test_safety_stop_contradiction_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            bundle = self._copy(Path(directory))
            path = bundle / "proof/termination.json"
            payload = json.loads(path.read_text(encoding="utf-8"))
            payload["reason"] = "LOW_AVAILABLE_MEMORY"
            path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
            self._rewrite_manifest(bundle)
            self.assertIn("SAFETY_STOP_CONTRADICTION", self._codes(bundle))

    def test_completed_event_before_first_token_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            bundle = self._copy(Path(directory))
            path = bundle / "events/events.jsonl"
            path.write_text(
                '{"event":"started"}\n'
                '{"event":"completed"}\n'
                '{"event":"first_token"}\n',
                encoding="utf-8",
            )
            self._rewrite_manifest(bundle)
            self.assertIn("EVENT_ORDER_INVALID", self._codes(bundle))

    def test_raw_output_hash_drift_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            bundle = self._copy(Path(directory))
            (bundle / "outputs/raw.txt").write_text("changed\n", encoding="utf-8")
            self._rewrite_manifest(bundle)
            self.assertIn("RAW_OUTPUT_HASH_MISMATCH", self._codes(bundle))

    def test_executable_payload_is_rejected_even_when_manifested(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            bundle = self._copy(Path(directory))
            (bundle / "payload.exe").write_bytes(b"not-an-executable")
            self._rewrite_manifest(bundle)
            self.assertIn("FORBIDDEN_PAYLOAD", self._codes(bundle))

    def test_shell_string_command_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            bundle = self._copy(Path(directory))
            path = bundle / "records/command.json"
            payload = json.loads(path.read_text(encoding="utf-8"))
            payload.pop("arguments")
            payload["command"] = "python fixture.py"
            path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
            self._rewrite_manifest(bundle)
            self.assertIn("COMMAND_BOUNDARY_INVALID", self._codes(bundle))

    def test_retry_without_prior_attempt_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            bundle = self._copy(Path(directory))
            path = bundle / "records/process-attempt.json"
            payload = json.loads(path.read_text(encoding="utf-8"))
            payload["attempt_number"] = 2
            payload["retry_of_attempt_id"] = "missing-attempt"
            path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
            self._rewrite_manifest(bundle)
            self.assertIn("RETRY_RELATION_INVALID", self._codes(bundle))


if __name__ == "__main__":
    unittest.main()
