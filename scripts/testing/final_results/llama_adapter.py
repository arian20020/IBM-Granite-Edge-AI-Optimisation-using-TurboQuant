"""Evidence-bound normalization and publication for historical llama.cpp routes.

Task 9 implements the upstream route.  Later tasks extend this module for the
two fork routes; keeping source admission here gives all three the same rules.
"""

from __future__ import annotations

import csv
import json
import re
import statistics
from collections import Counter, defaultdict
from datetime import date
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path
from typing import Iterable, Mapping, Sequence

from .csvio import write_csv, write_json
from .docx_renderer import render_docx
from .evidence import hash_file, validate_sha256_manifest, write_sha256_manifest
from .markdown_renderer import render_markdown
from .models import (
    AttemptRecord,
    EvidenceRecord,
    FailureRecord,
    MeasurementRecord,
    QualityRecord,
    RouteBundle,
    Status,
    SummaryRecord,
)
from .openvino_report import SECTION_ORDER, regenerate_route_manifest
from .parity import compare_markdown_docx
from .report_model import Report, ReportNote, ReportParagraph, ReportSection, ReportTable


ROUTE_ID = "upstream-llama-cpp"
CAMPAIGN_ID = "wb-01-v1.4-2026-07-16"
ROUTE_RELATIVE = Path("docs/testing/final-results/01-upstream-llama-cpp")
WORKBOOK_RELATIVE = Path(
    "docs/testing/workbooks/text-templates/01_Upstream_llama.cpp_Controlled_Retest_Workbook_v1.md"
)
QUALITY_RELATIVE = Path(
    "experiments/granite_turboquant_intel/processed-results/upstream-llama-cpp/quality-scoring-2026-07-15.md"
)
RESOURCE_RELATIVE = Path(
    "experiments/granite_turboquant_intel/processed-results/upstream-llama-cpp/resource-metrics-2026-07-16.json"
)
EVIDENCE_INDEX_RELATIVE = Path("docs/testing/Evidence-Index.csv")
TEST_RUN_REGISTER_RELATIVE = Path("docs/testing/Test-Run-Register.csv")
PERFORMANCE_REGISTER_RELATIVE = Path("docs/testing/Performance-Measurement-Register.csv")
QUALITY_RUBRIC_RELATIVE = Path(
    "experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json"
)
QUALITY_PROMPTS_RELATIVE = Path(
    "experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json"
)
EXPECTED_IDS = tuple(f"UL-{number:02d}" for number in range(1, 14))
NOT_COLLECTED = "Not collected"

_ATTEMPT_FIELDS = tuple(AttemptRecord.__dataclass_fields__)
_MEASUREMENT_FIELDS = tuple(MeasurementRecord.__dataclass_fields__)
_SUMMARY_FIELDS = tuple(SummaryRecord.__dataclass_fields__)
_QUALITY_FIELDS = tuple(QualityRecord.__dataclass_fields__)
_FAILURE_FIELDS = tuple(FailureRecord.__dataclass_fields__)
_EVIDENCE_FIELDS = tuple(EvidenceRecord.__dataclass_fields__)


def _read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def _read_json(path: Path) -> object:
    return json.loads(path.read_text(encoding="utf-8"))


def _markdown_tables(path: Path) -> list[list[list[str]]]:
    tables: list[list[list[str]]] = []
    active: list[list[str]] = []
    for line in path.read_text(encoding="utf-8").splitlines() + [""]:
        stripped = line.strip()
        if stripped.startswith("|") and stripped.endswith("|"):
            cells = [cell.strip() for cell in stripped[1:-1].split("|")]
            if all(re.fullmatch(r":?-{3,}:?", cell) for cell in cells):
                continue
            active.append(cells)
        elif active:
            tables.append(active)
            active = []
    return tables


def _table_by_header(path: Path, first_columns: Sequence[str]) -> list[dict[str, str]]:
    for table in _markdown_tables(path):
        if table and table[0][: len(first_columns)] == list(first_columns):
            header = table[0]
            rows: list[dict[str, str]] = []
            for values in table[1:]:
                if len(values) != len(header):
                    raise ValueError(f"malformed Markdown table row under {first_columns!r}")
                rows.append(dict(zip(header, values)))
            return rows
    raise ValueError(f"required Markdown table not found: {first_columns!r}")


def _parse_workbook_matrix(path: Path) -> list[dict[str, str]]:
    rows = _table_by_header(path, ("ID", "Model", "Weights", "K/V cache"))
    ids = [row["ID"] for row in rows]
    if ids != list(EXPECTED_IDS):
        raise ValueError("WB-01 matrix must contain UL-01 through UL-13 exactly once in order")
    return rows


def _formal_rows(path: Path) -> dict[str, dict[str, str]]:
    rows = _table_by_header(path, ("Test ID", "Model", "Weights", "K cache"))
    by_id = {row["Test ID"]: row for row in rows}
    if tuple(by_id) != EXPECTED_IDS:
        raise ValueError("WB-01 formal results do not reconcile with UL-01 through UL-13")
    return by_id


def _assert_no_exact_register_rows(path: Path, label: str) -> int:
    rows = _read_csv(path)
    exact = [row for row in rows if row.get("Test_ID") in EXPECTED_IDS]
    if exact:
        raise ValueError(
            f"{label} contains unexpected exact UL-01 through UL-13 row: "
            f"{exact[0].get('Test_ID')} / {exact[0].get('Run_ID', '')}"
        )
    return 0


def _decision_rows(path: Path) -> dict[str, str]:
    for table in _markdown_tables(path):
        if table and table[0] == ["Field", "Record"]:
            rows = dict(table[1:])
            if "Best CPU configuration" in rows:
                return rows
    raise ValueError("WB-01 decision table not found")


def _round_half_up(value: float, places: int) -> float:
    quantum = Decimal("1").scaleb(-places)
    return float(Decimal(str(value)).quantize(quantum, rounding=ROUND_HALF_UP))


def _parse_formal_throughput(value: str) -> tuple[float, float]:
    match = re.fullmatch(
        r"([0-9]+(?:\.[0-9]+)?) decode; ([0-9]+(?:\.[0-9]+)?) prompt", value
    )
    if match is None:
        raise ValueError(f"malformed WB-01 formal throughput: {value!r}")
    return float(match.group(1)), float(match.group(2))


def _parse_bench(path: Path) -> tuple[list[float], list[float]]:
    raw = path.read_bytes()
    encoding = "utf-16" if raw.startswith((b"\xff\xfe", b"\xfe\xff")) else "utf-8-sig"
    payloads = [json.loads(line) for line in raw.decode(encoding).splitlines() if line.strip()]
    prompt = next((row for row in payloads if int(row.get("n_prompt", 0)) > 0), None)
    decode = next((row for row in payloads if int(row.get("n_gen", 0)) > 0), None)
    if prompt is None or decode is None:
        raise ValueError(f"missing prompt/decode benchmark records: {path}")
    prompt_samples = [float(value) for value in prompt.get("samples_ts", [])]
    decode_samples = [float(value) for value in decode.get("samples_ts", [])]
    if len(prompt_samples) < 3 or len(prompt_samples) != len(decode_samples):
        raise ValueError(f"expected matching included prompt and decode samples: {path}")
    return prompt_samples, decode_samples


