"""Evidence-bound master reports for normalized OpenVINO campaigns."""

from __future__ import annotations

import json
import os
from collections import Counter, defaultdict
from datetime import date
from pathlib import Path
from typing import Iterable

from .evidence import validate_sha256_manifest, write_sha256_manifest
from .models import QualityRecord, RouteBundle, Status
from .report_model import (
    Report,
    ReportNote,
    ReportParagraph,
    ReportSection,
    ReportTable,
)


SECTION_ORDER = (
    "1. Title and document control",
    "2. Technical summary",
    "3. Key findings and decision-relevant evidence",
    "4. Repository, branch, commit, build, hardware, and software identity",
    "5. Objectives, scope, test matrix, and execution sequence",
    "6. Model, weight, cache-format, and backend availability",
    "7. Complete attempt accounting",
    "8. Performance results and repetition detail",
    "9. Quality methodology and results",
    "10. Device/backend use and fallback verification",
    "11. Failures, blocks, deviations, and recovery attempts",
    "12. Limitations, uncertainty, robustness checks, and claim boundaries",
    "13. Reproduction guidance",
    "14. Evidence index and hashes",
    "15. Revision history",
)

_ROUTE_TITLES = {
    "openvino-experimental-fork": "Experimental OpenVINO fork final results",
    "openvino-official-upstream": "Official upstream OpenVINO final results",
}

_STATUS_ORDER = (
    Status.PASSED,
    Status.FAILED,
    Status.BLOCKED,
    Status.ARTIFACT_UNAVAILABLE,
    Status.NOT_EXECUTED,
    Status.NOT_APPLICABLE,
    Status.NOT_COLLECTED,
)

_KEY_EVIDENCE_ROLES = {
    "source-results",
    "source-coverage",
    "source-ledger",
    "source-quality",
    "source-workbook",
    "source-validation",
    "source-preflight",
}

_CRITERION_DESCRIPTIONS = {
    "factuality:primary": "Primary factual correctness check",
    "numerical_accuracy:primary": "Primary arithmetic and statistical correctness check",
    "safety:primary": "Primary healthcare or safety constraint check",
    "fact_retention:secondary": "Retention of supplied facts and constraints",
    "instruction_following:supporting": "Requested structure and instruction compliance",
}


def _prompt_metadata(prompt_id: str, prompt_suite_id: str) -> tuple[str, str]:
    """Return frozen, verified suite metadata for one normalized prompt identity."""
    if prompt_suite_id != "OPENVINO-SECTOR-EXPERIENCE-QUALITY-v3":
        raise ValueError(f"unsupported OpenVINO prompt suite: {prompt_suite_id!r}")
    if len(prompt_id) != 3 or not prompt_id.startswith("Q") or not prompt_id[1:].isdigit():
        raise ValueError(f"unsupported OpenVINO prompt identity: {prompt_id!r}")
    number = int(prompt_id[1:])
    if 1 <= number <= 16:
        length = "short"
        boundaries = ((6, "healthcare"), (11, "education"), (14, "statistics"), (16, "general"))
    elif 17 <= number <= 32:
        length = "medium"
        boundaries = ((21, "healthcare"), (27, "education"), (29, "statistics"), (32, "general"))
    elif 33 <= number <= 48:
        length = "long"
        boundaries = ((37, "healthcare"), (42, "education"), (45, "statistics"), (48, "general"))
    else:
        raise ValueError(f"prompt is outside the verified suite: {prompt_id!r}")
    domain = next(label for upper, label in boundaries if number <= upper)
    return domain, length


def _table(
    table_id: str,
    title: str,
    columns: tuple[str, ...],
    rows: Iterable[Iterable[object]],
    *,
    subtitle: str = "",
    footnotes: tuple[str, ...] = (),
) -> ReportTable:
    return ReportTable(
        table_id=table_id,
        title=title,
        columns=columns,
        rows=tuple(tuple(str(cell) for cell in row) for row in rows),
        subtitle=subtitle,
        footnotes=footnotes,
    )


