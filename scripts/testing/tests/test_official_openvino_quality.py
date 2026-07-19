import unittest

from scripts.testing.official_openvino.quality import score_response, terminal_quality_record


class OfficialOpenVINOQualityTests(unittest.TestCase):
    def test_precision_label_never_changes_quality_score(self):
        evidence = {"P1": 8, "P2": 7, "P3": 6, "P4": 5, "P5": 4, "P6": 3}
        self.assertEqual(score_response(evidence, format_label="TBQ3"),
                         score_response(evidence, format_label="F16"))

    def test_terminal_quality_has_all_prompts_without_fake_scores(self):
        row = terminal_quality_record("OV-TQ-01", "conversion-memory-gate", "conversion.json")
        self.assertEqual(set(row["prompts"]), {f"P{i}" for i in range(1, 7)})
        self.assertTrue(all(item["score"] is None for item in row["prompts"].values()))
        self.assertEqual(row["mean_score"], None)


if __name__ == "__main__":
    unittest.main()
