import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.turbovec.embedding import DeterministicEmbeddingProvider
from scripts.testing.turbovec.runner import create_run_directory, run_fixture_campaign


class FailingProvider:
    def embed_documents(self, texts): raise RuntimeError("provider failed")
    def embed_queries(self, texts): raise RuntimeError("provider failed")


class RunnerTests(unittest.TestCase):
    def test_run_uses_identical_embedding_hash_and_matched_counts(self):
        result = run_fixture_campaign(DeterministicEmbeddingProvider())
        self.assertEqual(1, len({row["embedding_matrix_sha256"] for row in result["configurations"]}))
        self.assertEqual({"exact", "tq2", "tq3", "tq4"}, {row["name"] for row in result["configurations"]})
        self.assertTrue(all(row["warmup_batches"] == 5 and row["measured_batches"] == 30 for row in result["configurations"]))

    def test_existing_run_directory_is_never_overwritten(self):
        with tempfile.TemporaryDirectory() as root:
            run_id = "EXP-TV-COMP-001-20260902T120000Z-001"; Path(root, run_id).mkdir()
            with self.assertRaises(FileExistsError): create_run_directory(Path(root), run_id)

    def test_terminal_publication_is_closed_and_private(self):
        with tempfile.TemporaryDirectory() as root:
            run_id = "EXP-TV-COMP-001-20260902T120000Z-002"
            run_fixture_campaign(DeterministicEmbeddingProvider(), Path(root), run_id)
            terminal = json.loads((Path(root) / run_id / "terminal.json").read_text(encoding="utf-8"))
            self.assertEqual("completed", terminal["status"])
            combined = "".join(path.read_text(encoding="utf-8") for path in (Path(root) / run_id).glob("*.json*"))
            self.assertNotIn(str(Path(root)), combined); self.assertNotIn("PRIVATE_SENTINEL", combined)

    def test_provider_failure_never_publishes_success(self):
        with tempfile.TemporaryDirectory() as root:
            run_id = "EXP-TV-COMP-001-20260902T120000Z-003"
            with self.assertRaises(RuntimeError): run_fixture_campaign(FailingProvider(), Path(root), run_id)
            self.assertFalse((Path(root) / run_id / "terminal.json").exists())


if __name__ == "__main__": unittest.main()
