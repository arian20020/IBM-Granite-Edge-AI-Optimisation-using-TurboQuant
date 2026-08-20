"""Identity-bound atomic checkpoint contracts for Workbook 05 C2."""

from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.checkpoint import (
    CheckpointGenerationError,
    CheckpointIdentity,
    CheckpointIdentityError,
    first_incomplete_step,
    load_and_verify_checkpoint,
    record_checkpoint_step,
)


_SHA_A = "a" * 64
_SHA_B = "b" * 64
_SHA_C = "c" * 64
_SHA_D = "d" * 64
_SHA_E = "e" * 64
_SHA_F = "f" * 64
_SHA_1 = "1" * 64


class Phase3CheckpointTests(unittest.TestCase):
    """Require generation and full execution identity before resume."""

    def setUp(self) -> None:
        self.directory = tempfile.TemporaryDirectory()
        self.root = Path(self.directory.name)
        self.path = self.root / "checkpoint.json"
        self.identity = CheckpointIdentity(
            repository_head="1" * 40,
            prerequisite_proof_sha256=_SHA_A,
            asset_lock_sha256=_SHA_B,
            executable_sha256=_SHA_C,
            request_sha256=_SHA_D,
            configuration_sha256=_SHA_E,
            prompt_sha256=_SHA_F,
            rubric_sha256=_SHA_1,
        )

    def tearDown(self) -> None:
        self.directory.cleanup()

    def test_recorded_step_is_atomic_generation_bound_and_resumable(self) -> None:
        evidence = self.root / "step-one.json"
        evidence.write_text('{"status":"Passed"}\n', encoding="utf-8")
        digest = hashlib.sha256(evidence.read_bytes()).hexdigest()

        checkpoint = record_checkpoint_step(
            self.path,
            expected_generation=0,
            identity=self.identity,
            step_id="prepare",
            status="Passed",
            evidence_path=evidence,
            evidence_sha256=digest,
        )

        self.assertEqual(1, checkpoint["generation"])
        loaded = load_and_verify_checkpoint(self.path, self.identity)
        self.assertEqual("Passed", loaded["steps"][0]["status"])
        self.assertIsNone(first_incomplete_step(loaded))
        self.assertFalse(self.path.with_suffix(".json.tmp").exists())
        self.assertFalse(self.path.with_suffix(".json.lock").exists())

    def test_changed_executable_rejects_resume(self) -> None:
        evidence = self.root / "evidence.txt"
        evidence.write_text("ok\n", encoding="utf-8")
        digest = hashlib.sha256(evidence.read_bytes()).hexdigest()
        record_checkpoint_step(
            self.path,
            0,
            self.identity,
            "prepare",
            "Passed",
            evidence,
            digest,
        )
        changed = CheckpointIdentity(
            **{
                **self.identity.as_dict(),
                "executable_sha256": "9" * 64,
            }
        )

        with self.assertRaisesRegex(CheckpointIdentityError, "executable_sha256"):
            load_and_verify_checkpoint(self.path, changed)

    def test_changed_evidence_bytes_reject_resume(self) -> None:
        evidence = self.root / "evidence.txt"
        evidence.write_text("before\n", encoding="utf-8")
        digest = hashlib.sha256(evidence.read_bytes()).hexdigest()
        record_checkpoint_step(
            self.path,
            0,
            self.identity,
            "prepare",
            "Passed",
            evidence,
            digest,
        )
        evidence.write_text("after\n", encoding="utf-8")

        with self.assertRaisesRegex(CheckpointIdentityError, "evidence SHA-256"):
            load_and_verify_checkpoint(self.path, self.identity)

    def test_generation_conflict_is_rejected(self) -> None:
        evidence = self.root / "evidence.txt"
        evidence.write_text("ok\n", encoding="utf-8")
        digest = hashlib.sha256(evidence.read_bytes()).hexdigest()
        record_checkpoint_step(
            self.path,
            0,
            self.identity,
            "prepare",
            "Passed",
            evidence,
            digest,
        )

        with self.assertRaises(CheckpointGenerationError):
            record_checkpoint_step(
                self.path,
                0,
                self.identity,
                "run",
                "Passed",
                evidence,
                digest,
            )

    def test_duplicate_step_is_rejected(self) -> None:
        evidence = self.root / "evidence.txt"
        evidence.write_text("ok\n", encoding="utf-8")
        digest = hashlib.sha256(evidence.read_bytes()).hexdigest()
        record_checkpoint_step(
            self.path,
            0,
            self.identity,
            "prepare",
            "Passed",
            evidence,
            digest,
        )

        with self.assertRaisesRegex(ValueError, "already exists"):
            record_checkpoint_step(
                self.path,
                1,
                self.identity,
                "prepare",
                "Passed",
                evidence,
                digest,
            )

    def test_first_incomplete_step_returns_first_nonpassed_step(self) -> None:
        checkpoint = {
            "steps": [
                {"step_id": "prepare", "status": "Passed"},
                {"step_id": "run", "status": "Pending"},
                {"step_id": "validate", "status": "Pending"},
            ]
        }
        self.assertEqual("run", first_incomplete_step(checkpoint))

    def test_checkpoint_json_is_bom_free(self) -> None:
        evidence = self.root / "evidence.txt"
        evidence.write_text("ok\n", encoding="utf-8")
        digest = hashlib.sha256(evidence.read_bytes()).hexdigest()
        record_checkpoint_step(
            self.path,
            0,
            self.identity,
            "prepare",
            "Passed",
            evidence,
            digest,
        )
        self.assertFalse(self.path.read_bytes().startswith(b"\xef\xbb\xbf"))
        json.loads(self.path.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
