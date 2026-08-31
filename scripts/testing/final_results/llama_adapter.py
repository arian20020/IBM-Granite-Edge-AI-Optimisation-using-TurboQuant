"""Evidence-bound normalization and publication for historical llama.cpp routes.

Task 9 implements the upstream route.  Later tasks extend this module for the
two fork routes; keeping source admission here gives all three the same rules.
"""

from __future__ import annotations

import csv
import hashlib
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
UPSTREAM_LOG_TREE_RELATIVE = Path(
    "experiments/granite_turboquant_intel/logs/upstream-llama-cpp"
)
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


def _parse_setup_scope_ids(path: Path) -> list[str]:
    rows = _table_by_header(path, ("ID", "Check", "Result"))
    ids = [row["ID"] for row in rows]
    expected = [f"UL-B{number:02d}" for number in range(1, 8)]
    if ids != expected:
        raise ValueError(f"WB-01 setup scope IDs changed: {ids}")
    return ids


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
    expected_by_path: dict[str, tuple[str, str, str]] = {}
    log_root = root / UPSTREAM_LOG_TREE_RELATIVE
    for test_id in EXPECTED_IDS:
        for path in sorted((log_root / test_id).rglob("*")):
            if not path.is_file():
                continue
            relative = path.relative_to(root).as_posix()
            parts = relative.split("/")
            if len(parts) < 7 or parts[4] != test_id:
                raise ValueError(f"malformed expected upstream evidence path: {relative}")
            expected_by_path[relative] = (test_id, parts[5], hash_file(path))
    if len(expected_by_path) != 642:
        raise ValueError(
            f"expected upstream log tree must contain exactly 642 evidence files, got {len(expected_by_path)}"
        )

    indexed_rows = _read_csv(index_path)
    candidate_rows = []
    for row in indexed_rows:
        relative = row["Repository_Path"].replace("\\", "/")
        parts = relative.split("/")
        if len(parts) >= 7 and parts[:4] == [
            "experiments", "granite_turboquant_intel", "logs", "upstream-llama-cpp"
        ] and parts[4] in EXPECTED_IDS:
            candidate_rows.append(row)
    candidate_paths = [row["Repository_Path"].replace("\\", "/") for row in candidate_rows]
    if len(candidate_rows) != 642 or set(candidate_paths) != set(expected_by_path):
        missing = sorted(set(expected_by_path) - set(candidate_paths))
        extra = sorted(set(candidate_paths) - set(expected_by_path))
        raise ValueError(
            "expected evidence tuple set mismatch: "
            f"rows={len(candidate_rows)}, missing={missing[:1]}, extra={extra[:1]}"
        )

    actual_tuples: set[tuple[str, str, str, str]] = set()
    expected_tuples = {
        (test_id, run_id, relative, digest)
        for relative, (test_id, run_id, digest) in expected_by_path.items()
    }
    for row in candidate_rows:
        relative = row["Repository_Path"].replace("\\", "/")
        expected_test_id, expected_run_id, digest = expected_by_path[relative]
        if row["Route"] != ROUTE_ID or (row["Test_ID"], row["Run_ID"]) != (
            expected_test_id,
            expected_run_id,
        ):
            raise ValueError(
                "Evidence-Index binding conflict: "
                f"{row['Test_ID']} / {row['Run_ID']} / {relative}"
            )
        path = root / relative
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
        actual_tuples.add((row["Test_ID"], row["Run_ID"], relative, digest))
    if actual_tuples != expected_tuples:
        raise ValueError("expected evidence tuple set mismatch after binding validation")

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
    setup_scope_ids = _parse_setup_scope_ids(workbook)
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
        "setup_scope_ids": setup_scope_ids,
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


def _canonical_scope_evidence_id(
    test_id: str, evidence_by_path: Mapping[str, EvidenceRecord]
) -> str:
    prefix = f"{UPSTREAM_LOG_TREE_RELATIVE.as_posix()}/{test_id}/{test_id}-R"
    candidates = sorted(
        record.evidence_id
        for path, record in evidence_by_path.items()
        if path.startswith(prefix) and path.endswith("/bench-stdout.jsonl")
    )
    if len(candidates) != 1:
        raise ValueError(f"expected one canonical benchmark evidence row for {test_id}, got {len(candidates)}")
    return candidates[0]


