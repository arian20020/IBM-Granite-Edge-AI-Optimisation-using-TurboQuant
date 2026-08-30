"""Immutable, normalized records used by the final-results pipeline."""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum


class Status(str, Enum):
    """Controlled statuses used in canonical result records."""

    PASSED = "passed"
    FAILED = "failed"
    BLOCKED = "blocked"
    ARTIFACT_UNAVAILABLE = "artifact_unavailable"
    NOT_EXECUTED = "not_executed"
    NOT_APPLICABLE = "not_applicable"
    NOT_COLLECTED = "not_collected"

    @property
    def display_label(self) -> str:
        return _STATUS_DISPLAY_LABELS[self]

    @classmethod
    def from_source(cls, value: str) -> Status:
        """Map a source status to the controlled vocabulary.

        Adapters retain the original source value in a record's
        ``source_status`` or ``failure_kind`` field when that distinction is
        material to the reported failure.
        """

        normalized = value.strip().lower().replace("-", "_").replace(" ", "_")
        try:
            return _SOURCE_STATUS_ALIASES[normalized]
        except KeyError as error:
            raise ValueError(f"unsupported source status: {value!r}") from error


_STATUS_DISPLAY_LABELS: dict[Status, str] = {
    Status.PASSED: "Passed",
    Status.FAILED: "Failed",
    Status.BLOCKED: "Blocked",
    Status.ARTIFACT_UNAVAILABLE: "Artifact unavailable",
    Status.NOT_EXECUTED: "Not executed",
    Status.NOT_APPLICABLE: "Not applicable",
    Status.NOT_COLLECTED: "Not collected",
}

_SOURCE_STATUS_ALIASES: dict[str, Status] = {
    "passed": Status.PASSED,
    "pass": Status.PASSED,
    "success": Status.PASSED,
    "succeeded": Status.PASSED,
    "failed": Status.FAILED,
    "failure": Status.FAILED,
    "conversion_failed": Status.FAILED,
    "runtime_failed": Status.FAILED,
    "execution_failed": Status.FAILED,
    "benchmark_failed": Status.FAILED,
    "blocked": Status.BLOCKED,
    "hardware_preflight_blocked": Status.BLOCKED,
    "preflight_blocked": Status.BLOCKED,
    "artifact_unavailable": Status.ARTIFACT_UNAVAILABLE,
    "model_artifact_unavailable": Status.ARTIFACT_UNAVAILABLE,
    "unavailable": Status.ARTIFACT_UNAVAILABLE,
    "not_executed": Status.NOT_EXECUTED,
    "unexecuted": Status.NOT_EXECUTED,
    "not_run": Status.NOT_EXECUTED,
    "not_applicable": Status.NOT_APPLICABLE,
    "na": Status.NOT_APPLICABLE,
    "not_collected": Status.NOT_COLLECTED,
}


@dataclass(frozen=True, slots=True)
class AttemptRecord:
    """One intended configuration and its final execution status."""

    route_id: str
    campaign_id: str
    test_case_id: str
    attempt_id: str
    status: Status
    executed: bool
    reason: str = ""
    model_id: str | None = None
    weight_format_id: str | None = None
    cache_format_id: str | None = None
    backend_id: str | None = None
    source_status: str | None = None
    failure_kind: str | None = None
    evidence_ids: tuple[str, ...] = ()

    def __post_init__(self) -> None:
        if self.status is not Status.PASSED and not self.reason.strip():
            raise ValueError("non-passed attempt requires a reason")
        if self.status is Status.PASSED and not self.executed:
            raise ValueError("passed attempt must be executed")

    def to_row(self) -> dict[str, object]:
        return {
            "route_id": self.route_id,
            "campaign_id": self.campaign_id,
            "test_case_id": self.test_case_id,
            "attempt_id": self.attempt_id,
            "status": self.status.value,
            "executed": self.executed,
            "reason": self.reason,
            "model_id": self.model_id,
            "weight_format_id": self.weight_format_id,
            "cache_format_id": self.cache_format_id,
            "backend_id": self.backend_id,
            "source_status": self.source_status,
            "failure_kind": self.failure_kind,
            "evidence_ids": list(self.evidence_ids),
        }


@dataclass(frozen=True, slots=True)
class MeasurementRecord:
    """A single observed measurement; absent metrics remain ``None``."""

    route_id: str
    campaign_id: str
    test_case_id: str
    attempt_id: str
    measurement_id: str
    run_id: str | None = None
    repetition_id: str | None = None
    source_evidence_id: str | None = None
    latency_ms: float | None = None
    prompt_tokens_per_second: float | None = None
    generation_tokens_per_second: float | None = None
    peak_working_set_bytes: int | None = None
    input_tokens: int | None = None
    output_tokens: int | None = None

    def to_row(self) -> dict[str, object]:
        return {
            "route_id": self.route_id,
            "campaign_id": self.campaign_id,
            "test_case_id": self.test_case_id,
            "attempt_id": self.attempt_id,
            "measurement_id": self.measurement_id,
            "run_id": self.run_id,
            "repetition_id": self.repetition_id,
            "source_evidence_id": self.source_evidence_id,
            "latency_ms": self.latency_ms,
            "prompt_tokens_per_second": self.prompt_tokens_per_second,
            "generation_tokens_per_second": self.generation_tokens_per_second,
            "peak_working_set_bytes": self.peak_working_set_bytes,
            "input_tokens": self.input_tokens,
            "output_tokens": self.output_tokens,
        }