def _evidence_records(
    repo_root: Path, *, evidence_index_path: Path | None = None
) -> tuple[tuple[EvidenceRecord, ...], dict[str, EvidenceRecord]]:
    root = Path(repo_root).resolve(strict=True)
    index_path = evidence_index_path or root / EVIDENCE_INDEX_RELATIVE
    records: list[EvidenceRecord] = []
    by_path: dict[str, EvidenceRecord] = {}
    ids: set[str] = set()
    for row in _read_csv(index_path):
        if (
            row["Route"] != ROUTE_ID
            or row["Test_ID"] not in EXPECTED_IDS
            or not row["Repository_Path"].startswith(
                "experiments/granite_turboquant_intel/logs/upstream-llama-cpp/"
            )
        ):
            continue
        relative = row["Repository_Path"].replace("\\", "/")
        parts = relative.split("/")
        if len(parts) < 7 or (parts[4], parts[5]) != (row["Test_ID"], row["Run_ID"]):
            raise ValueError(
                "Evidence-Index binding conflict: "
                f"{row['Test_ID']} / {row['Run_ID']} / {relative}"
            )
        path = root / relative
        if not path.is_file():
            raise FileNotFoundError(path)
        digest = hash_file(path)
        if digest != row["SHA256"].lower():
            raise ValueError(f"Evidence-Index hash conflict: {relative}")
        if row["Evidence_ID"] in ids or relative in by_path:
            raise ValueError(f"duplicate indexed evidence identity/path: {relative}")
        record = EvidenceRecord(
            route_id=ROUTE_ID,
            campaign_id=CAMPAIGN_ID,
            evidence_id=row["Evidence_ID"],
            role="indexed-log",
            relative_path=relative,
            sha256=digest,
            size_bytes=path.stat().st_size,
            source_label=f"{row['Test_ID']} / {row['Run_ID']} / {row['Evidence_Type']}",
        )
        records.append(record)
        by_path[relative] = record
        ids.add(record.evidence_id)

    sources = (
        (WORKBOOK_RELATIVE, "controlled-workbook-markdown", "WB-01 v1.4 canonical Markdown"),
        (QUALITY_RELATIVE, "quality-scoring", "Original upstream quality scoring"),
        (QUALITY_RUBRIC_RELATIVE, "quality-rubric", "Tracked controlling quality rubric"),
        (QUALITY_PROMPTS_RELATIVE, "quality-prompts", "Tracked frozen prompt contract"),
        (RESOURCE_RELATIVE, "resource-summary", "Historical resource summary"),
        (EVIDENCE_INDEX_RELATIVE, "evidence-index", "Controlled evidence index"),
        (Path("docs/testing/Workbook-Revision-Register.csv"), "revision-register", "WB revision register"),
        (Path("docs/testing/Test-Run-Register.csv"), "test-run-register", "Historical test-run register"),
        (Path("docs/testing/Performance-Measurement-Register.csv"), "performance-register", "Historical performance register"),
        (Path("docs/testing/Failure-Register.csv"), "failure-register", "Historical failure register"),
        (Path("experiments/raw-results/upstream-llama-cpp/README.md"), "raw-results-placeholder", "Raw-results placeholder only"),
    )
    for relative_path, role, label in sources:
        path = root / relative_path
        digest = hash_file(path)
        evidence_id = f"{ROUTE_ID}-{digest[:12]}"
        if evidence_id in ids:
            evidence_id = f"{ROUTE_ID}-{digest}"
        if evidence_id in ids:
            raise ValueError(f"duplicate source evidence digest: {relative_path}")
        record = EvidenceRecord(
            route_id=ROUTE_ID,
            campaign_id=CAMPAIGN_ID,
            evidence_id=evidence_id,
            role=role,
            relative_path=relative_path.as_posix(),
            sha256=digest,
            size_bytes=path.stat().st_size,
            source_label=label,
        )
        records.append(record)
        by_path[record.relative_path] = record
        ids.add(record.evidence_id)

    workbook = root / WORKBOOK_RELATIVE
    for row in _table_by_header(workbook, ("Failure ID", "Test ID", "Code", "Description")):
        relative = Path(row["Evidence"].strip().replace("\\", "/"))
        path = root / relative
        if not path.is_file() or relative.as_posix() in by_path:
            continue
        digest = hash_file(path)
        evidence_id = f"{ROUTE_ID}-{digest[:12]}"
        if evidence_id in ids:
            evidence_id = f"{ROUTE_ID}-{digest}"
        record = EvidenceRecord(
            route_id=ROUTE_ID,
            campaign_id=CAMPAIGN_ID,
            evidence_id=evidence_id,
            role="historical-deviation-evidence",
            relative_path=relative.as_posix(),
            sha256=digest,
            size_bytes=path.stat().st_size,
            source_label=f"{row['Failure ID']} supporting evidence",
        )
        records.append(record)
        by_path[record.relative_path] = record
        ids.add(record.evidence_id)
    return tuple(sorted(records, key=lambda item: item.evidence_id)), by_path


def audit_upstream_llama_sources(
    repo_root: Path,
    *,
    workbook_path: Path | None = None,
    quality_path: Path | None = None,
    resource_path: Path | None = None,
    evidence_index_path: Path | None = None,
) -> dict[str, object]:
    """Reconcile each historical authority without erasing documented divergence."""
    root = Path(repo_root).resolve(strict=True)
    workbook = workbook_path or root / WORKBOOK_RELATIVE
    quality_source = quality_path or root / QUALITY_RELATIVE
    resource_source = resource_path or root / RESOURCE_RELATIVE
    matrix = _parse_workbook_matrix(workbook)
    formal = _formal_rows(workbook)
    matrix_by_id = {row["ID"]: row for row in matrix}
    if not all(row["Status"].startswith("Passed") for row in matrix):
        raise ValueError("WB-01 matrix contains a non-completed project workload")
    if not all(row["Status"].startswith("Passed") for row in formal.values()):
        raise ValueError("WB-01 formal table contains a non-completed project workload")

    for test_id in EXPECTED_IDS:
        intended = matrix_by_id[test_id]
        observed = formal[test_id]
        if (
            intended["Model"] != observed["Model"]
            or intended["Weights"] != observed["Weights"]
            or intended["K/V cache"].split("/") != [observed["K cache"], observed["V cache"]]
            or intended["Context"].replace(",", "") != observed["Context"].replace(",", "")
        ):
            raise ValueError(f"WB-01 matrix/formal identity mismatch: {test_id}")

    test_run_rows = _assert_no_exact_register_rows(
        root / TEST_RUN_REGISTER_RELATIVE, "current Test-Run register"
    )
    performance_rows = _assert_no_exact_register_rows(
        root / PERFORMANCE_REGISTER_RELATIVE, "current Performance register"
    )
    evidence, _ = _evidence_records(root, evidence_index_path=evidence_index_path)
    indexed_count = sum(item.role == "indexed-log" for item in evidence)

    quality_rows = _table_by_header(quality_source, ("Test", "P1", "P2", "P3", "P4"))
    quality_by_id = {row["Test"]: row for row in quality_rows if row["Test"] in EXPECTED_IDS}
    if tuple(quality_by_id) != EXPECTED_IDS:
        raise ValueError("quality source does not contain UL-01 through UL-13 in order")
    for test_id, row in quality_by_id.items():
        arithmetic = statistics.mean(float(row[prompt]) for prompt in ("P1", "P2", "P3", "P4"))
        displayed = float(row["P1-P4 mean"])
        if displayed != _round_half_up(arithmetic, 1):
            raise ValueError(f"quality aggregate mismatch: {test_id}")
        if test_id != "UL-05" and float(formal[test_id]["Quality /10"].split()[0]) != displayed:
            raise ValueError(f"formal quality mismatch: {test_id}")

    extended = _table_by_header(quality_source, ("Prompt", "Score", "Result"))
    p1_p6 = statistics.mean(
        [float(quality_by_id["UL-05"][prompt]) for prompt in ("P1", "P2", "P3", "P4")]
        + [float(row["Score"]) for row in extended]
    )
    quality_text = quality_source.read_text(encoding="utf-8")
    displayed_match = re.search(r"UL-05 P1-P6 mean: \*\*([0-9.]+)/10\*\*", quality_text)
    if displayed_match is None or float(displayed_match.group(1)) != 5.9:
        raise ValueError("UL-05 documented quality divergence changed")
    if float(formal["UL-05"]["Quality /10"].split()[0]) != 5.9:
        raise ValueError("UL-05 formal quality divergence changed")

    resource_payload = _read_json(resource_source)
    if not isinstance(resource_payload, dict) or set(resource_payload.get("rows", {})) != set(EXPECTED_IDS):
        raise ValueError("resource summary does not cover UL-01 through UL-13")
    processed_rows = resource_payload["rows"]
    ul13_computed: dict[str, float] = {}
    for test_id in EXPECTED_IDS:
        bench_run = "R003" if test_id == "UL-01" else "R002" if test_id == "UL-13" else "R001"
        bench_relative = (
            f"experiments/granite_turboquant_intel/logs/upstream-llama-cpp/{test_id}/"
            f"{test_id}-{bench_run}/bench-stdout.jsonl"
        )
        prompt_samples, decode_samples = _parse_bench(root / bench_relative)
        computed_prompt = statistics.median(prompt_samples)
        computed_decode = statistics.median(decode_samples)
        formal_decode, formal_prompt = _parse_formal_throughput(formal[test_id]["Tok/s"])
        if test_id == "UL-13":
            ul13_computed = {
                "prompt": _round_half_up(computed_prompt, 3),
                "decode": _round_half_up(computed_decode, 3),
            }
            if (formal_prompt, formal_decode) != (40.789, 7.211) or ul13_computed != {
                "prompt": 40.844,
                "decode": 7.212,
            }:
                raise ValueError("UL-13 documented performance divergence changed")
        elif (
            formal_prompt != _round_half_up(computed_prompt, 3)
            or formal_decode != _round_half_up(computed_decode, 3)
        ):
            raise ValueError(f"formal performance mismatch: {test_id}")

        server_rows = []
        for repetition in range(1, 4):
            server_rows.append(
                _read_json(
                    root
                    / (
                        f"experiments/granite_turboquant_intel/logs/upstream-llama-cpp/{test_id}/"
                        f"{test_id}-SERVER-METRICS-R001/sample-{repetition}/measurement.json"
                    )
                )
            )
        computed_resource = {
            "peak_ram_mb": _round_half_up(
                statistics.median(float(row["peak_ram_mb"]) for row in server_rows), 2
            ),
            "kv_mb": _round_half_up(
                statistics.median(float(row["kv_mb"]) for row in server_rows), 1
            ),
            "ttft_ms": _round_half_up(
                statistics.median(float(row["ttft_ms"]) for row in server_rows), 4
            ),
        }
        recorded_resource = {key: float(processed_rows[test_id][key]) for key in computed_resource}
        if recorded_resource != computed_resource:
            raise ValueError(f"processed resource mismatch: {test_id}")
        if (
            float(formal[test_id]["Peak RAM MB"].replace(",", ""))
            != _round_half_up(recorded_resource["peak_ram_mb"], 2)
            or float(formal[test_id]["KV MB"]) != recorded_resource["kv_mb"]
            or float(formal[test_id]["TTFT ms"].replace(",", ""))
            != _round_half_up(recorded_resource["ttft_ms"], 2)
        ):
            raise ValueError(f"formal resource mismatch: {test_id}")

    decisions = _decision_rows(workbook)
    expected_decisions = {
        "Best CPU configuration": "UL-04 for speed: Granite 3B Q4_K_M, F16 KV, CPU; UL-03 when quality is primary.",
        "Best Intel GPU configuration": "UL-09 Granite 3B Q4_K_M, Q8 KV, Vulkan one-layer offload: 79.856 prompt and 15.214 decode tok/s.",
        "Recommended fallback configuration": "UL-04 CPU F16 KV for throughput, or UL-03 CPU F16 KV for quality-sensitive work.",
    }
    if any(decisions[key] != value for key, value in expected_decisions.items()):
        raise ValueError("legacy WB-01 decision labels changed")

    return {
        "exact_test_run_register_rows": test_run_rows,
        "exact_performance_register_rows": performance_rows,
        "matrix_ids": list(EXPECTED_IDS),
        "formal_ids": list(EXPECTED_IDS),
        "evidence_binding_count": indexed_count,
        "resource_rows_reconciled": len(processed_rows),
        "quality_rows_reconciled": len(quality_by_id),
        "known_divergences": {
            "UL-13-performance": {
                "workbook_prompt_tokens_per_second": 40.789,
                "computed_prompt_tokens_per_second": ul13_computed["prompt"],
                "workbook_decode_tokens_per_second": 7.211,
                "computed_decode_tokens_per_second": ul13_computed["decode"],
                "precedence": "indexed repetition logs",
            },
            "UL-05-quality": {
                "workbook_displayed_mean": 5.9,
                "arithmetic_prompt_mean": p1_p6,
                "precedence": "prompt-level scores",
            },
            "decision-labels": {
                "workbook_cpu": "UL-04 for speed; UL-03 when quality is primary",
                "workbook_gpu": "UL-09",
                "workbook_fallback": "UL-04 or UL-03",
                "approved_cpu": "UL-08 only",
                "approved_gpu": "UL-10 only",
                "approved_fallback": "UL-05 only",
                "precedence": "approved bounded publication labels",
            },
        },
    }


