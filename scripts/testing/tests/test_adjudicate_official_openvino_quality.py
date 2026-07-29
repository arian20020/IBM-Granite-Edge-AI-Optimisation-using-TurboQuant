import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from scripts.testing.adjudicate_official_openvino_quality import (
    adjudicate_quality,
    build_blind_scoring_input,
    load_rubric,
)
from scripts.testing.run_official_openvino_quality import (
    QualityConfiguration,
    run_quality_campaign,
)
from scripts.testing.tests.test_run_official_openvino_quality import (
    COMPLETE_OUTPUTS,
    PROMPT_SET,
    RENDERED,
    RecordingExecutor,
    contains_null,
)


ROOT = Path(__file__).resolve().parents[3]
RUBRIC = (
    ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "rubrics"
    / "quality-rubric-v1.json"
)


def score_sheet(scoring_input, score=9):
    return {
        "schema_version": 1,
        "artifact_type": "openvino-quality-blind-scores",
        "prompt_set_id": scoring_input["prompt_set_id"],
        "rubric_id": scoring_input["rubric_id"],
        "scoring_input_sha256": scoring_input["scoring_input_sha256"],
        "adjudications": [
            {
                "blind_label": row["blind_label"],
                "prompt_id": row["prompt_id"],
                "response_sha256": row["response_sha256"],
                "dimensions": {
                    name: score for name in scoring_input["dimension_weights"]
                },
                "manual_critical_caps": [],
                "manual_cap_reasons": [],
                "unsupported_statements_count": 0,
                "reviewer": "Reviewer 1",
                "notes": "Scored independently from the response content.",
            }
            for row in scoring_input["responses"]
        ],
    }


class OfficialOpenVINOQualityAdjudicationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.raw_root = self.root / "raw"
        self.configurations = []
        for suffix, test_id, digest_char in (
            ("A7", "OV-TQ-03", "a"),
            ("K2", "OV-TQ-04", "b"),
        ):
            runtime = self.root / f"runtime-{suffix}.json"
            runtime.write_text(
                json.dumps(
                    {
                        "test_id": test_id,
                        "status": "complete",
                        "accepted": True,
                        "configuration_sha256": digest_char * 64,
                        "cleanup_process_count": 0,
                    }
                ),
                encoding="utf-8",
            )
            self.configurations.append(
                QualityConfiguration(
                    test_id=test_id,
                    blind_label=f"response-{suffix}",
                    configuration_sha256=digest_char * 64,
                    runtime_summary_path=runtime,
                    executor_command=(),
                )
            )

    def tearDown(self):
        self.temporary.cleanup()

    def capture(self, executor=None):
        run_quality_campaign(
            self.configurations,
            prompt_set_path=PROMPT_SET,
            rendered_root=RENDERED,
            output_root=self.raw_root,
            executor=executor or RecordingExecutor(),
        )

    def scoring_input(self):
        return build_blind_scoring_input(
            raw_root=self.raw_root,
            prompt_set_path=PROMPT_SET,
            rendered_root=RENDERED,
            rubric_path=RUBRIC,
        )

    def test_scoring_input_has_all_gates_and_no_configuration_identity(self):
        self.capture()
        payload = self.scoring_input()

        self.assertEqual(len(payload["responses"]), 12)
        self.assertEqual(set(payload["prompt_contracts"]), {f"P{i}" for i in range(1, 7)})
        self.assertIn(
            "IXN-TQ-7319",
            payload["prompt_contracts"]["P5"]["execution"]["prompt"],
        )
        self.assertTrue(
            all(row["deterministic_gate"]["passed"] for row in payload["responses"])
        )
        self.assertFalse(contains_null(payload))
        serialised = json.dumps(payload, sort_keys=True)
        self.assertNotIn("OV-TQ-03", serialised)
        self.assertNotIn("OV-TQ-04", serialised)
        self.assertNotIn("configuration_sha256", serialised)
        keys = set()

        def collect_keys(value):
            if isinstance(value, dict):
                keys.update(str(key).lower() for key in value)
                for item in value.values():
                    collect_keys(item)
            elif isinstance(value, list):
                for item in value:
                    collect_keys(item)

        collect_keys(payload)
        self.assertNotIn("precision", keys)
        self.assertNotIn("codec", keys)

    def test_harsh_objective_cap_overrides_perfect_manual_scores(self):
        class BadP2Executor(RecordingExecutor):
            def __call__(self, configuration, request_path, response_path):
                request = json.loads(request_path.read_text(encoding="utf-8"))
                prompt_id = request["prompt_id"]
                self.calls.append((configuration.blind_label, prompt_id))
                output = (
                    "This response ignores the exact three-line labels."
                    if prompt_id == "P2"
                    else COMPLETE_OUTPUTS[prompt_id]
                )
                result = {"status": "complete", "output": output}
                if prompt_id == "P6":
                    result["turn_1"] = "SAVED"
                response_path.write_text(json.dumps(result), encoding="utf-8")

        self.capture(BadP2Executor())
        blind_input = self.scoring_input()
        sheet = score_sheet(blind_input, score=10)
        result = adjudicate_quality(
            scoring_input=blind_input,
            score_sheet=sheet,
            blind_map={
                "response-A7": "OV-TQ-03",
                "response-K2": "OV-TQ-04",
            },
            rubric_path=RUBRIC,
        )

        for configuration in result["configurations"]:
            self.assertEqual(
                configuration["prompts"]["P2"]["critical_caps"], [4.0]
            )
            self.assertEqual(configuration["prompts"]["P2"]["final_score"], 4.0)
            self.assertEqual(configuration["mean_score"], 9.0)
            self.assertEqual(configuration["median_score"], 10.0)
            self.assertEqual(configuration["minimum_score"], 4.0)
            self.assertEqual(configuration["maximum_score"], 10.0)
        self.assertFalse(contains_null(result))

    def test_completed_empty_output_is_preserved_and_scores_zero(self):
        class EmptyP1Executor(RecordingExecutor):
            def __call__(self, configuration, request_path, response_path):
                request = json.loads(request_path.read_text(encoding="utf-8"))
                prompt_id = request["prompt_id"]
                self.calls.append((configuration.blind_label, prompt_id))
                result = {
                    "status": "complete",
                    "output": "" if prompt_id == "P1" else COMPLETE_OUTPUTS[prompt_id],
                }
                if prompt_id == "P6":
                    result["turn_1"] = "SAVED"
                response_path.write_text(json.dumps(result), encoding="utf-8")

        self.capture(EmptyP1Executor())
        blind_input = self.scoring_input()
        p1_rows = [
            row for row in blind_input["responses"] if row["prompt_id"] == "P1"
        ]
        self.assertTrue(all(row["output"] == "" for row in p1_rows))
        self.assertTrue(
            all(
                row["deterministic_gate"]["critical_caps"] == [0.0]
                for row in p1_rows
            )
        )

        result = adjudicate_quality(
            scoring_input=blind_input,
            score_sheet=score_sheet(blind_input, score=10),
            blind_map={
                "response-A7": "OV-TQ-03",
                "response-K2": "OV-TQ-04",
            },
            rubric_path=RUBRIC,
        )
        self.assertTrue(
            all(
                row["prompts"]["P1"]["final_score"] == 0.0
                for row in result["configurations"]
            )
        )

    def test_forged_hash_missing_prompt_and_null_score_are_rejected(self):
        self.capture()
        blind_input = self.scoring_input()
        valid = score_sheet(blind_input)

        forged = copy.deepcopy(valid)
        forged["adjudications"][0]["response_sha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "response hash"):
            adjudicate_quality(
                scoring_input=blind_input,
                score_sheet=forged,
                blind_map={
                    "response-A7": "OV-TQ-03",
                    "response-K2": "OV-TQ-04",
                },
                rubric_path=RUBRIC,
            )

        missing = copy.deepcopy(valid)
        missing["adjudications"].pop()
        with self.assertRaisesRegex(ValueError, "exactly one adjudication"):
            adjudicate_quality(
                scoring_input=blind_input,
                score_sheet=missing,
                blind_map={
                    "response-A7": "OV-TQ-03",
                    "response-K2": "OV-TQ-04",
                },
                rubric_path=RUBRIC,
            )

        null_score = copy.deepcopy(valid)
        null_score["adjudications"][0]["dimensions"][
            "correctness_and_grounding"
        ] = None
        with self.assertRaisesRegex(ValueError, "null"):
            adjudicate_quality(
                scoring_input=blind_input,
                score_sheet=null_score,
                blind_map={
                    "response-A7": "OV-TQ-03",
                    "response-K2": "OV-TQ-04",
                },
                rubric_path=RUBRIC,
            )

    def test_identical_text_cannot_receive_label_dependent_scores(self):
        self.capture()
        blind_input = self.scoring_input()
        sheet = score_sheet(blind_input)
        matches = [
            row
            for row in sheet["adjudications"]
            if row["prompt_id"] == "P1"
        ]
        self.assertEqual(len(matches), 2)
        matches[1]["dimensions"]["correctness_and_grounding"] = 8

        with self.assertRaisesRegex(ValueError, "label-dependent adjudication"):
            adjudicate_quality(
                scoring_input=blind_input,
                score_sheet=sheet,
                blind_map={
                    "response-A7": "OV-TQ-03",
                    "response-K2": "OV-TQ-04",
                },
                rubric_path=RUBRIC,
            )

    def test_p6_content_identity_includes_both_turns(self):
        class DifferentTurnOneExecutor(RecordingExecutor):
            def __call__(self, configuration, request_path, response_path):
                request = json.loads(request_path.read_text(encoding="utf-8"))
                prompt_id = request["prompt_id"]
                self.calls.append((configuration.blind_label, prompt_id))
                result = {
                    "status": "complete",
                    "output": COMPLETE_OUTPUTS[prompt_id],
                }
                if prompt_id == "P6":
                    result["turn_1"] = (
                        "SAVED"
                        if configuration.blind_label == "response-A7"
                        else "NOT-SAVED"
                    )
                response_path.write_text(json.dumps(result), encoding="utf-8")

        self.capture(DifferentTurnOneExecutor())
        blind_input = self.scoring_input()
        sheet = score_sheet(blind_input)
        different = next(
            row
            for row in sheet["adjudications"]
            if row["blind_label"] == "response-K2" and row["prompt_id"] == "P6"
        )
        different["dimensions"]["stability_and_output_integrity"] = 4

        result = adjudicate_quality(
            scoring_input=blind_input,
            score_sheet=sheet,
            blind_map={
                "response-A7": "OV-TQ-03",
                "response-K2": "OV-TQ-04",
            },
            rubric_path=RUBRIC,
        )
        self.assertEqual(
            result["configurations"][1]["prompts"]["P6"]["critical_caps"], [4.0]
        )

    def test_scoring_input_rejects_tampered_turn_evidence_and_objective_caps(self):
        self.capture()
        blind_input = self.scoring_input()
        p6 = next(
            row
            for row in blind_input["responses"]
            if row["blind_label"] == "response-A7" and row["prompt_id"] == "P6"
        )
        p6["turn_outputs"][0]["output"] = "NOT-SAVED"
        unsigned = {
            key: value
            for key, value in blind_input.items()
            if key != "scoring_input_sha256"
        }
        blind_input["scoring_input_sha256"] = hashlib.sha256(
            json.dumps(
                unsigned,
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
        ).hexdigest()
        with self.assertRaisesRegex(ValueError, "turn output hash"):
            adjudicate_quality(
                scoring_input=blind_input,
                score_sheet=score_sheet(blind_input),
                blind_map={
                    "response-A7": "OV-TQ-03",
                    "response-K2": "OV-TQ-04",
                },
                rubric_path=RUBRIC,
            )

        blind_input = self.scoring_input()
        p2 = next(
            row
            for row in blind_input["responses"]
            if row["blind_label"] == "response-A7" and row["prompt_id"] == "P2"
        )
        p2["deterministic_gate"] = {
            "passed": False,
            "checks": {"forged": True},
            "critical_caps": [9],
            "reasons": ["forged cap"],
        }
        unsigned = {
            key: value
            for key, value in blind_input.items()
            if key != "scoring_input_sha256"
        }
        blind_input["scoring_input_sha256"] = hashlib.sha256(
            json.dumps(
                unsigned,
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
        ).hexdigest()
        with self.assertRaisesRegex(ValueError, "deterministic gate cap"):
            adjudicate_quality(
                scoring_input=blind_input,
                score_sheet=score_sheet(blind_input),
                blind_map={
                    "response-A7": "OV-TQ-03",
                    "response-K2": "OV-TQ-04",
                },
                rubric_path=RUBRIC,
            )

    def test_valid_shape_gate_removal_cannot_override_objective_failure(self):
        class BadP2Executor(RecordingExecutor):
            def __call__(self, configuration, request_path, response_path):
                request = json.loads(request_path.read_text(encoding="utf-8"))
                prompt_id = request["prompt_id"]
                result = {
                    "status": "complete",
                    "output": (
                        "unlabelled format failure"
                        if prompt_id == "P2"
                        else COMPLETE_OUTPUTS[prompt_id]
                    ),
                }
                if prompt_id == "P6":
                    result["turn_1"] = "SAVED"
                response_path.write_text(json.dumps(result), encoding="utf-8")

        self.capture(BadP2Executor())
        blind_input = self.scoring_input()
        p2 = next(
            row
            for row in blind_input["responses"]
            if row["blind_label"] == "response-A7" and row["prompt_id"] == "P2"
        )
        self.assertEqual(p2["deterministic_gate"]["critical_caps"], [4.0])
        p2["deterministic_gate"] = {
            "passed": True,
            "checks": {"forged_pass": True},
            "critical_caps": [],
            "reasons": [],
        }
        unsigned = {
            key: value
            for key, value in blind_input.items()
            if key != "scoring_input_sha256"
        }
        blind_input["scoring_input_sha256"] = hashlib.sha256(
            json.dumps(
                unsigned,
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
        ).hexdigest()

        with self.assertRaisesRegex(ValueError, "deterministic gate mismatch"):
            adjudicate_quality(
                scoring_input=blind_input,
                score_sheet=score_sheet(blind_input, score=10),
                blind_map={
                    "response-A7": "OV-TQ-03",
                    "response-K2": "OV-TQ-04",
                },
                rubric_path=RUBRIC,
            )

    def test_frozen_rubric_rejects_same_id_weight_substitution(self):
        rubric_path = self.root / "substituted-rubric.json"
        rubric = json.loads(RUBRIC.read_text(encoding="utf-8"))
        replacement_weights = (0.3, 0.1, 0.2, 0.2, 0.2)
        for row, weight in zip(rubric["dimensions"], replacement_weights):
            row["weight"] = weight
        rubric_path.write_text(json.dumps(rubric), encoding="utf-8")

        with self.assertRaisesRegex(ValueError, "frozen rubric hash"):
            load_rubric(rubric_path)

    def test_rubric_parses_the_exact_bytes_that_were_hashed(self):
        altered = json.loads(RUBRIC.read_text(encoding="utf-8"))
        replacement_weights = (0.3, 0.1, 0.2, 0.2, 0.2)
        for row, weight in zip(altered["dimensions"], replacement_weights):
            row["weight"] = weight
        original_read_text = Path.read_text

        def switched_read_text(path, *args, **kwargs):
            if path == RUBRIC:
                return json.dumps(altered)
            return original_read_text(path, *args, **kwargs)

        with patch.object(Path, "read_text", switched_read_text):
            rubric = load_rubric(RUBRIC)
        self.assertEqual(
            rubric["weights"],
            {
                "correctness_and_grounding": 0.3,
                "instruction_and_format_adherence": 0.25,
                "completeness_and_fact_retention": 0.2,
                "relevance_clarity_and_coherence": 0.15,
                "stability_and_output_integrity": 0.1,
            },
        )

    def test_tampered_raw_response_cannot_enter_scoring_input(self):
        self.capture()
        path = self.raw_root / "response-A7" / "P3" / "response.json"
        response = json.loads(path.read_text(encoding="utf-8"))
        response["output"] = "{}"
        path.write_text(json.dumps(response), encoding="utf-8")

        with self.assertRaisesRegex(ValueError, "output hash"):
            self.scoring_input()


if __name__ == "__main__":
    unittest.main()
