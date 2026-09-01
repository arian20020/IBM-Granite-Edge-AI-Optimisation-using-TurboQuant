"""Guarded cross-route comparisons and collection-wide catalogs.

The comparison gate is deliberately conservative.  It describes route-wide
protocol compatibility; it never turns disjoint measurements into a ranking.
"""

from __future__ import annotations

from collections import Counter, defaultdict
from dataclasses import dataclass
from datetime import date
import json
from pathlib import Path
import re
from typing import Iterable, Mapping, Sequence

from .csvio import write_csv, write_json
from .docx_renderer import render_docx
from .evidence import validate_sha256_manifest
from .markdown_renderer import render_markdown
from .models import RouteBundle, Status
from .openvino_report import SECTION_ORDER, regenerate_route_manifest
from .parity import compare_markdown_docx
from .report_model import Report, ReportNote, ReportParagraph, ReportSection, ReportTable


_THROUGHPUT_ALIASES = {
    "throughput": "generation_tokens_per_second",
    "decode_throughput": "generation_tokens_per_second",
    "decode_tokens_per_second": "generation_tokens_per_second",
    "generation_tokens_per_second": "generation_tokens_per_second",
}
_LEVELS = {"direct", "normalized_with_caveat", "descriptive_only", "not_comparable"}
_STATUS_ORDER = tuple(Status)


@dataclass(frozen=True, slots=True)
class Comparability:
    """Machine-readable route-pair comparability decision."""

    classification: str
    reasons: tuple[str, ...]
    reason_details: Mapping[str, Mapping[str, object]]
    matched_case_ids: tuple[str, ...] = ()
    left_only_case_ids: tuple[str, ...] = ()
    right_only_case_ids: tuple[str, ...] = ()
    signature_mismatches: tuple[Mapping[str, object], ...] = ()

    def __post_init__(self) -> None:
        if self.classification not in _LEVELS:
            raise ValueError(f"unsupported comparability classification: {self.classification!r}")
        object.__setattr__(self, "reasons", tuple(self.reasons))
        object.__setattr__(self, "matched_case_ids", tuple(self.matched_case_ids))
        object.__setattr__(self, "left_only_case_ids", tuple(self.left_only_case_ids))
        object.__setattr__(self, "right_only_case_ids", tuple(self.right_only_case_ids))
        object.__setattr__(self, "signature_mismatches", tuple(dict(row) for row in self.signature_mismatches))
        object.__setattr__(
            self,
            "reason_details",
            {key: dict(value) for key, value in self.reason_details.items()},
        )

    @property
    def level(self) -> str:
        """Compatibility alias for consumers that call the decision a level."""
        return self.classification

    @property
    def matched_case_count(self) -> int:
        return len(self.matched_case_ids)

    @property
    def left_only_case_count(self) -> int:
        return len(self.left_only_case_ids)

    @property
    def right_only_case_count(self) -> int:
        return len(self.right_only_case_ids)

    def to_row(self) -> dict[str, object]:
        return {
            "classification": self.classification,
            "reason_codes_json": json.dumps(self.reasons, separators=(",", ":")),
            "reason_details_json": json.dumps(
                self.reason_details, ensure_ascii=False, sort_keys=True, separators=(",", ":")
            ),
            "matched_case_count": self.matched_case_count,
            "matched_case_ids_json": json.dumps(self.matched_case_ids, separators=(",", ":")),
            "left_only_case_count": self.left_only_case_count,
            "left_only_case_ids_json": json.dumps(self.left_only_case_ids, separators=(",", ":")),
            "right_only_case_count": self.right_only_case_count,
            "right_only_case_ids_json": json.dumps(self.right_only_case_ids, separators=(",", ":")),
            "signature_mismatches_json": json.dumps(
                self.signature_mismatches, ensure_ascii=False, sort_keys=True, separators=(",", ":")
            ),
        }


def _jsonable(value: object) -> object:
    if isinstance(value, (set, frozenset, tuple)):
        return [_jsonable(item) for item in sorted(value, key=str)]
    if isinstance(value, dict):
        return {str(key): _jsonable(item) for key, item in sorted(value.items(), key=lambda pair: str(pair[0]))}
    return value


def _backend_class(value: str) -> str:
    token = value.casefold()
    if "cpu" in token:
        return "cpu"
    if any(item in token for item in ("gpu", "vulkan", "sycl")):
        return "gpu"
    return token


def _passed_attempts_by_case(bundle: RouteBundle) -> dict[str, list[object]]:
    result: dict[str, list[object]] = defaultdict(list)
    for row in bundle.attempts:
        if row.status is Status.PASSED:
            result[row.test_case_id].append(row)
    return result


def _one_or_many(values: Iterable[object]) -> object:
    unique = sorted({_jsonable(value) if not isinstance(value, dict) else json.dumps(value, sort_keys=True) for value in values}, key=str)
    decoded = [json.loads(value) if isinstance(value, str) and value.startswith(("{", "[")) else value for value in unique]
    if not decoded:
        return {"status": "missing"}
    return decoded[0] if len(decoded) == 1 else decoded


def _case_identity(bundle: RouteBundle, case_id: str, attempts: Mapping[str, Sequence[object]]) -> dict[str, object]:
    rows = attempts.get(case_id, ())
    models = [str(row.model_id).casefold() for row in rows if row.model_id]
    backends = [_backend_class(str(row.backend_id)) for row in rows if row.backend_id]
    if not backends and bundle.route_id.startswith("openvino-"):
        backends = ["cpu"]
    return {
        "model_identity": _one_or_many(models),
        "backend_class": _one_or_many(backends),
    }


def _throughput_signatures(bundle: RouteBundle, metric: str) -> dict[str, dict[str, object]]:
    attempts = _passed_attempts_by_case(bundle)
    summaries_by_case: dict[str, list[object]] = defaultdict(list)
    for row in bundle.summaries:
        if row.metric_name == metric and row.value is not None and row.test_case_id in attempts:
            summaries_by_case[row.test_case_id].append(row)
    measurements_by_case: dict[str, dict[str, object]] = defaultdict(dict)
    for row in bundle.measurements:
        measurements_by_case[row.test_case_id][row.measurement_id] = row
    result: dict[str, dict[str, object]] = {}
    for case_id, summaries in sorted(summaries_by_case.items()):
        input_lengths: dict[str, object] = {}
        output_lengths: dict[str, object] = {}
        measurement_lineage: dict[str, dict[str, object]] = {}
        repetition_treatment: dict[str, dict[str, object]] = {}
        metric_definition: dict[str, dict[str, object]] = {}
        for summary_ordinal, summary in enumerate(summaries, start=1):
            summary_key = f"summary-{summary_ordinal:03d}"
            source_keys: list[str] = []
            for source_ordinal, measurement_id in enumerate(summary.source_measurement_ids, start=1):
                measurement = measurements_by_case[case_id].get(measurement_id)
                repetition_id = (
                    str(measurement.repetition_id)
                    if measurement is not None and measurement.repetition_id
                    else "missing"
                )
                lineage_key = (
                    f"{summary_key}/source-{source_ordinal:03d}/repetition-{repetition_id}"
                )
                source_keys.append(lineage_key)
                measurement_lineage[lineage_key] = {
                    "summary_ordinal": summary_ordinal,
                    "source_ordinal": source_ordinal,
                    "repetition_id": repetition_id if repetition_id != "missing" else {"status": "missing"},
                }
                input_lengths[lineage_key] = (
                    measurement.input_tokens
                    if measurement is not None and measurement.input_tokens is not None
                    else {"status": "missing"}
                )
                output_lengths[lineage_key] = (
                    measurement.output_tokens
                    if measurement is not None and measurement.output_tokens is not None
                    else {"status": "missing"}
                )
            repetition_treatment[summary_key] = {
                "aggregation": summary.aggregation.casefold(),
                "source_measurement_count": len(summary.source_measurement_ids),
                "source_lineage_keys": source_keys,
            }
            metric_definition[summary_key] = {
                "metric_name": summary.metric_name,
                "unit": summary.unit.casefold(),
            }
        signature = _case_identity(bundle, case_id, attempts)
        signature.update({
            "input_length": input_lengths or {"status": "missing"},
            "output_length": output_lengths or {"status": "missing"},
            "measurement_lineage": measurement_lineage or {"status": "missing"},
            "repetition_treatment": repetition_treatment or {"status": "missing"},
            "metric_definition": metric_definition or {"status": "missing"},
        })
        result[case_id] = signature
    return result