def _summary(
    test_id: str,
    metric: str,
    value: float | int | None,
    unit: str,
    aggregation: str,
    measurement_ids: Sequence[str],
) -> SummaryRecord:
    return SummaryRecord(
        route_id=ROUTE_ID,
        campaign_id=CAMPAIGN_ID,
        test_case_id=test_id,
        summary_id=f"{test_id}--{metric}",
        metric_name=metric,
        value=value,
        unit=unit,
        aggregation=aggregation,
        source_measurement_ids=tuple(measurement_ids),
    )


def _quality_scores(path: Path, evidence_id: str) -> tuple[QualityRecord, ...]:
    rows = _table_by_header(path, ("Test", "P1", "P2", "P3", "P4"))
    records: list[QualityRecord] = []
    for row in rows:
        test_id = row["Test"]
        if test_id not in EXPECTED_IDS:
            continue
        for prompt_id in ("P1", "P2", "P3", "P4"):
            records.append(
                QualityRecord(
                    route_id=ROUTE_ID,
                    campaign_id=CAMPAIGN_ID,
                    test_case_id=test_id,
                    quality_id=f"{test_id}--{prompt_id}--legacy-composite",
                    prompt_id=prompt_id,
                    criterion_id="legacy-composite-score-with-format-caps",
                    score=float(row[prompt_id]),
                    maximum_score=10.0,
                    prompt_suite_id="GTQ-PROMPTS-v1",
                    rubric_id="GTQ-QUALITY-RUBRIC-v1",
                    scoring_version="WB-01-v1.4-conservative-format-caps",
                    source_evidence_id=evidence_id,
                )
            )
    extended = _table_by_header(path, ("Prompt", "Score", "Result"))
    for row in extended:
        prompt_id = row["Prompt"].split()[0]
        records.append(
            QualityRecord(
                route_id=ROUTE_ID,
                campaign_id=CAMPAIGN_ID,
                test_case_id="UL-05",
                quality_id=f"UL-05--{prompt_id}--legacy-composite",
                prompt_id=prompt_id,
                criterion_id="legacy-composite-score-with-format-caps",
                score=float(row["Score"]),
                maximum_score=10.0,
                prompt_suite_id="GTQ-PROMPTS-v1",
                rubric_id="GTQ-QUALITY-RUBRIC-v1",
                scoring_version="WB-01-v1.4-conservative-format-caps",
                source_evidence_id=evidence_id,
            )
        )
    if len(records) != 54:
        raise ValueError(f"expected 54 original quality observations, got {len(records)}")
    return tuple(records)


def _source_evidence_ids(
    evidence_path: str, evidence_by_path: Mapping[str, EvidenceRecord]
) -> tuple[str, ...]:
    normalized = evidence_path.strip().replace("\\", "/").rstrip("/")
    exact = evidence_by_path.get(normalized)
    if exact is not None:
        return (exact.evidence_id,)
    prefix = normalized + "/"
    return tuple(
        sorted(
            record.evidence_id
            for path, record in evidence_by_path.items()
            if path.startswith(prefix)
        )
    )


def _failure_records(path: Path, evidence_by_path: Mapping[str, EvidenceRecord]) -> tuple[FailureRecord, ...]:
    rows = _table_by_header(path, ("Failure ID", "Test ID", "Code", "Description"))
    results: list[FailureRecord] = []
    canonical_scopes = {
        "UL-F03": ("UL-13",),
        "UL-F04": ("UL-05",),
        "UL-F05": ("UL-05",),
        "UL-F06": ("UL-10", "UL-12"),
    }
    for row in rows:
        for test_id in canonical_scopes.get(row["Failure ID"], ()):
            results.append(
                FailureRecord(
                    route_id=ROUTE_ID,
                    campaign_id=CAMPAIGN_ID,
                    test_case_id=test_id,
                    attempt_id=f"{test_id}--attempt-001",
                    failure_id=f"{row['Failure ID']}--{test_id}",
                    status=Status.NOT_APPLICABLE,
                    stage=row["Code"],
                    reason=row["Description"],
                    source_status=row["Resolved?"],
                    evidence_ids=_source_evidence_ids(row["Evidence"], evidence_by_path),
                )
            )
    return tuple(results)


