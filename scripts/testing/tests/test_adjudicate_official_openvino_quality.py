import copy
import hashlib
import json
import os
import subprocess
import tempfile
import unittest
from dataclasses import replace
from pathlib import Path
from unittest.mock import patch

import pytest

import scripts.testing.adjudicate_official_openvino_quality as adjudicator
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
from scripts.testing.tests.test_official_openvino_quality_campaign import (
    _accepted_input,
    _capture_api,
    _install_capture_runner,
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


def _write_governed_json(path, value):
    path.write_bytes(
        (
            json.dumps(
                value,
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            )
            + "\n"
        ).encode("utf-8")
    )


def _write_guard_json(path, value):
    path.write_bytes(
        (
            json.dumps(
                value,
                ensure_ascii=False,
                indent=2,
                sort_keys=True,
            )
            + "\n"
        ).encode("utf-8")
    )


def _governed_capture_root(
    tmp_path,
    monkeypatch,
    *,
    failed_turn_ids=(),
):
    raw_root = tmp_path / "blind-captures"
    _install_capture_runner(
        monkeypatch,
        failed_turn_ids=set(failed_turn_ids),
    )
    _add_governed_capture(
        raw_root,
        tmp_path,
        blind_label="response-A7",
    )
    return raw_root


def _add_governed_capture(
    raw_root,
    tmp_path,
    *,
    blind_label,
):
    source = _accepted_input(tmp_path / f"accepted-{blind_label}")
    source = replace(
        source,
        output_root=raw_root / blind_label,
    )
    _capture_api()(source, resume=False)


def _governed_scoring_input(raw_root):
    return build_blind_scoring_input(
        raw_root=raw_root,
        prompt_set_path=PROMPT_SET,
        rendered_root=RENDERED,
        rubric_path=RUBRIC,
    )


def _resign_governed_record(record):
    unsigned = {
        key: value for key, value in record.items() if key != "record_sha256"
    }
    record["record_sha256"] = hashlib.sha256(
        json.dumps(
            unsigned,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    ).hexdigest()


def _resign_governed_receipt(receipt):
    unsigned = {
        key: value
        for key, value in receipt.items()
        if key != "governed_execution_sha256"
    }
    receipt["governed_execution_sha256"] = hashlib.sha256(
        (
            json.dumps(
                unsigned,
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            )
            + "\n"
        ).encode("utf-8")
    ).hexdigest()


def _resign_governed_summary(summary):
    unsigned = {
        key: value
        for key, value in summary.items()
        if key != "capture_sha256"
    }
    summary["capture_sha256"] = hashlib.sha256(
        json.dumps(
            unsigned,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    ).hexdigest()


def _rebind_governed_evidence_hash(root, field, digest):
    receipt_path = root / "governed-execution.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    receipt[field] = digest
    _resign_governed_receipt(receipt)
    _write_governed_json(receipt_path, receipt)

    record_hashes = {}
    for prompt_id in (f"P{number}" for number in range(1, 7)):
        record_path = root / prompt_id / "response.json"
        record = json.loads(record_path.read_text(encoding="utf-8"))
        record[field] = digest
        _resign_governed_record(record)
        _write_governed_json(record_path, record)
        record_hashes[prompt_id] = record["record_sha256"]

    summary_path = root / "capture-summary.json"
    summary = json.loads(summary_path.read_text(encoding="utf-8"))
    summary[field] = digest
    for prompt_id, record_sha256 in record_hashes.items():
        summary["responses"][prompt_id]["record_sha256"] = record_sha256
    _resign_governed_summary(summary)
    _write_governed_json(summary_path, summary)


def _resign_worker_result(worker_result):
    unsigned = {
        "schema": worker_result["schema"],
        "outcomes": worker_result["outcomes"],
    }
    worker_result["worker_result_sha256"] = hashlib.sha256(
        (
            json.dumps(
                unsigned,
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            )
            + "\n"
        ).encode("utf-8")
    ).hexdigest()


def _redirect_guard_executable(guard, capture_root):
    attacker = capture_root.parent.parent / "attacker.exe"
    attacker.write_bytes(b"synthetic non-Python executable")
    guard["command"][0] = str(attacker.resolve())


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


def test_governed_capture_projects_six_blind_rows_without_private_identity(
    tmp_path,
    monkeypatch,
):
    raw_root = _governed_capture_root(tmp_path, monkeypatch)
    summary = json.loads(
        (
            raw_root
            / "response-A7"
            / "capture-summary.json"
        ).read_text(encoding="utf-8")
    )

    scoring = _governed_scoring_input(raw_root)

    assert len(scoring["responses"]) == 6
    assert {
        row["prompt_id"] for row in scoring["responses"]
    } == {f"P{number}" for number in range(1, 7)}
    assert {
        row["blind_label"] for row in scoring["responses"]
    } == {"response-A7"}
    forbidden_keys = {
        "test_id",
        "context_tokens",
        "campaign_identity_sha256",
        "runtime_summary_sha256",
        "runtime_config_sha256",
        "quality_worker_spec_sha256",
        "worker_result_sha256",
        "guard_evidence_sha256",
        "record_sha256",
        "precision",
        "codec",
        "device",
        "model",
        "build",
    }
    assert all(
        forbidden_keys.isdisjoint(row)
        for row in scoring["responses"]
    )
    encoded_rows = json.dumps(
        scoring["responses"],
        ensure_ascii=False,
        sort_keys=True,
    )
    assert summary["test_id"] not in encoded_rows
    assert summary["campaign_identity_sha256"] not in encoded_rows
    assert "TBQ" not in encoded_rows
    p6 = next(
        row for row in scoring["responses"] if row["prompt_id"] == "P6"
    )
    assert len(p6["turn_outputs"]) == 2
    assert p6["turn_outputs"][0]["output"] == "ACTUAL-SAVED\r\nverbatim"
    assert p6["turn_outputs"][1]["output"] == "  exact P6-turn-2\r\n"


def test_governed_capture_preserves_scoring_caps_and_unblinding(
    tmp_path,
    monkeypatch,
):
    scoring = _governed_scoring_input(
        _governed_capture_root(tmp_path, monkeypatch)
    )

    result = adjudicate_quality(
        scoring_input=scoring,
        score_sheet=score_sheet(scoring, score=10),
        blind_map={"response-A7": "OV-TQ-03"},
        rubric_path=RUBRIC,
    )

    assert result["status"] == "complete"
    assert result["configurations"][0]["test_id"] == "OV-TQ-03"
    assert result["configurations"][0]["blind_label"] == "response-A7"
    assert any(
        prompt["critical_caps"]
        for prompt in result["configurations"][0]["prompts"].values()
    )
    assert all(
        prompt["final_score"]
        <= min(prompt["critical_caps"] or [10.0])
        for prompt in result["configurations"][0]["prompts"].values()
    )


def test_governed_adapter_rejects_tampered_record_self_hash(
    tmp_path,
    monkeypatch,
):
    raw_root = _governed_capture_root(tmp_path, monkeypatch)
    path = raw_root / "response-A7" / "P6" / "response.json"
    record = json.loads(path.read_text(encoding="utf-8"))
    record["record_sha256"] = "0" * 64
    _write_governed_json(path, record)

    with pytest.raises(ValueError, match="record hash"):
        _governed_scoring_input(raw_root)


def test_governed_adapter_rejects_resigned_forged_response_hash(
    tmp_path,
    monkeypatch,
):
    raw_root = _governed_capture_root(tmp_path, monkeypatch)
    path = raw_root / "response-A7" / "P3" / "response.json"
    record = json.loads(path.read_text(encoding="utf-8"))
    record["response_sha256"] = "0" * 64
    _resign_governed_record(record)
    _write_governed_json(path, record)

    with pytest.raises(ValueError, match="prompt or output hash"):
        _governed_scoring_input(raw_root)


def test_governed_adapter_rejects_tampered_capture_summary_hash(
    tmp_path,
    monkeypatch,
):
    raw_root = _governed_capture_root(tmp_path, monkeypatch)
    path = raw_root / "response-A7" / "capture-summary.json"
    summary = json.loads(path.read_text(encoding="utf-8"))
    summary["capture_sha256"] = "0" * 64
    _write_governed_json(path, summary)

    with pytest.raises(ValueError, match="capture summary hash"):
        _governed_scoring_input(raw_root)


def test_governed_adapter_binds_summary_to_actual_guard_evidence_bytes(
    tmp_path,
    monkeypatch,
):
    raw_root = _governed_capture_root(tmp_path, monkeypatch)
    path = (
        raw_root
        / "response-A7"
        / "governed"
        / "guard-evidence.json"
    )
    path.write_bytes(path.read_bytes() + b" ")

    with pytest.raises(ValueError, match="guard evidence hash"):
        _governed_scoring_input(raw_root)


def test_governed_adapter_rejects_rehashed_wrong_guard_ram_floor(
    tmp_path,
    monkeypatch,
):
    raw_root = _governed_capture_root(tmp_path, monkeypatch)
    root = raw_root / "response-A7"
    guard_path = root / "governed" / "guard-evidence.json"
    guard = json.loads(guard_path.read_text(encoding="utf-8"))
    guard["configured_minimum_available_ram_bytes"] = 1
    _write_guard_json(guard_path, guard)
    _rebind_governed_evidence_hash(
        root,
        "guard_evidence_sha256",
        hashlib.sha256(guard_path.read_bytes()).hexdigest(),
    )

    with pytest.raises(ValueError, match="RAM floor"):
        _governed_scoring_input(raw_root)


@pytest.mark.parametrize(
    ("mutate", "message"),
    [
        (
            lambda guard, _root: guard.update(
                schema="official-openvino-owned-process-guard/legacy"
            ),
            "guard schema",
        ),
        (
            lambda guard, _root: guard.update(command=["attacker.exe"]),
            "guard command",
        ),
        (
            _redirect_guard_executable,
            "Python executable",
        ),
        (
            lambda guard, root: guard["command"].__setitem__(
                -3,
                str(root / "governed" / "worker-result.json"),
            ),
            "guard command",
        ),
        (
            lambda guard, _root: guard.update(
                environment_sha256="0" * 64
            ),
            "environment identity",
        ),
        (
            lambda guard, _root: guard.update(
                working_directory="C:/attacker"
            ),
            "working directory",
        ),
        (
            lambda guard, _root: guard.update(
                maximum_runtime_seconds=False
            ),
            "timeout",
        ),
        (
            lambda guard, root: guard.update(
                log_path=str(root / "governed" / "worker-spec.json")
            ),
            "guard log path",
        ),
        (
            lambda guard, root: guard.update(
                evidence_path=str(root / "governed" / "worker-spec.json")
            ),
            "guard evidence path",
        ),
    ],
)
def test_governed_adapter_rejects_consistently_rehashed_guard_identity(
    tmp_path,
    monkeypatch,
    mutate,
    message,
):
    raw_root = _governed_capture_root(tmp_path, monkeypatch)
    root = raw_root / "response-A7"
    guard_path = root / "governed" / "guard-evidence.json"
    guard = json.loads(guard_path.read_text(encoding="utf-8"))
    mutate(guard, root)
    _write_guard_json(guard_path, guard)
    _rebind_governed_evidence_hash(
        root,
        "guard_evidence_sha256",
        hashlib.sha256(guard_path.read_bytes()).hexdigest(),
    )

    with pytest.raises(ValueError, match=message):
        _governed_scoring_input(raw_root)


def test_governed_adapter_rejects_consistently_rehashed_stale_worker_spec(
    tmp_path,
    monkeypatch,
):
    raw_root = _governed_capture_root(tmp_path, monkeypatch)
    root = raw_root / "response-A7"
    spec_path = root / "governed" / "worker-spec.json"
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    spec["schema"] = "official-openvino-wb04-quality-worker-spec/legacy"
    _write_governed_json(spec_path, spec)
    spec_sha256 = hashlib.sha256(spec_path.read_bytes()).hexdigest()

    receipt_path = root / "governed-execution.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    receipt["quality_worker_spec_sha256"] = spec_sha256
    _resign_governed_receipt(receipt)
    _write_governed_json(receipt_path, receipt)

    record_hashes = {}
    for prompt_id in (f"P{number}" for number in range(1, 7)):
        record_path = root / prompt_id / "response.json"
        record = json.loads(record_path.read_text(encoding="utf-8"))
        record["quality_worker_spec_sha256"] = spec_sha256
        _resign_governed_record(record)
        _write_governed_json(record_path, record)
        record_hashes[prompt_id] = record["record_sha256"]

    summary_path = root / "capture-summary.json"
    summary = json.loads(summary_path.read_text(encoding="utf-8"))
    summary["quality_worker_spec_sha256"] = spec_sha256
    for prompt_id, record_sha256 in record_hashes.items():
        summary["responses"][prompt_id]["record_sha256"] = record_sha256
    _resign_governed_summary(summary)
    _write_governed_json(summary_path, summary)

    with pytest.raises(ValueError, match="worker spec schema"):
        _governed_scoring_input(raw_root)


def test_governed_adapter_rejects_rehashed_spec_prompt_not_executed(
    tmp_path,
    monkeypatch,
):
    raw_root = _governed_capture_root(tmp_path, monkeypatch)
    root = raw_root / "response-A7"
    spec_path = root / "governed" / "worker-spec.json"
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    spec["prompts"][0]["prompt"] = "different but structurally valid prompt"
    _write_governed_json(spec_path, spec)
    _rebind_governed_evidence_hash(
        root,
        "quality_worker_spec_sha256",
        hashlib.sha256(spec_path.read_bytes()).hexdigest(),
    )

    with pytest.raises(ValueError, match="worker spec does not match"):
        _governed_scoring_input(raw_root)


def test_governed_adapter_rejects_rehashed_invalid_worker_outcome_type(
    tmp_path,
    monkeypatch,
):
    raw_root = _governed_capture_root(tmp_path, monkeypatch)
    root = raw_root / "response-A7"
    worker_path = root / "governed" / "worker-result.json"
    worker = json.loads(worker_path.read_text(encoding="utf-8"))
    worker["outcomes"][0]["status"] = True
    _resign_worker_result(worker)
    _write_governed_json(worker_path, worker)
    _rebind_governed_evidence_hash(
        root,
        "worker_result_sha256",
        hashlib.sha256(worker_path.read_bytes()).hexdigest(),
    )

    with pytest.raises(ValueError, match="worker result outcome status"):
        _governed_scoring_input(raw_root)


def test_governed_adapter_rejects_rehashed_worker_output_not_in_records(
    tmp_path,
    monkeypatch,
):
    raw_root = _governed_capture_root(tmp_path, monkeypatch)
    root = raw_root / "response-A7"
    worker_path = root / "governed" / "worker-result.json"
    worker = json.loads(worker_path.read_text(encoding="utf-8"))
    worker["outcomes"][0]["raw_output"] = "forged worker-only output"
    worker["outcomes"][0]["raw_output_sha256"] = hashlib.sha256(
        b"forged worker-only output"
    ).hexdigest()
    _resign_worker_result(worker)
    _write_governed_json(worker_path, worker)
    _rebind_governed_evidence_hash(
        root,
        "worker_result_sha256",
        hashlib.sha256(worker_path.read_bytes()).hexdigest(),
    )

    with pytest.raises(ValueError, match="worker result does not match"):
        _governed_scoring_input(raw_root)


def test_governed_adapter_requires_exactly_six_capture_records(
    tmp_path,
    monkeypatch,
):
    raw_root = _governed_capture_root(tmp_path, monkeypatch)
    (raw_root / "response-A7" / "P3" / "response.json").unlink()

    with pytest.raises(ValueError, match="incomplete or unexpected"):
        _governed_scoring_input(raw_root)


def test_governed_adapter_rejects_hardlink_aliases_before_projection(
    tmp_path,
    monkeypatch,
):
    raw_root = _governed_capture_root(tmp_path, monkeypatch)
    source = raw_root / "response-A7" / "P1" / "response.json"
    alias = raw_root / "response-A7" / "P2" / "response.json"
    alias.unlink()
    try:
        os.link(source, alias)
    except OSError as error:
        pytest.skip(f"hardlink creation unavailable: {error}")

    with pytest.raises(ValueError, match="alias"):
        _governed_scoring_input(raw_root)


def test_governed_adapter_rejects_junction_or_symlink_raw_root(
    tmp_path,
    monkeypatch,
):
    raw_root = _governed_capture_root(tmp_path, monkeypatch)
    alias = tmp_path / "blind-captures-alias"
    if os.name == "nt":
        cmd = (
            Path(os.environ["SystemRoot"])
            / "System32"
            / "cmd.exe"
        )
        result = subprocess.run(
            [
                str(cmd),
                "/d",
                "/c",
                "mklink",
                "/J",
                str(alias),
                str(raw_root),
            ],
            capture_output=True,
            text=True,
            check=False,
        )
        if result.returncode != 0:
            pytest.skip(
                "junction creation unavailable: "
                f"{result.stdout}{result.stderr}"
            )
    else:
        try:
            alias.symlink_to(raw_root, target_is_directory=True)
        except OSError as error:
            pytest.skip(f"directory symlink creation unavailable: {error}")

    try:
        with pytest.raises(ValueError, match="alias|reparse|link"):
            _governed_scoring_input(alias)
    finally:
        if alias.exists():
            alias.rmdir()


def test_governed_adapter_rechecks_all_row_snapshots_at_final_boundary(
    tmp_path,
    monkeypatch,
):
    raw_root = _governed_capture_root(tmp_path, monkeypatch)
    _add_governed_capture(
        raw_root,
        tmp_path,
        blind_label="response-K2",
    )
    original_snapshot = adjudicator._snapshot_governed_capture
    mutated = False

    def replace_earlier_row_when_later_row_is_read(root):
        nonlocal mutated
        root = Path(root)
        if root.name == "response-K2" and not mutated:
            path = raw_root / "response-A7" / "P1" / "response.json"
            replacement = path.with_name("response.replacement")
            replacement.write_bytes(path.read_bytes())
            os.replace(replacement, path)
            mutated = True
        return original_snapshot(root)

    monkeypatch.setattr(
        adjudicator,
        "_snapshot_governed_capture",
        replace_earlier_row_when_later_row_is_read,
    )

    with pytest.raises(ValueError, match="changed|identity|snapshot"):
        _governed_scoring_input(raw_root)
    assert mutated


def test_governed_adapter_rechecks_exact_child_set_at_final_boundary(
    tmp_path,
    monkeypatch,
):
    raw_root = _governed_capture_root(tmp_path, monkeypatch)
    _add_governed_capture(
        raw_root,
        tmp_path,
        blind_label="response-K2",
    )
    original_snapshot = adjudicator._snapshot_governed_capture
    mutated = False

    def add_child_when_later_row_is_read(root):
        nonlocal mutated
        root = Path(root)
        if root.name == "response-K2" and not mutated:
            (raw_root / "response-Z9").mkdir()
            mutated = True
        return original_snapshot(root)

    monkeypatch.setattr(
        adjudicator,
        "_snapshot_governed_capture",
        add_child_when_later_row_is_read,
    )

    with pytest.raises(
        ValueError,
        match="blind configuration|child|changed|snapshot|state",
    ):
        _governed_scoring_input(raw_root)
    assert mutated


@pytest.mark.parametrize(
    "failed_turn_ids",
    [
        {"P2-turn-1"},
        {"P6-turn-1"},
        {"P6-turn-2"},
    ],
)
def test_governed_failed_null_evidence_is_preserved_and_non_scored(
    tmp_path,
    monkeypatch,
    failed_turn_ids,
):
    raw_root = _governed_capture_root(
        tmp_path,
        monkeypatch,
        failed_turn_ids=failed_turn_ids,
    )
    failed_prompt = "P6" if any(
        turn.startswith("P6-") for turn in failed_turn_ids
    ) else "P2"
    path = (
        raw_root
        / "response-A7"
        / failed_prompt
        / "response.json"
    )
    before = path.read_bytes()
    record = json.loads(before.decode("utf-8"))
    failed_turns = [
        turn
        for turn in record["turn_outputs"]
        if turn["status"] == "failed"
    ]
    assert failed_turns
    assert all(turn["output"] is None for turn in failed_turns)
    assert all(turn["output_sha256"] is None for turn in failed_turns)

    with pytest.raises(ValueError, match="failed.*non-scored"):
        _governed_scoring_input(raw_root)

    assert path.read_bytes() == before


if __name__ == "__main__":
    unittest.main()
