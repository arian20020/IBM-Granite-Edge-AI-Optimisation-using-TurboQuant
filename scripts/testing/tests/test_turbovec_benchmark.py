from pathlib import Path
import tempfile
import unittest

import numpy as np

from scripts.testing.turbovec.benchmark import run_scale_benchmark
from scripts.testing.turbovec.embedding import DeterministicEmbeddingProvider
from scripts.testing.turbovec.schedule import CONFIGURATIONS, build_schedule


class BenchmarkTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        provider = DeterministicEmbeddingProvider()
        cls.documents = provider.embed_documents([f"document {i}" for i in range(32)])
        cls.queries = provider.embed_queries([f"query {i}" for i in range(8)])
        cls.ids = np.arange(100, 132, dtype=np.uint64)

    def test_all_formats_use_identical_inputs_and_separate_cold_warm_samples(self):
        schedule = build_schedule(seed=42, repetitions=2, query_count=8)
        with tempfile.TemporaryDirectory() as directory:
            result = run_scale_benchmark(
                self.documents, self.queries, self.ids, scale=32, schedule=schedule,
                output_root=Path(directory), warmup_batches=1, measured_batches=3,
                shared_document_metadata_bytes=700,
            )
        self.assertEqual(2, len(result["repetitions"]))
        for repetition in result["repetitions"]:
            self.assertTrue(repetition["valid"])
            self.assertEqual(list(repetition["configuration_order"]), list(repetition["configurations"]))
            hashes = {row["embedding_matrix_sha256"] for row in repetition["configurations"].values()}
            query_hashes = {row["query_matrix_sha256"] for row in repetition["configurations"].values()}
            self.assertEqual(1, len(hashes))
            self.assertEqual(1, len(query_hashes))
            for name in CONFIGURATIONS:
                row = repetition["configurations"][name]
                self.assertGreater(row["cold_query_latency_ms"], 0)
                self.assertEqual(1, row["warmup_batches"])
                self.assertEqual(3, len(row["warm_query_latency_ms"]))
                self.assertEqual(8, len(row["result_ids"]))
                self.assertTrue(row["lifecycle"]["save_reload_integrity"])
                self.assertTrue(row["lifecycle"]["corruption_rejected"])
                self.assertTrue(row["lifecycle"]["cleanup_completed"])
                self.assertEqual(700, row["storage"]["document_chunk_metadata_bytes"])

    def test_contamination_invalidates_whole_repetition_without_dropping_rows(self):
        schedule = build_schedule(seed=7, repetitions=1, query_count=8)

        def probe(name, phase):
            return (False, "synthetic background load") if name == "tq3" and phase == "after" else (True, "")

        with tempfile.TemporaryDirectory() as directory:
            result = run_scale_benchmark(
                self.documents, self.queries, self.ids, scale=32, schedule=schedule,
                output_root=Path(directory), warmup_batches=1, measured_batches=1,
                shared_document_metadata_bytes=0, validity_probe=probe,
            )
        repetition = result["repetitions"][0]
        self.assertFalse(repetition["valid"])
        self.assertIn("synthetic background load", repetition["invalidation_reasons"])
        self.assertEqual(set(CONFIGURATIONS), set(repetition["configurations"]))


if __name__ == "__main__":
    unittest.main()
