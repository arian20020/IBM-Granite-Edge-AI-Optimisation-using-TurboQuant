"""Behavioral tests for the fail-closed WB-04 v1.9 comparison renderer."""

from __future__ import annotations

import hashlib
import statistics
from dataclasses import replace
from pathlib import Path
from typing import Any

import pytest

import scripts.testing.finalize_official_openvino_comparison_workbook as finalizer
from scripts.testing.finalize_official_openvino_comparison_workbook import (
    COMPARISON_SECTION_TITLES,
    finalize_release,
    render_v19_workbook,
    validate_comparison_workbook_text,
)
from scripts.testing.campaigns.openvino.comparison_reconcile import (
    BoundaryOutcome,
    ComparisonKey,
    ComparisonQualityOutcome,
    ComparisonRelease,
    ComparisonRuntimeOutcome,
)


PROMPTS = ("P1", "P2", "P3", "P4", "P5", "P6")
TIMING = (
    "load_ms",
    "ttft_ms",
    "prompt_tps",
    "tpot_ms",
    "decode_tps",
    "generation_duration_ms",
)
SOURCE = """# 04 Official OpenVINO Controlled Retest Workbook v1.8

Controlled retest revision 1.8 (WR-036).

# 1. Repository, runtime and host

Preserved project scope and environment.

# 2. Successful build and recovery checks

Preserved build and provenance.

# 3. Successful bounded diagnostics

Preserved method material.

# 4. Successful expected-rejection controls

Preserved controlled method.

[[PAGEBREAK]]

# 5. Accepted formal runtime measurements

Historical result body to replace.
"""


def _scalar(values: list[float]) -> dict[str, Any]:
    return {
        "values": values,
        "mean": statistics.fmean(values),
        "median": statistics.median(values),
        "min": min(values),
        "max": max(values),
        "count": len(values),
    }


def _memory(values: list[float], *, available: bool = False) -> dict[str, Any]:
    if available:
        return {"values": values, "global_min": min(values), "count": len(values)}
    return {
        "values": values,
        "median": statistics.median(values),
        "worst_max": max(values),
        "count": len(values),
    }


def _utilisation(values: list[float]) -> dict[str, Any]:
    return {
        "values": values,
        "mean": statistics.fmean(values),
        "median": statistics.median(values),
        "peak": max(values),
        "count": len(values),
    }


def _runtime(
    test_id: str,
    context: int,
    seed: float,
    *,
    artifact_sha256: str,
    algorithm: str = "STANDARD",
    observed: str = "f32",
) -> ComparisonRuntimeOutcome:
    key = ComparisonKey(test_id, context)
    timing = {
        name: _scalar([seed + offset, seed + offset + 1.0, seed + offset + 3.0])
        for offset, name in enumerate(TIMING, 1)
    }
    memory = {
        "peak_working_set_mib": _memory([seed + 101, seed + 103, seed + 102]),
        "peak_private_mib": _memory([seed + 81, seed + 83, seed + 82]),
        "available_ram_mib": _memory(
            [seed + 501, seed + 499, seed + 500], available=True
        ),
        "kv_mib": _memory([seed + 11, seed + 13, seed + 12]),
        "gpu_memory_peak_mib": _memory([seed + 2, seed + 4, seed + 3]),
    }
    utilisation = {
        "cpu_percent": _utilisation([10 + seed, 20 + seed, 30 + seed, 40 + seed]),
        "gpu_percent": _utilisation([1 + seed, 3 + seed, 5 + seed]),
    }
    telemetry = {
        "activated_key_algorithm": algorithm,
        "activated_value_algorithm": algorithm,
        "observed_key_state_precision": observed,
        "observed_value_state_precision": observed,
        "requested_device": "CPU",
        "actual_device": "CPU",
        "fallback": False,
    }
    samples = tuple(
        {
            name: values["values"][index]
            for name, values in timing.items()
        }
        for index in range(3)
    )
    return ComparisonRuntimeOutcome(
        key=key,
        samples=samples,  # type: ignore[arg-type]
        timing=timing,
        memory=memory,
        utilisation=utilisation,
        activation={
            "sample_count": 3,
            "telemetry": telemetry,
            "device": {"requested": "CPU", "actual": "CPU"},
            "fallback": False,
        },
        identity_hashes={
            "artifact_manifest_sha256": artifact_sha256,
            "prompt_sha256": "1" * 64,
            "matrix_sha256": "2" * 64,
            "build_provenance_sha256": "3" * 64,
            "command_sha256": "4" * 64,
            "evidence_sha256": "5" * 64,
        },
        evidence_path=Path(f"experiments/synthetic/runtime/{test_id}-{context}.json"),
        evidence_sha256=hashlib.sha256(f"runtime/{test_id}/{context}".encode()).hexdigest(),
    )


