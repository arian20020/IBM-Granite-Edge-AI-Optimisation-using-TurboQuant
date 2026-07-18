import unittest


class AnimehackerQualityTests(unittest.TestCase):
    def test_precision_label_does_not_change_score(self):
        from scripts.testing.atomicbot.quality import score_response

        decision = {"dimensions": {
            "correctness_and_grounding": 6, "instruction_and_format_adherence": 6,
            "completeness_and_fact_retention": 6, "relevance_clarity_and_coherence": 6,
            "stability_and_output_integrity": 6}, "deterministic_pass": True,
            "critical_caps": []}
        a = score_response("P1", "same response", decision)
        b = score_response("P1", "same response", decision)
        self.assertEqual(a.final_score, b.final_score)

    def test_structural_failure_caps_score_harshly(self):
        from scripts.testing.atomicbot.quality import score_response

        decision = {"dimensions": {
            "correctness_and_grounding": 9, "instruction_and_format_adherence": 9,
            "completeness_and_fact_retention": 9, "relevance_clarity_and_coherence": 9,
            "stability_and_output_integrity": 9}, "deterministic_pass": False,
            "critical_caps": [2]}
        self.assertEqual(score_response("P3", "invalid", decision).final_score, 2)


if __name__ == "__main__":
    unittest.main()