def _quality_aggregation(bundle: RouteBundle, rows: Sequence[object]) -> str:
    explicit = bundle.repository.get("quality_aggregation")
    if explicit:
        return str(explicit).casefold()
    criterion_ids = {str(getattr(row, "criterion_id", "")) for row in rows}
    if any(":" in criterion_id for criterion_id in criterion_ids):
        return "10 * awarded criterion points / total possible criterion points"
    return "arithmetic mean of per-prompt scores"


def _quality_signatures(bundle: RouteBundle) -> dict[str, dict[str, object]]:
    attempts = _passed_attempts_by_case(bundle)
    by_case: dict[str, list[object]] = defaultdict(list)
    for row in bundle.quality:
        if row.score is not None and row.test_case_id in attempts:
            by_case[row.test_case_id].append(row)
    result: dict[str, dict[str, object]] = {}
    for case_id, rows in sorted(by_case.items()):
        identity = _case_identity(bundle, case_id, attempts)
        prompt_set = sorted({str(row.prompt_id) for row in rows if row.prompt_id})
        maximums = [float(row.maximum_score) for row in rows if row.maximum_score is not None]
        row_signatures: dict[str, dict[str, object]] = {}
        for row in rows:
            prompt_key = str(row.prompt_id) if row.prompt_id else f"__missing_prompt__:{row.quality_id}"
            criterion_key = str(row.criterion_id) if row.criterion_id else f"__missing_criterion__:{row.quality_id}"
            row_id = f"{prompt_key}::{criterion_key}"
            if row_id in row_signatures:
                row_id = f"{row_id}::{row.quality_id}"
            row_signatures[row_id] = {
                "prompt_id": str(row.prompt_id) if row.prompt_id else {"status": "missing"},
                "criterion_id": str(row.criterion_id) if row.criterion_id else {"status": "missing"},
                "prompt_suite": str(row.prompt_suite_id) if row.prompt_suite_id else {"status": "missing"},
                "rubric": str(row.rubric_id) if row.rubric_id else {"status": "missing"},
                "scoring_version": str(row.scoring_version) if row.scoring_version else {"status": "missing"},
                "maximum_score": float(row.maximum_score) if row.maximum_score is not None else {"status": "missing"},
            }
        identity.update({
            "prompt_set": prompt_set if prompt_set else {"status": "missing"},
            "denominator": round(sum(maximums), 12) if len(maximums) == len(rows) else {"status": "missing"},
            "aggregation": _quality_aggregation(bundle, rows) or {"status": "missing"},
            "rows": row_signatures,
        })
        result[case_id] = identity
    return result


def _quality_method_profile(signatures: Mapping[str, Mapping[str, object]]) -> dict[str, object]:
    models: set[str] = set()
    backends: set[str] = set()
    prompts: set[str] = set()
    prompt_ids: set[str] = set()
    criterion_ids: set[str] = set()
    suites: set[str] = set()
    rubrics: set[str] = set()
    versions: set[str] = set()
    denominators: set[object] = set()
    aggregations: set[str] = set()
    for signature in signatures.values():
        if not _missing_status(signature["model_identity"]):
            model_values = signature["model_identity"] if isinstance(signature["model_identity"], list) else [signature["model_identity"]]
            models.update(str(value) for value in model_values)
        if not _missing_status(signature["backend_class"]):
            backend_values = signature["backend_class"] if isinstance(signature["backend_class"], list) else [signature["backend_class"]]
            backends.update(str(value) for value in backend_values)
        prompt_set = signature["prompt_set"]
        if isinstance(prompt_set, list):
            prompts.update(str(value) for value in prompt_set)
        denominator = signature["denominator"]
        if not _missing_status(denominator):
            denominators.add(denominator)
        aggregation = signature["aggregation"]
        if not _missing_status(aggregation):
            aggregations.add(str(aggregation))
        for row in signature["rows"].values():
            if not _missing_status(row["prompt_id"]):
                prompt_ids.add(str(row["prompt_id"]))
            if not _missing_status(row["criterion_id"]):
                criterion_ids.add(str(row["criterion_id"]))
            if not _missing_status(row["prompt_suite"]):
                suites.add(str(row["prompt_suite"]))
            if not _missing_status(row["rubric"]):
                rubrics.add(str(row["rubric"]))
            if not _missing_status(row["scoring_version"]):
                versions.add(str(row["scoring_version"]))
    return {
        "model_identity": sorted(models),
        "backend_class": sorted(backends),
        "prompt_set": sorted(prompts),
        "prompt_id": sorted(prompt_ids),
        "criterion_id": sorted(criterion_ids),
        "prompt_suite": sorted(suites),
        "rubric": sorted(rubrics),
        "scoring_version": sorted(versions),
        "denominator": sorted(denominators, key=str),
        "aggregation": sorted(aggregations),
    }


def _quality_method_missing_locations(
    signatures: Mapping[str, Mapping[str, object]],
) -> dict[str, list[dict[str, str]]]:
    """Return named route-method locations lacking required quality metadata."""
    missing: dict[str, list[dict[str, str]]] = {
        field: []
        for field in (
            "model_identity", "backend_class", "prompt_set", "prompt_id", "criterion_id",
            "prompt_suite", "rubric", "scoring_version", "denominator", "aggregation",
        )
    }
    for case_id, signature in signatures.items():
        for field in ("model_identity", "backend_class", "prompt_set", "denominator", "aggregation"):
            if _missing_status(signature[field]):
                missing[field].append({"case_id": case_id})
        for row_id, row in signature["rows"].items():
            for field in ("prompt_id", "criterion_id", "prompt_suite", "rubric", "scoring_version"):
                if _missing_status(row[field]):
                    missing[field].append({"case_id": case_id, "row_id": row_id})
    return missing


def _quality_profile_value(
    profile: Mapping[str, object],
    field: str,
    missing_locations: Mapping[str, Sequence[Mapping[str, str]]],
) -> object:
    locations = missing_locations[field]
    if not locations:
        return profile[field]
    return {"status": "missing", "locations": [dict(location) for location in locations]}


def _missing_status(value: object) -> bool:
    return isinstance(value, dict) and value == {"status": "missing"}


def _contains_missing(value: object) -> bool:
    if _missing_status(value):
        return True
    if isinstance(value, Mapping):
        return any(_contains_missing(item) for item in value.values())
    if isinstance(value, (list, tuple)):
        return any(_contains_missing(item) for item in value)
    return False


def _scope(left: Mapping[str, object], right: Mapping[str, object]) -> tuple[tuple[str, ...], tuple[str, ...], tuple[str, ...]]:
    left_ids = set(left)
    right_ids = set(right)
    return tuple(sorted(left_ids & right_ids)), tuple(sorted(left_ids - right_ids)), tuple(sorted(right_ids - left_ids))


def _add_mismatch(
    mismatches: list[dict[str, object]],
    *,
    case_id: str,
    field: str,
    left: object,
    right: object,
    row_id: str | None = None,
) -> None:
    item = {"case_id": case_id, "field": field, "left": _jsonable(left), "right": _jsonable(right)}
    if row_id is not None:
        item["row_id"] = row_id
    mismatches.append(item)


def _reason_details(reasons: Sequence[str], mismatches: Sequence[Mapping[str, object]]) -> dict[str, dict[str, object]]:
    return {
        reason: {"mismatches": [dict(row) for row in mismatches if row.get("reason_code") == reason]}
        for reason in reasons
    }