def _json_value(value: object) -> str:
    if isinstance(value, (dict, list, tuple)):
        return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    if value is None:
        return "Not collected"
    if isinstance(value, bool):
        return "true" if value else "false"
    return str(value)


def _status_counts(bundle: RouteBundle) -> Counter[Status]:
    return Counter(attempt.status for attempt in bundle.attempts)


def _status_rows(bundle: RouteBundle) -> tuple[tuple[str, int], ...]:
    counts = _status_counts(bundle)
    return tuple((status.display_label, counts[status]) for status in _STATUS_ORDER if counts[status])


def _summary_by_case(bundle: RouteBundle) -> dict[str, dict[str, object]]:
    result: dict[str, dict[str, object]] = defaultdict(dict)
    for summary in bundle.summaries:
        if summary.metric_name in result[summary.test_case_id]:
            raise ValueError(
                f"duplicate summary metric for {summary.test_case_id}: {summary.metric_name}"
            )
        result[summary.test_case_id][summary.metric_name] = summary.value
    return result


def _quality_by_case(bundle: RouteBundle) -> dict[str, tuple[float, int, int, int]]:
    grouped: dict[str, list[QualityRecord]] = defaultdict(list)
    for record in bundle.quality:
        grouped[record.test_case_id].append(record)
    result: dict[str, tuple[float, int, int, int]] = {}
    for case_id, records in grouped.items():
        if any(record.score is None or record.maximum_score is None for record in records):
            raise ValueError(f"quality records for {case_id} contain an unscored criterion")
        awarded = sum(float(record.score) for record in records)
        maximum = sum(float(record.maximum_score) for record in records)
        if maximum <= 0:
            raise ValueError(f"quality records for {case_id} have no positive denominator")
        prompts = {record.prompt_id for record in records if record.prompt_id is not None}
        result[case_id] = (10.0 * awarded / maximum, len(prompts), len(records), int(maximum))
    return result


def _attempt_lookup(bundle: RouteBundle) -> dict[str, object]:
    result = {attempt.test_case_id: attempt for attempt in bundle.attempts}
    if len(result) != len(bundle.attempts):
        raise ValueError("attempt test-case identities are not unique")
    return result


def _identity_rows(bundle: RouteBundle) -> tuple[tuple[str, str], ...]:
    rows: list[tuple[str, str]] = []
    for group_name, values in (
        ("Repository", bundle.repository),
        ("Hardware", bundle.hardware),
        ("Software", bundle.software),
    ):
        for key, value in sorted(values.items()):
            rows.append((f"{group_name}: {key}", _json_value(value)))
    return tuple(rows)


def _headline_rows(bundle: RouteBundle) -> tuple[tuple[str, str, str], ...]:
    summaries = _summary_by_case(bundle)
    quality = _quality_by_case(bundle)
    rows: list[tuple[str, str, str]] = []
    metric_specs = (
        ("generation_tokens_per_second", max, "Highest observed median decode throughput", "tokens/s", ".6f"),
        ("time_to_first_token", min, "Lowest observed selected-repetition TTFT", "ms", ".3f"),
        ("peak_working_set_bytes", min, "Lowest observed worst-repetition peak working set", "bytes", ".0f"),
    )
    for metric, selector, label, unit, formatting in metric_specs:
        values = [
            (case_id, float(case_summaries[metric]))
            for case_id, case_summaries in summaries.items()
            if case_summaries.get(metric) is not None
        ]
        if values:
            case_id, value = selector(values, key=lambda item: item[1])
            rows.append((label, format(value, formatting), f"{unit}; {case_id}"))
    if quality:
        case_id, (score, prompt_count, _, _) = max(quality.items(), key=lambda item: item[1][0])
        rows.append(
            (
                "Highest observed objective quality score",
                f"{score:.6f}",
                f"/10 across {prompt_count} prompts; {case_id}",
            )
        )
    return tuple(rows)


