import unittest

from scripts.testing.turbovec.gates import audit_gates


def metric(value):
    return {"n": 5, "median": value, "mean": value, "minimum": value, "maximum": value, "standard_deviation": 0.0, "ci95_low": value, "ci95_high": value}


class GateAuditTests(unittest.TestCase):
    def test_gate_b_prevents_integration_when_mixed_pdf_retrieval_was_not_run(self):
        candidate = {
            "recall_at_10": metric(0.95), "relative_ndcg_at_10": metric(0.97),
            "p95_slowdown_vs_exact": metric(1.05), "storage_ratio": metric(3.0),
            "source_accuracy": metric(0.98), "page_accuracy": metric(1.0),
            "absent_score_delta_vs_exact": metric(0.01),
        }
        summary = {"scale": 10_000, "valid_repetitions": 5, "statistics": {"tq4": candidate}}
        lifecycle = {"tq4": True}
        result = audit_gates(summary, lifecycle, mixed_pdf_retrieval=False, memory_safe=True)
        self.assertTrue(result["gate_a"]["tq4"]["passed"])
        self.assertFalse(result["gate_b"]["tq4"]["passed"])
        self.assertEqual("DEMONSTRATOR_ONLY", result["disposition"])

    def test_candidate_is_only_eligible_when_both_gates_pass(self):
        candidate = {
            "recall_at_10": metric(0.95), "relative_ndcg_at_10": metric(0.97),
            "p95_slowdown_vs_exact": metric(1.05), "storage_ratio": metric(3.0),
            "source_accuracy": metric(0.98), "page_accuracy": metric(1.0),
            "absent_score_delta_vs_exact": metric(0.01),
        }
        summary = {"scale": 10_000, "valid_repetitions": 5, "statistics": {"tq4": candidate}}
        result = audit_gates(summary, {"tq4": True}, mixed_pdf_retrieval=True, memory_safe=True)
        self.assertEqual("INTEGRATE_CANDIDATE", result["disposition"])


if __name__ == "__main__":
    unittest.main()