def _missing_metric_decision(
    left_signatures: Mapping[str, object], right_signatures: Mapping[str, object]
) -> Comparability | None:
    if left_signatures and right_signatures:
        return None
    reasons = tuple(
        reason
        for missing, reason in (
            (not left_signatures, "left_metric_evidence_missing"),
            (not right_signatures, "right_metric_evidence_missing"),
        )
        if missing
    )
    status = {
        "left": {"status": "present" if left_signatures else "missing"},
        "right": {"status": "present" if right_signatures else "missing"},
    }
    matched, left_only, right_only = _scope(left_signatures, right_signatures)
    return Comparability(
        "not_comparable",
        reasons,
        {reason: dict(status) for reason in reasons},
        matched,
        left_only,
        right_only,
    )


def classify_comparability(left: RouteBundle, right: RouteBundle, metric: str) -> Comparability:
    """Classify whether two route-wide metric protocols support comparison.

    A ``direct`` decision means every required route-wide protocol dimension
    matches.  It does not assert that every individual configuration can be
    paired; consumers must still match model/configuration rows before using a
    value.  Differences that token-rate normalization can expose safely receive
    ``normalized_with_caveat``.  Quality-method differences are descriptive
    only, never rankable.
    """

    normalized_metric = metric.strip().casefold().replace("-", "_").replace(" ", "_")
    if normalized_metric in {"quality", "quality_score", "quality_ranking"}:
        left_signatures = _quality_signatures(left)
        right_signatures = _quality_signatures(right)
        missing = _missing_metric_decision(left_signatures, right_signatures)
        if missing:
            return missing
        matched, left_only, right_only = _scope(left_signatures, right_signatures)
        if not matched:
            left_profile = _quality_method_profile(left_signatures)
            right_profile = _quality_method_profile(right_signatures)
            left_missing = _quality_method_missing_locations(left_signatures)
            right_missing = _quality_method_missing_locations(right_signatures)
            profile_mismatches: list[dict[str, object]] = []
            profile_reasons = ["no_matched_case_ids"]
            profile_codes = {
                "model_identity": "model_mismatch",
                "backend_class": "backend_class_mismatch",
                "prompt_set": "prompt_set_mismatch",
                "prompt_id": "prompt_set_mismatch",
                "criterion_id": "rubric_mismatch",
                "prompt_suite": "prompt_suite_mismatch",
                "rubric": "rubric_mismatch",
                "scoring_version": "scoring_version_mismatch",
                "denominator": "denominator_mismatch",
                "aggregation": "aggregation_mismatch",
            }
            for field, code in profile_codes.items():
                left_value = _quality_profile_value(left_profile, field, left_missing)
                right_value = _quality_profile_value(right_profile, field, right_missing)
                missing_code = f"{field}_missing"
                if left_missing[field] or right_missing[field]:
                    selected_code = missing_code
                elif left_value != right_value:
                    selected_code = code
                else:
                    continue
                if selected_code not in profile_reasons:
                    profile_reasons.append(selected_code)
                if left_value != right_value or selected_code.endswith("_missing"):
                    _add_mismatch(
                        profile_mismatches,
                        case_id="__route_method_profile__",
                        field=field,
                        left=left_value,
                        right=right_value,
                    )
                    profile_mismatches[-1]["reason_code"] = selected_code
            hard_missing = any(reason.endswith("_missing") for reason in profile_reasons)
            return Comparability(
                "not_comparable" if hard_missing else "descriptive_only",
                tuple(profile_reasons),
                {
                    "no_matched_case_ids": {"left": {"case_ids": list(left_only)}, "right": {"case_ids": list(right_only)}},
                    **_reason_details(profile_reasons[1:], profile_mismatches),
                },
                matched, left_only, right_only, tuple(profile_mismatches),
            )
        mismatches: list[dict[str, object]] = []
        reason_order: list[str] = []

        def record(code: str, case_id: str, field: str, left_value: object, right_value: object, row_id: str | None = None) -> None:
            if code not in reason_order:
                reason_order.append(code)
            _add_mismatch(mismatches, case_id=case_id, field=field, left=left_value, right=right_value, row_id=row_id)
            mismatches[-1]["reason_code"] = code

        for case_id in matched:
            left_case = left_signatures[case_id]
            right_case = right_signatures[case_id]
            for field, code in (
                ("model_identity", "model_mismatch"),
                ("backend_class", "backend_class_mismatch"),
                ("prompt_set", "prompt_set_mismatch"),
                ("denominator", "denominator_mismatch"),
                ("aggregation", "aggregation_mismatch"),
            ):
                left_value, right_value = left_case[field], right_case[field]
                if _missing_status(left_value) or _missing_status(right_value):
                    record(f"{field}_missing", case_id, field, left_value, right_value)
                elif left_value != right_value:
                    record(code, case_id, field, left_value, right_value)
            left_rows = left_case["rows"]
            right_rows = right_case["rows"]
            row_ids = sorted(set(left_rows) | set(right_rows))
            for row_id in row_ids:
                if row_id not in left_rows or row_id not in right_rows:
                    record(
                        "quality_row_set_mismatch", case_id, "quality_row",
                        left_rows.get(row_id, {"status": "missing"}),
                        right_rows.get(row_id, {"status": "missing"}), row_id,
                    )
                    continue
                for field, code in (
                    ("prompt_id", "prompt_set_mismatch"),
                    ("criterion_id", "rubric_mismatch"),
                    ("prompt_suite", "prompt_suite_mismatch"),
                    ("rubric", "rubric_mismatch"),
                    ("scoring_version", "scoring_version_mismatch"),
                    ("maximum_score", "denominator_mismatch"),
                ):
                    left_value, right_value = left_rows[row_id][field], right_rows[row_id][field]
                    if _missing_status(left_value) or _missing_status(right_value):
                        record(f"{field}_missing", case_id, field, left_value, right_value, row_id)
                    elif left_value != right_value:
                        record(code, case_id, field, left_value, right_value, row_id)
        if reason_order:
            hard_missing = any(reason.endswith("_missing") for reason in reason_order)
            classification = "not_comparable" if hard_missing else "descriptive_only"
            return Comparability(
                classification, tuple(reason_order), _reason_details(reason_order, mismatches),
                matched, left_only, right_only, tuple(mismatches),
            )
        return Comparability(
            "direct", ("all_required_dimensions_match",),
            {"all_required_dimensions_match": {"matched_case_ids": list(matched)}},
            matched, left_only, right_only,
        )

    try:
        canonical_metric = _THROUGHPUT_ALIASES[normalized_metric]
    except KeyError as error:
        raise ValueError(f"unsupported comparison metric: {metric!r}") from error
    left_signatures = _throughput_signatures(left, canonical_metric)
    right_signatures = _throughput_signatures(right, canonical_metric)
    missing = _missing_metric_decision(left_signatures, right_signatures)
    if missing:
        return missing
    matched, left_only, right_only = _scope(left_signatures, right_signatures)
    if not matched:
        return Comparability(
            "not_comparable", ("no_matched_case_ids",),
            {"no_matched_case_ids": {"left": {"case_ids": list(left_only)}, "right": {"case_ids": list(right_only)}}},
            matched, left_only, right_only,
        )
    mismatches: list[dict[str, object]] = []
    reasons: list[str] = []
    field_codes = (
        ("model_identity", "model_mismatch"),
        ("input_length", "input_length_mismatch"),
        ("output_length", "output_length_mismatch"),
        ("measurement_lineage", "measurement_lineage_mismatch"),
        ("backend_class", "backend_class_mismatch"),
        ("repetition_treatment", "repetition_treatment_mismatch"),
        ("metric_definition", "metric_definition_mismatch"),
    )
    for case_id in matched:
        for field, mismatch_code in field_codes:
            left_value = left_signatures[case_id][field]
            right_value = right_signatures[case_id][field]
            has_missing = _contains_missing(left_value) or _contains_missing(right_value)
            code = f"{field}_missing" if has_missing else mismatch_code
            if has_missing or left_value != right_value:
                if code not in reasons:
                    reasons.append(code)
                _add_mismatch(mismatches, case_id=case_id, field=field, left=left_value, right=right_value)
                mismatches[-1]["reason_code"] = code
    if not reasons:
        return Comparability(
            "direct", ("all_required_dimensions_match",),
            {"all_required_dimensions_match": {"matched_case_ids": list(matched)}},
            matched, left_only, right_only,
        )
    hard_codes = {
        "model_mismatch",
        "backend_class_mismatch",
        "metric_definition_mismatch",
        "model_identity_missing",
        "input_length_missing",
        "output_length_missing",
        "measurement_lineage_mismatch",
        "measurement_lineage_missing",
        "backend_class_missing",
        "repetition_treatment_missing",
        "metric_definition_missing",
    }
    hard_codes.update({reason for reason in reasons if reason.endswith("_missing")})
    classification = "not_comparable" if hard_codes.intersection(reasons) else "normalized_with_caveat"
    return Comparability(
        classification, tuple(reasons), _reason_details(reasons, mismatches),
        matched, left_only, right_only, tuple(mismatches),
    )


