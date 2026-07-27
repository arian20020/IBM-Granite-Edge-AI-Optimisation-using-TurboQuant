import hashlib
import json
import re
import unittest

from scripts.testing.official_openvino.quality import (
    evaluate_deterministic_gate,
    score_adjudication,
    score_response,
    summarize_quality,
    terminal_quality_record,
    validate_response_record,
)


SHA_A = "a" * 64
SHA_B = "b" * 64
SHA_C = "c" * 64


def canonical_sha256(value: dict) -> str:
    return hashlib.sha256(
        json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")
    ).hexdigest()


def gate_response(prompt_id: str, output: str, **extra) -> dict:
    response = {
        "prompt_id": prompt_id,
        "status": "complete",
        "output": output,
        "output_sha256": hashlib.sha256(output.encode("utf-8")).hexdigest(),
        "prompt_set_sha256": SHA_A,
        "prompt_sha256": SHA_B,
    }
    response.update(extra)
    if isinstance(response.get("turn_1"), str):
        response["turn_1_sha256"] = hashlib.sha256(
            response["turn_1"].encode("utf-8")
        ).hexdigest()
    return response


def evaluate_gate(
    response: dict,
    definition: dict,
    *,
    content_evidence: dict | None = None,
) -> dict:
    return evaluate_deterministic_gate(
        response,
        definition,
        content_evidence=content_evidence,
        expected_prompt_set_sha256=SHA_A,
        expected_prompt_sha256=SHA_B,
        expected_prompt_definition_sha256=canonical_sha256(definition),
    )


def review_metadata(output: str, definition: dict) -> dict:
    return {
        "schema_version": 1,
        "output_sha256": hashlib.sha256(output.encode("utf-8")).hexdigest(),
        "prompt_definition_sha256": canonical_sha256(definition),
        "reviewer_id": "manual-reviewer:test",
        "review_source": "content-review-fixture",
    }


def review_span(
    output: str,
    text: str,
    *,
    verdict: str,
    rationale: str,
    occurrence: int = 0,
) -> dict:
    matches = [match.start() for match in re.finditer(re.escape(text), output)]
    start = matches[occurrence]
    return {
        "start": start,
        "end": start + len(text),
        "text": text,
        "verdict": verdict,
        "rationale": rationale,
    }


def p1_evidence(output: str, definition: dict, *, repeated: bool = False) -> dict:
    evidence = review_metadata(output, definition)
    matches = list(re.finditer(r"(?m)^[ \t]*(?:[-*]|\u2022)[ \t]+\S.*$", output))
    roles = {
        "benefits": [(1, "benefit"), (2, "benefit")],
        "limitations": [(3, "limitation"), (4, "limitation")],
        "check": [(5, "check")],
    }
    evidence["semantic_slots"] = {}
    for role, assignments in roles.items():
        entries = []
        for bullet_index, verdict in assignments:
            match = matches[bullet_index - 1]
            entries.append({
                "bullet_index": bullet_index,
                "start": match.start(),
                "end": match.end(),
                "text": match.group(0),
                "verdict": verdict,
                "rationale": (
                    "Intentional repeated adversarial text."
                    if repeated
                    else f"Manual review classified bullet {bullet_index} as {verdict}."
                ),
            })
        evidence["semantic_slots"][role] = entries
    return evidence


