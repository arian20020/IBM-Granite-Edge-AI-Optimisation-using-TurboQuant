import sys
from pathlib import Path

import pytest


sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from scripts.testing.final_results.models import (
    AttemptRecord,
    EvidenceRecord,
    FailureRecord,
    MeasurementRecord,
    QualityRecord,
    RouteBundle,
    Status,
    SummaryRecord,
)


def test_source_statuses_map_without_losing_failure_kind():
    assert Status.from_source("passed") is Status.PASSED
    assert Status.from_source("model_artifact_unavailable") is Status.ARTIFACT_UNAVAILABLE
    assert Status.from_source("conversion_failed") is Status.FAILED
    assert Status.from_source("hardware_preflight_blocked") is Status.BLOCKED


def test_non_passed_attempt_requires_reason():
    with pytest.raises(ValueError, match="reason"):
        AttemptRecord(
            route_id="openvino-official-upstream",
            campaign_id="2026-08-30-fv2",
            test_case_id="granite-3b__fp16__tbq3",
            attempt_id="granite-3b__fp16__tbq3-attempt-1",
            status=Status.FAILED,
            executed=False,
            reason="",
        )


def test_passed_attempt_must_be_executed():
    with pytest.raises(ValueError, match="executed"):
        AttemptRecord(
            route_id="openvino-official-upstream",
            campaign_id="2026-08-30-fv2",
            test_case_id="granite-3b__fp16__tbq3",
            attempt_id="granite-3b__fp16__tbq3-attempt-1",
            status=Status.PASSED,
            executed=False,
        )


def test_status_exposes_normalized_value_and_display_label():
    assert Status.ARTIFACT_UNAVAILABLE.value == "artifact_unavailable"
    assert Status.ARTIFACT_UNAVAILABLE.display_label == "Artifact unavailable"


def test_canonical_records_serialize_stable_ids_and_nullable_metrics():
    attempt = AttemptRecord(
        route_id="openvino-official-upstream",
        campaign_id="2026-08-30-fv2",
        test_case_id="granite-3b__fp16__tbq3",
        attempt_id="granite-3b__fp16__tbq3-attempt-1",
        status=Status.PASSED,
        executed=True,
        model_id="granite-3b",
    )
    measurement = MeasurementRecord(
        route_id=attempt.route_id,
        campaign_id=attempt.campaign_id,
        test_case_id=attempt.test_case_id,
        attempt_id=attempt.attempt_id,
        measurement_id="measurement-1",
        repetition_id="repeat-1",
        generation_tokens_per_second=None,
    )
    summary = SummaryRecord(
        route_id=attempt.route_id,
        campaign_id=attempt.campaign_id,
        test_case_id=attempt.test_case_id,
        summary_id="summary-1",
        metric_name="generation_tokens_per_second",
        value=12.5,
        unit="tokens/s",
        aggregation="median",
        source_measurement_ids=(measurement.measurement_id,),
    )
    quality = QualityRecord(
        route_id=attempt.route_id,
        campaign_id=attempt.campaign_id,
        test_case_id=attempt.test_case_id,
        quality_id="quality-1",
        prompt_id="prompt-1",
        criterion_id="correctness",
        score=4.0,
        maximum_score=5.0,
    )
    failure = FailureRecord(
        route_id=attempt.route_id,
        campaign_id=attempt.campaign_id,
        test_case_id=attempt.test_case_id,
        attempt_id="granite-3b__fp16__tbq3-attempt-2",
        failure_id="failure-1",
        status=Status.FAILED,
        stage="conversion",
        reason="conversion failed",
    )
    evidence = EvidenceRecord(
        route_id=attempt.route_id,
        campaign_id=attempt.campaign_id,
        evidence_id="evidence-1",
        role="raw-result",
        relative_path="experiments/raw-results/result.json",
        sha256="a" * 64,
        size_bytes=42,
    )
    bundle = RouteBundle(
        route_id=attempt.route_id,
        campaign_id=attempt.campaign_id,
        attempts=(attempt,),
        measurements=(measurement,),
        summaries=(summary,),
        quality=(quality,),
        failures=(failure,),
        evidence=(evidence,),
        repository={"commit": "abc123"},
        hardware={"cpu": "example"},
        software={"python": "3.11"},
    )

    assert attempt.to_row()["status"] == "passed"
    assert measurement.to_row()["generation_tokens_per_second"] is None
    assert summary.to_row()["source_measurement_ids"] == ["measurement-1"]
    assert quality.to_row()["maximum_score"] == 5.0
    assert failure.to_row()["status"] == "failed"
    assert evidence.to_row()["relative_path"] == "experiments/raw-results/result.json"
    assert bundle.to_row()["attempt_count"] == 1
    assert isinstance(bundle.repository, dict)


@pytest.mark.parametrize(
    "relative_path",
    (
        "C:/Users/Student/result.json",
        r"C:\Users\Student\result.json",
        r"\\server\share\result.json",
        "/var/tmp/result.json",
        "../result.json",
        "evidence/../result.json",
    ),
)
def test_evidence_record_rejects_nonportable_paths(relative_path):
    with pytest.raises(ValueError, match="relative_path"):
        EvidenceRecord(
            route_id="openvino-official-upstream",
            campaign_id="2026-08-30-fv2",
            evidence_id="evidence-1",
            role="raw-result",
            relative_path=relative_path,
            sha256="a" * 64,
            size_bytes=42,
        )


@pytest.mark.parametrize("sha256", ("a" * 63, "g" * 64, "not-a-digest"))
def test_evidence_record_rejects_non_sha256_digest(sha256):
    with pytest.raises(ValueError, match="sha256"):
        EvidenceRecord(
            route_id="openvino-official-upstream",
            campaign_id="2026-08-30-fv2",
            evidence_id="evidence-1",
            role="raw-result",
            relative_path="evidence/result.json",
            sha256=sha256,
            size_bytes=42,
        )


def test_route_bundle_coerces_record_lists_to_isolated_tuples():
    attempt = AttemptRecord(
        route_id="openvino-official-upstream",
        campaign_id="2026-08-30-fv2",
        test_case_id="granite-3b__fp16__tbq3",
        attempt_id="granite-3b__fp16__tbq3-attempt-1",
        status=Status.PASSED,
        executed=True,
    )
    attempts = [attempt]
    measurements = []
    summaries = []
    quality = []
    failures = []
    evidence = []

    bundle = RouteBundle(
        route_id=attempt.route_id,
        campaign_id=attempt.campaign_id,
        attempts=attempts,
        measurements=measurements,
        summaries=summaries,
        quality=quality,
        failures=failures,
        evidence=evidence,
    )
    attempts.append(attempt)

    assert bundle.attempts == (attempt,)
    assert all(
        isinstance(collection, tuple)
        for collection in (
            bundle.attempts,
            bundle.measurements,
            bundle.summaries,
            bundle.quality,
            bundle.failures,
            bundle.evidence,
        )
    )
