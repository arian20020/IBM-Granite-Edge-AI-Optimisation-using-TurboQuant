import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.turbovec.contracts import (
    Decision,
    load_control_manifest,
    load_control_manifest_dict,
    validate_command_arithmetic,
    validate_evidence,
)
from scripts.testing.turbovec.provenance import sha256_file


ROOT = Path(__file__).resolve().parents[3]
MANIFEST = ROOT / "experiments/manifests/turbovec/feasibility-v1.json"


class TurboVecControlContractTests(unittest.TestCase):
    def test_manifest_locks_candidate_and_thresholds(self):
        manifest = load_control_manifest(MANIFEST)
        self.assertEqual("ccab9f325e6ce2a270a87daf01ae4e443bcf2d49", manifest.turbovec.commit)
        self.assertEqual(384, manifest.embedding.dimension)
        self.assertEqual(30, manifest.measured_batches)
        self.assertEqual(5, manifest.warmup_batches)

    def test_rejects_wrong_turbovec_wheel_hash(self):
        document = json.loads(MANIFEST.read_text(encoding="utf-8"))
        document["dependencies"]["turbovec"]["wheel_sha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "turbovec wheel"):
            load_control_manifest_dict(document)

    def test_all_four_outcomes_are_closed_enum_values(self):
        self.assertEqual(
            {item.value for item in Decision},
            {"INTEGRATE", "DEMONSTRATOR_ONLY", "EXCLUDE", "BLOCKED"},
        )

    def test_evidence_rejects_unknown_and_private_path(self):
        evidence = {
            "schema_version": "1.0",
            "run_id": "EXP-TV-COMP-001-20260902T120000Z-001",
            "command": {"discovered": 4, "executed": 4, "passed": 4, "failed": 0, "skipped": 0},
        }
        validate_evidence(evidence)
        evidence["private_path"] = r"C:\Users\Someone\secret.pdf"
        with self.assertRaises(ValueError):
            validate_evidence(evidence)

    def test_command_arithmetic_is_closed(self):
        validate_command_arithmetic({"discovered": 5, "executed": 4, "passed": 3, "failed": 1, "skipped": 1})
        with self.assertRaises(ValueError):
            validate_command_arithmetic({"discovered": 5, "executed": 4, "passed": 4, "failed": 1, "skipped": 0})

    def test_sha256_file_is_lowercase_and_byte_exact(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "sample.bin"
            path.write_bytes(b"abc")
            self.assertEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", sha256_file(path))


if __name__ == "__main__":
    unittest.main()