def _pairs(bundles: Sequence[RouteBundle]) -> Iterable[tuple[RouteBundle, RouteBundle]]:
    ordered = sorted(bundles, key=lambda bundle: bundle.route_id)
    for index, left in enumerate(ordered):
        for right in ordered[index + 1 :]:
            yield left, right


def _comparison_rows(bundles: Sequence[RouteBundle]) -> list[dict[str, object]]:
    rows: list[dict[str, object]] = []
    for left, right in _pairs(bundles):
        for metric in ("generation_tokens_per_second", "quality"):
            decision = classify_comparability(left, right, metric)
            rows.append(
                {
                    "left_route_id": left.route_id,
                    "right_route_id": right.route_id,
                    "metric": metric,
                    **decision.to_row(),
                }
            )
    return rows


def _quality_summary_rows(bundle: RouteBundle) -> Iterable[dict[str, object]]:
    by_case: dict[str, list[object]] = defaultdict(list)
    for row in bundle.quality:
        if row.score is not None and row.maximum_score is not None:
            by_case[row.test_case_id].append(row)
    for test_case_id, rows in sorted(by_case.items()):
        denominator = sum(float(row.maximum_score) for row in rows)
        awarded = sum(float(row.score) for row in rows)
        yield {
            "route_id": bundle.route_id,
            "campaign_id": bundle.campaign_id,
            "test_case_id": test_case_id,
            "prompt_count": len({row.prompt_id for row in rows if row.prompt_id}),
            "record_count": len(rows),
            "awarded_points": awarded,
            "denominator": denominator,
            "normalized_score_10": 10 * awarded / denominator if denominator else None,
            "prompt_suite_ids_json": json.dumps(sorted({row.prompt_suite_id for row in rows if row.prompt_suite_id})),
            "rubric_ids_json": json.dumps(sorted({row.rubric_id for row in rows if row.rubric_id})),
            "scoring_versions_json": json.dumps(sorted({row.scoring_version for row in rows if row.scoring_version})),
            "aggregation": _quality_aggregation(bundle, rows),
        }


def build_catalogs(bundles: Sequence[RouteBundle]) -> dict[str, list[dict[str, object]]]:
    """Build deterministic collection-wide catalog rows without dropping outcomes."""
    ordered = sorted(bundles, key=lambda bundle: bundle.route_id)
    route_register = [
        {
            "route_id": bundle.route_id,
            "campaign_id": bundle.campaign_id,
            "attempt_count": len(bundle.attempts),
            "measurement_count": len(bundle.measurements),
            "summary_count": len(bundle.summaries),
            "quality_count": len(bundle.quality),
            "failure_count": len(bundle.failures),
            "evidence_count": len(bundle.evidence),
        }
        for bundle in ordered
    ]
    campaign_summary = [
        {
            "route_id": bundle.route_id,
            "campaign_id": bundle.campaign_id,
            "test_case_id": row.test_case_id,
            "attempt_id": row.attempt_id,
            "status": row.status.value,
            "executed": row.executed,
            "reason": row.reason,
            "model_id": row.model_id,
            "weight_format_id": row.weight_format_id,
            "cache_format_id": row.cache_format_id,
            "backend_id": row.backend_id,
            "source_status": row.source_status,
            "failure_kind": row.failure_kind,
            "evidence_ids_json": json.dumps(row.evidence_ids),
        }
        for bundle in ordered
        for row in bundle.attempts
    ]
    performance = []
    for bundle in ordered:
        for record in bundle.summaries:
            row = record.to_row()
            row["source_measurement_ids"] = json.dumps(row["source_measurement_ids"])
            performance.append(row)
    quality = [row for bundle in ordered for row in _quality_summary_rows(bundle)]
    failures = []
    for bundle in ordered:
        for record in bundle.failures:
            row = record.to_row()
            row["evidence_ids"] = json.dumps(row["evidence_ids"])
            failures.append(row)
    evidence = []
    for bundle in ordered:
        for record in bundle.evidence:
            row = record.to_row()
            row["input_evidence_ids"] = json.dumps(row["input_evidence_ids"])
            evidence.append(row)
    claim_map = []
    for bundle in ordered:
        counts = Counter(row.status.value for row in bundle.attempts)
        evidence_ids = sorted(
            {evidence_id for row in bundle.attempts for evidence_id in row.evidence_ids}
            or {row.evidence_id for row in bundle.evidence}
        )
        claim_map.append(
            {
                "claim_id": f"CROSS-{bundle.route_id}-ACCOUNTING",
                "route_id": bundle.route_id,
                "claim": f"Complete route accounting: {json.dumps(dict(sorted(counts.items())), sort_keys=True)}",
                "test_case_ids_json": json.dumps(sorted({row.test_case_id for row in bundle.attempts})),
                "evidence_ids_json": json.dumps(evidence_ids),
                "claim_boundary": "Route-local validated accounting; not a universal ranking",
            }
        )
    return {
        "route-register.csv": route_register,
        "campaign-summary.csv": campaign_summary,
        "performance-summary.csv": performance,
        "quality-summary.csv": quality,
        "failure-summary.csv": failures,
        "evidence-manifest.csv": evidence,
        "claim-evidence-map.csv": claim_map,
        "comparability-matrix.csv": _comparison_rows(ordered),
    }