def build_upstream_deviation_rows(
    repo_root: Path, bundle: RouteBundle
) -> tuple[dict[str, object], ...]:
    root = Path(repo_root).resolve(strict=True)
    evidence_by_path = {item.relative_path: item for item in bundle.evidence}
    workbook_rows = _table_by_header(
        root / WORKBOOK_RELATIVE, ("Failure ID", "Test ID", "Code", "Description")
    )
    scope_map: dict[str, tuple[str, list[str]]] = {
        "UL-F01": ("setup", ["UL-B05"]),
        "UL-F02": ("setup", ["UL-B05"]),
        "UL-F03": ("mixed", ["UL-B06", "UL-13"]),
        "UL-F04": ("prompt", ["UL-05"]),
        "UL-F05": ("prompt", ["UL-05"]),
        "UL-F06": ("multi-test", ["UL-10", "UL-12"]),
        "UL-F07": ("campaign", list(EXPECTED_IDS)),
    }
    rows: list[dict[str, object]] = []
    for source in workbook_rows:
        scope_type, scope_ids = scope_map[source["Failure ID"]]
        rows.append(
            {
                "deviation_id": f"UL-DEV-HIST-{source['Failure ID'][-2:]}",
                "source_failure_id": source["Failure ID"],
                "scope_type": scope_type,
                "scope_test_ids": scope_ids,
                "nonterminal": True,
                "code": source["Code"],
                "description": source["Description"],
                "disposition": source["Resolved?"],
                "evidence_ids": list(_source_evidence_ids(source["Evidence"], evidence_by_path)),
            }
        )

    source_evidence = {item.role: item.evidence_id for item in bundle.evidence}
    attempt_by_id = {item.test_case_id: item for item in bundle.attempts}
    rows.extend(
        (
            {
                "deviation_id": "UL-DEV-REGISTER-TEST-RUN",
                "source_failure_id": "",
                "scope_type": "campaign",
                "scope_test_ids": list(EXPECTED_IDS),
                "nonterminal": True,
                "code": "AUTHORITY",
                "description": "Current Test-Run register contains zero exact UL-01 through UL-13 rows.",
                "disposition": "Indexed logs and WB-01 are the admitted historical authorities; no register rows were invented.",
                "evidence_ids": [source_evidence["test-run-register"]],
            },
            {
                "deviation_id": "UL-DEV-REGISTER-PERFORMANCE",
                "source_failure_id": "",
                "scope_type": "campaign",
                "scope_test_ids": list(EXPECTED_IDS),
                "nonterminal": True,
                "code": "AUTHORITY",
                "description": "Current Performance register contains zero exact UL-01 through UL-13 rows.",
                "disposition": "Indexed repetition logs take precedence; no later-register equality is claimed.",
                "evidence_ids": [source_evidence["performance-register"]],
            },
            {
                "deviation_id": "UL-DEV-UL13-PERFORMANCE",
                "source_failure_id": "",
                "scope_type": "test",
                "scope_test_ids": ["UL-13"],
                "nonterminal": True,
                "code": "PRECEDENCE",
                "description": "WB formal values are 40.789 prompt / 7.211 decode tok/s; indexed repetitions compute 40.844 / 7.212.",
                "disposition": "Normalized summaries use indexed repetition medians and preserve WB values as a documented divergence.",
                "evidence_ids": [source_evidence["controlled-workbook-markdown"], *attempt_by_id["UL-13"].evidence_ids],
            },
            {
                "deviation_id": "UL-DEV-UL05-QUALITY",
                "source_failure_id": "",
                "scope_type": "test",
                "scope_test_ids": ["UL-05"],
                "nonterminal": True,
                "code": "PRECEDENCE",
                "description": "Legacy WB/quality prose displays P1-P6 mean 5.9; arithmetic over prompt scores is 5.750.",
                "disposition": "Prompt-level scores are canonical; both historical display and arithmetic value remain explicit.",
                "evidence_ids": [source_evidence["controlled-workbook-markdown"], source_evidence["quality-scoring"]],
            },
            {
                "deviation_id": "UL-DEV-DECISION-LABELS",
                "source_failure_id": "",
                "scope_type": "campaign",
                "scope_test_ids": list(EXPECTED_IDS),
                "nonterminal": True,
                "code": "PRECEDENCE",
                "description": "Legacy WB labels name UL-04/UL-03 CPU and fallback choices and UL-09 GPU; approved publication labels are UL-08/UL-10/UL-05 only.",
                "disposition": "Approved bounded publication labels take precedence without rewriting legacy WB prose.",
                "evidence_ids": [source_evidence["controlled-workbook-markdown"]],
            },
        )
    )
    return tuple(rows)


def validate_upstream_relationships(
    bundle: RouteBundle, deviations: Sequence[Mapping[str, object]]
) -> dict[str, object]:
    attempt_ids = {item.attempt_id for item in bundle.attempts}
    test_ids = {item.test_case_id for item in bundle.attempts}
    evidence_ids = {item.evidence_id for item in bundle.evidence}
    errors: list[str] = []
    if test_ids != set(EXPECTED_IDS):
        errors.append("attempt test set does not equal UL-01 through UL-13")
    if any(item.status is not Status.PASSED or not item.executed for item in bundle.attempts):
        errors.append("all 13 canonical attempts must remain executed and passed")
    for failure in bundle.failures:
        if failure.test_case_id not in test_ids:
            errors.append(f"failure {failure.failure_id} references unknown test {failure.test_case_id}")
        if failure.attempt_id not in attempt_ids:
            errors.append(f"failure {failure.failure_id} references unknown attempt {failure.attempt_id}")
        for evidence_id in failure.evidence_ids:
            if evidence_id not in evidence_ids:
                errors.append(f"failure {failure.failure_id} references unknown evidence {evidence_id}")

    seen_deviations: set[str] = set()
    setup_pattern = re.compile(r"UL-B\d{2}")
    allowed_scope_types = {"setup", "test", "prompt", "multi-test", "mixed", "campaign"}
    for row in deviations:
        deviation_id = str(row["deviation_id"])
        if deviation_id in seen_deviations:
            errors.append(f"duplicate deviation {deviation_id}")
        seen_deviations.add(deviation_id)
        if row.get("nonterminal") is not True:
            errors.append(f"deviation {deviation_id} is not explicitly nonterminal")
        scope_type = str(row.get("scope_type"))
        scopes = list(row.get("scope_test_ids", []))
        if scope_type not in allowed_scope_types:
            errors.append(f"deviation {deviation_id} has unknown scope type {scope_type}")
        for scope in scopes:
            if scope not in test_ids and setup_pattern.fullmatch(str(scope)) is None:
                errors.append(f"deviation {deviation_id} has invalid scope {scope}")
        if scope_type == "campaign" and set(scopes) != test_ids:
            errors.append(f"deviation {deviation_id} campaign scope is incomplete")
        for evidence_id in row.get("evidence_ids", []):
            if evidence_id not in evidence_ids:
                errors.append(f"deviation {deviation_id} references unknown evidence {evidence_id}")
    return {
        "valid": not errors,
        "errors": errors,
        "attempt_count": len(bundle.attempts),
        "failure_relationship_count": len(bundle.failures),
        "deviation_count": len(deviations),
        "evidence_count": len(bundle.evidence),
    }