def p4_evidence(output: str, definition: dict, *, reuse_trivial_span: bool = False) -> dict:
    evidence = review_metadata(output, definition)
    required_facts = definition["deterministic_checks"]["required_facts"]
    if reuse_trivial_span:
        evidence["required_fact_spans"] = {
            fact: {
                **review_span(
                    output,
                    "x",
                    verdict="retained",
                    rationale=f"Adversarial reused span asserted for {fact}.",
                )
            }
            for fact in required_facts
        }
        evidence["sentence_reviews"] = [
            {
                **review_span(
                    output,
                    "x.",
                    occurrence=index,
                    verdict="supported",
                    rationale="Adversarial sentence review.",
                ),
                "source_facts": required_facts if index == 0 else [required_facts[-1]],
            }
            for index in range(2)
        ]
        return evidence

    evidence["required_fact_spans"] = {
        fact: review_span(
            output,
            fact,
            verdict="retained",
            rationale=f"Exact response span retains {fact}.",
        )
        for fact in required_facts
    }
    first_end = output.index(". ") + 1
    sentences = [(0, first_end), (first_end + 1, len(output))]
    evidence["sentence_reviews"] = []
    for index, (start, end) in enumerate(sentences):
        source_facts = [
            fact
            for fact, span in evidence["required_fact_spans"].items()
            if start <= span["start"] and span["end"] <= end
        ]
        evidence["sentence_reviews"].append({
            "start": start,
            "end": end,
            "text": output[start:end],
            "verdict": "supported",
            "rationale": "Every statement is grounded in the supplied route facts.",
            "source_facts": source_facts,
        })
    return evidence


def scoring_fixture(
    prompt_id: str,
    *,
    passed: bool = True,
) -> tuple[dict, dict, dict | None, dict]:
    definitions = {
        "P1": {
            "prompt_id": "P1",
            "deterministic_checks": {
                "exact_bullet_count": 5,
                "maximum_words": 89,
                "required_substrings": ["KV cache", "offline", "memory"],
                "required_semantic_slots": [
                    "two benefits", "two limitations", "one check"
                ],
            },
        },
        "P2": {
            "prompt_id": "P2",
            "deterministic_checks": {
                "exact_nonempty_lines": 3,
                "line_prefixes": ["BENEFIT:", "LIMITATION:", "CHECK:"],
                "prohibit_extra_text": True,
            },
        },
        "P3": {
            "prompt_id": "P3",
            "deterministic_checks": {
                "valid_json": True,
                "exact_top_level_keys": [
                    "optimisation", "memory_effect", "quality_risk", "verification"
                ],
                "exact_values": {"optimisation": "TurboQuant"},
                "all_other_values_type": "string",
                "prohibit_markdown_fence": True,
            },
        },
        "P4": {
            "prompt_id": "P4",
            "deterministic_checks": {
                "exact_sentences": 2,
                "required_facts": [
                    "IBM Granite 4.1 3B",
                    "patched OpenVINO GenAI",
                    "INT8 weights",
                    "TBQ4 K cache",
                    "TBQ3 V cache",
                    "4096",
                    "local Windows Intel",
                    "TurboQuant active",
                ],
                "unsupported_fact_count_expected": 0,
            },
        },
        "P5": {
            "prompt_id": "P5",
            "deterministic_checks": {
                "exact_output": "MARKER:IXN-TQ-7319",
                "exact_nonempty_lines": 1,
            },
        },
        "P6": {
            "prompt_id": "P6",
            "deterministic_checks": {
                "turn_1_exact_output": "SAVED",
                "turn_2_exact_output": "amber:4821",
            },
        },
    }
    passing_outputs = {
        "P1": (
            "- KV cache compression reduces memory use.\n"
            "- More context can fit while offline.\n"
            "- Compression can reduce answer accuracy.\n"
            "- Encoding can add compute overhead.\n"
            "- Check output quality before deployment."
        ),
        "P2": (
            "BENEFIT: lower memory\n"
            "LIMITATION: possible quality loss\n"
            "CHECK: compare outputs"
        ),
        "P3": (
            '{"optimisation":"TurboQuant","memory_effect":"lower",'
            '"quality_risk":"possible","verification":"compare"}'
        ),
        "P4": (
            "IBM Granite 4.1 3B ran through patched OpenVINO GenAI with INT8 "
            "weights, a TBQ4 K cache, and a TBQ3 V cache. "
            "The 4096-token test ran on a local Windows Intel laptop with "
            "TurboQuant active."
        ),
        "P5": "MARKER:IXN-TQ-7319",
        "P6": "amber:4821",
    }
    failing_outputs = {
        "P1": passing_outputs["P1"],
        "P2": "not compliant",
        "P3": "not JSON",
        "P4": passing_outputs["P4"],
        "P5": "wrong",
        "P6": "wrong:9999",
    }
    definition = definitions[prompt_id]
    output = passing_outputs[prompt_id] if passed else failing_outputs[prompt_id]
    response = gate_response(
        prompt_id,
        output,
        **({"turn_1": "SAVED"} if prompt_id == "P6" else {}),
    )
    content_evidence = None
    if prompt_id == "P1":
        content_evidence = p1_evidence(output, definition)
    elif prompt_id == "P4":
        content_evidence = p4_evidence(output, definition)
    gate = evaluate_gate(
        response,
        definition,
        content_evidence=content_evidence,
    )
    if gate["passed"] != passed:
        raise AssertionError(f"test scoring fixture did not reach {passed=}")
    return response, definition, content_evidence, gate