def _quality(
    test_id: str,
    context: int,
    score: float,
    *,
    status: str = "quality-complete",
) -> ComparisonQualityOutcome:
    key = ComparisonKey(test_id, context)
    if status != "quality-complete":
        return ComparisonQualityOutcome(
            key=key,
            status=status,
            prompt_scores=None,
            aggregates=None,
            evidence_path=Path(
                f"experiments/synthetic/quality/{test_id}-{context}-terminal.json"
            ),
            evidence_sha256=hashlib.sha256(
                f"quality-terminal/{test_id}/{context}".encode()
            ).hexdigest(),
            terminal_stage=("quality-capture" if status == "quality-terminal" else None),
            principal_reason=(
                "governed P1-P6 capture reached laptop RAM floor"
                if status == "quality-terminal"
                else None
            ),
        )
    prompt_scores = {
        prompt: score + (index * 0.1) for index, prompt in enumerate(PROMPTS)
    }
    values = list(prompt_scores.values())
    return ComparisonQualityOutcome(
        key=key,
        status=status,
        prompt_scores=prompt_scores,
        aggregates={
            "mean": statistics.fmean(values),
            "median": statistics.median(values),
            "minimum": min(values),
            "maximum": max(values),
        },
        evidence_path=Path(f"experiments/synthetic/quality/{test_id}-{context}.json"),
        evidence_sha256=hashlib.sha256(f"quality/{test_id}/{context}".encode()).hexdigest(),
    )


def complete_release() -> ComparisonRelease:
    shared_u8 = "a" * 64
    runtime_rows = (
        _runtime("OV-11", 512, 1, artifact_sha256="b" * 64),
        _runtime("OV-11", 1024, 2, artifact_sha256="b" * 64),
        _runtime("OV-11", 2048, 3, artifact_sha256="b" * 64),
        _runtime("OV-12", 512, 4, artifact_sha256=shared_u8),
        _runtime("OV-13", 512, 5, artifact_sha256="c" * 64),
        _runtime("OV-TQ-21", 512, 6, artifact_sha256=shared_u8, algorithm="TBQ4", observed="u8+f32+i32"),
        _runtime("OV-TQ-22", 512, 7, artifact_sha256=shared_u8, algorithm="TBQ3", observed="u8+f32+i32"),
        _runtime("OV-TQ-22", 1024, 8, artifact_sha256=shared_u8, algorithm="TBQ3", observed="u8+f32+i32"),
    )
    runtime = {row.key: row for row in runtime_rows}
    quality_rows = (
        _quality("OV-11", 512, 6.0),
        _quality("OV-11", 1024, 6.1),
        _quality("OV-11", 2048, 0.0, status="quality-terminal"),
        _quality("OV-12", 512, 7.0),
        _quality("OV-13", 512, 8.0),
        _quality("OV-TQ-21", 512, 9.0),
        _quality("OV-TQ-22", 512, 8.0),
        _quality("OV-TQ-22", 1024, 0.0, status="quality-terminal"),
    )
    quality = {row.key: row for row in quality_rows}
    terminal_key = ComparisonKey("OV-11", 4096)
    terminal_sha = hashlib.sha256(b"runtime terminal").hexdigest()
    terminals = {
        terminal_key: {
            "test_id": "OV-11",
            "context_tokens": 4096,
            "stage": "runtime-boundary",
            "principal_reason": "confirmed laptop RAM boundary",
            "evidence_path": Path("experiments/synthetic/terminal/OV-11-4096.json"),
            "evidence_sha256": terminal_sha,
        }
    }
    boundaries = {
        "OV-11": BoundaryOutcome(
            "OV-11", 2048, 1024, 4096, "runtime-boundary", terminal_sha
        ),
        "OV-12": BoundaryOutcome("OV-12", 512, 512, None, None, None),
        "OV-13": BoundaryOutcome("OV-13", 512, 512, None, None, None),
        "OV-TQ-21": BoundaryOutcome("OV-TQ-21", 512, 512, None, None, None),
        "OV-TQ-22": BoundaryOutcome("OV-TQ-22", 1024, 512, None, None, None),
    }
    return ComparisonRelease(
        runtime=runtime,
        quality=quality,
        terminals=terminals,
        shared_cache_contexts=(512,),
        shared_standard_contexts=(512,),
        boundaries=boundaries,
    )