def build_cross_route_validation(
    report: Report,
    catalogs: Mapping[str, Sequence[Mapping[str, object]]],
    bundles: Sequence[RouteBundle],
) -> dict[str, object]:
    """Derive comparison validation facts from the report and generated rows."""
    comparison_rows = list(catalogs.get("comparability-matrix.csv", ()))
    campaign_rows = list(catalogs.get("campaign-summary.csv", ()))
    expected_attempts = {
        (bundle.route_id, bundle.campaign_id, row.test_case_id, row.attempt_id)
        for bundle in bundles
        for row in bundle.attempts
    }
    published_attempts = {
        (str(row.get("route_id")), str(row.get("campaign_id")), str(row.get("test_case_id")), str(row.get("attempt_id")))
        for row in campaign_rows
    }
    complete_attempt_accounting = published_attempts == expected_attempts and len(campaign_rows) == len(expected_attempts)
    expected_comparison_rows = _comparison_rows(tuple(bundles))
    comparison_catalog_matches_bundles = comparison_rows == expected_comparison_rows

    reason_errors: list[str] = []
    for index, row in enumerate(comparison_rows):
        try:
            reasons = json.loads(str(row["reason_codes_json"]))
            details = json.loads(str(row["reason_details_json"]))
            mismatches = json.loads(str(row["signature_mismatches_json"]))
            matched = json.loads(str(row["matched_case_ids_json"]))
            left_only = json.loads(str(row["left_only_case_ids_json"]))
            right_only = json.loads(str(row["right_only_case_ids_json"]))
            if not reasons or not isinstance(details, dict) or not isinstance(mismatches, list):
                reason_errors.append(f"row {index}: missing machine-readable reason content")
            if int(row["matched_case_count"]) != len(matched):
                reason_errors.append(f"row {index}: matched case count mismatch")
            if int(row["left_only_case_count"]) != len(left_only):
                reason_errors.append(f"row {index}: left-only case count mismatch")
            if int(row["right_only_case_count"]) != len(right_only):
                reason_errors.append(f"row {index}: right-only case count mismatch")
        except (KeyError, TypeError, ValueError, json.JSONDecodeError) as error:
            reason_errors.append(f"row {index}: {error}")

    tables = [
        block
        for section in report.sections
        for block in section.blocks
        if isinstance(block, ReportTable)
    ]
    def is_ranking_table(table: ReportTable) -> bool:
        label = f"{table.table_id} {table.title}".casefold()
        if any(token in label for token in ("rank", "leaderboard", "scorecard", "standings", "best repository", "best route")):
            return True
        columns = {column.casefold().strip() for column in table.columns}
        position = bool(columns & {"rank", "ranking", "position", "place"})
        entity = bool(columns & {"repository", "route", "model", "configuration"})
        score = bool(columns & {"score", "quality", "performance", "composite", "overall"})
        return position and entity and score

    def has_affirmative_ranking_prose(text: str) -> bool:
        normalized = " ".join(text.casefold().replace("’", "'").split())
        for contraction, expanded in (
            ("isn't", "is not"),
            ("aren't", "are not"),
            ("wasn't", "was not"),
            ("weren't", "were not"),
            ("doesn't", "does not"),
            ("don't", "do not"),
            ("didn't", "did not"),
            ("can't", "cannot"),
            ("couldn't", "could not"),
            ("wouldn't", "would not"),
            ("shouldn't", "should not"),
        ):
            normalized = normalized.replace(contraction, expanded)
        raw_clauses = [
            clause.strip(" ,:")
            for clause in re.split(
                r"(?<=[.!?;])\s*|\s+(?:but|however|yet)\s+|"
                r",\s*(?:although|while|whereas|despite)\s+",
                normalized,
            )
            if clause.strip(" ,:")
        ]
        clauses: list[str] = []
        for clause in raw_clauses:
            if re.match(r"^(?:although|while|whereas|despite)\b", clause) and "," in clause:
                boundary, claim = clause.split(",", 1)
                clauses.extend((boundary.strip(), claim.strip()))
            else:
                clauses.append(clause)
        claim_patterns = (
            r"\b(?:repository|route|quality|performance|universal)\s+(?:ranking|leaderboard|standings)\s*:",
            r"\b(?:best|worst|top|highest|lowest)\s+(?:repository|route|model|configuration|quality|performance|score|throughput)\b",
            r"\b(?:route|repository|model|configuration)\s+(?:is|was|has|had|achieved)\s+(?:the\s+)?(?:best|worst|top|highest|lowest|first|second|third|fourth)\b",
            r"\b(?:ranks?|ranked)\s+(?:as\s+)?(?:the\s+)?(?:best|worst|top|first|second|third|fourth|1st|2nd|3rd|4th|above|below)\b",
            r"\b(?:ranks?|ranked)\s+(?:as\s+)?(?:the\s+)?number[-\s]+(?:one|two|three|four|1|2|3|4)\b",
            r"\b(?:places?|placed)\s+(?:first|second|third|fourth|1st|2nd|3rd|4th|ahead|above|below)\b",
            r"\bplaced\s+in\s+(?:first|second|third|fourth|1st|2nd|3rd|4th)\s+place\b",
            r"\b(?:takes?|took)\s+(?:the\s+)?(?:first|second|third|fourth|1st|2nd|3rd|4th)\s+place\b",
            r"\b(?:route|repository|model|configuration)\s+(?:is|was)\s+(?:first|second|third|fourth)\b",
            r"\b(?:is|was)\s+in\s+(?:first|second|third|fourth|1st|2nd|3rd|4th)\s+place\b",
            r"\b(?:is|was)\s+(?:the\s+)?(?:number[-\s]+(?:one|two|three|four|1|2|3|4)|1st|2nd|3rd|4th)\b",
            r"\b(?:higher|lower|better|worse)\b(?:\s+\w+){0,4}\s+than\b",
            r"\b(?:superior|inferior)\b(?:\s+\w+){0,4}\s+to\b",
            r"\b(?:outperforms?|beats?|leads?)\b",
        )
        denial = re.compile(
            r"\b(?:does|do|did|can|could|would|should|is|are|was|were)\s+(?:not|never)\s+"
            r"(?:support|establish|justify|permit|show|demonstrate|rank|describe)\b|"
            r"\b(?:cannot|can't)\s+(?:support|establish|justify|permit|show|demonstrate|rank|be described)\b|"
            r"\b(?:ranking|leaderboard|standings)\b.{0,80}\b(?:unsupported|prohibited|forbidden|not supported)\b"
        )
        local_negation = re.compile(
            r"\b(?:is|are|was|were|has|have|had|does|do|did|can|could|would|should)\s+"
            r"(?:not|never)(?:\s+\w+){0,3}\s*$|"
            r"\b(?:no|not|never|without)\b(?:\W+\w+){0,3}\W*$|"
            r"\bneither\b(?:(?!\b(?:but|however|yet)\b).)*$"
        )
        def is_locally_negated(clause: str, claim_start: int) -> bool:
            prefix = clause[:claim_start]
            if local_negation.search(prefix):
                return True
            no_matches = tuple(re.finditer(r"\bno\b", prefix))
            if not no_matches:
                return False
            no_scope = prefix[no_matches[-1].end():]
            if re.search(r"\b(?:and|but|however|yet)\b", no_scope):
                return False
            if "," in no_scope:
                preceding_item = no_scope.rsplit(",", 1)[0]
                if re.search(
                    r"\b(?:is|are|was|were|has|have|had|does|do|did|can|could|would|should)\s+"
                    r"(?:not\s+)?\w+",
                    preceding_item,
                ):
                    return False
            return True

        for clause in clauses:
            denial_matches = tuple(denial.finditer(clause))
            for pattern in claim_patterns:
                for match in re.finditer(pattern, clause):
                    if is_locally_negated(clause, match.start()):
                        continue
                    governed_by_denial = False
                    for denial_match in denial_matches:
                        if denial_match.start() > match.start():
                            continue
                        if denial_match.end() >= match.start():
                            governed_by_denial = True
                            break
                        bridge = clause[denial_match.end():match.start()]
                        if re.search(r"\b(?:that|whether)\b", bridge) or not re.search(r"\band\b", bridge):
                            governed_by_denial = True
                            break
                    if not governed_by_denial:
                        return True
        return False

    ranking_tables = [table.table_id for table in tables if is_ranking_table(table)]
    ranking_prose = [
        f"section:{section_index}/block:{block_index}"
        for section_index, section in enumerate(report.sections, start=1)
        for block_index, block in enumerate(section.blocks, start=1)
        if isinstance(block, (ReportParagraph, ReportNote)) and has_affirmative_ranking_prose(block.text)
    ]
    comparison_tables = [table for table in tables if table.table_id == "CP-01"]
    expected_report_rows = tuple(
        (
            *(str(row[field]) for field in (
                "left_route_id", "right_route_id", "metric", "classification", "matched_case_count",
                "left_only_case_count", "right_only_case_count",
            )),
            _report_reason_summary(row),
        )
        for row in comparison_rows
    )
    report_comparison_matrix_matches_catalog = (
        len(comparison_tables) == 1 and comparison_tables[0].rows == expected_report_rows
    )
    universal_ranking_present = bool(ranking_tables or ranking_prose)
    nondirect_quality = [
        row for row in comparison_rows
        if row.get("metric") == "quality" and row.get("classification") != "direct"
    ]
    incompatible_quality_ranking_present = bool((ranking_tables or ranking_prose) and nondirect_quality)
    expected_decisions = len(tuple(_pairs(tuple(bundles)))) * 2
    decision_count_valid = len(comparison_rows) == expected_decisions
    valid = all((
        not reason_errors,
        complete_attempt_accounting,
        comparison_catalog_matches_bundles,
        decision_count_valid,
        report_comparison_matrix_matches_catalog,
        not universal_ranking_present,
        not incompatible_quality_ranking_present,
    ))
    return {
        "valid": valid,
        "decision_count": len(comparison_rows),
        "expected_decision_count": expected_decisions,
        "decision_count_valid": decision_count_valid,
        "report_comparison_matrix_matches_catalog": report_comparison_matrix_matches_catalog,
        "classifications": dict(sorted(Counter(str(row.get("classification")) for row in comparison_rows).items())),
        "machine_readable_reasons_present": not reason_errors,
        "reason_validation_errors": reason_errors,
        "complete_attempt_accounting": complete_attempt_accounting,
        "comparison_catalog_matches_bundles": comparison_catalog_matches_bundles,
        "expected_attempt_count": len(expected_attempts),
        "published_attempt_count": len(campaign_rows),
        "universal_ranking_present": universal_ranking_present,
        "ranking_table_ids": ranking_tables,
        "ranking_prose_locations": ranking_prose,
        "incompatible_quality_ranking_present": incompatible_quality_ranking_present,
    }


