import json
import math
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from scripts.testing.final_results.csvio import validate_json, write_csv, write_json


SCHEMAS = (
    "route-manifest.schema.json",
    "attempts.schema.json",
    "measurements.schema.json",
    "results.schema.json",
    "quality.schema.json",
    "failures.schema.json",
    "evidence.schema.json",
)

SCHEMA_DIRECTORY = (
    Path(__file__).resolve().parents[3]
    / "docs"
    / "testing"
    / "final-results"
    / "standards"
    / "schemas"
)


def test_write_csv_is_utf8_deterministic_and_preserves_declared_columns(tmp_path):
    """Catches changes that depend on platform CSV defaults or row mapping order."""
    rows = (
        {"ignored": "not exported", "second": True, "first": "café", "third": None},
        {"second": False, "first": "next", "third": 3},
    )
    first = tmp_path / "first.csv"
    second = tmp_path / "second.csv"

    write_csv(first, rows, ("first", "second", "third"))
    write_csv(second, rows, ("first", "second", "third"))

    expected = "first,second,third\ncafé,true,\nnext,false,3\n".encode("utf-8")
    assert first.read_bytes() == expected
    assert second.read_bytes() == expected


def test_write_json_is_utf8_and_byte_stable(tmp_path):
    """Catches output that varies with mapping insertion order or platform newlines."""
    payload = {"zeta": 1, "alpha": [True, None, "café"]}
    first = tmp_path / "first.json"
    second = tmp_path / "second.json"

    write_json(first, payload)
    write_json(second, payload)

    assert first.read_bytes() == second.read_bytes()
    assert first.read_bytes() == (
        b'{\n  "alpha": [\n    true,\n    null,\n    "caf\xc3\xa9"\n  ],\n  "zeta": 1\n}\n'
    )


def test_validate_json_returns_json_pointer_paths_for_schema_errors(tmp_path):
    """Catches validators that lose the location of an invalid field."""
    schema = tmp_path / "schema.json"
    schema.write_text(
        json.dumps(
            {
                "$schema": "https://json-schema.org/draft/2020-12/schema",
                "type": "object",
                "properties": {"status": {"enum": ["passed"]}},
                "required": ["status"],
                "additionalProperties": False,
            }
        ),
        encoding="utf-8",
    )

    assert validate_json({"status": "failed"}, schema) == ["/status"]


def test_all_final_results_schemas_are_strict_and_accept_canonical_rows():
    """Catches schema drift from canonical row contracts and controlled statuses."""
    rows = {
        "route-manifest.schema.json": {
            "route_id": "route-1",
            "campaign_id": "campaign-1",
            "attempt_count": 1,
            "measurement_count": 1,
            "summary_count": 1,
            "quality_count": 1,
            "failure_count": 0,
            "evidence_count": 1,
            "repository": {"commit": "abc"},
            "hardware": {"cpu": "example"},
            "software": {"python": "3.11"},
        },
        "attempts.schema.json": {
            "route_id": "route-1",
            "campaign_id": "campaign-1",
            "test_case_id": "case-1",
            "attempt_id": "attempt-1",
            "status": "passed",
            "executed": True,
            "reason": "",
            "model_id": None,
            "weight_format_id": None,
            "cache_format_id": None,
            "backend_id": None,
            "source_status": None,
            "failure_kind": None,
            "evidence_ids": [],
        },
        "measurements.schema.json": {
            "route_id": "route-1",
            "campaign_id": "campaign-1",
            "test_case_id": "case-1",
            "attempt_id": "attempt-1",
            "measurement_id": "measurement-1",
            "run_id": None,
            "repetition_id": "repeat-1",
            "source_evidence_id": None,
            "latency_ms": None,
            "prompt_tokens_per_second": None,
            "generation_tokens_per_second": 12.5,
            "peak_working_set_bytes": 1024,
            "input_tokens": 8,
            "output_tokens": 4,
        },
        "results.schema.json": {
            "route_id": "route-1",
            "campaign_id": "campaign-1",
            "test_case_id": "case-1",
            "summary_id": "summary-1",
            "metric_name": "generation_tokens_per_second",
            "value": 12.5,
            "unit": "tokens/s",
            "aggregation": "median",
            "source_measurement_ids": ["measurement-1"],
        },
        "quality.schema.json": {
            "route_id": "route-1",
            "campaign_id": "campaign-1",
            "test_case_id": "case-1",
            "quality_id": "quality-1",
            "prompt_id": "prompt-1",
            "criterion_id": "correctness",
            "score": 4.0,
            "maximum_score": 5.0,
            "prompt_suite_id": "suite-v1",
            "rubric_id": "rubric-v1",
            "scoring_version": "v1",
            "source_evidence_id": "evidence-1",
        },
        "failures.schema.json": {
            "route_id": "route-1",
            "campaign_id": "campaign-1",
            "test_case_id": "case-1",
            "attempt_id": "attempt-1",
            "failure_id": "failure-1",
            "status": "failed",
            "stage": "conversion",
            "reason": "conversion failed",
            "source_status": None,
            "evidence_ids": [],
        },
        "evidence.schema.json": {
            "route_id": "route-1",
            "campaign_id": "campaign-1",
            "evidence_id": "evidence-1",
            "role": "raw-result",
            "relative_path": "experiments/raw-results/result.json",
            "sha256": "a" * 64,
            "size_bytes": 42,
            "source_label": None,
            "derived": False,
            "input_evidence_ids": [],
        },
    }

    assert {path.name for path in SCHEMA_DIRECTORY.glob("*.schema.json")} == set(SCHEMAS)
    for schema_name, row in rows.items():
        assert validate_json(row, SCHEMA_DIRECTORY / schema_name) == []


