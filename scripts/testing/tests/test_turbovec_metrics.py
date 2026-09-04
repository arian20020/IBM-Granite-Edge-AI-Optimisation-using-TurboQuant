import unittest

from scripts.testing.turbovec.metrics import aggregate_latency, mean_reciprocal_rank, ndcg_at_k, recall_at_k


class MetricsTests(unittest.TestCase):
    def test_recall_and_ndcg_known_ranking(self):
        expected = {10, 20}; ranked = [10, 30, 20, 40]
        self.assertEqual(1.0, recall_at_k(ranked, expected, 4))
        self.assertAlmostEqual(0.96394, ndcg_at_k(ranked, {10: 3, 20: 1}, 4), places=5)
        self.assertAlmostEqual(0.93856, ndcg_at_k(ranked, {10: 3, 20: 2}, 4), places=5)
        self.assertEqual(0.25, mean_reciprocal_rank([[3, 1], [9, 8]], [{1}, {7}]))

    def test_latency_nearest_rank_and_rejects_bad_samples(self):
        result = aggregate_latency(list(range(1, 21)))
        self.assertEqual(10.5, result["p50"]); self.assertEqual(19, result["p95"])
        for values in ([], [1, float("nan")], [-1]):
            with self.assertRaises(ValueError): aggregate_latency(values)


if __name__ == "__main__": unittest.main()