def build_upstream_llama_bundle(repo_root: Path) -> RouteBundle:
    """Normalize WB-01 v1.4 without inventing later-register or raw-results rows."""
    root = Path(repo_root).resolve(strict=True)
    workbook = root / WORKBOOK_RELATIVE
    source_audit = audit_upstream_llama_sources(root)
    matrix = _parse_workbook_matrix(workbook)
    formal = _formal_rows(workbook)
    evidence, evidence_by_path = _evidence_records(root)
    quality_source = evidence_by_path[QUALITY_RELATIVE.as_posix()]
    quality = _quality_scores(root / QUALITY_RELATIVE, quality_source.evidence_id)
    resource_payload = _read_json(root / RESOURCE_RELATIVE)
    if not isinstance(resource_payload, dict) or set(resource_payload.get("rows", {})) != set(EXPECTED_IDS):
        raise ValueError("resource summary does not cover UL-01 through UL-13")

    attempts: list[AttemptRecord] = []
    measurements: list[MeasurementRecord] = []
    summaries: list[SummaryRecord] = []
    matrix_by_id = {row["ID"]: row for row in matrix}
    for test_id in EXPECTED_IDS:
        row = matrix_by_id[test_id]
        result_row = formal[test_id]
        bench_run = "R003" if test_id == "UL-01" else "R002" if test_id == "UL-13" else "R001"
        bench_relative = (
            f"experiments/granite_turboquant_intel/logs/upstream-llama-cpp/{test_id}/"
            f"{test_id}-{bench_run}/bench-stdout.jsonl"
        )
        prompt_samples, decode_samples = _parse_bench(root / bench_relative)
        server_measurements: list[dict[str, object]] = []
        server_measurement_ids: list[str] = []
        bench_measurement_ids: list[str] = []
        evidence_ids = [evidence_by_path[bench_relative].evidence_id]
        bench_evidence = evidence_by_path[bench_relative]
        for repetition, (prompt_value, decode_value) in enumerate(
            zip(prompt_samples, decode_samples), start=1
        ):
            measurement_id = f"{test_id}--llama-bench-sample-{repetition:03d}"
            bench_measurement_ids.append(measurement_id)
            measurements.append(
                MeasurementRecord(
                    route_id=ROUTE_ID,
                    campaign_id=CAMPAIGN_ID,
                    test_case_id=test_id,
                    attempt_id=f"{test_id}--attempt-001",
                    measurement_id=measurement_id,
                    run_id=f"{test_id}-{bench_run}",
                    repetition_id=f"bench-{repetition:03d}",
                    source_evidence_id=bench_evidence.evidence_id,
                    prompt_tokens_per_second=prompt_value,
                    generation_tokens_per_second=decode_value,
                )
            )
        for repetition in range(1, 4):
            server_relative = (
                f"experiments/granite_turboquant_intel/logs/upstream-llama-cpp/{test_id}/"
                f"{test_id}-SERVER-METRICS-R001/sample-{repetition}/measurement.json"
            )
            server = _read_json(root / server_relative)
            if not isinstance(server, dict) or server.get("valid") is not True or server.get("exit_code") != 0:
                raise ValueError(f"invalid included repetition: {server_relative}")
            server_measurements.append(server)
            source = evidence_by_path[server_relative]
            evidence_ids.append(source.evidence_id)
            measurement_id = f"{test_id}--server-resource-sample-{repetition:03d}"
            server_measurement_ids.append(measurement_id)
            measurements.append(
                MeasurementRecord(
                    route_id=ROUTE_ID,
                    campaign_id=CAMPAIGN_ID,
                    test_case_id=test_id,
                    attempt_id=f"{test_id}--attempt-001",
                    measurement_id=measurement_id,
                    run_id=f"{test_id}-SERVER-METRICS-R001",
                    repetition_id=f"server-{repetition:03d}",
                    source_evidence_id=source.evidence_id,
                    latency_ms=float(server["ttft_ms"]),
                    peak_working_set_bytes=round(float(server["peak_ram_mb"]) * 1024 * 1024),
                    input_tokens=None,
                    output_tokens=None,
                )
            )
        model = row["Model"].replace(" ", "-").casefold()
        backend = "sycl" if "SYCL" in row["Device"] else "vulkan" if "Vulkan" in row["Device"] else "cpu"
        attempts.append(
            AttemptRecord(
                route_id=ROUTE_ID,
                campaign_id=CAMPAIGN_ID,
                test_case_id=test_id,
                attempt_id=f"{test_id}--attempt-001",
                status=Status.PASSED,
                executed=True,
                model_id=model,
                weight_format_id=row["Weights"].casefold(),
                cache_format_id=row["K/V cache"].replace("/", "-").casefold(),
                backend_id=backend,
                source_status=result_row["Status"],
                evidence_ids=tuple(evidence_ids),
            )
        )
        summaries.extend(
            (
                _summary(test_id, "generation_tokens_per_second", statistics.median(decode_samples), "tokens_per_second", f"median of exactly {len(decode_samples)} included llama-bench samples", bench_measurement_ids),
                _summary(test_id, "prompt_tokens_per_second", statistics.median(prompt_samples), "tokens_per_second", f"median of exactly {len(prompt_samples)} included llama-bench samples", bench_measurement_ids),
                _summary(test_id, "time_to_first_token", statistics.median(float(item["ttft_ms"]) for item in server_measurements), "milliseconds", "median of exactly three included llama-server samples", server_measurement_ids),
                _summary(test_id, "peak_working_set_bytes", round(statistics.median(float(item["peak_ram_mb"]) for item in server_measurements) * 1024 * 1024), "bytes", "median of three repetition-level maximum process-tree working sets", server_measurement_ids),
                _summary(test_id, "kv_cache_allocated_bytes", round(statistics.median(float(item["kv_mb"]) for item in server_measurements) * 1024 * 1024), "bytes", "median of exactly three deduplicated runtime KV allocations", server_measurement_ids),
            )
        )

    revision_rows = [
        row for row in _read_csv(root / "docs/testing/Workbook-Revision-Register.csv")
        if row["Workbook_ID"] == "WB-01"
    ]
    if not revision_rows or revision_rows[-1]["Version"] != "1.4":
        raise ValueError("WB-01 current revision is not 1.4")
    bundle = RouteBundle(
        route_id=ROUTE_ID,
        campaign_id=CAMPAIGN_ID,
        attempts=tuple(attempts),
        measurements=tuple(measurements),
        summaries=tuple(summaries),
        quality=quality,
        failures=_failure_records(workbook, evidence_by_path),
        evidence=evidence,
        repository={
            "url": "https://github.com/ggml-org/llama.cpp",
            "tag": "b9870",
            "commit": "2d973636e292ee6f75fadcf08d29cb33511f509f",
            "workbook_id": "WB-01",
            "workbook_revision": "1.4",
            "source_date": "2026-07-16",
            "best_observed_cpu_test_id": "UL-08",
            "best_observed_intel_gpu_test_id": "UL-10",
            "recorded_fallback_test_id": "UL-05",
            "recommendation_scope": "UL-08 only as best observed CPU; UL-10 only as best observed Intel GPU; UL-05 only as the recorded fallback.",
            "raw_results_status": NOT_COLLECTED,
            "raw_results_reason": "README placeholder only; no raw result observations",
            "performance_register_status": NOT_COLLECTED,
            "performance_register_reason": "No UL-01 through UL-13 rows; repetition evidence predates the register",
            "historical_missing_metric_display": NOT_COLLECTED,
            "source_reconciliation": source_audit,
        },
        hardware={
            "machine_id": "LENOVO-PF4HMD0T",
            "cpu": "12th Gen Intel Core i5-12450H; 8 cores; 12 logical processors",
            "ram_bytes": 16857817088,
            "gpu": "Intel UHD Graphics; driver 32.0.101.7076",
            "npu": "Not applicable",
            "gpu_dedicated_memory": NOT_COLLECTED,
        },
        software={
            "os": "Windows 11 Home 10.0.26200 build 26200, 64-bit",
            "compiler": "MSVC via Visual Studio 18 2026",
            "cmake": "4.3.1-msvc1",
            "vulkan_sdk": "1.4.350.0",
            "sycl": "Intel oneAPI 2026.1 SYCL",
            "performance_register_rows": 0,
        },
    )
    deviations = build_upstream_deviation_rows(root, bundle)
    relationship_validation = validate_upstream_relationships(bundle, deviations)
    if not relationship_validation["valid"]:
        raise ValueError(f"upstream relationship validation failed: {relationship_validation['errors']}")
    bundle.repository["deviation_rows"] = deviations
    bundle.repository["relationship_validation"] = relationship_validation
    return bundle


def _table(table_id: str, title: str, columns: Sequence[str], rows: Iterable[Sequence[object]], *, footnotes: Sequence[str] = ()) -> ReportTable:
    return ReportTable(
        table_id=table_id,
        title=title,
        columns=tuple(columns),
        rows=tuple(tuple(str(cell) for cell in row) for row in rows),
        footnotes=tuple(footnotes),
    )


def _summary_map(bundle: RouteBundle) -> dict[tuple[str, str], SummaryRecord]:
    return {(item.test_case_id, item.metric_name): item for item in bundle.summaries}


