import json
from pathlib import Path
import tempfile
import unittest

from scripts.testing.turbovec.artifacts import generate_embedding_artifact
from scripts.testing.turbovec.dataset import build_frozen_dataset
from scripts.testing.turbovec.embedding import DeterministicEmbeddingProvider
from scripts.testing.turbovec.formal import run_formal_scale


class FormalScaleTests(unittest.TestCase):
    def test_formal_scale_requires_ready_machine_and_publishes_closed_evidence(self):
        dataset = build_frozen_dataset(30)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            artifact = generate_embedding_artifact(
                DeterministicEmbeddingProvider(), dataset, root / "artifact",
                {"revision": "fixture", "device": "CPU"}, batch_size=16,
            )
            readiness = root / "readiness"
            readiness.mkdir()
            (readiness / "decision.json").write_text(json.dumps({"ready": True, "coverage_seconds": 60}), encoding="utf-8")
            output = root / "run"
            run_formal_scale(artifact, readiness, output, scale=30, seed=44, repetitions=1, warmup_batches=1, measured_batches=1, enforce_recovery=False)
            terminal = json.loads((output / "terminal.json").read_text(encoding="utf-8"))
            self.assertEqual("completed", terminal["status"])
            self.assertEqual({"benchmark.json", "evaluation.json", "summary.json"}, {item["name"] for item in terminal["files"]})
            summary = json.loads((output / "summary.json").read_text(encoding="utf-8"))
            self.assertEqual(30, summary["scale"])
            self.assertEqual(1, summary["valid_repetitions"])

    def test_formal_scale_fails_closed_when_readiness_was_blocked(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            readiness = root / "readiness"
            readiness.mkdir()
            (readiness / "decision.json").write_text(json.dumps({"ready": False, "coverage_seconds": 60}), encoding="utf-8")
            with self.assertRaises(ValueError):
                run_formal_scale(root / "missing", readiness, root / "run", scale=30, seed=1)
            self.assertFalse((root / "run" / "terminal.json").exists())


if __name__ == "__main__":
    unittest.main()
