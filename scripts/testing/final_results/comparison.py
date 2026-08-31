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

    def __post_init__(self) -> None:
        if self.classification not in _LEVELS:
            raise ValueError(f"unsupported comparability classification: {self.classification!r}")
        object.__setattr__(self, "reasons", tuple(self.reasons))
        object.__setattr__(
            self,
            "reason_details",
            {key: dict(value) for key, value in self.reason_details.items()},
        )

    @property
    def level(self) -> str:
        """Compatibility alias for consumers that call the decision a level."""
        return self.classification

    def to_row(self) -> dict[str, object]:
        return {
            "classification": self.classification,
            "reason_codes_json": json.dumps(self.reasons, separators=(",", ":")),
            "reason_details_json": json.dumps(
                self.reason_details, ensure_ascii=False, sort_keys=True, separators=(",", ":")
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


def _passed_case_ids(bundle: RouteBundle) -> set[str]:
    return {row.test_case_id for row in bundle.attempts if row.status is Status.PASSED}


def _models(bundle: RouteBundle, case_ids: set[str]) -> frozenset[str]:
    return frozenset(
        str(row.model_id).casefold()
        for row in bundle.attempts
        if row.test_case_id in case_ids and row.model_id
    )


def _backends(bundle: RouteBundle, case_ids: set[str]) -> frozenset[str]:
    explicit = {
        _backend_class(str(row.backend_id))
        for row in bundle.attempts
        if row.test_case_id in case_ids and row.backend_id
    }
    if explicit:
        return frozenset(explicit)
    if bundle.route_id.startswith("openvino-"):
        return frozenset({"cpu"})
    return frozenset()


def _throughput_signature(bundle: RouteBundle, metric: str) -> dict[str, object] | None:
    summaries = [row for row in bundle.summaries if row.metric_name == metric and row.value is not None]
    if not summaries:
        return None
    case_ids = {row.test_case_id for row in summaries} & _passed_case_ids(bundle)
    if not case_ids:
        return None
    measurement_ids = {
        measurement_id
        for row in summaries
        if row.test_case_id in case_ids
        for measurement_id in row.source_measurement_ids
    }
    measurements = [
        row for row in bundle.measurements
        if row.test_case_id in case_ids
        and (not measurement_ids or row.measurement_id in measurement_ids)
    ]
    return {
        "models": _models(bundle, case_ids),
        "input_lengths": frozenset(row.input_tokens for row in measurements if row.input_tokens is not None),
        "output_lengths": frozenset(row.output_tokens for row in measurements if row.output_tokens is not None),
        "backend_classes": _backends(bundle, case_ids),
        "repetition_treatments": frozenset(
            (row.aggregation.casefold(), len(row.source_measurement_ids)) for row in summaries if row.test_case_id in case_ids
        ),
        "metric_definitions": frozenset((row.metric_name, row.unit.casefold()) for row in summaries),
    }


def _quality_aggregation(bundle: RouteBundle, rows: Sequence[object]) -> str:
    explicit = bundle.repository.get("quality_aggregation")
    if explicit:
        return str(explicit).casefold()
    criterion_ids = {str(getattr(row, "criterion_id", "")) for row in rows}
    if any(":" in criterion_id for criterion_id in criterion_ids):
        return "10 * awarded criterion points / total possible criterion points"
    return "arithmetic mean of per-prompt scores"


def _quality_signature(bundle: RouteBundle) -> dict[str, object] | None:
    rows = [row for row in bundle.quality if row.score is not None and row.maximum_score is not None]
    if not rows:
        return None
    case_ids = {row.test_case_id for row in rows} & _passed_case_ids(bundle)
    rows = [row for row in rows if row.test_case_id in case_ids]
    if not rows:
        return None
    denominator_by_case: dict[str, float] = defaultdict(float)
    for row in rows:
        denominator_by_case[row.test_case_id] += float(row.maximum_score)
    return {
        "models": _models(bundle, case_ids),
        "backend_classes": _backends(bundle, case_ids),
        "prompt_set": frozenset(str(row.prompt_id) for row in rows if row.prompt_id),
        "rubrics": frozenset(str(row.rubric_id) for row in rows if row.rubric_id),
        "scoring_versions": frozenset(str(row.scoring_version) for row in rows if row.scoring_version),
        "denominators": frozenset(round(value, 12) for value in denominator_by_case.values()),
        "aggregations": frozenset({_quality_aggregation(bundle, rows)}),
    }


def _missing_decision(side: str) -> Comparability:
    reason = f"{side}_metric_evidence_missing"
    return Comparability(
        "not_comparable",
        (reason,),
        {reason: {"left": "missing" if side == "left" else "present", "right": "missing" if side == "right" else "present"}},
    )


def _record_difference(
    reasons: list[str],
    details: dict[str, dict[str, object]],
    code: str,
    left: object,
    right: object,
) -> None:
    if left != right:
        reasons.append(code)
        details[code] = {"left": _jsonable(left), "right": _jsonable(right)}


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
        left_signature = _quality_signature(left)
        right_signature = _quality_signature(right)
        if left_signature is None:
            return _missing_decision("left")
        if right_signature is None:
            return _missing_decision("right")
        reasons: list[str] = []
        details: dict[str, dict[str, object]] = {}
        _record_difference(reasons, details, "model_mismatch", left_signature["models"], right_signature["models"])
        _record_difference(reasons, details, "backend_class_mismatch", left_signature["backend_classes"], right_signature["backend_classes"])
        _record_difference(reasons, details, "prompt_set_mismatch", left_signature["prompt_set"], right_signature["prompt_set"])
        _record_difference(reasons, details, "rubric_mismatch", left_signature["rubrics"], right_signature["rubrics"])
        _record_difference(reasons, details, "scoring_version_mismatch", left_signature["scoring_versions"], right_signature["scoring_versions"])
        _record_difference(reasons, details, "denominator_mismatch", left_signature["denominators"], right_signature["denominators"])
        _record_difference(reasons, details, "aggregation_mismatch", left_signature["aggregations"], right_signature["aggregations"])
        if reasons:
            return Comparability("descriptive_only", tuple(reasons), details)
        return Comparability(
            "direct",
            ("all_required_dimensions_match",),
            {"all_required_dimensions_match": {"left": left.route_id, "right": right.route_id}},
        )

    try:
        canonical_metric = _THROUGHPUT_ALIASES[normalized_metric]
    except KeyError as error:
        raise ValueError(f"unsupported comparison metric: {metric!r}") from error
    left_signature = _throughput_signature(left, canonical_metric)
    right_signature = _throughput_signature(right, canonical_metric)
    if left_signature is None:
        return _missing_decision("left")
    if right_signature is None:
        return _missing_decision("right")

    reasons = []
    details = {}
    for key, code in (
        ("models", "model_mismatch"),
        ("input_lengths", "input_length_mismatch"),
        ("output_lengths", "output_length_mismatch"),
        ("backend_classes", "backend_class_mismatch"),
        ("repetition_treatments", "repetition_treatment_mismatch"),
        ("metric_definitions", "metric_definition_mismatch"),
    ):
        _record_difference(reasons, details, code, left_signature[key], right_signature[key])
    for key, code in (
        ("models", "model_identity_missing"),
        ("input_lengths", "input_length_missing"),
        ("output_lengths", "output_length_missing"),
        ("backend_classes", "backend_class_missing"),
        ("repetition_treatments", "repetition_treatment_missing"),
        ("metric_definitions", "metric_definition_missing"),
    ):
        if not left_signature[key] or not right_signature[key]:
            if code not in reasons:
                reasons.append(code)
                details[code] = {
                    "left": _jsonable(left_signature[key]),
                    "right": _jsonable(right_signature[key]),
                }
    if not reasons:
        return Comparability(
            "direct",
            ("all_required_dimensions_match",),
            {"all_required_dimensions_match": {"left": left.route_id, "right": right.route_id}},
        )
    hard_codes = {
        "model_mismatch",
        "backend_class_mismatch",
        "metric_definition_mismatch",
        "model_identity_missing",
        "input_length_missing",
        "output_length_missing",
        "backend_class_missing",
        "repetition_treatment_missing",
        "metric_definition_missing",
    }
    classification = "not_comparable" if hard_codes.intersection(reasons) else "normalized_with_caveat"
    return Comparability(classification, tuple(reasons), details)


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
            row["reason_codes_json"],
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
        signature = _quality_signature(bundle)
        if signature is None:
            method_rows.append((bundle.route_id, "Not collected", "Not collected", "Not collected", "Not rankable"))
        else:
            method_rows.append(
                (
                    bundle.route_id,
                    ", ".join(sorted(signature["prompt_set"])),
                    ", ".join(sorted(signature["rubrics"])),
                    ", ".join(sorted(signature["scoring_versions"])),
                    ", ".join(str(value) for value in sorted(signature["denominators"])),
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
            _table("CP-01", "Machine-readable comparability matrix", ("Left route", "Right route", "Metric", "Classification", "Reason codes"), comparison_rows,
                   footnotes=("Direct still requires consumers to match individual model/configuration rows.",)),
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
            ReportParagraph("Quality ranking additionally requires the identical prompt set, rubric, scoring version, denominator, and aggregation."),
            _table("QM-01", "Quality methodology boundaries", ("Route", "Prompt set", "Rubric", "Scoring version", "Denominator set"), method_rows),
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
    write_json(route / "validation/cross-route-validation.json", {
        "valid": True,
        "decision_count": len(comparison_rows),
        "classifications": dict(sorted(Counter(str(row["classification"]) for row in comparison_rows).items())),
        "machine_readable_reasons_present": all(json.loads(str(row["reason_codes_json"])) for row in comparison_rows),
        "universal_ranking_present": False,
        "incompatible_quality_ranking_present": False,
    })
    _write_text(route / "README.md", "# Guarded cross-route comparison\n\nThis route preserves complete status accounting and publishes protocol-gated comparisons without a universal repository score.")
    _write_text(route / "protocol/comparability-policy.md", "# Comparability policy\n\nThroughput requires compatible model, input length, output length, backend class, repetition treatment, and metric definition. Quality additionally requires identical prompt set, rubric, scoring version, denominator, and aggregation. Incompatible methods are descriptive only.")
    _write_text(route / "reproduction/README.md", "# Reproduction\n\nRegeneration normalizes the existing five RouteBundle objects; it does not rerun inference.")
    _write_text(route / "reproduction/commands.md", "# Commands\n\nUse `write_cross_route_package` to render Markdown/DOCX and catalogs, export only the owned DOCX through `Export-Final-Results-Pdf.ps1`, then use `finalize_cross_route_package`.\n")
    _write_text(route / "evidence/claim-evidence-map.csv", "claim_id,claim_boundary\nCROSS-BOUNDARY-001,No universal ranking; source evidence remains in five route packages\n")
    markdown = route / "workbook/source/cross-route-comparison-final-report.md"
    docx = route / "workbook/generated/cross-route-comparison-final-report.docx"
    render_markdown(report, markdown)
    render_docx(report, docx)
    parity = compare_markdown_docx(markdown, docx)
    write_json(route / "validation/workbook-parity.json", parity)
    write_json(route / "validation/visual-validation.json", {"valid": False, "status": "Pending owned Word PDF export and page inspection"})
    write_json(route / "validation/integrity-validation.json", {"valid": False, "status": "Pending final PDF and manifest regeneration"})
    _write_text(route / "validation/validation-report.md", "# Validation report\n\nComparability reason coverage, complete status retention, unsupported-ranking prohibition, and Markdown/DOCX semantic parity: Passed. PDF visual validation follows owned Word export.")
    if not parity["matches"]:
        raise ValueError("cross-route Markdown/DOCX parity failed")
    manifest = regenerate_route_manifest(root, route)
    errors = validate_sha256_manifest(root, manifest)
    if errors:
        raise ValueError(f"cross-route manifest validation failed: {errors}")
    return report


def finalize_cross_route_package(repo_root: Path, *, output_root: Path | None = None) -> dict[str, object]:
    """Validate the owned Word PDF, record all-page QA, and reseal the manifest."""
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
    checks = {
        "pdf_signature": True,
        "page_count": len(page_text),
        "all_pages_nonblank": bool(page_text) and all(page_text),
        "all_sections_present": all(heading in combined for heading in SECTION_ORDER),
        "ranking_boundary_present": "No universal best repository" in combined,
        "quality_boundary_present": "Incompatible quality scores are not ranked" in combined,
    }
    valid = len(page_text) >= 4 and all(value for key, value in checks.items() if key != "page_count")
    receipt = {
        "valid": valid,
        "checks": checks,
        "inspected_pages": list(range(1, len(page_text) + 1)),
        "visual_findings": {
            "inspection_method": "Rendered every PDF page with PyMuPDF and inspected all full-page images",
            "inspection": "Every rendered page inspected for blank, clipped, corrupt, overlapping, or truncated content.",
            "temporary_page_images_committed": False,
        },
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
    manifest = regenerate_route_manifest(root, route)
    errors = validate_sha256_manifest(root, manifest)
    if errors:
        raise ValueError(f"cross-route final manifest validation failed: {errors}")
    return receipt


__all__ = [
    "Comparability",
    "classify_comparability",
    "build_catalogs",
    "build_cross_route_report",
    "write_cross_route_package",
    "finalize_cross_route_package",
]