def _table(
    table_id: str,
    title: str,
    columns: Sequence[str],
    rows: Iterable[Iterable[object]],
    *,
    footnotes: Sequence[str] = (),
) -> ReportTable:
    return ReportTable(
        table_id=table_id,
        title=title,
        columns=tuple(columns),
        rows=tuple(tuple(str(cell) for cell in row) for row in rows),
        footnotes=tuple(footnotes),
    )


def _report_reason_summary(row: Mapping[str, object]) -> str:
    """Keep the report readable while the catalog retains complete reason JSON."""
    try:
        codes = [str(code) for code in json.loads(str(row["reason_codes_json"]))]
    except (KeyError, TypeError, json.JSONDecodeError):
        return "Invalid reason JSON; see machine-readable catalog"
    if len(codes) <= 2:
        return "; ".join(codes)
    return f"{codes[0]}; +{len(codes) - 1} more (see catalog CSV)"


def _route_conclusion(bundle: RouteBundle) -> str:
    counts = Counter(row.status for row in bundle.attempts)
    accounted = ", ".join(
        f"{counts[status]} {status.display_label.casefold()}" for status in _STATUS_ORDER if counts[status]
    )
    if bundle.route_id == "upstream-llama-cpp":
        return f"{accounted}; UL-08 is only the best observed CPU route, UL-10 only the best observed Intel GPU route, and UL-05 only the recorded fallback."
    if bundle.route_id == "atomicbot-turboquant":
        return f"{accounted}; runtime coverage is complete, while route-specific quality remains limited/provisional."
    if bundle.route_id == "animehacker-tq3-0":
        return f"{accounted}; seven runnable terminal rows are complete and historical/rejected evidence remains outside formal statistics."
    if bundle.route_id == "openvino-experimental-fork":
        return f"{accounted}; performance and v3 objective quality are published only for passed configurations."
    if bundle.route_id == "openvino-official-upstream":
        return f"{accounted}; conversion failures and hardware-preflight blocks have no fabricated measurements."
    return f"{accounted}; conclusion is bounded to this route's normalized records."


def _identity_value(values: Mapping[str, object], candidates: Sequence[str]) -> str:
    selected = {key: values[key] for key in candidates if key in values}
    if not selected:
        return "Not collected"
    return json.dumps(selected, ensure_ascii=False, sort_keys=True, default=str, separators=(",", ":"))