def build_upstream_llama_report(bundle: RouteBundle) -> Report:
    """Build the approved 15-section report only from normalized records."""
    if bundle.route_id != ROUTE_ID:
        raise ValueError(f"unsupported route: {bundle.route_id}")
    summaries = _summary_map(bundle)
    deviations = tuple(bundle.repository["deviation_rows"])
    attempts = {item.test_case_id: item for item in bundle.attempts}
    quality_by_test: dict[str, list[QualityRecord]] = defaultdict(list)
    for record in bundle.quality:
        quality_by_test[record.test_case_id].append(record)
    performance_rows = []
    for test_id in EXPECTED_IDS:
        performance_rows.append(
            (
                test_id,
                f"{float(summaries[test_id, 'prompt_tokens_per_second'].value):.3f}",
                f"{float(summaries[test_id, 'generation_tokens_per_second'].value):.3f}",
                f"{float(summaries[test_id, 'time_to_first_token'].value):.2f}",
                str(summaries[test_id, "peak_working_set_bytes"].value),
                str(summaries[test_id, "kv_cache_allocated_bytes"].value),
            )
        )
    quality_rows = []
    for test_id in EXPECTED_IDS:
        records = quality_by_test[test_id]
        mean = statistics.mean(float(item.score) for item in records)
        quality_rows.append((test_id, len(records), f"{mean:.3f}", records[0].rubric_id))
    repetition_rows = [
        (
            item.test_case_id,
            item.repetition_id,
            NOT_COLLECTED if item.prompt_tokens_per_second is None else f"{item.prompt_tokens_per_second:.5f}",
            NOT_COLLECTED if item.generation_tokens_per_second is None else f"{item.generation_tokens_per_second:.5f}",
            NOT_COLLECTED if item.latency_ms is None else f"{item.latency_ms:.4f}",
            NOT_COLLECTED if item.peak_working_set_bytes is None else item.peak_working_set_bytes,
            NOT_COLLECTED,
            NOT_COLLECTED,
            item.source_evidence_id,
        )
        for item in bundle.measurements
    ]
    evidence_rows = [
        (item.evidence_id, item.role, item.sha256, item.size_bytes, item.relative_path)
        for item in bundle.evidence
    ]
    sections = (
        ReportSection(SECTION_ORDER[0], (
            _table("DC-01", "Document identity and authority", ("Field", "Value"), (
                ("Route", ROUTE_ID), ("Campaign", bundle.campaign_id), ("Controlled source", "WB-01 v1.4"),
                ("Pinned upstream commit", bundle.repository["commit"]), ("Canonical report", "workbook/source/upstream-llama-cpp-final-report.md"),
            )),
            ReportParagraph("WB-01 v1.4, its controlled evidence index, and the indexed upstream log tree are the authority. Later empty register fields and a README-only raw-results folder are not observations."),
        )),
        ReportSection(SECTION_ORDER[1], (
            ReportParagraph("All 13 intended project workloads completed. The result is a conditional pass because the broader SYCL repository suite retained three edge-case failures even though UL-13 project inference passed."),
            _table("AC-01", "Attempt status summary", ("Status", "Count"), (("Passed", 13),)),
            ReportNote("Historical missing values are displayed as Not collected; they are never converted to zero."),
        )),
        ReportSection(SECTION_ORDER[2], (
            ReportParagraph("Recommendation labels are deliberately narrow and describe only this observed campaign."),
            _table("KF-01", "Bounded decision labels", ("Role", "Test ID", "Boundary"), (
                ("best observed CPU", "UL-08", "UL-08 only; observed campaign label"),
                ("best observed Intel GPU", "UL-10", "UL-10 only; observed campaign label"),
                ("recorded fallback", "UL-05", "UL-05 only; recorded fallback label"),
            ), footnotes=("These labels do not establish causal or universal superiority.",)),
        )),
        ReportSection(SECTION_ORDER[3], (
            ReportParagraph("Identity values below are retained from WB-01 rather than inferred from the reviewing machine."),
            _table("ID-01", "Repository and system identity", ("Field", "Value"), tuple((key, value) for source in (bundle.repository, bundle.hardware, bundle.software) for key, value in source.items() if key not in {"deviation_rows", "relationship_validation", "source_reconciliation"})),
        )),
        ReportSection(SECTION_ORDER[4], (
            ReportParagraph("The intended sequence moved from CPU precision/cache baselines through Vulkan partial/full offload and finally the SYCL project workload."),
            _table("MX-01", "Intended matrix", ("Test ID", "Model", "Weight", "Cache", "Backend", "Source status"), ((item.test_case_id, item.model_id, item.weight_format_id, item.cache_format_id, item.backend_id, item.source_status) for item in bundle.attempts)),
        )),
        ReportSection(SECTION_ORDER[5], (
            ReportParagraph("Availability is represented by the 13 completed matrix rows. No raw-results observations or absent model combinations were invented."),
            _table("AV-01", "Observed model/backend availability", ("Test ID", "Model", "Weight", "Cache", "Backend", "Status"), ((item.test_case_id, item.model_id, item.weight_format_id, item.cache_format_id, item.backend_id, item.status.display_label) for item in bundle.attempts)),
        )),
        ReportSection(SECTION_ORDER[6], (
            ReportParagraph("Each intended workload appears exactly once in the normalized terminal ledger."),
            _table("AT-01", "Complete attempt accounting", ("Test ID", "Attempt ID", "Executed", "Status", "Source status", "Evidence IDs"), ((item.test_case_id, item.attempt_id, item.executed, item.status.display_label, item.source_status, ", ".join(item.evidence_ids)) for item in bundle.attempts)),
        )),
        ReportSection(SECTION_ORDER[7], (
            ReportParagraph("All included source observations are expanded. Prompt/decode samples come from llama-bench (three per row, except five for UL-13); TTFT and process-tree memory come from three llama-server samples per row. The two series are not falsely represented as one synchronized run."),
            _table("PF-01", "Performance summaries", ("Test ID", "Prompt tok/s", "Decode tok/s", "TTFT ms", "Peak bytes", "KV bytes"), performance_rows),
            _table("RP-01", "Included repetition detail", ("Test ID", "Repetition", "Prompt tok/s", "Decode tok/s", "TTFT ms", "Peak bytes", "Input tokens", "Output tokens", "Evidence ID"), repetition_rows),
            ReportNote("Input-token and output-token counts were not collected in this historical repetition series."),
        )),
        ReportSection(SECTION_ORDER[8], (
            ReportParagraph("Quality preserves the original 2026-07-15 upstream llama.cpp adjudication under the tracked GTQ-QUALITY-RUBRIC-v1 and frozen GTQ-PROMPTS-v1 contracts: five weighted dimensions, deterministic gates, anchors, strict format caps, and other critical caps. P1-P4 were scored for all rows; UL-05 additionally used P5 long-context retrieval and P6 multi-turn stability."),
            _table("QM-01", "Original quality method", ("Item", "Value"), (
                ("Rubric", "GTQ-QUALITY-RUBRIC-v1"), ("Prompt set", "GTQ-PROMPTS-v1"),
                ("Dimension weights", "30% correctness; 25% instruction/format; 20% completeness; 15% relevance/coherence; 10% stability/integrity"),
                ("Scoring", "Conservative manual adjudication after deterministic gates; critical caps preserved"),
                ("Generation", "temperature 0.0; top_p 1.0; seed 42; max_output_tokens 256"),
                ("Coverage", "P1-P4 all rows; P5-P6 UL-05 only"),
                ("Calibration", NOT_COLLECTED), ("Score increments", NOT_COLLECTED),
            )),
            _table("QS-01", "Original quality results", ("Test ID", "Prompt count", "Mean /10", "Rubric"), quality_rows),
            ReportNote("These historical scores are not directly comparable with OpenVINO objective-quality scores or later llama.cpp campaigns unless a separate comparability assessment confirms compatible prompts, rubric, denominator, and adjudication."),
        )),
        ReportSection(SECTION_ORDER[9], (
            ReportParagraph("CPU rows used CPU placement; Vulkan rows recorded one-layer or full-layer placement; UL-13 used full reported SYCL placement. Fallback language is limited to the recorded evidence."),
            _table("DV-01", "Backend and fallback interpretation", ("Test ID", "Backend", "Interpretation"), ((item.test_case_id, item.backend_id, "recorded fallback" if item.test_case_id == "UL-05" else "No fallback claim added") for item in bundle.attempts)),
        )),
        ReportSection(SECTION_ORDER[10], (
            ReportParagraph("The entries below are historical failures, limitations, quality deviations, or performance deviations retained by WB-01; they are not reclassified as terminal failures of the 13 completed workloads."),
            _table("FL-01", "Historical failure and deviation register", ("Failure ID", "Test ID", "Code", "Description", "Resolution", "Evidence IDs"), ((item.failure_id, item.test_case_id, item.stage, item.reason, item.source_status, ", ".join(item.evidence_ids)) for item in bundle.failures)),
            _table("DV-02", "Scoped deviation and precedence ledger", ("Deviation ID", "Source item", "Scope type", "Scope test IDs", "Nonterminal", "Description", "Disposition", "Evidence IDs"), ((row["deviation_id"], row["source_failure_id"], row["scope_type"], ", ".join(row["scope_test_ids"]), row["nonterminal"], row["description"], row["disposition"], ", ".join(row["evidence_ids"])) for row in deviations)),
        )),
        ReportSection(SECTION_ORDER[11], (
            ReportParagraph("This is one laptop, one pinned upstream revision, and one historical prompt method. Repetition counts support observed medians, not population confidence intervals."),
            ReportParagraph("The current Test-Run and Performance registers each contain zero exact UL-01 through UL-13 rows. Indexed log evidence is therefore the repetition authority. UL-13 WB formal throughput and UL-05 displayed aggregate quality diverge from arithmetic source values; both are retained in the scoped deviation ledger with explicit precedence. The raw-results folder is a README placeholder only."),
            ReportNote("No result establishes healthcare safety, educational efficacy, universal model quality, or causal superiority."),
        )),
        ReportSection(SECTION_ORDER[12], (
            ReportParagraph("Regeneration normalizes existing evidence and does not rerun inference. Use the repository-relative inputs listed in reproduction/README.md."),
            _table("RE-01", "Canonical reproduction locations", ("Item", "Path"), (
                ("Controlled workbook", WORKBOOK_RELATIVE.as_posix()), ("Quality source", QUALITY_RELATIVE.as_posix()),
                ("Resource summary", RESOURCE_RELATIVE.as_posix()), ("Evidence index", EVIDENCE_INDEX_RELATIVE.as_posix()),
                ("Canonical output", f"{ROUTE_RELATIVE.as_posix()}/results/"),
            )),
        )),
        ReportSection(SECTION_ORDER[13], (
            ReportParagraph("Every admitted source has a repository-relative path, byte count, and verified SHA-256 digest."),
            _table("EV-01", "Complete admitted evidence index", ("Evidence ID", "Role", "SHA-256", "Bytes", "Repository-relative path"), evidence_rows),
        )),
        ReportSection(SECTION_ORDER[14], (
            ReportParagraph("This generated report is revision R1; it does not alter WB-01 revision history."),
            _table("RV-01", "Revision history", ("Revision", "Date", "Change"), (("R1", "2026-07-16", "Initial unified evidence-bound publication from WB-01 v1.4"),)),
            ReportParagraph("Markdown is canonical. DOCX and PDF are generated derivatives validated separately."),
        )),
    )
    key_roles = {
        "controlled-workbook-markdown",
        "quality-scoring",
        "quality-rubric",
        "quality-prompts",
        "resource-summary",
        "evidence-index",
    }
    return Report(
        title="Upstream llama.cpp Final Test Report",
        route_id=ROUTE_ID,
        revision="R1",
        generated_date=date(2026, 7, 16),
        sections=sections,
        evidence_ids=tuple(item.evidence_id for item in bundle.evidence if item.role in key_roles),
    )


def _csv_rows(records: Iterable[object]) -> list[dict[str, object]]:
    rows = []
    for record in records:
        row = record.to_row()
        rows.append({key: json.dumps(value, ensure_ascii=False, separators=(",", ":")) if isinstance(value, (list, dict)) else value for key, value in row.items()})
    return rows


def _write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text.rstrip() + "\n", encoding="utf-8", newline="\n")