def incomplete_release() -> ComparisonRelease:
    release = complete_release()
    key = ComparisonKey("OV-12", 512)
    quality = dict(release.quality)
    quality[key] = _quality(
        key.test_id,
        key.context_tokens,
        0.0,
        status="capture-complete-awaiting-adjudication",
    )
    return replace(release, quality=quality, shared_cache_contexts=())


def render_complete_workbook() -> str:
    return render_v19_workbook(SOURCE, complete_release())


def _section(text: str, title: str) -> str:
    start = text.index(title)
    following = [text.find(item, start + len(title)) for item in COMPARISON_SECTION_TITLES]
    ends = [position for position in following if position >= 0]
    return text[start : min(ends) if ends else len(text)]


def corrupt_first_success_cell(text: str, replacement: str) -> str:
    needle = "| OV-12 | 512 | U8 STANDARD | CPU STANDARD; observed f32/f32 state |"
    changed = needle.replace("U8 STANDARD", replacement, 1)
    assert needle in text
    return text.replace(needle, changed, 1)


def _duplicate_first_row(text: str, prefix: str) -> str:
    row = next(line for line in text.splitlines() if line.startswith(prefix))
    return text.replace(row + "\n", row + "\n" + row + "\n", 1)


def test_v19_identity_is_wr037() -> None:
    text = render_complete_workbook()
    assert "Workbook version: 1.9" in text
    assert "Revision ID: WR-037" in text
    assert "Revision date: 2026-08-01" in text


def test_preserves_method_material_and_replaces_results_idempotently() -> None:
    once = render_complete_workbook()
    twice = render_v19_workbook(once, complete_release())

    assert "Preserved project scope and environment." in once
    assert "Preserved build and provenance." in once
    assert "Preserved method material." in once
    assert "Historical result body to replace." not in once
    assert once == twice
    assert all(once.count(title) == 1 for title in COMPARISON_SECTION_TITLES)


@pytest.mark.parametrize("placeholder", ["", "N/A", "NA", "TBD", "TODO", "TBC", "-", "\u2014"])
def test_success_tables_have_zero_blank_or_placeholder_cells(placeholder: str) -> None:
    text = corrupt_first_success_cell(render_complete_workbook(), placeholder)
    with pytest.raises(ValueError, match="successful table"):
        validate_comparison_workbook_text(text, complete_release())


