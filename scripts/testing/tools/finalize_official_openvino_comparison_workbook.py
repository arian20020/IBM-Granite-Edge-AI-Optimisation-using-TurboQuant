"""Render and validate the hash-bound WB-04 adaptive comparison workbook.

The release input is reconciled by Task 7.  This module does not search for
attempts, trust precomputed workbook cells, or execute a runtime.  It renders
only the evidence represented by ``ComparisonRelease`` and reparses every
generated table before an atomic publication.
"""

from __future__ import annotations

import hashlib
import math
import os
import re
import sys
import tempfile
from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[3]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.campaigns.openvino.comparison_reconcile import (
    ComparisonKey,
    ComparisonRelease,
    ComparisonRuntimeOutcome,
    reconcile_comparison_release,
    validate_complete_release,
)


TARGET_WORKBOOK_VERSION = "1.9"
TARGET_REVISION_ID = "WR-037"
TARGET_REVISION_DATE = "2026-08-01"
COMPARISON_SECTION_TITLES = (
    "U8 STANDARD, TBQ4 and TBQ3 shared-context comparison",
    "U4, U8 and FP16 STANDARD deployment comparison",
    "Complete timing results",
    "Complete memory and CPU/GPU results",
    "P1–P6 and aggregate quality results",
    "Laptop runtime-capable and fully-comparable boundaries",
    "Terminal attempts and hash-bound evidence",
)

_BEGIN_MARKER = "<!-- BEGIN WB-04 V1.9 COMPARISON -->"
_END_MARKER = "<!-- END WB-04 V1.9 COMPARISON -->"
_PROMPTS = ("P1", "P2", "P3", "P4", "P5", "P6")
_TIMING = (
    ("Load ms", "load_ms"),
    ("TTFT ms", "ttft_ms"),
    ("Prompt tok/s", "prompt_tps"),
    ("TPOT ms", "tpot_ms"),
    ("Decode tok/s", "decode_tps"),
    ("Generation ms", "generation_duration_ms"),
)
_CACHE_ROUTES = {
    "OV-12": "U8 STANDARD",
    "OV-TQ-21": "TBQ4",
    "OV-TQ-22": "TBQ3",
}
_STANDARD_WEIGHTS = {
    "OV-11": "U4 STANDARD",
    "OV-12": "U8 STANDARD",
    "OV-13": "FP16 STANDARD",
}
_PLACEHOLDERS = frozenset({"N/A", "NA", "TBD", "TODO", "TBC", "-", "—"})
_SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
_TOP_LEVEL_FIVE_RE = re.compile(r"(?m)^# 5\.[^\n]*$")


@dataclass(frozen=True)
class _Table:
    header: tuple[str, ...]
    rows: tuple[tuple[str, ...], ...]


