import hashlib
import unittest

from scripts.testing.official_openvino.quality import (
    score_adjudication,
    score_response,
    summarize_quality,
    terminal_quality_record,
    validate_response_record,
)


SHA_A = "a" * 64
SHA_B = "b" * 64
SHA_C = "c" * 64


def response_record(prompt_id: str = "P1", output: str = "same evidence") -> dict:
    return {
        "schema_version": 1,
        "status": "complete",
        "test_id": "OV-TQ-03",
        "context_tokens": 4096,
        "prompt_id": prompt_id,
        "prompt_set_id": "GTQ-PROMPTS-v1",
        "prompt_set_sha256": SHA_A,
        "prompt_sha256": SHA_B,
        "rubric_id": "GTQ-QUALITY-RUBRIC-v1",
        "runtime_summary_sha256": SHA_C,
        "runtime_config_sha256": "d" * 64,
        "generation_settings": {
            "temperature": 0.0,
            "top_p": 1.0,
            "seed": 42,
            "max_output_tokens": 256,
        },
        "output": output,
        "output_sha256": hashlib.sha256(output.encode("utf-8")).hexdigest(),
    }


def adjudication(score: float = 8.0, *, passed: bool = True, cap=None) -> dict:
    return {
        "dimensions": {
            "correctness_and_grounding": score,
            "instruction_and_format_adherence": score,
            "completeness_and_fact_retention": score,
            "relevance_clarity_and_coherence": score,
            "stability_and_output_integrity": score,
        },
        "deterministic_pass": passed,
        "critical_caps": [] if cap is None else [cap],
        "critical_cap_reason": "No critical cap" if cap is None else "objective gate failed",
        "format_valid": "Yes" if passed else "No",
        "required_facts_retained": "Yes" if passed else "No",
        "unsupported_statements_count": 0,
        "integrity_issue": "No",
        "manual_result": "accepted" if passed else "capped",
        "notes": "Harsh evidence-only adjudication.",
    }


class OfficialOpenVINOQualityTests(unittest.TestCase):
    def test_precision_label_never_changes_quality_score(self):
        evidence = {"P1": 8, "P2": 7, "P3": 6, "P4": 5, "P5": 4, "P6": 3}
        self.assertEqual(score_response(evidence, format_label="TBQ3"),
                         score_response(evidence, format_label="F16"))

    def test_terminal_quality_has_all_prompts_without_fake_scores(self):
        row = terminal_quality_record(
            "OV-TQ-18",
            "expected-runtime-rejection",
            "rejection.json",
            evidence_sha256=SHA_A,
        )
        self.assertEqual(set(row["prompts"]), {f"P{i}" for i in range(1, 7)})
        self.assertTrue(all(item["score"] is None for item in row["prompts"].values()))
        self.assertEqual(row["mean_score"], None)
        self.assertEqual(row["median_score"], None)
        self.assertEqual(row["evidence_sha256"], SHA_A)

    def test_response_record_is_bound_to_hashes_and_runtime_identity(self):
        record = response_record()
        expected_runtime = {
            "test_id": "OV-TQ-03",
            "context_tokens": 4096,
            "runtime_summary_sha256": SHA_C,
            "runtime_config_sha256": "d" * 64,
        }
        self.assertEqual(
            validate_response_record(
                record,
                expected_runtime=expected_runtime,
                expected_prompt_set_sha256=SHA_A,
                expected_prompt_sha256=SHA_B,
            )["output_sha256"],
            record["output_sha256"],
        )
        for field, bad_value in (
            ("output_sha256", "0" * 64),
            ("runtime_summary_sha256", "1" * 64),
            ("runtime_config_sha256", "2" * 64),
            ("prompt_set_sha256", "3" * 64),
            ("prompt_sha256", "4" * 64),
        ):
            broken = dict(record)
            broken[field] = bad_value
            with self.assertRaisesRegex(ValueError, field.replace("_", " ")):
                validate_response_record(
                    broken,
                    expected_runtime=expected_runtime,
                    expected_prompt_set_sha256=SHA_A,
                    expected_prompt_sha256=SHA_B,
                )

    def test_weighted_score_and_objective_cap_are_recomputed(self):
        evidence = adjudication()
        evidence["dimensions"].update({
            "correctness_and_grounding": 10,
            "instruction_and_format_adherence": 8,
            "completeness_and_fact_retention": 6,
            "relevance_clarity_and_coherence": 4,
            "stability_and_output_integrity": 2,
        })
        evidence["deterministic_pass"] = False
        evidence["critical_caps"] = [4]
        result = score_adjudication("P2", "e" * 64, evidence, format_label="TBQ3")
        self.assertEqual(result["uncapped_score"], 7.0)
        self.assertEqual(result["score"], 4.0)
        self.assertFalse(result["deterministic_pass"])

    def test_identical_content_is_scored_identically_under_different_labels(self):
        first = score_adjudication("P1", "e" * 64, adjudication(), format_label="F16")
        second = score_adjudication("P1", "e" * 64, adjudication(), format_label="TBQ3")
        self.assertEqual(first, second)

    def test_summary_requires_p1_to_p6_and_recomputes_all_aggregates(self):
        records = []
        for number, score in enumerate((1, 2, 3, 4, 5, 10), start=1):
            item = score_adjudication(
                f"P{number}",
                f"{number:064x}",
                adjudication(float(score)),
                format_label="hidden",
            )
            records.append(item)
        summary = summarize_quality("OV-TQ-03", records)
        self.assertEqual(summary["prompt_count"], 6)
        self.assertEqual(summary["mean_score"], 25 / 6)
        self.assertEqual(summary["median_score"], 3.5)
        self.assertEqual(summary["min_score"], 1.0)
        self.assertEqual(summary["max_score"], 10.0)
        with self.assertRaisesRegex(ValueError, "P1-P6"):
            summarize_quality("OV-TQ-03", records[:-1])

    def test_content_key_and_required_adjudication_fields_are_fail_closed(self):
        with self.assertRaisesRegex(ValueError, "output SHA256"):
            score_adjudication("P1", "not-a-sha", adjudication(), format_label="F16")
        incomplete = adjudication()
        incomplete.pop("notes")
        with self.assertRaisesRegex(ValueError, "adjudication fields"):
            score_adjudication("P1", "e" * 64, incomplete, format_label="F16")


if __name__ == "__main__":
    unittest.main()