def test_final_write_is_fail_closed_for_existing_target(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    source = tmp_path / "source.md"
    source.write_text(SOURCE, encoding="utf-8")
    target = tmp_path / "workbook.md"
    target.write_text("known-good\n", encoding="utf-8")
    monkeypatch.setattr(finalizer, "reconcile_comparison_release", lambda _: incomplete_release())

    with pytest.raises(ValueError):
        finalize_release(
            tmp_path / "release.json",
            source_workbook=source,
            target=target,
            require_complete=True,
        )

    assert target.read_text(encoding="utf-8") == "known-good\n"
    assert list(tmp_path.glob(".workbook.md.*.tmp")) == []


def test_final_write_is_fail_closed_for_missing_target(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    source = tmp_path / "source.md"
    source.write_text(SOURCE, encoding="utf-8")
    target = tmp_path / "missing.md"
    monkeypatch.setattr(finalizer, "reconcile_comparison_release", lambda _: incomplete_release())

    with pytest.raises(ValueError):
        finalize_release(
            tmp_path / "release.json",
            source_workbook=source,
            target=target,
            require_complete=True,
        )

    assert not target.exists()
    assert list(tmp_path.glob(".missing.md.*.tmp")) == []


def test_validation_error_never_replaces_target_or_leaves_temp_file(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    source = tmp_path / "source.md"
    source.write_text(SOURCE, encoding="utf-8")
    target = tmp_path / "workbook.md"
    target.write_text("known-good\n", encoding="utf-8")
    monkeypatch.setattr(finalizer, "reconcile_comparison_release", lambda _: complete_release())
    monkeypatch.setattr(
        finalizer,
        "validate_comparison_workbook_text",
        lambda *_: (_ for _ in ()).throw(ValueError("synthetic validation failure")),
    )

    with pytest.raises(ValueError, match="synthetic validation failure"):
        finalize_release(
            tmp_path / "release.json",
            source_workbook=source,
            target=target,
            require_complete=False,
        )

    assert target.read_text(encoding="utf-8") == "known-good\n"
    assert list(tmp_path.glob(".workbook.md.*.tmp")) == []


def test_successful_finalize_writes_validated_content_atomically(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    source = tmp_path / "source.md"
    source.write_text(SOURCE, encoding="utf-8")
    target = tmp_path / "workbook.md"
    monkeypatch.setattr(finalizer, "reconcile_comparison_release", lambda _: complete_release())

    result = finalize_release(
        tmp_path / "release.json",
        source_workbook=source,
        target=target,
        require_complete=False,
    )

    assert result == {
        "path": str(target),
        "sha256": hashlib.sha256(target.read_bytes()).hexdigest(),
    }
    validate_comparison_workbook_text(target.read_text(encoding="utf-8"), complete_release())
    assert list(tmp_path.glob(".workbook.md.*.tmp")) == []


def test_cache_comparison_uses_only_shared_context_and_same_u8_artifact() -> None:
    section = _section(
        render_complete_workbook(),
        "U8 STANDARD, TBQ4 and TBQ3 shared-context comparison",
    )
    assert "| OV-12 | 512 | U8 STANDARD |" in section
    assert "| OV-TQ-21 | 512 | TBQ4 |" in section
    assert "| OV-TQ-22 | 512 | TBQ3 |" in section
    assert "| OV-TQ-22 | 1024 |" not in section
    assert section.count("a" * 64) == 3


def test_standard_comparison_uses_observed_activation_not_weight_precision() -> None:
    section = _section(
        render_complete_workbook(), "U4, U8 and FP16 STANDARD deployment comparison"
    )
    assert "| OV-11 | 512 | U4 STANDARD | CPU STANDARD; observed f32/f32 state |" in section
    assert "| OV-12 | 512 | U8 STANDARD | CPU STANDARD; observed f32/f32 state |" in section
    assert "| OV-13 | 512 | FP16 STANDARD | CPU STANDARD; observed f32/f32 state |" in section
    assert "observed u4/u4 state" not in section
    assert "observed u8/u8 state" not in section


@pytest.mark.parametrize(
    ("title", "expected_samples"),
    [
        ("Load ms", "2 | 3 | 5"),
        ("TTFT ms", "3 | 4 | 6"),
        ("Prompt tok/s", "4 | 5 | 7"),
        ("TPOT ms", "5 | 6 | 8"),
        ("Decode tok/s", "6 | 7 | 9"),
        ("Generation ms", "7 | 8 | 10"),
    ],
)
def test_each_timing_metric_has_a_separate_three_sample_mean_median_table(
    title: str, expected_samples: str
) -> None:
    section = _section(render_complete_workbook(), "Complete timing results")
    assert section.count(f"**{title}**") == 1
    assert section.count("| Test ID | Context | S1 | S2 | S3 | Mean | Median |") == 6
    assert f"| OV-11 | 512 | {expected_samples} |" in section


def test_resource_tables_have_exact_complete_columns() -> None:
    section = _section(render_complete_workbook(), "Complete memory and CPU/GPU results")
    assert "| Test ID | Context | WS S1 MiB | WS S2 MiB | WS S3 MiB | WS median MiB | WS worst MiB | Private median MiB | Private worst MiB |" in section
    assert "| Test ID | Context | Available RAM minimum MiB | KV MiB | GPU memory peak MiB |" in section
    assert "| Test ID | Context | CPU mean % | CPU median % | CPU peak % | CPU samples | GPU mean % | GPU median % | GPU peak % | GPU samples |" in section
    assert "| OV-11 | 512 | 102 | 104 | 103 | 103 | 104 | 83 | 84 |" in section
    assert "| OV-11 | 512 | 500 | 13 | 5 |" in section
    assert "| OV-11 | 512 | 26 | 26 | 41 | 4 | 4 | 4 | 6 | 3 |" in section


def test_quality_table_has_exact_numeric_p1_p6_aggregates_and_no_runtime_only_row() -> None:
    section = _section(render_complete_workbook(), "P1\u2013P6 and aggregate quality results")
    assert "| Test ID | Context | P1 | P2 | P3 | P4 | P5 | P6 | Mean | Median | Minimum | Maximum | Quality evidence |" in section
    assert "| OV-TQ-21 | 512 | 9 | 9.1 | 9.2 | 9.3 | 9.4 | 9.5 | 9.25 | 9.25 | 9 | 9.5 |" in section
    assert "| OV-TQ-22 | 1024 |" not in section


def test_boundaries_keep_runtime_comparable_and_blocked_contexts_distinct() -> None:
    section = _section(
        render_complete_workbook(),
        "Laptop runtime-capable and fully-comparable boundaries",
    )
    assert "| OV-11 | 2048 | 1024 | 4096 | runtime-boundary |" in section


def test_terminal_rows_are_compact_and_never_fabricate_success_metrics() -> None:
    section = _section(
        render_complete_workbook(), "Terminal attempts and hash-bound evidence"
    )
    assert "| Identity/context | Stage | Principal reason | Evidence |" in section
    assert "| OV-11/4096 | runtime-boundary | confirmed laptop RAM boundary |" in section
    quality_terminal_sha = hashlib.sha256(b"quality-terminal/OV-11/2048").hexdigest()
    assert (
        "| OV-11/2048 | quality-capture | governed P1-P6 capture reached laptop RAM floor "
        "| experiments/synthetic/quality/OV-11-2048-terminal.json#sha256="
        f"{quality_terminal_sha} |"
    ) in section
    terminal_table = section.split("**Hash-bound evidence**", 1)[0]
    for forbidden in (
        "Load ms",
        "TTFT ms",
        "Peak",
        "CPU mean",
        "GPU mean",
        "| P1 |",
    ):
        assert forbidden not in terminal_table


def test_terminal_cells_preserve_multiline_whitespace_and_commonmark_safely() -> None:
    release = complete_release()
    runtime_key = ComparisonKey("OV-11", 4096)
    runtime_terminals = {
        runtime_key: {
            **release.terminals[runtime_key],
            "principal_reason": " \tRAM | <low> \r\n **[x](url)**\\tail ",
            "evidence_path": Path(
                "experiments/synthetic/terminal/[x]`route`#file.json"
            ),
        }
    }
    quality_key = ComparisonKey("OV-11", 2048)
    quality = dict(release.quality)
    quality[quality_key] = replace(
        quality[quality_key],
        principal_reason="\tquality `capture` \n RAM_low [x](url) \t",
        evidence_path=Path(
            "experiments/synthetic/quality/[x]`capture`#file.json"
        ),
    )
    release = replace(release, terminals=runtime_terminals, quality=quality)

    text = render_v19_workbook(SOURCE, release)
    terminal = _section(text, "Terminal attempts and hash-bound evidence")

    assert "&#32;&#9;RAM &#124; &lt;low&gt;&#32;<br>&#32;" in terminal
    assert "&#42;&#42;&#91;x&#93;&#40;url&#41;&#42;&#42;&#92;tail&#32;" in terminal
    assert "&#9;quality &#96;capture&#96;&#32;<br>&#32;RAM&#95;low" in terminal
    assert "&#91;x&#93;&#96;route&#96;#file.json#sha256=" in terminal
    assert "&#91;x&#93;&#96;capture&#96;#file.json#sha256=" in terminal
    validate_comparison_workbook_text(text, release)


def test_overall_winner_requires_shared_complete_numeric_quality() -> None:
    release = incomplete_release()
    text = render_v19_workbook(SOURCE, release)
    assert "Overall winner:" not in text
    injected = text.replace(
        "<!-- END WB-04 V1.9 COMPARISON -->",
        "Overall winner: **OV-TQ-21**\n\n<!-- END WB-04 V1.9 COMPARISON -->",
    )
    with pytest.raises(ValueError, match="overall winner"):
        validate_comparison_workbook_text(injected, release)


def test_overall_winner_outside_generated_body_is_also_rejected() -> None:
    release = incomplete_release()
    text = render_v19_workbook(SOURCE, release)
    injected = text + "\nOverall winner: **OV-TQ-21**\n"

    with pytest.raises(ValueError, match="overall winner"):
        validate_comparison_workbook_text(injected, release)


def test_validator_rejects_unauthorized_table_before_first_generated_heading() -> None:
    release = complete_release()
    text = render_v19_workbook(SOURCE, release)
    injected = text.replace(
        "<!-- BEGIN WB-04 V1.9 COMPARISON -->",
        "<!-- BEGIN WB-04 V1.9 COMPARISON -->\n\n"
        "| Test ID | Context | Fake success |\n"
        "| --- | --- | --- |\n"
        "| OV-99 | 512 | fabricated |",
        1,
    )

    with pytest.raises(ValueError, match="comparison envelope"):
        validate_comparison_workbook_text(injected, release)


def test_validator_rejects_unauthorized_table_after_end_marker() -> None:
    release = complete_release()
    text = render_v19_workbook(SOURCE, release)
    injected = text + (
        "\n| Identity/context | Stage | Principal reason | Evidence |\n"
        "| --- | --- | --- | --- |\n"
        "| OV-99/512 | passed | fabricated | fake.json#sha256="
        + "f" * 64
        + " |\n"
    )

    with pytest.raises(ValueError, match="comparison envelope"):
        validate_comparison_workbook_text(injected, release)


@pytest.mark.parametrize(
    "mutation",
    [
        lambda text: text.replace(
            "# 04 Official OpenVINO Controlled Retest Workbook v1.9",
            "# 04 Official OpenVINO Controlled Retest Workbook v9.9",
            1,
        ),
        lambda text: text.replace(
            "Controlled retest revision 1.9 (WR-037).",
            "Controlled retest revision 9.9 (WR-999).",
            1,
        ),
        lambda text: text.replace(
            "Workbook version: 1.9",
            "Workbook version: 1.9\nWorkbook version: 9.9",
            1,
        ),
    ],
)
def test_validator_requires_exact_title_and_controlled_revision(mutation: Any) -> None:
    release = complete_release()
    text = render_v19_workbook(SOURCE, release)
    changed = mutation(text)
    assert changed != text

    with pytest.raises(ValueError, match="workbook identity"):
        validate_comparison_workbook_text(changed, release)


@pytest.mark.parametrize(
    ("name", "mutate", "message"),
    [
        ("sample", lambda text: text.replace("| OV-11 | 512 | 2 | 3 | 5 |", "| OV-11 | 512 | 2 | 3 | 6 |", 1), "timing table"),
        ("aggregate", lambda text: text.replace("| OV-11 | 512 | 2 | 3 | 5 | 3.333333 | 3 |", "| OV-11 | 512 | 2 | 3 | 5 | 9 | 3 |", 1), "timing table"),
        ("unit", lambda text: text.replace("**Load ms**", "**Load seconds**", 1), "timing table"),
        ("sample count", lambda text: text.replace("| OV-11 | 512 | 26 | 26 | 41 | 4 | 4 | 4 | 6 | 3 |", "| OV-11 | 512 | 26 | 26 | 41 | 99 | 4 | 4 | 6 | 3 |", 1), "utilisation table"),
        ("activation", lambda text: text.replace("CPU STANDARD; observed f32/f32 state", "CPU STANDARD; observed u8/u8 state", 1), "successful table"),
        ("evidence hash", lambda text: text.replace("#sha256=", "#sha256=" + "f", 1), "evidence"),
        ("boundary", lambda text: text.replace("| OV-11 | 2048 | 1024 | 4096 |", "| OV-11 | 4096 | 1024 | 4096 |", 1), "boundary table"),
        ("duplicate row", lambda text: _duplicate_first_row(text, "| OV-12 | 512 | U8 STANDARD |"), "successful table"),
        ("missing row", lambda text: text.replace(next(line for line in text.splitlines() if line.startswith("| OV-13 | 512 | FP16 STANDARD |")) + "\n", "", 1), "successful table"),
        ("unexpected row", lambda text: text.replace("| --- | --- | --- | --- | --- | --- |", "| --- | --- | --- | --- | --- | --- |\n| OV-99 | 512 | U8 STANDARD | CPU STANDARD; observed f32/f32 state | " + "f" * 64 + " | synthetic#sha256=" + "e" * 64 + " |", 1), "successful table"),
    ],
)
def test_exact_cell_validation_rejects_adversarial_changes(
    name: str, mutate: Any, message: str
) -> None:
    original = render_complete_workbook()
    changed = mutate(original)
    assert changed != original, name
    with pytest.raises(ValueError, match=message):
        validate_comparison_workbook_text(changed, complete_release())


def test_validation_rejects_duplicate_or_missing_sections_and_malformed_markdown() -> None:
    text = render_complete_workbook()
    duplicate = text.replace(
        "# 5. U8 STANDARD, TBQ4 and TBQ3 shared-context comparison",
        "# 5. U8 STANDARD, TBQ4 and TBQ3 shared-context comparison\n\n# 5. U8 STANDARD, TBQ4 and TBQ3 shared-context comparison",
        1,
    )
    missing = text.replace(
        "# 6. U4, U8 and FP16 STANDARD deployment comparison",
        "# 6. removed comparison section",
        1,
    )
    malformed = text.replace("| --- | --- | --- | --- | --- | --- |", "| --- | --- | broken |", 1)

    with pytest.raises(ValueError, match="section"):
        validate_comparison_workbook_text(duplicate, complete_release())
    with pytest.raises(ValueError, match="section"):
        validate_comparison_workbook_text(missing, complete_release())
    with pytest.raises(ValueError, match="Markdown"):
        validate_comparison_workbook_text(malformed, complete_release())