def _finite_number(value: Any, label: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{label} must be numeric")
    number = float(value)
    if not math.isfinite(number):
        raise ValueError(f"{label} must be finite")
    return number


def format_number(value: Any) -> str:
    """Apply the one deterministic display policy used by render and validation."""

    number = _finite_number(value, "displayed value")
    if number == 0:
        return "0"
    rendered = f"{number:.6f}".rstrip("0").rstrip(".")
    return "0" if rendered == "-0" else rendered


_MARKDOWN_META = frozenset("\\`*_[]()|!")


def _escape_cell(value: Any) -> str:
    """Encode one cell reversibly without activating CommonMark syntax."""

    text = str(value).replace("\r\n", "\n").replace("\r", "\n")
    result: list[str] = []
    last = len(text) - 1
    for index, character in enumerate(text):
        if character == "\n":
            result.append("<br>")
        elif character == "\t":
            result.append("&#9;")
        elif character == " " and (
            index == 0
            or index == last
            or (index > 0 and text[index - 1].isspace())
            or (index < last and text[index + 1].isspace())
        ):
            result.append("&#32;")
        elif character.isspace() and character != " ":
            result.append(f"&#{ord(character)};")
        elif character == "&":
            result.append("&amp;")
        elif character == "<":
            result.append("&lt;")
        elif character == ">":
            result.append("&gt;")
        elif character in _MARKDOWN_META:
            result.append(f"&#{ord(character)};")
        else:
            result.append(character)
    return "".join(result)


def _escaped_table(table: _Table) -> _Table:
    return _Table(
        tuple(_escape_cell(cell) for cell in table.header),
        tuple(
            tuple(_escape_cell(cell) for cell in row)
            for row in table.rows
        ),
    )


def _render_table(table: _Table) -> str:
    def row(values: Sequence[str]) -> str:
        return "| " + " | ".join(_escape_cell(value) for value in values) + " |"

    separator = tuple("---" for _ in table.header)
    return "\n".join(
        (row(table.header), row(separator), *(row(values) for values in table.rows))
    )


def _sha256(value: Any, label: str) -> str:
    if not isinstance(value, str) or _SHA256_RE.fullmatch(value) is None:
        raise ValueError(f"{label} must be a lowercase SHA-256")
    return value


def _repository_relative(path: Path) -> str:
    source = Path(path)
    if source.is_absolute():
        try:
            source = source.resolve().relative_to(ROOT.resolve())
        except ValueError as error:
            raise ValueError("evidence path is outside the repository") from error
    if source.anchor or ".." in source.parts:
        raise ValueError("evidence path must be repository-relative")
    value = source.as_posix()
    if not value or value == ".":
        raise ValueError("evidence path must identify a repository file")
    return value


def _evidence(path: Path, digest: str, label: str) -> str:
    return f"{_repository_relative(path)}#sha256={_sha256(digest, label)}"


def _ordered_runtime(
    release: ComparisonRelease,
) -> list[tuple[ComparisonKey, ComparisonRuntimeOutcome]]:
    return sorted(release.runtime.items(), key=lambda item: item[0])


def _metric_values(
    outcome: ComparisonRuntimeOutcome,
    group: str,
    metric: str,
) -> Mapping[str, Any]:
    container = getattr(outcome, group)
    value = container.get(metric)
    if not isinstance(value, Mapping):
        raise ValueError(f"{outcome.key} lacks {metric}")
    return value


def _three_values(value: Mapping[str, Any], label: str) -> tuple[str, str, str]:
    raw = value.get("values")
    if not isinstance(raw, (list, tuple)) or len(raw) != 3:
        raise ValueError(f"{label} requires exactly three samples")
    return tuple(format_number(item) for item in raw)  # type: ignore[return-value]


def _aggregate(value: Mapping[str, Any], field: str, label: str) -> str:
    if field not in value:
        raise ValueError(f"{label} lacks {field}")
    return format_number(value[field])


def _activation_label(outcome: ComparisonRuntimeOutcome) -> str:
    activation = outcome.activation
    telemetry = activation.get("telemetry") if isinstance(activation, Mapping) else None
    device = activation.get("device") if isinstance(activation, Mapping) else None
    if not isinstance(telemetry, Mapping) or not isinstance(device, Mapping):
        raise ValueError(f"{outcome.key} activation evidence is incomplete")
    actual_device = device.get("actual") or telemetry.get("actual_device")
    key_algorithm = telemetry.get("activated_key_algorithm")
    value_algorithm = telemetry.get("activated_value_algorithm")
    observed_key = telemetry.get("observed_key_state_precision")
    observed_value = telemetry.get("observed_value_state_precision")
    fields = (actual_device, key_algorithm, value_algorithm, observed_key, observed_value)
    if not all(isinstance(field, str) and field.strip() for field in fields):
        raise ValueError(f"{outcome.key} activation evidence is incomplete")
    route = (
        str(key_algorithm)
        if key_algorithm == value_algorithm
        else f"{key_algorithm}/{value_algorithm}"
    )
    return (
        f"{actual_device} {route}; observed "
        f"{observed_key}/{observed_value} state"
    )


def _comparison_table(
    release: ComparisonRelease,
    *,
    contexts: Sequence[int],
    routes: Mapping[str, str],
    route_header: str,
    artifact_header: str,
    require_common_artifact: bool,
) -> _Table:
    rows: list[tuple[str, ...]] = []
    for context in contexts:
        context_rows: list[ComparisonRuntimeOutcome] = []
        for test_id, display in routes.items():
            key = ComparisonKey(test_id, context)
            outcome = release.runtime.get(key)
            if outcome is None:
                raise ValueError(f"shared context lacks successful runtime row: {key}")
            context_rows.append(outcome)
            artifact = _sha256(
                outcome.identity_hashes.get("artifact_manifest_sha256"),
                f"{key} artifact manifest",
            )
            rows.append(
                (
                    test_id,
                    str(context),
                    display,
                    _activation_label(outcome),
                    artifact,
                    _evidence(
                        outcome.evidence_path,
                        outcome.evidence_sha256,
                        f"{key} runtime evidence",
                    ),
                )
            )
        if require_common_artifact:
            artifacts = {
                row.identity_hashes.get("artifact_manifest_sha256")
                for row in context_rows
            }
            if len(artifacts) != 1:
                raise ValueError(
                    "shared cache comparison requires the same validated U8 artifact"
                )
    return _Table(
        (
            "Test ID",
            "Context",
            route_header,
            "Activation",
            artifact_header,
            "Runtime evidence",
        ),
        tuple(rows),
    )


def _overall_winner(release: ComparisonRelease) -> str | None:
    if not release.shared_cache_contexts:
        return None
    scores: dict[str, list[float]] = {test_id: [] for test_id in _CACHE_ROUTES}
    for context in release.shared_cache_contexts:
        for test_id in _CACHE_ROUTES:
            outcome = release.quality.get(ComparisonKey(test_id, context))
            if (
                outcome is None
                or outcome.status != "quality-complete"
                or not isinstance(outcome.prompt_scores, Mapping)
                or set(outcome.prompt_scores) != set(_PROMPTS)
            ):
                return None
            scores[test_id].extend(
                _finite_number(outcome.prompt_scores[prompt], f"{test_id} {prompt}")
                for prompt in _PROMPTS
            )
    means = {
        test_id: sum(values) / len(values) for test_id, values in scores.items()
    }
    best = max(means.values())
    winners = [test_id for test_id, value in means.items() if value == best]
    return winners[0] if len(winners) == 1 else None


def _cache_table(release: ComparisonRelease) -> _Table:
    return _comparison_table(
        release,
        contexts=release.shared_cache_contexts,
        routes=_CACHE_ROUTES,
        route_header="Cache route",
        artifact_header="U8 artifact SHA-256",
        require_common_artifact=True,
    )


def render_cache_comparison(release: ComparisonRelease) -> str:
    lines = [
        "Only contexts completed by all three U8 routes are shown; every row names the same validated U8 artifact.",
        "",
        _render_table(_cache_table(release)),
    ]
    winner = _overall_winner(release)
    if winner is not None:
        lines.extend(
            ("", f"Overall winner: **{winner}** (shared complete numeric quality).")
        )
    return "\n".join(lines)


def _standard_table(release: ComparisonRelease) -> _Table:
    return _comparison_table(
        release,
        contexts=release.shared_standard_contexts,
        routes=_STANDARD_WEIGHTS,
        route_header="Weight format",
        artifact_header="Artifact SHA-256",
        require_common_artifact=False,
    )


def render_standard_weight_comparison(release: ComparisonRelease) -> str:
    return "\n".join(
        (
            "Only shared completed STANDARD contexts are compared; K/V state labels come from observed activation telemetry.",
            "",
            _render_table(_standard_table(release)),
        )
    )


def _timing_tables(release: ComparisonRelease) -> tuple[_Table, ...]:
    tables: list[_Table] = []
    for _, metric in _TIMING:
        rows: list[tuple[str, ...]] = []
        for key, outcome in _ordered_runtime(release):
            values = _metric_values(outcome, "timing", metric)
            rows.append(
                (
                    key.test_id,
                    str(key.context_tokens),
                    *_three_values(values, f"{key} {metric}"),
                    _aggregate(values, "mean", f"{key} {metric}"),
                    _aggregate(values, "median", f"{key} {metric}"),
                )
            )
        tables.append(
            _Table(
                ("Test ID", "Context", "S1", "S2", "S3", "Mean", "Median"),
                tuple(rows),
            )
        )
    return tuple(tables)


def render_timing_results(release: ComparisonRelease) -> str:
    parts: list[str] = []
    for (title, _), table in zip(_TIMING, _timing_tables(release), strict=True):
        parts.extend((f"**{title}**", "", _render_table(table), ""))
    return "\n".join(parts).rstrip()


def _memory_tables(release: ComparisonRelease) -> tuple[_Table, _Table, _Table]:
    peak_rows: list[tuple[str, ...]] = []
    ram_rows: list[tuple[str, ...]] = []
    utilisation_rows: list[tuple[str, ...]] = []
    for key, outcome in _ordered_runtime(release):
        ws = _metric_values(outcome, "memory", "peak_working_set_mib")
        private = _metric_values(outcome, "memory", "peak_private_mib")
        available = _metric_values(outcome, "memory", "available_ram_mib")
        kv = _metric_values(outcome, "memory", "kv_mib")
        gpu_memory = _metric_values(outcome, "memory", "gpu_memory_peak_mib")
        cpu = _metric_values(outcome, "utilisation", "cpu_percent")
        gpu = _metric_values(outcome, "utilisation", "gpu_percent")
        peak_rows.append(
            (
                key.test_id,
                str(key.context_tokens),
                *_three_values(ws, f"{key} working set"),
                _aggregate(ws, "median", f"{key} working set"),
                _aggregate(ws, "worst_max", f"{key} working set"),
                _aggregate(private, "median", f"{key} private memory"),
                _aggregate(private, "worst_max", f"{key} private memory"),
            )
        )
        ram_rows.append(
            (
                key.test_id,
                str(key.context_tokens),
                _aggregate(available, "global_min", f"{key} available RAM"),
                _aggregate(kv, "median", f"{key} KV memory"),
                _aggregate(gpu_memory, "worst_max", f"{key} GPU memory"),
            )
        )
        utilisation_rows.append(
            (
                key.test_id,
                str(key.context_tokens),
                _aggregate(cpu, "mean", f"{key} CPU utilisation"),
                _aggregate(cpu, "median", f"{key} CPU utilisation"),
                _aggregate(cpu, "peak", f"{key} CPU utilisation"),
                str(int(_finite_number(cpu.get("count"), f"{key} CPU sample count"))),
                _aggregate(gpu, "mean", f"{key} GPU utilisation"),
                _aggregate(gpu, "median", f"{key} GPU utilisation"),
                _aggregate(gpu, "peak", f"{key} GPU utilisation"),
                str(int(_finite_number(gpu.get("count"), f"{key} GPU sample count"))),
            )
        )
    return (
        _Table(
            (
                "Test ID",
                "Context",
                "WS S1 MiB",
                "WS S2 MiB",
                "WS S3 MiB",
                "WS median MiB",
                "WS worst MiB",
                "Private median MiB",
                "Private worst MiB",
            ),
            tuple(peak_rows),
        ),
        _Table(
            (
                "Test ID",
                "Context",
                "Available RAM minimum MiB",
                "KV MiB",
                "GPU memory peak MiB",
            ),
            tuple(ram_rows),
        ),
        _Table(
            (
                "Test ID",
                "Context",
                "CPU mean %",
                "CPU median %",
                "CPU peak %",
                "CPU samples",
                "GPU mean %",
                "GPU median %",
                "GPU peak %",
                "GPU samples",
            ),
            tuple(utilisation_rows),
        ),
    )


def render_memory_utilisation_results(release: ComparisonRelease) -> str:
    titles = (
        "**Peak process memory**",
        "**RAM, KV and GPU memory**",
        "**CPU and GPU utilisation**",
    )
    parts: list[str] = []
    for title, table in zip(titles, _memory_tables(release), strict=True):
        parts.extend((title, "", _render_table(table), ""))
    return "\n".join(parts).rstrip()


def _quality_table(release: ComparisonRelease) -> _Table:
    rows: list[tuple[str, ...]] = []
    for key, outcome in sorted(release.quality.items(), key=lambda item: item[0]):
        if outcome.status != "quality-complete":
            continue
        if (
            not isinstance(outcome.prompt_scores, Mapping)
            or set(outcome.prompt_scores) != set(_PROMPTS)
            or not isinstance(outcome.aggregates, Mapping)
        ):
            raise ValueError(f"{key} complete quality result is incomplete")
        rows.append(
            (
                key.test_id,
                str(key.context_tokens),
                *(format_number(outcome.prompt_scores[prompt]) for prompt in _PROMPTS),
                *(
                    _aggregate(outcome.aggregates, field, f"{key} quality")
                    for field in ("mean", "median", "minimum", "maximum")
                ),
                _evidence(
                    outcome.evidence_path,
                    outcome.evidence_sha256,
                    f"{key} quality evidence",
                ),
            )
        )
    return _Table(
        (
            "Test ID",
            "Context",
            *_PROMPTS,
            "Mean",
            "Median",
            "Minimum",
            "Maximum",
            "Quality evidence",
        ),
        tuple(rows),
    )


def render_quality_results(release: ComparisonRelease) -> str:
    return "\n".join(
        (
            "Only governed rows with numeric P1 through P6 adjudication are shown.",
            "",
            _render_table(_quality_table(release)),
        )
    )


def _boundary_value(value: int | str | None) -> str:
    return "not-observed" if value is None else str(value)


def _boundary_table(release: ComparisonRelease) -> _Table:
    rows = tuple(
        (
            test_id,
            _boundary_value(boundary.highest_runtime_context),
            _boundary_value(boundary.highest_fully_comparable_context),
            _boundary_value(boundary.first_confirmed_blocked_context),
            _boundary_value(boundary.terminal_stage),
            (
                _sha256(
                    boundary.terminal_evidence_sha256,
                    f"{test_id} terminal evidence",
                )
                if boundary.terminal_evidence_sha256 is not None
                else "not-observed"
            ),
        )
        for test_id, boundary in sorted(release.boundaries.items())
    )
    return _Table(
        (
            "Test ID",
            "Highest runtime context",
            "Highest fully comparable context",
            "First confirmed blocked context",
            "Terminal stage",
            "Terminal evidence SHA-256",
        ),
        rows,
    )


def render_laptop_boundaries(release: ComparisonRelease) -> str:
    return _render_table(_boundary_table(release))


def _terminal_table(release: ComparisonRelease) -> _Table:
    rows: list[tuple[str, ...]] = []
    seen: set[tuple[ComparisonKey, str]] = set()
    for key, terminal in sorted(release.terminals.items(), key=lambda item: item[0]):
        stage = terminal.get("stage")
        reason = terminal.get("principal_reason")
        path = terminal.get("evidence_path")
        digest = terminal.get("evidence_sha256")
        if (
            not isinstance(stage, str)
            or not stage.strip()
            or not isinstance(reason, str)
            or not reason.strip()
            or not isinstance(path, Path)
        ):
            raise ValueError(f"{key} terminal evidence is incomplete")
        rows.append(
            (
                f"{key.test_id}/{key.context_tokens}",
                stage,
                reason,
                _evidence(path, digest, f"{key} terminal evidence"),
            )
        )
        seen.add((key, stage))
    for key, outcome in sorted(release.quality.items(), key=lambda item: item[0]):
        if outcome.status != "quality-terminal":
            continue
        if (
            not isinstance(outcome.terminal_stage, str)
            or not outcome.terminal_stage.strip()
            or not isinstance(outcome.principal_reason, str)
            or not outcome.principal_reason.strip()
        ):
            raise ValueError(f"{key} quality terminal lacks governed stage or reason")
        identity = (key, outcome.terminal_stage)
        if identity in seen:
            raise ValueError(f"{key} has duplicate terminal evidence")
        rows.append(
            (
                f"{key.test_id}/{key.context_tokens}",
                outcome.terminal_stage,
                outcome.principal_reason,
                _evidence(
                    outcome.evidence_path,
                    outcome.evidence_sha256,
                    f"{key} quality terminal evidence",
                ),
            )
        )
        seen.add(identity)
    rows.sort(key=lambda row: (row[0].split("/", 1)[0], int(row[0].split("/", 1)[1]), row[1]))
    return _Table(
        ("Identity/context", "Stage", "Principal reason", "Evidence"),
        tuple(rows),
    )


def _evidence_lines(release: ComparisonRelease) -> tuple[str, ...]:
    lines: list[str] = []
    for key, outcome in _ordered_runtime(release):
        lines.append(
            f"- Runtime `{key.test_id}/{key.context_tokens}`: "
            f"{_escape_cell(_evidence(outcome.evidence_path, outcome.evidence_sha256, f'{key} runtime evidence'))}"
        )
    for key, outcome in sorted(release.quality.items(), key=lambda item: item[0]):
        lines.append(
            f"- Quality `{key.test_id}/{key.context_tokens}`: "
            f"{_escape_cell(_evidence(outcome.evidence_path, outcome.evidence_sha256, f'{key} quality evidence'))}"
        )
    return tuple(lines)


def render_terminal_attempts(release: ComparisonRelease) -> str:
    return "\n".join(
        (
            _render_table(_terminal_table(release)),
            "",
            "**Hash-bound evidence**",
            "",
            *_evidence_lines(release),
        )
    )


def _replace_identity(source: str) -> str:
    lines = source.splitlines()
    if not lines or not lines[0].startswith("# 04 Official OpenVINO Controlled Retest Workbook"):
        raise ValueError("source workbook identity is missing")
    lines[0] = (
        "# 04 Official OpenVINO Controlled Retest Workbook "
        f"v{TARGET_WORKBOOK_VERSION}"
    )
    controlled = next(
        (index for index, line in enumerate(lines) if line.startswith("Controlled retest revision ")),
        None,
    )
    if controlled is None:
        raise ValueError("source workbook revision record is missing")
    lines[controlled] = (
        f"Controlled retest revision {TARGET_WORKBOOK_VERSION} "
        f"({TARGET_REVISION_ID})."
    )
    labels = ("Workbook version:", "Revision ID:", "Revision date:")
    block_end = controlled + 1
    while block_end < len(lines) and (
        not lines[block_end].strip() or lines[block_end].startswith(labels)
    ):
        block_end += 1
    del lines[controlled + 1 : block_end]
    metadata = [
        f"Workbook version: {TARGET_WORKBOOK_VERSION}",
        f"Revision ID: {TARGET_REVISION_ID}",
        f"Revision date: {TARGET_REVISION_DATE}",
    ]
    lines[controlled + 1 : controlled + 1] = ["", *metadata, ""]
    return "\n".join(lines)


def _preserved_prefix(source: str) -> str:
    if source.count(_BEGIN_MARKER) == 1 and source.count(_END_MARKER) == 1:
        begin = source.index(_BEGIN_MARKER)
        return source[:begin].rstrip()
    if _BEGIN_MARKER in source or _END_MARKER in source:
        raise ValueError("comparison marker structure is invalid")
    matches = list(_TOP_LEVEL_FIVE_RE.finditer(source))
    if len(matches) != 1:
        raise ValueError("source workbook must have one top-level # 5 result body")
    return source[: matches[0].start()].rstrip()


def render_v19_workbook(source_text: str, release: ComparisonRelease) -> str:
    if not isinstance(source_text, str):
        raise TypeError("source workbook text must be a string")
    identified = _replace_identity(source_text)
    prefix = _preserved_prefix(identified)
    renderers = (
        render_cache_comparison,
        render_standard_weight_comparison,
        render_timing_results,
        render_memory_utilisation_results,
        render_quality_results,
        render_laptop_boundaries,
        render_terminal_attempts,
    )
    sections = []
    for number, (title, renderer) in enumerate(
        zip(COMPARISON_SECTION_TITLES, renderers, strict=True), start=5
    ):
        sections.append(f"# {number}. {title}\n\n{renderer(release).rstrip()}")
    generated = "\n\n".join((_BEGIN_MARKER, *sections, _END_MARKER))
    return f"{prefix}\n\n{generated}\n"


def _split_markdown_row(line: str) -> tuple[str, ...]:
    stripped = line.strip()
    if not stripped.startswith("|") or not stripped.endswith("|"):
        raise ValueError("Markdown table row is malformed")
    content = stripped[1:-1]
    cells: list[str] = []
    current: list[str] = []
    escaped = False
    for character in content:
        if escaped:
            current.append(character)
            escaped = False
        elif character == "\\":
            escaped = True
        elif character == "|":
            cells.append("".join(current).strip())
            current = []
        else:
            current.append(character)
    if escaped:
        current.append("\\")
    cells.append("".join(current).strip())
    return tuple(cells)


def _parse_tables(section: str) -> tuple[_Table, ...]:
    lines = section.splitlines()
    tables: list[_Table] = []
    index = 0
    while index < len(lines):
        if not lines[index].lstrip().startswith("|"):
            index += 1
            continue
        raw: list[str] = []
        while index < len(lines) and lines[index].lstrip().startswith("|"):
            raw.append(lines[index])
            index += 1
        if len(raw) < 2:
            raise ValueError("Markdown table is missing its separator")
        parsed = [_split_markdown_row(line) for line in raw]
        width = len(parsed[0])
        if width == 0 or any(len(row) != width for row in parsed):
            raise ValueError("Markdown table has inconsistent columns")
        if not all(re.fullmatch(r":?-{3,}:?", cell) for cell in parsed[1]):
            raise ValueError("Markdown table separator is malformed")
        tables.append(_Table(parsed[0], tuple(parsed[2:])))
    return tuple(tables)


def _non_table_lines(section: str) -> tuple[str, ...]:
    result: list[str] = []
    in_table = False
    for line in section.splitlines():
        is_table = line.lstrip().startswith("|")
        if is_table:
            in_table = True
            continue
        if in_table:
            in_table = False
        stripped = line.strip()
        if stripped:
            result.append(stripped)
    return tuple(result)


def _sections(text: str) -> tuple[str, ...]:
    if text.count(_BEGIN_MARKER) != 1 or text.count(_END_MARKER) != 1:
        raise ValueError("comparison section markers are invalid")
    begin = text.index(_BEGIN_MARKER) + len(_BEGIN_MARKER)
    end = text.index(_END_MARKER, begin)
    if text[end + len(_END_MARKER) :].strip():
        raise ValueError("comparison envelope has content after its end marker")
    body = text[begin:end]
    matches: list[re.Match[str]] = []
    for number, title in enumerate(COMPARISON_SECTION_TITLES, start=5):
        heading = f"# {number}. {title}"
        if text.count(heading) != 1:
            raise ValueError("comparison section heading is missing or duplicated")
        match = re.search(rf"(?m)^{re.escape(heading)}$", body)
        if match is None:
            raise ValueError("comparison section heading is outside the generated body")
        matches.append(match)
    if [match.start() for match in matches] != sorted(match.start() for match in matches):
        raise ValueError("comparison sections are out of order")
    if body[: matches[0].start()].strip():
        raise ValueError("comparison envelope has content before its first section")
    return tuple(
        body[match.end() : matches[index + 1].start() if index + 1 < len(matches) else len(body)].strip()
        for index, match in enumerate(matches)
    )


def _expected_tables(release: ComparisonRelease) -> tuple[tuple[_Table, ...], ...]:
    return (
        (_cache_table(release),),
        (_standard_table(release),),
        _timing_tables(release),
        _memory_tables(release),
        (_quality_table(release),),
        (_boundary_table(release),),
        (_terminal_table(release),),
    )


def _expected_sections(release: ComparisonRelease) -> tuple[str, ...]:
    return (
        render_cache_comparison(release),
        render_standard_weight_comparison(release),
        render_timing_results(release),
        render_memory_utilisation_results(release),
        render_quality_results(release),
        render_laptop_boundaries(release),
        render_terminal_attempts(release),
    )


def _table_error(section_index: int, table_index: int) -> str:
    if section_index in (0, 1):
        return "successful table"
    if section_index == 2:
        return "timing table"
    if section_index == 3:
        return ("peak memory table", "memory table", "utilisation table")[table_index]
    if section_index == 4:
        return "quality table"
    if section_index == 5:
        return "boundary table"
    return "terminal table"


def _compare_table(
    actual: _Table,
    expected: _Table,
    *,
    section_index: int,
    table_index: int,
) -> None:
    label = _table_error(section_index, table_index)
    expected = _escaped_table(expected)
    if actual.header != expected.header:
        raise ValueError(f"{label} header mismatch")
    if actual.rows == expected.rows:
        return
    for actual_row, expected_row in zip(actual.rows, expected.rows):
        if len(actual_row) != len(expected_row):
            raise ValueError(f"{label} row width mismatch")
        for column, (actual_cell, expected_cell) in enumerate(
            zip(actual_row, expected_row, strict=True)
        ):
            if actual_cell != expected_cell:
                header = actual.header[column].lower()
                if "evidence" in header or "sha-256" in header:
                    raise ValueError("evidence cell mismatch")
                raise ValueError(f"{label} cell mismatch")
    raise ValueError(f"{label} has duplicate, missing, or unexpected rows")


def validate_comparison_workbook_text(
    text: str,
    release: ComparisonRelease,
) -> None:
    """Reparse every comparison table/list and match it exactly to the release."""

    expected_title = (
        "# 04 Official OpenVINO Controlled Retest Workbook "
        f"v{TARGET_WORKBOOK_VERSION}"
    )
    expected_revision = (
        f"Controlled retest revision {TARGET_WORKBOOK_VERSION} "
        f"({TARGET_REVISION_ID})."
    )
    lines = text.splitlines()
    title_lines = tuple(
        line
        for line in lines
        if line.startswith("# 04 Official OpenVINO Controlled Retest Workbook")
    )
    revision_lines = tuple(
        line for line in lines if line.startswith("Controlled retest revision ")
    )
    if (
        not lines
        or lines[0] != expected_title
        or title_lines != (expected_title,)
        or revision_lines != (expected_revision,)
    ):
        raise ValueError("workbook identity is missing, contradictory, or duplicated")
    required_identity = (
        f"Workbook version: {TARGET_WORKBOOK_VERSION}",
        f"Revision ID: {TARGET_REVISION_ID}",
        f"Revision date: {TARGET_REVISION_DATE}",
    )
    if any(
        tuple(
            candidate
            for candidate in lines
            if candidate.startswith(expected.split(":", 1)[0] + ":")
        )
        != (expected,)
        for expected in required_identity
    ):
        raise ValueError("workbook identity is missing or duplicated")
    winner = _overall_winner(release)
    expected_global_winners = (
        (f"Overall winner: **{winner}** (shared complete numeric quality).",)
        if winner is not None
        else ()
    )
    actual_global_winners = tuple(
        line.strip()
        for line in text.splitlines()
        if line.strip().startswith("Overall winner:")
    )
    if actual_global_winners != expected_global_winners:
        raise ValueError("overall winner is not supported by shared numeric quality")
    actual_sections = _sections(text)
    expected_sections = _expected_sections(release)
    expected_tables = _expected_tables(release)
    for section_index, (actual_section, expected_section, tables) in enumerate(
        zip(actual_sections, expected_sections, expected_tables, strict=True)
    ):
        actual_tables = _parse_tables(actual_section)
        if len(actual_tables) != len(tables):
            raise ValueError(f"{_table_error(section_index, 0)} count mismatch")
        if section_index <= 4:
            for table in actual_tables:
                for row in table.rows:
                    if any(
                        not cell.strip() or cell.strip().upper() in _PLACEHOLDERS
                        for cell in row
                    ):
                        raise ValueError(
                            "successful table contains a blank or placeholder cell"
                        )
        for table_index, (actual, expected) in enumerate(
            zip(actual_tables, tables, strict=True)
        ):
            _compare_table(
                actual,
                expected,
                section_index=section_index,
                table_index=table_index,
            )
        actual_lines = _non_table_lines(actual_section)
        expected_lines = _non_table_lines(expected_section)
        actual_winners = tuple(
            line for line in actual_lines if line.startswith("Overall winner:")
        )
        expected_winners = tuple(
            line for line in expected_lines if line.startswith("Overall winner:")
        )
        if actual_winners != expected_winners:
            raise ValueError("overall winner is not supported by shared numeric quality")
        if actual_lines != expected_lines:
            if section_index == 2:
                raise ValueError("timing table metric or unit mismatch")
            if section_index == 6:
                raise ValueError("evidence list mismatch")
            raise ValueError(f"{_table_error(section_index, 0)} narrative mismatch")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def atomic_write_text(target: Path, text: str) -> None:
    destination = Path(target)
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            newline="\n",
            dir=destination.parent,
            prefix=f".{destination.name}.",
            suffix=".tmp",
            delete=False,
        ) as handle:
            temporary = Path(handle.name)
            handle.write(text)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, destination)
        temporary = None
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def finalize_release(
    release_input: Path,
    *,
    source_workbook: Path,
    target: Path,
    require_complete: bool,
) -> dict[str, Any]:
    release = reconcile_comparison_release(release_input)
    if require_complete:
        validate_complete_release(release)
    rendered = render_v19_workbook(
        Path(source_workbook).read_text(encoding="utf-8-sig"), release
    )
    validate_comparison_workbook_text(rendered, release)
    atomic_write_text(target, rendered)
    return {"path": str(target), "sha256": sha256_file(target)}


__all__ = [
    "COMPARISON_SECTION_TITLES",
    "TARGET_REVISION_DATE",
    "TARGET_REVISION_ID",
    "TARGET_WORKBOOK_VERSION",
    "atomic_write_text",
    "finalize_release",
    "format_number",
    "render_cache_comparison",
    "render_laptop_boundaries",
    "render_memory_utilisation_results",
    "render_quality_results",
    "render_standard_weight_comparison",
    "render_terminal_attempts",
    "render_timing_results",
    "render_v19_workbook",
    "sha256_file",
    "validate_comparison_workbook_text",
]