def _availability_rows(bundle: RouteBundle) -> tuple[tuple[object, ...], ...]:
    grouped: dict[tuple[str, str], Counter[Status]] = defaultdict(Counter)
    for attempt in bundle.attempts:
        grouped[(attempt.model_id or "Not recorded", attempt.weight_format_id or "Not recorded")][
            attempt.status
        ] += 1
    return tuple(
        (
            model,
            weight,
            sum(counts.values()),
            counts[Status.PASSED],
            counts[Status.FAILED],
            counts[Status.BLOCKED],
            counts[Status.ARTIFACT_UNAVAILABLE],
        )
        for (model, weight), counts in sorted(grouped.items())
    )


def _cache_rows(bundle: RouteBundle) -> tuple[tuple[object, ...], ...]:
    grouped: dict[str, Counter[Status]] = defaultdict(Counter)
    for attempt in bundle.attempts:
        grouped[attempt.cache_format_id or "Not recorded"][attempt.status] += 1
    return tuple(
        (
            cache,
            sum(counts.values()),
            counts[Status.PASSED],
            counts[Status.FAILED],
            counts[Status.BLOCKED],
            counts[Status.ARTIFACT_UNAVAILABLE],
        )
        for cache, counts in sorted(grouped.items())
    )


def _attempt_rows(bundle: RouteBundle) -> tuple[tuple[object, ...], ...]:
    return tuple(
        (
            attempt.test_case_id,
            attempt.model_id or "Not recorded",
            attempt.weight_format_id or "Not recorded",
            attempt.cache_format_id or "Not recorded",
            "true" if attempt.executed else "false",
            attempt.status.display_label,
            attempt.reason or "None recorded",
            attempt.source_status or "Not recorded",
            ", ".join(attempt.evidence_ids) or "Not recorded",
        )
        for attempt in sorted(bundle.attempts, key=lambda item: item.test_case_id)
    )


def _performance_rows(bundle: RouteBundle) -> tuple[tuple[object, ...], ...]:
    attempts = _attempt_lookup(bundle)
    summaries = _summary_by_case(bundle)
    required = {
        "generation_tokens_per_second",
        "time_to_first_token",
        "peak_working_set_bytes",
    }
    rows: list[tuple[object, ...]] = []
    for case_id, values in sorted(summaries.items()):
        if set(values) != required:
            raise ValueError(f"{case_id}: incomplete or unexpected summary metrics")
        attempt = attempts[case_id]
        peak_bytes = int(values["peak_working_set_bytes"])
        rows.append(
            (
                case_id,
                attempt.model_id,
                attempt.weight_format_id,
                attempt.cache_format_id,
                f"{float(values['generation_tokens_per_second']):.6f}",
                f"{float(values['time_to_first_token']):.3f}",
                str(peak_bytes),
                f"{peak_bytes / (1024 ** 3):.3f}",
            )
        )
    return tuple(rows)


def _repetition_rows(bundle: RouteBundle) -> tuple[tuple[object, ...], ...]:
    return tuple(
        (
            item.test_case_id,
            item.repetition_id or "Not recorded",
            item.measurement_id,
            item.source_evidence_id or "Not recorded",
            "Not collected" if item.generation_tokens_per_second is None else f"{item.generation_tokens_per_second:.6f}",
            "Not collected" if item.latency_ms is None else f"{item.latency_ms:.3f}",
            "Not collected" if item.peak_working_set_bytes is None else str(item.peak_working_set_bytes),
            "Not collected" if item.input_tokens is None else str(item.input_tokens),
            "Not collected" if item.output_tokens is None else str(item.output_tokens),
        )
        for item in sorted(bundle.measurements, key=lambda record: record.measurement_id)
    )