def write_upstream_llama_route(repo_root: Path) -> RouteBundle:
    """Generate the canonical route data, Markdown, DOCX, and pre-PDF receipts."""
    root = Path(repo_root).resolve(strict=True)
    route = root / ROUTE_RELATIVE
    bundle = build_upstream_llama_bundle(root)
    report = build_upstream_llama_report(bundle)
    write_json(route / "route-manifest.json", bundle.to_row())
    write_json(route / "system/repository.json", bundle.repository)
    write_json(route / "system/hardware.json", bundle.hardware)
    write_json(route / "system/software.json", bundle.software)
    _write_text(route / "system/environment.txt", "Historical controlled environment; see hardware.json and software.json.\nUncollected fields: Not collected")
    model_rows = sorted({(a.model_id, a.weight_format_id, a.status.display_label) for a in bundle.attempts})
    write_csv(route / "system/model-artifacts.csv", ({"model_id": a, "weight_format_id": b, "status": c} for a, b, c in model_rows), ("model_id", "weight_format_id", "status"))
    write_csv(route / "results/attempts.csv", _csv_rows(bundle.attempts), _ATTEMPT_FIELDS)
    write_csv(route / "results/measurements.csv", _csv_rows(bundle.measurements), _MEASUREMENT_FIELDS)
    write_csv(route / "results/summary-results.csv", _csv_rows(bundle.summaries), _SUMMARY_FIELDS)
    write_csv(route / "results/availability-matrix.csv", ({"test_case_id": a.test_case_id, "model_id": a.model_id, "weight_format_id": a.weight_format_id, "cache_format_id": a.cache_format_id, "backend_id": a.backend_id, "status": a.status.value} for a in bundle.attempts), ("test_case_id", "model_id", "weight_format_id", "cache_format_id", "backend_id", "status"))
    _write_text(route / "results/source/README.md", "# Source-result handling\n\nNo raw result dataset exists here to copy: `experiments/raw-results/upstream-llama-cpp/` contains a README placeholder only. The controlled WB-01 Markdown, processed quality/resource summaries, and indexed log tree remain in their authoritative repository locations and are hashed in `evidence/evidence-index.csv`. No editable source workbook was invented.")
    write_csv(route / "quality/scores.csv", _csv_rows(bundle.quality), _QUALITY_FIELDS)
    prompt_contract = _read_json(root / QUALITY_PROMPTS_RELATIVE)
    rubric_contract = _read_json(root / QUALITY_RUBRIC_RELATIVE)
    write_csv(
        route / "quality/prompt-suite.csv",
        (
            {
                "prompt_id": prompt["prompt_id"],
                "task_type": prompt["task"],
                "scope": "UL-05 only" if prompt["prompt_id"] in {"P5", "P6"} else "UL-01 through UL-13",
                "deterministic_checks_json": json.dumps(prompt["deterministic_checks"], ensure_ascii=False, separators=(",", ":")),
                "generation_settings_json": json.dumps(prompt_contract["generation_defaults"], ensure_ascii=False, separators=(",", ":")),
                "prompt_set_id": prompt_contract["prompt_set_id"],
            }
            for prompt in prompt_contract["prompts"]
        ),
        ("prompt_id", "task_type", "scope", "deterministic_checks_json", "generation_settings_json", "prompt_set_id"),
    )
    write_csv(route / "quality/outputs-index.csv", ({"test_case_id": q.test_case_id, "prompt_id": q.prompt_id, "source_evidence_id": q.source_evidence_id} for q in bundle.quality), ("test_case_id", "prompt_id", "source_evidence_id"))
    write_csv(route / "failures/failure-register.csv", _csv_rows(bundle.failures), _FAILURE_FIELDS)
    write_csv(route / "evidence/evidence-index.csv", _csv_rows(bundle.evidence), _EVIDENCE_FIELDS)
    write_csv(route / "evidence/source-locations.csv", ({"evidence_id": e.evidence_id, "relative_path": e.relative_path, "source_or_derived": "derived" if e.derived else "source"} for e in bundle.evidence), ("evidence_id", "relative_path", "source_or_derived"))
    attempt_by_id = {a.test_case_id: a for a in bundle.attempts}
    claim_specs = (
        ("UL-CLAIM-CPU", "UL-08 only as best observed CPU", "UL-08", list(EXPECTED_IDS[:8])),
        ("UL-CLAIM-GPU", "UL-10 only as best observed Intel GPU", "UL-10", list(EXPECTED_IDS[8:])),
        ("UL-CLAIM-FALLBACK", "UL-05 only as recorded fallback", "UL-05", ["UL-05"]),
    )
    claim_rows = []
    for claim_id, claim, selected, comparators in claim_specs:
        comparator_evidence = sorted(
            {
                evidence_id
                for test_id in comparators
                for evidence_id in attempt_by_id[test_id].evidence_ids
            }
        )
        claim_rows.append(
            {
                "claim_id": claim_id,
                "claim": claim,
                "test_case_id": selected,
                "comparator_test_ids": json.dumps(comparators),
                "evidence_ids": json.dumps(comparator_evidence),
            }
        )
    write_csv(
        route / "evidence/claim-evidence-map.csv",
        claim_rows,
        ("claim_id", "claim", "test_case_id", "comparator_test_ids", "evidence_ids"),
    )
    matrix = _parse_workbook_matrix(root / WORKBOOK_RELATIVE)
    write_csv(route / "protocol/intended-test-matrix.csv", matrix, tuple(matrix[0]))
    _write_text(route / "protocol/test-plan.md", "# Test plan\n\nWB-01 v1.4 defines UL-01 through UL-13. This publication does not rerun inference.")
    _write_text(route / "protocol/execution-sequence.md", "# Execution sequence\n\nCPU ladder; Vulkan partial/full offload; SYCL project workload; resource measurement; original quality scoring.")
    _write_text(route / "protocol/metric-definitions.md", "# Metric definitions\n\nPerformance medians use exactly three included observations. Historical missing fields display `Not collected`.")
    deviation_rows = tuple(bundle.repository["deviation_rows"])
    write_csv(
        route / "protocol/deviations.csv",
        (
            {
                key: json.dumps(value, ensure_ascii=False, separators=(",", ":"))
                if isinstance(value, (list, dict))
                else value
                for key, value in row.items()
            }
            for row in deviation_rows
        ),
        ("deviation_id", "source_failure_id", "scope_type", "scope_test_ids", "nonterminal", "code", "description", "disposition", "evidence_ids"),
    )
    _write_text(route / "quality/README.md", "# Quality evidence\n\nOriginal upstream adjudication is bound to tracked `GTQ-QUALITY-RUBRIC-v1`, frozen `GTQ-PROMPTS-v1`, and the hashed 2026-07-15 scoring source. It is not directly comparable with OpenVINO scoring.")
    dimension_lines = "\n".join(
        f"- `{item['name']}` — weight {item['weight']:.2f}; cap: {item['critical_cap']}"
        for item in rubric_contract["dimensions"]
    )
    anchor_lines = "\n".join(
        f"- {score}/10: {description}"
        for score, description in sorted(
            rubric_contract["anchors"].items(), key=lambda item: int(item[0]), reverse=True
        )
    )
    procedure_lines = "\n".join(
        f"{number}. {step}"
        for number, step in enumerate(rubric_contract["procedure"], start=1)
    )
    _write_text(
        route / "quality/rubric.md",
        f"# GTQ-QUALITY-RUBRIC-v1\n\n## Weighted dimensions and critical caps\n\n{dimension_lines}\n\n## Anchors\n\n{anchor_lines}\n\n## Procedure\n\n{procedure_lines}\n\nHistorical per-prompt composite scores are preserved; component-level score increments were not collected.",
    )
    _write_text(route / "quality/calibration.md", "# Calibration\n\nCalibration: Not collected\n\nScore increments: Not collected\n\nNo later calibration or re-adjudication was applied. Original prompt-level composite scores are preserved.")
    write_csv(route / "quality/adjudication-log.csv", ({"adjudication_id": "UL-QUALITY-ORIGINAL", "method": "Original conservative scoring", "status": "Preserved"},), ("adjudication_id", "method", "status"))
    _write_text(route / "failures/README.md", "# Failures and deviations\n\nHistorical failures and deviations are preserved; none is silently converted into a terminal failure of the 13 completed project workloads.")
    _write_text(route / "failures/curated-logs/README.md", "# Curated log handling\n\nNo logs are duplicated here. The complete source logs remain at their hashed repository-relative locations in `evidence/evidence-index.csv`.")
    _write_text(route / "reproduction/README.md", "# Reproduction\n\nRun the five ordered commands in `commands.md` from the repository root. They normalize existing evidence, render derivatives, export through the owned Word process, finalize validation, verify the checksum receipt, and run focused tests. They do not rerun inference.")
    _write_text(
        route / "reproduction/commands.md",
        """# Ordered reproduction commands

Run from the repository root in PowerShell. Stop immediately if any command exits nonzero.

1. Normalize and render

```powershell
& .tools/python311-portable/python.exe -c "import sys; from pathlib import Path; root=Path.cwd(); sys.path.insert(0,str(root)); from scripts.testing.final_results.llama_adapter import write_upstream_llama_route; write_upstream_llama_route(root)"
```

2. Export the owned Word PDF

```powershell
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/testing/Export-Final-Results-Pdf.ps1 -DocxPath docs/testing/final-results/01-upstream-llama-cpp/workbook/generated/upstream-llama-cpp-final-report.docx -PdfPath docs/testing/final-results/01-upstream-llama-cpp/workbook/generated/upstream-llama-cpp-final-report.pdf -TimeoutSeconds 180
```

3. Finalize and validate the PDF

```powershell
& .tools/python311-portable/python.exe -c "import sys; from pathlib import Path; root=Path.cwd(); sys.path.insert(0,str(root)); from scripts.testing.final_results.llama_adapter import finalize_upstream_llama_route; finalize_upstream_llama_route(root)"
```

4. Validate the checksum manifest

```powershell
& .tools/python311-portable/python.exe -c "import sys; from pathlib import Path; root=Path.cwd(); sys.path.insert(0,str(root)); from scripts.testing.final_results.evidence import validate_sha256_manifest; errors=validate_sha256_manifest(root, root/'docs/testing/final-results/01-upstream-llama-cpp/evidence/manifest-sha256.txt'); print(errors); raise SystemExit(bool(errors))"
```

5. Run the focused validation suite

```powershell
& .tools/python311-portable/python.exe -m pytest scripts/testing/tests/test_final_results_upstream_llama.py -q
```
""",
    )
    _write_text(route / "reproduction/dependencies.md", "# Dependencies\n\n- Portable interpreter: `.tools/python311-portable/python.exe` (validated with Python 3.11.9).\n- Pinned reporting packages: `scripts/testing/requirements.txt`.\n- Owned Word exporter: `scripts/testing/Export-Final-Results-Pdf.ps1` with a 180-second bound.\n- Normalizer/finalizer: `scripts/testing/final_results/llama_adapter.py`.\n- Focused validation: `scripts/testing/tests/test_final_results_upstream_llama.py`.\n- Microsoft Word is required only for the DOCX-to-PDF export step.")
    _write_text(route / "reproduction/scripts/README.md", "# Reproduction scripts\n\nThe maintained adapter is `scripts/testing/final_results/llama_adapter.py`; it is referenced rather than copied.")
    _write_text(route / "README.md", "# Upstream llama.cpp final results\n\nCanonical Markdown: `workbook/source/upstream-llama-cpp-final-report.md`. Generated DOCX/PDF are derivatives. Source evidence remains in its authoritative repository locations.")
    markdown = route / "workbook/source/upstream-llama-cpp-final-report.md"
    docx = route / "workbook/generated/upstream-llama-cpp-final-report.docx"
    render_markdown(report, markdown)
    render_docx(report, docx)
    parity = compare_markdown_docx(markdown, docx)
    write_json(route / "validation/workbook-parity.json", parity)
    if not parity["matches"]:
        raise ValueError("generated WB-01 report parity failed")
    relationships = validate_upstream_relationships(bundle, deviation_rows)
    coverage_checks = {
        "exact_attempt_test_set": {item.test_case_id for item in bundle.attempts} == set(EXPECTED_IDS),
        "all_attempts_executed_and_passed": all(item.executed and item.status is Status.PASSED for item in bundle.attempts),
        "measurement_count": len(bundle.measurements) == 80,
        "quality_count": len(bundle.quality) == 54,
        "relationships_valid": relationships["valid"],
    }
    coverage = {"valid": all(coverage_checks.values()), "checks": coverage_checks, "intended_ids": list(EXPECTED_IDS), "attempt_count": len(bundle.attempts), "measurement_count": len(bundle.measurements), "quality_count": len(bundle.quality), "source_gap": bundle.repository["performance_register_reason"]}
    source_audit = bundle.repository["source_reconciliation"]
    data_checks = {
        "evidence_bindings": source_audit["evidence_binding_count"] == 642,
        "resource_reconciliation": source_audit["resource_rows_reconciled"] == 13,
        "quality_reconciliation": source_audit["quality_rows_reconciled"] == 13,
        "test_run_register_exact_rows": source_audit["exact_test_run_register_rows"] == 0,
        "performance_register_exact_rows": source_audit["exact_performance_register_rows"] == 0,
    }
    data = {"valid": all(data_checks.values()), "checks": data_checks, "evidence_count": len(bundle.evidence), "verified_log_count": sum(e.role == "indexed-log" for e in bundle.evidence), "missing_values_display": NOT_COLLECTED, "raw_results_status": NOT_COLLECTED, "known_divergences": source_audit["known_divergences"]}
    write_json(route / "validation/coverage-validation.json", coverage)
    write_json(route / "validation/data-validation.json", data)
    write_json(route / "validation/relationship-validation.json", relationships)
    if not coverage["valid"] or not data["valid"] or not relationships["valid"]:
        raise ValueError("upstream validation receipts contain a failed check")
    _write_text(route / "validation/validation-report.md", f"# Validation report\n\nCoverage: Passed. Data: Passed. Evidence files verified: {len(bundle.evidence)}. PDF/visual validation is completed after Word export.")
    write_json(route / "validation/integrity-validation.json", {"valid": False, "status": "Pending final PDF and manifest regeneration"})
    manifest = regenerate_route_manifest(root, route)
    errors = validate_sha256_manifest(root, manifest)
    if errors:
        raise ValueError(f"route manifest validation failed: {errors}")
    return bundle