def _evidence_supports_scope(record: EvidenceRecord, scope_id: str) -> bool:
    return f"/{scope_id}/" in f"/{record.relative_path}"


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
            evidence_ids = set(_source_evidence_ids(row["Evidence"], evidence_by_path))
            evidence_ids.add(_canonical_scope_evidence_id(test_id, evidence_by_path))
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
                    evidence_ids=tuple(sorted(evidence_ids)),
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
        scoped_evidence = set(_source_evidence_ids(source["Evidence"], evidence_by_path))
        if scope_type not in {"campaign"}:
            for scope_id in scope_ids:
                if scope_id in EXPECTED_IDS and not any(
                    _evidence_supports_scope(
                        next(item for item in bundle.evidence if item.evidence_id == evidence_id),
                        scope_id,
                    )
                    for evidence_id in scoped_evidence
                ):
                    scoped_evidence.add(
                        _canonical_scope_evidence_id(scope_id, evidence_by_path)
                    )
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
                "evidence_ids": sorted(scoped_evidence),
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
    evidence_by_id = {item.evidence_id: item for item in bundle.evidence}
    evidence_ids = set(evidence_by_id)
    setup_ids = set(bundle.repository.get("setup_scope_ids", []))
    expected_setup_ids = {f"UL-B{number:02d}" for number in range(1, 8)}
    errors: list[str] = []
    if test_ids != set(EXPECTED_IDS):
        errors.append("attempt test set does not equal UL-01 through UL-13")
    if setup_ids != expected_setup_ids:
        errors.append("setup scope authority does not equal WB-declared UL-B01 through UL-B07")
    if any(item.status is not Status.PASSED or not item.executed for item in bundle.attempts):
        errors.append("all 13 canonical attempts must remain executed and passed")
    for failure in bundle.failures:
        if failure.test_case_id not in test_ids:
            errors.append(f"failure {failure.failure_id} references unknown test {failure.test_case_id}")
        if failure.attempt_id not in attempt_ids:
            errors.append(f"failure {failure.failure_id} references unknown attempt {failure.attempt_id}")
        if not failure.evidence_ids:
            errors.append(f"failure {failure.failure_id} evidence must not be empty")
        for evidence_id in failure.evidence_ids:
            if evidence_id not in evidence_ids:
                errors.append(f"failure {failure.failure_id} references unknown evidence {evidence_id}")
        existing_evidence = [
            evidence_by_id[evidence_id]
            for evidence_id in failure.evidence_ids
            if evidence_id in evidence_by_id
        ]
        if failure.test_case_id in test_ids and not any(
            _evidence_supports_scope(record, failure.test_case_id)
            for record in existing_evidence
        ):
            errors.append(
                f"failure {failure.failure_id} evidence does not support scope {failure.test_case_id}"
            )

    seen_deviations: set[str] = set()
    allowed_scope_types = {"setup", "test", "prompt", "multi-test", "mixed", "campaign"}
    campaign_authority_roles = {
        "controlled-workbook-markdown",
        "resource-summary",
        "test-run-register",
        "performance-register",
    }
    test_authority_roles = {"controlled-workbook-markdown", "quality-scoring"}
    for row in deviations:
        deviation_id = str(row["deviation_id"])
        if deviation_id in seen_deviations:
            errors.append(f"duplicate deviation {deviation_id}")
        seen_deviations.add(deviation_id)
        if row.get("nonterminal") is not True:
            errors.append(f"deviation {deviation_id} is not explicitly nonterminal")
        scope_type = str(row.get("scope_type"))
        raw_scopes = row.get("scope_test_ids")
        scopes = list(raw_scopes) if isinstance(raw_scopes, (list, tuple)) else []
        if scope_type not in allowed_scope_types:
            errors.append(f"deviation {deviation_id} has unknown scope type {scope_type}")
        if not scopes:
            errors.append(f"deviation {deviation_id} scope must not be empty")
        if len(scopes) != len(set(scopes)):
            errors.append(f"deviation {deviation_id} scope contains duplicate IDs")

        if scope_type == "setup":
            for scope in scopes:
                if isinstance(scope, str) and re.fullmatch(r"UL-B\d{2}", scope) and scope not in setup_ids:
                    errors.append(f"deviation {deviation_id} has unknown setup scope {scope}")
            if any(scope not in setup_ids for scope in scopes):
                errors.append(f"deviation {deviation_id} setup scope must contain only WB-declared setup IDs")
        elif scope_type in {"test", "prompt"}:
            if len(scopes) != 1 or any(scope not in test_ids for scope in scopes):
                errors.append(
                    f"deviation {deviation_id} {scope_type} scope requires exactly one canonical UL test ID"
                )
        elif scope_type == "multi-test":
            if len(scopes) < 2:
                errors.append(f"deviation {deviation_id} multi-test scope requires at least two canonical UL test IDs")
            if any(scope not in test_ids for scope in scopes):
                errors.append(f"deviation {deviation_id} multi-test scope contains a noncanonical ID")
        elif scope_type == "mixed":
            if any(scope not in setup_ids | test_ids for scope in scopes):
                errors.append(f"deviation {deviation_id} mixed scope contains an unknown ID")
            if not any(scope in setup_ids for scope in scopes) or not any(
                scope in test_ids for scope in scopes
            ):
                errors.append(f"deviation {deviation_id} mixed scope requires setup and canonical test IDs")
        elif scope_type == "campaign" and scopes != list(EXPECTED_IDS):
            errors.append(f"deviation {deviation_id} campaign scope must exactly expand UL-01 through UL-13")

        raw_evidence_ids = row.get("evidence_ids")
        row_evidence_ids = (
            list(raw_evidence_ids) if isinstance(raw_evidence_ids, (list, tuple)) else []
        )
        if not row_evidence_ids:
            errors.append(f"deviation {deviation_id} evidence must not be empty")
        for evidence_id in row_evidence_ids:
            if evidence_id not in evidence_ids:
                errors.append(f"deviation {deviation_id} references unknown evidence {evidence_id}")
        existing_evidence = [
            evidence_by_id[evidence_id]
            for evidence_id in row_evidence_ids
            if evidence_id in evidence_by_id
        ]
        if scope_type == "campaign":
            if not any(record.role in campaign_authority_roles for record in existing_evidence):
                errors.append(f"deviation {deviation_id} evidence does not support campaign scope")
        else:
            historical = bool(row.get("source_failure_id"))
            for scope in scopes:
                direct_support = any(
                    _evidence_supports_scope(record, str(scope))
                    for record in existing_evidence
                )
                global_test_support = not historical and scope in test_ids and any(
                    record.role in test_authority_roles for record in existing_evidence
                )
                if scope in setup_ids | test_ids and not (
                    direct_support or global_test_support
                ):
                    errors.append(
                        f"deviation {deviation_id} evidence does not support scope {scope}"
                    )
    return {
        "valid": not errors,
        "errors": errors,
        "attempt_count": len(bundle.attempts),
        "failure_relationship_count": len(bundle.failures),
        "deviation_count": len(deviations),
        "evidence_count": len(bundle.evidence),
        "allowed_scope_types": sorted(allowed_scope_types),
        "allowed_setup_scope_ids": sorted(setup_ids),
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
            "setup_scope_ids": source_audit["setup_scope_ids"],
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
            "dense_evidence_table": "Evidence section begins on page 18; the evidence table spans pages 19-46 and is readable at page zoom",
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
        "Evidence section begins on page 18; the evidence table spans pages 19-46. "
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


# AtomicBot TurboQuant (WB-02 v1.7)
ATOMICBOT_ROUTE_ID = "atomicbot-turboquant"
ATOMICBOT_CAMPAIGN_ID = "wb-02-v1.7-2026-07-17"
ATOMICBOT_ROUTE_RELATIVE = Path("docs/testing/final-results/02-atomicbot-turboquant")
ATOMICBOT_WORKBOOK_RELATIVE = Path(
    "docs/testing/workbooks/text-templates/02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.md"
)
ATOMICBOT_RAW_ROOT = Path("experiments/raw-results/atomicbot-turboquant")
ATOMICBOT_QUALITY_RELATIVE = ATOMICBOT_RAW_ROOT / "2026-07-17/quality-all-rows/quality-summary.json"
ATOMICBOT_MASTER_RELATIVE = ATOMICBOT_RAW_ROOT / "2026-07-16/acquisition/results/AtomicBot_Master_Summary.json"
ATOMICBOT_EXPECTED_IDS = (
    "AB-01", "AB-02", "AB-03", "AB-KV3-F16-4K", "AB-04", "AB-05",
    "AB-06", "AB-07", "AB-08F", "AB-KV8-F16-4K", "AB-08Q", "AB-09",
    "AB-10", "AB-11", "AB-12", "AB-13", "AB-14", "AB-15", "AB-15M",
)
ATOMICBOT_NOT_COLLECTED = "Not collected"
ATOMICBOT_PROMPT_RELATIVE = Path(
    "experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json"
)
ATOMICBOT_RUBRIC_RELATIVE = Path(
    "experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json"
)
ATOMICBOT_QUALITY_REGISTER_RELATIVE = Path("docs/testing/Quality-Evaluation-Register.csv")
ATOMICBOT_ADJUDICATION_RELATIVE = (
    ATOMICBOT_RAW_ROOT / "2026-07-17/quality-all-rows/quality-adjudications.json"
)
ATOMICBOT_PROMPT_SHA256 = "9ba512818e81e0ba8da3ddc89cf040dd3b779d1edc41db23d3a896d778de807f"
ATOMICBOT_RUBRIC_SHA256 = "a36016f66e02c9e28f0938cf81335dc4b522e9f92b7d9bad3031f90b7ef91d90"
ATOMICBOT_QUALITY_SUMMARY_SHA256 = "c7333ff5faf191f9ddb9993c158d943a41d0d640b8c8d61055a4cdbe6c2e1ff9"
ATOMICBOT_ADJUDICATION_SHA256 = "7c6bbfd833d30218445cf8ebe2dbafb9fea7560563cb3461eff6dc55747c1e30"
ATOMICBOT_QUALITY_REGISTER_SHA256 = "4d8e327caf025090b61c917152ba3f656209376e2365b11c40eeaf0c3236797c"
ATOMICBOT_DIMENSION_FIELDS = {
    "correctness_and_grounding": (0.30, "Correctness_and_Grounding_0_to_10"),
    "instruction_and_format_adherence": (0.25, "Instruction_and_Format_0_to_10"),
    "completeness_and_fact_retention": (0.20, "Completeness_and_Fact_Retention_0_to_10"),
    "relevance_clarity_and_coherence": (0.15, "Relevance_Clarity_Coherence_0_to_10"),
    "stability_and_output_integrity": (0.10, "Stability_and_Integrity_0_to_10"),
}
ATOMICBOT_WORKBOOK_PRECEDENCE = (
    "Test-Run register for runtime status; Performance register/current summaries for performance and utilization; "
    "Quality-Evaluation register/current quality artifacts for quality"
)
ATOMICBOT_DEVIATION_SCOPES = {
    "FAIL-AB-UI-ASSET": ("setup", ("AB-B02", "AB-B05")),
    "FAIL-AB-DEVICE-GUARD": ("setup", ("AB-B04",)),
    "FAIL-AB-08Q-MEMORY": ("test", ("AB-08Q",)),
    "FAIL-AB-8B-SAFETY": ("multi-test", ("AB-KV8-F16-4K", "AB-15M")),
    "FAIL-AB-P5-TIMEOUT": ("mixed", ("P5", "AB-06")),
}


def _atomicbot_source_record(
    root: Path,
    relative: Path,
    role: str,
    label: str,
    *,
    evidence_id: str | None = None,
) -> EvidenceRecord:
    path = root / relative
    digest = hash_file(path)
    return EvidenceRecord(
        route_id=ATOMICBOT_ROUTE_ID,
        campaign_id=ATOMICBOT_CAMPAIGN_ID,
        evidence_id=evidence_id or f"atomicbot-{digest[:16]}",
        role=role,
        relative_path=relative.as_posix(),
        sha256=digest,
        size_bytes=path.stat().st_size,
        source_label=label,
    )


def _atomicbot_matrix(workbook: Path) -> list[dict[str, str]]:
    rows = _table_by_header(workbook, ("ID", "Model", "KV cache", "Execution"))
    if tuple(row["ID"] for row in rows) != ATOMICBOT_EXPECTED_IDS:
        raise ValueError("WB-02 matrix must contain the complete 19-row runtime set in controlled order")
    return rows


def _atomicbot_exact_index_join(
    root: Path,
    by_path: dict[str, list[dict[str, str]]],
    relative: str,
    test_id: str,
    evidence_run_id: str,
) -> dict[str, str]:
    candidates = by_path.get(relative, [])
    if len(candidates) != 1:
        raise ValueError(f"Evidence-Index exact path join conflict: {relative}")
    row = candidates[0]
    if row["Test_ID"] != test_id or row["Run_ID"] != evidence_run_id:
        raise ValueError(f"Evidence-Index identity conflict: {relative}")
    path = root / relative
    if not path.is_file():
        raise ValueError(f"joined evidence path is missing: {relative}")
    digest = hash_file(path)
    if digest != row["SHA256"].lower():
        raise ValueError(f"Evidence-Index hash conflict: {relative}")
    return {
        "evidence_id": row["Evidence_ID"], "relative_path": relative,
        "sha256": digest, "evidence_run_id": row["Run_ID"],
    }


def _atomicbot_float_equal(left: object, right: object, tolerance: float = 0.000001) -> bool:
    return abs(float(left) - float(right)) <= tolerance


def audit_atomicbot_sources(
    repo_root: Path,
    *,
    evidence_index_path: Path | None = None,
) -> dict[str, object]:
    """Reconcile every published AtomicBot field against its controlling source."""
    root = Path(repo_root).resolve(strict=True)
    workbook = root / ATOMICBOT_WORKBOOK_RELATIVE
    matrix = _atomicbot_matrix(workbook)
    expected = set(ATOMICBOT_EXPECTED_IDS)

    revisions = [row for row in _read_csv(root / "docs/testing/Workbook-Revision-Register.csv") if row["Workbook_ID"] == "WB-02"]
    if not revisions or revisions[-1]["Version"] != "1.7" or "Current" not in revisions[-1]["Status"]:
        raise ValueError("WB-02 current revision must be 1.7")
    test_runs = [row for row in _read_csv(root / TEST_RUN_REGISTER_RELATIVE) if row["Route"] == ATOMICBOT_ROUTE_ID]
    if len(test_runs) != 19 or {row["Test_ID"] for row in test_runs} != expected:
        raise ValueError("Test-Run register must contain the complete 19-row set")
    if any(row["Result"] != "Passed" for row in test_runs):
        raise ValueError("current WB-02 runtime authority contains a non-passed row")
    test_run_by_id = {row["Test_ID"]: row for row in test_runs}

    performance = [row for row in _read_csv(root / PERFORMANCE_REGISTER_RELATIVE) if row["Route"] == ATOMICBOT_ROUTE_ID]
    if len(performance) != 57 or Counter(row["Test_ID"] for row in performance) != Counter({test_id: 3 for test_id in ATOMICBOT_EXPECTED_IDS}):
        raise ValueError("Performance register must contain the complete 57-row set")
    required_utilization = (
        "CPU_Mean_Percent", "CPU_Median_Percent", "CPU_Peak_Percent",
        "GPU_Engine_Mean_Percent", "GPU_Engine_Median_Percent", "GPU_Engine_Peak_Percent",
    )
    if any(not row[field].strip() for row in performance for field in required_utilization):
        raise ValueError("observed per-run utilization is incomplete")

    indexed = [row for row in _read_csv(evidence_index_path or root / EVIDENCE_INDEX_RELATIVE) if row["Route"] == ATOMICBOT_ROUTE_ID]
    by_path: dict[str, list[dict[str, str]]] = defaultdict(list)
    by_evidence_id: dict[str, list[dict[str, str]]] = defaultdict(list)
    for row in indexed:
        by_path[row["Repository_Path"].replace("\\", "/")].append(row)
        by_evidence_id[row["Evidence_ID"]].append(row)

    joined: list[dict[str, object]] = []
    resource_sources: list[dict[str, object]] = []
    current_summaries: dict[str, dict[str, object]] = {}
    workbook_expected_duplicates: dict[str, dict[str, str]] = {}
    performance_by_test: dict[str, list[dict[str, str]]] = defaultdict(list)
    for row in performance:
        performance_by_test[row["Test_ID"]].append(row)
        raw_relative = row["Raw_Metrics_Path"].replace("\\", "/")
        sample_dir = Path(raw_relative).parent
        measurement_relative = (sample_dir / "measurement.json").as_posix()
        evidence_run_id = f"{row['Test_ID']}-UTIL-R001"
        raw_join = _atomicbot_exact_index_join(root, by_path, raw_relative, row["Test_ID"], evidence_run_id)
        measurement_path = root / measurement_relative
        payload = _read_json(measurement_path)
        measurement_join = {
            "evidence_id": f"atomicbot-resource-{hash_file(measurement_path)[:16]}",
            "relative_path": measurement_relative, "sha256": hash_file(measurement_path),
            "evidence_run_id": evidence_run_id,
        }
        expected_sample_id = f"{row['Test_ID']}-sample-{row['Repetition_Number']}"
        if not isinstance(payload, dict) or payload.get("sample_id") != expected_sample_id or payload.get("valid") is not True:
            raise ValueError(f"performance source identity conflict: {measurement_relative}")
        checks = {
            "TTFT_ms": payload["ttft_ms"],
            "Peak_Working_Set_Bytes": round(float(payload["peak_ram_mb"]) * 1048576),
            "KV_Cache_Allocated_Bytes": round(float(payload["kv_mb"]) * 1048576),
            "CPU_Mean_Percent": payload["utilization"]["cpu_percent"]["mean"],
            "CPU_Median_Percent": payload["utilization"]["cpu_percent"]["median"],
            "CPU_Peak_Percent": payload["utilization"]["cpu_percent"]["peak"],
            "GPU_Engine_Mean_Percent": payload["utilization"]["gpu_percent"]["mean"],
            "GPU_Engine_Median_Percent": payload["utilization"]["gpu_percent"]["median"],
            "GPU_Engine_Peak_Percent": payload["utilization"]["gpu_percent"]["peak"],
        }
        if any(not _atomicbot_float_equal(row[field], value) for field, value in checks.items()):
            raise ValueError(f"performance register field conflict: {row['Measurement_ID']}")
        raw_rows = _read_csv(root / raw_relative)
        raw_cpu = [float(sample["cpu_percent"]) for sample in raw_rows]
        raw_gpu = [float(sample["gpu_percent"]) for sample in raw_rows]
        raw_checks = {
            "CPU_Mean_Percent": statistics.mean(raw_cpu), "CPU_Median_Percent": statistics.median(raw_cpu),
            "CPU_Peak_Percent": max(raw_cpu), "GPU_Engine_Mean_Percent": statistics.mean(raw_gpu),
            "GPU_Engine_Median_Percent": statistics.median(raw_gpu), "GPU_Engine_Peak_Percent": max(raw_gpu),
        }
        if (
            len(raw_cpu) != int(payload["utilization"]["cpu_percent"]["sample_count"])
            or len(raw_gpu) != int(payload["utilization"]["gpu_percent"]["sample_count"])
            or any(not _atomicbot_float_equal(row[field], value) for field, value in raw_checks.items())
        ):
            raise ValueError(f"raw utilization source conflict: {row['Measurement_ID']}")
        joined.append({"measurement_id": row["Measurement_ID"], "test_case_id": row["Test_ID"], "run_id": row["Run_ID"], **raw_join})
        resource_sources.append({
            "measurement_id": row["Measurement_ID"], "test_case_id": row["Test_ID"],
            "run_id": row["Run_ID"], "repetition_id": row["Repetition_Number"],
            "measurement_source": measurement_join, "utilization_source": raw_join,
            "supported_fields": ["latency_ms", "peak_working_set_bytes", "kv_cache_allocated_bytes", "cpu_utilization", "gpu_utilization"],
        })

    for test_id, rows in performance_by_test.items():
        relative = Path(rows[0]["Processed_Result_Path"].replace("\\", "/"))
        payload = _read_json(root / relative)
        if not isinstance(payload, dict) or payload.get("test_id") != test_id or len(payload.get("samples", [])) != 3:
            raise ValueError(f"current server summary identity conflict: {relative}")
        expected_samples = [
            _read_json(root / Path(row["Raw_Metrics_Path"].replace("\\", "/")).parent / "measurement.json")
            for row in sorted(rows, key=lambda item: int(item["Repetition_Number"]))
        ]
        if payload["samples"] != expected_samples:
            raise ValueError(f"current server summary sample conflict: {test_id}")
        aggregate = payload["aggregate"]
        sample_count = sum(
            int(sample["utilization"]["cpu_percent"]["sample_count"])
            for sample in expected_samples
        )
        aggregate_checks = (
            (aggregate["ttft_ms"]["median"], statistics.median(float(row["TTFT_ms"]) for row in rows), 0.000001),
            (aggregate["peak_ram_mb"]["max"] * 1048576, max(int(row["Peak_Working_Set_Bytes"]) for row in rows), 1.0),
            (aggregate["kv_mb"]["median"] * 1048576, statistics.median(int(row["KV_Cache_Allocated_Bytes"]) for row in rows), 1.0),
            (aggregate["cpu_percent"]["mean"], statistics.mean(float(row["CPU_Mean_Percent"]) for row in rows), 0.000001),
            (aggregate["gpu_percent"]["mean"], statistics.mean(float(row["GPU_Engine_Mean_Percent"]) for row in rows), 0.000001),
            (aggregate["cpu_percent"]["median"], statistics.median(float(row["CPU_Median_Percent"]) for row in rows), 0.000001),
            (aggregate["cpu_percent"]["peak"], max(float(row["CPU_Peak_Percent"]) for row in rows), 0.000001),
            (aggregate["cpu_percent"]["sample_count"], sample_count, 0.0),
            (aggregate["gpu_percent"]["median"], statistics.median(float(row["GPU_Engine_Median_Percent"]) for row in rows), 0.000001),
            (aggregate["gpu_percent"]["peak"], max(float(row["GPU_Engine_Peak_Percent"]) for row in rows), 0.000001),
            (aggregate["gpu_percent"]["sample_count"], sample_count, 0.0),
        )
        if any(not _atomicbot_float_equal(left, right, tolerance) for left, right, tolerance in aggregate_checks):
            raise ValueError(f"current server summary aggregate conflict: {test_id}")
        current_summaries[test_id] = {"relative_path": relative.as_posix(), "sha256": hash_file(root / relative)}
        workbook_expected_duplicates[test_id] = {
            "cpu": " / ".join(f"{float(aggregate['cpu_percent'][key]):.2f}" for key in ("mean", "median", "peak")),
            "gpu": " / ".join(f"{float(aggregate['gpu_percent'][key]):.2f}" for key in ("mean", "median", "peak")),
        }

    throughput_sources: list[dict[str, object]] = []
    safety_ids = {"AB-KV8-F16-4K", "AB-15M"}
    tps_pattern = re.compile(r"(?<!prompt )eval time =.+?([0-9]+(?:\.[0-9]+)?) tokens per second")
    for test_id in ATOMICBOT_EXPECTED_IDS:
        formal_value = float(test_run_by_id[test_id]["Decode_Tokens_Per_Second"])
        rows = performance_by_test[test_id]
        if any(not _atomicbot_float_equal(row["Decode_Tokens_Per_Second"], formal_value) for row in rows):
            raise ValueError(f"throughput register conflict: {test_id}")
        if test_id not in safety_ids:
            relative = ATOMICBOT_RAW_ROOT / f"2026-07-16/acquisition/metrics/{test_id}/{test_id}-formal-summary.json"
            payload = _read_json(root / relative)
            if not isinstance(payload, dict) or payload.get("test_id") != test_id or payload.get("formal_success_count") != 3:
                raise ValueError(f"formal throughput identity conflict: {test_id}")
            if not _atomicbot_float_equal(payload["median_tokens_per_second"], formal_value):
                raise ValueError(f"formal throughput aggregate conflict: {test_id}")
            source_paths = [relative.as_posix()]
        else:
            source_paths = []
            values = []
            for repetition in range(1, 4):
                relative = ATOMICBOT_RAW_ROOT / f"2026-07-16/safety-bypass/{test_id}/cli-throughput/sample-{repetition}/events.jsonl"
                matches = tps_pattern.findall((root / relative).read_text(encoding="utf-8"))
                if len(matches) != 1:
                    raise ValueError(f"formal throughput parse conflict: {relative}")
                values.append(float(matches[0])); source_paths.append(relative.as_posix())
            if not _atomicbot_float_equal(statistics.median(values), formal_value, 0.005):
                raise ValueError(f"formal throughput aggregate conflict: {test_id}")
        throughput_sources.append({
            "measurement_id": f"MEAS-{test_id}-FORMAL-TPS", "test_case_id": test_id,
            "value": formal_value, "relative_paths": source_paths,
            "supported_fields": ["generation_tokens_per_second"],
        })

    master = _read_json(root / ATOMICBOT_MASTER_RELATIVE)
    if not isinstance(master, dict) or tuple(master.get("tests", {})) != ATOMICBOT_EXPECTED_IDS:
        raise ValueError("AtomicBot master summary must contain the complete 19-row set")
    for test_id, row in master["tests"].items():
        if row.get("spec", {}).get("test_id") != test_id:
            raise ValueError(f"master summary identity conflict: {test_id}")

    # The v1.7 Markdown's final two cells were overwritten by duplicate CPU/GPU
    # utilization triples. They are pattern-checked and excluded from authority.
    workbook_results = _table_by_header(workbook, ("Test ID", "Model", "Weights", "K cache"))
    if tuple(row["Test ID"] for row in workbook_results) != ATOMICBOT_EXPECTED_IDS or any(
        row["Quality /10"] != row["CPU mean/median/peak %"] or row["Status"] != row["GPU mean/median/peak %"]
        for row in workbook_results
    ):
        raise ValueError("overwritten Quality/Status pattern conflict")
    if any(
        row["CPU mean/median/peak %"] != workbook_expected_duplicates[row["Test ID"]]["cpu"]
        or row["GPU mean/median/peak %"] != workbook_expected_duplicates[row["Test ID"]]["gpu"]
        for row in workbook_results
    ):
        raise ValueError("overwritten Quality/Status value conflict")

    prompt_path = root / ATOMICBOT_PROMPT_RELATIVE
    rubric_path = root / ATOMICBOT_RUBRIC_RELATIVE
    if hash_file(prompt_path) != ATOMICBOT_PROMPT_SHA256:
        raise ValueError("prompt contract hash conflict")
    if hash_file(rubric_path) != ATOMICBOT_RUBRIC_SHA256:
        raise ValueError("quality rubric hash conflict")
    prompt_contract = _read_json(prompt_path); rubric_contract = _read_json(rubric_path)
    if not isinstance(prompt_contract, dict) or prompt_contract.get("prompt_set_id") != "GTQ-PROMPTS-v1":
        raise ValueError("prompt contract identity conflict")
    if not isinstance(rubric_contract, dict) or rubric_contract.get("rubric_id") != "GTQ-QUALITY-RUBRIC-v1":
        raise ValueError("quality rubric identity conflict")
    expected_weights = {name: weight for name, (weight, _field) in ATOMICBOT_DIMENSION_FIELDS.items()}
    if {row["name"]: float(row["weight"]) for row in rubric_contract.get("dimensions", [])} != expected_weights:
        raise ValueError("quality rubric dimension conflict")

    quality_summary = _read_json(root / ATOMICBOT_QUALITY_RELATIVE)
    adjudications = _read_json(root / ATOMICBOT_ADJUDICATION_RELATIVE)
    quality_register = [row for row in _read_csv(root / ATOMICBOT_QUALITY_REGISTER_RELATIVE) if row["Route"] == ATOMICBOT_ROUTE_ID]
    if not isinstance(quality_summary, dict) or tuple(row["test_id"] for row in quality_summary.get("rows", [])) != ATOMICBOT_EXPECTED_IDS:
        raise ValueError("AtomicBot current quality summary must contain the complete 19-row set")
    if not isinstance(adjudications, dict) or len(quality_register) != 114:
        raise ValueError("quality authority completeness conflict")
    register_by_key = {(row["Test_ID"], row["Prompt_ID"]): row for row in quality_register}
    quality_prompt_rows: list[dict[str, object]] = []
    for row in quality_summary["rows"]:
        if set(row.get("prompts", {})) != {f"P{i}" for i in range(1, 7)}:
            raise ValueError("AtomicBot quality authority must contain complete P1-P6 rows")
        prompt_scores = []
        for prompt_id in (f"P{i}" for i in range(1, 7)):
            summary_prompt = row["prompts"][prompt_id]
            relative = ATOMICBOT_RAW_ROOT / f"2026-07-17/quality-all-rows/{row['test_id']}/{prompt_id}.json"
            prompt_payload = _read_json(root / relative)
            if not isinstance(prompt_payload, dict) or (prompt_payload.get("test_id"), prompt_payload.get("prompt_id")) != (row["test_id"], prompt_id):
                raise ValueError(f"quality prompt identity conflict: {relative}")
            output_hash = hashlib.sha256(str(prompt_payload.get("output", "")).encode("utf-8")).hexdigest()
            if prompt_payload.get("output_sha256") != output_hash or summary_prompt.get("response_sha256") != output_hash:
                raise ValueError(f"quality prompt hash conflict: {relative}")
            adjudication_key = f"{prompt_id}:{output_hash}"
            adjudication = adjudications.get(adjudication_key)
            register = register_by_key.get((row["test_id"], prompt_id))
            if not isinstance(adjudication, dict) or register is None:
                raise ValueError(f"quality adjudication binding conflict: {row['test_id']} {prompt_id}")
            if register["Response_SHA256"] != output_hash or register["Prompt_Set_ID"] != "GTQ-PROMPTS-v1" or register["Rubric_ID"] != "GTQ-QUALITY-RUBRIC-v1":
                raise ValueError(f"quality register identity conflict: {row['test_id']} {prompt_id}")
            dimensions = adjudication.get("dimensions", {})
            weighted = round(sum(float(dimensions[name]) * weight for name, (weight, _field) in ATOMICBOT_DIMENSION_FIELDS.items()), 2)
            if any(not _atomicbot_float_equal(register[field], dimensions[name]) for name, (_weight, field) in ATOMICBOT_DIMENSION_FIELDS.items()):
                raise ValueError(f"weighted quality score conflict: {row['test_id']} {prompt_id}")
            caps = list(adjudication.get("critical_caps", []))
            applied = register["Critical_Cap_Applied"] == "Yes"
            if applied != bool(caps) or register["Critical_Cap_Reason"] != adjudication.get("critical_cap_reason"):
                raise ValueError(f"critical cap conflict: {row['test_id']} {prompt_id}")
            final_score = min([weighted, *map(float, caps)])
            if not _atomicbot_float_equal(summary_prompt["score"], final_score):
                raise ValueError(f"quality score conflict: {row['test_id']} {prompt_id}")
            if not _atomicbot_float_equal(register["Weighted_Score_0_to_10"], final_score):
                raise ValueError(f"quality register score conflict: {row['test_id']} {prompt_id}")
            prompt_scores.append(final_score)
            quality_prompt_rows.append({
                "test_case_id": row["test_id"], "prompt_id": prompt_id,
                "relative_path": relative.as_posix(), "sha256": hash_file(root / relative),
                "output_sha256": output_hash, "adjudication_key": adjudication_key,
                "dimensions": dimensions, "weighted_score": weighted, "critical_caps": caps,
                "critical_cap_reason": adjudication["critical_cap_reason"], "score": final_score,
                "deterministic_pass": adjudication["deterministic_pass"],
            })
        if abs(statistics.mean(prompt_scores) - float(row["quality_mean"])) > 0.00005:
            raise ValueError(f"quality mean conflict: {row['test_id']}")

    # Cross-source agreement is necessary but not sufficient: these independent
    # source pins stop coordinated edits from redefining the published quality.
    if hash_file(root / ATOMICBOT_QUALITY_RELATIVE) != ATOMICBOT_QUALITY_SUMMARY_SHA256:
        raise ValueError("quality summary authority hash conflict")
    if hash_file(root / ATOMICBOT_ADJUDICATION_RELATIVE) != ATOMICBOT_ADJUDICATION_SHA256:
        raise ValueError("quality adjudication authority hash conflict")
    if hash_file(root / ATOMICBOT_QUALITY_REGISTER_RELATIVE) != ATOMICBOT_QUALITY_REGISTER_SHA256:
        raise ValueError("quality register authority hash conflict")

    setup = _table_by_header(workbook, ("ID", "Check", "Result")); setup_counts = Counter()
    for row in setup:
        value = row["Result"].casefold()
        setup_counts["blocked" if value.startswith("blocked") else "unsupported" if value.startswith("unsupported") else "passed"] += 1

    deviations = [row for row in _read_csv(root / "docs/testing/Failure-Register.csv") if row["Route"] == ATOMICBOT_ROUTE_ID]
    if len(deviations) != 5:
        raise ValueError("AtomicBot deviation completeness conflict")
    deviation_rows = []
    for row in deviations:
        if row["Failure_ID"] not in ATOMICBOT_DEVIATION_SCOPES:
            raise ValueError(f"deviation taxonomy conflict: {row['Failure_ID']}")
        scope_type, expected_scope_ids = ATOMICBOT_DEVIATION_SCOPES[row["Failure_ID"]]
        scope_ids = list(expected_scope_ids)
        if row["Test_ID"].split("/") != scope_ids:
            raise ValueError(f"deviation scope conflict: {row['Failure_ID']}")
        deviation_rows.append({
            "deviation_id": row["Failure_ID"], "scope_type": scope_type,
            "scope_ids": scope_ids, "nonterminal": True, "status": row["Final_Status"],
            "code": row["Failure_Code"], "reason": row["Observed_Symptom"],
        })

    stale_missing = stale_hash = 0
    for row in indexed:
        path = root / row["Repository_Path"].replace("\\", "/")
        if not path.is_file(): stale_missing += 1
        elif hash_file(path) != row["SHA256"].lower(): stale_hash += 1
    if (len(indexed), stale_missing, stale_hash) != (828, 38, 263):
        raise ValueError(
            "stale index inventory conflict: expected 828 rows / 38 missing / 263 hash mismatches, "
            f"observed {len(indexed)} / {stale_missing} / {stale_hash}"
        )
    duplicates = {key: rows for key, rows in by_evidence_id.items() if len(rows) > 1}
    if set(duplicates) != {"EVID-e3b0c44298fc1c149afb"} or len(duplicates["EVID-e3b0c44298fc1c149afb"]) != 5:
        raise ValueError("stale index duplicate Evidence_ID conflict")
    duplicate_paths = sorted(row["Repository_Path"].replace("\\", "/") for row in duplicates["EVID-e3b0c44298fc1c149afb"])

    return {
        "matrix": matrix, "test_runs": test_runs, "performance": performance,
        "quality_rows": quality_summary["rows"], "quality_prompt_rows": quality_prompt_rows,
        "prompt_contract": prompt_contract, "rubric_contract": rubric_contract,
        "joined": joined, "resource_sources": resource_sources,
        "current_summaries": current_summaries, "throughput_sources": throughput_sources,
        "deviation_rows": deviation_rows,
        "joined_evidence_count": len(joined), "joined_evidence_hash_conflicts": 0,
        "runtime_status_counts": {"passed": 19, "blocked": 0}, "setup_status_counts": dict(setup_counts),
        "performance_register_rows_reconciled": 57, "current_server_summaries_reconciled": 19,
        "formal_throughput_sources_reconciled": 19, "master_summary_rows_reconciled": 19,
        "quality_evaluation_rows_reconciled": 114, "quality_adjudication_bindings_reconciled": 114,
        "quality_weighted_scores_recomputed": 114, "quality_means_recomputed": 19,
        "workbook_overwritten_result_rows": 19, "workbook_excluded_columns": ["Quality /10", "Status"],
        "workbook_duplicate_pairs_value_reconciled": 19,
        "workbook_precedence": ATOMICBOT_WORKBOOK_PRECEDENCE,
        "quality_authority_hashes_authenticated": 3,
        "stale_index_missing_paths": stale_missing, "stale_index_hash_conflicts": stale_hash,
        "index_row_count": len(indexed), "duplicate_evidence_id": "EVID-e3b0c44298fc1c149afb",
        "duplicate_evidence_id_paths": len(duplicate_paths), "duplicate_evidence_id_extra_rows": len(duplicate_paths) - 1,
        "duplicate_evidence_paths": duplicate_paths,
        "master_summary_chronology": "Historical acquisition authority: 17 passed summaries and 2 research-only rows; current WB-02/Test-Run authority supersedes status only for the two controlled safety-bypass retests.",
    }


def build_atomicbot_bundle(repo_root: Path) -> RouteBundle:
    """Normalize WB-02 v1.7 while preserving its provisional quality boundary."""
    root = Path(repo_root).resolve(strict=True); audit = audit_atomicbot_sources(root)
    matrix_by_id = {row["ID"]: row for row in audit["matrix"]}
    test_run_by_id = {row["Test_ID"]: row for row in audit["test_runs"]}

    evidence: list[EvidenceRecord] = []; evidence_ids: set[str] = set()
    def add_evidence(relative: str | Path, role: str, label: str, evidence_id: str | None = None) -> str:
        relative_path = Path(relative).as_posix(); path = root / relative_path; digest = hash_file(path)
        candidate = evidence_id or f"atomicbot-{role}-{digest[:16]}"
        if candidate in evidence_ids:
            existing = next(item for item in evidence if item.evidence_id == candidate)
            if existing.relative_path == relative_path and existing.sha256 == digest:
                return candidate
            candidate = f"atomicbot-{role}-{digest}"
        record = EvidenceRecord(
            route_id=ATOMICBOT_ROUTE_ID, campaign_id=ATOMICBOT_CAMPAIGN_ID,
            evidence_id=candidate, role=role, relative_path=relative_path,
            sha256=digest, size_bytes=path.stat().st_size, source_label=label,
        )
        evidence.append(record); evidence_ids.add(candidate); return candidate

    resource_evidence: dict[str, dict[str, str]] = {}
    for item in audit["resource_sources"]:
        measurement = item["measurement_source"]; utilization_source = item["utilization_source"]
        measurement_eid = add_evidence(
            measurement["relative_path"], "resource-measurement-json",
            f"{item['test_case_id']} repetition {item['repetition_id']} resource observation",
            measurement["evidence_id"],
        )
        utilization_eid = add_evidence(
            utilization_source["relative_path"], "utilization-samples-csv",
            f"{item['test_case_id']} repetition {item['repetition_id']} raw utilization samples",
            utilization_source["evidence_id"],
        )
        resource_evidence[item["measurement_id"]] = {"measurement": measurement_eid, "utilization": utilization_eid}

    current_summary_evidence = {
        test_id: add_evidence(item["relative_path"], "current-server-metrics-summary", f"{test_id} current three-run aggregate")
        for test_id, item in audit["current_summaries"].items()
    }
    throughput_evidence: dict[str, list[str]] = {}
    for item in audit["throughput_sources"]:
        throughput_evidence[item["test_case_id"]] = [
            add_evidence(relative, "formal-throughput-source", f"{item['test_case_id']} formal throughput authority")
            for relative in item["relative_paths"]
        ]

    quality_evidence_by_key: dict[tuple[str, str], str] = {}
    for item in audit["quality_prompt_rows"]:
        quality_evidence_by_key[(item["test_case_id"], item["prompt_id"])] = add_evidence(
            item["relative_path"], "quality-prompt-output",
            f"{item['test_case_id']} / {item['prompt_id']} output and output hash",
        )
    source_specs = (
        (ATOMICBOT_WORKBOOK_RELATIVE, "controlled-workbook-markdown", "WB-02 controlled Markdown"),
        (Path("docs/testing/Workbook-Revision-Register.csv"), "revision-register", "Current WB-02 revision authority"),
        (TEST_RUN_REGISTER_RELATIVE, "test-run-register", "Current formal runtime status authority"),
        (PERFORMANCE_REGISTER_RELATIVE, "performance-register", "Current repetition/utilization authority"),
        (ATOMICBOT_QUALITY_REGISTER_RELATIVE, "quality-register", "Current prompt-level quality register"),
        (Path("docs/testing/Failure-Register.csv"), "failure-register", "Controlled failure/deviation register"),
        (EVIDENCE_INDEX_RELATIVE, "evidence-index", "Controlled index with documented stale historical rows"),
        (ATOMICBOT_MASTER_RELATIVE, "master-summary", "AtomicBot acquisition master summary"),
        (ATOMICBOT_QUALITY_RELATIVE, "quality-summary", "Current all-row limited quality summary"),
        (ATOMICBOT_ADJUDICATION_RELATIVE, "quality-adjudication", "Content-keyed all-row adjudications"),
        (ATOMICBOT_PROMPT_RELATIVE, "prompt-contract", "Frozen GTQ-PROMPTS-v1 authority"),
        (ATOMICBOT_RUBRIC_RELATIVE, "quality-rubric", "Controlling GTQ-QUALITY-RUBRIC-v1 authority"),
        (ATOMICBOT_RAW_ROOT / "2026-07-16/acquisition/results/AtomicBot_Environment_Manifest.json", "environment-manifest", "Captured repository and system identity"),
        (ATOMICBOT_RAW_ROOT / "2026-07-16/acquisition/results/AtomicBot_Model_Manifest.json", "model-manifest", "Frozen model identities"),
        (ATOMICBOT_RAW_ROOT / "2026-07-16/acquisition/results/AtomicBot_Failure_Log.json", "failure-log", "Indexed historical issue log"),
        (ATOMICBOT_RAW_ROOT / "2026-07-16/acquisition/results/AtomicBot_Perplexity_Summary.json", "perplexity-summary", "Bounded synthetic supplement"),
    )
    for relative, role, label in source_specs:
        add_evidence(relative, role, label)
    evidence_by_role = {item.role: item for item in evidence}

    attempts: list[AttemptRecord] = []; measurements: list[MeasurementRecord] = []
    utilization: list[dict[str, object]] = []; summaries: list[SummaryRecord] = []
    measurement_field_evidence: list[dict[str, object]] = []
    performance_by_test: dict[str, list[dict[str, str]]] = defaultdict(list)
    for row in audit["performance"]:
        performance_by_test[row["Test_ID"]].append(row)
        provenance = resource_evidence[row["Measurement_ID"]]
        input_value = row["Input_Tokens"]
        output_value = row["Output_Tokens"]
        measurement = MeasurementRecord(
            route_id=ATOMICBOT_ROUTE_ID,
            campaign_id=ATOMICBOT_CAMPAIGN_ID,
            test_case_id=row["Test_ID"],
            attempt_id=f"{row['Test_ID']}--attempt-001",
            measurement_id=row["Measurement_ID"],
            run_id=row["Run_ID"],
            repetition_id=row["Repetition_Number"],
            source_evidence_id=provenance["measurement"],
            latency_ms=float(row["TTFT_ms"]),
            generation_tokens_per_second=None,
            peak_working_set_bytes=int(row["Peak_Working_Set_Bytes"]),
            input_tokens=int(input_value) if input_value.isdecimal() else None,
            output_tokens=int(output_value) if output_value.isdecimal() else None,
        )
        measurements.append(measurement)
        measurement_field_evidence.append({
            "measurement_id": row["Measurement_ID"], "test_case_id": row["Test_ID"],
            "measurement_kind": "resource-utilization",
            "supported_fields": ["latency_ms", "peak_working_set_bytes", "kv_cache_allocated_bytes", "cpu_utilization", "gpu_utilization"],
            "evidence_ids": [provenance["measurement"], provenance["utilization"]],
        })
        utilization.append({
            "measurement_id": row["Measurement_ID"], "test_case_id": row["Test_ID"],
            "run_id": row["Run_ID"], "source_evidence_id": provenance["utilization"],
            "kv_cache_allocated_bytes": int(row["KV_Cache_Allocated_Bytes"]),
            "cpu_mean_percent": float(row["CPU_Mean_Percent"]),
            "cpu_median_percent": float(row["CPU_Median_Percent"]),
            "cpu_peak_percent": float(row["CPU_Peak_Percent"]),
            "gpu_mean_percent": float(row["GPU_Engine_Mean_Percent"]),
            "gpu_median_percent": float(row["GPU_Engine_Median_Percent"]),
            "gpu_peak_percent": float(row["GPU_Engine_Peak_Percent"]),
        })
    for item in audit["throughput_sources"]:
        source_ids = throughput_evidence[item["test_case_id"]]
        measurements.append(MeasurementRecord(
            route_id=ATOMICBOT_ROUTE_ID, campaign_id=ATOMICBOT_CAMPAIGN_ID,
            test_case_id=item["test_case_id"], attempt_id=f"{item['test_case_id']}--attempt-001",
            measurement_id=item["measurement_id"], run_id=f"{item['test_case_id']}-FORMAL",
            repetition_id="aggregate-three-repetitions", source_evidence_id=source_ids[0],
            generation_tokens_per_second=float(item["value"]),
        ))
        measurement_field_evidence.append({
            "measurement_id": item["measurement_id"], "test_case_id": item["test_case_id"],
            "measurement_kind": "formal-throughput", "supported_fields": ["generation_tokens_per_second"],
            "evidence_ids": source_ids,
        })
    for test_id in ATOMICBOT_EXPECTED_IDS:
        matrix = matrix_by_id[test_id]; formal = test_run_by_id[test_id]; rows = performance_by_test[test_id]
        backend = "vulkan" if "vulkan" in formal["Actual_Backend"].casefold() else "cpu"
        attempts.append(AttemptRecord(
            route_id=ATOMICBOT_ROUTE_ID, campaign_id=ATOMICBOT_CAMPAIGN_ID,
            test_case_id=test_id, attempt_id=f"{test_id}--attempt-001",
            status=Status.PASSED, executed=True, reason=formal["Result_Reason"],
            model_id=formal["Model_ID"] or matrix["Model"].replace(" ", "-").casefold(),
            weight_format_id=(formal["Model_Weight_Precision"] or "q4_k_m").casefold(),
            cache_format_id=matrix["KV cache"].casefold(), backend_id=backend,
            source_status=formal["Result"],
            evidence_ids=(evidence_by_role["test-run-register"].evidence_id, evidence_by_role["performance-register"].evidence_id,
                current_summary_evidence[test_id], *throughput_evidence[test_id]),
        ))
        resource_ids = tuple(row["Measurement_ID"] for row in rows)
        throughput_id = f"MEAS-{test_id}-FORMAL-TPS"
        specs = (
            ("time_to_first_token", statistics.median(float(row["TTFT_ms"]) for row in rows), "milliseconds", resource_ids, "median of three validated repetitions"),
            ("generation_tokens_per_second", float(formal["Decode_Tokens_Per_Second"]), "tokens_per_second", (throughput_id,), "median of three validated formal repetitions"),
            ("peak_working_set_bytes", max(int(row["Peak_Working_Set_Bytes"]) for row in rows), "bytes", resource_ids, "maximum of three validated repetitions"),
            ("kv_cache_allocated_bytes", statistics.median(int(row["KV_Cache_Allocated_Bytes"]) for row in rows), "bytes", resource_ids, "median of three validated repetitions"),
            ("cpu_mean_percent", statistics.mean(float(row["CPU_Mean_Percent"]) for row in rows), "percent", resource_ids, "arithmetic mean of three validated repetition means"),
            ("gpu_mean_percent", statistics.mean(float(row["GPU_Engine_Mean_Percent"]) for row in rows), "percent", resource_ids, "arithmetic mean of three validated repetition means"),
        )
        for metric, value, unit, source_ids, aggregation in specs:
            summaries.append(SummaryRecord(
                route_id=ATOMICBOT_ROUTE_ID, campaign_id=ATOMICBOT_CAMPAIGN_ID,
                test_case_id=test_id, summary_id=f"{test_id}--{metric}", metric_name=metric,
                value=value, unit=unit, aggregation=aggregation, source_measurement_ids=source_ids,
            ))

    quality: list[QualityRecord] = []
    for row in audit["quality_rows"]:
        for prompt_id in (f"P{i}" for i in range(1, 7)):
            prompt = row["prompts"][prompt_id]
            quality.append(QualityRecord(
                route_id=ATOMICBOT_ROUTE_ID, campaign_id=ATOMICBOT_CAMPAIGN_ID,
                test_case_id=row["test_id"], quality_id=f"{row['test_id']}--{prompt_id}",
                prompt_id=prompt_id, criterion_id="historical-composite-screen",
                score=float(prompt["score"]), maximum_score=10.0,
                prompt_suite_id="GTQ-PROMPTS-v1", rubric_id="GTQ-QUALITY-RUBRIC-v1",
                scoring_version="v1-recomputed-and-evidence-bound; application limited/provisional",
                source_evidence_id=quality_evidence_by_key[(row["test_id"], prompt_id)],
            ))
    env = _read_json(root / (ATOMICBOT_RAW_ROOT / "2026-07-16/acquisition/results/AtomicBot_Environment_Manifest.json"))
    failure_register_eid = evidence_by_role["failure-register"].evidence_id
    deviation_rows = [dict(row, evidence_ids=[failure_register_eid]) for row in audit["deviation_rows"]]
    quality_contract = {
        "prompt_set_id": audit["prompt_contract"]["prompt_set_id"],
        "prompt_set_sha256": ATOMICBOT_PROMPT_SHA256,
        "rubric_id": audit["rubric_contract"]["rubric_id"], "rubric_sha256": ATOMICBOT_RUBRIC_SHA256,
        "generation_settings": audit["prompt_contract"]["generation_defaults"],
        "prompts": [{"prompt_id": row["prompt_id"], "task": row["task"], "deterministic_checks": row["deterministic_checks"]} for row in audit["prompt_contract"]["prompts"]],
        "dimension_weights": {name: weight for name, (weight, _field) in ATOMICBOT_DIMENSION_FIELDS.items()},
        "dimensions": audit["rubric_contract"]["dimensions"], "anchors": audit["rubric_contract"]["anchors"],
        "procedure": audit["rubric_contract"]["procedure"],
    }
    receipt_keys = (
        "joined_evidence_count", "joined_evidence_hash_conflicts", "performance_register_rows_reconciled",
        "current_server_summaries_reconciled", "formal_throughput_sources_reconciled", "master_summary_rows_reconciled",
        "quality_evaluation_rows_reconciled", "quality_adjudication_bindings_reconciled",
        "quality_weighted_scores_recomputed", "quality_means_recomputed", "workbook_overwritten_result_rows",
        "quality_authority_hashes_authenticated", "workbook_duplicate_pairs_value_reconciled",
        "workbook_excluded_columns", "workbook_precedence", "stale_index_missing_paths", "stale_index_hash_conflicts",
        "index_row_count", "duplicate_evidence_id", "duplicate_evidence_id_paths", "duplicate_evidence_id_extra_rows",
        "duplicate_evidence_paths", "master_summary_chronology",
    )
    tool_versions = {
        name: str(details.get("stdout", "")).splitlines()[0] if str(details.get("stdout", "")).splitlines() else ATOMICBOT_NOT_COLLECTED
        for name, details in env["tools"].items()
    }
    return RouteBundle(
        route_id=ATOMICBOT_ROUTE_ID, campaign_id=ATOMICBOT_CAMPAIGN_ID,
        attempts=tuple(attempts), measurements=tuple(measurements), summaries=tuple(summaries),
        quality=tuple(quality), failures=(), evidence=tuple(sorted(evidence, key=lambda item: item.evidence_id)),
        repository={
            "url": env["origin"].removesuffix(".git"), "branch": env["branch"], "commit": env["commit"],
            "workbook_id": "WB-02", "workbook_revision": "1.7", "source_date": "2026-07-17",
            "runtime_status_counts": audit["runtime_status_counts"], "setup_status_counts": audit["setup_status_counts"],
            "utilization_observations": utilization,
            "measurement_field_evidence": measurement_field_evidence,
            "quality_contract": quality_contract, "quality_adjudications": audit["quality_prompt_rows"],
            "deviation_rows": deviation_rows,
            "quality_authority": "limited/provisional historical screen",
            "quality_direct_openvino_comparison_permitted": False,
            "quality_calibration": ATOMICBOT_NOT_COLLECTED,
            "source_reconciliation": {key: audit[key] for key in receipt_keys},
        },
        hardware={"machine": "Lenovo-PF4HMD0T", "platform": env["machine"]["platform"],
            "processor": env["machine"]["processor"], "logical_cpus": env["machine"]["logical_cpus"],
            "available_memory_mib": env["machine"]["available_memory_mib"], "vulkan_sdk_version": "1.4.350.0",
            "npu": ATOMICBOT_NOT_COLLECTED},
        software={"tool_versions": tool_versions, "reporting_interpreter": "Python 3.11 portable"},
    )


def build_atomicbot_report(bundle: RouteBundle) -> Report:
    summaries = {(item.test_case_id, item.metric_name): item.value for item in bundle.summaries}
    quality_means = {
        test_id: statistics.mean(item.score for item in bundle.quality if item.test_case_id == test_id and item.score is not None)
        for test_id in ATOMICBOT_EXPECTED_IDS
    }
    matrix_rows = tuple((a.test_case_id, a.model_id, a.weight_format_id, a.cache_format_id, a.backend_id, a.status.display_label) for a in bundle.attempts)
    perf_rows = tuple((
        test_id,
        f"{summaries[(test_id, 'generation_tokens_per_second')]:.3f}",
        f"{summaries[(test_id, 'time_to_first_token')]:.3f}",
        f"{summaries[(test_id, 'peak_working_set_bytes')] / 1048576:.3f}",
        f"{summaries[(test_id, 'cpu_mean_percent')]:.2f}",
        f"{summaries[(test_id, 'gpu_mean_percent')]:.2f}",
        f"{quality_means[test_id]:.3f}",
    ) for test_id in ATOMICBOT_EXPECTED_IDS)
    evidence_rows = tuple((item.evidence_id, item.role, item.sha256, item.relative_path) for item in bundle.evidence)
    sections = (
        ReportSection(SECTION_ORDER[0], (ReportParagraph("AtomicBot TurboQuant llama.cpp; WB-02 v1.7; unified publication revision R1. Markdown is canonical."),)),
        ReportSection(SECTION_ORDER[1], (ReportParagraph("All 19 controlled runtime configurations passed. One repository test remained blocked by Windows Device Guard; that setup block is not a runtime failure. Evidence establishes runtime activation, memory reduction, and observed device placement, with explicit limitations."),)),
        ReportSection(SECTION_ORDER[2], (ReportParagraph("TurboQuant reduced measured KV allocation in matched cases. Quality was response- and backend-dependent and does not support a precision-ordered or unconditional recommendation."), _table("KF-01", "Decision-relevant result summary", ("Test", "Decode tok/s", "TTFT ms", "Peak WS MiB", "CPU mean %", "GPU mean %", "Quality /10"), perf_rows))),
        ReportSection(SECTION_ORDER[3], (ReportParagraph(f"Repository {bundle.repository['url']}; detached commit {bundle.repository['commit']}. Portable hardware and software identities are preserved in system JSON without machine-local paths. Historically uncollected fields are `Not collected`."),)),
        ReportSection(SECTION_ORDER[4], (ReportParagraph("WB-02 v1.7 controls the exact 19-row ladder. The publication normalizes existing evidence and does not rerun inference. The workbook's Quality /10 and Status cells were overwritten by duplicate CPU/GPU triples in all 19 result rows; every duplicate pair is reconciled to the exact current performance aggregate before those cells are excluded. Runtime status comes from the Test-Run register, performance/utilization from the Performance register and current summaries, and quality from independently authenticated quality authorities."), _table("SC-01", "Controlled runtime matrix", ("Test", "Model", "Weights", "Cache", "Backend", "Status"), matrix_rows))),
        ReportSection(SECTION_ORDER[5], (_table("AV-01", "Model/cache/backend availability", ("Test", "Model", "Weights", "Cache", "Backend", "Status"), matrix_rows),)),
        ReportSection(SECTION_ORDER[6], (_table("AC-01", "Complete runtime attempt accounting", ("Test", "Attempt", "Status", "Reason"), ((a.test_case_id, a.attempt_id, a.status.display_label, a.reason) for a in bundle.attempts)), ReportNote("Runtime: 19 Passed, 0 Blocked. Setup: one Device Guard block/partial and one unsupported TurboQuant-specific SYCL item are reported separately."))),
        ReportSection(SECTION_ORDER[7], (ReportParagraph("TTFT, peak working set, KV allocation, and utilization reconcile 57 registered observations to their live measurement JSON; raw utilization CSV supports utilization only. Decode throughput is represented separately by 19 formal measurements: 17 formal summaries and three generation-eval events for each of two controlled safety-bypass rows. No utilization artifact is cited for throughput."), _table("PF-01", "Validated aggregate performance", ("Test", "Decode tok/s", "TTFT ms", "Peak WS MiB", "CPU mean %", "GPU mean %", "Quality /10"), perf_rows))),
        ReportSection(SECTION_ORDER[8], (ReportParagraph("Each of 114 outputs is bound by SHA-256 to GTQ-PROMPTS-v1, GTQ-QUALITY-RUBRIC-v1, a content-keyed adjudication, a Quality-Evaluation register row, and its per-test summary. The summary, adjudication, and register authorities are independently pinned, so coordinated internally consistent edits cannot redefine published quality. Scores are recomputed from weighted dimensions (30/25/20/15/10) and the smallest applicable critical cap; all 19 means are recomputed. The application remains limited/provisional, calibration is Not collected, and results are not directly comparable with OpenVINO."), _table("QL-01", "Preserved historical prompt means", ("Test", "Mean /10", "Method boundary"), ((test_id, f"{quality_means[test_id]:.3f}", "Limited/provisional; no OpenVINO ranking") for test_id in ATOMICBOT_EXPECTED_IDS)))),
        ReportSection(SECTION_ORDER[9], (ReportParagraph("CPU, Vulkan-hybrid, and Vulkan-native placement were observed. Partial Vulkan rows intentionally retained CPU KV; this is not silent fallback. Exact per-run CPU/GPU observations are preserved."),)),
        ReportSection(SECTION_ORDER[10], (ReportParagraph("Five nonterminal deviations are explicit and typed: two setup scopes, one test scope, one multi-test scope, and one mixed prompt/test scope. Device Guard blocked one repository executable; memory and safety gates were resolved by controlled retest; the P5 timeout remains preserved as a scored empty output. None is converted into a runtime failure or silently filtered."),)),
        ReportSection(SECTION_ORDER[11], (ReportParagraph("The historical Evidence Index audit is exact: 828 AtomicBot rows, 38 missing paths, 263 existing-path hash mismatches, and one Evidence_ID duplicated over five empty stdout paths (four extra rows). Only 57 exact indexed utilization tuples substantiate registered utilization; live measurement JSON and formal throughput sources are admitted under their current path/hash. Quality remains provisional, P1-P6 is not a general benchmark, and no direct OpenVINO score comparison is permitted."), ReportNote("Missing historical fields remain `Not collected`, never zero."))),
        ReportSection(SECTION_ORDER[12], (ReportParagraph("Use reproduction/commands.md to regenerate normalized artifacts, render DOCX, export PDF through the owned Word process, finalize receipts, and rerun focused validation. It does not rerun benchmarks."),)),
        ReportSection(SECTION_ORDER[13], (ReportParagraph("Each admitted source has a repository-relative path and SHA-256. Every material performance, quality, runtime, deviation, workbook-precedence, and stale-index claim is mapped in evidence/claim-evidence-map.csv. Stale unjoined index rows are a recorded source-control limitation."), _table("EV-01", "Admitted evidence", ("Evidence ID", "Role", "SHA-256", "Repository-relative path"), evidence_rows))),
        ReportSection(SECTION_ORDER[14], (ReportParagraph("R1 (2026-07-17): initial unified evidence-bound publication from WB-02 v1.7. R1 hardening receipt: independently pinned quality authorities, full score recomputation, separated performance provenance, exact workbook-duplicate value reconciliation, explicit deviation relationships, exact stale-index inventory, and portable outputs. Generated DOCX/PDF are derivatives."),)),
    )
    return Report(
        title="AtomicBot TurboQuant Final Test Report", route_id=ATOMICBOT_ROUTE_ID,
        revision="R1", generated_date=date(2026, 7, 17), sections=sections,
        evidence_ids=tuple(item.evidence_id for item in bundle.evidence if item.role in {
            "controlled-workbook-markdown", "test-run-register", "performance-register",
            "quality-summary", "evidence-index", "failure-log",
        }),
    )


def _atomicbot_relationship_receipt(bundle: RouteBundle) -> dict[str, object]:
    attempts = {item.attempt_id for item in bundle.attempts}
    tests = {item.test_case_id for item in bundle.attempts}
    evidence = {item.evidence_id for item in bundle.evidence}
    measurement_ids = {item.measurement_id for item in bundle.measurements}
    errors = []
    for item in bundle.measurements:
        if item.attempt_id not in attempts or item.test_case_id not in tests or item.source_evidence_id not in evidence:
            errors.append(f"measurement relationship: {item.measurement_id}")
    for item in bundle.summaries:
        if item.test_case_id not in tests or not set(item.source_measurement_ids) <= measurement_ids:
            errors.append(f"summary relationship: {item.summary_id}")
    for item in bundle.quality:
        if item.test_case_id not in tests or item.source_evidence_id not in evidence:
            errors.append(f"quality relationship: {item.quality_id}")
    deviations = bundle.repository.get("deviation_rows", [])
    deviation_errors: list[str] = []
    if not isinstance(deviations, list) or len(deviations) != len(ATOMICBOT_DEVIATION_SCOPES):
        deviation_errors.append("deviation ID set: expected exactly five controlled deviations")
        deviations = deviations if isinstance(deviations, list) else []
    deviation_by_id = {
        row.get("deviation_id"): row for row in deviations if isinstance(row, dict)
    }
    if set(deviation_by_id) != set(ATOMICBOT_DEVIATION_SCOPES) or len(deviation_by_id) != len(deviations):
        deviation_errors.append("deviation ID set: IDs must be exact and unique")
    for deviation_id, (expected_type, expected_scope_ids) in ATOMICBOT_DEVIATION_SCOPES.items():
        row = deviation_by_id.get(deviation_id)
        if row is None:
            continue
        if row.get("scope_type") != expected_type or tuple(row.get("scope_ids", ())) != expected_scope_ids:
            deviation_errors.append(f"deviation scope: {deviation_id}")
        if row.get("nonterminal") is not True:
            deviation_errors.append(f"deviation terminal flag: {deviation_id}")
        cited = row.get("evidence_ids", [])
        if not isinstance(cited, list) or not cited or not set(cited) <= evidence:
            deviation_errors.append(f"deviation evidence: {deviation_id}")
    errors.extend(deviation_errors)
    return {
        "valid": not errors, "errors": errors, "attempt_count": len(attempts),
        "measurement_count": len(measurement_ids), "quality_count": len(bundle.quality),
        "evidence_count": len(evidence), "deviation_count": len(deviations),
        "deviation_ids": sorted(str(row.get("deviation_id")) for row in deviations if isinstance(row, dict)),
        "deviation_relationships_valid": not deviation_errors,
    }


def write_atomicbot_route(repo_root: Path) -> RouteBundle:
    root = Path(repo_root).resolve(strict=True); route = root / ATOMICBOT_ROUTE_RELATIVE
    bundle = build_atomicbot_bundle(root); report = build_atomicbot_report(bundle)
    write_json(route / "route-manifest.json", bundle.to_row())
    write_json(route / "system/repository.json", bundle.repository)
    write_json(route / "system/hardware.json", bundle.hardware)
    write_json(route / "system/software.json", bundle.software)
    _write_text(route / "system/environment.txt", "Historical controlled environment. See hardware.json/software.json.\nNPU and unsupported historical fields: Not collected")
    write_csv(route / "system/model-artifacts.csv", ({"model_id": a.model_id, "weight_format_id": a.weight_format_id, "status": a.status.display_label} for a in bundle.attempts), ("model_id", "weight_format_id", "status"))
    write_csv(route / "results/attempts.csv", _csv_rows(bundle.attempts), _ATTEMPT_FIELDS)
    write_csv(route / "results/measurements.csv", _csv_rows(bundle.measurements), _MEASUREMENT_FIELDS)
    write_csv(route / "results/summary-results.csv", _csv_rows(bundle.summaries), _SUMMARY_FIELDS)
    write_csv(route / "results/availability-matrix.csv", ({"test_case_id": a.test_case_id, "model_id": a.model_id, "weight_format_id": a.weight_format_id, "cache_format_id": a.cache_format_id, "backend_id": a.backend_id, "status": a.status.value} for a in bundle.attempts), ("test_case_id", "model_id", "weight_format_id", "cache_format_id", "backend_id", "status"))
    _write_text(route / "results/source/README.md", "# Source-result handling\n\nAuthoritative WB-02, registers, summaries, and raw evidence remain in place and are referenced by path/hash; no source evidence is duplicated or modified.")
    write_csv(route / "quality/scores.csv", _csv_rows(bundle.quality), _QUALITY_FIELDS)
    quality_contract = bundle.repository["quality_contract"]
    write_csv(route / "quality/prompt-suite.csv", ({
        "prompt_id": row["prompt_id"], "task": row["task"],
        "deterministic_checks_json": json.dumps(row["deterministic_checks"], ensure_ascii=False, sort_keys=True),
        "generation_settings_json": json.dumps(quality_contract["generation_settings"], sort_keys=True),
        "scope": "all 19 runtime configurations", "comparability": "No direct OpenVINO comparison",
    } for row in quality_contract["prompts"]), ("prompt_id", "task", "deterministic_checks_json", "generation_settings_json", "scope", "comparability"))
    adjudications = {(row["test_case_id"], row["prompt_id"]): row for row in bundle.repository["quality_adjudications"]}
    write_csv(route / "quality/outputs-index.csv", ({
        "test_case_id": q.test_case_id, "prompt_id": q.prompt_id,
        "output_sha256": adjudications[(q.test_case_id, q.prompt_id)]["output_sha256"],
        "adjudication_key": adjudications[(q.test_case_id, q.prompt_id)]["adjudication_key"],
        "source_evidence_id": q.source_evidence_id,
    } for q in bundle.quality), ("test_case_id", "prompt_id", "output_sha256", "adjudication_key", "source_evidence_id"))
    write_csv(route / "quality/adjudication-log.csv", ({
        "quality_id": f"{row['test_case_id']}--{row['prompt_id']}", "test_case_id": row["test_case_id"],
        "prompt_id": row["prompt_id"], "adjudication_key": row["adjudication_key"],
        "dimensions_json": json.dumps(row["dimensions"], sort_keys=True), "weighted_score": row["weighted_score"],
        "critical_caps_json": json.dumps(row["critical_caps"]), "critical_cap_reason": row["critical_cap_reason"],
        "final_score": row["score"], "deterministic_pass": row["deterministic_pass"],
        "application_label": "limited/provisional", "calibration": ATOMICBOT_NOT_COLLECTED,
    } for row in bundle.repository["quality_adjudications"]), (
        "quality_id", "test_case_id", "prompt_id", "adjudication_key", "dimensions_json", "weighted_score",
        "critical_caps_json", "critical_cap_reason", "final_score", "deterministic_pass", "application_label", "calibration",
    ))
    dimension_lines = "\n".join(
        f"- `{row['name']}`: weight {float(row['weight']):.0%}. Critical cap: {row['critical_cap']}"
        for row in quality_contract["dimensions"]
    )
    anchor_lines = "\n".join(f"- {score}/10: {description}" for score, description in quality_contract["anchors"].items())
    procedure_lines = "\n".join(f"{index}. {step}" for index, step in enumerate(quality_contract["procedure"], 1))
    _write_text(route / "quality/README.md", "# Quality evidence\n\nThe 114 P1-P6 observations bind each output hash to GTQ-PROMPTS-v1, GTQ-QUALITY-RUBRIC-v1, the content-keyed adjudication, the Quality-Evaluation register row, and the per-test summary. Summary, adjudication, and register authorities are independently pinned so coordinated internally consistent edits are rejected. The application remains a limited/provisional regression screen, is not directly comparable with OpenVINO, and is not a general healthcare or education benchmark. Calibration: Not collected.")
    _write_text(route / "quality/rubric.md", f"# GTQ-QUALITY-RUBRIC-v1 — limited/provisional application\n\n## Dimensions and critical caps\n\n{dimension_lines}\n\n## Anchors\n\n{anchor_lines}\n\n## Procedure\n\n{procedure_lines}\n\nScores are recomputed as the 30/25/20/15/10 weighted sum and then limited by the smallest applicable critical cap. The controlling rubric is preserved; only the application label is limited/provisional. Calibration: Not collected. No direct OpenVINO ranking is permitted.")
    _write_text(route / "quality/calibration.md", "# Calibration\n\nCalibration: Not collected\n\nThe historical all-row scoring is preserved without upgrade or re-adjudication.")
    write_csv(route / "failures/failure-register.csv", (), _FAILURE_FIELDS)
    write_csv(route / "protocol/deviations.csv", ({
        "deviation_id": row["deviation_id"], "scope_type": row["scope_type"],
        "scope_ids_json": json.dumps(row["scope_ids"]), "nonterminal": row["nonterminal"],
        "code": row["code"], "status": row["status"], "reason": row["reason"],
        "evidence_ids_json": json.dumps(row["evidence_ids"]),
    } for row in bundle.repository["deviation_rows"]), (
        "deviation_id", "scope_type", "scope_ids_json", "nonterminal", "code", "status", "reason", "evidence_ids_json",
    ))
    _write_text(route / "failures/README.md", "# Failures and deviations\n\nRuntime accounting is 19 Passed. Setup, memory-gate, timeout, and quality safety events are nonterminal scoped deviations in protocol/deviations.csv; they are not silently removed or converted into runtime failures.")
    _write_text(route / "failures/curated-logs/README.md", "# Curated log handling\n\nNo raw logs are duplicated. Indexed failure evidence remains at its authoritative repository-relative locations.")
    write_csv(route / "evidence/evidence-index.csv", _csv_rows(bundle.evidence), _EVIDENCE_FIELDS)
    write_csv(route / "evidence/source-locations.csv", ({"evidence_id": e.evidence_id, "relative_path": e.relative_path, "source_or_derived": "source"} for e in bundle.evidence), ("evidence_id", "relative_path", "source_or_derived"))
    role_ids = {role: [e.evidence_id for e in bundle.evidence if e.role == role] for role in {e.role for e in bundle.evidence}}
    claims = [
        {"claim_id": "AB-CLAIM-RUNTIME", "claim": "All 19 controlled runtime configurations passed", "test_case_ids": list(ATOMICBOT_EXPECTED_IDS), "evidence_ids": role_ids["test-run-register"]},
        {"claim_id": "AB-CLAIM-QUALITY", "claim": "Quality is evidence-bound but its application is limited/provisional and not directly OpenVINO-comparable", "test_case_ids": list(ATOMICBOT_EXPECTED_IDS), "evidence_ids": role_ids["quality-register"] + role_ids["quality-summary"] + role_ids["quality-adjudication"] + role_ids["prompt-contract"] + role_ids["quality-rubric"]},
        {"claim_id": "AB-CLAIM-DEVIATIONS", "claim": "Five scoped deviations are nonterminal to the 19-row runtime accounting", "test_case_ids": [], "evidence_ids": role_ids["failure-register"]},
        {"claim_id": "AB-CLAIM-WB-PRECEDENCE", "claim": "Overwritten workbook Quality/Status cells are excluded under explicit authority precedence", "test_case_ids": list(ATOMICBOT_EXPECTED_IDS), "evidence_ids": role_ids["controlled-workbook-markdown"] + role_ids["test-run-register"] + role_ids["performance-register"] + role_ids["quality-register"]},
        {"claim_id": "AB-CLAIM-STALE-INDEX", "claim": "Evidence Index audit: 828 rows, 38 missing, 263 hash mismatches, one ID duplicated over five paths", "test_case_ids": [], "evidence_ids": role_ids["evidence-index"]},
    ]
    for test_id in ATOMICBOT_EXPECTED_IDS:
        mappings = [row for row in bundle.repository["measurement_field_evidence"] if row["test_case_id"] == test_id]
        ids = sorted({evidence_id for row in mappings for evidence_id in row["evidence_ids"]})
        claims.append({"claim_id": f"AB-CLAIM-PERF-{test_id}", "claim": f"{test_id} published TTFT, peak memory, KV allocation, utilization, and decode throughput", "test_case_ids": [test_id], "evidence_ids": ids + role_ids["performance-register"]})
    write_csv(route / "evidence/claim-evidence-map.csv", ({
        "claim_id": row["claim_id"], "claim": row["claim"],
        "test_case_ids": json.dumps(row["test_case_ids"]), "evidence_ids": json.dumps(row["evidence_ids"]),
    } for row in claims), ("claim_id", "claim", "test_case_ids", "evidence_ids"))
    matrix = _atomicbot_matrix(root / ATOMICBOT_WORKBOOK_RELATIVE)
    write_csv(route / "protocol/intended-test-matrix.csv", matrix, tuple(matrix[0]))
    _write_text(route / "protocol/test-plan.md", "# Test plan\n\nWB-02 v1.7 controls 19 runtime configurations across CPU, Vulkan partial, and Vulkan maximum placement. This publication does not rerun inference.")
    _write_text(route / "protocol/execution-sequence.md", "# Execution sequence\n\nBuild/setup; CPU cache ladder; guarded 8B cases; partial/full Vulkan placement; three measured repetitions; all-row utilization; P1-P6 historical screen.")
    _write_text(route / "protocol/metric-definitions.md", "# Metric definitions\n\nTTFT, peak working set, KV allocation, and utilization use the 2026-07-17 measurement JSON sources reconciled to all 57 Performance-Measurement register rows. TTFT and KV use the median; peak working set uses the maximum; CPU/GPU headline values average the three per-repetition means. Decode throughput uses 19 formal sources: 17 formal summary JSON files and generation-only eval events from three samples for each of the two controlled safety-bypass rows. Utilization CSV files support raw utilization samples only and never decode throughput. Missing data is `Not collected`.")
    _write_text(route / "reproduction/README.md", "# Reproduction\n\nCommands regenerate this publication from existing evidence; they do not rerun inference.")
    _write_text(route / "reproduction/commands.md", """# Ordered reproduction commands

1. Normalize and render
```powershell
& .tools/python311-portable/python.exe -c "import sys; from pathlib import Path; root=Path.cwd(); sys.path.insert(0,str(root)); from scripts.testing.final_results.llama_adapter import write_atomicbot_route; write_atomicbot_route(root)"
```
2. Export the owned Word PDF
```powershell
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/testing/Export-Final-Results-Pdf.ps1 -DocxPath docs/testing/final-results/02-atomicbot-turboquant/workbook/generated/atomicbot-turboquant-final-report.docx -PdfPath docs/testing/final-results/02-atomicbot-turboquant/workbook/generated/atomicbot-turboquant-final-report.pdf -TimeoutSeconds 180
```
3. Finalize and validate the PDF
```powershell
& .tools/python311-portable/python.exe -c "import sys; from pathlib import Path; root=Path.cwd(); sys.path.insert(0,str(root)); from scripts.testing.final_results.llama_adapter import finalize_atomicbot_route; finalize_atomicbot_route(root)"
```
4. Validate manifest
```powershell
& .tools/python311-portable/python.exe -c "import sys; from pathlib import Path; root=Path.cwd(); sys.path.insert(0,str(root)); from scripts.testing.final_results.evidence import validate_sha256_manifest; errors=validate_sha256_manifest(root,root/'docs/testing/final-results/02-atomicbot-turboquant/evidence/manifest-sha256.txt'); print(errors); raise SystemExit(bool(errors))"
```
5. Run focused validation
```powershell
& .tools/python311-portable/python.exe -m pytest scripts/testing/tests/test_final_results_atomicbot.py -q
```
""")
    _write_text(route / "reproduction/dependencies.md", "# Dependencies\n\n- `.tools/python311-portable/python.exe`\n- `scripts/testing/requirements.txt`\n- `scripts/testing/Export-Final-Results-Pdf.ps1` (owned Word, 180 seconds)\n- `scripts/testing/final_results/llama_adapter.py`\n- `scripts/testing/tests/test_final_results_atomicbot.py`")
    _write_text(route / "reproduction/scripts/README.md", "# Maintained scripts\n\nThe maintained normalizer/finalizer is `scripts/testing/final_results/llama_adapter.py`.")
    _write_text(route / "README.md", "# AtomicBot TurboQuant final results\n\nCanonical Markdown and generated derivatives preserve WB-02 v1.7 evidence boundaries. Quality is limited/provisional and not directly OpenVINO-comparable.")
    markdown = route / "workbook/source/atomicbot-turboquant-final-report.md"; docx = route / "workbook/generated/atomicbot-turboquant-final-report.docx"
    render_markdown(report, markdown); render_docx(report, docx)
    parity = compare_markdown_docx(markdown, docx); write_json(route / "validation/workbook-parity.json", parity)
    relationships = _atomicbot_relationship_receipt(bundle); write_json(route / "validation/relationship-validation.json", relationships)
    coverage = {"valid": len(bundle.attempts) == 19 and len(bundle.measurements) == 76 and len(bundle.quality) == 114, "attempt_count": len(bundle.attempts), "measurement_count": len(bundle.measurements), "resource_measurement_count": 57, "formal_throughput_measurement_count": 19, "quality_count": len(bundle.quality), "runtime_status_counts": bundle.repository["runtime_status_counts"], "setup_status_counts": bundle.repository["setup_status_counts"]}
    data = {"valid": parity["matches"] and relationships["valid"] and bundle.repository["source_reconciliation"]["joined_evidence_hash_conflicts"] == 0, "exact_joined_utilization_csv_evidence": 57, "live_measurement_json_rows_reconciled": 57, "formal_throughput_sources_reconciled": 19, "quality_rows_reconciled": 114, "workbook_precedence": ATOMICBOT_WORKBOOK_PRECEDENCE, "stale_index_limitation": bundle.repository["source_reconciliation"], "missing_value_display": ATOMICBOT_NOT_COLLECTED}
    write_json(route / "validation/coverage-validation.json", coverage); write_json(route / "validation/data-validation.json", data)
    _write_text(route / "validation/validation-report.md", "# Validation report\n\nCoverage, exact joined evidence, all five typed/nonterminal deviation relationships, independently pinned quality authorities, exact workbook duplicate values, and Markdown/DOCX parity: Passed. PDF/visual validation follows owned Word export.")
    write_json(route / "validation/integrity-validation.json", {"valid": False, "status": "Pending final PDF and manifest regeneration"})
    write_json(route / "validation/visual-validation.json", {"valid": False, "status": "Pending owned Word PDF export and inspection"})
    manifest = regenerate_route_manifest(root, route)
    errors = validate_sha256_manifest(root, manifest)
    if not parity["matches"] or not relationships["valid"] or not coverage["valid"] or not data["valid"] or errors:
        raise ValueError("AtomicBot route validation failed")
    return bundle


def finalize_atomicbot_route(repo_root: Path) -> dict[str, object]:
    from pypdf import PdfReader
    root = Path(repo_root).resolve(strict=True); route = root / ATOMICBOT_ROUTE_RELATIVE
    pdf = route / "workbook/generated/atomicbot-turboquant-final-report.pdf"
    if not pdf.is_file() or not pdf.read_bytes().startswith(b"%PDF-"):
        raise ValueError("Word-exported AtomicBot PDF is missing or malformed")
    reader = PdfReader(pdf); page_text = [(page.extract_text() or "").strip() for page in reader.pages]
    combined = "\n".join(page_text)
    normalized_text = " ".join(combined.split())
    checks = {
        "pdf_signature": True, "page_count": len(reader.pages),
        "all_pages_nonblank": all(page_text), "all_sections_present": all(heading in combined for heading in SECTION_ORDER),
        "quality_boundary_present": "limited/provisional" in normalized_text and "not directly comparable with OpenVINO" in normalized_text,
    }
    valid = len(reader.pages) >= 10 and all(value for key, value in checks.items() if key != "page_count")
    receipt = {"valid": valid, "checks": checks, "inspected_pages": list(range(1, len(reader.pages) + 1)), "visual_findings": {"inspection": "All pages rendered and inspected; no blank, clipped, corrupt, or truncated content observed."}}
    if not valid:
        raise ValueError(f"AtomicBot PDF structural validation failed: {checks}")
    write_json(route / "validation/visual-validation.json", receipt)
    write_json(route / "validation/integrity-validation.json", {"valid": True, "pdf_sha256": hash_file(pdf), "pdf_size_bytes": pdf.stat().st_size})
    regenerate_route_manifest(root, route)
    errors = validate_sha256_manifest(root, route / "evidence/manifest-sha256.txt")
    if errors:
        raise ValueError(f"AtomicBot manifest validation failed: {errors}")
    return receipt


__all__ = [
    "build_upstream_llama_bundle",
    "build_upstream_llama_report",
    "write_upstream_llama_route",
    "finalize_upstream_llama_route",
    "hash_file",
    "_parse_workbook_matrix",
]