def _quality_metadata(bundle: RouteBundle) -> tuple[str, str, str, set[str]]:
    suites = {record.prompt_suite_id for record in bundle.quality if record.prompt_suite_id}
    rubrics = {record.rubric_id for record in bundle.quality if record.rubric_id}
    versions = {record.scoring_version for record in bundle.quality if record.scoring_version}
    prompts = {record.prompt_id for record in bundle.quality if record.prompt_id}
    if len(suites) != 1 or len(rubrics) != 1 or len(versions) != 1 or not prompts:
        raise ValueError("OpenVINO quality metadata must identify one suite, rubric, and version")
    return next(iter(suites)), next(iter(rubrics)), next(iter(versions)), prompts


def _criterion_rows(bundle: RouteBundle, prompt_count: int) -> tuple[tuple[object, ...], ...]:
    grouped: dict[str, list[QualityRecord]] = defaultdict(list)
    for record in bundle.quality:
        if record.criterion_id is None:
            raise ValueError("quality record is missing criterion identity")
        grouped[record.criterion_id].append(record)
    rows = []
    for criterion, records in sorted(grouped.items()):
        maxima = {record.maximum_score for record in records}
        if len(maxima) != 1 or None in maxima:
            raise ValueError(f"criterion {criterion} has inconsistent maximum scores")
        maximum = float(next(iter(maxima)))
        prompts_for_criterion = {record.prompt_id for record in records}
        rows.append(
            (
                criterion,
                f"{maximum:g}",
                len(prompts_for_criterion),
                f"{maximum / prompt_count:.6f}",
                _CRITERION_DESCRIPTIONS.get(criterion, "Objective criterion recorded by the normalized rubric"),
            )
        )
    return tuple(rows)


def _quality_rows(bundle: RouteBundle) -> tuple[tuple[object, ...], ...]:
    attempts = _attempt_lookup(bundle)
    values = _quality_by_case(bundle)
    return tuple(
        (
            case_id,
            attempts[case_id].model_id,
            attempts[case_id].weight_format_id,
            attempts[case_id].cache_format_id,
            f"{score:.6f}",
            prompt_count,
            criterion_count,
            maximum,
        )
        for case_id, (score, prompt_count, criterion_count, maximum) in sorted(values.items())
    )


def _failure_rows(bundle: RouteBundle) -> tuple[tuple[object, ...], ...]:
    return tuple(
        (
            item.failure_id,
            item.test_case_id,
            item.status.display_label,
            item.stage,
            item.source_status or "Not recorded",
            item.reason,
            ", ".join(item.evidence_ids) or "Not recorded",
        )
        for item in sorted(bundle.failures, key=lambda record: record.failure_id)
    )


def _device_rows(bundle: RouteBundle) -> tuple[tuple[object, ...], ...]:
    properties = bundle.software.get("runtime_properties", [])
    if not isinstance(properties, list):
        raise ValueError("software.runtime_properties must be a list")
    return tuple(
        (index, _json_value(value), "Verified informational metadata")
        for index, value in enumerate(properties, start=1)
    )


def _evidence_rows(bundle: RouteBundle) -> tuple[tuple[object, ...], ...]:
    return tuple(
        (
            item.evidence_id,
            item.role,
            item.source_label or "Not recorded",
            item.sha256,
            item.size_bytes,
            item.relative_path,
            "Derived" if item.derived else "Source",
            ", ".join(item.input_evidence_ids) or "None",
        )
        for item in sorted(bundle.evidence, key=lambda record: record.evidence_id)
    )