@dataclass(frozen=True, slots=True)
class SummaryRecord:
    """A derived metric with its aggregation rule and source observations."""

    route_id: str
    campaign_id: str
    test_case_id: str
    summary_id: str
    metric_name: str
    value: float | int | None
    unit: str
    aggregation: str
    source_measurement_ids: tuple[str, ...] = ()

    def to_row(self) -> dict[str, object]:
        return {
            "route_id": self.route_id,
            "campaign_id": self.campaign_id,
            "test_case_id": self.test_case_id,
            "summary_id": self.summary_id,
            "metric_name": self.metric_name,
            "value": self.value,
            "unit": self.unit,
            "aggregation": self.aggregation,
            "source_measurement_ids": list(self.source_measurement_ids),
        }


@dataclass(frozen=True, slots=True)
class QualityRecord:
    """One quality score or quality observation for a prompt and criterion."""

    route_id: str
    campaign_id: str
    test_case_id: str
    quality_id: str
    prompt_id: str | None = None
    criterion_id: str | None = None
    score: float | None = None
    maximum_score: float | None = None
    prompt_suite_id: str | None = None
    rubric_id: str | None = None
    scoring_version: str | None = None
    source_evidence_id: str | None = None

    def to_row(self) -> dict[str, object]:
        return {
            "route_id": self.route_id,
            "campaign_id": self.campaign_id,
            "test_case_id": self.test_case_id,
            "quality_id": self.quality_id,
            "prompt_id": self.prompt_id,
            "criterion_id": self.criterion_id,
            "score": self.score,
            "maximum_score": self.maximum_score,
            "prompt_suite_id": self.prompt_suite_id,
            "rubric_id": self.rubric_id,
            "scoring_version": self.scoring_version,
            "source_evidence_id": self.source_evidence_id,
        }


@dataclass(frozen=True, slots=True)
class FailureRecord:
    """The structured explanation for a failed, blocked, or unavailable attempt."""

    route_id: str
    campaign_id: str
    test_case_id: str
    attempt_id: str
    failure_id: str
    status: Status
    stage: str
    reason: str
    source_status: str | None = None
    evidence_ids: tuple[str, ...] = ()

    def to_row(self) -> dict[str, object]:
        return {
            "route_id": self.route_id,
            "campaign_id": self.campaign_id,
            "test_case_id": self.test_case_id,
            "attempt_id": self.attempt_id,
            "failure_id": self.failure_id,
            "status": self.status.value,
            "stage": self.stage,
            "reason": self.reason,
            "source_status": self.source_status,
            "evidence_ids": list(self.evidence_ids),
        }


@dataclass(frozen=True, slots=True)
class EvidenceRecord:
    """Portable provenance for a source or derived evidence artifact."""

    route_id: str
    campaign_id: str
    evidence_id: str
    role: str
    relative_path: str
    sha256: str
    size_bytes: int
    source_label: str | None = None
    derived: bool = False
    input_evidence_ids: tuple[str, ...] = ()

    def to_row(self) -> dict[str, object]:
        return {
            "route_id": self.route_id,
            "campaign_id": self.campaign_id,
            "evidence_id": self.evidence_id,
            "role": self.role,
            "relative_path": self.relative_path,
            "sha256": self.sha256,
            "size_bytes": self.size_bytes,
            "source_label": self.source_label,
            "derived": self.derived,
            "input_evidence_ids": list(self.input_evidence_ids),
        }


@dataclass(frozen=True, slots=True)
class RouteBundle:
    """All normalized records and system metadata for one campaign route."""

    route_id: str
    campaign_id: str
    attempts: tuple[AttemptRecord, ...] = ()
    measurements: tuple[MeasurementRecord, ...] = ()
    summaries: tuple[SummaryRecord, ...] = ()
    quality: tuple[QualityRecord, ...] = ()
    failures: tuple[FailureRecord, ...] = ()
    evidence: tuple[EvidenceRecord, ...] = ()
    repository: dict[str, object] = field(default_factory=dict)
    hardware: dict[str, object] = field(default_factory=dict)
    software: dict[str, object] = field(default_factory=dict)

    def __post_init__(self) -> None:
        object.__setattr__(self, "repository", dict(self.repository))
        object.__setattr__(self, "hardware", dict(self.hardware))
        object.__setattr__(self, "software", dict(self.software))

    def to_row(self) -> dict[str, object]:
        return {
            "route_id": self.route_id,
            "campaign_id": self.campaign_id,
            "attempt_count": len(self.attempts),
            "measurement_count": len(self.measurements),
            "summary_count": len(self.summaries),
            "quality_count": len(self.quality),
            "failure_count": len(self.failures),
            "evidence_count": len(self.evidence),
            "repository": dict(self.repository),
            "hardware": dict(self.hardware),
            "software": dict(self.software),
        }