def build_cross_route_report(bundles: Sequence[RouteBundle]) -> Report:
    """Build an answer-first report that never ranks incompatible evidence."""
    ordered = tuple(sorted(bundles, key=lambda bundle: bundle.route_id))
    if len(ordered) < 2:
        raise ValueError("cross-route report requires at least two route bundles")
    route_ids = [bundle.route_id for bundle in ordered]
    if len(route_ids) != len(set(route_ids)):
        raise ValueError("cross-route report requires unique route IDs")
    comparisons = _comparison_rows(ordered)
    status_rows = [
        (
            bundle.route_id,
            bundle.campaign_id,
            row.test_case_id,
            row.attempt_id,
            row.status.display_label,
            "true" if row.executed else "false",
            row.reason or "Passed observation",
        )
        for bundle in ordered
        for row in bundle.attempts
    ]
    comparison_rows = [
        (
            row["left_route_id"],
            row["right_route_id"],
                row["metric"],
                row["classification"],
                row["matched_case_count"],
                row["left_only_case_count"],
                row["right_only_case_count"],
                _report_reason_summary(row),
        )
        for row in comparisons
    ]
    route_rows = [
        (
            bundle.route_id,
            bundle.campaign_id,
            len(bundle.attempts),
            len(bundle.measurements),
            len(bundle.quality),
            _route_conclusion(bundle),
        )
        for bundle in ordered
    ]
    method_rows = []
    for bundle in ordered:
        signatures = _quality_signatures(bundle)
        if not signatures:
            method_rows.append((bundle.route_id, "Not collected", "Not collected", "Not collected", "Not collected", "Not rankable"))
        else:
            profile = _quality_method_profile(signatures)
            method_rows.append(
                (
                    bundle.route_id,
                    ", ".join(profile["prompt_set"]),
                    ", ".join(profile["prompt_suite"]),
                    ", ".join(profile["rubric"]),
                    ", ".join(profile["scoring_version"]),
                    ", ".join(str(value) for value in profile["denominator"]),
                )
            )
    source_dates = [
        str(bundle.repository.get("source_date"))
        for bundle in ordered
        if bundle.repository.get("source_date")
    ]
    generated_date = date.fromisoformat(max(source_dates)) if source_dates else date(2026, 8, 30)
    evidence_ids = tuple(
        row.evidence_id
        for bundle in ordered
        for row in sorted(bundle.evidence, key=lambda item: item.evidence_id)[:2]
    )
    sections = (
        ReportSection(SECTION_ORDER[0], (
            ReportParagraph("This controlled report compares five normalized routes without manufacturing a universal score."),
            _table("DC-01", "Document identity", ("Field", "Value"), (
                ("Report", "Guarded cross-route comparison"),
                ("Route ID", "cross-route-comparison"),
                ("Revision", "R1"),
                ("Canonical source", "workbook/source/cross-route-comparison-final-report.md"),
            )),
        )),
        ReportSection(SECTION_ORDER[1], (
            ReportParagraph("The validated result is a bounded route comparison, not a league table. No universal ranking is supported."),
            _table("RT-01", "Validated route-level conclusions", ("Route", "Campaign", "Attempts", "Measurements", "Quality records", "Bounded conclusion"), route_rows),
        )),
        ReportSection(SECTION_ORDER[2], (
            ReportParagraph("Availability and complete attempt accounting precede any performance comparison."),
            ReportNote("Incompatible quality scores are not ranked. Descriptive side-by-side evidence retains its original methodology."),
        )),
        ReportSection(SECTION_ORDER[3], (
            ReportParagraph("Repository, hardware, and software identities remain in each route package and its hashed evidence index."),
            _table("ID-01", "Route identity index", ("Route", "Campaign", "Repository metadata", "Hardware metadata", "Software metadata"), (
                (
                    bundle.route_id,
                    bundle.campaign_id,
                    _identity_value(bundle.repository, ("url", "repository_url", "branch", "commit", "tag", "source_campaign", "source_date", "workbook_id", "workbook_revision")),
                    _identity_value(bundle.hardware, ("machine_id", "machine", "cpu", "gpu", "ram_bytes", "platform", "status")),
                    _identity_value(bundle.software, ("os", "openvino_versions", "cmake", "compiler", "msvc", "vulkan_sdk", "vulkan_sdk_version")),
                )
                for bundle in ordered
            )),
        )),
        ReportSection(SECTION_ORDER[4], (
            ReportParagraph("The gate evaluates route-wide protocol signatures and does not pair values across unmatched configurations."),
            _table("CP-01", "Comparability decision matrix", ("Left route", "Right route", "Metric", "Classification", "Matched", "Left only", "Right only", "Reason summary"), comparison_rows,
                   footnotes=(
                       "Direct still requires consumers to match individual model/configuration rows.",
                       "Complete machine-readable reason codes, details, case IDs, and signature mismatches are retained in the comparability catalog CSV.",
                   )),
        )),
        ReportSection(SECTION_ORDER[5], (
            ReportParagraph("Passed, failed, blocked, unavailable, and other explicit statuses remain visible; absence is never zero."),
            _table("SS-01", "Status totals by route", ("Route", "Status", "Count"), (
                (bundle.route_id, status.display_label, sum(row.status is status for row in bundle.attempts))
                for bundle in ordered for status in _STATUS_ORDER
                if any(row.status is status for row in bundle.attempts)
            )),
        )),
        ReportSection(SECTION_ORDER[6], (
            ReportParagraph("This is the complete cross-route attempt ledger, including every non-passed terminal outcome."),
            _table("ST-01", "Complete attempt accounting", ("Route", "Campaign", "Test case", "Attempt", "Status", "Executed", "Reason"), status_rows),
        )),
        ReportSection(SECTION_ORDER[7], (
            ReportParagraph("Throughput is direct only where model, input length, output length, backend class, repetition treatment, and metric definition match."),
            _table("TP-01", "Throughput comparison boundaries", ("Left route", "Right route", "Classification", "Reason codes"), (
                (row["left_route_id"], row["right_route_id"], row["classification"], row["reason_codes_json"])
                for row in comparisons if row["metric"] == "generation_tokens_per_second"
            )),
        )),
        ReportSection(SECTION_ORDER[8], (
            ReportParagraph("Quality ranking additionally requires the identical prompt set, prompt-suite identity, rubric, scoring version, denominator, and aggregation."),
            _table("QM-01", "Quality methodology boundaries", ("Route", "Prompt set", "Prompt suite", "Rubric", "Scoring version", "Denominator set"), method_rows),
            ReportNote("OpenVINO v3 and legacy llama quality evidence are descriptive only; incompatible quality scores are not ranked."),
        )),
        ReportSection(SECTION_ORDER[9], (
            ReportParagraph("Backend class is a required gate dimension. Recorded backend identity is not proof of device utilization beyond each route's own evidence."),
        )),
        ReportSection(SECTION_ORDER[10], (
            ReportParagraph("Failed, blocked, unavailable, rejected, and historical outcomes remain in their route ledgers and collection catalogs."),
            _table("FL-01", "Non-passed status accounting", ("Route", "Status", "Count"), (
                (bundle.route_id, status.display_label, sum(row.status is status for row in bundle.attempts))
                for bundle in ordered for status in _STATUS_ORDER if status is not Status.PASSED
                if any(row.status is status for row in bundle.attempts)
            )),
        )),
        ReportSection(SECTION_ORDER[11], (
            ReportParagraph("No universal best repository, universal best route, or combined quality/performance score is produced. Route extrema remain route-local observations."),
            ReportNote("A direct protocol classification permits matched-row comparison only; it does not establish causal superiority or deployment safety."),
        )),
        ReportSection(SECTION_ORDER[12], (
            ReportParagraph("Regeneration consumes the five existing normalized RouteBundle objects and does not rerun benchmarks."),
            _table("RE-01", "Reproduction outputs", ("Item", "Repository-relative path"), (
                ("Canonical report", "docs/testing/final-results/06-cross-route-comparison/workbook/source/cross-route-comparison-final-report.md"),
                ("Comparability matrix", "docs/testing/final-results/06-cross-route-comparison/results/comparability-matrix.csv"),
                ("Collection catalogs", "docs/testing/final-results/catalog/"),
            )),
        )),
        ReportSection(SECTION_ORDER[13], (
            ReportParagraph("Each route package retains its complete evidence index and checksum manifest; the cross-route claim map points back to those route authorities."),
            _table("EV-01", "Route evidence coverage", ("Route", "Evidence records", "Selected report evidence IDs"), (
                (bundle.route_id, len(bundle.evidence), ", ".join(row.evidence_id for row in sorted(bundle.evidence, key=lambda item: item.evidence_id)[:2]) or "Not collected")
                for bundle in ordered
            )),
        )),
        ReportSection(SECTION_ORDER[14], (
            ReportParagraph("Markdown is canonical; DOCX and PDF are generated derivatives with parity and visual-validation receipts."),
            _table("RV-01", "Revision history", ("Revision", "Date", "Change"), (("R1", generated_date.isoformat(), "Initial guarded cross-route comparison"),)),
        )),
    )
    return Report(
        title="Guarded cross-route comparison",
        route_id="cross-route-comparison",
        revision="R1",
        generated_date=generated_date,
        sections=sections,
        evidence_ids=evidence_ids,
    )


def _write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text if text.endswith("\n") else f"{text}\n", encoding="utf-8", newline="\n")


def _fieldnames(rows: Sequence[Mapping[str, object]], fallback: Sequence[str]) -> tuple[str, ...]:
    return tuple(rows[0]) if rows else tuple(fallback)


def write_cross_route_package(
    repo_root: Path,
    bundles: Sequence[RouteBundle],
    *,
    output_root: Path | None = None,
) -> Report:
    """Write the comparison route, synchronized MD/DOCX, receipts, and catalogs."""
    root = Path(repo_root).resolve(strict=True)
    output = (Path(output_root) if output_root else root / "docs/testing/final-results").resolve()
    try:
        output.relative_to(root)
    except ValueError as error:
        raise ValueError("output_root must resolve inside repo_root") from error
    route = output / "06-cross-route-comparison"
    catalog_root = output / "catalog"
    report = build_cross_route_report(bundles)
    catalogs = build_catalogs(bundles)
    for name, rows in catalogs.items():
        fallbacks = {
            "failure-summary.csv": ("route_id", "campaign_id", "failure_id", "status", "reason"),
            "evidence-manifest.csv": ("route_id", "campaign_id", "evidence_id", "relative_path", "sha256"),
        }
        write_csv(catalog_root / name, rows, _fieldnames(rows, fallbacks.get(name, ("route_id",))))
    comparison_rows = catalogs["comparability-matrix.csv"]
    status_rows = catalogs["campaign-summary.csv"]
    write_csv(route / "results/comparability-matrix.csv", comparison_rows, _fieldnames(comparison_rows, ("left_route_id", "right_route_id", "metric", "classification")))
    write_csv(route / "results/route-status-summary.csv", status_rows, _fieldnames(status_rows, ("route_id", "status")))
    write_json(route / "route-manifest.json", {
        "route_id": report.route_id,
        "revision": report.revision,
        "generated_date": report.generated_date.isoformat(),
        "source_route_ids": sorted(bundle.route_id for bundle in bundles),
        "source_campaign_ids": sorted(bundle.campaign_id for bundle in bundles),
        "attempt_count": sum(len(bundle.attempts) for bundle in bundles),
        "comparability_decision_count": len(comparison_rows),
        "universal_ranking_permitted": False,
    })
    cross_route_validation = build_cross_route_validation(report, catalogs, bundles)
    write_json(route / "validation/cross-route-validation.json", cross_route_validation)
    _write_text(route / "README.md", "# Guarded cross-route comparison\n\nThis route preserves complete status accounting and publishes protocol-gated comparisons without a universal repository score.")
    _write_text(route / "protocol/comparability-policy.md", "# Comparability policy\n\nThroughput requires compatible model, input length, output length, backend class, repetition treatment, and metric definition. Quality additionally requires identical prompt set, prompt-suite identity, rubric, scoring version, denominator, and aggregation. Missing required metadata is not comparable; incompatible complete methods are descriptive only.")
    _write_text(route / "reproduction/README.md", "# Reproduction\n\nRegeneration normalizes the existing five RouteBundle objects; it does not rerun inference.")
    _write_text(route / "reproduction/commands.md", "# Commands\n\nUse `write_cross_route_package` to render Markdown/DOCX and catalogs, export only the owned DOCX through `scripts/testing/cli/export_report.ps1`, then use `finalize_cross_route_package`.\n")
    _write_text(route / "evidence/claim-evidence-map.csv", "claim_id,claim_boundary\nCROSS-BOUNDARY-001,No universal ranking; source evidence remains in five route packages\n")
    markdown = route / "workbook/source/cross-route-comparison-final-report.md"
    docx = route / "workbook/generated/cross-route-comparison-final-report.docx"
    render_markdown(report, markdown)
    render_docx(report, docx)
    parity = compare_markdown_docx(markdown, docx)
    write_json(route / "validation/workbook-parity.json", parity)
    write_json(route / "validation/visual-validation.json", {"valid": False, "status": "Pending owned Word PDF export and automated rendering checks"})
    write_json(route / "validation/manual-visual-qa.json", {"valid": False, "status": "Pending manual full-page visual QA"})
    write_json(route / "validation/integrity-validation.json", {"valid": False, "status": "Pending final PDF and manifest regeneration"})
    _write_text(
        route / "validation/validation-report.md",
        "# Validation report\n\nDerived cross-route validation and Markdown/DOCX semantic parity passed. Automated PDF rendering and structural checks are pending owned Word export. Manual full-page visual QA is recorded separately when performed.",
    )
    if not parity["matches"] or not cross_route_validation["valid"]:
        raise ValueError("cross-route data/report validation failed")
    manifest = regenerate_route_manifest(root, route)
    errors = validate_sha256_manifest(root, manifest)
    if errors:
        raise ValueError(f"cross-route manifest validation failed: {errors}")
    return report


