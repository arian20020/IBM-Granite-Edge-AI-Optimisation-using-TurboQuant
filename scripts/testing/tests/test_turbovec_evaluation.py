import unittest

from scripts.testing.turbovec.dataset import build_frozen_dataset
from scripts.testing.turbovec.evaluation import evaluate_repetition, summarize_repetitions


class EvaluationTests(unittest.TestCase):
    def test_quality_uses_frozen_evaluation_relevance_and_provenance(self):
        dataset = build_frozen_dataset(30)
        ids_by_chunk = {chunk.chunk_id: chunk.ordinal for chunk in dataset.chunks}
        rankings = []
        scores = []
        for query in dataset.queries:
            relevant = [ids_by_chunk[item] for item in dataset.relevance[query.query_id]]
            row = relevant + [item for item in range(dataset.scale) if item not in relevant]
            rankings.append(row[:10])
            scores.append([1.0 - rank / 100 for rank in range(10)])
        configuration = {
            "result_ids": rankings,
            "result_scores": scores,
            "latency_summary_ms": {"p95": 2.0},
            "storage": {"equivalent_total_bytes": 100},
            "memory": {"incremental_peak_process_working_set_bytes": 20},
            "lifecycle": {"save_reload_integrity": True, "corruption_rejected": True, "cleanup_completed": True},
        }
        repetition = {
            "query_order": list(range(len(dataset.queries))),
            "valid": True,
            "configurations": {name: dict(configuration) for name in ("exact", "tq2", "tq3", "tq4")},
        }
        result = evaluate_repetition(dataset, repetition)
        for row in result["configurations"].values():
            self.assertEqual(1.0, row["recall_at_10"])
            self.assertEqual(1.0, row["relative_ndcg_at_10"])
            self.assertEqual(1.0, row["source_accuracy"])
            self.assertEqual(1.0, row["page_accuracy"])
        self.assertEqual(12, result["evaluation_query_counts"]["absent_answer"])

    def test_summary_uses_all_valid_repetitions_and_reports_95_percent_ci(self):
        rows = []
        for value in (1.0, 2.0, 3.0, 4.0, 5.0):
            rows.append({"valid": True, "configurations": {"exact": {"p95_latency_ms": value}}})
        summary = summarize_repetitions(rows)
        metric = summary["exact"]["p95_latency_ms"]
        self.assertEqual(5, metric["n"])
        self.assertEqual(3.0, metric["median"])
        self.assertLess(metric["ci95_low"], 3.0)
        self.assertGreater(metric["ci95_high"], 3.0)


if __name__ == "__main__":
    unittest.main()