def build_openvino_report(bundle: RouteBundle) -> Report:
    """Construct an OpenVINO master report solely from one normalized bundle."""
    try:
        title = _ROUTE_TITLES[bundle.route_id]
    except KeyError as error:
        raise ValueError(f"unsupported OpenVINO route: {bundle.route_id!r}") from error
    source_date = date.fromisoformat(str(bundle.repository["source_date"]))
    counts = _status_counts(bundle)
    suite, rubric, scoring_version, prompts = _quality_metadata(bundle)
    prompt_metadata = {prompt: _prompt_metadata(prompt, suite) for prompt in prompts}
    domains = Counter(domain for domain, _ in prompt_metadata.values())
    lengths = Counter(length for _, length in prompt_metadata.values())
    repetitions_per_case = Counter(item.test_case_id for item in bundle.measurements)
    repetition_values = sorted(set(repetitions_per_case.values()))
    key_evidence_ids = tuple(
        item.evidence_id
        for item in sorted(bundle.evidence, key=lambda record: record.evidence_id)
        if item.role in _KEY_EVIDENCE_ROLES
    )

    status_sentence = ", ".join(
        f"{count} {status.display_label.casefold()}"
        for status, count in ((status, counts[status]) for status in _STATUS_ORDER)
        if count
    )
    total_attempts = len(bundle.attempts)
    passed_count = counts[Status.PASSED]
    summary_rules = sorted(
        {(record.metric_name, record.unit, record.aggregation) for record in bundle.summaries}
    )

    if bundle.route_id == "openvino-official-upstream":
        authority_note = (
            "Final statuses come from the normalized fv2 campaign; performance and quality "
            "observations are joined from fv1 only for fv2-passed cases. Hardware preflight "
            "blocked configurations are reported as Blocked, never as unavailable."
        )
        limitation_status = (
            f"The normalized attempt ledger contains {counts[Status.FAILED]} conversion-failed "
            f"attempts and {counts[Status.BLOCKED]} hardware-preflight blocks. Blocked cases have "
            "no fabricated runtime or quality observations."
        )
    else:
        authority_note = (
            "The fv6 normalized campaign is the sole status and measurement authority. Cases without "
            "validated model artifacts are reported as Artifact unavailable, not as execution failures."
        )
        limitation_status = (
            f"The normalized ledger contains {counts[Status.ARTIFACT_UNAVAILABLE]} model-artifact "
            "unavailable cases. They were not executed and have no fabricated measurements."
        )

    sections = (
        ReportSection(
            SECTION_ORDER[0],
            (
                _table(
                    "DC-01",
                    "Document identity and authority",
                    ("Field", "Value"),
                    (
                        ("Report title", title),
                        ("Route ID", bundle.route_id),
                        ("Campaign ID", bundle.campaign_id),
                        ("Source date", source_date.isoformat()),
                        ("Report revision", "R1"),
                        ("Canonical content", f"workbook/source/{bundle.route_id}-final-report.md"),
                    ),
                ),
                ReportParagraph(authority_note),
            ),
        ),
        ReportSection(
            SECTION_ORDER[1],
            (
                ReportParagraph(
                    f"Campaign {bundle.campaign_id} accounts for {total_attempts} intended configuration "
                    f"attempts: {status_sentence}. Performance and objective quality results are published "
                    f"only for the {passed_count} passed configurations."
                ),
                _table(
                    "AC-01",
                    "Attempt status summary",
                    ("Status", "Count"),
                    _status_rows(bundle),
                    footnotes=("Counts are derived from the complete normalized attempt ledger.",),
                ),
                ReportNote(
                    "A missing observation is never represented as zero. Status text is authoritative; colour is supplementary."
                ),
            ),
        ),
        ReportSection(
            SECTION_ORDER[2],
            (
                ReportParagraph(
                    "The extrema below are navigation aids into the complete result tables, not standalone rankings."
                ),
                _table(
                    "KF-01",
                    "Observed campaign extrema",
                    ("Finding", "Value", "Basis"),
                    _headline_rows(bundle),
                    footnotes=(
                        "Extrema describe this campaign only and do not establish causal superiority.",
                    ),
                ),
                ReportParagraph(
                    "Decision use should begin with availability and status, then compare performance and quality only within compatible model, weight, cache, backend, prompt, and aggregation conditions."
                ),
            ),
        ),
        ReportSection(
            SECTION_ORDER[3],
            (
                ReportParagraph(
                    "The following values are the portable repository, hardware, and software identities retained by the normalized campaign."
                ),
                _table(
                    "ID-01",
                    "Verified repository, hardware, and software metadata",
                    ("Identity field", "Verified value"),
                    _identity_rows(bundle),
                ),
                ReportNote(
                    "Not collected means the normalized bundle contains no portable evidence for that identity field; it is not inferred from the reviewing computer."
                ),
            ),
        ),
        ReportSection(
            SECTION_ORDER[4],
            (
                ReportParagraph(
                    f"The intended matrix crosses {len({a.model_id for a in bundle.attempts})} models, "
                    f"{len({a.weight_format_id for a in bundle.attempts})} weight formats, and "
                    f"{len({a.cache_format_id for a in bundle.attempts})} cache formats for "
                    f"{total_attempts} configuration cases."
                ),
                _table(
                    "MX-01",
                    "Intended matrix dimensions",
                    ("Dimension", "Members", "Count"),
                    (
                        ("Models", ", ".join(sorted({str(a.model_id) for a in bundle.attempts if a.model_id})), len({a.model_id for a in bundle.attempts})),
                        ("Weight formats", ", ".join(sorted({str(a.weight_format_id) for a in bundle.attempts if a.weight_format_id})), len({a.weight_format_id for a in bundle.attempts})),
                        ("Cache formats", ", ".join(sorted({str(a.cache_format_id) for a in bundle.attempts if a.cache_format_id})), len({a.cache_format_id for a in bundle.attempts})),
                    ),
                ),
                ReportParagraph(
                    "Execution sequence was availability/acquisition or conversion gating, three benchmark repetitions for each executable case, then the frozen sector-quality suite. Non-passed gates stopped downstream measurement."
                ),
            ),
        ),
        ReportSection(
            SECTION_ORDER[5],
            (
                ReportParagraph(
                    "Availability is accounted for before performance: each intended model, weight, and cache combination retains its terminal status."
                ),
                _table(
                    "AV-01",
                    "Model and weight availability by final status",
                    ("Model", "Weight", "Intended", "Passed", "Failed", "Blocked", "Artifact unavailable"),
                    _availability_rows(bundle),
                ),
                _table(
                    "AV-02",
                    "Cache-format coverage by final status",
                    ("Cache format", "Intended", "Passed", "Failed", "Blocked", "Artifact unavailable"),
                    _cache_rows(bundle),
                ),
                ReportParagraph(
                    "The normalized software metadata records SDPA attention-backend properties. No backend fallback is inferred where a separate fallback observation was not collected."
                ),
            ),
        ),
        ReportSection(
            SECTION_ORDER[6],
            (
                ReportParagraph(
                    "The audit ledger below lists every intended case exactly once, including cases that did not produce observations."
                ),
                _table(
                    "AT-01",
                    "Complete normalized attempt ledger",
                    ("Test case", "Model", "Weight", "Cache", "Executed", "Status", "Reason", "Source status", "Evidence IDs"),
                    _attempt_rows(bundle),
                    footnotes=(
                        "Every intended case appears once. Non-passed reasons are preserved from normalized source evidence.",
                    ),
                ),
            ),
        ),
        ReportSection(
            SECTION_ORDER[7],
            (
                ReportParagraph(
                    "Performance summaries are joined to their passed configurations and followed by the individual source repetitions."
                ),
                _table(
                    "PF-01",
                    "Passed-case performance summary",
                    ("Test case", "Model", "Weight", "Cache", "Decode tokens/s", "TTFT ms", "Peak bytes", "Peak GiB"),
                    _performance_rows(bundle),
                ),
                _table(
                    "AG-01",
                    "Metric aggregation definitions",
                    ("Metric", "Unit", "Aggregation"),
                    summary_rules,
                    footnotes=(
                        f"Observed benchmark repetitions per passed case: {', '.join(str(value) for value in repetition_values)}.",
                    ),
                ),
                _table(
                    "RP-01",
                    "Individual benchmark repetition detail",
                    ("Test case", "Repetition", "Measurement ID", "Evidence ID", "Decode tokens/s", "Latency/TTFT ms", "Peak bytes", "Input tokens", "Output tokens"),
                    _repetition_rows(bundle),
                ),
            ),
        ),
        ReportSection(
            SECTION_ORDER[8],
            (
                ReportParagraph(
                    f"Quality uses {len(prompts)} distinct prompts from {suite}: healthcare, education, statistics, and general tasks across short, medium, and long prompt lengths. "
                    f"The normalized objective rubric {rubric} ({scoring_version}) awards criterion checks with unequal importance. Each prompt has a primary 5-point check, a 3-point fact-retention check, and a 2-point instruction-following check. "
                    f"The reported /10 case score is the arithmetic mean of per-prompt totals, equivalently 10 multiplied by total awarded criterion points divided by total possible points. The output-health gate is part of the recorded rubric; this report does not add subjective re-judgment."
                ),
                _table(
                    "QC-01",
                    "Objective quality criteria, weights, and score increments",
                    ("Criterion", "Weight", "Distinct prompts", "Satisfied-check contribution to case /10", "Purpose"),
                    _criterion_rows(bundle, len(prompts)),
                    footnotes=(
                        "Contribution increments are derived from criterion weight divided by the number of prompts in the case score denominator.",
                    ),
                ),
                _table(
                    "QD-01",
                    "Prompt coverage by sector",
                    ("Sector", "Prompt count"),
                    ((key.title(), value) for key, value in sorted(domains.items())),
                ),
                _table(
                    "QL-01",
                    "Prompt coverage by length",
                    ("Prompt length", "Prompt count"),
                    ((key.title(), value) for key, value in sorted(lengths.items())),
                ),
                _table(
                    "QS-01",
                    "Passed-case objective quality results",
                    ("Test case", "Model", "Weight", "Cache", "Quality /10", "Prompts", "Criterion records", "Possible points"),
                    _quality_rows(bundle),
                    footnotes=(
                        "Only passed cases with normalized prompt-level criterion evidence are scored.",
                    ),
                ),
            ),
        ),
        ReportSection(
            SECTION_ORDER[9],
            (
                ReportParagraph(
                    "Runtime-property metadata records the requested attention and cache activation settings; it is not a substitute for an independent device trace."
                ),
                _table(
                    "DV-01",
                    "Normalized runtime-property evidence",
                    ("Property set", "Runtime properties", "Evidence status"),
                    _device_rows(bundle),
                ),
                ReportParagraph(
                    "ATTENTION_BACKEND=SDPA is recorded in the normalized runtime-property sets. Cache activation properties distinguish TurboQuant from scalar or codec configurations. The bundle does not contain an independent device-fallback trace, so this report does not claim that fallback was exercised or excluded beyond the recorded properties."
                ),
            ),
        ),
        ReportSection(
            SECTION_ORDER[10],
            (
                ReportParagraph(
                    "Every non-passed terminal outcome is retained with its stage, source status, reason, and linked evidence."
                ),
                _table(
                    "FL-01",
                    "Non-passed outcomes and evidence",
                    ("Failure ID", "Test case", "Status", "Stage", "Source status", "Reason", "Evidence IDs"),
                    _failure_rows(bundle),
                ),
                ReportParagraph(authority_note),
                ReportParagraph(
                    "Recovery and retry history is claimed only where the normalized failure records and linked evidence IDs provide it; an absent recovery record is not converted into a presumed attempt."
                ),
            ),
        ),
        ReportSection(
            SECTION_ORDER[11],
            (
                ReportParagraph(limitation_status),
                ReportParagraph(
                    f"Performance summaries use {', '.join(str(value) for value in repetition_values)} recorded repetitions per passed case. They describe observed medians, selected-repetition TTFT, and worst-observed memory; no confidence interval or population-level uncertainty estimate was collected."
                ),
                ReportParagraph(
                    "Within-route ranking is valid only on the same model, weight format, cache format, backend, prompt suite, rubric, denominator, and aggregation definition. Cross-repository or legacy-quality comparisons remain descriptive unless a separate comparability matrix confirms every required condition."
                ),
                ReportNote(
                    "No result establishes clinical safety, educational efficacy, model correctness for unseen tasks, or causal superiority of a quantization method."
                ),
            ),
        ),
        ReportSection(
            SECTION_ORDER[12],
            (
                ReportParagraph(
                    f"Use the frozen evidence and normalization instructions in docs/testing/final-results/{bundle.route_id}/reproduction/README.md. Regeneration validates source identities and normalizes existing evidence; it does not rerun inference."
                ),
                _table(
                    "RE-01",
                    "Reproduction inputs and outputs",
                    ("Item", "Repository-relative location"),
                    (
                        ("Canonical attempts", f"docs/testing/final-results/{bundle.route_id}/results/attempts.csv"),
                        ("Canonical measurements", f"docs/testing/final-results/{bundle.route_id}/results/measurements.csv"),
                        ("Canonical quality", f"docs/testing/final-results/{bundle.route_id}/quality/scores.csv"),
                        ("Evidence index", f"docs/testing/final-results/{bundle.route_id}/evidence/evidence-index.csv"),
                        ("Source workbook", f"docs/testing/final-results/{bundle.route_id}/results/source/"),
                    ),
                ),
            ),
        ),
        ReportSection(
            SECTION_ORDER[13],
            (
                ReportParagraph(
                    "The evidence index provides portable identities and hashes for every source item admitted to the normalized campaign bundle."
                ),
                _table(
                    "EV-01",
                    "Complete normalized evidence index",
                    ("Evidence ID", "Role", "Source label", "SHA-256", "Bytes", "Repository-relative path", "Kind", "Input evidence IDs"),
                    _evidence_rows(bundle),
                    footnotes=(
                        "SHA-256 and byte counts are preserved from the verified normalized evidence records.",
                    ),
                ),
            ),
        ),
        ReportSection(
            SECTION_ORDER[14],
            (
                ReportParagraph(
                    "Revision entries describe changes to this generated master report, separately from the source campaign revision."
                ),
                _table(
                    "RV-01",
                    "Report revision history",
                    ("Revision", "Date", "Change"),
                    (("R1", source_date.isoformat(), "Initial evidence-bound master report publication"),),
                ),
                ReportParagraph(
                    "Markdown is canonical. DOCX and PDF are generated derivatives; semantic parity and PDF layout validation are recorded beside the report."
                ),
            ),
        ),
    )
    return Report(
        title=title,
        route_id=bundle.route_id,
        revision="R1",
        generated_date=source_date,
        sections=sections,
        evidence_ids=key_evidence_ids,
    )