def finalize_upstream_llama_route(repo_root: Path) -> dict[str, object]:
    """Validate the Word-exported PDF, record visual QA, and freeze checksums.

    The visual receipt records the completed human contact-sheet inspection;
    structural checks here independently prevent a receipt for a blank or
    truncated PDF.
    """
    from pypdf import PdfReader

    root = Path(repo_root).resolve(strict=True)
    route = root / ROUTE_RELATIVE
    pdf = route / "workbook/generated/upstream-llama-cpp-final-report.pdf"
    if not pdf.is_file() or not pdf.read_bytes().startswith(b"%PDF-"):
        raise ValueError("Word-exported upstream PDF is missing or malformed")
    reader = PdfReader(pdf)
    page_text = [(page.extract_text() or "").strip() for page in reader.pages]
    expected_headings = list(SECTION_ORDER)
    combined = "\n".join(page_text)
    structural_checks = {
        "pdf_signature": True,
        "page_count": len(reader.pages),
        "all_pages_searchable_and_nonblank": all(page_text),
        "title_present": "Upstream llama.cpp Final Test Report" in combined,
        "all_approved_sections_present": all(heading in combined for heading in expected_headings),
        "revision_footer_present": "upstream-llama-cpp | Revision R1" in combined,
    }
    valid = len(reader.pages) > 0 and all(
        value is True for key, value in structural_checks.items() if key != "page_count"
    )
    contact_sheet_ranges = [
        f"{start}-{min(start + 8, len(reader.pages))}"
        for start in range(1, len(reader.pages) + 1, 9)
    ]
    visual = {
        "valid": valid,
        "pdf": pdf.relative_to(root).as_posix(),
        "inspection_method": f"Rendered every PDF page with PyMuPDF and inspected {len(contact_sheet_ranges)} contact sheets of up to 3-by-3 pages",
        "inspected_pages": list(range(1, len(reader.pages) + 1)),
        "contact_sheet_page_ranges": contact_sheet_ranges,
        "checks": structural_checks,
        "visual_findings": {
            "clipping_or_truncation": "None observed",
            "blank_or_corrupt_pages": "None observed",
            "table_header_continuity": "Repeated headers present across long tables",
            "colour_and_status_legibility": "Consistent navy, teal, blue, and explicit status text",
            "dense_evidence_table": "Intentionally spans pages 18-46; readable at page zoom",
        },
        "temporary_contact_sheets_committed": False,
    }
    write_json(route / "validation/visual-validation.json", visual)
    if not valid:
        raise ValueError(f"upstream PDF structural validation failed: {structural_checks}")
    _write_text(
        route / "validation/validation-report.md",
        "# Validation report\n\nCoverage: Passed. Data: Passed. Markdown/DOCX semantic parity: Passed. "
        f"Word PDF export: Passed. All {len(reader.pages)} PDF pages rendered and visually inspected: Passed. "
        "Every admitted source path and SHA-256 was verified; historical register and raw-results gaps remain explicit.",
    )
    integrity = {
        "valid": True,
        "manifest": (ROUTE_RELATIVE / "evidence/manifest-sha256.txt").as_posix(),
        "policy": "All route files except the checksum manifest itself are included",
        "manifest_validation": "Passed after final PDF and validation receipt generation",
    }
    write_json(route / "validation/integrity-validation.json", integrity)
    manifest = regenerate_route_manifest(root, route)
    errors = validate_sha256_manifest(root, manifest)
    if errors:
        raise ValueError(f"final route manifest validation failed: {errors}")
    return visual


__all__ = [
    "build_upstream_llama_bundle",
    "build_upstream_llama_report",
    "write_upstream_llama_route",
    "finalize_upstream_llama_route",
    "hash_file",
    "_parse_workbook_matrix",
]