def finalize_cross_route_package(repo_root: Path, *, output_root: Path | None = None) -> dict[str, object]:
    """Validate PDF structure and automated all-page rendering, then reseal."""
    import warnings
    with warnings.catch_warnings():
        warnings.filterwarnings("ignore", message="builtin type .* has no __module__ attribute", category=DeprecationWarning)
        import fitz
    from pypdf import PdfReader

    root = Path(repo_root).resolve(strict=True)
    output = (Path(output_root) if output_root else root / "docs/testing/final-results").resolve()
    route = output / "06-cross-route-comparison"
    pdf = route / "workbook/generated/cross-route-comparison-final-report.pdf"
    if not pdf.is_file() or not pdf.read_bytes().startswith(b"%PDF-"):
        raise ValueError("Word-exported cross-route PDF is missing or malformed")
    reader = PdfReader(pdf)
    page_text = [(page.extract_text() or "").strip() for page in reader.pages]
    combined = "\n".join(page_text)
    rendered_pages: list[int] = []
    rendered_dimensions_positive = True
    rendered_pages_have_nonwhite_pixels = True
    with fitz.open(pdf) as rendered_document:
        for page_number, page in enumerate(rendered_document, 1):
            pixmap = page.get_pixmap(matrix=fitz.Matrix(1.0, 1.0), alpha=False)
            rendered_pages.append(page_number)
            rendered_dimensions_positive = rendered_dimensions_positive and pixmap.width > 0 and pixmap.height > 0
            rendered_pages_have_nonwhite_pixels = rendered_pages_have_nonwhite_pixels and any(
                value < 250 for value in pixmap.samples
            )
    checks = {
        "pdf_signature": True,
        "page_count": len(page_text),
        "all_pages_nonblank": bool(page_text) and all(page_text),
        "all_sections_present": all(heading in combined for heading in SECTION_ORDER),
        "ranking_boundary_present": "No universal best repository" in combined,
        "quality_boundary_present": "Incompatible quality scores are not ranked" in combined,
        "every_page_rendered": rendered_pages == list(range(1, len(page_text) + 1)),
        "rendered_dimensions_positive": rendered_dimensions_positive,
        "rendered_pages_have_nonwhite_pixels": rendered_pages_have_nonwhite_pixels,
    }
    valid = len(page_text) >= 4 and all(value for key, value in checks.items() if key != "page_count")
    receipt = {
        "valid": valid,
        "checks": checks,
        "automated_rendering": {
            "engine": "PyMuPDF",
            "rendered_page_count": len(rendered_pages),
            "rendered_pages": rendered_pages,
            "temporary_page_images_committed": False,
        },
        "manual_visual_qa_performed": False,
        "claim_boundary": "Automated checks do not establish absence of clipping, overlap, or other visual-layout defects.",
    }
    if not valid:
        raise ValueError(f"cross-route PDF structural validation failed: {checks}")
    write_json(route / "validation/visual-validation.json", receipt)
    from .evidence import hash_file
    write_json(route / "validation/integrity-validation.json", {
        "valid": True,
        "pdf_sha256": hash_file(pdf),
        "pdf_size_bytes": pdf.stat().st_size,
    })
    manual_path = route / "validation/manual-visual-qa.json"
    manual = json.loads(manual_path.read_text(encoding="utf-8")) if manual_path.is_file() else {"valid": False}
    _write_text(
        route / "validation/validation-report.md",
        "# Validation report\n\nDerived cross-route data/report validation and Markdown/DOCX semantic parity: Passed.\n\nAutomated PDF rendering and structural checks: Passed.\n\nManual full-page visual QA: "
        + ("Passed." if manual.get("valid") else "Pending."),
    )
    manifest = regenerate_route_manifest(root, route)
    errors = validate_sha256_manifest(root, manifest)
    if errors:
        raise ValueError(f"cross-route final manifest validation failed: {errors}")
    return receipt


def record_cross_route_manual_visual_qa(
    repo_root: Path,
    inspected_pages: Sequence[int],
    findings: str,
    *,
    output_root: Path | None = None,
) -> dict[str, object]:
    """Record separately performed human review only for an exact all-page set."""
    from pypdf import PdfReader

    root = Path(repo_root).resolve(strict=True)
    output = (Path(output_root) if output_root else root / "docs/testing/final-results").resolve()
    route = output / "06-cross-route-comparison"
    pdf = route / "workbook/generated/cross-route-comparison-final-report.pdf"
    if not pdf.is_file():
        raise ValueError("cross-route PDF is missing")
    page_count = len(PdfReader(pdf).pages)
    pages = tuple(inspected_pages)
    if pages != tuple(range(1, page_count + 1)):
        raise ValueError("manual visual QA must cover all PDF pages exactly once in page order")
    if not findings.strip():
        raise ValueError("manual visual QA requires findings")
    receipt = {
        "valid": True,
        "inspection_method": "Manual review of full-page PyMuPDF renders",
        "inspected_pages": list(pages),
        "page_count": page_count,
        "findings": findings.strip(),
        "temporary_page_images_committed": False,
    }
    write_json(route / "validation/manual-visual-qa.json", receipt)
    automated_path = route / "validation/visual-validation.json"
    automated = json.loads(automated_path.read_text(encoding="utf-8")) if automated_path.is_file() else {"valid": False}
    _write_text(
        route / "validation/validation-report.md",
        "# Validation report\n\nDerived cross-route data/report validation and Markdown/DOCX semantic parity: Passed.\n\nAutomated PDF rendering and structural checks: "
        + ("Passed." if automated.get("valid") else "Pending.")
        + "\n\nManual full-page visual QA: Passed.",
    )
    manifest = regenerate_route_manifest(root, route)
    errors = validate_sha256_manifest(root, manifest)
    if errors:
        raise ValueError(f"cross-route manual-QA manifest validation failed: {errors}")
    return receipt


__all__ = [
    "Comparability",
    "classify_comparability",
    "build_catalogs",
    "build_cross_route_report",
    "build_cross_route_validation",
    "write_cross_route_package",
    "finalize_cross_route_package",
    "record_cross_route_manual_visual_qa",
]
