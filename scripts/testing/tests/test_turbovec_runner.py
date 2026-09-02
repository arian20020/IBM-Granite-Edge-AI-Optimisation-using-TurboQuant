import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from scripts.testing.turbovec.embedding import DeterministicEmbeddingProvider
from scripts.testing.turbovec.runner import (
    create_run_directory,
    evaluate_matched_result,
    prepare_controlled_inputs,
    run_fixture_campaign,
    run_live_campaign,
)
from scripts.testing.run_turbovec_feasibility import parser


class FailingProvider:
    def embed_documents(self, texts): raise RuntimeError("provider failed")
    def embed_queries(self, texts): raise RuntimeError("provider failed")


class RunnerTests(unittest.TestCase):
    def test_script_entry_point_does_not_shadow_published_turbovec(self):
        with tempfile.TemporaryDirectory() as root:
            run_id = "EXP-TV-COMP-001-20260903T010000Z-009"
            completed = subprocess.run(
                [sys.executable, str(Path(__file__).resolve().parents[1] / "run_turbovec_feasibility.py"),
                 "fixture", "--output-root", root, "--run-id", run_id],
                cwd=Path(__file__).resolve().parents[3], capture_output=True, text=True,
            )
            self.assertEqual(0, completed.returncode, completed.stderr)

    def test_cli_accepts_measured_model_and_output_contract(self):
        args = parser().parse_args([
            "measured", "--model-root", "model", "--output-root", "out",
            "--run-id", "EXP-TV-COMP-001-20260903T010000Z-002",
        ])
        self.assertEqual("measured", args.command)

    def test_live_campaign_publishes_query_metrics_and_closed_terminal(self):
        with tempfile.TemporaryDirectory() as root:
            run_id = "EXP-TV-COMP-001-20260903T010000Z-001"
            run_live_campaign(
                DeterministicEmbeddingProvider(),
                Path(__file__).resolve().parents[3],
                Path(root),
                run_id,
                {"provider": "deterministic-test-only"},
            )
            run = Path(root) / run_id
            metrics = json.loads((run / "metrics.json").read_text(encoding="utf-8"))
            terminal = json.loads((run / "terminal.json").read_text(encoding="utf-8"))
            self.assertEqual(120, metrics["queries"])
            self.assertEqual("completed", terminal["status"])
            self.assertEqual(4, len(terminal["files"]))

    def test_controlled_inputs_batch_real_protocol_and_evaluate_every_query(self):
        prepared = prepare_controlled_inputs(Path(__file__).resolve().parents[3], DeterministicEmbeddingProvider())
        self.assertEqual((30, 384), prepared["document_embeddings"].shape)
        self.assertEqual((120, 384), prepared["query_embeddings"].shape)
        result = run_fixture_campaign(DeterministicEmbeddingProvider())
        # Evaluation rejects a result that does not contain all controlled queries.
        with self.assertRaises(ValueError):
            evaluate_matched_result(result, prepared["query_ids"], prepared["grades"])

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