def score_with_gate(
    prompt_id: str,
    evidence: dict,
    *,
    format_label: str,
) -> dict:
    response, definition, content_evidence, gate = scoring_fixture(
        prompt_id,
        passed=evidence["deterministic_pass"],
    )
    return score_adjudication(
        prompt_id,
        response["output_sha256"],
        evidence,
        format_label=format_label,
        deterministic_gate=gate,
        response=response,
        prompt_definition=definition,
        content_evidence=content_evidence,
        expected_prompt_set_sha256=SHA_A,
        expected_prompt_sha256=SHA_B,
        expected_prompt_definition_sha256=canonical_sha256(definition),
    )


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
        result = score_with_gate(
            "P2",
            evidence,
            format_label="TBQ3",
        )
        self.assertEqual(result["uncapped_score"], 7.0)
        self.assertEqual(result["score"], 4.0)
        self.assertFalse(result["deterministic_pass"])

    def test_identical_content_is_scored_identically_under_different_labels(self):
        first = score_with_gate(
            "P1", adjudication(), format_label="F16"
        )
        second = score_with_gate(
            "P1", adjudication(), format_label="TBQ3"
        )
        self.assertEqual(first, second)

    def test_summary_requires_p1_to_p6_and_recomputes_all_aggregates(self):
        records = []
        for number, score in enumerate((1, 2, 3, 4, 5, 10), start=1):
            item = score_with_gate(
                f"P{number}",
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
        response, definition, content_evidence, gate = scoring_fixture("P1")
        with self.assertRaisesRegex(ValueError, "output SHA256"):
            score_adjudication(
                "P1",
                "not-a-sha",
                adjudication(),
                format_label="F16",
                deterministic_gate=gate,
                response=response,
                prompt_definition=definition,
                content_evidence=content_evidence,
                expected_prompt_set_sha256=SHA_A,
                expected_prompt_sha256=SHA_B,
                expected_prompt_definition_sha256=canonical_sha256(definition),
            )
        incomplete = adjudication()
        incomplete.pop("notes")
        with self.assertRaisesRegex(ValueError, "adjudication fields"):
            score_with_gate("P1", incomplete, format_label="F16")

    def test_p1_semantic_slots_are_content_reviewed_not_label_heuristics(self):
        output = (
            "- It reduces KV cache memory use.\n"
            "- More context can fit while offline.\n"
            "- Compression may reduce answer accuracy.\n"
            "- Encoding can add compute overhead.\n"
            "- Compare output quality before deployment."
        )
        definition = {
            "prompt_id": "P1",
            "deterministic_checks": {
                "exact_bullet_count": 5,
                "maximum_words": 89,
                "required_substrings": ["KV cache", "offline", "memory"],
                "required_semantic_slots": [
                    "two benefits", "two limitations", "one check"
                ],
            },
        }
        with self.assertRaisesRegex(ValueError, "content evidence"):
            evaluate_gate(gate_response("P1", output), definition)
        gate = evaluate_gate(
            gate_response("P1", output),
            definition,
            content_evidence=p1_evidence(output, definition),
        )
        self.assertTrue(gate["passed"])
        self.assertEqual(gate["failures"], [])

    def test_p2_p3_p5_and_p6_objective_gates_are_exact(self):
        p2 = {
            "prompt_id": "P2",
            "deterministic_checks": {
                "exact_nonempty_lines": 3,
                "line_prefixes": ["BENEFIT:", "LIMITATION:", "CHECK:"],
                "prohibit_extra_text": True,
            },
        }
        good_p2 = "BENEFIT: lower memory\nLIMITATION: quality risk\nCHECK: compare outputs"
        self.assertTrue(evaluate_gate(
            gate_response("P2", good_p2), p2
        )["passed"])
        self.assertFalse(evaluate_gate(
            gate_response("P2", "Title\n" + good_p2), p2
        )["passed"])

        p3 = {
            "prompt_id": "P3",
            "deterministic_checks": {
                "valid_json": True,
                "exact_top_level_keys": [
                    "optimisation", "memory_effect", "quality_risk", "verification"
                ],
                "exact_values": {"optimisation": "TurboQuant"},
                "all_other_values_type": "string",
                "prohibit_markdown_fence": True,
            },
        }
        good_p3 = (
            '{"optimisation":"TurboQuant","memory_effect":"lower",'
            '"quality_risk":"possible","verification":"compare"}'
        )
        self.assertTrue(evaluate_gate(
            gate_response("P3", good_p3), p3
        )["passed"])
        self.assertFalse(evaluate_gate(
            gate_response("P3", f"```json\n{good_p3}\n```"),
            p3,
        )["passed"])

        p5 = {"prompt_id": "P5", "deterministic_checks": {
            "exact_output": "MARKER:IXN-TQ-7319", "exact_nonempty_lines": 1
        }}
        self.assertFalse(evaluate_gate(
            gate_response("P5", "MARKER: IXN-TQ-7319"),
            p5,
        )["passed"])

        p6 = {"prompt_id": "P6", "deterministic_checks": {
            "turn_1_exact_output": "SAVED", "turn_2_exact_output": "amber:4821"
        }}
        self.assertTrue(evaluate_gate(
            gate_response("P6", "amber:4821", turn_1="SAVED"),
            p6,
        )["passed"])

    def test_p4_fact_retention_uses_auditable_spans_and_rejects_new_facts(self):
        definition = {
            "prompt_id": "P4",
            "deterministic_checks": {
                "exact_sentences": 2,
                "required_facts": [
                    "IBM Granite 4.1 3B",
                    "patched OpenVINO GenAI",
                    "INT8 weights",
                    "TBQ4 K cache",
                    "TBQ3 V cache",
                    "4096",
                    "local Windows Intel",
                    "TurboQuant active",
                ],
                "unsupported_fact_count_expected": 0,
            },
        }
        output = (
            "IBM Granite 4.1 3B ran through patched OpenVINO GenAI with INT8 "
            "weights, a TBQ4 K cache, and a TBQ3 V cache. "
            "The 4096-token test ran on a local Windows Intel laptop with "
            "TurboQuant active."
        )
        gate = evaluate_gate(
            gate_response("P4", output),
            definition,
            content_evidence=p4_evidence(output, definition),
        )
        self.assertTrue(gate["passed"])
        bad_evidence = p4_evidence(output, definition)
        bad_evidence["sentence_reviews"][1]["verdict"] = "unsupported"
        bad_evidence["sentence_reviews"][1]["rationale"] = (
            "Manual review found this sentence unsupported."
        )
        bad = evaluate_gate(
            gate_response("P4", output),
            definition,
            content_evidence=bad_evidence,
        )
        self.assertFalse(bad["passed"])
        self.assertIn("unsupported statements", " ".join(bad["failures"]))

    def test_p1_rejects_arbitrary_roles_assigned_to_repeated_nonsense(self):
        output = "\n".join(["- KV cache offline memory."] * 5)
        definition = {
            "prompt_id": "P1",
            "deterministic_checks": {
                "exact_bullet_count": 5,
                "maximum_words": 89,
                "required_substrings": ["KV cache", "offline", "memory"],
                "required_semantic_slots": [
                    "two benefits", "two limitations", "one check"
                ],
            },
        }
        gate = evaluate_gate(
            gate_response("P1", output),
            definition,
            content_evidence=p1_evidence(output, definition, repeated=True),
        )
        self.assertFalse(gate["passed"])

    def test_p4_rejects_one_trivial_span_reused_for_every_required_fact(self):
        required_facts = [
            "IBM Granite 4.1 3B",
            "patched OpenVINO GenAI",
            "INT8 weights",
            "TBQ4 K cache",
            "TBQ3 V cache",
            "4096",
            "local Windows Intel",
            "TurboQuant active",
        ]
        definition = {
            "prompt_id": "P4",
            "deterministic_checks": {
                "exact_sentences": 2,
                "required_facts": required_facts,
                "unsupported_fact_count_expected": 0,
            },
        }
        output = "x. x."
        gate = evaluate_gate(
            gate_response("P4", output),
            definition,
            content_evidence=p4_evidence(
                output, definition, reuse_trivial_span=True
            ),
        )
        self.assertFalse(gate["passed"])

    def test_p3_rejects_duplicate_json_keys(self):
        definition = {
            "prompt_id": "P3",
            "deterministic_checks": {
                "valid_json": True,
                "exact_top_level_keys": [
                    "optimisation", "memory_effect", "quality_risk", "verification"
                ],
                "exact_values": {"optimisation": "TurboQuant"},
                "all_other_values_type": "string",
                "prohibit_markdown_fence": True,
            },
        }
        output = (
            '{"optimisation":"wrong","optimisation":"TurboQuant",'
            '"memory_effect":"lower","quality_risk":"possible",'
            '"verification":"compare"}'
        )
        self.assertFalse(evaluate_gate(
            gate_response("P3", output),
            definition,
        )["passed"])

    def test_gate_is_bound_to_prompt_hashes_and_controls_p6_cap(self):
        definition = {
            "prompt_id": "P6",
            "deterministic_checks": {
                "turn_1_exact_output": "SAVED",
                "turn_2_exact_output": "amber:4821",
            },
        }
        output = "wrong:9999"
        output_sha256 = hashlib.sha256(output.encode("utf-8")).hexdigest()
        response = gate_response("P6", output, turn_1="SAVED")
        definition_sha256 = canonical_sha256(definition)
        gate = evaluate_deterministic_gate(
            response,
            definition,
            expected_prompt_set_sha256=SHA_A,
            expected_prompt_sha256=SHA_B,
            expected_prompt_definition_sha256=definition_sha256,
        )
        self.assertFalse(gate["passed"])
        evidence = adjudication(10.0, passed=False, cap=10)
        result = score_adjudication(
            "P6",
            output_sha256,
            evidence,
            format_label="hidden",
            deterministic_gate=gate,
            response=response,
            prompt_definition=definition,
            content_evidence=None,
            expected_prompt_set_sha256=SHA_A,
            expected_prompt_sha256=SHA_B,
            expected_prompt_definition_sha256=definition_sha256,
        )
        self.assertEqual(result["score"], 2.0)

    def test_gate_rejects_weakened_definition_against_trusted_hash(self):
        trusted = {
            "prompt_id": "P2",
            "deterministic_checks": {
                "exact_nonempty_lines": 3,
                "line_prefixes": ["BENEFIT:", "LIMITATION:", "CHECK:"],
                "prohibit_extra_text": True,
            },
        }
        weakened = {
            "prompt_id": "P2",
            "deterministic_checks": {
                "exact_nonempty_lines": 1,
                "line_prefixes": [],
                "prohibit_extra_text": False,
            },
        }
        with self.assertRaisesRegex(ValueError, "prompt definition SHA256"):
            evaluate_deterministic_gate(
                {
                    "prompt_id": "P2",
                    "status": "complete",
                    "output": "anything",
                    "output_sha256": hashlib.sha256(b"anything").hexdigest(),
                    "prompt_set_sha256": SHA_A,
                    "prompt_sha256": SHA_B,
                },
                weakened,
                expected_prompt_set_sha256=SHA_A,
                expected_prompt_sha256=SHA_B,
                expected_prompt_definition_sha256=canonical_sha256(trusted),
            )

    def test_scorer_rejects_a_forged_too_high_gate_cap(self):
        response, definition, content_evidence, gate = scoring_fixture(
            "P6", passed=False
        )
        gate["required_cap"] = 10.0
        gate["gate_sha256"] = canonical_sha256({
            key: value for key, value in gate.items() if key != "gate_sha256"
        })
        with self.assertRaisesRegex(ValueError, "failure-specific cap"):
            score_adjudication(
                "P6",
                response["output_sha256"],
                adjudication(10.0, passed=False, cap=10.0),
                format_label="hidden",
                deterministic_gate=gate,
                response=response,
                prompt_definition=definition,
                content_evidence=content_evidence,
                expected_prompt_set_sha256=SHA_A,
                expected_prompt_sha256=SHA_B,
                expected_prompt_definition_sha256=canonical_sha256(definition),
            )

    def test_scorer_requires_hashed_content_review_for_p1_and_p4(self):
        for prompt_id in ("P1", "P4"):
            response, definition, _, gate = scoring_fixture(prompt_id)
            with self.assertRaisesRegex(ValueError, "content evidence"):
                score_adjudication(
                    prompt_id,
                    response["output_sha256"],
                    adjudication(),
                    format_label="hidden",
                    deterministic_gate=gate,
                    response=response,
                    prompt_definition=definition,
                    content_evidence=None,
                    expected_prompt_set_sha256=SHA_A,
                    expected_prompt_sha256=SHA_B,
                    expected_prompt_definition_sha256=canonical_sha256(definition),
                )

    def test_p1_duplicate_semantics_ignore_marker_indent_and_punctuation(self):
        output = "\n".join([
            "- KV cache offline memory",
            "* KV cache offline memory.",
            "  -   KV cache offline memory!",
            "\t* KV cache offline memory?",
            "\u2022 KV cache offline memory;",
        ])
        definition = {
            "prompt_id": "P1",
            "deterministic_checks": {
                "exact_bullet_count": 5,
                "maximum_words": 89,
                "required_substrings": ["KV cache", "offline", "memory"],
                "required_semantic_slots": [
                    "two benefits", "two limitations", "one check"
                ],
            },
        }
        gate = evaluate_gate(
            gate_response("P1", output),
            definition,
            content_evidence=p1_evidence(output, definition, repeated=True),
        )
        self.assertFalse(gate["passed"])

    def test_p4_fact_spans_must_contain_the_named_required_fact(self):
        required_facts = [
            "IBM Granite 4.1 3B",
            "patched OpenVINO GenAI",
            "INT8 weights",
            "TBQ4 K cache",
            "TBQ3 V cache",
            "4096",
            "local Windows Intel",
            "TurboQuant active",
        ]
        definition = {
            "prompt_id": "P4",
            "deterministic_checks": {
                "exact_sentences": 2,
                "required_facts": required_facts,
                "unsupported_fact_count_expected": 0,
            },
        }
        output = "a b c d e f g h. i."
        evidence = review_metadata(output, definition)
        one_letter_spans = {}
        for index, fact in enumerate(required_facts):
            start = index * 2
            one_letter_spans[fact] = {
                "start": start,
                "end": start + 1,
                "text": output[start:start + 1],
                "verdict": "retained",
                "rationale": f"Adversarial assertion for {fact}.",
            }
        evidence["required_fact_spans"] = one_letter_spans
        first_end = output.index(".") + 1
        evidence["sentence_reviews"] = [
            {
                "start": 0,
                "end": first_end,
                "text": output[:first_end],
                "verdict": "supported",
                "rationale": "Adversarial sentence assertion.",
                "source_facts": required_facts,
            },
            {
                "start": first_end + 1,
                "end": len(output),
                "text": output[first_end + 1:],
                "verdict": "supported",
                "rationale": "Adversarial sentence assertion.",
                "source_facts": [required_facts[-1]],
            },
        ]
        gate = evaluate_gate(
            gate_response("P4", output),
            definition,
            content_evidence=evidence,
        )
        self.assertFalse(gate["passed"])

    def test_scorer_recomputes_instead_of_trusting_a_self_rehashed_gate(self):
        definition = {
            "prompt_id": "P2",
            "deterministic_checks": {
                "exact_nonempty_lines": 3,
                "line_prefixes": ["BENEFIT:", "LIMITATION:", "CHECK:"],
                "prohibit_extra_text": True,
            },
        }
        response = gate_response("P2", "not compliant")
        actual_gate = evaluate_gate(response, definition)
        self.assertFalse(actual_gate["passed"])
        forged_gate = dict(actual_gate)
        forged_gate.update({
            "passed": True,
            "failures": [],
            "failure_codes": [],
            "required_cap": None,
        })
        forged_gate["gate_sha256"] = canonical_sha256({
            key: value
            for key, value in forged_gate.items()
            if key != "gate_sha256"
        })
        with self.assertRaisesRegex(ValueError, "recomputed deterministic gate"):
            score_adjudication(
                "P2",
                response["output_sha256"],
                adjudication(10.0),
                format_label="hidden",
                deterministic_gate=forged_gate,
                response=response,
                prompt_definition=definition,
                content_evidence=None,
                expected_prompt_set_sha256=SHA_A,
                expected_prompt_sha256=SHA_B,
                expected_prompt_definition_sha256=canonical_sha256(definition),
            )

    def test_scorer_rejects_a_failure_code_borrowed_from_another_prompt(self):
        response, definition, content_evidence, gate = scoring_fixture(
            "P6", passed=False
        )
        gate["failure_codes"] = ["p1_exact_bullet_count"]
        gate["required_cap"] = 4.0
        gate["gate_sha256"] = canonical_sha256({
            key: value for key, value in gate.items() if key != "gate_sha256"
        })
        with self.assertRaisesRegex(ValueError, "does not belong to P6"):
            score_adjudication(
                "P6",
                response["output_sha256"],
                adjudication(10.0, passed=False, cap=4.0),
                format_label="hidden",
                deterministic_gate=gate,
                response=response,
                prompt_definition=definition,
                content_evidence=content_evidence,
                expected_prompt_set_sha256=SHA_A,
                expected_prompt_sha256=SHA_B,
                expected_prompt_definition_sha256=canonical_sha256(definition),
            )

    def test_p6_gate_hash_binds_the_exact_turn_one_output(self):
        definition = {
            "prompt_id": "P6",
            "deterministic_checks": {
                "turn_1_exact_output": "SAVED",
                "turn_2_exact_output": "amber:4821",
            },
        }
        first = evaluate_gate(
            gate_response("P6", "amber:4821", turn_1="NO"),
            definition,
        )
        second = evaluate_gate(
            gate_response("P6", "amber:4821", turn_1="MAYBE"),
            definition,
        )
        self.assertNotEqual(first["turn_1_sha256"], second["turn_1_sha256"])
        self.assertNotEqual(first["gate_sha256"], second["gate_sha256"])


if __name__ == "__main__":
    unittest.main()
