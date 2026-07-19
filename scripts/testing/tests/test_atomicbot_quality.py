import hashlib
import tempfile
import unittest
from pathlib import Path


class AtomicBotQualityTests(unittest.TestCase):
    def test_score_depends_on_evidence_not_format_label(self):
        from scripts.testing.atomicbot.quality import score_response

        adjudication = {"dimensions": {
            "correctness_and_grounding": 8,
            "instruction_and_format_adherence": 8,
            "completeness_and_fact_retention": 8,
            "relevance_clarity_and_coherence": 8,
            "stability_and_output_integrity": 8,
        }, "deterministic_pass": True, "critical_caps": []}
        first = score_response("P1", "same output", adjudication)
        second = score_response("P1", "same output", adjudication)
        self.assertEqual(first.final_score, second.final_score)
        self.assertEqual(first.final_score, 8.0)

    def test_deterministic_failure_and_critical_cap_are_enforced(self):
        from scripts.testing.atomicbot.quality import score_response

        adjudication = {"dimensions": {
            "correctness_and_grounding": 10,
            "instruction_and_format_adherence": 10,
            "completeness_and_fact_retention": 10,
            "relevance_clarity_and_coherence": 10,
            "stability_and_output_integrity": 10,
        }, "deterministic_pass": False, "critical_caps": [4]}
        result = score_response("P2", "wrong format", adjudication)
        self.assertEqual(result.uncapped_score, 10.0)
        self.assertEqual(result.final_score, 4.0)
        self.assertFalse(result.deterministic_pass)

    def test_perplexity_fixture_requires_matching_hash(self):
        from scripts.testing.atomicbot.quality import run_perplexity_gate

        self.assertFalse(run_perplexity_gate(None, None).allowed)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "dataset.txt"
            path.write_text("controlled dataset", encoding="utf-8")
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            self.assertTrue(run_perplexity_gate(path, digest).allowed)
            self.assertEqual(run_perplexity_gate(path, "0" * 64).reason, "dataset-hash-mismatch")


if __name__ == "__main__":
    unittest.main()
