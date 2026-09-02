from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.checkpoint import (
    CheckpointConflictError,
    first_incomplete_step,
    load_checkpoint,
    record_step,
)


class CheckpointTests(unittest.TestCase):
    def _create_checkpoint(self, directory: Path) -> Path:
        path = directory / "checkpoint.json"
        path.write_text(
            json.dumps(
                {
                    "schema_version": "1.0",
                    "campaign_id": "GTQ-WB05-MF-v1",
                    "generation": 0,
                    "phase_order": ["phase-0-preflight", "phase-1-source-admission"],
                    "steps": [
                        {"step_id": "phase-0-preflight", "status": "Not started", "evidence_sha256": ""},
                        {"step_id": "phase-1-source-admission", "status": "Not started", "evidence_sha256": ""},
                    ],
                    "updated_at_utc": "2026-08-03T00:00:00Z",
                }
            ),
            encoding="utf-8",
        )
        return path

    def test_record_step_is_atomic_and_increments_generation(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            path = self._create_checkpoint(Path(temporary_directory))
            updated = record_step(path, expected_generation=0, step_id="phase-0-preflight", status="Passed", evidence_sha256="a" * 64)
            persisted = load_checkpoint(path)

            self.assertEqual(1, updated["generation"])
            self.assertEqual(updated, persisted)
            self.assertFalse(path.with_suffix(".json.tmp").exists())

    def test_stale_generation_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            path = self._create_checkpoint(Path(temporary_directory))
            record_step(path, expected_generation=0, step_id="phase-0-preflight", status="Passed", evidence_sha256="a" * 64)
            with self.assertRaises(CheckpointConflictError):
                record_step(path, expected_generation=0, step_id="phase-1-source-admission", status="Passed", evidence_sha256="b" * 64)

    def test_resume_returns_the_first_nonpassed_phase(self) -> None:
        checkpoint = {
            "phase_order": ["phase-0-preflight", "phase-1-source-admission"],
            "steps": [
                {"step_id": "phase-0-preflight", "status": "Passed"},
                {"step_id": "phase-1-source-admission", "status": "Blocked"},
            ],
        }
        self.assertEqual("phase-1-source-admission", first_incomplete_step(checkpoint))


if __name__ == "__main__":
    unittest.main()