@pytest.mark.parametrize("reason", ("", " \t"))
def test_attempt_schema_requires_non_whitespace_reason_for_each_non_passed_status(
    reason,
):
    """Catches non-passed attempts that retain only whitespace as a reason."""
    row = {
        "route_id": "route-1",
        "campaign_id": "campaign-1",
        "test_case_id": "case-1",
        "attempt_id": "attempt-1",
        "status": "failed",
        "executed": False,
        "reason": reason,
        "model_id": None,
        "weight_format_id": None,
        "cache_format_id": None,
        "backend_id": None,
        "source_status": None,
        "failure_kind": None,
        "evidence_ids": [],
    }

    errors = validate_json(row, SCHEMA_DIRECTORY / "attempts.schema.json")

    assert errors


def _evidence_row(**overrides):
    row = {
        "route_id": "route-1",
        "campaign_id": "campaign-1",
        "evidence_id": "evidence-1",
        "role": "raw-result",
        "relative_path": "experiments/raw-results/result.json",
        "sha256": "a" * 64,
        "size_bytes": 42,
        "source_label": "raw result export",
        "derived": False,
        "input_evidence_ids": [],
    }
    row.update(overrides)
    return row


@pytest.mark.parametrize(
    "relative_path",
    (
        "foo\n../escape.txt",
        "foo\nC:\\secret.txt",
        "../escape.txt",
        "folder\\result.txt",
        "/var/tmp/result.json",
        "C:/secret.txt",
        "\\\\server\\share\\result.json",
        "folder/\x01result.txt",
    ),
)
def test_evidence_schema_rejects_nonportable_relative_paths(relative_path):
    """Catches path validation that accepts traversal, machine paths, or controls."""
    errors = validate_json(
        _evidence_row(relative_path=relative_path),
        SCHEMA_DIRECTORY / "evidence.schema.json",
    )

    assert errors


def test_evidence_schema_requires_inputs_for_derived_records():
    """Catches derived evidence records that do not identify their inputs."""
    schema = SCHEMA_DIRECTORY / "evidence.schema.json"

    assert validate_json(
        _evidence_row(derived=True, input_evidence_ids=[]), schema
    )
    assert validate_json(_evidence_row(derived=False, input_evidence_ids=[]), schema) == []


@pytest.mark.parametrize(
    "source_label",
    (
        "",
        " \t",
        "/machine/path",
        "C:\\secret.txt",
        "\\\\server\\share",
        "source\\label",
        "source\nlabel",
        "source\x01label",
    ),
)
def test_evidence_schema_rejects_nonportable_source_labels(source_label):
    """Catches source labels that leak machine paths or control characters."""
    errors = validate_json(
        _evidence_row(source_label=source_label),
        SCHEMA_DIRECTORY / "evidence.schema.json",
    )

    assert errors


@pytest.mark.parametrize("value", (math.nan, math.inf, -math.inf))
def test_write_json_rejects_nonfinite_numbers(tmp_path, value):
    """Catches JSON output that emits non-standard NaN or Infinity literals."""
    with pytest.raises(ValueError):
        write_json(tmp_path / "nonfinite.json", {"value": value})