def regenerate_route_manifest(repo_root: Path, route: Path) -> Path:
    """Atomically replace a route checksum manifest after its final file set is known."""
    root = Path(repo_root).resolve(strict=True)
    route_path = Path(route).resolve(strict=True)
    try:
        route_path.relative_to(root)
    except ValueError as error:
        raise ValueError("route must resolve inside the repository root") from error
    manifest = route_path / "evidence/manifest-sha256.txt"
    manifest.parent.mkdir(parents=True, exist_ok=True)
    temporary = manifest.with_name(f".{manifest.name}.task8.tmp")
    if temporary.exists():
        raise FileExistsError(temporary)
    files = sorted(path for path in route_path.rglob("*") if path.is_file() and path != manifest)
    try:
        write_sha256_manifest(root, files, temporary)
        errors = validate_sha256_manifest(root, temporary)
        if errors:
            raise ValueError(f"replacement checksum manifest is invalid: {errors}")
        os.replace(temporary, manifest)
    finally:
        temporary.unlink(missing_ok=True)
    final_errors = validate_sha256_manifest(root, manifest)
    if final_errors:
        raise ValueError(f"regenerated checksum manifest is invalid: {final_errors}")
    return manifest


__all__ = ["SECTION_ORDER", "build_openvino_report", "regenerate_route_manifest"]
